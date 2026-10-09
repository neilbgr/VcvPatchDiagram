using System.Formats.Tar;
using System.Text;
using System.Text.Json.Nodes;
using VcvPatchTools.Core.Patch;
using ZstdSharp;

namespace VcvPatchTools.Tests;

public class PatchArchiveTests
{
    [Fact]
    public void Loads_raw_JSON_vcv()
    {
        JsonObject root = TestPatchBuilder.Root(new[] { TestPatchBuilder.Module(1, "Cardinal", "HostMIDI") });
        PatchArchive patch = TestPatchBuilder.ToPatchArchive(root);

        Assert.False(patch.WasArchive);
        Assert.Single(patch.Root["modules"]!.AsArray());
    }

    [Fact]
    public void Write_then_Read_round_trips_through_tar_plus_zstd()
    {
        JsonObject root = TestPatchBuilder.Root(new[] { TestPatchBuilder.Module(42, "Cardinal", "HostMIDI") });
        PatchArchive patch = TestPatchBuilder.ToPatchArchive(root);

        byte[] bytes = patch.Write();
        Assert.Equal(new byte[] { 0x28, 0xB5, 0x2F, 0xFD }, bytes.Take(4));

        PatchArchive reloaded = PatchArchive.Read(bytes);
        Assert.True(reloaded.WasArchive);
        JsonObject module = (JsonObject)reloaded.Root["modules"]![0]!;
        Assert.Equal(42, module["id"]!.GetValue<long>());
        Assert.Equal("HostMIDI", module["model"]!.GetValue<string>());
        // The diagram side reads the same bytes.
        Assert.Equal("HostMIDI", Assert.Single(PatchReader.Read(bytes).Modules).Model);
    }

    [Fact]
    public void Extra_archive_entries_survive_a_write_read_cycle()
    {
        JsonObject root = TestPatchBuilder.Root(new[] { TestPatchBuilder.Module(1, "Cardinal", "AudioFile") });
        PatchArchive patch = TestPatchBuilder.ToPatchArchive(root);
        patch.ExtraEntries.Add(("some-sample.wav", new byte[] { 1, 2, 3, 4 }));

        PatchArchive reloaded = PatchArchive.Read(patch.Write());

        Assert.Single(reloaded.ExtraEntries);
        Assert.Equal("some-sample.wav", reloaded.ExtraEntries[0].Name);
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, reloaded.ExtraEntries[0].Data);
    }

    [Fact]
    public void Written_archives_open_with_the_standard_tar_reader()
    {
        PatchArchive patch = TestPatchBuilder.ToPatchArchive(TestPatchBuilder.Root(new[] { TestPatchBuilder.Module(3, "Fundamental", "VCO") }));
        patch.ExtraEntries.Add(("modules/3/sample.wav", new byte[] { 9, 8, 7 }));

        using MemoryStream tar = new MemoryStream();
        using (DecompressionStream zstd = new DecompressionStream(new MemoryStream(patch.Write())))
        {
            zstd.CopyTo(tar);
        }
        tar.Position = 0;
        using TarReader reader = new TarReader(tar);
        TarEntry json = reader.GetNextEntry()!;
        TarEntry sample = reader.GetNextEntry()!;

        Assert.Equal("patch.json", json.Name);
        Assert.Equal("modules/3/sample.wav", sample.Name);
        Assert.Null(reader.GetNextEntry());
    }

    [Fact]
    public void Loads_archives_where_patch_json_has_a_leading_dot_slash()
    {
        // Some real-world Cardinal/Rack builds (seen on Windows) write entries as "./" and
        // "./patch.json" instead of a bare "patch.json".
        JsonObject root = TestPatchBuilder.Root(new[] { TestPatchBuilder.Module(7, "Cardinal", "HostMIDI") });
        byte[] patchJsonBytes = Encoding.UTF8.GetBytes(root.ToJsonString());

        using MemoryStream compressed = new MemoryStream();
        using (MemoryStream tarBuffer = new MemoryStream())
        {
            using (TarWriter tarWriter = new TarWriter(tarBuffer, TarEntryFormat.Pax, leaveOpen: true))
            {
                tarWriter.WriteEntry(new PaxTarEntry(TarEntryType.Directory, "./"));
                tarWriter.WriteEntry(new PaxTarEntry(TarEntryType.RegularFile, "./patch.json")
                {
                    DataStream = new MemoryStream(patchJsonBytes),
                });
            }

            tarBuffer.Position = 0;
            using CompressionStream compressionStream = new CompressionStream(compressed, level: 3, leaveOpen: true);
            tarBuffer.CopyTo(compressionStream);
        }

        PatchArchive loaded = PatchArchive.Read(compressed.ToArray());
        Assert.True(loaded.WasArchive);
        JsonObject module = (JsonObject)loaded.Root["modules"]![0]!;
        Assert.Equal(7, module["id"]!.GetValue<long>());
    }
}