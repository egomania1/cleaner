using Clean.Core.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace Clean.App.Converters;

public sealed partial class RiskLevelToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var key = value switch
        {
            RiskLevel.Safe => "SafeBrush",
            RiskLevel.Caution or RiskLevel.Expert => "CautionBrush",
            RiskLevel.Blocked => "BlockedBrush",
            _ => "MutedTextBrush",
        };

        return Application.Current.Resources[key];
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
