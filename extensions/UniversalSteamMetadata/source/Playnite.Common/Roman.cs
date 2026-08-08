using System.Collections.Generic;
using System.Text;

namespace Playnite.Common;

public static class Roman
{
	public static readonly Dictionary<char, int> RomanNumberDictionary;

	public static readonly Dictionary<int, string> NumberRomanDictionary;

	static Roman()
	{
		RomanNumberDictionary = new Dictionary<char, int>
		{
			{ 'I', 1 },
			{ 'V', 5 },
			{ 'X', 10 },
			{ 'L', 50 },
			{ 'C', 100 },
			{ 'D', 500 },
			{ 'M', 1000 }
		};
		NumberRomanDictionary = new Dictionary<int, string>
		{
			{ 1000, "M" },
			{ 900, "CM" },
			{ 500, "D" },
			{ 400, "CD" },
			{ 100, "C" },
			{ 90, "XC" },
			{ 50, "L" },
			{ 40, "XL" },
			{ 10, "X" },
			{ 9, "IX" },
			{ 5, "V" },
			{ 4, "IV" },
			{ 1, "I" }
		};
	}

	public static string To(int number)
	{
		StringBuilder stringBuilder = new StringBuilder();
		foreach (KeyValuePair<int, string> item in NumberRomanDictionary)
		{
			while (number >= item.Key)
			{
				stringBuilder.Append(item.Value);
				number -= item.Key;
			}
		}
		return stringBuilder.ToString();
	}

	public static int From(string roman)
	{
		int num = 0;
		int num2 = 0;
		char c = '\0';
		foreach (char c2 in roman)
		{
			num2 = ((c != 0) ? RomanNumberDictionary[c] : 0);
			int num3 = RomanNumberDictionary[c2];
			num = ((num2 == 0 || num3 <= num2) ? (num + num3) : (num - 2 * num2 + num3));
			c = c2;
		}
		return num;
	}
}
