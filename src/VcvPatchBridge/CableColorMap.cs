using System.Globalization;

namespace VcvPatchBridge;

/// <summary>
/// Default cable-color palettes used by VCV Rack ("Core") and Cardinal, and the hue-nearest
/// mapping between them. Rack's 5-color palette comes from its bundled Rack source
/// (src/Rack/src/settings.cpp, pinned at v2.4.1); Cardinal's 16-color palette comes from
/// Cardinal's own startup override (src/CardinalCommon.cpp), sourced from the community
/// "16 colour cable palette" thread on the VCV forum. The two palettes are hue wheels at
/// different saturation/value, so matching is done on hue angle alone.
/// </summary>
public static class CableColorMap
{
    public static readonly string[] RackPalette =
    {
        "#f3374b", "#ffb437", "#00b56e", "#3695ef", "#8b4ade",
    };

    public static readonly string[] CardinalPalette =
    {
        "#ff5252", "#ff9352", "#ffd452", "#e8ff52", "#a8ff52", "#67ff52", "#52ff7d", "#52ffbe",
        "#52ffff", "#52beff", "#527dff", "#6752ff", "#a852ff", "#e952ff", "#ff52d4", "#ff5293",
    };

    public static string MapToCardinal(string hex) => NearestByHue(hex, CardinalPalette);

    public static string MapToRack(string hex) => NearestByHue(hex, RackPalette);

    private static string NearestByHue(string hex, string[] palette)
    {
        if (!TryParseRgb(hex, out (int r, int g, int b) rgb))
        {
            return hex; // not a recognizable "#rrggbb" color: leave untouched
        }

        (double hue, double saturation) = ToHueSaturation(rgb);
        bool useRgbDistance = saturation < 0.08; // near-gray: hue is numerically unstable

        string best = palette[0];
        double bestDistance = double.MaxValue;
        foreach (string candidate in palette)
        {
            TryParseRgb(candidate, out (int r, int g, int b) candidateRgb);
            double distance = useRgbDistance
                ? RgbDistance(rgb, candidateRgb)
                : HueDistance(hue, ToHueSaturation(candidateRgb).hue);

            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = candidate;
            }
        }

        return best;
    }

    private static double HueDistance(double a, double b)
    {
        double delta = Math.Abs(a - b) % 360;
        return delta > 180 ? 360 - delta : delta;
    }

    private static double RgbDistance((int r, int g, int b) a, (int r, int g, int b) b)
    {
        double dr = a.r - b.r;
        double dg = a.g - b.g;
        double db = a.b - b.b;
        return (dr * dr) + (dg * dg) + (db * db);
    }

    private static (double hue, double saturation) ToHueSaturation((int r, int g, int b) rgb)
    {
        double r = rgb.r / 255.0;
        double g = rgb.g / 255.0;
        double b = rgb.b / 255.0;

        double max = Math.Max(r, Math.Max(g, b));
        double min = Math.Min(r, Math.Min(g, b));
        double diff = max - min;

        double saturation = max <= 0 ? 0 : diff / max;
        if (diff <= 0)
        {
            return (0, saturation);
        }

        double hue;
        if (max == r)
        {
            hue = 60 * (((g - b) / diff) % 6);
        }
        else if (max == g)
        {
            hue = 60 * (((b - r) / diff) + 2);
        }
        else
        {
            hue = 60 * (((r - g) / diff) + 4);
        }

        if (hue < 0)
        {
            hue += 360;
        }

        return (hue, saturation);
    }

    private static bool TryParseRgb(string hex, out (int r, int g, int b) rgb)
    {
        rgb = default;
        if (hex.Length != 7 || hex[0] != '#')
        {
            return false;
        }

        if (!byte.TryParse(hex.AsSpan(1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte r)
            || !byte.TryParse(hex.AsSpan(3, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte g)
            || !byte.TryParse(hex.AsSpan(5, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte b))
        {
            return false;
        }

        rgb = (r, g, b);
        return true;
    }
}