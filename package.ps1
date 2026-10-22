$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'build.ps1')
$distFolder = Join-Path $PSScriptRoot 'dist'
$portableFolder = Join-Path $distFolder 'ClipBoard-Windows'
New-Item -ItemType Directory -Path $portableFolder -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'build\ClipBoard.exe') -Destination $portableFolder
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'windows\README.md') -Destination (Join-Path $portableFolder 'README.md')
Compress-Archive -LiteralPath (Join-Path $portableFolder 'ClipBoard.exe'),(Join-Path $portableFolder 'README.md') -DestinationPath (Join-Path $distFolder 'ClipBoard-Windows-portable.zip') -Force
Write-Host "Portable app: $distFolder\ClipBoard-Windows-portable.zip"
