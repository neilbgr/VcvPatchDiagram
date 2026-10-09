using System.Text.Json.Nodes;

namespace VcvPatchBridge;

public sealed class ConversionResult
{
    public List<string> Warnings { get; } = new();
}

/// <summary>
/// Converts the module list of a loaded patch between the VCV Rack ("Core" plugin) and
/// Cardinal ("Cardinal" plugin) representations of the host-dependent MIDI/Audio/etc. modules.
/// Mutates patch.Root in place.
/// </summary>
public static class PatchConverter
{
    private static readonly Random idRandom = new();

    public static ConversionResult Convert(PatchFile patch, PatchOrigin targetOrigin, bool remapCableColors = true)
    {
        ConversionResult result = new ConversionResult();

        JsonArray modulesArray = patch.Root["modules"] as JsonArray
            ?? throw new InvalidDataException("Patch has no \"modules\" array.");
        JsonArray cablesArray = patch.Root["cables"] as JsonArray ?? new JsonArray();
        patch.Root["cables"] ??= cablesArray;

        if (remapCableColors)
        {
            RemapCableColors(cablesArray, targetOrigin);
        }

        List<JsonObject> modules = modulesArray.Select(m => (JsonObject)m!.AsObject()).ToList();
        // Detach everything from the original array up front so nodes are free to move around.
        modulesArray.Clear();

        List<JsonObject> converted = targetOrigin == PatchOrigin.Rack
            ? ConvertCardinalToRack(modules, cablesArray, result.Warnings)
            : ConvertRackToCardinal(modules, cablesArray, result.Warnings);

        foreach (JsonObject m in converted)
        {
            modulesArray.Add(m);
        }

        return result;
    }

    private static void RemapCableColors(JsonArray cables, PatchOrigin targetOrigin)
    {
        foreach (JsonObject cable in cables.OfType<JsonObject>())
        {
            if (cable["color"]?.GetValue<string>() is not string color)
            {
                continue;
            }

            cable["color"] = targetOrigin == PatchOrigin.Rack
                ? CableColorMap.MapToRack(color)
                : CableColorMap.MapToCardinal(color);
        }
    }

    // ---------------------------------------------------------------- Cardinal -> Rack

    private static List<JsonObject> ConvertCardinalToRack(List<JsonObject> modules, JsonArray cables, List<string> warnings)
    {
        CompactHostMidiGateCells(modules, cables, warnings);

        List<JsonObject> output = new List<JsonObject>();
        Dictionary<JsonObject, double> newX = ComputeSplitReflow(modules, cables);

        foreach (JsonObject module in modules)
        {
            double x = newX[module];
            double y = ReadPos(module).y;

            string? plugin = module["plugin"]?.GetValue<string>();
            string? model = module["model"]?.GetValue<string>();

            if (plugin != ModuleMap.CardinalPlugin || model is null)
            {
                SetPos(module, x, y);
                output.Add(module);
                continue;
            }

            MidiSplitMapping? split = ModuleMap.FindByCardinalModel(model);
            if (split is not null)
            {
                (JsonObject? inModule, JsonObject? outModule) = SplitMidiModule(module, split, x, y, cables, warnings);
                if (inModule is not null)
                {
                    output.Add(inModule);
                }
                if (outModule is not null)
                {
                    output.Add(outModule);
                }
                if (inModule is null && outModule is null)
                {
                    warnings.Add($"Cardinal module \"{model}\" (id {module["id"]}) has no cables connected: dropped instead of converted.");
                }
                continue;
            }

            if (model == ModuleMap.CardinalTextEditorModel)
            {
                SetPos(module, x, y);
                output.Add(RenameTextEditorToNotes(module));
                continue;
            }

            if (ModuleMap.CardinalToCoreSimple.TryGetValue(model, out string? coreModel))
            {
                SetPos(module, x, y);
                module["plugin"] = ModuleMap.CorePlugin;
                module["model"] = coreModel;
                output.Add(module);
                continue;
            }

            // No Rack equivalent: leave the module untouched (but still reflowed), warn.
            SetPos(module, x, y);
            warnings.Add($"Cardinal module \"{model}\" (id {module["id"]}) has no VCV Rack equivalent: copied as-is, will not load in Rack.");
            output.Add(module);
        }

        return output;
    }

    /// <summary>
    /// Splitting a duplex Cardinal MIDI module into two separate Rack modules almost always widens
    /// its footprint (e.g. HostMIDICC's 14 HP becomes 20 HP as MIDICCToCVInterface + CV-CC). Left
    /// alone, the extra width lands on top of whatever module used to sit right next to it, and
    /// Rack's own overlap resolution on load then shuffles unrelated modules around unpredictably.
    /// This computes, per row (same Y), how far every module must shift right to keep the original
    /// touching/gap relationships intact once any splits on that row have widened it.
    /// </summary>
    private static Dictionary<JsonObject, double> ComputeSplitReflow(List<JsonObject> modules, JsonArray cables)
    {
        Dictionary<JsonObject, double> newX = new Dictionary<JsonObject, double>();

        foreach (IGrouping<double, (JsonObject Module, (double x, double y) Pos)> row in modules.Select(m => (Module: m, Pos: ReadPos(m))).GroupBy(t => t.Pos.y))
        {
            double shift = 0;
            foreach ((JsonObject module, (double x, double y) pos) in row.OrderBy(t => t.Pos.x))
            {
                newX[module] = pos.x + shift;

                if (module["plugin"]?.GetValue<string>() == ModuleMap.CardinalPlugin
                    && module["model"]?.GetValue<string>() is string model
                    && ModuleMap.FindByCardinalModel(model) is { } split)
                {
                    long oldId = module["id"]!.GetValue<long>();
                    (bool usedIn, bool usedOut) = DetermineSplitUsage(oldId, split, cables);
                    int newWidth = (usedIn ? split.RackInWidthHp : 0) + (usedOut ? split.RackOutWidthHp : 0);
                    shift += newWidth - split.CardinalWidthHp;
                }
            }
        }

        return newX;
    }

    /// <summary>Whether HostMIDI/CC/Gate's MIDI-in half (outputs) and/or MIDI-out half (inputs) is actually cabled.</summary>
    private static (bool usedIn, bool usedOut) DetermineSplitUsage(long oldId, MidiSplitMapping split, JsonArray cables)
    {
        bool usedIn = false, usedOut = false;
        foreach (JsonObject cable in cables.OfType<JsonObject>())
        {
            if (cable["outputModuleId"]!.GetValue<long>() == oldId && cable["outputId"]!.GetValue<int>() < split.Outputs.Count)
            {
                usedIn = true;
            }
            if (cable["inputModuleId"]!.GetValue<long>() == oldId && cable["inputId"]!.GetValue<int>() < split.Inputs.Count)
            {
                usedOut = true;
            }
        }
        return (usedIn, usedOut);
    }

    /// <summary>
    /// Cardinal's HostMIDIGate has 18 gate cells (id 0-17, shared between its MIDI-in and MIDI-out
    /// halves); VCV Rack's MIDITriggerToCVInterface/CV-Gate only have 16 each. Truncating anything at
    /// index >= 16 (as if cells were reserved 1:1 by index) silently drops whichever cells happen to
    /// sit at the high end, even when low-numbered cells are unused - and worse, on reload the kept
    /// cables no longer line up with the notes a patcher expects at that port. Instead, compact
    /// whichever cells are actually cabled down into Rack's 0..15 range, in ascending original-id
    /// order, carrying their notes and cable port indices along; only warn about cells that don't fit
    /// (i.e. this particular module has more than 16 cells in simultaneous use).
    /// </summary>
    private static void CompactHostMidiGateCells(List<JsonObject> modules, JsonArray cables, List<string> warnings)
    {
        const int rackCapacity = 16;

        foreach (JsonObject module in modules)
        {
            if (module["plugin"]?.GetValue<string>() != ModuleMap.CardinalPlugin || module["model"]?.GetValue<string>() != "HostMIDIGate")
            {
                continue;
            }

            long oldId = module["id"]!.GetValue<long>();

            SortedSet<int> usedCells = new SortedSet<int>();
            foreach (JsonObject cable in cables.OfType<JsonObject>())
            {
                if (cable["outputModuleId"]!.GetValue<long>() == oldId)
                {
                    usedCells.Add(cable["outputId"]!.GetValue<int>());
                }
                if (cable["inputModuleId"]!.GetValue<long>() == oldId)
                {
                    usedCells.Add(cable["inputId"]!.GetValue<int>());
                }
            }

            if (usedCells.Count == 0 || usedCells.Max() < rackCapacity)
            {
                continue; // already fits Rack's range as-is, nothing to compact
            }

            List<int> orderedCells = usedCells.ToList(); // SortedSet enumerates ascending
            Dictionary<int, int> remap = new Dictionary<int, int>();
            for (int i = 0; i < orderedCells.Count && i < rackCapacity; i++)
            {
                remap[orderedCells[i]] = i;
            }

            foreach (int overflowCell in orderedCells.Skip(rackCapacity))
            {
                warnings.Add($"Cardinal module \"HostMIDIGate\" (id {oldId}) has more than {rackCapacity} gate cells in use; cell #{overflowCell + 1}'s cable(s) have no free VCV Rack slot and were dropped.");
            }

            foreach (JsonObject cable in cables.OfType<JsonObject>().ToList())
            {
                bool removed = false;
                if (cable["outputModuleId"]!.GetValue<long>() == oldId)
                {
                    int cell = cable["outputId"]!.GetValue<int>();
                    if (remap.TryGetValue(cell, out int newCell))
                    {
                        cable["outputId"] = newCell;
                    }
                    else
                    {
                        cables.Remove(cable);
                        removed = true;
                    }
                }
                if (!removed && cable["inputModuleId"]!.GetValue<long>() == oldId)
                {
                    int cell = cable["inputId"]!.GetValue<int>();
                    if (remap.TryGetValue(cell, out int newCell))
                    {
                        cable["inputId"] = newCell;
                    }
                    else
                    {
                        cables.Remove(cable);
                    }
                }
            }

            if (module["data"] is JsonObject data && data["notes"] is JsonArray oldNotes)
            {
                JsonArray newNotes = new JsonArray();
                for (int i = 0; i < rackCapacity; i++)
                {
                    newNotes.Add(-1);
                }
                foreach ((int oldCell, int newCell) in remap)
                {
                    newNotes[newCell] = oldCell < oldNotes.Count ? oldNotes[oldCell]!.GetValue<int>() : -1;
                }
                data["notes"] = newNotes;
            }
        }
    }

    private static (JsonObject? inModule, JsonObject? outModule) SplitMidiModule(
        JsonObject cardinalModule, MidiSplitMapping split, double x, double y, JsonArray cables, List<string> warnings)
    {
        long oldId = cardinalModule["id"]!.GetValue<long>();
        (bool usedIn, bool usedOut) = DetermineSplitUsage(oldId, split, cables);

        long inId = usedIn ? NewModuleId() : 0;
        long outId = usedOut ? NewModuleId() : 0;

        JsonObject? inModule = usedIn ? NewModuleObject(ModuleMap.CorePlugin, split.RackInModel, inId, x, y) : null;
        JsonObject? outModule = usedOut
            ? NewModuleObject(ModuleMap.CorePlugin, split.RackOutModel, outId, x + (usedIn ? split.RackInWidthHp : 0), y)
            : null;

        foreach (JsonObject cable in cables.OfType<JsonObject>().ToList())
        {
            long outputModuleId = cable["outputModuleId"]!.GetValue<long>();
            long inputModuleId = cable["inputModuleId"]!.GetValue<long>();

            if (outputModuleId == oldId)
            {
                int portIndex = cable["outputId"]!.GetValue<int>();
                if (portIndex < split.Outputs.Count)
                {
                    cable["outputModuleId"] = inId;
                }
                else
                {
                    string label = ExtraPortLabel(split.UnmappedExtraOutputs, portIndex, split.Outputs.Count);
                    warnings.Add($"Cable removed: output \"{label}\" of Cardinal module \"{split.CardinalModel}\" (id {oldId}) has no VCV Rack equivalent.");
                    cables.Remove(cable);
                }
            }

            if (inputModuleId == oldId)
            {
                int portIndex = cable["inputId"]!.GetValue<int>();
                if (portIndex < split.Inputs.Count)
                {
                    cable["inputModuleId"] = outId;
                }
                else
                {
                    string label = ExtraPortLabel(split.UnmappedExtraInputs, portIndex, split.Inputs.Count);
                    warnings.Add($"Cable removed: input \"{label}\" of Cardinal module \"{split.CardinalModel}\" (id {oldId}) has no VCV Rack equivalent.");
                    cables.Remove(cable);
                }
            }
        }

        ApplyCardinalMidiData(cardinalModule, split, inModule, outModule);

        return (inModule, outModule);
    }

    /// <summary>
    /// Carries over the settings a Cardinal Host MIDI/CC/Gate module keeps in its "data" block
    /// (MIDI channel, learned CC numbers, learned gate notes) onto the Rack module(s) replacing it.
    /// Field names ("ccs", "notes", "velocity", "smooth", "mpeMode", "lsbMode") are identical on both
    /// sides; only the MIDI channel needs converting (Cardinal input channel is 1-based with 0 = "All",
    /// Rack's midi::Port channel is 0-based with -1 = "All"; output channel is 0-based on both sides).
    /// </summary>
    private static void ApplyCardinalMidiData(JsonObject cardinalModule, MidiSplitMapping split, JsonObject? inModule, JsonObject? outModule)
    {
        JsonObject? data = cardinalModule["data"] as JsonObject;
        int inputChannel = data?["inputChannel"]?.GetValue<int>() ?? 0;
        int outputChannel = data?["outputChannel"]?.GetValue<int>() ?? 0;
        int rackInChannel = inputChannel == 0 ? -1 : inputChannel - 1;

        switch (split.CardinalModel)
        {
            case "HostMIDI":
                if (inModule is not null)
                {
                    JsonObject inData = new JsonObject { ["midi"] = new JsonObject { ["channel"] = rackInChannel } };
                    CopyBool(data, "smooth", inData);
                    CopyInt(data, "channels", inData);
                    CopyInt(data, "polyMode", inData);
                    inModule["data"] = inData;
                }
                if (outModule is not null)
                {
                    outModule["data"] = new JsonObject { ["midi"] = new JsonObject { ["channel"] = outputChannel } };
                }
                break;

            case "HostMIDICC":
                JsonArray? ccs = data?["ccs"] as JsonArray;
                if (inModule is not null)
                {
                    JsonObject inData = new JsonObject { ["midi"] = new JsonObject { ["channel"] = rackInChannel } };
                    CopyBool(data, "smooth", inData);
                    CopyBool(data, "mpeMode", inData);
                    CopyBool(data, "lsbMode", inData);
                    if (ccs is not null)
                    {
                        inData["ccs"] = CloneArray(ccs);
                    }
                    inModule["data"] = inData;
                }
                if (outModule is not null)
                {
                    JsonObject outData = new JsonObject { ["midi"] = new JsonObject { ["channel"] = outputChannel } };
                    if (ccs is not null)
                    {
                        outData["ccs"] = CloneArray(ccs);
                    }
                    outModule["data"] = outData;
                }
                break;

            case "HostMIDIGate":
                JsonArray? notes = data?["notes"] as JsonArray;
                if (inModule is not null)
                {
                    JsonObject inData = new JsonObject { ["midi"] = new JsonObject { ["channel"] = rackInChannel } };
                    CopyBool(data, "velocity", inData);
                    CopyBool(data, "mpeMode", inData);
                    if (notes is not null)
                    {
                        inData["notes"] = CloneArray(notes);
                    }
                    inModule["data"] = inData;
                }
                if (outModule is not null)
                {
                    JsonObject outData = new JsonObject { ["midi"] = new JsonObject { ["channel"] = outputChannel } };
                    CopyBool(data, "velocity", outData);
                    if (notes is not null)
                    {
                        outData["notes"] = CloneArray(notes);
                    }
                    outModule["data"] = outData;
                }
                break;
        }
    }

    private static void CopyBool(JsonObject? source, string key, JsonObject target)
    {
        if (source?[key]?.GetValue<bool>() is bool value)
        {
            target[key] = value;
        }
    }

    private static void CopyInt(JsonObject? source, string key, JsonObject target)
    {
        if (source?[key]?.GetValue<int>() is int value)
        {
            target[key] = value;
        }
    }

    private static JsonArray CloneArray(JsonArray array) => (JsonArray)JsonNode.Parse(array.ToJsonString())!;

    private static JsonObject RenameTextEditorToNotes(JsonObject module)
    {
        string text = module["data"]?["etext"]?.GetValue<string>() ?? "";
        long id = module["id"]!.GetValue<long>();
        (double x, double y) = ReadPos(module);

        JsonObject notes = NewModuleObject(ModuleMap.CorePlugin, ModuleMap.CoreNotesModel, id, x, y);
        notes["data"] = new JsonObject { ["text"] = text };
        return notes;
    }

    // ---------------------------------------------------------------- Rack -> Cardinal

    private static List<JsonObject> ConvertRackToCardinal(List<JsonObject> modules, JsonArray cables, List<string> warnings)
    {
        HashSet<JsonObject> consumed = new HashSet<JsonObject>();
        List<JsonObject> output = new List<JsonObject>();

        foreach (MidiSplitMapping mapping in ModuleMap.MidiSplits)
        {
            List<JsonObject> inCandidates = modules.Where(m => !consumed.Contains(m) && IsModel(m, ModuleMap.CorePlugin, mapping.RackInModel)).ToList();

            foreach (JsonObject inModule in inCandidates)
            {
                JsonObject? outModule = FindAdjacent(inModule, modules, consumed, ModuleMap.CorePlugin, mapping.RackOutModel);

                long inId = inModule["id"]!.GetValue<long>();
                long newId = NewModuleId();
                (double x, double y) = ReadPos(inModule);
                JsonObject merged = NewModuleObject(ModuleMap.CardinalPlugin, mapping.CardinalModel, newId, x, y);

                foreach (JsonObject cable in cables.OfType<JsonObject>())
                {
                    if (cable["outputModuleId"]!.GetValue<long>() == inId)
                    {
                        cable["outputModuleId"] = newId;
                    }
                }

                consumed.Add(inModule);

                if (outModule is not null)
                {
                    long outId = outModule["id"]!.GetValue<long>();
                    foreach (JsonObject cable in cables.OfType<JsonObject>())
                    {
                        if (cable["inputModuleId"]!.GetValue<long>() == outId)
                        {
                            cable["inputModuleId"] = newId;
                        }
                    }
                    consumed.Add(outModule);
                }
                else
                {
                    warnings.Add($"Rack module \"{mapping.RackInModel}\" (id {inId}) converted alone into \"{mapping.CardinalModel}\": no adjacent \"{mapping.RackOutModel}\" found, the merged module's MIDI-out inputs will remain unconnected.");
                }

                output.Add(merged);
            }

            // Any leftover "out" modules (CV-MIDI / CV-CC / CV-Gate) without a matching "in" pair.
            List<JsonObject> outLeftovers = modules.Where(m => !consumed.Contains(m) && IsModel(m, ModuleMap.CorePlugin, mapping.RackOutModel)).ToList();
            foreach (JsonObject outModule in outLeftovers)
            {
                long outId = outModule["id"]!.GetValue<long>();
                long newId = NewModuleId();
                (double x, double y) = ReadPos(outModule);
                JsonObject merged = NewModuleObject(ModuleMap.CardinalPlugin, mapping.CardinalModel, newId, x, y);

                foreach (JsonObject cable in cables.OfType<JsonObject>())
                {
                    if (cable["inputModuleId"]!.GetValue<long>() == outId)
                    {
                        cable["inputModuleId"] = newId;
                    }
                }

                consumed.Add(outModule);
                warnings.Add($"Rack module \"{mapping.RackOutModel}\" (id {outId}) converted alone into \"{mapping.CardinalModel}\": no adjacent \"{mapping.RackInModel}\" found, the merged module's MIDI-in outputs will remain unconnected.");
                output.Add(merged);
            }
        }

        foreach (JsonObject module in modules)
        {
            if (consumed.Contains(module))
            {
                continue;
            }

            string? plugin = module["plugin"]?.GetValue<string>();
            string? model = module["model"]?.GetValue<string>();

            if (plugin != ModuleMap.CorePlugin || model is null)
            {
                output.Add(module);
                continue;
            }

            if (model == ModuleMap.CoreNotesModel)
            {
                output.Add(RenameNotesToTextEditor(module));
                continue;
            }

            if (ModuleMap.CoreToCardinalSimple.TryGetValue(model, out string? cardinalModel))
            {
                module["plugin"] = ModuleMap.CardinalPlugin;
                module["model"] = cardinalModel;
                output.Add(module);
                continue;
            }

            if (ModuleMap.CoreNoEquivalent.Contains(model))
            {
                warnings.Add($"VCV Rack module \"{model}\" (id {module["id"]}) has no Cardinal equivalent: copied as-is, will not load in Cardinal.");
            }

            output.Add(module);
        }

        return output;
    }

    private static JsonObject? FindAdjacent(JsonObject module, List<JsonObject> allModules, HashSet<JsonObject> consumed, string plugin, string model)
    {
        long? left = module["leftModuleId"]?.GetValue<long>();
        long? right = module["rightModuleId"]?.GetValue<long>();

        foreach (JsonObject candidate in allModules)
        {
            if (consumed.Contains(candidate) || !IsModel(candidate, plugin, model))
            {
                continue;
            }

            long candidateId = candidate["id"]!.GetValue<long>();
            if (candidateId == left || candidateId == right)
            {
                return candidate;
            }

            // Also match the reverse link, in case only the neighbour records the adjacency.
            long? candidateLeft = candidate["leftModuleId"]?.GetValue<long>();
            long? candidateRight = candidate["rightModuleId"]?.GetValue<long>();
            long moduleId = module["id"]!.GetValue<long>();
            if (candidateLeft == moduleId || candidateRight == moduleId)
            {
                return candidate;
            }
        }

        return null;
    }

    private static JsonObject RenameNotesToTextEditor(JsonObject module)
    {
        string text = module["data"]?["text"]?.GetValue<string>() ?? "";
        long id = module["id"]!.GetValue<long>();
        (double x, double y) = ReadPos(module);

        JsonObject editor = NewModuleObject(ModuleMap.CardinalPlugin, ModuleMap.CardinalTextEditorModel, id, x, y);
        editor["data"] = new JsonObject
        {
            ["filepath"] = "",
            ["lang"] = "None",
            ["etext"] = text,
            ["width"] = 23,
        };
        return editor;
    }

    // ---------------------------------------------------------------- helpers

    private static bool IsModel(JsonObject module, string plugin, string model) =>
        module["plugin"]?.GetValue<string>() == plugin && module["model"]?.GetValue<string>() == model;

    private static (double x, double y) ReadPos(JsonObject module)
    {
        if (module["pos"] is JsonArray { Count: 2 } pos)
        {
            return (pos[0]!.GetValue<double>(), pos[1]!.GetValue<double>());
        }
        return (0, 0);
    }

    private static void SetPos(JsonObject module, double x, double y) =>
        module["pos"] = new JsonArray { x, y };

    private static JsonObject NewModuleObject(string plugin, string model, long id, double x, double y) => new()
    {
        ["id"] = id,
        ["plugin"] = plugin,
        ["model"] = model,
        ["version"] = "2.0",
        ["params"] = new JsonArray(),
        ["pos"] = new JsonArray { x, y },
    };

    private static string ExtraPortLabel(string[]? labels, int index, int baseCount)
    {
        int extraIndex = index - baseCount;
        if (labels is not null && extraIndex >= 0 && extraIndex < labels.Length)
        {
            return labels[extraIndex];
        }
        return $"port #{index}";
    }

    private static long NewModuleId()
    {
        // Rack module ids are just unique 53-bit-ish integers; a random positive long is fine.
        Span<byte> buf = stackalloc byte[8];
        idRandom.NextBytes(buf);
        long value = BitConverter.ToInt64(buf) & 0x1F_FFFF_FFFF_FFFF; // keep it positive and JS-safe-ish
        return value == 0 ? 1 : value;
    }
}