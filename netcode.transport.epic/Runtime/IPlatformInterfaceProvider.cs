using Epic.OnlineServices.Platform;

namespace Netcode.Transports.Epic
{
	public interface IPlatformInterfaceProvider
	{
		public PlatformInterface PlatformInterface { get; }
	}
}
