using Kasetownik9000.Model;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace Kasetownik9000;

/// <summary>Wiersz na liście warstw: element (w grupie — wcięty) albo nagłówek grupy.</summary>
public sealed partial class LayerItem
{
    private LayerItem(string name, string glyph, IReadOnlyList<CardElement> members, bool hidden, bool locked, double indent,
        CardElement? element, ElementGroup? group)
    {
        Name = name;
        Glyph = glyph;
        Members = members;
        StateGlyph = hidden ? "" : locked ? "" : "";
        TextOpacity = hidden ? 0.45 : 1.0;
        Indent = new Thickness(indent, 0, 0, 0);
        Element = element;
        Group = group;
    }

    /// <summary>Element tego wiersza (null dla nagłówka grupy).</summary>
    public CardElement? Element { get; }
    /// <summary>Grupa, jeśli to nagłówek grupy.</summary>
    public ElementGroup? Group { get; }
    /// <summary>Elementy, które zaznacza ten wiersz (nagłówek — całą grupę).</summary>
    public IReadOnlyList<CardElement> Members { get; }

    public string Name { get; }
    public string Glyph { get; }
    public string StateGlyph { get; }
    public double TextOpacity { get; }
    public Thickness Indent { get; }
    public Windows.UI.Text.FontWeight Weight => Group is null ? Microsoft.UI.Text.FontWeights.Normal : Microsoft.UI.Text.FontWeights.SemiBold;

    public static LayerItem ForElement(CardElement e, bool inGroup) =>
        new(string.IsNullOrWhiteSpace(e.Name) ? e.KindName : e.Name, e.Glyph, [e], e.Hidden, e.Locked, inGroup ? 18 : 0, e, null);

    public static LayerItem ForGroup(ElementGroup g, IReadOnlyList<CardElement> members) =>
        new(g.Name, "", members, members.All(m => m.Hidden), members.All(m => m.Locked), 0, null, g);
}

/// <summary>Pozycja na liście czcionek (wyświetlana własną czcionką).</summary>
public sealed partial class FontItem(string name)
{
    private FontFamily? _family;
    public string Name { get; } = name;
    public FontFamily Family => _family ??= new FontFamily(Name);
    public override string ToString() => Name;
}
