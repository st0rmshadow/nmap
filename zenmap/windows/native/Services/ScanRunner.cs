using Zenmap.Windows.Models;

namespace Zenmap.Windows.Services;

public sealed class ScanRequest
{
    public required string TargetText { get; init; }
    public required string ArgumentsText { get; init; }
    public bool AutoAddStatsEvery { get; init; } = true;
    public string StatsEveryValue { get; init; } = "1s";
    public bool AutoAddVerbose { get; init; }
    public string NmapBinary { get; init; } = "nmap";
    public bool AllowPrivileged { get; init; }
}

public sealed class ScanRunner : IDisposable
{
    private readonly Action<string> _onOutput;
    private readonly Action<string> _onStatus;
    private readonly Action<ZenmapScanLifecycleState, int?> _onLifecycle;
    private readonly Action<IReadOnlyList<ScannedHost>> _onHosts;
    private readonly Action<ScanProgressState>? _onProgress;
    private readonly SynchronizationContext? _syncContext;

    private System.Diagnostics.Process? _process;
    private PrivilegedScanHandle? _privilegedHandle;
    private System.Threading.Timer? _privilegedPollTimer;
    private string? _xmlPath;
    private ScanProgressTracker? _progressTracker;

    public ScanRunner(
        Action<string> onOutput,
        Action<string> onStatus,
        Action<ZenmapScanLifecycleState, int?> onLifecycle,
        Action<IReadOnlyList<ScannedHost>> onHosts,
        Action<ScanProgressState>? onProgress = null,
        SynchronizationContext? syncContext = null)
    {
        _onOutput = onOutput;
        _onStatus = onStatus;
        _onLifecycle = onLifecycle;
        _onHosts = onHosts;
        _onProgress = onProgress;
        _syncContext = syncContext ?? SynchronizationContext.Current;
    }

    public bool IsRunning => _process is { HasExited: false } || _privilegedHandle is not null;

    public string CommandPreview { get; private set; } = "";

    public string? XmlPath => _xmlPath;

    public void Run(ScanRequest request)
    {
        if (IsRunning)
        {
            return;
        }

        var targets = ShellUtils.SplitTargets(request.TargetText);
        if (targets.Count == 0)
        {
            PostOutput("\nNo target specified.\n");
            PostStatus("Idle");
            return;
        }

        var binary = NmapPathResolver.ResolveNmapBinary(request.NmapBinary);
        if (binary is null)
        {
            PostOutput("\nFailed to run nmap: no executable nmap was found.\n");
            PostStatus("Failed");
            PostLifecycle(ZenmapScanLifecycleState.Failed, null);
            return;
        }

        var args = ShellUtils.ShellSplit(request.ArgumentsText).ToList();
        if (request.AutoAddStatsEvery && !args.Any(arg => arg == "--stats-every" || arg.StartsWith("--stats-every=", StringComparison.Ordinal)))
        {
            args.Add("--stats-every");
            args.Add(request.StatsEveryValue);
        }

        if (request.AutoAddVerbose && !ContainsVerboseFlag(args))
        {
            args.Add("-v");
        }

        _xmlPath = Path.Combine(Path.GetTempPath(), $"zenmap-{Guid.NewGuid():N}.xml");
        args.Add("-oX");
        args.Add(_xmlPath);
        args.AddRange(targets);

        var privilege = ScanPrivilegeEvaluator.Evaluate(args);
        if (privilege.Mode == ZenmapScanExecutionMode.Administrator)
        {
            if (!request.AllowPrivileged)
            {
                PostOutput($"\n{privilege.Reason}\n");
                PostOutput("Approve the privilege prompt to run this scan with administrator rights.\n");
                PostStatus("Privileges required");
                PostLifecycle(ZenmapScanLifecycleState.WaitingForAuthorization, null);
                return;
            }

            RunPrivilegedScan(args, _xmlPath, binary);
            return;
        }

        RunUserScan(request, args, _xmlPath, binary);
    }

    public void Stop()
    {
        if (_process is { HasExited: false })
        {
            PostStatus("Stopping");
            PostLifecycle(ZenmapScanLifecycleState.Stopping, null);
            try
            {
                _process.Kill(entireProcessTree: true);
            }
            catch (Exception)
            {
            }

            return;
        }

        if (_privilegedHandle is not null)
        {
            PostStatus("Stopping privileged scan");
            PostLifecycle(ZenmapScanLifecycleState.Stopping, null);
            PostOutput("\n\nStopping privileged scan...\n");
            PrivilegedScanRunner.StopPrivilegedScan(_privilegedHandle.WrapperProcessId, _privilegedHandle.ChildPidPath);
        }
    }

    public void Dispose()
    {
        _privilegedPollTimer?.Dispose();
        if (_process is { HasExited: false })
        {
            try
            {
                _process.Kill(entireProcessTree: true);
            }
            catch (Exception)
            {
            }
        }

        _process?.Dispose();
    }

    private void RunUserScan(ScanRequest request, List<string> args, string xmlPath, string binary)
    {
        CommandPreview = string.Join(' ', new[] { binary }.Concat(args).Select(ShellUtils.ShellEscape));
        _progressTracker = new ScanProgressTracker(request.ArgumentsText, request.TargetText);
        _progressTracker.Start();
        PostStatus("Running");
        PostLifecycle(ZenmapScanLifecycleState.Running, null);
        PostOutput($"Running {CommandPreview}...\n");
        PostOutput($"Using nmap: {binary}\n");
        PostOutput($"Using NMAPDIR: {NmapPathResolver.ResolveNmapDataDirectory(binary)}\n");
        PostOutput("Privilege mode: normal user\n");
        PostOutput($"XML output: {xmlPath}\n\n");

        var startInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = binary,
            Arguments = string.Join(' ', args.Select(ShellUtils.ShellEscape)),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        startInfo.Environment["NMAPDIR"] = NmapPathResolver.ResolveNmapDataDirectory(binary);

        try
        {
            _process = System.Diagnostics.Process.Start(startInfo)
                ?? throw new InvalidOperationException("Failed to start nmap process.");
        }
        catch (Exception error)
        {
            PostOutput($"\nFailed to start nmap: {error.Message}\n");
            PostStatus("Failed");
            PostLifecycle(ZenmapScanLifecycleState.Failed, null);
            _process = null;
            return;
        }

        _process.OutputDataReceived += (_, argsEvent) =>
        {
            if (!string.IsNullOrEmpty(argsEvent.Data))
            {
                PostOutput(argsEvent.Data + "\n");
                EmitProgress(argsEvent.Data + "\n");
            }
        };
        _process.ErrorDataReceived += (_, argsEvent) =>
        {
            if (!string.IsNullOrEmpty(argsEvent.Data))
            {
                PostOutput(argsEvent.Data + "\n");
                EmitProgress(argsEvent.Data + "\n");
            }
        };
        _process.EnableRaisingEvents = true;
        _process.Exited += (_, _) => FinishScan(_process.ExitCode);
        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();
    }

    private void RunPrivilegedScan(List<string> args, string xmlPath, string binary)
    {
        CommandPreview = string.Join(' ', new[] { binary }.Concat(args).Select(ShellUtils.ShellEscape));
        _progressTracker = new ScanProgressTracker(string.Join(' ', args), "");
        _progressTracker.Start();
        PostStatus("Running as administrator");
        PostLifecycle(ZenmapScanLifecycleState.Running, null);
        PostOutput($"Running {CommandPreview}...\n");
        PostOutput($"Using nmap: {binary}\n");
        PostOutput($"Using NMAPDIR: {NmapPathResolver.ResolveNmapDataDirectory(binary)}\n");
        PostOutput("Privilege mode: administrator\n");
        PostOutput("Administrator authorization requested. Running nmap elevated...\n");

        try
        {
            _privilegedHandle = PrivilegedScanRunner.StartPrivilegedScan(args, binary);
        }
        catch (PrivilegedRunnerException error)
        {
            PostOutput($"\nFailed to start privileged nmap: {error.Message}\n");
            PostStatus("Privileged scan failed");
            PostLifecycle(ZenmapScanLifecycleState.Failed, 1);
            return;
        }

        PostOutput($"Privileged output log: {_privilegedHandle.LogPath}\n");
        PostOutput($"Privileged wrapper PID: {_privilegedHandle.WrapperProcessId}\n\n");

        long offset = 0;
        _privilegedPollTimer = new System.Threading.Timer(_ =>
        {
            if (_privilegedHandle is null)
            {
                return;
            }

            var (text, newOffset) = PrivilegedScanRunner.ReadNewText(_privilegedHandle.LogPath, offset);
            offset = newOffset;
            if (!string.IsNullOrEmpty(text))
            {
                PostOutput(text);
                EmitProgress(text);
            }

            var done = File.Exists(_privilegedHandle.DonePath);
            var running = PrivilegedScanRunner.IsProcessRunning(_privilegedHandle.WrapperProcessId);
            if (!done && running)
            {
                return;
            }

            (text, newOffset) = PrivilegedScanRunner.ReadNewText(_privilegedHandle.LogPath, offset);
            offset = newOffset;
            if (!string.IsNullOrEmpty(text))
            {
                PostOutput(text);
                EmitProgress(text);
            }

            var exitStatus = PrivilegedScanRunner.ReadExitStatus(_privilegedHandle.StatusPath) ?? 1;
            _privilegedHandle = null;
            _privilegedPollTimer?.Dispose();
            _privilegedPollTimer = null;
            FinishScan(exitStatus);
        }, null, TimeSpan.FromMilliseconds(750), TimeSpan.FromMilliseconds(750));
    }

    private void FinishScan(int exitStatus)
    {
        _process?.Dispose();
        _process = null;
        _privilegedPollTimer?.Dispose();
        _privilegedPollTimer = null;
        _privilegedHandle = null;

        var hosts = !string.IsNullOrWhiteSpace(_xmlPath) ? XmlParsing.ParseNmapXml(_xmlPath!) : Array.Empty<ScannedHost>();
        PostHosts(hosts);
        PostOutput($"\nExit status: {exitStatus}\n");

        if (exitStatus == 0)
        {
            PostStatus("Completed");
            PostLifecycle(ZenmapScanLifecycleState.Completed, exitStatus);
        }
        else
        {
            PostStatus($"Failed ({exitStatus})");
            PostLifecycle(ZenmapScanLifecycleState.Failed, exitStatus);
        }
    }

    private void EmitProgress(string text)
    {
        if (_progressTracker is null || _onProgress is null)
        {
            return;
        }

        PostProgress(_progressTracker.Consume(text));
    }

    private static bool ContainsVerboseFlag(IEnumerable<string> arguments) =>
        arguments.Any(argument =>
            argument is "-v" or "-vv" or "-d" or "--verbose" ||
            argument.StartsWith("-v", StringComparison.Ordinal) ||
            argument.StartsWith("-d", StringComparison.Ordinal) ||
            argument.StartsWith("--verbose=", StringComparison.Ordinal));

    private void PostOutput(string text) => Post(() => _onOutput(text));
    private void PostStatus(string status) => Post(() => _onStatus(status));
    private void PostLifecycle(ZenmapScanLifecycleState state, int? exitStatus) => Post(() => _onLifecycle(state, exitStatus));
    private void PostHosts(IReadOnlyList<ScannedHost> hosts) => Post(() => _onHosts(hosts));
    private void PostProgress(ScanProgressState progress) => Post(() => _onProgress?.Invoke(progress));

    private void Post(Action action)
    {
        if (_syncContext is not null)
        {
            _syncContext.Post(_ => action(), null);
        }
        else
        {
            action();
        }
    }
}
