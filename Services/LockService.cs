namespace DetonatorAgent.Services;

public class LockService : ILockService {
    private bool _inUse = false;
    private DateTime _acquiredAt = DateTime.MinValue;
    private readonly TimeSpan _lockTimeout = TimeSpan.FromMinutes(30);
    private readonly object _lockObject = new object();
    private readonly ILogger<LockService> _logger;

    public LockService(ILogger<LockService> logger) {
        _logger = logger;
    }

    public bool IsInUse {
        get {
            lock (_lockObject) {
                if (_inUse && DateTime.UtcNow - _acquiredAt > _lockTimeout) {
                    _logger.LogWarning("Lock expired after {Timeout} minutes — auto-releasing stale lock", _lockTimeout.TotalMinutes);
                    _inUse = false;
                    _acquiredAt = DateTime.MinValue;
                }
                return _inUse;
            }
        }
    }

    public bool TryAcquireLock() {
        lock (_lockObject) {
            // Auto-release stale lock if it has expired
            if (_inUse && DateTime.UtcNow - _acquiredAt > _lockTimeout) {
                _logger.LogWarning("Lock expired after {Timeout} minutes — auto-releasing before new acquisition", _lockTimeout.TotalMinutes);
                _inUse = false;
                _acquiredAt = DateTime.MinValue;
            }

            if (_inUse) {
                _logger.LogWarning("Attempt to acquire lock when already in use");
                return false;
            }

            _inUse = true;
            _acquiredAt = DateTime.UtcNow;
            _logger.LogInformation("Lock acquired");
            return true;
        }
    }

    public void ReleaseLock() {
        lock (_lockObject) {
            if (!_inUse) {
                _logger.LogInformation("Release lock even though it was not acquired");
            }

            _inUse = false;
            _acquiredAt = DateTime.MinValue;
            _logger.LogInformation("Lock released");
        }
    }
}
