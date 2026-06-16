using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using RigAudit.Core.Findings;

namespace RigAudit.App.Converters;

public class SeverityToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Severity severity)
        {
            return severity switch
            {
                Severity.Warning => new SolidColorBrush(Colors.DarkOrange),
                Severity.Info => new SolidColorBrush(Colors.SteelBlue),
                _ => new SolidColorBrush(Colors.Gray)
            };
        }
        return new SolidColorBrush(Colors.Gray);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
