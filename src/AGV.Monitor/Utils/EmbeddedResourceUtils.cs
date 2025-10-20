using AGV.Monitor.Parsers;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace AGV.Monitor.Utils;

/// <summary>
/// 嵌入资源工具类
/// </summary>
public static class EmbeddedResourceUtils
{
    private static readonly Assembly Assembly = Assembly.GetExecutingAssembly();

    /// <summary>
    /// 直接从嵌入资源解析地图数据
    /// </summary>
    /// <returns>地图元素列表</returns>
    public static List<MapElement> ParseMapDataFromResource()
    {
        using (var stream = GetEmbeddedResourceStream("map_data.csv"))
        {
            return MapElementParser.ParseFromStream(stream);
        }
    }

    /// <summary>
    /// 直接从嵌入资源解析任务数据
    /// </summary>
    /// <returns>任务记录列表</returns>
    public static List<TaskRecord> ParseTaskDataFromResource()
    {
        using (var stream = GetEmbeddedResourceStream("task_csv.csv"))
        {
            return TaskCsvParser.ParseFromStream(stream);
        }
    }

    /// <summary>
    /// 获取嵌入资源的Stream
    /// </summary>
    /// <param name="resourceName">资源名称</param>
    /// <returns>资源Stream</returns>
    private static Stream GetEmbeddedResourceStream(string resourceName)
    {
        var fullResourceName = $"AGV.Monitor.EmbeddedResources.{resourceName}";

        var stream = Assembly.GetManifestResourceStream(fullResourceName);
        if (stream == null)
            throw new FileNotFoundException($"嵌入资源未找到: {fullResourceName}");

        return stream;
    }
}
