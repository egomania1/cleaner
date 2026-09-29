using System.Diagnostics;
using Clean.Core.Interfaces;

namespace Clean.Infrastructure.Windows;

public sealed class FileExplorer : IFileExplorer
{
    public void Reveal(string path)
    {
        var arguments = Directory.Exists(path) ? $"\"{path}\"" : $"/select,\"{path}\"";
        Process.Start(new ProcessStartInfo("explorer.exe", arguments) { UseShellExecute = true });
    }
}
