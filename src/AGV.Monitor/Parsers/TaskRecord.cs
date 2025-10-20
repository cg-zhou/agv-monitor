namespace AGV.Monitor.Parsers;

/// <summary>
/// 任务数据模型
/// </summary>
public class TaskRecord
{
    public string TaskId { get; set; } = string.Empty;
    public string StartPoint { get; set; } = string.Empty;
    public string EndPoint { get; set; } = string.Empty;
    public TaskPriority Priority { get; set; } = TaskPriority.Normal;
    public int? RemainingTime { get; set; }
}
