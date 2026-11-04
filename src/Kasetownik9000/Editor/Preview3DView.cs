using System.Numerics;
using Kasetownik9000.Model;
using Kasetownik9000.Rendering;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
using Microsoft.Graphics.Canvas.Effects;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.Graphics.Canvas.Text;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using Windows.UI;

namespace Kasetownik9000.Editor;

/// <summary>
/// Podgląd 3D: panele karty są renderowane do tekstur i rzutowane w perspektywie (Transform3DEffect)
/// na ścianki J-karty złożonej wokół kasety, w przezroczystym pudełku. Bez GPU-owego bufora głębi —
/// kolejność rysowania wynika z budowy sceny (pudło → karta od środka → kaseta → karta → plastik).
/// </summary>
public sealed partial class Preview3DView : UserControl
{
    private const float TexPxPerMm = 10f;
    private const float CassPxPerMm = 8f;
    private const float CameraDistance = 420f;
    private const float CassW = 100.4f, CassH = 63.8f, CassT = 12f;

    private static readonly Vector3 LightDir = Vector3.Normalize(new Vector3(-0.4f, -0.25f, 0.9f));
    private static readonly Color Paper = Color.FromArgb(255, 246, 242, 233);
    private static readonly Color ShellEdge = Color.FromArgb(255, 38, 35, 33);

    private readonly CanvasControl _canvas;
    private readonly TextBlock _modeText;
    private readonly Button _rotateButton, _caseButton;
    private readonly Brush _keyBrush, _accentBrush, _creamBrush, _darkBrush;

    private EditorSession? _session;
    private Scene? _scene;
    private bool _active, _dirty = true, _regenQueued;

    private float _yaw = -28, _pitch = 24, _zoom = 1;
    private float _targetYaw, _targetPitch;
    private bool _animating, _autoRotate, _showCase = true, _renderingHooked;
    private bool _dragging;
    private Vector2 _dragStart;
    private float _yaw0, _pitch0;

    public event Action? SaveImageRequested;

    public Preview3DView()
    {
        var res = Application.Current.Resources;
        _keyBrush = (Brush)res["DeckKeyBrush"];
        _accentBrush = (Brush)res["DeckKeyAccentBrush"];
        _creamBrush = (Brush)res["RetroCreamBrush"];
        _darkBrush = new SolidColorBrush(Color.FromArgb(255, 30, 25, 22));

        _canvas = new CanvasControl { ClearColor = Color.FromArgb(255, 22, 18, 15) };
        _canvas.Draw += (s, a) => DrawScene(a.DrawingSession, s.ActualSize);
        _canvas.PointerPressed += OnPointerPressed;
        _canvas.PointerMoved += OnPointerMoved;
        _canvas.PointerReleased += (_, e) => { _dragging = false; _canvas.ReleasePointerCapture(e.Pointer); };
        _canvas.PointerCaptureLost += (_, _) => _dragging = false;
        _canvas.PointerWheelChanged += OnWheel;
        _canvas.DoubleTapped += (_, _) => { _zoom = 1; GoTo(-28, 24); };

        var root = new Grid();
        root.Children.Add(_canvas);

        // --- lewy górny róg: "wyświetlacz" z nazwą trybu
        var lcd = new Border { Style = (Style)res["LcdBorderStyle"], HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(16) };
        _modeText = new TextBlock { Style = (Style)res["LcdTextStyle"], Text = "PODGLĄD 3D" };
        lcd.Child = _modeText;
        root.Children.Add(lcd);

        var hint = new TextBlock
        {
            Text = "Przeciągnij — obrót · kółko — przybliżenie · dwuklik — widok startowy",
            FontSize = 11, Foreground = (Brush)res["RetroCreamDimBrush"],
            HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(16, 22, 16, 0),
        };
        root.Children.Add(hint);

        // --- dolny pasek klawiszy
        var bar = new StackPanel
        {
            Orientation = Orientation.Horizontal, Spacing = 6, HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(16, 16, 16, 18),
        };
        bar.Children.Add(Key("3/4", () => GoTo(-28, 24), "Widok z ukosa"));
        bar.Children.Add(Key("PRZÓD", () => GoTo(0, 0), "Okładka na wprost"));
        bar.Children.Add(Key("GRZBIET", () => GoTo(0, 90), "Grzbiet — tak kaseta stoi na półce"));
        bar.Children.Add(Key("TYŁ", () => { if (_scene?.CassetteOnly == true) GoTo(180, 0); else GoTo(0, 180); }, "Tył pudełka (klapka)"));
        bar.Children.Add(new Microsoft.UI.Xaml.Shapes.Rectangle { Width = 1, Margin = new Thickness(6, 6, 6, 9), Fill = (Brush)res["RetroLineBrush"] });
        _rotateButton = Key("OBRACAJ", ToggleAutoRotate, "Powolny automatyczny obrót");
        _caseButton = Key("PUDEŁKO", ToggleCase, "Pokaż / ukryj plastikowe pudełko");
        bar.Children.Add(_rotateButton);
        bar.Children.Add(_caseButton);
        bar.Children.Add(new Microsoft.UI.Xaml.Shapes.Rectangle { Width = 1, Margin = new Thickness(6, 6, 6, 9), Fill = (Brush)res["RetroLineBrush"] });
        var save = Key("ZAPISZ OBRAZ", () => SaveImageRequested?.Invoke(), "Zapisz ten widok jako obraz PNG (np. do pokazania znajomym)");
        bar.Children.Add(save);
        root.Children.Add(bar);

        Content = root;
        ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.SizeAll);
        SetKeyActive(_caseButton, _showCase);
        Unloaded += (_, _) => UnhookRendering();
    }

    private Button Key(string text, Action click, string tip)
    {
        var b = new Button { Content = text, Style = (Style)Application.Current.Resources["DeckKeySmallStyle"], Padding = new Thickness(12, 0, 12, 0) };
        ToolTipService.SetToolTip(b, tip);
        b.Click += (_, _) => click();
        return b;
    }

    private void SetKeyActive(Button b, bool on)
    {
        b.Background = on ? _accentBrush : _keyBrush;
        b.Foreground = on ? _darkBrush : _creamBrush;
    }

    // ================================================================== sesja / aktywacja

    public EditorSession? Session
    {
        get => _session;
        set
        {
            if (_session is not null) _session.Changed -= OnSessionChanged;
            _session = value;
            if (_session is not null) _session.Changed += OnSessionChanged;
            _scene?.Dispose();
            _scene = null;
            _dirty = true;
            if (_active) { Rebuild(); ApplyModeDefaults(); _canvas.Invalidate(); }
        }
    }

    /// <summary>Włącza/wyłącza widok (tekstury są przeliczane tylko, gdy widok jest aktywny).</summary>
    public bool IsActive
    {
        get => _active;
        set
        {
            if (_active == value) return;
            _active = value;
            if (_active)
            {
                if (_dirty || _scene is null) { Rebuild(); ApplyModeDefaults(); }
                _canvas.Invalidate();
                if (_autoRotate) HookRendering();
            }
            else
            {
                UnhookRendering();
            }
        }
    }

    public void Shutdown()
    {
        UnhookRendering();
        _scene?.Dispose();
        _canvas.RemoveFromVisualTree();
    }

    private bool _lastCassetteOnly;

    private void ApplyModeDefaults()
    {
        if (_scene is null || _scene.CassetteOnly == _lastCassetteOnly) return;
        _lastCassetteOnly = _scene.CassetteOnly;
        _showCase = !_scene.CassetteOnly;
        SetKeyActive(_caseButton, _showCase);
    }

    private void OnSessionChanged(ChangeFlags flags)
    {
        _dirty = true;
        if (!_active || _regenQueued) return;
        _regenQueued = true;
        // kilka zmian pod rząd (np. przeciąganie suwaka) = jedno przeliczenie tekstur
        DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
        {
            _regenQueued = false;
            if (!_active || !_dirty) return;
            Rebuild();
            ApplyModeDefaults();
            _canvas.Invalidate();
        });
    }

    private void Rebuild()
    {
        _scene?.Dispose();
        _scene = _session is null ? null : BuildScene(_session);
        _dirty = false;
        if (_scene is not null) _modeText.Text = _scene.Title;
    }

    // ================================================================== sterowanie

    public void ZoomBy(float f)
    {
        _zoom = Math.Clamp(_zoom * f, 0.4f, 4f);
        _canvas.Invalidate();
    }

    /// <summary>Ustawia kamerę bez animacji (np. do eksportu z linii poleceń).</summary>
    public void SetView(float yaw, float pitch, float zoom = 1, bool? showCase = null)
    {
        _yaw = Wrap(yaw);
        _pitch = Wrap(pitch);
        _zoom = Math.Clamp(zoom, 0.4f, 4f);
        if (showCase is { } sc) { _showCase = sc; SetKeyActive(_caseButton, sc); }
        _animating = false;
        _canvas.Invalidate();
    }

    /// <summary>Wczytuje obrazy i buduje scenę (z domyślnymi ustawieniami dla danego formatu).</summary>
    public async Task PrepareAsync()
    {
        if (_session is null) return;
        await _session.Assets.EnsureLoadedAsync(_session.UsedAssetIds());
        Rebuild();
        ApplyModeDefaults();
    }

    public void ResetView()
    {
        _zoom = 1;
        GoTo(-28, 24);
    }

    private void GoTo(float yaw, float pitch)
    {
        _targetYaw = yaw;
        _targetPitch = pitch;
        _animating = true;
        if (_autoRotate) { _autoRotate = false; SetKeyActive(_rotateButton, false); }
        HookRendering();
    }

    private void ToggleAutoRotate()
    {
        _autoRotate = !_autoRotate;
        SetKeyActive(_rotateButton, _autoRotate);
        if (_autoRotate) { _animating = false; HookRendering(); }
    }

    private void ToggleCase()
    {
        _showCase = !_showCase;
        SetKeyActive(_caseButton, _showCase);
        _canvas.Invalidate();
    }

    private void HookRendering()
    {
        if (_renderingHooked || !_active) return;
        CompositionTarget.Rendering += OnRendering;
        _renderingHooked = true;
    }

    private void UnhookRendering()
    {
        if (!_renderingHooked) return;
        CompositionTarget.Rendering -= OnRendering;
        _renderingHooked = false;
    }

    private static float Wrap(float a)
    {
        a %= 360;
        if (a > 180) a -= 360;
        if (a <= -180) a += 360;
        return a;
    }

    private void OnRendering(object? sender, object e)
    {
        if (_autoRotate && !_dragging)
        {
            // obrotowy stolik: kręć wokół pionu i łagodnie wróć do lekkiego pochylenia
            _yaw = Wrap(_yaw + 0.45f);
            _pitch = Wrap(_pitch + Wrap(14 - _pitch) * 0.04f);
        }
        if (_animating)
        {
            float dy = Wrap(_targetYaw - _yaw), dp = Wrap(_targetPitch - _pitch);
            if (Math.Abs(dy) < 0.15f && Math.Abs(dp) < 0.15f)
            {
                _yaw = _targetYaw;
                _pitch = _targetPitch;
                _animating = false;
            }
            else
            {
                _yaw = Wrap(_yaw + dy * 0.16f);
                _pitch = Wrap(_pitch + dp * 0.16f);
            }
        }
        if (!_animating && !_autoRotate) UnhookRendering();
        _canvas.Invalidate();
    }

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var pt = e.GetCurrentPoint(_canvas);
        if (!pt.Properties.IsLeftButtonPressed) return;
        _dragging = true;
        _animating = false;
        _dragStart = pt.Position.ToVector2();
        _yaw0 = _yaw;
        _pitch0 = _pitch;
        _canvas.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_dragging) return;
        var d = e.GetCurrentPoint(_canvas).Position.ToVector2() - _dragStart;
        _yaw = Wrap(_yaw0 + d.X * 0.45f);
        _pitch = Wrap(_pitch0 - d.Y * 0.45f);
        _canvas.Invalidate();
    }

    private void OnWheel(object sender, PointerRoutedEventArgs e)
    {
        int delta = e.GetCurrentPoint(_canvas).Properties.MouseWheelDelta;
        ZoomBy(MathF.Pow(1.0015f, delta));
        e.Handled = true;
    }

    // ================================================================== eksport

    public async Task ExportPngAsync(string path, int width = 2400, int height = 1600)
    {
        if (_dirty || _scene is null)
        {
            if (_session is not null) await _session.Assets.EnsureLoadedAsync(_session.UsedAssetIds());
            Rebuild();
        }
        using var rt = new CanvasRenderTarget(CanvasDevice.GetSharedDevice(), width, height, 96);
        using (var ds = rt.CreateDrawingSession())
        {
            ds.Clear(Color.FromArgb(255, 22, 18, 15));
            DrawScene(ds, new Vector2(width, height));
        }
        await rt.SaveAsync(path, CanvasBitmapFileFormat.Png);
    }

    // ================================================================== scena

    private sealed class Face : IDisposable
    {
        public Vector3 Origin, U, V, Normal;
        public float W, H;
        public CanvasRenderTarget? Texture;
        public Transform3DEffect? Effect;
        public Color Fill;

        public Vector3 Center => Origin + U * (W / 2) + V * (H / 2);
        public Vector3[] Corners => [Origin, Origin + U * W, Origin + U * W + V * H, Origin + V * H];

        public void SetTexture(CanvasRenderTarget tex)
        {
            Texture = tex;
            Effect = new Transform3DEffect { Source = tex, InterpolationMode = CanvasImageInterpolation.Anisotropic };
        }

        public void Dispose()
        {
            Effect?.Dispose();
            Texture?.Dispose();
        }
    }

    private sealed class Scene : IDisposable
    {
        public readonly List<Face> Card = [];
        public readonly List<Face> Cassette = [];
        public readonly List<Face> Case = [];
        public Vector3 CaseSize;
        public bool CassetteOnly;
        public string Title = "PODGLĄD 3D";

        public void Dispose()
        {
            foreach (var f in Card) f.Dispose();
            foreach (var f in Cassette) f.Dispose();
            foreach (var f in Case) f.Dispose();
        }
    }

    /// <summary>Sześć ścian prostopadłościanu o środku w (0,0,0). Kolejność: przód, tył, góra, dół, lewa, prawa.</summary>
    private static Face[] BoxFaces(Vector3 size)
    {
        float x = size.X / 2, y = size.Y / 2, z = size.Z / 2;
        return
        [
            new() { Origin = new(-x, -y, z), U = Vector3.UnitX, V = Vector3.UnitY, Normal = Vector3.UnitZ, W = size.X, H = size.Y },
            new() { Origin = new(x, -y, -z), U = -Vector3.UnitX, V = Vector3.UnitY, Normal = -Vector3.UnitZ, W = size.X, H = size.Y },
            new() { Origin = new(-x, -y, -z), U = Vector3.UnitX, V = Vector3.UnitZ, Normal = -Vector3.UnitY, W = size.X, H = size.Z },
            new() { Origin = new(-x, y, z), U = Vector3.UnitX, V = -Vector3.UnitZ, Normal = Vector3.UnitY, W = size.X, H = size.Z },
            new() { Origin = new(-x, -y, -z), U = Vector3.UnitZ, V = Vector3.UnitY, Normal = -Vector3.UnitX, W = size.Z, H = size.Y },
            new() { Origin = new(x, -y, z), U = -Vector3.UnitZ, V = Vector3.UnitY, Normal = Vector3.UnitX, W = size.Z, H = size.Y },
        ];
    }

    private static Scene BuildScene(EditorSession session)
    {
        var doc = session.Doc;
        var scene = new Scene();
        int n = doc.Panels.Count;
        int front = doc.Panels.FindIndex(p => p.Role == PanelRole.Front);
        var labels = Enumerable.Range(0, n).Where(i => doc.Panels[i].Role == PanelRole.Label).ToList();

        CanvasRenderTarget? labelA = null, labelB = null;
        float cw = CassW, ch = CassH, ct = CassT;

        if (front < 0 && labels.Count > 0)
        {
            // naklejki: sama kaseta z etykietami A/B
            scene.CassetteOnly = true;
            scene.Title = "KASETA Z NAKLEJKAMI";
            labelA = RenderPanel(session, labels[0]);
            if (labels.Count > 1) labelB = RenderPanel(session, labels[1]);
            scene.CaseSize = new Vector3(110, 69.5f, 17);
        }
        else
        {
            if (front < 0) front = 0;
            float W = (float)doc.Width, Hf = (float)doc.Panels[front].Height;
            int spine = front + 1 < n ? front + 1 : -1;
            int back = front + 2 < n ? front + 2 : -1;
            float Ds = spine >= 0 ? (float)Math.Clamp(doc.Panels[spine].Height, 4, 40) : 12.7f;
            float Hb = back >= 0 ? (float)Math.Min(doc.Panels[back].Height, Hf) : 0;

            var f = new Face { Origin = new(-W / 2, -Hf / 2, Ds / 2), U = Vector3.UnitX, V = Vector3.UnitY, Normal = Vector3.UnitZ, W = W, H = Hf, Fill = Paper };
            f.SetTexture(RenderPanel(session, front));
            scene.Card.Add(f);
            if (spine >= 0)
            {
                var s = new Face { Origin = new(-W / 2, Hf / 2, Ds / 2), U = Vector3.UnitX, V = -Vector3.UnitZ, Normal = Vector3.UnitY, W = W, H = Ds, Fill = Paper };
                s.SetTexture(RenderPanel(session, spine));
                scene.Card.Add(s);
            }
            if (back >= 0)
            {
                var b = new Face { Origin = new(-W / 2, Hf / 2, -Ds / 2), U = Vector3.UnitX, V = -Vector3.UnitY, Normal = -Vector3.UnitZ, W = W, H = Hb, Fill = Paper };
                b.SetTexture(RenderPanel(session, back, Hb));
                scene.Card.Add(b);
            }

            cw = Math.Min(CassW, W - 1);
            ch = Math.Min(CassH, Hf - 1);
            ct = Math.Min(CassT, Ds - 0.8f);
            scene.CaseSize = new Vector3(Math.Max(W, CassW) + 8.4f, Hf + 4.5f, Ds + 4.3f);
            bool hiddenInserts = front > 0;
            scene.Title = (doc.FormatId == "custom" ? "WŁASNY FORMAT" : "J-CARD W PUDEŁKU") + (hiddenInserts ? " · WKŁADKI W ŚRODKU" : "");
        }

        // kaseta: strona A z przodu, strona B z tyłu (obrócona jak przy przekładaniu kasety)
        var cass = BoxFaces(new Vector3(cw, ch, ct));
        cass[0].SetTexture(RenderCassette(labelA, "A", scene.CassetteOnly));
        cass[1].SetTexture(RenderCassette(labelB, "B", scene.CassetteOnly));
        for (int i = 2; i < 6; i++) cass[i].Fill = ShellEdge;
        scene.Cassette.AddRange(cass);
        labelA?.Dispose();
        labelB?.Dispose();

        scene.Case.AddRange(BoxFaces(scene.CaseSize));
        return scene;
    }

    /// <summary>Renderuje jeden panel karty (bez spadu) do tekstury.</summary>
    private static CanvasRenderTarget RenderPanel(EditorSession session, int index, double? heightOverride = null)
    {
        var doc = session.Doc;
        double top = doc.PanelTop(index);
        double h = heightOverride ?? doc.Panels[index].Height;
        var rt = new CanvasRenderTarget(CanvasDevice.GetSharedDevice(),
            Math.Max(1, (float)(doc.Width * TexPxPerMm)), Math.Max(1, (float)(h * TexPxPerMm)), 96);
        using var ds = rt.CreateDrawingSession();
        ds.Clear(Microsoft.UI.Colors.White);
        ds.TextAntialiasing = CanvasTextAntialiasing.Grayscale;
        ds.Transform = Matrix3x2.CreateScale(TexPxPerMm) * Matrix3x2.CreateTranslation(0, (float)(-top * TexPxPerMm));
        CardRenderer.DrawCard(ds, doc, session.Assets, highQuality: true);
        return rt;
    }

    /// <summary>Rysuje obudowę kasety (wymiary rzeczywiste 100,4 × 63,8 mm) z etykietą.</summary>
    private static CanvasRenderTarget RenderCassette(CanvasRenderTarget? label, string side, bool cassetteOnly)
    {
        var rt = new CanvasRenderTarget(CanvasDevice.GetSharedDevice(), CassW * CassPxPerMm, CassH * CassPxPerMm, 96);
        using var ds = rt.CreateDrawingSession();
        ds.Clear(Microsoft.UI.Colors.Transparent);
        ds.Transform = Matrix3x2.CreateScale(CassPxPerMm);

        var shell = new Rect(0, 0, CassW, CassH);
        using (var body = new CanvasLinearGradientBrush(ds, Color.FromArgb(255, 62, 57, 53), Color.FromArgb(255, 30, 27, 25))
               { StartPoint = new Vector2(0, 0), EndPoint = new Vector2(0, CassH) })
        {
            ds.FillRoundedRectangle(shell, 3, 3, body);
        }
        ds.DrawRoundedRectangle(new Rect(0.3, 0.3, CassW - 0.6, CassH - 0.6), 2.8f, 2.8f, Color.FromArgb(255, 14, 12, 11), 0.6f);

        // etykieta
        var lr = new Rect(5.7, 3.5, 89, 42);
        using (var clip = CanvasGeometry.CreateRoundedRectangle(ds, lr, 2, 2))
        using (ds.CreateLayer(1f, clip))
        {
            if (label is not null)
            {
                ds.DrawImage(label, lr, new Rect(0, 0, label.Size.Width, label.Size.Height), 1f, CanvasImageInterpolation.HighQualityCubic);
            }
            else
            {
                ds.FillRectangle(lr, Color.FromArgb(255, 243, 233, 210));
                if (!cassetteOnly)
                {
                    string[] stripes = ["#FFF2B035", "#FFE8722C", "#FFC8402F", "#FF7A3B2E"];
                    for (int i = 0; i < stripes.Length; i++)
                        ds.FillRectangle(new Rect(lr.X, lr.Y + 31 + i * 2.2, lr.Width, 2.25), ColorUtil.Parse(stripes[i]));
                }
                var ink = Color.FromArgb(120, 43, 36, 32);
                ds.DrawLine((float)lr.X + 14, (float)lr.Y + 7, (float)lr.Right - 4, (float)lr.Y + 7, ink, 0.25f);
                ds.DrawLine((float)lr.X + 14, (float)lr.Y + 12, (float)lr.Right - 4, (float)lr.Y + 12, ink, 0.25f);
                using var fmt = new CanvasTextFormat { FontFamily = "Bahnschrift", FontSize = 9, FontWeight = new Windows.UI.Text.FontWeight { Weight = 700 } };
                ds.DrawText(side, (float)lr.X + 3, (float)lr.Y + 1, Color.FromArgb(255, 200, 64, 47), fmt);
            }
        }

        // okienko z taśmą i szpule
        var dark = Color.FromArgb(255, 24, 21, 19);
        var win = new Rect(36, 20, 28.4, 11);
        ds.FillRoundedRectangle(win, 1.5f, 1.5f, dark);
        using (var clip = CanvasGeometry.CreateRoundedRectangle(ds, win, 1.5f, 1.5f))
        using (ds.CreateLayer(1f, clip))
        {
            var tape = Color.FromArgb(255, 92, 58, 38);
            ds.FillCircle(28.9f, 25.5f, 15f, tape);
            ds.FillCircle(71.5f, 25.5f, 10.5f, tape);
            ds.FillRectangle(new Rect(win.X, win.Y, win.Width, 1.2), Color.FromArgb(50, 255, 255, 255));
        }
        foreach (var cx in new[] { 28.9f, 71.5f })
        {
            ds.FillCircle(cx, 25.5f, 6.6f, dark);
            ds.FillCircle(cx, 25.5f, 4.6f, Color.FromArgb(255, 236, 228, 212));
            ds.FillCircle(cx, 25.5f, 3.1f, dark);
            for (int k = 0; k < 6; k++)
            {
                double a = k * Math.PI / 3;
                ds.FillCircle(cx + (float)Math.Cos(a) * 3.1f, 25.5f + (float)Math.Sin(a) * 3.1f, 0.75f, Color.FromArgb(255, 236, 228, 212));
            }
        }

        // trapez na dole, otwory i śrubki
        using (var trap = CanvasGeometry.CreatePolygon(ds, [new(19, CassH), new(25, 50.5f), new(CassW - 25, 50.5f), new(CassW - 19, CassH)]))
        {
            ds.FillGeometry(trap, Color.FromArgb(255, 70, 64, 60));
            ds.DrawGeometry(trap, Color.FromArgb(255, 20, 18, 16), 0.4f);
        }
        foreach (var hx in new[] { 33f, CassW - 33f }) ds.FillCircle(hx, 57.5f, 2.1f, Color.FromArgb(255, 10, 9, 8));
        foreach (var hx in new[] { 45.5f, 52f }) ds.FillRectangle(new Rect(hx, 56, 2.9, 2.9), Color.FromArgb(255, 10, 9, 8));
        foreach (var (sx, sy) in new[] { (3.6f, 3.6f), (CassW - 3.6f, 3.6f), (3.6f, CassH - 3.6f), (CassW - 3.6f, CassH - 3.6f), (CassW / 2, 61f) })
        {
            ds.FillCircle(sx, sy, 1.4f, Color.FromArgb(255, 150, 142, 134));
            ds.DrawLine(sx - 0.9f, sy, sx + 0.9f, sy, Color.FromArgb(255, 60, 55, 50), 0.35f);
        }
        return rt;
    }

    // ================================================================== rysowanie

    private static float Rad(float deg) => deg * MathF.PI / 180f;

    private void DrawScene(CanvasDrawingSession ds, Vector2 size)
    {
        if (size.X < 2 || size.Y < 2) return;

        // tło: ciepła "scena" ze światłem
        var center = size / 2;
        using (var bg = new CanvasRadialGradientBrush(ds, Color.FromArgb(255, 70, 56, 46), Color.FromArgb(255, 20, 16, 13))
               { Center = center - new Vector2(0, size.Y * 0.08f), RadiusX = size.X * 0.75f, RadiusY = size.Y * 0.85f })
        {
            ds.FillRectangle(0, 0, size.X, size.Y, bg);
        }

        var scene = _scene;
        if (scene is null) return;

        float s = Math.Min(size.X, size.Y) / 150f * _zoom;
        var view = Matrix4x4.CreateRotationY(Rad(_yaw)) * Matrix4x4.CreateRotationX(Rad(_pitch));
        var proj = new Matrix4x4(1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, -1f / CameraDistance, 0, 0, 0, 1);
        var screen = Matrix4x4.CreateScale(s, s, 1) * Matrix4x4.CreateTranslation(center.X, center.Y, 0);
        var vps = view * proj * screen;

        // cień pod obiektem
        var shadowC = center + new Vector2(0, s * 50);
        using (var sh = new CanvasRadialGradientBrush(ds, Color.FromArgb(120, 0, 0, 0), Color.FromArgb(0, 0, 0, 0))
               { Center = shadowC, RadiusX = s * 66, RadiusY = s * 9 })
        {
            ds.FillEllipse(shadowC, s * 66, s * 9, sh);
        }

        var camera = new Vector3(0, 0, CameraDistance);
        bool Visible(Face f)
        {
            var n = Vector3.TransformNormal(f.Normal, view);
            var c = Vector3.Transform(f.Center, view);
            return Vector3.Dot(n, camera - c) > 0.001f;
        }
        float Light(Face f)
        {
            var n = Vector3.Normalize(Vector3.TransformNormal(f.Normal, view));
            return 0.64f + 0.36f * Math.Max(0, Vector3.Dot(n, LightDir));
        }
        Vector2 P(Vector3 p)
        {
            var v = Vector4.Transform(new Vector4(p, 1), vps);
            return new Vector2(v.X / v.W, v.Y / v.W);
        }
        Vector2[] Poly(Face f) => [.. f.Corners.Select(P)];

        // 1. tylne ścianki pudełka (od środka)
        if (_showCase)
            foreach (var f in scene.Case.Where(f => !Visible(f))) DrawPlastic(ds, Poly(f), front: false);

        // 2. karta widziana od wewnątrz (niezadrukowany papier)
        foreach (var f in scene.Card.Where(f => !Visible(f)))
        {
            using var geo = CanvasGeometry.CreatePolygon(ds, Poly(f));
            ds.FillGeometry(geo, Shade(Paper, 0.75f));
        }

        // 3. kaseta (wypukła bryła — wystarczy pominąć niewidoczne ścianki)
        foreach (var f in scene.Cassette.Where(Visible)) DrawFace(ds, f, vps, Light(f), Poly(f));

        // 4. karta od zewnątrz
        foreach (var f in scene.Card.Where(Visible)) DrawFace(ds, f, vps, Light(f), Poly(f));

        // 5. przednie ścianki pudełka z odblaskiem
        if (_showCase)
        {
            for (int i = 0; i < scene.Case.Count; i++)
            {
                var f = scene.Case[i];
                if (!Visible(f)) continue;
                var poly = Poly(f);
                DrawPlastic(ds, poly, front: true);
                if (i < 2) DrawLidFrame(ds, f, P); // wytłoczona ramka wieczka (przód i tył)
            }
        }
    }

    private static Color Shade(Color c, float k) =>
        Color.FromArgb(c.A, (byte)(c.R * k), (byte)(c.G * k), (byte)(c.B * k));

    private static void DrawFace(CanvasDrawingSession ds, Face f, Matrix4x4 vps, float light, Vector2[] poly)
    {
        if (f.Effect is { } fx && f.Texture is { } tex)
        {
            float ku = f.W / (float)tex.Size.Width, kv = f.H / (float)tex.Size.Height;
            var m = new Matrix4x4(
                f.U.X * ku, f.U.Y * ku, f.U.Z * ku, 0,
                f.V.X * kv, f.V.Y * kv, f.V.Z * kv, 0,
                f.Normal.X, f.Normal.Y, f.Normal.Z, 0,
                f.Origin.X, f.Origin.Y, f.Origin.Z, 1);
            fx.TransformMatrix = m * vps;
            ds.DrawImage(fx);
            if (light < 0.995f)
            {
                using var geo = CanvasGeometry.CreatePolygon(ds, poly);
                ds.FillGeometry(geo, Color.FromArgb((byte)((1 - light) * 200), 0, 0, 0));
            }
        }
        else
        {
            using var geo = CanvasGeometry.CreatePolygon(ds, poly);
            ds.FillGeometry(geo, Shade(f.Fill, light));
        }
    }

    private static readonly CanvasGradientStop[] GlareStops =
    [
        new() { Position = 0f, Color = Color.FromArgb(52, 255, 255, 255) },
        new() { Position = 0.38f, Color = Color.FromArgb(10, 255, 255, 255) },
        new() { Position = 0.46f, Color = Color.FromArgb(34, 255, 255, 255) },
        new() { Position = 0.52f, Color = Color.FromArgb(8, 255, 255, 255) },
        new() { Position = 1f, Color = Color.FromArgb(18, 220, 235, 255) },
    ];

    private static void DrawPlastic(CanvasDrawingSession ds, Vector2[] poly, bool front)
    {
        using var geo = CanvasGeometry.CreatePolygon(ds, poly);
        if (front)
        {
            using var glare = new CanvasLinearGradientBrush(ds, GlareStops) { StartPoint = poly[0], EndPoint = poly[2] };
            ds.FillGeometry(geo, glare);
            ds.DrawGeometry(geo, Color.FromArgb(110, 255, 255, 255), 1.2f);
        }
        else
        {
            ds.FillGeometry(geo, Color.FromArgb(14, 220, 235, 255));
            ds.DrawGeometry(geo, Color.FromArgb(45, 255, 255, 255), 0.8f);
        }
    }

    private static void DrawLidFrame(CanvasDrawingSession ds, Face f, Func<Vector3, Vector2> project)
    {
        const float inset = 2.4f;
        var o = f.Origin + f.U * inset + f.V * inset;
        float w = f.W - 2 * inset, h = f.H - 2 * inset;
        Vector2[] pts = [project(o), project(o + f.U * w), project(o + f.U * w + f.V * h), project(o + f.V * h)];
        using var geo = CanvasGeometry.CreatePolygon(ds, pts);
        ds.DrawGeometry(geo, Color.FromArgb(40, 255, 255, 255), 1f);
    }
}
