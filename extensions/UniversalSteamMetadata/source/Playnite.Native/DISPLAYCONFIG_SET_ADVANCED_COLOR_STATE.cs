namespace Playnite.Native;

public struct DISPLAYCONFIG_SET_ADVANCED_COLOR_STATE
{
	public DISPLAYCONFIG_DEVICE_INFO_HEADER header;

	public uint value;

	public bool enableAdvancedColor
	{
		get
		{
			return (value & 1) == 1;
		}
		set
		{
			uint num = 1u;
			if (value)
			{
				this.value |= num;
			}
			else
			{
				this.value &= ~num;
			}
		}
	}
}
