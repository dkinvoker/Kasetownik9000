using Microsoft.Win32;

namespace Kasetownik9000;

/// <summary>
/// Skojarzenie plików .kaseta z aplikacją — dwuklik w Eksploratorze otwiera projekt.
/// Wpisy idą do HKCU\Software\Classes (tylko dla bieżącego użytkownika, bez uprawnień administratora);
/// skrypt odinstalowania je usuwa. Przy każdym starcie ścieżka jest odświeżana, więc działa też po przeniesieniu exe.
/// </summary>
internal static class FileAssociation
{
    public const string Extension = ".kaseta";
    public const string ProgId = "Kasetownik9000.Projekt";

    /// <summary>Jednoplikowy starter podaje tu swoją ścieżkę — to on ma otwierać pliki, nie rozpakowana kopia.</summary>
    public const string LauncherVariable = "KASETOWNIK9000_LAUNCHER";

    /// <summary>Rejestruje skojarzenie (w tle, przy starcie okna). Wersja Debug niczego nie zmienia w systemie.</summary>
    public static void EnsureRegistered()
    {
#if !DEBUG
        try
        {
            var exe = Environment.GetEnvironmentVariable(LauncherVariable);
            if (string.IsNullOrEmpty(exe) || !File.Exists(exe)) exe = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exe)) return;

            using var classes = Registry.CurrentUser.CreateSubKey(@"Software\Classes");
            bool changed = false;
            changed |= Set(classes, ProgId, null, "Projekt kasety (Kasetownik 9000)");
            changed |= Set(classes, $@"{ProgId}\DefaultIcon", null, $"\"{exe}\",0");
            changed |= Set(classes, $@"{ProgId}\shell\open\command", null, $"\"{exe}\" \"%1\"");
            changed |= Set(classes, Extension, null, ProgId);
            changed |= Set(classes, Extension, "PerceivedType", "document");
            changed |= Set(classes, $@"{Extension}\OpenWithProgids", ProgId, "");
            // Eksplorator odświeża ikony i „Otwórz za pomocą” dopiero po tym powiadomieniu
            if (changed) Output.Native.SHChangeNotify(Output.Native.SHCNE_ASSOCCHANGED, 0, 0, 0);
        }
        catch
        {
            // brak dostępu do rejestru itp. — aplikacja działa normalnie, tylko bez dwukliku
        }
#endif
    }

    /// <summary>Zapisuje wartość, jeśli jest inna niż obecna. Zwraca true, gdy coś zmieniono.</summary>
    private static bool Set(RegistryKey root, string path, string? name, string value)
    {
        using var key = root.CreateSubKey(path);
        if (key.GetValue(name) is string current && current == value) return false;
        key.SetValue(name, value, RegistryValueKind.String);
        return true;
    }
}
