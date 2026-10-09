using System.Formats.Tar;
using System.Text;
using System.Text.Json.Nodes;
using VcvPatchBridge;
using ZstdSharp;

namespace VcvPatchBridge.Tests;

public class PatchFileTests
{
    [Fact]
    public void Loads_raw_JSON_vcv()
    {
        JsonObject root = TestPatchBuilder.Root(new[] { TestPatchBuilder.Module(1, "Cardinal", "HostMIDI") });
        PatchFile patch = TestPatchBuilder.ToPatchFile(root);

        Assert.False(patch.WasArchive);
        Assert.Single(patch.Root["modules"]!.AsArray());
    }

    [Fact]
    public void Save_then_Load_round_trips_through_tar_plus_zstd()
    {
        JsonObject root = TestPatchBuilder.Root(new[] { TestPatchBuilder.Module(42, "Cardinal", "HostMIDI") });
        PatchFile patch = TestPatchBuilder.ToPatchFile(root);

        string outPath = Path.Combine(Path.GetTempPath(), $"vcvpatchbridge-roundtrip-{Guid.NewGuid():N}.vcv");
        patch.Save(outPath);

        byte[] bytes = File.ReadAllBytes(outPath);
        Assert.Equal(new byte[] { 0x28, 0xB5, 0x2F, 0xFD }, bytes.Take(4));

        PatchFile reloaded = PatchFile.Load(outPath);
        Assert.True(reloaded.WasArchive);
        JsonObject module = (JsonObject)reloaded.Root["modules"]![0]!;
        Assert.Equal(42, module["id"]!.GetValue<long>());
        Assert.Equal("HostMIDI", module["model"]!.GetValue<string>());

        File.Delete(outPath);
    }

    [Fact]
    public void Extra_archive_entries_survive_a_save_reload_cycle()
    {
        JsonObject root = TestPatchBuilder.Root(new[] { TestPatchBuilder.Module(1, "Cardinal", "AudioFile") });
        PatchFile patch = TestPatchBuilder.ToPatchFile(root);
        patch.ExtraEntries.Add(("some-sample.wav", new byte[] { 1, 2, 3, 4 }));

        string outPath = Path.Combine(Path.GetTempPath(), $"vcvpatchbridge-extras-{Guid.NewGuid():N}.vcv");
        patch.Save(outPath);

        PatchFile reloaded = PatchFile.Load(outPath);
        Assert.Single(reloaded.ExtraEntries);
        Assert.Equal("some-sample.wav", reloaded.ExtraEntries[0].Name);
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, reloaded.ExtraEntries[0].Data);

        File.Delete(outPath);
    }

    [Fact]
    public void Loads_archives_where_patch_json_has_a_leading_dot_slash()
    {
        // Some real-world Cardinal/Rack builds (seen on Windows) write entries as "./" and
        // "./patch.json" instead of a bare "patch.json".
        JsonObject root = TestPatchBuilder.Root(new[] { TestPatchBuilder.Module(7, "Cardinal", "HostMIDI") });
        byte[] patchJsonBytes = Encoding.UTF8.GetBytes(root.ToJsonString());

        string outPath = Path.Combine(Path.GetTempPath(), $"vcvpatchbridge-dotslash-{Guid.NewGuid():N}.vcv");

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
            using FileStream outFile = File.Create(outPath);
            using CompressionStream compressionStream = new CompressionStream(outFile, level: 3);
            tarBuffer.CopyTo(compressionStream);
        }

        PatchFile loaded = PatchFile.Load(outPath);
        Assert.True(loaded.WasArchive);
        JsonObject module = (JsonObject)loaded.Root["modules"]![0]!;
        Assert.Equal(7, module["id"]!.GetValue<long>());

        File.Delete(outPath);
    }
}