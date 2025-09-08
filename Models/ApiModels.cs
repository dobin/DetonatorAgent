namespace DetonatorAgent.Models;

public class ExecuteFileRequest
{
    public IFormFile File { get; set; } = null!;
    public string? Path { get; set; }
    public string? FileArgs { get; set; }
}

public class ExecuteFileResponse
{
    public string Status { get; set; } = string.Empty;
    public int? Pid { get; set; }
    public string? Message { get; set; }
}

public class KillResponse
{
    public string Status { get; set; } = string.Empty;
    public string? Message { get; set; }
}

public class EdrLogsResponse
{
    public string Logs { get; set; } = string.Empty;
    public string EdrVersion { get; set; } = string.Empty;
    public string PluginVersion { get; set; } = string.Empty;
}

public class ExecutionLogsResponse
{
    public int Pid { get; set; }
    public string Stdout { get; set; } = string.Empty;
    public string Stderr { get; set; } = string.Empty;
}

public class AgentLogsResponse
{
    public List<string> Logs { get; set; } = new();
    public int Count { get; set; }
}

public class LockStatusResponse
{
    public bool InUse { get; set; }
}

public class LockErrorResponse
{
    public string Status { get; set; } = "error";
    public string Message { get; set; } = string.Empty;
}

public class ApiResponse<T>
{
    public bool Success { get; set; }
    public T? Data { get; set; }
    public string? Error { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
