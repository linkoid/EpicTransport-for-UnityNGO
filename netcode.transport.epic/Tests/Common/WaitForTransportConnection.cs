
using System;
using Unity.Netcode;

namespace Netcode.Transports.Epic.Tests
{
	internal class WaitForTransportConnection : EpicTransportYieldInstruction
	{
		private bool isConnected = false;

		public ulong ConnectedId { get; private set; }

		public WaitForTransportConnection(EOSSDKComponent eossdk, EpicTransport epicTransport, float timeout = 30)
			: base(eossdk, epicTransport, timeout)
		{ }

		protected override bool CheckIsComplete()
		{
			if (!epicTransport.IsRunning)
				throw new Exception("EpicTransport client connect failed");

			return isConnected;
		}

		protected override void OnPollEvent(NetworkEvent networkEvent, ulong clientId, ArraySegment<byte> payload, float recieveTime)
		{
			if (networkEvent == NetworkEvent.Connect)
			{
				ConnectedId = clientId;
				isConnected = true;
			}
		}
	}
}
