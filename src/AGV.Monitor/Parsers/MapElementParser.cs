using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AGV.Monitor.Geometry;

namespace AGV.Monitor.Parsers;

/// <summary>
/// 地图数据结构
/// </summary>
public class MapData
{
    public Dictionary<string, (int X, int Y)> StartPoints { get; set; } = new Dictionary<string, (int, int)>();
    public Dictionary<string, (int X, int Y)> EndPoints { get; set; } = new Dictionary<string, (int, int)>();
    public Dictionary<string, (int X, int Y, Direction Pitch)> AgvPositions { get; set; } = new Dictionary<string, (int, int, Direction)>();
    public Dictionary<string, (int X, int Y)> PickupRules { get; set; } = new Dictionary<string, (int, int)>();
}

/// <summary>
/// 地图数据CSV文件解析器
/// </summary>
public static class MapElementParser
{
    /// <summary>
    /// 解析地图数据CSV文件
    /// </summary>
    /// <param name="filePath">CSV文件路径</param>
    /// <returns>地图元素列表</returns>
    public static List<MapElement> Parse(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"文件未找到: {filePath}");

        var content = File.ReadAllText(filePath);
        return ParseFromString(content);
    }

    /// <summary>
    /// 从Stream解析地图数据
    /// </summary>
    /// <param name="stream">包含CSV数据的Stream</param>
    /// <returns>地图元素列表</returns>
    public static List<MapElement> ParseFromStream(Stream stream)
    {
        using (var reader = new StreamReader(stream))
        {
            var content = reader.ReadToEnd();
            return ParseFromString(content);
        }
    }

    /// <summary>
    /// 从字符串解析地图数据
    /// </summary>
    /// <param name="csvContent">CSV内容字符串</param>
    /// <returns>地图元素列表</returns>
    public static List<MapElement> ParseFromString(string csvContent)
    {
        var elements = new List<MapElement>();
        var lines = csvContent.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);

        if (lines.Length <= 1)
            return elements;

        // 跳过标题行，从第二行开始解析
        for (int i = 1; i < lines.Length; i++)
        {
            try
            {
                var element = ParseLine(lines[i]);
                if (element != null)
                    elements.Add(element);
            }
            catch (Exception ex)
            {
                throw new InvalidDataException($"解析第{i + 1}行数据失败: {ex.Message}");
            }
        }

        return elements;
    }

    /// <summary>
    /// 解析单行CSV数据
    /// </summary>
    /// <param name="line">CSV行数据</param>
    /// <returns>地图元素对象</returns>
    private static MapElement ParseLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return null;
        }

        var parts = line.Split(',');
        if (parts.Length < 4)
            throw new ArgumentException($"数据列数不足，期望至少4列，实际{parts.Length}列");

        // 解析类型，支持snake_case和PascalCase格式
        var typeString = parts[0].Trim();
        MapElementType elementType;

        switch (typeString.ToLower())
        {
            case "start_point":
            case "startpoint":
                elementType = MapElementType.StartPoint;
                break;
            case "end_point":
            case "endpoint":
                elementType = MapElementType.EndPoint;
                break;
            case "agv":
                elementType = MapElementType.Agv;
                break;
            default:
                throw new ArgumentException($"未知的元素类型: {typeString}");
        }

        var element = new MapElement
        {
            Type = elementType,
            Name = parts[1].Trim(),
            X = int.Parse(parts[2].Trim()),
            Y = int.Parse(parts[3].Trim())
        };

        // 解析pitch字段（如果存在）
        if (parts.Length > 4 && !string.IsNullOrWhiteSpace(parts[4]))
        {
            if (int.TryParse(parts[4].Trim(), out int pitch))
            {
                element.Pitch = (Direction)pitch;
            }
        }

        return element;
    }

    /// <summary>
    /// 将地图元素列表转换为结构化地图数据
    /// </summary>
    /// <param name="elements">地图元素列表</param>
    /// <returns>结构化地图数据</returns>
    public static MapData ToMapData(List<MapElement> elements)
    {
        var mapData = new MapData();

        foreach (var element in elements)
        {
            switch (element.Type)
            {
                case MapElementType.StartPoint:
                    mapData.StartPoints[element.Name] = (element.X, element.Y);
                    // 根据起点位置计算取货点
                    if (new[] { "SP01", "SP02", "SP03" }.Any(x => x == element.Name))
                        mapData.PickupRules[element.Name] = (element.X + 1, element.Y); // 右侧取货
                    else // SP04, SP05, SP06
                        mapData.PickupRules[element.Name] = (element.X - 1, element.Y); // 左侧取货
                    break;

                case MapElementType.EndPoint:
                    mapData.EndPoints[element.Name] = (element.X, element.Y);
                    break;

                case MapElementType.Agv:
                    mapData.AgvPositions[element.Name] = (element.X, element.Y, element.Pitch.Value);
                    break;
            }
        }

        return mapData;
    }

    /// <summary>
    /// 按类型分组地图元素
    /// </summary>
    /// <param name="elements">地图元素列表</param>
    /// <returns>按类型分组的字典</returns>
    public static Dictionary<string, List<MapElement>> GroupByType(List<MapElement> elements)
    {
        return elements.GroupBy(e => e.Type.ToString())
                      .ToDictionary(g => g.Key, g => g.ToList());
    }

    /// <summary>
    /// 获取地图边界
    /// </summary>
    /// <param name="elements">地图元素列表</param>
    /// <returns>地图边界(最小X, 最小Y, 最大X, 最大Y)</returns>
    public static RectEx GetBounds(List<MapElement> elements)
    {
        if (!elements.Any())
        {
            return new RectEx(0, 0, 0, 0);
        }

        return new RectEx(
            elements.Min(e => e.X),
            elements.Max(e => e.Y),
            elements.Max(e => e.X),
            elements.Min(e => e.Y)
        );
    }
}
