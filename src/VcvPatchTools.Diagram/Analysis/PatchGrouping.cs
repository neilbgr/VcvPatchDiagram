using System.Globalization;
using VcvPatchTools.Core.Catalog;

namespace VcvPatchTools.Diagram.Analysis;

/// <summary>A foldable part of the patch: a voice (sound + what animates it), the bus, or a band of shared modules.</summary>
/// <param name="Key">Stable id ("voice-1", "bus", "time"…), used to remember which groups are unfolded.</param>
/// <param name="Count">Number of identical voices folded together (four drones = 1 group, Count 4).</param>
public sealed record PatchGroup(string Key, Band Band, string Title, IReadOnlyList<long> Members, IReadOnlyList<int> Voices, int Count, Role Role);

/// <summary>
/// Splits a patch into groups the way it's explained: a voice is its sound path plus the modulators and pitch
/// processors that only serve it; the bus is mixers, bus effects and what only modulates them; whatever is
/// shared (external control, sequencing, shared modulators) keeps its own group.
/// </summary>
public static class PatchGrouping
{
    public static IReadOnlyList<PatchGroup> Build(PatchAnalysis analysis)
    {
        Dictionary<long, string> groupOf = new Dictionary<long, string>();
        List<(string Key, Band Band, string Title, List<int> Voices, Role Role)> groups = new List<(string, Band, string, List<int>, Role)>();

        // Voices: identical one-module voices (e.g. four drones) fold into one group.
        foreach (IGrouping<string, Voice> lane in analysis.Voices.GroupBy(v =>
            v.ModuleIds.Count == 1 ? "single:" + analysis.Module(v.ModuleIds[0]).Module.CatalogKey : "voice:" + v.Index.ToString(CultureInfo.InvariantCulture)))
        {
            List<Voice> voices = lane.ToList();
            string key = "voice-" + string.Join("-", voices.Select(v => v.Index + 1));
            string title = voices.Count == 1 ? voices[0].Name : $"Voices {string.Join(", ", voices.Select(v => v.Index + 1))}: {BaseTitle(voices[0].Sources)} ×{voices.Count}";
            groups.Add((key, Band.Voice, title, voices.Select(v => v.Index).ToList(), Role.Source));
            foreach (AnalyzedModule module in analysis.Modules.Where(m => m.Voice is int v && voices.Any(x => x.Index == v)))
            {
                groupOf[module.Module.Id] = key;
            }
        }

        foreach (AnalyzedModule module in analysis.Modules.Where(m => m.Band == Band.Bus))
        {
            groupOf[module.Module.Id] = "bus";
        }

        // Modulators and pitch processors whose every cable goes into one voice (or the bus) belong to it.
        // Repeat so chains (LFO → envelope → filter) follow along.
        List<AnalyzedModule> candidates = analysis.Modules.Where(m => m.Band is Band.Modulation or Band.Pitch).ToList();
        for (bool changed = true; changed;)
        {
            changed = false;
            foreach (AnalyzedModule candidate in candidates.Where(c => !groupOf.ContainsKey(c.Module.Id)))
            {
                // Cables to scopes don't count: watching a signal doesn't make it shared.
                List<string?> targets = analysis.Cables
                    .Where(c => c.Cable.From.ModuleId == candidate.Module.Id && analysis.Module(c.Cable.To.ModuleId).Band != Band.Monitor)
                    .Select(c => groupOf.TryGetValue(c.Cable.To.ModuleId, out string? g) ? g : null)
                    .Distinct()
                    .ToList();
                if (targets.Count == 1 && targets[0] is string owner)
                {
                    groupOf[candidate.Module.Id] = owner;
                    changed = true;
                }
            }
        }

        // Whatever is left is shared: one group per band.
        foreach (AnalyzedModule module in analysis.Modules.Where(m => !groupOf.ContainsKey(m.Module.Id)))
        {
            string key = module.Band.ToString().ToLowerInvariant();
            groupOf[module.Module.Id] = key;
            if (!groups.Any(g => g.Key == key))
            {
                groups.Add((key, module.Band, BandTitle(module.Band, shared: true), new List<int>(), RoleOf(module.Band)));
            }
        }
        if (groupOf.ContainsValue("bus"))
        {
            groups.Add(("bus", Band.Bus, BandTitle(Band.Bus, shared: false), new List<int>(), Role.Mixer));
        }

        return groups
            .OrderBy(g => g.Band)
            .Select(g => new PatchGroup(
                g.Key, g.Band, g.Title,
                analysis.Modules.Where(m => groupOf[m.Module.Id] == g.Key).Select(m => m.Module.Id).ToList(),
                g.Voices, Math.Max(1, g.Voices.Count), g.Role))
            .ToList();
    }

    /// <summary>Short name for a folded box.</summary>
    public static string BoxTitle(Band band) => band switch
    {
        Band.External => "Played live",
        Band.Time => "Sequencing",
        Band.Pitch => "Pitch processing",
        Band.Modulation => "Shared modulation",
        Band.Voice => "Voice",
        Band.Monitor => "Scopes & displays",
        _ => "Mix & effects",
    };

    public static string BandTitle(Band band, bool shared) => band switch
    {
        Band.External => "Performance (MIDI, host, played live)",
        Band.Time => "Time & sequencing",
        Band.Pitch => shared ? "Shared pitch processing" : "Pitch",
        Band.Modulation => shared ? "Shared modulation" : "Modulation",
        Band.Voice => "Voices",
        Band.Monitor => "Monitoring (scopes, displays)",
        _ => "Mix & effects",
    };

    private static Role RoleOf(Band band) => band switch
    {
        Band.External => Role.Performance,
        Band.Time => Role.Time,
        Band.Pitch => Role.Pitch,
        Band.Bus => Role.Mixer,
        Band.Monitor => Role.Monitor,
        _ => Role.Controller,
    };

    private static string BaseTitle(string title)
    {
        int hash = title.LastIndexOf(" #", StringComparison.Ordinal);
        return hash > 0 ? title[..hash] : title;
    }
}