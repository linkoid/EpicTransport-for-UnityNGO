using System.Collections;
using System.Collections.Generic;
using Netcode.Transports.Epic.Tests;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class EOSSDKComponentTests
{
	private EOSSDKComponent eossdk;

	[UnitySetUp]
	public IEnumerator SetUpEOSSDK()
	{
		Secrets.Load();
		var gameObject = new GameObject("EOSSDK");
		eossdk = gameObject.AddComponent<EOSSDKComponent>();
		yield return null;
	}

	[UnityTearDown]
	public IEnumerator TearDownEOSSDK()
	{
		Object.DestroyImmediate(eossdk.gameObject);
		yield return null;
	}

	[UnityTest]
    public IEnumerator EOSSDKComponent_Login()
	{
		eossdk.Login(Secrets.AuthInfo[0]);
		yield return new WaitForUserConnected(eossdk);
	}

	[UnityTest]
	public IEnumerator EOSSDKComponent_InitializeOnce()
	{
		var gameObject = new GameObject("EOSSDK2");
		var eossdk2 = gameObject.AddComponent<EOSSDKComponent>();

		yield return null;
	}
}
