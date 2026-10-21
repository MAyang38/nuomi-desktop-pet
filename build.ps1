param([string]$OutputPath = (Join-Path $PSScriptRoot 'KittenPet.exe'))
$ErrorActionPreference = 'Stop'
$framework = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319'
$compiler = Join-Path $framework 'csc.exe'
$references = @(
    (Join-Path $framework 'WPF/PresentationCore.dll'),
    (Join-Path $framework 'WPF/PresentationFramework.dll'),
    (Join-Path $framework 'WPF/WindowsBase.dll'),
    (Join-Path $framework 'System.Xaml.dll'),
    (Join-Path $framework 'System.Windows.Forms.dll'),
    (Join-Path $framework 'System.Drawing.dll')
)
$compilerArgs = @('/nologo', '/target:winexe', '/optimize+', '/codepage:65001', '/utf8output', ('/out:' + $OutputPath))
$compilerArgs += $references | ForEach-Object { '/reference:' + $_ }
$compilerArgs += @((Join-Path $PSScriptRoot 'KittenPet.cs'), (Join-Path $PSScriptRoot 'CatArt.cs'), (Join-Path $PSScriptRoot 'CatSleepArt.cs'))
& $compiler $compilerArgs
if ($LASTEXITCODE -ne 0) { throw 'KittenPet compilation failed.' }
Write-Output 'KittenPet.exe built successfully.'
