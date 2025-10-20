using AGV.Monitor.Geometry;
using System;
using System.Collections.Generic;
using System.IO;

namespace AGV.Monitor.Parsers;

/// <summary>
/// AGV轨迹CSV文件解析器
/// </summary>
public static class TrajectoryRecordParser
{
    /// <summary>
    /// 解析AGV轨迹CSV文件
    /// </summary>
    /// <param name="filePath">CSV文件路径</param>
    /// <returns>轨迹记录列表</returns>
    public static List<TrajectoryRecord> Parse(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"文件未找到: {filePath}");
        }

        var records = new List<TrajectoryRecord>();
        var lines = File.ReadAllLines(filePath);

        if (lines.Length <= 1)
        {
            return records;
        }

        // 跳过标题行，从第二行开始解析
        for (int i = 1; i < lines.Length; i++)
        {
            try
            {
                var record = ParseLine(lines[i]);
                if (record != null)
                {
                    records.Add(record);
                }
            }
            catch (Exception ex)
            {
                throw new InvalidDataException($"解析第{i + 1}行数据失败: {ex.Message}");
            }
        }

        return records;
    }

    /// <summary>
    /// 解析单行CSV数据
    /// </summary>
    /// <param name="line">CSV行数据</param>
    /// <returns>轨迹记录对象</returns>
    private static TrajectoryRecord ParseLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return null;
        }

        var parts = line.Split(',');
        if (parts.Length < 8)
        {
            throw new ArgumentException($"数据列数不足，期望至少8列，实际{parts.Length}列");
        }

        return new TrajectoryRecord
        {
            Timestamp = int.Parse(parts[0].Trim()),
            Name = parts[1].Trim(),
            X = int.Parse(parts[2].Trim()),
            Y = int.Parse(parts[3].Trim()),
            Pitch = (Direction)int.Parse(parts[4].Trim()),
            Loaded = parts[5].Trim().ToLower() == "true",
            Destination = parts[6].Trim(),
            Emergency = parts[7].Trim().ToLower() == "true"
        };
    }
}
