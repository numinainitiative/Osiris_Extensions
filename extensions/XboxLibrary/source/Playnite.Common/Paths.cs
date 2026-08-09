using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Playnite.Native;

namespace Playnite.Common;

public class Paths
{
	private const string longPathPrefix = "\\\\?\\";

	private const string longPathUncPrefix = "\\\\?\\UNC\\";

	public static readonly char[] DirectorySeparators = new char[2] { '\\', '/' };

	public static string GetFinalPathName(string path)
	{
		IntPtr intPtr = Kernel32.CreateFile(path, 0u, FileShare.ReadWrite | FileShare.Delete, IntPtr.Zero, FileMode.Open, 33554432u, IntPtr.Zero);
		if (path.StartsWith("\\\\"))
		{
			return path;
		}
		if (intPtr == Winuser.INVALID_HANDLE_VALUE)
		{
			throw new Win32Exception();
		}
		try
		{
			StringBuilder stringBuilder = new StringBuilder(1024);
			if (Kernel32.GetFinalPathNameByHandle(intPtr, stringBuilder, 1024u, 0u) == 0)
			{
				throw new Win32Exception();
			}
			string text = stringBuilder.ToString();
			if (text.StartsWith("\\\\?\\UNC\\"))
			{
				return text.Replace("\\\\?\\UNC\\", "\\\\");
			}
			return text.Replace("\\\\?\\", string.Empty);
		}
		finally
		{
			Kernel32.CloseHandle(intPtr);
		}
	}

	public static bool IsValidFilePath(string path)
	{
		try
		{
			if (string.IsNullOrEmpty(path))
			{
				return false;
			}
			if (string.IsNullOrEmpty(Path.GetExtension(path)))
			{
				return false;
			}
			string pathRoot = Path.GetPathRoot(path);
			if (!string.IsNullOrEmpty(pathRoot) && !Directory.Exists(pathRoot))
			{
				return false;
			}
			return true;
		}
		catch
		{
			return false;
		}
	}

	public static string FixSeparators(string path)
	{
		if (path.IsNullOrWhiteSpace())
		{
			return path;
		}
		char c = '\0';
		StringBuilder stringBuilder = new StringBuilder(path.Length);
		for (int i = 0; i < path.Length; i++)
		{
			char c2 = path[i];
			if (c2 == Path.AltDirectorySeparatorChar)
			{
				c2 = Path.DirectorySeparatorChar;
			}
			if (c != c2 || c2 != Path.DirectorySeparatorChar || (c2 == Path.DirectorySeparatorChar && c != Path.DirectorySeparatorChar))
			{
				c = c2;
				stringBuilder.Append(c2);
			}
		}
		if (path.StartsWith("\\\\"))
		{
			stringBuilder.Insert(0, "\\");
		}
		return stringBuilder.ToString();
	}

	private static string Normalize(string path)
	{
		string path2 = path;
		try
		{
			path2 = new Uri(path).LocalPath;
		}
		catch
		{
		}
		return Path.GetFullPath(path2).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).ToUpperInvariant();
	}

	public static bool AreEqual(string path1, string path2)
	{
		if (string.IsNullOrEmpty(path1) && !string.IsNullOrEmpty(path2))
		{
			return false;
		}
		if (!string.IsNullOrEmpty(path1) && string.IsNullOrEmpty(path2))
		{
			return false;
		}
		if (string.IsNullOrEmpty(path1) && string.IsNullOrEmpty(path2))
		{
			return false;
		}
		try
		{
			return Normalize(path1) == Normalize(path2);
		}
		catch
		{
			return false;
		}
	}

	public static string GetSafePathName(string filename)
	{
		return Regex.Replace(string.Join(" ", filename.Split(Path.GetInvalidFileNameChars())), "\\s+", " ").Trim();
	}

	public static bool IsFullPath(string path)
	{
		if (string.IsNullOrWhiteSpace(path))
		{
			return false;
		}
		return Regex.IsMatch(path, "^([a-zA-Z]:\\\\|\\\\\\\\)");
	}

	public static string GetCommonDirectory(string[] paths)
	{
		int num = paths[0].Length;
		for (int i = 1; i < paths.Length; i++)
		{
			num = Math.Min(num, paths[i].Length);
			for (int j = 0; j < num; j++)
			{
				if (paths[i][j] != paths[0][j])
				{
					num = j;
					break;
				}
			}
		}
		string text = paths[0].Substring(0, num);
		if (text.Length == 0)
		{
			return string.Empty;
		}
		if (text[text.Length - 1] == Path.DirectorySeparatorChar)
		{
			return text;
		}
		return text.Substring(0, text.LastIndexOf(Path.DirectorySeparatorChar) + 1);
	}

	public static bool MathcesFilePattern(string filePath, string pattern)
	{
		if (filePath.IsNullOrEmpty() || pattern.IsNullOrEmpty())
		{
			return false;
		}
		if (pattern.Contains(';'))
		{
			return Shlwapi.PathMatchSpecExW(filePath, pattern, MatchPatternFlags.Multiple) == 0;
		}
		return Shlwapi.PathMatchSpecExW(filePath, pattern, MatchPatternFlags.Normal) == 0;
	}

	public static string FixPathLength(string path, bool forcePrefix = false)
	{
		if (path.IsNullOrWhiteSpace())
		{
			return path;
		}
		if (!IsFullPath(path))
		{
			return path;
		}
		if ((path.Length >= 258 || forcePrefix) && !path.StartsWith("\\\\?\\"))
		{
			if (path.StartsWith("\\\\"))
			{
				return "\\\\?\\UNC\\" + path.Substring(2);
			}
			return "\\\\?\\" + path;
		}
		return path;
	}

	public static string TrimLongPathPrefix(string path)
	{
		if (path.IsNullOrWhiteSpace())
		{
			return path;
		}
		if (path.StartsWith("\\\\?\\UNC\\"))
		{
			return path.Replace("\\\\?\\UNC\\", "\\\\");
		}
		return path.Replace("\\\\?\\", string.Empty);
	}
}
