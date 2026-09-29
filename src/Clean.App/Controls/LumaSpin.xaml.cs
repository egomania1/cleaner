using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.Foundation;

namespace Clean.App.Controls;

public sealed partial class LumaSpin : UserControl
{
    private const double BoxSize = 65;
    private const double Step = 35;
    private static readonly TimeSpan CycleDuration = TimeSpan.FromSeconds(2.5);

    // Distances from each edge of the box, in the order the shape travels around it.
    private static readonly Inset[] Path =
    [
        new(Top: 0, Right: Step, Bottom: Step, Left: 0),
        new(Top: 0, Right: Step, Bottom: 0, Left: 0),
        new(Top: Step, Right: Step, Bottom: 0, Left: 0),
        new(Top: Step, Right: 0, Bottom: 0, Left: 0),
        new(Top: Step, Right: 0, Bottom: 0, Left: Step),
        new(Top: 0, Right: 0, Bottom: 0, Left: Step),
        new(Top: 0, Right: 0, Bottom: Step, Left: Step),
        new(Top: 0, Right: 0, Bottom: Step, Left: 0),
    ];

    public static readonly DependencyProperty StartStepProperty =
        DependencyProperty.Register(nameof(StartStep), typeof(int), typeof(LumaSpin), new PropertyMetadata(0));

    private readonly Storyboard _storyboard = new() { RepeatBehavior = RepeatBehavior.Forever };

    public LumaSpin()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        FirstShape.SizeChanged += OnShapeSizeChanged;
        SecondShape.SizeChanged += OnShapeSizeChanged;
    }

    public int StartStep
    {
        get => (int)GetValue(StartStepProperty);
        set => SetValue(StartStepProperty, value);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _storyboard.Children.Clear();
        AddShapeAnimations(FirstShape, StartStep);
        AddShapeAnimations(SecondShape, StartStep + Path.Length / 2);
        _storyboard.Begin();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e) => _storyboard.Stop();

    // WinUI stretches an oversized corner radius into an ellipse, whereas the CSS original clamps it
    // to half the shortest side, which keeps the long shapes pill-shaped.
    private static void OnShapeSizeChanged(object sender, SizeChangedEventArgs e)
    {
        var shape = (Border)sender;
        shape.CornerRadius = new CornerRadius(Math.Min(e.NewSize.Width, e.NewSize.Height) / 2);
    }

    private void AddShapeAnimations(Border shape, int startStep)
    {
        AddAnimation(shape, "(Canvas.Left)", startStep, inset => inset.Left);
        AddAnimation(shape, "(Canvas.Top)", startStep, inset => inset.Top);
        AddAnimation(shape, nameof(Width), startStep, inset => BoxSize - inset.Left - inset.Right);
        AddAnimation(shape, nameof(Height), startStep, inset => BoxSize - inset.Top - inset.Bottom);
    }

    private void AddAnimation(Border shape, string property, int startStep, Func<Inset, double> valueOf)
    {
        var animation = new DoubleAnimationUsingKeyFrames { EnableDependentAnimation = true };
        var stepDuration = CycleDuration / Path.Length;

        for (var index = 0; index <= Path.Length; index++)
        {
            var inset = Path[(startStep + index) % Path.Length];
            animation.KeyFrames.Add(CreateKeyFrame(valueOf(inset), stepDuration * index, isFirst: index == 0));
        }

        Storyboard.SetTarget(animation, shape);
        Storyboard.SetTargetProperty(animation, property);
        _storyboard.Children.Add(animation);
    }

    private static DoubleKeyFrame CreateKeyFrame(double value, TimeSpan time, bool isFirst)
    {
        var keyTime = KeyTime.FromTimeSpan(time);
        if (isFirst)
        {
            return new DiscreteDoubleKeyFrame { Value = value, KeyTime = keyTime };
        }

        // Same curve as the CSS "ease" timing function used by the original web loader.
        var ease = new KeySpline { ControlPoint1 = new Point(0.25, 0.1), ControlPoint2 = new Point(0.25, 1) };
        return new SplineDoubleKeyFrame { Value = value, KeyTime = keyTime, KeySpline = ease };
    }

    private readonly record struct Inset(double Top, double Right, double Bottom, double Left);
}
