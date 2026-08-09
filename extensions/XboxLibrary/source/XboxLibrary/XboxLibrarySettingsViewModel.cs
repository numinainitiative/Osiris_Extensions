using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Playnite.SDK;
using XboxLibrary.Services;

namespace XboxLibrary;

public class XboxLibrarySettingsViewModel : PluginSettingsViewModel<XboxLibrarySettings, XboxLibrary>
{
	public bool IsFirstRunUse { get; set; }

	public bool IsUserLoggedIn => new XboxAccountClient(base.Plugin).GetIsUserLoggedIn().GetAwaiter().GetResult();

	public RelayCommand<object> LoginCommand => new RelayCommand<object>(async delegate
	{
		await Login();
	});

	public XboxLibrarySettingsViewModel(XboxLibrary plugin, IPlayniteAPI api)
		: base(plugin, api)
	{
		XboxLibrarySettings xboxLibrarySettings = LoadSavedSettings();
		if (xboxLibrarySettings != null)
		{
			base.Settings = xboxLibrarySettings;
		}
		else
		{
			base.Settings = new XboxLibrarySettings();
		}
	}

	private async Task Login()
	{
		try
		{
			await new XboxAccountClient(base.Plugin).Login();
			OnPropertyChanged("IsUserLoggedIn");
		}
		catch (Exception exception) when (!Debugger.IsAttached)
		{
			Logger.Error(exception, "Failed to authenticate user.");
		}
	}
}
