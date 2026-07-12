using Zenmap.Windows.Models;

namespace Zenmap.Windows.Services;

public sealed class ProfileStore
{
    public List<ScanProfile> CustomProfiles { get; private set; }

    public ProfileStore()
    {
        CustomProfiles = LoadCustomProfiles();
    }

    public IReadOnlyList<ScanProfile> Profiles => BuiltInProfiles.All.Concat(CustomProfiles).ToArray();

    public ScanProfile AddCustomProfile(string name, string arguments, string description)
    {
        var profile = new ScanProfile
        {
            Name = name,
            Arguments = arguments,
            Description = description,
            IsBuiltIn = false,
        };
        CustomProfiles.Add(profile);
        Save();
        return profile;
    }

    public void UpdateCustomProfile(Guid profileId, string name, string arguments, string description)
    {
        var index = CustomProfiles.FindIndex(profile => profile.Id == profileId);
        if (index < 0)
        {
            return;
        }

        CustomProfiles[index] = new ScanProfile
        {
            Id = profileId,
            Name = name,
            Arguments = arguments,
            Description = description,
            IsBuiltIn = false,
        };
        Save();
    }

    public void DeleteCustomProfile(Guid profileId)
    {
        CustomProfiles.RemoveAll(profile => profile.Id == profileId);
        Save();
    }

    public ScanProfile DuplicateProfile(ScanProfile profile) =>
        AddCustomProfile($"{profile.Name} Copy", profile.Arguments, profile.Description);

    public void MergeImported(IEnumerable<ScanProfile> importedProfiles)
    {
        foreach (var imported in importedProfiles)
        {
            var existing = CustomProfiles.FirstOrDefault(profile => profile.Name == imported.Name);
            if (existing is not null)
            {
                UpdateCustomProfile(existing.Id, imported.Name, imported.Arguments, imported.Description);
            }
            else
            {
                CustomProfiles.Add(new ScanProfile
                {
                    Id = imported.Id,
                    Name = imported.Name,
                    Arguments = imported.Arguments,
                    Description = imported.Description,
                    IsBuiltIn = false,
                });
            }
        }

        Save();
    }

    public int ExportCustomProfiles(string destination)
    {
        File.WriteAllText(destination, JsonSerialization.EncodeProfiles(CustomProfiles));
        return CustomProfiles.Count;
    }

    public IReadOnlyList<ScanProfile> ImportCustomProfiles(string source)
    {
        var imported = JsonSerialization.DecodeProfiles(File.ReadAllText(source))
            .Where(profile => !string.IsNullOrWhiteSpace(profile.Name))
            .Select(profile => new ScanProfile
            {
                Id = profile.Id,
                Name = profile.Name,
                Arguments = profile.Arguments,
                Description = profile.Description,
                IsBuiltIn = false,
            })
            .ToArray();
        MergeImported(imported);
        return imported;
    }

    private List<ScanProfile> LoadCustomProfiles()
    {
        var raw = JsonSerialization.ReadJsonFile(WindowsPaths.CustomProfilesPath, "[]");
        try
        {
            return JsonSerialization.DecodeProfiles(raw);
        }
        catch (Exception)
        {
            return [];
        }
    }

    private void Save() =>
        JsonSerialization.WriteJsonFile(WindowsPaths.CustomProfilesPath, JsonSerialization.EncodeProfiles(CustomProfiles));
}
