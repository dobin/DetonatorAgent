# Simple workflow script for DetonatorAgent
# Usage: .\scan-file.ps1 -File "C:\path\to\executable.exe" [-DropPath "C:\target\path\"] [-ExecutableArgs "arg1 arg2"] [-ExecutableName "file.exe"] [-ExecutionMode "exec"]
# 
# Parameters:
#   -File: Path to the file to execute (required)
#   -DropPath: Target directory to write the file (optional, default: C:\RedEdr\data\)
#   -ExecutableArgs: Arguments to pass to the executable (optional)
#   -ExecutableName: Specific file to execute from an archive (optional, for ZIP/ISO files)
#   -ExecutionMode: Execution service to use: "exec", "autoit", "autoitexplorer" (optional, default: "exec")
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
    
    [Parameter(Mandatory=$false, HelpMessage="Execution service type (exec, autoit, autoitexplorer)")]
    [ValidateSet("exec", "autoit", "autoitexplorer", "")]
    [string]$ExecutionMode = "exec",
    
    [Parameter(Mandatory=$false, HelpMessage="Base URL of the DetonatorAgent API")]
    [string]$BaseUrl = "http://localhost:8080"
)

# Validate input file exists
if (-not (Test-Path $File)) {
    Write-Host "Error: File not found: $File" -ForegroundColor Red
    exit 1
}

Write-Host "=== DetonatorAgent Workflow ===" -ForegroundColor Green
Write-Host "File: $File" -ForegroundColor Yellow
Write-Host "Drop Path: $DropPath" -ForegroundColor Yellow
Write-Host "Executable Args: $ExecutableArgs" -ForegroundColor Yellow
Write-Host "Executable Name: $ExecutableName" -ForegroundColor Yellow
Write-Host "Execution Mode: $ExecutionMode" -ForegroundColor Yellow
Write-Host "Base URL: $BaseUrl" -ForegroundColor Yellow
Write-Host ""

# Step 1: Acquire Lock
Write-Host "Step 1: Acquiring lock..." -ForegroundColor Cyan
$lockResponse = curl.exe -s -X POST "$BaseUrl/api/lock/acquire"
$lockStatus = $LASTEXITCODE

if ($lockStatus -ne 0) {
    Write-Host "Error: Failed to acquire lock (curl exit code: $lockStatus)" -ForegroundColor Red
    exit 1
}

Write-Host "Lock acquired successfully" -ForegroundColor Green

try {
    # Step 2: Execute file
    Write-Host "`nStep 2: Executing file..." -ForegroundColor Cyan
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
    
    $execResponse = & curl.exe $curlArgs
    $execStatus = $LASTEXITCODE
    
    if ($execStatus -ne 0) {
        Write-Host "Error: Failed to execute file (curl exit code: $execStatus)" -ForegroundColor Red
        throw "Execution failed"
    }
    
    Write-Host "File executed successfully" -ForegroundColor Green
    Write-Host "Response: $execResponse" -ForegroundColor Gray
    
    # Step 3: Wait 10 seconds
    Write-Host "`nStep 3: Waiting 10 seconds..." -ForegroundColor Cyan
    for ($i = 10; $i -gt 0; $i--) {
        Write-Host "  $i seconds remaining..." -ForegroundColor Gray
        Start-Sleep -Seconds 1
    }
    Write-Host "Wait completed" -ForegroundColor Green
    
    # Step 4: Retrieve logs
    Write-Host "`nStep 4: Retrieving logs..." -ForegroundColor Cyan
    
    # Get EDR logs
    Write-Host "  Getting EDR logs..." -ForegroundColor Gray
    $edrLogs = curl.exe -s -X GET "$BaseUrl/api/logs/edr"
    if ($LASTEXITCODE -eq 0) {
        Write-Host "  EDR logs retrieved" -ForegroundColor Green
        Write-Host "  Response: $edrLogs" -ForegroundColor Gray
    } else {
        Write-Host "  Warning: Failed to retrieve EDR logs" -ForegroundColor Yellow
    }
    
    # Get execution logs
    Write-Host "  Getting execution logs..." -ForegroundColor Gray
    $execLogs = curl.exe -s -X GET "$BaseUrl/api/logs/execution"
    if ($LASTEXITCODE -eq 0) {
        Write-Host "  Execution logs retrieved" -ForegroundColor Green
        #Write-Host "  Response: $execLogs" -ForegroundColor Gray
    } else {
        Write-Host "  Warning: Failed to retrieve execution logs" -ForegroundColor Yellow
    }
    
    # Get agent logs
    Write-Host "  Getting agent logs..." -ForegroundColor Gray
    $agentLogs = curl.exe -s -X GET "$BaseUrl/api/logs/agent"
    if ($LASTEXITCODE -eq 0) {
        Write-Host "  Agent logs retrieved" -ForegroundColor Green
        #Write-Host "  Response: $agentLogs" -ForegroundColor Gray
    } else {
        Write-Host "  Warning: Failed to retrieve agent logs" -ForegroundColor Yellow
    }
    
    # Step 5: Kill process
    Write-Host "`nStep 5: Killing process..." -ForegroundColor Cyan
    $killResponse = curl.exe -s -X POST "$BaseUrl/api/execute/kill"
    $killStatus = $LASTEXITCODE
    
    if ($killStatus -eq 0) {
        Write-Host "Process killed successfully" -ForegroundColor Green
        Write-Host "Response: $killResponse" -ForegroundColor Gray
    } else {
        Write-Host "Warning: Failed to kill process (curl exit code: $killStatus)" -ForegroundColor Yellow
    }
}
finally {
    # Step 6: Release Lock (always execute)
    Write-Host "`nStep 6: Releasing lock..." -ForegroundColor Cyan
    $unlockResponse = curl.exe -s -X POST "$BaseUrl/api/lock/release"
    $unlockStatus = $LASTEXITCODE
    
    if ($unlockStatus -eq 0) {
        Write-Host "Lock released successfully" -ForegroundColor Green
    } else {
        Write-Host "Warning: Failed to release lock (curl exit code: $unlockStatus)" -ForegroundColor Yellow
    }
}

Write-Host "`n=== Workflow completed ===" -ForegroundColor Green
