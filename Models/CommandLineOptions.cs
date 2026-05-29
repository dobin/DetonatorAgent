using CommandLine;

namespace DetonatorAgent.Models;

/// <summary>
/// Command line options for DetonatorAgent
/// </summary>
public class CommandLineOptions
{
    [Option('p', "port", Default = 8080, HelpText = "Port number to listen on (1-65535). Default: 8080")]
    public int Port { get; set; }

    [Option('e', "edr", Default = "defender", HelpText = "EDR plugin to use: defender, fibratus, example. Default: defender")]
    public string Edr { get; set; } = "defender";

    [Option("help", HelpText = "Display this help text")]
    public bool Help { get; set; }
}
