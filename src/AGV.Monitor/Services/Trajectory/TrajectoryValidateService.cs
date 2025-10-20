using AGV.Monitor.Geometry;
using AGV.Monitor.Parsers;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AGV.Monitor.Services.Trajectory;

/// <summary>
/// AGV轨迹校验器
/// </summary>
public class TrajectoryValidateService
{
    private readonly MapData mapData;
    private readonly List<TaskRecord> tasks;
    private readonly List<ValidationResult> results = new List<ValidationResult>();

    // 地图边界
    private const int MAP_MIN = 1;
    private const int MAP_MAX = 20;

    public TrajectoryValidateService(MapData mapData, List<TaskRecord> tasks)
    {
        this.mapData = mapData;
        this.tasks = tasks;
    }

    /// <summary>
    /// 校验轨迹记录
    /// </summary>
    /// <param name="trajectories">轨迹记录列表</param>
    /// <returns>校验结果列表</returns>
    public List<ValidationResult> ValidateTrajectories(List<TrajectoryRecord> trajectories)
    {
        results.Clear();

        // 按时间戳排序
        var sortedTrajectories = trajectories.OrderBy(t => t.Timestamp).ToList();

        // 按AGV分组
        var agvTrajectories = sortedTrajectories.GroupBy(t => t.Name)
            .ToDictionary(g => g.Key, g => g.OrderBy(t => t.Timestamp).ToList());

        // 检验所有元素，不超出地图边界
        ValidateMapBoundaries(sortedTrajectories);

        // 检验移动和旋转，是否符合规则
        ValidateMovementRules(agvTrajectories);

        // 检验 AGV 是否发生碰撞
        ValidateCollisions(sortedTrajectories);

        // 检验取送货位置是否正确
        ValidatePickupAndDelivery(agvTrajectories);

        // 验证取货顺序是否正确
        ValidateTaskSequence(agvTrajectories);

        return results;
    }

    /// <summary>
    /// 校验地图边界
    /// </summary>
    private void ValidateMapBoundaries(List<TrajectoryRecord> trajectories)
    {
        foreach (var record in trajectories)
        {
            if (record.X < MAP_MIN || record.X > MAP_MAX || record.Y < MAP_MIN || record.Y > MAP_MAX)
            {
                results.Add(new ValidationResult
                {
                    IsValid = false,
                    Message = $"AGV {record.Name} 超出地图边界 ({record.X}, {record.Y})",
                    Timestamp = record.Timestamp,
                    AgvName = record.Name
                });
            }
        }
    }

    /// <summary>
    /// 校验移动规则
    /// </summary>
    private void ValidateMovementRules(Dictionary<string, List<TrajectoryRecord>> agvTrajectories)
    {
        foreach (var agvPair in agvTrajectories)
        {
            var agvName = agvPair.Key;
            var trajectory = agvPair.Value;

            for (int i = 1; i < trajectory.Count; i++)
            {
                var prev = trajectory[i - 1];
                var curr = trajectory[i];

                // 检查移动距离（每秒最多移动一格）
                var distance = Math.Abs(curr.X - prev.X) + Math.Abs(curr.Y - prev.Y);
                var timeDiff = curr.Timestamp - prev.Timestamp;

                if (distance > timeDiff)
                {
                    results.Add(new ValidationResult
                    {
                        IsValid = false,
                        Message = $"AGV {agvName} 移动速度过快: 在 {timeDiff}s 内移动了 {distance} 格",
                        Timestamp = curr.Timestamp,
                        AgvName = agvName
                    });
                }

                // 检查斜向移动
                if (curr.X != prev.X && curr.Y != prev.Y && distance > 0)
                {
                    results.Add(new ValidationResult
                    {
                        IsValid = false,
                        Message = $"AGV {agvName} 斜向移动: 从 ({prev.X}, {prev.Y}) 到 ({curr.X}, {curr.Y})",
                        Timestamp = curr.Timestamp,
                        AgvName = agvName
                    });
                }

                var prevPosition = new PointEx(prev.X, prev.Y);
                var currPosition = new PointEx(curr.X, curr.Y);
                if (prevPosition != currPosition)
                {
                    if (prevPosition.GetPitchToNeighbour(currPosition) != prev.Pitch)
                    {
                        results.Add(new ValidationResult
                        {
                            IsValid = false,
                            Message = $"AGV 在移动时转向: 从 ({prev.X}, {prev.Y}) 到 ({curr.X}, {curr.Y})",
                            Timestamp = curr.Timestamp,
                            AgvName = agvName
                        });
                    }
                }

                // 检查转向规则
                ValidateRotation(prev, curr, agvName);
            }
        }
    }

    /// <summary>
    /// 校验转向规则
    /// </summary>
    private void ValidateRotation(TrajectoryRecord prev, TrajectoryRecord curr, string agvName)
    {
        if (prev.Pitch != curr.Pitch)
        {
            var angleDiff = Math.Abs(curr.Pitch - prev.Pitch);
            // 处理角度跨越360度的情况
            if (angleDiff > 180)
                angleDiff = 360 - angleDiff;

            // 只允许90度和180度转向
            if (angleDiff != 90 && angleDiff != 180)
            {
                results.Add(new ValidationResult
                {
                    IsValid = false,
                    Message = $"AGV {agvName} 非法转向: {prev.Pitch}° 到 {curr.Pitch}° (差值: {angleDiff}°)",
                    Timestamp = curr.Timestamp,
                    AgvName = agvName
                });
            }

            // 检查取放料时是否转向
            if (prev.Loaded != curr.Loaded)
            {
                results.Add(new ValidationResult
                {
                    IsValid = false,
                    Message = $"AGV {agvName} 在取料、放料的同时转向",
                    Timestamp = curr.Timestamp,
                    AgvName = agvName
                });
            }
        }
    }

    /// <summary>
    /// 校验碰撞
    /// </summary>
    private void ValidateCollisions(List<TrajectoryRecord> trajectories)
    {
        var timeGroups = trajectories.GroupBy(t => t.Timestamp);

        foreach (var timeGroup in timeGroups)
        {
            var recordsAtTime = timeGroup.ToList();

            // 检查同一时刻同一位置
            var positionGroups = recordsAtTime.GroupBy(r => new { r.X, r.Y });
            foreach (var posGroup in positionGroups)
            {
                var agvsAtPosition = posGroup.ToList();
                if (agvsAtPosition.Count > 1)
                {
                    var agvNames = string.Join(", ", agvsAtPosition.Select(a => a.Name));
                    results.Add(new ValidationResult
                    {
                        IsValid = false,
                        Message = $"碰撞: AGV {agvNames} 在时刻 {timeGroup.Key} 同时出现在位置 ({posGroup.Key.X}, {posGroup.Key.Y})",
                        Timestamp = timeGroup.Key
                    });
                }
            }
        }

        // 检查相向运动交换位置
        ValidatePositionExchange(trajectories);
    }

    /// <summary>
    /// 校验位置交换
    /// </summary>
    private void ValidatePositionExchange(List<TrajectoryRecord> trajectories)
    {
        var agvTrajectories = trajectories.GroupBy(t => t.Name)
            .ToDictionary(g => g.Key, g => g.OrderBy(t => t.Timestamp).ToList());

        var agvNames = agvTrajectories.Keys.ToList();

        for (int i = 0; i < agvNames.Count; i++)
        {
            for (int j = i + 1; j < agvNames.Count; j++)
            {
                var agv1 = agvNames[i];
                var agv2 = agvNames[j];
                var traj1 = agvTrajectories[agv1];
                var traj2 = agvTrajectories[agv2];

                CheckPositionExchangeBetweenAgvs(agv1, traj1, agv2, traj2);
            }
        }
    }

    /// <summary>
    /// 检查两个AGV之间的位置交换
    /// </summary>
    private void CheckPositionExchangeBetweenAgvs(string agv1, List<TrajectoryRecord> traj1,
        string agv2, List<TrajectoryRecord> traj2)
    {
        for (int i = 1; i < traj1.Count; i++)
        {
            var agv1Prev = traj1[i - 1];
            var agv1Curr = traj1[i];

            // 找到agv2在相同时间段的记录
            var agv2Prev = traj2.FirstOrDefault(t => t.Timestamp == agv1Prev.Timestamp);
            var agv2Curr = traj2.FirstOrDefault(t => t.Timestamp == agv1Curr.Timestamp);

            if (agv2Prev != null && agv2Curr != null)
            {
                // 检查是否交换位置
                if (agv1Prev.X == agv2Curr.X && agv1Prev.Y == agv2Curr.Y &&
                    agv1Curr.X == agv2Prev.X && agv1Curr.Y == agv2Prev.Y)
                {
                    results.Add(new ValidationResult
                    {
                        IsValid = false,
                        Message = $"位置交换碰撞: AGV {agv1} 和 {agv2} 在时刻 {agv1Prev.Timestamp}-{agv1Curr.Timestamp} 交换位置",
                        Timestamp = agv1Curr.Timestamp
                    });
                }
            }
        }
    }

    /// <summary>
    /// 校验取货和送货规则
    /// </summary>
    private void ValidatePickupAndDelivery(Dictionary<string, List<TrajectoryRecord>> agvTrajectories)
    {
        foreach (var agvPair in agvTrajectories)
        {
            var agvName = agvPair.Key;
            var trajectory = agvPair.Value;

            for (int i = 1; i < trajectory.Count; i++)
            {
                var prev = trajectory[i - 1];
                var curr = trajectory[i];

                // 检查取货
                if (!prev.Loaded && curr.Loaded)
                {
                    ValidatePickup(agvName, curr);
                }

                // 检查送货
                if (prev.Loaded && !curr.Loaded)
                {
                    ValidateDelivery(agvName, curr);
                }

                // 检查一次只能运输一个快件
                if (curr.Loaded && !string.IsNullOrEmpty(curr.Destination))
                {
                    // AGV已装载货物时不能再取货
                    if (i + 1 < trajectory.Count)
                    {
                        var next = trajectory[i + 1];
                        if (!curr.Loaded && next.Loaded && curr.Destination == next.Destination)
                        {
                            results.Add(new ValidationResult
                            {
                                IsValid = false,
                                Message = $"AGV {agvName} 同时运输多个快件",
                                Timestamp = next.Timestamp,
                                AgvName = agvName
                            });
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// 校验取货操作
    /// </summary>
    private void ValidatePickup(string agvName, TrajectoryRecord record)
    {
        // 检查取货位置是否正确
        bool validPickupLocation = false;

        foreach (var startPoint in mapData.StartPoints)
        {
            var pickupLocation = mapData.PickupRules[startPoint.Key];
            if (record.X == pickupLocation.X && record.Y == pickupLocation.Y)
            {
                validPickupLocation = true;
                break;
            }
        }

        if (!validPickupLocation)
        {
            results.Add(new ValidationResult
            {
                IsValid = false,
                Message = $"AGV {agvName} 在非法位置取货: ({record.X}, {record.Y})",
                Timestamp = record.Timestamp,
                AgvName = agvName
            });
        }
    }

    /// <summary>
    /// 校验送货操作
    /// </summary>
    private void ValidateDelivery(string agvName, TrajectoryRecord record)
    {
        if (string.IsNullOrEmpty(record.Destination))
            return;

        // 检查送货位置是否在目的地的卸料点
        if (mapData.EndPoints.TryGetValue(record.Destination, out var endPoint))
        {
            var validUnloadPoints = GetUnloadPoints(endPoint.X, endPoint.Y);
            bool validUnloadLocation = validUnloadPoints.Any(p => p.X == record.X && p.Y == record.Y);

            if (!validUnloadLocation)
            {
                results.Add(new ValidationResult
                {
                    IsValid = false,
                    Message = $"AGV {agvName} 在非法位置卸货: ({record.X}, {record.Y}), 目的地: {record.Destination}",
                    Timestamp = record.Timestamp,
                    AgvName = agvName
                });
            }
        }
    }

    /// <summary>
    /// 获取目的地的卸料点
    /// </summary>
    private List<(int X, int Y)> GetUnloadPoints(int destX, int destY)
    {
        return new List<(int X, int Y)>
        {
            (destX, destY - 1), // 上
            (destX, destY + 1), // 下
            (destX - 1, destY), // 左
            (destX + 1, destY)  // 右
        }.Where(p => p.X >= MAP_MIN && p.X <= MAP_MAX && p.Y >= MAP_MIN && p.Y <= MAP_MAX).ToList();
    }

    /// <summary>
    /// 校验任务序列
    /// </summary>
    private void ValidateTaskSequence(Dictionary<string, List<TrajectoryRecord>> agvTrajectories)
    {
        // 按起始点分组任务
        var tasksByStartPoint = tasks.GroupBy(t => t.StartPoint)
            .ToDictionary(g => g.Key, g => g.ToList());

        // 按实际轨迹分组
        var trajectoryGroupItems = new Dictionary<string, List<string>>();
        foreach (var agvPair in agvTrajectories)
        {
            var agvName = agvPair.Key;
            var trajectory = agvPair.Value;

            bool? previousLoaded = null;
            foreach (var record in trajectory)
            {
                if (previousLoaded != null)
                {
                    // 取货时刻
                    if (record.Loaded && !previousLoaded.Value)
                    {
                        var startPoint = mapData.PickupRules.FirstOrDefault(x => x.Value.X == record.X && x.Value.Y == record.Y).Key;
                        if (!trajectoryGroupItems.ContainsKey(startPoint))
                        {
                            trajectoryGroupItems[startPoint] = new List<string>();
                        }
                        trajectoryGroupItems[startPoint].Add($"{record.Timestamp}_{record.Destination}");
                    }
                }

                previousLoaded = record.Loaded;
            }
        }

        if (trajectoryGroupItems.Count > tasksByStartPoint.Count)
        {
            throw new Exception($"轨迹的起点数量({trajectoryGroupItems.Count})不能大于任务起点数量({tasksByStartPoint.Count})");
        }

        foreach (var startPoint in tasksByStartPoint.Keys)
        {
            // 按时间排序，从轨迹中还原出取料的目标点序列
            if (trajectoryGroupItems.TryGetValue(startPoint, out var items))
            {
                var destinationsFramTrajectory = items
                    .OrderBy(x => int.Parse(x.Split('_')[0]))
                    .Select(x => x.Split('_')[1])
                    .ToList();

                var destinationsFromTasks = tasksByStartPoint[startPoint].Select(x => x.EndPoint).ToArray();

                var isEqual = destinationsFramTrajectory.SequenceEqual(destinationsFromTasks);
                if (!isEqual)
                {
                    var taskQueue = string.Join(",", destinationsFromTasks);
                    var trajectoryTaskQueue = string.Join(",", destinationsFramTrajectory);
                    throw new Exception($"任务和轨迹的任务序列不一致：{startPoint}，任务序列：{taskQueue}，轨迹的任务序列{trajectoryTaskQueue}");
                }
            }
        }
    }
}
