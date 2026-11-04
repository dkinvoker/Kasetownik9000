using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Kasetownik9000.Editor;
using static Kasetownik9000.Output.Native;

namespace Kasetownik9000.Output;

/// <summary>
/// Drukowanie przez okno "Drukuj" Windows i GDI. Karta jest rasteryzowana tym samym
/// rendererem co podgląd i umieszczana w skali 1:1 (z uwzględnieniem marginesów fizycznych drukarki).
/// </summary>
public static class PrintService
{
    public sealed record Result(bool Printed, string? Error, string? Info = null);

    /// <summary>Pokazuje okno wyboru drukarki i drukuje.</summary>
    public static async Task<Result> PrintAsync(nint hwnd, EditorSession session, PrintOptions opt)
    {
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
            return err == 0 ? new Result(false, null) : new Result(false, $"Nie udało się otworzyć okna drukowania (kod 0x{err:X}).");
        }

        try
        {
            if (pd.hDC == 0) return new Result(false, "Drukarka nie zwróciła kontekstu urządzenia.");
            return await PrintToDcAsync(pd.hDC, session, opt, Math.Max(1, (int)pd.nCopies), null);
        }
        finally
        {
            if (pd.hDC != 0) DeleteDC(pd.hDC);
            if (pd.hDevMode != 0) GlobalFree(pd.hDevMode);
            if (pd.hDevNames != 0) GlobalFree(pd.hDevNames);
        }
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

    private static async Task<Result> PrintToDcAsync(nint hdc, EditorSession session, PrintOptions opt, int pages, string? outputFile)
    {
        nint docName = 0, output = 0;
        bool started = false;
        try
        {
            int dpiX = GetDeviceCaps(hdc, LOGPIXELSX), dpiY = GetDeviceCaps(hdc, LOGPIXELSY);
            int physW = GetDeviceCaps(hdc, PHYSICALWIDTH), physH = GetDeviceCaps(hdc, PHYSICALHEIGHT);
            int offX = GetDeviceCaps(hdc, PHYSICALOFFSETX), offY = GetDeviceCaps(hdc, PHYSICALOFFSETY);
            if (dpiX <= 0 || dpiY <= 0) return new Result(false, "Nieprawidłowa rozdzielczość drukarki.");

            double pageW = physW * 25.4 / dpiX, pageH = physH * 25.4 / dpiY;
            if (physW <= 0 || physH <= 0)
            {
                pageW = GetDeviceCaps(hdc, HORZRES) * 25.4 / dpiX;
                pageH = GetDeviceCaps(hdc, VERTRES) * 25.4 / dpiY;
                offX = offY = 0;
            }

            var doc = session.Doc;
            var layout = SheetLayout.Compute(pageW, pageH, doc, opt);
            double renderDpi = Math.Clamp(Math.Max(dpiX, dpiY), 150, 600);

            using var rt = await CardRasterizer.RenderAsync(session, renderDpi, includeBleed: true);
            byte[] pixels = rt.GetPixelBytes();
            int bw = (int)rt.SizeInPixels.Width, bh = (int)rt.SizeInPixels.Height;

            int X(double mm) => (int)Math.Round(mm / 25.4 * dpiX) - offX;
            int Y(double mm) => (int)Math.Round(mm / 25.4 * dpiY) - offY;

            docName = Marshal.StringToHGlobalUni($"Kasetownik 9000 – {session.DisplayName}");
            if (outputFile is not null) output = Marshal.StringToHGlobalUni(outputFile);
            var di = new DOCINFOW { cbSize = Unsafe.SizeOf<DOCINFOW>(), lpszDocName = docName, lpszOutput = output };
            if (StartDocW(hdc, ref di) <= 0) return new Result(false, "Drukarka odrzuciła zadanie drukowania.");
            started = true;

            double b = Math.Max(0, doc.Bleed);
            for (int page = 0; page < pages; page++)
            {
                if (StartPage(hdc) <= 0) throw new InvalidOperationException("Błąd podczas rozpoczynania strony.");

                SetStretchBltMode(hdc, HALFTONE);
                SetBrushOrgEx(hdc, 0, 0, 0);

                foreach (var p in layout.Cards)
                {
                    int dx = X(p.X - b), dy = Y(p.Y - b);
                    int dw = X(p.X + doc.Width + b) - dx, dh = Y(p.Y + doc.Height + b) - dy;
                    BlitBanded(hdc, pixels, bw, bh, dx, dy, dw, dh);
                }

                int penW = Math.Max(1, (int)Math.Round(0.2 / 25.4 * dpiX));
                DrawMarks(hdc, layout, false, CreatePen(PS_SOLID, penW, Rgb(0, 0, 0)), X, Y);
                DrawMarks(hdc, layout, true, CreatePen(PS_SOLID, penW, Rgb(130, 130, 130)), X, Y);

                if (EndPage(hdc) <= 0) throw new InvalidOperationException("Błąd podczas kończenia strony.");
            }

            EndDoc(hdc);
            started = false;
            return new Result(true, null, $"Wysłano do drukarki: {layout.Cards.Count} szt. na stronie, {pages} str.");
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

    private static void DrawMarks(nint hdc, SheetLayout layout, bool fold, nint pen, Func<double, int> X, Func<double, int> Y)
    {
        var old = SelectObject(hdc, pen);
        foreach (var m in layout.Marks)
        {
            if (m.Fold != fold) continue;
            MoveToEx(hdc, X(m.X1), Y(m.Y1), 0);
            LineTo(hdc, X(m.X2), Y(m.Y2));
        }
        SelectObject(hdc, old);
        DeleteObject(pen);
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
