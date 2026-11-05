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
