// YouTube: inicio de sesión con código (igual que en Vortexs Live: sirven el mismo ID y secreto de
// cliente de Google, tipo "TVs y dispositivos de entrada limitada") y subida reanudable de vídeos.
// La programación la hace YouTube: el vídeo se sube como privado con publishAt y YouTube lo
// publica solo a esa hora, aunque el PC esté apagado.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;

namespace VortexsPost
{
	internal class YouTube
	{
		private const string Scope = "https://www.googleapis.com/auth/youtube";

		private const string AuthBase = "https://oauth2.googleapis.com";

		public static readonly string[][] Categories = new string[][]
		{
			new string[] { "20", "Videojuegos" },
			new string[] { "22", "Gente y blogs" },
			new string[] { "24", "Entretenimiento" },
			new string[] { "23", "Humor" },
			new string[] { "10", "Música" },
			new string[] { "17", "Deportes" },
			new string[] { "28", "Ciencia y tecnología" },
			new string[] { "27", "Educación" },
			new string[] { "26", "Consejos y estilo" },
			new string[] { "1", "Cine y animación" },
			new string[] { "19", "Viajes y eventos" },
			new string[] { "15", "Animales" }
		};

		public string ClientId = "";

		public string ClientSecret = "";

		public string Account = "";

		private string access = "";

		private string refresh = "";

		private DateTime expires = DateTime.MinValue;

		private readonly object tokenLock = new object();

		public bool Configured
		{
			get
			{
				return ClientId.Length > 0 && ClientSecret.Length > 0;
			}
		}

		public bool Connected
		{
			get
			{
				return refresh.Length > 0;
			}
		}

		public static string CategoryName(string id)
		{
			foreach (string[] c in Categories)
			{
				if (c[0] == id)
				{
					return c[1];
				}
			}
			return "Videojuegos";
		}

		// ---------- Guardar ----------

		public void Load(Accounts a)
		{
			ClientId = a.YtClient ?? "";
			ClientSecret = Secret.Unprotect(a.YtSecret);
			Account = a.YtAccount ?? "";
			string[] parts = Secret.Unprotect(a.YtToken).Split('\n');
			if (parts.Length >= 3)
			{
				access = parts[0];
				refresh = parts[1];
				long ticks;
				expires = long.TryParse(parts[2], out ticks) ? new DateTime(ticks, DateTimeKind.Utc) : DateTime.MinValue;
			}
		}

		public void Save(Accounts a)
		{
			lock (tokenLock)
			{
				a.YtClient = ClientId;
				a.YtSecret = Secret.Protect(ClientSecret);
				a.YtAccount = Account;
				a.YtToken = Connected ? Secret.Protect(access + "\n" + refresh + "\n" + expires.Ticks) : "";
			}
		}

		public void Logout()
		{
			lock (tokenLock)
			{
				access = (refresh = (Account = ""));
				expires = DateTime.MinValue;
			}
		}

		// ---------- Conectar ----------

		// showCode(código, dirección) enseña el código al usuario; cancelled() dice si ha cerrado la ventana.
		public void Login(Action<string, string> showCode, Func<bool> cancelled)
		{
			Dictionary<string, object> d = Http.Send("POST", AuthBase + "/device/code", null, null, "application/x-www-form-urlencoded", Http.Form("client_id", ClientId, "scope", Scope));
			string url = Http.Str(d, "verification_url");
			if (url.Length == 0)
			{
				url = Http.Str(d, "verification_uri");
			}
			showCode(Http.Str(d, "user_code"), url);
			int every = Math.Max(2, Http.Int(d, "interval", 5));
			DateTime deadline = DateTime.UtcNow.AddSeconds(Math.Max(60, Http.Int(d, "expires_in", 1800)));
			while (DateTime.UtcNow < deadline)
			{
				for (int i = 0; i < every * 10; i++)
				{
					if (cancelled())
					{
						throw new OperationCanceledException();
					}
					Thread.Sleep(100);
				}
				try
				{
					SetTokens(Http.Send("POST", AuthBase + "/token", null, null, "application/x-www-form-urlencoded", Http.Form("client_id", ClientId, "client_secret", ClientSecret, "device_code", Http.Str(d, "device_code"), "grant_type", "urn:ietf:params:oauth:grant-type:device_code")));
					LoadAccount();
					return;
				}
				catch (ApiException ex)
				{
					string m = ex.Message.ToLowerInvariant();
					if (m.Contains("pending"))
					{
						continue;
					}
					if (m.Contains("slow_down"))
					{
						every += 5;
						continue;
					}
					if (m.Contains("denied"))
					{
						throw new ApiException(ex.Status, "has rechazado el acceso");
					}
					throw;
				}
			}
			throw new ApiException(0, "el código ha caducado, vuelve a intentarlo");
		}

		private void SetTokens(Dictionary<string, object> r)
		{
			lock (tokenLock)
			{
				string a = Http.Str(r, "access_token");
				if (a.Length == 0)
				{
					throw new ApiException(0, "Google no devolvió el acceso");
				}
				access = a;
				string rf = Http.Str(r, "refresh_token");
				if (rf.Length > 0)
				{
					refresh = rf;
				}
				expires = DateTime.UtcNow.AddSeconds(Math.Max(60, Http.Int(r, "expires_in", 3600) - 60));
			}
		}

		public string Token()
		{
			lock (tokenLock)
			{
				if (access.Length == 0 || DateTime.UtcNow >= expires)
				{
					if (refresh.Length == 0)
					{
						throw new ApiException(401, "YouTube no está conectado (pestaña Cuentas)");
					}
					try
					{
						SetTokens(Http.Send("POST", AuthBase + "/token", null, null, "application/x-www-form-urlencoded", Http.Form("client_id", ClientId, "client_secret", ClientSecret, "refresh_token", refresh, "grant_type", "refresh_token")));
					}
					catch (ApiException ex)
					{
						if (ex.Status == 400 || ex.Status == 401)
						{
							throw new ApiException(401, "la sesión de YouTube ha caducado: vuelve a conectarla en Cuentas");
						}
						throw;
					}
				}
				return access;
			}
		}

		private void LoadAccount()
		{
			Dictionary<string, object> d = Http.Send("GET", "https://www.googleapis.com/youtube/v3/channels?part=snippet&mine=true", Token(), null, null, null);
			List<Dictionary<string, object>> items = Http.Arr(d, "items");
			Account = (items.Count > 0) ? Http.Str(Http.Obj(items[0], "snippet"), "title") : "(sin canal)";
		}

		// ---------- Subir ----------

		// Sube el vídeo. Devuelve el id. progress(0-100). Si t.When > 0 queda programado en YouTube.
		public string Upload(Target t, string file, Action<int> progress, Func<bool> cancel)
		{
			long size = new System.IO.FileInfo(file).Length;
			Dictionary<string, object> snippet = new Dictionary<string, object>();
			snippet["title"] = CleanTitle(t.Title);
			snippet["description"] = Clean(t.Text, 5000);
			snippet["categoryId"] = (t.Category.Length > 0) ? t.Category : "20";
			List<string> tags = new List<string>();
			foreach (string tag in (t.Tags ?? "").Split(','))
			{
				string clean = tag.Trim().TrimStart('#').Trim();
				if (clean.Length > 0 && !tags.Contains(clean))
				{
					tags.Add(clean);
				}
			}
			if (tags.Count > 0)
			{
				snippet["tags"] = tags;
			}
			Dictionary<string, object> status = new Dictionary<string, object>();
			if (t.When > 0)
			{
				// Para programar, YouTube exige privado + publishAt (y lo pone público a esa hora).
				status["privacyStatus"] = "private";
				status["publishAt"] = new DateTime(t.When, DateTimeKind.Utc).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
			}
			else
			{
				status["privacyStatus"] = t.Privacy;
			}
			status["selfDeclaredMadeForKids"] = t.Kids;
			status["embeddable"] = true;
			Dictionary<string, object> body = new Dictionary<string, object>();
			body["snippet"] = snippet;
			body["status"] = status;

			// 1) Abrir la subida: YouTube responde con la dirección a la que enviar el vídeo.
			Resp init = Net.Send("POST", "https://www.googleapis.com/upload/youtube/v3/videos?uploadType=resumable&part=snippet,status", Net.H("Authorization", "Bearer " + Token(), "X-Upload-Content-Length", size.ToString(CultureInfo.InvariantCulture), "X-Upload-Content-Type", "video/*"), "application/json; charset=UTF-8", Http.ToJson(body));
			if (!init.Ok)
			{
				throw new ApiException(init.Status, "YouTube: " + init.Error());
			}
			string session = init.Header("Location");
			if (session.Length == 0)
			{
				throw new ApiException(0, "YouTube no ha dado dirección de subida");
			}

			// 2) Enviar el vídeo. Si se corta, se pregunta cuánto ha llegado y se sigue desde ahí (hasta 5 veces).
			long offset = 0L;
			for (int attempt = 0; ; attempt++)
			{
				long start = offset;
				Dictionary<string, string> h = Net.H("Authorization", "Bearer " + Token());
				if (start > 0)
				{
					h["Content-Range"] = "bytes " + start + "-" + (size - 1L) + "/" + size;
				}
				Resp up = Net.SendFile("PUT", session, h, "video/*", file, start, size - start, delegate(long sent)
				{
					progress((int)((start + sent) * 100L / Math.Max(1L, size)));
				}, cancel);
				if (up.Ok)
				{
					string id = Http.Str(up.Json, "id");
					if (id.Length == 0)
					{
						throw new ApiException(0, "YouTube no ha devuelto el vídeo");
					}
					// Proyectos de API sin auditar: YouTube deja el vídeo privado aunque se pida público.
					string got = Http.Str(Http.Obj(up.Json, "status"), "privacyStatus");
					if (t.When == 0 && t.Privacy != "private" && got == "private")
					{
						t.Msg = "YouTube lo ha dejado privado: tu proyecto de Google aún no ha pasado la auditoría de la API (ver GUIA)";
					}
					return id;
				}
				if (up.Status >= 400 && up.Status < 500 && up.Status != 408)
				{
					throw new ApiException(up.Status, "YouTube: " + up.Error());
				}
				if (attempt >= 5)
				{
					throw new ApiException(up.Status, "YouTube: la subida se ha cortado varias veces (" + up.Error() + ")");
				}
				Thread.Sleep(3000 * (attempt + 1));
				// ¿Cuánto ha recibido YouTube? 308 + Range: bytes=0-N.
				Resp q = Net.Send("PUT", session, Net.H("Authorization", "Bearer " + Token(), "Content-Range", "bytes */" + size), null, "");
				if (q.Ok)
				{
					string id2 = Http.Str(q.Json, "id");
					if (id2.Length > 0)
					{
						return id2;
					}
				}
				offset = 0L;
				string range = q.Header("Range");
				int dash = range.LastIndexOf('-');
				long last;
				if (q.Status == 308 && dash > 0 && long.TryParse(range.Substring(dash + 1), out last))
				{
					offset = last + 1L;
				}
			}
		}

		// YouTube no admite < ni > en el título y como máximo 100 caracteres.
		private static string CleanTitle(string s)
		{
			string t = Clean(s, 100).Replace('\n', ' ').Replace('\r', ' ').Trim();
			if (t.Length == 0)
			{
				t = "Vídeo";
			}
			return t;
		}

		private static string Clean(string s, int max)
		{
			string t = (s ?? "").Replace("<", "‹").Replace(">", "›");
			if (t.Length > max)
			{
				t = t.Substring(0, max);
			}
			return t;
		}
	}
}
