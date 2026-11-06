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

/// <summary>Kierunek odwracania kartki, żeby zobaczyć jej wnętrze.</summary>
public enum InsideFlip { LeftRight, TopBottom }

public sealed class PanelDef
{
    public string Name { get; set; } = "Panel";
    public double Height { get; set; } = 20;
    public PanelRole Role { get; set; } = PanelRole.Other;

    /// <summary>Krawędź pod tym panelem to linia cięcia (a nie zgięcia).</summary>
    public bool CutAfter { get; set; }

    /// <summary>Naklejka na kasetę: okienko na szpule do wycięcia (patrz <see cref="LabelWindow"/>).</summary>
    public bool ReelWindow { get; set; } = true;

    public PanelDef Clone() => new() { Name = Name, Height = Height, Role = Role, CutAfter = CutAfter, ReelWindow = ReelWindow };
}

/// <summary>Jedna karta do wydruku (J-card, naklejki…). Projekt kasety może mieć ich kilka.</summary>
public sealed class CardDocument
{
    public int Version { get; set; } = 1;
    /// <summary>Stały identyfikator karty — wiąże wnętrze (drugą stronę kartki) z kartą zewnętrzną.</summary>
    public string Id { get; set; } = CardElement.NewId();
    /// <summary>
    /// Wnętrze karty o tym Id: druga strona tej samej kartki (druk dwustronny). Wymiary i panele są zawsze
    /// takie jak w karcie zewnętrznej; treść projektuje się tak, jak widać ją po odwróceniu kartki (lewa ↔ prawa).
    /// null = zwykła karta.
    /// </summary>
    public string? InsideOf { get; set; }
    /// <summary>
    /// Wnętrze: jak obraca się kartkę, żeby je zobaczyć. Góra ↔ dół — tak widać wnętrze po otwarciu pudełka
    /// (J-karta obraca się na zawiasie przy grzbiecie); panele wnętrza idą wtedy w odwrotnej kolejności.
    /// Brak wartości w pliku = lewa ↔ prawa (tak działały wnętrza z wersji 1.7).
    /// </summary>
    public InsideFlip Flip { get; set; } = InsideFlip.LeftRight;
    /// <summary>Nazwa zakładki, np. "J-card" albo "Naklejki".</summary>
    public string Name { get; set; } = "";
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

    public int PanelIndexAt(double y) => PanelIndexAtIn(Panels, y);

    /// <summary>Indeks panelu na wysokości y w podanym układzie paneli (np. sprzed zmiany).</summary>
    public int PanelIndexAtIn(List<PanelDef> panels, double y)
    {
        double top = 0;
        for (int i = 0; i < panels.Count; i++)
        {
            if (y < top + panels[i].Height) return i;
            top += panels[i].Height;
        }
        return Math.Max(0, panels.Count - 1);
    }

    public CardElement? FindElement(string? id) => id is null ? null : Elements.Find(e => e.Id == id);

    public bool HasRole(PanelRole role) => Panels.Exists(p => p.Role == role);

    [JsonIgnore] public bool IsInside => InsideOf is not null;

    /// <summary>Czy ta karta może mieć wnętrze (naklejki na kasetę są jednostronne).</summary>
    [JsonIgnore] public bool CanHaveInside => !IsInside && !HasRole(PanelRole.Label);
}

/// <summary>Dane albumu — wstawiane do tekstów przez pola {tytuł}, {wykonawca}…</summary>
public sealed class AlbumInfo
{
    public string Title { get; set; } = "Tytuł albumu";
    public string Artist { get; set; } = "Wykonawca";
    public string Year { get; set; } = DateTime.Now.Year.ToString();
    public string Date { get; set; } = DateTime.Now.ToString("dd.MM.yyyy");
    public string Note { get; set; } = "";
}

public sealed class TapeTrack
{
    public string Title { get; set; } = "";
    public string Artist { get; set; } = "";
    public double Seconds { get; set; }
    /// <summary>0 = strona A, 1 = strona B.</summary>
    public int Side { get; set; }
    public string? SourcePath { get; set; }
}

/// <summary>Plan nagrania: długość kasety i utwory na stronach A/B.</summary>
public sealed class TapePlan
{
    public int Minutes { get; set; } = 60;
    public double GapSeconds { get; set; } = 2;
    public bool ShowTimes { get; set; } = true;
    public bool ShowArtist { get; set; } = true;
    public bool SideNumbers { get; set; }
    public List<TapeTrack> Tracks { get; set; } = [];

    [JsonIgnore] public string TapeName => $"C-{Minutes}";
    [JsonIgnore] public double SideCapacitySeconds => Minutes * 30.0;
}

/// <summary>Projekt jednej kasety: dane albumu, plan taśmy i karty do wydruku.</summary>
public sealed class CardProject
{
    public int Version { get; set; } = 2;
    /// <summary>Styl startowy projektu — nowe karty (np. dodane naklejki) dostają ten sam wygląd.</summary>
    public string StyleId { get; set; } = "empty";
    public AlbumInfo Album { get; set; } = new();
    public TapePlan Tape { get; set; } = new();
    public List<CardDocument> Cards { get; set; } = [];

    /// <summary>Wnętrze danej karty (druga strona kartki) albo null.</summary>
    public CardDocument? InsideOf(CardDocument outer) => Cards.Find(c => c.InsideOf == outer.Id);

    /// <summary>Karta zewnętrzna dla wnętrza albo null.</summary>
    public CardDocument? OuterOf(CardDocument inside) => inside.InsideOf is null ? null : Cards.Find(c => c.Id == inside.InsideOf);

    /// <summary>Karty „z zewnątrz” — to one leżą na przedniej stronie arkusza, w 3D itd.</summary>
    public IEnumerable<CardDocument> OuterCards => Cards.Where(c => !c.IsInside);

    /// <summary>
    /// Wnętrza dostają wymiary, spad i panele karty zewnętrznej (zawsze w tej samej kolejności — kartkę obracamy
    /// jak stronę książki). Wnętrze bez karty zewnętrznej staje się zwykłą kartą. Wywoływane po każdej zmianie.
    /// </summary>
    public void SyncInsides()
    {
        foreach (var inside in Cards)
        {
            if (inside.InsideOf is null) continue;
            var outer = OuterOf(inside);
            if (outer is null || outer.IsInside || ReferenceEquals(outer, inside)) { inside.InsideOf = null; continue; }
            inside.FormatId = outer.FormatId;
            inside.Width = outer.Width;
            inside.Bleed = outer.Bleed;
            var expected = InsidePanels(outer, inside.Flip);
            bool same = inside.Panels.Count == expected.Count;
            for (int i = 0; same && i < expected.Count; i++)
            {
                var a = inside.Panels[i]; var b = expected[i];
                same = a.Name == b.Name && a.Height == b.Height && a.Role == b.Role && a.CutAfter == b.CutAfter;
            }
            if (!same) inside.Panels = expected;
        }
    }

    /// <summary>
    /// Panele wnętrza: przy obracaniu lewa ↔ prawa w tej samej kolejności co na zewnątrz, przy góra ↔ dół —
    /// w odwrotnej (linia cięcia „pod panelem” przechodzi wtedy na krawędź nad nim).
    /// </summary>
    public static List<PanelDef> InsidePanels(CardDocument outer, InsideFlip flip)
    {
        var src = outer.Panels;
        if (flip == InsideFlip.LeftRight) return [.. src.Select(p => p.Clone())];
        int n = src.Count;
        var list = new List<PanelDef>(n);
        for (int j = 0; j < n; j++)
        {
            var p = src[n - 1 - j].Clone();
            p.CutAfter = j < n - 1 && src[n - 2 - j].CutAfter;
            list.Add(p);
        }
        return list;
    }

    /// <summary>Który panel wnętrza leży za panelem <paramref name="outerIndex"/> karty zewnętrznej.</summary>
    public static int InsidePanelIndex(CardDocument inside, int outerIndex) =>
        inside.Flip == InsideFlip.TopBottom ? inside.Panels.Count - 1 - outerIndex : outerIndex;
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
    /// <summary>Odbicie lustrzane w poziomie (lewa ↔ prawa) i w pionie (góra ↕ dół), wokół środka ramki.</summary>
    public bool FlipX { get; set; }
    public bool FlipY { get; set; }

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

/// <summary>Format pliku projektu (.kaseta): cały projekt + osadzone obrazy.</summary>
public sealed class KasetaFile
{
    public string App { get; set; } = "Kasetownik9000";
    public string? AppVersion { get; set; }
    public int FormatVersion { get; set; } = 2;
    public CardProject? Project { get; set; }
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
[JsonSerializable(typeof(CardProject))]
[JsonSerializable(typeof(TapePlan))]
[JsonSerializable(typeof(AppSettings))]
[JsonSerializable(typeof(CardDocument))]
[JsonSerializable(typeof(CardElement))]
[JsonSerializable(typeof(ClipboardPayload))]
internal partial class ModelJson : JsonSerializerContext
{
}
