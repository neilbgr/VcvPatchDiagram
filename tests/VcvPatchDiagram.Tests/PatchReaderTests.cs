using System.Text;
using VcvPatchDiagram.Core.Patch;

namespace VcvPatchDiagram.Tests;

public class PatchReaderTests
{
    [Fact]
    public void ReadsCardinalArchiveWithDotSlashEntries()
    {
        PatchDocument patch = PatchReader.Read(Fixtures.AmbientJamBytes);

        Assert.Equal("2.4.1", patch.RackVersion);
        Assert.Equal(23, patch.Modules.Count);
        Assert.Equal(38, patch.Cables.Count);
    }

    [Fact]
    public void ReadsRawJsonPatch()
    {
        string json = """
            {
              "version": "2.5.2",
              "modules": [
                { "id": 1, "plugin": "Fundamental", "model": "VCO", "pos": [0, 0] },
                { "id": 2, "plugin": "Fundamental", "model": "VCF", "pos": [10, 0] }
              ],
              "cables": [
                { "id": 7, "outputModuleId": 1, "outputId": 2, "inputModuleId": 2, "inputId": 3, "color": "#ff5252" }
              ]
            }
            """;

        PatchDocument patch = PatchReader.Read(Encoding.UTF8.GetBytes(json));

        PatchCable cable = Assert.Single(patch.Cables);
        Assert.Equal(new PortRef(1, 2), cable.From);
        Assert.Equal(new PortRef(2, 3), cable.To);
        Assert.Equal("#ff5252", cable.Color);
        Assert.Equal("Fundamental/VCF", patch.Module(2).CatalogKey);
    }
}

public class TarEntriesTests
{
    [Fact]
    public void ReadsPaxLongPathLikeSystemFormatsTar()
    {
        string longName = new string('d', 120) + "/patch.json";
        using MemoryStream buffer = new MemoryStream();
        using (System.Formats.Tar.TarWriter writer = new System.Formats.Tar.TarWriter(buffer, System.Formats.Tar.TarEntryFormat.Pax, leaveOpen: true))
        {
            writer.WriteEntry(new System.Formats.Tar.PaxTarEntry(System.Formats.Tar.TarEntryType.Directory, "dir/"));
            writer.WriteEntry(new System.Formats.Tar.PaxTarEntry(System.Formats.Tar.TarEntryType.RegularFile, longName)
            {
                DataStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("{}")),
            });
        }

        (string name, byte[] data) = Assert.Single(VcvPatchDiagram.Core.Patch.TarEntries.Read(buffer.ToArray()));

        Assert.Equal(longName, name);
        Assert.Equal("{}", System.Text.Encoding.UTF8.GetString(data));
    }
}