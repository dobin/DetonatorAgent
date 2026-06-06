# Load config
$config = Get-Content -Raw -Path ".\azure_config.json" | ConvertFrom-Json

# build
Write-Output "build..."
dotnet build --configuration Release

# cleanup
$sourceDir = "bin\Release\net8.0"
$destination = "$env:TEMP\detonatoragent.zip"

if (Test-Path $destination) {
  Write-Output "cleanup: $destination"
  Get-ChildItem $destination | Remove-Item -Force -ErrorAction SilentlyContinue
}

# make a zip (exclude logs directory)
Write-Output "zip: $sourceDir to $destination (excluding logs/)"
Get-ChildItem -Path $sourceDir -Exclude "logs" | Compress-Archive -DestinationPath $destination -Force

# Upload zip as blob
Write-Output "upload: $destination"
az storage blob upload `
  --account-name $($config.StorageAccount) `
  --container-name $($config.ContainerName) `
  --name $($config.BlobName) `
  --file $destination `
  --sas-token """$($config.SasToken)""" `
  --overwrite

# delete zip
Write-Output "cleanup..."
Get-ChildItem $destination | Remove-Item -Force
