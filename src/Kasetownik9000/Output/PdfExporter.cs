using System.Globalization;
using System.IO.Compression;
using System.Text;
using Kasetownik9000.Editor;
using Kasetownik9000.Model;
using Microsoft.Graphics.Canvas;

namespace Kasetownik9000.Output;

/// <summary>Eksport do PDF (arkusze A4/Letter, karty w skali 1:1 + znaczniki) i PNG.</summary>
public static class Exporter
{
    public static async Task ExportPngAsync(string path, EditorSession s, PrintOptions opt)
    {
        using var rt = await CardRasterizer.RenderAsync(s, s.Doc, opt.Dpi, opt.IncludeBleed);
        await rt.SaveAsync(path, CanvasBitmapFileFormat.Png);
    }

    /// <summary>Zapisuje PDF. Zwraca liczbę kart i stron.</summary>
    public static async Task<(int Cards, int Pages)> ExportPdfAsync(string path, EditorSession s, PrintOptions opt)
    {
        var cards = s.Project.Cards;
        var (pageW, pageH) = opt.PaperMm;
        var layout = SheetLayout.Compute(pageW, pageH, cards, CardRasterizer.CardsToPrint(s, opt), opt);

        var settings = AppSettings.Current;
        double sx = opt.ApplyCalibration ? settings.PrintScaleX : 1, sy = opt.ApplyCalibration ? settings.PrintScaleY : 1;
        double ox = opt.ApplyCalibration ? settings.PrintOffsetX : 0, oy = opt.ApplyCalibration ? settings.PrintOffsetY : 0;

        // obrazy kart (każda karta raz, nawet gdy jest na arkuszu kilka razy)
        var used = layout.Pages.SelectMany(p => p.Cards).Select(c => c.CardIndex).Distinct().ToList();
        var images = new Dictionary<int, (int W, int H, byte[] Data)>();
        foreach (var idx in used)
        {
            using var rt = await CardRasterizer.RenderAsync(s, cards[idx], opt.Dpi, includeBleed: true);
            int w = (int)rt.SizeInPixels.Width, h = (int)rt.SizeInPixels.Height;
            byte[] bgra = rt.GetPixelBytes();
            images[idx] = (w, h, await Task.Run(() => Deflate(bgra)));
        }

        const double pt = 72 / 25.4;
        var inv = CultureInfo.InvariantCulture;
        string F(double v) => v.ToString("0.###", inv);
        double PX(double mm) => (mm * sx + ox) * pt;
        double PY(double mm) => (pageH - (mm * sy + oy)) * pt;

        // numeracja obiektów: 1 katalog, 2 drzewo stron, obrazy, (strona + treść) × n, informacje
        int firstImage = 3;
        var imageObj = used.Select((idx, i) => (idx, num: firstImage + i)).ToDictionary(x => x.idx, x => x.num);
        int firstPage = firstImage + used.Count;
        int infoObj = firstPage + layout.Pages.Count * 2;

        await using var fs = File.Create(path);
        var offsets = new SortedDictionary<int, long>();
        void W(string str) => fs.Write(Encoding.Latin1.GetBytes(str));
        void Obj(int num, string body)
        {
            offsets[num] = fs.Position;
            W($"{num} 0 obj\n{body}\nendobj\n");
        }
        void Stream(int num, string dict, byte[] data)
        {
            offsets[num] = fs.Position;
            W($"{num} 0 obj\n<< {dict} /Length {data.Length} >>\nstream\n");
            fs.Write(data);
            W("\nendstream\nendobj\n");
        }

        W("%PDF-1.4\n%âãÏÓ\n");
        Obj(1, "<< /Type /Catalog /Pages 2 0 R >>");
        var kids = string.Join(" ", Enumerable.Range(0, layout.Pages.Count).Select(i => $"{firstPage + i * 2} 0 R"));
        Obj(2, $"<< /Type /Pages /Kids [{kids}] /Count {layout.Pages.Count} >>");

        foreach (var idx in used)
        {
            var (w, h, data) = images[idx];
            Stream(imageObj[idx], $"/Type /XObject /Subtype /Image /Width {w} /Height {h} /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /FlateDecode", data);
        }

        for (int p = 0; p < layout.Pages.Count; p++)
        {
            var page = layout.Pages[p];
            int pageNum = firstPage + p * 2, contentNum = pageNum + 1;
            var xobjects = string.Join(" ", page.Cards.Select(c => c.CardIndex).Distinct().Select(i => $"/Im{i} {imageObj[i]} 0 R"));
            Obj(pageNum, $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {F(pageW * pt)} {F(pageH * pt)}] " +
                         $"/Resources << /XObject << {xobjects} >> >> /Contents {contentNum} 0 R >>");

            var content = new StringBuilder();
            foreach (var pc in page.Cards)
            {
                var card = cards[pc.CardIndex];
                double b = Math.Max(0, card.Bleed);
                double left = pc.Position.X - b, topMm = pc.Position.Y - b;
                double wPt = (card.Width + 2 * b) * sx * pt, hPt = (card.Height + 2 * b) * sy * pt;
                double x = PX(left), y = PY(topMm + card.Height + 2 * b);
                content.Append($"q {F(wPt)} 0 0 {F(hPt)} {F(x)} {F(y)} cm /Im{pc.CardIndex} Do Q\n");
            }
            foreach (var fold in new[] { false, true })
            {
                content.Append(fold ? "0.5 G 0.5 w\n" : "0 G 0.5 w\n");
                foreach (var m in page.Marks.Where(m => m.Fold == fold))
                    content.Append($"{F(PX(m.X1))} {F(PY(m.Y1))} m {F(PX(m.X2))} {F(PY(m.Y2))} l S\n");
            }
            Stream(contentNum, "", Encoding.ASCII.GetBytes(content.ToString()));
        }

        Obj(infoObj, $"<< /Title {PdfText($"Kasetownik 9000 – {s.DisplayName}")} /Producer {PdfText(AppInfo.FullName)} " +
                     $"/CreationDate (D:{DateTime.Now:yyyyMMddHHmmss}) >>");

        long xref = fs.Position;
        var sb = new StringBuilder();
        sb.Append($"xref\n0 {infoObj + 1}\n0000000000 65535 f \n");
        foreach (var o in offsets.Values) sb.Append($"{o:D10} 00000 n \n");
        sb.Append($"trailer\n<< /Size {infoObj + 1} /Root 1 0 R /Info {infoObj} 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        W(sb.ToString());
        return (layout.CardCount, layout.Pages.Count);
    }

    private static byte[] Deflate(byte[] bgra)
    {
        var rgb = new byte[bgra.Length / 4 * 3];
        for (int i = 0, j = 0; i < bgra.Length; i += 4, j += 3)
        {
            rgb[j] = bgra[i + 2];
            rgb[j + 1] = bgra[i + 1];
            rgb[j + 2] = bgra[i];
        }
        using var ms = new MemoryStream();
        using (var z = new ZLibStream(ms, CompressionLevel.Optimal, leaveOpen: true)) z.Write(rgb);
        return ms.ToArray();
    }

    /// <summary>Tekst PDF jako UTF-16BE (obsługuje polskie znaki).</summary>
    private static string PdfText(string s)
    {
        var sb = new StringBuilder("<FEFF");
        foreach (var c in s) sb.Append(((int)c).ToString("X4"));
        return sb.Append('>').ToString();
    }
}
