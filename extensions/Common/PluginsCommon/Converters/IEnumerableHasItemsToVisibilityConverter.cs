using System;
using System.Collections;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;

namespace PluginsCommon.Converters
{
    public class IEnumerableHasItemsToVisibilityConverter : MarkupExtension, IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is IEnumerable enumerable)
            {
                bool invertResult = parameter != null && System.Convert.ToBoolean(parameter);
                if (enumerable.GetEnumerator().MoveNext())
                {
                    return invertResult ? Visibility.Collapsed : Visibility.Visible;
                }
                else
                {
                    return invertResult ? Visibility.Visible : Visibility.Collapsed;
                }
            }

            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            return this;
        }
    }
}