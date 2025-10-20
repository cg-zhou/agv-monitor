namespace AGV.Monitor.Services.Trajectory;

/// <summary>
/// 校验结果
/// </summary>
public class ValidationResult
{
    public bool IsValid { get; set; }
    public string Message { get; set; } = string.Empty;
    public int Timestamp { get; set; }
    public string AgvName { get; set; } = string.Empty;
}
