using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace KanbanFiles.Converters;

public class TagColorToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is string hex && !string.IsNullOrEmpty(hex))
        {
            try
            {
                hex = hex.TrimStart('#');
                byte r = System.Convert.ToByte(hex[..2], 16);
                byte g = System.Convert.ToByte(hex[2..4], 16);
                byte b = System.Convert.ToByte(hex[4..6], 16);
                return new SolidColorBrush(Color.FromArgb(255, r, g, b));
            }
            catch
            {
                // Fall through to default
            }
        }
        return new SolidColorBrush(Color.FromArgb(255, 52, 152, 219));
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
