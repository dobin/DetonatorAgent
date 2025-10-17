


## API Endpoints

### Execute Controller
- **POST /api/execute/exec** - Upload and execute a file
  - Supports ZIP file extraction to %LOCALAPPDATA%\Temp
  - Parameters:
    - `file`: File to upload (required)
    - `drop_path`: Target directory (optional, defaults to C:\RedEdr\data\)
    - `executable_args`: Command line arguments (optional)
    - `executable_name`: Specific file to execute from ZIP (optional, defaults to alphabetically first .exe/.bat/.com/.lnk)
- **POST /api/execute/kill** - Kill the last executed process

### Logs Controller
- **GET /api/logs/edr** - Get EDR logs with version info
- **GET /api/logs/execution** - Get execution logs (PID, stdout, stderr)
- **GET /api/logs/agent** - Get agent internal logs
- **DELETE /api/logs/agent** - Clear agent logs

### Lock Controller
- **POST /api/lock/acquire** - Acquire resource lock
- **POST /api/lock/release** - Release resource lock  
- **GET /api/lock/status** - Check lock status
