using System.Text.RegularExpressions;

namespace Zenmap.Windows.Services;

public sealed class ScanProgressState
{
    public double? OverallPercent { get; set; }
    public bool IsEstimated { get; set; }
    public string Message { get; set; } = "";
    public string EstimatedCompletionText { get; set; } = "";
    public string ElapsedText { get; set; } = "";
    public string PhaseText { get; set; } = "";
}

public sealed class ScanProgressTracker
{
    private readonly string _arguments;
    private readonly string _target;
    private string _buffer = "";
    private DateTimeOffset? _startedAt;

    public ScanProgressState State { get; private set; } = new();

    public ScanProgressTracker(string arguments, string target)
    {
        _arguments = arguments.ToLowerInvariant();
        _target = target;
    }

    public void Start()
    {
        _startedAt = DateTimeOffset.Now;
        _buffer = "";
        State = new ScanProgressState { Message = "Waiting for Nmap progress" };
    }

    public ScanProgressState Consume(string text)
    {
        _buffer = (_buffer + text)[Math.Max(0, _buffer.Length + text.Length - 20000)..];
        var normalized = _buffer.Replace('\r', '\n');
        foreach (var line in normalized.Split('\n'))
        {
            var trimmed = line.Trim();
            if (!string.IsNullOrEmpty(trimmed))
            {
                ConsumeLine(trimmed);
            }
        }

        UpdateElapsed();
        return State;
    }

    private void ConsumeLine(string line)
    {
        var percentText = ProgressPercentText(line);
        if (percentText is not null &&
            double.TryParse(percentText, out var phasePercent))
        {
            phasePercent = Math.Clamp(phasePercent, 0, 100);
            var overall = OverallProgressPercent(line, phasePercent);
            if (overall is not null)
            {
                State.IsEstimated = false;
                State.OverallPercent = Math.Max(State.OverallPercent ?? 0, overall.Value.Percent);
                State.Message = overall.Value.Message;
                State.PhaseText = overall.Value.PhaseMessage;
                UpdateEta(State.OverallPercent.Value);
            }
            else
            {
                State.IsEstimated = true;
                var elapsed = ElapsedSeconds();
                var estimatedDuration = EstimatedScanDurationSeconds();
                var estimated = Math.Min(95, Math.Max(State.OverallPercent ?? 1, (elapsed / estimatedDuration) * 100));
                State.OverallPercent = estimated;
                State.Message = $"Overall {estimated:0}% estimated";
                State.PhaseText = $"Phase: Nmap {phasePercent:0.0}%";
                UpdateEta(estimated);
            }
        }
        else if (ProgressFloorPercent(line) is { } floorPercent)
        {
            State.IsEstimated = true;
            State.OverallPercent = Math.Max(State.OverallPercent ?? 0, floorPercent);
            State.Message = $"Overall {State.OverallPercent:0}% estimated";
            if (string.IsNullOrEmpty(State.PhaseText))
            {
                State.PhaseText = "Phase: waiting for Nmap timing";
            }

            UpdateEta(State.OverallPercent.Value);
        }

        var etcMatch = Regex.Match(line, @"ETC:\s*[^()]+");
        if (etcMatch.Success)
        {
            State.EstimatedCompletionText = etcMatch.Value.Trim();
        }

        var remainingMatch = Regex.Match(line, @"\([^)]*remaining\)");
        if (remainingMatch.Success)
        {
            var remainingText = remainingMatch.Value.Trim('(', ')');
            if (string.IsNullOrEmpty(State.EstimatedCompletionText))
            {
                State.EstimatedCompletionText = remainingText;
            }
            else if (!State.EstimatedCompletionText.Contains(remainingText, StringComparison.Ordinal))
            {
                State.EstimatedCompletionText += $" {remainingText}";
            }
        }

        if ((line.StartsWith("Stats:", StringComparison.Ordinal) ||
             line.Contains("Timing:", StringComparison.Ordinal) ||
             line.StartsWith("Initiating ", StringComparison.Ordinal) ||
             line.StartsWith("Completed ", StringComparison.Ordinal) ||
             line.StartsWith("Scanning ", StringComparison.Ordinal) ||
             line.StartsWith("Discovered ", StringComparison.Ordinal) ||
             line.StartsWith("Nmap scan report", StringComparison.Ordinal)) &&
            percentText is null)
        {
            State.PhaseText = line;
        }
    }

    private void UpdateElapsed()
    {
        var elapsed = (int)ElapsedSeconds();
        State.ElapsedText = $"Elapsed {elapsed / 60}:{elapsed % 60:00}";
        if (State.OverallPercent is null || State.IsEstimated)
        {
            var estimatedDuration = EstimatedScanDurationSeconds();
            var estimated = Math.Min(95, Math.Max(State.OverallPercent ?? 1, (ElapsedSeconds() / estimatedDuration) * 100));
            State.OverallPercent = estimated;
            State.IsEstimated = true;
            State.Message = $"Overall {estimated:0}% estimated";
            if (string.IsNullOrEmpty(State.PhaseText))
            {
                State.PhaseText = "Phase: waiting for Nmap timing";
            }

            UpdateEta(estimated);
        }
        else if (State.Message == "Waiting for Nmap progress" && ElapsedSeconds() >= 5)
        {
            State.Message = "Nmap is running";
        }
    }

    private void UpdateEta(double percent)
    {
        if (percent <= 0 || percent >= 100 || _startedAt is null)
        {
            return;
        }

        var elapsed = ElapsedSeconds();
        if (elapsed <= 0)
        {
            return;
        }

        var totalEstimated = elapsed / (percent / 100.0);
        var remainingSeconds = Math.Max(0, (int)(totalEstimated - elapsed));
        var completion = DateTime.Now.AddSeconds(remainingSeconds);
        State.EstimatedCompletionText =
            $"ETA {completion:HH:mm} ({remainingSeconds / 60}:{remainingSeconds % 60:00} remaining)";
    }

    private double ElapsedSeconds() =>
        _startedAt is null ? 0 : Math.Max(0, (DateTimeOffset.Now - _startedAt.Value).TotalSeconds);

    private double EstimatedScanDurationSeconds()
    {
        if (_arguments.Contains("-su", StringComparison.Ordinal))
        {
            return 420;
        }

        if (_arguments.Split(' ').Contains("-a") || _arguments.Contains(" -a ", StringComparison.Ordinal))
        {
            return 180;
        }

        if (_arguments.Contains("-sv", StringComparison.Ordinal))
        {
            return 120;
        }

        if (_arguments.Contains("-sn", StringComparison.Ordinal))
        {
            return 45;
        }

        return _target.Contains('/') ? 240 : 90;
    }

    private static string? ProgressPercentText(string line)
    {
        var lower = line.ToLowerInvariant();
        var aboutIndex = lower.IndexOf("about", StringComparison.Ordinal);
        if (aboutIndex < 0)
        {
            return null;
        }

        var percentIndex = line.IndexOf('%', aboutIndex);
        if (percentIndex < 0)
        {
            return null;
        }

        var candidate = line[(aboutIndex + "About".Length)..percentIndex];
        var digits = new string(candidate.Where(character => char.IsDigit(character) || character == '.').ToArray());
        return string.IsNullOrEmpty(digits) ? null : digits;
    }

    private static (double Percent, string Message, string PhaseMessage)? OverallProgressPercent(string line, double phasePercent)
    {
        if (line.Contains("Connect Scan Timing:", StringComparison.Ordinal) ||
            line.Contains("SYN Stealth Scan Timing:", StringComparison.Ordinal))
        {
            var overall = Math.Min(15 + (phasePercent * 0.50), 65);
            return (overall, $"Overall {overall:0}%", $"Phase: port scan {phasePercent:0.0}%");
        }

        if (line.Contains("Service scan Timing:", StringComparison.Ordinal))
        {
            var overall = Math.Min(65 + (phasePercent * 0.15), 80);
            return (overall, $"Overall {overall:0}%", $"Phase: service scan {phasePercent:0.0}%");
        }

        if (line.Contains("NSE Timing:", StringComparison.Ordinal))
        {
            var overall = Math.Min(80 + (phasePercent * 0.16), 96);
            return (overall, $"Overall {overall:0}%", $"Phase: script scan {phasePercent:0.0}%");
        }

        return null;
    }

    private static double? ProgressFloorPercent(string line)
    {
        if (line.StartsWith("Completed Connect Scan", StringComparison.Ordinal) ||
            line.StartsWith("Completed SYN Stealth Scan", StringComparison.Ordinal))
        {
            return 65;
        }

        if (line.StartsWith("Completed Service scan", StringComparison.Ordinal))
        {
            return 80;
        }

        if (line.StartsWith("Nmap done", StringComparison.Ordinal))
        {
            return 98;
        }

        if (line.StartsWith("Nmap scan report", StringComparison.Ordinal))
        {
            return null;
        }

        if (line.Contains("NSE Timing:", StringComparison.Ordinal) ||
            line.StartsWith("NSE: Script scanning", StringComparison.Ordinal))
        {
            return 85;
        }

        if (line.Contains("Service scan Timing:", StringComparison.Ordinal) ||
            line.Contains("undergoing Service Scan", StringComparison.Ordinal) ||
            line.StartsWith("Initiating Service scan", StringComparison.Ordinal))
        {
            return 70;
        }

        if (line.Contains("Connect Scan Timing:", StringComparison.Ordinal) ||
            line.Contains("SYN Stealth Scan Timing:", StringComparison.Ordinal) ||
            line.Contains("undergoing Connect Scan", StringComparison.Ordinal) ||
            line.Contains("undergoing SYN Stealth Scan", StringComparison.Ordinal))
        {
            return 25;
        }

        if (line.StartsWith("Completed Ping Scan", StringComparison.Ordinal) ||
            line.StartsWith("Initiating Connect Scan", StringComparison.Ordinal) ||
            line.StartsWith("Initiating SYN Stealth Scan", StringComparison.Ordinal))
        {
            return 15;
        }

        if (line.StartsWith("Initiating Ping Scan", StringComparison.Ordinal) ||
            line.StartsWith("Scanning ", StringComparison.Ordinal))
        {
            return 5;
        }

        return null;
    }
}
