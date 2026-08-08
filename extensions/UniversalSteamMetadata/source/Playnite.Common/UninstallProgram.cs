namespace Playnite.Common;

public class UninstallProgram
{
	public string DisplayIcon { get; set; }

	public string DisplayName { get; set; }

	public string DisplayVersion { get; set; }

	public string InstallLocation { get; set; }

	public string Publisher { get; set; }

	public string UninstallString { get; set; }

	public string URLInfoAbout { get; set; }

	public string RegistryKeyName { get; set; }

	public string Path { get; set; }

	public override string ToString()
	{
		return DisplayName ?? RegistryKeyName;
	}
}
