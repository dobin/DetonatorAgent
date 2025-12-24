using System.Text.Json.Serialization;

namespace DetonatorAgent.Models;

public class ExecuteFileRequest {
    public IFormFile File { get; set; } = null!;
    public string? DropPath { get; set; }
    public string? ExecutableArgs { get; set; }
    public string? ExecutableName { get; set; }
    public string? ExecutionMode { get; set; }
}

public class ExecuteFileResponse {
    public string Status { get; set; } = string.Empty;
    public int? Pid { get; set; }
    public string? Message { get; set; }
}

public class KillResponse {
    public string Status { get; set; } = string.Empty;
    public string? Message { get; set; }
}

public class EdrVersionResponse {
    public string EdrVersion { get; set; } = string.Empty;
}

public class ExecutionLogsResponse {
    public int Pid { get; set; }
    public string Stdout { get; set; } = string.Empty;
    public string Stderr { get; set; } = string.Empty;
}

public class AgentLogsResponse {
    public List<string> Logs { get; set; } = new();
    public int Count { get; set; }
}

public class ExecutionTypesResponse {
    public List<string> Types { get; set; } = new();
    public string Default { get; set; } = string.Empty;
}

public class LockStatusResponse {
    [JsonPropertyName("in_use")]
    public bool InUse { get; set; }
}

public class LockErrorResponse {
    public string Status { get; set; } = "error";
    public string Message { get; set; } = string.Empty;
}
