using System.Numerics;
using Kasetownik9000.Model;
using Kasetownik9000.Rendering;
using Microsoft.Graphics.Canvas;

namespace Kasetownik9000.Output;

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
            // naklejki: okienko na szpule zostaje białe, z linią cięcia
            CardRenderer.DrawLabelWindows(ds, card, LabelWindowStyle.Print);
        }
        return rt;
    }

    /// <summary>
    /// Które karty drukować (na przodzie arkusza): cały komplet albo tylko aktywna. Wnętrza nie są osobnymi
    /// kartami — idą na odwrocie swojej karty; gdy aktywne jest wnętrze, drukujemy jego kartę zewnętrzną.
    /// </summary>
    public static List<int> CardsToPrint(Editor.EditorSession s, PrintOptions opt)
    {
        var cards = s.Project.Cards;
        if (opt.AllCards) return [.. Enumerable.Range(0, cards.Count).Where(i => !cards[i].IsInside)];
        var outer = s.Doc.IsInside ? s.Project.OuterOf(s.Doc) : null;
        return [outer is null ? s.ActiveIndex : cards.IndexOf(outer)];
    }

    /// <summary>Obraca bitmapę BGRA o 180° (odwrocie przy obracaniu kartki wzdłuż krótszej krawędzi).</summary>
    public static void Rotate180(byte[] bgra) =>
        System.Runtime.InteropServices.MemoryMarshal.Cast<byte, uint>(bgra.AsSpan()).Reverse();
}
