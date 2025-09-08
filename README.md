# DetonatorAgent

A cross-platform ASP.NET Core Web API for log retrieval and command execution.

## Features

- **Cross-platform support**: Runs on both Windows and Linux
- **Platform-specific log retrieval**: 
  - Windows: Event Log integration (ready for implementation)
  - Linux: File-based log reading (ready for implementation)
- **Command execution**: OS-specific command execution capabilities
- **REST API**: Simple endpoints for logs and execution
- **Swagger documentation**: Built-in API documentation

## API Endpoints

### GET /api/logs
Retrieves system logs based on the current platform.

**Response:**
```json
{
  "success": true,
  "data": "log entries...",
  "error": null,
  "timestamp": "2025-09-08T10:30:00Z"
}
```

### POST /api/execute
Executes a command on the current platform.

**Request:**
```json
{
  "command": "dir" // Windows example
}
```

**Response:**
```json
{
  "success": true,
  "data": "command output...",
  "error": null,
  "timestamp": "2025-09-08T10:30:00Z"
}
```

## Running the Application

### Prerequisites
- .NET 8.0 SDK

### Windows
```powershell
dotnet run
```

### Linux
```bash
dotnet run
```

The API will be available at:
- HTTP: http://localhost:5000
- HTTPS: https://localhost:5001
- Swagger UI: https://localhost:5001/swagger

## Architecture

The application uses dependency injection to provide platform-specific implementations:

- `ILogService`: Interface for log retrieval
  - `WindowsLogService`: Windows Event Log implementation (stub)
  - `LinuxLogService`: Linux file-based log implementation (stub)

- `IExecutionService`: Interface for command execution  
  - `WindowsExecutionService`: Windows command execution (stub)
  - `LinuxExecutionService`: Linux command execution (stub)

## Development Notes

This is currently a skeleton implementation with dummy data. To implement real functionality:

1. **Windows Log Service**: Use `System.Diagnostics.EventLog` to read Windows Event Logs
2. **Linux Log Service**: Read from `/var/log/*` files or use `journalctl` for systemd logs
3. **Execution Services**: Use `System.Diagnostics.Process` with platform-specific shells
4. **Security**: Add authentication, input validation, and command whitelisting
5. **Configuration**: Extend appsettings.json for log sources and execution policies
