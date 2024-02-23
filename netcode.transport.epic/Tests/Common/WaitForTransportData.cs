
using System;
using Unity.Netcode;

namespace Netcode.Transports.Epic.Tests
{
	internal class WaitForTransportData : EpicTransportYieldInstruction
	{
		public ArraySegment<byte> Data { get; private set; } = null;

		public WaitForTransportData(EOSSDKComponent eossdk, EpicTransport epicTransport, float timeout = 30)
			: base(eossdk, epicTransport, timeout)
		{ }

		protected override bool CheckIsComplete()
		{
			if (!epicTransport.IsRunning)
				throw new Exception("EpicTransport client connect failed");

			return Data != null;
		}

		protected override void OnPollEvent(NetworkEvent networkEvent, ulong clientId, ArraySegment<byte> payload, float recieveTime)
		{
			if (networkEvent == NetworkEvent.Data)
			{
				Data = payload;
			}
		}
	}
}
