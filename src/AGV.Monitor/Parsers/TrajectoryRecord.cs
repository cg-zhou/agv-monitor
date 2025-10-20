using AGV.Monitor.Geometry;

namespace AGV.Monitor.Parsers;

/// <summary>
/// AGV轨迹记录数据模型
/// </summary>
public class TrajectoryRecord
{
    public TrajectoryRecord()
    {
    }

    public TrajectoryRecord(
        int timestamp, string name, int x, int y, Direction pitch,
        bool loaded, string destination, bool emergency, string taskId)
    {
        Timestamp = timestamp;
        Name = name;
        X = x;
        Y = y;
        Pitch = pitch;
        Loaded = loaded;
        Destination = destination;
        Emergency = emergency;
        TaskId = taskId;
    }

    public int Timestamp { get; set; }
    public string Name { get; set; } = string.Empty;
    public int X { get; set; }
    public int Y { get; set; }
    public Direction Pitch { get; set; }
    public bool Loaded { get; set; }
    public string Destination { get; set; } = string.Empty;
    public bool Emergency { get; set; }
    public string TaskId { get; }
}
