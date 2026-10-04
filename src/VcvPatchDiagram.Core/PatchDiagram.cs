using System.Text.Json;
using System.Text.Json.Serialization;
using VcvPatchDiagram.Core.Analysis;
using VcvPatchDiagram.Core.Layout;
using VcvPatchDiagram.Core.Patch;
using VcvPatchDiagram.Core.Render;

namespace VcvPatchDiagram.Core;

public enum DiagramFormat
{
    Html,
    Svg,
    Dot,
    Mermaid,
    Json,
}

/// <summary>Entry point shared by the CLI and the Blazor app: patch bytes in, diagram out.</summary>
public static class PatchDiagram
{
    private static readonly Lazy<PatchAnalyzer> analyzer = new Lazy<PatchAnalyzer>(PatchAnalyzer.CreateDefault);

    public static PatchAnalysis Analyze(byte[] patchBytes) => analyzer.Value.Analyze(PatchReader.Read(patchBytes));

    /// <param name="unfolded">Groups to show in detail (see <see cref="GroupKeys"/>); none by default: the overview.</param>
    /// <param name="showMonitors">Also draw scopes and displays (hidden by default).</param>
    public static DiagramLayout Layout(PatchAnalysis analysis, string title, IReadOnlySet<string>? unfolded = null, IReadOnlyDictionary<long, string>? intents = null, bool showMonitors = false) =>
        LayeredLayout.Build(analysis, title, unfolded, intents, showMonitors);

    /// <summary>Keys of every foldable group, e.g. to unfold everything.</summary>
    public static IReadOnlySet<string> GroupKeys(PatchAnalysis analysis) => PatchGrouping.Build(analysis).Select(g => g.Key).ToHashSet();

    public static string Export(DiagramLayout layout, DiagramFormat format) => format switch
    {
        DiagramFormat.Html => HtmlRenderer.Render(layout),
        DiagramFormat.Svg => SvgRenderer.Render(layout, standalone: true),
        DiagramFormat.Dot => DotExporter.Export(layout),
        DiagramFormat.Mermaid => MermaidExporter.Export(layout),
        _ => JsonSerializer.Serialize(layout, new JsonSerializerOptions { WriteIndented = true, Converters = { new JsonStringEnumConverter() } }),
    };

    public static string Extension(DiagramFormat format) => format switch
    {
        DiagramFormat.Html => ".html",
        DiagramFormat.Svg => ".svg",
        DiagramFormat.Dot => ".dot",
        DiagramFormat.Mermaid => ".mmd",
        _ => ".json",
    };

    public static string MimeType(DiagramFormat format) => format switch
    {
        DiagramFormat.Html => "text/html",
        DiagramFormat.Svg => "image/svg+xml",
        DiagramFormat.Json => "application/json",
        _ => "text/plain",
    };

    public static bool TryParseFormat(string text, out DiagramFormat format)
    {
        string normalized = text.TrimStart('.').ToLowerInvariant();
        format = normalized switch
        {
            "html" => DiagramFormat.Html,
            "svg" => DiagramFormat.Svg,
            "dot" or "gv" => DiagramFormat.Dot,
            "mmd" or "mermaid" => DiagramFormat.Mermaid,
            "json" => DiagramFormat.Json,
            _ => (DiagramFormat)(-1),
        };
        return Enum.IsDefined(format);
    }
}