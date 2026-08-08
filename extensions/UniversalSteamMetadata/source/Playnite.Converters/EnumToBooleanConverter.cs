using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Markup;

namespace Playnite.Converters;

public class EnumToBooleanConverter : MarkupExtension, IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		return value.Equals(parameter);
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		if (!value.Equals(true))
		{
			return Binding.DoNothing;
		}
		return parameter;
	}

	public override object ProvideValue(IServiceProvider serviceProvider)
	{
		return this;
	}
}
