param([ValidatePattern('^[A-Za-z0-9_-]+\.exe$')][string]$OutputName = 'ClipBoard.exe')
$ErrorActionPreference = 'Stop'
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
if (!(Test-Path (Join-Path $framework 'csc.exe'))) { $framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319' }
$compiler = Join-Path $framework 'csc.exe'
if (!(Test-Path $compiler)) { throw 'The Windows .NET Framework C# compiler was not found. Enable .NET Framework 4.8.' }
$output = Join-Path $PSScriptRoot 'build'
New-Item -ItemType Directory -Path $output -Force | Out-Null
$references = @('System.dll','System.Core.dll','System.Xaml.dll','System.Security.dll','System.Web.Extensions.dll','System.Drawing.dll','System.Windows.Forms.dll') | ForEach-Object { '/reference:' + (Join-Path $framework $_) }
$references += @('WindowsBase.dll','PresentationCore.dll','PresentationFramework.dll') | ForEach-Object { '/reference:' + (Join-Path $framework ('WPF\' + $_)) }
$sources = Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'windows') -Filter '*.cs' | ForEach-Object { $_.FullName }
& $compiler /nologo /target:winexe /platform:anycpu /optimize+ /warn:4 /utf8output ('/out:' + (Join-Path $output $OutputName)) ('/win32icon:' + (Join-Path $PSScriptRoot 'windows\assets\ClipBoard.ico')) ('/win32manifest:' + (Join-Path $PSScriptRoot 'windows\app.manifest')) ('/resource:' + (Join-Path $PSScriptRoot 'windows\assets\ClipboardLogo.png') + ',ClipboardLogo.png') $references $sources
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
Write-Host "Built $output\$OutputName"
