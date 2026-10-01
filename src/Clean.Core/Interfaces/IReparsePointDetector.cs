namespace Clean.Core.Interfaces;

public interface IReparsePointDetector
{
    // The first link (junction, symbolic link, mount point) met while walking from the drive down to the path, if any.
    string? FindLinkOnPath(string path);
}
