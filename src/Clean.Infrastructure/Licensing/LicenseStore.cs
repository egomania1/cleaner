namespace Clean.Infrastructure.Licensing;

// Keeps the signed token on this PC. It is not secret: without the server's private key nobody can forge one.
public sealed class LicenseStore(string path)
{
    public static readonly string DefaultPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Clean", "license.key");

    public string? Load()
    {
        try
        {
            return File.Exists(path) ? File.ReadAllText(path).Trim() : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public void Save(string token)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        // Written next to the real file first, so a crash never leaves half a token.
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, token.Trim());
        File.Move(temporary, path, overwrite: true);
    }

    public void Delete()
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Nothing to remove, or it is locked: the next check will decide.
        }
    }
}
