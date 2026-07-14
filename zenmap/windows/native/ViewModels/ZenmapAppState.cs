using Microsoft.UI.Dispatching;
using Zenmap.Windows.Models;
using Zenmap.Windows.Services;

namespace Zenmap.Windows.ViewModels;

public sealed class ZenmapAppState
{
    private readonly DispatcherQueue _dispatcher;
    private readonly ScanRunner _scanRunner;

    public SettingsStore SettingsStore { get; }
    public ProfileStore ProfileStore { get; }
    public ScanHistoryStore ScanHistoryStore { get; }

    public string Target { get; set; }
    public string Arguments { get; set; }
    public ScanProfile SelectedProfile { get; set; }
    public string OutputText { get; private set; } = "";
    public string StatusText { get; private set; } = "Idle";
    public string ProgressText { get; private set; } = "";
    public double? ProgressPercent { get; private set; }
    public List<ScannedHost> Hosts { get; private set; } = [];
    public ScannedHost? SelectedHost { get; set; }
    public string LastCommand { get; private set; } = "";
    public string LastXmlPath { get; private set; } = "";
    public int? ExitStatus { get; private set; }
    public bool IsScanRunning => _scanRunner.IsRunning;

    public event Action? Changed;
    public event Action<string>? OutputAppended;
    public event Action? SavedScansChanged;
    public event Action? ProfilesChanged;
    public event Action<AppSettings>? DisableSaveScansConfirmationRequested;

    public ZenmapAppState(
        DispatcherQueue dispatcher,
        SettingsStore settingsStore,
        ProfileStore profileStore,
        ScanHistoryStore scanHistoryStore)
    {
        _dispatcher = dispatcher;
        SettingsStore = settingsStore;
        ProfileStore = profileStore;
        ScanHistoryStore = scanHistoryStore;

        SelectedProfile = ProfileByName(SettingsStore.Settings.DefaultProfileName);
        Target = SettingsStore.Settings.DefaultTarget;
        Arguments = SelectedProfile.Arguments;

        _scanRunner = new ScanRunner(
            text => AppendOutput(text),
            status => SetStatus(status),
            OnLifecycle,
            hosts => SetHosts(hosts),
            progress => SetProgress(progress),
            new DispatcherQueueSynchronizationContext(dispatcher));
    }

    public IReadOnlyList<ScanProfile> Profiles => ProfileStore.Profiles;

    public string CommandPreview
    {
        get
        {
            var args = ShellUtils.ShellSplit(Arguments);
            var targets = ShellUtils.SplitTargets(Target);
            var joinedArgs = string.Join(' ', args);
            var joinedTargets = string.Join(' ', targets);
            var binary = SettingsStore.Settings.NmapBinary;
            return string.IsNullOrWhiteSpace(joinedArgs)
                ? $"{binary} {joinedTargets}".Trim()
                : $"{binary} {joinedArgs} {joinedTargets}".Trim();
        }
    }

    public void SelectProfile(ScanProfile profile)
    {
        SelectedProfile = profile;
        Arguments = profile.Arguments;
        NotifyChanged();
    }

    public void StartScan(bool allowPrivileged)
    {
        OutputText = "";
        ExitStatus = null;
        NotifyChanged();
        OutputAppended?.Invoke("");

        LastCommand = CommandPreview;
        _scanRunner.Run(new ScanRequest
        {
            TargetText = Target,
            ArgumentsText = Arguments,
            AutoAddStatsEvery = SettingsStore.Settings.AutoAddStatsEvery,
            StatsEveryValue = SettingsStore.Settings.StatsEveryValue,
            AutoAddVerbose = SettingsStore.Settings.AutoAddVerbose,
            NmapBinary = SettingsStore.Settings.NmapBinary,
            AllowPrivileged = allowPrivileged,
        });
        NotifyChanged();
    }

    public bool RequiresPrivilegePrompt()
    {
        var args = ShellUtils.ShellSplit(Arguments);
        return ScanPrivilegeEvaluator.Evaluate(args).Mode == ZenmapScanExecutionMode.Administrator;
    }

    public string PrivilegeReason()
    {
        var args = ShellUtils.ShellSplit(Arguments);
        return ScanPrivilegeEvaluator.Evaluate(args).Reason;
    }

    public void StopScan() => _scanRunner.Stop();

    public void ClearOutput()
    {
        if (IsScanRunning)
        {
            return;
        }

        OutputText = "";
        NotifyChanged();
        OutputAppended?.Invoke("");
    }

    /// <summary>Replace output buffer (UI / verification). Does not start a scan.</summary>
    public void SetOutputText(string text)
    {
        OutputText = text ?? "";
        NotifyChanged();
        OutputAppended?.Invoke(OutputText);
    }

    public void ClearResults()
    {
        if (IsScanRunning)
        {
            return;
        }

        OutputText = "Ready. Choose a profile, enter a target, then run a scan.";
        StatusText = "Idle";
        ProgressText = "";
        ProgressPercent = null;
        ExitStatus = null;
        LastCommand = "";
        LastXmlPath = "";
        Hosts = [];
        SelectedHost = null;
        NotifyChanged();
        OutputAppended?.Invoke(OutputText);
    }

    public void AppendOutputLine(string line)
    {
        if (string.IsNullOrEmpty(OutputText) || OutputText.EndsWith('\n'))
        {
            OutputText += line + "\n";
        }
        else
        {
            OutputText += "\n" + line + "\n";
        }

        NotifyChanged();
        OutputAppended?.Invoke(OutputText);
    }

    public void SaveSettings(AppSettings settings, bool confirmedDisableSaveScans = false)
    {
        if (SettingsStore.Settings.SaveScansByDefault
            && !settings.SaveScansByDefault
            && !confirmedDisableSaveScans)
        {
            DisableSaveScansConfirmationRequested?.Invoke(settings);
            return;
        }

        ApplySettings(settings);
    }

    public void ApplySettings(AppSettings settings)
    {
        SettingsStore.Settings = settings;
        SettingsStore.Save();
        Target = settings.DefaultTarget;
        SelectedProfile = ProfileByName(settings.DefaultProfileName);
        Arguments = SelectedProfile.Arguments;
        NotifyChanged();
    }

    public void LoadSavedScan(SavedScan scan)
    {
        var hosts = XmlParsing.ParseNmapXml(scan.XmlPath);
        var (arguments, targets) = ScanFormUtils.ValuesFromCommand(scan.Command);
        Target = targets;
        Arguments = arguments;
        OutputText = $"Loaded saved scan: {scan.Title}\nCommand: {scan.Command}\nXML: {scan.XmlPath}\n";
        LastCommand = scan.Command;
        LastXmlPath = scan.XmlPath;
        ExitStatus = 0;
        SetHosts(hosts);
        SetStatus("Loaded saved scan");
        NotifyChanged();
        OutputAppended?.Invoke(OutputText);
    }

    public void ImportXmlFile(string xmlPath)
    {
        var hosts = XmlParsing.ParseNmapXml(xmlPath);
        var ephemeral = !SettingsStore.Settings.SaveScansByDefault;
        ScanHistoryStore.ImportXml(Path.GetFileNameWithoutExtension(xmlPath), "", xmlPath, hosts, ephemeral);
        SavedScansChanged?.Invoke();
        LoadSavedScan(ScanHistoryStore.SavedScans[0]);
    }

    public bool PersistSavedScan(Guid scanId)
    {
        if (!ScanHistoryStore.PersistScan(scanId))
        {
            return false;
        }

        SavedScansChanged?.Invoke();
        return true;
    }

    public void CleanupEphemeralScans() => ScanHistoryStore.CleanupEphemeralScans();

    public void DeleteSavedScan(Guid scanId)
    {
        ScanHistoryStore.RemoveScan(scanId);
        SavedScansChanged?.Invoke();
    }

    public void ClearSavedScans()
    {
        ScanHistoryStore.Clear();
        SavedScansChanged?.Invoke();
    }

    public void UpdateSavedScanMetadata(Guid scanId, string notes, string tags)
    {
        ScanHistoryStore.UpdateScanMetadata(scanId, notes, tags);
        SavedScansChanged?.Invoke();
    }

    public int ExportSavedScanHistory(string destination) =>
        ScanHistoryStore.ExportHistory(destination);

    public int ImportSavedScanHistory(string source)
    {
        var importedCount = ScanHistoryStore.ImportHistory(source);
        if (importedCount > 0)
        {
            SavedScansChanged?.Invoke();
        }

        return importedCount;
    }

    public void UseProfile(ScanProfile profile) => SelectProfile(profile);

    public void AddCustomProfile(string name, string arguments, string description)
    {
        ProfileStore.AddCustomProfile(name, arguments, description);
        ProfilesChanged?.Invoke();
        NotifyChanged();
    }

    public void UpdateCustomProfile(Guid profileId, string name, string arguments, string description)
    {
        ProfileStore.UpdateCustomProfile(profileId, name, arguments, description);
        ProfilesChanged?.Invoke();
        NotifyChanged();
    }

    public void DeleteCustomProfile(Guid profileId)
    {
        ProfileStore.DeleteCustomProfile(profileId);
        ProfilesChanged?.Invoke();
        NotifyChanged();
    }

    public void DuplicateProfile(ScanProfile profile)
    {
        ProfileStore.DuplicateProfile(profile);
        ProfilesChanged?.Invoke();
        NotifyChanged();
    }

    public void ImportProfiles(string source)
    {
        ProfileStore.ImportCustomProfiles(source);
        ProfilesChanged?.Invoke();
        NotifyChanged();
    }

    public void ExportProfiles(string destination) => ProfileStore.ExportCustomProfiles(destination);

    public void ShowHostDetails(ScannedHost host)
    {
        SelectedHost = host;
        NotifyChanged();
    }

    private ScanProfile ProfileByName(string name) =>
        Profiles.FirstOrDefault(profile => profile.Name == name) ?? Profiles[0];

    private void AppendOutput(string text)
    {
        OutputText += text;
        OutputAppended?.Invoke(text);
    }

    private void SetStatus(string status)
    {
        StatusText = status;
        NotifyChanged();
    }

    private void SetProgress(ScanProgressState progress)
    {
        ProgressPercent = progress.OverallPercent;
        ProgressText = string.Join(" · ", new[]
        {
            progress.Message,
            progress.PhaseText,
            progress.ElapsedText,
            progress.EstimatedCompletionText,
        }.Where(part => !string.IsNullOrWhiteSpace(part)));
        NotifyChanged();
    }

    private void SetHosts(IReadOnlyList<ScannedHost> hosts)
    {
        var previousAddress = SelectedHost?.Address;
        Hosts = hosts.ToList();
        if (!string.IsNullOrWhiteSpace(previousAddress))
        {
            SelectedHost = Hosts.FirstOrDefault(host => host.Address == previousAddress);
        }
        else if (SelectedHost is not null && !Hosts.Contains(SelectedHost))
        {
            SelectedHost = Hosts.FirstOrDefault();
        }

        NotifyChanged();
    }

    private void OnLifecycle(ZenmapScanLifecycleState state, int? exitStatus)
    {
        ExitStatus = exitStatus;
        if (state == ZenmapScanLifecycleState.Completed && _scanRunner.XmlPath is not null)
        {
            LastXmlPath = _scanRunner.XmlPath;
            var title = $"{Target.Trim()} - {SelectedProfile.Name}";
            ScanHistoryStore.AddScan(
                title,
                LastCommand,
                LastXmlPath,
                Hosts,
                ephemeral: !SettingsStore.Settings.SaveScansByDefault);
            SavedScansChanged?.Invoke();
        }

        NotifyChanged();
    }

    private void NotifyChanged() => Changed?.Invoke();
}
