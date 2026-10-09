using VcvPatchTools.Core.Catalog;

namespace VcvPatchTools.Diagram.Analysis;

/// <summary>
/// Types a cable from its color (the patch author's own convention, see <see cref="DefaultPalette"/>),
/// then cross-checks with the port names. When the color is unknown, the port names decide,
/// then the module roles (audio between sources/modifiers/mixers, CV otherwise).
/// </summary>
public sealed class SignalClassifier
{
    /// <summary>Neil's cable color convention.</summary>
    public static IReadOnlyDictionary<string, SignalType> DefaultPalette { get; } = new Dictionary<string, SignalType>(StringComparer.OrdinalIgnoreCase)
    {
        ["#ff5252"] = SignalType.Audio,
        ["#ffd452"] = SignalType.Pitch,
        ["#52ff7d"] = SignalType.Cv,
        ["#a8ff52"] = SignalType.Cv,
        ["#52beff"] = SignalType.Gate,
    };

    private static readonly string[] pitchWords = { "v/oct", "1v/octave", "pitch", "v/o", "voct" };
    private static readonly string[] cvWords = { "env", "envelope", "lfo", "cv", "phase", "mod", "modulation", "s&h", "sample", "random", "level" };
    private static readonly string[] gateWords = { "gate", "trig", "clock", "clk", "reset", "run", "start", "stop", "continue" };

    private readonly IReadOnlyDictionary<string, SignalType> palette;

    public SignalClassifier(IReadOnlyDictionary<string, SignalType>? palette = null) => this.palette = palette ?? DefaultPalette;

    public (SignalType Type, string? Mismatch) Classify(string? color, string fromPort, string toPort, Role fromRole, Role toRole)
    {
        SignalType? byColor = color is not null && palette.TryGetValue(color, out SignalType t) ? t : null;
        SignalType? byFromPort = FromPortName(fromPort);
        SignalType? byToPort = FromPortName(toPort);

        // A source's plain output (no CV/gate/pitch in its name) patched into a processor or a mixer is its audio,
        // whatever the cable color says: the color is then most likely a slip, so it's reported.
        bool sourceAudio = fromRole == Role.Source && toRole is Role.Modifier or Role.Effect or Role.Mixer
            && byFromPort is null && !IsCvName(fromPort);
        if (sourceAudio && byColor is SignalType wrongColor && wrongColor != SignalType.Audio)
        {
            return (SignalType.Audio, $"color says {wrongColor}, but a source's audio output into a {toRole.ToString().ToLowerInvariant()} is audio: treated as audio");
        }

        if (byColor is SignalType colorType)
        {
            // Only the source port says what the signal *is*; a pitch CV patched into an "Attack" input is still pitch.
            string? mismatch = byFromPort is SignalType portType && portType != colorType
                ? $"color says {colorType}, output '{fromPort}' says {portType}"
                : null;
            return (colorType, mismatch);
        }

        if (byFromPort is SignalType fromType)
        {
            return (fromType, null);
        }
        if (byToPort is SignalType toType)
        {
            return (toType, null);
        }

        bool audioFrom = fromRole is Role.Source or Role.Modifier or Role.Effect or Role.Mixer;
        bool audioTo = toRole is Role.Modifier or Role.Effect or Role.Mixer or Role.Io;
        return (audioFrom && audioTo ? SignalType.Audio : SignalType.Cv, null);
    }

    public static SignalType? FromPortName(string portName)
    {
        string lower = portName.ToLowerInvariant();
        if (pitchWords.Any(lower.Contains))
        {
            return SignalType.Pitch;
        }
        if (gateWords.Any(w => ContainsWord(lower, w)))
        {
            return SignalType.Gate;
        }
        return null;
    }

    /// <summary>Whole words only: "Envelope" is CV, "VCO (enveloped)" is not.</summary>
    private static bool IsCvName(string portName)
    {
        string lower = portName.ToLowerInvariant();
        return cvWords.Any(w => System.Text.RegularExpressions.Regex.IsMatch(lower, $@"(^|[^a-z]){System.Text.RegularExpressions.Regex.Escape(w)}([^a-z]|$)"));
    }

    private static bool ContainsWord(string text, string word)
    {
        int index = text.IndexOf(word, StringComparison.Ordinal);
        return index >= 0 && (index == 0 || !char.IsLetter(text[index - 1]));
    }
}