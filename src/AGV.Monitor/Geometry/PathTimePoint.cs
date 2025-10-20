namespace AGV.Monitor.Geometry;

/// <summary>
/// 路径时间点数据结构，包含位置和累计时间
/// </summary>
public class PathTimePoint
{
    public PointEx Position { get; set; }
    public int TimeCost { get; set; }

    public PathTimePoint(PointEx position, int cumulativeTime)
    {
        Position = position;
        TimeCost = cumulativeTime;
    }

    public override string ToString()
    {
        return $"{Position} {TimeCost}";
    }
}
