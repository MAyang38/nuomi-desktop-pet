param(
    [string]$WorkDirectory
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'
$projectDirectory = Split-Path -Parent $PSScriptRoot
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$compiler = Join-Path $framework 'csc.exe'
$runDirectory = $null
$exitCode = 1

try {
    if (-not (Test-Path -LiteralPath $compiler -PathType Leaf)) { throw 'The .NET Framework 4 C# compiler (csc.exe) was not found.' }
    if ([string]::IsNullOrWhiteSpace($WorkDirectory)) { $WorkDirectory = [System.IO.Path]::GetTempPath() }
    $workRoot = [System.IO.Path]::GetFullPath($WorkDirectory)
    [System.IO.Directory]::CreateDirectory($workRoot) | Out-Null
    $runDirectory = Join-Path $workRoot ('kitten-behavior-tests-' + [Guid]::NewGuid().ToString('N'))
    [System.IO.Directory]::CreateDirectory($runDirectory) | Out-Null
    $petExecutable = Join-Path $runDirectory 'KittenPet.exe'
    $runnerExecutable = Join-Path $runDirectory 'BehaviorTestRunner.exe'
    & (Join-Path $projectDirectory 'build.ps1') -OutputPath $petExecutable
    $references = @(
        $petExecutable,
        (Join-Path $framework 'WPF\PresentationCore.dll'),
        (Join-Path $framework 'WPF\PresentationFramework.dll'),
        (Join-Path $framework 'WPF\WindowsBase.dll'),
        (Join-Path $framework 'System.Xaml.dll'),
        (Join-Path $framework 'System.Windows.Forms.dll'),
        (Join-Path $framework 'System.Drawing.dll'),
        (Join-Path $framework 'System.Web.Extensions.dll')
    )
    $compilerArgs = @('/nologo', '/target:exe', '/codepage:65001', '/utf8output', ('/out:' + $runnerExecutable))
    $compilerArgs += $references | ForEach-Object { '/reference:' + $_ }
    $compilerArgs += (Join-Path $PSScriptRoot 'BehaviorTestRunner.cs')
    & $compiler $compilerArgs
    if ($LASTEXITCODE -ne 0) { throw "Behavior test compilation failed (exit $LASTEXITCODE)." }
    # The runner and app assembly share this isolated AppDomain base directory.
    & $runnerExecutable $runDirectory
    $exitCode = $LASTEXITCODE
}
catch {
    Write-Error -Message $_.Exception.Message -ErrorAction Continue
    $exitCode = 1
}
finally {
    if ($runDirectory -and (Test-Path -LiteralPath $runDirectory -PathType Container)) {
        Get-ChildItem -LiteralPath $runDirectory -File | ForEach-Object { Remove-Item -LiteralPath $_.FullName -Force }
        Remove-Item -LiteralPath $runDirectory
    }
}

exit $exitCode
