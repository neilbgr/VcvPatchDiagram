namespace VcvPatchTools.Core.Catalog;

/// <summary>What a module does, one step finer than its <see cref="Role"/>: the word a teacher would write on the box.</summary>
public enum ModuleFunction
{
    Utility,
    Oscillator,
    Noise,
    Sampler,
    Drum,
    Filter,
    Vca,
    Waveshaper,
    Equalizer,
    Dynamics,
    Envelope,
    Lfo,
    Random,
    SampleAndHold,
    Slew,
    Follower,
    Sequencer,
    Arpeggiator,
    Clock,
    Logic,
    Quantizer,
    Mixer,
    Reverb,
    Delay,
    ModulationEffect,
    Effect,
    Keyboard,
    Pads,
    Joystick,
    Midi,
    Audio,
    Display,
}

/// <summary>
/// Works out a module's function within its role: words of its own name first (a module called "Random" is one,
/// whatever else its tags say), then its plugin tags, most specific first.
/// The role bounds the guess: a "Random" tag means a random source for a controller, not for an effect.
/// </summary>
public static class ModuleFunctions
{
    private static readonly Dictionary<Role, (string[] Words, ModuleFunction Function)[]> rules = new Dictionary<Role, (string[], ModuleFunction)[]>
    {
        [Role.Source] = new[]
        {
            // A noise or chaos source that also calls itself an oscillator is played as one.
            (new[] { "Oscillator", "VCO", "Voltage-controlled oscillator", "Synth voice" }, ModuleFunction.Oscillator),
            (new[] { "Noise" }, ModuleFunction.Noise),
            (new[] { "Sampler", "Granular", "Recording" }, ModuleFunction.Sampler),
            (new[] { "Drum", "Kick", "Snare", "Hat" }, ModuleFunction.Drum),
        },
        [Role.Modifier] = new[]
        {
            (new[] { "Filter", "VCF" }, ModuleFunction.Filter),
            (new[] { "VCA", "Amplifier", "Voltage-controlled amplifier" }, ModuleFunction.Vca),
            (new[] { "Equalizer", "EQ" }, ModuleFunction.Equalizer),
            (new[] { "Compressor", "Dynamics", "Limiter" }, ModuleFunction.Dynamics),
            (new[] { "Waveshaper", "Distortion", "Shaper", "Fold", "Ring modulator" }, ModuleFunction.Waveshaper),
        },
        [Role.Effect] = new[]
        {
            (new[] { "Reverb", "Plateau" }, ModuleFunction.Reverb),
            (new[] { "Delay", "Echo" }, ModuleFunction.Delay),
            (new[] { "Chorus", "Flanger", "Phaser" }, ModuleFunction.ModulationEffect),
        },
        [Role.Controller] = new[]
        {
            (new[] { "Envelope generator", "ADSR", "Envelope", "Env" }, ModuleFunction.Envelope),
            (new[] { "Low-frequency oscillator", "LFO" }, ModuleFunction.Lfo),
            (new[] { "Slew limiter", "Slew" }, ModuleFunction.Slew),
            (new[] { "Sample and hold" }, ModuleFunction.SampleAndHold),
            (new[] { "Envelope follower", "Follower" }, ModuleFunction.Follower),
            (new[] { "Random" }, ModuleFunction.Random),
        },
        [Role.Time] = new[]
        {
            (new[] { "Arpeggiator", "Arp" }, ModuleFunction.Arpeggiator),
            (new[] { "Sequencer", "Seq" }, ModuleFunction.Sequencer),
            (new[] { "Clock generator", "Clock modulator", "Clock", "Clk" }, ModuleFunction.Clock),
            (new[] { "Logic" }, ModuleFunction.Logic),
        },
        [Role.Pitch] = new[]
        {
            (new[] { "Quantizer", "Quant" }, ModuleFunction.Quantizer),
        },
        [Role.Performance] = new[]
        {
            (new[] { "Joystick" }, ModuleFunction.Joystick),
            (new[] { "Pads", "Pad" }, ModuleFunction.Pads),
        },
        [Role.Io] = new[]
        {
            (new[] { "MIDI" }, ModuleFunction.Midi),
            (new[] { "Audio" }, ModuleFunction.Audio),
        },
    };

    private static readonly Dictionary<Role, ModuleFunction> byDefault = new Dictionary<Role, ModuleFunction>
    {
        [Role.Source] = ModuleFunction.Oscillator,
        [Role.Effect] = ModuleFunction.Effect,
        [Role.Time] = ModuleFunction.Sequencer,
        [Role.Mixer] = ModuleFunction.Mixer,
        [Role.Performance] = ModuleFunction.Keyboard,
        [Role.Io] = ModuleFunction.Midi,
        [Role.Monitor] = ModuleFunction.Display,
    };

    public static ModuleFunction Of(Role role, IReadOnlyList<string> tags, string model, string? name)
    {
        (string[] Words, ModuleFunction Function)[] candidates = rules.GetValueOrDefault(role) ?? Array.Empty<(string[], ModuleFunction)>();
        string text = $"{model} {name}";
        foreach ((string[] words, ModuleFunction function) in candidates)
        {
            if (words.Any(w => w.Length > 2 && text.Contains(w, StringComparison.OrdinalIgnoreCase)))
            {
                return function;
            }
        }
        foreach ((string[] words, ModuleFunction function) in candidates)
        {
            if (tags.Any(t => words.Contains(t, StringComparer.OrdinalIgnoreCase)))
            {
                return function;
            }
        }
        return byDefault.GetValueOrDefault(role, ModuleFunction.Utility);
    }

    public static string Label(ModuleFunction function) => function switch
    {
        ModuleFunction.Vca => "VCA",
        ModuleFunction.Equalizer => "EQ",
        ModuleFunction.Lfo => "LFO",
        ModuleFunction.SampleAndHold => "sample & hold",
        ModuleFunction.Follower => "envelope follower",
        ModuleFunction.ModulationEffect => "chorus / phaser",
        ModuleFunction.Midi => "MIDI",
        ModuleFunction.Audio => "audio I/O",
        _ => function.ToString().ToLowerInvariant(),
    };
}