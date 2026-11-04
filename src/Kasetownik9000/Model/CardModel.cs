using System.Text.Json.Serialization;

namespace Kasetownik9000.Model;

public static class Units
{
    /// <summary>Milimetry na punkt typograficzny.</summary>
    public const double MmPerPt = 25.4 / 72.0;

    /// <summary>DIP (1/96 cala) na milimetr.</summary>
    public const double DipPerMm = 96.0 / 25.4;
}

public enum PanelRole { Front, Spine, Flap, Inner, Label, Other }

public sealed class PanelDef
{
    public string Name { get; set; } = "Panel";
    public double Height { get; set; } = 20;
    public PanelRole Role { get; set; } = PanelRole.Other;

    /// <summary>Krawędź pod tym panelem to linia cięcia (a nie zgięcia).</summary>
    public bool CutAfter { get; set; }

    public PanelDef Clone() => new() { Name = Name, Height = Height, Role = Role, CutAfter = CutAfter };
}

public sealed class CardDocument
{
    public int Version { get; set; } = 1;
    public string FormatId { get; set; } = "jcard";
    public double Width { get; set; } = 101.6;
    public List<PanelDef> Panels { get; set; } = [];
    public string Background { get; set; } = "#FFFFFFFF";
    public double Bleed { get; set; } = 1.0;
    public List<CardElement> Elements { get; set; } = [];

    [JsonIgnore]
    public double Height
    {
        get
        {
            double h = 0;
            foreach (var p in Panels) h += p.Height;
            return h;
        }
    }

    public double PanelTop(int index)
    {
        double y = 0;
        for (int i = 0; i < index && i < Panels.Count; i++) y += Panels[i].Height;
        return y;
    }

    public int PanelIndexAt(double y)
    {
        double top = 0;
        for (int i = 0; i < Panels.Count; i++)
        {
            if (y < top + Panels[i].Height) return i;
            top += Panels[i].Height;
        }
        return Panels.Count - 1;
    }

    public CardElement? FindElement(string? id) => id is null ? null : Elements.Find(e => e.Id == id);
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(TextElement), "text")]
[JsonDerivedType(typeof(ImageElement), "image")]
[JsonDerivedType(typeof(ShapeElement), "shape")]
[JsonDerivedType(typeof(StripesElement), "stripes")]
public abstract class CardElement
{
    public string Id { get; set; } = NewId();
    public string Name { get; set; } = "";
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; } = 40;
    public double Height { get; set; } = 10;
    public double Rotation { get; set; }
    public double Opacity { get; set; } = 1;
    public bool Locked { get; set; }
    public bool Hidden { get; set; }

    public static string NewId() => Guid.NewGuid().ToString("N")[..12];

    [JsonIgnore] public abstract string KindName { get; }
    [JsonIgnore] public abstract string Glyph { get; }

    [JsonIgnore] public double CenterX => X + Width / 2;
    [JsonIgnore] public double CenterY => Y + Height / 2;
}

public enum TextAlign { Left, Center, Right }
public enum TextVAlign { Top, Middle, Bottom }

public sealed class TextElement : CardElement
{
    public string Text { get; set; } = "Tekst";
    public string FontFamily { get; set; } = "Bahnschrift";
    public double FontSize { get; set; } = 14; // pt
    public string Color { get; set; } = "#FF2B2420";
    public bool Bold { get; set; }
    public bool Italic { get; set; }
    public bool Uppercase { get; set; }
    public TextAlign Align { get; set; } = TextAlign.Left;
    public TextVAlign VAlign { get; set; } = TextVAlign.Top;
    /// <summary>Odstęp między literami jako ułamek wielkości fontu (0.1 = 10%).</summary>
    public double LetterSpacing { get; set; }
    public double LineSpacing { get; set; } = 1.0;
    /// <summary>Zmniejsz font tak, by tekst zmieścił się w ramce.</summary>
    public bool AutoFit { get; set; }
    public bool Shadow { get; set; }
    public string ShadowColor { get; set; } = "#FFE8722C";
    public double ShadowOffset { get; set; } = 0.5;

    public override string KindName => "Tekst";
    public override string Glyph => "";
}

public enum ImageFit { Fill, Fit, Stretch }
public enum ImageFilter { None, Grayscale, Sepia }

public sealed class ImageElement : CardElement
{
    public string AssetId { get; set; } = "";
    public ImageFit Fit { get; set; } = ImageFit.Fill;
    public ImageFilter Filter { get; set; } = ImageFilter.None;

    public override string KindName => "Obraz";
    public override string Glyph => "";
}

public enum ShapeKind { Rectangle, Ellipse, Line }

public sealed class ShapeElement : CardElement
{
    public ShapeKind Shape { get; set; } = ShapeKind.Rectangle;
    public bool HasFill { get; set; } = true;
    public string FillColor { get; set; } = "#FFE8722C";
    public bool HasStroke { get; set; }
    public string StrokeColor { get; set; } = "#FF2B2420";
    public double StrokeWidth { get; set; } = 0.5; // mm
    public double CornerRadius { get; set; } // mm
    public bool Dashed { get; set; }

    public override string KindName => Shape switch
    {
        ShapeKind.Ellipse => "Elipsa",
        ShapeKind.Line => "Linia",
        _ => "Prostokąt",
    };

    public override string Glyph => Shape switch
    {
        ShapeKind.Ellipse => "",
        ShapeKind.Line => "",
        _ => "",
    };
}

public sealed class StripesElement : CardElement
{
    public List<string> Colors { get; set; } = [];
    public bool Vertical { get; set; }
    public double Gap { get; set; }

    public override string KindName => "Paski retro";
    public override string Glyph => "";
}

/// <summary>Format pliku projektu (.kaseta).</summary>
public sealed class KasetaFile
{
    public string App { get; set; } = "Kasetownik9000";
    public CardDocument Card { get; set; } = new();
    /// <summary>Obrazy osadzone w projekcie: id → base64.</summary>
    public Dictionary<string, string> Assets { get; set; } = [];
}

/// <summary>Element skopiowany do schowka (z osadzonym obrazem, jeśli to obraz).</summary>
public sealed class ClipboardPayload
{
    public CardElement? Element { get; set; }
    public string? AssetId { get; set; }
    public string? AssetData { get; set; }
}

[JsonSourceGenerationOptions(
    WriteIndented = false,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.Never)]
[JsonSerializable(typeof(KasetaFile))]
[JsonSerializable(typeof(CardDocument))]
[JsonSerializable(typeof(CardElement))]
[JsonSerializable(typeof(ClipboardPayload))]
internal partial class ModelJson : JsonSerializerContext
{
}
