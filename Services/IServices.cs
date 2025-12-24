namespace DetonatorAgent.Services;

public interface ILockService {
    bool IsInUse { get; }
    bool TryAcquireLock();
    void ReleaseLock();
}

public interface IExecutionService {
    /// <summary>
    /// Gets the execution type name for this service (e.g., "exec", "autoit")
    /// </summary>
    string ExecutionTypeName { get; }

    Task<bool> WriteMalwareAsync(string filePath, byte[] content, byte? xorKey = null);
    Task<(bool Success, int Pid, string? ErrorMessage)> StartProcessAsync(string? arguments = null);
    Task<(bool Success, string? ErrorMessage)> KillLastExecutionAsync();
    Task<(int Pid, string Stdout, string Stderr)> GetExecutionLogsAsync();
}

public interface IEdrService {
    /// <summary>
    /// Starts EDR log collection from this point in time
    /// </summary>
    bool StartCollection();

    /// <summary>
    /// Stops EDR log collection and processes collected events
    /// </summary>
    bool StopCollection();

    /// <summary>
    /// Gets the collected EDR logs since StartCollection was called
    /// </summary>
    string GetLogs();

    /// <summary>
    /// Gets the EDR vendor name and version
    /// </summary>
    string GetEdrVersion();
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
