using Windows.Storage;

namespace Kasetownik9000.Model;

/// <summary>Wczytywanie utworów z plików audio (tytuł, wykonawca, czas z tagów — przez system Windows).</summary>
public static class TapeImport
{
    public static readonly string[] AudioExtensions =
        [".mp3", ".flac", ".m4a", ".aac", ".wav", ".wma", ".ogg", ".opus", ".aif", ".aiff", ".mp4", ".alac"];

    public static bool IsAudio(string path) => AudioExtensions.Contains(Path.GetExtension(path).ToLowerInvariant());

    public static async Task<TapeTrack> ReadTrackAsync(string path)
    {
        var track = new TapeTrack { Title = Path.GetFileNameWithoutExtension(path), SourcePath = path };
        try
        {
            var file = await StorageFile.GetFileFromPathAsync(path);
            var props = await file.Properties.GetMusicPropertiesAsync();
            if (!string.IsNullOrWhiteSpace(props.Title)) track.Title = props.Title.Trim();
            track.Artist = (!string.IsNullOrWhiteSpace(props.Artist) ? props.Artist : props.AlbumArtist ?? "").Trim();
            track.Seconds = Math.Round(props.Duration.TotalSeconds);
        }
        catch
        {
            // brak tagów / nieobsługiwany format — zostaje nazwa pliku, czas można wpisać ręcznie
        }
        return track;
    }

    /// <summary>Pliki audio z folderu albo listy ścieżek/wzorców rozdzielonych średnikiem (np. "C:\Muzyka\*.mp3").</summary>
    public static List<string> ExpandPaths(string spec)
    {
        var result = new List<string>();
        foreach (var part in spec.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (Directory.Exists(part))
            {
                result.AddRange(Directory.EnumerateFiles(part).Where(IsAudio).Order(StringComparer.CurrentCultureIgnoreCase));
            }
            else if (part.Contains('*') || part.Contains('?'))
            {
                var dir = Path.GetDirectoryName(part);
                if (string.IsNullOrEmpty(dir)) dir = ".";
                if (Directory.Exists(dir))
                    result.AddRange(Directory.EnumerateFiles(dir, Path.GetFileName(part)).Where(IsAudio).Order(StringComparer.CurrentCultureIgnoreCase));
            }
            else if (File.Exists(part))
            {
                result.Add(part);
            }
        }
        return result;
    }
}
