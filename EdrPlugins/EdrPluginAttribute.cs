namespace DetonatorAgent.EdrPlugins;

/// <summary>
/// Platform on which an EDR plugin is available.
/// </summary>
public enum EdrPlatform
{
    Windows,
    Linux,
    Cross
}

/// <summary>
/// Marks a class as an EDR plugin. The <see cref="EdrPluginRegistry"/> discovers
/// all types annotated with this attribute at startup via reflection, so adding
/// a new plugin is a matter of dropping a new file into the EdrPlugins folder.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class EdrPluginAttribute : Attribute
{
    /// <summary>
    /// CLI name of the plugin (e.g. "defender", "logfile"). Case-insensitive.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Platform on which this plugin can run.
    /// </summary>
    public EdrPlatform Platform { get; }

    /// <summary>
    /// If true, this plugin is the default on Windows when --edr is not specified.
    /// Only one plugin may set this to true.
    /// </summary>
    public bool WindowsDefault { get; set; }

    /// <summary>
    /// If true, this plugin is the default on Linux when --edr is not specified.
    /// Only one plugin may set this to true.
    /// </summary>
    public bool LinuxDefault { get; set; }

    public EdrPluginAttribute(string name, EdrPlatform platform)
    {
        Name = name;
        Platform = platform;
    }
}
