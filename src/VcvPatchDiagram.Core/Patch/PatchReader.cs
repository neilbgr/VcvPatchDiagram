using System.Text.Json.Nodes;
using ZstdSharp;

namespace VcvPatchDiagram.Core.Patch;

/// <summary>
/// Reads a .vcv patch. A .vcv is either raw JSON, or a zstd-compressed tar archive containing
/// "patch.json" (what Rack/Cardinal write from the UI). Detection matches Rack's own check
/// (src/patch.cpp): first 4 bytes == zstd magic. Read-only: the source is never modified.
/// </summary>
public static class PatchReader
{
    private static readonly byte[] zstdMagic = { 0x28, 0xB5, 0x2F, 0xFD };

    public static PatchDocument Read(Stream stream) => Parse(ReadJson(stream));

    public static PatchDocument Read(byte[] bytes) => Parse(ReadJson(bytes));

    public static JsonObject ReadJson(Stream stream)
    {
        using MemoryStream buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return ReadJson(buffer.ToArray());
    }

    public static JsonObject ReadJson(byte[] bytes)
    {
        bool isArchive = bytes.Length >= 4 && bytes.AsSpan(0, 4).SequenceEqual(zstdMagic);
        if (!isArchive)
        {
            return ParseJson(bytes);
        }

        using MemoryStream compressedStream = new MemoryStream(bytes);
        using DecompressionStream tarStream = new DecompressionStream(compressedStream);
        using MemoryStream tarCopy = new MemoryStream();
        tarStream.CopyTo(tarCopy);
        foreach ((string name, byte[] data) in TarEntries.Read(tarCopy.ToArray()))
        {
            if (NormalizeEntryName(name) == "patch.json")
            {
                return ParseJson(data);
            }
        }

        throw new InvalidDataException("The file is a tar+zstd archive but contains no patch.json entry.");
    }

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

    /// <summary>Strips a leading "./" (or repeated ones), matching tar writers that emit paths relative to "./".</summary>
    private static string NormalizeEntryName(string name)
    {
        while (name.StartsWith("./", StringComparison.Ordinal))
        {
            name = name[2..];
        }
        return name;
    }

    private static JsonObject ParseJson(byte[] bytes)
    {
        JsonNode? node = JsonNode.Parse(bytes)
            ?? throw new InvalidDataException("Empty or invalid patch JSON.");
        return node as JsonObject
            ?? throw new InvalidDataException("Root patch JSON value is not an object.");
    }
}