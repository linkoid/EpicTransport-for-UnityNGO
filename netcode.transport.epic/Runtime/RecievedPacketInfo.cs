using Epic.OnlineServices.P2P;
using Epic.OnlineServices;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Netcode.Transports.Epic
{
	internal struct RecievedPacketInfo
	{
		public SocketId SocketId;
		public ProductUserId RemoteUserId;
		public byte Channel;

		public override bool Equals(object obj)
		{
			if (obj is not RecievedPacketInfo other)
			{
				return false;
			}
			return Equals(other);
		}

		public bool Equals(RecievedPacketInfo other)
		{
			return SocketId.SocketName.Equals(other.SocketId.SocketName)
				&& RemoteUserId.ToString().Equals(other.RemoteUserId.ToString())
				&& Channel.Equals(other.Channel);
		}

		public override int GetHashCode()
		{
			return HashCode.Combine(SocketId, RemoteUserId, Channel);
		}

		public static bool operator ==(RecievedPacketInfo left, RecievedPacketInfo right)
		{
			return left.Equals(right);
		}

		public static bool operator !=(RecievedPacketInfo left, RecievedPacketInfo right)
		{
			return !(left == right);
		}
	}
}
