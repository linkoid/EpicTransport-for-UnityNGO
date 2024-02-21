using System.Collections;
using System.Collections.Generic;
using Netcode.Transports.Epic.Tests;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class EOSSDKTests
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
    public IEnumerator EOSLoginTest()
	{
		eossdk.Login(Secrets.AuthInfo[0]);
		yield return new WaitForUserConnected(eossdk);
	}
}
