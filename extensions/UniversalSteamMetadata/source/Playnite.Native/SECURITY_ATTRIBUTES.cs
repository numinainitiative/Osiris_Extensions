using System;

namespace Playnite.Native;

public struct SECURITY_ATTRIBUTES
{
	public int nLength;

	public IntPtr lpSecurityDescriptor;

	public int bInheritHandle;
}
