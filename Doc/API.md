# API

## API Endpoints

### Execute Controller

- **POST /api/execute/exec** - Upload and execute a file
  - Supports ZIP, RAR, and ISO file extraction/mounting
  - Parameters:
    - `file`: File to upload (required)
    - `drop_path`: Target directory (optional, defaults to C:\RedEdr\data\)
    - `executable_args`: Command line arguments (optional)
    - `executable_name`: Specific file to execute from archive (optional, defaults to alphabetically first .exe/.bat/.com/.lnk)
    - `execution_mode`: Execution service to use (optional, uses default if not specified)
    - `xor_key`: XOR key for file decryption (optional, 0-255)
  - Response:
    - `status`: "ok", "virus", or "error"
    - `pid`: Process ID (if successful)
    - `message`: Error message (if failed)
  - Response examples:
    ```json
    {"status": "ok", "pid": 1234}
    {"status": "virus", "pid": null}
    {"status": "error", "message": "Failed to execute malware: ..."}
    ```

- **POST /api/execute/kill** - Kill the last executed process
  - Response:
    - `status`: "ok" or "error"
    - `message`: Success/error message
  - Response examples:
    ```json
    {"status": "ok", "message": "Process killed"}
    {"status": "error", "message": "No execution service found - no execution has been run yet"}
    ```

### EDR Controller

- **GET /api/edr/sysinfo** - Get system information and EDR device identifiers
  - Returns:
    - `hostname`: System hostname
    - `osVersion`: Operating system description
    - `deviceId`: Defender SenseId (Windows only, null if not available)
  - Response example:
    ```json
    {
      "hostname": "DESKTOP-ABC123",
      "osVersion": "Microsoft Windows 10.0.19045",
      "deviceId": "12345678-abcd-1234-abcd-123456789012"
    }
    ```

### Logs Controller

- **GET /api/logs/edr** - Get EDR logs with version info
  - Automatically stops EDR collection before retrieving logs
  - Returns:
    - `logs`: EDR event logs
    - `edr_version`: EDR software version
    - `plugin_version`: EDR plugin version
  - Response example:
    ```json
    {
      "logs": "...",
      "edr_version": "4.18.24070.5",
      "plugin_version": "1.0.0"
    }
    ```

- **GET /api/logs/execution** - Get execution logs (PID, stdout, stderr)
  - Returns:
    - `pid`: Process ID
    - `stdout`: Standard output
    - `stderr`: Standard error
  - Response example:
    ```json
    {
      "pid": 1234,
      "stdout": "Hello World",
      "stderr": ""
    }
    ```

- **GET /api/logs/agent** - Get agent internal logs
  - Returns: Array of log messages
  - Response example:
    ```json
    ["[INFO] Agent started", "[INFO] File uploaded: test.exe"]
    ```

- **DELETE /api/logs/agent** - Clear agent logs
  - Returns: Success message
  - Response example:
    ```json
    "Agent logs cleared successfully"
    ```

### Lock Controller

- **POST /api/lock/acquire** - Acquire resource lock
  - Returns: 200 OK if successful, 409 Conflict if already locked
  - Error response (409):
    ```json
    {"status": "error", "message": "Resource is already in use"}
    ```

- **POST /api/lock/release** - Release resource lock
  - Returns: 200 OK

- **GET /api/lock/status** - Check lock status
  - Returns:
    - `in_use`: Boolean indicating if resource is locked
  - Response example:
    ```json
    {"in_use": false}
    ```
