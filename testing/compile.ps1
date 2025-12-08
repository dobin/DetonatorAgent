# Compile C++ files into EXE and DLL
# This script compiles testexe.cpp into testexe.exe and testdll.cpp into testdll.dll

$ErrorActionPreference = "Stop"

# Get the script directory
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

# Define paths
$testExeCpp = Join-Path $scriptDir "testexe.cpp"
$testDllCpp = Join-Path $scriptDir "testdll.cpp"
$testExeOut = Join-Path $scriptDir "testexe.exe"
$testDllOut = Join-Path $scriptDir "testdll.dll"

# Find cl.exe (C++ compiler) using vswhere
$vsWherePath = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
if (-not (Test-Path $vsWherePath)) {
    Write-Error "vswhere.exe not found. Please ensure Visual Studio is installed."
    exit 1
}

$vsPath = & $vsWherePath -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
if (-not $vsPath) {
    Write-Error "Visual Studio with C++ tools not found. Please install Visual Studio with C++ development tools."
    exit 1
}

# Find vcvarsall.bat to set up environment
$vcVarsPath = Join-Path $vsPath "VC\Auxiliary\Build\vcvarsall.bat"
if (-not (Test-Path $vcVarsPath)) {
    Write-Error "vcvarsall.bat not found at: $vcVarsPath"
    exit 1
}

Write-Host "Using Visual Studio at: $vsPath" -ForegroundColor Cyan

# Create a temporary batch file to compile
$tempBat = Join-Path $env:TEMP "compile_cpp.bat"

# Compile testexe.cpp into an executable
Write-Host "`nCompiling testexe.cpp..." -ForegroundColor Yellow
@"
@echo off
call "$vcVarsPath" x64 >nul 2>&1
cl.exe /EHsc /Fe:"$testExeOut" "$testExeCpp"
"@ | Out-File -FilePath $tempBat -Encoding ASCII

& cmd.exe /c $tempBat
if ($LASTEXITCODE -eq 0) {
    Write-Host "Successfully compiled: $testExeOut" -ForegroundColor Green
} else {
    Write-Error "Failed to compile testexe.cpp"
    Remove-Item $tempBat -ErrorAction SilentlyContinue
    exit 1
}

# Clean up intermediate files
Remove-Item (Join-Path $scriptDir "testexe.obj") -ErrorAction SilentlyContinue

# Compile testdll.cpp into a DLL
Write-Host "`nCompiling testdll.cpp..." -ForegroundColor Yellow
@"
@echo off
call "$vcVarsPath" x64 >nul 2>&1
cl.exe /LD /EHsc /Fe:"$testDllOut" "$testDllCpp"
"@ | Out-File -FilePath $tempBat -Encoding ASCII

& cmd.exe /c $tempBat
if ($LASTEXITCODE -eq 0) {
    Write-Host "Successfully compiled: $testDllOut" -ForegroundColor Green
} else {
    Write-Error "Failed to compile testdll.cpp"
    Remove-Item $tempBat -ErrorAction SilentlyContinue
    exit 1
}

# Clean up intermediate files
Remove-Item (Join-Path $scriptDir "testdll.obj") -ErrorAction SilentlyContinue
Remove-Item (Join-Path $scriptDir "testdll.lib") -ErrorAction SilentlyContinue
Remove-Item (Join-Path $scriptDir "testdll.exp") -ErrorAction SilentlyContinue
Remove-Item $tempBat -ErrorAction SilentlyContinue

Write-Host "`nCompilation complete!" -ForegroundColor Green
Write-Host "EXE: $testExeOut"
Write-Host "DLL: $testDllOut"
