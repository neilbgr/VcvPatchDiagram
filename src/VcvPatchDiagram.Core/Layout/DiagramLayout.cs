using VcvPatchDiagram.Core.Analysis;
using VcvPatchDiagram.Core.Catalog;

namespace VcvPatchDiagram.Core.Layout;

/// <summary>A box in the diagram: a module of an unfolded group, or a whole folded group.</summary>
/// <param name="Key">"m{moduleId}" for a module, "g:{groupKey}" for a folded group.</param>
/// <param name="Group">Key of the group this box belongs to (or is).</param>
/// <param name="Details">Tooltip lines: ports and cables for a module, member modules for a folded group.</param>
public sealed record DiagramNode(
    string Key,
    long? ModuleId,
    string Group,
    bool IsFolded,
    string Title,
    string Subtitle,
    Role Role,
    Band Band,
    bool IsInsert,
    IReadOnlyList<string> Details,
    double X,
    double Y,
    double Width,
    double Height)
{
    /// <summary>Scopes/displays watching a signal of this box, drawn as a badge instead of boxes and cables.</summary>
    public IReadOnlyList<string> Watchers { get; init; } = Array.Empty<string>();
}

/// <summary>A drawn cable, or several cables of the same signal type merged between two boxes when a group is folded.</summary>
public sealed record DiagramEdge(
    string Key,
    string From,
    string To,
    IReadOnlyList<long> CableIds,
    string FromTitle,
    string FromPort,
    string ToTitle,
    string ToPort,
    SignalType Signal,
    string Intent,
    string Path,
    double LabelX,
    double LabelY)
{
    private const int labelMaxChars = 36;

    /// <summary>The intent as drawn on the cable: long ones are shortened, the tooltip keeps them whole.</summary>
    public string Label => Intent.Length <= labelMaxChars ? Intent : Intent[..(labelMaxChars - 1)].TrimEnd() + "…";

    /// <summary>Teaching layer: audio first, then pitch, modulation, and gate/trig/clock last.</summary>
    public string Layer => Signal switch
    {
        SignalType.Audio => "audio",
        SignalType.Pitch => "pitch",
        SignalType.Gate => "gate",
        _ => "modulation",
    };

    /// <summary>One real cable: its intent can be edited.</summary>
    public bool IsSingleCable => CableIds.Count == 1;

    /// <summary>Goes back against the flow (cross-modulation, feedback loop): drawn turning back to its target.</summary>
    public bool IsFeedback { get; init; }
}

/// <param name="Group">The unfolded group shown in this lane, or null for a lane of folded groups.</param>
public sealed record DiagramBand(Band Band, string? Group, string Title, double Y, double Height);

/// <summary>Positions of everything in the diagram: the single source of truth for every renderer and exporter.</summary>
public sealed record DiagramLayout(
    string Title,
    double Width,
    double Height,
    IReadOnlyList<DiagramBand> Bands,
    IReadOnlyList<DiagramNode> Nodes,
    IReadOnlyList<DiagramEdge> Edges,
    IReadOnlyList<PatchGroup> Groups,
    IReadOnlyList<string> Diagnostics)
{
    public static IReadOnlyList<string> Layers { get; } = new[] { "audio", "pitch", "modulation", "gate" };

    public static string LayerLabel(string layer) => layer == "gate" ? "gate / trig / clock" : layer;

    public DiagramNode Node(string key) => Nodes.First(n => n.Key == key);

    /// <summary>Boxes drawn in a band.</summary>
    public IEnumerable<DiagramNode> Members(DiagramBand band) =>
        Nodes.Where(n => band.Group is not null ? n.Group == band.Group && !n.IsFolded : n.IsFolded && n.Band == band.Band);
}