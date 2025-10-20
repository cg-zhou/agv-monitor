using AGV.Monitor.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AGV.Monitor.Services;

public class SchedulerService
{
    public SchedulerService(AgvContext context)
    {
        this.context = context;
        Timestamp = 0;
    }

    private AgvContext context;
    public int Timestamp { get; private set; }

    public void ProcessToComplete()
    {
        while (!context.AllTasksCompleted)
        {
            Process();
        }
    }

    public void Process()
    {
        // 如果所有任务都完成，不进行任何命令，计时停止
        if (context.AllTasksCompleted)
        {
            return;
        }

        // 如果当前时间，超过最大限制秒数，计时停止，防止死锁
        const int maxTimestamp = 400;
        if (Timestamp > maxTimestamp)
        {
            throw new Exception($"Failed to complete all tasks after {maxTimestamp}s");
        }

        ++Timestamp;

        var agvs = context.Agvs;
        var handledAgvs = new List<Agv>();

        // 处理卸载货物
        foreach (var agv in agvs.Except(handledAgvs).Where(x => x.CanUnload()))
        {
            agv.Unload(Timestamp);
            handledAgvs.Add(agv);
        }

        // 处理装载货物
        var pendingLoadTasks = context.GetSortedPendingTasks();
        foreach (var agv in agvs.Except(handledAgvs).Where(x => !x.IsLoaded))
        {
            var pendingLoadTask = pendingLoadTasks.FirstOrDefault(x => x.PickupPosition == agv.Position);
            if (pendingLoadTask != null)
            {
                agv.Load(pendingLoadTask, Timestamp);
                handledAgvs.Add(agv);
            }
        }

        var movedItems = new List<Tuple<Agv, PointEx>>();

        // 已经装载货物的 AGV 的移动
        BatchMoveAgvs(agvs, handledAgvs, true, null);

        // 已经装载货物的 AGV 的转向
        foreach (var agv in agvs.Except(handledAgvs).Where(x => x.IsLoaded))
        {
            if (agv.ShouldTurn())
            {
                agv.Turn();
                handledAgvs.Add(agv);
            }
        }

        var tempAssignments = new Dictionary<Agv, TaskEx>();
        var pendingTasks = context.GetSortedPendingTasks().ToList();
        var idleAgvs = agvs.Except(handledAgvs).Where(x => !x.IsLoaded)
            .ToList();

        // 任务排序，分配给空闲 AGV
        foreach (var task in pendingTasks)
        {
            if (!idleAgvs.Any())
            {
                break;
            }

            var list = new List<Tuple<Agv, List<PathTimePoint>>>();
            foreach (var agv in idleAgvs)
            {
                var obstacles = GetObstacles(agv, agvs);
                var path = CalculatePathToPickUpPosition(agv, task, obstacles);
                var pathTimePoints = PathPlanning.CalculatePathTiming(path, agv.Pitch);
                list.Add(Tuple.Create(agv, pathTimePoints));
            }

            // 找出最近的 AGV
            var tuple = list.OrderBy(x => x.Item2.LastOrDefault()?.TimeCost ?? int.MaxValue).FirstOrDefault();
            if (tuple != null)
            {
                var selectedAgv = tuple.Item1;
                idleAgvs.Remove(selectedAgv);

                selectedAgv.PathTimePoints = tuple.Item2;
                tempAssignments[selectedAgv] = task;
            }
        }

        var turnAgvs = tempAssignments.Keys.Where(x => x.ShouldTurn()).ToArray();
        var moveAgvs = tempAssignments.Keys.Where(x => x.ShouldMove()).ToArray();

        // 空闲 AGV 转向
        foreach (var assignment in turnAgvs)
        {
            assignment.Turn();
        }

        // 空闲 AGV 移动
        BatchMoveAgvs(moveAgvs, handledAgvs, false, tempAssignments);

        // 当待处理的任务，都完成后，将空闲 AGV 移动到地图边缘，防止死锁
        if (!pendingTasks.Any())
        {
            foreach (var idleAgv in agvs.Except(handledAgvs))
            {
                var additionalObstacles = GetObstacles(idleAgv, agvs);
                var obstacles = BuildObstacles(additionalObstacles);

                // 找到距离地图边缘最近的坐标
                var x = idleAgv.Position.X;
                var y = idleAgv.Position.Y;

                var points = new List<PointEx>();
                var loadedAgvs = agvs.Where(x => x.IsLoaded).ToArray();
                if (!loadedAgvs.Any(agv => agv.Position.X == x && agv.Position.Y > y))
                {
                    points.Add(new PointEx(x, 20));
                }
                if (!loadedAgvs.Any(agv => agv.Position.X == x && agv.Position.Y < y))
                {
                    points.Add(new PointEx(x, 1));
                }
                if (!loadedAgvs.Any(agv => agv.Position.X > x && agv.Position.Y == y))
                {
                    points.Add(new PointEx(20, y));
                }
                if (!loadedAgvs.Any(agv => agv.Position.X < x && agv.Position.Y == y))
                {
                    points.Add(new PointEx(1, y));
                }

                if (points.Any())
                {
                    var goal = points.OrderBy(point => Math.Abs(point.X - idleAgv.Position.X) + Math.Abs(point.Y - idleAgv.Position.Y))
                        .FirstOrDefault();

                    var path = PathPlanning.AStarWithOrientation(idleAgv.Position, goal, idleAgv.Pitch, obstacles);
                    var pathTimePoints = PathPlanning.CalculatePathTiming(path, idleAgv.Pitch);
                    idleAgv.PathTimePoints = pathTimePoints;
                    if (idleAgv.ShouldMove())
                    {
                        idleAgv.Move();
                    }
                    else if (idleAgv.ShouldTurn())
                    {
                        idleAgv.Turn();
                    }

                }
            }
        }

        context.TrajectoryRecorder.Add(Timestamp);
    }

    private void BatchMoveAgvs(Agv[] agvs, List<Agv> handledAgvs, bool isLoaded, Dictionary<Agv, TaskEx> tempAssignments)
    {
        var movedItems = new List<Tuple<Agv, PointEx, TaskEx>>();
        while (true)
        {
            var isAssigned = false;

            foreach (var agv in agvs.Except(handledAgvs).Where(x => x.IsLoaded == isLoaded))
            {
                if (handledAgvs.Contains(agv))
                {
                    break;
                }

                var currentAgvTask = isLoaded ? agv.LoadedTask : tempAssignments[agv];
                var obstacles = GetObstacles(agv, context.Agvs);
                var path = isLoaded
                    ? CalculatePathToEndPoint(agv, currentAgvTask, obstacles)
                    : CalculatePathToPickUpPosition(agv, currentAgvTask, obstacles);
                var pathTimePoints = PathPlanning.CalculatePathTiming(path, agv.Pitch);
                agv.PathTimePoints = pathTimePoints;

                if (pathTimePoints.Count < 2)
                {
                    continue;
                }

                var expectMovePitch = pathTimePoints[0].Position.GetPitchToNeighbour(pathTimePoints[1].Position);
                if (expectMovePitch != agv.Pitch)
                {
                    continue;
                }

                if (movedItems.Any(item => item.Item1.Pitch == agv.Pitch
                    // 沿 X 轴移动，当前 AGV 在之前移动的 AGV 下方
                    && (agv.Pitch == Direction.Left || agv.Pitch == Direction.Right)
                    && item.Item2.X == agv.Position.X
                    && item.Item2.Y == agv.Position.Y + 1
                    && currentAgvTask.EndPosition.Y > agv.Position.Y
                    && item.Item3.EndPosition.Y <= item.Item1.Position.Y))
                {
                    handledAgvs.Add(agv);
                    agv.Turn(Direction.Up);
                    agv.PathTimePoints = new List<PathTimePoint>();
                    isAssigned = true;
                    continue;

                }

                if (movedItems.Any(item => item.Item1.Pitch == agv.Pitch
                    // 沿 X 轴移动，当前 AGV 在之前移动的 AGV 上方
                    && (agv.Pitch == Direction.Left || agv.Pitch == Direction.Right)
                    && item.Item2.X == agv.Position.X
                    && item.Item2.Y == agv.Position.Y - 1
                    && currentAgvTask.EndPosition.Y < agv.Position.Y
                    && item.Item3.EndPosition.Y >= item.Item1.Position.Y))
                {
                    handledAgvs.Add(agv);
                    agv.Turn(Direction.Down);
                    agv.PathTimePoints = new List<PathTimePoint>();
                    isAssigned = true;
                    continue;
                }

                if (movedItems.Any(item => item.Item1.Pitch == agv.Pitch
                    // 沿 Y 轴移动，当前 AGV 在之前移动的 AGV 右方
                    && (agv.Pitch == Direction.Up || agv.Pitch == Direction.Down)
                    && item.Item2.Y == agv.Position.Y
                    && item.Item2.X == agv.Position.X - 1
                    && currentAgvTask.EndPosition.X < agv.Position.X
                    && item.Item3.EndPosition.X >= item.Item1.Position.X))
                {
                    handledAgvs.Add(agv);
                    agv.Turn(Direction.Left);
                    agv.PathTimePoints = new List<PathTimePoint>();
                    isAssigned = true;
                    continue;
                }


                if (movedItems.Any(item => item.Item1.Pitch == agv.Pitch
                    // 沿 Y 轴移动，当前 AGV 在之前移动的 AGV 左方
                    && (agv.Pitch == Direction.Up || agv.Pitch == Direction.Down)
                    && item.Item2.Y == agv.Position.Y
                    && item.Item2.X == agv.Position.X + 1
                    && currentAgvTask.EndPosition.X > agv.Position.X
                    && item.Item3.EndPosition.X <= item.Item1.Position.X))
                {
                    handledAgvs.Add(agv);
                    agv.Turn(Direction.Right);
                    agv.PathTimePoints = new List<PathTimePoint>();
                    isAssigned = true;
                    continue;
                }

                movedItems.Add(Tuple.Create(agv, agv.Position, currentAgvTask));

                handledAgvs.Add(agv);
                agv.Move();
                isAssigned = true;
            }

            if (!isAssigned)
            {
                break;
            }
        }
    }

    /// <summary>
    /// 计算AGV到任务的路径
    /// </summary>
    private List<PointEx> CalculatePathToPickUpPosition(Agv agv, TaskEx task, PointEx[] additionalObstacles)
    {
        var obstacles = BuildObstacles(additionalObstacles);
        var goal = task.PickupPosition;
        return PathPlanning.AStarWithOrientation(agv.Position, goal, agv.Pitch, obstacles);
    }

    private List<PointEx> CalculatePathToEndPoint(Agv agv, TaskEx task, PointEx[] additionalObstacles)
    {
        var obstacles = BuildObstacles(additionalObstacles);
        var goal = task.EndPosition;
        obstacles.Remove(goal);
        return PathPlanning.AStarWithOrientation(agv.Position, goal, agv.Pitch, obstacles);
    }

    private HashSet<PointEx> BuildObstacles(PointEx[] additionalObstacles)
    {
        // 构建障碍物集合（起点和终点位置）
        var obstacles = context.FixedMapObstacles.ToHashSet();
        if (additionalObstacles != null)
        {
            foreach (var additionalObstacle in additionalObstacles)
            {
                obstacles.Add(additionalObstacle);
            }
        }
        return obstacles;
    }

    private PointEx[] GetObstacles(Agv agv, Agv[] agvs)
    {
        // 所有 AGV 的位置
        var agvPositions = agvs.Select(x => x.Position).ToArray();

        // 当前 AGV 的邻接点，如果有 AGV 存在，则作为障碍点
        var obstacles = agv.Position.Neighbours.Where(agvPositions.Contains).ToList();

        // 将可能出现 “十字互锁” 的点，加入到障碍点，十字互锁的示例：
        // 其中 □ 为取货点；○ 和 箭头表示 AGV
        //   ↓
        // → ○ □
        //   ↑
        // 十字互锁的条件：
        // 1. 一个 AGV #n 周围四个临接点中，有三个障碍点（障碍点是固定障碍，或者其它 AGV #m）
        // 2. 把 AGV #n 的第四个邻接点，作为当前 AGV 移动的障碍点，防止十字互锁
        foreach (var agvItem in agvs.Except([agv]))
        {
            var neighbours = agvItem.Position.Neighbours.ToArray();
            neighbours = neighbours.Except(context.FixedMapObstacles).ToArray();

            foreach (var neighbourAgv in agvs.Except([agvItem])
                .Where(x => x.Position.IsNeighbour(agvItem.Position)))
            {
                neighbours = neighbours.Except([neighbourAgv.Position]).ToArray();
            }

            if (neighbours.Length == 1
                && agv.Position.Neighbours.Contains(neighbours.First()))
            {
                obstacles.Add(neighbours.First());
            }
        }

        return obstacles.ToArray();
    }
}
