// This file is excluded from non-Windows builds via the csproj.
// It is the single place that references Windows-only types so that
// Program.cs stays free of preprocessor directives.
using DetonatorAgent.Services;
using System.Runtime.Versioning;

namespace DetonatorAgent.Services;

[SupportedOSPlatform("windows")]
public static class WindowsStartupExtensions
{
    /// <summary>
    /// Registers all Windows execution services (exec, autoit, clickfix).
    /// EDR plugin registration is handled centrally in Program.cs via
    /// EdrPluginRegistry.
    /// </summary>
    public static IServiceCollection AddWindowsExecutionServices(
        this IServiceCollection services)
    {
        services.AddSingleton<IExecutionService, WindowsExecutionServiceExec>();
        services.AddSingleton<IExecutionService, WindowsExecutionServiceAutoit>();
        services.AddSingleton<IExecutionService, WindowsExecutionServiceClickfix>();
        return services;
    }
}
