
using System;
using Unity.Netcode;
using UnityEngine;

namespace Netcode.Transports.Epic.Tests
{
	internal abstract class EpicTransportYieldInstruction : EOSSDKYieldInstruction
	{
		protected EpicTransport epicTransport;

		public EpicTransportYieldInstruction(EOSSDKComponent eossdk, EpicTransport epicTransport, float timeout = 30)
			: base(eossdk, timeout)
		{
			this.epicTransport = epicTransport;
		}

		protected override void Update()
		{
			base.Update();

			NetworkEvent networkEvent;
			while ((networkEvent = epicTransport.PollEvent(out var clientId, out var payload, out var recieveTime))
				!= NetworkEvent.Nothing)
			{
				OnPollEvent(networkEvent, clientId, payload, recieveTime);
			}
		}

		protected abstract void OnPollEvent(NetworkEvent networkEvent, ulong clientId, ArraySegment<byte> payload, float recieveTime);
	}
}
