using DetonatorAgent.Services;
using DetonatorAgent.EdrPlugins;
using DetonatorAgent.Models;
using CommandLine;

var builder = WebApplication.CreateBuilder(args);

// Parse command line arguments
var options = new CommandLineOptions();
var parseResult = Parser.Default.ParseArguments<CommandLineOptions>(args)
    .WithParsed(opts => {
        options.Port = opts.Port;
        options.Edr = opts.Edr;
    })
    .WithNotParsed(errors => {
        // If help was requested or parsing failed, exit
        Environment.Exit(0);
    });

// Validate port range
if (options.Port < 1 || options.Port > 65535)
{
    Console.WriteLine($"Invalid --port value '{options.Port}'. Must be an integer between 1 and 65535.");
    Environment.Exit(1);
}

// Discover available EDR plugins for the current OS via reflection.
var availablePlugins = EdrPluginRegistry.GetAvailablePlugins();
var availableNames = string.Join(", ", availablePlugins.Select(p => p.Name));

// Special sentinel: `--edr ?` lists plugins available on this OS and exits.
if (options.Edr == "?")
{
    Console.WriteLine($"Available EDR plugins on this OS: {availableNames}");
    var defaultName = EdrPluginRegistry.GetDefaultPluginName();
    if (defaultName is not null)
        Console.WriteLine($"Default: {defaultName}");
    Environment.Exit(0);
}

// If --edr was not provided, fall back to the platform default.
if (string.IsNullOrEmpty(options.Edr))
{
    var defaultName = EdrPluginRegistry.GetDefaultPluginName();
    if (defaultName is null)
    {
        Console.WriteLine($"No default EDR plugin is defined for this OS. Available: {availableNames}");
        Environment.Exit(1);
    }
    options.Edr = defaultName;
}

// Resolve the plugin type; error out if the name is unknown on this OS.
var edrPluginType = EdrPluginRegistry.Resolve(options.Edr);
if (edrPluginType is null)
{
    Console.WriteLine($"Unknown EDR plugin '{options.Edr}'. Available on this OS: {availableNames}");
    Environment.Exit(1);
}

// Configure Kestrel to accept larger request bodies (100MB)
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.Limits.MaxRequestBodySize = 104857600; // 100 MB
});

// Configure console logging to use simple format
builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(options =>
{
    options.SingleLine = true;
    options.IncludeScopes = false;
    options.TimestampFormat = "yyyy-MM-dd HH:mm:ss ";
});

// Configure port from command line argument
builder.WebHost.UseUrls($"http://0.0.0.0:{options.Port}");

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Register lock service as singleton to maintain state across requests
builder.Services.AddSingleton<ILockService, LockService>();

// Register execution tracking service as singleton to maintain state across requests
builder.Services.AddSingleton<ExecutionTrackingService>();

// Register agent log service as singleton to maintain logs across requests
builder.Services.AddSingleton<AgentLogService>();
builder.Services.AddSingleton<IAgentLogService>(provider => provider.GetRequiredService<AgentLogService>());

// Register platform-specific execution services and EDR plugin.
// The extension methods are defined in platform-specific files
// (WindowsStartupExtensions.cs / LinuxStartupExtensions.cs) that are
// conditionally compiled via the csproj, so no #if directives are needed here.
var edrService = options.Edr.ToLower();

// Register execution services (platform-specific). EDR plugin is registered
// centrally below, regardless of OS, using the type resolved via reflection.
#if WINDOWS_BUILD
// WindowsStartupExtensions.cs is excluded from Linux builds, so its methods
// must be guarded here. The Linux branch uses only cross-platform types.
if (OperatingSystem.IsWindows()) {
    builder.Services.AddWindowsExecutionServices();
}
#else
builder.Services.AddLinuxExecutionServices();
#endif

// Register the selected EDR plugin (discovered via EdrPluginRegistry).
builder.Services.AddSingleton(typeof(IEdrService), edrPluginType!);

var app = builder.Build();

// Configure custom logging to capture agent logs
var agentLogService = app.Services.GetRequiredService<AgentLogService>();
var loggerFactory = app.Services.GetRequiredService<ILoggerFactory>();
loggerFactory.AddProvider(new AgentLoggerProvider(agentLogService));

// Add initial startup log
agentLogService.AddLog("DetonatorAgent - Starting up");
agentLogService.AddLog($"EDR Plugin: {edrService}");

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment()) {
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Serve the vanilla HTML/JS web UI from wwwroot/
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseAuthorization();
app.MapControllers();

// Add lifetime events for logging
var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
lifetime.ApplicationStopping.Register(() =>
{
    agentLogService.AddLog("DetonatorAgent - Shutting down");
});

try
{
    agentLogService.AddLog("DetonatorAgent - Running");
    await app.RunAsync();
    agentLogService.AddLog("DetonatorAgent - Stopped normally");
    return 0;
}
catch (Exception ex)
{
    agentLogService.AddLog($"DetonatorAgent - FATAL ERROR: {ex.GetType().Name}: {ex.Message}");
    agentLogService.AddLog($"Stack trace: {ex.StackTrace}");
    Console.WriteLine($"Fatal error: {ex}");
    return 1;
}
