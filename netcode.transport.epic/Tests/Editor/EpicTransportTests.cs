using Netcode.Transports.Epic.Tests;
using Netcode.Transports.Epic;
using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.TestTools;

public class EpicTransportTests
{
	private EOSSDKComponent eossdk;

	[UnitySetUp]
	public IEnumerator SetUpEOSSDK()
	{
		Secrets.Load();
		var gameObject = new GameObject("EpicTransport");
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


	// Check that starting an IPv4 server succeeds.
	[Test]
	public void EpicTransport_BasicInitServer_IPv4()
	{
		EpicTransport transport = eossdk.gameObject.AddComponent<EpicTransport>();
		transport.Initialize();

		Assert.True(transport.StartServer());

		transport.Shutdown();
	}

	// Check that starting an IPv4 client succeeds.
	[Test]
	public void EpicTransport_BasicInitClient_IPv4()
	{
		EpicTransport transport = eossdk.gameObject.AddComponent<EpicTransport>();
		transport.Initialize();
		transport.HostUserId = eossdk.LocalUserId;

		Assert.True(transport.StartClient());

		transport.Shutdown();
	}

	// Check that starting an IPv6 server succeeds.
	[Test]
	public void EpicTransport_BasicInitServer_IPv6()
	{
		EpicTransport transport = eossdk.gameObject.AddComponent<EpicTransport>();
		transport.Initialize();
		//transport.SetConnectionData("::1", 7777);

		Assert.True(transport.StartServer());

		transport.Shutdown();
	}

	// Check that starting an IPv6 client succeeds.
	[Test]
	public void EpicTransport_BasicInitClient_IPv6()
	{
		EpicTransport transport = eossdk.gameObject.AddComponent<EpicTransport>();
		transport.Initialize();
		transport.HostUserId = eossdk.LocalUserId;
		//transport.SetConnectionData("::1", 7777);

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

		//transport.SetConnectionData("127.0.0.1", 4242, "127.0.0.1");
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

		var savedHostId = transport.HostUserId;
		var badHostId = new Epic.OnlineServices.ProductUserId();
		transport.HostUserId = badHostId;

		//transport.SetConnectionData("foobar", 4242);
		LogAssert.Expect(LogType.Error, "TryRequestOrAcceptConnection failed: InvalidUser");
		Assert.False(transport.StartClient());

		//LogAssert.Expect(LogType.Error, "Invalid network endpoint: foobar:4242.");
		//LogAssert.Expect(LogType.Error, "Target server network address (foobar) is Invalid!");

		transport.Shutdown();
		transport.HostUserId = savedHostId;
	}

#if UTP_TRANSPORT_2_0_ABOVE
        [Test]
        public void EpicTransport_EmptySecurityStringsShouldThrow([Values("", null)] string cert, [Values("", null)] string secret)
        {
            var supportingGO = new GameObject();
            try
            {
                var networkManager = supportingGO.AddComponent<NetworkManager>(); // NM is required for UTP to work with certificates.
                networkManager.NetworkConfig = new NetworkConfig();
                EpicTransport transport = supportingGO.AddComponent<EpicTransport>();
                networkManager.NetworkConfig.NetworkTransport = transport;
                transport.Initialize();
                transport.SetServerSecrets(serverCertificate: cert, serverPrivateKey: secret);

                // Use encryption, but don't set certificate and check for exception
                transport.UseEncryption = true;
                Assert.Throws<System.Exception>(() =>
                {
                    networkManager.StartServer();
                });
                // Make sure StartServer failed
                Assert.False(transport.NetworkDriver.IsCreated);
                Assert.False(networkManager.IsServer);
                Assert.False(networkManager.IsListening);
            }
            finally
            {
                if (supportingGO != null)
                {
                    Object.DestroyImmediate(supportingGO);
                }
            }
        }
#endif
}
