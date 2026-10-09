namespace VcvPatchTools.Cli;

internal static class InputPath
{
    /// <summary>Accepts "C:\Users\…" pasted from Windows when running under WSL, mapping it to /mnt/c/Users/….</summary>
    public static string Resolve(string path)
    {
        bool isWindowsPath = path.Length >= 3 && char.IsLetter(path[0]) && path[1] == ':' && (path[2] == '\\' || path[2] == '/');
        if (!isWindowsPath || OperatingSystem.IsWindows())
        {
            return path;
        }
        return $"/mnt/{char.ToLowerInvariant(path[0])}/{path[3..].Replace('\\', '/')}";
    }
}