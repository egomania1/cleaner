using Clean.Core.Apps;
using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.UI;

namespace Clean.App.Controls;

public sealed record TreemapTile(string Key, string Label, string Caption, string ValueText, double Value, Color Color);

public sealed partial class TreemapChart : UserControl
{
    private const double Gap = 2;
    private const double DimmedOpacity = 0.45;

    public static readonly DependencyProperty TilesProperty =
        DependencyProperty.Register(nameof(Tiles), typeof(object), typeof(TreemapChart), new PropertyMetadata(null, OnTilesChanged));

    public static readonly DependencyProperty SelectedKeyProperty =
        DependencyProperty.Register(nameof(SelectedKey), typeof(string), typeof(TreemapChart), new PropertyMetadata(null, (sender, _) => ((TreemapChart)sender).UpdateHighlight()));

    private readonly Canvas _canvas = new();
    private readonly List<(TreemapTile Tile, Border Element)> _elements = [];
    private readonly Border _tooltip;
    private readonly TextBlock _tooltipTitle = new() { FontWeight = FontWeights.SemiBold, FontSize = 13 };
    private readonly TextBlock _tooltipBody = new() { FontSize = 12, Foreground = new SolidColorBrush(Color.FromArgb(255, 0xC3, 0xC2, 0xB7)) };
    private string? _hoveredKey;
    private bool _hasAnimated;

    public TreemapChart()
    {
        _tooltip = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0xF2, 0x24, 0x24, 0x24)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12, 8, 12, 8),
            IsHitTestVisible = false,
            Visibility = Visibility.Collapsed,
            Child = new StackPanel { Spacing = 2, Children = { _tooltipTitle, _tooltipBody } },
        };

        Content = _canvas;
        IsTabStop = true;
        UseSystemFocusVisuals = true;
        KeyDown += OnKeyDown;
        SizeChanged += (_, _) => Redraw();
    }

    public event EventHandler<string>? TileSelected;

    public IReadOnlyList<TreemapTile>? Tiles
    {
        get => (IReadOnlyList<TreemapTile>?)GetValue(TilesProperty);
        set => SetValue(TilesProperty, value);
    }

    public string? SelectedKey
    {
        get => (string?)GetValue(SelectedKeyProperty);
        set => SetValue(SelectedKeyProperty, value);
    }

    private static void OnTilesChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e) =>
        ((TreemapChart)sender).Redraw();

    private void Redraw()
    {
        _canvas.Children.Clear();
        _elements.Clear();

        var tiles = Tiles;
        if (tiles is null || tiles.Count == 0 || ActualWidth <= 0 || ActualHeight <= 0)
        {
            return;
        }

        // Tiles rise in once, when the data first arrives; later refreshes and resizes stay still.
        _canvas.ChildrenTransitions = _hasAnimated ? null : [new EntranceThemeTransition { IsStaggeringEnabled = true, FromVerticalOffset = 24 }];
        _hasAnimated = true;

        var rects = Treemap.Layout(tiles.Select(tile => tile.Value).ToList(), ActualWidth, ActualHeight);
        for (var index = 0; index < tiles.Count; index++)
        {
            var rect = rects[index];
            if (rect.Width < Gap * 2 || rect.Height < Gap * 2)
            {
                continue;
            }

            var element = CreateTile(tiles[index], rect.Width - Gap, rect.Height - Gap);
            Canvas.SetLeft(element, rect.X + Gap / 2);
            Canvas.SetTop(element, rect.Y + Gap / 2);
            _canvas.Children.Add(element);
            _elements.Add((tiles[index], element));
        }

        _canvas.Children.Add(_tooltip);
        UpdateHighlight();
    }

    private Border CreateTile(TreemapTile tile, double width, double height)
    {
        var element = new Border
        {
            Width = width,
            Height = height,
            CornerRadius = new CornerRadius(4),
            Background = new SolidColorBrush(tile.Color),
            BorderBrush = new SolidColorBrush(Colors.White),
            Padding = new Thickness(10, 8, 10, 8),
        };

        // Labels only where they fit: a clipped name is worse than none, the tooltip covers the rest.
        if (width >= 72 && height >= 40)
        {
            var labels = new StackPanel { Spacing = 1 };
            labels.Children.Add(new TextBlock
            {
                Text = tile.Label,
                FontSize = width > 180 && height > 80 ? 15 : 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Colors.White),
                TextTrimming = TextTrimming.CharacterEllipsis,
                TextWrapping = TextWrapping.NoWrap,
            });
            if (height >= 56)
            {
                labels.Children.Add(new TextBlock
                {
                    Text = tile.ValueText,
                    FontSize = 12,
                    Foreground = new SolidColorBrush(Color.FromArgb(0xD9, 0xFF, 0xFF, 0xFF)),
                });
            }

            element.Child = labels;
        }

        element.PointerEntered += (_, _) => SetHovered(tile.Key);
        element.PointerExited += (_, _) => SetHovered(null);
        element.PointerMoved += (_, e) => MoveTooltip(tile, e.GetCurrentPoint(_canvas).Position);
        element.Tapped += (_, _) =>
        {
            Focus(FocusState.Pointer);
            TileSelected?.Invoke(this, tile.Key);
        };
        return element;
    }

    // Arrow keys walk through the tiles in their list order (largest first), Home and End jump to the ends:
    // the same choice a mouse click makes, so the detail panel follows.
    private void OnKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (_elements.Count == 0)
        {
            return;
        }

        var current = _elements.FindIndex(entry => entry.Tile.Key == SelectedKey);
        int? target = e.Key switch
        {
            Windows.System.VirtualKey.Right or Windows.System.VirtualKey.Down => current < 0 ? 0 : Math.Min(current + 1, _elements.Count - 1),
            Windows.System.VirtualKey.Left or Windows.System.VirtualKey.Up => current < 0 ? 0 : Math.Max(current - 1, 0),
            Windows.System.VirtualKey.Home => 0,
            Windows.System.VirtualKey.End => _elements.Count - 1,
            _ => null,
        };

        if (target is { } index)
        {
            e.Handled = true;
            TileSelected?.Invoke(this, _elements[index].Tile.Key);
        }
    }

    private void MoveTooltip(TreemapTile tile, Windows.Foundation.Point position)
    {
        _tooltipTitle.Text = tile.Label;
        _tooltipBody.Text = $"{tile.ValueText} · {tile.Caption}";
        _tooltip.Visibility = Visibility.Visible;
        _tooltip.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));

        // Kept inside the chart: flipped to the left or above the pointer near the edges.
        var x = position.X + 16 + _tooltip.DesiredSize.Width > ActualWidth ? position.X - 16 - _tooltip.DesiredSize.Width : position.X + 16;
        var y = position.Y + 16 + _tooltip.DesiredSize.Height > ActualHeight ? position.Y - 16 - _tooltip.DesiredSize.Height : position.Y + 16;
        Canvas.SetLeft(_tooltip, Math.Max(0, x));
        Canvas.SetTop(_tooltip, Math.Max(0, y));
    }

    private void SetHovered(string? key)
    {
        _hoveredKey = key;
        if (key is null)
        {
            _tooltip.Visibility = Visibility.Collapsed;
        }

        ProtectedCursor = InputSystemCursor.Create(key is null ? InputSystemCursorShape.Arrow : InputSystemCursorShape.Hand);
        UpdateHighlight();
    }

    private void UpdateHighlight()
    {
        // x:Bind hands an empty string, not null, when nothing is selected.
        var selected = string.IsNullOrEmpty(SelectedKey) ? null : SelectedKey;
        var focus = _hoveredKey ?? selected;
        foreach (var (tile, element) in _elements)
        {
            var isFocused = tile.Key == _hoveredKey || tile.Key == selected;
            element.Opacity = focus is null || isFocused ? 1 : DimmedOpacity;
            element.BorderThickness = new Thickness(tile.Key == selected ? 2 : 0);
        }
    }
}
