using VcvPatchDiagram.Core.Catalog;

namespace VcvPatchDiagram.Tests;

public class CatalogTests
{
    private static readonly PortCatalog catalog = PortCatalog.LoadEmbedded();

    [Theory]
    [InlineData("Fundamental", "ADSR", 4, "Gate")]
    [InlineData("Fundamental", "ADSR", 5, "Retrigger")]
    [InlineData("Bogaudio", "Bogaudio-Mix4", 4, "Channel 2 pan CV")]
    [InlineData("SurgeXTRack", "SurgeXTVCF", 2, "Modulation Signal 1")]
    public void EmbeddedCatalogNamesInputs(string plugin, string model, int index, string expected) =>
        Assert.Equal(expected, catalog.InputName(plugin, model, index));

    [Fact]
    public void EmbeddedCatalogNamesOutputs() => Assert.Equal("1V/octave pitch", catalog.OutputName("Cardinal", "HostMIDI", 0));

    [Fact]
    public void UnknownPortFallsBackToIndex() => Assert.Equal("in#3", catalog.InputName("Nope", "Nothing", 3));

    [Fact]
    public void OverridesKeepGeneratedNameAndTags()
    {
        PortCatalog generated = new PortCatalog(new Dictionary<string, ModuleInfo>
        {
            ["P/M"] = new ModuleInfo("Nice", new[] { "Filter" }, new[] { "a" }, new[] { "b" }),
        });
        PortCatalog overrides = PortCatalog.FromJson("""{ "P/M": { "inputs": ["Left"] } }""");

        ModuleInfo merged = generated.MergedWith(overrides).Get("P", "M");

        Assert.Equal("Nice", merged.Name);
        Assert.Equal(new[] { "Left" }, merged.Inputs);
        Assert.Equal(new[] { "b" }, merged.Outputs);
    }

    [Fact]
    public void RoleFromTagsWhenNotExplicit()
    {
        RoleCatalog roles = RoleCatalog.FromJson("{}");

        (Role role, bool guessed) = roles.Resolve("P", "X", new ModuleInfo(null, new[] { "Polyphonic", "Reverb" }, Array.Empty<string>(), Array.Empty<string>()));

        Assert.Equal(Role.Effect, role);
        Assert.False(guessed);
    }

    [Fact]
    public void RoleGuessedFromSlugIsFlagged()
    {
        (Role role, bool guessed) = RoleCatalog.FromJson("{}").Resolve("P", "SuperLFO", ModuleInfo.Empty);

        Assert.Equal(Role.Controller, role);
        Assert.True(guessed);
    }

    [Fact]
    public void PluginNameInModelSlugIsNotAKeyword()
    {
        // "Bogaudio-Switch" used to match "Audio" and be taken for an I/O module.
        (Role role, bool _) = RoleCatalog.FromJson("{}").Resolve("Bogaudio", "Bogaudio-Switch", new ModuleInfo(null, new[] { "Switch" }, Array.Empty<string>(), Array.Empty<string>()));

        Assert.Equal(Role.Controller, role);
    }
}