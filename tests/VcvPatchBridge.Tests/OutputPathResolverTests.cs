using VcvPatchBridge;

namespace VcvPatchBridge.Tests;

public class OutputPathResolverTests
{
    [Fact]
    public void No_collision_returns_base_candidate_with_target_slug()
    {
        string inputPath = Path.Combine(Path.GetTempPath(), $"vcvpatchbridge-resolver-{Guid.NewGuid():N}.vcv");

        string result = OutputPathResolver.Resolve(inputPath, PatchOrigin.Cardinal, force: false);

        string expected = Path.Combine(Path.GetTempPath(), $"{Path.GetFileNameWithoutExtension(inputPath)}.cardinal.vcv");
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Rack_target_uses_rack_slug()
    {
        string inputPath = Path.Combine(Path.GetTempPath(), $"vcvpatchbridge-resolver-{Guid.NewGuid():N}.vcv");

        string result = OutputPathResolver.Resolve(inputPath, PatchOrigin.Rack, force: false);

        string expected = Path.Combine(Path.GetTempPath(), $"{Path.GetFileNameWithoutExtension(inputPath)}.rack.vcv");
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Existing_base_candidate_gets_incremental_suffix()
    {
        string inputPath = Path.Combine(Path.GetTempPath(), $"vcvpatchbridge-resolver-{Guid.NewGuid():N}.vcv");
        string baseName = Path.GetFileNameWithoutExtension(inputPath);
        string basePath = Path.Combine(Path.GetTempPath(), $"{baseName}.cardinal.vcv");
        File.WriteAllBytes(basePath, Array.Empty<byte>());

        try
        {
            string result = OutputPathResolver.Resolve(inputPath, PatchOrigin.Cardinal, force: false);

            Assert.Equal(Path.Combine(Path.GetTempPath(), $"{baseName}.cardinal.x1.vcv"), result);
        }
        finally
        {
            File.Delete(basePath);
        }
    }

    [Fact]
    public void Existing_base_and_x1_candidates_skip_to_x2()
    {
        string inputPath = Path.Combine(Path.GetTempPath(), $"vcvpatchbridge-resolver-{Guid.NewGuid():N}.vcv");
        string baseName = Path.GetFileNameWithoutExtension(inputPath);
        string basePath = Path.Combine(Path.GetTempPath(), $"{baseName}.cardinal.vcv");
        string x1Path = Path.Combine(Path.GetTempPath(), $"{baseName}.cardinal.x1.vcv");
        File.WriteAllBytes(basePath, Array.Empty<byte>());
        File.WriteAllBytes(x1Path, Array.Empty<byte>());

        try
        {
            string result = OutputPathResolver.Resolve(inputPath, PatchOrigin.Cardinal, force: false);

            Assert.Equal(Path.Combine(Path.GetTempPath(), $"{baseName}.cardinal.x2.vcv"), result);
        }
        finally
        {
            File.Delete(basePath);
            File.Delete(x1Path);
        }
    }

    [Fact]
    public void Existing_cardinal_suffix_is_replaced_not_stacked()
    {
        string inputPath = Path.Combine(Path.GetTempPath(), $"vcvpatchbridge-resolver-{Guid.NewGuid():N}.cardinal.vcv");

        string result = OutputPathResolver.Resolve(inputPath, PatchOrigin.Rack, force: false);

        string baseName = Path.GetFileNameWithoutExtension(inputPath)[..^".cardinal".Length];
        Assert.Equal(Path.Combine(Path.GetTempPath(), $"{baseName}.rack.vcv"), result);
    }

    [Fact]
    public void Existing_rack_suffix_is_replaced_not_stacked()
    {
        string inputPath = Path.Combine(Path.GetTempPath(), $"vcvpatchbridge-resolver-{Guid.NewGuid():N}.rack.vcv");

        string result = OutputPathResolver.Resolve(inputPath, PatchOrigin.Cardinal, force: false);

        string baseName = Path.GetFileNameWithoutExtension(inputPath)[..^".rack".Length];
        Assert.Equal(Path.Combine(Path.GetTempPath(), $"{baseName}.cardinal.vcv"), result);
    }

    [Fact]
    public void Force_returns_base_candidate_even_if_it_exists()
    {
        string inputPath = Path.Combine(Path.GetTempPath(), $"vcvpatchbridge-resolver-{Guid.NewGuid():N}.vcv");
        string baseName = Path.GetFileNameWithoutExtension(inputPath);
        string basePath = Path.Combine(Path.GetTempPath(), $"{baseName}.cardinal.vcv");
        File.WriteAllBytes(basePath, Array.Empty<byte>());

        try
        {
            string result = OutputPathResolver.Resolve(inputPath, PatchOrigin.Cardinal, force: true);

            Assert.Equal(basePath, result);
        }
        finally
        {
            File.Delete(basePath);
        }
    }
}