using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace HotelRatingViewer.Converters
{
    public class ColorNameToBrushConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string colorName)
            {
                try
                {
                    // Handle basic color names
                    if (string.Equals(colorName, "Green", StringComparison.OrdinalIgnoreCase))
                        return Brushes.Green;
                    if (string.Equals(colorName, "Orange", StringComparison.OrdinalIgnoreCase))
                        return Brushes.Orange;
                    if (string.Equals(colorName, "Red", StringComparison.OrdinalIgnoreCase))
                        return Brushes.Red;
                    if (string.Equals(colorName, "Blue", StringComparison.OrdinalIgnoreCase))
                        return Brushes.Blue;

                    // Fallback to parsing (e.g., hex codes or other names)
                    return Brush.Parse(colorName);
                }
                catch
                {
                    return Brushes.Black;
                }
            }
            return Brushes.Black;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
