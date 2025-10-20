using AGV.Monitor.Geometry;
using AGV.Monitor.Parsers;
using AGV.Monitor.Services.Trajectory;
using AGV.Monitor.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AGV.Monitor.Services;

/// <summary>
/// AGV 上下文，包含 AGV 相关的环境信息
/// </summary>
public class AgvContext
{
    public SchedulerService Scheduler { get; }
    public TrajectoryRecorderService TrajectoryRecorder { get; }

    public List<PointEx> FixedMapObstacles { get; }
    public List<TaskEx> Tasks { get; }
    public Agv[] Agvs { get; }
    public List<MapElement> MapElements { get; }
    public List<TaskRecord> TaskRecords { get; }
    public RectEx MapBounds { get; }

    public static AgvContext Create(Random taskOrderRandom = null, List<TaskRecord> taskRecords = null)
    {
        var mapElements = EmbeddedResourceUtils.ParseMapDataFromResource();
        if (taskRecords == null)
        {
            taskRecords = EmbeddedResourceUtils.ParseTaskDataFromResource();
        }

        if (taskOrderRandom != null)
        {
            taskRecords = taskRecords.OrderBy(x => taskOrderRandom.Next()).ToList();
        }

        return new AgvContext(mapElements, taskRecords);
    }

    private AgvContext(List<MapElement> mapElements, List<TaskRecord> taskRecords)
    {
        MapElements = mapElements;
        TaskRecords = taskRecords;

        Tasks = taskRecords
            .Select(task => new TaskEx(
                task,
                GetPositionByName(MapElementType.StartPoint, task.StartPoint),
                GetPositionByName(MapElementType.EndPoint, task.EndPoint)))
            .ToList();

        Agvs = mapElements
            .Where(item => item.Type == MapElementType.Agv)
            .Select(item => new Agv(item.Name, new PointEx(item.X, item.Y), (Direction)item.Pitch))
            .ToArray();

        var obstacles = mapElements
            .Where(item => item.Type == MapElementType.StartPoint || item.Type == MapElementType.EndPoint)
            .Select(item => new PointEx(item.X, item.Y))
            .ToList();

        // 将地图边界，加入障碍点
        var minX = mapElements.Min(item => item.X);
        var maxX = mapElements.Max(item => item.X);
        var minY = mapElements.Min(item => item.Y);
        var maxY = mapElements.Max(item => item.Y);
        for (var x = minX - 1; x <= maxX + 1; x++)
        {
            obstacles.Add(new PointEx(x, minY - 1));
            obstacles.Add(new PointEx(x, maxY + 1));
        }

        for (var y = minY - 1; y <= maxY + 1; y++)
        {
            obstacles.Add(new PointEx(minX - 1, y));
            obstacles.Add(new PointEx(maxX + 1, y));
        }

        FixedMapObstacles = obstacles.ToList();

        MapBounds = MapElementParser.GetBounds(mapElements);

        Scheduler = new(this);
        TrajectoryRecorder = new(Agvs);
    }

    private PointEx GetPositionByName(MapElementType elementType, string name)
    {
        var element = MapElements.First(item => item.Type == elementType && item.Name == name);
        return new PointEx(element.X, element.Y);
    }

    public TaskEx[] GetCompletedTasks()
    {
        return Tasks.Where(x => x.IsCompleted).ToArray();
    }

    public bool AllTasksCompleted => Tasks.All(x => x.IsCompleted);

    public TaskEx[] GetSortedPendingTasks()
    {
        var pendingTasks = Tasks.Where(x => x.IsPending).ToArray();
        var groupedTasks = pendingTasks.GroupBy(x => x.StartPoint);

        const int middleY = 10;
        pendingTasks = pendingTasks
            // 先按序列
            .OrderBy(x => groupedTasks.First(group => group.Key == x.StartPoint).ToList().IndexOf(x))
            // 再按优先级
            .ThenByDescending(x => x.Priority)
            // 然后根据队列中是否有优先级高的任务
            .ThenByDescending(x => groupedTasks.First(group => group.Key == x.StartPoint).Any(t => t.Priority == TaskPriority.High))
            // 最后根据队列长度排序
            .ThenByDescending(x => groupedTasks.First(group => group.Key == x.StartPoint).Count())
            .ThenByDescending(x => x.PickupPosition.Y != middleY)
            .ToArray();

        return pendingTasks;
    }
}
