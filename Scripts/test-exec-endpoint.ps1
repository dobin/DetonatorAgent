# Test script for the /api/execute/exec endpoint with all parameters
# 
# This script demonstrates how to use all available parameters:
#   - file: The file to execute
#   - path: Target directory to write the file
#   - fileargs: Arguments to pass to the executable
#   - executeFile: Specific file to execute from an archive (for ZIP/ISO)
#   - executiontype: Execution service to use (exec, autoit, autoitexplorer)

param(
    [Parameter(Mandatory=$false)]
    [string]$TestFile = "C:\tools\procexp64.exe",
    
    [Parameter(Mandatory=$false)]
    [string]$Path = "C:\RedEdr\data\",
    
    [Parameter(Mandatory=$false)]
    [string]$FileArgs = "",
    
    [Parameter(Mandatory=$false)]
    [string]$ExecuteFile = "",
    
    [Parameter(Mandatory=$false)]
    [ValidateSet("exec", "autoit", "autoitexplorer", "")]
    [string]$ExecutionType = "exec",
    
    [Parameter(Mandatory=$false)]
    [string]$BaseUrl = "http://localhost:8080"
)

Write-Host "=== Testing /api/execute/exec endpoint ===" -ForegroundColor Green
Write-Host "Test File: $TestFile" -ForegroundColor Yellow
Write-Host "Path: $Path" -ForegroundColor Yellow
Write-Host "File Args: $FileArgs" -ForegroundColor Yellow
Write-Host "Execute File: $ExecuteFile" -ForegroundColor Yellow
Write-Host "Execution Type: $ExecutionType" -ForegroundColor Yellow
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
    "-F", "path=$Path"
)

if ($FileArgs) {
    $curlArgs += "-F"
    $curlArgs += "fileargs=$FileArgs"
}

if ($ExecuteFile) {
    $curlArgs += "-F"
    $curlArgs += "executeFile=$ExecuteFile"
}

if ($ExecutionType) {
    $curlArgs += "-F"
    $curlArgs += "executiontype=$ExecutionType"
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
Write-Host '  .\test-exec-endpoint.ps1 -TestFile "C:\path\to\file.exe" -FileArgs "-arg1 -arg2"' -ForegroundColor Gray
Write-Host ""
Write-Host "With specific execution type:" -ForegroundColor Yellow
Write-Host '  .\test-exec-endpoint.ps1 -TestFile "C:\path\to\file.exe" -ExecutionType "autoit"' -ForegroundColor Gray
Write-Host ""
Write-Host "Execute file from ZIP:" -ForegroundColor Yellow
Write-Host '  .\test-exec-endpoint.ps1 -TestFile "C:\path\to\archive.zip" -ExecuteFile "malware.exe"' -ForegroundColor Gray
