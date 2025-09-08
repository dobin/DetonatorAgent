using DetonatorAgent.Services;
using DetonatorAgent.Services.Platform;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Register lock service as singleton to maintain state across requests
builder.Services.AddSingleton<ILockService, LockService>();

// Register agent log service as singleton to maintain logs across requests
builder.Services.AddSingleton<AgentLogService>();
builder.Services.AddSingleton<IAgentLogService>(provider => provider.GetRequiredService<AgentLogService>());

// Register platform-specific services
if (OperatingSystem.IsWindows())
{
    builder.Services.AddScoped<ILogService, WindowsLogService>();
    builder.Services.AddScoped<IExecutionService, WindowsExecutionService>();
    builder.Services.AddScoped<IEdrService, WindowsDefenderEdrService>();
}
else
{
    builder.Services.AddScoped<ILogService, LinuxLogService>();
    builder.Services.AddScoped<IExecutionService, LinuxExecutionService>();
    builder.Services.AddScoped<IEdrService, LinuxEdrService>();
}

var app = builder.Build();

// Configure custom logging to capture agent logs
var agentLogService = app.Services.GetRequiredService<AgentLogService>();
var loggerFactory = app.Services.GetRequiredService<ILoggerFactory>();
loggerFactory.AddProvider(new AgentLoggerProvider(agentLogService));

// Add initial startup log
agentLogService.AddLog("DetonatorAgent 0.4 - Starting up");

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
