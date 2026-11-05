namespace Kasetownik9000.Model;

/// <summary>Style startowe dodane w wersji 1.6: synthwave, wideo, chrom, punk, kraft, szwajcarski.</summary>
public static partial class Starters
{
    /// <summary>Elementy tła blokujemy, żeby nie przeszkadzały przy klikaniu w napisy (da się je odblokować w warstwach).</summary>
    private static T Locked<T>(T e) where T : CardElement
    {
        e.Locked = true;
        return e;
    }

    // ================================================================== Synthwave

    private static void Synthwave(CardDocument d)
    {
        const string Sky = "#FF1B0B33", Floor = "#FF0E0420", Pink = "#FFFF2E9A", Cyan = "#FF00E5FF", Sun = "#FFFF7A45", Grid = "#FFB13CFF";
        d.Background = Sky;
        double W = d.Width, b = d.Bleed;
        var els = d.Elements;
        foreach (var (p, top, li) in Panels(d))
        {
            double h = p.Height;
            switch (p.Role)
            {
                case PanelRole.Front:
                    {
                        // słońce zaczyna się dopiero pod napisami, żeby różowy tytuł nie leżał na pomarańczowym tle
                        double horizon = top + h * 0.66, sun = h * 0.42;
                        SynthSun(els, W / 2 - sun / 2, horizon - sun * 0.82, sun, Sky, Sun);
                        SynthFloor(els, d, top, h, horizon, W / 2, Floor, Grid, Pink);
                        els.Add(Text("Tytuł", "{tytuł}", 5, top + 3, W - 10, 11, 26, color: Pink, bold: true, italic: true,
                            align: TextAlign.Center, valign: TextVAlign.Middle, spacing: 0.05, upper: true, autoFit: true, shadow: Cyan, shadowOffset: 0.6));
                        els.Add(Text("Wykonawca", "{wykonawca}", 6, top + 14, W - 12, 5, 9, color: Cyan,
                            align: TextAlign.Center, spacing: 0.35, upper: true, autoFit: true));
                        break;
                    }
                case PanelRole.Spine:
                    {
                        double sun = h * 0.78;
                        SynthSun(els, W - 6 - sun, top + (h - sun) / 2, sun, Sky, Sun);
                        els.Add(Text("Grzbiet", "{wykonawca} · {tytuł}", 4, top + 1, W - 14 - sun, h - 2, 13, color: Pink, bold: true, italic: true,
                            valign: TextVAlign.Middle, spacing: 0.05, upper: true, autoFit: true, shadow: Cyan, shadowOffset: 0.4));
                        break;
                    }
                case PanelRole.Flap:
                    els.Add(Segment("Linia", -b, top + 1.4, W + b, top + 1.4, Pink, 0.4));
                    els.Add(Text("Klapka", "{kaseta} · {rok} · stereo", 6, top + 2, W - 12, h - 3, 8, color: Cyan,
                        align: TextAlign.Center, valign: TextVAlign.Middle, spacing: 0.35, upper: true));
                    break;
                case PanelRole.Inner:
                    {
                        double horizon = top + h * 0.8;
                        SynthFloor(els, d, top, h, horizon, W / 2, Floor, Grid, Pink);
                        els.Add(Text("Strona A", TracksA, 6, top + 6, W / 2 - 8, horizon - top - 9, 9, color: Cyan, autoFit: true));
                        els.Add(Text("Strona B", TracksB, W / 2 + 2, top + 6, W / 2 - 8, horizon - top - 9, 9, color: Cyan, autoFit: true));
                        break;
                    }
                case PanelRole.Label:
                    {
                        // horyzont tuż pod okienkiem, słońce wschodzi z prawej strony okienka
                        var win = Window(d, top, h);
                        double horizon = win.Bottom + 0.6, sun = Math.Min(12, W - win.Right - 2.5);
                        double sunX = win.Right + (W - win.Right - sun) / 2;
                        SynthSun(els, sunX, horizon - sun * 0.82, sun, Sky, Sun);
                        SynthFloor(els, d, top, h, horizon, sunX + sun / 2, Floor, Grid, Pink);
                        els.Add(Text("Strona", SideLetter(li), 2, top + 1.5, 12, win.Y - top - 2, 24, color: Pink, bold: true, italic: true,
                            align: TextAlign.Center, valign: TextVAlign.Middle, autoFit: true, shadow: Cyan, shadowOffset: 0.5));
                        els.Add(Text("Tytuł", "{tytuł}", 15, top + 2, W - 19, win.Y - top - 3, 14, color: Pink, bold: true, italic: true,
                            valign: TextVAlign.Middle, upper: true, autoFit: true, shadow: Cyan, shadowOffset: 0.4));
                        els.Add(Text("Długość", "{kaseta}", 1.5, win.Y, win.X - 3, win.H, 8, color: Cyan, italic: true,
                            align: TextAlign.Center, valign: TextVAlign.Middle, spacing: 0.1, autoFit: true));
                        break;
                    }
            }
        }
    }

    /// <summary>Słońce z poziomymi „cięciami” w kolorze nieba na dolnej połowie.</summary>
    private static void SynthSun(List<CardElement> els, double x, double y, double size, string sky, string sun)
    {
        els.Add(Shape("Słońce", ShapeKind.Ellipse, x, y, size, size, sun));
        double cy = y + size * 0.5, ch = size * 0.5;
        els.Add(Stripes("Cięcia słońca", x - 0.5, cy, size + 1, ch, Enumerable.Repeat(sky, 5), gap: ch * 0.12));
    }

    /// <summary>Ciemna podłoga z neonową siatką zbiegającą się na horyzoncie.</summary>
    private static void SynthFloor(List<CardElement> els, CardDocument d, double top, double h, double horizon, double vanishX,
        string floor, string grid, string glow)
    {
        double W = d.Width, b = Math.Max(0, d.Bleed);
        double bottom = top + h + (top + h >= d.Height - 0.001 ? b : 0);
        els.Add(Locked(Shape("Podłoga", ShapeKind.Rectangle, -b, horizon, W + 2 * b, bottom - horizon, floor)));
        foreach (var dy in new[] { 1.0, 2.6, 5.0, 8.6, 13.6, 20.5 })
            if (horizon + dy < bottom) els.Add(Locked(Segment("Siatka", -b, horizon + dy, W + b, horizon + dy, grid, 0.25)));
        // linie kończą się dokładnie na dolnej krawędzi panelu (nie mogą wejść na grzbiet ani sąsiednią naklejkę)
        for (int k = -6; k <= 6; k++)
            els.Add(Locked(Segment("Siatka", vanishX, horizon, vanishX + k * 11.5, bottom, grid, 0.25)));
        els.Add(Locked(Segment("Horyzont", -b, horizon, W + b, horizon, glow, 0.5)));
    }

    // ================================================================== Wideo 80s

    private static void Video(CardDocument d)
    {
        const string White = "#FFF5F5F5", Gray = "#FFB0B0B0";
        string[] rainbow = ["#FFE53935", "#FFFB8C00", "#FFFDD835", "#FF43A047", "#FF1E88E5"];
        d.Background = "#FF121212";
        double W = d.Width, b = d.Bleed;
        var els = d.Elements;
        foreach (var (p, top, li) in Panels(d))
        {
            double h = p.Height;
            switch (p.Role)
            {
                case PanelRole.Front:
                    els.Add(Stripes("Pasy", -5, top + h * 0.52 - h * 0.14, W + 10, h * 0.28, rainbow, gap: 0.9, rotation: -20));
                    els.Add(Text("Tytuł", "{tytuł}", 6, top + 5, W * 0.68, 15, 28, color: White, font: "Impact",
                        italic: true, upper: true, autoFit: true, spacing: 0.02));
                    els.Add(Text("Wykonawca", "{wykonawca}", 6, top + 20, W * 0.6, 6, 9, color: Gray, spacing: 0.3, upper: true, autoFit: true));
                    els.Add(Text("Opis", "HIGH GRADE · LONG PLAY", 6, top + h - 8.5, 60, 5, 7.5, color: White, bold: true, spacing: 0.3));
                    els.Add(Text("Długość", "{kaseta}", W - 38, top + h - 13, 32, 10, 22, color: White, font: "Impact",
                        italic: true, align: TextAlign.Right));
                    break;
                case PanelRole.Spine:
                    {
                        var (y, hh) = WithBleed(d, top, h);
                        els.Add(Stripes("Pasy grzbietu", -b, y, 18 + b, hh, rainbow, vertical: true));
                        els.Add(Text("Grzbiet", "{wykonawca} — {tytuł}", 22, top + 0.8, W - 27, h - 1.6, 15, color: White, font: "Impact",
                            italic: true, valign: TextVAlign.Middle, upper: true, autoFit: true, spacing: 0.02));
                        break;
                    }
                case PanelRole.Flap:
                    els.Add(Text("Klapka", "high grade · long play · {rok}", 6, top + 1, W - 12, h - 2, 8, color: Gray,
                        align: TextAlign.Center, valign: TextVAlign.Middle, spacing: 0.3, upper: true));
                    break;
                case PanelRole.Inner:
                    els.Add(Stripes("Pasy wkładki", -b, top + 3, W + 2 * b, 3.2, rainbow));
                    els.Add(Text("Strona A", TracksA, 6, top + 9, W / 2 - 8, h - 13, 9, color: White, font: "Consolas", autoFit: true));
                    els.Add(Text("Strona B", TracksB, W / 2 + 2, top + 9, W / 2 - 8, h - 13, 9, color: White, font: "Consolas", autoFit: true));
                    break;
                case PanelRole.Label:
                    {
                        // tęczowe pasy wychodzą zza okienka w prawy górny róg
                        var win = Window(d, top, h);
                        els.Add(Stripes("Pasy", win.Right - 9, win.CenterY - 5, 32, 10, rainbow, gap: 0.5, rotation: -20));
                        els.Add(Text("Tytuł", "{tytuł}", 5, top + 2, W * 0.66, win.Y - top - 3, 14, color: White, font: "Impact", italic: true,
                            valign: TextVAlign.Middle, upper: true, autoFit: true));
                        els.Add(Text("Strona", SideLetter(li), 1.5, win.Y, win.X - 3, win.H, 28, color: White, font: "Impact", italic: true,
                            align: TextAlign.Center, valign: TextVAlign.Middle, autoFit: true));
                        double by = win.Bottom + 1.5, bh = top + h - by - 2;
                        els.Add(Text("Długość", "{kaseta}", 5, by, 30, bh, 11, color: Gray, font: "Impact", italic: true,
                            valign: TextVAlign.Middle, autoFit: true));
                        els.Add(Text("Opis", "HIGH GRADE", W - 45, by, 40, bh, 7.5, color: White, bold: true, spacing: 0.3,
                            align: TextAlign.Right, valign: TextVAlign.Middle));
                        break;
                    }
            }
        }
    }

    // ================================================================== Chrom (Type II)

    private static void Chrome(CardDocument d)
    {
        const string Steel = "#FF23272D", Silver = "#FFE8EBEF", Red = "#FFD32F2F", Muted = "#FF59616B";
        string[] metal = ["#FFF7F8FA", "#FFE6E9ED", "#FFD3D8DE", "#FFBFC5CC", "#FFAEB5BE", "#FFC4CAD1", "#FFDCE0E5", "#FFF0F2F4", "#FFD0D5DB", "#FFB9C0C8"];
        d.Background = "#FFDADEE3";
        double W = d.Width, b = d.Bleed;
        var els = d.Elements;
        foreach (var (p, top, li) in Panels(d))
        {
            double h = p.Height;
            var (fy, fh) = WithBleed(d, top, h);
            switch (p.Role)
            {
                case PanelRole.Front:
                    {
                        els.Add(Locked(Stripes("Metal", -b, fy, W + 2 * b, fh, metal)));
                        double bandY = top + h * 0.55, bandH = h * 0.24;
                        els.Add(Shape("Pas", ShapeKind.Rectangle, -b, bandY, W + 2 * b, bandH, Steel));
                        els.Add(Shape("Linia", ShapeKind.Rectangle, -b, bandY + bandH, W + 2 * b, 0.9, Red));
                        els.Add(Text("Tytuł", "{tytuł}", 6, bandY + 1, W - 12, bandH - 2, 20, color: Silver, bold: true,
                            align: TextAlign.Center, valign: TextVAlign.Middle, spacing: 0.14, upper: true, autoFit: true));
                        els.Add(Text("Wykonawca", "{wykonawca}", 6, top + 6, W - 12, 8, 13, color: Steel, bold: true, spacing: 0.25, upper: true, autoFit: true));
                        els.Add(Text("Opis", "chrome · type II · high bias", 6, top + 15, W - 12, 5, 7, color: Muted, spacing: 0.35, upper: true));
                        els.Add(Text("EQ", "70 µs EQ", 6, top + h - 8.5, 30, 5, 7, color: Muted, spacing: 0.2));
                        els.Add(Text("Długość", "{kaseta}", W - 32, top + h - 10, 26, 7, 13, color: Steel, bold: true, align: TextAlign.Right));
                        break;
                    }
                case PanelRole.Spine:
                    els.Add(Shape("Tło grzbietu", ShapeKind.Rectangle, -b, fy, W + 2 * b, fh, Steel));
                    els.Add(Text("Grzbiet", "{wykonawca} · {tytuł}", 6, top + 1, W - 22, h - 2, 11, color: Silver, bold: true,
                        valign: TextVAlign.Middle, spacing: 0.15, upper: true, autoFit: true));
                    els.Add(Shape("Znak", ShapeKind.Rectangle, W - 12, top + h / 2 - 2, 4, 4, Red));
                    break;
                case PanelRole.Flap:
                    els.Add(Locked(Stripes("Metal", -b, fy, W + 2 * b, fh, metal.Take(5))));
                    els.Add(Text("Klapka", "type II · chrome · {data}", 6, top + 1, W - 12, h - 2, 7.5, color: Steel,
                        align: TextAlign.Center, valign: TextVAlign.Middle, spacing: 0.3, upper: true));
                    break;
                case PanelRole.Inner:
                    els.Add(Locked(Stripes("Metal", -b, fy, W + 2 * b, fh, metal)));
                    els.Add(Shape("Pas", ShapeKind.Rectangle, -b, top + 3, W + 2 * b, 3, Steel));
                    els.Add(Text("Strona A", TracksA, 6, top + 10, W / 2 - 8, h - 14, 9, color: Steel, autoFit: true));
                    els.Add(Text("Strona B", TracksB, W / 2 + 2, top + 10, W / 2 - 8, h - 14, 9, color: Steel, autoFit: true));
                    break;
                case PanelRole.Label:
                    {
                        // wykonawca nad okienkiem, grafitowy pas z tytułem pod nim
                        var win = Window(d, top, h);
                        els.Add(Locked(Stripes("Metal", -b, fy, W + 2 * b, fh, metal)));
                        double bandY = win.Bottom + 1.2, bandH = top + h - 2 - bandY;
                        els.Add(Shape("Pas", ShapeKind.Rectangle, -b, bandY, W + 2 * b, bandH, Steel));
                        els.Add(Text("Strona", SideLetter(li), 4, bandY, 10, bandH, 18, color: Red, bold: true,
                            align: TextAlign.Center, valign: TextVAlign.Middle, autoFit: true));
                        els.Add(Text("Tytuł", "{tytuł}", 16, bandY + 0.5, W - 20, bandH - 1, 13, color: Silver, bold: true,
                            valign: TextVAlign.Middle, spacing: 0.12, upper: true, autoFit: true));
                        els.Add(Text("Wykonawca", "{wykonawca}", 6, top + 3, W - 12, win.Y - top - 5.5, 10, color: Steel, bold: true,
                            valign: TextVAlign.Middle, spacing: 0.25, upper: true, autoFit: true));
                        els.Add(Shape("Linia", ShapeKind.Rectangle, 6, win.Y - 2, W - 12, 0.6, Red));
                        break;
                    }
            }
        }
    }

    // ================================================================== Punk ksero

    private static void Punk(CardDocument d)
    {
        const string Black = "#FF111111", Red = "#FFD7261E", Tape = "#FFCFC6B4", OffWhite = "#FFF7F7F7", Mono = "Courier New";
        d.Background = "#FFF0EEE8";
        double W = d.Width, b = d.Bleed;
        var els = d.Elements;
        foreach (var (p, top, li) in Panels(d))
        {
            double h = p.Height;
            switch (p.Role)
            {
                case PanelRole.Front:
                    els.Add(Shape("Taśma", ShapeKind.Rectangle, -3, top + 4, 20, 6, Tape, rotation: -32, opacity: 0.85));
                    els.Add(Shape("Taśma", ShapeKind.Rectangle, W - 16, top + 4, 20, 6, Tape, rotation: 30, opacity: 0.85));
                    els.Add(Shape("Tło tytułu", ShapeKind.Rectangle, 5, top + 7, W - 22, 17, Black, rotation: -2.5));
                    els.Add(Text("Tytuł", "{tytuł}", 7, top + 7.5, W - 26, 16, 28, color: OffWhite, font: "Impact",
                        align: TextAlign.Center, valign: TextVAlign.Middle, upper: true, autoFit: true, rotation: -2.5));
                    els.Add(Shape("Kartka", ShapeKind.Rectangle, W * 0.3, top + 27, W * 0.62, 10, "#FFFFFFFF", stroke: Black, strokeWidth: 0.4, rotation: 2.2));
                    els.Add(Text("Wykonawca", "{wykonawca}", W * 0.3 + 1, top + 27.5, W * 0.62 - 2, 9, 13, color: Black, bold: true, font: Mono,
                        align: TextAlign.Center, valign: TextVAlign.Middle, upper: true, autoFit: true, rotation: 2.2));
                    els.Add(Segment("Kreska", 8, top + 42, W * 0.5, top + 40.5, Black, 1.1));
                    els.Add(Shape("Pieczątka", ShapeKind.Ellipse, W - 32, top + h - 28, 23, 23, Red, hasFill: false, stroke: Red, strokeWidth: 0.9, dashed: true));
                    els.Add(Text("Pieczątka", "DIY\n{rok}", W - 32, top + h - 28, 23, 23, 12, color: Red, font: "Impact",
                        align: TextAlign.Center, valign: TextVAlign.Middle, rotation: -14, lineSpacing: 0.9));
                    els.Add(Text("Opis", "nagrane w garażu · {kaseta}", 6, top + h - 9, 62, 6, 8, color: Black, font: Mono));
                    break;
                case PanelRole.Spine:
                    {
                        var (y, hh) = WithBleed(d, top, h);
                        els.Add(Shape("Tło grzbietu", ShapeKind.Rectangle, -b, y, W + 2 * b, hh, Black));
                        els.Add(Shape("Pasek", ShapeKind.Rectangle, W - 14, top + 1.5, 7, h - 3, Red));
                        els.Add(Text("Grzbiet", "{wykonawca} — {tytuł}", 5, top + 0.8, W - 24, h - 1.6, 15, color: OffWhite, font: "Impact",
                            valign: TextVAlign.Middle, upper: true, autoFit: true, spacing: 0.03));
                        break;
                    }
                case PanelRole.Flap:
                    els.Add(Segment("Cięcie", 4, top + 1.5, W - 4, top + 1.5, Black, 0.3, dashed: true));
                    els.Add(Text("Klapka", "kopiuj · nagrywaj · rozdawaj · {data}", 6, top + 2, W - 12, h - 3, 8, color: Black, font: Mono,
                        align: TextAlign.Center, valign: TextVAlign.Middle));
                    break;
                case PanelRole.Inner:
                    els.Add(Shape("Tło nagłówka", ShapeKind.Rectangle, -b, top + 3, W + 2 * b, 5.5, Black));
                    els.Add(Text("Nagłówek", "LISTA UTWORÓW", 6, top + 3, W - 12, 5.5, 9, color: OffWhite, font: "Impact",
                        valign: TextVAlign.Middle, spacing: 0.2));
                    els.Add(Text("Strona A", TracksA, 6, top + 11, W / 2 - 8, h - 15, 9, color: Black, bold: true, font: Mono, autoFit: true));
                    els.Add(Text("Strona B", TracksB, W / 2 + 2, top + 11, W / 2 - 8, h - 15, 9, color: Black, bold: true, font: Mono, autoFit: true));
                    break;
                case PanelRole.Label:
                    {
                        var win = Window(d, top, h);
                        double s = win.X - 4; // kwadrat z literą w lewym pasie obok okienka
                        els.Add(Shape("Tło litery", ShapeKind.Rectangle, 2, win.CenterY - s / 2, s, s, Black, rotation: -4));
                        els.Add(Text("Strona", SideLetter(li), 2, win.CenterY - s / 2, s, s, 22, color: OffWhite, font: "Impact",
                            align: TextAlign.Center, valign: TextVAlign.Middle, autoFit: true, rotation: -4));
                        els.Add(Text("Tytuł", "{tytuł}", 5, top + 2, W - 10, win.Y - top - 3, 14, color: Black, bold: true, font: Mono,
                            valign: TextVAlign.Middle, upper: true, autoFit: true));
                        double by = win.Bottom + 1.2;
                        els.Add(Text("Wykonawca", "{wykonawca}", 6, by, W * 0.6, top + h - by - 3.5, 9, color: Black, font: Mono,
                            valign: TextVAlign.Middle, upper: true, autoFit: true));
                        els.Add(Segment("Kreska", 6, top + h - 2.6, W * 0.6, top + h - 3.4, Black, 0.8));
                        els.Add(Shape("Taśma", ShapeKind.Rectangle, W - 24, by + 1.5, 19, 5.5, Tape, rotation: -12, opacity: 0.85));
                        break;
                    }
            }
        }
    }

    // ================================================================== Kraft

    private static void Kraft(CardDocument d)
    {
        const string Paper = "#FFC9A57A", InkBrown = "#FF3A2716", Red = "#FF8E2C24", Tag = "#FFF5ECDA", Twine = "#FFEFE0BF";
        const string Serif = "Georgia", Hand = "Segoe Print", Mono = "Courier New";
        d.Background = Paper;
        double W = d.Width;
        var els = d.Elements;
        foreach (var (p, top, li) in Panels(d))
        {
            double h = p.Height;
            switch (p.Role)
            {
                case PanelRole.Front:
                    {
                        double tagY = top + h * 0.2, tagH = h * 0.5;
                        els.Add(Segment("Sznurek", 14.5, tagY + tagH / 2, -4, top + 2, Twine, 0.7));
                        els.Add(Shape("Zawieszka", ShapeKind.Rectangle, 10, tagY, W - 20, tagH, Tag, stroke: InkBrown, strokeWidth: 0.35, cornerRadius: 2));
                        els.Add(Shape("Ramka", ShapeKind.Rectangle, 12.5, tagY + 2.5, W - 25, tagH - 5, Tag, hasFill: false, stroke: InkBrown,
                            strokeWidth: 0.25, dashed: true, cornerRadius: 1));
                        els.Add(Shape("Dziurka", ShapeKind.Ellipse, 13, tagY + tagH / 2 - 1.5, 3, 3, Paper, stroke: InkBrown, strokeWidth: 0.3));
                        els.Add(Text("Tytuł", "{tytuł}", 18, tagY + 3.5, W - 34, tagH * 0.5, 20, color: InkBrown, bold: true, font: Serif,
                            align: TextAlign.Center, valign: TextVAlign.Middle, autoFit: true));
                        els.Add(Text("Wykonawca", "{wykonawca}", 18, tagY + tagH * 0.58, W - 34, tagH * 0.32, 12, color: InkBrown, font: Hand,
                            align: TextAlign.Center, valign: TextVAlign.Middle, autoFit: true));
                        els.Add(Text("Opis", "{kaseta} · nagranie domowe", 6, top + 3, W - 12, 5, 7.5, color: InkBrown, bold: true, font: Mono,
                            align: TextAlign.Center, spacing: 0.15, upper: true));
                        els.Add(Shape("Pieczątka", ShapeKind.Ellipse, W - 27, top + h - 22, 19, 19, Red, hasFill: false, stroke: Red, strokeWidth: 0.8));
                        els.Add(Text("Pieczątka", "{rok}", W - 27, top + h - 22, 19, 19, 10, color: Red, bold: true, font: Mono,
                            align: TextAlign.Center, valign: TextVAlign.Middle, rotation: -12));
                        break;
                    }
                case PanelRole.Spine:
                    els.Add(Segment("Linia", 5, top + 1.2, W - 5, top + 1.2, InkBrown, 0.25));
                    els.Add(Segment("Linia", 5, top + h - 1.2, W - 5, top + h - 1.2, InkBrown, 0.25));
                    els.Add(Text("Grzbiet", "{wykonawca} · {tytuł}", 6, top + 1.5, W - 12, h - 3, 11, color: InkBrown, bold: true, font: Mono,
                        align: TextAlign.Center, valign: TextVAlign.Middle, spacing: 0.08, upper: true, autoFit: true));
                    break;
                case PanelRole.Flap:
                    els.Add(Text("Klapka", "nagrano {data} · {kaseta}", 6, top + 1, W - 12, h - 2, 9.5, color: InkBrown, font: Hand,
                        align: TextAlign.Center, valign: TextVAlign.Middle));
                    break;
                case PanelRole.Inner:
                    els.Add(Shape("Kartka", ShapeKind.Rectangle, 6, top + 5, W - 12, h - 10, Tag, stroke: InkBrown, strokeWidth: 0.25, rotation: -1));
                    els.Add(Text("Strona A", TracksA, 10, top + 9, W / 2 - 12, h - 18, 9, color: InkBrown, font: Mono, autoFit: true, rotation: -1));
                    els.Add(Text("Strona B", TracksB, W / 2 + 1, top + 9, W / 2 - 11, h - 18, 9, color: InkBrown, font: Mono, autoFit: true, rotation: -1));
                    break;
                case PanelRole.Label:
                    {
                        // tytuł nad okienkiem, wykonawca pod nim, litera strony i rok po bokach — nic nie wpada w otwór
                        var win = Window(d, top, h);
                        els.Add(Shape("Zawieszka", ShapeKind.Rectangle, 2.5, top + 2.5, W - 5, h - 5, Tag, stroke: InkBrown, strokeWidth: 0.35, cornerRadius: 2));
                        els.Add(Shape("Ramka", ShapeKind.Rectangle, 4.2, top + 4.2, W - 8.4, h - 8.4, Tag, hasFill: false, stroke: InkBrown,
                            strokeWidth: 0.25, dashed: true, cornerRadius: 1));
                        els.Add(Text("Tytuł", "{tytuł}", 7, top + 4.8, W - 14, win.Y - top - 5.6, 15, color: InkBrown, bold: true, font: Serif,
                            align: TextAlign.Center, valign: TextVAlign.Middle, autoFit: true));
                        els.Add(Text("Strona", SideLetter(li), 4.6, win.Y, win.X - 5.8, win.H, 24, color: Red, bold: true, font: Serif,
                            align: TextAlign.Center, valign: TextVAlign.Middle, autoFit: true));
                        // pionowy napis: ramka (długość okienka × szerokość pasa) obrócona o -90° wokół środka pasa
                        double zw = W - win.Right - 5.8, zx = win.Right + 1.2 + zw / 2;
                        els.Add(Text("Rok", "{rok}", zx - win.H / 2, win.CenterY - zw / 2, win.H, zw, 9, color: Red, bold: true, font: Mono,
                            align: TextAlign.Center, valign: TextVAlign.Middle, spacing: 0.15, autoFit: true, rotation: -90));
                        els.Add(Text("Wykonawca", "{wykonawca}", 7, win.Bottom + 0.6, W - 14, top + h - win.Bottom - 5.4, 10, color: InkBrown, font: Hand,
                            align: TextAlign.Center, valign: TextVAlign.Middle, autoFit: true));
                        break;
                    }
            }
        }
    }

    // ================================================================== Szwajcarski

    private static void Swiss(CardDocument d)
    {
        const string Red = "#FFE30613", Black = "#FF111111", Gray = "#FF6B6B6B", White = "#FFFFFFFF", Sans = "Arial";
        d.Background = White;
        double W = d.Width, b = d.Bleed;
        double col = W * 0.32;
        var els = d.Elements;
        foreach (var (p, top, li) in Panels(d))
        {
            double h = p.Height;
            var (fy, fh) = WithBleed(d, top, h);
            switch (p.Role)
            {
                case PanelRole.Front:
                    {
                        els.Add(Shape("Blok", ShapeKind.Rectangle, -b, fy, col + b, fh, Red));
                        // napis pionowy w bloku: pudełko (wysokość panelu × szerokość bloku) obrócone o -90°
                        double bw = h - 10, bh = col - 8;
                        els.Add(Text("Typ", "{kaseta}", col / 2 - bw / 2, top + h / 2 - bh / 2, bw, bh, 30, color: White, bold: true, font: Sans,
                            valign: TextVAlign.Bottom, autoFit: true, rotation: -90));
                        els.Add(Text("Tytuł", "{tytuł}", col + 5, top + 5, W - col - 10, h * 0.48, 24, color: Black, bold: true, font: Sans,
                            autoFit: true, lineSpacing: 0.95));
                        els.Add(Shape("Linia", ShapeKind.Rectangle, col + 5, top + h * 0.6, W - col - 10, 0.5, Black));
                        els.Add(Text("Wykonawca", "{wykonawca}", col + 5, top + h * 0.6 + 2, W - col - 10, 8, 11, color: Black, font: Sans, autoFit: true));
                        els.Add(Text("Data", "{data}", col + 5, top + h - 8.5, 30, 5, 7, color: Gray, font: Sans));
                        els.Add(Text("Stereo", "stereo", W - 30, top + h - 8.5, 24, 5, 7, color: Gray, font: Sans, align: TextAlign.Right));
                        break;
                    }
                case PanelRole.Spine:
                    els.Add(Shape("Blok", ShapeKind.Rectangle, -b, fy, col + b, fh, Red));
                    els.Add(Text("Typ", "{kaseta}", 4, top + 1, col - 8, h - 2, 11, color: White, bold: true, font: Sans,
                        valign: TextVAlign.Middle, autoFit: true));
                    els.Add(Text("Grzbiet", "{wykonawca} — {tytuł}", col + 5, top + 1, W - col - 10, h - 2, 11, color: Black, bold: true, font: Sans,
                        valign: TextVAlign.Middle, autoFit: true));
                    break;
                case PanelRole.Flap:
                    els.Add(Shape("Blok", ShapeKind.Rectangle, -b, fy, col + b, fh, Red));
                    els.Add(Shape("Linia", ShapeKind.Rectangle, col + 5, top + 2, W - col - 10, 0.4, Black));
                    els.Add(Text("Klapka", "nagrano {data}", col + 5, top + 3.5, W - col - 10, h - 5, 8, color: Black, font: Sans,
                        valign: TextVAlign.Middle));
                    break;
                case PanelRole.Inner:
                    {
                        els.Add(Shape("Blok", ShapeKind.Rectangle, -b, fy, col + b, fh, Red));
                        double bw = h - 10, bh = col - 8;
                        els.Add(Text("Lista", "lista utworów", col / 2 - bw / 2, top + h / 2 - bh / 2, bw, bh, 16, color: White, bold: true, font: Sans,
                            valign: TextVAlign.Bottom, autoFit: true, rotation: -90));
                        double listH = (h - 12) / 2;
                        els.Add(Text("Strona A", TracksA, col + 5, top + 5, W - col - 10, listH, 8.5, color: Black, font: Sans, autoFit: true));
                        els.Add(Text("Strona B", TracksB, col + 5, top + 7 + listH, W - col - 10, listH, 8.5, color: Black, font: Sans, autoFit: true));
                        break;
                    }
                case PanelRole.Label:
                    {
                        // czerwony blok kończy się przed okienkiem; tytuł nad okienkiem, wykonawca pod linią
                        var win = Window(d, top, h);
                        double lc = Math.Min(W * 0.22, win.X - 1.5);
                        els.Add(Shape("Blok", ShapeKind.Rectangle, -b, fy, lc + b, fh, Red));
                        els.Add(Text("Strona", SideLetter(li), 1.5, top + 2, lc - 3, h - 4, 40, color: White, bold: true, font: Sans,
                            align: TextAlign.Center, valign: TextVAlign.Middle, autoFit: true));
                        els.Add(Text("Tytuł", "{tytuł}", lc + 3.5, top + 2.5, W - lc - 7, win.Y - top - 3.5, 16, color: Black, bold: true, font: Sans,
                            valign: TextVAlign.Bottom, autoFit: true));
                        els.Add(Shape("Linia", ShapeKind.Rectangle, lc + 3.5, win.Bottom + 1.5, W - lc - 7, 0.4, Black));
                        els.Add(Text("Wykonawca", "{wykonawca}", lc + 3.5, win.Bottom + 2.6, W - lc - 7, top + h - win.Bottom - 4.6, 10, color: Black,
                            font: Sans, autoFit: true));
                        break;
                    }
            }
        }
    }
}
