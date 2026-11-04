using Kasetownik9000.Model;
using Microsoft.UI.Xaml.Media;

namespace Kasetownik9000;

/// <summary>Wiersz na liście warstw.</summary>
public sealed partial class LayerItem(CardElement element)
{
    public CardElement Element { get; } = element;
    public string Name { get; } = string.IsNullOrWhiteSpace(element.Name) ? element.KindName : element.Name;
    public string Glyph { get; } = element.Glyph;
    public string StateGlyph { get; } = element.Hidden ? "" : element.Locked ? "" : "";
    public double TextOpacity { get; } = element.Hidden ? 0.45 : 1.0;
}

/// <summary>Pozycja na liście czcionek (wyświetlana własną czcionką).</summary>
public sealed partial class FontItem(string name)
{
    private FontFamily? _family;
    public string Name { get; } = name;
    public FontFamily Family => _family ??= new FontFamily(Name);
    public override string ToString() => Name;
}
