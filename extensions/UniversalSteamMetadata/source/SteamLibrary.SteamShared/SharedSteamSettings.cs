using System.Collections.Generic;
using System.Collections.ObjectModel;
using Playnite.SDK.Models;
using Steam;

namespace SteamLibrary.SteamShared;

public abstract class SharedSteamSettings : ObservableObject
{
	private string languageKey = "english";

	private bool limitTagsToFixedAmount;

	private int fixedTagCount = 5;

	private bool useTagPrefix;

	private bool setTagCategoryAsPrefix;

	private string tagPrefix = string.Empty;

	public bool LimitTagsToFixedAmount
	{
		get
		{
			return limitTagsToFixedAmount;
		}
		set
		{
			SetValue(ref limitTagsToFixedAmount, value, "LimitTagsToFixedAmount");
		}
	}

	public int FixedTagCount
	{
		get
		{
			return fixedTagCount;
		}
		set
		{
			SetValue(ref fixedTagCount, value, "FixedTagCount");
		}
	}

	public bool UseTagPrefix
	{
		get
		{
			return useTagPrefix;
		}
		set
		{
			SetValue(ref useTagPrefix, value, "UseTagPrefix");
		}
	}

	public bool SetTagCategoryAsPrefix
	{
		get
		{
			return setTagCategoryAsPrefix;
		}
		set
		{
			SetValue(ref setTagCategoryAsPrefix, value, "SetTagCategoryAsPrefix");
		}
	}

	public string TagPrefix
	{
		get
		{
			return tagPrefix;
		}
		set
		{
			SetValue(ref tagPrefix, value, "TagPrefix");
		}
	}

	public string LanguageKey
	{
		get
		{
			return languageKey;
		}
		set
		{
			SetValue(ref languageKey, value, "LanguageKey");
		}
	}

	public bool DownloadVerticalCovers { get; set; } = true;

	public BackgroundSource BackgroundSource { get; set; }

	public ObservableCollection<int> BlacklistedTags { get; set; } = new ObservableCollection<int>();

	public GameField SteamDeckCompatibilityField { get; set; } = GameField.None;
}
