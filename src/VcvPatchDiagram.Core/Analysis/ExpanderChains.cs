using VcvPatchDiagram.Core.Catalog;
using VcvPatchDiagram.Core.Patch;

namespace VcvPatchDiagram.Core.Analysis;

/// <summary>
/// An expander and its base work as one module: the expander only adds options (panning, mutes, sends…) to the
/// module it is docked to. A module is folded into a base when:
///  - it is an expander (tagged "Expander", or "Expander" in its model name: not every expander is tagged);
///  - following its docked neighbours (Rack saves them as leftModuleId/rightModuleId), possibly through other
///    expanders, leads to a module of the same plugin that is not an expander: its base. Left first, the usual side.
/// An expander docked to nothing of its family stays a module of its own.
/// </summary>
public static class ExpanderChains
{
    /// <summary>Base module id of every docked expander.</summary>
    public static Dictionary<long, long> BaseOf(IReadOnlyList<PatchModule> modules, PortCatalog ports)
    {
        Dictionary<long, PatchModule> byId = modules.ToDictionary(m => m.Id);
        bool IsExpander(PatchModule m) =>
            ports.Get(m.Plugin, m.Model).Tags.Contains("Expander", StringComparer.OrdinalIgnoreCase)
            || m.Model.Contains("Expander", StringComparison.OrdinalIgnoreCase);

        long? Walk(PatchModule start, Func<PatchModule, long?> next)
        {
            HashSet<long> seen = new HashSet<long> { start.Id };
            PatchModule current = start;
            while (next(current) is long id && byId.TryGetValue(id, out PatchModule? neighbour) && neighbour.Plugin == start.Plugin && seen.Add(id))
            {
                if (!IsExpander(neighbour))
                {
                    return neighbour.Id;
                }
                current = neighbour;
            }
            return null;
        }

        Dictionary<long, long> baseOf = new Dictionary<long, long>();
        foreach (PatchModule module in modules.Where(IsExpander))
        {
            if ((Walk(module, m => m.LeftModuleId) ?? Walk(module, m => m.RightModuleId)) is long found)
            {
                baseOf[module.Id] = found;
            }
        }
        return baseOf;
    }
}