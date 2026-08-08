using System;
using System.Text;
using System.Text.RegularExpressions;

namespace PlayniteExtensions.Common;

public class GameNameMatcher
{
	public static string ToAlphanumericLower(string str)
	{
		if (string.IsNullOrEmpty(str))
		{
			return string.Empty;
		}
		StringBuilder stringBuilder = new StringBuilder(str.Length);
		foreach (char c in str)
		{
			if (char.IsLetterOrDigit(c))
			{
				stringBuilder.Append(char.ToLowerInvariant(c));
			}
		}
		return stringBuilder.ToString();
	}

	public static string ToGameKey(string str)
	{
		if (string.IsNullOrEmpty(str))
		{
			return string.Empty;
		}
		string str2 = str;
		str2 = RemoveTrademarks(str2);
		str2 = RemoveUnlessThatEmptiesTheString(str2, "\\[.*?\\]");
		str2 = RemoveUnlessThatEmptiesTheString(str2, "\\(.*?\\)");
		string text = str2.TrimEnd();
		if (text.EndsWith(", The", StringComparison.OrdinalIgnoreCase))
		{
			str2 = "The " + text.Substring(0, text.Length - 5);
		}
		else
		{
			int num = text.IndexOf(", The:", StringComparison.OrdinalIgnoreCase);
			if (num >= 0)
			{
				string text2 = text.Substring(0, num);
				string text3 = text.Substring(num + ", The:".Length);
				str2 = "The " + text2 + ":" + text3;
			}
		}
		StringBuilder stringBuilder = new StringBuilder(str2.Length);
		string text4 = str2;
		foreach (char c in text4)
		{
			if (char.IsLetterOrDigit(c))
			{
				stringBuilder.Append(char.ToLowerInvariant(c));
			}
		}
		return stringBuilder.ToString();
	}

	private static string RemoveTrademarks(string str)
	{
		if (string.IsNullOrEmpty(str))
		{
			return str;
		}
		if (str.IndexOfAny(new char[3] { '™', '©', '®' }) < 0)
		{
			return str;
		}
		StringBuilder stringBuilder = new StringBuilder(str.Length);
		foreach (char c in str)
		{
			if (c != '©' && c != '®' && c != '™')
			{
				stringBuilder.Append(c);
			}
		}
		return stringBuilder.ToString();
	}

	private static string RemoveUnlessThatEmptiesTheString(string input, string pattern)
	{
		string text = Regex.Replace(input, pattern, string.Empty);
		if (string.IsNullOrWhiteSpace(text))
		{
			return input;
		}
		return text;
	}
}
