using System.Text;
using VcvPatchDiagram.Core.Analysis;
using VcvPatchDiagram.Core.Layout;

namespace VcvPatchDiagram.Core.Render;

/// <summary>Graphviz export: one cluster per band, line styles per signal type. Render with "dot -Tsvg".</summary>
public static class DotExporter
{
    public static string Export(DiagramLayout layout)
    {
        StringBuilder dot = new StringBuilder();
        dot.AppendLine($"digraph \"{Quote(layout.Title)}\" {{");
        dot.AppendLine("  rankdir=LR; newrank=true; compound=true; fontname=\"Helvetica\";");
        dot.AppendLine("  node [shape=box, style=\"rounded,filled\", fillcolor=white, fontname=\"Helvetica\", fontsize=11];");
        dot.AppendLine("  edge [fontname=\"Helvetica\", fontsize=9];");

        int clusterIndex = 0;
        foreach (DiagramBand band in layout.Bands)
        {
            List<DiagramNode> members = layout.Members(band).ToList();
            dot.AppendLine($"  subgraph cluster_{clusterIndex++} {{");
            dot.AppendLine($"    label=\"{Quote(band.Title)}\"; style=\"rounded,filled\"; color=\"#e6e4de\"; fillcolor=\"#f4f3ef\";");
            foreach (DiagramNode node in members)
            {
                string style = node.IsInsert || node.IsFolded ? (node.IsFolded ? ", style=\"rounded,filled,bold\", peripheries=2" : ", style=\"rounded,dashed,filled\"") : "";
                dot.AppendLine($"    {Id(node.Key)} [label=\"{Quote(node.Title)}{(node.Watchers.Count > 0 ? " 👁" : "")}\\n{Quote(node.Subtitle)}\", color=\"{RoleColor(node)}\", penwidth=2{style}];");
            }
            dot.AppendLine("  }");
        }

        foreach (DiagramEdge edge in layout.Edges)
        {
            (string color, string style) = edge.Signal switch
            {
                SignalType.Audio => ("#d64545", "penwidth=3"),
                SignalType.Pitch => ("#b97d00", "penwidth=2"),
                SignalType.Gate => ("#2f74c4", "style=dashed"),
                _ => ("#2c9457", "penwidth=1"),
            };
            dot.AppendLine($"  {Id(edge.From)} -> {Id(edge.To)} [color=\"{color}\", {style}, label=\"{Quote(edge.Intent)}\", tooltip=\"{Quote($"{edge.FromPort} → {edge.ToPort}")}\"];");
        }

        dot.AppendLine("}");
        return dot.ToString();
    }

    private static string RoleColor(DiagramNode node) => node.Role switch
    {
        Catalog.Role.Time => "#2f74c4",
        Catalog.Role.Pitch => "#b97d00",
        Catalog.Role.Controller => "#2c9457",
        Catalog.Role.Source => "#d64545",
        Catalog.Role.Modifier => "#c2611f",
        Catalog.Role.Mixer => "#7a52b3",
        Catalog.Role.Effect => "#b0479a",
        Catalog.Role.Monitor => "#9a958a",
        _ => "#6b6b70",
    };

    private static string Quote(string text) => text.Replace("\\", "\\\\").Replace("\"", "\\\"");

    /// <summary>"m123" / "g:voice-1" → a valid identifier.</summary>
    private static string Id(string key) => "n_" + new string(key.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());
}