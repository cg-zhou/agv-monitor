using System;
using System.Collections.Generic;
using System.IO;

namespace AGV.Monitor.Parsers;

/// <summary>
/// 任务CSV文件解析器
/// </summary>
public static class TaskCsvParser
{
    /// <summary>
    /// 解析任务CSV文件
    /// </summary>
    /// <param name="filePath">CSV文件路径</param>
    /// <returns>任务记录列表</returns>
    public static List<TaskRecord> Parse(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"文件未找到: {filePath}");
        }

        var content = File.ReadAllText(filePath);
        return ParseFromString(content);
    }

    /// <summary>
    /// 从Stream解析任务数据
    /// </summary>
    /// <param name="stream">包含CSV数据的Stream</param>
    /// <returns>任务记录列表</returns>
    public static List<TaskRecord> ParseFromStream(Stream stream)
    {
        using (var reader = new StreamReader(stream))
        {
            var content = reader.ReadToEnd();
            return ParseFromString(content);
        }
    }

    /// <summary>
    /// 从字符串解析任务数据
    /// </summary>
    /// <param name="csvContent">CSV内容字符串</param>
    /// <returns>任务记录列表</returns>
    public static List<TaskRecord> ParseFromString(string csvContent)
    {
        var tasks = new List<TaskRecord>();
        var lines = csvContent.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);

        if (lines.Length <= 1)
            return tasks;

        // 跳过标题行，从第二行开始解析
        for (int i = 1; i < lines.Length; i++)
        {
            try
            {
                var task = ParseLine(lines[i]);
                if (task != null)
                    tasks.Add(task);
            }
            catch (Exception ex)
            {
                throw new InvalidDataException($"解析第{i + 1}行数据失败: {ex.Message}");
            }
        }

        return tasks;
    }

    /// <summary>
    /// 解析单行CSV数据
    /// </summary>
    /// <param name="line">CSV行数据</param>
    /// <returns>任务记录对象</returns>
    private static TaskRecord ParseLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return null;
        }

        var parts = line.Split(',');
        if (parts.Length < 4)
            throw new ArgumentException($"数据列数不足，期望至少4列，实际{parts.Length}列");

        // 解析优先级
        var priorityString = parts[3].Trim();
        TaskPriority priority = TaskPriority.Normal; // 默认值

        if (Enum.TryParse<TaskPriority>(priorityString, true, out var parsedpriority))
        {
            priority = parsedpriority;
        }
        else
        {
            // 兼容旧的字符串格式
            switch (priorityString.ToLower())
            {
                case "high":
                    priority = TaskPriority.High;
                    break;
                case "normal":
                default:
                    priority = TaskPriority.Normal;
                    break;
            }
        }

        var task = new TaskRecord
        {
            TaskId = parts[0].Trim(),
            StartPoint = parts[1].Trim(),
            EndPoint = parts[2].Trim(),
            Priority = priority
        };

        // 解析remaining_time字段（如果存在且不为空）
        if (parts.Length > 4 && !string.IsNullOrWhiteSpace(parts[4]))
        {
            if (int.TryParse(parts[4].Trim(), out int remainingTime))
                task.RemainingTime = remainingTime;
        }

        return task;
    }

    /// <summary>
    /// 将任务记录数组写入CSV文件
    /// </summary>
    /// <param name="tasks">任务记录数组</param>
    /// <param name="filePath">CSV文件路径</param>
    public static void WriteToFile(TaskRecord[] tasks, string filePath)
    {
        var csvContent = GenerateCsvContent(tasks);
        File.WriteAllText(filePath, csvContent);
    }

    /// <summary>
    /// 生成CSV内容字符串
    /// </summary>
    /// <param name="tasks">任务记录数组</param>
    /// <returns>CSV格式的字符串</returns>
    public static string GenerateCsvContent(TaskRecord[] tasks)
    {
        var lines = new List<string>
        {
            "id,start_point,end_point,priority,remaining_time"
        };

        foreach (var task in tasks)
        {
            var remainingTimeStr = task.RemainingTime?.ToString() ?? "None";
            var line = $"{task.TaskId},{task.StartPoint},{task.EndPoint},{task.Priority},{remainingTimeStr}";
            lines.Add(line);
        }

        return string.Join(Environment.NewLine, lines);
    }
}
