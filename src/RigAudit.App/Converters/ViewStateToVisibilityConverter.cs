using System.Globalization;
using System.Windows;
using System.Windows.Data;
using RigAudit.App.ViewModels;

namespace RigAudit.App.Converters;

public class ViewStateToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ViewState current && parameter is string target && Enum.TryParse<ViewState>(target, out var expected))
            return current == expected ? Visibility.Visible : Visibility.Collapsed;
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
