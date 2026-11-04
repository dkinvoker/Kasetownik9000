using System.Numerics;
using Kasetownik9000.Model;
using Kasetownik9000.Rendering;
using Microsoft.Graphics.Canvas;
using Windows.Foundation;

namespace Kasetownik9000.Output;

public enum PaperSize { A4, Letter }

public sealed class PrintOptions
{
    public bool CropMarks { get; set; } = true;
    public bool FoldMarks { get; set; } = true;
    public bool FillPage { get; set; }
    public PaperSize Paper { get; set; } = PaperSize.A4;
    public int Dpi { get; set; } = 600;
    public bool IncludeBleed { get; set; }

    public (double W, double H) PaperMm => Paper == PaperSize.Letter ? (215.9, 279.4) : (210, 297);
}

public readonly record struct MarkLine(double X1, double Y1, double X2, double Y2, bool Fold);

/// <summary>Rozmieszczenie kart na arkuszu + znaczniki cięcia i zgięcia (wszystko w mm).</summary>
public sealed class SheetLayout
{
    public double PageWidth { get; init; }
    public double PageHeight { get; init; }
    public List<Point> Cards { get; } = [];
    public List<MarkLine> Marks { get; } = [];

    public static SheetLayout Compute(double pageW, double pageH, CardDocument doc, PrintOptions opt, double margin = 8)
    {
        double w = doc.Width, h = doc.Height, b = Math.Max(0, doc.Bleed);
        double spacing = Math.Max(14, 2 * b + 12);
        int cols = 1, rows = 1;
        if (opt.FillPage)
        {
            cols = Math.Max(1, (int)Math.Floor((pageW - 2 * margin + spacing) / (w + spacing)));
            rows = Math.Max(1, (int)Math.Floor((pageH - 2 * margin + spacing) / (h + spacing)));
        }
        double blockW = cols * w + (cols - 1) * spacing, blockH = rows * h + (rows - 1) * spacing;
        double x0 = (pageW - blockW) / 2, y0 = (pageH - blockH) / 2;
        if (!opt.FillPage) y0 = Math.Min(y0, Math.Max(margin + 10, 20)); // jedna karta: u góry arkusza

        var layout = new SheetLayout { PageWidth = pageW, PageHeight = pageH };
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                layout.Cards.Add(new Point(x0 + c * (w + spacing), y0 + r * (h + spacing)));

        double gap = b + 1.5;
        double len = Math.Clamp(spacing / 2 - gap - 0.5, 2, 6);
        foreach (var p in layout.Cards)
        {
            double x = p.X, y = p.Y;
            if (opt.CropMarks)
            {
                foreach (var cx in new[] { x, x + w })
                {
                    layout.Marks.Add(new(cx, y - gap - len, cx, y - gap, false));
                    layout.Marks.Add(new(cx, y + h + gap, cx, y + h + gap + len, false));
                }
                foreach (var cy in new[] { y, y + h })
                {
                    layout.Marks.Add(new(x - gap - len, cy, x - gap, cy, false));
                    layout.Marks.Add(new(x + w + gap, cy, x + w + gap + len, cy, false));
                }
            }
            double top = 0;
            for (int i = 0; i < doc.Panels.Count - 1; i++)
            {
                top += doc.Panels[i].Height;
                bool cut = doc.Panels[i].CutAfter;
                if (cut ? !opt.CropMarks : !opt.FoldMarks) continue;
                double yy = y + top;
                layout.Marks.Add(new(x - gap - len, yy, x - gap, yy, !cut));
                layout.Marks.Add(new(x + w + gap, yy, x + w + gap + len, yy, !cut));
            }
        }
        return layout;
    }
}

public static class CardRasterizer
{
    /// <summary>Renderuje kartę (z opcjonalnym spadem) do bitmapy o zadanej rozdzielczości.</summary>
    public static async Task<CanvasRenderTarget> RenderAsync(Editor.EditorSession s, double dpi, bool includeBleed)
    {
        await s.Assets.EnsureLoadedAsync(s.UsedAssetIds());
        var doc = s.Doc;
        double b = includeBleed ? Math.Max(0, doc.Bleed) : 0;
        double wMm = doc.Width + 2 * b, hMm = doc.Height + 2 * b;
        var dev = CanvasDevice.GetSharedDevice();

        // nie przekraczaj maksymalnego rozmiaru tekstury
        int max = dev.MaximumBitmapSizeInPixels;
        double maxDpi = Math.Min(max / (wMm / 25.4), max / (hMm / 25.4)) * 0.98;
        dpi = Math.Min(dpi, maxDpi);

        var rt = new CanvasRenderTarget(dev, (float)(wMm * Units.DipPerMm), (float)(hMm * Units.DipPerMm), (float)dpi);
        using (var ds = rt.CreateDrawingSession())
        {
            ds.Clear(Microsoft.UI.Colors.White);
            ds.TextAntialiasing = Microsoft.Graphics.Canvas.Text.CanvasTextAntialiasing.Grayscale;
            ds.Transform = Matrix3x2.CreateScale((float)Units.DipPerMm) *
                           Matrix3x2.CreateTranslation((float)(b * Units.DipPerMm), (float)(b * Units.DipPerMm));
            CardRenderer.DrawCard(ds, doc, s.Assets, highQuality: true);
        }
        return rt;
    }
}
