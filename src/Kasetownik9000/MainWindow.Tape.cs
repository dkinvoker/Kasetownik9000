using System.Text;
using System.Text.Json;
using Kasetownik9000.Controls;
using Kasetownik9000.Editor;
using Kasetownik9000.Model;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.Windows.Storage.Pickers;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.UI;

namespace Kasetownik9000;

/// <summary>Kalkulator taśmy: utwory, podział na strony A/B, czasy, playlisty M3U.</summary>
public sealed partial class MainWindow
{
    private static readonly string[] AudioExtensions = TapeImport.AudioExtensions;
    private static readonly int[] TapeLengths = [46, 60, 90, 120];

    private static bool IsAudio(string path) => TapeImport.IsAudio(path);

    private void TapeCalculator_Click(object sender, RoutedEventArgs e) => _ = OpenTapeCalculatorAsync(null);

    private async Task OpenTapeCalculatorAsync(IReadOnlyList<string>? initialFiles)
    {
        if (_dialogOpen) return;
        var res = Application.Current.Resources;

        // pracujemy na kopii — zmiany trafiają do projektu dopiero po "Zapisz na okładce"
        var plan = JsonSerializer.Deserialize(JsonSerializer.Serialize(_session.Project.Tape, ModelJson.Default.TapePlan), ModelJson.Default.TapePlan)!;

        // ---------------------------------------------------------------- górny pasek: kaseta i przerwa
        var tapeCombo = new ComboBox { Header = "Kaseta", MinWidth = 210 };
        foreach (var m in TapeLengths) tapeCombo.Items.Add($"C-{m}  (2 × {m / 2} min)");
        tapeCombo.Items.Add("Własna długość");
        int tapeIdx = Array.IndexOf(TapeLengths, plan.Minutes);
        tapeCombo.SelectedIndex = tapeIdx >= 0 ? tapeIdx : TapeLengths.Length;

        var minutesBox = new NumberBox
        {
            Header = "Minut łącznie", Value = plan.Minutes, Minimum = 2, Maximum = 240, SmallChange = 2,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact, Width = 140,
            Visibility = tapeIdx >= 0 ? Visibility.Collapsed : Visibility.Visible,
        };
        var gapBox = new NumberBox
        {
            Header = "Przerwa między utworami (s)", Value = plan.GapSeconds, Minimum = 0, Maximum = 30, SmallChange = 1,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact, Width = 210,
        };
        var topRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        topRow.Children.Add(tapeCombo);
        topRow.Children.Add(minutesBox);
        topRow.Children.Add(gapBox);

        // ---------------------------------------------------------------- przyciski
        var status = new TextBlock { FontSize = 12, Opacity = 0.75, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap };
        Button Btn(string glyph, string text, string tip)
        {
            var b = new Button
            {
                Content = new StackPanel
                {
                    Orientation = Orientation.Horizontal, Spacing = 8,
                    Children = { new FontIcon { Glyph = glyph, FontSize = 13 }, new TextBlock { Text = text } },
                },
            };
            ToolTipService.SetToolTip(b, tip);
            return b;
        }
        var addFiles = Btn("", "Dodaj pliki…", "Wybierz utwory (MP3, FLAC, M4A, WAV…) — tytuł, wykonawca i czas wczytają się same");
        var addManual = Btn("", "Dodaj ręcznie", "Dodaj utwór bez pliku i wpisz tytuł oraz czas");
        var split = Btn("", "Podziel automatycznie", "Rozdziel utwory na strony A i B tak, żeby były równe (kolejność zostaje)");
        var m3u = Btn("", "Playlisty M3U…", "Zapisz playlisty strony A i B — puść je w odtwarzaczu i nagrywaj stronę za stroną");
        var clear = Btn("", "Wyczyść", "Usuń wszystkie utwory z listy");
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        foreach (var b in new[] { addFiles, addManual, split, m3u, clear }) buttons.Children.Add(b);

        // ---------------------------------------------------------------- dwie strony
        var sideLcd = new TextBlock[2];
        var sideBar = new ProgressBar[2];
        var sideRows = new StackPanel[2];
        var sidesGrid = new Grid { ColumnSpacing = 16 };
        sidesGrid.ColumnDefinitions.Add(new ColumnDefinition());
        sidesGrid.ColumnDefinitions.Add(new ColumnDefinition());
        for (int s = 0; s < 2; s++)
        {
            var col = new StackPanel { Spacing = 6 };
            col.Children.Add(new SectionHeader { Text = s == 0 ? "Strona A" : "Strona B" });
            sideLcd[s] = new TextBlock { Style = (Style)res["LcdTextStyle"], FontSize = 14 };
            col.Children.Add(new Border { Style = (Style)res["LcdBorderStyle"], Child = sideLcd[s] });
            sideBar[s] = new ProgressBar { Height = 6, Minimum = 0 };
            col.Children.Add(sideBar[s]);
            sideRows[s] = new StackPanel { Spacing = 4 };
            col.Children.Add(new ScrollViewer
            {
                Content = sideRows[s], Height = 300, // stała wysokość — okno nie zmienia rozmiaru przy przenoszeniu utworów
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new Thickness(0, 0, 10, 0),
            });
            Grid.SetColumn(col, s);
            sidesGrid.Children.Add(col);
        }

        // ---------------------------------------------------------------- opcje listy na okładce
        var showTimes = new CheckBox { Content = "Pokaż czasy", IsChecked = plan.ShowTimes };
        var showArtist = new CheckBox { Content = "Pokaż wykonawców", IsChecked = plan.ShowArtist };
        var sideNumbers = new CheckBox { Content = "Numeracja A1, A2… B1…", IsChecked = plan.SideNumbers };
        var options = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 18 };
        options.Children.Add(new TextBlock { Text = "Lista na okładce:", VerticalAlignment = VerticalAlignment.Center, Opacity = 0.8 });
        options.Children.Add(showTimes);
        options.Children.Add(showArtist);
        options.Children.Add(sideNumbers);

        var summary = new TextBlock { FontSize = 12, TextWrapping = TextWrapping.Wrap };

        var root = new StackPanel { Spacing = 12, Width = 980, AllowDrop = true, Background = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0)) };
        root.Children.Add(topRow);
        root.Children.Add(buttons);
        root.Children.Add(sidesGrid);
        root.Children.Add(summary);
        root.Children.Add(options);
        root.Children.Add(status);

        // ---------------------------------------------------------------- logika
        void UpdateTotals()
        {
            double cap = plan.SideCapacitySeconds;
            for (int s = 0; s < 2; s++)
            {
                double t = TapeMath.SideSeconds(plan, s);
                bool over = t > cap + 0.5;
                sideLcd[s].Text = $"{TextResolver.FormatTime(t)} / {TextResolver.FormatTime(cap)}" + (over ? "  ZA DŁUGO!" : "");
                sideLcd[s].Foreground = over ? new SolidColorBrush(Color.FromArgb(255, 255, 92, 52)) : (Brush)res["RetroLcdFgBrush"];
                sideBar[s].Maximum = cap;
                sideBar[s].Value = Math.Min(t, cap);
                sideBar[s].ShowError = over;
            }
            double a = TapeMath.SideSeconds(plan, 0), b = TapeMath.SideSeconds(plan, 1);
            string Left(double t) => t <= cap ? $"zostało {TextResolver.FormatTime(cap - t)}" : $"brakuje {TextResolver.FormatTime(t - cap)}";
            summary.Text = plan.Tracks.Count == 0
                ? "Dodaj utwory przyciskiem „Dodaj pliki…” albo przeciągnij pliki muzyczne prosto do tego okna."
                : $"{plan.Tracks.Count} utworów · razem {TextResolver.FormatTime(a + b)} na {plan.TapeName} · " +
                  $"strona A: {Left(a)} · strona B: {Left(b)}";
        }

        void Rebuild()
        {
            for (int s = 0; s < 2; s++) sideRows[s].Children.Clear();
            var counters = new int[2];
            foreach (var track in plan.Tracks)
            {
                int s = Math.Clamp(track.Side, 0, 1);
                sideRows[s].Children.Add(TrackRow(track, ++counters[s]));
            }
            for (int s = 0; s < 2; s++)
            {
                if (sideRows[s].Children.Count == 0)
                    sideRows[s].Children.Add(new TextBlock { Text = "— pusto —", Opacity = 0.5, Margin = new Thickness(4, 8, 0, 0) });
            }
            UpdateTotals();
        }

        Button Mini(string glyph, string tip, Action click)
        {
            var b = new Button { Content = new FontIcon { Glyph = glyph, FontSize = 11 }, Width = 28, Height = 30, Padding = new Thickness(0), MinWidth = 0 };
            ToolTipService.SetToolTip(b, tip);
            b.Click += (_, _) => click();
            return b;
        }

        void Move(TapeTrack t, int dir)
        {
            var list = plan.Tracks;
            int i = list.IndexOf(t), j = i + dir;
            while (j >= 0 && j < list.Count && list[j].Side != t.Side) j += dir;
            if (j < 0 || j >= list.Count) return;
            (list[i], list[j]) = (list[j], list[i]);
            Rebuild();
        }

        Grid TrackRow(TapeTrack t, int number)
        {
            var g = new Grid { ColumnSpacing = 4 };
            foreach (var w in new[] { 22.0, -1.6, -1, 58, 28, 28, 28, 28 })
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = w > 0 ? new GridLength(w) : new GridLength(-w, GridUnitType.Star) });

            var num = new TextBlock { Text = $"{number}.", VerticalAlignment = VerticalAlignment.Center, Opacity = 0.7 };
            var title = new TextBox { Text = t.Title, PlaceholderText = "Tytuł", MinWidth = 0 };
            title.TextChanged += (_, _) => t.Title = title.Text;
            var artist = new TextBox { Text = t.Artist, PlaceholderText = "Wykonawca", MinWidth = 0 };
            artist.TextChanged += (_, _) => t.Artist = artist.Text;
            var time = new TextBox { Text = t.Seconds > 0 ? TextResolver.FormatTime(t.Seconds) : "", PlaceholderText = "m:ss", MinWidth = 0 };
            time.TextChanged += (_, _) =>
            {
                if (TextResolver.TryParseTime(time.Text, out var sec)) { t.Seconds = sec; UpdateTotals(); }
            };
            time.LostFocus += (_, _) => time.Text = t.Seconds > 0 ? TextResolver.FormatTime(t.Seconds) : "";
            if (!string.IsNullOrEmpty(t.SourcePath)) ToolTipService.SetToolTip(title, t.SourcePath);

            var cells = new UIElement[]
            {
                num, title, artist, time,
                Mini(t.Side == 0 ? "" : "", t.Side == 0 ? "Przenieś na stronę B" : "Przenieś na stronę A", () => { t.Side = 1 - t.Side; Rebuild(); }),
                Mini("", "Wyżej", () => Move(t, -1)),
                Mini("", "Niżej", () => Move(t, 1)),
                Mini("", "Usuń", () => { plan.Tracks.Remove(t); Rebuild(); }),
            };
            for (int i = 0; i < cells.Length; i++)
            {
                Grid.SetColumn((FrameworkElement)cells[i], i);
                g.Children.Add(cells[i]);
            }
            return g;
        }

        async Task AddPathsAsync(IEnumerable<string> paths)
        {
            var audio = paths.Where(IsAudio).ToList();
            if (audio.Count == 0)
            {
                status.Text = "To nie są pliki muzyczne (obsługiwane: MP3, FLAC, M4A, AAC, WAV, WMA, OGG…).";
                return;
            }
            status.Text = $"Wczytuję {audio.Count} plik(ów)…";
            foreach (var p in audio) plan.Tracks.Add(await TapeImport.ReadTrackAsync(p));
            TapeMath.AutoSplit(plan);
            int noTime = plan.Tracks.Count(t => t.Seconds <= 0);
            status.Text = $"Dodano {audio.Count} utwór(ów) i podzielono na strony." +
                          (noTime > 0 ? $" {noTime} bez czasu — wpisz go ręcznie (m:ss)." : " Przenoś utwory przyciskiem ⇄.");
            Rebuild();
        }

        tapeCombo.SelectionChanged += (_, _) =>
        {
            int i = tapeCombo.SelectedIndex;
            bool custom = i >= TapeLengths.Length;
            minutesBox.Visibility = custom ? Visibility.Visible : Visibility.Collapsed;
            if (!custom && i >= 0) { plan.Minutes = TapeLengths[i]; minutesBox.Value = plan.Minutes; }
            UpdateTotals();
        };
        minutesBox.ValueChanged += (_, a) => { if (!double.IsNaN(a.NewValue)) { plan.Minutes = (int)Math.Round(a.NewValue); UpdateTotals(); } };
        gapBox.ValueChanged += (_, a) => { if (!double.IsNaN(a.NewValue)) { plan.GapSeconds = a.NewValue; UpdateTotals(); } };
        showTimes.Click += (_, _) => plan.ShowTimes = showTimes.IsChecked == true;
        showArtist.Click += (_, _) => plan.ShowArtist = showArtist.IsChecked == true;
        sideNumbers.Click += (_, _) => plan.SideNumbers = sideNumbers.IsChecked == true;

        addFiles.Click += async (_, _) =>
        {
            try
            {
                var picker = new FileOpenPicker(AppWindow.Id) { SuggestedStartLocation = PickerLocationId.MusicLibrary, ViewMode = PickerViewMode.List };
                foreach (var x in AudioExtensions) picker.FileTypeFilter.Add(x);
                var files = await picker.PickMultipleFilesAsync();
                if (files is { Count: > 0 }) await AddPathsAsync(files.Select(f => f.Path));
            }
            catch (Exception ex)
            {
                status.Text = $"Nie udało się otworzyć wyboru plików: {ex.Message}";
            }
        };
        addManual.Click += (_, _) =>
        {
            plan.Tracks.Add(new TapeTrack { Title = "", Side = plan.Tracks.Count > 0 ? plan.Tracks[^1].Side : 0 });
            Rebuild();
            if (sideRows[plan.Tracks[^1].Side].Children.LastOrDefault() is Grid row && row.Children[1] is TextBox tb) tb.Focus(FocusState.Programmatic);
        };
        split.Click += (_, _) => { TapeMath.AutoSplit(plan); status.Text = "Podzielono — strony są możliwie równe, kolejność utworów bez zmian."; Rebuild(); };
        clear.Click += (_, _) => { plan.Tracks.Clear(); status.Text = ""; Rebuild(); };
        m3u.Click += async (_, _) => status.Text = await SavePlaylistsAsync(plan);

        root.DragOver += (_, e) =>
        {
            if (e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                e.AcceptedOperation = DataPackageOperation.Copy;
                e.DragUIOverride.Caption = "Dodaj do kasety";
            }
        };
        root.Drop += async (_, e) =>
        {
            if (!e.DataView.Contains(StandardDataFormats.StorageItems)) return;
            var def = e.GetDeferral();
            List<string> paths;
            try { paths = [.. (await e.DataView.GetStorageItemsAsync()).Select(i => i.Path).Where(p => !string.IsNullOrEmpty(p))]; }
            finally { def.Complete(); }
            await AddPathsAsync(paths);
        };

        Rebuild();
        if (initialFiles is { Count: > 0 })
        {
            // pliki przeciągnięte na płótno — dodaj, gdy okno już się otworzy
            DispatcherQueue.TryEnqueue(async () => await AddPathsAsync(initialFiles));
        }

        var dlg = new ContentDialog
        {
            Title = "Kalkulator taśmy",
            Content = root,
            PrimaryButtonText = "Zapisz na okładce",
            CloseButtonText = "Anuluj",
            DefaultButton = ContentDialogButton.Primary,
        };
        dlg.Resources["ContentDialogMaxWidth"] = 1100.0;
        if (await ShowDialogAsync(dlg) != ContentDialogResult.Primary) return;

        plan.Tracks.RemoveAll(t => string.IsNullOrWhiteSpace(t.Title) && t.Seconds <= 0);
        _session.EditProject(null, p => p.Tape = plan);

        bool listOnCover = _session.Project.Cards.SelectMany(c => c.Elements).OfType<TextElement>()
            .Any(t => t.Text.Contains("{lista", StringComparison.OrdinalIgnoreCase));
        if (!listOnCover && plan.Tracks.Count > 0)
        {
            AddTracklist_Click(this, new RoutedEventArgs());
            ShowToast("Dodano listę utworów na kartę — przesuń ją tam, gdzie ma być. Aktualizuje się sama z kalkulatora.", InfoBarSeverity.Informational);
        }
        else
        {
            ShowToast($"Zapisano plan taśmy: {plan.Tracks.Count} utworów na {plan.TapeName}. Listy na okładce zaktualizowane.");
        }
    }

    /// <summary>Zapisuje playlisty M3U dla stron A i B w wybranym folderze. Zwraca komunikat.</summary>
    private async Task<string> SavePlaylistsAsync(TapePlan plan)
    {
        if (plan.Tracks.Count == 0) return "Najpierw dodaj utwory.";
        try
        {
            var picker = new FolderPicker(AppWindow.Id) { SuggestedStartLocation = PickerLocationId.MusicLibrary };
            var folder = await picker.PickSingleFolderAsync();
            if (folder is null) return "";
            var name = string.Join("_", (_session.Project.Album.Title is { Length: > 0 } t ? t : "Kaseta").Split(Path.GetInvalidFileNameChars()));
            var written = new List<string>();
            for (int side = 0; side < 2; side++)
            {
                var tracks = plan.Tracks.Where(x => x.Side == side).ToList();
                if (tracks.Count == 0) continue;
                var sb = new StringBuilder("#EXTM3U\n");
                foreach (var tr in tracks)
                {
                    var label = string.IsNullOrWhiteSpace(tr.Artist) ? tr.Title : $"{tr.Artist} - {tr.Title}";
                    if (string.IsNullOrEmpty(tr.SourcePath))
                    {
                        sb.Append($"# {label} (brak pliku)\n");
                        continue;
                    }
                    sb.Append($"#EXTINF:{(int)Math.Round(tr.Seconds)},{label}\n{tr.SourcePath}\n");
                }
                var file = Path.Combine(folder.Path, $"{name} - strona {(side == 0 ? "A" : "B")}.m3u8");
                await File.WriteAllTextAsync(file, sb.ToString(), new UTF8Encoding(false));
                written.Add(Path.GetFileName(file));
            }
            return $"Zapisano: {string.Join(", ", written)}";
        }
        catch (Exception ex)
        {
            return $"Nie udało się zapisać playlist: {ex.Message}";
        }
    }
}
