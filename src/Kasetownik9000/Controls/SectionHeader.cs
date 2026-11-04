using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.UI;

namespace Kasetownik9000.Controls;

/// <summary>Nagłówek sekcji w stylu etykiety kasety: kolorowe paski + napis z dużym odstępem liter.</summary>
public sealed partial class SectionHeader : UserControl
{
    private static readonly Color[] StripeColors =
    [
        Color.FromArgb(255, 242, 176, 53), Color.FromArgb(255, 232, 114, 44),
        Color.FromArgb(255, 200, 64, 47), Color.FromArgb(255, 122, 59, 46),
    ];

    private readonly TextBlock _text;

    public SectionHeader()
    {
        var grid = new Grid { ColumnSpacing = 8, Margin = new Thickness(0, 4, 0, 2) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var stripes = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        foreach (var c in StripeColors)
            stripes.Children.Add(new Rectangle { Width = 3, Height = 12, Fill = new SolidColorBrush(c) });
        grid.Children.Add(stripes);

        _text = new TextBlock
        {
            FontFamily = new FontFamily("Bahnschrift"),
            FontSize = 12,
            FontWeight = new Windows.UI.Text.FontWeight { Weight = 700 },
            CharacterSpacing = 160,
            Foreground = new SolidColorBrush(Color.FromArgb(255, 242, 176, 53)),
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(_text, 1);
        grid.Children.Add(_text);

        var line = new Rectangle
        {
            Height = 1,
            Fill = new SolidColorBrush(Color.FromArgb(255, 78, 65, 56)),
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(line, 2);
        grid.Children.Add(line);

        Content = grid;
    }

    public string Text
    {
        get => _text.Text;
        set => _text.Text = (value ?? "").ToUpperInvariant();
    }
}
