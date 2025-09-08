namespace DetonatorAgent.Services;

public interface ILogService
{
    Task<string> GetLogsAsync();
}

public interface IExecutionService
{
    Task<string> ExecuteCommandAsync(string command);
}
