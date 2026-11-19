// TikTok: inicio de sesión de escritorio (Login Kit for Desktop: se abre el navegador y TikTok
// vuelve a http://127.0.0.1:<puerto>/callback/, donde escucha el programa; PKCE con SHA-256 en
// hexadecimal, como pide TikTok) y publicación directa (Content Posting API, Direct Post) subiendo
// el archivo por trozos. TikTok no programa: Vortexs Post publica a la hora elegida.
//
// Mientras la app de TikTok no pase la auditoría, TikTok solo deja publicar en "Solo yo" y con la
// cuenta en privado (ver GUIA).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace VortexsPost
{
	internal class TikTok
	{
		private const string Api = "https://open.tiktokapis.com";

		public string ClientKey = "";

		public string ClientSecret = "";

		public string Account = "";

		public string OpenId = "";

		private string access = "";

		private string refresh = "";

		private DateTime expires = DateTime.MinValue;

		private readonly object tokenLock = new object();

		// Lo último que dijo TikTok de la cuenta (privacidades que deja elegir, nombre…).
		public CreatorInfo Info;

		public class CreatorInfo
		{
			public string Nickname = "";

			public string Username = "";

			public List<string> Privacy = new List<string>();

			public bool CommentOff;

			public bool DuetOff;

			public bool StitchOff;

			public int MaxSeconds;
		}

		public bool Configured
		{
			get
			{
				return ClientKey.Length > 0 && ClientSecret.Length > 0;
			}
		}

		public bool Connected
		{
			get
			{
				return refresh.Length > 0;
			}
		}

		public static string PrivacyName(string p)
		{
			switch (p)
			{
			case "PUBLIC_TO_EVERYONE":
				return "Todos";
			case "MUTUAL_FOLLOW_FRIENDS":
				return "Amigos";
			case "FOLLOWER_OF_CREATOR":
				return "Seguidores";
			case "SELF_ONLY":
				return "Solo yo";
			default:
				return p;
			}
		}

		// ---------- Guardar ----------

		public void Load(Accounts a)
		{
			ClientKey = a.TtClient ?? "";
			ClientSecret = Secret.Unprotect(a.TtSecret);
			Account = a.TtAccount ?? "";
			OpenId = a.TtOpenId ?? "";
			string[] parts = Secret.Unprotect(a.TtToken).Split('\n');
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
				a.TtClient = ClientKey;
				a.TtSecret = Secret.Protect(ClientSecret);
				a.TtAccount = Account;
				a.TtOpenId = OpenId;
				a.TtToken = Connected ? Secret.Protect(access + "\n" + refresh + "\n" + expires.Ticks) : "";
			}
		}

		public void Logout()
		{
			lock (tokenLock)
			{
				access = (refresh = (Account = (OpenId = "")));
				expires = DateTime.MinValue;
				Info = null;
			}
		}

		// ---------- Conectar ----------

		public void Login(Func<bool> cancelled)
		{
			// Puerto libre en 127.0.0.1 (en TikTok se registra http://127.0.0.1:*/callback/).
			TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
			listener.Start();
			try
			{
				int port = ((IPEndPoint)listener.LocalEndpoint).Port;
				string redirect = "http://127.0.0.1:" + port + "/callback/";
				string verifier = RandomText(64);
				string challenge = Hex(SHA256.Create().ComputeHash(Encoding.ASCII.GetBytes(verifier)));
				string state = RandomText(24);
				string url = "https://www.tiktok.com/v2/auth/authorize/?" + Http.Form("client_key", ClientKey, "scope", "user.info.basic,video.upload,video.publish", "response_type", "code", "redirect_uri", redirect, "state", state, "code_challenge", challenge, "code_challenge_method", "S256");
				Http.OpenBrowser(url);
				DateTime deadline = DateTime.UtcNow.AddMinutes(10.0);
				string code = null;
				while (code == null)
				{
					while (!listener.Pending())
					{
						if (cancelled())
						{
							throw new OperationCanceledException();
						}
						if (DateTime.UtcNow > deadline)
						{
							throw new ApiException(0, "no ha llegado la respuesta de TikTok, vuelve a intentarlo");
						}
						Thread.Sleep(150);
					}
					using (TcpClient client = listener.AcceptTcpClient())
					{
						client.ReceiveTimeout = 5000;
						NetworkStream stream = client.GetStream();
						string line = ReadLine(stream);
						// "GET /callback/?code=…&state=… HTTP/1.1"
						string[] parts = line.Split(' ');
						string path = (parts.Length >= 2) ? parts[1] : "";
						Dictionary<string, string> q = Query(path);
						string msg;
						bool ok = false;
						string got;
						if (!path.StartsWith("/callback"))
						{
							msg = "";
						}
						else if (q.TryGetValue("error", out got))
						{
							string desc;
							q.TryGetValue("error_description", out desc);
							Reply(stream, false, "TikTok ha devuelto un error: " + got + " " + (desc ?? ""));
							throw new ApiException(0, "TikTok: " + got + ((desc != null) ? (" (" + desc + ")") : ""));
						}
						else if (!q.TryGetValue("state", out got) || got != state)
						{
							msg = "Respuesta que no corresponde a este inicio de sesión.";
						}
						else if (q.TryGetValue("code", out got) && got.Length > 0)
						{
							code = got;
							ok = true;
							msg = "¡Listo! TikTok ya está conectado. Puedes cerrar esta pestaña y volver a Vortexs Post.";
						}
						else
						{
							msg = "Falta el código de TikTok.";
						}
						Reply(stream, ok, msg);
					}
				}
				SetTokens(Http.Send("POST", Api + "/v2/oauth/token/", null, null, "application/x-www-form-urlencoded", Http.Form("client_key", ClientKey, "client_secret", ClientSecret, "code", code, "grant_type", "authorization_code", "redirect_uri", redirect, "code_verifier", verifier)));
			}
			finally
			{
				listener.Stop();
			}
			try
			{
				Dictionary<string, object> u = Http.Send("GET", Api + "/v2/user/info/?fields=open_id,display_name", Token(), null, null, null);
				Account = Http.Str(Http.Obj(Http.Obj(u, "data"), "user"), "display_name");
			}
			catch
			{
			}
			try
			{
				ReadInfo();
				if (Info != null && Info.Nickname.Length > 0)
				{
					Account = Info.Nickname + ((Info.Username.Length > 0) ? (" (@" + Info.Username + ")") : "");
				}
			}
			catch
			{
			}
			if (Account.Length == 0)
			{
				Account = "conectado";
			}
		}

		private static string ReadLine(NetworkStream s)
		{
			StringBuilder sb = new StringBuilder();
			try
			{
				while (sb.Length < 8192)
				{
					int b = s.ReadByte();
					if (b < 0 || b == 10)
					{
						break;
					}
					if (b != 13)
					{
						sb.Append((char)b);
					}
				}
			}
			catch
			{
			}
			return sb.ToString();
		}

		private static void Reply(NetworkStream s, bool ok, string msg)
		{
			string html = "<!DOCTYPE html><html lang=\"es\"><head><meta charset=\"utf-8\"><title>Vortexs Post</title></head>" +
				"<body style=\"margin:0;height:100vh;display:flex;align-items:center;justify-content:center;background:#09090c;color:#f4f4f5;font-family:Segoe UI,sans-serif\">" +
				"<div style=\"max-width:460px;padding:32px;border:1px solid #26262e;border-radius:18px;background:#111116;text-align:center\">" +
				"<div style=\"font-size:26px;font-weight:700;margin-bottom:12px\">Vortexs <span style=\"background:linear-gradient(20deg,#c09afd,#e3d5ff,#ede103);-webkit-background-clip:text;color:transparent\">Post</span></div>" +
				"<div style=\"color:" + (ok ? "#34d399" : "#ff4655") + ";font-size:15px\">" + WebUtility.HtmlEncode(msg) + "</div></div></body></html>";
			byte[] body = Encoding.UTF8.GetBytes(html);
			byte[] head = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: text/html; charset=utf-8\r\nContent-Length: " + body.Length + "\r\nConnection: close\r\n\r\n");
			try
			{
				s.Write(head, 0, head.Length);
				s.Write(body, 0, body.Length);
				s.Flush();
			}
			catch
			{
			}
		}

		private static Dictionary<string, string> Query(string path)
		{
			Dictionary<string, string> d = new Dictionary<string, string>();
			int q = path.IndexOf('?');
			if (q < 0)
			{
				return d;
			}
			foreach (string pair in path.Substring(q + 1).Split('&'))
			{
				int eq = pair.IndexOf('=');
				if (eq > 0)
				{
					d[Uri.UnescapeDataString(pair.Substring(0, eq).Replace('+', ' '))] = Uri.UnescapeDataString(pair.Substring(eq + 1).Replace('+', ' '));
				}
			}
			return d;
		}

		private static string RandomText(int n)
		{
			const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-._~";
			byte[] bytes = new byte[n];
			using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
			{
				rng.GetBytes(bytes);
			}
			StringBuilder sb = new StringBuilder(n);
			foreach (byte b in bytes)
			{
				sb.Append(chars[b % chars.Length]);
			}
			return sb.ToString();
		}

		private static string Hex(byte[] bytes)
		{
			StringBuilder sb = new StringBuilder(bytes.Length * 2);
			foreach (byte b in bytes)
			{
				sb.Append(b.ToString("x2"));
			}
			return sb.ToString();
		}

		private void SetTokens(Dictionary<string, object> r)
		{
			lock (tokenLock)
			{
				string a = Http.Str(r, "access_token");
				if (a.Length == 0)
				{
					string e = Http.Str(r, "error_description");
					throw new ApiException(0, "TikTok no devolvió el acceso" + ((e.Length > 0) ? (": " + e) : ""));
				}
				access = a;
				string rf = Http.Str(r, "refresh_token");
				if (rf.Length > 0)
				{
					refresh = rf;
				}
				string oid = Http.Str(r, "open_id");
				if (oid.Length > 0)
				{
					OpenId = oid;
				}
				expires = DateTime.UtcNow.AddSeconds(Math.Max(60, Http.Int(r, "expires_in", 86400) - 120));
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
						throw new ApiException(401, "TikTok no está conectado (pestaña Cuentas)");
					}
					try
					{
						SetTokens(Http.Send("POST", Api + "/v2/oauth/token/", null, null, "application/x-www-form-urlencoded", Http.Form("client_key", ClientKey, "client_secret", ClientSecret, "grant_type", "refresh_token", "refresh_token", refresh)));
					}
					catch (ApiException ex)
					{
						if (ex.Status == 400 || ex.Status == 401)
						{
							throw new ApiException(401, "la sesión de TikTok ha caducado: vuelve a conectarla en Cuentas");
						}
						throw;
					}
				}
				return access;
			}
		}

		// ---------- Publicar ----------

		private Dictionary<string, object> Call(string path, object body)
		{
			Resp r = Net.Send("POST", Api + path, Net.H("Authorization", "Bearer " + Token()), "application/json; charset=UTF-8", Http.ToJson(body));
			if (!r.Ok)
			{
				throw new ApiException(r.Status, "TikTok: " + r.Error());
			}
			string code = Http.Str(Http.Obj(r.Json, "error"), "code");
			if (code.Length > 0 && code != "ok")
			{
				throw new ApiException(r.Status, "TikTok: " + r.Error());
			}
			return Http.Obj(r.Json, "data") ?? new Dictionary<string, object>();
		}

		// Qué deja hacer TikTok ahora con esta cuenta (lo pide TikTok antes de cada publicación).
		public CreatorInfo ReadInfo()
		{
			Dictionary<string, object> d = Call("/v2/post/publish/creator_info/query/", new Dictionary<string, object>());
			CreatorInfo info = new CreatorInfo();
			info.Nickname = Http.Str(d, "creator_nickname");
			info.Username = Http.Str(d, "creator_username");
			object list;
			if (d.TryGetValue("privacy_level_options", out list) && list is System.Collections.IEnumerable)
			{
				foreach (object o in (System.Collections.IEnumerable)list)
				{
					if (o != null)
					{
						info.Privacy.Add(o.ToString());
					}
				}
			}
			info.CommentOff = Http.Str(d, "comment_disabled").ToLowerInvariant() == "true";
			info.DuetOff = Http.Str(d, "duet_disabled").ToLowerInvariant() == "true";
			info.StitchOff = Http.Str(d, "stitch_disabled").ToLowerInvariant() == "true";
			info.MaxSeconds = Http.Int(d, "max_video_post_duration_sec", 0);
			Info = info;
			return info;
		}

		// Publica el vídeo ya (TikTok lo procesa y lo publica). Devuelve la dirección del vídeo si TikTok la da.
		public string Publish(Target t, string file, Action<int> progress, Func<bool> cancel)
		{
			if (t.Draft)
			{
				return SendDraft(t, file, progress, cancel);
			}
			CreatorInfo info = ReadInfo();
			string privacy = t.Privacy;
			if (info.Privacy.Count > 0 && !info.Privacy.Contains(privacy))
			{
				// Sin auditoría TikTok solo ofrece SELF_ONLY: se usa lo que permita y se avisa.
				string chosen = info.Privacy.Contains("SELF_ONLY") ? "SELF_ONLY" : info.Privacy[0];
				t.Msg = "TikTok no deja «" + PrivacyName(privacy) + "» ahora mismo: publicado como «" + PrivacyName(chosen) + "»";
				privacy = chosen;
			}
			long size = new FileInfo(file).Length;
			// Trozos: hasta 64 MB se sube de una vez; si no, trozos de 32 MB (el último se lleva el resto, como pide TikTok).
			long chunk = (size <= 67108864L) ? size : 33554432L;
			long count = Math.Max(1L, size / chunk);

			Dictionary<string, object> postInfo = new Dictionary<string, object>();
			postInfo["title"] = Cut(t.Text, 2200);
			postInfo["privacy_level"] = privacy;
			postInfo["disable_comment"] = !t.Comments || info.CommentOff;
			postInfo["disable_duet"] = !t.Duet || info.DuetOff;
			postInfo["disable_stitch"] = !t.Stitch || info.StitchOff;
			if (t.Brand)
			{
				postInfo["brand_content_toggle"] = t.BrandOther;
				postInfo["brand_organic_toggle"] = !t.BrandOther;
			}
			Dictionary<string, object> source = new Dictionary<string, object>();
			source["source"] = "FILE_UPLOAD";
			source["video_size"] = size;
			source["chunk_size"] = chunk;
			source["total_chunk_count"] = count;
			Dictionary<string, object> body = new Dictionary<string, object>();
			body["post_info"] = postInfo;
			body["source_info"] = source;
			Dictionary<string, object> init = Call("/v2/post/publish/video/init/", body);
			string publishId = Http.Str(init, "publish_id");
			string uploadUrl = Http.Str(init, "upload_url");
			if (publishId.Length == 0 || uploadUrl.Length == 0)
			{
				throw new ApiException(0, "TikTok no ha dado dirección de subida");
			}

			for (long i = 0L; i < count; i++)
			{
				long first = i * chunk;
				long len = (i == count - 1L) ? (size - first) : chunk;
				long last = first + len - 1L;
				Resp up = null;
				for (int attempt = 0; attempt < 3; attempt++)
				{
					up = Net.SendFile("PUT", uploadUrl, Net.H("Content-Range", "bytes " + first + "-" + last + "/" + size), VideoType(file), file, first, len, delegate(long sent)
					{
						progress((int)((first + sent) * 95L / Math.Max(1L, size)));
					}, cancel);
					if (up.Ok)
					{
						break;
					}
					if (up.Status >= 400 && up.Status < 500)
					{
						break;
					}
					Thread.Sleep(3000 * (attempt + 1));
				}
				if (!up.Ok)
				{
					throw new ApiException(up.Status, "TikTok (subida): " + up.Error());
				}
			}

			// TikTok procesa el vídeo y lo publica: se pregunta cada 5 s (hasta 20 minutos).
			DateTime deadline = DateTime.UtcNow.AddMinutes(20.0);
			while (DateTime.UtcNow < deadline)
			{
				for (int i = 0; i < 50; i++)
				{
					if (cancel != null && cancel())
					{
						throw new OperationCanceledException();
					}
					Thread.Sleep(100);
				}
				Dictionary<string, object> q = new Dictionary<string, object>();
				q["publish_id"] = publishId;
				Dictionary<string, object> s = Call("/v2/post/publish/status/fetch/", q);
				string status = Http.Str(s, "status");
				if (status == "PUBLISH_COMPLETE")
				{
					progress(100);
					string user = (info.Username.Length > 0) ? info.Username : "";
					foreach (object id in J(s, "publicaly_available_post_id"))
					{
						if (id != null && user.Length > 0)
						{
							return "https://www.tiktok.com/@" + user + "/video/" + Convert.ToString(id, CultureInfo.InvariantCulture);
						}
					}
					return (user.Length > 0) ? ("https://www.tiktok.com/@" + user) : "";
				}
				if (status == "FAILED")
				{
					throw new ApiException(0, "TikTok no ha podido publicarlo: " + FailText(Http.Str(s, "fail_reason")));
				}
				if (status == "SEND_TO_USER_INBOX")
				{
					t.Msg = "TikTok lo ha enviado a tu bandeja de entrada para terminarlo desde la app";
					return "";
				}
				progress(96);
			}
			throw new ApiException(0, "TikTok sigue procesando el vídeo después de 20 minutos; mira en la app si ha salido");
		}

		// Borrador: el vídeo llega a la bandeja de entrada de TikTok (notificación en el móvil) y lo
		// publicas tú desde la app. TikTok no pide la cuenta en privado ni la auditoría para esto.
		// La API de borradores no lleva texto: el texto se queda en la cola para copiarlo.
		private string SendDraft(Target t, string file, Action<int> progress, Func<bool> cancel)
		{
			long size = new FileInfo(file).Length;
			long chunk = (size <= 67108864L) ? size : 33554432L;
			long count = Math.Max(1L, size / chunk);
			Dictionary<string, object> source = new Dictionary<string, object>();
			source["source"] = "FILE_UPLOAD";
			source["video_size"] = size;
			source["chunk_size"] = chunk;
			source["total_chunk_count"] = count;
			Dictionary<string, object> body = new Dictionary<string, object>();
			body["source_info"] = source;
			Dictionary<string, object> init = Call("/v2/post/publish/inbox/video/init/", body);
			string publishId = Http.Str(init, "publish_id");
			string uploadUrl = Http.Str(init, "upload_url");
			if (publishId.Length == 0 || uploadUrl.Length == 0)
			{
				throw new ApiException(0, "TikTok no ha dado dirección de subida");
			}
			UploadChunks(uploadUrl, file, size, chunk, count, progress, cancel);
			DateTime deadline = DateTime.UtcNow.AddMinutes(20.0);
			while (DateTime.UtcNow < deadline)
			{
				for (int i = 0; i < 50; i++)
				{
					if (cancel != null && cancel())
					{
						throw new OperationCanceledException();
					}
					Thread.Sleep(100);
				}
				Dictionary<string, object> q = new Dictionary<string, object>();
				q["publish_id"] = publishId;
				Dictionary<string, object> s = Call("/v2/post/publish/status/fetch/", q);
				string status = Http.Str(s, "status");
				if (status == "SEND_TO_USER_INBOX" || status == "PUBLISH_COMPLETE")
				{
					progress(100);
					t.Msg = "En tus borradores de TikTok: abre la notificación en el móvil, pega el texto (clic aquí para copiarlo) y publica";
					return "";
				}
				if (status == "FAILED")
				{
					throw new ApiException(0, "TikTok no ha aceptado el vídeo: " + FailText(Http.Str(s, "fail_reason")));
				}
				progress(96);
			}
			throw new ApiException(0, "TikTok sigue procesando el vídeo después de 20 minutos; mira tus borradores en la app");
		}

		private void UploadChunks(string uploadUrl, string file, long size, long chunk, long count, Action<int> progress, Func<bool> cancel)
		{
			for (long i = 0L; i < count; i++)
			{
				long first = i * chunk;
				long len = (i == count - 1L) ? (size - first) : chunk;
				long last = first + len - 1L;
				Resp up = null;
				for (int attempt = 0; attempt < 3; attempt++)
				{
					up = Net.SendFile("PUT", uploadUrl, Net.H("Content-Range", "bytes " + first + "-" + last + "/" + size), VideoType(file), file, first, len, delegate(long sent)
					{
						progress((int)((first + sent) * 95L / Math.Max(1L, size)));
					}, cancel);
					if (up.Ok || (up.Status >= 400 && up.Status < 500))
					{
						break;
					}
					Thread.Sleep(3000 * (attempt + 1));
				}
				if (!up.Ok)
				{
					throw new ApiException(up.Status, "TikTok (subida): " + up.Error());
				}
			}
		}

		private static List<object> J(Dictionary<string, object> d, string key)
		{
			List<object> list = new List<object>();
			object v;
			if (d != null && d.TryGetValue(key, out v) && v is System.Collections.IEnumerable && !(v is string))
			{
				foreach (object o in (System.Collections.IEnumerable)v)
				{
					list.Add(o);
				}
			}
			return list;
		}

		private static string FailText(string reason)
		{
			switch (reason)
			{
			case "file_format_check_failed":
				return "el formato del vídeo no vale (usa MP4 H.264)";
			case "duration_check_failed":
				return "el vídeo es demasiado largo o corto para tu cuenta";
			case "frame_rate_check_failed":
				return "los fps del vídeo no valen (23-60)";
			case "picture_size_check_failed":
				return "la resolución no vale (mínimo 360 px)";
			case "video_pull_failed":
			case "internal":
				return "error interno de TikTok, vuelve a intentarlo";
			case "spam_risk_too_many_posts":
				return "has publicado demasiado hoy";
			case "spam_risk_user_banned_from_posting":
				return "la cuenta no puede publicar ahora mismo";
			case "auth_removed":
				return "has quitado el permiso a la app: vuelve a conectar TikTok";
			default:
				return (reason.Length > 0) ? reason : "sin motivo";
			}
		}

		// TikTok acepta MP4, MOV y WebM.
		private static string VideoType(string file)
		{
			string ext = Path.GetExtension(file).ToLowerInvariant();
			if (ext == ".mov")
			{
				return "video/quicktime";
			}
			if (ext == ".webm")
			{
				return "video/webm";
			}
			return "video/mp4";
		}

		private static string Cut(string s, int max)
		{
			s = s ?? "";
			return (s.Length > max) ? s.Substring(0, max) : s;
		}
	}
}
