using VcvPatchTools.Bridge;

namespace VcvPatchTools.Cli;

/// <summary>Derives an output patch path from the input path and the conversion target when none was given explicitly.</summary>
public static class OutputPathResolver
{
    public static string Resolve(string inputPath, PatchOrigin target, bool force)
    {
        string fullInput = Path.GetFullPath(inputPath);
        string dir = Path.GetDirectoryName(fullInput) ?? ".";
        string baseName = ConvertedName.BaseName(Path.GetFileNameWithoutExtension(fullInput), target);
        string ext = Path.GetExtension(fullInput);

        string candidate = Path.Combine(dir, $"{baseName}{ext}");
        if (force || !File.Exists(candidate))
        {
            return candidate;
        }

        for (int n = 1; ; n++)
        {
            string numbered = Path.Combine(dir, $"{baseName}.x{n}{ext}");
            if (!File.Exists(numbered))
            {
                return numbered;
            }
        }
    }
}