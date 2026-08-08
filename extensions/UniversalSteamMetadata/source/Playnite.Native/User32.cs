using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace Playnite.Native;

public class User32
{
	private const string dllName = "User32.dll";

	[DllImport("User32.dll")]
	public static extern int GetDisplayConfigBufferSizes(QUERY_DEVICE_CONFIG_FLAGS flags, out uint numPathArrayElements, out uint numModeInfoArrayElements);

	[DllImport("User32.dll")]
	public static extern int QueryDisplayConfig(QUERY_DEVICE_CONFIG_FLAGS flags, ref uint numPathArrayElements, [Out] DISPLAYCONFIG_PATH_INFO[] PathInfoArray, ref uint numModeInfoArrayElements, [Out] DISPLAYCONFIG_MODE_INFO[] ModeInfoArray, IntPtr currentTopologyId);

	[DllImport("User32.dll")]
	public static extern int DisplayConfigGetDeviceInfo(ref DISPLAYCONFIG_TARGET_DEVICE_NAME deviceName);

	[DllImport("User32.dll")]
	public static extern int DisplayConfigGetDeviceInfo(ref DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO requestPacket);

	[DllImport("User32.dll")]
	public static extern int DisplayConfigSetDeviceInfo(ref DISPLAYCONFIG_DEVICE_INFO_HEADER setPacket);

	[DllImport("User32.dll")]
	public static extern IntPtr GetForegroundWindow();

	[DllImport("User32.dll")]
	public static extern uint GetWindowThreadProcessId(IntPtr hWnd, IntPtr ProcessId);

	[DllImport("User32.dll")]
	public static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

	[DllImport("User32.dll")]
	public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

	[DllImport("User32.dll")]
	public static extern int ShowCursor(bool bShow);

	[DllImport("User32.dll", EntryPoint = "SetWindowPos", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool _SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, SWP uFlags);

	public static void SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, SWP uFlags)
	{
		if (!_SetWindowPos(hWnd, hWndInsertAfter, x, y, cx, cy, uFlags))
		{
			throw new Win32Exception();
		}
	}

	[DllImport("user32")]
	public static extern IntPtr DefWindowProc([In] IntPtr hwnd, [In] int msg, [In] IntPtr wParam, [In] IntPtr lParam);

	[DllImport("User32.dll", CharSet = CharSet.Auto)]
	public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, StringBuilder lParam);

	[DllImport("User32.dll", CharSet = CharSet.Auto)]
	public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, [MarshalAs(UnmanagedType.LPWStr)] string lParam);

	[DllImport("User32.dll", CharSet = CharSet.Auto)]
	public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, int wParam, [MarshalAs(UnmanagedType.LPWStr)] string lParam);

	[DllImport("User32.dll", CharSet = CharSet.Auto)]
	public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, int wParam, ref IntPtr lParam);

	[DllImport("User32.dll", CharSet = CharSet.Auto)]
	public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, int wParam, IntPtr lParam);

	[DllImport("User32.dll", CharSet = CharSet.Auto)]
	public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, uint wParam, IntPtr lParam);

	[DllImport("Wintrust.dll")]
	public static extern uint WinVerifyTrust(IntPtr hWnd, IntPtr pgActionID, IntPtr pWinTrustData);

	[DllImport("User32.dll", CharSet = CharSet.Auto)]
	public static extern IntPtr FindWindow(string strClassName, string strWindowName);

	[DllImport("User32.dll")]
	public static extern IntPtr MonitorFromWindow(IntPtr hwnd, MonitorOptions dwFlags);

	[DllImport("User32.dll", EntryPoint = "GetMonitorInfo", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool _GetMonitorInfo(IntPtr hMonitor, [In][Out] MONITORINFO lpmi);

	[DllImport("User32.dll", SetLastError = true)]
	public static extern int DestroyIcon(IntPtr hIcon);

	public static MONITORINFO GetMonitorInfo(IntPtr hMonitor)
	{
		MONITORINFO mONITORINFO = new MONITORINFO();
		if (!_GetMonitorInfo(hMonitor, mONITORINFO))
		{
			throw new Win32Exception();
		}
		return mONITORINFO;
	}

	[DllImport("User32.dll", CharSet = CharSet.Auto, SetLastError = true)]
	public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

	[DllImport("User32.dll", CharSet = CharSet.Auto)]
	public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

	[DllImport("User32.dll", CharSet = CharSet.Auto, SetLastError = true)]
	public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

	[DllImport("User32.dll", CharSet = CharSet.Auto, SetLastError = true)]
	public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

	[DllImport("User32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool GetCursorPos(out POINT lpPoint);
}
