namespace Kasetownik9000.Tests;

/// <summary>Style startowe na wszystkich formatach — m.in. czy napisy na naklejkach omijają okienko na szpule.</summary>
public class StarterTests
{
    public static IEnumerable<object[]> Styles() => Starter.All.Select(s => new object[] { s.Id });

    public static IEnumerable<object[]> StylesAndFormats() =>
        from s in Starter.All from f in CardFormats.All select new object[] { s.Id, f.Id };

    [Theory]
    [MemberData(nameof(StylesAndFormats))]
    public void EveryStyle_WorksOnEveryFormat(string style, string format)
    {
        var doc = CardFormats.Get(format).CreateDocument();
        Starters.Apply(doc, style);

        if (style != "empty") Assert.NotEmpty(doc.Elements);
        Assert.All(doc.Elements, e =>
        {
            Assert.True(e.Width > 0 && e.Height > 0, $"{e.Name}: pusty rozmiar");
            Assert.False(double.IsNaN(e.X) || double.IsNaN(e.Y), $"{e.Name}: NaN");
        });
    }

    [Theory]
    [MemberData(nameof(Styles))]
    public void LabelTexts_StayOutOfReelWindow(string style)
    {
        var doc = CardFormats.Get("labels").CreateDocument();
        Starters.Apply(doc, style);
        var windows = LabelWindow.All(doc).ToList();
        Assert.Equal(2, windows.Count);

        foreach (var t in doc.Elements.OfType<TextElement>())
        {
            var b = ElementOps.Bounds(t);
            foreach (var w in windows)
            {
                // ramka tekstu może dotykać okienka (0,3 mm luzu), ale nie może w nie wchodzić
                bool overlaps = b.X < w.Right - 0.3 && b.Right > w.X + 0.3 && b.Y < w.Bottom - 0.3 && b.Bottom > w.Y + 0.3;
                Assert.False(overlaps, $"{style}: tekst „{t.Name}” wchodzi w okienko naklejki");
            }
        }
    }

    [Fact]
    public void CreateProject_WithLabels_AddsSecondCardInSameStyle()
    {
        var p = Starters.CreateProject("jcard4", "kraft", withLabels: true);
        Assert.Equal(2, p.Cards.Count);
        Assert.Equal("labels", p.Cards[1].FormatId);
        Assert.Equal("kraft", p.StyleId);
        Assert.Equal("Domowe archiwum", p.Album.Title);
    }

    [Fact]
    public void ApplyInside_PutsTextsOnBigPanelsOnly()
    {
        var outer = CardFormats.Get("jcard").CreateDocument();
        Starters.Apply(outer, "minimal");
        var inside = new CardDocument { Width = outer.Width, Panels = CardProject.InsidePanels(outer, InsideFlip.TopBottom) };

        Starters.ApplyInside(inside, outer);

        Assert.Equal(outer.Background, inside.Background);
        var frontTop = inside.PanelTop(inside.Panels.Count - 1);
        Assert.All(inside.Elements, e => Assert.True(e.Y >= frontTop, $"{e.Name} poza wnętrzem przodu"));
        // ciemne tło „Minimal noir” → jasny tekst
        Assert.All(inside.Elements.OfType<TextElement>(), t => Assert.Equal("#FFF2EDE4", t.Color));
    }
}
