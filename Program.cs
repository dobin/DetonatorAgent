using DetonatorAgent.Services;
using DetonatorAgent.Services.Platform;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

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
