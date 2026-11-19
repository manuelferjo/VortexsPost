// Llamadas HTTP a las APIs de las plataformas: envía la petición (JSON o formulario), lee la
// respuesta como JSON y, si la plataforma devuelve un error, lo convierte en ApiException con un
// mensaje legible. También ayudas para leer el JSON y para abrir el navegador.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Web.Script.Serialization;

namespace VortexsPost
{
	internal static class Http
	{
		private static readonly JavaScriptSerializer json;

		static Http()
		{
			json = new JavaScriptSerializer
			{
				MaxJsonLength = 8388608
			};
			try
			{
				// Windows antiguos no activan TLS 1.2 por defecto.
				ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
			}
			catch
			{
			}
		}

		// Formulario "a=1&b=2" a partir de pares nombre, valor (los valores null se omiten).
		public static string Form(params string[] kv)
		{
			StringBuilder sb = new StringBuilder();
			for (int i = 0; i + 1 < kv.Length; i += 2)
			{
				if (kv[i + 1] != null)
				{
					if (sb.Length > 0)
					{
						sb.Append('&');
					}
					sb.Append(Uri.EscapeDataString(kv[i])).Append('=').Append(Uri.EscapeDataString(kv[i + 1]));
				}
			}
			return sb.ToString();
		}

		public static string ToJson(object o)
		{
			return json.Serialize(o);
		}

		// Hace la petición y devuelve la respuesta JSON (null si no es un objeto JSON).
		// Si el código no es 2xx, lanza ApiException (estado 0 = sin conexión).
		public static Dictionary<string, object> Send(string method, string url, string bearer, Dictionary<string, string> headers, string contentType, string body)
		{
			HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
			request.Method = method;
			request.UserAgent = "VortexsPost";
			request.Accept = "application/json";
			request.Timeout = 20000;
			request.ReadWriteTimeout = 20000;
			if (bearer != null)
			{
				request.Headers["Authorization"] = "Bearer " + bearer;
			}
			if (headers != null)
			{
				foreach (KeyValuePair<string, string> header in headers)
				{
					request.Headers[header.Key] = header.Value;
				}
			}
			if (body != null)
			{
				byte[] bytes = Encoding.UTF8.GetBytes(body);
				request.ContentType = contentType;
				request.ContentLength = bytes.Length;
				using (Stream stream = request.GetRequestStream())
				{
					stream.Write(bytes, 0, bytes.Length);
				}
			}
			int statusCode;
			string text;
			try
			{
				using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
				{
					statusCode = (int)response.StatusCode;
					text = Read(response);
				}
			}
			catch (WebException ex)
			{
				// Los errores HTTP (4xx, 5xx) llegan como excepción, pero traen respuesta.
				HttpWebResponse errorResponse = ex.Response as HttpWebResponse;
				if (errorResponse == null)
				{
					throw new ApiException(0, "sin conexión (" + ex.Message + ")");
				}
				statusCode = (int)errorResponse.StatusCode;
				text = Read(errorResponse);
				errorResponse.Close();
			}
			Dictionary<string, object> result = null;
			if (!string.IsNullOrEmpty(text) && text.TrimStart().StartsWith("{"))
			{
				try
				{
					result = json.DeserializeObject(text) as Dictionary<string, object>;
				}
				catch
				{
				}
			}
			if (statusCode < 200 || statusCode > 299)
			{
				throw new ApiException(statusCode, ErrorText(result, text, statusCode));
			}
			return result;
		}

		private static string Read(HttpWebResponse r)
		{
			using (Stream stream = r.GetResponseStream())
			{
				if (stream == null)
				{
					return "";
				}
				using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
				{
					return reader.ReadToEnd();
				}
			}
		}

		// El mensaje de error de la respuesta, venga como venga según la plataforma:
		// "message", "error" + "error_description", o un objeto "error": { "message": … }.
		private static string ErrorText(Dictionary<string, object> d, string text, int status)
		{
			if (d != null)
			{
				if (d.ContainsKey("message") && d["message"] is string && ((string)d["message"]).Length > 0)
				{
					return (string)d["message"];
				}
				string error = Str(d, "error");
				if (error.Length > 0 && !(d["error"] is Dictionary<string, object>))
				{
					string description = Str(d, "error_description");
					return error + ((description.Length > 0) ? (": " + description) : "");
				}
				foreach (string key in new string[3] { "message", "error_description", "error" })
				{
					object value;
					if (d.TryGetValue(key, out value))
					{
						if (value is string && ((string)value).Length > 0)
						{
							return (string)value;
						}
						Dictionary<string, object> inner = value as Dictionary<string, object>;
						if (inner != null && inner.ContainsKey("message"))
						{
							return Str(inner, "message");
						}
					}
				}
			}
			// Sin JSON: el código y, si es corta, la respuesta tal cual.
			string raw = (text ?? "").Trim();
			return "error " + status + ((raw.Length > 0 && raw.Length < 200) ? (": " + raw) : "");
		}

		// ---------- Leer el JSON ----------

		// Valor como texto ("" si no está).
		public static string Str(Dictionary<string, object> d, string k)
		{
			object value;
			if (d == null || !d.TryGetValue(k, out value) || value == null)
			{
				return "";
			}
			return Convert.ToString(value, CultureInfo.InvariantCulture);
		}

		// Objeto dentro del objeto (null si no está).
		public static Dictionary<string, object> Obj(Dictionary<string, object> d, string k)
		{
			object value;
			if (d == null || !d.TryGetValue(k, out value))
			{
				return null;
			}
			return value as Dictionary<string, object>;
		}

		// Lista de objetos (vacía si no está).
		public static List<Dictionary<string, object>> Arr(Dictionary<string, object> d, string k)
		{
			List<Dictionary<string, object>> list = new List<Dictionary<string, object>>();
			object value;
			if (d == null || !d.TryGetValue(k, out value))
			{
				return list;
			}
			IEnumerable items = value as IEnumerable;
			if (items == null || value is string)
			{
				return list;
			}
			foreach (object item in items)
			{
				Dictionary<string, object> obj = item as Dictionary<string, object>;
				if (obj != null)
				{
					list.Add(obj);
				}
			}
			return list;
		}

		public static int Int(Dictionary<string, object> d, string k, int def)
		{
			int result;
			if (!int.TryParse(Str(d, k), out result))
			{
				return def;
			}
			return result;
		}

		// Abre la dirección en el navegador predeterminado.
		public static void OpenBrowser(string url)
		{
			try
			{
				ProcessStartInfo startInfo = new ProcessStartInfo(url);
				startInfo.UseShellExecute = true;
				Process.Start(startInfo);
			}
			catch
			{
			}
		}
	}
}
