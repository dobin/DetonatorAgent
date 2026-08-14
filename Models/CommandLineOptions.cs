using CommandLine;

namespace DetonatorAgent.Models;

/// <summary>
/// Command line options for DetonatorAgent
/// </summary>
public class CommandLineOptions
{
    [Option('p', "port", Default = 8080, HelpText = "Port number to listen on (1-65535). Default: 8080")]
    public int Port { get; set; }

    [Option('e', "edr", Default = "", HelpText = "EDR plugin to use. Use '--edr ?' to list available plugins on this OS. Default: platform-specific (defender on Windows, logfile on Linux).")]
    public string Edr { get; set; } = "";

    [Option("help", HelpText = "Display this help text")]
    public bool Help { get; set; }
}
