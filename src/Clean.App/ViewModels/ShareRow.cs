using Clean.Core.Formatting;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace Clean.App.ViewModels;

// One bar of a ranked bar chart: label, value and a bar proportional to the largest row.
public sealed class ShareRow(string label, long sizeBytes, string detail, double ratio, SolidColorBrush brush)
{
    public string Label => label;

    public long SizeBytes => sizeBytes;

    public string SizeText => ByteSize.Format(sizeBytes);

    public string DetailText => detail;

    public SolidColorBrush Brush => brush;

    public GridLength BarWidth => new(Math.Max(ratio, 0.002), GridUnitType.Star);

    public GridLength BarRest => new(Math.Max(1 - ratio, 0.0001), GridUnitType.Star);
}
