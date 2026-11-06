using Kasetownik9000.Editor;
using Kasetownik9000.Model;
using Kasetownik9000.Output;
using Microsoft.UI.Xaml;

namespace Kasetownik9000;

public partial class App : Application
{
    private Window? _window;

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var cli = Environment.GetCommandLineArgs();
        if (cli.Skip(1).Any(a => a.StartsWith("--export-", StringComparison.OrdinalIgnoreCase) || a.StartsWith("--print", StringComparison.OrdinalIgnoreCase)))
        {
            _ = RunCliExportAsync(cli);
            return;
        }

        _window = new MainWindow();
        _window.Activate();
    }

    /// <summary>
    /// Eksport bez okna:
    ///   Kasetownik9000.exe [projekt.kaseta] [--format jcard] [--starter retro] [--komplet] [--card 1] [--fill] [--dpi 300]
    ///       [--tracks "C:\Muzyka\Składanka" [--tape 60]]   (utwory z plików → listy {lista A}/{lista B})
    ///       [--export-pdf out.pdf [--calibrated]] [--export-png out.png] [--export-kaseta out.kaseta]
    ///       [--print "Nazwa drukarki" [--print-file wydruk.pdf]] [--print-calibration "Nazwa drukarki" [--print-file test.pdf]]
    ///       [--export-3d mockup.png [--yaw -28] [--pitch 24] [--zoom 1] [--case on|off]] [--export-gif obrot.gif]
    ///       [--inside] (nowy projekt: dodaj wnętrze J-karty) [--no-duplex] [--duplex-short] [--print-duplex-test "Nazwa drukarki"]
    ///   PDF i druk obejmują cały komplet kart, chyba że podano --card albo --fill.
    ///   Wnętrza kart idą na odwrocie arkusza (w PDF: strony przód/tył na przemian), chyba że podano --no-duplex.
    /// </summary>
    private async Task RunCliExportAsync(string[] cli)
    {
        int code = 0;
        try
        {
            string? Arg(string name)
            {
                int i = Array.FindIndex(cli, a => string.Equals(a, name, StringComparison.OrdinalIgnoreCase));
                return i > 0 && i + 1 < cli.Length ? cli[i + 1] : null;
            }

            // pierwszy argument pozycyjny (nie wartość opcji) = projekt do wczytania
            string? project = null;
            string[] flags = ["--fill", "--komplet", "--calibrated", "--inside", "--no-duplex", "--duplex-short"];
            for (int i = 1; i < cli.Length; i++)
            {
                if (flags.Contains(cli[i])) continue;
                if (cli[i].StartsWith("--", StringComparison.Ordinal)) { i++; continue; }
                project = cli[i];
                break;
            }

            EditorSession session;
            if (project is not null)
            {
                session = await EditorSession.LoadAsync(project);
            }
            else
            {
                session = new EditorSession(Starters.CreateProject(Arg("--format") ?? "jcard", Arg("--starter") ?? "retro", cli.Contains("--komplet")));
                if (cli.Contains("--inside")) session.AddInside(session.Project.Cards[0]);
            }
            if (int.TryParse(Arg("--card"), out var cardIndex)) session.SelectCard(cardIndex - 1);
            if (Arg("--tracks") is { } trackSpec)
            {
                // utwory z plików → plan taśmy (podział A/B automatyczny)
                var tape = session.Project.Tape;
                if (int.TryParse(Arg("--tape"), out var minutes)) tape.Minutes = minutes;
                foreach (var path in TapeImport.ExpandPaths(trackSpec)) tape.Tracks.Add(await TapeImport.ReadTrackAsync(path));
                TapeMath.AutoSplit(tape);
            }

            var opt = new PrintOptions
            {
                FillPage = cli.Contains("--fill"),
                AllCards = !cli.Contains("--fill") && Arg("--card") is null,
                ApplyCalibration = cli.Contains("--calibrated"),
                Dpi = int.TryParse(Arg("--dpi"), out var dpi) ? dpi : 300,
                Duplex = !cli.Contains("--no-duplex"),
                DuplexShortEdge = cli.Contains("--duplex-short"),
            };
            if (Arg("--export-png") is { } png) await Exporter.ExportPngAsync(Path.GetFullPath(png), session, opt);
            if (Arg("--export-pdf") is { } pdf)
            {
                opt.Dpi = Math.Max(opt.Dpi, 600);
                await Exporter.ExportPdfAsync(Path.GetFullPath(pdf), session, opt);
            }
            if (Arg("--export-kaseta") is { } kas) await session.SaveAsync(Path.GetFullPath(kas));
            if (Arg("--export-3d") is { } png3d)
            {
                var preview = new Preview3DView { Session = session };
                await preview.PrepareAsync();
                float F(string name, float def) => float.TryParse(Arg(name), System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : def;
                bool? showCase = Arg("--case") switch { "on" => true, "off" => false, _ => null };
                bool? showCard = Arg("--cover") switch { "on" => true, "off" => false, _ => null };
                preview.SetView(F("--yaw", -28), F("--pitch", 24), F("--zoom", 1), showCase, showCard);
                await preview.ExportPngAsync(Path.GetFullPath(png3d), 1600, 1100);
            }
            if (Arg("--export-gif") is { } gif)
            {
                var preview = new Preview3DView { Session = session };
                await preview.PrepareAsync();
                await preview.ExportGifAsync(Path.GetFullPath(gif));
            }
            if (Arg("--print-calibration") is { } calPrinter)
            {
                var file = Arg("--print-file") is { } pf ? Path.GetFullPath(pf) : null;
                var r = await PrintService.PrintCalibrationToPrinterAsync(calPrinter, file);
                if (r.Error is not null) throw new InvalidOperationException(r.Error);
            }
            if (Arg("--print-duplex-test") is { } dupPrinter)
            {
                var file = Arg("--print-file") is { } pf ? Path.GetFullPath(pf) : null;
                var r = await PrintService.PrintDuplexTestToPrinterAsync(dupPrinter, cli.Contains("--duplex-short"), file);
                if (r.Error is not null) throw new InvalidOperationException(r.Error);
            }
            if (Arg("--print") is { } printer)
            {
                var file = Arg("--print-file") is { } pf ? Path.GetFullPath(pf) : null;
                var r = await PrintService.PrintToPrinterAsync(printer, session, opt, file);
                if (r.Error is not null) throw new InvalidOperationException(r.Error);
            }
        }
        catch (Exception ex)
        {
            code = 1;
            try { await File.WriteAllTextAsync(Path.Combine(Path.GetTempPath(), "Kasetownik9000-cli-error.txt"), ex.ToString()); } catch { }
        }
        Environment.Exit(code);
    }
}
