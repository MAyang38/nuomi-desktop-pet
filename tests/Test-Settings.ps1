param(
    [string]$WorkDirectory
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

$projectDirectory = Split-Path -Parent $PSScriptRoot
$settingsSource = Join-Path $projectDirectory 'PetSettings.cs'
$compilerCandidates = @(
    (Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'),
    (Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe')
)
$compiler = $compilerCandidates | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1
$runDirectory = $null
$exitCode = 1

try {
    if (-not $compiler) { throw 'The .NET Framework 4 C# compiler (csc.exe) was not found.' }
    if (-not (Test-Path -LiteralPath $settingsSource -PathType Leaf)) { throw "Missing source: $settingsSource" }
    if ([string]::IsNullOrWhiteSpace($WorkDirectory)) { $WorkDirectory = [System.IO.Path]::GetTempPath() }
    $workRoot = [System.IO.Path]::GetFullPath($WorkDirectory)
    [System.IO.Directory]::CreateDirectory($workRoot) | Out-Null
    # Every invocation owns a fresh directory, including when a work root is supplied.
    $runDirectory = Join-Path $workRoot ('kitten-settings-tests-' + [Guid]::NewGuid().ToString('N'))
    [System.IO.Directory]::CreateDirectory($runDirectory) | Out-Null
    $runnerSource = Join-Path $runDirectory 'SettingsTestRunner.cs'
    $runnerExecutable = Join-Path $runDirectory 'SettingsTestRunner.exe'
    $source = @'
using System;
using System.Globalization;
using System.IO;
using DesktopKitten;

internal static class SettingsTestRunner
{
    private static string root;
    private static int passed;
    private static int failed;

    private static int Main(string[] args)
    {
        root = args[0];
        Run("missing file and empty file use defaults", TestDefaults);
        Run("legacy three-line settings remain readable", TestLegacy);
        Run("damaged size does not discard boolean settings", TestDamagedSize);
        Run("whitespace and uppercase boolean values are accepted", TestWhitespace);
        Run("NaN and Infinity coordinates are rejected as a pair", TestInvalidCoordinates);
        Run("negative desktop coordinates are preserved", TestNegativeCoordinates);
        Run("five-line save, round trip and atomic overwrite", TestRoundTrip);
        Run("save leaves no temporary files", TestNoTemporaryFiles);
        Console.WriteLine("Settings regression tests: {0} passed, {1} failed.", passed, failed);
        return failed == 0 ? 0 : 1;
    }

    private static void Run(string name, Action test)
    {
        try
        {
            test();
            passed++;
            Console.WriteLine("PASS: " + name);
        }
        catch (Exception error)
        {
            failed++;
            Console.Error.WriteLine("FAIL: " + name + " - " + error);
        }
    }

    private static string Fixture(string name, params string[] lines)
    {
        string path = Path.Combine(root, name + ".txt");
        File.WriteAllLines(path, lines);
        return path;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void CheckDefaults(PetSettings settings)
    {
        Check(settings.Size == .9, "Default size must be 0.9.");
        Check(settings.Roaming, "Default roaming must be enabled.");
        Check(settings.Topmost, "Default topmost must be enabled.");
        Check(!settings.Left.HasValue && !settings.Top.HasValue, "Default position must be unset.");
    }

    private static void TestDefaults()
    {
        string missing = Path.Combine(root, "missing.txt");
        Check(!File.Exists(missing), "Missing-file fixture unexpectedly exists.");
        CheckDefaults(new PetSettings());
        CheckDefaults(PetSettings.Load(missing));
        CheckDefaults(PetSettings.Load(Fixture("empty")));
    }

    private static void TestLegacy()
    {
        PetSettings settings = PetSettings.Load(Fixture("legacy", "1.2", "false", "false"));
        Check(settings.Size == 1.2 && !settings.Roaming && !settings.Topmost, "Three-line values changed.");
        Check(!settings.Left.HasValue && !settings.Top.HasValue, "Legacy settings must not invent a position.");
    }

    private static void TestDamagedSize()
    {
        string[] values = { "broken", "", "0.59", "1.31", "NaN", "Infinity", "-Infinity" };
        for (int i = 0; i < values.Length; i++)
        {
            PetSettings settings = PetSettings.Load(Fixture("bad-size-" + i, values[i], "false", "false"));
            Check(settings.Size == .9, "Invalid size did not fall back: " + values[i]);
            Check(!settings.Roaming && !settings.Topmost, "Invalid size discarded boolean values: " + values[i]);
        }
        Check(PetSettings.Load(Fixture("min-size", "0.6", "true", "true")).Size == .6, "Minimum valid size rejected.");
        Check(PetSettings.Load(Fixture("max-size", "1.3", "true", "true")).Size == 1.3, "Maximum valid size rejected.");
    }

    private static void TestWhitespace()
    {
        CultureInfo previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            PetSettings settings = PetSettings.Load(Fixture("whitespace", " 1.1 ", " FALSE ", " TRUE ", " -12.5 ", " 34.25 "));
            Check(settings.Size == 1.1 && !settings.Roaming && settings.Topmost, "Trimmed or uppercase fields were not read.");
            Check(settings.Left == -12.5 && settings.Top == 34.25, "Position parsing must use invariant culture.");
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    private static void TestInvalidCoordinates()
    {
        string[] invalid = { "NaN", "Infinity", "-Infinity", "not-a-number", "", "1e999" };
        for (int i = 0; i < invalid.Length; i++)
        {
            PetSettings left = PetSettings.Load(Fixture("bad-left-" + i, "1", "false", "true", invalid[i], "25"));
            Check(!left.Left.HasValue && !left.Top.HasValue, "Invalid left must reject the position pair: " + invalid[i]);
            Check(left.Size == 1 && !left.Roaming && left.Topmost, "Invalid left damaged other settings.");
            PetSettings top = PetSettings.Load(Fixture("bad-top-" + i, "1", "true", "false", "25", invalid[i]));
            Check(!top.Left.HasValue && !top.Top.HasValue, "Invalid top must reject the position pair: " + invalid[i]);
            Check(top.Size == 1 && top.Roaming && !top.Topmost, "Invalid top damaged other settings.");
        }
        PetSettings incomplete = PetSettings.Load(Fixture("incomplete-position", "1", "true", "true", "25"));
        Check(!incomplete.Left.HasValue && !incomplete.Top.HasValue, "A position requires both coordinates.");
    }

    private static void TestNegativeCoordinates()
    {
        PetSettings settings = PetSettings.Load(Fixture("negative-position", "0.9", "true", "true", "-1920.5", "-120.25"));
        Check(settings.Left == -1920.5 && settings.Top == -120.25, "Negative monitor coordinates were discarded.");
    }

    private static void TestRoundTrip()
    {
        string path = Path.Combine(root, "round-trip.txt");
        PetSettings original = new PetSettings();
        original.Size = 1.2;
        original.Roaming = false;
        original.Topmost = false;
        original.Left = -1234.5678901234567;
        original.Top = 987.6543210987654;
        CultureInfo previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            original.Save(path);
        }
        finally { CultureInfo.CurrentCulture = previous; }
        string[] lines = File.ReadAllLines(path);
        Check(lines.Length == 5, "Saved settings must contain exactly five lines.");
        Check(lines[0] == "1.2" && lines[1] == "false" && lines[2] == "false", "Legacy fields must use invariant format.");
        Check(lines[3] == original.Left.Value.ToString("R", CultureInfo.InvariantCulture), "Left coordinate lost precision.");
        Check(lines[4] == original.Top.Value.ToString("R", CultureInfo.InvariantCulture), "Top coordinate lost precision.");
        PetSettings loaded = PetSettings.Load(path);
        Check(loaded.Size == original.Size && loaded.Roaming == original.Roaming && loaded.Topmost == original.Topmost, "Round trip changed settings.");
        Check(loaded.Left == original.Left && loaded.Top == original.Top, "Round trip changed the exact coordinates.");
        Check(!File.Exists(path + ".tmp"), "First save left a temporary file.");

        // A second save must replace an existing destination and clear an unset position.
        PetSettings replacement = new PetSettings();
        replacement.Size = .6;
        replacement.Save(path);
        lines = File.ReadAllLines(path);
        Check(lines.Length == 5 && lines[0] == "0.6" && lines[1] == "true" && lines[2] == "true", "Existing settings were not overwritten.");
        Check(lines[3] == "" && lines[4] == "", "Unset position must be saved as two empty lines.");
        loaded = PetSettings.Load(path);
        Check(loaded.Size == .6 && loaded.Roaming && loaded.Topmost, "Replacement did not load.");
        Check(!loaded.Left.HasValue && !loaded.Top.HasValue, "Previous coordinates survived replacement.");
        Check(!File.Exists(path + ".tmp"), "Overwrite left a temporary file.");
    }

    private static void TestNoTemporaryFiles()
    {
        Check(Directory.GetFiles(root, "*.tmp").Length == 0, "A settings temporary file remains in the test directory.");
    }
}
'@
    [System.IO.File]::WriteAllText($runnerSource, $source, [System.Text.Encoding]::UTF8)
    & $compiler '/nologo' '/target:exe' ('/out:' + $runnerExecutable) $settingsSource $runnerSource
    if ($LASTEXITCODE -ne 0) { throw "Test compilation failed (exit $LASTEXITCODE)." }
    & $runnerExecutable $runDirectory
    $exitCode = $LASTEXITCODE
}
catch {
    Write-Error -Message $_.Exception.Message -ErrorAction Continue
    $exitCode = 1
}
finally {
    if ($runDirectory -and (Test-Path -LiteralPath $runDirectory -PathType Container)) {
        # Only this invocation's flat, uniquely named directory is cleaned up.
        Get-ChildItem -LiteralPath $runDirectory -File | ForEach-Object { Remove-Item -LiteralPath $_.FullName -Force }
        Remove-Item -LiteralPath $runDirectory
    }
}

exit $exitCode
