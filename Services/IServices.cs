namespace DetonatorAgent.Services;

public interface ILockService {
    bool IsInUse { get; }
    bool TryAcquireLock();
    void ReleaseLock();
}

public interface IExecutionService {
    Task<bool> WriteMalwareAsync(string filePath, byte[] content);
    Task<(bool Success, int Pid, string? ErrorMessage)> StartProcessAsync(string filePath, string? arguments = null);
    Task<(bool Success, string? ErrorMessage)> KillLastExecutionAsync();
    Task<(int Pid, string Stdout, string Stderr)> GetExecutionLogsAsync();
    Task<(bool Success, string? FilePath, string? ErrorMessage)> PrepareFileForExecutionAsync(string filePath, string? executeFile = null);
}

public interface IEdrService {
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

public interface IAgentLogService {
    /// <summary>
    /// Gets all agent logs as a list of strings
    /// </summary>
    List<string> GetAgentLogs();

    /// <summary>
    /// Clears all captured agent logs
    /// </summary>
    void ClearLogs();
}
