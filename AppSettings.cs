using System.Text.Json;

namespace CleanTVRemote;

internal sealed class AppSettings
{
    public string TvIp { get; set; } = "";
    public string MacAddress { get; set; } = "";
    public bool Secure { get; set; } = true;
    public string Token { get; set; } = "";

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
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new AppSettings();
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

        return new AppSettings();
    }

    public void Save()
    {
        Directory.CreateDirectory(DirectoryPath);
        string json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(FilePath, json);
    }
}
