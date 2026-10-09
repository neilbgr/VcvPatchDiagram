using System.Text.Json.Nodes;
using VcvPatchBridge;

namespace VcvPatchBridge.Tests;

/// <summary>Small helper to build in-memory patch.json documents for tests, without touching real files.</summary>
internal static class TestPatchBuilder
{
    public static JsonObject Module(long id, string plugin, string model, long? left = null, long? right = null)
    {
        JsonObject m = new JsonObject
        {
            ["id"] = id,
            ["plugin"] = plugin,
            ["model"] = model,
            ["version"] = "2.0",
            ["params"] = new JsonArray(),
            ["pos"] = new JsonArray { 0, 0 },
        };
        if (left is not null)
        {
            m["leftModuleId"] = left;
        }
        if (right is not null)
        {
            m["rightModuleId"] = right;
        }
        return m;
    }

    public static JsonObject Cable(long id, long outModId, int outId, long inModId, int inId, string color = "#ffffff") => new()
    {
        ["id"] = id,
        ["outputModuleId"] = outModId,
        ["outputId"] = outId,
        ["inputModuleId"] = inModId,
        ["inputId"] = inId,
        ["color"] = color,
    };

    public static JsonObject Root(IEnumerable<JsonObject> modules, IEnumerable<JsonObject>? cables = null)
    {
        JsonArray modulesArray = new JsonArray();
        foreach (JsonObject m in modules)
        {
            modulesArray.Add(m);
        }

        JsonArray cablesArray = new JsonArray();
        foreach (JsonObject c in cables ?? Enumerable.Empty<JsonObject>())
        {
            cablesArray.Add(c);
        }

        return new JsonObject
        {
            ["version"] = "2.1",
            ["zoom"] = 1.0,
            ["modules"] = modulesArray,
            ["cables"] = cablesArray,
        };
    }

    /// <summary>Writes the given root as a raw-JSON .vcv to a temp file and loads it back through the real PatchFile.Load path.</summary>
    public static PatchFile ToPatchFile(JsonObject root)
    {
        string path = Path.Combine(Path.GetTempPath(), $"vcvpatchbridge-test-{Guid.NewGuid():N}.vcv");
        File.WriteAllText(path, root.ToJsonString());
        return PatchFile.Load(path);
    }
}