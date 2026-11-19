// Peticiones HTTP para las subidas: como Http.Send pero sin lanzar error por el código (lo mira
// quien llama), con las cabeceras de la respuesta (Location, Range) y enviando un trozo de un
// archivo de vídeo sin cargarlo entero en memoria, avisando del progreso.
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Web.Script.Serialization;

namespace VortexsPost
{
	internal class Resp
	{
		public int Status;

		public string Body = "";

		public WebHeaderCollection Headers = new WebHeaderCollection();

		public Dictionary<string, object> Json;

		public bool Ok
		{
			get
			{
				return Status >= 200 && Status <= 299;
			}
		}

		public string Header(string name)
		{
			return Headers[name] ?? "";
		}

		// El mensaje de error que venga en la respuesta (o el código).
		public string Error()
		{
			if (Json != null)
			{
				Dictionary<string, object> err = Http.Obj(Json, "error");
				if (err != null)
				{
					string user = Http.Str(err, "error_user_msg");
					if (user.Length > 0)
					{
						return user;
					}
					string msg = Http.Str(err, "message");
					string code = Http.Str(err, "code");
					if (msg.Length > 0)
					{
						return (code.Length > 0 && code != "ok") ? (msg + " (" + code + ")") : msg;
					}
				}
				string e = Http.Str(Json, "error");
				if (e.Length > 0)
				{
					string d = Http.Str(Json, "error_description");
					return e + ((d.Length > 0) ? (": " + d) : "");
				}
				if (Http.Str(Json, "message").Length > 0)
				{
					return Http.Str(Json, "message");
				}
			}
			string raw = Body.Trim();
			if (Status == 0)
			{
				return "sin conexión" + ((raw.Length > 0) ? (" (" + raw + ")") : "");
			}
			return "error " + Status + ((raw.Length > 0 && raw.Length < 300) ? (": " + raw) : "");
		}
	}

	internal static class Net
	{
		static Net()
		{
			try
			{
				ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
				ServicePointManager.Expect100Continue = false;
				ServicePointManager.DefaultConnectionLimit = 8;
			}
			catch
			{
			}
		}

		private static HttpWebRequest Make(string method, string url, Dictionary<string, string> headers)
		{
			HttpWebRequest r = (HttpWebRequest)WebRequest.Create(url);
			r.Method = method;
			r.UserAgent = "VortexsPost";
			r.Accept = "application/json";
			r.AllowAutoRedirect = false;
			r.Timeout = 60000;
			r.ReadWriteTimeout = 120000;
			if (headers != null)
			{
				foreach (KeyValuePair<string, string> h in headers)
				{
					if (h.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))
					{
						r.ContentType = h.Value;
					}
					else
					{
						r.Headers[h.Key] = h.Value;
					}
				}
			}
			return r;
		}

		// Petición con un cuerpo de texto (JSON o formulario) o sin cuerpo.
		public static Resp Send(string method, string url, Dictionary<string, string> headers, string contentType, string body)
		{
			try
			{
				HttpWebRequest r = Make(method, url, headers);
				if (body != null)
				{
					byte[] bytes = Encoding.UTF8.GetBytes(body);
					if (contentType != null)
					{
						r.ContentType = contentType;
					}
					r.ContentLength = bytes.Length;
					using (Stream s = r.GetRequestStream())
					{
						s.Write(bytes, 0, bytes.Length);
					}
				}
				else if (method == "POST" || method == "PUT")
				{
					r.ContentLength = 0L;
				}
				return Finish(r);
			}
			catch (Exception ex)
			{
				return new Resp { Status = 0, Body = ex.Message };
			}
		}

		// Envía count bytes del archivo a partir de offset. progress(bytes enviados de este trozo).
		// cancel: se comprueba entre bloque y bloque.
		public static Resp SendFile(string method, string url, Dictionary<string, string> headers, string contentType, string file, long offset, long count, Action<long> progress, Func<bool> cancel)
		{
			try
			{
				HttpWebRequest r = Make(method, url, headers);
				if (contentType != null)
				{
					r.ContentType = contentType;
				}
				r.ContentLength = count;
				r.AllowWriteStreamBuffering = false;
				r.SendChunked = false;
				r.Timeout = 1800000;
				r.ReadWriteTimeout = 300000;
				byte[] buffer = new byte[1048576];
				using (FileStream f = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
				{
					f.Seek(offset, SeekOrigin.Begin);
					using (Stream s = r.GetRequestStream())
					{
						long sent = 0L;
						while (sent < count)
						{
							if (cancel != null && cancel())
							{
								r.Abort();
								throw new OperationCanceledException();
							}
							int want = (int)Math.Min(buffer.Length, count - sent);
							int got = f.Read(buffer, 0, want);
							if (got <= 0)
							{
								throw new IOException("el archivo se ha acabado antes de tiempo (¿lo has cambiado mientras se subía?)");
							}
							s.Write(buffer, 0, got);
							sent += got;
							if (progress != null)
							{
								progress(sent);
							}
						}
					}
				}
				return Finish(r);
			}
			catch (OperationCanceledException)
			{
				throw;
			}
			catch (Exception ex)
			{
				return new Resp { Status = 0, Body = ex.Message };
			}
		}

		private static Resp Finish(HttpWebRequest r)
		{
			Resp resp = new Resp();
			HttpWebResponse w = null;
			try
			{
				try
				{
					w = (HttpWebResponse)r.GetResponse();
				}
				catch (WebException ex)
				{
					w = ex.Response as HttpWebResponse;
					if (w == null)
					{
						resp.Status = 0;
						resp.Body = ex.Message;
						return resp;
					}
				}
				resp.Status = (int)w.StatusCode;
				resp.Headers = w.Headers;
				using (Stream s = w.GetResponseStream())
				{
					if (s != null)
					{
						using (StreamReader sr = new StreamReader(s, Encoding.UTF8))
						{
							resp.Body = sr.ReadToEnd();
						}
					}
				}
			}
			finally
			{
				if (w != null)
				{
					w.Close();
				}
			}
			resp.Json = Parse(resp.Body);
			return resp;
		}

		public static Dictionary<string, object> Parse(string text)
		{
			if (string.IsNullOrEmpty(text) || !text.TrimStart().StartsWith("{"))
			{
				return null;
			}
			try
			{
				return new JavaScriptSerializer { MaxJsonLength = 16777216 }.DeserializeObject(text) as Dictionary<string, object>;
			}
			catch
			{
				return null;
			}
		}

		public static Dictionary<string, string> H(params string[] kv)
		{
			Dictionary<string, string> d = new Dictionary<string, string>();
			for (int i = 0; i + 1 < kv.Length; i += 2)
			{
				d[kv[i]] = kv[i + 1];
			}
			return d;
		}
	}
}
