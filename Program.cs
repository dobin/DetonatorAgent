using DetonatorAgent.Services;
using DetonatorAgent.EdrPlugins;

var builder = WebApplication.CreateBuilder(args);

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

// Configure port from command line argument or use default from appsettings.json (8080)
var portArg = args.FirstOrDefault(arg => arg.StartsWith("--port="))?.Split('=')[1];
if (!string.IsNullOrEmpty(portArg))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{portArg}");
}

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
var edrService = args.FirstOrDefault(arg => arg.StartsWith("--edr="))?.Split('=')[1]?.ToLower() ?? "defender";

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
agentLogService.AddLog("DetonatorAgent 0.4 - Starting up");
agentLogService.AddLog($"EDR Plugin: {edrService}");

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment()) {
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
