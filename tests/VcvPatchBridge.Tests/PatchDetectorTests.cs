using System.Text.Json.Nodes;
using VcvPatchBridge;

namespace VcvPatchBridge.Tests;

public class PatchDetectorTests
{
    [Fact]
    public void Detects_Cardinal_patch()
    {
        JsonObject root = TestPatchBuilder.Root(new[]
        {
            TestPatchBuilder.Module(1, "Cardinal", "HostMIDI"),
            TestPatchBuilder.Module(2, "AmbientModules", "Lunar50Drone"),
        });

        DetectionResult result = PatchDetector.Detect(TestPatchBuilder.ToPatchFile(root));

        Assert.Equal(PatchOrigin.Cardinal, result.Origin);
        Assert.Contains(result.CardinalOnlyModules, m => m.Model == "HostMIDI");
    }

    [Fact]
    public void Detects_Rack_patch()
    {
        JsonObject root = TestPatchBuilder.Root(new[]
        {
            TestPatchBuilder.Module(1, "Core", "MIDIToCVInterface"),
            TestPatchBuilder.Module(2, "Befaco", "EvenVCO"),
        });

        DetectionResult result = PatchDetector.Detect(TestPatchBuilder.ToPatchFile(root));

        Assert.Equal(PatchOrigin.Rack, result.Origin);
        Assert.Contains(result.RackOnlyModules, m => m.Model == "MIDIToCVInterface");
    }

    [Fact]
    public void Ambiguous_when_no_divergent_module_present()
    {
        JsonObject root = TestPatchBuilder.Root(new[]
        {
            TestPatchBuilder.Module(1, "Befaco", "EvenVCO"),
            TestPatchBuilder.Module(2, "AmbientModules", "Lunar50Drone"),
        });

        DetectionResult result = PatchDetector.Detect(TestPatchBuilder.ToPatchFile(root));

        Assert.Equal(PatchOrigin.Ambiguous, result.Origin);
    }
}