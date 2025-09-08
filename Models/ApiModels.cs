namespace DetonatorAgent.Models;

public class ExecuteRequest
{
    public string Command { get; set; } = string.Empty;
}

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

public class ApiResponse<T>
{
    public bool Success { get; set; }
    public T? Data { get; set; }
    public string? Error { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
