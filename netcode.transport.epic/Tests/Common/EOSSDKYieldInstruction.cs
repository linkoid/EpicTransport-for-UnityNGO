using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Netcode.Transports.Epic.Tests
{
	internal abstract class EOSSDKYieldInstruction : CustomYieldInstruction
	{
		protected readonly EOSSDKComponent eossdk;
		private float timeout;
		private float startTime;

		public EOSSDKYieldInstruction(EOSSDKComponent eossdk, float timeout = 30)
		{
			this.eossdk = eossdk;
			this.timeout = timeout;
			startTime = Time.realtimeSinceStartup;
		}

		public sealed override bool keepWaiting
		{
			get
			{
				Update();

				if (CheckIsComplete())
					return false;

				if (Time.realtimeSinceStartup > startTime + timeout)
					throw new System.TimeoutException($"EOSSDK Login took longer than {timeout} seconds");

				return true;
			}
		}

		protected virtual void Update()
		{
			eossdk.Update();
		}

		protected abstract bool CheckIsComplete();
	}
}
