namespace Playnite.Native;

public struct DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO
{
	public DISPLAYCONFIG_DEVICE_INFO_HEADER header;

	public uint value;

	public DISPLAYCONFIG_COLOR_ENCODING colorEncoding;

	public uint bitsPerColorChannel;

	public bool advancedColorSupported => (value & 1) == 1;

	public bool advancedColorEnabled => (value & 2) == 2;
}
