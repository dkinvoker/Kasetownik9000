using System.Globalization;
using Windows.UI;

namespace Kasetownik9000.Model;

public static class ColorUtil
{
    public static Color Parse(string? hex, Color fallback = default)
    {
        if (string.IsNullOrWhiteSpace(hex)) return fallback;
        var s = hex.Trim().TrimStart('#');
        if (s.Length == 6) s = "FF" + s;
        if (s.Length != 8 || !uint.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var v))
            return fallback;
        return Color.FromArgb((byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v);
    }

    public static string ToHex(Color c) => $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";

    public static Color WithAlpha(Color c, byte a) => Color.FromArgb(a, c.R, c.G, c.B);

    /// <summary>Retro paleta wyświetlana w próbnikach kolorów.</summary>
    public static readonly string[] RetroPalette =
    [
        "#FFF3E9D2", "#FFFFFFFF", "#FFCDBFA5", "#FF9A9086",
        "#FF2B2420", "#FF111111", "#FF7A3B2E", "#FFC8402F",
        "#FFE8722C", "#FFF2B035", "#FFF6D55C", "#FF8A9A3B",
        "#FF3E8E87", "#FF20639B", "#FF6B3E75", "#FFE85D75",
    ];
}

public sealed class StripePalette(string name, params string[] colors)
{
    public string Name { get; } = name;
    public string[] Colors { get; } = colors;

    public static readonly StripePalette[] All =
    [
        new("Zachód słońca", "#FFF2B035", "#FFE8722C", "#FFC8402F", "#FF7A3B2E"),
        new("Tęcza 70s", "#FFC8402F", "#FFE8722C", "#FFF2B035", "#FF8A9A3B", "#FF3E8E87", "#FF20639B"),
        new("Ocean", "#FF9ED8DB", "#FF3E8E87", "#FF20639B", "#FF173F5F"),
        new("Neon 80s", "#FFFF3EA5", "#FFB967FF", "#FF01CDFE", "#FF05FFA1"),
        new("Kawa z mlekiem", "#FFE6CCB2", "#FFB08968", "#FF7F5539", "#FF4A2E1F"),
        new("Chrom", "#FFEDEDED", "#FFB8B8B8", "#FF7D7D7D", "#FF3C3C3C"),
        new("Malina", "#FFFFC2D1", "#FFFF8FAB", "#FFE85D75", "#FF9E2A4B"),
        new("Las", "#FFD9E4C6", "#FF8A9A3B", "#FF4F6D3A", "#FF2E3B24"),
    ];
}
