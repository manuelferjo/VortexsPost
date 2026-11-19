// Cifra las claves y tokens que se guardan en VortexsPost.json con DPAPI de Windows: solo el mismo
// usuario de Windows en el mismo PC puede descifrarlos. Si no se puede, devuelve "" (hay que volver a ponerlos).
using System;
using System.Security.Cryptography;
using System.Text;

namespace VortexsPost
{
	internal static class Secret
	{
		public static string Protect(string s)
		{
			if (string.IsNullOrEmpty(s))
			{
				return "";
			}
			return Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(s), null, DataProtectionScope.CurrentUser));
		}

		public static string Unprotect(string s)
		{
			if (string.IsNullOrEmpty(s))
			{
				return "";
			}
			try
			{
				return Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(s), null, DataProtectionScope.CurrentUser));
			}
			catch
			{
				return "";
			}
		}
	}
}
