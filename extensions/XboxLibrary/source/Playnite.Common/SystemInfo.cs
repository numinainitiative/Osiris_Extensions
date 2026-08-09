using System.Collections.Generic;

namespace Playnite.Common;

public class SystemInfo
{
	public bool Is64Bit { get; set; }

	public string WindowsVersion { get; set; }

	public string ActualWindowsVersion { get; set; }

	public string WindowsEdition { get; set; }

	public int WindowsBuildVersion { get; set; }

	public string Cpu { get; set; }

	public int Ram { get; set; }

	public List<string> Gpus { get; set; }

	public List<ComputerScreen> Screens { get; set; }
}
