using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Playnite.Native;
using Playnite.SDK;

namespace Playnite.Common;

public static class ProcessStarter
{
	private static ILogger logger = LogManager.GetLogger();

	public static int ShellExecute(string cmdLine)
	{
		logger.Debug("Executing shell command: " + cmdLine);
		STARTUPINFO lpStartupInfo = default(STARTUPINFO);
		PROCESS_INFORMATION lpProcessInformation = default(PROCESS_INFORMATION);
		SECURITY_ATTRIBUTES lpProcessAttributes = default(SECURITY_ATTRIBUTES);
		SECURITY_ATTRIBUTES lpThreadAttributes = default(SECURITY_ATTRIBUTES);
		lpProcessAttributes.nLength = Marshal.SizeOf(lpProcessAttributes);
		lpThreadAttributes.nLength = Marshal.SizeOf(lpThreadAttributes);
		try
		{
			if (Kernel32.CreateProcess(null, cmdLine, ref lpProcessAttributes, ref lpThreadAttributes, bInheritHandles: false, 32u, IntPtr.Zero, null, ref lpStartupInfo, out lpProcessInformation))
			{
				return lpProcessInformation.dwProcessId;
			}
			throw new Win32Exception(Marshal.GetLastWin32Error());
		}
		finally
		{
			if (lpProcessInformation.hProcess != IntPtr.Zero)
			{
				Kernel32.CloseHandle(lpProcessInformation.hProcess);
			}
			if (lpProcessInformation.hThread != IntPtr.Zero)
			{
				Kernel32.CloseHandle(lpProcessInformation.hThread);
			}
		}
	}

	public static Process StartUrl(string url)
	{
		logger.Debug("Opening URL: " + url);
		try
		{
			return Process.Start(url);
		}
		catch (Exception exception)
		{
			logger.Error(exception, "Failed to open URL.");
			return Process.Start("cmd", "/C start " + url);
		}
	}

	public static Process StartProcess(string path, bool asAdmin = false)
	{
		return StartProcess(path, string.Empty, string.Empty, asAdmin);
	}

	public static Process StartProcess(string path, string arguments, bool asAdmin = false)
	{
		return StartProcess(path, arguments, string.Empty, asAdmin);
	}

	public static Process StartProcess(string path, string arguments, string workDir, bool asAdmin = false)
	{
		logger.Debug($"Starting process: {path}, {arguments}, {workDir}, {asAdmin}");
		if (path.IsNullOrWhiteSpace())
		{
			throw new ArgumentNullException("Cannot start process, executable path is specified.");
		}
		string fileName = path;
		if (path.Contains(".."))
		{
			fileName = Path.GetFullPath(path);
		}
		ProcessStartInfo processStartInfo = new ProcessStartInfo(fileName)
		{
			Arguments = arguments,
			WorkingDirectory = (string.IsNullOrEmpty(workDir) ? new FileInfo(fileName).Directory.FullName : workDir)
		};
		if (asAdmin)
		{
			processStartInfo.Verb = "runas";
		}
		return Process.Start(processStartInfo);
	}

	public static int StartProcessWait(string path, string arguments, string workDir, bool noWindow = false)
	{
		logger.Debug("Starting process: " + path + ", " + arguments + ", " + workDir);
		if (path.IsNullOrWhiteSpace())
		{
			throw new ArgumentNullException("Cannot start process, executable path is specified.");
		}
		string fileName = path;
		if (path.Contains(".."))
		{
			fileName = Path.GetFullPath(path);
		}
		ProcessStartInfo processStartInfo = new ProcessStartInfo(fileName)
		{
			Arguments = arguments,
			WorkingDirectory = (string.IsNullOrEmpty(workDir) ? new FileInfo(fileName).Directory.FullName : workDir)
		};
		if (noWindow)
		{
			processStartInfo.CreateNoWindow = true;
			processStartInfo.UseShellExecute = false;
		}
		using Process process = Process.Start(processStartInfo);
		process.WaitForExit();
		return process.ExitCode;
	}

	public static int StartProcessWait(string path, string arguments, string workDir, out string stdOutput, out string stdError)
	{
		logger.Debug("Starting process: " + path + ", " + arguments + ", " + workDir);
		if (path.IsNullOrWhiteSpace())
		{
			throw new ArgumentNullException("Cannot start process, executable path is specified.");
		}
		string fileName = path;
		if (path.Contains(".."))
		{
			fileName = Path.GetFullPath(path);
		}
		ProcessStartInfo startInfo = new ProcessStartInfo(fileName)
		{
			Arguments = arguments,
			WorkingDirectory = (string.IsNullOrEmpty(workDir) ? new FileInfo(fileName).Directory.FullName : workDir),
			RedirectStandardError = true,
			RedirectStandardOutput = true,
			CreateNoWindow = true,
			UseShellExecute = false
		};
		string stdout = string.Empty;
		string stderr = string.Empty;
		using Process process = new Process();
		process.StartInfo = startInfo;
		process.OutputDataReceived += delegate(object _, DataReceivedEventArgs e)
		{
			if (e.Data != null)
			{
				stdout = stdout + e.Data + Environment.NewLine;
			}
		};
		process.ErrorDataReceived += delegate(object _, DataReceivedEventArgs e)
		{
			if (e.Data != null)
			{
				stderr = stderr + e.Data + Environment.NewLine;
			}
		};
		process.Start();
		process.BeginOutputReadLine();
		process.BeginErrorReadLine();
		process.WaitForExit();
		stdOutput = stdout;
		stdError = stderr;
		return process.ExitCode;
	}
}
