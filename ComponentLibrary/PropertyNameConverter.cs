using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Windows.Data;
using System.Windows.Media;

namespace ComponentLibrary
{
    internal class PropertyNameConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return CustomConvert(value);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value;
        }

        public static string CustomConvert(object value)
        {
            return value switch
            {
                PropertyInfo propertyInfo => propertyInfo.Name.Split().Last(),
                int intValue => intValue.ToString(),
                FontFamily fontFamily => fontFamily.Source,
                _ => value?.ToString()
            };
        }
    }
}