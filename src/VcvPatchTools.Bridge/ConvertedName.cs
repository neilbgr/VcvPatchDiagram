namespace VcvPatchTools.Bridge;

/// <summary>Names a converted patch after its source and target: "Patch.vcv" → "Patch.rack.vcv".</summary>
public static class ConvertedName
{
    public static string Slug(PatchOrigin target) => target switch
    {
        PatchOrigin.Cardinal => "cardinal",
        PatchOrigin.Rack => "rack",
        _ => throw new ArgumentOutOfRangeException(nameof(target), target, "Target must be Cardinal or Rack."),
    };

    /// <summary>The file name without its extension, with the target's suffix in place of any origin suffix.</summary>
    /// <remarks>
    /// An existing ".cardinal"/".rack" suffix is replaced instead of stacking another one
    /// (e.g. "Patch.cardinal" → "Patch.rack", not "Patch.cardinal.rack").
    /// </remarks>
    public static string BaseName(string baseName, PatchOrigin target)
    {
        foreach (string originSuffix in new[] { ".cardinal", ".rack" })
        {
            if (baseName.EndsWith(originSuffix, StringComparison.OrdinalIgnoreCase))
            {
                baseName = baseName[..^originSuffix.Length];
                break;
            }
        }
        return $"{baseName}.{Slug(target)}";
    }

    /// <summary>"Patch.cardinal.vcv" → "Patch.rack.vcv".</summary>
    public static string For(string fileName, PatchOrigin target) =>
        BaseName(Path.GetFileNameWithoutExtension(fileName), target) + Path.GetExtension(fileName);

    public static string Describe(PatchOrigin origin) => origin switch
    {
        PatchOrigin.Cardinal => "Cardinal",
        PatchOrigin.Rack => "VCV Rack",
        PatchOrigin.Ambiguous => "Ambiguous (no divergent module detected)",
        _ => origin.ToString(),
    };

    /// <summary>The other side of a detected origin; null when the patch could be either.</summary>
    public static PatchOrigin? OppositeOf(PatchOrigin origin) => origin switch
    {
        PatchOrigin.Cardinal => PatchOrigin.Rack,
        PatchOrigin.Rack => PatchOrigin.Cardinal,
        _ => null,
    };
}