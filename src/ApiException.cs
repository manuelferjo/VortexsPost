// Error de una plataforma. Status es el código HTTP (0 = sin conexión).
using System;

namespace VortexsPost
{
	internal class ApiException : Exception
	{
		public int Status;

		public ApiException(int status, string msg)
			: base(msg)
		{
			Status = status;
		}
	}
}
