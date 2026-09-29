using System.Diagnostics;

namespace Clean.Tests;

internal sealed class TestDirectory : IDisposable
{
    private readonly List<string> _junctions = [];

    public TestDirectory()
    {
        RootPath = Path.Combine(Path.GetTempPath(), "Clean.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(RootPath);
    }

    public string RootPath { get; }

    public string CreateFile(string relativePath, int sizeBytes)
    {
        var fullPath = Path.Combine(RootPath, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllBytes(fullPath, new byte[sizeBytes]);
        return fullPath;
    }

    public string CreateDirectory(string relativePath) =>
        Directory.CreateDirectory(Path.Combine(RootPath, relativePath)).FullName;

    public void CreateJunction(string relativeLinkPath, string targetPath)
    {
        // Junctions, unlike symbolic links, can be created without administrator rights.
        var linkPath = Path.Combine(RootPath, relativeLinkPath);
        using var process = Process.Start(new ProcessStartInfo("cmd.exe", $"/c mklink /J \"{linkPath}\" \"{targetPath}\"")
        {
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
        })!;
        process.WaitForExit();

        if (!Directory.Exists(linkPath))
        {
            throw new InvalidOperationException($"Could not create the test junction '{linkPath}'.");
        }

        _junctions.Add(linkPath);
    }

    public void Dispose()
    {
        // Removing the link itself first; a recursive delete trips over junctions whose target is already gone.
        foreach (var junction in _junctions)
        {
            Directory.Delete(junction);
        }

        if (Directory.Exists(RootPath))
        {
            Directory.Delete(RootPath, recursive: true);
        }
    }
}
