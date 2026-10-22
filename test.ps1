$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'build.ps1') -OutputName 'ClipBoard-tests.exe'
$testFolder = Join-Path $PSScriptRoot 'artifacts'
$testProcess = Start-Process -FilePath (Join-Path $PSScriptRoot 'build\ClipBoard-tests.exe') -ArgumentList @('--self-test', ('"' + $testFolder + '"')) -WindowStyle Hidden -Wait -PassThru
Get-Content -LiteralPath (Join-Path $testFolder 'self-test-results.txt')
if ($testProcess.ExitCode -ne 0) { throw 'ClipBoard self-tests failed.' }
