# Load config
$config = Get-Content -Raw -Path ".\azure_config.json" | ConvertFrom-Json

# build
Write-Output "build..."
dotnet build --configuration Release

# cleanup
$source = "bin\Release\net8.0\*"
$destination = "$env:TEMP\detonatoragent.zip"

if (Test-Path $destination) {
  Write-Output "cleanup: $destination"
  Get-ChildItem $destination | Remove-Item -Force -ErrorAction SilentlyContinue
}

# make a zip
Write-Output "zip: $source to $destination"
Compress-Archive -Path $source -DestinationPath $destination -Force

# Upload zip as blob
Write-Output "upload: $destination"
az storage blob upload `
  --account-name $($config.StorageAccount) `
  --container-name $($config.ContainerName) `
  --name $($config.BlobName) `
  --file $destination `
  --sas-token "`"$($config.SasToken)`"" `
  --overwrite

# delete zip
Write-Output "cleanup..."
Get-ChildItem $destination | Remove-Item -Force
