using System.Text.Json.Nodes;

namespace VcvPatchTools.Core.Patch;

/// <summary>
/// Reads a .vcv patch into a typed, read-only document of its modules and cables
/// (<see cref="PatchArchive"/> handles the file format, and writing a patch back).
/// </summary>
public static class PatchReader
{
    public static PatchDocument Read(Stream stream) => Parse(ReadJson(stream));

    public static PatchDocument Read(byte[] bytes) => Parse(ReadJson(bytes));

    public static JsonObject ReadJson(Stream stream)
    {
        using MemoryStream buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return ReadJson(buffer.ToArray());
    }

    public static JsonObject ReadJson(byte[] bytes) => PatchArchive.Read(bytes).Root;

    public static PatchDocument Parse(JsonObject root)
    {
        List<PatchModule> modules = new List<PatchModule>();
        foreach (JsonNode? node in root["modules"]?.AsArray() ?? new JsonArray())
        {
            if (node is not JsonObject module)
            {
                continue;
            }

            JsonArray? pos = module["pos"]?.AsArray();
            Dictionary<int, double> parameters = new Dictionary<int, double>();
            foreach (JsonNode? param in module["params"]?.AsArray() ?? new JsonArray())
            {
                if (param?["id"] is JsonNode id && param["value"] is JsonNode value)
                {
                    parameters[id.GetValue<int>()] = value.GetValue<double>();
                }
            }

            modules.Add(new PatchModule(
                module["id"]!.GetValue<long>(),
                module["plugin"]?.GetValue<string>() ?? "",
                module["model"]?.GetValue<string>() ?? "",
                pos?[0]?.GetValue<int>() ?? 0,
                pos?[1]?.GetValue<int>() ?? 0)
            {
                Params = parameters,
                LeftModuleId = module["leftModuleId"]?.GetValue<long>(),
                RightModuleId = module["rightModuleId"]?.GetValue<long>(),
                // A module's data is its own: an object for most, a string for some (Ildaeil keeps a Carla XML project).
                LearnedNotes = Integers((module["data"] as JsonObject)?["notes"]),
                LearnedCcs = Integers((module["data"] as JsonObject)?["ccs"]),
            });
        }

        List<PatchCable> cables = new List<PatchCable>();
        long fallbackId = 0;
        foreach (JsonNode? node in root["cables"]?.AsArray() ?? new JsonArray())
        {
            if (node is not JsonObject cable)
            {
                continue;
            }

            fallbackId++;
            cables.Add(new PatchCable(
                cable["id"]?.GetValue<long>() ?? fallbackId,
                new PortRef(cable["outputModuleId"]!.GetValue<long>(), cable["outputId"]!.GetValue<int>()),
                new PortRef(cable["inputModuleId"]!.GetValue<long>(), cable["inputId"]!.GetValue<int>()),
                cable["color"]?.GetValue<string>()));
        }

        return new PatchDocument(root["version"]?.GetValue<string>(), modules, cables);
    }

    private static List<int> Integers(JsonNode? node) =>
        node is JsonArray array ? array.Select(n => n is JsonValue v && v.TryGetValue(out int i) ? i : -1).ToList() : new List<int>();
}