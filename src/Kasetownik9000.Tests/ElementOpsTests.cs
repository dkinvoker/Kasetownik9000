using static Kasetownik9000.Tests.TestData;

namespace Kasetownik9000.Tests;

/// <summary>Grupy, kolejność warstw, wyrównywanie, duplikowanie, skalowanie i obrót kilku elementów.</summary>
public class ElementOpsTests
{
    [Fact]
    public void Group_PutsMembersNextToEachOther_AtTopmostMember()
    {
        var a = Box("A", 0, 0); var b = Box("B", 0, 0); var c = Box("C", 0, 0); var d = Box("D", 0, 0);
        var doc = DocWith(a, b, c, d);

        var g = ElementOps.Group(doc, [a, c]);

        Assert.NotNull(g);
        Assert.Equal("Grupa 1", g!.Name);
        Assert.Equal("B,A,C,D", Names(doc)); // grupa (A, C) leży tam, gdzie był jej najwyższy element
        Assert.Equal(g.Id, a.GroupId);
        Assert.Equal(g.Id, c.GroupId);
        Assert.Null(b.GroupId);
    }

    [Fact]
    public void Group_NeedsTwoElements_AndNamesAreUnique()
    {
        var a = Box("A", 0, 0); var b = Box("B", 0, 0); var c = Box("C", 0, 0); var d = Box("D", 0, 0);
        var doc = DocWith(a, b, c, d);

        Assert.Null(ElementOps.Group(doc, [a]));
        Assert.Equal("Grupa 1", ElementOps.Group(doc, [a, b])!.Name);
        Assert.Equal("Grupa 2", ElementOps.Group(doc, [c, d])!.Name);
        Assert.Equal(2, doc.Groups.Count);
    }

    [Fact]
    public void Group_OfElementsFromAnotherGroup_LeavesNoSingleMemberGroups()
    {
        var a = Box("A", 0, 0); var b = Box("B", 0, 0); var c = Box("C", 0, 0);
        var doc = DocWith(a, b, c);
        ElementOps.Group(doc, [a, b]);

        ElementOps.Group(doc, [b, c]); // B przechodzi do nowej grupy — w starej zostaje tylko A

        Assert.Null(a.GroupId);
        Assert.Single(doc.Groups);
        Assert.Equal(b.GroupId, c.GroupId);
    }

    [Fact]
    public void Ungroup_RemovesGroupRecord()
    {
        var a = Box("A", 0, 0); var b = Box("B", 0, 0);
        var doc = DocWith(a, b);
        ElementOps.Group(doc, [a, b]);

        ElementOps.Ungroup(doc, [a, b]);

        Assert.Empty(doc.Groups);
        Assert.Null(a.GroupId);
        Assert.Null(b.GroupId);
    }

    [Fact]
    public void Delete_LastButOneMember_DissolvesGroup()
    {
        var a = Box("A", 0, 0); var b = Box("B", 0, 0); var c = Box("C", 0, 0);
        var doc = DocWith(a, b, c);
        ElementOps.Group(doc, [a, b]);

        ElementOps.Delete(doc, [a]);

        Assert.Equal("B,C", Names(doc));
        Assert.Null(b.GroupId);
        Assert.Empty(doc.Groups);
    }

    [Fact]
    public void ExpandToGroups_AddsWholeGroups_InLayerOrder()
    {
        var a = Box("A", 0, 0); var b = Box("B", 0, 0); var c = Box("C", 0, 0); var d = Box("D", 0, 0);
        var doc = DocWith(a, b, c, d);
        ElementOps.Group(doc, [b, c]);

        var set = ElementOps.ExpandToGroups(doc, [d, b]);

        Assert.Equal(new[] { "B", "C", "D" }, set.Select(e => e.Name));
    }

    [Theory]
    [InlineData(LayerOrder.Front, "A,D,B,C")]
    [InlineData(LayerOrder.Back, "B,C,A,D")]
    [InlineData(LayerOrder.Forward, "A,D,B,C")]
    [InlineData(LayerOrder.Backward, "B,C,A,D")]
    public void Reorder_MovesGroupAsOneUnit(LayerOrder order, string expected)
    {
        var a = Box("A", 0, 0); var b = Box("B", 0, 0); var c = Box("C", 0, 0); var d = Box("D", 0, 0);
        var doc = DocWith(a, b, c, d);
        ElementOps.Group(doc, [b, c]);

        ElementOps.Reorder(doc, [b, c], order);

        Assert.Equal(expected, Names(doc));
    }

    [Fact]
    public void Reorder_SingleElementInsideGroup_StaysInsideGroup()
    {
        var a = Box("A", 0, 0); var b = Box("B", 0, 0); var c = Box("C", 0, 0); var d = Box("D", 0, 0);
        var doc = DocWith(a, b, c, d);
        ElementOps.Group(doc, [b, c]);

        ElementOps.Reorder(doc, [b], LayerOrder.Front);

        Assert.Equal("A,C,B,D", Names(doc)); // B na wierzch grupy, ale nie ponad D
    }

    [Fact]
    public void Reorder_ForwardTwoSeparateElements()
    {
        var a = Box("A", 0, 0); var b = Box("B", 0, 0); var c = Box("C", 0, 0); var d = Box("D", 0, 0);
        var doc = DocWith(a, b, c, d);

        ElementOps.Reorder(doc, [a, c], LayerOrder.Forward);

        Assert.Equal("B,A,D,C", Names(doc));
    }

    [Fact]
    public void Duplicate_WholeGroup_GetsNewGroupAboveOriginals()
    {
        var a = Box("A", 0, 0); var b = Box("B", 5, 5); var c = Box("C", 0, 0);
        var doc = DocWith(a, b, c);
        var g = ElementOps.Group(doc, [a, b])!;

        var copies = ElementOps.Duplicate(doc, [a, b], 3, 3);

        Assert.Equal(2, copies.Count);
        Assert.Equal("A,B,A 2,B 2,C", Names(doc));
        Assert.NotNull(copies[0].GroupId);
        Assert.NotEqual(g.Id, copies[0].GroupId);
        Assert.Equal(copies[0].GroupId, copies[1].GroupId);
        Assert.Equal($"{g.Name} (kopia)", doc.FindGroup(copies[0].GroupId)!.Name);
        Assert.Equal(8, copies[1].X);
        Assert.NotEqual(a.Id, copies[0].Id);
    }

    [Fact]
    public void Prepare_PartOfGroup_HasNoGroup_AndIsUnlocked()
    {
        var a = Box("A", 0, 0); var b = Box("B", 0, 0);
        a.Locked = true;
        var doc = DocWith(a, b);
        ElementOps.Group(doc, [a, b]);

        var copies = ElementOps.Prepare(doc, [a], doc.Groups, 0, 0);

        Assert.Null(copies[0].GroupId);
        Assert.False(copies[0].Locked);
        Assert.Equal("A 2", copies[0].Name);
    }

    [Fact]
    public void Bounds_OfRotatedElement_SwapsSides()
    {
        var e = Box("A", 0, 0, 20, 10);
        e.Rotation = 90;

        var b = ElementOps.Bounds(e);

        Assert.Equal(10, b.W, 6);
        Assert.Equal(20, b.H, 6);
        Assert.Equal(10, b.CenterX, 6);
        Assert.Equal(5, b.CenterY, 6);
    }

    [Theory]
    [InlineData(AlignMode.Left, 0, 0)]
    [InlineData(AlignMode.Right, 40, 40)]
    [InlineData(AlignMode.CenterX, 20, 20)]
    public void Align_Horizontally(AlignMode mode, double expectedAx, double expectedBx)
    {
        var a = Box("A", 0, 0); var b = Box("B", 40, 20);
        var doc = DocWith(a, b);

        ElementOps.Align(doc, [a, b], mode);

        Assert.Equal(expectedAx, a.X, 6);
        Assert.Equal(expectedBx, b.X, 6);
        Assert.Equal(0, a.Y);
        Assert.Equal(20, b.Y);
    }

    [Fact]
    public void Align_KeepsGroupLayout_AndSkipsLockedElements()
    {
        var a = Box("A", 10, 0); var b = Box("B", 30, 0); // grupa: B 20 mm na prawo od A
        var c = Box("C", 60, 0);
        var locked = Box("L", 0, 40);
        locked.Locked = true;
        var doc = DocWith(a, b, c, locked);
        ElementOps.Group(doc, [a, b]);

        ElementOps.Align(doc, [a, b, c, locked], AlignMode.Right);

        Assert.Equal(40, a.X, 6);  // grupa przesunięta w całości: prawa krawędź B = 70
        Assert.Equal(60, b.X, 6);
        Assert.Equal(60, c.X, 6);
        Assert.Equal(0, locked.X); // zablokowany się nie rusza
    }

    [Fact]
    public void Align_WholeGroupSelected_AlignsMembersToEachOther()
    {
        var a = Box("A", 10, 0); var b = Box("B", 30, 5);
        var doc = DocWith(a, b);
        ElementOps.Group(doc, [a, b]);

        ElementOps.Align(doc, [a, b], AlignMode.Top);

        Assert.Equal(0, a.Y);
        Assert.Equal(0, b.Y);
    }

    [Fact]
    public void Distribute_MakesEqualGaps()
    {
        var a = Box("A", 0, 0, 10); var b = Box("B", 14, 0, 20); var c = Box("C", 90, 0, 10);
        var doc = DocWith(a, b, c);

        ElementOps.Distribute(doc, [a, b, c], horizontal: true);

        // od 0 do 100, szerokości 10 + 20 + 10 → odstępy po 30
        Assert.Equal(0, a.X, 6);
        Assert.Equal(40, b.X, 6);
        Assert.Equal(90, c.X, 6);
    }

    [Fact]
    public void ApplyScale_ScalesFromAnchor_IncludingFontAndStroke()
    {
        var t0 = Text("T", 10, 10, 20, 10, pt: 12);
        var s0 = new ShapeElement { X = 30, Y = 10, Width = 10, Height = 10, StrokeWidth = 0.5, CornerRadius = 2 };
        var t = (TextElement)ElementOps.Clone(t0);
        var s = (ShapeElement)ElementOps.Clone(s0);

        ElementOps.ApplyScale(t, t0, 10, 10, 2);
        ElementOps.ApplyScale(s, s0, 10, 10, 2);

        Assert.Equal((10d, 10d, 40d, 20d), (t.X, t.Y, t.Width, t.Height));
        Assert.Equal(24, t.FontSize);
        Assert.Equal((50d, 10d, 20d, 20d), (s.X, s.Y, s.Width, s.Height));
        Assert.Equal(1, s.StrokeWidth);
        Assert.Equal(4, s.CornerRadius);
    }

    [Fact]
    public void ApplyRotation_AroundSharedCenter()
    {
        var e0 = Box("A", 20, -5, 10, 10); // środek (25, 0)
        var e = ElementOps.Clone(e0);

        ElementOps.ApplyRotation(e, e0, 0, 0, 90);

        Assert.Equal(0, e.CenterX, 2);  // (25, 0) obrócone o 90° wokół (0, 0) → (0, 25)
        Assert.Equal(25, e.CenterY, 2);
        Assert.Equal(90, e.Rotation);

        ElementOps.ApplyRotation(e, e0, 0, 0, 270);
        Assert.Equal(-90, e.Rotation);  // kąt sprowadzony do (-180, 180]
    }

    [Fact]
    public void WholeGroup_OnlyForExactlyOneCompleteGroup()
    {
        var a = Box("A", 0, 0); var b = Box("B", 0, 0); var c = Box("C", 0, 0);
        var doc = DocWith(a, b, c);
        var g = ElementOps.Group(doc, [a, b]);

        Assert.Same(g, ElementOps.WholeGroup(doc, [a, b]));
        Assert.Null(ElementOps.WholeGroup(doc, [a]));
        Assert.Null(ElementOps.WholeGroup(doc, [a, b, c]));
    }

    [Fact]
    public void DecodeClipboardText_Utf8AndUtf16()
    {
        const string json = "{\"x\":\"zażółć\"}";
        Assert.Equal(json, ElementOps.DecodeClipboardText(System.Text.Encoding.UTF8.GetBytes(json)));
        // tak oddaje dane Windows, gdy zapisano je jako string: UTF-16 z zerem na końcu
        var utf16 = System.Text.Encoding.Unicode.GetBytes(json + "\0");
        Assert.Equal(json, ElementOps.DecodeClipboardText(utf16));
        var bom = System.Text.Encoding.UTF8.GetPreamble().Concat(System.Text.Encoding.UTF8.GetBytes(json)).ToArray();
        Assert.Equal(json, ElementOps.DecodeClipboardText(bom));
    }
}
