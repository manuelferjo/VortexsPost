// Instagram (cuenta profesional: Creador o Empresa): publicación de reels con la API oficial de
// publicación de contenido. Se conecta pegando un token de acceso:
//   · "IGAA…" (API de Instagram con inicio de sesión de Instagram, el que da el panel de la app de
//     Meta en "Generar token"): dura 60 días y el programa lo renueva solo cada semana.
//   · "EAA…" (inicio de sesión de Facebook, cuenta de Instagram unida a una página): se busca la
//     página con Instagram y se usa su token.
// Subida: contenedor con upload_type=resumable, el vídeo se envía a rupload.facebook.com, se espera a
// que Instagram lo procese (estado FINISHED) y se publica con media_publish. Instagram no programa:
// Vortexs Post sube el vídeo unos minutos antes y lo publica a la hora elegida.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;

namespace VortexsPost
{
	internal class Instagram
	{
		public const string Version = "v25.0";

		public string Token = "";

		public string UserId = "";

		public string Account = "";

		// graph.instagram.com o graph.facebook.com, según el tipo de token.
		public string Host = "";

		public DateTime Since = DateTime.MinValue;

		public DateTime Expires = DateTime.MinValue;

		private readonly object tokenLock = new object();

		public bool Connected
		{
			get
			{
				return Token.Length > 0 && UserId.Length > 0;
			}
		}

		private string Base
		{
			get
			{
				return "https://" + ((Host.Length > 0) ? Host : "graph.instagram.com") + "/" + Version;
			}
		}

		public void Load(Accounts a)
		{
			Token = Secret.Unprotect(a.IgToken);
			UserId = a.IgUser ?? "";
			Account = a.IgAccount ?? "";
			Host = a.IgHost ?? "";
			Since = (a.IgSince > 0) ? new DateTime(a.IgSince, DateTimeKind.Utc) : DateTime.MinValue;
			Expires = (a.IgExpires > 0) ? new DateTime(a.IgExpires, DateTimeKind.Utc) : DateTime.MinValue;
		}

		public void Save(Accounts a)
		{
			lock (tokenLock)
			{
				a.IgToken = Secret.Protect(Token);
				a.IgUser = UserId;
				a.IgAccount = Account;
				a.IgHost = Host;
				a.IgSince = (Since == DateTime.MinValue) ? 0L : Since.Ticks;
				a.IgExpires = (Expires == DateTime.MinValue) ? 0L : Expires.Ticks;
			}
		}

		public void Logout()
		{
			lock (tokenLock)
			{
				Token = (UserId = (Account = (Host = "")));
				Since = (Expires = DateTime.MinValue);
			}
		}

		// Comprueba el token pegado y saca la cuenta de Instagram.
		public void Connect(string token)
		{
			token = (token ?? "").Trim();
			if (token.Length < 20)
			{
				throw new ApiException(0, "pega el token de acceso completo");
			}
			if (token.StartsWith("EAA"))
			{
				// Facebook: páginas del usuario con su cuenta de Instagram.
				Resp r = Net.Send("GET", "https://graph.facebook.com/" + Version + "/me/accounts?fields=name,access_token,instagram_business_account%7Bid,username%7D&access_token=" + Uri.EscapeDataString(token), null, null, null);
				if (!r.Ok)
				{
					throw new ApiException(r.Status, "Instagram: " + r.Error());
				}
				foreach (Dictionary<string, object> page in Http.Arr(r.Json, "data"))
				{
					Dictionary<string, object> ig = Http.Obj(page, "instagram_business_account");
					if (ig != null && Http.Str(ig, "id").Length > 0)
					{
						lock (tokenLock)
						{
							Host = "graph.facebook.com";
							UserId = Http.Str(ig, "id");
							Account = "@" + Http.Str(ig, "username");
							// El token de la página (si sale de un token largo de usuario, no caduca).
							Token = (Http.Str(page, "access_token").Length > 0) ? Http.Str(page, "access_token") : token;
							Since = DateTime.UtcNow;
							Expires = DateTime.MinValue;
						}
						return;
					}
				}
				throw new ApiException(0, "ninguna de tus páginas de Facebook tiene una cuenta de Instagram profesional unida");
			}
			Resp me = Net.Send("GET", "https://graph.instagram.com/" + Version + "/me?fields=user_id,username,account_type&access_token=" + Uri.EscapeDataString(token), null, null, null);
			if (!me.Ok)
			{
				throw new ApiException(me.Status, "Instagram: " + me.Error());
			}
			string id = Http.Str(me.Json, "user_id");
			if (id.Length == 0)
			{
				id = Http.Str(me.Json, "id");
			}
			lock (tokenLock)
			{
				Host = "graph.instagram.com";
				UserId = id;
				Account = "@" + Http.Str(me.Json, "username");
				Token = token;
				Since = DateTime.UtcNow;
				// Los tokens de Instagram duran 60 días desde que se crearon (se renuevan solos, ver Refresh).
				Expires = DateTime.UtcNow.AddDays(59.0);
			}
		}

		// Token válido. Los "IGAA" se renuevan una vez a la semana (Instagram exige que tengan al menos 24 h).
		private string Tok()
		{
			lock (tokenLock)
			{
				if (Token.Length == 0)
				{
					throw new ApiException(401, "Instagram no está conectado (pestaña Cuentas)");
				}
				if (Host == "graph.instagram.com" && Since != DateTime.MinValue && DateTime.UtcNow - Since > TimeSpan.FromDays(7.0))
				{
					Refresh();
				}
				return Token;
			}
		}

		public bool Refresh()
		{
			lock (tokenLock)
			{
				if (Host != "graph.instagram.com" || Token.Length == 0)
				{
					return false;
				}
				Resp r = Net.Send("GET", "https://graph.instagram.com/refresh_access_token?grant_type=ig_refresh_token&access_token=" + Uri.EscapeDataString(Token), null, null, null);
				string t = Http.Str(r.Json, "access_token");
				if (!r.Ok || t.Length == 0)
				{
					return false;
				}
				Token = t;
				Since = DateTime.UtcNow;
				Expires = DateTime.UtcNow.AddSeconds(Http.Int(r.Json, "expires_in", 5184000));
				return true;
			}
		}

		// Sube el vídeo y espera a que Instagram lo procese. Devuelve el id del contenedor (listo para publicar).
		public string Prepare(Target t, string file, Action<int> progress, Func<bool> cancel)
		{
			string token = Tok();
			long size = new FileInfo(file).Length;
			string form = Http.Form("media_type", "REELS", "upload_type", "resumable", "caption", Cut(t.Text, 2200), "share_to_feed", t.Feed ? "true" : "false", "access_token", token);
			Resp c = Net.Send("POST", Base + "/" + UserId + "/media", null, "application/x-www-form-urlencoded", form);
			if (!c.Ok)
			{
				throw new ApiException(c.Status, "Instagram: " + c.Error());
			}
			string container = Http.Str(c.Json, "id");
			string uri = Http.Str(c.Json, "uri");
			if (container.Length == 0)
			{
				throw new ApiException(0, "Instagram no ha creado la publicación");
			}
			if (uri.Length == 0)
			{
				uri = "https://rupload.facebook.com/ig-api-upload/" + Version + "/" + container;
			}
			Resp up = null;
			for (int attempt = 0; attempt < 3; attempt++)
			{
				up = Net.SendFile("POST", uri, Net.H("Authorization", "OAuth " + token, "offset", "0", "file_size", size.ToString(CultureInfo.InvariantCulture)), "application/octet-stream", file, 0L, size, delegate(long sent)
				{
					progress((int)(sent * 85L / Math.Max(1L, size)));
				}, cancel);
				if (up.Ok || (up.Status >= 400 && up.Status < 500))
				{
					break;
				}
				Thread.Sleep(3000 * (attempt + 1));
			}
			if (!up.Ok)
			{
				throw new ApiException(up.Status, "Instagram (subida): " + up.Error());
			}
			// Instagram procesa el vídeo: se pregunta cada 5 s (hasta 15 minutos).
			DateTime deadline = DateTime.UtcNow.AddMinutes(15.0);
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
				Resp s = Net.Send("GET", Base + "/" + container + "?fields=status_code,status&access_token=" + Uri.EscapeDataString(token), null, null, null);
				string code = Http.Str(s.Json, "status_code");
				if (code == "FINISHED")
				{
					progress(100);
					return container;
				}
				if (code == "ERROR" || code == "EXPIRED")
				{
					string why = Http.Str(s.Json, "status");
					throw new ApiException(0, "Instagram no ha aceptado el vídeo" + ((why.Length > 0) ? (": " + why) : "") + " (reels: MP4, 3 s a 15 min, 9:16 recomendado)");
				}
				progress(90);
			}
			throw new ApiException(0, "Instagram sigue procesando el vídeo después de 15 minutos");
		}

		// Publica un contenedor ya procesado. Devuelve el enlace del reel.
		public string Publish(string container)
		{
			string token = Tok();
			Resp p = Net.Send("POST", Base + "/" + UserId + "/media_publish", null, "application/x-www-form-urlencoded", Http.Form("creation_id", container, "access_token", token));
			if (!p.Ok)
			{
				throw new ApiException(p.Status, "Instagram: " + p.Error());
			}
			string media = Http.Str(p.Json, "id");
			if (media.Length > 0)
			{
				Resp l = Net.Send("GET", Base + "/" + media + "?fields=permalink&access_token=" + Uri.EscapeDataString(token), null, null, null);
				string link = Http.Str(l.Json, "permalink");
				if (link.Length > 0)
				{
					return link;
				}
			}
			return "https://www.instagram.com/" + Account.TrimStart('@') + "/";
		}

		private static string Cut(string s, int max)
		{
			s = s ?? "";
			return (s.Length > max) ? s.Substring(0, max) : s;
		}
	}
}
