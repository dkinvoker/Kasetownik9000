using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;

namespace Kasetownik9000.Launcher;

/// <summary>
/// Jednoplikowy starter: rozpakowuje osadzoną aplikację (raz na wersję) i ją uruchamia,
/// przekazując argumenty (np. plik .kaseta albo opcje eksportu z linii poleceń).
/// </summary>
internal static partial class Program
{
    private const string AppFolderName = "Kasetownik9000";
    private const string AppExe = "Kasetownik9000.exe";
    private const string CompleteMarker = ".complete";

    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            var appDir = EnsureExtracted();
            var psi = new ProcessStartInfo(Path.Combine(appDir, AppExe))
            {
                UseShellExecute = false,
                WorkingDirectory = Environment.CurrentDirectory,
            };
            foreach (var a in args) psi.ArgumentList.Add(a);

            using var process = Process.Start(psi) ?? throw new InvalidOperationException("Nie udało się uruchomić procesu aplikacji.");

            // tryb linii poleceń: czekamy na wynik i zwracamy kod wyjścia; okno — od razu kończymy
            bool cli = args.Any(a => a.StartsWith("--export-", StringComparison.OrdinalIgnoreCase)
                                     || a.Equals("--print", StringComparison.OrdinalIgnoreCase));
            if (!cli) return 0;
            process.WaitForExit();
            return process.ExitCode;
        }
        catch (Exception ex)
        {
            MessageBoxW(0, $"Nie udało się uruchomić Kasetownika 9000.\n\n{ex.Message}", "Kasetownik 9000", 0x10 /* MB_ICONERROR */);
            return 1;
        }
    }

    private static Stream OpenResource(string name) =>
        typeof(Program).Assembly.GetManifestResourceStream(name)
        ?? throw new InvalidDataException("Ten plik nie zawiera aplikacji (zbuduj go przez publish.ps1).");

    /// <summary>Zwraca folder z rozpakowaną aplikacją; rozpakowuje tylko przy pierwszym starcie danej wersji.</summary>
    private static string EnsureExtracted()
    {
        string id;
        using (var reader = new StreamReader(OpenResource("payload.id")))
            id = reader.ReadToEnd().Trim().ToLowerInvariant();
        if (id.Length < 12) throw new InvalidDataException("Uszkodzony identyfikator wersji.");
        id = id[..12];

        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppFolderName, "app");
        var target = Path.Combine(root, id);
        if (IsComplete(target, id)) return target;

        Directory.CreateDirectory(root);
        var temp = $"{target}.tmp{Environment.ProcessId}";
        TryDeleteDirectory(temp);
        using (var zip = OpenResource("payload.zip"))
            ZipFile.ExtractToDirectory(zip, temp, overwriteFiles: true);
        File.WriteAllText(Path.Combine(temp, CompleteMarker), id);

        try
        {
            if (Directory.Exists(target)) Directory.Delete(target, recursive: true); // niedokończona wcześniejsza próba
            Directory.Move(temp, target);
        }
        catch (IOException) when (IsComplete(target, id))
        {
            // równolegle uruchomiona druga kopia zdążyła pierwsza — użyj jej wyniku
            TryDeleteDirectory(temp);
        }

        CleanupOldVersions(root, id);
        return target;
    }

    private static bool IsComplete(string dir, string id) =>
        File.Exists(Path.Combine(dir, AppExe)) &&
        File.Exists(Path.Combine(dir, CompleteMarker)) &&
        File.ReadAllText(Path.Combine(dir, CompleteMarker)).Trim() == id;

    /// <summary>Usuwa foldery starszych wersji (jeśli któraś jeszcze działa, zostanie usunięta następnym razem).</summary>
    private static void CleanupOldVersions(string root, string currentId)
    {
        try
        {
            foreach (var dir in Directory.EnumerateDirectories(root))
            {
                var name = Path.GetFileName(dir);
                if (name.Equals(currentId, StringComparison.OrdinalIgnoreCase)) continue;
                TryDeleteDirectory(dir);
            }
        }
        catch
        {
            // sprzątanie jest opcjonalne
        }
    }

    private static void TryDeleteDirectory(string dir)
    {
        try
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
        catch
        {
            // plik w użyciu — spróbujemy przy kolejnym starcie
        }
    }

    [LibraryImport("user32.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial int MessageBoxW(nint hWnd, string text, string caption, uint type);
}
