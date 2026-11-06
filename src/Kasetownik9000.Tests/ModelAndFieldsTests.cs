using System.Text.Json;

namespace Kasetownik9000.Tests;

/// <summary>Zapis i odczyt plików .kaseta (także starszych), pola {…} i kalkulator taśmy.</summary>
public class ModelJsonTests
{
    [Fact]
    public void Project_RoundTrip_KeepsGroupsInsidesAndElementTypes()
    {
        var p = Starters.CreateProject("jcard", "retro", withLabels: true);
        var outer = p.Cards[0];
        p.Cards.Add(new CardDocument { InsideOf = outer.Id, Flip = InsideFlip.TopBottom, Panels = CardProject.InsidePanels(outer, InsideFlip.TopBottom) });
        var img = new ImageElement { Name = "Okładka", AssetId = "abc", FlipX = true };
        outer.Elements.Add(img);
        var g = ElementOps.Group(outer, [outer.Elements[0], img])!;
        p.Cards[1].Panels[0].ReelWindow = false;

        var json = JsonSerializer.Serialize(new KasetaFile { Project = p }, ModelJson.Default.KasetaFile);
        var back = JsonSerializer.Deserialize(json, ModelJson.Default.KasetaFile)!.Project!;

        Assert.Equal(3, back.Cards.Count);
        Assert.Equal(outer.Id, back.Cards[2].InsideOf);
        Assert.Equal(InsideFlip.TopBottom, back.Cards[2].Flip);
        Assert.False(back.Cards[1].Panels[0].ReelWindow);
        var outer2 = back.Cards[0];
        Assert.Equal(g.Name, Assert.Single(outer2.Groups).Name);
        Assert.Equal(2, outer2.Elements.Count(e => e.GroupId == g.Id));
        var img2 = Assert.Single(outer2.Elements.OfType<ImageElement>());
        Assert.True(img2.FlipX);
        Assert.Equal(outer.Elements.Count, outer2.Elements.Count);
    }

    [Fact]
    public void OlderFile_GetsSensibleDefaults()
    {
        // fragment pliku z wersji 1.6/1.7: bez okienka, bez kierunku wnętrza, bez grup
        const string json = """
            {"version":1,"id":"in1","insideOf":"out1","name":"x","formatId":"labels","width":89,
             "panels":[{"name":"Strona A","height":42,"role":"Label","cutAfter":true}],
             "background":"#FFFFFFFF","bleed":1,
             "elements":[{"$type":"text","text":"Hej","id":"e1","name":"T","x":1,"y":2,"width":3,"height":4}]}
            """;
        var doc = JsonSerializer.Deserialize(json, ModelJson.Default.CardDocument)!;

        Assert.True(doc.Panels[0].ReelWindow);             // naklejki z 1.6 dostają okienko
        Assert.Equal(InsideFlip.LeftRight, doc.Flip);       // wnętrza z 1.7 zostają lewa ↔ prawa
        Assert.Empty(doc.Groups);
        Assert.Null(doc.Elements[0].GroupId);
        Assert.Equal("Hej", ((TextElement)doc.Elements[0]).Text);
    }

    [Fact]
    public void ClipboardPayload_RoundTrip_ManyElements()
    {
        var payload = new ClipboardPayload
        {
            Elements = [new TextElement { Text = "A" }, new ShapeElement { Shape = ShapeKind.Ellipse }],
            Assets = new() { ["id1"] = "AAAA" },
            Groups = [new ElementGroup { Name = "Grupa 7" }],
        };
        var json = JsonSerializer.Serialize(payload, ModelJson.Default.ClipboardPayload);
        var back = JsonSerializer.Deserialize(json, ModelJson.Default.ClipboardPayload)!;

        Assert.Equal(2, back.Elements!.Count);
        Assert.IsType<ShapeElement>(back.Elements[1]);
        Assert.Equal("AAAA", back.Assets!["id1"]);
        Assert.Equal("Grupa 7", back.Groups![0].Name);
    }

    [Fact]
    public void NormalizeGroups_DropsUnknownGroupIds()
    {
        var a = new ShapeElement { Name = "A", GroupId = "nie-ma" };
        var doc = new CardDocument();
        doc.Elements.Add(a);

        doc.NormalizeGroups();

        Assert.Null(a.GroupId);
    }
}

public class TextFieldsTests
{
    [Theory]
    [InlineData("{tytuł}", "Moja kaseta")]
    [InlineData("{Tytul}", "Moja kaseta")]
    [InlineData("{WYKONAWCA} — {rok}", "Ktoś — 1987")]
    [InlineData("{kaseta}", "C-90")]
    [InlineData("{nieznane}", "{nieznane}")]
    public void Resolve_Fields(string text, string expected)
    {
        var p = new CardProject { Album = new AlbumInfo { Title = "Moja kaseta", Artist = "Ktoś", Year = "1987" } };
        p.Tape.Minutes = 90;
        Assert.Equal(expected, new TextResolver(p).Resolve(text));
    }

    [Fact]
    public void TrackList_UsesTapePlan_WithTimes()
    {
        var p = new CardProject();
        p.Tape.Tracks.Add(new TapeTrack { Title = "Pierwsza", Artist = "X", Seconds = 200, Side = 0 });
        p.Tape.Tracks.Add(new TapeTrack { Title = "Druga", Seconds = 65, Side = 1 });
        var r = new TextResolver(p);

        Assert.Equal("1. Pierwsza — X  3:20", r.Resolve("{lista A}"));
        Assert.Equal("1. Druga  1:05", r.Resolve("{lista B}"));
    }

    [Fact]
    public void AutoSplit_BalancesSides_WithoutReordering()
    {
        var tape = new TapePlan { Minutes = 60, GapSeconds = 0 };
        foreach (var s in new[] { 600, 600, 600, 600, 600, 600 }) tape.Tracks.Add(new TapeTrack { Seconds = s });

        TapeMath.AutoSplit(tape);

        Assert.Equal(new[] { 0, 0, 0, 1, 1, 1 }, tape.Tracks.Select(t => t.Side));
        Assert.Equal(1800, TapeMath.SideSeconds(tape, 0));
    }

    [Theory]
    [InlineData("3:45", 225)]
    [InlineData("1:02:03", 3723)]
    [InlineData("90", 90)]
    public void TryParseTime(string text, double seconds)
    {
        Assert.True(TextResolver.TryParseTime(text, out var s));
        Assert.Equal(seconds, s);
        Assert.False(TextResolver.TryParseTime("abc", out _));
    }
}
