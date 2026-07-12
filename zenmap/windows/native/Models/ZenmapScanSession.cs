namespace Zenmap.Windows.Models;

public enum ZenmapScanExecutionMode
{
    NormalUser,
    Administrator,
}

public sealed class ZenmapScanExecutionModeDetail
{
    public ZenmapScanExecutionMode Mode { get; init; } = ZenmapScanExecutionMode.NormalUser;
    public string Reason { get; init; } = "";
}

public enum ZenmapScanLifecycleState
{
    Idle,
    Preparing,
    WaitingForAuthorization,
    Running,
    Stopping,
    Completed,
    Failed,
    Cancelled,
}

public sealed class ZenmapScanPhaseProgress
{
    public double? PortPercent { get; init; }
    public double? ServicePercent { get; init; }
    public double? ScriptPercent { get; init; }
    public string PhaseText { get; init; } = "";

    public static ZenmapScanPhaseProgress Empty { get; } = new();
}

public sealed class ZenmapScanProgressSnapshot
{
    public double? OverallPercent { get; init; }
    public bool IsEstimated { get; init; }
    public string Message { get; init; } = "";
    public string EstimatedCompletionText { get; init; } = "";
    public string ElapsedText { get; init; } = "";
    public ZenmapScanPhaseProgress Phases { get; init; } = ZenmapScanPhaseProgress.Empty;

    public static ZenmapScanProgressSnapshot Empty { get; } = new();
}

public sealed class ZenmapScanCommand
{
    public required string BinaryDisplayName { get; init; }
    public IReadOnlyList<string> Arguments { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Targets { get; init; } = Array.Empty<string>();
    public string? XmlOutputPath { get; init; }

    public string DisplayText
    {
        get
        {
            var joinedArguments = string.Join(' ', Arguments);
            var joinedTargets = string.Join(' ', Targets);
            return string.IsNullOrWhiteSpace(joinedArguments)
                ? $"{BinaryDisplayName} {joinedTargets}"
                : $"{BinaryDisplayName} {joinedArguments} {joinedTargets}";
        }
    }
}

/// <summary>
/// Platform-neutral scan session model aligned with macOS and Linux native front ends.
/// </summary>
public sealed class ZenmapScanSession
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required ZenmapScanCommand Command { get; init; }
    public ZenmapScanExecutionModeDetail ExecutionMode { get; init; } = new();
    public ZenmapScanLifecycleState LifecycleState { get; init; } = ZenmapScanLifecycleState.Idle;
    public ZenmapScanProgressSnapshot Progress { get; init; } = ZenmapScanProgressSnapshot.Empty;
    public DateTimeOffset? StartedAt { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
    public string OutputText { get; init; } = "";
    public string? XmlOutputPath { get; init; }
    public IReadOnlyList<ScannedHost> ParsedHosts { get; init; } = Array.Empty<ScannedHost>();
}
