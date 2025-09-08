namespace DetonatorAgent.Services;

public class LockService : ILockService
{
    private bool _inUse = false;
    private readonly object _lockObject = new object();
    private readonly ILogger<LockService> _logger;

    public LockService(ILogger<LockService> logger)
    {
        _logger = logger;
    }

    public bool IsInUse
    {
        get
        {
            lock (_lockObject)
            {
                return _inUse;
            }
        }
    }

    public bool TryAcquireLock()
    {
        lock (_lockObject)
        {
            if (_inUse)
            {
                _logger.LogWarning("Attempt to acquire lock when already in use");
                return false;
            }

            _inUse = true;
            _logger.LogInformation("Lock acquired successfully");
            return true;
        }
    }

    public void ReleaseLock()
    {
        lock (_lockObject)
        {
            if (!_inUse)
            {
                _logger.LogInformation("Release lock even though it was not acquired");
            }

            _inUse = false;
            _logger.LogInformation("Lock released");
        }
    }
}
