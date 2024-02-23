using Epic.OnlineServices;
using Epic.OnlineServices.P2P;
using Epic.OnlineServices.Platform;
using SplashOfSlimes.OnlineServices;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.InteropServices;
using Unity.Netcode;
using UnityEngine;

namespace Netcode.Transports.Epic
{
	public class EpicTransport : NetworkTransport
	{
		private const int EOS_P2P_MAX_PACKET_SIZE = 1170;

		private struct Timestamped<T>
		{
			public T Value;
			public float Timestamp;

			public Timestamped(T value)
			{
				Value = value;
				Timestamp = Time.realtimeSinceStartup;
			}

			public Timestamped(T value, float timestamp)
			{
				Value = value;
				Timestamp = timestamp;
			}

			public static explicit operator Timestamped<T>(T value)
			{
				return new Timestamped<T>(value);
			}

			public static explicit operator T(Timestamped<T> timestamped)
			{
				return timestamped.Value;
			}
		}

		[Flags]
		private enum PacketFlags
		{
			None,
			Fragment,
			LastFragment,
		}

		[StructLayout(LayoutKind.Sequential, Pack = 0)]
		private struct PacketHeader
		{
			public PacketFlags Flags;
		}

		private struct Packet
		{
			public PacketHeader Header;
			public ArraySegment<byte> Payload;

			public static unsafe Packet FromBytes(ArraySegment<byte> packetBytes)
			{
				Packet packet;
				var headerSpan = packetBytes.AsSpan(0, sizeof(PacketHeader));
				fixed (byte* ptr = headerSpan)
				{
					packet.Header = (PacketHeader)Marshal.PtrToStructure((IntPtr)ptr, typeof(PacketHeader));
				}

				return new()
				{
					Header = packet.Header,
					Payload = packetBytes[sizeof(PacketHeader)..]
				};
			}

			public unsafe byte[] ToBytes()
			{
				var bytes = new byte[sizeof(PacketHeader) + Payload.Count];
				ToBytes(bytes);
				return bytes;
			}

			public unsafe ArraySegment<byte> ToBytes(byte[] bytes, int startIndex = 0)
			{
				int size = sizeof(PacketHeader) + Payload.Count;
				var arraySegment = new ArraySegment<byte>(bytes, startIndex, size);
				ToBytes(arraySegment);
				return arraySegment;
			}

			public unsafe void ToBytes(ArraySegment<byte> bytes)
			{
				fixed (byte* ptr = bytes.AsSpan(0, sizeof(PacketHeader)))
				{
					Marshal.StructureToPtr(Header, (IntPtr)ptr, true);
				}
				Payload.CopyTo(bytes[sizeof(PacketHeader)..]);
			}
		}

		public override ulong ServerClientId => 0;

		//private string clientSocketSuffix = "client";
		//private string serverSocketSuffix = "server";
		private const string transportSocketSuffix = "transport";

		private SocketId transportSocket;

		private P2PInterface p2pHandle;
		private EpicP2PWrapper p2p;
		private NetworkManager networkManager;

		//private Queue<Timestamped<OnIncomingConnectionRequestInfo>> connectionRequests = new();
		private Queue<Timestamped<OnPeerConnectionEstablishedInfo>> establishedConnections = new();
		private Queue<Timestamped<OnRemoteConnectionClosedInfo>> disconnections = new();

		private Dictionary<ulong, ProductUserId> remoteConnections = new();

		private List<ulong> connectionRequestNotificationIds = new();
		private List<ulong> connectionEstablishedNotificationIds = new();
		private List<ulong> connectionInterruptedNotificationIds = new();
		private List<ulong> connectionClosedNotificationIds = new();

		private byte OutboundChannel => 0;

		public string SocketCategory { get => _socketCategory; set => SetIfNotRunning(ref _socketCategory, value, nameof(SocketCategory)); }
		[SerializeField] private string _socketCategory = "default";

		/// <summary>
		/// Setting for controlling whether relay servers are used.
		/// </summary>
		public RelayControl RelayControl { get => _relayControl; set => SetIfNotRunning(ref _relayControl, value, nameof(RelayControl)); }

		[SerializeField, Tooltip("Setting for controlling whether relay servers are used.")]
		private RelayControl _relayControl = RelayControl.AllowRelays;

		public PlatformInterface PlatformInterface { get => _platformInterface; set => SetIfNotRunning(ref _platformInterface, value, nameof(PlatformInterface)); }
		private PlatformInterface _platformInterface;

		public ProductUserId LocalUserId { get => GetOrCreateProductUserId(_localUserId, _localUserIdString); set => SetIfNotRunning(ref _localUserId, ref _localUserIdString, value, nameof(LocalUserId)); }
		private ProductUserId _localUserId;
		[SerializeField] private string _localUserIdString;

		public ProductUserId HostUserId { get => GetOrCreateProductUserId(_hostUserId, _hostUserIdString); set => SetIfNotRunning(ref _hostUserId, ref _hostUserIdString, value, nameof(HostUserId)); }
		private ProductUserId _hostUserId;
		[SerializeField] private string _hostUserIdString;

		private bool SetIfNotRunning<T>(ref T backingField, T value, string nameofProperty = "this property")
		{
			if (IsRunning)
			{
				Debug.LogError($"Cannot set {nameofProperty} while {nameof(EpicTransport)} is running.");
				return false;
			}
			backingField = value;
			return true;
		}

		private bool SetIfNotRunning(ref ProductUserId backingField, ref string stringField, ProductUserId value, string nameofProperty = "this property")
		{
			if (!SetIfNotRunning(ref backingField, value, nameofProperty))
			{
				return false;
			}
			stringField = backingField?.ToString() ?? null;
			return true;
		}

		private ProductUserId GetOrCreateProductUserId(ProductUserId backingField, string stringField)
		{
			if (backingField != null)
			{
				return backingField;
			}
			else if (!string.IsNullOrEmpty(stringField))
			{
				return ProductUserId.FromString(stringField);
			}
			else
			{
				return null;
			}
		}


		public bool IsRunning { get; private set; } = false;


		private Dictionary<RecievedPacketInfo, List<byte[]>> fragmentedPackets = new();

		/// <summary>
		/// Set the <see cref="HostUserId"/> using the given <paramref name="hostUserIdString"/>.
		/// </summary>
		/// <param name="hostUserIdString">The product user id string of the host.</param>
		/// <exception cref="InvalidOperationException">If EpicTransport is already running</exception>
		public void SetHost(string hostUserIdString)
		{
			if (IsRunning)
				throw new InvalidOperationException($"Cannot {nameof(SetHost)}() while {nameof(EpicTransport)} is running.");

			if (hostUserIdString != null)
			{
				HostUserId = ProductUserId.FromString(hostUserIdString);
			}
			else
			{
				HostUserId = null;
			}
		}

		/// <summary>
		/// Set the <see cref="LocalUserId"/> using the given <paramref name="localUserIdString"/>.
		/// </summary>
		/// <param name="localUserIdString">The product user id string of the local user.</param>
		/// <exception cref="InvalidOperationException">If EpicTransport is already running</exception>
		public void SetLocalUser(string localUserIdString)
		{
			if (IsRunning)
				throw new InvalidOperationException($"Cannot {nameof(SetLocalUser)}() while {nameof(EpicTransport)} is running.");

			if (localUserIdString != null)
			{
				LocalUserId = ProductUserId.FromString(localUserIdString);
			}
			else
			{
				LocalUserId = null;
			}
		}

		public override void Initialize(NetworkManager networkManager = null)
		{
			LogDeveloper($"Initialize({networkManager})");

			if (PlatformInterface == null)
			{
				if (TryGetNearestComponent(out IPlatformInterfaceProvider platformInterfaceProvider))
				{
					PlatformInterface = platformInterfaceProvider.PlatformInterface;
				}
				else
				{
					throw new InvalidOperationException($"Cannot {nameof(Initialize)} {nameof(EpicTransport)}:" +
						$"{nameof(PlatformInterface)} is null, and no {nameof(IPlatformInterfaceProvider)} could be found.");
				}
			}

			if (LocalUserId == null)
			{
				if (TryGetNearestComponent(out ILocalUserIdProvider localUserIdProvider))
				{
					LocalUserId = localUserIdProvider.LocalUserId;
				}
				else
				{
					throw new InvalidOperationException($"Cannot {nameof(Initialize)} {nameof(EpicTransport)}:" +
						$"{nameof(LocalUserId)} is null, and no {nameof(ILocalUserIdProvider)} could be found.");
				}
			}

			p2pHandle = PlatformInterface.GetP2PInterface();
			p2p = new EpicP2PWrapper(p2pHandle, LocalUserId);

			var setRelayControlOptions = new SetRelayControlOptions()
			{
				RelayControl = RelayControl,
			};
			p2pHandle.SetRelayControl(ref setRelayControlOptions);

			this.networkManager = networkManager;
		}

		private bool TryGetNearestComponent<T>(out T component, bool includeInactive = false)
			where T : class
		{
			if (TryGetComponent(out component)) return true;

			component = GetComponentInChildren<T>(includeInactive);
			if (component != null) return true;

			component = GetComponentInParent<T>(includeInactive);
			if (component != null) return true;

			foreach (var comp in Resources.FindObjectsOfTypeAll<Component>())
			{
				if (comp is T t)
				{
					component = t;
					break;
				}
			}

			if (component != null) return true;
			return false;
		}

		public override bool StartClient()
		{
			if (IsRunning)
			{
				LogError("Cannot start client: a client/server is already running.");
				return false;
			}

			LogDeveloper($"StartClient()");

			if (HostUserId == null)
				throw new InvalidOperationException($"Cannot {nameof(StartClient)}: {nameof(HostUserId)} has not been assigned");

			transportSocket = GetSocketId(SocketCategory, HostUserId.ToString().GetHashCode(), transportSocketSuffix);

			//if (!AcceptPeerConnectionRequestsOn(transportSocket)) return false;

			if (!EstablishPeerConnectionsOn(transportSocket)) return false;

			if (!p2p.TryRequestOrAcceptConnection(HostUserId, transportSocket)) return false;

			IsRunning = true;
			return true;
		}

		public override bool StartServer()
		{
			if (IsRunning)
			{
				LogError("Cannot start server: a client/server is already running.");
				return false;
			}

			LogDeveloper($"StartServer()");

			transportSocket = GetSocketId(SocketCategory, LocalUserId.ToString().GetHashCode(), transportSocketSuffix);

			if (!AcceptPeerConnectionRequestsOn(transportSocket)) return false;

			if (!EstablishPeerConnectionsOn(transportSocket)) return false;


			IsRunning = true;
			return true;
		}

		private bool AcceptPeerConnectionRequestsOn(SocketId socketId)
		{
			var connectionRequestOptions = new AddNotifyPeerConnectionRequestOptions()
			{
				LocalUserId = LocalUserId,
				SocketId = socketId,
			};
			var connectionRequestNotificationId = p2pHandle.AddNotifyPeerConnectionRequest(
				ref connectionRequestOptions,
				clientData: null,
				OnPeerConnectionRequest
			);
			if (connectionRequestNotificationId == Common.InvalidNotificationid)
			{
				Debug.LogError($"AddNotifyPeerConnectionRequest failed");
				return false;
			}
			connectionRequestNotificationIds.Add(connectionRequestNotificationId);

			return true;
		}

		private bool EstablishPeerConnectionsOn(SocketId socketId)
		{
			var connectionEstablishedOptions = new AddNotifyPeerConnectionEstablishedOptions()
			{
				LocalUserId = LocalUserId,
				SocketId = socketId,
			};
			var connectionEstablishedNotificationId = p2pHandle.AddNotifyPeerConnectionEstablished(
				ref connectionEstablishedOptions,
				clientData: null,
				OnPeerConnectionEstablished
			);
			if (connectionEstablishedNotificationId == Common.InvalidNotificationid)
			{
				Debug.LogError($"AddNotifyPeerConnectionEstablished failed");
				return false;
			}
			connectionEstablishedNotificationIds.Add(connectionEstablishedNotificationId);

			var connectionInterruptedOptions = new AddNotifyPeerConnectionInterruptedOptions()
			{
				LocalUserId = LocalUserId,
				SocketId = socketId,
			};
			var connectionInterruptedNotificationId = p2pHandle.AddNotifyPeerConnectionInterrupted(
				ref connectionInterruptedOptions,
				clientData: null,
				OnPeerConnectionInterrupted
			);
			if (connectionInterruptedNotificationId == Common.InvalidNotificationid)
			{
				Debug.LogError($"AddNotifyPeerConnectionInterrupted failed");
				return false;
			}
			connectionInterruptedNotificationIds.Add(connectionInterruptedNotificationId);

			var connectionClosedOptions = new AddNotifyPeerConnectionClosedOptions()
			{
				LocalUserId = LocalUserId,
				SocketId = socketId,
			};
			var connectionClosedNotificationId = p2pHandle.AddNotifyPeerConnectionClosed(
				ref connectionClosedOptions,
				clientData: null,
				OnPeerConnectionClosed
			);
			if (connectionClosedNotificationId == Common.InvalidNotificationid)
			{
				Debug.LogError($"AddNotifyPeerConnectionClosed failed");
				return false;
			}
			connectionClosedNotificationIds.Add(connectionClosedNotificationId);
			return true;
		}

		private byte[] tempSendPacketBytes = new byte[EOS_P2P_MAX_PACKET_SIZE];
		public override void Send(ulong clientId, ArraySegment<byte> payload, NetworkDelivery networkDelivery)
		{
			LogDeveloper($"Send({clientId}, ..., {networkDelivery})");
			var remoteUserId = GetUserId(clientId);

			int splitSize;
			unsafe { splitSize = EOS_P2P_MAX_PACKET_SIZE - sizeof(PacketHeader); }

			if (networkDelivery == NetworkDelivery.ReliableFragmentedSequenced)
			{

				for (int i = 0; i < payload.Count; i += splitSize)
				{
					int to = Math.Min(i + splitSize, payload.Count);
					bool end = to == payload.Count;
					bool start = i == 0;
					PacketHeader header = new();
					header.Flags |= PacketFlags.Fragment;
					if (to == payload.Count) // end
					{
						header.Flags |= PacketFlags.LastFragment;
					}
					var packet = new Packet()
					{
						Header = header,
						Payload = payload[i..to],
					};
					var packetBytes = packet.ToBytes(tempSendPacketBytes);
					p2p.SendPacket(
						remoteUserId,
						transportSocket,
						packetBytes,
						OutboundChannel,
						GetPacketReliability(networkDelivery)
					);
				}

			}
			else
			{
				var packet = new Packet()
				{
					Payload = payload,
				};
				var packetBytes = packet.ToBytes(tempSendPacketBytes);
				p2p.SendPacket(
					remoteUserId,
					transportSocket,
					packetBytes,
					OutboundChannel,
					GetPacketReliability(networkDelivery)
				);
			}
		}

		public override NetworkEvent PollEvent(out ulong clientId, out ArraySegment<byte> payload, out float receiveTime)
		{
			if (establishedConnections.Count > 0)
			{
				var request = establishedConnections.Dequeue();
				clientId = GetClientId(request.Value.RemoteUserId);
				payload = default;
				receiveTime = request.Timestamp;
				return NetworkEvent.Connect;
			}

			if (disconnections.Count > 0)
			{
				var request = disconnections.Dequeue();
				clientId = GetClientId(request.Value.RemoteUserId);
				payload = default;
				receiveTime = request.Timestamp;
				return NetworkEvent.Disconnect;
			}

			if (GetNextPacket(out var remoteUserId, out payload, out receiveTime))
			{
				clientId = GetClientId(remoteUserId);
				return NetworkEvent.Data;
			}


			clientId = default;
			payload = default;
			receiveTime = default;
			return NetworkEvent.Nothing;
		}

		private bool GetNextPacket(out ProductUserId remoteUserId, out ArraySegment<byte> payload, out float receiveTime)
		{
			remoteUserId = default;
			payload = null;
			receiveTime = default;

			int frag_loop = 0;
		RecievePacket:
			if (frag_loop++ > 100)
			{
				Log("frag_loop ran for too long!");
				return false;
			}
			if (!TryRecieveNextPacket(out var recievedPacketInfo, out Packet packet))
			{
				return false;
			}
			remoteUserId = recievedPacketInfo.RemoteUserId;


			if (packet.Header.Flags.HasFlag(PacketFlags.Fragment))
			{
				if (!fragmentedPackets.TryGetValue(recievedPacketInfo, out var fragments))
				{
					// XXX FIXME This is a Pseudo-Memory Leak!
					fragmentedPackets[recievedPacketInfo] = fragments = new();
				}
				fragments.Add(packet.Payload.ToArray());

				if (!packet.Header.Flags.HasFlag(PacketFlags.LastFragment))
				{
					LogDeveloper($"GOT A FRAGMENTED PACKET!");
					goto RecievePacket; // Read the next packet.
				}
				else
				{
					LogDeveloper($"GOT END OF A FRAGMENTED PACKET!");
					var length = fragments.Sum(x => x.Length);
					byte[] combinedBytes = new byte[length];
					int index = 0;
					for (int i = 0; i < length; i += fragments[index++].Length)
					{
						fragments[index].CopyTo(combinedBytes, i);
					}
					payload = combinedBytes.ToArray();
					fragments.Clear();
				}
			}
			else
			{
				LogDeveloper($"GOT A PACKET!");
				payload = packet.Payload.ToArray();
			}

			receiveTime = Time.realtimeSinceStartup;
			return true;
		}

		private byte[] tempRecievePacketBytes = new byte[EOS_P2P_MAX_PACKET_SIZE];
		private bool TryRecieveNextPacket(out RecievedPacketInfo recievedPacketInfo, out Packet packet)
		{
			packet = default;

			if (!p2p.TryRecievePacket(tempRecievePacketBytes, out recievedPacketInfo, out var bytesWritten))
			{
				//Debug.LogError("Got error in TryRecievePacket, but not in TryGetNextRecievedPacketSize?");
				return false;
			}

			packet = Packet.FromBytes(new ArraySegment<byte>(tempRecievePacketBytes, 0, (int)bytesWritten));
			return true;
		}

		public override ulong GetCurrentRtt(ulong clientId)
		{
			//var remoteUserId = GetUserId(clientId);
			return 0;
		}

		public override void DisconnectLocalClient()
		{
			LogDeveloper($"DisconnectLocalClient()");

			bool error = !p2p.TryCloseConnection(HostUserId, transportSocket);

			remoteConnections.Remove(GetClientId(HostUserId));

			if (error) return;
		}

		public override void DisconnectRemoteClient(ulong clientId)
		{
			LogDeveloper($"DisconnectRemoteClient({clientId})");
			var remoteUserId = GetUserId(clientId);

			bool error = !p2p.TryCloseConnection(remoteUserId, transportSocket);

			remoteConnections.Remove(clientId);

			if (error) return;
		}

		public override void Shutdown()
		{
			LogDeveloper($"Shutdown()");

			var closeServerConnectionsOptions = new CloseConnectionsOptions()
			{
				LocalUserId = LocalUserId,
				SocketId = transportSocket,
			};
			var result1 = p2pHandle.CloseConnections(ref closeServerConnectionsOptions);

			ClearNotificationIds(p2pHandle.RemoveNotifyPeerConnectionRequest, connectionRequestNotificationIds);
			ClearNotificationIds(p2pHandle.RemoveNotifyPeerConnectionEstablished, connectionEstablishedNotificationIds);
			ClearNotificationIds(p2pHandle.RemoveNotifyPeerConnectionInterrupted, connectionInterruptedNotificationIds);
			ClearNotificationIds(p2pHandle.RemoveNotifyPeerConnectionClosed, connectionClosedNotificationIds);

			bool error = CheckResult(result1, nameof(p2pHandle.CloseConnections));

			IsRunning = false;

			if (error) return;
		}

		private void ClearNotificationIds(Action<ulong> removeMethod, ICollection<ulong> list)
		{
			foreach (var notificationId in list)
			{
				removeMethod(notificationId);
			}
			list.Clear();
		}



		#region EOS Callbacks

		void OnPeerConnectionRequest(ref OnIncomingConnectionRequestInfo data)
		{
			float recieveTime = Time.realtimeSinceStartup;
			LogDeveloper($"OnPeerConnectionRequest: {data.SocketId} {data.RemoteUserId}");
			var options = new AcceptConnectionOptions()
			{
				LocalUserId = data.LocalUserId,
				RemoteUserId = data.RemoteUserId,
				SocketId = data.SocketId,
			};
			var result = p2pHandle.AcceptConnection(ref options);
			if (!CheckResult(result, nameof(p2pHandle.AcceptConnection))) return;

			//if (networkManager.IsServer || networkManager.IsHost)
			//{
			//	connectionRequests.Enqueue(new(data, recieveTime));
			//}
		}

		void OnPeerConnectionEstablished(ref OnPeerConnectionEstablishedInfo data)
		{
			float recieveTime = Time.realtimeSinceStartup;
			LogDeveloper($"OnPeerConnectionEstablished: {data.SocketId} {data.RemoteUserId}");
			if (data.ConnectionType == ConnectionEstablishedType.Reconnection) return;
			establishedConnections.Enqueue(new(data, recieveTime));
		}

		void OnPeerConnectionInterrupted(ref OnPeerConnectionInterruptedInfo data)
		{
			LogDeveloper($"OnPeerConnectionInterrupted: {data.SocketId} {data.RemoteUserId}");
		}

		void OnPeerConnectionClosed(ref OnRemoteConnectionClosedInfo data)
		{
			float recieveTime = Time.realtimeSinceStartup;
			LogDeveloper($"OnPeerConnectionClosed: {data.SocketId} {data.RemoteUserId}");
			disconnections.Enqueue(new(data, recieveTime));
		}

		#endregion
		


		private ulong GetClientId(ProductUserId productUserId)
		{
			if (productUserId == HostUserId) return ServerClientId;

			var clientId = ((Utf8String)productUserId).GetHashCodeUInt64();
			remoteConnections[clientId] = productUserId;
			return clientId;
		}

		private ProductUserId GetUserId(ulong clientId)
		{
			if (clientId == ServerClientId) return HostUserId;

			return remoteConnections[clientId];
		}

		private PacketReliability GetPacketReliability(NetworkDelivery networkDelivery)
		{
			var reliability = networkDelivery switch
			{
				NetworkDelivery.Unreliable => PacketReliability.UnreliableUnordered,
				NetworkDelivery.UnreliableSequenced => throw new NotImplementedException(),
				NetworkDelivery.Reliable => PacketReliability.ReliableUnordered,
				NetworkDelivery.ReliableSequenced => PacketReliability.ReliableOrdered,
				NetworkDelivery.ReliableFragmentedSequenced => PacketReliability.ReliableOrdered,
				_ => throw new ArgumentOutOfRangeException()
			};
			return reliability;
		}

		private bool CheckResult(Result result, string nameofMethod, bool requireCompletion = true)
		{
			if (result != Result.Success && Common.IsOperationComplete(result))
			{
				LogError($"{nameofMethod} failed: {result}");
				return false;
			}
			else if (result != Result.Success)
			{
				LogWarning($"{nameofMethod} incomplete: {result}");
				return !requireCompletion;
			}
			else
			{
				return true;
			}
		}

		private void Log(object message, UnityEngine.Object context = null)
		{
			if (networkManager == null || networkManager.LogLevel <= LogLevel.Normal)
			{
				Debug.Log($"[EpicTransport] {message}", context);
			}
		}

		private void LogWarning(object message, UnityEngine.Object context = null)
		{
			if (networkManager == null || networkManager.LogLevel <= LogLevel.Error)
			{
				Debug.LogWarning($"[EpicTransport] {message}", context);
			}
		}


		private void LogError(object message, UnityEngine.Object context = null)
		{
			if (networkManager == null || networkManager.LogLevel <= LogLevel.Error)
			{
				Debug.LogError($"[EpicTransport] {message}", context);
			}
		}

		private void LogDeveloper(object message, UnityEngine.Object context = null)
		{
			if (networkManager == null || networkManager.LogLevel <= LogLevel.Developer)
			{
				Debug.Log($"[EpicTransport] {message}", context);
			}
		}


		private static SocketId GetSocketId(string category, int instanceId, string suffix)
		{
			return new SocketId()
			{
				SocketName = $"{category}.{(uint)instanceId:X8}.{suffix}",
			};
		}
	}
}
