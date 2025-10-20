using AGV.Monitor.Parsers;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AGV.Monitor.Views;

/// <summary>
/// 热点图数据管理类，用于统计和管理AGV在各个位置的停留次数
/// </summary>
public class HeatmapData
{
    /// <summary>
    /// 位置访问计数字典，Key为"X,Y"格式的位置字符串，Value为访问次数
    /// </summary>
    private readonly Dictionary<string, int> _positionCounts = new();

    /// <summary>
    /// 最大访问次数，用于计算热度比例
    /// </summary>
    public int MaxCount { get; private set; }

    /// <summary>
    /// 从轨迹记录中构建热点图数据
    /// </summary>
    /// <param name="trajectoryRecords">轨迹记录列表</param>
    public void BuildFromTrajectoryRecords(IEnumerable<TrajectoryRecord> trajectoryRecords)
    {
        _positionCounts.Clear();
        MaxCount = 0;

        foreach (var record in trajectoryRecords)
        {
            var positionKey = $"{record.X},{record.Y}";
            
            if (_positionCounts.ContainsKey(positionKey))
            {
                _positionCounts[positionKey]++;
            }
            else
            {
                _positionCounts[positionKey] = 1;
            }

            // 更新最大访问次数
            if (_positionCounts[positionKey] > MaxCount)
            {
                MaxCount = _positionCounts[positionKey];
            }
        }
    }

    /// <summary>
    /// 获取指定位置的访问次数
    /// </summary>
    /// <param name="x">X坐标</param>
    /// <param name="y">Y坐标</param>
    /// <returns>访问次数，如果位置未被访问过则返回0</returns>
    public int GetCount(int x, int y)
    {
        var positionKey = $"{x},{y}";
        return _positionCounts.ContainsKey(positionKey) ? _positionCounts[positionKey] : 0;
    }

    /// <summary>
    /// 获取指定位置的热度比例（0.0 - 1.0）
    /// </summary>
    /// <param name="x">X坐标</param>
    /// <param name="y">Y坐标</param>
    /// <returns>热度比例，0表示未访问，1表示最热点</returns>
    public double GetHeatRatio(int x, int y)
    {
        if (MaxCount == 0) return 0.0;
        
        var count = GetCount(x, y);
        if (count == 0) return 0.0;
        
        // 特殊处理：当所有位置访问次数都相同时，显示最低热度（纯绿色）
        if (MaxCount == 1) return 0.01; // 非常接近0，确保显示纯绿色
        
        // 找到最小的非0访问次数
        var minCount = GetAllPositions().Min(pos => pos.Count);
        
        // 如果当前位置就是最小访问次数，返回绿色
        if (count == minCount) return 0.01;
        
        // 对于其他位置，使用对数缩放映射到 0.05-0.9 范围
        var logCount = Math.Log(count + 1);
        var logMax = Math.Log(MaxCount + 1);
        var logMin = Math.Log(minCount + 1);
        
        // 重新归一化：从最小值到最大值映射到 0.05-0.9
        var normalizedRatio = (logCount - logMin) / (logMax - logMin);
        var smoothRatio = Math.Sqrt(normalizedRatio);
        
        // 映射到 0.05-0.9 范围
        return 0.05 + smoothRatio * 0.85;
    }

    /// <summary>
    /// 获取所有有访问记录的位置
    /// </summary>
    /// <returns>位置坐标列表</returns>
    public IEnumerable<(int X, int Y, int Count)> GetAllPositions()
    {
        return _positionCounts.Select(kvp =>
        {
            var parts = kvp.Key.Split(',');
            var x = int.Parse(parts[0]);
            var y = int.Parse(parts[1]);
            return (x, y, kvp.Value);
        });
    }

    /// <summary>
    /// 清除所有热点图数据
    /// </summary>
    public void Clear()
    {
        _positionCounts.Clear();
        MaxCount = 0;
    }

    /// <summary>
    /// 获取热点图统计信息
    /// </summary>
    /// <returns>统计信息字符串</returns>
    public string GetStatistics()
    {
        var totalPositions = _positionCounts.Count;
        var totalVisits = _positionCounts.Values.Sum();
        var avgVisits = totalPositions > 0 ? (double)totalVisits / totalPositions : 0;

        return $"热点统计: {totalPositions}个位置, {totalVisits}次访问, 平均{avgVisits:F1}次/位置, 最高{MaxCount}次";
    }
}
