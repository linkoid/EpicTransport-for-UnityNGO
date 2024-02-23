using Netcode.Transports.Epic.Tests;
using Netcode.Transports.Epic;
using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.TestTools;
using System.Text;

public class EpicTransportTests
{
	private EOSSDKComponent eossdk;

	[UnitySetUp]
	public IEnumerator SetUpEOSSDK()
	{
		Secrets.Load();
		GameObject gameObject = new GameObject("EpicTransport");
		eossdk = gameObject.AddComponent<EOSSDKComponent>();
		yield return null;
		eossdk.Login(Secrets.AuthInfo[0]);
		yield return new WaitForUserConnected(eossdk);
	}

	[UnityTearDown]
	public IEnumerator TearDownEOSSDK()
	{
		Object.DestroyImmediate(eossdk.gameObject);
		yield return null;
	}


	// Check that starting a server succeeds.
	[Test]
	public void EpicTransport_BasicInitServer()
	{
		EpicTransport transport = eossdk.gameObject.AddComponent<EpicTransport>();
		transport.Initialize();

		Assert.True(transport.StartServer());

		transport.Shutdown();
	}

	// Check that starting a server when the transport is on a separate GameObject
	// from the IPlatformInterfaceProvider succeeds.
	[Test]
	public void EpicTransport_BasicInitServer_OnSeparateGameObject()
	{
		GameObject gameObject = new GameObject("EpicTransport");
		EpicTransport transport = gameObject.AddComponent<EpicTransport>();
		transport.Initialize();

		Assert.True(transport.StartServer());

		transport.Shutdown();
	}

	// Check that starting an client succeeds.
	[Test]
	public void EpicTransport_BasicInitClient()
	{
		EpicTransport transport = eossdk.gameObject.AddComponent<EpicTransport>();
		transport.Initialize();
		transport.HostUserId = eossdk.LocalUserId;

		Assert.True(transport.StartClient());

		transport.Shutdown();
	}

	// Check that we can't restart a server.
	[Test]
	public void EpicTransport_NoRestartServer()
	{
		EpicTransport transport = eossdk.gameObject.AddComponent<EpicTransport>();
		transport.Initialize();

		transport.StartServer();
		LogAssert.Expect(LogType.Error, "[EpicTransport] Cannot start server: a client/server is already running.");
		Assert.False(transport.StartServer());

		transport.Shutdown();
	}

	// Check that we can't restart a client.
	[Test]
	public void EpicTransport_NoRestartClient()
	{
		EpicTransport transport = eossdk.gameObject.AddComponent<EpicTransport>();
		transport.Initialize();
		transport.HostUserId = eossdk.LocalUserId;

		transport.StartClient();
		LogAssert.Expect(LogType.Error, "[EpicTransport] Cannot start client: a client/server is already running.");
		Assert.False(transport.StartClient());

		transport.Shutdown();
	}

	// Check that we can't start both a server and client on the same transport.
	[Test]
	public void EpicTransport_NotBothServerAndClient()
	{
		EpicTransport transport;

		// Start server then client.
		transport = eossdk.gameObject.AddComponent<EpicTransport>();
		transport.Initialize();
		transport.HostUserId = eossdk.LocalUserId;

		transport.StartServer();
		LogAssert.Expect(LogType.Error, "[EpicTransport] Cannot start client: a client/server is already running.");
		Assert.False(transport.StartClient());

		transport.Shutdown();

		// Start client then server.
		transport = eossdk.gameObject.AddComponent<EpicTransport>();
		transport.Initialize();
		transport.HostUserId = eossdk.LocalUserId;

		transport.StartClient();
		LogAssert.Expect(LogType.Error, "[EpicTransport] Cannot start server: a client/server is already running.");
		Assert.False(transport.StartServer());

		transport.Shutdown();
	}

	// Check that restarting after failure succeeds.
	[Test]
	public void EpicTransport_RestartSucceedsAfterFailure()
	{
		EpicTransport transport = eossdk.gameObject.AddComponent<EpicTransport>();
		transport.Initialize();

		var savedUserId = transport.LocalUserId;
		var badUserId = new Epic.OnlineServices.ProductUserId();
		transport.LocalUserId = badUserId;
		LogAssert.Expect(LogType.Error, "AddNotifyPeerConnectionRequest failed");
		
		Assert.False(transport.StartServer());

		transport.LocalUserId = savedUserId;
		Assert.True(transport.StartServer());

		transport.Shutdown();
	}

	// Check that StartClient returns false with bad connection data.
	[Test]
	public void EpicTransport_StartClientFailsWithBadAddress()
	{
		EpicTransport transport = eossdk.gameObject.AddComponent<EpicTransport>();
		transport.Initialize();

		var badHostId = new Epic.OnlineServices.ProductUserId();
		transport.HostUserId = badHostId;

		LogAssert.Expect(LogType.Error, "TryRequestOrAcceptConnection failed: InvalidUser");
		Assert.False(transport.StartClient());

		transport.Shutdown();
	}


	// Check that StartClient returns false with bad connection data.
	[UnityTest]
	public IEnumerator EpicTransport_ClientConnectsToServer()
	{
		GameObject serverGameObject = new GameObject("EpicTransport_Server");
		EpicTransport serverTransport = serverGameObject.AddComponent<EpicTransport>();
		serverTransport.Initialize();

		GameObject clientGameObject = new GameObject("EpicTransport_Client");
		EpicTransport clientTransport = clientGameObject.AddComponent<EpicTransport>();
		clientTransport.Initialize();
		clientTransport.HostUserId = eossdk.LocalUserId;

		Assert.True(serverTransport.StartServer());
		Assert.True(clientTransport.StartClient());

		yield return new WaitForTransportConnection(eossdk, serverTransport);
	}


	// Check that StartClient returns false with bad connection data.
	[UnityTest]
	public IEnumerator EpicTransport_SendRecievePacket()
	{
		GameObject serverGameObject = new GameObject("EpicTransport_Server");
		EpicTransport serverTransport = serverGameObject.AddComponent<EpicTransport>();
		serverTransport.Initialize();

		GameObject clientGameObject = new GameObject("EpicTransport_Client");
		EpicTransport clientTransport = clientGameObject.AddComponent<EpicTransport>();
		clientTransport.Initialize();
		clientTransport.HostUserId = eossdk.LocalUserId;

		Assert.True(serverTransport.StartServer());
		Assert.True(clientTransport.StartClient());

		yield return new WaitForTransportConnection(eossdk, serverTransport);

		var payload = Encoding.ASCII.GetBytes("Hello World!");
		clientTransport.Send(0, payload, Unity.Netcode.NetworkDelivery.ReliableSequenced);

		var waitForTransportData = new WaitForTransportData(eossdk, serverTransport);
		yield return waitForTransportData;
		Assert.AreEqual(waitForTransportData.Data, payload);
	}
}
