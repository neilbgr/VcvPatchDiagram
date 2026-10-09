namespace VcvPatchTools.Core.Catalog;

/// <summary>What the catalog knows about a module model: display name, plugin.json tags and port names by index.</summary>
public sealed record ModuleInfo(string? Name, IReadOnlyList<string> Tags, IReadOnlyList<string> Inputs, IReadOnlyList<string> Outputs)
{
    public static ModuleInfo Empty { get; } = new ModuleInfo(null, Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>());

    /// <summary>Programmable modulation matrix (Surge XT style), when the module has one.</summary>
    public ModMatrix? Modulation { get; init; }
}

/// <summary>
/// Surge XT modules have a few generic "Modulation Signal n" inputs; how much each one moves each knob is stored
/// in a block of params: depth of input m on target t = param[FirstDepthParam + t * InputCount + m], in -1..1.
/// </summary>
public sealed record ModMatrix(int FirstInput, int InputCount, int FirstDepthParam, IReadOnlyList<string> Targets);