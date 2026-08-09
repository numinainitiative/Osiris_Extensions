namespace Playnite.Native;

public static class Windef
{
	internal static int LOWORD(int i)
	{
		return (short)(i & 0xFFFF);
	}
}
