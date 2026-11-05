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

    /// <summary>Pokazuje okno wyboru drukarki i drukuje.</summary>
    public static async Task<Result> PrintAsync(nint hwnd, EditorSession session, PrintOptions opt)
    {
        using var dc = ShowPrintDialog(hwnd, out var error);
        if (dc is null) return new Result(false, error);
        return await PrintToDcAsync(dc.Hdc, session, opt, dc.Copies, null);
    }

    /// <summary>Drukuje na wskazanej drukarce bez okna (np. z linii poleceń). <paramref name="outputFile"/> — wydruk do pliku.</summary>
    public static async Task<Result> PrintToPrinterAsync(string printerName, EditorSession session, PrintOptions opt, string? outputFile = null)
    {
        nint hdc = CreateDCW("WINSPOOL", printerName, null, 0);
        if (hdc == 0) return new Result(false, $"Nie znaleziono drukarki „{printerName}”.");
        try
        {
            return await PrintToDcAsync(hdc, session, opt, 1, outputFile);
        }
        finally
        {
            DeleteDC(hdc);
        }
    }

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

    private static async Task<Result> PrintToDcAsync(nint hdc, EditorSession session, PrintOptions opt, int copies, string? outputFile)
    {
        nint docName = 0, output = 0;
        bool started = false;
        var bitmaps = new Dictionary<int, (byte[] Pixels, int W, int H)>();
        try
        {
            var g = DeviceGeometry.From(hdc, calibrated: true);
            if (g is null) return new Result(false, "Nieprawidłowa rozdzielczość drukarki.");

            var cards = session.Project.Cards;
            var layout = SheetLayout.Compute(g.PageW, g.PageH, cards, CardRasterizer.CardsToPrint(session, opt), opt);
            double renderDpi = Math.Clamp(Math.Max(g.DpiX, g.DpiY), 150, 600);
            foreach (var idx in layout.Pages.SelectMany(p => p.Cards).Select(c => c.CardIndex).Distinct())
            {
                using var rt = await CardRasterizer.RenderAsync(session, cards[idx], renderDpi, includeBleed: true);
                bitmaps[idx] = (rt.GetPixelBytes(), (int)rt.SizeInPixels.Width, (int)rt.SizeInPixels.Height);
            }

            docName = Marshal.StringToHGlobalUni($"Kasetownik 9000 – {session.DisplayName}");
            if (outputFile is not null) output = Marshal.StringToHGlobalUni(outputFile);
            var di = new DOCINFOW { cbSize = Unsafe.SizeOf<DOCINFOW>(), lpszDocName = docName, lpszOutput = output };
            if (StartDocW(hdc, ref di) <= 0) return new Result(false, "Drukarka odrzuciła zadanie drukowania.");
            started = true;

            for (int copy = 0; copy < copies; copy++)
            {
                foreach (var page in layout.Pages)
                {
                    if (StartPage(hdc) <= 0) throw new InvalidOperationException("Błąd podczas rozpoczynania strony.");
                    SetStretchBltMode(hdc, HALFTONE);
                    SetBrushOrgEx(hdc, 0, 0, 0);

                    foreach (var pc in page.Cards)
                    {
                        var card = cards[pc.CardIndex];
                        var (px, bw, bh) = bitmaps[pc.CardIndex];
                        double b = Math.Max(0, card.Bleed);
                        int dx = g.X(pc.Position.X - b), dy = g.Y(pc.Position.Y - b);
                        int dw = g.X(pc.Position.X + card.Width + b) - dx, dh = g.Y(pc.Position.Y + card.Height + b) - dy;
                        BlitBanded(hdc, px, bw, bh, dx, dy, dw, dh);
                    }

                    int penW = Math.Max(1, (int)Math.Round(0.2 / 25.4 * g.DpiX));
                    DrawMarks(hdc, page, false, CreatePen(PS_SOLID, penW, Rgb(0, 0, 0)), g);
                    DrawMarks(hdc, page, true, CreatePen(PS_SOLID, penW, Rgb(130, 130, 130)), g);

                    if (EndPage(hdc) <= 0) throw new InvalidOperationException("Błąd podczas kończenia strony.");
                }
            }

            EndDoc(hdc);
            started = false;
            int pages = layout.Pages.Count * copies;
            return new Result(true, null, $"Wysłano do drukarki: {layout.CardCount} kart(y) na {pages} {(pages == 1 ? "stronie" : "stronach")}.");
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

    private static void DrawMarks(nint hdc, SheetPage page, bool fold, nint pen, DeviceGeometry g)
    {
        var old = SelectObject(hdc, pen);
        foreach (var m in page.Marks)
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
