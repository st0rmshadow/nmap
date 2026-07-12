namespace Zenmap.Windows.Services;

public sealed class PrivilegedScanHandle
{
    public required int WrapperProcessId { get; init; }
    public required string LogPath { get; init; }
    public required string StatusPath { get; init; }
    public required string DonePath { get; init; }
    public required string ChildPidPath { get; init; }
}

public sealed class PrivilegedRunnerException : Exception
{
    public PrivilegedRunnerException(string message) : base(message)
    {
    }
}

public static class PrivilegedScanRunner
{
    public static PrivilegedScanHandle StartPrivilegedScan(
        IReadOnlyList<string> arguments,
        string nmapExecutable)
    {
        if (string.IsNullOrWhiteSpace(nmapExecutable) || !File.Exists(nmapExecutable))
        {
            throw new PrivilegedRunnerException("No executable nmap binary was found.");
        }

        var suffix = Guid.NewGuid().ToString("N");
        var tempDirectory = Path.GetTempPath();
        var logPath = Path.Combine(tempDirectory, $"zenmap-{suffix}-privileged.log");
        var statusPath = Path.Combine(tempDirectory, $"zenmap-{suffix}-privileged.status");
        var donePath = Path.Combine(tempDirectory, $"zenmap-{suffix}-privileged.done");
        var childPidPath = Path.Combine(tempDirectory, $"zenmap-{suffix}-privileged.childpid");
        var scriptPath = Path.Combine(tempDirectory, $"zenmap-{suffix}-privileged.ps1");

        var command = string.Join(' ', new[] { nmapExecutable }.Concat(arguments).Select(ShellUtils.ShellEscape));
        var nmapDir = ShellUtils.ShellEscape(NmapPathResolver.ResolveNmapDataDirectory(nmapExecutable));
        var script = string.Join(Environment.NewLine,
        [
            "$ErrorActionPreference = 'Stop'",
            $"Remove-Item -Force -ErrorAction SilentlyContinue '{statusPath}','{donePath}','{childPidPath}'",
            $"$env:NMAPDIR = {nmapDir.Trim('"')}",
            "$job = Start-Job -ScriptBlock {",
            $"  & {ShellUtils.ShellEscape(nmapExecutable)} {string.Join(' ', arguments.Select(ShellUtils.ShellEscape))} *>&1 | Out-File -FilePath '{logPath}' -Encoding utf8",
            "}",
            "$child = $job.Id",
            $"Set-Content -Path '{childPidPath}' -Value $child",
            "try {",
            "  Wait-Job $job | Out-Null",
            "  $code = if ((Receive-Job $job -ErrorAction SilentlyContinue) -ne $null) { $LASTEXITCODE } else { 0 }",
            "} catch { $code = 1 }",
            "if ($code -eq $null) { $code = 0 }",
            $"Set-Content -Path '{statusPath}' -Value $code",
            $"New-Item -ItemType File -Path '{donePath}' -Force | Out-Null",
            "exit $code",
        ]);

        File.WriteAllText(scriptPath, script);

        var startInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"",
            UseShellExecute = true,
            Verb = "runas",
            CreateNoWindow = false,
        };

        var process = System.Diagnostics.Process.Start(startInfo)
            ?? throw new PrivilegedRunnerException("Failed to start elevated PowerShell wrapper.");

        return new PrivilegedScanHandle
        {
            WrapperProcessId = process.Id,
            LogPath = logPath,
            StatusPath = statusPath,
            DonePath = donePath,
            ChildPidPath = childPidPath,
        };
    }

    public static bool IsProcessRunning(int processId)
    {
        if (processId <= 0)
        {
            return false;
        }

        try
        {
            var process = System.Diagnostics.Process.GetProcessById(processId);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    public static (string Text, long NewOffset) ReadNewText(string path, long offset)
    {
        if (!File.Exists(path))
        {
            return ("", offset);
        }

        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        stream.Seek(offset, SeekOrigin.Begin);
        using var reader = new StreamReader(stream);
        var text = reader.ReadToEnd();
        return (text, stream.Position);
    }

    public static int? ReadExitStatus(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        return int.TryParse(File.ReadAllText(path).Trim(), out var status) ? status : null;
    }

    public static void StopPrivilegedScan(int wrapperProcessId, string? childPidPath)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(childPidPath) && File.Exists(childPidPath))
            {
                var childText = File.ReadAllText(childPidPath).Trim();
                if (int.TryParse(childText, out var childPid))
                {
                    TryKill(childPid);
                }
            }

            TryKill(wrapperProcessId);
        }
        catch (Exception)
        {
        }
    }

    public static bool RequiresElevation(ZenmapScanExecutionModeDetail requirement) =>
        requirement.Mode == Models.ZenmapScanExecutionMode.Administrator;

    private static void TryKill(int processId)
    {
        try
        {
            var process = System.Diagnostics.Process.GetProcessById(processId);
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception)
        {
        }
    }
}
