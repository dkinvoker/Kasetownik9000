using System.Globalization;
using Kasetownik9000.Controls;
using Kasetownik9000.Editor;
using Kasetownik9000.Model;
using Microsoft.Graphics.Canvas.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Windows.Globalization.NumberFormatting;

namespace Kasetownik9000;

/// <summary>Panel właściwości (prawa kolumna).</summary>
public sealed partial class MainWindow
{
    private List<FontItem> _fonts = [];
    private string? _stripeKey;

    private static DecimalFormatter MakeFormatter(int digits) => new()
    {
        FractionDigits = digits,
        IntegerDigits = 1,
        IsGrouped = false,
        NumberRounder = new IncrementNumberRounder
        {
            Increment = Math.Pow(10, -digits),
            RoundingAlgorithm = RoundingAlgorithm.RoundHalfUp,
        },
    };

    private void SetupControls()
    {
        var f1 = MakeFormatter(1);
        var f2 = MakeFormatter(2);
        foreach (var nb in new[] { CardWidthBox, BleedBox, XBox, YBox, WBox, HBox, RotationBox, FontSizeBox, LineSpacingBox,
                     ShadowOffsetBox, StrokeWidthBox, CornerRadiusBox, StripeGapBox })
        {
            nb.SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact;
            nb.SmallChange = 0.5;
            nb.LargeChange = 5;
            nb.NumberFormatter = f1;
            nb.HorizontalAlignment = HorizontalAlignment.Stretch;
        }
        RotationBox.SmallChange = 1;
        FontSizeBox.SmallChange = 1;
        LineSpacingBox.SmallChange = 0.1;
        LineSpacingBox.NumberFormatter = f2;
        StrokeWidthBox.SmallChange = 0.1;
        StrokeWidthBox.NumberFormatter = f2;
        ShadowOffsetBox.SmallChange = 0.1;
        ShadowOffsetBox.NumberFormatter = f2;
        BleedBox.SmallChange = 0.5;

        FormatCombo.ItemsSource = CardFormats.All.Select(f => f.Name).ToList();
        FitCombo.ItemsSource = new List<string> { "Wypełnij ramkę (przytnij)", "Zmieść cały obraz", "Rozciągnij" };
        FilterCombo.ItemsSource = new List<string> { "Bez filtra", "Czarno-biały", "Sepia (retro)" };
        ShapeKindCombo.ItemsSource = new List<string> { "Prostokąt", "Elipsa", "Linia" };
        PaletteCombo.ItemsSource = StripePalette.All.Select(p => p.Name).ToList();
        BuildFontList();

        CardBackgroundButton.AlphaEnabled = false;
        CardBackgroundButton.ColorChanged += (_, c) => EditDoc("bg", d => d.Background = ColorUtil.ToHex(c));
        TextColorButton.ColorChanged += (_, c) => Edit<TextElement>("tcolor", t => t.Color = ColorUtil.ToHex(c));
        ShadowColorButton.ColorChanged += (_, c) => Edit<TextElement>("scolor", t => t.ShadowColor = ColorUtil.ToHex(c));
        FillColorButton.ColorChanged += (_, c) => Edit<ShapeElement>("fill", s => { s.FillColor = ColorUtil.ToHex(c); s.HasFill = true; },
            ChangeFlags.Visual | ChangeFlags.Properties);
        StrokeColorButton.ColorChanged += (_, c) => Edit<ShapeElement>("stroke", s =>
        {
            s.StrokeColor = ColorUtil.ToHex(c);
            if (s.Shape != ShapeKind.Line) s.HasStroke = true;
        }, ChangeFlags.Visual | ChangeFlags.Properties);

        CardEditor.ZoomChanged += () =>
        {
            if (!_is3D) ZoomLcd.Text = string.Format(CultureInfo.InvariantCulture, "{0:0}%", CardEditor.ZoomPercent);
        };
        Preview3D.SaveImageRequested += async () => await Save3DImageAsync();
        Preview3D.SaveGifRequested += async () => await Save3DGifAsync();
        SetupAddCardMenu();
        SetupInsertFieldMenu();
        CardEditor.PointerWorldMoved += p => CursorLcd.Text = p is { } w
            ? string.Format(CultureInfo.InvariantCulture, "X {0,6:0.0}   Y {1,6:0.0}  MM", w.X, w.Y)
            : "X  ---.-   Y  ---.-  MM";
        CardEditor.EditTextRequested += _ =>
        {
            PropsScroller.ChangeView(null, 0, null);
            TextInput.Focus(FocusState.Programmatic);
            TextInput.SelectAll();
        };
        CardEditor.ContextMenuRequested += ShowContextMenu;
        CardEditor.FilesDropped += async (paths, world) => await HandleDroppedFilesAsync(paths, world);
        RootHost.KeyDown += Root_KeyDown;
    }

    private void BuildFontList()
    {
        string[] names;
        try { names = CanvasTextFormat.GetSystemFontFamilies(); }
        catch { names = ["Bahnschrift", "Segoe UI", "Arial", "Consolas", "Ink Free", "Georgia", "Impact"]; }
        _fonts = [.. names.Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.CurrentCultureIgnoreCase).Select(n => new FontItem(n))];
        FontCombo.ItemsSource = _fonts;
    }

    private void SelectFont(string name)
    {
        var item = _fonts.FirstOrDefault(f => string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase));
        if (item is null && !string.IsNullOrWhiteSpace(name))
        {
            item = new FontItem(name);
            _fonts = [item, .. _fonts];
            FontCombo.ItemsSource = _fonts;
        }
        FontCombo.SelectedItem = item;
    }

    private static void SetNum(NumberBox nb, double v)
    {
        if (double.IsNaN(nb.Value) || Math.Abs(nb.Value - v) > 1e-6) nb.Value = v;
    }

    private static bool Same(double a, double b) => Math.Abs(a - b) < 1e-9;

    private static void SetText(TextBox box, string value)
    {
        if (box.Text != value) box.Text = value;
    }

    // ================================================================== album i pola

    private void AlbumField_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_ui || sender is not TextBox box) return;
        var a = _session.Project.Album;
        var text = box.Text;
        // TextChanged przychodzi asynchronicznie — reaguj tylko na faktyczną zmianę
        if (ReferenceEquals(box, AlbumTitleBox) && a.Title != text) _session.EditProject("title", p => p.Album.Title = text);
        else if (ReferenceEquals(box, AlbumArtistBox) && a.Artist != text) _session.EditProject("artist", p => p.Album.Artist = text);
        else if (ReferenceEquals(box, AlbumYearBox) && a.Year != text) _session.EditProject("year", p => p.Album.Year = text);
        else if (ReferenceEquals(box, AlbumDateBox) && a.Date != text) _session.EditProject("date", p => p.Album.Date = text);
        else if (ReferenceEquals(box, AlbumNoteBox) && a.Note != text) _session.EditProject("note", p => p.Album.Note = text);
    }

    private void SetupInsertFieldMenu()
    {
        var menu = new MenuFlyout { Placement = Microsoft.UI.Xaml.Controls.Primitives.FlyoutPlacementMode.BottomEdgeAlignedLeft };
        foreach (var field in TextResolver.Fields)
        {
            var item = new MenuFlyoutItem { Text = $"{field.Label}   {field.Token}" };
            item.Click += (_, _) => InsertIntoText(field.Token);
            menu.Items.Add(item);
        }
        InsertFieldButton.Flyout = menu;
    }

    private void InsertIntoText(string token)
    {
        if (_session.Selected is not TextElement) return;
        int pos = Math.Clamp(TextInput.SelectionStart, 0, TextInput.Text.Length);
        int len = Math.Clamp(TextInput.SelectionLength, 0, TextInput.Text.Length - pos);
        TextInput.Text = TextInput.Text.Remove(pos, len).Insert(pos, token);
        TextInput.SelectionStart = pos + token.Length;
        TextInput.Focus(FocusState.Programmatic);
    }

    private void UpdateTapeLcd()
    {
        var tape = _session.Project.Tape;
        int n = tape.Tracks.Count;
        TapeLcdTop.Text = $"{tape.TapeName}   {n} {(n == 1 ? "UTWÓR" : n is >= 2 and <= 4 ? "UTWORY" : "UTWORÓW")}";
        double a = TapeMath.SideSeconds(tape, 0), b = TapeMath.SideSeconds(tape, 1), cap = tape.SideCapacitySeconds;
        TapeLcdSides.Text = $"A {TextResolver.FormatTime(a)}   B {TextResolver.FormatTime(b)}";
        var over = a > cap || b > cap;
        TapeLcdSides.Foreground = over
            ? new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 92, 52))
            : (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["RetroLcdFgBrush"];
    }

    private static void SetSlider(Slider s, double v)
    {
        if (Math.Abs(s.Value - v) > 1e-6) s.Value = v;
    }

    // ================================================================== edycja

    private void Edit<T>(string? key, Action<T> change, ChangeFlags flags = ChangeFlags.Visual) where T : CardElement
    {
        if (_ui || _session.Selected is not T e) return;
        _session.Edit(key is null ? null : $"{key}:{e.Id}", () => change(e), flags);
    }

    private void EditDoc(string? key, Action<CardDocument> change, ChangeFlags flags = ChangeFlags.Visual)
    {
        if (_ui) return;
        _session.Edit(key is null ? null : "doc:" + key, () => change(_session.Doc), flags);
    }

    // ================================================================== odświeżanie

    private void RefreshDocPanel()
    {
        bool old = _ui;
        _ui = true;
        try
        {
            var album = _session.Project.Album;
            SetText(AlbumTitleBox, album.Title);
            SetText(AlbumArtistBox, album.Artist);
            SetText(AlbumYearBox, album.Year);
            SetText(AlbumDateBox, album.Date);
            SetText(AlbumNoteBox, album.Note);

            var d = _session.Doc;
            int idx = Array.FindIndex(CardFormats.All, f => f.Id == d.FormatId);
            FormatCombo.SelectedIndex = idx;
            FormatDescription.Text = idx >= 0 ? $"{CardFormats.All[idx].SizeText} · {CardFormats.All[idx].Description}" : "";
            SetNum(CardWidthBox, d.Width);
            SetNum(BleedBox, d.Bleed);
            CardBackgroundButton.Hex = d.Background;
            RebuildPanelsEditor();

            // wnętrze: wymiary i panele są zawsze takie jak w karcie zewnętrznej — zmienia się je tam
            bool structural = !d.IsInside;
            FormatCombo.IsEnabled = CardWidthBox.IsEnabled = BleedBox.IsEnabled = PanelsHost.IsHitTestVisible = structural;
            PanelsHost.Opacity = structural ? 1 : 0.5;
            AddPanelButton.IsEnabled = structural;
            if (!structural)
            {
                FormatDescription.Text = $"Wnętrze karty „{_session.Project.OuterOf(d)?.Name}” — druga strona tej samej kartki. " +
                                         "Wymiary i panele zmieniasz na karcie zewnętrznej. Projektuj tak, jak widać kartkę " +
                                         "po odwróceniu (lewa ↔ prawa).";
            }
        }
        finally { _ui = old; }
    }

    private void RebuildPanelsEditor()
    {
        PanelsHost.Children.Clear();
        var d = _session.Doc;
        int count = d.Panels.Count;
        var fmt = MakeFormatter(1);
        bool anyLabel = d.Panels.Exists(p => p.Role == PanelRole.Label);
        for (int i = 0; i < count; i++)
        {
            int index = i;
            var p = d.Panels[i];
            var row = new Grid { ColumnSpacing = 4 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(96) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            int off = anyLabel ? 1 : 0; // kolumna na okienko naklejki przed cięciem i usuwaniem
            if (anyLabel) row.ColumnDefinitions.Insert(2, new ColumnDefinition { Width = new GridLength(32) });

            var name = new TextBox { Text = p.Name, MinWidth = 0 };
            // TextChanged przychodzi asynchronicznie — reaguj tylko na faktyczną zmianę
            name.TextChanged += (_, _) =>
            {
                var panels = _session.Doc.Panels;
                if (index >= panels.Count || panels[index].Name == name.Text) return;
                EditDoc("pname" + index, doc => doc.Panels[index].Name = name.Text);
            };
            row.Children.Add(name);

            var h = new NumberBox
            {
                Value = p.Height, Minimum = 2, Maximum = 400, SmallChange = 0.5, LargeChange = 5,
                SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact, NumberFormatter = fmt,
            };
            h.ValueChanged += (_, a) =>
            {
                var panels = _session.Doc.Panels;
                if (double.IsNaN(a.NewValue) || index >= panels.Count || Same(panels[index].Height, a.NewValue)) return;
                EditDoc("pheight" + index, doc => { if (index < doc.Panels.Count) doc.Panels[index].Height = a.NewValue; });
                UpdateFormatLcd();
            };
            Grid.SetColumn(h, 1);
            row.Children.Add(h);

            var cut = new ToggleButton
            {
                Content = new FontIcon { Glyph = "", FontSize = 12 },
                IsChecked = p.CutAfter, Width = 32, Height = 32, Padding = new Thickness(0),
                IsEnabled = i < count - 1, Opacity = i < count - 1 ? 1 : 0.3,
            };
            ToolTipService.SetToolTip(cut, "Pod tym panelem: linia cięcia zamiast zgięcia");
            cut.Click += (_, _) => EditDoc(null, doc => { if (index < doc.Panels.Count) doc.Panels[index].CutAfter = cut.IsChecked == true; });
            Grid.SetColumn(cut, 2 + off);
            row.Children.Add(cut);

            var del = new Button
            {
                Content = new FontIcon { Glyph = "", FontSize = 11 },
                Width = 32, Height = 32, Padding = new Thickness(0), IsEnabled = count > 1,
            };
            ToolTipService.SetToolTip(del, "Usuń panel");
            del.Click += (_, _) =>
            {
                EditDoc(null, doc => { if (index < doc.Panels.Count && doc.Panels.Count > 1) doc.Panels.RemoveAt(index); }, ChangeFlags.All);
                CardEditor.FitToView();
            };
            Grid.SetColumn(del, 3 + off);
            row.Children.Add(del);

            if (p.Role == PanelRole.Label)
            {
                // naklejka: okienko na szpule (do wycięcia) — można wyłączyć np. dla wąskiego paska nad okienkiem
                var window = new ToggleButton
                {
                    Content = new TextBlock { Text = "◎◎", FontSize = 10, CharacterSpacing = -150 },
                    IsChecked = p.ReelWindow, Width = 32, Height = 32, Padding = new Thickness(0),
                };
                ToolTipService.SetToolTip(window, "Okienko na szpule — otwór do wycięcia (widać go w edytorze, na wydruku i w 3D)");
                window.Click += (_, _) => EditDoc(null, doc => { if (index < doc.Panels.Count) doc.Panels[index].ReelWindow = window.IsChecked == true; });
                Grid.SetColumn(window, 2);
                row.Children.Add(window);
            }

            PanelsHost.Children.Add(row);
        }
    }

    private void RefreshProperties()
    {
        var e = _session.Selected;
        DocSection.Visibility = e is null ? Visibility.Visible : Visibility.Collapsed;
        ElementSection.Visibility = e is null ? Visibility.Collapsed : Visibility.Visible;
        if (e is null)
        {
            RefreshDocPanel();
            return;
        }

        bool old = _ui;
        _ui = true;
        try
        {
            ElementHeader.Text = e.KindName;
            if (NameBox.Text != e.Name) NameBox.Text = e.Name;
            SetNum(XBox, e.X);
            SetNum(YBox, e.Y);
            SetNum(WBox, e.Width);
            SetNum(HBox, e.Height);
            SetNum(RotationBox, e.Rotation);
            SetSlider(OpacitySlider, Math.Round(e.Opacity * 100));
            LockToggle.IsChecked = e.Locked;

            TextSection.Visibility = e is TextElement ? Visibility.Visible : Visibility.Collapsed;
            ImageSection.Visibility = e is ImageElement ? Visibility.Visible : Visibility.Collapsed;
            ShapeSection.Visibility = e is ShapeElement ? Visibility.Visible : Visibility.Collapsed;
            StripesSection.Visibility = e is StripesElement ? Visibility.Visible : Visibility.Collapsed;
            HBox.IsEnabled = e is not ShapeElement { Shape: ShapeKind.Line };

            switch (e)
            {
                case TextElement t:
                    if (NormalizeNewlines(TextInput.Text) != t.Text) TextInput.Text = t.Text;
                    SelectFont(t.FontFamily);
                    SetNum(FontSizeBox, t.FontSize);
                    TextColorButton.Hex = t.Color;
                    BoldToggle.IsChecked = t.Bold;
                    ItalicToggle.IsChecked = t.Italic;
                    UpperToggle.IsChecked = t.Uppercase;
                    AlignLeftToggle.IsChecked = t.Align == TextAlign.Left;
                    AlignCenterToggle.IsChecked = t.Align == TextAlign.Center;
                    AlignRightToggle.IsChecked = t.Align == TextAlign.Right;
                    VTopToggle.IsChecked = t.VAlign == TextVAlign.Top;
                    VMiddleToggle.IsChecked = t.VAlign == TextVAlign.Middle;
                    VBottomToggle.IsChecked = t.VAlign == TextVAlign.Bottom;
                    SetSlider(LetterSpacingSlider, Math.Round(t.LetterSpacing * 100));
                    SetNum(LineSpacingBox, t.LineSpacing);
                    AutoFitCheck.IsChecked = t.AutoFit;
                    ShadowCheck.IsChecked = t.Shadow;
                    ShadowOptions.Visibility = t.Shadow ? Visibility.Visible : Visibility.Collapsed;
                    ShadowColorButton.Hex = t.ShadowColor;
                    SetNum(ShadowOffsetBox, t.ShadowOffset);
                    break;

                case ImageElement img:
                    FitCombo.SelectedIndex = (int)img.Fit;
                    FilterCombo.SelectedIndex = (int)img.Filter;
                    FlipXToggle.IsChecked = img.FlipX;
                    FlipYToggle.IsChecked = img.FlipY;
                    break;

                case ShapeElement s:
                    {
                        bool line = s.Shape == ShapeKind.Line;
                        ShapeKindCombo.SelectedIndex = (int)s.Shape;
                        FillCheck.IsChecked = s.HasFill;
                        FillColorButton.Hex = s.FillColor;
                        StrokeCheck.IsChecked = line || s.HasStroke;
                        StrokeColorButton.Hex = s.StrokeColor;
                        SetNum(StrokeWidthBox, s.StrokeWidth);
                        SetNum(CornerRadiusBox, s.CornerRadius);
                        DashedCheck.IsChecked = s.Dashed;
                        var fillVis = line ? Visibility.Collapsed : Visibility.Visible;
                        FillCheck.Visibility = FillColorButton.Visibility = fillVis;
                        StrokeCheck.IsEnabled = !line;
                        CornerRadiusBox.IsEnabled = s.Shape == ShapeKind.Rectangle;
                        break;
                    }

                case StripesElement st:
                    {
                        var key = st.Id + "|" + string.Join(',', st.Colors);
                        if (key != _stripeKey) RebuildStripeColors(st);
                        _stripeKey = key;
                        VerticalStripesCheck.IsChecked = st.Vertical;
                        SetNum(StripeGapBox, st.Gap);
                        PaletteCombo.SelectedIndex = Array.FindIndex(StripePalette.All, p => p.Colors.SequenceEqual(st.Colors));
                        break;
                    }
            }
        }
        finally { _ui = old; }
    }

    private void RebuildStripeColors(StripesElement st)
    {
        StripeColorsHost.Children.Clear();
        for (int i = 0; i < st.Colors.Count; i++)
        {
            int index = i;
            var b = new ColorButton { Compact = true, AlphaEnabled = true, Hex = st.Colors[i], Height = 32 };
            b.ColorChanged += (_, c) =>
            {
                Edit<StripesElement>("stripe" + index, s =>
                {
                    if (index < s.Colors.Count) s.Colors[index] = ColorUtil.ToHex(c);
                });
                if (_session.Selected is StripesElement cur) _stripeKey = cur.Id + "|" + string.Join(',', cur.Colors);
            };
            StripeColorsHost.Children.Add(b);
        }
    }

    // ================================================================== karta

    private void FormatCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_ui || FormatCombo.SelectedIndex < 0) return;
        var f = CardFormats.All[FormatCombo.SelectedIndex];
        EditDoc(null, d =>
        {
            d.FormatId = f.Id;
            d.Width = f.Width;
            d.Panels = [.. f.Panels.Select(p => p.Clone())];
        }, ChangeFlags.All);
        CardEditor.FitToView();
    }

    private void CardWidth_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (double.IsNaN(args.NewValue) || Same(_session.Doc.Width, args.NewValue)) return;
        EditDoc("width", d => d.Width = args.NewValue);
    }

    private void Bleed_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (double.IsNaN(args.NewValue) || Same(_session.Doc.Bleed, args.NewValue)) return;
        EditDoc("bleed", d => d.Bleed = args.NewValue);
    }

    private void AddPanel_Click(object sender, RoutedEventArgs e)
    {
        EditDoc(null, d => d.Panels.Add(new PanelDef { Name = $"Panel {d.Panels.Count + 1}", Height = 20, Role = PanelRole.Other }), ChangeFlags.All);
        CardEditor.FitToView();
    }

    // ================================================================== element — wspólne

    private void NameBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        // TextChanged przychodzi asynchronicznie — reaguj tylko na faktyczną zmianę
        if (_ui || _session.Selected is not { } el || el.Name == NameBox.Text) return;
        Edit<CardElement>("name", x => x.Name = NameBox.Text, ChangeFlags.Visual | ChangeFlags.Layers);
    }

    private void Geometry_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (double.IsNaN(args.NewValue) || _session.Selected is not { } cur) return;
        double v = args.NewValue;
        double old = sender == XBox ? cur.X : sender == YBox ? cur.Y : sender == WBox ? cur.Width : sender == HBox ? cur.Height : cur.Rotation;
        if (Same(old, v)) return;
        Edit<CardElement>("geo" + sender.Name, el =>
        {
            if (sender == XBox) el.X = v;
            else if (sender == YBox) el.Y = v;
            else if (sender == WBox) el.Width = Math.Max(0.5, v);
            else if (sender == HBox) el.Height = Math.Max(0.5, v);
            else if (sender == RotationBox) el.Rotation = v;
        });
    }

    private static double NormalizeAngle(double a)
    {
        a %= 360;
        if (a > 180) a -= 360;
        if (a <= -180) a += 360;
        return a;
    }

    private void RotateLeft_Click(object sender, RoutedEventArgs e) =>
        Edit<CardElement>(null, el => el.Rotation = NormalizeAngle(el.Rotation - 90), ChangeFlags.Visual | ChangeFlags.Properties);

    private void RotateRight_Click(object sender, RoutedEventArgs e) =>
        Edit<CardElement>(null, el => el.Rotation = NormalizeAngle(el.Rotation + 90), ChangeFlags.Visual | ChangeFlags.Properties);

    private void Opacity_ValueChanged(object sender, RangeBaseValueChangedEventArgs e) =>
        Edit<CardElement>("opacity", el => el.Opacity = Math.Clamp(e.NewValue / 100, 0, 1));

    private void LockToggle_Click(object sender, RoutedEventArgs e) =>
        Edit<CardElement>(null, el => el.Locked = LockToggle.IsChecked == true, ChangeFlags.Visual | ChangeFlags.Layers);

    private void CenterH_Click(object sender, RoutedEventArgs e) =>
        Edit<CardElement>(null, el => el.X = Math.Round((_session.Doc.Width - el.Width) / 2, 2), ChangeFlags.Visual | ChangeFlags.Properties);

    private void CenterV_Click(object sender, RoutedEventArgs e) =>
        Edit<CardElement>(null, el =>
        {
            var d = _session.Doc;
            int pi = d.PanelIndexAt(el.CenterY);
            el.Y = Math.Round(d.PanelTop(pi) + (d.Panels[pi].Height - el.Height) / 2, 2);
        }, ChangeFlags.Visual | ChangeFlags.Properties);

    private void FillPanel_Click(object sender, RoutedEventArgs e) =>
        Edit<CardElement>(null, el =>
        {
            var d = _session.Doc;
            int pi = d.PanelIndexAt(el.CenterY);
            double b = Math.Max(0, d.Bleed);
            double top = d.PanelTop(pi), h = d.Panels[pi].Height;
            double extraTop = pi == 0 ? b : 0, extraBottom = pi == d.Panels.Count - 1 ? b : 0;
            el.Rotation = 0;
            el.X = -b;
            el.Width = d.Width + 2 * b;
            el.Y = top - extraTop;
            el.Height = h + extraTop + extraBottom;
        }, ChangeFlags.Visual | ChangeFlags.Properties);

    // ================================================================== tekst

    /// <summary>TextBox w WinUI zwraca nowe linie jako '\r' — w modelu trzymamy '\n'.</summary>
    private static string NormalizeNewlines(string s) => s.Replace("\r\n", "\n").Replace('\r', '\n');

    private void TextInput_TextChanged(object sender, TextChangedEventArgs e)
    {
        var text = NormalizeNewlines(TextInput.Text);
        if (_session.Selected is not TextElement cur || cur.Text == text) return;
        Edit<TextElement>("text", t => t.Text = text);
    }

    private void FontCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (FontCombo.SelectedItem is FontItem f) Edit<TextElement>(null, t => t.FontFamily = f.Name);
    }

    private void FontSize_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (double.IsNaN(args.NewValue) || _session.Selected is TextElement { } cur && Same(cur.FontSize, args.NewValue)) return;
        Edit<TextElement>("fsize", t => t.FontSize = Math.Clamp(args.NewValue, 1, 400));
    }

    private void TextStyle_Click(object sender, RoutedEventArgs e) =>
        Edit<TextElement>(null, t =>
        {
            t.Bold = BoldToggle.IsChecked == true;
            t.Italic = ItalicToggle.IsChecked == true;
            t.Uppercase = UpperToggle.IsChecked == true;
        });

    private void Align_Click(object sender, RoutedEventArgs e)
    {
        var align = ReferenceEquals(sender, AlignCenterToggle) ? TextAlign.Center
            : ReferenceEquals(sender, AlignRightToggle) ? TextAlign.Right : TextAlign.Left;
        Edit<TextElement>(null, t => t.Align = align, ChangeFlags.Visual | ChangeFlags.Properties);
        RefreshProperties();
    }

    private void VAlign_Click(object sender, RoutedEventArgs e)
    {
        var v = ReferenceEquals(sender, VMiddleToggle) ? TextVAlign.Middle
            : ReferenceEquals(sender, VBottomToggle) ? TextVAlign.Bottom : TextVAlign.Top;
        Edit<TextElement>(null, t => t.VAlign = v, ChangeFlags.Visual | ChangeFlags.Properties);
        RefreshProperties();
    }

    private void LetterSpacing_ValueChanged(object sender, RangeBaseValueChangedEventArgs e) =>
        Edit<TextElement>("lspacing", t => t.LetterSpacing = e.NewValue / 100);

    private void LineSpacing_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (double.IsNaN(args.NewValue)) return;
        Edit<TextElement>("lheight", t => t.LineSpacing = args.NewValue);
    }

    private void AutoFit_Click(object sender, RoutedEventArgs e) =>
        Edit<TextElement>(null, t => t.AutoFit = AutoFitCheck.IsChecked == true);

    private void Shadow_Click(object sender, RoutedEventArgs e) =>
        Edit<TextElement>(null, t => t.Shadow = ShadowCheck.IsChecked == true, ChangeFlags.Visual | ChangeFlags.Properties);

    private void ShadowOffset_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (double.IsNaN(args.NewValue)) return;
        Edit<TextElement>("soffset", t => t.ShadowOffset = args.NewValue);
    }

    // ================================================================== obraz

    private void FitCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (FitCombo.SelectedIndex >= 0) Edit<ImageElement>(null, i => i.Fit = (ImageFit)FitCombo.SelectedIndex);
    }

    private void FilterCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (FilterCombo.SelectedIndex >= 0) Edit<ImageElement>(null, i => i.Filter = (ImageFilter)FilterCombo.SelectedIndex);
    }

    private async void ReplaceImage_Click(object sender, RoutedEventArgs e)
    {
        if (_session.Selected is not ImageElement) return;
        var path = await PickOpenAsync(ImageExtensions, pictures: true);
        if (path is null) return;
        var loaded = await LoadImageAsync(path);
        if (loaded is null || _session.Selected is not ImageElement) return;
        Edit<ImageElement>(null, i => i.AssetId = loaded.Value.Id);
    }

    private void Flip_Click(object sender, RoutedEventArgs e)
    {
        bool x = FlipXToggle.IsChecked == true, y = FlipYToggle.IsChecked == true;
        Edit<ImageElement>(null, i => { i.FlipX = x; i.FlipY = y; });
    }

    private void ImageAspect_Click(object sender, RoutedEventArgs e)
    {
        if (_session.Selected is not ImageElement img) return;
        var bmp = _session.Assets.TryGetBitmap(img.AssetId);
        if (bmp is null) return;
        double aspect = bmp.SizeInPixels.Width / (double)Math.Max(1, bmp.SizeInPixels.Height);
        Edit<ImageElement>(null, i =>
        {
            double cy = i.CenterY;
            i.Height = Math.Round(i.Width / aspect, 2);
            i.Y = Math.Round(cy - i.Height / 2, 2);
        }, ChangeFlags.Visual | ChangeFlags.Properties);
    }

    // ================================================================== kształt

    private void ShapeKind_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ShapeKindCombo.SelectedIndex < 0) return;
        var kind = (ShapeKind)ShapeKindCombo.SelectedIndex;
        Edit<ShapeElement>(null, s =>
        {
            s.Shape = kind;
            if (kind == ShapeKind.Line) { s.Height = 2; s.StrokeWidth = Math.Max(0.2, s.StrokeWidth); }
            else if (s.Height < 3) s.Height = Math.Min(15, s.Width);
        }, ChangeFlags.All);
    }

    private void ShapeFlags_Click(object sender, RoutedEventArgs e) =>
        Edit<ShapeElement>(null, s =>
        {
            s.HasFill = FillCheck.IsChecked == true;
            if (s.Shape != ShapeKind.Line) s.HasStroke = StrokeCheck.IsChecked == true;
            s.Dashed = DashedCheck.IsChecked == true;
        });

    private void StrokeWidth_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (double.IsNaN(args.NewValue)) return;
        Edit<ShapeElement>("swidth", s => s.StrokeWidth = Math.Max(0.05, args.NewValue));
    }

    private void CornerRadius_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (double.IsNaN(args.NewValue)) return;
        Edit<ShapeElement>("radius", s => s.CornerRadius = Math.Max(0, args.NewValue));
    }

    // ================================================================== paski

    private void PaletteCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PaletteCombo.SelectedIndex < 0) return;
        var p = StripePalette.All[PaletteCombo.SelectedIndex];
        Edit<StripesElement>(null, s => s.Colors = [.. p.Colors], ChangeFlags.Visual | ChangeFlags.Properties);
    }

    private void AddStripe_Click(object sender, RoutedEventArgs e) =>
        Edit<StripesElement>(null, s =>
        {
            var palette = ColorUtil.RetroPalette;
            s.Colors.Add(s.Colors.Count > 0 ? palette[(Array.IndexOf(palette, s.Colors[^1]) + 1 + palette.Length) % palette.Length] : palette[8]);
        }, ChangeFlags.Visual | ChangeFlags.Properties);

    private void RemoveStripe_Click(object sender, RoutedEventArgs e) =>
        Edit<StripesElement>(null, s => { if (s.Colors.Count > 1) s.Colors.RemoveAt(s.Colors.Count - 1); },
            ChangeFlags.Visual | ChangeFlags.Properties);

    private void ReverseStripes_Click(object sender, RoutedEventArgs e) =>
        Edit<StripesElement>(null, s => s.Colors.Reverse(), ChangeFlags.Visual | ChangeFlags.Properties);

    private void VerticalStripes_Click(object sender, RoutedEventArgs e) =>
        Edit<StripesElement>(null, s => s.Vertical = VerticalStripesCheck.IsChecked == true);

    private void StripeGap_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (double.IsNaN(args.NewValue)) return;
        Edit<StripesElement>("gap", s => s.Gap = Math.Max(0, args.NewValue));
    }
}
