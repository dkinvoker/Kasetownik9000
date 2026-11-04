using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using Kasetownik9000.Editor;
using Kasetownik9000.Model;
using Kasetownik9000.Output;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.Foundation;
using Windows.Graphics;
using Windows.System;
using Windows.UI.Core;
using DispatcherQueueTimer = Microsoft.UI.Dispatching.DispatcherQueueTimer;

namespace Kasetownik9000;

public sealed partial class MainWindow : Window
{
    private EditorSession _session = null!;
    private bool _ui;
    private bool _dialogOpen;
    private bool _forceClose;
    private bool _is3D;
    private readonly nint _hwnd;
    private readonly DispatcherQueueTimer _toastTimer;

    private PrintOptions _printOptions = new();
    private PrintOptions _pdfOptions = new();
    private PrintOptions _pngOptions = new() { Dpi = 300 };

    public MainWindow()
    {
        InitializeComponent();
        _hwnd = Win32Interop.GetWindowFromWindowId(AppWindow.Id);

        _toastTimer = DispatcherQueue.CreateTimer();
        _toastTimer.Interval = TimeSpan.FromSeconds(6);
        _toastTimer.Tick += (t, _) => { t.Stop(); Toast.IsOpen = false; };

        SetupWindow();
        SetupControls();

        var doc = CardFormats.Get("jcard").CreateDocument();
        Starters.Apply(doc, "retro");
        LoadSession(new EditorSession(doc));

        var args = Environment.GetCommandLineArgs();
        if (args.Length > 1 && File.Exists(args[1]))
            DispatcherQueue.TryEnqueue(async () => await OpenPathAsync(args[1]));
    }

    // ================================================================== okno

    private void SetupWindow()
    {
        Title = "Kasetownik 9000";
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(TitleBar);

        var tb = AppWindow.TitleBar;
        tb.PreferredHeightOption = TitleBarHeightOption.Tall;
        var cream = Windows.UI.Color.FromArgb(255, 243, 233, 210);
        tb.ButtonBackgroundColor = Colors.Transparent;
        tb.ButtonInactiveBackgroundColor = Colors.Transparent;
        tb.ButtonForegroundColor = cream;
        tb.ButtonInactiveForegroundColor = Windows.UI.Color.FromArgb(255, 140, 128, 112);
        tb.ButtonHoverBackgroundColor = Windows.UI.Color.FromArgb(255, 58, 48, 42);
        tb.ButtonHoverForegroundColor = cream;
        tb.ButtonPressedBackgroundColor = Windows.UI.Color.FromArgb(255, 232, 114, 44);
        tb.ButtonPressedForegroundColor = Windows.UI.Color.FromArgb(255, 30, 25, 22);

        try { AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico")); } catch { }

        double scale = Math.Max(1, Native.GetDpiForWindow(_hwnd)) / 96.0;
        var area = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary).WorkArea;
        int w = Math.Min((int)(1480 * scale), area.Width - 40);
        int h = Math.Min((int)(920 * scale), area.Height - 40);
        AppWindow.MoveAndResize(new RectInt32(area.X + (area.Width - w) / 2, area.Y + (area.Height - h) / 2, w, h));
        if (AppWindow.Presenter is OverlappedPresenter op)
        {
            op.PreferredMinimumWidth = (int)(1100 * scale);
            op.PreferredMinimumHeight = (int)(640 * scale);
        }

        AppWindow.Closing += OnAppWindowClosing;
        Closed += (_, _) => { CardEditor.Shutdown(); Preview3D.Shutdown(); };
        RootHost.Loaded += (_, _) => UpdateCaptionInset();
        // wąskie okno: chowamy wyświetlacz formatu, żeby klawisze się zmieściły
        RootHost.SizeChanged += (_, e) =>
            FormatLcdBorder.Visibility = e.NewSize.Width < 1420 ? Visibility.Collapsed : Visibility.Visible;
        AppWindow.Changed += (_, _) => UpdateCaptionInset();
    }

    private void UpdateCaptionInset()
    {
        var xr = RootHost.XamlRoot;
        if (xr is null) return;
        double inset = AppWindow.TitleBar.RightInset / xr.RasterizationScale;
        if (inset > 0) CaptionColumn.Width = new GridLength(inset + 8);
    }

    private async void OnAppWindowClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_forceClose || !_session.IsDirty) return;
        args.Cancel = true;
        if (_dialogOpen) return;
        if (await ConfirmDiscardAsync())
        {
            _forceClose = true;
            Close();
        }
    }

    // ================================================================== sesja

    private void LoadSession(EditorSession s)
    {
        if (_session is not null)
        {
            _session.Changed -= OnSessionChanged;
            _session.SelectionChanged -= OnSelectionChanged;
            _session.DirtyChanged -= UpdateTitle;
        }
        _session = s;
        s.Changed += OnSessionChanged;
        s.SelectionChanged += OnSelectionChanged;
        s.DirtyChanged += UpdateTitle;
        CardEditor.Session = s;
        Preview3D.Session = s;
        _stripeKey = null;
        OnSessionChanged(ChangeFlags.All);
        UpdateTitle();
    }

    private void OnSessionChanged(ChangeFlags f)
    {
        if (f.HasFlag(ChangeFlags.Layers)) RebuildLayers();
        if (f.HasFlag(ChangeFlags.Document)) RefreshDocPanel();
        if (f.HasFlag(ChangeFlags.Properties)) RefreshProperties();
        UpdateCommandStates();
        UpdateFormatLcd();
    }

    private void OnSelectionChanged()
    {
        SyncLayerSelection();
        UpdateCommandStates();
    }

    private void UpdateTitle()
    {
        var name = _session.DisplayName;
        DocTitleText.Text = _session.IsDirty ? $"{name}  •  niezapisane" : name;
        Title = $"{name}{(_session.IsDirty ? "*" : "")} — Kasetownik 9000";
    }

    private void UpdateCommandStates()
    {
        UndoButton.IsEnabled = _session.CanUndo;
        RedoButton.IsEnabled = _session.CanRedo;
        bool sel = _session.Selected is not null;
        LayerUpButton.IsEnabled = LayerDownButton.IsEnabled = LayerLockButton.IsEnabled =
            LayerHideButton.IsEnabled = LayerDeleteButton.IsEnabled = sel;
    }

    private void UpdateFormatLcd()
    {
        var d = _session.Doc;
        var f = CardFormats.All.FirstOrDefault(x => x.Id == d.FormatId);
        string name = f?.Id switch
        {
            "labels" => "NAKLEJKI",
            "custom" or null => "WŁASNY",
            _ => d.Panels.Count switch { 3 => "J-CARD", 4 => "J-CARD 4P", 5 => "J-CARD 5P", _ => "J-CARD" },
        };
        FormatLcd.Text = string.Format(CultureInfo.InvariantCulture, "{0}  {1:0.#}×{2:0.#} MM", name, d.Width, d.Height);
    }

    // ================================================================== warstwy

    private void RebuildLayers()
    {
        _ui = true;
        try
        {
            var items = new List<LayerItem>();
            var els = _session.Doc.Elements;
            for (int i = els.Count - 1; i >= 0; i--) items.Add(new LayerItem(els[i]));
            LayersList.ItemsSource = items;
            SelectLayerItem(items);
        }
        finally { _ui = false; }
    }

    private void SyncLayerSelection()
    {
        if (LayersList.ItemsSource is not List<LayerItem> items) return;
        _ui = true;
        try { SelectLayerItem(items); }
        finally { _ui = false; }
    }

    private void SelectLayerItem(List<LayerItem> items)
    {
        var item = items.FirstOrDefault(i => ReferenceEquals(i.Element, _session.Selected));
        LayersList.SelectedItem = item;
        if (item is not null) LayersList.ScrollIntoView(item);
    }

    private void Layers_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_ui) return;
        _session.Select((LayersList.SelectedItem as LayerItem)?.Element);
    }

    // ================================================================== dodawanie elementów

    private Vector2 InsertPoint() => CardEditor.ViewCenterWorld();

    private void AfterAdd(bool focusText = false)
    {
        if (focusText)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                TextInput.Focus(FocusState.Programmatic);
                TextInput.SelectAll();
            });
        }
        else
        {
            CardEditor.Focus(FocusState.Programmatic);
        }
    }

    private void AddText_Click(object sender, RoutedEventArgs e)
    {
        var c = InsertPoint();
        _session.AddElement(Starters.Text("", "Nowy tekst", c.X - 30, c.Y - 5, 60, 10, 16));
        AfterAdd(focusText: true);
    }

    private async void AddImage_Click(object sender, RoutedEventArgs e)
    {
        var path = await PickOpenAsync(ImageExtensions, pictures: true);
        if (path is not null) await AddImageFromFileAsync(path, null);
    }

    private void AddRect_Click(object sender, RoutedEventArgs e)
    {
        var c = InsertPoint();
        _session.AddElement(new ShapeElement { Shape = ShapeKind.Rectangle, X = c.X - 15, Y = c.Y - 8, Width = 30, Height = 16, FillColor = "#FFE8722C" });
        AfterAdd();
    }

    private void AddEllipse_Click(object sender, RoutedEventArgs e)
    {
        var c = InsertPoint();
        _session.AddElement(new ShapeElement { Shape = ShapeKind.Ellipse, X = c.X - 9, Y = c.Y - 9, Width = 18, Height = 18, FillColor = "#FFC8402F" });
        AfterAdd();
    }

    private void AddLine_Click(object sender, RoutedEventArgs e)
    {
        var c = InsertPoint();
        _session.AddElement(new ShapeElement
        {
            Shape = ShapeKind.Line, X = c.X - 25, Y = c.Y - 1, Width = 50, Height = 2, HasFill = false,
            StrokeColor = "#FF2B2420", StrokeWidth = 0.5,
        });
        AfterAdd();
    }

    private void AddStripes_Click(object sender, RoutedEventArgs e)
    {
        var c = InsertPoint();
        var d = _session.Doc;
        _session.AddElement(Starters.Stripes("", -d.Bleed, c.Y - 6, d.Width + 2 * d.Bleed, 12, StripePalette.All[0].Colors));
        AfterAdd();
    }

    private void AddTracklist_Click(object sender, RoutedEventArgs e)
    {
        var d = _session.Doc;
        var c = InsertPoint();
        int pi = d.PanelIndexAt(c.Y);
        double top = d.PanelTop(pi), h = Math.Max(20, Math.Min(d.Panels[pi].Height - 8, 55));
        double y = d.Panels[pi].Height > h + 8 ? top + (d.Panels[pi].Height - h) / 2 : c.Y - h / 2;
        double colW = d.Width / 2 - 8;
        var a = Starters.Text("Strona A", Starters.TracksA, 6, y, colW, h, 9, autoFit: true);
        var b = Starters.Text("Strona B", Starters.TracksB, d.Width / 2 + 2, y, colW, h, 9, autoFit: true);
        _session.AddElements([a, b]);
        AfterAdd(focusText: true);
    }

    // ================================================================== polecenia

    private async void New_Click(object sender, RoutedEventArgs e) => await NewAsync();
    private async void Open_Click(object sender, RoutedEventArgs e) => await OpenAsync();
    private async void Save_Click(object sender, RoutedEventArgs e) => await SaveAsync(IsDown(VirtualKey.Shift));
    private void Undo_Click(object sender, RoutedEventArgs e) => _session.Undo();
    private void Redo_Click(object sender, RoutedEventArgs e) => _session.Redo();
    private async void ExportPng_Click(object sender, RoutedEventArgs e) => await ExportPngAsync();
    private async void ExportPdf_Click(object sender, RoutedEventArgs e) => await ExportPdfAsync();
    private async void Print_Click(object sender, RoutedEventArgs e) => await PrintAsync();
    private void ZoomIn_Click(object sender, RoutedEventArgs e) => ZoomBy(1.25f);
    private void ZoomOut_Click(object sender, RoutedEventArgs e) => ZoomBy(0.8f);
    private void ZoomFit_Click(object sender, RoutedEventArgs e) { if (_is3D) Preview3D.ResetView(); else CardEditor.FitToView(); }
    private void Zoom100_Click(object sender, RoutedEventArgs e) { if (!_is3D) CardEditor.SetZoomPercent(100); }

    private void ZoomBy(float f)
    {
        if (_is3D) Preview3D.ZoomBy(f);
        else CardEditor.ZoomBy(f);
    }

    // ================================================================== podgląd 3D

    private void Toggle3D_Click(object sender, RoutedEventArgs e) => Set3DMode(!_is3D);

    private void Set3DMode(bool on)
    {
        if (_is3D == on) return;
        _is3D = on;
        CardEditor.Visibility = on ? Visibility.Collapsed : Visibility.Visible;
        Preview3D.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
        Preview3D.IsActive = on;
        View3DLed.Fill = new Microsoft.UI.Xaml.Media.SolidColorBrush(on
            ? Windows.UI.Color.FromArgb(255, 255, 92, 52)
            : Windows.UI.Color.FromArgb(255, 74, 42, 32));
        ZoomLcd.Text = on ? "3D" : string.Format(CultureInfo.InvariantCulture, "{0:0}%", CardEditor.ZoomPercent);
        if (!on) CardEditor.Focus(FocusState.Programmatic);
    }

    private async Task Save3DImageAsync()
    {
        var path = await PickSaveAsync(SuggestedName + " 3D", "Obraz PNG", ".png");
        if (path is null) return;
        try
        {
            await Preview3D.ExportPngAsync(path);
            ShowToast($"Zapisano wizualizację: {Path.GetFileName(path)}", openPath: path);
        }
        catch (Exception ex)
        {
            ShowToast($"Nie udało się zapisać obrazu: {ex.Message}", InfoBarSeverity.Error);
        }
    }

    private void Delete_Click(object sender, RoutedEventArgs e) => _session.DeleteSelected();
    private void Duplicate_Click(object sender, RoutedEventArgs e) => _session.DuplicateSelected();
    private void Forward_Click(object sender, RoutedEventArgs e) => _session.Reorder(EditorSession.Order.Forward);
    private void Backward_Click(object sender, RoutedEventArgs e) => _session.Reorder(EditorSession.Order.Backward);
    private void BringToFront_Click(object sender, RoutedEventArgs e) => _session.Reorder(EditorSession.Order.Front);
    private void SendToBack_Click(object sender, RoutedEventArgs e) => _session.Reorder(EditorSession.Order.Back);

    private void ToggleLock_Click(object sender, RoutedEventArgs e) =>
        Edit<CardElement>(null, el => el.Locked = !el.Locked, ChangeFlags.Visual | ChangeFlags.Layers | ChangeFlags.Properties);

    private void ToggleHide_Click(object sender, RoutedEventArgs e) =>
        Edit<CardElement>(null, el => el.Hidden = !el.Hidden, ChangeFlags.Visual | ChangeFlags.Layers | ChangeFlags.Properties);

    // ================================================================== klawiatura

    private static bool IsDown(VirtualKey key) =>
        InputKeyboardSource.GetKeyStateForCurrentThread(key).HasFlag(CoreVirtualKeyStates.Down);

    private async void Root_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (_dialogOpen) return;
        bool ctrl = IsDown(VirtualKey.Control), shift = IsDown(VirtualKey.Shift);
        var focused = FocusManager.GetFocusedElement(RootHost.XamlRoot);
        bool typing = focused is TextBox or PasswordBox or AutoSuggestBox or ComboBox or Slider or ListViewItem;

        if (e.Key == VirtualKey.F3 || (_is3D && e.Key == VirtualKey.Escape && !typing))
        {
            e.Handled = true;
            Set3DMode(e.Key == VirtualKey.F3 && !_is3D);
            return;
        }

        if (ctrl)
        {
            switch (e.Key)
            {
                case VirtualKey.N: e.Handled = true; await NewAsync(); return;
                case VirtualKey.O: e.Handled = true; await OpenAsync(); return;
                case VirtualKey.S: e.Handled = true; await SaveAsync(shift); return;
                case VirtualKey.P: e.Handled = true; await PrintAsync(); return;
                case VirtualKey.E: e.Handled = true; await ExportPngAsync(); return;
                case VirtualKey.Number0 or VirtualKey.NumberPad0: e.Handled = true; ZoomFit_Click(this, new RoutedEventArgs()); return;
                case VirtualKey.Number1 or VirtualKey.NumberPad1: e.Handled = true; Zoom100_Click(this, new RoutedEventArgs()); return;
                case VirtualKey.Add or (VirtualKey)187: e.Handled = true; ZoomBy(1.25f); return;
                case VirtualKey.Subtract or (VirtualKey)189: e.Handled = true; ZoomBy(0.8f); return;
            }
            if (typing) return;
            switch (e.Key)
            {
                case VirtualKey.Z when shift: _session.Redo(); break;
                case VirtualKey.Z: _session.Undo(); break;
                case VirtualKey.Y: _session.Redo(); break;
                case VirtualKey.D: _session.DuplicateSelected(); break;
                case VirtualKey.C: CopySelected(); break;
                case VirtualKey.X: if (CopySelected()) _session.DeleteSelected(); break;
                case VirtualKey.V: e.Handled = true; await PasteAsync(null); return;
                default: return;
            }
            e.Handled = true;
            return;
        }

        if (typing) return;
        double step = shift ? 5 : 0.5;
        switch (e.Key)
        {
            case VirtualKey.Delete or VirtualKey.Back when _session.Selected is not null:
                _session.DeleteSelected();
                break;
            case VirtualKey.Escape:
                _session.Select(null);
                break;
            case VirtualKey.Left: Nudge(-step, 0); break;
            case VirtualKey.Right: Nudge(step, 0); break;
            case VirtualKey.Up: Nudge(0, -step); break;
            case VirtualKey.Down: Nudge(0, step); break;
            default: return;
        }
        e.Handled = true;
    }

    private void Nudge(double dx, double dy)
    {
        if (_session.Selected is null or { Locked: true }) return;
        Edit<CardElement>("nudge", el => { el.X = Math.Round(el.X + dx, 2); el.Y = Math.Round(el.Y + dy, 2); },
            ChangeFlags.Visual | ChangeFlags.Properties);
    }

    // ================================================================== menu kontekstowe

    private void ShowContextMenu(Point pos, CardElement? hit)
    {
        var menu = new MenuFlyout();
        void Add(string text, string glyph, string? accel, Action action)
        {
            var item = new MenuFlyoutItem { Text = text, Icon = new FontIcon { Glyph = glyph } };
            if (accel is not null) item.KeyboardAcceleratorTextOverride = accel;
            item.Click += (_, _) => action();
            menu.Items.Add(item);
        }

        if (hit is not null)
        {
            Add("Duplikuj", "", "Ctrl+D", _session.DuplicateSelected);
            Add("Kopiuj", "", "Ctrl+C", () => CopySelected());
            Add("Usuń", "", "Del", _session.DeleteSelected);
            menu.Items.Add(new MenuFlyoutSeparator());
            Add("Na wierzch", "", null, () => _session.Reorder(EditorSession.Order.Front));
            Add("Wyżej", "", null, () => _session.Reorder(EditorSession.Order.Forward));
            Add("Niżej", "", null, () => _session.Reorder(EditorSession.Order.Backward));
            Add("Na spód", "", null, () => _session.Reorder(EditorSession.Order.Back));
            menu.Items.Add(new MenuFlyoutSeparator());
            Add("Rozciągnij na panel", "", null, () => FillPanel_Click(this, new RoutedEventArgs()));
            Add(hit.Locked ? "Odblokuj" : "Zablokuj", hit.Locked ? "" : "", null, () => ToggleLock_Click(this, new RoutedEventArgs()));
            Add("Ukryj", "", null, () => ToggleHide_Click(this, new RoutedEventArgs()));
            menu.Items.Add(new MenuFlyoutSeparator());
        }
        Add("Wklej", "", "Ctrl+V", async () => await PasteAsync(null));
        menu.ShowAt(CardEditor, new Microsoft.UI.Xaml.Controls.Primitives.FlyoutShowOptions { Position = pos });
    }

    // ================================================================== komunikaty

    private void ShowToast(string message, InfoBarSeverity severity = InfoBarSeverity.Success, string? openPath = null)
    {
        Toast.Title = severity switch
        {
            InfoBarSeverity.Error => "Ups!",
            InfoBarSeverity.Warning => "Uwaga",
            InfoBarSeverity.Informational => "",
            _ => "Gotowe",
        };
        Toast.Message = message;
        Toast.Severity = severity;
        if (openPath is not null)
        {
            var btn = new HyperlinkButton { Content = "Otwórz" };
            btn.Click += (_, _) =>
            {
                try { Process.Start(new ProcessStartInfo(openPath) { UseShellExecute = true }); } catch { }
            };
            Toast.ActionButton = btn;
        }
        else
        {
            Toast.ActionButton = null;
        }
        Toast.IsOpen = true;
        _toastTimer.Stop();
        _toastTimer.Interval = TimeSpan.FromSeconds(severity == InfoBarSeverity.Error ? 10 : 6);
        _toastTimer.Start();
    }
}
