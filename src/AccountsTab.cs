// Pestaña Cuentas: conectar YouTube (código en google.com/device), TikTok (se abre el navegador y
// vuelve solo al programa) e Instagram (pegando un token de acceso). Los secretos y tokens se
// guardan cifrados con DPAPI en VortexsPost.json.
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace VortexsPost
{
	internal partial class MainForm
	{
		private Field fldYtId;

		private Field fldYtSecret;

		private Field fldTtKey;

		private Field fldTtSecret;

		private Field fldIgToken;

		private VButton btnYtConn;

		private VButton btnTtConn;

		private VButton btnIgConn;

		private PaintBox ytStatus;

		private PaintBox ttStatus;

		private PaintBox igStatus;

		private void BuildAccounts(int top)
		{
			// YouTube
			Card yt = NewCard("YouTube", "Google Cloud · YouTube Data API", 20f, top, 244f, 410f, PageAccounts);
			Label(yt, "CLIENT ID", 16f, 60f, 212f);
			fldYtId = NewField(yt, 16f, 78f, 212f, 32f, false);
			Label(yt, "CLIENT SECRET", 16f, 118f, 212f);
			fldYtSecret = NewField(yt, 16f, 136f, 212f, 32f, false);
			fldYtSecret.Box.UseSystemPasswordChar = true;
			btnYtConn = Btn(yt, "Conectar", ButtonKind.Primary, 16f, 184f, 212f, 36f);
			ytStatus = Box(yt, 16f, 232f, 212f, 120f, delegate(Graphics g, Rectangle r)
			{
				PaintAccount(g, r, pub.Yt.Connected, pub.Yt.Account, pub.Yt.Connected ? "Programa con publishAt: YouTube publica aunque el PC esté apagado." : "Sirven el mismo ID y secreto que usas en Vortexs Live (cliente «TVs y dispositivos de entrada limitada»).");
			});
			VButton ytLink = Btn(yt, "Abrir Google Cloud ↗", ButtonKind.Link, 16f, 366f, 212f, 24f);
			ytLink.Click += delegate
			{
				Open("https://console.cloud.google.com/apis/credentials");
			};
			btnYtConn.Click += delegate
			{
				ConnectYouTube();
			};

			// TikTok
			Card tt = NewCard("TikTok", "TikTok for Developers · Content Posting", 278f, top, 244f, 410f, PageAccounts);
			Label(tt, "CLIENT KEY", 16f, 60f, 212f);
			fldTtKey = NewField(tt, 16f, 78f, 212f, 32f, false);
			Label(tt, "CLIENT SECRET", 16f, 118f, 212f);
			fldTtSecret = NewField(tt, 16f, 136f, 212f, 32f, false);
			fldTtSecret.Box.UseSystemPasswordChar = true;
			btnTtConn = Btn(tt, "Conectar", ButtonKind.Primary, 16f, 184f, 212f, 36f);
			ttStatus = Box(tt, 16f, 232f, 212f, 120f, delegate(Graphics g, Rectangle r)
			{
				PaintAccount(g, r, pub.Tt.Connected, pub.Tt.Account, pub.Tt.Connected ? "Sin la auditoría de TikTok solo se puede publicar como «Solo yo» y con la cuenta en privado." : "En tu app de TikTok: Login Kit (Desktop) con la dirección http://127.0.0.1:*/callback/ y Content Posting API con Direct Post.");
			});
			VButton ttLink = Btn(tt, "Abrir TikTok Developers ↗", ButtonKind.Link, 16f, 366f, 212f, 24f);
			ttLink.Click += delegate
			{
				Open("https://developers.tiktok.com/apps/");
			};
			btnTtConn.Click += delegate
			{
				ConnectTikTok();
			};

			// Instagram
			Card ig = NewCard("Instagram", "Meta for Developers · cuenta profesional", 536f, top, 244f, 410f, PageAccounts);
			Label(ig, "TOKEN DE ACCESO", 16f, 60f, 212f);
			fldIgToken = NewField(ig, 16f, 78f, 212f, 32f, false);
			fldIgToken.Box.UseSystemPasswordChar = true;
			Hint(ig, 16f, 116f, 212f, 62f, delegate
			{
				return "Lo da tu app de Meta: Instagram → «Generar token» junto a tu cuenta. Empieza por IGAA.";
			});
			btnIgConn = Btn(ig, "Conectar", ButtonKind.Primary, 16f, 184f, 212f, 36f);
			igStatus = Box(ig, 16f, 232f, 212f, 120f, delegate(Graphics g, Rectangle r)
			{
				string extra;
				if (!pub.Ig.Connected)
				{
					extra = "Tu cuenta de Instagram tiene que ser de Creador o de Empresa.";
				}
				else if (pub.Ig.Expires != DateTime.MinValue)
				{
					extra = "El token se renueva solo cada semana (caduca el " + pub.Ig.Expires.ToLocalTime().ToString("dd/MM/yyyy") + " si no abres el programa).";
				}
				else
				{
					extra = "Conectado con el token de la página de Facebook.";
				}
				PaintAccount(g, r, pub.Ig.Connected, pub.Ig.Account, extra);
			});
			VButton igLink = Btn(ig, "Abrir Meta for Developers ↗", ButtonKind.Link, 16f, 366f, 212f, 24f);
			igLink.Click += delegate
			{
				Open("https://developers.facebook.com/apps/");
			};
			btnIgConn.Click += delegate
			{
				ConnectInstagram();
			};

			// Aviso de las auditorías.
			Card note = NewCard("Antes de publicar en público", "", 20f, top + 420, 760f, 136f, PageAccounts);
			Hint(note, 20f, 44f, 560f, 84f, delegate
			{
				return "YouTube y TikTok dejan los vídeos subidos por apps sin revisar en privado («Solo yo»). Para publicar en público tienes que pedir una vez la auditoría de Google (YouTube API Services) y la de TikTok (Content Posting API). Es gratis y tarda de días a semanas. Instagram funciona ya con tu propia cuenta.";
			});
			VButton guide = Btn(note, "Abrir la guía", ButtonKind.Ghost, 596f, 64f, 144f, 36f);
			guide.Click += delegate
			{
				OpenGuide();
			};

			fldYtId.Box.Text = pub.Yt.ClientId;
			fldYtSecret.Box.Text = pub.Yt.ClientSecret;
			fldTtKey.Box.Text = pub.Tt.ClientKey;
			fldTtSecret.Box.Text = pub.Tt.ClientSecret;
			RefreshAccounts();
		}

		private void PaintAccount(Graphics g, Rectangle r, bool connected, string account, string extra)
		{
			Color c = connected ? Theme.Green : Theme.Dim;
			using (SolidBrush b = new SolidBrush(c))
			{
				g.FillEllipse(b, 0f, S(6f), S(8f), S(8f));
			}
			Theme.DrawText(g, connected ? "CONECTADO" : "SIN CONECTAR", Theme.Mono(7.5f), c, new RectangleF(S(14f), 0f, r.Width - S(14f), S(20f)), StringAlignment.Near, StringAlignment.Center);
			float y = S(22f);
			if (connected && account.Length > 0)
			{
				Theme.DrawText(g, account, Theme.BodyBold(9.5f), Theme.Text, new RectangleF(0f, y, r.Width, S(22f)), StringAlignment.Near, StringAlignment.Center);
				y += S(26f);
			}
			Theme.DrawWrapped(g, extra, Theme.Body(8f), Theme.Muted, new RectangleF(0f, y, r.Width, r.Height - y));
		}

		private void RefreshAccounts()
		{
			btnYtConn.Text = pub.Yt.Connected ? "Desconectar" : "Conectar";
			btnYtConn.Kind = pub.Yt.Connected ? ButtonKind.Ghost : ButtonKind.Primary;
			btnTtConn.Text = pub.Tt.Connected ? "Desconectar" : "Conectar";
			btnTtConn.Kind = pub.Tt.Connected ? ButtonKind.Ghost : ButtonKind.Primary;
			btnIgConn.Text = pub.Ig.Connected ? "Desconectar" : "Conectar";
			btnIgConn.Kind = pub.Ig.Connected ? ButtonKind.Ghost : ButtonKind.Primary;
			fldYtId.Locked = pub.Yt.Connected;
			fldYtSecret.Locked = pub.Yt.Connected;
			fldTtKey.Locked = pub.Tt.Connected;
			fldTtSecret.Locked = pub.Tt.Connected;
			fldIgToken.Locked = pub.Ig.Connected;
			if (pub.Ig.Connected)
			{
				fldIgToken.Box.Text = "••••••••••••";
			}
			foreach (Control c in new Control[9] { btnYtConn, btnTtConn, btnIgConn, ytStatus, ttStatus, igStatus, fldYtId, fldTtKey, fldIgToken })
			{
				c.Invalidate();
			}
			RefreshSummary();
			if (platHint != null)
			{
				platHint.Invalidate();
			}
		}

		// Ventana de espera con texto y "Cancelar". Devuelve la ventana; cancelled[0] se pone a true al cerrarla.
		private Form WaitDialog(string title, Func<string> text, bool[] cancelled, out PaintBox textBox)
		{
			Form f = DarkForm(title, 440, 230);
			textBox = Box(f, 20f, 18f, 400f, 150f, delegate(Graphics g, Rectangle r)
			{
				Theme.DrawWrapped(g, text(), Theme.Body(9.5f), Theme.Text, r);
			});
			VButton cancel = Btn(f, "Cancelar", ButtonKind.Ghost, 300f, 180f, 120f, 34f);
			cancel.Click += delegate
			{
				f.Close();
			};
			f.FormClosed += delegate
			{
				cancelled[0] = true;
			};
			return f;
		}

		private void ConnectYouTube()
		{
			if (pub.Yt.Connected)
			{
				if (Ask("YouTube", "¿Desconectar YouTube? Lo que ya está subido o programado en YouTube no cambia.", "Desconectar"))
				{
					pub.Yt.Logout();
					pub.SaveAccounts();
					RefreshAccounts();
				}
				return;
			}
			pub.Yt.ClientId = fldYtId.Box.Text.Trim();
			pub.Yt.ClientSecret = fldYtSecret.Box.Text.Trim();
			if (!pub.Yt.Configured)
			{
				Info("YouTube", "Pon el Client ID y el Client secret de tu proyecto de Google (los mismos que en Vortexs Live sirven). Cómo crearlos: GUIA.html.");
				return;
			}
			pub.SaveAccounts();
			bool[] cancelled = new bool[1];
			string code = "";
			string url = "";
			PaintBox tb;
			Form f = WaitDialog("Conectar YouTube", delegate
			{
				if (code.Length == 0)
				{
					return "Pidiendo el código a Google…";
				}
				return "1. Abre " + url + " (ya se ha abierto en el navegador).\n2. Escribe este código:\n\n      " + code + "\n\n3. Elige tu canal y acepta. Esta ventana se cierra sola.";
			}, cancelled, out tb);
			Background(delegate
			{
				pub.Yt.Login(delegate(string c, string u)
				{
					code = c;
					url = u;
					try
					{
						BeginInvoke((Action)delegate
						{
							tb.Invalidate();
							Open(u);
						});
					}
					catch
					{
					}
				}, delegate
				{
					return cancelled[0];
				});
			}, delegate(Exception err)
			{
				if (!cancelled[0])
				{
					f.Close();
				}
				pub.SaveAccounts();
				RefreshAccounts();
				if (err != null && !(err is OperationCanceledException))
				{
					Info("YouTube", "No se ha podido conectar: " + err.Message);
				}
			});
			f.ShowDialog(this);
		}

		private void ConnectTikTok()
		{
			if (pub.Tt.Connected)
			{
				if (Ask("TikTok", "¿Desconectar TikTok?", "Desconectar"))
				{
					pub.Tt.Logout();
					pub.SaveAccounts();
					RefreshAccounts();
				}
				return;
			}
			pub.Tt.ClientKey = fldTtKey.Box.Text.Trim();
			pub.Tt.ClientSecret = fldTtSecret.Box.Text.Trim();
			if (!pub.Tt.Configured)
			{
				Info("TikTok", "Pon el Client key y el Client secret de tu app de TikTok for Developers. Cómo crearla: GUIA.html.");
				return;
			}
			pub.SaveAccounts();
			bool[] cancelled = new bool[1];
			PaintBox tb;
			Form f = WaitDialog("Conectar TikTok", delegate
			{
				return "Se ha abierto TikTok en el navegador. Inicia sesión con tu cuenta y pulsa «Autorizar».\n\nCuando veas «¡Listo!» en el navegador, esta ventana se cierra sola.";
			}, cancelled, out tb);
			Background(delegate
			{
				pub.Tt.Login(delegate
				{
					return cancelled[0];
				});
			}, delegate(Exception err)
			{
				if (!cancelled[0])
				{
					f.Close();
				}
				pub.SaveAccounts();
				RefreshAccounts();
				SyncPills();
				if (err != null && !(err is OperationCanceledException))
				{
					Info("TikTok", "No se ha podido conectar: " + err.Message);
				}
			});
			f.ShowDialog(this);
		}

		private void ConnectInstagram()
		{
			if (pub.Ig.Connected)
			{
				if (Ask("Instagram", "¿Desconectar Instagram? Las publicaciones de Instagram pendientes darán error hasta que lo vuelvas a conectar.", "Desconectar"))
				{
					pub.Ig.Logout();
					fldIgToken.Box.Text = "";
					pub.SaveAccounts();
					RefreshAccounts();
				}
				return;
			}
			string token = fldIgToken.Box.Text.Trim();
			if (token.Length == 0)
			{
				Info("Instagram", "Pega el token de acceso de tu app de Meta. Cómo conseguirlo: GUIA.html.");
				return;
			}
			btnIgConn.Enabled = false;
			btnIgConn.Text = "Comprobando…";
			Background(delegate
			{
				pub.Ig.Connect(token);
			}, delegate(Exception err)
			{
				btnIgConn.Enabled = true;
				pub.SaveAccounts();
				RefreshAccounts();
				if (err != null)
				{
					Info("Instagram", "No se ha podido conectar: " + err.Message);
				}
			});
		}
	}
}
