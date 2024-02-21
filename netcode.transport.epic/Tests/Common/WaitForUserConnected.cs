using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Netcode.Transports.Epic.Tests
{
	internal class WaitForUserConnected : CustomYieldInstruction
	{
		private EOSSDKComponent eossdk;
		private float timeout;
		private float startTime;

		public override bool keepWaiting => !CheckIsUserConnected();

		public WaitForUserConnected(EOSSDKComponent eossdk, float timeout = 30)
		{
			this.eossdk = eossdk;
			this.timeout = timeout;
			startTime = Time.realtimeSinceStartup;
		}

		private bool CheckIsUserConnected()
		{
			eossdk.Update();
			if (eossdk.IsUserConnected) return true;

			if (eossdk.LoginFailed)
				throw new System.Exception("EOSSDK Login failed");

			if (Time.realtimeSinceStartup > startTime + timeout)
				throw new System.TimeoutException($"EOSSDK Login took longer than {timeout} seconds");

			return false;
		}
	}
}
