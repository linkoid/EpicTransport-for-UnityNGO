using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Immutable;
using System.IO;
using UnityEngine;
using Newtonsoft.Json;
using Epic.OnlineServices.Auth;
using Epic_OnlineServices = Epic.OnlineServices;

namespace Netcode.Transports.Epic.Tests
{
	[System.Serializable]
	internal struct SecretApplicationInfo
	{
		public string ProductId;
		public string SandboxId;
		public string DeploymentId;
		public string ClientId;
		public string ClientSecret;
	}

	[System.Serializable]
	internal struct SecretAuthInfo
	{
		public LoginCredentialType Type
		{
			get => System.Enum.TryParse<LoginCredentialType>(LoginCredentialType, true, out var result)
				? result : Epic_OnlineServices.Auth.LoginCredentialType.AccountPortal;
			set => LoginCredentialType = value.ToString();
		}

		[SerializeField]
		private string LoginCredentialType;

		/// These fields correspond to <see cref="Credentials.Id" /> and <see cref="Credentials.Token" />, and their use differs based on the login type.
		/// For more information, see <see cref="Credentials" /> and the Auth Interface documentation.
		public string Id;
		public string Token;
	}

	internal static class Secrets
	{
		public static SecretApplicationInfo ApplicationInfo => secretData.ApplicationInfo;
		public static IReadOnlyList<SecretAuthInfo> AuthInfo => secretData.AuthInfo != null ? new ReadOnlyCollection<SecretAuthInfo>(secretData.AuthInfo) : null;

		[System.Serializable]
		private struct SecretData
		{
			public SecretApplicationInfo ApplicationInfo;
			public SecretAuthInfo[] AuthInfo;

			public SecretData(SecretApplicationInfo applicationInfo, SecretAuthInfo[] authInfo)
			{
				ApplicationInfo = applicationInfo;
				AuthInfo = authInfo;
			}
		}

		private static SecretData secretData;

		static Secrets()
		{
			Load();
		}

		public static void Load()
		{
			var filePath = Path.GetFullPath($"{Application.dataPath}/../netcode.transports.epic.tests.secrets.json");
			Load(filePath);
		}

		public static void Load(string filePath)
		{
			string jsonString;
			using (var fileStream = File.OpenRead(filePath))
			{
				using var reader = new StreamReader(fileStream);
				jsonString = reader.ReadToEnd();
			}

			secretData = JsonUtility.FromJson<SecretData>(jsonString);
		}
	}
}