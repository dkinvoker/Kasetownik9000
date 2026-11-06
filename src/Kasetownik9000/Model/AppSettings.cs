using System.Text.Json;

namespace Kasetownik9000.Model;

/// <summary>
/// Ustawienia użytkownika zapisywane w %LOCALAPPDATA%\Kasetownik9000\settings.json
/// (skrypt odinstalowania usuwa je razem z aplikacją).
/// </summary>
public sealed class AppSettings
{
    /// <summary>Korekta skali drukarki (1 = brak). Wydruk w mm = mm × Scale + Offset.</summary>
    public double PrintScaleX { get; set; } = 1;
    public double PrintScaleY { get; set; } = 1;
    public double PrintOffsetX { get; set; }
    public double PrintOffsetY { get; set; }

    public bool HasCalibration =>
        Math.Abs(PrintScaleX - 1) > 1e-6 || Math.Abs(PrintScaleY - 1) > 1e-6 ||
        Math.Abs(PrintOffsetX) > 1e-6 || Math.Abs(PrintOffsetY) > 1e-6;

    public string CalibrationText => HasCalibration
        ? $"skala {PrintScaleX * 100:0.0}% × {PrintScaleY * 100:0.0}%, przesunięcie {PrintOffsetX:+0.0;-0.0;0} / {PrintOffsetY:+0.0;-0.0;0} mm"
        : "brak (drukarka drukuje dokładnie)";

    public void ResetCalibration()
    {
        PrintScaleX = PrintScaleY = 1;
        PrintOffsetX = PrintOffsetY = 0;
    }

    /// <summary>
    /// Druk dwustronny: o ile tył kartki wychodzi przesunięty względem przodu (patrząc od przodu, pod światło):
    /// X dodatnie = w prawo, Y dodatnie = w dół. Korygujemy to przy drukowaniu odwrocia.
    /// </summary>
    public double DuplexOffsetX { get; set; }
    public double DuplexOffsetY { get; set; }

    [System.Text.Json.Serialization.JsonIgnore]
    public bool HasDuplexAlignment => Math.Abs(DuplexOffsetX) > 1e-6 || Math.Abs(DuplexOffsetY) > 1e-6;

    [System.Text.Json.Serialization.JsonIgnore]
    public string DuplexAlignmentText => HasDuplexAlignment
        ? $"tył przesunięty o {DuplexOffsetX:+0.0;-0.0;0} mm w poziomie i {DuplexOffsetY:+0.0;-0.0;0} mm w pionie — korygowane"
        : "brak korekty";

    [System.Text.Json.Serialization.JsonIgnore]
    public string DuplexAlignmentShort => HasDuplexAlignment
        ? $"korekta {DuplexOffsetX:+0.0;-0.0;0} / {DuplexOffsetY:+0.0;-0.0;0} mm"
        : "brak korekty";

    /// <summary>Ostatnio wybrany sposób druku dwustronnego (zapamiętany między uruchomieniami).</summary>
    public bool DuplexManual { get; set; }
    public bool DuplexShortEdge { get; set; }

    // ------------------------------------------------------------------ zapis

    private static string FilePath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Kasetownik9000", "settings.json");

    private static AppSettings? _current;

    public static AppSettings Current => _current ??= Load();

    private static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize(File.ReadAllText(FilePath), ModelJson.Default.AppSettings) ?? new AppSettings();
        }
        catch
        {
            // uszkodzony plik — zaczynamy od domyślnych
        }
        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, ModelJson.Default.AppSettings));
        }
        catch
        {
            // brak zapisu ustawień nie może zablokować pracy
        }
    }
}
