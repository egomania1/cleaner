using System.Globalization;
using Clean.App.Controls;
using Clean.Core.Apps;
using Clean.Core.Formatting;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace Clean.App.ViewModels;

public sealed class GroupShareRow(AppGroup group, long sizeBytes, int appCount, long totalBytes, long largestBytes)
{
    private static readonly CultureInfo French = CultureInfo.GetCultureInfo("fr-FR");

    public string Label => AppGroups.Label(group);

    public SolidColorBrush Brush => new(group == AppGroup.Other ? ChartPalette.Other : ChartPalette.Of(group));

    public string SizeText => ByteSize.Format(sizeBytes);

    public string DetailText => $"{appCount} appli{(appCount > 1 ? "s" : string.Empty)} · {(totalBytes > 0 ? (double)sizeBytes / totalBytes * 100 : 0).ToString("0", French)} %";

    public GridLength BarWidth => new(Math.Max(Ratio, 0.002), GridUnitType.Star);

    public GridLength BarRest => new(Math.Max(1 - Ratio, 0.0001), GridUnitType.Star);

    private double Ratio => largestBytes > 0 ? (double)sizeBytes / largestBytes : 0;
}
