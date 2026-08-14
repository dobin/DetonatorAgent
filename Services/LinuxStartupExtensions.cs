// This file is excluded from Windows builds via the csproj.
// It is the single place that references Linux-only types so that
// Program.cs stays free of preprocessor directives.
using DetonatorAgent.Services;

namespace DetonatorAgent.Services;

public static class LinuxStartupExtensions
{
    /// <summary>
    /// Registers the Linux execution service (exec only).
    /// EDR plugin registration is handled centrally in Program.cs via
    /// EdrPluginRegistry.
    /// </summary>
    public static IServiceCollection AddLinuxExecutionServices(
        this IServiceCollection services)
    {
        services.AddSingleton<IExecutionService, LinuxExecutionService>();
        return services;
    }
}
