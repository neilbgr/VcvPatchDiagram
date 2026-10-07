using System.Globalization;
using System.Net;
using System.Text;
using VcvPatchDiagram.Core.Analysis;
using VcvPatchDiagram.Core.Layout;

namespace VcvPatchDiagram.Core.Render;

/// <summary>Draws a <see cref="DiagramLayout"/> as SVG with semantic classes (roles, signals, layers) styled by diagram.css.</summary>
public static class SvgRenderer
{
    /// <param name="standalone">Embeds the stylesheet and wraps in a .vpd-root group, so the .svg file renders on its own.</param>
    public static string Render(DiagramLayout layout, bool standalone)
    {
        StringBuilder svg = new StringBuilder();
        svg.Append(Invariant($"<svg xmlns=\"http://www.w3.org/2000/svg\" class=\"vpd-svg{(standalone ? " vpd-root" : "")}\" viewBox=\"0 0 {layout.Width:0} {layout.Height:0}\" width=\"{layout.Width:0}\" height=\"{layout.Height:0}\" role=\"img\" aria-label=\"{Escape(layout.Title)}\">"));
        if (standalone)
        {
            svg.Append("<style>").Append(Resources.Css).Append("</style>");
            svg.Append(Invariant($"<rect width=\"{layout.Width:0}\" height=\"{layout.Height:0}\" style=\"fill: var(--vpd-bg)\"/>"));
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
            svg.Append($"<g class=\"{BandClass(band)}\">");
            svg.Append(Invariant($"<rect x=\"8\" y=\"{band.Y:0.#}\" width=\"{layout.Width - 16:0.#}\" height=\"{band.Height:0.#}\" rx=\"8\"/>"));
            svg.Append(Invariant($"<text x=\"20\" y=\"{band.Y + 19:0.#}\">{Escape(band.Title)}</text>"));
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
            svg.Append(Invariant($"<rect class=\"box\" x=\"{node.X:0.#}\" y=\"{node.Y:0.#}\" width=\"{node.Width:0.#}\" height=\"{node.Height:0.#}\" rx=\"6\"/>"));
            svg.Append(Invariant($"<rect class=\"stripe\" x=\"{node.X:0.#}\" y=\"{node.Y:0.#}\" width=\"5\" height=\"{node.Height:0.#}\" rx=\"2\"/>"));
            svg.Append(Invariant($"<text class=\"title\" x=\"{node.X + 14:0.#}\" y=\"{node.Y + 20:0.#}\">{Escape(Fit(node.Title, 21))}</text>"));
            svg.Append(Invariant($"<text class=\"subtitle\" x=\"{node.X + 14:0.#}\" y=\"{node.Y + 36:0.#}\">{Escape(Fit(NodeSubtitle(node), 30))}</text>"));
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

        svg.Append("</svg>");
        return svg.ToString();
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
        $"vpd-edge sig-{SignalClass(edge.Signal)} layer-{edge.Layer}{(edge.IsSingleCable ? "" : " merged")}{(edge.IsFeedback ? " feedback" : "")}";

    public static string NodeClass(DiagramNode node) =>
        $"vpd-node role-{node.Role.ToString().ToLowerInvariant()}{(node.IsInsert ? " insert" : "")}{(node.IsFolded ? " folded" : "")}";

    public static string NodeSubtitle(DiagramNode node) => node.IsFolded ? node.Subtitle : $"{node.Subtitle} · {RoleLabel(node.Role)}";

    /// <summary>Shortens text to fit a box, ending with an ellipsis (full text stays in the tooltip).</summary>
    public static string Fit(string text, int maxChars) => text.Length <= maxChars ? text : text[..(maxChars - 1)].TrimEnd() + "…";

    public static string RoleLabel(Catalog.Role role) => role switch
    {
        Catalog.Role.Io => "I/O",
        _ => role.ToString().ToLowerInvariant(),
    };

    public static string SignalClass(SignalType signal) => signal.ToString().ToLowerInvariant();

    public static string EdgeTooltip(DiagramEdge edge) =>
        $"{edge.FromTitle} '{edge.FromPort}' → {edge.ToTitle} '{edge.ToPort}' ({edge.Signal})\n{edge.Intent}"
        + (edge.IsFeedback ? "\nFeedback: goes back to a module earlier in the flow (a loop)" : "");

    internal static string Escape(string text) => WebUtility.HtmlEncode(text);

    private static string Invariant(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}