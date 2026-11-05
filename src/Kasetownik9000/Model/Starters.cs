namespace Kasetownik9000.Model;

/// <summary>Gotowe style startowe — układają przykładowe elementy na panelach karty.</summary>
public sealed class Starter(string id, string name, string description)
{
    public string Id { get; } = id;
    public string Name { get; } = name;
    public string Description { get; } = description;

    public static readonly Starter[] All =
    [
        new("retro", "Retro paski", "Kremowy papier, kolorowe paski jak z lat 80."),
        new("minimal", "Minimal noir", "Czarne tło, jasna typografia z dużym odstępem liter."),
        new("mixtape", "Składanka", "Papier w linie i odręczne pismo — klasyczny mixtape."),
        new("synthwave", "Synthwave", "Neonowy zachód słońca nad siatką — lata 80. w wersji retro-futurystycznej."),
        new("video", "Wideo 80s", "Czarne pudełko i tęczowe skośne pasy jak na kasetach wideo z wypożyczalni."),
        new("chrome", "Chrom (Type II)", "Metaliczne srebro i grafitowy pas — okładka kasety chromowej."),
        new("punk", "Punk ksero", "Czarno-biały zin z ksero: przekrzywione napisy, taśma i pieczątka DIY."),
        new("kraft", "Kraft", "Szary papier pakowy, zawieszka z dziurką i maszynopis."),
        new("swiss", "Szwajcarski", "Czerwony blok, czarna typografia i siatka — styl międzynarodowy."),
        new("empty", "Pusta karta", "Czysta biała karta — wszystko od zera."),
    ];
}

public static partial class Starters
{
    private const string Ink = "#FF2B2420";
    private const string Cream = "#FFF3E9D2";

    /// <summary>Przykładowe dane albumu pasujące do stylu (teksty na kartach używają pól {tytuł} itd.).</summary>
    public static AlbumInfo DefaultAlbum(string starterId) => starterId switch
    {
        "mixtape" => new AlbumInfo { Title = "Moja składanka", Artist = "różni wykonawcy", Note = "dla: Ciebie ♥" },
        "minimal" => new AlbumInfo { Title = "Tytuł", Artist = "Wykonawca" },
        "synthwave" => new AlbumInfo { Title = "Neonowe noce", Artist = "Wykonawca" },
        "punk" => new AlbumInfo { Title = "Garażowe nagrania", Artist = "Zespół" },
        "kraft" => new AlbumInfo { Title = "Domowe archiwum", Artist = "Wykonawca" },
        _ => new AlbumInfo(),
    };

    /// <summary>Nowy projekt: wybrany format + (opcjonalnie) naklejki A/B w tym samym stylu.</summary>
    public static CardProject CreateProject(string formatId, string starterId, bool withLabels)
    {
        var project = new CardProject { Album = DefaultAlbum(starterId), StyleId = starterId };
        var main = CardFormats.Get(formatId).CreateDocument();
        Apply(main, starterId);
        project.Cards.Add(main);
        if (withLabels && formatId != "labels")
        {
            var labels = CardFormats.Get("labels").CreateDocument();
            Apply(labels, starterId);
            project.Cards.Add(labels);
        }
        return project;
    }

    public static void Apply(CardDocument d, string starterId)
    {
        d.Elements.Clear();
        switch (starterId)
        {
            case "retro": Retro(d); break;
            case "minimal": Minimal(d); break;
            case "mixtape": Mixtape(d); break;
            case "synthwave": Synthwave(d); break;
            case "video": Video(d); break;
            case "chrome": Chrome(d); break;
            case "punk": Punk(d); break;
            case "kraft": Kraft(d); break;
            case "swiss": Swiss(d); break;
            default: d.Background = "#FFFFFFFF"; break;
        }
    }

    public static TextElement Text(string name, string text, double x, double y, double w, double h, double pt,
        string color = Ink, bool bold = false, TextAlign align = TextAlign.Left, TextVAlign valign = TextVAlign.Top,
        string font = "Bahnschrift", double spacing = 0, bool upper = false, bool autoFit = false,
        bool italic = false, string? shadow = null, double shadowOffset = 0.5, double rotation = 0, double lineSpacing = 1) => new()
    {
        Name = name, Text = text, X = x, Y = y, Width = w, Height = h, FontSize = pt, Color = color, Bold = bold,
        Align = align, VAlign = valign, FontFamily = font, LetterSpacing = spacing, Uppercase = upper, AutoFit = autoFit,
        Italic = italic, Shadow = shadow is not null, ShadowColor = shadow ?? "#FF000000", ShadowOffset = shadowOffset,
        Rotation = rotation, LineSpacing = lineSpacing,
    };

    public static StripesElement Stripes(string name, double x, double y, double w, double h, IEnumerable<string> colors,
        bool vertical = false, double gap = 0, double rotation = 0) => new()
    {
        Name = name, X = x, Y = y, Width = w, Height = h, Colors = [.. colors], Vertical = vertical, Gap = gap, Rotation = rotation,
    };

    public static ShapeElement Shape(string name, ShapeKind kind, double x, double y, double w, double h, string fill,
        bool hasFill = true, string? stroke = null, double strokeWidth = 0.5,
        double rotation = 0, bool dashed = false, double cornerRadius = 0, double opacity = 1) => new()
    {
        Name = name, Shape = kind, X = x, Y = y, Width = w, Height = h, FillColor = fill, HasFill = hasFill,
        HasStroke = stroke is not null, StrokeColor = stroke ?? Ink, StrokeWidth = strokeWidth,
        Rotation = rotation, Dashed = dashed, CornerRadius = cornerRadius, Opacity = opacity,
    };

    /// <summary>Linia z punktu (x1, y1) do (x2, y2) — linia w modelu jest pozioma, więc liczymy środek, długość i kąt.</summary>
    public static ShapeElement Segment(string name, double x1, double y1, double x2, double y2, string color, double width, bool dashed = false)
    {
        double dx = x2 - x1, dy = y2 - y1, len = Math.Max(0.5, Math.Sqrt(dx * dx + dy * dy));
        double cx = (x1 + x2) / 2, cy = (y1 + y2) / 2;
        return new ShapeElement
        {
            Name = name, Shape = ShapeKind.Line, X = cx - len / 2, Y = cy - 1, Width = len, Height = 2,
            Rotation = Math.Atan2(dy, dx) * 180 / Math.PI, StrokeColor = color, StrokeWidth = width, HasFill = false, Dashed = dashed,
        };
    }

    /// <summary>Pionowy zakres panelu rozszerzony o spad, jeśli panel leży przy górnej/dolnej krawędzi karty.</summary>
    private static (double Y, double H) WithBleed(CardDocument d, double top, double h)
    {
        double b = Math.Max(0, d.Bleed);
        double et = top <= 0.001 ? b : 0, eb = top + h >= d.Height - 0.001 ? b : 0;
        return (top - et, h + et + eb);
    }

    /// <summary>
    /// Okienko na szpule na naklejce (w układzie karty). Napisy stylów omijają je: tytuł nad okienkiem,
    /// wykonawca pod nim, litera strony z boku — tak jak na prawdziwych naklejkach.
    /// </summary>
    private static MmRect Window(CardDocument d, double top, double h)
    {
        var r = LabelWindow.InPanel(d.Width, h);
        return r with { Y = r.Y + top };
    }

    private static string SideLetter(int labelIndex) => labelIndex switch { 0 => "A", 1 => "B", _ => $"{labelIndex + 1}" };

    public const string TracksA = "STRONA A\n{lista A}";
    public const string TracksB = "STRONA B\n{lista B}";

    private static IEnumerable<(PanelDef P, double Top, int LabelIndex)> Panels(CardDocument d)
    {
        int label = 0;
        for (int i = 0; i < d.Panels.Count; i++)
        {
            var p = d.Panels[i];
            yield return (p, d.PanelTop(i), p.Role == PanelRole.Label ? label++ : -1);
        }
    }

    private static void Retro(CardDocument d)
    {
        d.Background = Cream;
        double W = d.Width, b = d.Bleed;
        var sunset = StripePalette.All[0].Colors;
        var els = d.Elements;
        foreach (var (p, top, li) in Panels(d))
        {
            double h = p.Height;
            switch (p.Role)
            {
                case PanelRole.Front:
                    els.Add(Text("Tytuł", "{tytuł}", 6, top + 7, W - 12, 12, 22, bold: true, spacing: 0.04, upper: true, autoFit: true));
                    els.Add(Text("Wykonawca", "{wykonawca}", 6, top + 19, W - 12, 8, 13, color: "#FF7A3B2E"));
                    els.Add(Stripes("Paski", -b, top + h * 0.58, W + 2 * b, h * 0.2, sunset));
                    els.Add(Text("Opis", "STEREO · TYPE I · NORMAL", 6, top + h - 8.5, 60, 5, 7, spacing: 0.15));
                    els.Add(Text("Długość", "{kaseta}", W - 26, top + h - 9.5, 20, 6, 11, color: "#FFC8402F", bold: true, align: TextAlign.Right));
                    break;
                case PanelRole.Spine:
                    els.Add(Stripes("Paski grzbietu", W - 14, top, 14 + b, h, sunset, vertical: true));
                    els.Add(Text("Grzbiet", "{wykonawca} · {tytuł}", 4, top + 1, W - 22, h - 2, 11, bold: true,
                        valign: TextVAlign.Middle, spacing: 0.05, upper: true, autoFit: true));
                    break;
                case PanelRole.Flap:
                    els.Add(Text("Klapka", "Nagrano: {data}\nDolby B NR", 6, top + 1.5, W - 12, h - 3, 8,
                        color: "#FF7A3B2E", valign: TextVAlign.Middle));
                    break;
                case PanelRole.Inner:
                    els.Add(Stripes("Paski wkładki", -b, top, W + 2 * b, 3, sunset));
                    els.Add(Text("Strona A", TracksA, 6, top + 8, W / 2 - 8, h - 12, 9, autoFit: true));
                    els.Add(Text("Strona B", TracksB, W / 2 + 2, top + 8, W / 2 - 8, h - 12, 9, autoFit: true));
                    break;
                case PanelRole.Label:
                    {
                        var win = Window(d, top, h);
                        double side = win.X - 1.5, cy = win.CenterY;
                        els.Add(Text("Tytuł", "{tytuł}", 5, top + 2.5, W - 10, win.Y - top - 3.5, 14, bold: true, valign: TextVAlign.Middle,
                            spacing: 0.04, upper: true, autoFit: true));
                        els.Add(Shape("Kółko", ShapeKind.Ellipse, side / 2 - 5, cy - 5, 10, 10, "#FFC8402F"));
                        els.Add(Text("Strona", SideLetter(li), side / 2 - 5, cy - 5, 10, 10, 16, color: Cream,
                            bold: true, align: TextAlign.Center, valign: TextVAlign.Middle));
                        els.Add(Text("Długość", "{kaseta}", win.Right + 1.5, win.Y, W - win.Right - 3, win.H, 9, color: "#FFC8402F", bold: true,
                            align: TextAlign.Center, valign: TextVAlign.Middle, autoFit: true));
                        els.Add(Stripes("Paski", -b, win.Bottom + 1.5, W + 2 * b, 7, sunset));
                        break;
                    }
            }
        }
    }

    private static void Minimal(CardDocument d)
    {
        d.Background = "#FF161616";
        double W = d.Width;
        const string White = "#FFF2F2F2", Gray = "#FFA8A8A8", Accent = "#FFE8722C";
        var els = d.Elements;
        foreach (var (p, top, li) in Panels(d))
        {
            double h = p.Height;
            switch (p.Role)
            {
                case PanelRole.Front:
                    els.Add(Text("Tytuł", "{tytuł}", 6, top + h * 0.3, W - 12, 14, 26, color: White, align: TextAlign.Center,
                        valign: TextVAlign.Middle, font: "Bahnschrift Light", spacing: 0.3, upper: true, autoFit: true));
                    els.Add(new ShapeElement
                    {
                        Name = "Linia", Shape = ShapeKind.Line, X = W / 2 - 15, Y = top + h * 0.6, Width = 30, Height = 2,
                        StrokeColor = Accent, StrokeWidth = 0.4, HasFill = false,
                    });
                    els.Add(Text("Wykonawca", "{wykonawca}", 6, top + h * 0.64, W - 12, 7, 10, color: Gray,
                        align: TextAlign.Center, spacing: 0.25, upper: true));
                    break;
                case PanelRole.Spine:
                    els.Add(Text("Grzbiet", "{wykonawca} — {tytuł}", 6, top + 1, W - 12, h - 2, 10, color: White,
                        align: TextAlign.Center, valign: TextVAlign.Middle, spacing: 0.2, upper: true, autoFit: true));
                    break;
                case PanelRole.Flap:
                    els.Add(Text("Rok", "{rok}", 6, top + 1, W - 12, h - 2, 8, color: Gray,
                        align: TextAlign.Center, valign: TextVAlign.Middle, spacing: 0.4));
                    break;
                case PanelRole.Inner:
                    els.Add(Text("Strona A", TracksA, 8, top + 8, W / 2 - 10, h - 14, 8.5, color: White, spacing: 0.05, autoFit: true));
                    els.Add(Text("Strona B", TracksB, W / 2 + 2, top + 8, W / 2 - 10, h - 14, 8.5, color: White, spacing: 0.05, autoFit: true));
                    break;
                case PanelRole.Label:
                    {
                        var win = Window(d, top, h);
                        els.Add(Text("Tytuł", "{tytuł}", 6, top + 2.5, W - 12, win.Y - top - 3.5, 12, color: White, align: TextAlign.Center,
                            valign: TextVAlign.Middle, spacing: 0.25, upper: true, autoFit: true));
                        els.Add(Text("Strona", SideLetter(li), 1.5, win.Y, win.X - 3, win.H, 28, color: Accent,
                            font: "Bahnschrift Light", align: TextAlign.Center, valign: TextVAlign.Middle, autoFit: true));
                        els.Add(Text("Długość", "{kaseta}", win.Right + 1.5, win.Y, W - win.Right - 3, win.H, 7, color: Gray,
                            align: TextAlign.Center, valign: TextVAlign.Middle, spacing: 0.1, autoFit: true));
                        els.Add(Text("Wykonawca", "{wykonawca}", 6, win.Bottom + 1.5, W - 12, top + h - win.Bottom - 3.5, 8, color: Gray,
                            align: TextAlign.Center, valign: TextVAlign.Middle, spacing: 0.25, upper: true, autoFit: true));
                        break;
                    }
            }
        }
    }

    private static void Mixtape(CardDocument d)
    {
        d.Background = "#FFF7F1E1";
        double W = d.Width, b = d.Bleed;
        const string Pen = "#FF1F3A93", Line = "#FF9DB7D5", Margin = "#FFE85D75", Hand = "Ink Free";
        var els = d.Elements;
        foreach (var (p, top, li) in Panels(d))
        {
            double h = p.Height;
            switch (p.Role)
            {
                case PanelRole.Front:
                    {
                        int lines = Math.Max(2, (int)((h - 16) / 6.5));
                        double hh = (lines - 1) * 6.5 + 0.35;
                        els.Add(Stripes("Linie", -b, top + 14, W + 2 * b, hh, Enumerable.Repeat(Line, lines), gap: 6.5 - 0.35));
                        els.Add(Shape("Margines", ShapeKind.Rectangle, 12, top, 0.4, h, Margin));
                        els.Add(Text("Tytuł", "{tytuł}", 16, top + 3, W - 20, 11, 22, color: Pen, font: Hand, bold: true, autoFit: true));
                        els.Add(Text("Dla kogo", "{dopisek}", 16, top + 15.5, W - 22, 6, 13, color: Pen, font: Hand));
                        els.Add(Text("Podpis", "lato {rok}", W - 40, top + h - 9, 34, 7, 12, color: Pen, font: Hand, align: TextAlign.Right));
                        break;
                    }
                case PanelRole.Inner:
                    els.Add(Shape("Margines", ShapeKind.Rectangle, 12, top, 0.4, h, Margin));
                    els.Add(Shape("Linia", ShapeKind.Rectangle, -b, top + 6, W + 2 * b, 0.35, Line));
                    els.Add(Text("Strona A", TracksA, 15, top + 9, W / 2 - 15, h - 13, 11, color: Pen, font: Hand, autoFit: true));
                    els.Add(Text("Strona B", TracksB, W / 2 + 2, top + 9, W / 2 - 6, h - 13, 11, color: Pen, font: Hand, autoFit: true));
                    break;
                case PanelRole.Spine:
                    els.Add(Text("Grzbiet", "{tytuł} ♥ {rok}", 6, top + 0.5, W - 12, h - 1, 14, color: Pen, font: Hand,
                        bold: true, align: TextAlign.Center, valign: TextVAlign.Middle, autoFit: true));
                    break;
                case PanelRole.Flap:
                    els.Add(Text("Klapka", "nagrane z radia i płyt", 6, top + 1, W - 12, h - 2, 11, color: Pen, font: Hand,
                        align: TextAlign.Center, valign: TextVAlign.Middle));
                    break;
                case PanelRole.Label:
                    {
                        // linie biegną też przez okienko (i tak zostanie wycięte); tytuł nad nim, wykonawca na linii pod nim
                        var win = Window(d, top, h);
                        double first = win.Y - 1.2, last = top + h - 3.5;
                        int lines = Math.Max(2, (int)((last - first) / 6.4) + 1);
                        double step = (last - first) / (lines - 1);
                        els.Add(Stripes("Linie", -b, first, W + 2 * b, last - first + 0.35, Enumerable.Repeat(Line, lines), gap: step - 0.35));
                        els.Add(Text("Strona", SideLetter(li), 3, top + 1.5, 11, first - top - 1.8, 24, color: Margin, font: Hand, bold: true,
                            align: TextAlign.Center, valign: TextVAlign.Bottom, autoFit: true));
                        els.Add(Text("Tytuł", "{tytuł}", 16, top + 2, W - 20, first - top - 2.3, 18, color: Pen, font: Hand, bold: true,
                            valign: TextVAlign.Bottom, autoFit: true));
                        els.Add(Text("Wykonawca", "{wykonawca}", 16, last - 7.2, W - 22, 7, 12, color: Pen, font: Hand,
                            valign: TextVAlign.Bottom, autoFit: true));
                        break;
                    }
            }
        }
    }
}
