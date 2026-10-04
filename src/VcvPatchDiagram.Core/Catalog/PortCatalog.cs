using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace VcvPatchDiagram.Core.Catalog;

/// <summary>
/// Port names and module metadata, keyed by "Plugin/Model". Built offline from the plugin sources
/// (see <see cref="SourceScanner"/>), then merged with hand-written overrides that always win.
/// </summary>
public sealed class PortCatalog
{
    private static readonly JsonSerializerOptions jsonOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly Dictionary<string, ModuleInfo> modules;

    public PortCatalog(IDictionary<string, ModuleInfo> modules) => this.modules = new Dictionary<string, ModuleInfo>(modules, StringComparer.Ordinal);

    public IReadOnlyDictionary<string, ModuleInfo> Modules => modules;

    /// <summary>The generated catalog merged with the overrides, both embedded in this assembly.</summary>
    public static PortCatalog LoadEmbedded()
    {
        PortCatalog generated = FromJson(ReadResource("catalog.ports.json") ?? "{}");
        PortCatalog overrides = FromJson(ReadResource("catalog.ports.overrides.json") ?? "{}");
        return generated.MergedWith(overrides);
    }

    public static PortCatalog FromJson(string json)
    {
        Dictionary<string, ModuleInfo>? parsed = JsonSerializer.Deserialize<Dictionary<string, ModuleInfo>>(json, jsonOptions);
        // Hand-written overrides may leave out any list: normalize missing ones to empty.
        Dictionary<string, ModuleInfo> normalized = (parsed ?? new Dictionary<string, ModuleInfo>()).ToDictionary(
            kv => kv.Key,
            kv => new ModuleInfo(kv.Value.Name, kv.Value.Tags ?? Array.Empty<string>(), kv.Value.Inputs ?? Array.Empty<string>(), kv.Value.Outputs ?? Array.Empty<string>())
            {
                Modulation = kv.Value.Modulation,
            });
        return new PortCatalog(normalized);
    }

    public string ToJson() => JsonSerializer.Serialize(modules.OrderBy(kv => kv.Key, StringComparer.Ordinal).ToDictionary(kv => kv.Key, kv => kv.Value), jsonOptions);

    /// <summary>Overrides replace whole port lists, but keep the generated name/tags when the override leaves them empty.</summary>
    public PortCatalog MergedWith(PortCatalog overrides)
    {
        Dictionary<string, ModuleInfo> merged = new Dictionary<string, ModuleInfo>(modules, StringComparer.Ordinal);
        foreach ((string key, ModuleInfo patch) in overrides.modules)
        {
            ModuleInfo baseInfo = merged.GetValueOrDefault(key) ?? ModuleInfo.Empty;
            merged[key] = new ModuleInfo(
                patch.Name ?? baseInfo.Name,
                patch.Tags.Count > 0 ? patch.Tags : baseInfo.Tags,
                patch.Inputs.Count > 0 ? patch.Inputs : baseInfo.Inputs,
                patch.Outputs.Count > 0 ? patch.Outputs : baseInfo.Outputs)
            {
                Modulation = patch.Modulation ?? baseInfo.Modulation,
            };
        }
        return new PortCatalog(merged);
    }

    public ModuleInfo Get(string plugin, string model) => modules.GetValueOrDefault($"{plugin}/{model}") ?? ModuleInfo.Empty;

    public string InputName(string plugin, string model, int index) => NameAt(Get(plugin, model).Inputs, index, "in");

    public string OutputName(string plugin, string model, int index) => NameAt(Get(plugin, model).Outputs, index, "out");

    private static string NameAt(IReadOnlyList<string> names, int index, string prefix) =>
        index >= 0 && index < names.Count && names[index].Length > 0 ? names[index] : $"{prefix}#{index}";

    internal static string? ReadResource(string logicalName)
    {
        using Stream? stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(logicalName);
        if (stream is null)
        {
            return null;
        }
        using StreamReader reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}