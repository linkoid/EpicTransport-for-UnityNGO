using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace SplashOfSlimes.OnlineServices
{
	internal static class Utf8StringExtensions
	{
		public static ulong GetHashCodeUInt64(this Epic.OnlineServices.Utf8String utf8String)
		{
			if (utf8String == null) return default;

			HashAlgorithm hashAlgorithm = new SHA256Managed();
			var hash = hashAlgorithm.ComputeHash(utf8String.Bytes);
			return BitConverter.ToUInt64(hash, 0);
		}
	}
}
