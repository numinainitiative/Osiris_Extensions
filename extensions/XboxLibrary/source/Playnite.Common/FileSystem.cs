using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Playnite.Native;
using Playnite.SDK;

namespace Playnite.Common;

public static class FileSystem
{
	private static ILogger logger = LogManager.GetLogger();

	public static void CreateDirectory(string path)
	{
		CreateDirectory(path, clean: false);
	}

	public static void CreateDirectory(string path, bool clean)
	{
		string text = Paths.FixPathLength(path);
		if (string.IsNullOrEmpty(text))
		{
			return;
		}
		if (Directory.Exists(text))
		{
			if (!clean)
			{
				return;
			}
			DeleteDirectory(text, includeReadonly: true);
		}
		Directory.CreateDirectory(text);
	}

	public static void PrepareSaveFile(string path)
	{
		path = Paths.FixPathLength(path);
		CreateDirectory(Path.GetDirectoryName(path));
		if (File.Exists(path))
		{
			File.Delete(path);
		}
	}

	public static bool IsDirectoryEmpty(string path)
	{
		path = Paths.FixPathLength(path);
		if (Directory.Exists(path))
		{
			return !Directory.EnumerateFileSystemEntries(path).Any();
		}
		return true;
	}

	public static void DeleteFile(string path)
	{
		path = Paths.FixPathLength(path);
		if (File.Exists(path))
		{
			File.Delete(path);
		}
	}

	public static void CreateFile(string path)
	{
		path = Paths.FixPathLength(path);
		PrepareSaveFile(path);
		File.Create(path).Dispose();
	}

	public static void CopyFile(string sourcePath, string targetPath, bool overwrite = true)
	{
		sourcePath = Paths.FixPathLength(sourcePath);
		targetPath = Paths.FixPathLength(targetPath);
		logger.Debug("Copying file " + sourcePath + " to " + targetPath);
		PrepareSaveFile(targetPath);
		File.Copy(sourcePath, targetPath, overwrite);
	}

	public static void DeleteDirectory(string path)
	{
		path = Paths.FixPathLength(path, forcePrefix: true);
		if (Directory.Exists(path))
		{
			Directory.Delete(path, recursive: true);
		}
	}

	public static void DeleteDirectory(string path, bool includeReadonly)
	{
		path = Paths.FixPathLength(path);
		if (!Directory.Exists(path))
		{
			return;
		}
		if (includeReadonly)
		{
			string[] directories = Directory.GetDirectories(path);
			for (int i = 0; i < directories.Length; i++)
			{
				DeleteDirectory(directories[i], includeReadonly: true);
			}
			directories = Directory.GetFiles(path);
			for (int i = 0; i < directories.Length; i++)
			{
				string path2 = Paths.FixPathLength(directories[i]);
				FileAttributes attributes = File.GetAttributes(path2);
				if ((attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
				{
					File.SetAttributes(path2, attributes ^ FileAttributes.ReadOnly);
				}
				File.Delete(path2);
			}
			FileAttributes attributes2 = File.GetAttributes(path);
			if ((attributes2 & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
			{
				File.SetAttributes(path, attributes2 ^ FileAttributes.ReadOnly);
			}
			Directory.Delete(path, recursive: false);
		}
		else
		{
			DeleteDirectory(path);
		}
	}

	public static bool CanWriteToFolder(string folder)
	{
		folder = Paths.FixPathLength(folder);
		try
		{
			if (!Directory.Exists(folder))
			{
				Directory.CreateDirectory(folder);
			}
			using (File.Create(Path.Combine(folder, Path.GetRandomFileName()), 1, FileOptions.DeleteOnClose))
			{
			}
			return true;
		}
		catch
		{
			return false;
		}
	}

	public static string ReadFileAsStringSafe(string path, int retryAttempts = 5)
	{
		path = Paths.FixPathLength(path);
		IOException innerException = null;
		for (int i = 0; i < retryAttempts; i++)
		{
			try
			{
				return File.ReadAllText(path);
			}
			catch (IOException ex)
			{
				logger.Debug("Can't read from file, trying again. " + path);
				innerException = ex;
				Task.Delay(500).Wait();
			}
		}
		throw new IOException("Failed to read " + path, innerException);
	}

	public static byte[] ReadFileAsBytesSafe(string path, int retryAttempts = 5)
	{
		path = Paths.FixPathLength(path);
		IOException innerException = null;
		for (int i = 0; i < retryAttempts; i++)
		{
			try
			{
				return File.ReadAllBytes(path);
			}
			catch (IOException ex)
			{
				logger.Debug("Can't read from file, trying again. " + path);
				innerException = ex;
				Task.Delay(500).Wait();
			}
		}
		throw new IOException("Failed to read " + path, innerException);
	}

	public static Stream CreateWriteFileStreamSafe(string path, int retryAttempts = 5)
	{
		path = Paths.FixPathLength(path);
		IOException innerException = null;
		for (int i = 0; i < retryAttempts; i++)
		{
			try
			{
				return new FileStream(path, FileMode.Create, FileAccess.ReadWrite);
			}
			catch (IOException ex)
			{
				logger.Debug("Can't open write file stream, trying again. " + path);
				innerException = ex;
				Task.Delay(500).Wait();
			}
		}
		throw new IOException("Failed to read " + path, innerException);
	}

	public static Stream OpenReadFileStreamSafe(string path, int retryAttempts = 5)
	{
		path = Paths.FixPathLength(path);
		IOException innerException = null;
		for (int i = 0; i < retryAttempts; i++)
		{
			try
			{
				return new FileStream(path, FileMode.Open, FileAccess.Read);
			}
			catch (IOException ex)
			{
				logger.Debug("Can't open read file stream, trying again. " + path);
				innerException = ex;
				Task.Delay(500).Wait();
			}
		}
		throw new IOException("Failed to read " + path, innerException);
	}

	public static void WriteStringToFile(string path, string content)
	{
		path = Paths.FixPathLength(path);
		PrepareSaveFile(path);
		File.WriteAllText(path, content);
	}

	public static string ReadStringFromFile(string path)
	{
		path = Paths.FixPathLength(path);
		return File.ReadAllText(path);
	}

	public static void WriteStringToFileSafe(string path, string content, int retryAttempts = 5)
	{
		path = Paths.FixPathLength(path);
		IOException innerException = null;
		for (int i = 0; i < retryAttempts; i++)
		{
			try
			{
				PrepareSaveFile(path);
				File.WriteAllText(path, content);
				return;
			}
			catch (IOException ex)
			{
				logger.Debug("Can't write to a file, trying again. " + path);
				innerException = ex;
				Task.Delay(500).Wait();
			}
		}
		throw new IOException("Failed to write to " + path, innerException);
	}

	public static void DeleteFileSafe(string path, int retryAttempts = 5)
	{
		if (!File.Exists(path))
		{
			return;
		}
		IOException innerException = null;
		for (int i = 0; i < retryAttempts; i++)
		{
			try
			{
				File.Delete(path);
				return;
			}
			catch (IOException ex)
			{
				logger.Debug("Can't detele file, trying again. " + path);
				innerException = ex;
				Task.Delay(500).Wait();
			}
			catch (UnauthorizedAccessException exception)
			{
				logger.Error(exception, "Can't detele file, UnauthorizedAccessException. " + path);
				return;
			}
		}
		throw new IOException("Failed to delete " + path, innerException);
	}

	public static long GetFreeSpace(string drivePath)
	{
		string root = Path.GetPathRoot(drivePath);
		return DriveInfo.GetDrives().FirstOrDefault((DriveInfo a) => a.RootDirectory.FullName.Equals(root, StringComparison.OrdinalIgnoreCase))?.AvailableFreeSpace ?? 0;
	}

	public static long GetFileSize(string path)
	{
		path = Paths.FixPathLength(path);
		return GetFileSize(new FileInfo(path));
	}

	public static long GetFileSize(FileInfo fi)
	{
		return fi.Length;
	}

	public static long GetDirectorySize(string path, bool getSizeOnDisk)
	{
		return GetDirectorySize(new DirectoryInfo(Paths.FixPathLength(path)), getSizeOnDisk);
	}

	private static long GetDirectorySize(DirectoryInfo dirInfo, bool getSizeOnDisk)
	{
		long num = 0L;
		try
		{
			FileInfo[] files = dirInfo.GetFiles();
			foreach (FileInfo fileInfo in files)
			{
				num += (getSizeOnDisk ? GetFileSizeOnDisk(fileInfo) : GetFileSize(fileInfo));
			}
		}
		catch (DirectoryNotFoundException)
		{
			return num;
		}
		DirectoryInfo[] directories = dirInfo.GetDirectories();
		foreach (DirectoryInfo directoryInfo in directories)
		{
			if (IsDirectorySubdirSafeToRecurse(directoryInfo))
			{
				num += GetDirectorySize(directoryInfo.FullName, getSizeOnDisk);
			}
		}
		return num;
	}

	public static long GetFileSizeOnDisk(string path)
	{
		return GetFileSizeOnDisk(new FileInfo(Paths.FixPathLength(path)));
	}

	public static long GetFileSizeOnDisk(FileInfo fileInfo)
	{
		if (fileInfo.Length == 0L)
		{
			return 0L;
		}
		if (fileInfo.Directory == null)
		{
			return 0L;
		}
		if (Kernel32.GetDiskFreeSpaceW(fileInfo.Directory.Root.FullName, out var lpSectorsPerCluster, out var lpBytesPerSector, out var _, out var _) == 0)
		{
			throw new Win32Exception();
		}
		uint num = lpSectorsPerCluster * lpBytesPerSector;
		uint lpFileSizeHigh;
		uint compressedFileSizeW = Kernel32.GetCompressedFileSizeW(Paths.FixPathLength(fileInfo.FullName), out lpFileSizeHigh);
		int lastWin32Error = Marshal.GetLastWin32Error();
		if (compressedFileSizeW == uint.MaxValue && lastWin32Error != 0)
		{
			throw new Win32Exception(lastWin32Error);
		}
		return (long)((((ulong)lpFileSizeHigh << 32) | compressedFileSizeW) + num - 1) / (long)num * num;
	}

	private static bool IsDirectorySubdirSafeToRecurse(DirectoryInfo childDirectory)
	{
		if (childDirectory.Name.IsNullOrWhiteSpace())
		{
			return false;
		}
		return true;
	}

	public static void CopyDirectory(string sourceDirName, string destDirName, bool copySubDirs = true, bool overwrite = true)
	{
		sourceDirName = Paths.FixPathLength(sourceDirName);
		destDirName = Paths.FixPathLength(destDirName);
		DirectoryInfo directoryInfo = new DirectoryInfo(sourceDirName);
		if (!directoryInfo.Exists)
		{
			throw new DirectoryNotFoundException("Source directory does not exist or could not be found: " + sourceDirName);
		}
		DirectoryInfo[] directories = directoryInfo.GetDirectories();
		if (!Directory.Exists(destDirName))
		{
			Directory.CreateDirectory(destDirName);
		}
		FileInfo[] files = directoryInfo.GetFiles();
		foreach (FileInfo fileInfo in files)
		{
			string destFileName = Path.Combine(destDirName, fileInfo.Name);
			fileInfo.CopyTo(destFileName, overwrite);
		}
		if (copySubDirs)
		{
			DirectoryInfo[] array = directories;
			foreach (DirectoryInfo directoryInfo2 in array)
			{
				string destDirName2 = Path.Combine(destDirName, directoryInfo2.Name);
				CopyDirectory(directoryInfo2.FullName, destDirName2, copySubDirs);
			}
		}
	}

	public static bool FileExistsOnAnyDrive(string filePath, out string existringPath)
	{
		return PathExistsOnAnyDrive(filePath, (string path) => File.Exists(path), out existringPath);
	}

	public static bool DirectoryExistsOnAnyDrive(string directoryPath, out string existringPath)
	{
		return PathExistsOnAnyDrive(directoryPath, (string path) => Directory.Exists(path), out existringPath);
	}

	private static bool PathExistsOnAnyDrive(string originalPath, Predicate<string> predicate, out string existringPath)
	{
		originalPath = Paths.FixPathLength(originalPath);
		existringPath = null;
		try
		{
			if (predicate(originalPath))
			{
				existringPath = originalPath;
				return true;
			}
			if (!Paths.IsFullPath(originalPath))
			{
				return false;
			}
			Path.GetPathRoot(originalPath);
			foreach (DriveInfo item in from d in DriveInfo.GetDrives()
				where d.IsReady
				select d)
			{
				string path = originalPath.Substring(item.Name.Length);
				string text = Path.Combine(item.Name, path);
				if (predicate(text))
				{
					existringPath = text;
					return true;
				}
			}
		}
		catch (Exception exception) when (!Debugger.IsAttached)
		{
			logger.Error(exception, "Error checking if path exists on different drive \"" + originalPath + "\"");
		}
		return false;
	}

	public static bool DirectoryExists(string path)
	{
		return Directory.Exists(Paths.FixPathLength(path));
	}

	public static bool FileExists(string path)
	{
		return File.Exists(Paths.FixPathLength(path));
	}

	public static DateTime DirectoryGetLastWriteTime(string path)
	{
		return Directory.GetLastWriteTime(Paths.FixPathLength(path));
	}

	public static DateTime FileGetLastWriteTime(string path)
	{
		return File.GetLastWriteTime(Paths.FixPathLength(path));
	}

	public static void ReplaceStringInFile(string path, string oldValue, string newValue, Encoding encoding = null)
	{
		encoding = encoding ?? Encoding.UTF8;
		string text = File.ReadAllText(path, encoding);
		File.WriteAllText(path, text.Replace(oldValue, newValue), encoding);
	}
}
