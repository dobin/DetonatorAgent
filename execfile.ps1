# Simple workflow script for DetonatorAgent
# Usage: .\scan-file.ps1 -File "C:\path\to\executable.exe" [-DropPath "C:\target\path\"] [-ExecutableArgs "arg1 arg2"] [-ExecutableName "file.exe"] [-ExecutionMode "exec"]
# 
# Parameters:
#   -File: Path to the file to execute (required)
#   -DropPath: Target directory to write the file (optional, default: C:\RedEdr\data\)
#   -ExecutableArgs: Arguments to pass to the executable (optional)
#   -ExecutableName: Specific file to execute from an archive (optional, for ZIP/ISO files)
#   -ExecutionMode: Execution service to use: "exec", "autoit"
#   -BaseUrl: Base URL of the DetonatorAgent API (optional, default: http://localhost:8080)

param(
    [Parameter(Mandatory=$true, HelpMessage="Path to the file to execute")]
    [string]$File,
    
    [Parameter(Mandatory=$false, HelpMessage="Target directory to write the file")]
    [string]$DropPath = "C:\RedEdr\data\",
    
    [Parameter(Mandatory=$false, HelpMessage="Arguments to pass to the executable")]
    [string]$ExecutableArgs = "",
    
    [Parameter(Mandatory=$false, HelpMessage="Specific file to execute from an archive")]
    [string]$ExecutableName = "",
    
    [Parameter(Mandatory=$false, HelpMessage="Execution service type (exec, autoit)")]
    [ValidateSet("exec", "autoit", "clickfix")]
    [string]$ExecutionMode = "autoit",
    
    [Parameter(Mandatory=$false, HelpMessage="Base URL of the DetonatorAgent API")]
    [string]$BaseUrl = "http://localhost:8080"
)

# Validate input file exists
if (-not (Test-Path $File)) {
    Write-Host "Error: File not found: $File" -ForegroundColor Red
    exit 1
}

Write-Host "File: $File"
Write-Host "Drop Path: $DropPath"
Write-Host "Executable Args: $ExecutableArgs"
Write-Host "Executable Name: $ExecutableName"
Write-Host "Execution Mode: $ExecutionMode"
Write-Host "Base URL: $BaseUrl"
Write-Host ""

# Acquire Lock
#Write-Host "Acquiring lock..." -ForegroundColor Cyan
$lockResponse = curl.exe -s -X POST "$BaseUrl/api/lock/acquire"
$lockStatus = $LASTEXITCODE
if ($lockStatus -ne 0) {
    Write-Host "Error: Failed to acquire lock (curl exit code: $lockStatus)" -ForegroundColor Red
    exit 1
}
#Write-Host "Lock acquired successfully" -ForegroundColor Green

try {
    # Execute file
    $fileName = [System.IO.Path]::GetFileName($File)
    
    # Build curl command with all parameters
    $curlArgs = @(
        "-s",
        "-X", "POST",
        "$BaseUrl/api/execute/exec",
        "-F", "file=@$File",
        "-F", "drop_path=$DropPath"
    )
    # Add optional executable_args parameter
    if ($ExecutableArgs) {
        $curlArgs += "-F"
        $curlArgs += "executable_args=$ExecutableArgs"
    }
    # Add optional executable_name parameter
    if ($ExecutableName) {
        $curlArgs += "-F"
        $curlArgs += "executable_name=$ExecutableName"
    }
    # Add optional execution_mode parameter
    if ($ExecutionMode) {
        $curlArgs += "-F"
        $curlArgs += "execution_mode=$ExecutionMode"
    }
    
    # Execute
    Write-Host "Executing file..."
    $execResponse = & curl.exe $curlArgs
    $execStatus = $LASTEXITCODE
    if ($execStatus -ne 0) {
        Write-Host "Error: Failed to execute file (curl exit code: $execStatus)" -ForegroundColor Red
        throw "Execution failed"
    }
    # Parse and check the execution response
    $status = ""
    try {
        $responseObj = $execResponse | ConvertFrom-Json
        $status = $responseObj.status # "virus", "ok", "error"
    }
    catch {
        Write-Host "Warning: Failed to parse execution response" -ForegroundColor Red
        Write-Host "Response: $execResponse" -ForegroundColor Gray
    }
    Write-Host "File Execution status: $status" -ForegroundColor Yellow

    # Wait, if execution was successful
    if ($status -eq "ok") {
        Write-Host "Execution running, waiting 10 seconds..."
        for ($i = 10; $i -gt 0; $i--) {
            Write-Host "." -NoNewline -ForegroundColor Gray
            Start-Sleep -Seconds 1
        }
        Write-Host ""
    }
    # Kill process if it ran
    if ($status -eq "ok") {
        Write-Host "Killing process..."
        $killResponse = curl.exe -s -X POST "$BaseUrl/api/execute/kill"
        $killStatus = $LASTEXITCODE
        if ($killStatus -ne 0) {
            Write-Host "Warning: Failed to kill process (curl exit code: $killStatus)" -ForegroundColor Yellow
        }
    }

    # If its detected on file write, still wait a bit to allow EDR to process
    if ($status -eq "virus") {
        Write-Host -NoNewline "`nWait a bit for EDR to process before getting EDR alerts " -ForegroundColor Gray
        for ($i = 3; $i -gt 0; $i--) {
            Write-Host "." -NoNewline -ForegroundColor Gray
            Start-Sleep -Seconds 1
        }
        Write-Host ""
    }
    
    # Retrieve logs
    #Write-Host "`nRetrieving logs..." -ForegroundColor Cyan
    
    # Get EDR logs if executed, or detected on file write
    if ($status -eq "virus" -or $status -eq "ok") {
        #Write-Host "  Getting EDR logs..." -ForegroundColor Gray
        $edrLogs = curl.exe -s -X GET "$BaseUrl/api/logs/edr"
        if ($LASTEXITCODE -eq 0) {
            #Write-Host "  EDR logs retrieved" -ForegroundColor Green
            
            # Parse and display alerts as table
            try {
                $edrResponse = $edrLogs | ConvertFrom-Json
                if ($edrResponse.alerts -and $edrResponse.alerts.Count -gt 0) {
                    #Write-Host "EDR Alerts:" -ForegroundColor Yellow
                    $edrResponse.alerts | Select-Object title, severity, category | Format-Table -AutoSize
                } else {
                    Write-Host "No alerts found" -ForegroundColor Green
                }
            }
            catch {
                Write-Host "  Warning: Failed to parse EDR logs" -ForegroundColor Yellow
                Write-Host "  Response: $edrLogs" -ForegroundColor Gray
            }

        } else {
            Write-Host "  Warning: Failed to retrieve EDR logs" -ForegroundColor Yellow
        }
    }
    
    # Get execution logs if executed
    if ($status -eq "ok") {
        #Write-Host "  Getting execution logs..." -ForegroundColor Gray
        $execLogs = curl.exe -s -X GET "$BaseUrl/api/logs/execution"
        if ($LASTEXITCODE -eq 0) {
            #Write-Host "  Execution logs retrieved" -ForegroundColor Green
            #Write-Host "  Response: $execLogs" -ForegroundColor Gray
        } else {
            Write-Host "  Warning: Failed to retrieve execution logs" -ForegroundColor Yellow
        }
    }
    
    # Get agent logs
    #Write-Host "  Getting agent logs..." -ForegroundColor Gray
    $agentLogs = curl.exe -s -X GET "$BaseUrl/api/logs/agent"
    if ($LASTEXITCODE -ne 0) {
        Write-Host "  Warning: Failed to retrieve agent logs" -ForegroundColor Yellow
    }
}
finally {
    # Release Lock (always execute)
    #Write-Host "`nReleasing lock..." -ForegroundColor Cyan
    $unlockResponse = curl.exe -s -X POST "$BaseUrl/api/lock/release"
    $unlockStatus = $LASTEXITCODE
    if ($unlockStatus -ne 0) {
        Write-Host "Warning: Failed to release lock (curl exit code: $unlockStatus)" -ForegroundColor Yellow
    }
}
