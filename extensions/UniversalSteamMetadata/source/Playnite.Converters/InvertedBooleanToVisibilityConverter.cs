using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;

namespace Playnite.Converters;

[ValueConversion(typeof(bool), typeof(Visibility))]
public class InvertedBooleanToVisibilityConverter : MarkupExtension, IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		return ((bool)value) ? Visibility.Collapsed : Visibility.Visible;
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		return null;
	}

	public override object ProvideValue(IServiceProvider serviceProvider)
	{
		return this;
	}
}
