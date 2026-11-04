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
        if (cli.Any(a => a.StartsWith("--export-", StringComparison.OrdinalIgnoreCase) || a.Equals("--print", StringComparison.OrdinalIgnoreCase)))
        {
            _ = RunCliExportAsync(cli);
            return;
        }

        _window = new MainWindow();
        _window.Activate();
    }

    /// <summary>
    /// Eksport bez okna:
    ///   Kasetownik9000.exe [projekt.kaseta] [--format jcard] [--starter retro] [--fill] [--dpi 300]
    ///       [--export-pdf out.pdf] [--export-png out.png] [--export-kaseta out.kaseta]
    ///       [--print "Nazwa drukarki" [--print-file wydruk.pdf]]
    ///       [--export-3d mockup.png [--yaw -28] [--pitch 24] [--zoom 1] [--case on|off]]
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
            for (int i = 1; i < cli.Length; i++)
            {
                if (cli[i] == "--fill") continue;
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
                var doc = CardFormats.Get(Arg("--format") ?? "jcard").CreateDocument();
                Starters.Apply(doc, Arg("--starter") ?? "retro");
                session = new EditorSession(doc);
            }

            var opt = new PrintOptions { FillPage = cli.Contains("--fill"), Dpi = int.TryParse(Arg("--dpi"), out var dpi) ? dpi : 300 };
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
                preview.SetView(F("--yaw", -28), F("--pitch", 24), F("--zoom", 1), showCase);
                await preview.ExportPngAsync(Path.GetFullPath(png3d), 1600, 1100);
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
