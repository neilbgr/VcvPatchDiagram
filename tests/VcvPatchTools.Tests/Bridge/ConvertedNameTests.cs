using VcvPatchTools.Bridge;

namespace VcvPatchTools.Tests;

public class ConvertedNameTests
{
    [Theory]
    [InlineData("My Patch.vcv", PatchOrigin.Rack, "My Patch.rack.vcv")]
    [InlineData("My Patch.cardinal.vcv", PatchOrigin.Rack, "My Patch.rack.vcv")]
    [InlineData("My Patch.rack.vcv", PatchOrigin.Cardinal, "My Patch.cardinal.vcv")]
    public void NamesTheConvertedPatchAfterItsTarget(string fileName, PatchOrigin target, string expected)
    {
        Assert.Equal(expected, ConvertedName.For(fileName, target));
    }

    [Fact]
    public void ConvertsToTheOtherSideOfTheDetectedOrigin()
    {
        Assert.Equal(PatchOrigin.Rack, ConvertedName.OppositeOf(PatchOrigin.Cardinal));
        Assert.Equal(PatchOrigin.Cardinal, ConvertedName.OppositeOf(PatchOrigin.Rack));
        Assert.Null(ConvertedName.OppositeOf(PatchOrigin.Ambiguous));
    }
}