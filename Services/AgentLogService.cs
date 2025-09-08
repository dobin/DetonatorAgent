using System.Collections.Concurrent;

namespace DetonatorAgent.Services;

public class AgentLogService : IAgentLogService
{
    private readonly ConcurrentQueue<string> _logs = new();
    private readonly int _maxLogCount = 1000; // Prevent memory issues

    public List<string> GetAgentLogs()
    {
        return _logs.ToList();
    }

    public void ClearLogs()
    {
        _logs.Clear();
    }

    public void AddLog(string logMessage)
    {
        // Add timestamp to the log message
        var timestampedLog = $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff} UTC] {logMessage}";
        
        _logs.Enqueue(timestampedLog);
        
        // Remove old logs if we exceed the limit
        while (_logs.Count > _maxLogCount)
        {
            _logs.TryDequeue(out _);
        }
    }
}
