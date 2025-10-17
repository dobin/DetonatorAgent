namespace DetonatorAgent.Services;

/// <summary>
/// Provides access to execution service implementations based on execution type
/// </summary>
public class ExecutionServiceProvider : IExecutionServiceProvider {
    private readonly Dictionary<string, IExecutionService> _executionServices;
    private readonly IExecutionService _defaultService;
    private readonly ILogger<ExecutionServiceProvider> _logger;
    private IExecutionService? _lastUsedService;
    private readonly object _lock = new object();

    public ExecutionServiceProvider(
        IEnumerable<IExecutionService> executionServices,
        ILogger<ExecutionServiceProvider> logger) {
        _logger = logger;
        _executionServices = new Dictionary<string, IExecutionService>(StringComparer.OrdinalIgnoreCase);

        // Register all execution services by their ExecutionTypeName
        foreach (var service in executionServices) {
            var serviceName = service.ExecutionTypeName;
            _executionServices[serviceName] = service;
            _logger.LogInformation("Registered execution service: {ServiceName} ({ServiceType})", 
                serviceName, service.GetType().Name);
        }

        // Set default service (first one registered)
        _defaultService = _executionServices.Values.FirstOrDefault() 
            ?? throw new InvalidOperationException("No execution services registered");

        _logger.LogInformation("Default execution service: {DefaultService}", 
            _defaultService.ExecutionTypeName);
    }

    public IExecutionService? GetExecutionService(string? executionType) {
        IExecutionService? service;

        if (string.IsNullOrWhiteSpace(executionType)) {
            service = _defaultService;
        }
        else if (_executionServices.TryGetValue(executionType, out service)) {
            _logger.LogInformation("Using execution service: {ExecutionType}", executionType);
        }
        else {
            _logger.LogWarning("Execution service not found: {ExecutionType}. Available: {Available}", 
                executionType, string.Join(", ", _executionServices.Keys));
            return null;
        }

        // Track the last used service
        lock (_lock) {
            _lastUsedService = service;
        }

        return service;
    }

    public IExecutionService GetDefaultExecutionService() {
        return _defaultService;
    }

    public IEnumerable<string> GetAvailableExecutionTypes() {
        return _executionServices.Keys;
    }

    public string GetDefaultExecutionTypeName() {
        return _defaultService.ExecutionTypeName;
    }

    public IExecutionService? GetLastUsedExecutionService() {
        lock (_lock) {
            return _lastUsedService;
        }
    }

    public void SetLastUsedExecutionService(IExecutionService service) {
        lock (_lock) {
            _lastUsedService = service;
        }
    }
}
