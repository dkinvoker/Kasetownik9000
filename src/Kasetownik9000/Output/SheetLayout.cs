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
    /// <summary>Cały komplet (wszystkie karty projektu) zamiast tylko bieżącej karty.</summary>
    public bool AllCards { get; set; } = true;
    /// <summary>Uwzględnij kalibrację drukarki z ustawień (druk zawsze, PDF — opcjonalnie).</summary>
    public bool ApplyCalibration { get; set; }

    public (double W, double H) PaperMm => Paper == PaperSize.Letter ? (215.9, 279.4) : (210, 297);
}

public readonly record struct MarkLine(double X1, double Y1, double X2, double Y2, bool Fold);

public sealed record PlacedCard(int CardIndex, Point Position);

public sealed class SheetPage
{
    public List<PlacedCard> Cards { get; } = [];
    public List<MarkLine> Marks { get; } = [];
}

/// <summary>Rozmieszczenie kart na arkuszach + znaczniki cięcia i zgięcia (wszystko w mm).</summary>
public sealed class SheetLayout
{
    public double PageWidth { get; init; }
    public double PageHeight { get; init; }
    public List<SheetPage> Pages { get; } = [];

    public int CardCount => Pages.Sum(p => p.Cards.Count);

    private static double Spacing(CardDocument c) => Math.Max(14, 2 * Math.Max(0, c.Bleed) + 12);

    public static SheetLayout Compute(double pageW, double pageH, IReadOnlyList<CardDocument> cards,
        IReadOnlyList<int> indices, PrintOptions opt, double margin = 8)
    {
        var layout = new SheetLayout { PageWidth = pageW, PageHeight = pageH };
        if (indices.Count == 0) return layout;
        double top = Math.Max(margin + 10, 20);

        if (indices.Count == 1 && opt.FillPage)
        {
            // jedna karta powielona tyle razy, ile się zmieści
            var c = cards[indices[0]];
            double w = c.Width, h = c.Height, spacing = Spacing(c);
            int cols = Math.Max(1, (int)Math.Floor((pageW - 2 * margin + spacing) / (w + spacing)));
            int rows = Math.Max(1, (int)Math.Floor((pageH - 2 * margin + spacing) / (h + spacing)));
            double blockW = cols * w + (cols - 1) * spacing, blockH = rows * h + (rows - 1) * spacing;
            double x0 = (pageW - blockW) / 2, y0 = (pageH - blockH) / 2;
            var page = new SheetPage();
            for (int r = 0; r < rows; r++)
                for (int col = 0; col < cols; col++)
                    page.Cards.Add(new PlacedCard(indices[0], new Point(x0 + col * (w + spacing), y0 + r * (h + spacing))));
            layout.Pages.Add(page);
        }
        else
        {
            // półki: karty w wierszach od lewej, wiersze od góry, nowy arkusz gdy brak miejsca
            double spacing = indices.Max(i => Spacing(cards[i]));
            double usableW = pageW - 2 * margin, bottom = pageH - margin;
            var rows = new List<List<int>>();
            foreach (var i in indices)
            {
                var row = rows.LastOrDefault();
                double rowW = row is null ? 0 : row.Sum(j => cards[j].Width) + spacing * row.Count;
                if (row is null || rowW + cards[i].Width > usableW) rows.Add([i]);
                else row.Add(i);
            }

            var pageRows = new List<List<List<int>>>();
            double y = top;
            foreach (var row in rows)
            {
                double rowH = row.Max(j => cards[j].Height);
                if (pageRows.Count == 0 || (y + rowH > bottom && y > top))
                {
                    pageRows.Add([]);
                    y = top;
                }
                pageRows[^1].Add(row);
                y += rowH + spacing;
            }

            foreach (var pr in pageRows)
            {
                var page = new SheetPage();
                double py = top;
                foreach (var row in pr)
                {
                    double rowW = row.Sum(j => cards[j].Width) + spacing * (row.Count - 1);
                    double px = Math.Max(margin, (pageW - rowW) / 2);
                    foreach (var j in row)
                    {
                        page.Cards.Add(new PlacedCard(j, new Point(px, py)));
                        px += cards[j].Width + spacing;
                    }
                    py += row.Max(j => cards[j].Height) + spacing;
                }
                layout.Pages.Add(page);
            }
        }

        foreach (var page in layout.Pages)
            foreach (var pc in page.Cards)
                AddMarks(page, cards[pc.CardIndex], pc.Position, opt);
        return layout;
    }

    private static void AddMarks(SheetPage page, CardDocument card, Point p, PrintOptions opt)
    {
        double w = card.Width, h = card.Height, b = Math.Max(0, card.Bleed);
        double gap = b + 1.5;
        double len = Math.Clamp(Spacing(card) / 2 - gap - 0.5, 2, 6);
        double x = p.X, y = p.Y;
        if (opt.CropMarks)
        {
            foreach (var cx in new[] { x, x + w })
            {
                page.Marks.Add(new(cx, y - gap - len, cx, y - gap, false));
                page.Marks.Add(new(cx, y + h + gap, cx, y + h + gap + len, false));
            }
            foreach (var cy in new[] { y, y + h })
            {
                page.Marks.Add(new(x - gap - len, cy, x - gap, cy, false));
                page.Marks.Add(new(x + w + gap, cy, x + w + gap + len, cy, false));
            }
        }
        double t = 0;
        for (int i = 0; i < card.Panels.Count - 1; i++)
        {
            t += card.Panels[i].Height;
            bool cut = card.Panels[i].CutAfter;
            if (cut ? !opt.CropMarks : !opt.FoldMarks) continue;
            double yy = y + t;
            page.Marks.Add(new(x - gap - len, yy, x - gap, yy, !cut));
            page.Marks.Add(new(x + w + gap, yy, x + w + gap + len, yy, !cut));
        }
    }
}

public static class CardRasterizer
{
    /// <summary>Renderuje kartę (z opcjonalnym spadem) do bitmapy o zadanej rozdzielczości.</summary>
    public static async Task<CanvasRenderTarget> RenderAsync(Editor.EditorSession s, CardDocument card, double dpi, bool includeBleed)
    {
        await s.Assets.EnsureLoadedAsync(s.UsedAssetIds());
        double b = includeBleed ? Math.Max(0, card.Bleed) : 0;
        double wMm = card.Width + 2 * b, hMm = card.Height + 2 * b;
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
            CardRenderer.DrawCard(ds, card, s.Assets, highQuality: true, s.Resolver);
        }
        return rt;
    }

    /// <summary>Które karty drukować: cały komplet albo tylko aktywna.</summary>
    public static List<int> CardsToPrint(Editor.EditorSession s, PrintOptions opt) =>
        opt.AllCards ? [.. Enumerable.Range(0, s.Project.Cards.Count)] : [s.ActiveIndex];
}
