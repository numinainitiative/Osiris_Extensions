using System;
using System.Collections.Generic;

namespace XboxLibrary.Models;

public class TitleHistoryResponse
{
	public class Detail
	{
		public string description;

		public string publisherName;

		public string developerName;

		public DateTime? releaseDate;

		public int? minAge;
	}

	public class TitleHistory
	{
		public DateTime? lastTimePlayed;
	}

	public class Title
	{
		public string titleId;

		public string pfn;

		public string type;

		public string name;

		public string windowsPhoneProductId;

		public string modernTitleId;

		public string mediaItemType;

		public Detail detail;

		public List<string> devices;

		public TitleHistory titleHistory;

		public string minutesPlayed;

		public override string ToString()
		{
			return name;
		}
	}

	public string xuid;

	public List<Title> titles;
}
