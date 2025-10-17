# Test script for the /api/execute/exec endpoint with all parameters
# 
# This script demonstrates how to use all available parameters:
#   - file: The file to execute
#   - drop_path: Target directory to write the file
#   - executable_args: Arguments to pass to the executable
#   - executable_name: Specific file to execute from an archive (for ZIP/ISO)
#   - execution_mode: Execution service to use (exec, autoit, autoitexplorer)

param(
    [Parameter(Mandatory=$false)]
    [string]$TestFile = "C:\tools\procexp64.exe",
    
    [Parameter(Mandatory=$false)]
    [string]$DropPath = "C:\RedEdr\data\",
    
    [Parameter(Mandatory=$false)]
    [string]$ExecutableArgs = "",
    
    [Parameter(Mandatory=$false)]
    [string]$ExecutableName = "",
    
    [Parameter(Mandatory=$false)]
    [ValidateSet("exec", "autoit", "autoitexplorer", "")]
    [string]$ExecutionMode = "exec",
    
    [Parameter(Mandatory=$false)]
    [string]$BaseUrl = "http://localhost:8080"
)

Write-Host "=== Testing /api/execute/exec endpoint ===" -ForegroundColor Green
Write-Host "Test File: $TestFile" -ForegroundColor Yellow
Write-Host "Drop Path: $DropPath" -ForegroundColor Yellow
Write-Host "Executable Args: $ExecutableArgs" -ForegroundColor Yellow
Write-Host "Executable Name: $ExecutableName" -ForegroundColor Yellow
Write-Host "Execution Mode: $ExecutionMode" -ForegroundColor Yellow
Write-Host ""

# Check if test file exists
if (-not (Test-Path $TestFile)) {
    Write-Host "Error: Test file not found: $TestFile" -ForegroundColor Red
    Write-Host "Please specify a valid file path using -TestFile parameter" -ForegroundColor Yellow
    exit 1
}

# Build curl command with all parameters
$curlArgs = @(
    "-X", "POST",
    "$BaseUrl/api/execute/exec",
    "-F", "file=@$TestFile",
    "-F", "drop_path=$DropPath"
)

if ($ExecutableArgs) {
    $curlArgs += "-F"
    $curlArgs += "executable_args=$ExecutableArgs"
}

if ($ExecutableName) {
    $curlArgs += "-F"
    $curlArgs += "executable_name=$ExecutableName"
}

if ($ExecutionMode) {
    $curlArgs += "-F"
    $curlArgs += "execution_mode=$ExecutionMode"
}

Write-Host "Executing curl command..." -ForegroundColor Cyan
Write-Host "curl.exe $($curlArgs -join ' ')" -ForegroundColor Gray
Write-Host ""

$response = & curl.exe $curlArgs
$exitCode = $LASTEXITCODE

Write-Host "Response:" -ForegroundColor Cyan
Write-Host $response -ForegroundColor White

if ($exitCode -eq 0) {
    Write-Host "`nTest completed successfully!" -ForegroundColor Green
} else {
    Write-Host "`nTest failed with exit code: $exitCode" -ForegroundColor Red
}

Write-Host "`n=== Examples ===" -ForegroundColor Green
Write-Host "Basic execution with default (exec):" -ForegroundColor Yellow
Write-Host '  .\test-exec-endpoint.ps1 -TestFile "C:\path\to\file.exe"' -ForegroundColor Gray
Write-Host ""
Write-Host "With arguments:" -ForegroundColor Yellow
Write-Host '  .\test-exec-endpoint.ps1 -TestFile "C:\path\to\file.exe" -ExecutableArgs "-arg1 -arg2"' -ForegroundColor Gray
Write-Host ""
Write-Host "With specific execution type:" -ForegroundColor Yellow
Write-Host '  .\test-exec-endpoint.ps1 -TestFile "C:\path\to\file.exe" -ExecutionMode "autoit"' -ForegroundColor Gray
Write-Host ""
Write-Host "Execute file from ZIP:" -ForegroundColor Yellow
Write-Host '  .\test-exec-endpoint.ps1 -TestFile "C:\path\to\archive.zip" -ExecutableName "malware.exe"' -ForegroundColor Gray
