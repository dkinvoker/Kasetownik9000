namespace Kasetownik9000.Tests;

/// <summary>Wnętrze karty (druga strona kartki) i okienko na szpule w naklejkach.</summary>
public class InsideTests
{
    private static (CardProject P, CardDocument Outer, CardDocument Inside) WithInside(string format, InsideFlip flip)
    {
        var p = new CardProject();
        var outer = CardFormats.Get(format).CreateDocument();
        var inside = new CardDocument { InsideOf = outer.Id, Flip = flip, Panels = CardProject.InsidePanels(outer, flip) };
        p.Cards.Add(outer);
        p.Cards.Add(inside);
        p.SyncInsides();
        return (p, outer, inside);
    }

    [Fact]
    public void TopBottom_ReversesPanels_AndMovesCutLines()
    {
        var outer = CardFormats.Get("labels").CreateDocument(); // A (cięcie pod spodem), B
        var panels = CardProject.InsidePanels(outer, InsideFlip.TopBottom);

        Assert.Equal(new[] { "Strona B", "Strona A" }, panels.Select(x => x.Name));
        Assert.True(panels[0].CutAfter);  // cięcie leży teraz pod pierwszym panelem wnętrza (B)
        Assert.False(panels[1].CutAfter);
    }

    [Fact]
    public void LeftRight_KeepsPanelOrder()
    {
        var outer = CardFormats.Get("jcard4").CreateDocument();
        var panels = CardProject.InsidePanels(outer, InsideFlip.LeftRight);
        Assert.Equal(outer.Panels.Select(x => x.Name), panels.Select(x => x.Name));
    }

    [Fact]
    public void InsidePanelIndex_MatchesFlip()
    {
        var (_, _, tb) = WithInside("jcard", InsideFlip.TopBottom);
        var (_, _, lr) = WithInside("jcard", InsideFlip.LeftRight);
        Assert.Equal(2, CardProject.InsidePanelIndex(tb, 0)); // przód zewnątrz ↔ ostatni panel wnętrza
        Assert.Equal(0, CardProject.InsidePanelIndex(lr, 0));
    }

    [Fact]
    public void SyncInsides_FollowsOuterChanges()
    {
        var (p, outer, inside) = WithInside("jcard", InsideFlip.TopBottom);
        outer.Width = 110;
        outer.Bleed = 2;
        outer.Panels[0].Height = 70;

        p.SyncInsides();

        Assert.Equal(110, inside.Width);
        Assert.Equal(2, inside.Bleed);
        Assert.Equal(70, inside.Panels[^1].Height); // przód jest ostatni we wnętrzu „góra ↔ dół”
    }

    [Fact]
    public void SyncInsides_InsideWithoutOuter_BecomesNormalCard()
    {
        var (p, outer, inside) = WithInside("jcard", InsideFlip.TopBottom);
        p.Cards.Remove(outer);

        p.SyncInsides();

        Assert.False(inside.IsInside);
    }

    [Fact]
    public void SetInsideFlip_KeepsElementsOnTheirPanel()
    {
        var (p, outer, inside) = WithInside("jcard", InsideFlip.LeftRight);
        // tekst na wnętrzu przodu (lewa ↔ prawa: przód jest pierwszy, 0–65 mm), 10 mm od góry panelu
        var t = new TextElement { Name = "T", X = 5, Y = 10, Width = 50, Height = 8 };
        inside.Elements.Add(t);

        p.SetInsideFlip(inside, InsideFlip.TopBottom);

        Assert.Equal(InsideFlip.TopBottom, inside.Flip);
        Assert.Equal("Przód", inside.Panels[^1].Name);
        Assert.Equal(inside.PanelTop(inside.Panels.Count - 1) + 10, t.Y, 6); // nadal 10 mm od góry wnętrza przodu
        Assert.Equal(5, t.X);
    }

    [Fact]
    public void Labels_CannotHaveInside()
    {
        Assert.False(CardFormats.Get("labels").CreateDocument().CanHaveInside);
        Assert.True(CardFormats.Get("jcard").CreateDocument().CanHaveInside);
    }
}

public class LabelWindowTests
{
    [Fact]
    public void StandardLabel_WindowIsCentered60x16()
    {
        var r = LabelWindow.InPanel(89, 42);
        Assert.Equal(14.5, r.X, 6);
        Assert.Equal(14, r.Y, 6);
        Assert.Equal(60, r.W);
        Assert.Equal(16, r.H);
    }

    [Fact]
    public void Window_OnlyOnLabelPanels_ThatAreBigEnough_AndEnabled()
    {
        var doc = CardFormats.Get("labels").CreateDocument();
        Assert.Equal(2, LabelWindow.All(doc).Count());

        doc.Panels[1].ReelWindow = false;
        Assert.Single(LabelWindow.All(doc));

        doc.Panels[0].Height = 15; // wąski pasek nad okienkiem — otwór by się nie zmieścił
        Assert.Empty(LabelWindow.All(doc));

        Assert.Empty(LabelWindow.All(CardFormats.Get("jcard").CreateDocument()));
    }

    [Fact]
    public void SecondLabel_WindowIsInItsOwnPanel()
    {
        var doc = CardFormats.Get("labels").CreateDocument();
        var b = LabelWindow.For(doc, 1)!.Value;
        Assert.Equal(42 + 14, b.Y, 6);
    }
}
