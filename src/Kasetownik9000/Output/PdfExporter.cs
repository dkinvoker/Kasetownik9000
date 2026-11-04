using System.Globalization;
using System.IO.Compression;
using System.Text;
using Kasetownik9000.Editor;
using Microsoft.Graphics.Canvas;

namespace Kasetownik9000.Output;

/// <summary>Eksport do PDF (arkusz A4/Letter, karta w skali 1:1 + znaczniki) i PNG.</summary>
public static class Exporter
{
    public static async Task ExportPngAsync(string path, EditorSession s, PrintOptions opt)
    {
        using var rt = await CardRasterizer.RenderAsync(s, opt.Dpi, opt.IncludeBleed);
        await rt.SaveAsync(path, CanvasBitmapFileFormat.Png);
    }

    public static async Task<int> ExportPdfAsync(string path, EditorSession s, PrintOptions opt)
    {
        var doc = s.Doc;
        var (pageW, pageH) = opt.PaperMm;
        var layout = SheetLayout.Compute(pageW, pageH, doc, opt);

        using var rt = await CardRasterizer.RenderAsync(s, opt.Dpi, includeBleed: true);
        int w = (int)rt.SizeInPixels.Width, h = (int)rt.SizeInPixels.Height;
        byte[] bgra = rt.GetPixelBytes();

        byte[] imageData = await Task.Run(() =>
        {
            var rgb = new byte[w * h * 3];
            for (int i = 0, j = 0; i < bgra.Length; i += 4, j += 3)
            {
                rgb[j] = bgra[i + 2];
                rgb[j + 1] = bgra[i + 1];
                rgb[j + 2] = bgra[i];
            }
            using var ms = new MemoryStream();
            using (var z = new ZLibStream(ms, CompressionLevel.Optimal, leaveOpen: true)) z.Write(rgb);
            return ms.ToArray();
        });

        const double pt = 72 / 25.4;
        var inv = CultureInfo.InvariantCulture;
        string F(double v) => v.ToString("0.###", inv);

        double b = Math.Max(0, doc.Bleed);
        double cw = (doc.Width + 2 * b) * pt, ch = (doc.Height + 2 * b) * pt;
        var content = new StringBuilder();
        foreach (var p in layout.Cards)
        {
            double x = (p.X - b) * pt;
            double y = (pageH - (p.Y - b) - (doc.Height + 2 * b)) * pt;
            content.Append($"q {F(cw)} 0 0 {F(ch)} {F(x)} {F(y)} cm /Im0 Do Q\n");
        }
        foreach (var fold in new[] { false, true })
        {
            content.Append(fold ? "0.5 G 0.5 w\n" : "0 G 0.5 w\n");
            foreach (var m in layout.Marks.Where(m => m.Fold == fold))
                content.Append($"{F(m.X1 * pt)} {F((pageH - m.Y1) * pt)} m {F(m.X2 * pt)} {F((pageH - m.Y2) * pt)} l S\n");
        }
        byte[] contentBytes = Encoding.ASCII.GetBytes(content.ToString());

        await using var fs = File.Create(path);
        var offsets = new List<long>();
        void W(string str) { var bytes = Encoding.Latin1.GetBytes(str); fs.Write(bytes); }
        void Obj(string body)
        {
            offsets.Add(fs.Position);
            W($"{offsets.Count} 0 obj\n{body}\nendobj\n");
        }

        W("%PDF-1.4\n%âãÏÓ\n");
        Obj("<< /Type /Catalog /Pages 2 0 R >>");
        Obj("<< /Type /Pages /Kids [3 0 R] /Count 1 >>");
        Obj($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {F(pageW * pt)} {F(pageH * pt)}] " +
            "/Resources << /XObject << /Im0 4 0 R >> >> /Contents 5 0 R >>");

        offsets.Add(fs.Position);
        W($"4 0 obj\n<< /Type /XObject /Subtype /Image /Width {w} /Height {h} /ColorSpace /DeviceRGB " +
          $"/BitsPerComponent 8 /Filter /FlateDecode /Length {imageData.Length} >>\nstream\n");
        fs.Write(imageData);
        W("\nendstream\nendobj\n");

        offsets.Add(fs.Position);
        W($"5 0 obj\n<< /Length {contentBytes.Length} >>\nstream\n");
        fs.Write(contentBytes);
        W("\nendstream\nendobj\n");

        Obj($"<< /Title {PdfText($"Kasetownik 9000 – {s.DisplayName}")} /Producer {PdfText("Kasetownik 9000")} " +
            $"/CreationDate (D:{DateTime.Now:yyyyMMddHHmmss}) >>");

        long xref = fs.Position;
        var sb = new StringBuilder();
        sb.Append($"xref\n0 {offsets.Count + 1}\n0000000000 65535 f \n");
        foreach (var o in offsets) sb.Append($"{o:D10} 00000 n \n");
        sb.Append($"trailer\n<< /Size {offsets.Count + 1} /Root 1 0 R /Info {offsets.Count} 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        W(sb.ToString());
        return layout.Cards.Count;
    }

    /// <summary>Tekst PDF jako UTF-16BE (obsługuje polskie znaki).</summary>
    private static string PdfText(string s)
    {
        var sb = new StringBuilder("<FEFF");
        foreach (var c in s) sb.Append(((int)c).ToString("X4"));
        return sb.Append('>').ToString();
    }
}
