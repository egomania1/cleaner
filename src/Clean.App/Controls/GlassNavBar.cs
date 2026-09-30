using System.Diagnostics;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using Windows.UI.ViewManagement;

namespace Clean.App.Controls;

public sealed record NavItem(string Tag, string Label, string Glyph, bool StartsGroup = false);

// Floating glass navigation. Ten labelled tabs do not fit the minimum window width, so only the selected tab
// shows its label; the others show their icon with the label as a tooltip. A glass block slides to the selected tab.
public sealed partial class GlassNavBar : UserControl
{
    private static readonly UISettings Settings = new();
    private static readonly TimeSpan SlideDuration = TimeSpan.FromMilliseconds(320);

    private readonly StackPanel _tabs = new() { Orientation = Orientation.Horizontal, Spacing = 2 };
    private readonly Border _indicator = new()
    {
        HorizontalAlignment = HorizontalAlignment.Left,
        CornerRadius = new CornerRadius(6),
        BorderThickness = new Thickness(1),
    };

    private readonly TranslateTransform _indicatorShift = new();
    private readonly Dictionary<string, (Button Button, TextBlock Label)> _buttons = [];
    private string? _selectedTag;
    private Rect _from;
    private Rect _to;
    private Rect _current;
    private long _slideStart;
    private bool _isSliding;

    public GlassNavBar()
    {
        IsTabStop = false;
        _indicator.Background = Resource("NavIndicatorBrush");
        _indicator.BorderBrush = Resource("CardBorderBrush");
        _indicator.RenderTransform = _indicatorShift;
        _indicator.Opacity = 0;

        Content = new Border
        {
            Padding = new Thickness(4),
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            Background = Resource("NavGlassBrush"),
            BorderBrush = Resource("CardBorderBrush"),
            Child = new Grid { Children = { _indicator, _tabs } },
        };

        Unloaded += (_, _) => StopSlide();
    }

    public event Action<string>? Selected;

    public string? SelectedTag => _selectedTag;

    public void SetItems(IEnumerable<NavItem> items)
    {
        _tabs.Children.Clear();
        _buttons.Clear();

        foreach (var item in items)
        {
            if (item.StartsGroup)
            {
                _tabs.Children.Add(new Border
                {
                    Width = 1,
                    Margin = new Thickness(6, 8, 6, 8),
                    Background = Resource("CardBorderBrush"),
                });
            }

            var label = new TextBlock
            {
                Text = item.Label,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                Visibility = Visibility.Collapsed,
            };
            var button = new Button
            {
                Style = (Style)Application.Current.Resources["NavTabButtonStyle"],
                Content = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    Children = { new FontIcon { Glyph = item.Glyph, FontSize = 15 }, label },
                },
            };
            AutomationProperties.SetName(button, item.Label);
            ToolTipService.SetToolTip(button, item.Label);
            button.Click += (_, _) => Select(item.Tag);
            button.SizeChanged += (_, _) =>
            {
                if (item.Tag == _selectedTag)
                {
                    SlideTo(button);
                }
            };

            _tabs.Children.Add(button);
            _buttons[item.Tag] = (button, label);
        }
    }

    public void Select(string tag)
    {
        if (tag == _selectedTag || !_buttons.TryGetValue(tag, out var target))
        {
            return;
        }

        if (_selectedTag is not null && _buttons.TryGetValue(_selectedTag, out var previous))
        {
            previous.Label.Visibility = Visibility.Collapsed;
            previous.Button.Opacity = 0.7;
        }

        _selectedTag = tag;
        target.Label.Visibility = Visibility.Visible;
        target.Button.Opacity = 1;
        foreach (var (otherTag, other) in _buttons)
        {
            if (otherTag != tag)
            {
                other.Button.Opacity = 0.7;
            }
        }

        // The label just became visible; the slide starts once the tab has been measured with it (SizeChanged).
        Selected?.Invoke(tag);
    }

    private void SlideTo(Button button)
    {
        var position = button.TransformToVisual(_tabs).TransformPoint(default);
        _to = new Rect(position.X, 0, button.ActualWidth, button.ActualHeight);

        if (_indicator.Opacity == 0 || !Settings.AnimationsEnabled)
        {
            _indicator.Opacity = 1;
            Apply(_to);
            return;
        }

        _from = _current;
        _slideStart = Stopwatch.GetTimestamp();
        if (!_isSliding)
        {
            CompositionTarget.Rendering += OnRendering;
            _isSliding = true;
        }
    }

    private void OnRendering(object? sender, object e)
    {
        var progress = Math.Min(Stopwatch.GetElapsedTime(_slideStart) / SlideDuration, 1);
        var eased = 1 - Math.Pow(1 - progress, 4);
        Apply(new Rect(
            _from.X + (_to.X - _from.X) * eased,
            0,
            _from.Width + (_to.Width - _from.Width) * eased,
            _to.Height));

        if (progress >= 1)
        {
            StopSlide();
        }
    }

    private void Apply(Rect rect)
    {
        _current = rect;
        _indicatorShift.X = rect.X;
        _indicator.Width = rect.Width;
        _indicator.Height = rect.Height;
    }

    private void StopSlide()
    {
        if (_isSliding)
        {
            CompositionTarget.Rendering -= OnRendering;
            _isSliding = false;
        }
    }

    private static Brush Resource(string key) => (Brush)Application.Current.Resources[key];
}
