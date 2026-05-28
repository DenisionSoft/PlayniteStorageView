using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace PlayniteStorageView.Views.Converters
{
    /// <summary>
    /// Collapses an element when the bound string is null or empty.
    /// Used to hide the game-list icon placeholder for entries without an icon.
    /// </summary>
    [ValueConversion(typeof(string), typeof(Visibility))]
    public sealed class NullOrEmptyToCollapsedConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var s = value as string;
            return string.IsNullOrEmpty(s) ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
