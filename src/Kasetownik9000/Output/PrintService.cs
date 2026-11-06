using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Kasetownik9000.Editor;
using Kasetownik9000.Model;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Text;
using Windows.UI;
using static Kasetownik9000.Output.Native;

namespace Kasetownik9000.Output;

/// <summary>
/// Drukowanie przez okno "Drukuj" Windows i GDI. Karty są rasteryzowane tym samym
/// rendererem co podgląd i umieszczane w skali 1:1 (z uwzględnieniem marginesów fizycznych
/// drukarki i — opcjonalnie — kalibracji użytkownika).
/// </summary>
public static class PrintService
{
    public sealed record Result(bool Printed, string? Error, string? Info = null);

    /// <summary>Otwarty kontekst drukarki z oknem "Drukuj".</summary>
    private sealed class PrinterDc : IDisposable
    {
        public nint Hdc;
        public nint DevMode, DevNames;
        public int Copies = 1;

        public void Dispose()
        {
            if (Hdc != 0) DeleteDC(Hdc);
            if (DevMode != 0) GlobalFree(DevMode);
            if (DevNames != 0) GlobalFree(DevNames);
        }
    }

    private static PrinterDc? ShowPrintDialog(nint hwnd, out string? error)
    {
        error = null;
        var pd = new PRINTDLGW
        {
            lStructSize = (uint)Unsafe.SizeOf<PRINTDLGW>(),
            hwndOwner = hwnd,
            Flags = PD_RETURNDC | PD_NOPAGENUMS | PD_NOSELECTION | PD_USEDEVMODECOPIESANDCOLLATE | PD_HIDEPRINTTOFILE,
            nCopies = 1,
            nFromPage = 1, nToPage = 1, nMinPage = 1, nMaxPage = 1,
        };
        if (!PrintDlgW(ref pd))
        {
            uint err = CommDlgExtendedError();
            if (err != 0) error = $"Nie udało się otworzyć okna drukowania (kod 0x{err:X}).";
            return null;
        }
        var dc = new PrinterDc { Hdc = pd.hDC, DevMode = pd.hDevMode, DevNames = pd.hDevNames, Copies = Math.Max(1, (int)pd.nCopies) };
        if (dc.Hdc == 0)
        {
            dc.Dispose();
            error = "Drukarka nie zwróciła kontekstu urządzenia.";
            return null;
        }
        return dc;
    }

    /// <summary>
    /// Ręczny druk dwustronny: komunikat dla użytkownika („przełóż kartki…”) i odpowiedź — true = drukuj tyły.
    /// </summary>
    public delegate Task<bool> FlipPrompt(string message);

    /// <summary>Pokazuje okno wyboru drukarki i drukuje.</summary>
    public static async Task<Result> PrintAsync(nint hwnd, EditorSession session, PrintOptions opt, FlipPrompt? flip)
    {
        using var dc = ShowPrintDialog(hwnd, out var error);
        if (dc is null) return new Result(false, error);
        return await PrintToDcAsync(dc, session, opt, flip, null);
    }

    /// <summary>Drukuje na wskazanej drukarce bez okna (np. z linii poleceń). <paramref name="outputFile"/> — wydruk do pliku.</summary>
    public static async Task<Result> PrintToPrinterAsync(string printerName, EditorSession session, PrintOptions opt, string? outputFile = null)
    {
        using var dc = new PrinterDc { Hdc = CreateDCW("WINSPOOL", printerName, null, 0) };
        if (dc.Hdc == 0) return new Result(false, $"Nie znaleziono drukarki „{printerName}”.");
        return await PrintToDcAsync(dc, session, opt, null, outputFile);
    }

    // ================================================================== druk dwustronny — ustawienia drukarki

    /// <summary>Czy drukarka sama drukuje dwustronnie (null = nie wiadomo, np. bez okna drukowania).</summary>
    private static unsafe bool? SupportsDuplex(PrinterDc dc)
    {
        if (dc.DevNames == 0) return null;
        nint names = GlobalLock(dc.DevNames);
        if (names == 0) return null;
        try
        {
            // DEVNAMES: przesunięcia (w znakach) nazwy sterownika, urządzenia i portu
            ushort* offsets = (ushort*)names;
            string device = new((char*)names + offsets[1]);
            string port = new((char*)names + offsets[2]);
            nint dm = dc.DevMode != 0 ? GlobalLock(dc.DevMode) : 0;
            try { return DeviceCapabilitiesW(device, port, DC_DUPLEX, 0, dm) == 1; }
            finally { if (dm != 0) GlobalUnlock(dc.DevMode); }
        }
        finally
        {
            GlobalUnlock(dc.DevNames);
        }
    }

    /// <summary>Ustawia w DEVMODE drukarki druk jedno- albo dwustronny i odświeża kontekst urządzenia.</summary>
    private static unsafe void SetDuplexMode(PrinterDc dc, short mode)
    {
        if (dc.DevMode == 0) return;
        nint dm = GlobalLock(dc.DevMode);
        if (dm == 0) return;
        try
        {
            *(uint*)((byte*)dm + DevModeFieldsOffset) |= DM_DUPLEX;
            *(short*)((byte*)dm + DevModeDuplexOffset) = mode;
            ResetDCW(dc.Hdc, dm);
        }
        finally
        {
            GlobalUnlock(dc.DevMode);
        }
    }

    /// <summary>
    /// Wybiera tryb: drukarka z dupleksem dostaje jedno zadanie (przód, tył, przód, tył…), a bez dupleksu —
    /// albo gdy użytkownik tak woli — drukujemy ręcznie: najpierw przody, potem tyły po przełożeniu kartek.
    /// </summary>
    private static (bool Manual, string? Note) PrepareDuplex(PrinterDc dc, bool wantManual, bool shortEdge, bool interactive)
    {
        if (!interactive) return (false, null); // linia poleceń: jedno zadanie, ustawienia drukarki decydują
        if (wantManual)
        {
            SetDuplexMode(dc, DMDUP_SIMPLEX); // przody muszą wyjść na osobnych kartkach
            return (true, null);
        }
        if (SupportsDuplex(dc) == false)
        {
            SetDuplexMode(dc, DMDUP_SIMPLEX);
            return (true, "Ta drukarka nie drukuje dwustronnie sama, więc tyły wydrukujemy po przełożeniu kartek.");
        }
        SetDuplexMode(dc, shortEdge ? DMDUP_HORIZONTAL : DMDUP_VERTICAL);
        return (false, null);
    }

    private static string FlipInstructions(bool shortEdge, int sheets, string? note) =>
        (note is null ? "" : note + "\n\n") +
        $"Przody są wydrukowane. Weź {(sheets == 1 ? "kartkę" : $"wszystkie {sheets} kartki (w tej samej kolejności)")} " +
        "i włóż z powrotem do podajnika czystą stroną do druku, " +
        (shortEdge
            ? "obracając kartkę wzdłuż krótszej krawędzi (góra kartki trafia na dół)."
            : "obracając kartkę wzdłuż dłuższej krawędzi (jak stronę w książce — góra zostaje u góry).") +
        "\n\nNie wiesz, jak Twoja drukarka pobiera papier? Wydrukuj najpierw kartkę testową w „Wyrównanie stron…”.";

    /// <summary>Geometria strony drukarki + przeliczanie mm → piksele urządzenia.</summary>
    private sealed class DeviceGeometry
    {
        public int DpiX, DpiY, OffX, OffY;
        public double PageW, PageH;
        public double ScaleX = 1, ScaleY = 1, ShiftX, ShiftY;

        public static DeviceGeometry? From(nint hdc, bool calibrated)
        {
            var g = new DeviceGeometry
            {
                DpiX = GetDeviceCaps(hdc, LOGPIXELSX), DpiY = GetDeviceCaps(hdc, LOGPIXELSY),
                OffX = GetDeviceCaps(hdc, PHYSICALOFFSETX), OffY = GetDeviceCaps(hdc, PHYSICALOFFSETY),
            };
            if (g.DpiX <= 0 || g.DpiY <= 0) return null;
            int physW = GetDeviceCaps(hdc, PHYSICALWIDTH), physH = GetDeviceCaps(hdc, PHYSICALHEIGHT);
            if (physW > 0 && physH > 0)
            {
                g.PageW = physW * 25.4 / g.DpiX;
                g.PageH = physH * 25.4 / g.DpiY;
            }
            else
            {
                g.PageW = GetDeviceCaps(hdc, HORZRES) * 25.4 / g.DpiX;
                g.PageH = GetDeviceCaps(hdc, VERTRES) * 25.4 / g.DpiY;
                g.OffX = g.OffY = 0;
            }
            if (calibrated)
            {
                var s = AppSettings.Current;
                g.ScaleX = s.PrintScaleX; g.ScaleY = s.PrintScaleY;
                g.ShiftX = s.PrintOffsetX; g.ShiftY = s.PrintOffsetY;
            }
            return g;
        }

        public int X(double mm) => (int)Math.Round((mm * ScaleX + ShiftX) / 25.4 * DpiX) - OffX;
        public int Y(double mm) => (int)Math.Round((mm * ScaleY + ShiftY) / 25.4 * DpiY) - OffY;
    }

    /// <summary>Zadanie drukowania (StartDoc…EndDoc) z bezpiecznym przerwaniem przy błędzie.</summary>
    private sealed class PrintJob : IDisposable
    {
        private readonly nint _hdc;
        private nint _name, _output;
        private bool _started;

        public PrintJob(nint hdc, string name, string? outputFile)
        {
            _hdc = hdc;
            _name = Marshal.StringToHGlobalUni(name);
            if (outputFile is not null) _output = Marshal.StringToHGlobalUni(outputFile);
            var di = new DOCINFOW { cbSize = Unsafe.SizeOf<DOCINFOW>(), lpszDocName = _name, lpszOutput = _output };
            if (StartDocW(hdc, ref di) <= 0) throw new InvalidOperationException("Drukarka odrzuciła zadanie drukowania.");
            _started = true;
        }

        /// <summary>Jedna strona: przygotowanie GDI, rysowanie, zakończenie strony.</summary>
        public void Page(Action draw)
        {
            if (StartPage(_hdc) <= 0) throw new InvalidOperationException("Błąd podczas rozpoczynania strony.");
            SetStretchBltMode(_hdc, HALFTONE);
            SetBrushOrgEx(_hdc, 0, 0, 0);
            draw();
            if (EndPage(_hdc) <= 0) throw new InvalidOperationException("Błąd podczas kończenia strony.");
        }

        public void Finish()
        {
            EndDoc(_hdc);
            _started = false;
        }

        public void Dispose()
        {
            if (_started) AbortDoc(_hdc);
            if (_name != 0) Marshal.FreeHGlobal(_name);
            if (_output != 0) Marshal.FreeHGlobal(_output);
            _name = _output = 0;
        }
    }

    private static async Task<Result> PrintToDcAsync(PrinterDc dc, EditorSession session, PrintOptions opt, FlipPrompt? flip, string? outputFile)
    {
        try
        {
            var cards = session.Project.Cards;
            var indices = CardRasterizer.CardsToPrint(session, opt);
            opt.ApplyCalibration = true; // drukarka: zawsze z kalibracją (także wyrównaniem tyłu)

            // tryb dwustronny trzeba ustawić w drukarce przed odczytem geometrii strony
            bool hasInsides = opt.Duplex && indices.Exists(i => session.Project.InsideOf(cards[i]) is not null);
            var (manual, note) = hasInsides ? PrepareDuplex(dc, opt.DuplexManual, opt.DuplexShortEdge, flip is not null) : (false, null);

            var g = DeviceGeometry.From(dc.Hdc, calibrated: true);
            if (g is null) return new Result(false, "Nieprawidłowa rozdzielczość drukarki.");
            var layout = SheetLayout.Compute(g.PageW, g.PageH, cards, indices, opt);
            bool duplex = layout.HasBack;

            // bitmapy kart: każda raz (osobno wersja obrócona o 180° dla odwrocia „wzdłuż krótszej krawędzi”)
            double renderDpi = Math.Clamp(Math.Max(g.DpiX, g.DpiY), 150, 600);
            var bitmaps = new Dictionary<(int Index, bool Rotated), (byte[] Pixels, int W, int H)>();
            foreach (var pc in layout.Pages.SelectMany(p => p.Cards.Concat(p.Back)))
            {
                if (bitmaps.ContainsKey((pc.CardIndex, pc.Rotated))) continue;
                using var rt = await CardRasterizer.RenderAsync(session, cards[pc.CardIndex], renderDpi, includeBleed: true);
                var px = rt.GetPixelBytes();
                if (pc.Rotated) CardRasterizer.Rotate180(px);
                bitmaps[(pc.CardIndex, pc.Rotated)] = (px, (int)rt.SizeInPixels.Width, (int)rt.SizeInPixels.Height);
            }

            void Side(List<PlacedCard> placed, List<MarkLine> marks)
            {
                foreach (var pc in placed)
                {
                    var card = cards[pc.CardIndex];
                    var (px, bw, bh) = bitmaps[(pc.CardIndex, pc.Rotated)];
                    double b = Math.Max(0, card.Bleed);
                    int dx = g.X(pc.Position.X - b), dy = g.Y(pc.Position.Y - b);
                    int dw = g.X(pc.Position.X + card.Width + b) - dx, dh = g.Y(pc.Position.Y + card.Height + b) - dy;
                    BlitBanded(dc.Hdc, px, bw, bh, dx, dy, dw, dh);
                }
                if (marks.Count == 0) return;
                int penW = Math.Max(1, (int)Math.Round(0.2 / 25.4 * g.DpiX));
                DrawMarks(dc.Hdc, marks, false, CreatePen(PS_SOLID, penW, Rgb(0, 0, 0)), g);
                DrawMarks(dc.Hdc, marks, true, CreatePen(PS_SOLID, penW, Rgb(130, 130, 130)), g);
            }

            string title = $"Kasetownik 9000 – {session.DisplayName}";
            int copies = dc.Copies, sheets = layout.Pages.Count * copies;
            if (!duplex || !manual)
            {
                // jednostronnie albo drukarka z dupleksem: przód, tył, przód, tył…
                using var job = new PrintJob(dc.Hdc, title, outputFile);
                for (int copy = 0; copy < copies; copy++)
                {
                    foreach (var page in layout.Pages)
                    {
                        job.Page(() => Side(page.Cards, page.Marks));
                        if (duplex) job.Page(() => Side(page.Back, []));
                    }
                }
                job.Finish();
            }
            else
            {
                // ręcznie: najpierw same przody, potem — po przełożeniu kartek — tyły w tej samej kolejności
                using (var fronts = new PrintJob(dc.Hdc, title + " (przody)", outputFile))
                {
                    for (int copy = 0; copy < copies; copy++)
                        foreach (var page in layout.Pages) fronts.Page(() => Side(page.Cards, page.Marks));
                    fronts.Finish();
                }
                if (flip is null || !await flip(FlipInstructions(opt.DuplexShortEdge, sheets, note)))
                    return new Result(true, null, "Wydrukowano przody — tyły (wnętrza kart) pominięte.");
                using var backs = new PrintJob(dc.Hdc, title + " (tyły)", outputFile);
                for (int copy = 0; copy < copies; copy++)
                    foreach (var page in layout.Pages) backs.Page(() => Side(page.Back, []));
                backs.Finish();
            }

            string sides = duplex ? " dwustronnie" : "";
            return new Result(true, null,
                $"Wysłano do drukarki{sides}: {layout.CardCount} kart(y) na {sheets} {(sheets == 1 ? "arkuszu" : "arkuszach")}.");
        }
        catch (Exception ex)
        {
            return new Result(false, ex.Message);
        }
    }

    private static void DrawMarks(nint hdc, List<MarkLine> marks, bool fold, nint pen, DeviceGeometry g)
    {
        var old = SelectObject(hdc, pen);
        foreach (var m in marks)
        {
            if (m.Fold != fold) continue;
            MoveToEx(hdc, g.X(m.X1), g.Y(m.Y1), 0);
            LineTo(hdc, g.X(m.X2), g.Y(m.Y2));
        }
        SelectObject(hdc, old);
        DeleteObject(pen);
    }

    // ================================================================== strona kalibracyjna

    public const double CalibrationRuler = 100;   // długość miarek (mm)
    public const double CalibrationOrigin = 30;   // krzyżyk w odległości 30 mm od lewej i górnej krawędzi

    /// <summary>Drukuje stronę testową BEZ korekty — z pomiarów liczona jest kalibracja.</summary>
    public static async Task<Result> PrintCalibrationPageAsync(nint hwnd)
    {
        using var dc = ShowPrintDialog(hwnd, out var error);
        if (dc is null) return new Result(false, error);
        return await PrintCalibrationToDcAsync(dc.Hdc, null);
    }

    /// <summary>Strona testowa na wskazanej drukarce (bez okna) — np. z linii poleceń.</summary>
    public static async Task<Result> PrintCalibrationToPrinterAsync(string printerName, string? outputFile)
    {
        nint hdc = CreateDCW("WINSPOOL", printerName, null, 0);
        if (hdc == 0) return new Result(false, $"Nie znaleziono drukarki „{printerName}”.");
        try { return await PrintCalibrationToDcAsync(hdc, outputFile); }
        finally { DeleteDC(hdc); }
    }

    private static async Task<Result> PrintCalibrationToDcAsync(nint hdc, string? outputFile)
    {
        await Task.Yield();
        nint docName = 0, output = 0;
        bool started = false;
        try
        {
            var g = DeviceGeometry.From(hdc, calibrated: false);
            if (g is null) return new Result(false, "Nieprawidłowa rozdzielczość drukarki.");

            const double size = 170, dpi = 300;
            using var rt = new CanvasRenderTarget(CanvasDevice.GetSharedDevice(),
                (float)(size * Units.DipPerMm), (float)(size * Units.DipPerMm), (float)dpi);
            using (var ds = rt.CreateDrawingSession())
            {
                ds.Clear(Microsoft.UI.Colors.White);
                ds.TextAntialiasing = CanvasTextAntialiasing.Grayscale;
                ds.Transform = Matrix3x2.CreateScale((float)Units.DipPerMm);
                DrawCalibrationPage(ds);
            }
            var px = rt.GetPixelBytes();
            int bw = (int)rt.SizeInPixels.Width, bh = (int)rt.SizeInPixels.Height;

            docName = Marshal.StringToHGlobalUni("Kasetownik 9000 – strona kalibracyjna");
            if (outputFile is not null) output = Marshal.StringToHGlobalUni(outputFile);
            var di = new DOCINFOW { cbSize = Unsafe.SizeOf<DOCINFOW>(), lpszDocName = docName, lpszOutput = output };
            if (StartDocW(hdc, ref di) <= 0) return new Result(false, "Drukarka odrzuciła zadanie drukowania.");
            started = true;
            StartPage(hdc);
            SetStretchBltMode(hdc, HALFTONE);
            SetBrushOrgEx(hdc, 0, 0, 0);
            int x0 = g.X(0), y0 = g.Y(0);
            BlitBanded(hdc, px, bw, bh, x0, y0, g.X(size) - x0, g.Y(size) - y0);
            EndPage(hdc);
            EndDoc(hdc);
            started = false;
            return new Result(true, null, "Wydrukowano stronę kalibracyjną — zmierz miarki i wpisz wyniki.");
        }
        catch (Exception ex)
        {
            if (started) AbortDoc(hdc);
            return new Result(false, ex.Message);
        }
        finally
        {
            if (docName != 0) Marshal.FreeHGlobal(docName);
            if (output != 0) Marshal.FreeHGlobal(output);
        }
    }

    private static void DrawCalibrationPage(CanvasDrawingSession ds)
    {
        var ink = Color.FromArgb(255, 0, 0, 0);
        const float o = (float)CalibrationOrigin, len = (float)CalibrationRuler, lw = 0.15f;
        using var small = new CanvasTextFormat { FontFamily = "Bahnschrift", FontSize = 2.6f, HorizontalAlignment = CanvasHorizontalAlignment.Center };
        using var smallV = new CanvasTextFormat { FontFamily = "Bahnschrift", FontSize = 2.6f };
        using var body = new CanvasTextFormat { FontFamily = "Segoe UI", FontSize = 3.4f, WordWrapping = CanvasWordWrapping.Wrap };
        using var title = new CanvasTextFormat { FontFamily = "Bahnschrift", FontSize = 5f, FontWeight = new Windows.UI.Text.FontWeight { Weight = 700 } };

        // krzyżyk w punkcie (30, 30) mm od krawędzi kartki
        ds.DrawLine(o - 9, o, o + 9, o, ink, lw);
        ds.DrawLine(o, o - 9, o, o + 9, ink, lw);
        ds.DrawCircle(o, o, 3, ink, lw);

        // miarka pozioma i pionowa (od krzyżyka, 100 mm)
        for (int mm = 0; mm <= (int)len; mm++)
        {
            float t = mm % 10 == 0 ? 5 : mm % 5 == 0 ? 3 : 1.6f;
            ds.DrawLine(o + mm, o, o + mm, o + t, ink, lw);
            ds.DrawLine(o, o + mm, o + t, o + mm, ink, lw);
            if (mm % 10 == 0 && mm > 0)
            {
                ds.DrawText(mm.ToString(), o + mm - 5, o + 5.6f, 10, 4, ink, small);
                ds.DrawText(mm.ToString(), o + 5.8f, o + mm - 1.6f, ink, smallV);
            }
        }
        ds.DrawLine(o, o, o + len, o, ink, lw);
        ds.DrawLine(o, o, o, o + len, ink, lw);

        ds.DrawText($"{AppInfo.FullName} — strona kalibracyjna", o + 14, o + 16, ink, title);
        ds.DrawText(
            "1. Zmierz linijką poziomą miarkę od 0 do 100 — powinna mieć dokładnie 100 mm.\n" +
            "2. Zmierz tak samo miarkę pionową.\n" +
            "3. Zmierz odległość środka krzyżyka od lewej krawędzi kartki oraz od górnej krawędzi — powinny wynosić po 30 mm.\n" +
            "4. Wpisz cztery wyniki w oknie kalibracji w Kasetowniku i zapisz.\n\n" +
            "Drukuj tę stronę w skali 100% (bez dopasowania do strony), na tym samym papierze co okładki.",
            new Windows.Foundation.Rect(o + 14, o + 26, 105, 60), ink, body);
    }

    // ================================================================== kartka testowa druku dwustronnego

    public const double DuplexTestSize = 150; // kwadrat na środku arkusza (mm)

    /// <summary>Kartka testowa: z przodu miarki, z tyłu gruby krzyżyk w tym samym miejscu. Bez korekty wyrównania.</summary>
    public static async Task<Result> PrintDuplexTestAsync(nint hwnd, bool manual, bool shortEdge, FlipPrompt? flip)
    {
        using var dc = ShowPrintDialog(hwnd, out var error);
        if (dc is null) return new Result(false, error);
        return await PrintDuplexTestToDcAsync(dc, manual, shortEdge, flip, null);
    }

    /// <summary>Kartka testowa na wskazanej drukarce (bez okna) — np. z linii poleceń.</summary>
    public static async Task<Result> PrintDuplexTestToPrinterAsync(string printerName, bool shortEdge, string? outputFile)
    {
        using var dc = new PrinterDc { Hdc = CreateDCW("WINSPOOL", printerName, null, 0) };
        if (dc.Hdc == 0) return new Result(false, $"Nie znaleziono drukarki „{printerName}”.");
        return await PrintDuplexTestToDcAsync(dc, false, shortEdge, null, outputFile);
    }

    private static async Task<Result> PrintDuplexTestToDcAsync(PrinterDc dc, bool manualWanted, bool shortEdge, FlipPrompt? flip, string? outputFile)
    {
        await Task.Yield();
        try
        {
            var (manual, note) = PrepareDuplex(dc, manualWanted, shortEdge, flip is not null);
            var g = DeviceGeometry.From(dc.Hdc, calibrated: true);
            if (g is null) return new Result(false, "Nieprawidłowa rozdzielczość drukarki.");

            const double size = DuplexTestSize;
            double x = (g.PageW - size) / 2, y = (g.PageH - size) / 2;
            var (backPos, rotated) = SheetLayout.MirrorToBack(x, y, size, size, g.PageW, g.PageH, shortEdge);
            var front = RenderDuplexTest(back: false, rotated: false);
            var back = RenderDuplexTest(back: true, rotated);

            void Blit((byte[] Px, int W, int H) img, double left, double top)
            {
                int dx = g.X(left), dy = g.Y(top);
                BlitBanded(dc.Hdc, img.Px, img.W, img.H, dx, dy, g.X(left + size) - dx, g.Y(top + size) - dy);
            }

            const string title = "Kasetownik 9000 – kartka testowa druku dwustronnego";
            if (!manual)
            {
                using var job = new PrintJob(dc.Hdc, title, outputFile);
                job.Page(() => Blit(front, x, y));
                job.Page(() => Blit(back, backPos.X, backPos.Y));
                job.Finish();
            }
            else
            {
                using (var job = new PrintJob(dc.Hdc, title + " (przód)", outputFile))
                {
                    job.Page(() => Blit(front, x, y));
                    job.Finish();
                }
                if (flip is null || !await flip(FlipInstructions(shortEdge, 1, note)))
                    return new Result(true, null, "Wydrukowano tylko przód kartki testowej.");
                using var backJob = new PrintJob(dc.Hdc, title + " (tył)", outputFile);
                backJob.Page(() => Blit(back, backPos.X, backPos.Y));
                backJob.Finish();
            }
            return new Result(true, null, "Wydrukowano kartkę testową — obejrzyj ją pod światło i wpisz przesunięcie krzyżyka.");
        }
        catch (Exception ex)
        {
            return new Result(false, ex.Message);
        }
    }

    private static (byte[] Px, int W, int H) RenderDuplexTest(bool back, bool rotated)
    {
        const double size = DuplexTestSize, dpi = 300;
        using var rt = new CanvasRenderTarget(CanvasDevice.GetSharedDevice(),
            (float)(size * Units.DipPerMm), (float)(size * Units.DipPerMm), (float)dpi);
        using (var ds = rt.CreateDrawingSession())
        {
            ds.Clear(Microsoft.UI.Colors.White);
            ds.TextAntialiasing = CanvasTextAntialiasing.Grayscale;
            ds.Transform = Matrix3x2.CreateScale((float)Units.DipPerMm);
            if (back) DrawDuplexBack(ds); else DrawDuplexFront(ds);
        }
        var px = rt.GetPixelBytes();
        if (rotated) CardRasterizer.Rotate180(px);
        return (px, (int)rt.SizeInPixels.Width, (int)rt.SizeInPixels.Height);
    }

    private static void DrawDuplexFront(CanvasDrawingSession ds)
    {
        var ink = Color.FromArgb(255, 0, 0, 0);
        const float c = (float)(DuplexTestSize / 2), lw = 0.12f, reach = 20;
        using var num = new CanvasTextFormat
        {
            FontFamily = "Bahnschrift", FontSize = 2.2f,
            HorizontalAlignment = CanvasHorizontalAlignment.Center, VerticalAlignment = CanvasVerticalAlignment.Center,
        };
        using var foot = new CanvasTextFormat { FontFamily = "Bahnschrift", FontSize = 2.4f };
        using var body = new CanvasTextFormat { FontFamily = "Segoe UI", FontSize = 3.2f, WordWrapping = CanvasWordWrapping.Wrap };
        using var title = new CanvasTextFormat { FontFamily = "Bahnschrift", FontSize = 5f, FontWeight = new Windows.UI.Text.FontWeight { Weight = 700 } };

        ds.DrawText("PRZÓD — kartka testowa druku dwustronnego", 8, 6, ink, title);
        ds.DrawText(
            "1. Obejrzyj kartkę pod światło, patrząc na tę stronę.\n" +
            "2. Z tyłu prześwituje gruby krzyżyk w kółku. Odczytaj na miarkach, o ile jego środek jest przesunięty od zera: " +
            "w prawo (+) albo w lewo (−) oraz w dół (+) albo w górę (−).\n" +
            "3. Wpisz oba wyniki w oknie „Wyrównanie stron” w Kasetowniku i zapisz.\n" +
            "4. Napis „TYŁ” na odwrocie powinien stać tak samo jak ten tekst. Jeśli jest do góry nogami, " +
            "zmień sposób obracania kartki (dłuższa ↔ krótsza krawędź).",
            new Windows.Foundation.Rect(8, 14, DuplexTestSize - 16, 40), ink, body);

        // miarki co 1 mm (dłuższe co 5 mm) przecinające się w środku arkusza; opisy rozłożone tak,
        // żeby w każdej ćwiartce był tylko jeden komplet liczb (inaczej „+5” i „−5” nachodzą na siebie przy środku)
        ds.DrawLine(c - reach, c, c + reach, c, ink, lw);
        ds.DrawLine(c, c - reach, c, c + reach, ink, lw);
        for (int mm = -(int)reach; mm <= reach; mm++)
        {
            float t = mm % 5 == 0 ? 2.4f : 1.1f;
            ds.DrawLine(c + mm, c - t, c + mm, c + t, ink, lw);
            ds.DrawLine(c - t, c + mm, c + t, c + mm, ink, lw);
            if (mm % 5 != 0 || mm == 0) continue;
            string label = mm > 0 ? $"+{mm}" : $"−{-mm}";
            ds.DrawText(label, c + mm - 4, mm > 0 ? c + 3.2f : c - 6.2f, 8, 3, ink, num);   // poziomo: + pod spodem, − nad
            ds.DrawText(label, mm > 0 ? c - 11.5f : c + 3.5f, c + mm - 1.5f, 8, 3, ink, num); // pionowo: + po lewej, − po prawej
        }
        ds.DrawText($"{AppInfo.FullName} · kartka testowa druku dwustronnego", 8, (float)DuplexTestSize - 10, ink, foot);
    }

    private static void DrawDuplexBack(CanvasDrawingSession ds)
    {
        var ink = Color.FromArgb(255, 0, 0, 0);
        const float c = (float)(DuplexTestSize / 2);
        using var big = new CanvasTextFormat
        {
            FontFamily = "Bahnschrift", FontSize = 14f, FontWeight = new Windows.UI.Text.FontWeight { Weight = 700 },
            HorizontalAlignment = CanvasHorizontalAlignment.Center,
        };
        using var body = new CanvasTextFormat { FontFamily = "Segoe UI", FontSize = 3.4f, HorizontalAlignment = CanvasHorizontalAlignment.Center, WordWrapping = CanvasWordWrapping.Wrap };

        ds.DrawText("TYŁ ↑", new Windows.Foundation.Rect(0, c - 52, DuplexTestSize, 18), ink, big);
        ds.DrawText("Ta strona powinna stać tak samo jak przód (strzałka w stronę górnej krawędzi kartki).",
            new Windows.Foundation.Rect(20, c - 32, DuplexTestSize - 40, 12), ink, body);
        ds.DrawLine(c - 10, c, c + 10, c, ink, 0.6f);
        ds.DrawLine(c, c - 10, c, c + 10, ink, 0.6f);
        ds.DrawCircle(c, c, 3.2f, ink, 0.6f);
    }

    /// <summary>Wysyła bitmapę pasami — część sterowników drukarek źle znosi jedną ogromną bitmapę.</summary>
    private static unsafe void BlitBanded(nint hdc, byte[] bgra, int bw, int bh, int dx, int dy, int dw, int dh)
    {
        const int band = 256;
        fixed (byte* p = bgra)
        {
            for (int row = 0; row < bh; row += band)
            {
                int rows = Math.Min(band, bh - row);
                var bmi = new BITMAPINFOHEADER
                {
                    biSize = (uint)sizeof(BITMAPINFOHEADER),
                    biWidth = bw,
                    biHeight = -rows, // top-down
                    biPlanes = 1,
                    biBitCount = 32,
                    biCompression = 0, // BI_RGB
                };
                int y0 = dy + (int)Math.Round((double)row * dh / bh);
                int y1 = dy + (int)Math.Round((double)(row + rows) * dh / bh);
                StretchDIBits(hdc, dx, y0, dw, y1 - y0, 0, 0, bw, rows, p + (long)row * bw * 4, &bmi, DIB_RGB_COLORS, SRCCOPY);
            }
        }
    }
}
