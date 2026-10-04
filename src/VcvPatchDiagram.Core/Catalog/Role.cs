namespace VcvPatchDiagram.Core.Catalog;

/// <summary>Functional role of a module in a patch, which decides its band in the diagram.</summary>
public enum Role
{
    /// <summary>Clocks, sequencers, logic, dividers.</summary>
    Time,
    /// <summary>Quantizers, pitch processors.</summary>
    Pitch,
    /// <summary>LFOs, envelopes, random, slew, CV utilities.</summary>
    Controller,
    /// <summary>Oscillators, noise, samplers.</summary>
    Source,
    /// <summary>Filters, VCAs, waveshapers, EQs: in a voice, on the bus or as an insert depending on where they're patched.</summary>
    Modifier,
    /// <summary>Reverbs, delays, chorus…: placed by the patch like modifiers (voice, bus or send/return).</summary>
    Effect,
    /// <summary>Mixers: where voices end and the bus starts.</summary>
    Mixer,
    /// <summary>Host audio/MIDI interfaces: the patch's link with the outside world.</summary>
    Io,
    /// <summary>Scopes, meters, displays: they watch signals but make no sound.</summary>
    Monitor,
}