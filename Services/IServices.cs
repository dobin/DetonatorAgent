namespace DetonatorAgent.Services;

public interface ILogService
{
    Task<string> GetLogsAsync();
}

public interface IExecutionService
{
    Task<string> ExecuteCommandAsync(string command);
    Task<bool> WriteMalwareAsync(string filePath, byte[] content);
    Task<(bool Success, int Pid, string? ErrorMessage)> StartProcessAsync(string filePath, string? arguments = null);
}
