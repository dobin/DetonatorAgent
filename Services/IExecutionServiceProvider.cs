namespace DetonatorAgent.Services;

/// <summary>
/// Provides access to execution service implementations based on execution type
/// </summary>
public interface IExecutionServiceProvider {
    /// <summary>
    /// Gets the execution service for the specified type
    /// </summary>
    /// <param name="executionType">The execution type (e.g., "exec", "autoit")</param>
    /// <returns>The execution service implementation, or null if not found</returns>
    IExecutionService? GetExecutionService(string? executionType);

    /// <summary>
    /// Gets the default execution service for the current platform
    /// </summary>
    IExecutionService GetDefaultExecutionService();

    /// <summary>
    /// Gets a list of available execution types for the current platform
    /// </summary>
    IEnumerable<string> GetAvailableExecutionTypes();

    /// <summary>
    /// Gets the name of the default execution type
    /// </summary>
    string GetDefaultExecutionTypeName();

    /// <summary>
    /// Gets the last used execution service (for operations like kill)
    /// </summary>
    IExecutionService? GetLastUsedExecutionService();

    /// <summary>
    /// Sets the last used execution service
    /// </summary>
    void SetLastUsedExecutionService(IExecutionService service);
}
