namespace Kasetownik9000.Model;

/// <summary>Format karty (szablon wymiarów): szerokość i panele ułożone od góry do dołu.</summary>
public sealed class CardFormat(string id, string name, string description, double width, PanelDef[] panels)
{
    public string Id { get; } = id;
    public string Name { get; } = name;
    public string Description { get; } = description;
    public double Width { get; } = width;
    public PanelDef[] Panels { get; } = panels;

    public double Height => Panels.Sum(p => p.Height);

    public string SizeText => $"{Width:0.#} × {Height:0.#} mm";

    public CardDocument CreateDocument() => new()
    {
        FormatId = Id,
        Width = Width,
        Panels = [.. Panels.Select(p => p.Clone())],
    };
}

public static class CardFormats
{
    private static PanelDef P(string name, double h, PanelRole role, bool cut = false) =>
        new() { Name = name, Height = h, Role = role, CutAfter = cut };

    public static readonly CardFormat[] All =
    [
        new("jcard", "J-card (standard)",
            "Klasyczna okładka do pudełka: przód, grzbiet i klapka.",
            101.6, [P("Przód", 65, PanelRole.Front), P("Grzbiet", 12.7, PanelRole.Spine), P("Klapka", 15.9, PanelRole.Flap)]),

        new("jcard4", "J-card 4-panelowa",
            "Z dodatkową wkładką na listę utworów, zaginaną za przodem.",
            101.6, [P("Wkładka", 65, PanelRole.Inner), P("Przód", 65, PanelRole.Front), P("Grzbiet", 12.7, PanelRole.Spine), P("Klapka", 15.9, PanelRole.Flap)]),

        new("jcard5", "J-card 5-panelowa",
            "Rozkładana harmonijka z dwiema wkładkami — na długie opisy i teksty.",
            101.6, [P("Wkładka 2", 65, PanelRole.Inner), P("Wkładka 1", 65, PanelRole.Inner), P("Przód", 65, PanelRole.Front), P("Grzbiet", 12.7, PanelRole.Spine), P("Klapka", 15.9, PanelRole.Flap)]),

        new("jcard-long", "J-card z długą klapką",
            "Klapka tylna na całą wysokość pudełka (np. na listę utworów).",
            101.6, [P("Przód", 65, PanelRole.Front), P("Grzbiet", 12.7, PanelRole.Spine), P("Tył", 65, PanelRole.Inner)]),

        new("labels", "Naklejki na kasetę A/B",
            "Dwie naklejki na obudowę kasety (strona A i B), 89 × 42 mm.",
            89, [P("Strona A", 42, PanelRole.Label, cut: true), P("Strona B", 42, PanelRole.Label)]),

        new("custom", "Własny format",
            "Dowolne wymiary — ustawisz je w panelu właściwości.",
            100, [P("Panel 1", 60, PanelRole.Front), P("Panel 2", 15, PanelRole.Spine)]),
    ];

    public static CardFormat Get(string id) => All.FirstOrDefault(f => f.Id == id) ?? All[0];

    public static string RoleName(PanelRole r) => r switch
    {
        PanelRole.Front => "Przód",
        PanelRole.Spine => "Grzbiet",
        PanelRole.Flap => "Klapka",
        PanelRole.Inner => "Wkładka",
        PanelRole.Label => "Naklejka",
        _ => "Inny",
    };
}
