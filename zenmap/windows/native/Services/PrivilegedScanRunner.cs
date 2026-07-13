using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using Zenmap.Windows.Models;

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

    public PrivilegedRunnerException(string message, Exception innerException)
        : base(message, innerException)
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
        var errPath = Path.Combine(tempDirectory, $"zenmap-{suffix}-privileged.err");
        var statusPath = Path.Combine(tempDirectory, $"zenmap-{suffix}-privileged.status");
        var donePath = Path.Combine(tempDirectory, $"zenmap-{suffix}-privileged.done");
        var childPidPath = Path.Combine(tempDirectory, $"zenmap-{suffix}-privileged.childpid");
        var scriptPath = Path.Combine(tempDirectory, $"zenmap-{suffix}-privileged.ps1");

        var nmapDir = NmapPathResolver.ResolveNmapDataDirectory(nmapExecutable);
        var workingDir = Path.GetDirectoryName(nmapExecutable) ?? nmapDir;
        // Pass a single pre-quoted argument string. Windows PowerShell 5.1's
        // Start-Process -ArgumentList mishandles string[] (joins/re-parses args).
        var argumentLine = string.Join(' ', arguments.Select(ShellUtils.ShellEscape));

        // Run nmap directly in the elevated process (same pattern as Linux pkexec / macOS
        // osascript wrappers). Avoid Start-Job: it stores a job id instead of a PID,
        // drops parent env vars, and does not report nmap's exit code reliably.
        var script = new StringBuilder();
        script.AppendLine("$ErrorActionPreference = 'Stop'");
        script.AppendLine(
            $"Remove-Item -Force -ErrorAction SilentlyContinue {PsSingleQuote(statusPath)}, {PsSingleQuote(donePath)}, {PsSingleQuote(childPidPath)}, {PsSingleQuote(errPath)}");
        script.AppendLine($"$env:NMAPDIR = {PsSingleQuote(nmapDir)}");
        script.AppendLine($"$nmapExe = {PsSingleQuote(nmapExecutable)}");
        script.AppendLine($"$workingDir = {PsSingleQuote(workingDir)}");
        script.AppendLine($"$logPath = {PsSingleQuote(logPath)}");
        script.AppendLine($"$errPath = {PsSingleQuote(errPath)}");
        script.AppendLine($"$argumentLine = {PsSingleQuote(argumentLine)}");
        script.AppendLine(
            "$p = Start-Process -FilePath $nmapExe -ArgumentList $argumentLine -WorkingDirectory $workingDir " +
            "-PassThru -WindowStyle Hidden -RedirectStandardOutput $logPath -RedirectStandardError $errPath");
        script.AppendLine("if ($null -eq $p) { throw 'Failed to start nmap.' }");
        script.AppendLine($"Set-Content -Path {PsSingleQuote(childPidPath)} -Value $p.Id");
        script.AppendLine("try {");
        script.AppendLine("  Wait-Process -Id $p.Id");
        script.AppendLine("  $p.Refresh()");
        script.AppendLine("  $code = $p.ExitCode");
        script.AppendLine("} catch { $code = 1 }");
        script.AppendLine("if (Test-Path -LiteralPath $errPath) {");
        script.AppendLine("  Get-Content -LiteralPath $errPath -ErrorAction SilentlyContinue | Add-Content -LiteralPath $logPath");
        script.AppendLine("  Remove-Item -Force -ErrorAction SilentlyContinue $errPath");
        script.AppendLine("}");
        script.AppendLine("if ($null -eq $code) { $code = 0 }");
        script.AppendLine($"Set-Content -Path {PsSingleQuote(statusPath)} -Value $code");
        script.AppendLine($"New-Item -ItemType File -Path {PsSingleQuote(donePath)} -Force | Out-Null");
        script.AppendLine("exit $code");

        File.WriteAllText(scriptPath, script.ToString());

        var startInfo = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"",
            UseShellExecute = true,
            Verb = "runas",
            WindowStyle = ProcessWindowStyle.Hidden,
        };

        try
        {
            var process = Process.Start(startInfo)
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
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            throw new PrivilegedRunnerException("Administrator authorization was canceled.", ex);
        }
        catch (Win32Exception ex)
        {
            throw new PrivilegedRunnerException($"Failed to start elevated PowerShell wrapper: {ex.Message}", ex);
        }
    }

    public static bool IsProcessRunning(int processId)
    {
        if (processId <= 0)
        {
            return false;
        }

        try
        {
            var process = Process.GetProcessById(processId);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (Win32Exception)
        {
            // Elevated child: non-elevated callers can see the PID but not query HasExited.
            return true;
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
        var pids = new List<int>();
        try
        {
            if (!string.IsNullOrWhiteSpace(childPidPath) && File.Exists(childPidPath))
            {
                var childText = File.ReadAllText(childPidPath).Trim();
                if (int.TryParse(childText, out var childPid) && childPid > 0)
                {
                    pids.Add(childPid);
                }
            }

            if (wrapperProcessId > 0)
            {
                pids.Add(wrapperProcessId);
            }

            // Prefer a local kill first (works if we somehow have rights).
            foreach (var pid in pids)
            {
                if (TryKillLocal(pid))
                {
                    continue;
                }
            }

            // Elevated nmap cannot be killed from a medium-IL GUI process; elevate taskkill.
            if (pids.Count > 0 && pids.Any(IsProcessRunning))
            {
                TryElevatedTaskKill(pids);
            }
        }
        catch (Exception)
        {
        }
    }

    public static bool RequiresElevation(ZenmapScanExecutionModeDetail requirement) =>
        requirement.Mode == Models.ZenmapScanExecutionMode.Administrator;

    private static string PsSingleQuote(string value) =>
        "'" + value.Replace("'", "''", StringComparison.Ordinal) + "'";

    private static bool TryKillLocal(int processId)
    {
        try
        {
            var process = Process.GetProcessById(processId);
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }

            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static void TryElevatedTaskKill(IReadOnlyList<int> processIds)
    {
        var filters = string.Join(" ", processIds.Select(pid => $"/PID {pid}"));
        var startInfo = new ProcessStartInfo
        {
            FileName = "taskkill.exe",
            Arguments = $"{filters} /T /F",
            UseShellExecute = true,
            Verb = "runas",
            WindowStyle = ProcessWindowStyle.Hidden,
        };

        try
        {
            using var process = Process.Start(startInfo);
            process?.WaitForExit(15_000);
        }
        catch (Exception)
        {
        }
    }
}
