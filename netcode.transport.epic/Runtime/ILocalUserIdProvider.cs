using Epic.OnlineServices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netcode.Transports.Epic
{
	public interface ILocalUserIdProvider
	{
		public ProductUserId LocalUserId { get; }
	}
}
