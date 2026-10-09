using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ZstdSharp;

namespace VcvPatchTools.Core.Patch;

/// <summary>
/// A .vcv patch file, to read and write back. A .vcv is either:
///   - raw JSON (used for patches shipped inside the Cardinal repo), or
///   - a zstd-compressed tar archive containing "patch.json" (+ optional extra files, e.g. module data),
///     which is what Rack/Cardinal actually write when a user saves a patch from the UI.
/// Detection matches Rack's own check (src/patch.cpp): first 4 bytes == zstd magic.
/// Bytes in, bytes out: no file I/O, so it also runs in the browser.
/// </summary>
public sealed class PatchArchive
{
    private static readonly byte[] zstdMagic = { 0x28, 0xB5, 0x2F, 0xFD };

    public JsonObject Root { get; }

    /// <summary>Any non-"patch.json" files found in the source archive, preserved byte-for-byte on save.</summary>
    public List<(string Name, byte[] Data)> ExtraEntries { get; }

    public bool WasArchive { get; }

    public PatchArchive(JsonObject root, List<(string Name, byte[] Data)>? extraEntries = null, bool wasArchive = false)
    {
        Root = root;
        ExtraEntries = extraEntries ?? new List<(string Name, byte[] Data)>();
        WasArchive = wasArchive;
    }

    public static PatchArchive Read(byte[] bytes)
    {
        bool isArchive = bytes.Length >= 4 && bytes.AsSpan(0, 4).SequenceEqual(zstdMagic);
        if (!isArchive)
        {
            return new PatchArchive(ParseJson(bytes));
        }

        using MemoryStream compressedStream = new MemoryStream(bytes);
        using DecompressionStream tarStream = new DecompressionStream(compressedStream);
        using MemoryStream tarCopy = new MemoryStream();
        tarStream.CopyTo(tarCopy);

        JsonObject? patchJson = null;
        List<(string Name, byte[] Data)> extras = new List<(string Name, byte[] Data)>();
        foreach ((string name, byte[] data) in TarEntries.Read(tarCopy.ToArray()))
        {
            if (NormalizeEntryName(name) == "patch.json")
            {
                patchJson = ParseJson(data);
            }
            else
            {
                extras.Add((name, data));
            }
        }

        return patchJson is null
            ? throw new InvalidDataException("The file is a tar+zstd archive but contains no patch.json entry.")
            : new PatchArchive(patchJson, extras, wasArchive: true);
    }

    /// <summary>Always a tar+zstd archive, matching what Rack/Cardinal produce when saving from the UI.</summary>
    /// <param name="modified">Modification time of the archive's files (now, when omitted).</param>
    public byte[] Write(DateTimeOffset? modified = null)
    {
        byte[] patchJsonBytes = Encoding.UTF8.GetBytes(Root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        byte[] tar = TarEntries.Write(ExtraEntries.Prepend(("patch.json", patchJsonBytes)), modified ?? DateTimeOffset.UtcNow);

        using MemoryStream output = new MemoryStream();
        using (CompressionStream compressionStream = new CompressionStream(output, level: 19))
        {
            compressionStream.Write(tar);
        }
        return output.ToArray();
    }

    /// <summary>Strips a leading "./" (or repeated ones), matching tar writers that emit paths relative to "./".</summary>
    internal static string NormalizeEntryName(string name)
    {
        while (name.StartsWith("./", StringComparison.Ordinal))
        {
            name = name[2..];
        }
        return name;
    }

    internal static JsonObject ParseJson(byte[] bytes)
    {
        JsonNode? node = JsonNode.Parse(bytes)
            ?? throw new InvalidDataException("Empty or invalid patch JSON.");
        return node as JsonObject
            ?? throw new InvalidDataException("Root patch JSON value is not an object.");
    }
}