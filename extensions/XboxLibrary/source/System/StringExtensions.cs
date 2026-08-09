using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;

namespace System;

public static class StringExtensions
{
	private static readonly CultureInfo enUSCultInfo = new CultureInfo("en-US", useUserOverride: false);

	public static string MD5(this string s)
	{
		StringBuilder stringBuilder = new StringBuilder();
		byte[] array = s.MD5Bytes();
		foreach (byte b in array)
		{
			stringBuilder.Append(b.ToString("x2").ToLower());
		}
		return stringBuilder.ToString();
	}

	public static byte[] MD5Bytes(this string s)
	{
		using MD5 mD = System.Security.Cryptography.MD5.Create();
		return mD.ComputeHash(Encoding.UTF8.GetBytes(s));
	}

	public static string RemoveTrademarks(this string str, string remplacement = "")
	{
		if (str.IsNullOrEmpty())
		{
			return str;
		}
		return Regex.Replace(str, "[™©®]", remplacement);
	}

	public static bool IsNullOrEmpty(this string source)
	{
		return string.IsNullOrEmpty(source);
	}

	public static bool IsNullOrWhiteSpace(this string source)
	{
		return string.IsNullOrWhiteSpace(source);
	}

	public static string Format(this string source, params object[] args)
	{
		return string.Format(source, args);
	}

	public static string TrimEndString(this string source, string value, StringComparison comp = StringComparison.Ordinal)
	{
		if (!source.EndsWith(value, comp))
		{
			return source;
		}
		return source.Remove(source.LastIndexOf(value, comp));
	}

	public static string ToTileCase(this string source, CultureInfo culture = null)
	{
		if (source.IsNullOrEmpty())
		{
			return source;
		}
		if (culture != null)
		{
			return culture.TextInfo.ToTitleCase(source);
		}
		return enUSCultInfo.TextInfo.ToTitleCase(source);
	}

	public static bool IsStartOfStringAcronym(this string acronymStart, string input)
	{
		if (string.IsNullOrEmpty(input) || string.IsNullOrEmpty(acronymStart) || acronymStart.Length < 2 || acronymStart.Length > input.Length)
		{
			return false;
		}
		for (int i = 0; i < acronymStart.Length; i++)
		{
			if (!char.IsLetterOrDigit(acronymStart[i]))
			{
				return false;
			}
		}
		int num = 0;
		for (int j = 0; j < input.Length; j++)
		{
			if (char.IsLetterOrDigit(input[j]) && (j == 0 || input[j - 1] == ' '))
			{
				if (char.ToUpperInvariant(input[j]) != char.ToUpperInvariant(acronymStart[num]))
				{
					return false;
				}
				num++;
				if (num == acronymStart.Length)
				{
					return true;
				}
			}
		}
		return false;
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

	public static string NormalizeGameName(this string name)
	{
		if (string.IsNullOrEmpty(name))
		{
			return string.Empty;
		}
		string str = name;
		str = str.RemoveTrademarks();
		str = str.Replace("_", " ");
		str = str.Replace(".", " ");
		str = str.Replace('’', '\'');
		str = RemoveUnlessThatEmptiesTheString(str, "\\[.*?\\]");
		str = RemoveUnlessThatEmptiesTheString(str, "\\(.*?\\)");
		str = Regex.Replace(str, "\\s*:\\s*", ": ");
		str = Regex.Replace(str, "\\s+", " ");
		if (Regex.IsMatch(str, ",\\s*The$"))
		{
			str = "The " + Regex.Replace(str, ",\\s*The$", "", RegexOptions.IgnoreCase);
		}
		return str.Trim();
	}

	public static string GetSHA256Hash(this string input)
	{
		using SHA256 sHA = SHA256.Create();
		return BitConverter.ToString(sHA.ComputeHash(Encoding.UTF8.GetBytes(input))).Replace("-", "");
	}

	public static string GetPathWithoutAllExtensions(string path)
	{
		if (string.IsNullOrEmpty(path))
		{
			return string.Empty;
		}
		return Regex.Replace(path, "(\\.[A-Za-z0-9]+)+$", "");
	}

	public static bool Contains(this string str, string value, StringComparison comparisonType)
	{
		if (str == null)
		{
			return true;
		}
		return str.IndexOf(value, 0, comparisonType) != -1;
	}

	public static bool ContainsAny(this string str, char[] chars)
	{
		if (str == null)
		{
			return false;
		}
		return str.IndexOfAny(chars) >= 0;
	}

	public static bool IsHttpUrl(this string str)
	{
		if (string.IsNullOrWhiteSpace(str))
		{
			return false;
		}
		return Regex.IsMatch(str, "^https?:\\/\\/", RegexOptions.IgnoreCase);
	}

	public static bool IsUri(this string str)
	{
		if (string.IsNullOrWhiteSpace(str))
		{
			return false;
		}
		return Uri.IsWellFormedUriString(str, UriKind.Absolute);
	}

	public static string UrlEncode(this string str)
	{
		if (string.IsNullOrWhiteSpace(str))
		{
			return str;
		}
		return HttpUtility.UrlPathEncode(str);
	}

	public static string UrlDecode(this string str)
	{
		if (string.IsNullOrWhiteSpace(str))
		{
			return str;
		}
		return HttpUtility.UrlDecode(str);
	}

	public static int GetLineCount(this string str)
	{
		if (str == null)
		{
			return 0;
		}
		return Regex.Matches(str, "\n").Count + 1;
	}

	public static string Replace(this string str, string oldValue, string newValue, StringComparison comparisonType)
	{
		if (str == null)
		{
			throw new ArgumentNullException("str");
		}
		if (str.Length == 0)
		{
			return str;
		}
		if (oldValue == null)
		{
			throw new ArgumentNullException("oldValue");
		}
		if (oldValue.Length == 0)
		{
			throw new ArgumentException("String cannot be of zero length.");
		}
		StringBuilder stringBuilder = new StringBuilder(str.Length);
		bool flag = string.IsNullOrEmpty(newValue);
		int num = 0;
		int num2;
		while ((num2 = str.IndexOf(oldValue, num, comparisonType)) != -1)
		{
			int num3 = num2 - num;
			if (num3 != 0)
			{
				stringBuilder.Append(str, num, num3);
			}
			if (!flag)
			{
				stringBuilder.Append(newValue);
			}
			num = num2 + oldValue.Length;
			if (num == str.Length)
			{
				return stringBuilder.ToString();
			}
		}
		int count = str.Length - num;
		stringBuilder.Append(str, num, count);
		return stringBuilder.ToString();
	}

	public static string EndWithDirSeparator(this string source)
	{
		if (source.IsNullOrEmpty())
		{
			return source;
		}
		return source.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
	}

	public static bool ContainsInvariantCulture(this string source, string value, CompareOptions compareOptions)
	{
		return CultureInfo.InvariantCulture.CompareInfo.IndexOf(source, value, compareOptions) >= 0;
	}

	public static bool ContainsCurrentCulture(this string source, string value, CompareOptions compareOptions)
	{
		return CultureInfo.CurrentCulture.CompareInfo.IndexOf(source, value, compareOptions) >= 0;
	}
}
