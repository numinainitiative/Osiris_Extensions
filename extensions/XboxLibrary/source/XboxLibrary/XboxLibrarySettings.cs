namespace XboxLibrary;

public class XboxLibrarySettings
{
	public bool ConnectAccount { get; set; }

	public bool ImportInstalledGames { get; set; } = true;

	public bool ImportUninstalledGames { get; set; }

	public bool XboxAppClientPriorityLaunch { get; set; }

	public bool ImportConsoleGames { get; set; }
}
