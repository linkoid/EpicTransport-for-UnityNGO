using Epic.OnlineServices;
using Epic.OnlineServices.P2P;
using System;
using UnityEngine;

namespace Netcode.Transports.Epic
{
	internal class EpicP2PWrapper
	{
		private P2PInterface handle;
		private ProductUserId localUserId;

		public EpicP2PWrapper(P2PInterface handle, ProductUserId localUserId)
		{
			this.handle = handle;
			this.localUserId = localUserId;
		}

		public bool TryRequestOrAcceptConnection(ProductUserId remoteUserId, SocketId? socketId)
		{
			var result = RequestOrAcceptConnectionInternal(remoteUserId, socketId);
			return CheckResult(result, nameof(TryRequestOrAcceptConnection));
		}

		private Result RequestOrAcceptConnectionInternal(ProductUserId remoteUserId, SocketId? socketId)
		{
			var options = new AcceptConnectionOptions()
			{
				LocalUserId = localUserId,
				RemoteUserId = remoteUserId,
				SocketId = socketId,
			};
			return handle.AcceptConnection(ref options);
		}

		public void SendPacket(ProductUserId remoteUserId, SocketId? socketId, ArraySegment<byte> data, byte channel, PacketReliability reliability)
		{
			var result = SendPacketInternal(remoteUserId, socketId, data, channel, reliability);
			AssertSuccess(result, nameof(SendPacket));
		}

		private Result SendPacketInternal(ProductUserId remoteUserId, SocketId? socketId, ArraySegment<byte> data, byte channel, PacketReliability reliability)
		{
			var sendPacketOptions = new SendPacketOptions()
			{
				LocalUserId = localUserId,
				RemoteUserId = remoteUserId,
				SocketId = socketId,
				Data = data,
				Channel = channel,
				Reliability = reliability,
			};
			return handle.SendPacket(ref sendPacketOptions);
		}

		public bool TryGetNextRecievedPacketSize(byte requestedChannel, out uint packetSize)
		{
			var result = GetNextRecievedPacketSizeInternal(requestedChannel, out packetSize);
			if (result == Result.NotFound) return false; // There are no more packets
			return CheckResult(result, nameof(TryGetNextRecievedPacketSize));
		}

		private Result GetNextRecievedPacketSizeInternal(byte requestedChannel, out uint packetSize)
		{
			var getNextReceivedPacketSizeOptions = new GetNextReceivedPacketSizeOptions()
			{
				LocalUserId = localUserId,
				RequestedChannel = default,
			};
			return handle.GetNextReceivedPacketSize(ref getNextReceivedPacketSizeOptions, out packetSize);
		}


		public bool TryRecievePacket(ArraySegment<byte> outData, out RecievedPacketInfo recievedPacketInfo, out uint bytesWritten, byte? requestedChannel = null)
		{
			var result = RecievePacketInternal(outData, out recievedPacketInfo, out bytesWritten, requestedChannel);
			if (result == Result.NotFound) return false; // There are no more packets
			return CheckResult(result, nameof(TryRecievePacket));
		}

		private Result RecievePacketInternal(ArraySegment<byte> outData, out RecievedPacketInfo recievedPacketInfo, out uint bytesWritten, byte? requestedChannel)
		{
			recievedPacketInfo = default;

			var receivePacketOptions = new ReceivePacketOptions()
			{
				LocalUserId = localUserId,
				RequestedChannel = requestedChannel,
				MaxDataSizeBytes = (uint)outData.Count,
			};

			return handle.ReceivePacket(
				ref receivePacketOptions,
				out recievedPacketInfo.RemoteUserId,
				out recievedPacketInfo.SocketId,
				out recievedPacketInfo.Channel,
				outData,
				out bytesWritten
			);
		}

		public bool TryCloseConnection(ProductUserId remoteUserId, SocketId? socketId)
		{
			var result = CloseConnectionInternal(remoteUserId, socketId);
			return CheckResult(result, nameof(TryCloseConnection));
		}

		private Result CloseConnectionInternal(ProductUserId remoteUserId, SocketId? socketId)
		{
			var closeServerConnectionOptions = new CloseConnectionOptions()
			{
				LocalUserId = localUserId,
				RemoteUserId = remoteUserId,
				SocketId = socketId,
			};
			return handle.CloseConnection(ref closeServerConnectionOptions);
		}

		public bool CheckResult(Result result, string nameofMethod, bool requireCompletion = true)
		{
			if (result != Result.Success && Common.IsOperationComplete(result))
			{
				Debug.LogError($"{nameofMethod} failed: {result}");
				return false;
			}
			else if (result != Result.Success)
			{
				Debug.LogWarning($"{nameofMethod} incomplete: {result}");
				return !requireCompletion;
			}
			else
			{
				return true;
			}
		}

		public void AssertSuccess(Result result, string nameofMethod)
		{
			if (result != Result.Success && Common.IsOperationComplete(result))
			{
				throw new EpicP2PException($"{nameofMethod} failed: {result}");
			}
			else if (result != Result.Success)
			{
				throw new EpicP2PException($"{nameofMethod} incomplete: {result}");
			}
		}
	}
}
