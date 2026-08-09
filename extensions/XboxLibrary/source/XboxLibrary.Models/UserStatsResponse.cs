using System.Collections.Generic;

namespace XboxLibrary.Models;

public class UserStatsResponse
{
	public class Stats
	{
		public string titleid;

		public string value;
	}

	public class StatListsCollection
	{
		public List<Stats> stats;
	}

	public List<StatListsCollection> statlistscollection;
}
