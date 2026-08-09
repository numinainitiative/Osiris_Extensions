using System.Collections.Generic;

namespace XboxLibrary.Models;

public class ProfileRequest
{
	public List<string> settings;

	public List<ulong> userIds;
}
