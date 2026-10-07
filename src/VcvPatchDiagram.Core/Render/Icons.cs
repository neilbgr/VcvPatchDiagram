using VcvPatchDiagram.Core.Catalog;

namespace VcvPatchDiagram.Core.Render;

/// <summary>
/// One small line drawing per module function, in a 16 × 12 box, stroked in the role color (see .vpd-node .icon):
/// a waveform for an oscillator, a cutoff slope for a filter, an ADSR outline for an envelope…
/// </summary>
public static class Icons
{
    public const double Width = 16;

    public static string Path(ModuleFunction function) => function switch
    {
        ModuleFunction.Oscillator => "M0 6 Q2 0 4 6 T8 6 T12 6 T16 6",
        ModuleFunction.Noise => "M0 6 L2 2 L3 9 L5 3 L6 10 L8 1 L9 8 L11 4 L12 11 L14 2 L16 7",
        ModuleFunction.Sampler => "M1 6 V6 M3.5 3 V9 M6 1 V11 M8.5 4 V8 M11 2 V10 M13.5 5 V7 M16 4 V8",
        ModuleFunction.Drum => "M0 11 L2 1 Q4 9 16 11",
        ModuleFunction.Filter => "M0 4 H7 Q10 4 11 2 Q12.5 9 16 11",
        ModuleFunction.Vca => "M2 1 L14 6 L2 11 Z",
        ModuleFunction.Waveshaper => "M0 10 Q2 10 3 3 H7 Q8 10 9 10 Q10 10 11 3 H16",
        ModuleFunction.Equalizer => "M0 8 Q4 8 5 3 Q6 8 9 8 Q12 8 13 5 Q14 8 16 8",
        ModuleFunction.Dynamics => "M0 11 L7 5 L16 3",
        ModuleFunction.Envelope => "M0 11 L3 1 L6 5 H11 L15 11",
        ModuleFunction.Lfo => "M0 9 L4 3 L8 9 L12 3 L16 9",
        ModuleFunction.Random or ModuleFunction.SampleAndHold => "M0 7 H3 V2 H6 V9 H9 V4 H12 V10 H16",
        ModuleFunction.Slew => "M0 10 H3 Q7 10 9 2 H16",
        ModuleFunction.Follower => "M0 10 Q4 0 8 6 T16 8",
        ModuleFunction.Sequencer => "M1 11 V6 M4.5 11 V3 M8 11 V8 M11.5 11 V2 M15 11 V5",
        ModuleFunction.Arpeggiator => "M1 10 H4 M5 7 H8 M9 4 H12 M13 1 H16",
        ModuleFunction.Clock => "M0 10 H2 V2 H5 V10 H8 V2 H11 V10 H14 V2 H16",
        ModuleFunction.Logic => "M2 1 H8 Q15 6 8 11 H2 Z",
        ModuleFunction.Quantizer => "M0 11 H4 V7 H8 V4 H12 V1 H16",
        ModuleFunction.Mixer => "M3 1 V11 M8 1 V11 M13 1 V11 M1 8 H5 M6 4 H10 M11 6 H15",
        ModuleFunction.Reverb or ModuleFunction.Effect => "M3 2 Q7 6 3 10 M7 1 Q12 6 7 11 M11 0 Q17 6 11 12",
        ModuleFunction.Delay => "M2 11 V1 M6.5 11 V4 M11 11 V7 M15.5 11 V9",
        ModuleFunction.ModulationEffect => "M0 4 Q2 0 4 4 T8 4 T12 4 T16 4 M0 9 Q2 5 4 9 T8 9 T12 9 T16 9",
        ModuleFunction.Keyboard => "M0 1 H16 V11 H0 Z M4 1 V11 M8 1 V11 M12 1 V11",
        ModuleFunction.Pads => "M1 1 H7 V5 H1 Z M9 1 H15 V5 H9 Z M1 7 H7 V11 H1 Z M9 7 H15 V11 H9 Z",
        ModuleFunction.Joystick => "M8 10 V5 M4 11 H12 M10 3 A2 2 0 1 1 6 3 A2 2 0 1 1 10 3",
        ModuleFunction.Midi => "M13 6 A5 5 0 1 1 3 6 A5 5 0 1 1 13 6 M5.5 6.5 V6.4 M8 4 V3.9 M10.5 6.5 V6.4",
        ModuleFunction.Audio => "M1 4 H4 L8 1 V11 L4 8 H1 Z M11 3 Q14 6 11 9",
        ModuleFunction.Display => "M0 6 Q8 -2 16 6 Q8 14 0 6 M8 6 V5.9",
        _ => "M2 6 H14 M8 2 V10",
    };
}
/// <summary>"Open elsewhere" arrow out of a square, in a 10 × 10 box: the link to a module's VCV Library page.</summary>
public static class LibraryIcon
{
    public const double Size = 10;

    public const string Path = "M4 1 H1 V9 H9 V6 M6 1 H9 V4 M9 1 L4.5 5.5";
}