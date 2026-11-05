namespace Kasetownik9000.Model;

/// <summary>Prostokąt w milimetrach (model nie zależy od typów WinRT).</summary>
public readonly record struct MmRect(double X, double Y, double W, double H)
{
    public double Right => X + W;
    public double Bottom => Y + H;
    public double CenterX => X + W / 2;
    public double CenterY => Y + H / 2;
}

/// <summary>
/// Okienko w naklejce na kasetę — otwór na trzpienie magnetofonu (szpule) i podgląd taśmy, który trzeba wyciąć.
/// Wymiary rzeczywiste kasety kompaktowej: osie szpul 42,6 mm od siebie, otwory szpul ok. 13 mm.
/// Ten sam kształt widać w edytorze, na wydruku (linia cięcia) i w podglądzie 3D.
/// </summary>
public static class LabelWindow
{
    /// <summary>Otwór: prostokąt z zaokrąglonymi rogami (łatwo wyciąć nożykiem przy linijce).</summary>
    public const double Width = 60, Height = 16, CornerRadius = 2.5;

    /// <summary>Odległość osi szpul od środka okienka (połowa rozstawu).</summary>
    public const double HubOffset = 21.3;

    /// <summary>Środek okienka na naklejce 89 × 42 mm: w poziomie na środku, w pionie 22 mm od góry.</summary>
    private const double CenterYRatio = 22.0 / 42.0;

    /// <summary>Czy ten panel ma okienko (naklejka z włączonym okienkiem, dość duża, żeby się zmieściło).</summary>
    public static bool Has(PanelDef p, double cardWidth) =>
        p.Role == PanelRole.Label && p.ReelWindow && p.Height >= Height + 4 && cardWidth >= Width + 4;

    /// <summary>Okienko względem lewego górnego rogu panelu o podanych wymiarach.</summary>
    public static MmRect InPanel(double panelWidth, double panelHeight)
    {
        double cy = panelHeight * CenterYRatio;
        return new MmRect((panelWidth - Width) / 2, cy - Height / 2, Width, Height);
    }

    /// <summary>Okienko panelu w układzie współrzędnych karty (null, gdy panel go nie ma).</summary>
    public static MmRect? For(CardDocument doc, int panelIndex)
    {
        var p = doc.Panels[panelIndex];
        if (!Has(p, doc.Width)) return null;
        var r = InPanel(doc.Width, p.Height);
        return r with { Y = r.Y + doc.PanelTop(panelIndex) };
    }

    /// <summary>Wszystkie okienka na karcie (zwykle dwa: naklejka A i B).</summary>
    public static IEnumerable<MmRect> All(CardDocument doc)
    {
        for (int i = 0; i < doc.Panels.Count; i++)
        {
            if (For(doc, i) is { } r) yield return r;
        }
    }
}
