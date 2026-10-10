using System.Globalization;
using System.Net;
using System.Text;
using VcvPatchTools.Diagram.Analysis;
using VcvPatchTools.Diagram.Layout;

namespace VcvPatchTools.Diagram.Render;

/// <summary>Draws a <see cref="DiagramLayout"/> as SVG with semantic classes (roles, signals, layers) styled by diagram.css.</summary>
public static class SvgRenderer
{
    private const double legendRow = 22;
    private const double legendCharWidth = 6.2;

    /// <param name="standalone">
    /// Embeds the stylesheet and wraps in a .vpd-root group, so the .svg file renders on its own; it then also carries
    /// its legend at the bottom (the HTML page and the app have theirs in the toolbar).
    /// </param>
    public static string Render(DiagramLayout layout, bool standalone)
    {
        StringBuilder svg = new StringBuilder();
        List<(string Class, string Text, bool Line)> legend = standalone ? LegendItems(layout) : new List<(string, string, bool)>();
        List<List<(string Class, string Text, bool Line)>> legendRows = WrapLegend(legend, layout.Width);
        double height = layout.Height + (legendRows.Count * legendRow);
        svg.Append(Invariant($"<svg xmlns=\"http://www.w3.org/2000/svg\" class=\"vpd-svg{(standalone ? " vpd-root" : "")}\" viewBox=\"0 0 {layout.Width:0} {height:0}\" width=\"{layout.Width:0}\" height=\"{height:0}\" role=\"img\" aria-label=\"{Escape(layout.Title)}\">"));
        if (standalone)
        {
            svg.Append("<style>").Append(Resources.Css).Append("</style>");
            svg.Append(Invariant($"<rect width=\"{layout.Width:0}\" height=\"{height:0}\" style=\"fill: var(--vpd-bg)\"/>"));
        }

        svg.Append("<defs>");
        foreach (SignalType signal in Enum.GetValues<SignalType>())
        {
            string key = SignalClass(signal);
            svg.Append($"<marker id=\"vpd-arrow-{key}\" viewBox=\"0 0 10 10\" refX=\"0\" refY=\"5\" markerUnits=\"userSpaceOnUse\" markerWidth=\"{LayeredLayout.ArrowLength}\" markerHeight=\"{LayeredLayout.ArrowLength}\" orient=\"auto-start-reverse\"><path d=\"M 0 0 L 10 5 L 0 10 z\" class=\"vpd-marker-{key}\"/></marker>");
        }
        svg.Append("</defs>");

        svg.Append("<g class=\"vpd-bands\">");
        foreach (DiagramBand band in layout.Bands)
        {
            (double bandX, double bandWidth) = layout.Extent(band);
            svg.Append($"<g class=\"{BandClass(band)}\">");
            svg.Append(Invariant($"<rect x=\"{bandX:0.#}\" y=\"{band.Y:0.#}\" width=\"{bandWidth:0.#}\" height=\"{band.Height:0.#}\" rx=\"8\"/>"));
            svg.Append(Invariant($"<text x=\"{bandX + 12:0.#}\" y=\"{band.Y + 19:0.#}\">{Escape(band.Title)}</text>"));
            svg.Append("</g>");
        }
        svg.Append("</g>");

        svg.Append("<g class=\"vpd-edges\">");
        foreach (DiagramEdge edge in layout.Edges)
        {
            string key = SignalClass(edge.Signal);
            svg.Append($"<g class=\"{EdgeClass(edge)}\" data-from=\"{edge.From}\" data-to=\"{edge.To}\">");
            svg.Append($"<title>{Escape(EdgeTooltip(edge))}</title>");
            svg.Append($"<path class=\"hit\" d=\"{edge.Path}\"/>");
            svg.Append($"<path d=\"{edge.Path}\" marker-end=\"url(#vpd-arrow-{key})\"/>");
            svg.Append("</g>");
        }
        svg.Append("</g>");

        svg.Append("<g class=\"vpd-nodes\">");
        foreach (DiagramNode node in layout.Nodes)
        {
            svg.Append($"<g class=\"{NodeClass(node)}\" data-id=\"{node.Key}\">");
            svg.Append($"<title>{Escape(string.Join("\n", node.Details))}</title>");
            if (node.IsFolded)
            {
                // A second card offset behind: "there's more inside".
                svg.Append(Invariant($"<rect class=\"stack\" x=\"{node.X + 4:0.#}\" y=\"{node.Y + 4:0.#}\" width=\"{node.Width:0.#}\" height=\"{node.Height:0.#}\" rx=\"6\"/>"));
            }
            foreach (PortTab tab in node.Tabs)
            {
                svg.Append($"<g class=\"tab sig-{SignalClass(tab.Signal)}\"><title>{Escape(tab.Tooltip)}</title>");
                svg.Append(Invariant($"<rect x=\"{TabX(node, tab):0.#}\" y=\"{tab.Y - (PortTabs.Height / 2):0.#}\" width=\"{tab.Width:0.#}\" height=\"{PortTabs.Height:0.#}\" rx=\"3\"/>"));
                svg.Append(Invariant($"<text x=\"{TabX(node, tab) + (tab.Width / 2):0.#}\" y=\"{tab.Y + 3.2:0.#}\" text-anchor=\"middle\">{Escape(tab.Text)}</text></g>"));
            }
            svg.Append(Invariant($"<rect class=\"box\" x=\"{node.X:0.#}\" y=\"{node.Y:0.#}\" width=\"{node.Width:0.#}\" height=\"{node.Height:0.#}\" rx=\"6\"/>"));
            svg.Append(Invariant($"<rect class=\"stripe\" x=\"{node.X:0.#}\" y=\"{node.Y:0.#}\" width=\"5\" height=\"{node.Height:0.#}\" rx=\"2\"/>"));
            svg.Append(Invariant($"<text class=\"title\" x=\"{node.X + 14:0.#}\" y=\"{node.Y + 20:0.#}\">{Escape(Fit(node.Title, TitleChars(node)))}</text>"));
            if (node.Function is Core.Catalog.ModuleFunction function)
            {
                svg.Append(Invariant($"<path class=\"icon\" transform=\"translate({IconX(node):0.#} {IconY(node):0.#})\" d=\"{Icons.Path(function)}\"/>"));
            }
            svg.Append(Invariant($"<text class=\"subtitle\" x=\"{node.X + 14:0.#}\" y=\"{node.Y + 36:0.#}\">{Escape(Fit(NodeSubtitle(node), SubtitleChars(node)))}</text>"));
            if (node.LibraryKey is string library)
            {
                // A link of its own, so clicking the box keeps unfolding/highlighting; the preview is loaded on hover by the page script.
                svg.Append($"<a class=\"lib\" href=\"{Escape(Core.Catalog.VcvLibrary.PageUrl(library))}\" target=\"_blank\" rel=\"noopener\" data-library=\"{Escape(library)}\" aria-label=\"{LibraryTooltip}\">{IconTitle(standalone, LibraryTooltip)}");
                svg.Append(Invariant($"<rect x=\"{LibraryX(node) - 2:0.#}\" y=\"{LibraryY(node) - 2:0.#}\" width=\"{LibraryIcon.Size + 4:0.#}\" height=\"{LibraryIcon.Size + 4:0.#}\" rx=\"3\"/>"));
                svg.Append(Invariant($"<path transform=\"translate({LibraryX(node):0.#} {LibraryY(node):0.#})\" d=\"{LibraryIcon.Path}\"/></a>"));
            }
            else if (node.LibraryPanels.Count > 0)
            {
                // A folded group: no single page to open, only its modules' panels to preview on hover.
                svg.Append($"<g class=\"lib panels\" data-panels=\"{Escape(PanelsAttribute(node))}\" data-unlisted=\"{node.UnlistedCount}\" aria-label=\"{PanelsTooltip}\">{IconTitle(standalone, PanelsTooltip)}");
                svg.Append(Invariant($"<rect x=\"{LibraryX(node) - 2:0.#}\" y=\"{LibraryY(node) - 2:0.#}\" width=\"{LibraryIcon.Size + 4:0.#}\" height=\"{LibraryIcon.Size + 4:0.#}\" rx=\"3\"/>"));
                svg.Append(Invariant($"<path transform=\"translate({LibraryX(node):0.#} {LibraryY(node):0.#})\" d=\"{LibraryIcon.PanelsPath}\"/></g>"));
            }
            if (node.Watchers.Count > 0)
            {
                svg.Append($"<g class=\"watch\"><title>{Escape(WatchTooltip(node))}</title>");
                svg.Append(Invariant($"<circle cx=\"{node.X + node.Width - 2:0.#}\" cy=\"{node.Y + 2:0.#}\" r=\"10\"/>"));
                svg.Append(Invariant($"<text x=\"{node.X + node.Width - 2:0.#}\" y=\"{node.Y + 6:0.#}\" text-anchor=\"middle\">👁</text>"));
                svg.Append("</g>");
            }
            svg.Append("</g>");
        }
        svg.Append("</g>");

        // Labels last, over every cable and box. Each keeps its cable's classes and ends, so layers and hover still apply.
        svg.Append("<g class=\"vpd-labels\">");
        foreach (DiagramEdge edge in layout.Edges.Where(e => e.Intent.Length > 0 || e.IsFeedback))
        {
            svg.Append($"<g class=\"{EdgeClass(edge)}\" data-from=\"{edge.From}\" data-to=\"{edge.To}\">");
            if (edge.Intent.Length > 0)
            {
                svg.Append(Invariant($"<text class=\"label\" x=\"{edge.LabelX:0.#}\" y=\"{IntentY(edge):0.#}\" text-anchor=\"middle\">{Escape(edge.Label)}</text>"));
            }
            if (edge.IsFeedback)
            {
                svg.Append(Invariant($"<text class=\"feedback-tag\" x=\"{edge.LabelX:0.#}\" y=\"{edge.LabelY + FeedbackTagOffset:0.#}\" text-anchor=\"middle\">{FeedbackTag}</text>"));
            }
            svg.Append("</g>");
        }
        svg.Append("</g>");

        AppendLegend(svg, legendRows, layout.Height);
        svg.Append("</svg>");
        return svg.ToString();
    }

    /// <summary>The signals and roles actually drawn: a line sample per signal, a color chip per role.</summary>
    private static List<(string Class, string Text, bool Line)> LegendItems(DiagramLayout layout)
    {
        List<(string Class, string Text, bool Line)> items = layout.Edges
            .Select(e => (e.Signal, e.Layer)).Distinct()
            .OrderBy(s => DiagramLayout.Layers.ToList().IndexOf(s.Layer)).ThenBy(s => s.Signal)
            .Select(s => ($"vpd-edge sig-{SignalClass(s.Signal)}", DiagramLayout.LayerLabel(s.Layer), true))
            .ToList();
        items.AddRange(Enum.GetValues<Core.Catalog.Role>()
            .Where(r => layout.Nodes.Any(n => n.Role == r))
            .Select(r => ($"role-{r.ToString().ToLowerInvariant()}", RoleLabel(r), false)));
        return items;
    }

    private static double LegendWidth((string Class, string Text, bool Line) item) => 26 + (item.Text.Length * legendCharWidth) + 18;

    private static List<List<(string Class, string Text, bool Line)>> WrapLegend(List<(string Class, string Text, bool Line)> items, double width)
    {
        List<List<(string Class, string Text, bool Line)>> rows = new List<List<(string, string, bool)>>();
        double x = width;
        foreach ((string Class, string Text, bool Line) item in items)
        {
            if (x + LegendWidth(item) > width - 16)
            {
                rows.Add(new List<(string, string, bool)>());
                x = 20;
            }
            rows[^1].Add(item);
            x += LegendWidth(item);
        }
        return rows;
    }

    private static void AppendLegend(StringBuilder svg, List<List<(string Class, string Text, bool Line)>> rows, double top)
    {
        if (rows.Count == 0)
        {
            return;
        }
        svg.Append("<g class=\"vpd-legend\">");
        for (int row = 0; row < rows.Count; row++)
        {
            double y = top + (row * legendRow) + 8;
            double x = 20;
            foreach ((string Class, string Text, bool Line) item in rows[row])
            {
                svg.Append($"<g class=\"{item.Class}\">");
                svg.Append(item.Line
                    ? Invariant($"<path d=\"M {x:0.#} {y:0.#} H {x + 20:0.#}\"/>")
                    : Invariant($"<rect class=\"chip\" x=\"{x + 4:0.#}\" y=\"{y - 6:0.#}\" width=\"12\" height=\"12\" rx=\"3\"/>"));
                svg.Append(Invariant($"<text x=\"{x + 26:0.#}\" y=\"{y + 4:0.#}\">{Escape(item.Text)}</text>"));
                svg.Append("</g>");
                x += LegendWidth(item);
            }
        }
        svg.Append("</g>");
    }

    public static string WatchTooltip(DiagramNode node) => "Watched by:\n" + string.Join("\n", node.Watchers);

    public static string BandClass(DiagramBand band) =>
        $"vpd-band band-{band.Band.ToString().ToLowerInvariant()}{(band.Group is not null ? " unfolded" : "")}";

    /// <summary>Always shown on a cable going back against the flow: a loop is worth pointing out when explaining a patch.</summary>
    public const string FeedbackTag = "↺ feedback";

    public const double FeedbackTagOffset = -4;

    /// <summary>Intent labels sit above their cable, or below it when the feedback tag already takes that place.</summary>
    public static double IntentY(DiagramEdge edge) => edge.LabelY + (edge.IsFeedback ? 13 : -4);

    public static string EdgeClass(DiagramEdge edge) =>
        $"vpd-edge sig-{SignalClass(edge.Signal)} layer-{edge.Layer}{(edge.IsSingleCable ? "" : " merged")}{(edge.IsFeedback ? " feedback" : "")}{(edge.IsMonitor ? " monitor" : "")}";

    public static string NodeClass(DiagramNode node) =>
        $"vpd-node role-{node.Role.ToString().ToLowerInvariant()}{(node.IsInsert ? " insert" : "")}{(node.IsFolded ? " folded" : "")}";

    public static string NodeSubtitle(DiagramNode node) => node.Subtitle;

    /// <summary>Room left for the title: module boxes have their function icon in the top right corner.</summary>
    public static int TitleChars(DiagramNode node) => node.Function is null ? 21 : 18;

    /// <summary>Left edge of a port tab: against the box, outside it.</summary>
    public static double TabX(DiagramNode node, PortTab tab) => tab.Output ? node.X + node.Width : node.X - tab.Width;

    public static double IconX(DiagramNode node) => node.X + node.Width - Icons.Width - 8;

    public static double IconY(DiagramNode node) => node.Y + 9;

    /// <summary>Room left for the subtitle: the library link sits at its end.</summary>
    public static int SubtitleChars(DiagramNode node) => node.LibraryKey is null && node.LibraryPanels.Count == 0 ? 30 : 24;

    public const string LibraryTooltip = "Open in the VCV Library";

    public const string PanelsTooltip = "The modules of this group, as panels from the VCV Library";

    /// <summary>
    /// A tooltip for the library icons only in a standalone .svg: pages show a preview on hover instead, and the browser's
    /// tooltip would cover it.
    /// </summary>
    private static string IconTitle(bool standalone, string text) => standalone ? $"<title>{text}</title>" : "";

    /// <summary>"plugin/model*count …", read by library.js (slugs have no spaces).</summary>
    public static string PanelsAttribute(DiagramNode node) => string.Join(" ", node.LibraryPanels.Select(p => $"{p.Key}*{p.Count}"));

    /// <summary>The library link, in the bottom right corner of the box's first row of text, under the function icon.</summary>
    public static double LibraryX(DiagramNode node) => node.X + node.Width - LibraryIcon.Size - 10;

    public static double LibraryY(DiagramNode node) => node.Y + 27;

    /// <summary>Shortens text to fit a box, ending with an ellipsis (full text stays in the tooltip).</summary>
    public static string Fit(string text, int maxChars) => text.Length <= maxChars ? text : text[..(maxChars - 1)].TrimEnd() + "…";

    public static string RoleLabel(Core.Catalog.Role role) => role switch
    {
        Core.Catalog.Role.Io => "I/O",
        _ => role.ToString().ToLowerInvariant(),
    };

    public static string SignalClass(SignalType signal) => signal.ToString().ToLowerInvariant();

    public static string EdgeTooltip(DiagramEdge edge) =>
        $"{edge.FromTitle} '{edge.FromPort}' → {edge.ToTitle} '{edge.ToPort}' ({edge.Signal})\n{edge.Intent}"
        + (edge.IsFeedback ? "\nFeedback: goes back to a module earlier in the flow (a loop)" : "")
        + (edge.IsMonitor ? "\nWatched only: the scope or display makes no sound" : "");

    internal static string Escape(string text) => WebUtility.HtmlEncode(text);

    private static string Invariant(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}