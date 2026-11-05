using System.Numerics;
using Kasetownik9000.Model;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Effects;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.Graphics.Canvas.Text;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Text;

namespace Kasetownik9000.Rendering;

public enum LabelWindowStyle { Editor, Preview, Print }

/// <summary>
/// Jeden renderer dla ekranu, PNG, PDF i druku — dzięki temu wydruk wygląda dokładnie jak podgląd.
/// Wszystkie współrzędne w milimetrach; transformacja sesji rysowania mapuje mm → DIP.
/// </summary>
public static class CardRenderer
{
    private static readonly CanvasStrokeStyle DashStyle = new() { DashStyle = CanvasDashStyle.Dash };
    private static readonly CanvasStrokeStyle RoundCaps = new() { StartCap = CanvasCapStyle.Round, EndCap = CanvasCapStyle.Round };
    private static readonly CanvasStrokeStyle DashRoundCaps = new()
    {
        DashStyle = CanvasDashStyle.Dash, DashCap = CanvasCapStyle.Round, StartCap = CanvasCapStyle.Round, EndCap = CanvasCapStyle.Round,
    };

    public static Rect CardRect(CardDocument doc) => new(0, 0, doc.Width, doc.Height);

    public static Rect BleedRect(CardDocument doc)
    {
        var b = Math.Max(0, doc.Bleed);
        return new Rect(-b, -b, doc.Width + 2 * b, doc.Height + 2 * b);
    }

    /// <param name="fields">Podstawia pola {tytuł} itd.; null = tekst bez zmian.</param>
    public static void DrawCard(CanvasDrawingSession ds, CardDocument doc, AssetStore assets, bool highQuality, TextResolver? fields)
    {
        var clip = BleedRect(doc);
        using (ds.CreateLayer(1f, clip))
        {
            ds.FillRectangle(clip, ColorUtil.Parse(doc.Background, Microsoft.UI.Colors.White));
            foreach (var e in doc.Elements)
            {
                if (!e.Hidden) DrawElement(ds, e, assets, highQuality, fields);
            }
        }
    }

    // ------------------------------------------------------------------ okienko naklejki

    private static readonly Color ShellColor = Color.FromArgb(255, 46, 42, 39);
    private static readonly Color ShellDark = Color.FromArgb(255, 24, 21, 19);
    private static readonly Color HubCream = Color.FromArgb(255, 236, 228, 212);
    private static readonly Color CutOrange = Color.FromArgb(230, 232, 114, 44);

    /// <summary>
    /// Okienka naklejek (otwór na szpule) rysowane na wierzchu karty.
    /// Editor — półprzezroczysta kaseta widoczna przez otwór + linia cięcia; Preview — jak na gotowej kasecie;
    /// Print — otwór biały (bez farby) z cienką linią cięcia i napisem „wytnij”.
    /// </summary>
    /// <param name="hairline">Grubość linii w mm (w edytorze: 1 piksel ekranu).</param>
    public static void DrawLabelWindows(CanvasDrawingSession ds, CardDocument doc, LabelWindowStyle style, float hairline = 0.2f)
    {
        float cr = (float)LabelWindow.CornerRadius;
        foreach (var w in LabelWindow.All(doc))
        {
            var r = new Rect(w.X, w.Y, w.W, w.H);
            if (style == LabelWindowStyle.Print)
            {
                ds.FillRoundedRectangle(r, cr, cr, Microsoft.UI.Colors.White);
                using var fmt = new CanvasTextFormat
                {
                    FontFamily = "Bahnschrift", FontSize = 2.3f,
                    HorizontalAlignment = CanvasHorizontalAlignment.Center, VerticalAlignment = CanvasVerticalAlignment.Center,
                };
                ds.DrawText("✂  W Y T N I J", r, Color.FromArgb(255, 190, 190, 190), fmt);
                var inner = new Rect(r.X + hairline / 2, r.Y + hairline / 2, r.Width - hairline, r.Height - hairline);
                ds.DrawRoundedRectangle(inner, cr, cr, Color.FromArgb(255, 128, 128, 128), hairline);
                continue;
            }

            using (var clip = CanvasGeometry.CreateRoundedRectangle(ds, r, cr, cr))
            using (ds.CreateLayer(style == LabelWindowStyle.Editor ? 0.82f : 1f, clip))
            {
                ds.FillRectangle(r, ShellColor);
                DrawReels(ds, (float)w.CenterX, (float)w.CenterY);
            }
            if (style == LabelWindowStyle.Editor)
                ds.DrawRoundedRectangle(r, cr, cr, CutOrange, hairline);
            else
                ds.DrawRoundedRectangle(r, cr, cr, Color.FromArgb(160, 0, 0, 0), hairline);
        }
    }

    /// <summary>Okienko z taśmą i dwie szpule kasety (środek między osiami szpul) — wspólne dla edytora i 3D.</summary>
    public static void DrawReels(CanvasDrawingSession ds, float cx, float cy)
    {
        float hub = (float)LabelWindow.HubOffset;
        var win = new Rect(cx - 14.2, cy - 5.5, 28.4, 11);
        ds.FillRoundedRectangle(win, 1.5f, 1.5f, ShellDark);
        using (var clip = CanvasGeometry.CreateRoundedRectangle(ds, win, 1.5f, 1.5f))
        using (ds.CreateLayer(1f, clip))
        {
            var tape = Color.FromArgb(255, 92, 58, 38);
            ds.FillCircle(cx - hub, cy, 15f, tape);
            ds.FillCircle(cx + hub, cy, 10.5f, tape);
            ds.FillRectangle(new Rect(win.X, win.Y, win.Width, 1.2), Color.FromArgb(50, 255, 255, 255));
        }
        foreach (var hx in new[] { cx - hub, cx + hub })
        {
            ds.FillCircle(hx, cy, 6.6f, ShellDark);
            ds.FillCircle(hx, cy, 4.6f, HubCream);
            ds.FillCircle(hx, cy, 3.1f, ShellDark);
            for (int k = 0; k < 6; k++)
            {
                double a = k * Math.PI / 3;
                ds.FillCircle(hx + (float)Math.Cos(a) * 3.1f, cy + (float)Math.Sin(a) * 3.1f, 0.75f, HubCream);
            }
        }
    }

    public static Matrix3x2 ElementTransform(CardElement e) =>
        Matrix3x2.CreateRotation((float)(e.Rotation * Math.PI / 180), new Vector2((float)e.CenterX, (float)e.CenterY));

    public static void DrawElement(CanvasDrawingSession ds, CardElement e, AssetStore assets, bool highQuality, TextResolver? fields)
    {
        var baseT = ds.Transform;
        if (e.Rotation != 0) ds.Transform = ElementTransform(e) * baseT;
        CanvasActiveLayer? layer = e.Opacity < 0.999 ? ds.CreateLayer((float)Math.Clamp(e.Opacity, 0, 1)) : null;
        try
        {
            switch (e)
            {
                case TextElement t: DrawText(ds, t, fields); break;
                case ImageElement i: DrawImage(ds, i, assets, highQuality); break;
                case ShapeElement s: DrawShape(ds, s); break;
                case StripesElement st: DrawStripes(ds, st); break;
            }
        }
        finally
        {
            layer?.Dispose();
            ds.Transform = baseT;
        }
    }

    // ------------------------------------------------------------------ tekst

    public static string DisplayText(TextElement t, TextResolver? fields)
    {
        var text = fields?.Resolve(t.Text) ?? t.Text;
        return t.Uppercase ? text.ToUpperInvariant() : text;
    }

    public static CanvasTextLayout CreateLayout(ICanvasResourceCreator rc, TextElement t, float sizeMm, string text)
    {
        using var fmt = new CanvasTextFormat
        {
            FontFamily = string.IsNullOrWhiteSpace(t.FontFamily) ? "Bahnschrift" : t.FontFamily,
            FontSize = Math.Max(0.05f, sizeMm),
            FontWeight = new FontWeight { Weight = (ushort)(t.Bold ? 700 : 400) },
            FontStyle = t.Italic ? FontStyle.Italic : FontStyle.Normal,
            HorizontalAlignment = t.Align switch
            {
                TextAlign.Center => CanvasHorizontalAlignment.Center,
                TextAlign.Right => CanvasHorizontalAlignment.Right,
                _ => CanvasHorizontalAlignment.Left,
            },
            VerticalAlignment = t.VAlign switch
            {
                TextVAlign.Middle => CanvasVerticalAlignment.Center,
                TextVAlign.Bottom => CanvasVerticalAlignment.Bottom,
                _ => CanvasVerticalAlignment.Top,
            },
            WordWrapping = CanvasWordWrapping.WholeWord,
            Options = CanvasDrawTextOptions.EnableColorFont,
        };
        if (Math.Abs(t.LineSpacing - 1) > 0.01)
        {
            fmt.LineSpacingMode = CanvasLineSpacingMode.Proportional;
            fmt.LineSpacing = (float)Math.Clamp(t.LineSpacing, 0.3, 5);
            fmt.LineSpacingBaseline = (float)Math.Clamp(t.LineSpacing, 0.3, 5);
        }
        var layout = new CanvasTextLayout(rc, text, fmt, (float)Math.Max(0.1, t.Width), (float)Math.Max(0.1, t.Height));
        if (Math.Abs(t.LetterSpacing) > 0.0001 && text.Length > 0)
        {
            float s = (float)(t.LetterSpacing * sizeMm);
            layout.SetCharacterSpacing(0, text.Length, s / 2, s / 2, 0);
        }
        return layout;
    }

    private static bool Fits(CanvasTextLayout l, TextElement t)
    {
        var b = l.LayoutBounds;
        return b.Width <= t.Width + 0.01 && b.Height <= t.Height + 0.01;
    }

    /// <summary>Rozmiar fontu (mm) po ewentualnym dopasowaniu do ramki.</summary>
    public static float EffectiveSizeMm(ICanvasResourceCreator rc, TextElement t, string text)
    {
        float size = (float)(t.FontSize * Units.MmPerPt);
        if (!t.AutoFit || string.IsNullOrEmpty(text)) return size;
        using (var full = CreateLayout(rc, t, size, text))
        {
            if (Fits(full, t)) return size;
        }
        float lo = size * 0.05f, hi = size;
        for (int i = 0; i < 12; i++)
        {
            float mid = (lo + hi) / 2;
            using var l = CreateLayout(rc, t, mid, text);
            if (Fits(l, t)) lo = mid; else hi = mid;
        }
        return lo;
    }

    private static void DrawText(CanvasDrawingSession ds, TextElement t, TextResolver? fields)
    {
        var text = DisplayText(t, fields);
        if (string.IsNullOrEmpty(text)) return;
        float size = EffectiveSizeMm(ds, t, text);
        using var layout = CreateLayout(ds, t, size, text);
        float x = (float)t.X, y = (float)t.Y;
        if (t.Shadow)
        {
            float o = (float)t.ShadowOffset;
            ds.DrawTextLayout(layout, x + o, y + o, ColorUtil.Parse(t.ShadowColor));
        }
        ds.DrawTextLayout(layout, x, y, ColorUtil.Parse(t.Color, Microsoft.UI.Colors.Black));
    }

    // ------------------------------------------------------------------ obraz

    private static void DrawImage(CanvasDrawingSession ds, ImageElement img, AssetStore assets, bool highQuality)
    {
        var dest = new Rect(img.X, img.Y, Math.Max(0.01, img.Width), Math.Max(0.01, img.Height));
        var bmp = string.IsNullOrEmpty(img.AssetId) ? null : assets.TryGetBitmap(img.AssetId);
        if (bmp is null)
        {
            DrawImagePlaceholder(ds, dest, assets.HasFailed(img.AssetId));
            return;
        }

        var size = bmp.Size;
        var src = new Rect(0, 0, size.Width, size.Height);
        double srcAspect = size.Width / Math.Max(1e-6, size.Height);
        double dstAspect = dest.Width / dest.Height;

        switch (img.Fit)
        {
            case ImageFit.Fill when srcAspect > dstAspect:
                {
                    double w = size.Height * dstAspect;
                    src = new Rect((size.Width - w) / 2, 0, w, size.Height);
                    break;
                }
            case ImageFit.Fill:
                {
                    double h = size.Width / dstAspect;
                    src = new Rect(0, (size.Height - h) / 2, size.Width, h);
                    break;
                }
            case ImageFit.Fit when srcAspect > dstAspect:
                {
                    double h = dest.Width / srcAspect;
                    dest = new Rect(dest.X, dest.Y + (dest.Height - h) / 2, dest.Width, h);
                    break;
                }
            case ImageFit.Fit:
                {
                    double w = dest.Height * srcAspect;
                    dest = new Rect(dest.X + (dest.Width - w) / 2, dest.Y, w, dest.Height);
                    break;
                }
        }

        var interp = highQuality ? CanvasImageInterpolation.HighQualityCubic : CanvasImageInterpolation.Linear;
        switch (img.Filter)
        {
            case ImageFilter.Grayscale:
                using (var fx = new GrayscaleEffect { Source = bmp })
                    ds.DrawImage(fx, dest, src, 1f, interp);
                break;
            case ImageFilter.Sepia:
                using (var fx = new SepiaEffect { Source = bmp, Intensity = 0.9f })
                    ds.DrawImage(fx, dest, src, 1f, interp);
                break;
            default:
                ds.DrawImage(bmp, dest, src, 1f, interp);
                break;
        }
    }

    private static void DrawImagePlaceholder(CanvasDrawingSession ds, Rect r, bool failed)
    {
        var bg = failed ? Color.FromArgb(60, 200, 64, 47) : Color.FromArgb(40, 128, 128, 128);
        var fg = failed ? Color.FromArgb(200, 200, 64, 47) : Color.FromArgb(120, 100, 100, 100);
        ds.FillRectangle(r, bg);
        float sw = 0.3f;
        ds.DrawRectangle(r, fg, sw);
        ds.DrawLine((float)r.Left, (float)r.Top, (float)r.Right, (float)r.Bottom, fg, sw);
        ds.DrawLine((float)r.Right, (float)r.Top, (float)r.Left, (float)r.Bottom, fg, sw);
    }

    // ------------------------------------------------------------------ kształty

    private static void DrawShape(CanvasDrawingSession ds, ShapeElement s)
    {
        var fill = ColorUtil.Parse(s.FillColor);
        var stroke = ColorUtil.Parse(s.StrokeColor);
        float sw = (float)Math.Max(0.01, s.StrokeWidth);
        var r = new Rect(s.X, s.Y, Math.Max(0.01, s.Width), Math.Max(0.01, s.Height));

        switch (s.Shape)
        {
            case ShapeKind.Line:
                {
                    float cy = (float)(s.Y + s.Height / 2);
                    ds.DrawLine((float)s.X, cy, (float)(s.X + s.Width), cy, stroke, sw, s.Dashed ? DashRoundCaps : RoundCaps);
                    break;
                }
            case ShapeKind.Ellipse:
                {
                    var c = new Vector2((float)(s.X + s.Width / 2), (float)(s.Y + s.Height / 2));
                    float rx = (float)s.Width / 2, ry = (float)s.Height / 2;
                    if (s.HasFill) ds.FillEllipse(c, rx, ry, fill);
                    if (s.HasStroke) ds.DrawEllipse(c, rx - sw / 2, ry - sw / 2, stroke, sw, s.Dashed ? DashStyle : null);
                    break;
                }
            default:
                {
                    float cr = (float)Math.Max(0, Math.Min(s.CornerRadius, Math.Min(s.Width, s.Height) / 2));
                    if (s.HasFill)
                    {
                        if (cr > 0) ds.FillRoundedRectangle(r, cr, cr, fill);
                        else ds.FillRectangle(r, fill);
                    }
                    if (s.HasStroke)
                    {
                        var inner = new Rect(r.X + sw / 2, r.Y + sw / 2, Math.Max(0.01, r.Width - sw), Math.Max(0.01, r.Height - sw));
                        float icr = Math.Max(0, cr - sw / 2);
                        ds.DrawRoundedRectangle(inner, icr, icr, stroke, sw, s.Dashed ? DashStyle : null);
                    }
                    break;
                }
        }
    }

    private static void DrawStripes(CanvasDrawingSession ds, StripesElement s)
    {
        int n = s.Colors.Count;
        if (n == 0) return;
        double gap = Math.Max(0, s.Gap);
        double total = s.Vertical ? s.Width : s.Height;
        double band = (total - gap * (n - 1)) / n;
        if (band <= 0.01) { gap = 0; band = total / n; }
        const double seam = 0.02; // lekka zakładka, żeby nie było jasnych szczelin między paskami
        for (int i = 0; i < n; i++)
        {
            var color = ColorUtil.Parse(s.Colors[i]);
            double pos = i * (band + gap);
            double extra = gap == 0 && i < n - 1 ? seam : 0;
            var r = s.Vertical
                ? new Rect(s.X + pos, s.Y, band + extra, s.Height)
                : new Rect(s.X, s.Y + pos, s.Width, band + extra);
            ds.FillRectangle(r, color);
        }
    }
}
