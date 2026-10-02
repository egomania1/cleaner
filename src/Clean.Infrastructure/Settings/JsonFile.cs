using System.Text.Json;

namespace Clean.Infrastructure.Settings;

// Small JSON files under %LOCALAPPDATA%\Clean. A missing or unreadable file reads as "nothing saved yet".
internal static class JsonFile
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };

    public static string InAppFolder(string fileName) =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Clean", fileName);

    public static T? Read<T>(string path)
        where T : class
    {
        try
        {
            return File.Exists(path) ? JsonSerializer.Deserialize<T>(File.ReadAllText(path), Options) : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    public static void Write<T>(string path, T value)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var temporary = path + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(value, Options));
            File.Move(temporary, path, overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // A setting that cannot be saved is lost at the next start; the app keeps working.
        }
    }
}
