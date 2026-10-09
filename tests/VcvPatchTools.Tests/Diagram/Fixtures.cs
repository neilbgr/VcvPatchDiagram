using VcvPatchTools.Diagram;
using VcvPatchTools.Diagram.Analysis;

namespace VcvPatchTools.Tests;

internal static class Fixtures
{
    public static string Path(string name) => System.IO.Path.Combine(AppContext.BaseDirectory, "fixtures", name);

    public static byte[] AmbientJamBytes => File.ReadAllBytes(Path("AmbientJam.vcv"));

    public static PatchAnalysis AmbientJam => PatchDiagram.Analyze(AmbientJamBytes);
}