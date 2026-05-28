using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace PlayniteStorageView.Views.Converters
{
    /// <summary>
    /// Collapses an element when bound value is null, zero, or false.
    /// Useful for hiding the "unknown-size" caption when the count is zero.
    /// Pass ConverterParameter="Invert" to reverse the logic.
    /// </summary>
    [ValueConversion(typeof(object), typeof(Visibility))]
    public sealed class ZeroToCollapsedConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isZero = IsZeroOrNullOrFalse(value);
            bool invert = string.Equals(parameter as string, "Invert", StringComparison.OrdinalIgnoreCase);
            bool collapsed = invert ? !isZero : isZero;
            return collapsed ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }

        private static bool IsZeroOrNullOrFalse(object v)
        {
            if (v == null) return true;
            switch (v)
            {
                case bool b: return !b;
                case ulong u: return u == 0UL;
                case long l: return l == 0L;
                case int i: return i == 0;
                case double d: return d == 0.0;
                default: return false;
            }
        }
    }
}
