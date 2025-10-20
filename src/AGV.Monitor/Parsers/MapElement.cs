using AGV.Monitor.Geometry;

namespace AGV.Monitor.Parsers;

/// <summary>
/// 地图元素类型
/// </summary>
public enum MapElementType
{
    StartPoint,
    EndPoint,
    Agv
}

/// <summary>
/// 地图元素数据模型
/// </summary>
public class MapElement
{
    public MapElementType Type { get; set; }
    public string Name { get; set; } = string.Empty;
    public int X { get; set; }
    public int Y { get; set; }
    public Direction? Pitch { get; set; }

    public override string ToString()
    {
        return $"{Type} {Name} ({X}, {Y})";
    }
}
