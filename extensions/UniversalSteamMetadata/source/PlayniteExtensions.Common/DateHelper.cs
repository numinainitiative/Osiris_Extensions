using System;
using System.Globalization;
using Playnite.SDK.Models;

namespace PlayniteExtensions.Common;

public static class DateHelper
{
	public static ReleaseDate? ParseReleaseDate(string dateString)
	{
		if (string.IsNullOrWhiteSpace(dateString))
		{
			return null;
		}
		CultureInfo currentCulture = CultureInfo.CurrentCulture;
		CultureInfo invariantCulture = CultureInfo.InvariantCulture;
		DateTimeStyles dateTimeStyles = DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeLocal;
		if (DateTime.TryParseExact(dateString, "yyyy", currentCulture, dateTimeStyles, out var result) || DateTime.TryParseExact(dateString, "yyyy", invariantCulture, dateTimeStyles, out result))
		{
			return new ReleaseDate(result.Year);
		}
		string[] formats = new string[2] { "MMM yyyy", "MMMM yyyy" };
		if (DateTime.TryParseExact(dateString, formats, currentCulture, dateTimeStyles, out result) || DateTime.TryParseExact(dateString, formats, invariantCulture, dateTimeStyles, out result))
		{
			return new ReleaseDate(result.Year, result.Month);
		}
		if (DateTime.TryParse(dateString, currentCulture, dateTimeStyles, out result) || DateTime.TryParse(dateString, invariantCulture, dateTimeStyles, out result))
		{
			return new ReleaseDate(result);
		}
		return null;
	}
}
