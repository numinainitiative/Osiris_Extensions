using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SteamKit2;
using SteamLibrary.SteamShared;

namespace Steam;

public class SteamApiClient
{
	private SteamClient steamClient;

	private CallbackManager manager;

	private SteamUser steamUser;

	private SteamApps steamApps;

	private bool isRunning;

	private bool isConnected;

	private bool isLoggedIn;

	private AutoResetEvent onConnectedEvent = new AutoResetEvent(initialState: false);

	private EResult onConnectedResult;

	private AutoResetEvent onDisconnectedEvent = new AutoResetEvent(initialState: false);

	private AutoResetEvent onLoggedOnEvent = new AutoResetEvent(initialState: false);

	private EResult onLoggedOnResult;

	private AutoResetEvent onLoggedOffEvent = new AutoResetEvent(initialState: false);

	private readonly SharedSteamSettings settings;

	public bool IsConnected => isConnected;

	public bool IsLoggedIn => isLoggedIn;

	public SteamApiClient(SharedSteamSettings settings)
	{
		steamClient = new SteamClient();
		manager = new CallbackManager(steamClient);
		steamUser = steamClient.GetHandler<SteamUser>();
		steamApps = steamClient.GetHandler<SteamApps>();
		manager.Subscribe<SteamClient.ConnectedCallback>(onConnected);
		manager.Subscribe<SteamClient.DisconnectedCallback>(onDisconnected);
		manager.Subscribe<SteamUser.LoggedOnCallback>(onLoggedOn);
		manager.Subscribe<SteamUser.LoggedOffCallback>(onLoggedOff);
		this.settings = settings;
	}

	private void onConnected(SteamClient.ConnectedCallback callback)
	{
		onConnectedResult = callback.Result;
		onConnectedEvent.Set();
	}

	private void onDisconnected(SteamClient.DisconnectedCallback callback)
	{
		isRunning = false;
		onDisconnectedEvent.Set();
	}

	private async void onLoggedOn(SteamUser.LoggedOnCallback callback)
	{
		onLoggedOnResult = callback.Result;
		onLoggedOnEvent.Set();
	}

	private void onLoggedOff(SteamUser.LoggedOffCallback callback)
	{
		onLoggedOffEvent.Set();
	}

	public async Task<EResult> Connect()
	{
		steamClient.Connect();
		isRunning = true;
		EResult result = EResult.OK;
		Task.Run(delegate
		{
			while (isRunning)
			{
				manager.RunWaitCallbacks(TimeSpan.FromSeconds(1.0));
			}
		});
		await Task.Run(delegate
		{
			onConnectedEvent.WaitOne(10000);
			if (onConnectedResult != EResult.OK)
			{
				isConnected = false;
				result = onConnectedResult;
			}
			else
			{
				isConnected = true;
			}
		});
		return result;
	}

	public async Task<EResult> Login()
	{
		EResult result = EResult.OK;
		steamUser.LogOnAnonymous(new SteamUser.AnonymousLogOnDetails
		{
			ClientLanguage = settings.LanguageKey
		});
		await Task.Run(delegate
		{
			onLoggedOnEvent.WaitOne(10000);
			if (onLoggedOnResult != EResult.OK)
			{
				isLoggedIn = false;
				result = onLoggedOnResult;
			}
			else
			{
				isLoggedIn = true;
			}
		});
		return result;
	}

	public void Logout()
	{
		steamClient.Disconnect();
		isConnected = false;
		isLoggedIn = false;
		isRunning = false;
	}

	public async Task<KeyValue> GetProductInfo(uint id)
	{
		if (!IsConnected)
		{
			if (await Connect() != EResult.OK)
			{
				EResult eResult = await Connect();
				if (eResult != EResult.OK)
				{
					throw new Exception("Failed to connect to Steam " + eResult);
				}
			}
			isConnected = true;
		}
		if (!IsLoggedIn)
		{
			EResult eResult2 = await Login();
			if (eResult2 != EResult.OK)
			{
				throw new Exception("Failed to logon to Steam " + eResult2);
			}
			isLoggedIn = true;
		}
		try
		{
			Task<AsyncJobMultiple<SteamApps.PICSProductInfoCallback>.ResultSet> task = steamApps.PICSGetProductInfo(id, null, onlyPublic: false).ToTask();
			if (task.Wait(10000))
			{
				AsyncJobMultiple<SteamApps.PICSProductInfoCallback>.ResultSet result = task.Result;
				SteamApps.PICSProductInfoCallback pICSProductInfoCallback = ((!result.Complete) ? result.Results.FirstOrDefault((SteamApps.PICSProductInfoCallback prodCallback) => prodCallback.Apps.ContainsKey(id)) : result.Results.First());
				if (pICSProductInfoCallback == null)
				{
					throw new Exception("Failed to get product info for app " + id);
				}
				return pICSProductInfoCallback.Apps[id].KeyValues;
			}
			throw new Exception("Failed to get product info for app (timeout) " + id);
		}
		catch (Exception ex)
		{
			throw new Exception("Failed to get product info for app " + id + ". " + ex.Message);
		}
	}
}
