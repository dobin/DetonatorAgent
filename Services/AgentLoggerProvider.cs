using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DetonatorAgent.Services;

public class AgentLoggerProvider : ILoggerProvider
{
    private readonly AgentLogService _agentLogService;

    public AgentLoggerProvider(AgentLogService agentLogService)
    {
        _agentLogService = agentLogService;
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new AgentLogger(categoryName, _agentLogService);
    }

    public void Dispose() { }
}

public class AgentLogger : ILogger
{
    private readonly string _categoryName;
    private readonly AgentLogService _agentLogService;

    public AgentLogger(string categoryName, AgentLogService agentLogService)
    {
        _categoryName = categoryName;
        _agentLogService = agentLogService;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
            return;

        var message = formatter(state, exception);
        var logEntry = $"[{logLevel}] {_categoryName}: {message}";
        
        if (exception != null)
        {
            logEntry += $" | Exception: {exception.Message}";
        }

        _agentLogService.AddLog(logEntry);
    }
}
