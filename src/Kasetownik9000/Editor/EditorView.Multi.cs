using System.Numerics;
using Kasetownik9000.Model;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Input;
using Windows.Foundation;
using Windows.UI;

namespace Kasetownik9000.Editor;

/// <summary>Płótno: zaznaczanie kilku elementów (Shift/Ctrl + klik, prostokąt) i przekształcanie ich razem.</summary>
public sealed partial class EditorView
{
    /// <summary>Stan elementów z chwili rozpoczęcia przeciągania — każdy ruch liczony jest od niego (bez kumulacji błędów).</summary>
    private readonly Dictionary<CardElement, CardElement> _setStart = [];
    private MmRect _setBox;
    private Vector2 _setAnchor, _setGrab;

    private Vector2 _marqueeEnd;
    private List<CardElement> _marqueeBase = [];

    private static readonly Color MarqueeFill = Color.FromArgb(28, 243, 233, 210);

    // ------------------------------------------------------------------ początek przeciągania

    private void BeginSetDrag(EditorSession s, DragMode mode, PointerRoutedEventArgs e)
    {
        _mode = mode;
        _setStart.Clear();
        foreach (var el in s.MovableSelection()) _setStart[el] = s.CloneElement(el);
        _setBox = ElementOps.Bounds(_setStart.Keys);
        if (mode == DragMode.SetScale)
        {
            // narożnik przeciwległy do chwyconego stoi w miejscu
            var corners = BoxCorners(_setBox);
            int nearest = 0;
            for (int i = 1; i < 4; i++)
                if (Vector2.Distance(corners[i], _dragStartWorld) < Vector2.Distance(corners[nearest], _dragStartWorld)) nearest = i;
            _setGrab = corners[nearest];
            _setAnchor = corners[(nearest + 2) % 4];
        }
        _canvas.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private static Vector2[] BoxCorners(MmRect b) =>
    [
        new((float)b.X, (float)b.Y), new((float)b.Right, (float)b.Y),
        new((float)b.Right, (float)b.Bottom), new((float)b.X, (float)b.Bottom),
    ];

    /// <summary>Uchwyty wspólnej ramki: narożniki (skalowanie) i kółko nad ramką (obrót).</summary>
    private DragMode? SetHandleAt(EditorSession s, Vector2 screen)
    {
        if (s.MovableSelection().Count == 0) return null;
        var box = ElementOps.Bounds(s.Selection.Where(e => !e.Hidden));
        if (box.W <= 0 && box.H <= 0) return null;
        if (Vector2.Distance(SetRotateHandleScreen(box), screen) <= HandleRadius + 1) return DragMode.SetRotate;
        foreach (var c in BoxCorners(box))
            if (Vector2.Distance(ToScreen(c), screen) <= HandleRadius) return DragMode.SetScale;
        return null;
    }

    private Vector2 SetRotateHandleScreen(MmRect box) =>
        ToScreen(box.CenterX, box.Y) - new Vector2(0, RotateHandleDistance);

    // ------------------------------------------------------------------ przesuwanie / skalowanie / obrót

    private void DoSetMove(Vector2 world, bool shift, bool alt)
    {
        var d = world - _dragStartWorld;
        double dx = d.X, dy = d.Y;
        if (shift)
        {
            if (Math.Abs(dx) > Math.Abs(dy)) dy = 0; else dx = 0;
        }

        // przyciąganie całej ramki zaznaczenia do krawędzi paneli, środka karty i innych elementów
        _guides.Clear();
        if (!alt)
        {
            double thr = 6 / _zoom;
            var (xs, ys) = SnapTargets(_setStart.Keys);
            var b = _setBox;
            if (!(shift && dx == 0))
            {
                var sx = BestSnap([b.X + dx, b.CenterX + dx, b.Right + dx], xs, thr, out var gx);
                if (sx is not null) { dx += sx.Value; _guides.Add((true, gx)); }
            }
            if (!(shift && dy == 0))
            {
                var sy = BestSnap([b.Y + dy, b.CenterY + dy, b.Bottom + dy], ys, thr, out var gy);
                if (sy is not null) { dy += sy.Value; _guides.Add((false, gy)); }
            }
        }

        foreach (var (el, start) in _setStart)
        {
            el.X = Math.Round(start.X + dx, 2);
            el.Y = Math.Round(start.Y + dy, 2);
        }
    }

    private void DoSetScale(Vector2 world)
    {
        // skala jednolita: rzut wektora kotwica→kursor na wektor kotwica→chwycony narożnik
        var v0 = _setGrab - _setAnchor;
        var v1 = world - _setAnchor;
        float len2 = v0.LengthSquared();
        if (len2 < 1e-6f) return;
        double s = Math.Max(0.05, Vector2.Dot(v1, v0) / len2);
        foreach (var (el, start) in _setStart) ElementOps.ApplyScale(el, start, _setAnchor.X, _setAnchor.Y, s);
        _guides.Clear();
    }

    private void DoSetRotate(Vector2 world, bool shift)
    {
        double cx = _setBox.CenterX, cy = _setBox.CenterY;
        double a0 = Math.Atan2(_dragStartWorld.Y - cy, _dragStartWorld.X - cx);
        double a1 = Math.Atan2(world.Y - cy, world.X - cx);
        double deg = (a1 - a0) * 180 / Math.PI;
        if (shift) deg = Math.Round(deg / 15) * 15;
        foreach (var (el, start) in _setStart) ElementOps.ApplyRotation(el, start, cx, cy, deg);
        _guides.Clear();
    }

    // ------------------------------------------------------------------ zaznaczanie prostokątem

    private void BeginMarquee(EditorSession s, Vector2 world, bool additive, PointerRoutedEventArgs e)
    {
        _mode = DragMode.Marquee;
        _marqueeEnd = world;
        _marqueeBase = additive ? [.. s.Selection] : [];
        if (!additive) s.Select(null);
        _canvas.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private MmRect MarqueeRect()
    {
        double x = Math.Min(_dragStartWorld.X, _marqueeEnd.X), y = Math.Min(_dragStartWorld.Y, _marqueeEnd.Y);
        return new MmRect(x, y, Math.Abs(_marqueeEnd.X - _dragStartWorld.X), Math.Abs(_marqueeEnd.Y - _dragStartWorld.Y));
    }

    /// <summary>Zaznacza elementy, których obrys dotyka prostokąta (całe grupy); zablokowane i ukryte pomija.</summary>
    private void UpdateMarquee(EditorSession s, Vector2 world)
    {
        _marqueeEnd = world;
        var r = MarqueeRect();
        var hits = new List<CardElement>(_marqueeBase);
        if (r.W > 0.2 || r.H > 0.2)
        {
            foreach (var el in s.Doc.Elements)
            {
                if (el.Hidden || el.Locked) continue;
                var b = ElementOps.Bounds(el);
                if (b.X <= r.Right && b.Right >= r.X && b.Y <= r.Bottom && b.Bottom >= r.Y) hits.Add(el);
            }
        }
        s.SetSelection(ElementOps.ExpandToGroups(s.Doc, hits));
        UpdateCursor(InputSystemCursorShape.Cross);
        _canvas.Invalidate();
    }

    // ------------------------------------------------------------------ rysowanie

    private void DrawMarquee(CanvasDrawingSession ds)
    {
        var r = MarqueeRect();
        var a = ToScreen(r.X, r.Y);
        var b = ToScreen(r.Right, r.Bottom);
        var rect = new Rect(a.X, a.Y, b.X - a.X, b.Y - a.Y);
        ds.FillRectangle(rect, MarqueeFill);
        ds.DrawRectangle(rect, Cream, 1, _dash);
    }

    /// <summary>Kilka zaznaczonych: obrys każdego + wspólna ramka z uchwytami skalowania i obrotu.</summary>
    private void DrawMultiSelection(CanvasDrawingSession ds, EditorSession s)
    {
        var visible = s.Selection.Where(e => !e.Hidden).ToList();
        if (visible.Count == 0) return;
        foreach (var el in visible) DrawOutline(ds, el, Color.FromArgb(220, 232, 114, 44), false);

        var box = ElementOps.Bounds(visible);
        var tl = ToScreen(box.X, box.Y);
        var br = ToScreen(box.Right, box.Bottom);
        var rect = new Rect(tl.X - 3, tl.Y - 3, br.X - tl.X + 6, br.Y - tl.Y + 6);
        ds.DrawRectangle(rect, Color.FromArgb(160, 0, 0, 0), 3);
        ds.DrawRectangle(rect, Orange, 1.5f, _dash);

        // nazwa grupy nad ramką
        if (s.SelectedGroup is { } g)
        {
            var label = new Rect(rect.X, rect.Y - 20, Math.Max(60, g.Name.Length * 7 + 16), 17);
            ds.FillRoundedRectangle(label, 3, 3, Orange);
            ds.DrawText(g.Name.ToUpperInvariant(), label, Workbench, _dimFormat);
        }

        if (s.MovableSelection().Count == 0) return;
        var rh = SetRotateHandleScreen(box);
        var topMid = ToScreen(box.CenterX, box.Y);
        ds.DrawLine(topMid, rh, Orange, 1.5f);
        ds.FillCircle(rh, 6, Cream);
        ds.DrawCircle(rh, 6, Orange, 2);
        foreach (var c in BoxCorners(box))
        {
            var p = ToScreen(c);
            var r = new Rect(p.X - 4.5, p.Y - 4.5, 9, 9);
            ds.FillRectangle(r, Cream);
            ds.DrawRectangle(r, Orange, 2);
        }
    }

    /// <summary>Kursor nad uchwytami wspólnej ramki.</summary>
    private bool UpdateSetHoverCursor(EditorSession s, Vector2 pos)
    {
        if (s.Selection.Count < 2) return false;
        switch (SetHandleAt(s, pos))
        {
            case DragMode.SetRotate:
                UpdateCursor(InputSystemCursorShape.Cross);
                SetHover(null);
                return true;
            case DragMode.SetScale:
                UpdateCursor(InputSystemCursorShape.SizeNorthwestSoutheast);
                SetHover(null);
                return true;
            default:
                return false;
        }
    }
}
