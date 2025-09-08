namespace DetonatorAgent.Services;

public interface ILogService
{
    Task<string> GetLogsAsync();
}

public interface IExecutionService
{
    Task<bool> WriteMalwareAsync(string filePath, byte[] content);
    Task<(bool Success, int Pid, string? ErrorMessage)> StartProcessAsync(string filePath, string? arguments = null);
    Task<bool> KillLastExecutionAsync();
    Task<(int Pid, string Stdout, string Stderr)> GetExecutionLogsAsync();
}

public interface IEdrService
{
    /// <summary>
    /// Starts EDR log collection from this point in time
    /// </summary>
    Task<bool> StartCollectionAsync();
    
    /// <summary>
    /// Stops EDR log collection and processes collected events
    /// </summary>
    Task<bool> StopCollectionAsync();
    
    /// <summary>
    /// Gets the collected EDR logs since StartCollection was called
    /// </summary>
    Task<string> GetLogsAsync();
    
    /// <summary>
    /// Gets the EDR vendor name and version
    /// </summary>
    string GetEdrVersion();
    
    /// <summary>
    /// Gets the plugin version for this EDR implementation
    /// </summary>
    string GetPluginVersion();
}
