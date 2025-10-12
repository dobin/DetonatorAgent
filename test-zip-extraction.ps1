# Test script for ZIP file extraction functionality

Write-Host "=== Testing ZIP file extraction functionality ==="
Write-Host ""

# Create a simple test executable (batch file)
$testBatchContent = @"
@echo off
echo Hello from extracted batch file!
echo Current directory: %CD%
echo Command line args: %*
pause
"@

$tempDir = [System.IO.Path]::GetTempPath()
$testBatchFile = Join-Path $tempDir "test_executable.bat"
$testZipFile = Join-Path $tempDir "test_archive.zip"

try {
    # Create test batch file
    Write-Host "Creating test batch file: $testBatchFile"
    Set-Content -Path $testBatchFile -Value $testBatchContent -Encoding ASCII
    
    # Create ZIP file containing the batch file
    Write-Host "Creating ZIP file: $testZipFile"
    if (Test-Path $testZipFile) {
        Remove-Item $testZipFile -Force
    }
    
    # Use PowerShell's built-in compression
    Compress-Archive -Path $testBatchFile -DestinationPath $testZipFile -Force
    
    # Verify ZIP file was created
    if (Test-Path $testZipFile) {
        Write-Host "ZIP file created successfully"
        Write-Host "ZIP file size: $((Get-Item $testZipFile).Length) bytes"
    } else {
        Write-Host "ERROR: Failed to create ZIP file"
        exit 1
    }
    
    # Test the API endpoint with the ZIP file
    Write-Host ""
    Write-Host "Testing API endpoint with ZIP file..."
    
    # Test 1: Without specifying executeFile (should find alphabetically first)
    Write-Host "Test 1: Auto-detection of executable file"
    $curlCommand1 = "curl.exe -X POST http://localhost:8080/api/execute/exec -F `"file=@$testZipFile`" -F `"path=C:\temp\`""
    Write-Host "Running: $curlCommand1"
    Invoke-Expression $curlCommand1
    
    Write-Host ""
    Write-Host "Waiting 3 seconds before next test..."
    Start-Sleep -Seconds 3
    
    # Test 2: With specifying executeFile
    Write-Host "Test 2: Specified executable file"
    $curlCommand2 = "curl.exe -X POST http://localhost:8080/api/execute/exec -F `"file=@$testZipFile`" -F `"path=C:\temp\`" -F `"executeFile=test_executable.bat`""
    Write-Host "Running: $curlCommand2"
    Invoke-Expression $curlCommand2
    
    Write-Host ""
    Write-Host "=== Test completed ==="
    
} catch {
    Write-Host "ERROR: $($_.Exception.Message)"
    exit 1
} finally {
    # Cleanup
    if (Test-Path $testBatchFile) {
        Remove-Item $testBatchFile -Force
        Write-Host "Cleaned up test batch file"
    }
    if (Test-Path $testZipFile) {
        Remove-Item $testZipFile -Force  
        Write-Host "Cleaned up test ZIP file"
    }
}