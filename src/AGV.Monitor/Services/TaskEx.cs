using AGV.Monitor.Geometry;
using AGV.Monitor.Parsers;

namespace AGV.Monitor.Services;

public class TaskEx : TaskRecord
{
    public TaskEx(TaskRecord taskRecord, PointEx startPosition, PointEx endPosition)
    {
        TaskId = taskRecord.TaskId;
        StartPoint = taskRecord.StartPoint;
        EndPoint = taskRecord.EndPoint;
        Priority = taskRecord.Priority;
        RemainingTime = taskRecord.RemainingTime;
        StartPosition = startPosition;
        EndPosition = endPosition;

        PickupPosition = startPosition.X > 10 ? startPosition.LeftNeighbour : startPosition.RightNeighbour;
    }

    public bool IsPending => Agv == null;
    public bool IsRunning => !IsPending && !IsCompleted;
    public bool IsCompleted { get; private set; }

    public int StartTimestamp { get; private set; }
    public int CompleteTimestamp { get; private set; }
    public Agv Agv { get; private set; } = null;
    public void LoadBy(Agv agv, int startTimestamp)
    {
        Agv = agv;
        StartTimestamp = startTimestamp;
    }

    public void Unload(int completeTimestamp)
    {
        CompleteTimestamp = completeTimestamp;
        IsCompleted = true;
    }


    public PointEx StartPosition { get; }
    public PointEx EndPosition { get; }

    /// <summary>
    /// 取料点坐标
    /// </summary>
    public PointEx PickupPosition { get; }

    public override string ToString()
    {
        return $"{StartPoint} -> {EndPoint} {Priority}";
    }
}
