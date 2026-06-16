using System.Globalization;
using System.Windows.Data;
using RigAudit.Core.Findings;

namespace RigAudit.App.Converters;

public class SeverityToGlyphConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Severity severity)
        {
            return severity switch
            {
                Severity.Warning => "[!]",
                Severity.Info => "[i]",
                _ => "[ ]"
            };
        }

        return "[ ]";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
