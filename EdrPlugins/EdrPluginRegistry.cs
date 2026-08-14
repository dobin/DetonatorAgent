using System.Reflection;
using DetonatorAgent.Services;

namespace DetonatorAgent.EdrPlugins;

/// <summary>
/// Discovered EDR plugin metadata.
/// </summary>
public sealed record EdrPluginInfo(string Name, EdrPlatform Platform, Type ImplementationType);

/// <summary>
/// Reflection-based EDR plugin discovery. Scans the executing assembly for
/// classes annotated with <see cref="EdrPluginAttribute"/> that implement
/// <see cref="IEdrService"/>, and filters them by the current OS.
///
/// To add a new EDR plugin:
///   1. Create a class implementing IEdrService in EdrPlugins/.
///   2. Annotate it with [EdrPlugin("name", EdrPlatform.Xxx)].
///   3. (Optional) Set WindowsDefault=true or LinuxDefault=true.
/// No other file needs to be touched.
/// </summary>
public static class EdrPluginRegistry
{
    private static readonly Lazy<IReadOnlyList<EdrPluginInfo>> _available =
        new(DiscoverAvailable);

    /// <summary>
    /// Plugins available on the current OS, sorted by name.
    /// </summary>
    public static IReadOnlyList<EdrPluginInfo> GetAvailablePlugins() => _available.Value;

    /// <summary>
    /// Returns the default plugin name for the current OS, or null if none is
    /// marked as default (in which case the caller should error).
    /// </summary>
    public static string? GetDefaultPluginName()
    {
        var wantWindows = OperatingSystem.IsWindows();
        foreach (var p in _available.Value)
        {
            var attr = p.ImplementationType.GetCustomAttribute<EdrPluginAttribute>()!;
            if (wantWindows && attr.WindowsDefault) return p.Name;
            if (!wantWindows && attr.LinuxDefault) return p.Name;
        }
        return null;
    }

    /// <summary>
    /// Resolves a plugin name to its implementation type (case-insensitive),
    /// or null if no available plugin matches.
    /// </summary>
    public static Type? Resolve(string name)
    {
        foreach (var p in _available.Value)
        {
            if (string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))
                return p.ImplementationType;
        }
        return null;
    }

    private static IReadOnlyList<EdrPluginInfo> DiscoverAvailable()
    {
        var isWindows = OperatingSystem.IsWindows();
        var results = new List<EdrPluginInfo>();

        foreach (var type in Assembly.GetExecutingAssembly().GetTypes())
        {
            if (type.IsAbstract || type.IsInterface) continue;
            if (!typeof(IEdrService).IsAssignableFrom(type)) continue;

            var attr = type.GetCustomAttribute<EdrPluginAttribute>();
            if (attr is null) continue;

            var matches = attr.Platform switch
            {
                EdrPlatform.Cross => true,
                EdrPlatform.Windows => isWindows,
                EdrPlatform.Linux => !isWindows,
                _ => false
            };
            if (!matches) continue;

            results.Add(new EdrPluginInfo(attr.Name, attr.Platform, type));
        }

        return results
            .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
