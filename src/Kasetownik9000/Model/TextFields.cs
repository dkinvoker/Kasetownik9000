using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Kasetownik9000.Model;

/// <summary>Pole, które można wstawić do tekstu.</summary>
public sealed record TextField(string Token, string Label);

/// <summary>
/// Podstawia pola {tytuł}, {wykonawca}, {lista A}… w tekstach na podstawie danych projektu.
/// Wielkość liter, polskie znaki i spacje w nazwie pola nie mają znaczenia ({Tytul}, {LISTA a}).
/// </summary>
public sealed partial class TextResolver(CardProject project)
{
    public static readonly TextField[] Fields =
    [
        new("{tytuł}", "Tytuł albumu"),
        new("{wykonawca}", "Wykonawca"),
        new("{rok}", "Rok"),
        new("{data}", "Data nagrania"),
        new("{dopisek}", "Dopisek"),
        new("{kaseta}", "Typ kasety (np. C-60)"),
        new("{lista A}", "Lista utworów — strona A"),
        new("{lista B}", "Lista utworów — strona B"),
        new("{czas A}", "Czas strony A"),
        new("{czas B}", "Czas strony B"),
        new("{czas}", "Łączny czas"),
    ];

    private static readonly string[] SampleA = ["Pierwszy utwór", "Drugi utwór", "Trzeci utwór", "Czwarty utwór", "Piąty utwór"];
    private static readonly string[] SampleB = ["Szósty utwór", "Siódmy utwór", "Ósmy utwór", "Dziewiąty utwór", "Dziesiąty utwór"];

    [GeneratedRegex(@"\{([^{}\r\n]{1,24})\}")]
    private static partial Regex FieldRegex();

    public CardProject Project { get; } = project;

    public string Resolve(string text)
    {
        if (string.IsNullOrEmpty(text) || text.IndexOf('{') < 0) return text;
        return FieldRegex().Replace(text, m => Lookup(m.Groups[1].Value) ?? m.Value);
    }

    private string? Lookup(string rawKey)
    {
        var a = Project.Album;
        var tape = Project.Tape;
        return Normalize(rawKey) switch
        {
            "tytul" or "title" => a.Title,
            "wykonawca" or "artysta" or "artist" => a.Artist,
            "rok" or "year" => a.Year,
            "data" or "date" => a.Date,
            "dopisek" or "note" => a.Note,
            "kaseta" or "tasma" or "tape" => tape.TapeName,
            "listaa" or "stronaa" => TrackList(0),
            "listab" or "stronab" => TrackList(1),
            "lista" => TrackList(0) + "\n" + TrackList(1),
            "czasa" => FormatTime(TapeMath.SideSeconds(tape, 0)),
            "czasb" => FormatTime(TapeMath.SideSeconds(tape, 1)),
            "czas" => FormatTime(TapeMath.SideSeconds(tape, 0) + TapeMath.SideSeconds(tape, 1)),
            _ => null,
        };
    }

    private static string Normalize(string key)
    {
        var sb = new StringBuilder(key.Length);
        foreach (var ch in key.Normalize(NormalizationForm.FormD))
        {
            if (char.IsWhiteSpace(ch) || ch == '_' || ch == '-') continue;
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;
            sb.Append(ch switch { 'ł' or 'Ł' => 'l', _ => char.ToLowerInvariant(ch) });
        }
        return sb.ToString();
    }

    /// <summary>Lista utworów strony; bez utworów w planie — przykładowa lista, żeby było widać układ.</summary>
    public string TrackList(int side)
    {
        var tape = Project.Tape;
        var sideLetter = side == 0 ? "A" : "B";
        if (tape.Tracks.Count == 0)
        {
            var sample = side == 0 ? SampleA : SampleB;
            return string.Join("\n", sample.Select((t, i) => $"{Number(i, sideLetter, tape.SideNumbers)} {t}"));
        }

        var lines = new List<string>();
        int n = 0;
        foreach (var t in tape.Tracks.Where(t => t.Side == side))
        {
            var line = new StringBuilder();
            line.Append(Number(n++, sideLetter, tape.SideNumbers)).Append(' ');
            line.Append(string.IsNullOrWhiteSpace(t.Title) ? "(bez tytułu)" : t.Title.Trim());
            if (tape.ShowArtist && !string.IsNullOrWhiteSpace(t.Artist)) line.Append(" — ").Append(t.Artist.Trim());
            if (tape.ShowTimes && t.Seconds > 0) line.Append("  ").Append(FormatTime(t.Seconds));
            lines.Add(line.ToString());
        }
        return string.Join("\n", lines);
    }

    private static string Number(int i, string side, bool sideNumbers) => sideNumbers ? $"{side}{i + 1}." : $"{i + 1}.";

    public static string FormatTime(double seconds)
    {
        if (seconds <= 0) return "0:00";
        var ts = TimeSpan.FromSeconds(Math.Round(seconds));
        return ts.TotalHours >= 1 ? $"{(int)ts.TotalHours}:{ts.Minutes:00}:{ts.Seconds:00}" : $"{(int)ts.TotalMinutes}:{ts.Seconds:00}";
    }

    /// <summary>Parsuje "3:45", "1:02:03" albo "225" (sekundy).</summary>
    public static bool TryParseTime(string text, out double seconds)
    {
        seconds = 0;
        var parts = text.Trim().Split(':');
        if (parts.Length is < 1 or > 3) return false;
        double total = 0;
        foreach (var p in parts)
        {
            if (!double.TryParse(p.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) || v < 0) return false;
            total = total * 60 + v;
        }
        seconds = total;
        return true;
    }
}

/// <summary>Obliczenia na planie taśmy.</summary>
public static class TapeMath
{
    /// <summary>Czas strony: suma utworów + przerwy między nimi.</summary>
    public static double SideSeconds(TapePlan tape, int side)
    {
        double sum = 0;
        int count = 0;
        foreach (var t in tape.Tracks)
        {
            if (t.Side != side) continue;
            sum += Math.Max(0, t.Seconds);
            count++;
        }
        return count == 0 ? 0 : sum + tape.GapSeconds * (count - 1);
    }

    /// <summary>
    /// Dzieli utwory na strony A/B bez zmiany kolejności: wybiera miejsce podziału, przy którym
    /// dłuższa strona jest możliwie najkrótsza (najpierw tak, żeby obie zmieściły się na kasecie).
    /// </summary>
    public static void AutoSplit(TapePlan tape)
    {
        var tracks = tape.Tracks;
        int n = tracks.Count;
        if (n == 0) return;
        double cap = tape.SideCapacitySeconds, gap = tape.GapSeconds;

        double Span(int from, int to) // [from, to)
        {
            if (to <= from) return 0;
            double s = 0;
            for (int i = from; i < to; i++) s += Math.Max(0, tracks[i].Seconds);
            return s + gap * (to - from - 1);
        }

        int best = 0;
        double bestScore = double.MaxValue;
        for (int k = 0; k <= n; k++)
        {
            double a = Span(0, k), b = Span(k, n);
            double overflow = Math.Max(0, a - cap) + Math.Max(0, b - cap);
            // przepełnienie jest dużo gorsze niż nierówne strony; przy remisie wolimy dłuższą stronę A
            double score = overflow * 1000 + Math.Max(a, b) - (a >= b ? 0.001 : 0);
            if (score < bestScore) { bestScore = score; best = k; }
        }
        for (int i = 0; i < n; i++) tracks[i].Side = i < best ? 0 : 1;
    }
}
