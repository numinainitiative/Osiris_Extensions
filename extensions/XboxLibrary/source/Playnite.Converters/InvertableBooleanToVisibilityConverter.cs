using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Playnite.Converters;

[ValueConversion(typeof(bool), typeof(Visibility))]
public class InvertableBooleanToVisibilityConverter : IValueConverter
{
	private enum Parameters
	{
		Normal,
		Inverted
	}

	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		bool flag = (bool)value;
		if ((Parameters)Enum.Parse(typeof(Parameters), (string)parameter) == Parameters.Inverted)
		{
			return flag ? Visibility.Collapsed : Visibility.Visible;
		}
		return (!flag) ? Visibility.Collapsed : Visibility.Visible;
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		return null;
	}
}
