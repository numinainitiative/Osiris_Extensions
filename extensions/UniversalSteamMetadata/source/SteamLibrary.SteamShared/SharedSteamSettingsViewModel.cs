using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using Playnite.Common.Web;
using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;

namespace SteamLibrary.SteamShared;

public abstract class SharedSteamSettingsViewModel<TSettings, TPlugin> : PluginSettingsViewModel<TSettings, TPlugin> where TSettings : SharedSteamSettings, new() where TPlugin : Plugin
{
	public class TagInfo
	{
		public int Id { get; set; }

		public string Name { get; set; }

		public TagInfo(int id, string name)
		{
			Id = id;
			Name = name;
		}
	}

	public class NamedField
	{
		public string Name { get; set; }

		public GameField Field { get; set; }

		public NamedField(string name, GameField field)
		{
			Name = name;
			Field = field;
		}
	}

	private ObservableCollection<TagInfo> okayTags;

	private ObservableCollection<TagInfo> blacklistedTags;

	private string fixedTagCountString;

	internal readonly string ApiKeysPath;

	public Dictionary<string, string> Languages { get; } = new Dictionary<string, string>
	{
		{ "schinese", "简体中文 (Simplified Chinese)" },
		{ "tchinese", "繁體中文 (Traditional Chinese)" },
		{ "japanese", "日本語 (Japanese)" },
		{ "koreana", "한국어 (Korean)" },
		{ "thai", "ไทย (Thai)" },
		{ "bulgarian", "Български (Bulgarian)" },
		{ "czech", "Čeština (Czech)" },
		{ "danish", "Dansk (Danish)" },
		{ "german", "Deutsch (German)" },
		{ "english", "English" },
		{ "spanish", "Español - España (Spanish - Spain)" },
		{ "latam", "Español - Latinoamérica (Spanish - Latin America)" },
		{ "greek", "Ελληνικά (Greek)" },
		{ "french", "Français (French)" },
		{ "indonesian", "Bahasa Indonesia (Indonesian)" },
		{ "italian", "Italiano (Italian)" },
		{ "hungarian", "Magyar (Hungarian)" },
		{ "dutch", "Nederlands (Dutch)" },
		{ "norwegian", "Norsk (Norwegian)" },
		{ "polish", "Polski (Polish)" },
		{ "portuguese", "Português (Portuguese)" },
		{ "brazilian", "Português - Brasil (Portuguese - Brazil)" },
		{ "romanian", "Română (Romanian)" },
		{ "russian", "Русский (Russian)" },
		{ "finnish", "Suomi (Finnish)" },
		{ "swedish", "Svenska (Swedish)" },
		{ "turkish", "Türkçe (Turkish)" },
		{ "vietnamese", "Tiếng Việt (Vietnamese)" },
		{ "ukrainian", "Українська (Ukrainian)" }
	};

	public ObservableCollection<TagInfo> OkayTags
	{
		get
		{
			return okayTags;
		}
		set
		{
			SetValue(ref okayTags, value, "OkayTags");
		}
	}

	public ObservableCollection<TagInfo> BlacklistedTags
	{
		get
		{
			return blacklistedTags;
		}
		set
		{
			SetValue(ref blacklistedTags, value, "BlacklistedTags");
		}
	}

	public string FixedTagCountString
	{
		get
		{
			return fixedTagCountString;
		}
		set
		{
			SetValue(ref fixedTagCountString, value, "FixedTagCountString");
		}
	}

	public RelayCommand<IList<object>> WhitelistCommand => new RelayCommand<IList<object>>(delegate(IList<object> selectedItems)
	{
		foreach (TagInfo item in selectedItems.Cast<TagInfo>().ToList())
		{
			BlacklistedTags.Remove(item);
			OkayTags.Add(item);
			base.Settings.BlacklistedTags.Remove(item.Id);
		}
	}, (IList<object> a) => a != null && a.Count > 0);

	public RelayCommand<IList<object>> BlacklistCommand => new RelayCommand<IList<object>>(delegate(IList<object> selectedItems)
	{
		foreach (TagInfo item in selectedItems.Cast<TagInfo>().ToList())
		{
			OkayTags.Remove(item);
			BlacklistedTags.Add(item);
			base.Settings.BlacklistedTags.Add(item.Id);
		}
	}, (IList<object> a) => a != null && a.Count > 0);

	public List<NamedField> SteamDeckCompatibilityFieldOptions { get; set; }

	protected SharedSteamSettingsViewModel(TPlugin plugin, IPlayniteAPI playniteApi)
		: base(plugin, playniteApi)
	{
		ApiKeysPath = Path.Combine(plugin.GetPluginUserDataPath(), "keys.dat");
		TSettings val = LoadSavedSettings();
		if (val != null)
		{
			base.Settings = val;
			OnLoadSettings();
		}
		else
		{
			base.Settings = new TSettings
			{
				LanguageKey = GetSteamLanguageForCurrentPlayniteLanguage()
			};
			OnInitSettings();
		}
		InitializeSteamDeckCompatibilitySettings();
		InitializeTagNames();
		base.Settings.PropertyChanged += delegate(object sender, PropertyChangedEventArgs ev)
		{
			if (ev.PropertyName == "LanguageKey" || ev.PropertyName == "UseTagPrefix" || ev.PropertyName == "TagPrefix" || ev.PropertyName == "SetTagCategoryAsPrefix")
			{
				InitializeTagNames();
			}
		};
		FixedTagCountString = base.Settings.FixedTagCount.ToString();
	}

	private string GetSteamLanguageForCurrentPlayniteLanguage()
	{
		switch (base.PlayniteApi.ApplicationSettings.Language)
		{
		case "cs_CZ":
			return "czech";
		case "da_DK":
			return "danish";
		case "de_DE":
			return "german";
		case "el_GR":
			return "greek";
		case "es_ES":
			return "spanish";
		case "fi_FI":
			return "finnish";
		case "fr_FR":
			return "french";
		case "hu_HU":
			return "hungarian";
		case "it_IT":
			return "italian";
		case "ja_JP":
			return "japanese";
		case "ko_KR":
			return "korean";
		case "nl_NL":
			return "dutch";
		case "no_NO":
			return "norwegian";
		case "pl_PL":
			return "polish";
		case "pt_BR":
			return "brazilian";
		case "pt_PT":
			return "portuguese";
		case "ro_RO":
			return "romanian";
		case "ru_RU":
			return "russian";
		case "sv_SE":
			return "swedish";
		case "tr_TR":
			return "turkish";
		case "uk_UA":
			return "ukrainian";
		case "vi_VN":
			return "vietnamese";
		case "zh_CN":
		case "zh_TW":
			return "schinese";
		default:
			return "english";
		}
	}

	protected virtual void OnLoadSettings()
	{
	}

	protected virtual void OnInitSettings()
	{
	}

	private void InitializeTagNames()
	{
		SteamTagNamer tagNamer = new SteamTagNamer(base.Plugin, base.Settings, new Downloader());
		List<TagInfo> source = (from t in tagNamer.GetTagNames()
			select new TagInfo(t.Key, tagNamer.GetFinalTagName(t.Value, t.Key)) into t
			orderby t.Name
			select t).ToList();
		OkayTags = source.Where((TagInfo t) => !base.Settings.BlacklistedTags.Contains(t.Id)).ToObservable();
		BlacklistedTags = source.Where((TagInfo t) => base.Settings.BlacklistedTags.Contains(t.Id)).ToObservable();
	}

	public override bool VerifySettings(out List<string> errors)
	{
		if (base.Settings.LimitTagsToFixedAmount && (!int.TryParse(FixedTagCountString, out var result) || result < 0))
		{
			errors = new List<string> { base.PlayniteApi.Resources.GetString("LOCSteamValidationFixedTagCount") };
			return false;
		}
		return base.VerifySettings(out errors);
	}

	public override void EndEdit()
	{
		if (int.TryParse(FixedTagCountString, out var result) && result >= 0)
		{
			base.Settings.FixedTagCount = result;
		}
		base.EndEdit();
	}

	public void InitializeSteamDeckCompatibilitySettings()
	{
		SteamDeckCompatibilityFieldOptions = new List<NamedField>
		{
			new NamedField(base.PlayniteApi.Resources.GetString("LOCNone"), GameField.None),
			new NamedField(base.PlayniteApi.Resources.GetString("LOCFeatureLabel"), GameField.Features),
			new NamedField(base.PlayniteApi.Resources.GetString("LOCTagLabel"), GameField.Tags)
		};
	}
}
