using System.Collections.Concurrent;

namespace DetonatorAgent.Services;

public class AgentLogService : IAgentLogService {
    private readonly ConcurrentQueue<string> _logs = new();
    private readonly int _maxLogCount = 1000; // Prevent memory issues
    private readonly string _logFilePath;
    private readonly object _fileLock = new();

    public AgentLogService() {
        // Create logs directory if it doesn't exist
        var logsDir = Path.Combine(AppContext.BaseDirectory, "logs");
        Directory.CreateDirectory(logsDir);
        
        // Create log file
        var logFileName = "detonator-agent.log";
        _logFilePath = Path.Combine(logsDir, logFileName);
    }

    public List<string> GetAgentLogs() {
        return _logs.ToList();
    }

    public void ClearLogs() {
        _logs.Clear();
    }

    public void AddLog(string logMessage) {
        // Add timestamp to the log message
        var timestampedLog = $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff} UTC] {logMessage}";

        _logs.Enqueue(timestampedLog);

        // Remove old logs if we exceed the limit
        while (_logs.Count > _maxLogCount) {
            _logs.TryDequeue(out _);
        }

        // Write to log file
        WriteToFile(timestampedLog);
    }

    private void WriteToFile(string logMessage) {
        try {
            lock (_fileLock) {
                File.AppendAllText(_logFilePath, logMessage + Environment.NewLine);
            }
        }
        catch (Exception ex) {
            // If we can't write to the file, at least write to console
            Console.WriteLine($"Failed to write to log file: {ex.Message}");
        }
    }
}
