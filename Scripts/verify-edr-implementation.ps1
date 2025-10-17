# Simple test for checking if the EDR endpoint exists
Write-Host "Testing if EDR endpoint exists in controller..." -ForegroundColor Green

$controllerPath = "C:\Users\dobin\Repos\DetonatorAgent\Controllers\LogsController.cs"
$controllerContent = Get-Content $controllerPath -Raw

if ($controllerContent -match 'HttpGet\("edr"\)') {
    Write-Host "✓ EDR endpoint found in LogsController" -ForegroundColor Green
} else {
    Write-Host "✗ EDR endpoint NOT found in LogsController" -ForegroundColor Red
}

if ($controllerContent -match 'EdrLogsResponse') {
    Write-Host "✓ EdrLogsResponse model usage found" -ForegroundColor Green
} else {
    Write-Host "✗ EdrLogsResponse model usage NOT found" -ForegroundColor Red
}

if ($controllerContent -match 'IEdrService') {
    Write-Host "✓ IEdrService dependency found" -ForegroundColor Green
} else {
    Write-Host "✗ IEdrService dependency NOT found" -ForegroundColor Red
}

Write-Host "`nChecking if services are registered..." -ForegroundColor Yellow
$programPath = "C:\Users\dobin\Repos\DetonatorAgent\Program.cs"
$programContent = Get-Content $programPath -Raw

if ($programContent -match 'WindowsDefenderEdrService') {
    Write-Host "✓ WindowsDefenderEdrService registration found" -ForegroundColor Green
} else {
    Write-Host "✗ WindowsDefenderEdrService registration NOT found" -ForegroundColor Red
}

if ($programContent -match 'LinuxEdrService') {
    Write-Host "✓ LinuxEdrService registration found" -ForegroundColor Green
} else {
    Write-Host "✗ LinuxEdrService registration NOT found" -ForegroundColor Red
}

Write-Host "`nChecking if Windows service integration exists..." -ForegroundColor Yellow
$windowsServicePath = "C:\Users\dobin\Repos\DetonatorAgent\Services\Platform\WindowsServices.cs"
$windowsServiceContent = Get-Content $windowsServicePath -Raw

if ($windowsServiceContent -match 'StartCollectionAsync') {
    Write-Host "✓ EDR StartCollectionAsync call found in WindowsExecutionService" -ForegroundColor Green
} else {
    Write-Host "✗ EDR StartCollectionAsync call NOT found in WindowsExecutionService" -ForegroundColor Red
}

Write-Host "`nImplementation Summary:" -ForegroundColor Cyan
Write-Host "======================" -ForegroundColor Cyan
Write-Host "• IEdrService interface created for multi-EDR support" -ForegroundColor White
Write-Host "• WindowsDefenderEdrService implements Windows Event Log collection" -ForegroundColor White
Write-Host "• LinuxEdrService provides stub implementation for cross-platform support" -ForegroundColor White
Write-Host "• /api/logs/edr endpoint added to LogsController" -ForegroundColor White
Write-Host "• EDR collection starts after malware writing on Windows" -ForegroundColor White
Write-Host "• Endpoint stops collection and returns logs with version info" -ForegroundColor White

Write-Host "`nNext steps to test:" -ForegroundColor Yellow
Write-Host "1. Run 'dotnet run' to start the server" -ForegroundColor Gray
Write-Host "2. Upload malware via /api/execute/file to trigger EDR collection" -ForegroundColor Gray
Write-Host "3. Call GET /api/logs/edr to stop collection and get logs" -ForegroundColor Gray
