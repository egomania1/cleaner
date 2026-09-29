using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;

namespace Clean.App.Controls;

public sealed partial class LoadingOverlay : UserControl
{
    private static readonly TimeSpan FadeDuration = TimeSpan.FromMilliseconds(350);

    public static readonly DependencyProperty MessageProperty =
        DependencyProperty.Register(nameof(Message), typeof(string), typeof(LoadingOverlay), new PropertyMetadata(string.Empty));

    public LoadingOverlay()
    {
        InitializeComponent();
    }

    public string Message
    {
        get => (string)GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    public Task FadeOutAsync()
    {
        var completion = new TaskCompletionSource();
        var fade = new DoubleAnimation { To = 0, Duration = FadeDuration };
        Storyboard.SetTarget(fade, this);
        Storyboard.SetTargetProperty(fade, nameof(Opacity));

        var storyboard = new Storyboard();
        storyboard.Children.Add(fade);
        storyboard.Completed += (_, _) => completion.SetResult();
        storyboard.Begin();

        return completion.Task;
    }
}
