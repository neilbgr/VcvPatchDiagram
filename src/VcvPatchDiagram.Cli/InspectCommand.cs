using VcvPatchDiagram.Core.Analysis;
using VcvPatchDiagram.Core.Patch;

namespace VcvPatchDiagram.Cli;

/// <summary>"inspect": text summary of how the patch is understood (bands, voices, typed cables, diagnostics).</summary>
internal static class InspectCommand
{
    public static int Run(PatchAnalysis analysis, TextWriter output)
    {
        PatchDocument patch = analysis.Patch;
        output.WriteLine($"Rack {patch.RackVersion}: {patch.Modules.Count} modules, {patch.Cables.Count} cables, {analysis.Voices.Count} voices");

        foreach (Band band in Enum.GetValues<Band>())
        {
            List<AnalyzedModule> inBand = analysis.Modules.Where(m => m.Band == band).ToList();
            if (inBand.Count == 0)
            {
                continue;
            }
            output.WriteLine();
            output.WriteLine($"[{band}]");
            if (band == Band.Voice)
            {
                foreach (Voice voice in analysis.Voices)
                {
                    output.WriteLine($"  {voice.Name}");
                    output.WriteLine($"    {string.Join(" → ", voice.ModuleIds.Select(id => analysis.Module(id).Title))}");
                    foreach (AnalyzedModule insert in inBand.Where(m => m.Voice == voice.Index && m.InsertOf is not null))
                    {
                        output.WriteLine($"      + {insert.Title} (insert of {analysis.Module(insert.InsertOf!.Value).Title})");
                    }
                }
                continue;
            }
            foreach (AnalyzedModule module in inBand)
            {
                string attached = module.InsertOf is long host ? $" (insert of {analysis.Module(host).Title})" : "";
                output.WriteLine($"  {module.Title,-24} {module.Role,-10} {module.Plugin}{attached}");
            }
        }

        output.WriteLine();
        output.WriteLine("[Groups] (fold/unfold units, keys usable with render --unfold)");
        foreach (PatchGroup group in PatchGrouping.Build(analysis))
        {
            output.WriteLine($"  {group.Key,-16} {group.Title}");
            output.WriteLine($"  {"",-16}   {string.Join(", ", group.Members.Select(id => analysis.Module(id).Title))}");
        }

        output.WriteLine();
        output.WriteLine("[Cables]");
        foreach (AnalyzedCable cable in analysis.Cables.OrderBy(c => c.Signal))
        {
            string from = analysis.Module(cable.Cable.From.ModuleId).Title;
            string to = analysis.Module(cable.Cable.To.ModuleId).Title;
            output.WriteLine($"  {cable.Signal,-6} {from} '{cable.FromPort}' → {to} '{cable.ToPort}'   ~ {cable.SuggestedIntent}");
        }

        if (analysis.Loops.Count > 0)
        {
            output.WriteLine();
            output.WriteLine("[Loops] (feedback, cross-modulation)");
            foreach (IReadOnlyList<long> loop in analysis.Loops)
            {
                IEnumerable<string> signals = analysis.Cables
                    .Where(c => loop.Contains(c.Cable.From.ModuleId) && loop.Contains(c.Cable.To.ModuleId) && c.Cable.From.ModuleId != c.Cable.To.ModuleId)
                    .Select(c => c.Signal.ToString().ToLowerInvariant()).Distinct();
                output.WriteLine($"  {string.Join(loop.Count == 2 ? " ⇄ " : ", ", loop.Select(id => analysis.Module(id).Title))}   ({string.Join(", ", signals)})");
            }
        }

        if (analysis.Diagnostics.Count > 0)
        {
            output.WriteLine();
            output.WriteLine("[Diagnostics]");
            foreach (string diagnostic in analysis.Diagnostics)
            {
                output.WriteLine($"  ! {diagnostic}");
            }
        }
        return 0;
    }
}