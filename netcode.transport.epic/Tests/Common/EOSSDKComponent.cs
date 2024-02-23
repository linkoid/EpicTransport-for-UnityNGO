// This code is provided for unit test functionality and is not intended to represent ideal practices.
#if UNITY_EDITOR
	#define EOS_EDITOR
#endif

#if UNITY_EDITOR || UNITY_STANDALONE || UNITY_PS4 || UNITY_XBOXONE || UNITY_SWITCH || UNITY_IOS || UNITY_ANDROID
	#define EOS_UNITY
#endif

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN || PLATFORM_64BITS || PLATFORM_32BITS
	#if UNITY_EDITOR_WIN || UNITY_64 || PLATFORM_64BITS
		#define EOS_PLATFORM_WINDOWS_64
	#else
		#define EOS_PLATFORM_WINDOWS_32
	#endif

#elif UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
	#define EOS_PLATFORM_OSX

#elif UNITY_EDITOR_LINUX || UNITY_STANDALONE_LINUX
	#define EOS_PLATFORM_LINUX

#elif UNITY_PS4
	#define EOS_PLATFORM_PS4

#elif UNITY_XBOXONE
	#define EOS_PLATFORM_XBOXONE

#elif UNITY_SWITCH
	#define EOS_PLATFORM_SWITCH

#elif UNITY_IOS || __IOS__
	#define EOS_PLATFORM_IOS

#elif UNITY_ANDROID || __ANDROID__
	#define EOS_PLATFORM_ANDROID
#endif




using Epic.OnlineServices;
using Epic.OnlineServices.Auth;
using Epic.OnlineServices.Connect;
using Epic.OnlineServices.Logging;
using Epic.OnlineServices.Platform;
using System;
using System.Runtime.InteropServices;
using UnityEngine;
using Epic_OnlineServices = Epic.OnlineServices;

namespace Netcode.Transports.Epic.Tests
{
	[ExecuteAlways]
	internal class EOSSDKComponent : MonoBehaviour, IPlatformInterfaceProvider, ILocalUserIdProvider
	{
		private static bool s_IsInitialized = false;

		public PlatformInterface PlatformInterface { get; private set; }
		private const float c_PlatformTickInterval = 0.1f;
		private float m_PlatformTickTimer = 0f;

		public bool IsInitialized { get; private set; } = false;
		public bool IsAuthenticated { get; private set; } = false;
		public bool IsUserConnected { get; private set; } = false;
		public bool LoginFailed { get; private set; } = false;
		public EpicAccountId EpicAccountId { get; private set; }
		public ProductUserId LocalUserId { get; private set; }


		// If we're in editor, we should dynamically load and unload the SDK between play sessions.
		// This allows us to initialize the SDK each time the game is run in editor.
		[DllImport("kernel32.dll")]
		private static extern IntPtr LoadLibrary(string lpLibFileName);

		[DllImport("kernel32.dll")]
		private static extern int FreeLibrary(IntPtr hLibModule);

		[DllImport("kernel32.dll")]
		private static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

		private static IntPtr m_LibraryPointer;


		private void Awake()
		{
			LoadSDK();
		}

		private void Start()
		{
			Initialize();
		}

		// Calling tick on a regular interval is required for callbacks to work.
		internal void Update()
		{
			if (PlatformInterface != null)
			{
				m_PlatformTickTimer += Time.deltaTime;

				if (m_PlatformTickTimer >= c_PlatformTickInterval)
				{
					m_PlatformTickTimer = 0;
					PlatformInterface.Tick();
				}
			}
		}

		private void OnDestroy()
		{
			if (IsInitialized)
			{
				Shutdown();
			}
		}

		private void OnApplicationQuit()
		{
			if (IsInitialized)
			{
				Shutdown();
			}
		}

		private static void LoadSDK()
		{
#if EOS_DYNAMIC_BINDINGS || EOS_EDITOR
#if UNITY_EDITOR
			var libraryPath = System.IO.Path.GetFullPath($"{Application.dataPath}/Plugins/EOSSDK/Bin/{Config.LibraryName}");
#else
			var libraryPath = System.IO.Path.GetFullPath($"{Application.dataPath}/Plugins/x86_64/{Config.LibraryName}");
#endif

			m_LibraryPointer = LoadLibrary(libraryPath);
			if (m_LibraryPointer == IntPtr.Zero)
			{
				throw new Exception("Failed to load library: " + libraryPath);
			}
			Bindings.Hook(m_LibraryPointer, GetProcAddress);

#if EOS_PLATFORM_WINDOWS_64 || EOS_PLATFORM_WINDOWS_32
			WindowsBindings.Hook(m_LibraryPointer, GetProcAddress);
#elif EOS_PLATFORM_ANDROID
			AndroidBindings.Hook(s_LibraryPointer, GetProcAddress);
#elif EOS_PLATFORM_IOS
			IOSBindings.Hook(s_LibraryPointer, GetProcAddress);
#endif
#endif
		}

		private void Initialize()
		{
			if (IsInitialized || PlatformInterface != null || s_IsInitialized)
			{
				// Already initialized
				return;
			}

			var assemblyName = System.Reflection.Assembly.GetExecutingAssembly().GetName();
			var initializeOptions = new InitializeOptions()
			{
				ProductName = assemblyName.Name,
				ProductVersion = assemblyName.Version.ToString()
			};

			var initializeResult = PlatformInterface.Initialize(ref initializeOptions);
			if (initializeResult != Result.Success)
			{
				throw new Exception("Failed to initialize platform: " + initializeResult);
			}

			// The SDK outputs lots of information that is useful for debugging.
			// Make sure to set up the logging interface as early as possible: after initializing.
			LoggingInterface.SetLogLevel(LogCategory.AllCategories, LogLevel.VeryVerbose);
			LoggingInterface.SetCallback(OnLog);

			PlatformFlags platformFlags = PlatformFlags.DisableOverlay | PlatformFlags.DisableSocialOverlay;
#if UNITY_EDITOR
			platformFlags |= PlatformFlags.LoadingInEditor;
#endif

			var options = new Options()
			{
				ProductId = Secrets.ApplicationInfo.ProductId,
				SandboxId = Secrets.ApplicationInfo.SandboxId,
				DeploymentId = Secrets.ApplicationInfo.DeploymentId,
				ClientCredentials = new ClientCredentials()
				{
					ClientId = Secrets.ApplicationInfo.ClientId,
					ClientSecret = Secrets.ApplicationInfo.ClientSecret
				},
				Flags = platformFlags,
			};

			var windowsOptions = new WindowsOptions()
			{
				ProductId = Secrets.ApplicationInfo.ProductId,
				SandboxId = Secrets.ApplicationInfo.SandboxId,
				DeploymentId = Secrets.ApplicationInfo.DeploymentId,
				ClientCredentials = new ClientCredentials()
				{
					ClientId = Secrets.ApplicationInfo.ClientId,
					ClientSecret = Secrets.ApplicationInfo.ClientSecret
				},
				Flags = platformFlags,
				IsServer = false,
			};

			PlatformInterface = PlatformInterface.Create(ref windowsOptions);
			if (PlatformInterface == null)
			{
				throw new Exception("Failed to create platform");
			}

			s_IsInitialized = true;
			IsInitialized = true;
		}

		internal void Login(SecretAuthInfo authInfo)
		{
			AuthLogin(authInfo);
		}

		private void Shutdown()
		{
			if (PlatformInterface != null)
			{
				PlatformInterface.Release();
				PlatformInterface = null;
				PlatformInterface.Shutdown();
				Debug.Log("Shutdown!");
			}
			else
			{
				Debug.LogWarning("Not shutting down platform interface because it is null");
			}

			if (m_LibraryPointer != IntPtr.Zero)
			{
				UnloadSDK();
			}
			else
			{
				Debug.LogWarning("Not shutting down EOS SDK because library pointer is null");
			}

			s_IsInitialized = false;
			IsInitialized = false;
		}

		private static void UnloadSDK()
		{
#if EOS_DYNAMIC_BINDINGS || EOS_EDITOR
			Bindings.Unhook();
#if EOS_PLATFORM_WINDOWS_64 || EOS_PLATFORM_WINDOWS_32
			WindowsBindings.Unhook();
#elif EOS_PLATFORM_ANDROID
			AndroidBindings.Unhook();
#elif EOS_PLATFORM_IOS
			IOSBindings.Unhook();
#endif
#endif
			int i = 0;
			int lastHandleCount = 0;
			while (true)
			{
				int handleCount = FreeLibrary(m_LibraryPointer);
				if (handleCount == 0) break;

				if (handleCount == lastHandleCount)
				{
					i++;
				}
				else
				{
					i = 0;
				}

				if (i >= 1000)
				{
					Debug.LogError($"Could not free all library handles for {m_LibraryPointer}");
					return;
				}

				lastHandleCount = handleCount;
			}
			m_LibraryPointer = IntPtr.Zero;
		}

		private void AuthLogin(SecretAuthInfo authInfo)
		{
			Debug.Log($"Login via {authInfo.Type}");

			var loginOptions = new Epic_OnlineServices.Auth.LoginOptions()
			{
				Credentials = new Epic_OnlineServices.Auth.Credentials()
				{
					Type = authInfo.Type,
					Id = authInfo.Id,
					Token = authInfo.Token
				},
				ScopeFlags = AuthScopeFlags.BasicProfile | AuthScopeFlags.FriendsList | AuthScopeFlags.Presence,
			};

			// Ensure platform tick is called on an interval, or this will not callback.
			PlatformInterface.GetAuthInterface().Login(ref loginOptions, null, OnAuthLogin);
		}

		private void ConnectLogin(Utf8String loginToken)
		{
			var options = new Epic_OnlineServices.Connect.LoginOptions()
			{
				Credentials = new()
				{
					Type = ExternalCredentialType.EpicIdToken,
					Token = loginToken,
				},
			};
			PlatformInterface.GetConnectInterface().Login(ref options, this, OnConnectLogin);
		}

		void OnAuthLogin(ref Epic_OnlineServices.Auth.LoginCallbackInfo loginCallbackInfo)
		{
			if (!Common.IsOperationComplete(loginCallbackInfo.ResultCode)) return;

			if (loginCallbackInfo.ResultCode != Result.Success)
			{
				Debug.LogError($"{nameof(AuthLogin)} failed: " + loginCallbackInfo.ResultCode, this);
				LoginFailed = true;
				return;
			}

			IsAuthenticated = true;

			Debug.Log($"{nameof(AuthLogin)} Success! {loginCallbackInfo.LocalUserId}", this);
			EpicAccountId = loginCallbackInfo.LocalUserId;

			IsAuthenticated = true;

			var options = new Epic_OnlineServices.Auth.CopyIdTokenOptions()
			{
				AccountId = EpicAccountId,
			};
			var result = PlatformInterface.GetAuthInterface().CopyIdToken(ref options, out var token);

			if (result != Result.Success)
			{
				Debug.LogError($"{nameof(OnAuthLogin)} CopyIdToken Failed: {result}", this);
				return;
			}

			ConnectLogin(token.Value.JsonWebToken);
		}

		void OnConnectLogin(ref Epic_OnlineServices.Connect.LoginCallbackInfo data)
		{
			if (!Common.IsOperationComplete(data.ResultCode)) return;

			if (data.ResultCode == Result.InvalidUser)
			{
				var createUserOptions = new CreateUserOptions()
				{
					ContinuanceToken = data.ContinuanceToken,
				};
				Debug.Log($"{nameof(OnConnectLogin)} Creating new user for {data.LocalUserId}", this);
				PlatformInterface.GetConnectInterface().CreateUser(ref createUserOptions, null, OnCreateUser);
				return;
			}
			if (data.ResultCode != Result.Success)
			{
				Debug.LogError($"{nameof(ConnectLogin)} Failed: {data.ResultCode}", this);
				LoginFailed = true;
				return;
			}

			IsUserConnected = true;
			LocalUserId = data.LocalUserId;
			LoginFailed = false;
			Debug.Log($"{nameof(ConnectLogin)} Success! {data.LocalUserId}", this);
		}

		void OnCreateUser(ref CreateUserCallbackInfo data)
		{
			if (!Common.IsOperationComplete(data.ResultCode)) return;

			if (data.ResultCode != Result.Success)
			{
				Debug.LogError($"CreateUser Failed: {data.ResultCode}", this);
				return;
			}

			Debug.Log($"CreateUser Success! {data.LocalUserId}", this);
			LocalUserId = data.LocalUserId;
		}

		static void OnLog(ref LogMessage message)
		{
			//if (LogLevel < message.Level) return;
			string output = $"[EOS: {message.Category}] {message.Message}";
			switch (message.Level)
			{
				case LogLevel.VeryVerbose:
				case LogLevel.Verbose:
				case LogLevel.Info:
					Debug.Log(output);
					break;
				case LogLevel.Warning:
					Debug.LogWarning(output);
					break;
				case LogLevel.Error:
				case LogLevel.Fatal:
					Debug.LogError(output);
					break;
				case LogLevel.Off:
					break;
			}
		}
	}
}