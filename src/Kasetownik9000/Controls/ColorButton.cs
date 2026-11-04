using Kasetownik9000.Model;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.UI;

namespace Kasetownik9000.Controls;

/// <summary>Przycisk z próbką koloru; po kliknięciu paleta retro + pełny próbnik kolorów.</summary>
public sealed partial class ColorButton : Button
{
    private readonly Border _swatch;
    private readonly TextBlock _hex;
    private readonly ColorPicker _picker;
    private bool _suppress;
    private Color _color = Microsoft.UI.Colors.Black;

    public event Action<ColorButton, Color>? ColorChanged;

    public ColorButton()
    {
        HorizontalAlignment = HorizontalAlignment.Stretch;
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        Padding = new Thickness(4);
        MinHeight = 32;

        var grid = new Grid { ColumnSpacing = 8 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(34) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // szachownica pod próbką, żeby było widać przezroczystość
        var checker = new Grid { CornerRadius = new CornerRadius(3) };
        checker.Children.Add(new Rectangle { Fill = new SolidColorBrush(Color.FromArgb(255, 220, 220, 220)) });
        checker.Children.Add(new Polygon
        {
            Points = { new Windows.Foundation.Point(0, 22), new Windows.Foundation.Point(34, 0), new Windows.Foundation.Point(34, 22) },
            Fill = new SolidColorBrush(Color.FromArgb(255, 120, 120, 120)),
        });
        _swatch = new Border
        {
            CornerRadius = new CornerRadius(3),
            BorderBrush = new SolidColorBrush(Color.FromArgb(160, 0, 0, 0)),
            BorderThickness = new Thickness(1),
        };
        checker.Children.Add(_swatch);
        checker.Height = 22;
        grid.Children.Add(checker);

        _hex = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            FontFamily = new FontFamily("Cascadia Mono, Consolas"),
            FontSize = 12,
            Opacity = 0.85,
        };
        Grid.SetColumn(_hex, 1);
        grid.Children.Add(_hex);
        Content = grid;

        _picker = new ColorPicker
        {
            IsAlphaEnabled = true,
            IsAlphaSliderVisible = true,
            IsAlphaTextInputVisible = true,
            IsColorChannelTextInputVisible = false,
            IsHexInputVisible = true,
            IsMoreButtonVisible = false,
            ColorSpectrumShape = ColorSpectrumShape.Box,
        };
        _picker.ColorChanged += (_, e) =>
        {
            if (_suppress) return;
            SetColor(e.NewColor, raise: true, updatePicker: false);
        };

        var palette = new Grid { RowSpacing = 4, ColumnSpacing = 4 };
        for (int i = 0; i < 8; i++) palette.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });
        for (int i = 0; i < ColorUtil.RetroPalette.Length; i++)
        {
            var c = ColorUtil.Parse(ColorUtil.RetroPalette[i]);
            var b = new Button
            {
                Width = 30, Height = 24, Padding = new Thickness(0), MinWidth = 0, MinHeight = 0,
                Background = new SolidColorBrush(c),
                BorderBrush = new SolidColorBrush(Color.FromArgb(120, 0, 0, 0)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(3),
            };
            ToolTipService.SetToolTip(b, ColorUtil.ToHex(c));
            b.Click += (_, _) => SetColor(c, raise: true, updatePicker: true);
            if (i / 8 >= palette.RowDefinitions.Count) palette.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid.SetRow(b, i / 8);
            Grid.SetColumn(b, i % 8);
            palette.Children.Add(b);
        }

        var panel = new StackPanel { Spacing = 10 };
        panel.Children.Add(new TextBlock { Text = "PALETA RETRO", FontSize = 11, CharacterSpacing = 120, Opacity = 0.75 });
        panel.Children.Add(palette);
        panel.Children.Add(_picker);

        Flyout = new Flyout { Content = panel, Placement = FlyoutPlacementMode.Left };
        UpdateSwatch();
    }

    /// <summary>Tryb kompaktowy — sama próbka bez kodu koloru.</summary>
    public bool Compact
    {
        get => _hex.Visibility == Visibility.Collapsed;
        set
        {
            _hex.Visibility = value ? Visibility.Collapsed : Visibility.Visible;
            if (Content is Grid g)
                g.ColumnDefinitions[1].Width = value ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
            if (value)
            {
                HorizontalAlignment = HorizontalAlignment.Left;
                Width = 44;
            }
        }
    }

    public bool AlphaEnabled
    {
        get => _picker.IsAlphaEnabled;
        set
        {
            _picker.IsAlphaEnabled = value;
            _picker.IsAlphaSliderVisible = value;
            _picker.IsAlphaTextInputVisible = value;
        }
    }

    public Color Color
    {
        get => _color;
        set => SetColor(value, raise: false, updatePicker: true);
    }

    public string Hex
    {
        get => ColorUtil.ToHex(_color);
        set => Color = ColorUtil.Parse(value);
    }

    private void SetColor(Color c, bool raise, bool updatePicker)
    {
        bool changed = !c.Equals(_color);
        _color = c;
        UpdateSwatch();
        if (updatePicker)
        {
            _suppress = true;
            _picker.Color = c;
            _suppress = false;
        }
        if (raise && changed) ColorChanged?.Invoke(this, c);
    }

    private void UpdateSwatch()
    {
        _swatch.Background = new SolidColorBrush(_color);
        _hex.Text = _color.A == 255 ? $"#{_color.R:X2}{_color.G:X2}{_color.B:X2}" : $"#{_color.R:X2}{_color.G:X2}{_color.B:X2} · {_color.A * 100 / 255}%";
    }
}
