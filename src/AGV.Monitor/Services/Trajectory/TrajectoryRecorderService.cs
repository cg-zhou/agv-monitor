using AGV.Monitor.Parsers;
using System.Collections.Generic;

namespace AGV.Monitor.Services.Trajectory;

public class TrajectoryRecorderService
{
    private readonly Agv[] agvs;
    private List<TrajectoryRecord> records { get; } = new List<TrajectoryRecord>();

    public TrajectoryRecorderService(Agv[] agvs)
    {
        this.agvs = agvs;

        // 记录第 0 秒
        Add(0);
    }

    public TrajectoryRecord[] GetRecords()
    {
        return records.ToArray();
    }

    public void Add(int timestamp)
    {
        foreach (var agv in agvs)
        {
            var task = agv.LoadedTask;
            var loaded = agv.IsLoaded;
            var destination = task?.EndPoint ?? string.Empty;
            var emergency = task?.Priority == TaskPriority.High;
            var taskId = task?.TaskId ?? string.Empty;
            var record = new TrajectoryRecord(
                timestamp, agv.Name, agv.Position.X, agv.Position.Y, agv.Pitch,
                loaded, destination, emergency, taskId);
            records.Add(record);
        }
    }
}
