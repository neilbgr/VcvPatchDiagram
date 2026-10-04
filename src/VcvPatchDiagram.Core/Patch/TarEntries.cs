using System.Text;

namespace VcvPatchDiagram.Core.Patch;

/// <summary>
/// Minimal read-only tar walker. System.Formats.Tar throws PlatformNotSupportedException in the browser (Blazor WASM),
/// and a .vcv only needs "find patch.json", so this handles just what Rack/Cardinal write:
/// ustar headers (name + prefix), pax extended headers ('x', "path=") and GNU long names ('L').
/// </summary>
public static class TarEntries
{
    private const int blockSize = 512;

    public static IEnumerable<(string Name, byte[] Data)> Read(byte[] tar)
    {
        int offset = 0;
        string? longName = null;
        while (offset + blockSize <= tar.Length)
        {
            ReadOnlySpan<byte> header = tar.AsSpan(offset, blockSize);
            if (header.IndexOfAnyExcept((byte)0) < 0)
            {
                yield break; // End-of-archive marker (zero block).
            }

            string name = Text(header.Slice(0, 100));
            long size = Octal(header.Slice(124, 12));
            char type = (char)header[156];
            string prefix = Text(header.Slice(345, 155));
            if (prefix.Length > 0 && Encoding.ASCII.GetString(header.Slice(257, 5)) == "ustar")
            {
                name = $"{prefix}/{name}";
            }

            int dataStart = offset + blockSize;
            if (size < 0 || dataStart + size > tar.Length)
            {
                throw new InvalidDataException("Truncated or corrupted tar archive.");
            }
            byte[] data = tar.AsSpan(dataStart, (int)size).ToArray();
            offset = dataStart + (int)(((size + blockSize - 1) / blockSize) * blockSize);

            switch (type)
            {
                case 'x':
                    longName = PaxPath(data) ?? longName;
                    continue;
                case 'L':
                    longName = Text(data);
                    continue;
                case 'g':
                    continue;
                case '0' or '\0' or '7':
                    yield return (longName ?? name, data);
                    break;
            }
            longName = null;
        }
    }

    /// <summary>Pax records are "LENGTH key=value\n"; only "path" matters here.</summary>
    private static string? PaxPath(byte[] data)
    {
        foreach (string record in Encoding.UTF8.GetString(data).Split('\n'))
        {
            int space = record.IndexOf(' ');
            if (space > 0 && record[(space + 1)..].StartsWith("path=", StringComparison.Ordinal))
            {
                return record[(space + 6)..];
            }
        }
        return null;
    }

    private static string Text(ReadOnlySpan<byte> field)
    {
        int end = field.IndexOf((byte)0);
        return Encoding.UTF8.GetString(end < 0 ? field : field[..end]);
    }

    private static long Octal(ReadOnlySpan<byte> field)
    {
        long value = 0;
        foreach (byte b in field)
        {
            if (b is >= (byte)'0' and <= (byte)'7')
            {
                value = (value * 8) + (b - '0');
            }
            else if (b is 0 or (byte)' ' && value > 0)
            {
                break;
            }
        }
        return value;
    }
}