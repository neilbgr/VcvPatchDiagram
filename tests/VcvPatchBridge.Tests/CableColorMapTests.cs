using VcvPatchBridge;

namespace VcvPatchBridge.Tests;

public class CableColorMapTests
{
    [Theory]
    [InlineData("#f3374b", "#ff5252")]
    [InlineData("#ffb437", "#ffd452")]
    [InlineData("#00b56e", "#52ffbe")]
    [InlineData("#3695ef", "#52beff")]
    [InlineData("#8b4ade", "#a852ff")]
    public void MapToCardinal_picks_nearest_hue(string rackColor, string expectedCardinalColor) =>
        Assert.Equal(expectedCardinalColor, CableColorMap.MapToCardinal(rackColor));

    [Theory]
    [InlineData("#ff5252", "#f3374b")]
    [InlineData("#ff9352", "#ffb437")]
    [InlineData("#ffd452", "#ffb437")]
    [InlineData("#e8ff52", "#ffb437")]
    [InlineData("#a8ff52", "#ffb437")]
    [InlineData("#67ff52", "#00b56e")]
    [InlineData("#52ff7d", "#00b56e")]
    [InlineData("#52ffbe", "#00b56e")]
    [InlineData("#52ffff", "#00b56e")]
    [InlineData("#52beff", "#3695ef")]
    [InlineData("#527dff", "#3695ef")]
    [InlineData("#6752ff", "#8b4ade")]
    [InlineData("#a852ff", "#8b4ade")]
    [InlineData("#e952ff", "#8b4ade")]
    [InlineData("#ff52d4", "#f3374b")]
    [InlineData("#ff5293", "#f3374b")]
    public void MapToRack_picks_nearest_hue(string cardinalColor, string expectedRackColor) =>
        Assert.Equal(expectedRackColor, CableColorMap.MapToRack(cardinalColor));

    [Fact]
    public void MapToCardinal_is_case_insensitive() =>
        Assert.Equal("#ff5252", CableColorMap.MapToCardinal("#F3374B"));

    [Fact]
    public void MapToCardinal_leaves_malformed_color_untouched()
    {
        Assert.Equal("not-a-color", CableColorMap.MapToCardinal("not-a-color"));
        Assert.Equal("#zzzzzz", CableColorMap.MapToCardinal("#zzzzzz"));
    }
}