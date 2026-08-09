using System.Collections.Generic;

namespace XboxLibrary.Models;

public class UserStatsRequest
{
	public class Stats
	{
		public string name;

		public string titleid;
	}

	public string arrangebyfield;

	public List<Stats> stats;

	public List<string> xuids;
}
