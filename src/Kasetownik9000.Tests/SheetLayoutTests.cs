using Kasetownik9000.Output;

namespace Kasetownik9000.Tests;

/// <summary>Rozkład kart na arkuszu, znaczniki i druk dwustronny (odwrocie arkusza).</summary>
public class SheetLayoutTests
{
    private const double A4W = 210, A4H = 297;

    private static CardProject Komplet(bool inside, InsideFlip flip = InsideFlip.TopBottom)
    {
        var project = Starters.CreateProject("jcard", "retro", withLabels: true);
        if (inside)
        {
            var outer = project.Cards[0];
            var ins = new CardDocument { InsideOf = outer.Id, Flip = flip, Width = outer.Width, Panels = CardProject.InsidePanels(outer, flip) };
            project.Cards.Insert(1, ins);
            project.SyncInsides();
        }
        return project;
    }

    private static List<int> Outer(CardProject p) => [.. Enumerable.Range(0, p.Cards.Count).Where(i => !p.Cards[i].IsInside)];

    [Fact]
    public void Komplet_JCardAndLabels_FitOnOneA4_InsideMargins()
    {
        var p = Komplet(inside: false);
        var layout = SheetLayout.Compute(A4W, A4H, p.Cards, Outer(p), new PrintOptions());

        Assert.Single(layout.Pages);
        Assert.Equal(2, layout.CardCount);
        foreach (var pc in layout.Pages[0].Cards)
        {
            var card = p.Cards[pc.CardIndex];
            Assert.InRange(pc.Position.X, 8, A4W - 8 - card.Width);
            Assert.InRange(pc.Position.Y, 8, A4H - 8 - card.Height);
        }
    }

    [Fact]
    public void CropMarks_FourCornersPerCard_PlusCutBetweenLabels()
    {
        var p = Komplet(inside: false);
        var opt = new PrintOptions { FoldMarks = false };
        var layout = SheetLayout.Compute(A4W, A4H, p.Cards, Outer(p), opt);

        // 8 znaczników narożnych na kartę + naklejki: linia cięcia między A i B (2 kreski)
        Assert.Equal(8 + 8 + 2, layout.Pages[0].Marks.Count);
        Assert.All(layout.Pages[0].Marks, m => Assert.False(m.Fold));
    }

    [Fact]
    public void FillPage_RepeatsOneCard()
    {
        var p = Komplet(inside: false);
        var layout = SheetLayout.Compute(A4W, A4H, p.Cards, [0], new PrintOptions { FillPage = true });
        Assert.Equal(2, layout.CardCount); // J-card 101,6 × 93,6 mieści się na A4 dwa razy
    }

    [Fact]
    public void NoInside_NoBackPage()
    {
        var p = Komplet(inside: false);
        var layout = SheetLayout.Compute(A4W, A4H, p.Cards, Outer(p), new PrintOptions());
        Assert.False(layout.HasBack);
    }

    [Theory]
    [InlineData(InsideFlip.LeftRight, false, false)]
    [InlineData(InsideFlip.LeftRight, true, true)]
    [InlineData(InsideFlip.TopBottom, false, true)]
    [InlineData(InsideFlip.TopBottom, true, false)]
    public void Inside_GoesOnBack_MirroredBehindItsCard(InsideFlip flip, bool shortEdge, bool expectRotated)
    {
        var p = Komplet(inside: true, flip);
        var layout = SheetLayout.Compute(A4W, A4H, p.Cards, Outer(p), new PrintOptions { DuplexShortEdge = shortEdge });

        Assert.True(layout.HasBack);
        var page = layout.Pages[0];
        var front = page.Cards.Single(c => c.CardIndex == 0);
        var back = Assert.Single(page.Back);
        Assert.Equal(1, back.CardIndex);
        Assert.Equal(expectRotated, back.Rotated);

        var card = p.Cards[0];
        if (shortEdge)
        {
            Assert.Equal(front.Position.X, back.Position.X, 3); // Point trzyma float — 0,001 mm wystarczy
            Assert.Equal(A4H - front.Position.Y - card.Height, back.Position.Y, 3);
        }
        else
        {
            Assert.Equal(A4W - front.Position.X - card.Width, back.Position.X, 3);
            Assert.Equal(front.Position.Y, back.Position.Y, 3);
        }
    }

    [Fact]
    public void Duplex_Off_SkipsInsides()
    {
        var p = Komplet(inside: true);
        var layout = SheetLayout.Compute(A4W, A4H, p.Cards, Outer(p), new PrintOptions { Duplex = false });
        Assert.False(layout.HasBack);
    }

    [Theory]
    [InlineData(false, 1.5, 0.5, 1.5, -0.5)]
    [InlineData(true, 1.5, 0.5, -1.5, 0.5)]
    public void MirrorToBack_CorrectsMeasuredShift(bool shortEdge, double shiftX, double shiftY, double expectDx, double expectDy)
    {
        // tył wychodzi przesunięty o (shiftX, shiftY) patrząc od przodu — korekta przesuwa go z powrotem
        var (plain, _) = SheetLayout.MirrorToBack(20, 30, 100, 90, A4W, A4H, shortEdge);
        var (fixedPos, _) = SheetLayout.MirrorToBack(20, 30, 100, 90, A4W, A4H, shortEdge, shiftX, shiftY);

        Assert.Equal(expectDx, fixedPos.X - plain.X, 3);
        Assert.Equal(expectDy, fixedPos.Y - plain.Y, 3);
    }
}
