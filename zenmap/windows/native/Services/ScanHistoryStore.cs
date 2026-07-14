using Zenmap.Windows.Models;

namespace Zenmap.Windows.Services;

public sealed class ScanHistoryStore
{
    public List<SavedScan> SavedScans { get; private set; }

    public ScanHistoryStore()
    {
        SavedScans = Load();
        PruneOrphanSessionScans();
    }

    public SavedScan AddScan(
        string title,
        string command,
        string xmlPath,
        IReadOnlyList<ScannedHost> hosts,
        bool ephemeral = false)
    {
        var destinationDirectory = ephemeral ? WindowsPaths.SessionScansDirectory : WindowsPaths.SavedScansDirectory;
        var destination = Path.Combine(destinationDirectory, $"{Guid.NewGuid()}.xml");
        File.Copy(xmlPath, destination, overwrite: true);
        var savedScan = new SavedScan
        {
            Title = title,
            Command = command,
            XmlPath = destination,
            ScannedAt = DateTimeOffset.Now,
            HostCount = hosts.Count,
            PortCount = hosts.Sum(host => host.Ports.Count),
            Ephemeral = ephemeral,
        };
        SavedScans.Insert(0, savedScan);
        Save();
        return savedScan;
    }

    public SavedScan ImportXml(
        string title,
        string command,
        string xmlPath,
        IReadOnlyList<ScannedHost> hosts,
        bool ephemeral = false)
    {
        var destinationDirectory = ephemeral ? WindowsPaths.SessionScansDirectory : WindowsPaths.SavedScansDirectory;
        var destination = Path.Combine(destinationDirectory, $"{Guid.NewGuid()}.xml");
        File.Copy(xmlPath, destination, overwrite: true);
        var scannedAt = File.Exists(xmlPath)
            ? new DateTimeOffset(File.GetLastWriteTime(xmlPath))
            : DateTimeOffset.Now;
        var savedScan = new SavedScan
        {
            Title = string.IsNullOrWhiteSpace(title) ? Path.GetFileNameWithoutExtension(xmlPath) : title,
            Command = string.IsNullOrWhiteSpace(command) ? $"nmap (imported) {Path.GetFileName(xmlPath)}" : command,
            XmlPath = destination,
            ScannedAt = scannedAt,
            HostCount = hosts.Count,
            PortCount = hosts.Sum(host => host.Ports.Count),
            Ephemeral = ephemeral,
        };
        SavedScans.Insert(0, savedScan);
        Save();
        return savedScan;
    }

    public bool PersistScan(Guid scanId)
    {
        var index = SavedScans.FindIndex(scan => scan.Id == scanId);
        if (index < 0 || !SavedScans[index].Ephemeral)
        {
            return false;
        }

        var scan = SavedScans[index];
        if (!File.Exists(scan.XmlPath))
        {
            return false;
        }

        var destination = Path.Combine(WindowsPaths.SavedScansDirectory, $"{Guid.NewGuid()}.xml");
        File.Copy(scan.XmlPath, destination, overwrite: true);
        TryDelete(scan.XmlPath);
        SavedScans[index] = new SavedScan
        {
            Id = scan.Id,
            Title = scan.Title,
            Command = scan.Command,
            XmlPath = destination,
            ScannedAt = scan.ScannedAt,
            HostCount = scan.HostCount,
            PortCount = scan.PortCount,
            Notes = scan.Notes,
            Tags = scan.Tags,
            Ephemeral = false,
        };
        Save();
        return true;
    }

    public void RemoveScan(Guid scanId, bool deleteFile = true)
    {
        var remaining = new List<SavedScan>();
        foreach (var scan in SavedScans)
        {
            if (scan.Id == scanId)
            {
                if (deleteFile)
                {
                    TryDelete(scan.XmlPath);
                }

                continue;
            }

            remaining.Add(scan);
        }

        SavedScans = remaining;
        Save();
    }

    public void Clear(bool deleteFiles = true)
    {
        if (deleteFiles)
        {
            foreach (var scan in SavedScans)
            {
                TryDelete(scan.XmlPath);
            }
        }

        SavedScans = [];
        Save();
    }

    public void CleanupEphemeralScans()
    {
        var remaining = new List<SavedScan>();
        foreach (var scan in SavedScans)
        {
            if (scan.Ephemeral)
            {
                TryDelete(scan.XmlPath);
                continue;
            }

            remaining.Add(scan);
        }

        SavedScans = remaining;
        Save();
    }

    public void UpdateScanMetadata(Guid scanId, string notes, string tags)
    {
        var index = SavedScans.FindIndex(scan => scan.Id == scanId);
        if (index < 0)
        {
            return;
        }

        var scan = SavedScans[index];
        SavedScans[index] = new SavedScan
        {
            Id = scan.Id,
            Title = scan.Title,
            Command = scan.Command,
            XmlPath = scan.XmlPath,
            ScannedAt = scan.ScannedAt,
            HostCount = scan.HostCount,
            PortCount = scan.PortCount,
            Notes = notes,
            Tags = tags,
            Ephemeral = scan.Ephemeral,
        };
        Save();
    }

    public void MergeImported(IEnumerable<SavedScan> importedScans)
    {
        var merged = new List<SavedScan>(SavedScans);
        foreach (var imported in importedScans)
        {
            var existingIndex = merged.FindIndex(
                scan => scan.Id == imported.Id || scan.XmlPath == imported.XmlPath);
            if (existingIndex < 0)
            {
                merged.Add(imported);
            }
            else
            {
                merged[existingIndex] = imported;
            }
        }

        SavedScans = merged
            .Where(scan => File.Exists(scan.XmlPath))
            .OrderByDescending(scan => scan.ScannedAt)
            .ToList();
        Save();
    }

    public int ExportHistory(string destination)
    {
        var persistentScans = SavedScans.Where(scan => !scan.Ephemeral).ToList();
        File.WriteAllText(destination, JsonSerialization.EncodeSavedScans(persistentScans));
        return persistentScans.Count;
    }

    public int ImportHistory(string source)
    {
        var imported = JsonSerialization.DecodeSavedScans(File.ReadAllText(source))
            .Where(scan => !string.IsNullOrWhiteSpace(scan.Title) && File.Exists(scan.XmlPath))
            .ToList();
        if (imported.Count > 0)
        {
            MergeImported(imported);
        }

        return imported.Count;
    }

    private void PruneOrphanSessionScans()
    {
        if (!Directory.Exists(WindowsPaths.SessionScansDirectory))
        {
            return;
        }

        var referencedPaths = SavedScans.Select(scan => scan.XmlPath).ToHashSet(StringComparer.Ordinal);
        foreach (var path in Directory.EnumerateFiles(WindowsPaths.SessionScansDirectory, "*.xml"))
        {
            if (!referencedPaths.Contains(path))
            {
                TryDelete(path);
            }
        }
    }

    private List<SavedScan> Load()
    {
        var raw = JsonSerialization.ReadJsonFile(WindowsPaths.SavedScansIndexPath, "[]");
        try
        {
            return JsonSerialization.DecodeSavedScans(raw)
                .Where(scan => File.Exists(scan.XmlPath))
                .ToList();
        }
        catch (Exception)
        {
            return [];
        }
    }

    private void Save() =>
        JsonSerialization.WriteJsonFile(WindowsPaths.SavedScansIndexPath, JsonSerialization.EncodeSavedScans(SavedScans));

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
