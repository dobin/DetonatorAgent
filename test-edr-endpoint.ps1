# Test script for EDR endpoint
param(
    [string]$BaseUrl = "http://localhost:5000"
)

Write-Host "Testing EDR logs endpoint..." -ForegroundColor Green

try {
    $response = Invoke-RestMethod -Uri "$BaseUrl/api/logs/edr" -Method GET -ContentType "application/json"
    
    Write-Host "Response received:" -ForegroundColor Yellow
    Write-Host "Success: $($response.success)" -ForegroundColor Cyan
    Write-Host "EDR Version: $($response.data.edrVersion)" -ForegroundColor Cyan
    Write-Host "Plugin Version: $($response.data.pluginVersion)" -ForegroundColor Cyan
    Write-Host "Logs Length: $($response.data.logs.Length) characters" -ForegroundColor Cyan
    Write-Host "Timestamp: $($response.timestamp)" -ForegroundColor Cyan
    
    # Show first 500 characters of logs
    if ($response.data.logs.Length -gt 0) {
        $logPreview = $response.data.logs.Substring(0, [Math]::Min(500, $response.data.logs.Length))
        Write-Host "`nLog Preview (first 500 chars):" -ForegroundColor Yellow
        Write-Host $logPreview -ForegroundColor Gray
        if ($response.data.logs.Length -gt 500) {
            Write-Host "... (truncated)" -ForegroundColor Gray
        }
    }
    
} catch {
    Write-Host "Error testing EDR endpoint: $_" -ForegroundColor Red
    Write-Host "Exception Details: $($_.Exception.Message)" -ForegroundColor Red
}
