using System.Numerics;
using Kasetownik9000.Model;
using Kasetownik9000.Rendering;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.Graphics.Canvas.Text;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.System;
using Windows.UI;

namespace Kasetownik9000.Editor;

/// <summary>Płótno edytora: rysuje kartę i obsługuje przeciąganie, skalowanie, obracanie, zoom i przesuwanie widoku.</summary>
public sealed partial class EditorView : UserControl
{
    private const float MinZoom = 0.6f, MaxZoom = 60f;
    private const float HandleRadius = 7f;
    private const float RotateHandleDistance = 26f;

    private static readonly Color Workbench = Color.FromArgb(255, 30, 25, 22);
    private static readonly Color Cream = Color.FromArgb(255, 243, 233, 210);
    private static readonly Color Orange = Color.FromArgb(255, 232, 114, 44);
    private static readonly Color Teal = Color.FromArgb(255, 62, 142, 135);
    private static readonly Color Magenta = Color.FromArgb(255, 255, 79, 163);

    private readonly CanvasControl _canvas;
    private readonly CanvasStrokeStyle _dash = new() { DashStyle = CanvasDashStyle.Dash };
    private readonly CanvasTextFormat _labelFormat = new()
    {
        FontFamily = "Bahnschrift", FontSize = 12, FontWeight = new Windows.UI.Text.FontWeight { Weight = 600 },
        HorizontalAlignment = CanvasHorizontalAlignment.Right, VerticalAlignment = CanvasVerticalAlignment.Center,
        WordWrapping = CanvasWordWrapping.NoWrap,
    };
    private readonly CanvasTextFormat _dimFormat = new()
    {
        FontFamily = "Bahnschrift", FontSize = 11,
        HorizontalAlignment = CanvasHorizontalAlignment.Center, VerticalAlignment = CanvasVerticalAlignment.Center,
        WordWrapping = CanvasWordWrapping.NoWrap,
    };

    private readonly CanvasTextFormat _iconFormat = new()
    {
        FontFamily = "Segoe Fluent Icons", FontSize = 11,
        HorizontalAlignment = CanvasHorizontalAlignment.Center, VerticalAlignment = CanvasVerticalAlignment.Center,
    };

    private EditorSession? _session;
    private float _zoom = 4f;
    private Vector2 _pan = new(120, 60);
    private bool _needsFit = true;

    private enum DragMode { None, Move, Resize, Rotate, Pan }
    private DragMode _mode;
    private Vector2 _dragStartScreen, _dragStartWorld, _panStart;
    private double _startX, _startY, _startW, _startH, _startRot;
    private int _hx, _hy;
    private bool _dragEdited;
    private bool _spaceDown;
    private CardElement? _hover;
    private readonly List<(bool Vertical, double Pos)> _guides = [];
    private InputSystemCursorShape _cursorShape = InputSystemCursorShape.Arrow;

    public event Action<Vector2?>? PointerWorldMoved;
    public event Action? ZoomChanged;
    public event Action<CardElement>? EditTextRequested;
    public event Action<Point, CardElement?>? ContextMenuRequested;
    public event Action<IReadOnlyList<string>, Vector2>? FilesDropped;

    public EditorView()
    {
        _canvas = new CanvasControl { ClearColor = Workbench };
        _canvas.Draw += OnDraw;
        _canvas.PointerPressed += OnPointerPressed;
        _canvas.PointerMoved += OnPointerMoved;
        _canvas.PointerReleased += OnPointerReleased;
        _canvas.PointerCaptureLost += (_, _) => EndDrag();
        _canvas.PointerExited += (_, _) => { if (_mode == DragMode.None) { SetHover(null); PointerWorldMoved?.Invoke(null); } };
        _canvas.PointerWheelChanged += OnWheel;
        _canvas.DoubleTapped += OnDoubleTapped;
        _canvas.SizeChanged += (_, _) => { if (_needsFit) FitToView(); else _canvas.Invalidate(); };
        Content = _canvas;

        IsTabStop = true;
        UseSystemFocusVisuals = false;
        AllowDrop = true;
        DragOver += OnDragOver;
        Drop += OnDrop;
        KeyDown += (_, e) => { if (e.Key == VirtualKey.Space) { _spaceDown = true; UpdateCursor(InputSystemCursorShape.Hand); } };
        KeyUp += (_, e) => { if (e.Key == VirtualKey.Space) { _spaceDown = false; UpdateCursor(InputSystemCursorShape.Arrow); } };
    }

    public EditorSession? Session
    {
        get => _session;
        set
        {
            if (_session is not null) _session.Changed -= OnSessionChanged;
            _session = value;
            if (_session is not null) _session.Changed += OnSessionChanged;
            _needsFit = true;
            _hover = null;
            FitToView();
        }
    }

    private void OnSessionChanged(ChangeFlags f)
    {
        if (_hover is not null && _session?.Doc.Elements.Contains(_hover) != true) _hover = null;
        _canvas.Invalidate();
    }

    public void Invalidate() => _canvas.Invalidate();

    /// <summary>Usuwa kontrolkę Win2D z drzewa (zalecane przy zamykaniu okna).</summary>
    public void Shutdown() => _canvas.RemoveFromVisualTree();

    // ------------------------------------------------------------------ widok / zoom

    public double ZoomPercent => _zoom / Units.DipPerMm * 100;

    private Vector2 ToWorld(Vector2 p) => (p - _pan) / _zoom;
    private Vector2 ToScreen(Vector2 w) => w * _zoom + _pan;
    private Vector2 ToScreen(double x, double y) => ToScreen(new Vector2((float)x, (float)y));

    public void FitToView()
    {
        var size = _canvas.ActualSize;
        if (_session is null || size.X < 50 || size.Y < 50)
        {
            _needsFit = true;
            return;
        }
        var doc = _session.Doc;
        const float ml = 130, mr = 40, mt = 64, mb = 36;
        float aw = size.X - ml - mr, ah = size.Y - mt - mb;
        _zoom = Math.Clamp(Math.Min(aw / (float)doc.Width, ah / (float)doc.Height), MinZoom, MaxZoom);
        _pan = new Vector2(ml + (aw - (float)doc.Width * _zoom) / 2, mt + (ah - (float)doc.Height * _zoom) / 2);
        _needsFit = false;
        ZoomChanged?.Invoke();
        _canvas.Invalidate();
    }

    public void ZoomBy(float factor) => ZoomAt(factor, _canvas.ActualSize / 2);

    public void SetZoomPercent(double percent)
    {
        float target = (float)(percent / 100 * Units.DipPerMm);
        ZoomAt(target / _zoom, _canvas.ActualSize / 2);
    }

    private void ZoomAt(float factor, Vector2 screen)
    {
        var world = ToWorld(screen);
        _zoom = Math.Clamp(_zoom * factor, MinZoom, MaxZoom);
        _pan = screen - world * _zoom;
        ZoomChanged?.Invoke();
        _canvas.Invalidate();
    }

    // ------------------------------------------------------------------ rysowanie

    private void OnDraw(CanvasControl sender, CanvasDrawEventArgs args)
    {
        var ds = args.DrawingSession;
        var s = _session;
        if (s is null) return;
        if (_needsFit) FitToView();
        var doc = s.Doc;

        DrawMat(ds, sender.ActualSize);

        // cień "papieru"
        var bleed = CardRenderer.BleedRect(doc);
        var tl = ToScreen(bleed.X, bleed.Y);
        var br = ToScreen(bleed.Right, bleed.Bottom);
        for (int i = 1; i <= 6; i++)
        {
            ds.FillRoundedRectangle(new Rect(tl.X + 2, tl.Y + 4 + i * 0.6, br.X - tl.X + i * 1.2, br.Y - tl.Y + i * 1.2),
                i, i, Color.FromArgb(22, 0, 0, 0));
        }

        ds.Transform = Matrix3x2.CreateScale(_zoom) * Matrix3x2.CreateTranslation(_pan);
        CardRenderer.DrawCard(ds, doc, s.Assets, highQuality: false);

        // strefa spadu przyciemniona — to, co poza linią cięcia
        var card = CardRenderer.CardRect(doc);
        if (doc.Bleed > 0)
        {
            var tint = Color.FromArgb(120, 30, 25, 22);
            ds.FillRectangle(new Rect(bleed.X, bleed.Y, bleed.Width, -bleed.Y), tint);
            ds.FillRectangle(new Rect(bleed.X, card.Bottom, bleed.Width, bleed.Bottom - card.Bottom), tint);
            ds.FillRectangle(new Rect(bleed.X, 0, -bleed.X, card.Height), tint);
            ds.FillRectangle(new Rect(card.Right, 0, bleed.Right - card.Right, card.Height), tint);
        }
        ds.Transform = Matrix3x2.Identity;

        DrawCardGuides(ds, doc);
        DrawSnapGuides(ds, doc);
        if (_hover is not null && !ReferenceEquals(_hover, s.Selected) && !_hover.Hidden) DrawOutline(ds, _hover, Color.FromArgb(160, 243, 233, 210), false);
        if (s.Selected is { Hidden: false } sel) DrawSelection(ds, sel);
    }

    private void DrawMat(CanvasDrawingSession ds, Vector2 size)
    {
        float step = 10 * _zoom;
        int majorEvery = 5;
        if (step < 12) { step *= 5; majorEvery = 2; }
        if (step < 12) return;
        var minor = Color.FromArgb(14, 243, 233, 210);
        var major = Color.FromArgb(30, 243, 233, 210);
        int k0 = (int)Math.Floor(-_pan.X / step), k1 = (int)Math.Ceiling((size.X - _pan.X) / step);
        for (int k = k0; k <= k1; k++)
        {
            float x = _pan.X + k * step;
            ds.DrawLine(x, 0, x, size.Y, k % majorEvery == 0 ? major : minor, 1);
        }
        k0 = (int)Math.Floor(-_pan.Y / step);
        k1 = (int)Math.Ceiling((size.Y - _pan.Y) / step);
        for (int k = k0; k <= k1; k++)
        {
            float y = _pan.Y + k * step;
            ds.DrawLine(0, y, size.X, y, k % majorEvery == 0 ? major : minor, 1);
        }
    }

    private void DrawCardGuides(CanvasDrawingSession ds, CardDocument doc)
    {
        var tl = ToScreen(0, 0);
        var br = ToScreen(doc.Width, doc.Height);

        // linia cięcia (krawędź karty)
        ds.DrawRectangle(new Rect(tl.X, tl.Y, br.X - tl.X, br.Y - tl.Y), Color.FromArgb(200, 232, 114, 44), 1);

        // linie zgięcia / cięcia między panelami
        double y = 0;
        for (int i = 0; i < doc.Panels.Count - 1; i++)
        {
            y += doc.Panels[i].Height;
            float sy = ToScreen(0, y).Y;
            if (doc.Panels[i].CutAfter)
            {
                ds.DrawLine(tl.X - 10, sy, br.X + 10, sy, Color.FromArgb(220, 232, 114, 44), 1);
            }
            else
            {
                ds.DrawLine(tl.X - 10, sy, br.X + 10, sy, Color.FromArgb(90, 255, 255, 255), 1);
                ds.DrawLine(tl.X - 10, sy, br.X + 10, sy, Color.FromArgb(230, 62, 142, 135), 1, _dash);
            }
        }

        // opisy paneli po lewej (styl rysunku technicznego)
        float bx = ToScreen(-Math.Max(0, doc.Bleed), 0).X - 12;
        var dim = Color.FromArgb(150, 243, 233, 210);
        y = 0;
        foreach (var p in doc.Panels)
        {
            float t = ToScreen(0, y).Y, b = ToScreen(0, y + p.Height).Y;
            y += p.Height;
            ds.DrawLine(bx, t + 1.5f, bx, b - 1.5f, dim, 1);
            ds.DrawLine(bx - 4, t + 1.5f, bx + 2, t + 1.5f, dim, 1);
            ds.DrawLine(bx - 4, b - 1.5f, bx + 2, b - 1.5f, dim, 1);
            var rect = new Rect(bx - 160, t, 150, Math.Max(1, b - t));
            if (b - t >= 34)
            {
                float mid = (t + b) / 2;
                ds.DrawText(p.Name.ToUpperInvariant(), new Rect(rect.X, mid - 17, rect.Width, 16), Cream, _labelFormat);
                ds.DrawText($"{p.Height:0.#} mm", new Rect(rect.X, mid + 1, rect.Width, 16), dim, _labelFormat);
            }
            else if (b - t >= 12)
            {
                ds.DrawText($"{p.Name.ToUpperInvariant()} · {p.Height:0.#}", rect, Cream, _labelFormat);
            }
        }

        // wymiar szerokości nad kartą
        float dy = ToScreen(0, -Math.Max(0, doc.Bleed)).Y - 16;
        ds.DrawLine(tl.X, dy, br.X, dy, dim, 1);
        ds.DrawLine(tl.X, dy - 4, tl.X, dy + 4, dim, 1);
        ds.DrawLine(br.X, dy - 4, br.X, dy + 4, dim, 1);
        var label = $"{doc.Width:0.#} mm";
        var lr = new Rect((tl.X + br.X) / 2 - 36, dy - 9, 72, 18);
        ds.FillRectangle(lr, Workbench);
        ds.DrawText(label, lr, Cream, _dimFormat);
    }

    private void DrawSnapGuides(CanvasDrawingSession ds, CardDocument doc)
    {
        if (_guides.Count == 0) return;
        var tl = ToScreen(-8, -8);
        var br = ToScreen(doc.Width + 8, doc.Height + 8);
        foreach (var (vertical, pos) in _guides)
        {
            if (vertical)
            {
                float x = ToScreen(pos, 0).X;
                ds.DrawLine(x, tl.Y, x, br.Y, Magenta, 1);
            }
            else
            {
                float y = ToScreen(0, pos).Y;
                ds.DrawLine(tl.X, y, br.X, y, Magenta, 1);
            }
        }
    }

    private Vector2[] CornersScreen(CardElement e)
    {
        double hw = e.Width / 2, hh = e.Height / 2;
        if (e is ShapeElement { Shape: ShapeKind.Line }) hh = Math.Max(hh, 0);
        return
        [
            ToScreen(LocalToWorld(e, -hw, -hh)), ToScreen(LocalToWorld(e, hw, -hh)),
            ToScreen(LocalToWorld(e, hw, hh)), ToScreen(LocalToWorld(e, -hw, hh)),
        ];
    }

    private void DrawOutline(CanvasDrawingSession ds, CardElement e, Color color, bool dashed)
    {
        var c = CornersScreen(e);
        using var geo = CanvasGeometry.CreatePolygon(ds, c);
        ds.DrawGeometry(geo, color, 1, dashed ? _dash : null);
    }

    private void DrawSelection(CanvasDrawingSession ds, CardElement e)
    {
        var c = CornersScreen(e);
        using (var geo = CanvasGeometry.CreatePolygon(ds, c))
        {
            ds.DrawGeometry(geo, Color.FromArgb(160, 0, 0, 0), 3);
            ds.DrawGeometry(geo, Orange, 1.5f);
        }

        if (e.Locked)
        {
            var p = c[0];
            ds.FillCircle(p, 9, Orange);
            ds.DrawText("", new Rect(p.X - 9, p.Y - 9, 18, 18), Color.FromArgb(255, 30, 25, 22), _iconFormat);
            return;
        }

        // uchwyt obrotu
        var topMid = ToScreen(LocalToWorld(e, 0, -e.Height / 2));
        var rh = RotateHandleScreen(e);
        ds.DrawLine(topMid, rh, Orange, 1.5f);
        ds.FillCircle(rh, 6, Cream);
        ds.DrawCircle(rh, 6, Orange, 2);

        foreach (var (hx, hy) in Handles(e))
        {
            var p = ToScreen(LocalToWorld(e, hx * e.Width / 2, hy * e.Height / 2));
            var r = new Rect(p.X - 4.5, p.Y - 4.5, 9, 9);
            ds.FillRectangle(r, Cream);
            ds.DrawRectangle(r, Orange, 2);
        }
    }

    // ------------------------------------------------------------------ geometria

    private static Vector2 LocalToWorld(CardElement e, double lx, double ly)
    {
        double a = e.Rotation * Math.PI / 180, cos = Math.Cos(a), sin = Math.Sin(a);
        return new Vector2((float)(e.CenterX + lx * cos - ly * sin), (float)(e.CenterY + lx * sin + ly * cos));
    }

    private static (double X, double Y) WorldToLocal(CardElement e, Vector2 w)
    {
        double a = e.Rotation * Math.PI / 180, cos = Math.Cos(a), sin = Math.Sin(a);
        double dx = w.X - e.CenterX, dy = w.Y - e.CenterY;
        return (dx * cos + dy * sin, -dx * sin + dy * cos);
    }

    private static readonly (int, int)[] AllHandles = [(-1, -1), (0, -1), (1, -1), (1, 0), (1, 1), (0, 1), (-1, 1), (-1, 0)];
    private static readonly (int, int)[] LineHandles = [(-1, 0), (1, 0)];

    private (int, int)[] Handles(CardElement e)
    {
        if (e is ShapeElement { Shape: ShapeKind.Line }) return LineHandles;
        // przy bardzo małych elementach zostaw tylko narożniki
        if (e.Width * _zoom < 26 || e.Height * _zoom < 26) return [(-1, -1), (1, -1), (1, 1), (-1, 1)];
        return AllHandles;
    }

    private Vector2 RotateHandleScreen(CardElement e)
    {
        var topMid = ToScreen(LocalToWorld(e, 0, -e.Height / 2));
        var center = ToScreen(LocalToWorld(e, 0, 0));
        var dir = topMid - center;
        if (dir.LengthSquared() < 1e-3)
        {
            double a = (e.Rotation - 90) * Math.PI / 180;
            dir = new Vector2((float)Math.Cos(a), (float)Math.Sin(a));
        }
        return topMid + Vector2.Normalize(dir) * RotateHandleDistance;
    }

    private bool HitHandle(CardElement e, Vector2 screen, out int hx, out int hy)
    {
        foreach (var (x, y) in Handles(e))
        {
            var p = ToScreen(LocalToWorld(e, x * e.Width / 2, y * e.Height / 2));
            if (Vector2.Distance(p, screen) <= HandleRadius)
            {
                hx = x; hy = y;
                return true;
            }
        }
        hx = hy = 0;
        return false;
    }

    private bool HitRotateHandle(CardElement e, Vector2 screen) =>
        Vector2.Distance(RotateHandleScreen(e), screen) <= HandleRadius + 1;

    private static bool Contains(CardElement e, Vector2 world, double tol)
    {
        var (lx, ly) = WorldToLocal(e, world);
        if (e is ShapeElement { Shape: ShapeKind.Line } line)
            return Math.Abs(lx) <= e.Width / 2 + tol && Math.Abs(ly) <= line.StrokeWidth / 2 + tol * 1.5;
        return Math.Abs(lx) <= e.Width / 2 + tol && Math.Abs(ly) <= e.Height / 2 + tol;
    }

    private CardElement? HitTest(Vector2 world)
    {
        if (_session is null) return null;
        double tol = 2 / _zoom;
        var list = _session.Doc.Elements;
        for (int i = list.Count - 1; i >= 0; i--)
        {
            var e = list[i];
            if (e.Hidden || e.Locked) continue;
            if (Contains(e, world, tol)) return e;
        }
        return null;
    }

    private static (double W, double H) RotatedSize(CardElement e)
    {
        double a = e.Rotation * Math.PI / 180, c = Math.Abs(Math.Cos(a)), s = Math.Abs(Math.Sin(a));
        return (e.Width * c + e.Height * s, e.Width * s + e.Height * c);
    }

    // ------------------------------------------------------------------ przyciąganie

    private (List<double> X, List<double> Y) SnapTargets(CardElement moving)
    {
        var doc = _session!.Doc;
        var xs = new List<double> { 0, doc.Width / 2, doc.Width };
        var ys = new List<double> { 0, doc.Height };
        double top = 0;
        foreach (var p in doc.Panels)
        {
            ys.Add(top + p.Height / 2);
            top += p.Height;
            ys.Add(top);
        }
        foreach (var e in doc.Elements)
        {
            if (ReferenceEquals(e, moving) || e.Hidden) continue;
            var (w, h) = RotatedSize(e);
            xs.Add(e.CenterX - w / 2); xs.Add(e.CenterX); xs.Add(e.CenterX + w / 2);
            ys.Add(e.CenterY - h / 2); ys.Add(e.CenterY); ys.Add(e.CenterY + h / 2);
        }
        return (xs, ys);
    }

    private static double? BestSnap(IEnumerable<double> candidates, List<double> targets, double threshold, out double guide)
    {
        double? best = null;
        guide = 0;
        foreach (var c in candidates)
        {
            foreach (var t in targets)
            {
                double d = t - c;
                if (Math.Abs(d) <= threshold && (best is null || Math.Abs(d) < Math.Abs(best.Value)))
                {
                    best = d;
                    guide = t;
                }
            }
        }
        return best;
    }

    private void SnapMove(CardElement e, ref double nx, ref double ny)
    {
        _guides.Clear();
        double thr = 6 / _zoom;
        var (xs, ys) = SnapTargets(e);
        var (w, h) = RotatedSize(e);
        double cx = nx + e.Width / 2, cy = ny + e.Height / 2;
        var dx = BestSnap([cx - w / 2, cx, cx + w / 2], xs, thr, out var gx);
        if (dx is not null) { nx += dx.Value; _guides.Add((true, gx)); }
        var dy = BestSnap([cy - h / 2, cy, cy + h / 2], ys, thr, out var gy);
        if (dy is not null) { ny += dy.Value; _guides.Add((false, gy)); }
    }

    // ------------------------------------------------------------------ wskaźnik

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var s = _session;
        if (s is null) return;
        Focus(FocusState.Pointer);
        var pt = e.GetCurrentPoint(_canvas);
        var pos = pt.Position.ToVector2();
        var world = ToWorld(pos);
        var props = pt.Properties;
        _dragStartScreen = pos;
        _dragStartWorld = world;
        _dragEdited = false;

        if (props.IsMiddleButtonPressed || (props.IsLeftButtonPressed && _spaceDown))
        {
            StartPan(e);
            return;
        }

        if (props.IsRightButtonPressed)
        {
            var hit = s.Selected is { Hidden: false } cur && Contains(cur, world, 2 / _zoom) ? cur : HitTest(world);
            if (hit is not null) s.Select(hit);
            ContextMenuRequested?.Invoke(pt.Position, hit);
            e.Handled = true;
            return;
        }

        if (!props.IsLeftButtonPressed) return;

        var sel = s.Selected;
        if (sel is { Hidden: false, Locked: false })
        {
            if (HitRotateHandle(sel, pos))
            {
                BeginElementDrag(sel, DragMode.Rotate, e);
                return;
            }
            if (HitHandle(sel, pos, out _hx, out _hy))
            {
                BeginElementDrag(sel, DragMode.Resize, e);
                return;
            }
        }

        var target = HitTest(world);
        if (target is not null)
        {
            s.Select(target);
            BeginElementDrag(target, DragMode.Move, e);
        }
        else
        {
            s.Select(null);
            StartPan(e);
        }
    }

    private void StartPan(PointerRoutedEventArgs e)
    {
        _mode = DragMode.Pan;
        _panStart = _pan;
        _canvas.CapturePointer(e.Pointer);
        UpdateCursor(InputSystemCursorShape.Hand);
        e.Handled = true;
    }

    private void BeginElementDrag(CardElement el, DragMode mode, PointerRoutedEventArgs e)
    {
        _mode = mode;
        _startX = el.X; _startY = el.Y; _startW = el.Width; _startH = el.Height; _startRot = el.Rotation;
        _canvas.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        var s = _session;
        if (s is null) return;
        var pos = e.GetCurrentPoint(_canvas).Position.ToVector2();
        var world = ToWorld(pos);
        PointerWorldMoved?.Invoke(world);
        var mods = e.KeyModifiers;
        bool shift = mods.HasFlag(VirtualKeyModifiers.Shift);
        bool alt = mods.HasFlag(VirtualKeyModifiers.Menu);

        switch (_mode)
        {
            case DragMode.None:
                UpdateHover(pos, world);
                return;

            case DragMode.Pan:
                _pan = _panStart + (pos - _dragStartScreen);
                _canvas.Invalidate();
                return;
        }

        var el = s.Selected;
        if (el is null) { _mode = DragMode.None; return; }

        if (!_dragEdited)
        {
            if (_mode == DragMode.Move && Vector2.Distance(pos, _dragStartScreen) < 3) return;
            s.BeginEdit();
            _dragEdited = true;
        }

        switch (_mode)
        {
            case DragMode.Move:
                {
                    var d = world - _dragStartWorld;
                    double dx = d.X, dy = d.Y;
                    if (shift)
                    {
                        if (Math.Abs(dx) > Math.Abs(dy)) dy = 0; else dx = 0;
                    }
                    double nx = _startX + dx, ny = _startY + dy;
                    if (!alt) SnapMove(el, ref nx, ref ny); else _guides.Clear();
                    if (shift)
                    {
                        if (dy == 0) ny = _startY; else nx = _startX;
                    }
                    el.X = Math.Round(nx, 2);
                    el.Y = Math.Round(ny, 2);
                    break;
                }
            case DragMode.Resize:
                DoResize(el, world, shift || el is ImageElement && _hx != 0 && _hy != 0 && !alt, alt);
                break;
            case DragMode.Rotate:
                {
                    var c = new Vector2((float)(_startX + _startW / 2), (float)(_startY + _startH / 2));
                    double a0 = Math.Atan2(_dragStartWorld.Y - c.Y, _dragStartWorld.X - c.X);
                    double a1 = Math.Atan2(world.Y - c.Y, world.X - c.X);
                    double rot = _startRot + (a1 - a0) * 180 / Math.PI;
                    if (shift) rot = Math.Round(rot / 15) * 15;
                    else
                    {
                        double r90 = Math.Round(rot / 90) * 90;
                        if (Math.Abs(rot - r90) < 3) rot = r90;
                    }
                    rot %= 360;
                    if (rot > 180) rot -= 360;
                    if (rot <= -180) rot += 360;
                    el.Rotation = Math.Round(rot, 1);
                    break;
                }
        }
        s.NotifyChanged(ChangeFlags.Visual | ChangeFlags.Properties);
    }

    private void DoResize(CardElement el, Vector2 world, bool keepAspect, bool noSnap)
    {
        double a = _startRot * Math.PI / 180, cos = Math.Cos(a), sin = Math.Sin(a);
        double wx = world.X - _dragStartWorld.X, wy = world.Y - _dragStartWorld.Y;
        double dlx = wx * cos + wy * sin, dly = -wx * sin + wy * cos;

        double left = -_startW / 2, right = _startW / 2, top = -_startH / 2, bottom = _startH / 2;
        if (_hx < 0) left += dlx; else if (_hx > 0) right += dlx;
        if (_hy < 0) top += dly; else if (_hy > 0) bottom += dly;

        double cx0 = _startX + _startW / 2, cy0 = _startY + _startH / 2;
        _guides.Clear();
        if (!noSnap && Math.Abs(_startRot) < 0.01)
        {
            double thr = 6 / _zoom;
            var (xs, ys) = SnapTargets(el);
            if (_hx != 0)
            {
                double edge = cx0 + (_hx < 0 ? left : right);
                var d = BestSnap([edge], xs, thr, out var g);
                if (d is not null) { if (_hx < 0) left += d.Value; else right += d.Value; _guides.Add((true, g)); }
            }
            if (_hy != 0)
            {
                double edge = cy0 + (_hy < 0 ? top : bottom);
                var d = BestSnap([edge], ys, thr, out var g);
                if (d is not null) { if (_hy < 0) top += d.Value; else bottom += d.Value; _guides.Add((false, g)); }
            }
        }

        const double min = 1;
        if (right - left < min) { if (_hx < 0) left = right - min; else right = left + min; }
        if (bottom - top < min) { if (_hy < 0) top = bottom - min; else bottom = top + min; }

        if (keepAspect && _hx != 0 && _hy != 0 && _startH > 0)
        {
            double ratio = _startW / _startH;
            double nw = right - left, nh = bottom - top;
            if (nw / nh > ratio) nw = nh * ratio; else nh = nw / ratio;
            if (_hx < 0) left = right - nw; else right = left + nw;
            if (_hy < 0) top = bottom - nh; else bottom = top + nh;
        }

        double lcx = (left + right) / 2, lcy = (top + bottom) / 2;
        double cx = cx0 + lcx * cos - lcy * sin, cy = cy0 + lcx * sin + lcy * cos;
        double w = right - left, h = bottom - top;
        el.Width = Math.Round(w, 2);
        el.Height = Math.Round(h, 2);
        el.X = Math.Round(cx - w / 2, 2);
        el.Y = Math.Round(cy - h / 2, 2);
    }

    private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        _canvas.ReleasePointerCapture(e.Pointer);
        EndDrag();
    }

    private void EndDrag()
    {
        if (_mode == DragMode.None) return;
        bool edited = _dragEdited;
        _mode = DragMode.None;
        _dragEdited = false;
        _guides.Clear();
        UpdateCursor(_spaceDown ? InputSystemCursorShape.Hand : InputSystemCursorShape.Arrow);
        if (edited && _session is not null)
        {
            _session.BreakCoalescing();
            _session.NotifyChanged(ChangeFlags.Visual | ChangeFlags.Properties);
        }
        _canvas.Invalidate();
    }

    private void UpdateHover(Vector2 pos, Vector2 world)
    {
        var s = _session!;
        if (_spaceDown) { UpdateCursor(InputSystemCursorShape.Hand); return; }
        var sel = s.Selected;
        if (sel is { Hidden: false, Locked: false })
        {
            if (HitRotateHandle(sel, pos)) { UpdateCursor(InputSystemCursorShape.Cross); SetHover(null); return; }
            if (HitHandle(sel, pos, out var hx, out var hy))
            {
                UpdateCursor(ResizeCursor(sel.Rotation, hx, hy));
                SetHover(null);
                return;
            }
        }
        var hit = HitTest(world);
        SetHover(hit);
        UpdateCursor(hit is null ? InputSystemCursorShape.Arrow : InputSystemCursorShape.SizeAll);
    }

    private static InputSystemCursorShape ResizeCursor(double rotation, int hx, int hy)
    {
        double ang = Math.Atan2(hy, hx) * 180 / Math.PI + rotation;
        ang = ((ang % 180) + 180) % 180; // 0..180
        return ang switch
        {
            < 22.5 or >= 157.5 => InputSystemCursorShape.SizeWestEast,
            < 67.5 => InputSystemCursorShape.SizeNorthwestSoutheast,
            < 112.5 => InputSystemCursorShape.SizeNorthSouth,
            _ => InputSystemCursorShape.SizeNortheastSouthwest,
        };
    }

    private void SetHover(CardElement? e)
    {
        if (ReferenceEquals(_hover, e)) return;
        _hover = e;
        _canvas.Invalidate();
    }

    private void UpdateCursor(InputSystemCursorShape shape)
    {
        if (_cursorShape == shape) return;
        _cursorShape = shape;
        ProtectedCursor = InputSystemCursor.Create(shape);
    }

    private void OnWheel(object sender, PointerRoutedEventArgs e)
    {
        var pt = e.GetCurrentPoint(_canvas);
        int delta = pt.Properties.MouseWheelDelta;
        var mods = e.KeyModifiers;
        if (mods.HasFlag(VirtualKeyModifiers.Control))
        {
            ZoomAt(MathF.Pow(1.0015f, delta), pt.Position.ToVector2());
        }
        else if (mods.HasFlag(VirtualKeyModifiers.Shift) || pt.Properties.IsHorizontalMouseWheel)
        {
            _pan.X += (pt.Properties.IsHorizontalMouseWheel ? -delta : delta) * 0.6f;
            _canvas.Invalidate();
        }
        else
        {
            _pan.Y += delta * 0.6f;
            _canvas.Invalidate();
        }
        e.Handled = true;
    }

    private void OnDoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        var world = ToWorld(e.GetPosition(_canvas).ToVector2());
        if (_session?.Selected is TextElement t && Contains(t, world, 2 / _zoom)) EditTextRequested?.Invoke(t);
        else if (HitTest(world) is TextElement t2) EditTextRequested?.Invoke(t2);
    }

    // ------------------------------------------------------------------ przeciągnij i upuść pliki

    private void OnDragOver(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
            e.DragUIOverride.Caption = "Upuść tutaj";
        }
    }

    private async void OnDrop(object sender, DragEventArgs e)
    {
        if (!e.DataView.Contains(StandardDataFormats.StorageItems)) return;
        var world = ToWorld(e.GetPosition(_canvas).ToVector2());
        var def = e.GetDeferral();
        List<string> paths;
        try
        {
            var items = await e.DataView.GetStorageItemsAsync();
            paths = [.. items.Select(i => i.Path).Where(p => !string.IsNullOrEmpty(p))];
        }
        finally
        {
            def.Complete();
        }
        if (paths.Count > 0) FilesDropped?.Invoke(paths, world);
    }

    /// <summary>Środek widocznego obszaru w mm — gdzie wstawiać nowe elementy.</summary>
    public Vector2 ViewCenterWorld()
    {
        var c = ToWorld(_canvas.ActualSize / 2);
        if (_session is null) return c;
        var doc = _session.Doc;
        return new Vector2(Math.Clamp(c.X, 0, (float)doc.Width), Math.Clamp(c.Y, 0, (float)doc.Height));
    }
}
