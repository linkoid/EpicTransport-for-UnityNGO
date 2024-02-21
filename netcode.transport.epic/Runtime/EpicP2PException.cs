using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Netcode.Transports.Epic
{
	internal class EpicP2PException : Exception
	{
		public EpicP2PException() : base() { }
		public EpicP2PException(string message) : base(message) { }
	}
}
