namespace DetonatorAgent.Services;

public class ExecutionTrackingService {
    private IExecutionService? _lastExecutionService;
    private readonly object _lock = new object();

    public IExecutionService? GetLastExecutionService() {
        lock (_lock) {
            return _lastExecutionService;
        }
    }

    public void SetLastExecutionService(IExecutionService service) {
        lock (_lock) {
            _lastExecutionService = service;
        }
    }
}
