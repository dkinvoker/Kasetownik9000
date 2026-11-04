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
        string starterId = "retro";
        CardDocument Build()
        {
            var d = CardFormats.Get(formatId).CreateDocument();
            Starters.Apply(d, starterId);
            return d;
        }
        var previewDoc = Build();
        var previewAssets = new AssetStore();

        var preview = new CanvasControl { Width = 300, Height = 360, ClearColor = Color.FromArgb(255, 30, 25, 22) };
        preview.Draw += (s, a) => DrawPreview(a.DrawingSession, s.Size, previewDoc, previewAssets);

        var formats = new StackPanel { Spacing = 2 };
        foreach (var f in CardFormats.All)
        {
            var rb = new RadioButton { GroupName = "fmt", IsChecked = f.Id == formatId, Content = TwoLines(f.Name, $"{f.SizeText} · {f.Description}") };
            rb.Checked += (_, _) => { formatId = f.Id; previewDoc = Build(); preview.Invalidate(); };
            formats.Children.Add(rb);
        }
        var starters = new StackPanel { Spacing = 2 };
        foreach (var st in Starter.All)
        {
            var rb = new RadioButton { GroupName = "starter", IsChecked = st.Id == starterId, Content = TwoLines(st.Name, st.Description) };
            rb.Checked += (_, _) => { starterId = st.Id; previewDoc = Build(); preview.Invalidate(); };
            starters.Children.Add(rb);
        }

        var grid = new Grid { ColumnSpacing = 24 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(280) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(230) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.Children.Add(Column("Format", formats, 0));
        grid.Children.Add(Column("Styl startowy", starters, 1));
        var previewBorder = new Border { CornerRadius = new CornerRadius(4), Child = preview };
        grid.Children.Add(Column("Podgląd", previewBorder, 2));

        var dlg = new ContentDialog
        {
            Title = "Nowa okładka",
            Content = grid,
            PrimaryButtonText = "Utwórz",
            CloseButtonText = "Anuluj",
            DefaultButton = ContentDialogButton.Primary,
        };
        dlg.Resources["ContentDialogMaxWidth"] = 1100.0;
        var result = await ShowDialogAsync(dlg);
        preview.RemoveFromVisualTree();
        if (result != ContentDialogResult.Primary) return;

        LoadSession(new EditorSession(Build()));
        ShowToast("Nowa karta gotowa — klikaj elementy, żeby je zmieniać.", InfoBarSeverity.Informational);
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

    private static void DrawPreview(CanvasDrawingSession ds, Size size, CardDocument doc, AssetStore assets)
    {
        const float pad = 18;
        float scale = (float)Math.Min((size.Width - 2 * pad) / doc.Width, (size.Height - 2 * pad) / doc.Height);
        float ox = (float)(size.Width - doc.Width * scale) / 2, oy = (float)(size.Height - doc.Height * scale) / 2;
        ds.FillRectangle(ox + 3, oy + 4, (float)doc.Width * scale, (float)doc.Height * scale, Color.FromArgb(90, 0, 0, 0));
        ds.Transform = Matrix3x2.CreateScale(scale) * Matrix3x2.CreateTranslation(ox, oy);
        using (ds.CreateLayer(1f, new Rect(0, 0, doc.Width, doc.Height)))
            CardRenderer.DrawCard(ds, doc, assets, highQuality: false);
        ds.Transform = Matrix3x2.Identity;
        using var dash = new CanvasStrokeStyle { DashStyle = CanvasDashStyle.Dash };
        double y = 0;
        for (int i = 0; i < doc.Panels.Count - 1; i++)
        {
            y += doc.Panels[i].Height;
            float sy = oy + (float)y * scale;
            ds.DrawLine(ox, sy, ox + (float)doc.Width * scale, sy, Color.FromArgb(200, 62, 142, 135), 1, dash);
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
        int n = 0;
        foreach (var p in paths)
        {
            if (!ImageExtensions.Contains(Path.GetExtension(p).ToLowerInvariant())) continue;
            await AddImageFromFileAsync(p, world + new Vector2(4 * n, 4 * n));
            n++;
        }
        if (n == 0) ShowToast("Upuść plik obrazu (PNG, JPG…) albo projekt .kaseta.", InfoBarSeverity.Warning);
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

    private async Task<PrintOptions?> ShowOutputOptionsAsync(OutputKind kind, PrintOptions current)
    {
        var d = _session.Doc;
        var panel = new StackPanel { Spacing = 10, MinWidth = 420 };
        RadioButtons? dpi = null;
        ComboBox? paper = null;
        CheckBox? bleed = null, crop = null, fold = null, fill = null;

        if (kind == OutputKind.Png)
        {
            dpi = new RadioButtons { Header = "Rozdzielczość", SelectedIndex = current.Dpi >= 600 ? 1 : 0 };
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
            int fits = SheetLayout.Compute(210, 297, d, new PrintOptions { FillPage = true }).Cards.Count;
            crop = new CheckBox { Content = "Znaczniki cięcia w narożnikach", IsChecked = current.CropMarks };
            fold = new CheckBox { Content = "Znaczniki zgięcia przy krawędziach", IsChecked = current.FoldMarks };
            fill = new CheckBox { Content = $"Wypełnij arkusz kopiami (na A4 zmieści się: {fits})", IsChecked = current.FillPage };
            panel.Children.Add(crop);
            panel.Children.Add(fold);
            panel.Children.Add(fill);
            panel.Children.Add(new Border
            {
                Margin = new Thickness(0, 6, 0, 0),
                Padding = new Thickness(12),
                CornerRadius = new CornerRadius(4),
                Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Color.FromArgb(26, 242, 176, 53)),
                Child = new TextBlock
                {
                    TextWrapping = TextWrapping.Wrap,
                    FontSize = 12,
                    Text = kind == OutputKind.Print
                        ? "Karta zostanie wydrukowana w rzeczywistym rozmiarze (1:1). Po kliknięciu wybierzesz drukarkę i liczbę kopii. " +
                          "Najlepiej sprawdza się gruby papier 160–200 g/m²."
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
            CloseButtonText = "Anuluj",
            DefaultButton = ContentDialogButton.Primary,
        };
        if (await ShowDialogAsync(dlg) != ContentDialogResult.Primary) return null;

        return new PrintOptions
        {
            Dpi = dpi is null ? current.Dpi : dpi.SelectedIndex == 1 ? 600 : 300,
            IncludeBleed = bleed?.IsChecked == true,
            Paper = paper?.SelectedIndex == 1 ? PaperSize.Letter : PaperSize.A4,
            CropMarks = crop?.IsChecked ?? current.CropMarks,
            FoldMarks = fold?.IsChecked ?? current.FoldMarks,
            FillPage = fill?.IsChecked ?? current.FillPage,
        };
    }

    private static int Px(double mm, int dpi) => (int)Math.Round(mm / 25.4 * dpi);

    private async Task ExportPngAsync()
    {
        if (_dialogOpen) return;
        var opt = await ShowOutputOptionsAsync(OutputKind.Png, _pngOptions);
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
        var opt = await ShowOutputOptionsAsync(OutputKind.Pdf, _pdfOptions);
        if (opt is null) return;
        _pdfOptions = opt;
        var path = await PickSaveAsync(SuggestedName, "Dokument PDF", ".pdf");
        if (path is null) return;
        try
        {
            ShowToast("Przygotowuję PDF…", InfoBarSeverity.Informational);
            int count = await Exporter.ExportPdfAsync(path, _session, opt);
            ShowToast($"Zapisano PDF ({count} szt. na arkuszu): {Path.GetFileName(path)}", openPath: path);
        }
        catch (Exception ex)
        {
            ShowToast($"Eksport PDF nie powiódł się: {ex.Message}", InfoBarSeverity.Error);
        }
    }

    private async Task PrintAsync()
    {
        if (_dialogOpen) return;
        var opt = await ShowOutputOptionsAsync(OutputKind.Print, _printOptions);
        if (opt is null) return;
        _printOptions = opt;
        var r = await PrintService.PrintAsync(_hwnd, _session, opt);
        if (r.Error is not null) ShowToast(r.Error, InfoBarSeverity.Error);
        else if (r.Printed) ShowToast(r.Info ?? "Wysłano do drukarki.");
    }
}
