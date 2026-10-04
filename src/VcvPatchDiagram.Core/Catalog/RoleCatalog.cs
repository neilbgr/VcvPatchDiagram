using System.Text.Json;
using System.Text.Json.Serialization;

namespace VcvPatchDiagram.Core.Catalog;

/// <summary>
/// Decides a module's <see cref="Role"/>: an explicit entry in catalog/roles.json wins,
/// otherwise the plugin.json tags (from the port catalog), otherwise keywords in the model slug.
/// </summary>
public sealed class RoleCatalog
{
    private static readonly JsonSerializerOptions jsonOptions = new JsonSerializerOptions
    {
        Converters = { new JsonStringEnumConverter() },
    };

    // Ordered: the first matching tag wins, so the most specific tags come first.
    private static readonly (string Tag, Role Role)[] tagRoles =
    {
        // A module tagged both "Synth voice"/"Oscillator" and "Envelope generator" is a voice with its own VCA, not a controller.
        ("Synth voice", Role.Source),
        ("Visual", Role.Monitor),
        ("Quantizer", Role.Pitch),
        ("Arpeggiator", Role.Time),
        ("Sequencer", Role.Time),
        ("Clock generator", Role.Time),
        ("Clock modulator", Role.Time),
        ("Logic", Role.Time),
        ("Envelope generator", Role.Controller),
        ("Low-frequency oscillator", Role.Controller),
        ("LFO", Role.Controller),
        ("Random", Role.Controller),
        ("Sample and hold", Role.Controller),
        ("Slew limiter", Role.Controller),
        ("Envelope follower", Role.Controller),
        ("Controller", Role.Controller),
        ("Switch", Role.Controller),
        ("Voltage-controlled oscillator", Role.Source),
        ("VCO", Role.Source),
        ("Oscillator", Role.Source),
        ("Noise", Role.Source),
        ("Sampler", Role.Source),
        ("Drum", Role.Source),
        ("Physical modeling", Role.Source),
        ("Synth voice", Role.Source),
        ("Visual", Role.Monitor),
        ("Reverb", Role.Effect),
        ("Delay", Role.Effect),
        ("Chorus", Role.Effect),
        ("Flanger", Role.Effect),
        ("Phaser", Role.Effect),
        ("Mixer", Role.Mixer),
        ("Effect", Role.Effect),
        ("Filter", Role.Modifier),
        ("VCF", Role.Modifier),
        ("Amplifier", Role.Modifier),
        ("VCA", Role.Modifier),
        ("Waveshaper", Role.Modifier),
        ("Distortion", Role.Modifier),
        ("Equalizer", Role.Modifier),
        ("External", Role.Io),
    };

    private static readonly (string Keyword, Role Role)[] slugRoles =
    {
        ("Host", Role.Io), ("Audio", Role.Io), ("MIDI", Role.Io),
        ("Seq", Role.Time), ("Clock", Role.Time), ("Clk", Role.Time),
        ("Quant", Role.Pitch),
        ("LFO", Role.Controller), ("ADSR", Role.Controller), ("Env", Role.Controller), ("Random", Role.Controller), ("Slew", Role.Controller),
        ("VCO", Role.Source), ("Osc", Role.Source), ("Noise", Role.Source),
        ("Reverb", Role.Effect), ("Delay", Role.Effect), ("Mix", Role.Mixer),
        ("VCF", Role.Modifier), ("Filter", Role.Modifier), ("VCA", Role.Modifier), ("Shaper", Role.Modifier), ("EQ", Role.Modifier),
    };

    private readonly Dictionary<string, Role> explicitRoles;

    public RoleCatalog(IDictionary<string, Role> explicitRoles) => this.explicitRoles = new Dictionary<string, Role>(explicitRoles, StringComparer.Ordinal);

    public static RoleCatalog LoadEmbedded() => FromJson(PortCatalog.ReadResource("catalog.roles.json") ?? "{}");

    public static RoleCatalog FromJson(string json) =>
        new RoleCatalog(JsonSerializer.Deserialize<Dictionary<string, Role>>(json, jsonOptions) ?? new Dictionary<string, Role>());

    /// <summary>Returns the role and whether it was a guess (no explicit entry, no recognized tag).</summary>
    public (Role Role, bool Guessed) Resolve(string plugin, string model, ModuleInfo info)
    {
        if (explicitRoles.TryGetValue($"{plugin}/{model}", out Role explicitRole))
        {
            return (explicitRole, false);
        }

        foreach ((string tag, Role role) in tagRoles)
        {
            if (info.Tags.Any(t => string.Equals(t, tag, StringComparison.OrdinalIgnoreCase)))
            {
                return (role, false);
            }
        }

        // "Bogaudio-Switch" must not match "Audio": look at the model name without its plugin prefix.
        string bareModel = model.Replace(plugin, "", StringComparison.OrdinalIgnoreCase).Trim('-', '_', ' ');
        foreach ((string keyword, Role role) in slugRoles)
        {
            if (bareModel.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                return (role, true);
            }
        }

        return (Role.Controller, true);
    }
}