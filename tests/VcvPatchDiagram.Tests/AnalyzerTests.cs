using System.Text;
using VcvPatchDiagram.Core;
using VcvPatchDiagram.Core.Analysis;
using VcvPatchDiagram.Core.Catalog;
using VcvPatchDiagram.Core.Layout;
using VcvPatchDiagram.Core.Patch;

namespace VcvPatchDiagram.Tests;

public class AnalyzerTests
{
    private static readonly PatchAnalysis analysis = Fixtures.AmbientJam;

    private static List<string> Chain(Voice voice) => voice.ModuleIds.Select(id => analysis.Module(id).Title).ToList();

    [Fact]
    public void DetectsTheTwoVoicesInSignalOrder()
    {
        Assert.Equal(2, analysis.Voices.Count);
        Voice keyboard = analysis.Voices.Single(v => v.PitchOrigin == "Host MIDI");
        Voice sequenced = analysis.Voices.Single(v => v.PitchOrigin == "ADDR-SEQ");
        Assert.Equal(new[] { "PULSE", "VCF #1", "Waveshaper", "EQ" }, Chain(keyboard));
        Assert.Equal(new[] { "LVCO", "VCF #2" }, Chain(sequenced));
    }

    [Theory]
    [InlineData("Host MIDI", Band.External)]
    [InlineData("Host MIDI Gate", Band.External)]
    [InlineData("Clkd", Band.Time)]
    [InlineData("Quantizer", Band.Pitch)]
    [InlineData("LLFO", Band.Modulation)]
    [InlineData("MIX4", Band.Bus)]
    [InlineData("Plateau", Band.Bus)]
    [InlineData("Audio 2", Band.Bus)]
    public void PutsModulesInTheirBand(string title, Band band) =>
        Assert.Equal(band, analysis.Modules.Single(m => m.Title == title).Band);

    [Fact]
    public void ModulesWithoutCablesAreLeftOut() => Assert.DoesNotContain(analysis.Modules, m => m.Title == "ASX");

    [Fact]
    public void TypesEveryCableFromNeilsPalette()
    {
        Dictionary<SignalType, int> counts = analysis.Cables.GroupBy(c => c.Signal).ToDictionary(g => g.Key, g => g.Count());

        Assert.Equal(12, counts[SignalType.Audio]);
        Assert.Equal(5, counts[SignalType.Pitch]);
        Assert.Equal(11, counts[SignalType.Cv]);
        Assert.Equal(10, counts[SignalType.Gate]);
        Assert.Empty(analysis.Diagnostics);
    }

    [Fact]
    public void NamesSurgeModulationInputsByTheKnobsTheyMove()
    {
        AnalyzedCable cc4 = analysis.Cables.Single(c => analysis.Module(c.Cable.From.ModuleId).Title == "Host MIDI CC" && c.FromPort == "Cell 4");

        Assert.Equal("Mod 3 → Frequency +40%, Pre-Filter Gain +7%", cc4.ToPort);
        Assert.Equal(new[] { "Frequency", "Pre-Filter Gain" }, cc4.Routes.Select(r => r.Target));
        Assert.Equal("hand control of frequency & pre-filter gain", cc4.SuggestedIntent);
    }

    [Fact]
    public void FlagsAModulationInputWithoutTarget()
    {
        string json = """
            {
              "modules": [
                { "id": 1, "plugin": "Bogaudio", "model": "Bogaudio-LLFO", "pos": [0, 0] },
                { "id": 2, "plugin": "SurgeXTRack", "model": "SurgeXTVCF", "pos": [10, 0], "params": [ { "id": 5, "value": 0.5 } ] }
              ],
              "cables": [
                { "id": 1, "outputModuleId": 1, "outputId": 0, "inputModuleId": 2, "inputId": 3, "color": "#52ff7d" }
              ]
            }
            """;

        PatchAnalysis lonely = PatchDiagram.Analyze(Encoding.UTF8.GetBytes(json));

        Assert.Contains(lonely.Diagnostics, d => d.Contains("does nothing", StringComparison.Ordinal));
    }

    [Fact]
    public void TellsVoiceProcessingFromBusProcessingAndInserts()
    {
        PatchAnalysis meditations = PatchDiagram.Analyze(File.ReadAllBytes(Fixtures.Path("MeditationsOnDeath.vcv")));
        AnalyzedModule Find(string title) => meditations.Modules.Single(m => m.Title == title);

        // Reverb right after a voice's filter, before any mixer: part of that voice.
        Assert.Equal(Band.Voice, Find("Plateau #1").Band);
        Assert.Equal(Find("LunarPapaSrapa #1").Voice, Find("Plateau #1").Voice);
        // Filter fed by a mixer: bus processing, with the EQ patched into its insert send/return.
        Assert.Equal(Band.Bus, Find("LunarFilter").Band);
        Assert.Null(Find("LunarFilter").InsertOf);
        Assert.Equal(Find("LunarFilter").Module.Id, Find("EQ").InsertOf);
        // Reverb on an aux send/return of the mixer (its docked AuxSpander is part of it).
        Assert.Equal(Find("MixMasterJr").Module.Id, Find("Plateau #2").InsertOf);
        Assert.Equal(Band.Bus, Find("Plateau #2").Band);
    }

    [Fact]
    public void FindsFeedbackLoopsButNotEffectLoops()
    {
        PatchAnalysis feedback = PatchDiagram.Analyze(File.ReadAllBytes(Fixtures.Path("FeedbackTest.vcv")));
        List<string> Titles(PatchAnalysis patch, IReadOnlyList<long> loop) => loop.Select(id => patch.Module(id).Title).Order().ToList();

        Assert.Equal(2, feedback.Loops.Count);
        Assert.Contains(feedback.Loops, l => Titles(feedback, l).SequenceEqual(new[] { "FM-OP #1", "FM-OP #2" }));
        Assert.Contains(feedback.Loops, l => Titles(feedback, l).SequenceEqual(new[] { "VCO #1", "VCO #2" }));
        // An EQ in a filter's insert, a reverb on a mixer's aux send/return: effect loops, not feedback.
        Assert.Empty(PatchDiagram.Analyze(File.ReadAllBytes(Fixtures.Path("MeditationsOnDeathRack.vcv"))).Loops);
    }

    [Fact]
    public void DockedExpandersAreFoldedIntoTheirBase()
    {
        PatchAnalysis drone = PatchDiagram.Analyze(File.ReadAllBytes(Fixtures.Path("NotBoringDrone3.vcv")));

        Assert.DoesNotContain(drone.Modules, m => m.Module.Model is "MixPan" or "MixMute" or "MixFade");
        AnalyzedModule mixer = drone.Modules.Single(m => m.Title == "VCA Mix 4 Stereo");
        Assert.Contains("Mix Pan", mixer.Expanders);
        Assert.Contains(drone.Cables, c => c.Cable.To.ModuleId == mixer.Module.Id && c.ToPort == "Mix Pan › Pan CV 2");
    }

    [Fact]
    public void AnExpanderNeedsABaseOfItsOwnFamilyDockedToIt()
    {
        PatchModule mixer = new PatchModule(1, "Venom", "Mix4", 0, 0) { RightModuleId = 2 };
        PatchModule pan = new PatchModule(2, "Venom", "MixPan", 5, 0) { LeftModuleId = 1, RightModuleId = 3 };
        PatchModule mute = new PatchModule(3, "Venom", "MixMute", 8, 0) { LeftModuleId = 2, RightModuleId = 4 };
        PatchModule stranger = new PatchModule(4, "Bogaudio", "Bogaudio-LLFO", 11, 0) { LeftModuleId = 3 };
        PatchModule alone = new PatchModule(5, "Venom", "MixSolo", 30, 0);
        PatchModule wrongFamily = new PatchModule(6, "Venom", "MixSend", 40, 0) { LeftModuleId = 4 };

        Dictionary<long, long> baseOf = ExpanderChains.BaseOf(new[] { mixer, pan, mute, stranger, alone, wrongFamily }, PortCatalog.LoadEmbedded());

        Assert.Equal(new Dictionary<long, long> { [2] = 1, [3] = 1 }, baseOf);
    }

    [Fact]
    public void LunarVoicesAreSourcesEvenWithAMiscoloredCable()
    {
        PatchAnalysis meditations = PatchDiagram.Analyze(File.ReadAllBytes(Fixtures.Path("MeditationsOnDeath.vcv")));

        Assert.Equal(8, meditations.Voices.Count);
        Assert.Equal(4, meditations.Voices.Count(v => v.Sources.StartsWith("Lunar50Drone", StringComparison.Ordinal)));
        Assert.Contains(meditations.Diagnostics, d => d.Contains("treated as audio", StringComparison.Ordinal));
    }

    [Fact]
    public void ModulatorsServingOnlyOneVoiceBelongToIt()
    {
        IReadOnlyList<PatchGroup> groups = PatchGrouping.Build(analysis);
        PatchGroup Owner(string title) => groups.Single(g => g.Members.Contains(analysis.Modules.Single(m => m.Title == title).Module.Id));

        Assert.Equal(Owner("PULSE"), Owner("LLFO"));
        Assert.Equal(Owner("PULSE"), Owner("SLEW"));
        Assert.Equal(Owner("LVCO"), Owner("Quantizer"));
        Assert.Equal(Owner("MIX4"), Owner("Random"));
        Assert.Equal("time", Owner("ADDR-SEQ").Key);
        Assert.Equal("external", Owner("Host MIDI").Key);
    }

    [Fact]
    public void IdenticalOneModuleVoicesFoldTogether()
    {
        PatchAnalysis meditations = PatchDiagram.Analyze(File.ReadAllBytes(Fixtures.Path("MeditationsOnDeathRack.vcv")));

        PatchGroup drones = Assert.Single(PatchGrouping.Build(meditations), g => g.Title.Contains("Lunar50Drone ×4", StringComparison.Ordinal));
        Assert.Equal(4, drones.Count);
        Assert.Empty(meditations.Diagnostics);
    }

    [Theory]
    [InlineData(true, Band.External)]
    [InlineData(false, Band.Pitch)]
    public void KeyboardZonesRelayTheOutsideOnlyWhenFedByHostMidi(bool fromHostMidi, Band expected)
    {
        string source = fromHostMidi
            ? """{ "id": 1, "plugin": "Cardinal", "model": "HostMIDI", "pos": [0, 0] }"""
            : """{ "id": 1, "plugin": "Bogaudio", "model": "Bogaudio-AddrSeq", "pos": [0, 0] }""";
        string json = $$"""
            {
              "modules": [
                {{source}},
                { "id": 2, "plugin": "Aluminium", "model": "Zones", "pos": [10, 0] },
                { "id": 3, "plugin": "Bogaudio", "model": "Bogaudio-LVCO", "pos": [20, 0] },
                { "id": 4, "plugin": "JW-Modules", "model": "FullScope", "pos": [30, 0] }
              ],
              "cables": [
                { "id": 1, "outputModuleId": 1, "outputId": 0, "inputModuleId": 2, "inputId": 0, "color": "#ffd452" },
                { "id": 2, "outputModuleId": 2, "outputId": 0, "inputModuleId": 3, "inputId": 0, "color": "#ffd452" },
                { "id": 3, "outputModuleId": 3, "outputId": 0, "inputModuleId": 4, "inputId": 0, "color": "#ff5252" }
              ]
            }
            """;

        PatchAnalysis patch = PatchDiagram.Analyze(Encoding.UTF8.GetBytes(json));

        Assert.Equal(expected, patch.Modules.Single(m => m.Module.Model == "Zones").Band);
        Assert.Equal(Band.Monitor, patch.Modules.Single(m => m.Module.Model == "FullScope").Band);
        Assert.DoesNotContain(PatchDiagram.Layout(patch, "t").Nodes, n => n.Group == "monitor" || n.Watchers.Count > 0);
        DiagramLayout withScopes = PatchDiagram.Layout(patch, "t", PatchDiagram.GroupKeys(patch), showMonitors: true);
        Assert.DoesNotContain(withScopes.Nodes, n => n.Group == "monitor");
        Assert.Equal("Full Scope 'X' ← LVCO 'Signal'", Assert.Single(withScopes.Nodes.Single(n => n.Title == "LVCO").Watchers));
    }

    [Fact]
    public void ReportsStackedCablesOnOneInput()
    {
        string json = """
            {
              "modules": [
                { "id": 1, "plugin": "Fundamental", "model": "LFO", "pos": [0, 0] },
                { "id": 2, "plugin": "Fundamental", "model": "LFO", "pos": [10, 0] },
                { "id": 3, "plugin": "Fundamental", "model": "VCF", "pos": [20, 0] }
              ],
              "cables": [
                { "id": 1, "outputModuleId": 1, "outputId": 0, "inputModuleId": 3, "inputId": 0 },
                { "id": 2, "outputModuleId": 2, "outputId": 0, "inputModuleId": 3, "inputId": 0 }
              ]
            }
            """;

        PatchAnalysis stacked = PatchDiagram.Analyze(Encoding.UTF8.GetBytes(json));

        Assert.Contains(stacked.Diagnostics, d => d.Contains("2 stacked cables", StringComparison.Ordinal));
    }

    [Fact]
    public void TopologicalOrderSurvivesFeedbackLoops()
    {
        List<long> order = GraphOrder.Topological(new long[] { 1, 2, 3 }, new[] { (1L, 2L), (2L, 3L), (3L, 2L) });

        Assert.Equal(new long[] { 1, 2, 3 }, order);
    }
}