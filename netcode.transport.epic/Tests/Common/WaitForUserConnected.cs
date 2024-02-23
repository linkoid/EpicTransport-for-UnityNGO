
namespace Netcode.Transports.Epic.Tests
{
	internal class WaitForUserConnected : EOSSDKYieldInstruction
	{
		public WaitForUserConnected(EOSSDKComponent eossdk, float timeout = 30)
			: base(eossdk, timeout)
		{ }

		protected override bool CheckIsComplete()
		{
			if (eossdk.LoginFailed)
				throw new System.Exception("EOSSDK Login failed");

			return eossdk.IsUserConnected;
		}
	}
}
