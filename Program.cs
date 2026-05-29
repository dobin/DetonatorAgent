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

// Validate EDR plugin
var validEdrPlugins = new[] { "defender", "fibratus", "example" };
if (!validEdrPlugins.Contains(options.Edr.ToLower()))
{
    Console.WriteLine($"Unknown EDR plugin '{options.Edr}'. Valid options: {string.Join(", ", validEdrPlugins)}");
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

// Register platform-specific services
if (OperatingSystem.IsWindows()) {
    // Register all Windows execution service implementations
    builder.Services.AddSingleton<IExecutionService, WindowsExecutionServiceExec>();
    builder.Services.AddSingleton<IExecutionService, WindowsExecutionServiceAutoit>();
    builder.Services.AddSingleton<IExecutionService, WindowsExecutionServiceClickfix>();
}
else {
    // Register Linux execution service implementation
    builder.Services.AddSingleton<IExecutionService, LinuxExecutionService>();
}

// Register EDR service based on command line argument
var edrService = options.Edr.ToLower();

if (OperatingSystem.IsWindows()) {
    switch (edrService) {
        case "defender":
            builder.Services.AddSingleton<IEdrService, DefenderEdrPlugin>();
            break;
        case "fibratus":
            builder.Services.AddSingleton<IEdrService, FibratusEdrPlugin>();
            break;
        case "example":
            builder.Services.AddSingleton<IEdrService, ExampleEdrPlugin>();
            break;
        default:
            Console.WriteLine($"Unknown EDR service '{edrService}' specified. Use 'defender' or 'fibratus'");
            return 1;
    }
}
else {
    switch (edrService) {
        default:
            builder.Services.AddSingleton<IEdrService, ExampleEdrPlugin>();
            break;
    }
}

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
