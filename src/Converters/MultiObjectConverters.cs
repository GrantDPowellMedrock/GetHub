using System;
using System.Collections.Generic;
using System.Globalization;

using Avalonia.Data.Converters;

namespace GetHub.Converters
{
    public class AreEqualMultiConverter : IMultiValueConverter
    {
        public object Convert(IList<object> values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Count < 2)
                return false;

            var first = values[0];
            for (var i = 1; i < values.Count; i++)
            {
                if (!Equals(first, values[i]))
                    return false;
            }
            return true;
        }
    }

    public static class MultiObjectConverters
    {
        public static readonly IMultiValueConverter AreEqual = new AreEqualMultiConverter();
    }
}
