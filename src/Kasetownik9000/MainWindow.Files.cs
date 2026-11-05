using System.Numerics;
using System.Text.Json;
using Kasetownik9000.Controls;
using Kasetownik9000.Editor;
using Kasetownik9000.Model;
using Kasetownik9000.Output;
using Kasetownik9000.Rendering;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.Storage.Pickers;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Storage.Streams;
using Windows.UI;

namespace Kasetownik9000;

/// <summary>Pliki, dialogi, schowek, eksport i drukowanie.</summary>
public sealed partial class MainWindow
{
    private static readonly string[] ImageExtensions = [".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tif", ".tiff", ".webp", ".heic", ".jfif"];
    private const string ProjectExtension = ".kaseta";
    private const string ClipboardFormat = "Kasetownik9000.Element";

    // ================================================================== dialogi — pomocnicze

    private async Task<ContentDialogResult> ShowDialogAsync(ContentDialog dialog)
    {
        if (_dialogOpen) return ContentDialogResult.None;
        _dialogOpen = true;
        try
        {
            dialog.XamlRoot = RootHost.XamlRoot;
            dialog.RequestedTheme = ElementTheme.Dark;
            if (Application.Current.Resources.TryGetValue("DefaultContentDialogStyle", out var style) && style is Style s)
                dialog.Style = s;
            // brązowe tło zamiast systemowej szarości
            dialog.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Color.FromArgb(255, 43, 36, 31));
            dialog.BorderBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Color.FromArgb(255, 78, 65, 56));
            dialog.Resources["ContentDialogTopOverlay"] = new Microsoft.UI.Xaml.Media.SolidColorBrush(Color.FromArgb(255, 53, 44, 39));
            dialog.Resources["ContentDialogBackground"] = new Microsoft.UI.Xaml.Media.SolidColorBrush(Color.FromArgb(255, 35, 29, 25));
            return await dialog.ShowAsync();
        }
        finally
        {
            _dialogOpen = false;
        }
    }

    private async Task<bool> ConfirmDiscardAsync()
    {
        if (!_session.IsDirty) return true;
        var dlg = new ContentDialog
        {
            Title = "Niezapisane zmiany",
            Content = $"Zapisać zmiany w projekcie „{_session.DisplayName}”?",
            PrimaryButtonText = "Zapisz",
            SecondaryButtonText = "Nie zapisuj",
            CloseButtonText = "Anuluj",
            DefaultButton = ContentDialogButton.Primary,
        };
        var r = await ShowDialogAsync(dlg);
        return r switch
        {
            ContentDialogResult.Primary => await SaveAsync(false),
            ContentDialogResult.Secondary => true,
            _ => false,
        };
    }

    // ================================================================== wybór plików

    private async Task<string?> PickOpenAsync(IEnumerable<string> extensions, bool pictures = false)
    {
        try
        {
            var picker = new FileOpenPicker(AppWindow.Id)
            {
                SuggestedStartLocation = pictures ? PickerLocationId.PicturesLibrary : PickerLocationId.DocumentsLibrary,
                ViewMode = pictures ? PickerViewMode.Thumbnail : PickerViewMode.List,
            };
            foreach (var x in extensions) picker.FileTypeFilter.Add(x);
            var result = await picker.PickSingleFileAsync();
            return result?.Path;
        }
        catch (Exception ex)
        {
            ShowToast($"Nie udało się otworzyć okna wyboru pliku: {ex.Message}", InfoBarSeverity.Error);
            return null;
        }
    }

    private async Task<string?> PickSaveAsync(string suggestedName, string typeName, string extension)
    {
        try
        {
            var picker = new FileSavePicker(AppWindow.Id)
            {
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
                SuggestedFileName = suggestedName,
            };
            picker.FileTypeChoices.Add(typeName, new List<string> { extension });
            var result = await picker.PickSaveFileAsync();
            if (result is null) return null;
            var path = result.Path;
            if (!path.EndsWith(extension, StringComparison.OrdinalIgnoreCase)) path += extension;
            return path;
        }
        catch (Exception ex)
        {
            ShowToast($"Nie udało się otworzyć okna zapisu: {ex.Message}", InfoBarSeverity.Error);
            return null;
        }
    }

    private string SuggestedName => _session.FilePath is null ? "Moja kaseta" : _session.DisplayName;

    // ================================================================== nowy / otwórz / zapisz

    private async Task NewAsync()
    {
        if (_dialogOpen || !await ConfirmDiscardAsync()) return;

        string formatId = CardFormats.All.Any(f => f.Id == _session.Doc.FormatId) ? _session.Doc.FormatId : "jcard";
        string starterId = _session.Project.StyleId is "retro" or "minimal" or "mixtape" ? _session.Project.StyleId : "retro";
        bool withLabels = true;
        CardProject Build() => Starters.CreateProject(formatId, starterId, withLabels);
        var previewProject = Build();
        var previewAssets = new AssetStore();

        var preview = new CanvasControl { Width = 340, Height = 360, ClearColor = Color.FromArgb(255, 30, 25, 22) };
        preview.Draw += (s, a) => DrawPreview(a.DrawingSession, s.Size, previewProject, previewAssets);

        var labelsCheck = new CheckBox
        {
            Content = "Dołącz naklejki A/B na kasetę (komplet)",
            IsChecked = withLabels,
            Margin = new Thickness(0, 6, 0, 0),
        };
        ToolTipService.SetToolTip(labelsCheck, "Druga karta w projekcie — wspólne dane albumu, druk na jednym arkuszu, widoczne w podglądzie 3D");
        void Refresh()
        {
            labelsCheck.IsEnabled = formatId != "labels";
            previewProject = Build();
            preview.Invalidate();
        }
        labelsCheck.Click += (_, _) => { withLabels = labelsCheck.IsChecked == true; Refresh(); };

        var formats = new StackPanel { Spacing = 2 };
        foreach (var f in CardFormats.All)
        {
            var rb = new RadioButton { GroupName = "fmt", IsChecked = f.Id == formatId, Content = TwoLines(f.Name, $"{f.SizeText} · {f.Description}") };
            rb.Checked += (_, _) => { formatId = f.Id; Refresh(); };
            formats.Children.Add(rb);
        }
        formats.Children.Add(labelsCheck);

        // same nazwy stylów (jest ich sporo) — opis wybranego pokazujemy pod podglądem
        var styleDescription = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap, FontSize = 12, Opacity = 0.8, MaxWidth = 340, Margin = new Thickness(2, 4, 0, 0),
            Text = Starter.All.FirstOrDefault(s => s.Id == starterId)?.Description ?? "",
        };
        var starters = new StackPanel { Spacing = 0 };
        foreach (var st in Starter.All)
        {
            var rb = new RadioButton
            {
                GroupName = "starter", IsChecked = st.Id == starterId, MinHeight = 34,
                Content = new TextBlock { Text = st.Name, FontSize = 14, FontWeight = new Windows.UI.Text.FontWeight { Weight = 600 } },
            };
            ToolTipService.SetToolTip(rb, st.Description);
            rb.Checked += (_, _) => { starterId = st.Id; styleDescription.Text = st.Description; Refresh(); };
            starters.Children.Add(rb);
        }

        var grid = new Grid { ColumnSpacing = 24 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(280) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(230) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.Children.Add(Column("Format", formats, 0));
        grid.Children.Add(Column("Styl startowy", starters, 1));
        var previewBorder = new Border { CornerRadius = new CornerRadius(4), Child = preview };
        var previewColumn = new StackPanel { Spacing = 6 };
        previewColumn.Children.Add(previewBorder);
        previewColumn.Children.Add(styleDescription);
        grid.Children.Add(Column("Podgląd", previewColumn, 2));

        var dlg = new ContentDialog
        {
            Title = "Nowa kaseta",
            Content = grid,
            PrimaryButtonText = "Utwórz",
            CloseButtonText = "Anuluj",
            DefaultButton = ContentDialogButton.Primary,
        };
        dlg.Resources["ContentDialogMaxWidth"] = 1150.0;
        var result = await ShowDialogAsync(dlg);
        preview.RemoveFromVisualTree();
        if (result != ContentDialogResult.Primary) return;

        LoadSession(new EditorSession(Build()));
        ShowToast("Nowa kaseta gotowa — wpisz dane albumu po prawej, a teksty na kartach uzupełnią się same.", InfoBarSeverity.Informational);
    }

    private static StackPanel Column(string header, UIElement content, int column)
    {
        var sp = new StackPanel { Spacing = 10 };
        sp.Children.Add(new SectionHeader { Text = header });
        sp.Children.Add(content);
        Grid.SetColumn(sp, column);
        return sp;
    }

    private static StackPanel TwoLines(string title, string subtitle)
    {
        var sp = new StackPanel { Spacing = 1, Margin = new Thickness(0, 4, 0, 4) };
        sp.Children.Add(new TextBlock { Text = title, FontWeight = new Windows.UI.Text.FontWeight { Weight = 600 }, FontSize = 14 });
        sp.Children.Add(new TextBlock { Text = subtitle, FontSize = 11, Opacity = 0.7, TextWrapping = TextWrapping.Wrap, MaxWidth = 220 });
        return sp;
    }

    /// <summary>Miniatury wszystkich kart projektu obok siebie (ta sama skala).</summary>
    private static void DrawPreview(CanvasDrawingSession ds, Size size, CardProject project, AssetStore assets)
    {
        const float pad = 16, gapMm = 8;
        var cards = project.Cards;
        double totalW = cards.Sum(c => c.Width) + gapMm * (cards.Count - 1);
        double maxH = cards.Max(c => c.Height);
        float scale = (float)Math.Min((size.Width - 2 * pad) / totalW, (size.Height - 2 * pad) / maxH);
        float x = (float)(size.Width - totalW * scale) / 2;
        var fields = new TextResolver(project);
        using var dash = new CanvasStrokeStyle { DashStyle = CanvasDashStyle.Dash };
        foreach (var doc in cards)
        {
            float w = (float)doc.Width * scale, h = (float)doc.Height * scale;
            float y = (float)(size.Height - h) / 2;
            ds.Transform = Matrix3x2.Identity;
            ds.FillRectangle(x + 3, y + 4, w, h, Color.FromArgb(90, 0, 0, 0));
            ds.Transform = Matrix3x2.CreateScale(scale) * Matrix3x2.CreateTranslation(x, y);
            using (ds.CreateLayer(1f, new Rect(0, 0, doc.Width, doc.Height)))
            {
                CardRenderer.DrawCard(ds, doc, assets, highQuality: false, fields);
                CardRenderer.DrawLabelWindows(ds, doc, LabelWindowStyle.Preview, 1f / scale);
            }
            ds.Transform = Matrix3x2.Identity;
            double py = 0;
            for (int i = 0; i < doc.Panels.Count - 1; i++)
            {
                py += doc.Panels[i].Height;
                float sy = y + (float)py * scale;
                var color = doc.Panels[i].CutAfter ? Color.FromArgb(200, 232, 114, 44) : Color.FromArgb(200, 62, 142, 135);
                ds.DrawLine(x, sy, x + w, sy, color, 1, doc.Panels[i].CutAfter ? null : dash);
            }
            x += w + gapMm * scale;
        }
    }

    private async Task OpenAsync()
    {
        if (_dialogOpen || !await ConfirmDiscardAsync()) return;
        var path = await PickOpenAsync([ProjectExtension]);
        if (path is not null) await OpenPathAsync(path);
    }

    private async Task OpenPathAsync(string path)
    {
        try
        {
            var s = await EditorSession.LoadAsync(path);
            LoadSession(s);
            ShowToast($"Otwarto „{s.DisplayName}”.", InfoBarSeverity.Informational);
        }
        catch (Exception ex)
        {
            ShowToast($"Nie udało się otworzyć pliku: {ex.Message}", InfoBarSeverity.Error);
        }
    }

    private async Task<bool> SaveAsync(bool saveAs)
    {
        var path = _session.FilePath;
        if (saveAs || path is null)
        {
            path = await PickSaveAsync(SuggestedName, "Projekt Kasetownika", ProjectExtension);
            if (path is null) return false;
        }
        try
        {
            await _session.SaveAsync(path);
            UpdateTitle();
            ShowToast($"Zapisano „{_session.DisplayName}”.");
            return true;
        }
        catch (Exception ex)
        {
            ShowToast($"Nie udało się zapisać: {ex.Message}", InfoBarSeverity.Error);
            return false;
        }
    }

    // ================================================================== obrazy

    private static bool IsPngOrJpeg(byte[] b) =>
        b.Length > 4 && ((b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47) || (b[0] == 0xFF && b[1] == 0xD8));

    private static async Task<byte[]> ReadAllAsync(IRandomAccessStream stream)
    {
        var size = (uint)stream.Size;
        using var reader = new DataReader(stream.GetInputStreamAt(0));
        await reader.LoadAsync(size);
        var bytes = new byte[size];
        reader.ReadBytes(bytes);
        return bytes;
    }

    private static async Task<byte[]> EncodePngAsync(CanvasBitmap bmp)
    {
        using var ms = new InMemoryRandomAccessStream();
        await bmp.SaveAsync(ms, CanvasBitmapFileFormat.Png);
        return await ReadAllAsync(ms);
    }

    /// <summary>Wczytuje i rejestruje obraz w projekcie. Zwraca id i rozmiar w pikselach.</summary>
    private async Task<(string Id, double Aspect)?> LoadImageBytesAsync(byte[] bytes)
    {
        CanvasBitmap bmp;
        try
        {
            bmp = await AssetStore.DecodeAsync(bytes);
        }
        catch
        {
            ShowToast("Tego obrazu nie da się wczytać (nieobsługiwany format albo uszkodzony plik).", InfoBarSeverity.Error);
            return null;
        }
        if (!IsPngOrJpeg(bytes)) bytes = await EncodePngAsync(bmp);
        var id = _session.Assets.Add(bytes);
        _session.Assets.SetBitmap(id, bmp);
        return (id, bmp.SizeInPixels.Width / (double)Math.Max(1, bmp.SizeInPixels.Height));
    }

    private async Task<(string Id, double Aspect)?> LoadImageAsync(string path)
    {
        try
        {
            return await LoadImageBytesAsync(await File.ReadAllBytesAsync(path));
        }
        catch (Exception ex)
        {
            ShowToast($"Nie udało się odczytać pliku: {ex.Message}", InfoBarSeverity.Error);
            return null;
        }
    }

    private async Task AddImageFromFileAsync(string path, Vector2? at)
    {
        var img = await LoadImageAsync(path);
        if (img is not null) AddImageElement(img.Value.Id, img.Value.Aspect, Path.GetFileNameWithoutExtension(path), at);
    }

    private void AddImageElement(string assetId, double aspect, string name, Vector2? at)
    {
        var d = _session.Doc;
        var pos = at ?? CardEditor.ViewCenterWorld();
        int pi = d.PanelIndexAt(pos.Y);
        double top = d.PanelTop(pi), ph = d.Panels[pi].Height;
        double w = d.Width * 0.5, h = w / aspect;
        double maxH = Math.Max(ph * 0.9, 10);
        if (h > maxH) { h = maxH; w = h * aspect; }
        var el = new ImageElement
        {
            Name = _session.UniqueName(string.IsNullOrWhiteSpace(name) ? "Obraz" : name),
            AssetId = assetId,
            Width = Math.Round(w, 2),
            Height = Math.Round(h, 2),
            X = Math.Round(pos.X - w / 2, 2),
            Y = Math.Round(at is null ? top + (ph - h) / 2 : pos.Y - h / 2, 2),
        };
        _session.AddElement(el);
        CardEditor.Focus(FocusState.Programmatic);
    }

    private async Task HandleDroppedFilesAsync(IReadOnlyList<string> paths, Vector2 world)
    {
        var project = paths.FirstOrDefault(p => p.EndsWith(ProjectExtension, StringComparison.OrdinalIgnoreCase));
        if (project is not null)
        {
            if (await ConfirmDiscardAsync()) await OpenPathAsync(project);
            return;
        }
        var audio = paths.Where(IsAudio).ToList();
        if (audio.Count > 0)
        {
            await OpenTapeCalculatorAsync(audio);
            return;
        }
        int n = 0;
        foreach (var p in paths)
        {
            if (!ImageExtensions.Contains(Path.GetExtension(p).ToLowerInvariant())) continue;
            await AddImageFromFileAsync(p, world + new Vector2(4 * n, 4 * n));
            n++;
        }
        if (n == 0) ShowToast("Upuść obraz (PNG, JPG…), pliki muzyczne (do kalkulatora taśmy) albo projekt .kaseta.", InfoBarSeverity.Warning);
    }

    // ================================================================== schowek

    private bool CopySelected()
    {
        var e = _session.Selected;
        if (e is null) return false;
        var payload = new ClipboardPayload { Element = e };
        if (e is ImageElement img && _session.Assets.GetBytes(img.AssetId) is { } bytes)
        {
            payload.AssetId = img.AssetId;
            payload.AssetData = Convert.ToBase64String(bytes);
        }
        var dp = new DataPackage();
        dp.SetData(ClipboardFormat, JsonSerializer.Serialize(payload, ModelJson.Default.ClipboardPayload));
        if (e is TextElement t) dp.SetText(t.Text);
        try
        {
            Clipboard.SetContent(dp);
            Clipboard.Flush();
        }
        catch { }
        return true;
    }

    private async Task PasteAsync(Vector2? at)
    {
        try
        {
            var view = Clipboard.GetContent();
            if (view.Contains(ClipboardFormat) && await view.GetDataAsync(ClipboardFormat) is string json)
            {
                var payload = JsonSerializer.Deserialize(json, ModelJson.Default.ClipboardPayload);
                if (payload?.Element is { } el)
                {
                    if (payload.AssetId is not null && payload.AssetData is not null && _session.Assets.GetBytes(payload.AssetId) is null)
                        _session.Assets.Put(payload.AssetId, Convert.FromBase64String(payload.AssetData));
                    var copy = _session.CloneElement(el);
                    copy.Name = _session.UniqueName(el.Name.Length > 0 ? el.Name : copy.KindName);
                    copy.Locked = false;
                    if (at is { } p) { copy.X = p.X - copy.Width / 2; copy.Y = p.Y - copy.Height / 2; }
                    else { copy.X += 3; copy.Y += 3; }
                    _session.AddElement(copy);
                    return;
                }
            }
            if (view.Contains(StandardDataFormats.Bitmap))
            {
                var reference = await view.GetBitmapAsync();
                using var stream = await reference.OpenReadAsync();
                var img = await LoadImageBytesAsync(await ReadAllAsync(stream));
                if (img is not null) AddImageElement(img.Value.Id, img.Value.Aspect, "Wklejony obraz", at);
                return;
            }
            if (view.Contains(StandardDataFormats.StorageItems))
            {
                var items = await view.GetStorageItemsAsync();
                await HandleDroppedFilesAsync([.. items.Select(i => i.Path)], at ?? CardEditor.ViewCenterWorld());
                return;
            }
            if (view.Contains(StandardDataFormats.Text))
            {
                var text = await view.GetTextAsync();
                if (string.IsNullOrWhiteSpace(text)) return;
                var c = at ?? CardEditor.ViewCenterWorld();
                _session.AddElement(Starters.Text("", NormalizeNewlines(text.Trim()), c.X - 30, c.Y - 5, 60, 10, 12, autoFit: false));
            }
        }
        catch (Exception ex)
        {
            ShowToast($"Nie udało się wkleić: {ex.Message}", InfoBarSeverity.Error);
        }
    }

    // ================================================================== eksport i druk

    private enum OutputKind { Print, Pdf, Png }

    /// <summary>Okno opcji wydruku/eksportu. Calibrate = użytkownik chce najpierw skalibrować drukarkę.</summary>
    private async Task<(PrintOptions? Options, bool Calibrate)> ShowOutputOptionsAsync(OutputKind kind, PrintOptions current)
    {
        var d = _session.Doc;
        var cards = _session.Project.Cards;
        var settings = AppSettings.Current;
        var panel = new StackPanel { Spacing = 10, MinWidth = 440 };
        RadioButtons? dpi = null, scope = null;
        ComboBox? paper = null;
        CheckBox? bleed = null, crop = null, fold = null, fill = null, calibrated = null;

        if (kind == OutputKind.Png)
        {
            dpi = new RadioButtons { Header = $"Rozdzielczość — karta „{d.Name}”", SelectedIndex = current.Dpi >= 600 ? 1 : 0 };
            dpi.Items.Add($"300 DPI — {Px(d.Width, 300)} × {Px(d.Height, 300)} px");
            dpi.Items.Add($"600 DPI — {Px(d.Width, 600)} × {Px(d.Height, 600)} px");
            panel.Children.Add(dpi);
            bleed = new CheckBox { Content = $"Dołącz spad ({d.Bleed:0.#} mm z każdej strony)", IsChecked = current.IncludeBleed, IsEnabled = d.Bleed > 0 };
            panel.Children.Add(bleed);
        }
        else
        {
            if (kind == OutputKind.Pdf)
            {
                paper = new ComboBox
                {
                    Header = "Arkusz",
                    ItemsSource = new List<string> { "A4 (210 × 297 mm)", "Letter (216 × 279 mm)" },
                    SelectedIndex = (int)current.Paper,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                };
                panel.Children.Add(paper);
            }

            int fits = SheetLayout.Compute(210, 297, cards, [_session.ActiveIndex], new PrintOptions { FillPage = true }).CardCount;
            fill = new CheckBox { Content = $"Wypełnij arkusz kopiami bieżącej karty (na A4: {fits})", IsChecked = current.FillPage };

            if (cards.Count > 1)
            {
                var all = Enumerable.Range(0, cards.Count).ToList();
                int sheets = SheetLayout.Compute(210, 297, cards, all, new PrintOptions()).Pages.Count;
                scope = new RadioButtons { Header = "Co wydrukować", SelectedIndex = current.AllCards ? 0 : 1 };
                scope.Items.Add($"Cały komplet: {string.Join(" + ", cards.Select(c => c.Name))} ({sheets} {(sheets == 1 ? "arkusz" : "arkusze")} A4)");
                scope.Items.Add($"Tylko bieżącą kartę: {d.Name}");
                scope.SelectionChanged += (_, _) => fill.IsEnabled = scope.SelectedIndex == 1;
                fill.IsEnabled = scope.SelectedIndex == 1;
                panel.Children.Add(scope);
            }

            crop = new CheckBox { Content = "Znaczniki cięcia w narożnikach", IsChecked = current.CropMarks };
            fold = new CheckBox { Content = "Znaczniki zgięcia przy krawędziach", IsChecked = current.FoldMarks };
            panel.Children.Add(crop);
            panel.Children.Add(fold);
            panel.Children.Add(fill);

            if (kind == OutputKind.Pdf)
            {
                calibrated = new CheckBox
                {
                    Content = "Uwzględnij kalibrację mojej drukarki",
                    IsChecked = current.ApplyCalibration && settings.HasCalibration,
                    IsEnabled = settings.HasCalibration,
                };
                ToolTipService.SetToolTip(calibrated, "Zaznacz, jeśli PDF wydrukujesz na tej samej drukarce. Do drukarni wysyłaj bez korekty.");
                panel.Children.Add(calibrated);
            }

            panel.Children.Add(new TextBlock
            {
                Text = $"Kalibracja drukarki: {settings.CalibrationText}",
                FontSize = 12,
                Opacity = 0.75,
                TextWrapping = TextWrapping.Wrap,
            });
            panel.Children.Add(new Border
            {
                Margin = new Thickness(0, 2, 0, 0),
                Padding = new Thickness(12),
                CornerRadius = new CornerRadius(4),
                Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Color.FromArgb(26, 242, 176, 53)),
                Child = new TextBlock
                {
                    TextWrapping = TextWrapping.Wrap,
                    FontSize = 12,
                    Text = kind == OutputKind.Print
                        ? "Karty zostaną wydrukowane w rzeczywistym rozmiarze (1:1). Jeśli wydruk wychodzi minimalnie za mały lub przesunięty, " +
                          "kliknij „Kalibracja…”. Najlepiej sprawdza się gruby papier 160–200 g/m²."
                        : "Drukując PDF wybierz w przeglądarce PDF skalę „100%” / „Rzeczywisty rozmiar”, a nie „Dopasuj do strony” — " +
                          "tylko wtedy karta idealnie pasuje do pudełka.",
                },
            });
        }

        var dlg = new ContentDialog
        {
            Title = kind switch { OutputKind.Print => "Drukowanie", OutputKind.Pdf => "Eksport do PDF", _ => "Eksport do PNG" },
            Content = panel,
            PrimaryButtonText = kind == OutputKind.Print ? "Wybierz drukarkę…" : "Zapisz…",
            SecondaryButtonText = kind == OutputKind.Png ? "" : "Kalibracja…",
            CloseButtonText = "Anuluj",
            DefaultButton = ContentDialogButton.Primary,
        };
        var result = await ShowDialogAsync(dlg);
        if (result == ContentDialogResult.Secondary) return (null, true);
        if (result != ContentDialogResult.Primary) return (null, false);

        return (new PrintOptions
        {
            Dpi = dpi is null ? current.Dpi : dpi.SelectedIndex == 1 ? 600 : 300,
            IncludeBleed = bleed?.IsChecked == true,
            Paper = paper?.SelectedIndex == 1 ? PaperSize.Letter : PaperSize.A4,
            CropMarks = crop?.IsChecked ?? current.CropMarks,
            FoldMarks = fold?.IsChecked ?? current.FoldMarks,
            AllCards = scope is null || scope.SelectedIndex == 0,
            FillPage = fill is { IsEnabled: true, IsChecked: true },
            ApplyCalibration = calibrated?.IsChecked == true,
        }, false);
    }

    /// <summary>Pyta o opcje; „Kalibracja…” otwiera kalibrację i wraca do opcji.</summary>
    private async Task<PrintOptions?> AskOutputOptionsAsync(OutputKind kind, PrintOptions current)
    {
        while (true)
        {
            var (opt, calibrate) = await ShowOutputOptionsAsync(kind, current);
            if (!calibrate) return opt;
            await CalibratePrinterAsync();
        }
    }

    private static int Px(double mm, int dpi) => (int)Math.Round(mm / 25.4 * dpi);

    private async Task ExportPngAsync()
    {
        if (_dialogOpen) return;
        var opt = await AskOutputOptionsAsync(OutputKind.Png, _pngOptions);
        if (opt is null) return;
        _pngOptions = opt;
        var path = await PickSaveAsync(SuggestedName, "Obraz PNG", ".png");
        if (path is null) return;
        try
        {
            await Exporter.ExportPngAsync(path, _session, opt);
            ShowToast($"Zapisano obraz: {Path.GetFileName(path)}", openPath: path);
        }
        catch (Exception ex)
        {
            ShowToast($"Eksport PNG nie powiódł się: {ex.Message}", InfoBarSeverity.Error);
        }
    }

    private async Task ExportPdfAsync()
    {
        if (_dialogOpen) return;
        var opt = await AskOutputOptionsAsync(OutputKind.Pdf, _pdfOptions);
        if (opt is null) return;
        _pdfOptions = opt;
        var path = await PickSaveAsync(SuggestedName, "Dokument PDF", ".pdf");
        if (path is null) return;
        try
        {
            ShowToast("Przygotowuję PDF…", InfoBarSeverity.Informational);
            var (cardsCount, pages) = await Exporter.ExportPdfAsync(path, _session, opt);
            ShowToast($"Zapisano PDF ({cardsCount} kart(y) na {pages} {(pages == 1 ? "arkuszu" : "arkuszach")}): {Path.GetFileName(path)}", openPath: path);
        }
        catch (Exception ex)
        {
            ShowToast($"Eksport PDF nie powiódł się: {ex.Message}", InfoBarSeverity.Error);
        }
    }

    private async Task PrintAsync()
    {
        if (_dialogOpen) return;
        var opt = await AskOutputOptionsAsync(OutputKind.Print, _printOptions);
        if (opt is null) return;
        _printOptions = opt;
        var r = await PrintService.PrintAsync(_hwnd, _session, opt);
        if (r.Error is not null) ShowToast(r.Error, InfoBarSeverity.Error);
        else if (r.Printed) ShowToast(r.Info ?? "Wysłano do drukarki.");
    }

    // ================================================================== kalibracja drukarki

    private async Task CalibratePrinterAsync()
    {
        var settings = AppSettings.Current;
        var fmt = MakeFormatter(1);
        NumberBox Box(string header, double value, double min, double max) => new()
        {
            Header = header, Value = value, Minimum = min, Maximum = max, SmallChange = 0.5, LargeChange = 1,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact, NumberFormatter = fmt,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        var horizontal = Box("Miarka pozioma (powinno być 100 mm)", PrintService.CalibrationRuler, 80, 120);
        var vertical = Box("Miarka pionowa (powinno być 100 mm)", PrintService.CalibrationRuler, 80, 120);
        var left = Box("Krzyżyk od lewej krawędzi (30 mm)", PrintService.CalibrationOrigin, 0, 80);
        var top = Box("Krzyżyk od górnej krawędzi (30 mm)", PrintService.CalibrationOrigin, 0, 80);

        var status = new TextBlock { TextWrapping = TextWrapping.Wrap, FontSize = 12, Opacity = 0.75, Text = $"Obecna korekta: {settings.CalibrationText}" };
        var printTest = new Button { HorizontalAlignment = HorizontalAlignment.Stretch };
        printTest.Content = new StackPanel
        {
            Orientation = Orientation.Horizontal, Spacing = 8,
            Children = { new FontIcon { Glyph = "", FontSize = 13 }, new TextBlock { Text = "1. Wydrukuj stronę testową" } },
        };
        printTest.Click += async (_, _) =>
        {
            var r = await PrintService.PrintCalibrationPageAsync(_hwnd);
            status.Text = r.Error ?? r.Info ?? status.Text;
        };

        var grid = new Grid { ColumnSpacing = 10, RowSpacing = 10 };
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Grid.SetColumn(vertical, 1);
        Grid.SetRow(left, 1);
        Grid.SetRow(top, 1);
        Grid.SetColumn(top, 1);
        grid.Children.Add(horizontal);
        grid.Children.Add(vertical);
        grid.Children.Add(left);
        grid.Children.Add(top);

        var panel = new StackPanel { Spacing = 12, MaxWidth = 520 };
        panel.Children.Add(new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            FontSize = 13,
            Text = "Drukarki często drukują odrobinę za mało (np. 98–99%) albo z małym przesunięciem. " +
                   "Wydrukuj stronę testową, zmierz linijką miarki i odległość krzyżyka od krawędzi kartki, " +
                   "a Kasetownik będzie to korygował przy każdym wydruku.",
        });
        panel.Children.Add(printTest);
        panel.Children.Add(new TextBlock { Text = "2. Wpisz zmierzone wartości (mm):", FontSize = 13, Margin = new Thickness(0, 4, 0, 0) });
        panel.Children.Add(grid);
        panel.Children.Add(status);

        var dlg = new ContentDialog
        {
            Title = "Kalibracja drukarki",
            Content = panel,
            PrimaryButtonText = "Zapisz kalibrację",
            SecondaryButtonText = settings.HasCalibration ? "Usuń kalibrację" : "",
            CloseButtonText = "Anuluj",
            DefaultButton = ContentDialogButton.Primary,
        };
        var result = await ShowDialogAsync(dlg);
        if (result == ContentDialogResult.Secondary)
        {
            settings.ResetCalibration();
            settings.Save();
            ShowToast("Kalibracja drukarki usunięta — drukowanie bez korekty.", InfoBarSeverity.Informational);
            return;
        }
        if (result != ContentDialogResult.Primary) return;

        double lh = horizontal.Value, lv = vertical.Value, cx = left.Value, cy = top.Value;
        if (new[] { lh, lv, cx, cy }.Any(double.IsNaN) || lh < 80 || lv < 80)
        {
            ShowToast("Nieprawidłowe pomiary — kalibracja nie została zapisana.", InfoBarSeverity.Warning);
            return;
        }
        // drukarka: wydrukowane = k·mm + e  →  korekta: mm' = mm/k − e/k
        double kx = lh / PrintService.CalibrationRuler, ky = lv / PrintService.CalibrationRuler;
        double ex = cx - PrintService.CalibrationOrigin * kx, ey = cy - PrintService.CalibrationOrigin * ky;
        settings.PrintScaleX = 1 / kx;
        settings.PrintScaleY = 1 / ky;
        settings.PrintOffsetX = -ex / kx;
        settings.PrintOffsetY = -ey / ky;
        settings.Save();
        ShowToast($"Zapisano kalibrację: {settings.CalibrationText}.");
    }
}
