using System.Collections.Generic;

namespace SteamLibrary.SteamShared;

public class SteamTagResponse
{
	public string version_hash { get; set; }

	public List<SteamTag> tags { get; set; } = new List<SteamTag>();
}
