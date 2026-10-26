using System.Text.Json;
using System.Text.Json.Serialization;

namespace CleanTVRemote;

internal sealed class AppSettings
{
    private const string DefaultTvIp = "192.168.1.166";
    private const string DefaultMacAddress = "E0-03-6B-50-AB-E3";

    public List<TvProfile> Profiles { get; set; } = [];
    public string ActiveProfileId { get; set; } = "";

    [JsonIgnore]
    public TvProfile ActiveProfile
    {
        get
        {
            Profiles ??= [];
            if (Profiles.Count == 0)
                Profiles.Add(CreateLivingRoomProfile());

            return Profiles.FirstOrDefault(profile => profile.Id == ActiveProfileId) ?? SelectFirstProfile();
        }
    }

    [JsonIgnore] public string TvIp { get => ActiveProfile.TvIp; set => ActiveProfile.TvIp = value; }
    [JsonIgnore] public string MacAddress { get => ActiveProfile.MacAddress; set => ActiveProfile.MacAddress = value; }
    [JsonIgnore] public bool Secure { get => ActiveProfile.Secure; set => ActiveProfile.Secure = value; }
    [JsonIgnore] public string Token { get => ActiveProfile.Token; set => ActiveProfile.Token = value; }

    private static string DirectoryPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "CleanTVRemote");

    private static string FilePath => Path.Combine(DirectoryPath, "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                string json = File.ReadAllText(FilePath);
                using JsonDocument document = JsonDocument.Parse(json);
                JsonElement root = document.RootElement;
                if (root.TryGetProperty("Profiles", out JsonElement profiles) &&
                    profiles.ValueKind == JsonValueKind.Array && profiles.GetArrayLength() > 0)
                {
                    AppSettings settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                    _ = settings.ActiveProfile;
                    return settings;
                }

                LegacySettings? legacy = JsonSerializer.Deserialize<LegacySettings>(json);
                return FromLegacy(legacy);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
        catch (JsonException)
        {
        }

        return FromLegacy(null);
    }

    public void Save()
    {
        _ = ActiveProfile;
        Directory.CreateDirectory(DirectoryPath);
        string json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(FilePath, json);
    }

    private TvProfile SelectFirstProfile()
    {
        TvProfile profile = Profiles[0];
        ActiveProfileId = profile.Id;
        return profile;
    }

    private static AppSettings FromLegacy(LegacySettings? legacy)
    {
        var settings = new AppSettings();
        TvProfile livingRoom = CreateLivingRoomProfile();
        if (!string.IsNullOrWhiteSpace(legacy?.TvIp)) livingRoom.TvIp = legacy.TvIp;
        if (!string.IsNullOrWhiteSpace(legacy?.MacAddress)) livingRoom.MacAddress = legacy.MacAddress;
        if (legacy != null)
        {
            livingRoom.Secure = legacy.Secure;
            livingRoom.Token = legacy.Token ?? "";
        }
        settings.Profiles.Add(livingRoom);
        settings.ActiveProfileId = livingRoom.Id;
        return settings;
    }

    private static TvProfile CreateLivingRoomProfile() => new()
    {
        Name = "Living Room TV",
        TvIp = DefaultTvIp,
        MacAddress = DefaultMacAddress
    };

    private sealed class LegacySettings
    {
        public string? TvIp { get; set; }
        public string? MacAddress { get; set; }
        public bool Secure { get; set; } = true;
        public string? Token { get; set; }
    }
}

internal sealed class TvProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "TV";
    public string TvIp { get; set; } = "";
    public string MacAddress { get; set; } = "";
    public bool Secure { get; set; } = true;
    public string Token { get; set; } = "";
}
