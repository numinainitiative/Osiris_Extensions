using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Playnite.Common;

public class SafeFileEnumerator : IEnumerable<FileSystemInfo>, IEnumerable
{
	private class Enumerator : IEnumerator<FileSystemInfo>, IDisposable, IEnumerator
	{
		private IEnumerator<FileSystemInfo> fileEnumerator;

		private IEnumerator<DirectoryInfo> directoryEnumerator;

		private DirectoryInfo root;

		private string pattern;

		private SearchOption searchOption;

		private IList<Exception> errors;

		public FileSystemInfo Current => fileEnumerator.Current;

		object IEnumerator.Current => Current;

		public Enumerator(DirectoryInfo root, string pattern, SearchOption option, IList<Exception> errors)
		{
			this.root = root;
			this.pattern = pattern;
			this.errors = errors;
			searchOption = option;
			Reset();
		}

		public void Dispose()
		{
			Dispose(file: true, dir: true);
		}

		private void Dispose(bool file, bool dir)
		{
			if (file)
			{
				if (fileEnumerator != null)
				{
					fileEnumerator.Dispose();
				}
				fileEnumerator = null;
			}
			if (dir)
			{
				if (directoryEnumerator != null)
				{
					directoryEnumerator.Dispose();
				}
				directoryEnumerator = null;
			}
		}

		public bool MoveNext()
		{
			if (fileEnumerator != null && fileEnumerator.MoveNext())
			{
				return true;
			}
			if (searchOption == SearchOption.TopDirectoryOnly)
			{
				return false;
			}
			while (directoryEnumerator != null && directoryEnumerator.MoveNext())
			{
				Dispose(file: true, dir: false);
				try
				{
					fileEnumerator = new SafeFileEnumerator(directoryEnumerator.Current, pattern, SearchOption.AllDirectories, errors).GetEnumerator();
				}
				catch (Exception item)
				{
					errors.Add(item);
					continue;
				}
				if (fileEnumerator.MoveNext())
				{
					return true;
				}
			}
			Dispose(file: true, dir: true);
			return false;
		}

		public void Reset()
		{
			Dispose(file: true, dir: true);
			if (root != null)
			{
				try
				{
					fileEnumerator = root.GetFileSystemInfos(pattern, SearchOption.TopDirectoryOnly).AsEnumerable().GetEnumerator();
				}
				catch (Exception item)
				{
					errors.Add(item);
					fileEnumerator = null;
				}
				try
				{
					directoryEnumerator = root.GetDirectories("*", SearchOption.TopDirectoryOnly).AsEnumerable().GetEnumerator();
				}
				catch (Exception item2)
				{
					errors.Add(item2);
					directoryEnumerator = null;
				}
			}
		}
	}

	private DirectoryInfo root;

	private string pattern;

	private SearchOption searchOption;

	private IList<Exception> errors;

	public Exception[] Errors => errors.ToArray();

	public SafeFileEnumerator(string root, string pattern, SearchOption option)
		: this(new DirectoryInfo(root), pattern, option)
	{
	}

	public SafeFileEnumerator(DirectoryInfo root, string pattern, SearchOption option)
		: this(root, pattern, option, new List<Exception>())
	{
	}

	private SafeFileEnumerator(DirectoryInfo root, string pattern, SearchOption option, IList<Exception> errors)
	{
		if (root == null || !root.Exists)
		{
			throw new ArgumentException("Root directory is not set or does not exist.", "root");
		}
		this.root = root;
		searchOption = option;
		this.pattern = (string.IsNullOrEmpty(pattern) ? "*" : pattern);
		this.errors = errors;
	}

	public IEnumerator<FileSystemInfo> GetEnumerator()
	{
		return new Enumerator(root, pattern, searchOption, errors);
	}

	IEnumerator IEnumerable.GetEnumerator()
	{
		return GetEnumerator();
	}
}
