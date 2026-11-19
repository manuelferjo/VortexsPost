// Pestaña Cola: cada publicación con sus redes, su estado (pendiente, subiendo con %, listo,
// programado en YouTube, publicado, error…) y acciones: Abrir, Cancelar, Reintentar y Quitar.
// Lo pendiente va arriba (lo más próximo primero) y lo terminado debajo (lo más nuevo primero).
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace VortexsPost
{
	internal partial class MainForm
	{
		private QueueList queueList;

		private void BuildQueue(int top)
		{
			Card c = NewCard("Cola e historial", "Lo pendiente arriba; lo publicado debajo", 20f, top, 760f, 556f, PageQueue);
			VButton clear = Btn(c, "Borrar historial", ButtonKind.Link, 590f, 16f, 154f, 24f);
			clear.Click += delegate
			{
				if (!Ask("Borrar historial", "Se quitan de la lista las publicaciones ya terminadas (publicadas, programadas en YouTube, canceladas o con error). No se borra nada de las redes ni tus vídeos.", "Borrar"))
				{
					return;
				}
				lock (Store.Lock)
				{
					Store.D.Posts.RemoveAll(delegate(Post p)
					{
						return !QueueList.Active(p);
					});
				}
				Store.Save();
				pub.Changes++;
			};
			queueList = new QueueList();
			queueList.Pub = pub;
			queueList.OpenUrl = Open;
			queueList.Confirm = Ask;
			queueList.ShowInfo = Info;
			Place(c, queueList, 12f, 54f, 736f, 490f);
		}

		internal class QueueList : VControl
		{
			public Publisher Pub;

			public Action<string> OpenUrl;

			public Func<string, string, string, bool> Confirm;

			public Action<string, string> ShowInfo;

			private int scroll;

			private int contentHeight;

			private readonly List<KeyValuePair<RectangleF, Action>> hits = new List<KeyValuePair<RectangleF, Action>>();

			private static readonly Color[] Dots = new Color[3] { Color.FromArgb(255, 70, 85), Color.FromArgb(37, 244, 238), Color.FromArgb(225, 48, 108) };

			public QueueList()
			{
				SetStyle(ControlStyles.Selectable, true);
				TabStop = false;
			}

			public static bool Active(Post p)
			{
				foreach (Target t in p.Targets)
				{
					if (t.Status == St.Pending || t.Status == St.Uploading || t.Status == St.Ready)
					{
						return true;
					}
				}
				return false;
			}

			private static long NextTime(Post p)
			{
				long best = long.MaxValue;
				foreach (Target t in p.Targets)
				{
					if (t.Status == St.Pending || t.Status == St.Uploading || t.Status == St.Ready)
					{
						best = Math.Min(best, (t.When == 0) ? p.Created : t.When);
					}
				}
				return best;
			}

			protected override void OnMouseEnter(EventArgs e)
			{
				Focus();
				base.OnMouseEnter(e);
			}

			protected override void OnMouseWheel(MouseEventArgs e)
			{
				scroll -= e.Delta / 120 * Theme.S(60f);
				Clamp();
				Invalidate();
				base.OnMouseWheel(e);
			}

			private void Clamp()
			{
				scroll = Math.Max(0, Math.Min(scroll, Math.Max(0, contentHeight - Height)));
			}

			protected override void OnMouseMove(MouseEventArgs e)
			{
				bool over = false;
				foreach (KeyValuePair<RectangleF, Action> h in hits)
				{
					if (h.Key.Contains(e.Location))
					{
						over = true;
					}
				}
				Cursor = over ? Cursors.Hand : Cursors.Default;
				base.OnMouseMove(e);
			}

			protected override void OnMouseClick(MouseEventArgs e)
			{
				foreach (KeyValuePair<RectangleF, Action> h in hits.ToArray())
				{
					if (h.Key.Contains(e.Location))
					{
						h.Value();
						Invalidate();
						break;
					}
				}
				base.OnMouseClick(e);
			}

			private static int S(float v)
			{
				return Theme.S(v);
			}

			private static string StatusText(Target t)
			{
				switch (t.Status)
				{
				case St.Pending:
					return "PENDIENTE";
				case St.Uploading:
					return (t.Progress >= 0 && t.Progress < 99) ? ("SUBIENDO " + t.Progress + " %") : "PROCESANDO";
				case St.Ready:
					return "LISTO";
				case St.Scheduled:
					return "PROGRAMADO";
				case St.Done:
					return t.Draft ? "EN BORRADORES" : "PUBLICADO";
				case St.Error:
					return "ERROR";
				default:
					return "CANCELADO";
				}
			}

			private static Color StatusColor(Target t)
			{
				switch (t.Status)
				{
				case St.Pending:
					return Theme.VioletLight;
				case St.Uploading:
					return Theme.Yellow;
				case St.Ready:
					return Theme.VioletPale;
				case St.Scheduled:
					return Theme.Violet;
				case St.Done:
					return Theme.Green;
				case St.Error:
					return Theme.Red;
				default:
					return Theme.Dim;
				}
			}

			private static string WhenText(Target t)
			{
				if (t.Status == St.Done)
				{
					return (t.DoneAt > 0) ? Store.Nice(t.DoneAt) : "";
				}
				if (t.Status == St.Scheduled || t.Status == St.Ready)
				{
					return "sale " + Store.Nice(t.When);
				}
				if (t.When == 0)
				{
					return (t.Status == St.Pending) ? "en cola" : "";
				}
				return Store.Nice(t.When);
			}

			protected override void OnPaint(PaintEventArgs e)
			{
				Graphics g = e.Graphics;
				Theme.Quality(g);
				hits.Clear();
				List<Post> active = new List<Post>();
				List<Post> done = new List<Post>();
				lock (Store.Lock)
				{
					foreach (Post p in Store.D.Posts)
					{
						if (Active(p))
						{
							active.Add(p);
						}
						else
						{
							done.Add(p);
						}
					}
					active.Sort(delegate(Post a, Post b)
					{
						return NextTime(a).CompareTo(NextTime(b));
					});
					done.Sort(delegate(Post a, Post b)
					{
						return b.Created.CompareTo(a.Created);
					});
					List<Post> all = new List<Post>(active);
					all.AddRange(done);
					float y = -scroll;
					float w = Width - S(10f);
					foreach (Post p in all)
					{
						y = PaintPost(g, p, y, w) + S(10f);
					}
					contentHeight = (int)(y + scroll);
				}
				if (active.Count + done.Count == 0)
				{
					Theme.DrawText(g, "Aquí verás lo programado, lo que se está subiendo y lo ya publicado.", Theme.Body(9f), Theme.Muted, new RectangleF(0f, 0f, Width, Height), StringAlignment.Center, StringAlignment.Center);
				}
				// Barra de desplazamiento fina.
				if (contentHeight > Height)
				{
					float bar = Math.Max(S(30f), Height * (float)Height / contentHeight);
					float pos = (Height - bar) * scroll / Math.Max(1f, contentHeight - Height);
					using (GraphicsPath path = Theme.Round(new RectangleF(Width - S(5f), pos, S(4f), bar), S(2f)))
					{
						using (SolidBrush b = new SolidBrush(Color.FromArgb(90, Theme.Violet)))
						{
							g.FillPath(b, path);
						}
					}
				}
			}

			// Dibuja una publicación desde y; devuelve dónde acaba.
			private float PaintPost(Graphics g, Post p, float y, float w)
			{
				float head = S(36f);
				float h = head + S(6f);
				foreach (Target t in p.Targets)
				{
					h += S(28f) + ((t.Msg.Length > 0) ? S(18f) : 0f);
				}
				if (y + h < 0f || y > Height)
				{
					return y + h;
				}
				RectangleF box = new RectangleF(0.5f, y + 0.5f, w - 1f, h - 1f);
				using (GraphicsPath path = Theme.Round(box, S(14f)))
				{
					using (SolidBrush b = new SolidBrush(Color.FromArgb(200, Theme.Elevated)))
					{
						g.FillPath(b, path);
					}
					using (Pen pen = new Pen(Active(p) ? Color.FromArgb(90, Theme.Violet) : Theme.Border))
					{
						g.DrawPath(pen, path);
					}
				}
				float pad = S(14f);
				string created = new DateTime(p.Created, DateTimeKind.Utc).ToLocalTime().ToString("dd/MM HH:mm");
				Theme.DrawText(g, Path.GetFileName(p.File), Theme.BodyBold(9.5f), Theme.Text, new RectangleF(pad, y + S(6f), w - pad * 2f - S(190f), S(24f)), StringAlignment.Near, StringAlignment.Center);
				float right = w - pad;
				bool uploading = false;
				foreach (Target t in p.Targets)
				{
					if (t.Status == St.Uploading)
					{
						uploading = true;
					}
				}
				if (!uploading)
				{
					right = Link(g, "Quitar", right, y + S(6f), S(24f), Theme.Muted, delegate
					{
						bool sure = !Active(p) || Confirm("Quitar", "Se quita de la cola y no se publicará lo que falta. ¿Seguro?", "Quitar");
						if (sure)
						{
							Pub.Remove(p);
						}
					}) - S(14f);
				}
				Theme.DrawText(g, "creado " + created, Theme.Mono(7f), Theme.Dim, new RectangleF(right - S(120f), y + S(6f), S(120f), S(24f)), StringAlignment.Far, StringAlignment.Center);

				float ly = y + head;
				foreach (Target t in p.Targets)
				{
					Target ft = t;
					int pi = (t.P == "yt") ? 0 : ((t.P == "tt") ? 1 : 2);
					float lh = S(28f);
					using (SolidBrush b = new SolidBrush(Dots[pi]))
					{
						g.FillEllipse(b, pad, ly + lh / 2f - S(3.5f), S(7f), S(7f));
					}
					Theme.DrawText(g, Store.PlatformName(t.P), Theme.BodySemi(9f), Theme.Text, new RectangleF(pad + S(14f), ly, S(80f), lh), StringAlignment.Near, StringAlignment.Center);
					// Estado en pastilla (con barra de progreso al subir).
					Color c = StatusColor(t);
					RectangleF chip = new RectangleF(pad + S(100f), ly + S(4f), S(124f), lh - S(8f));
					using (GraphicsPath path = Theme.Round(chip, chip.Height / 2f))
					{
						using (SolidBrush b = new SolidBrush(Color.FromArgb(22, c)))
						{
							g.FillPath(b, path);
						}
						if (t.Status == St.Uploading && t.Progress > 0)
						{
							g.SetClip(path);
							using (SolidBrush fill = new SolidBrush(Color.FromArgb(60, Theme.Yellow)))
							{
								g.FillRectangle(fill, chip.X, chip.Y, chip.Width * Math.Min(100, t.Progress) / 100f, chip.Height);
							}
							g.ResetClip();
						}
						using (Pen pen = new Pen(Color.FromArgb(120, c)))
						{
							g.DrawPath(pen, path);
						}
					}
					Theme.DrawText(g, StatusText(t), Theme.Mono(7f), c, chip, StringAlignment.Center, StringAlignment.Center);
					Theme.DrawText(g, WhenText(t), Theme.Body(8.5f), Theme.Muted, new RectangleF(chip.Right + S(12f), ly, S(170f), lh), StringAlignment.Near, StringAlignment.Center);
					// Acciones a la derecha.
					float x = w - pad;
					if (t.Url.Length > 0)
					{
						string url = t.Url;
						x = Link(g, "Abrir", x, ly, lh, Theme.Violet, delegate
						{
							OpenUrl(url);
						}) - S(14f);
					}
					if (t.Status == St.Pending || t.Status == St.Ready || t.Status == St.Uploading)
					{
						x = Link(g, "Cancelar", x, ly, lh, Theme.Muted, delegate
						{
							if (Confirm("Cancelar", "¿Cancelar la publicación en " + Store.PlatformName(ft.P) + "?", "Cancelar"))
							{
								Pub.Drop(ft);
							}
						}) - S(14f);
					}
					if (t.Status == St.Error || t.Status == St.Cancelled)
					{
						x = Link(g, "Reintentar", x, ly, lh, Theme.Violet, delegate
						{
							Pub.Retry(ft);
						}) - S(14f);
					}
					ly += lh;
					if (t.Msg.Length > 0)
					{
						string msg = t.Msg;
						RectangleF mr = new RectangleF(pad + S(100f), ly - S(2f), w - pad * 2f - S(100f), S(18f));
						Theme.DrawText(g, msg, Theme.Body(8f), (t.Status == St.Error) ? Color.FromArgb(255, 120, 130) : Theme.Muted, mr, StringAlignment.Near, StringAlignment.Center);
						hits.Add(new KeyValuePair<RectangleF, Action>(mr, delegate
						{
							if (ft.Draft && ft.Status == St.Done)
							{
								// Borrador de TikTok: el texto, al portapapeles para pegarlo en la app.
								try
								{
									Clipboard.SetText((ft.Text ?? "").Length > 0 ? ft.Text : " ");
								}
								catch
								{
								}
								ShowInfo("TikTok", "Texto copiado al portapapeles. Pégalo en el borrador desde la app de TikTok (o pásatelo al móvil):\n\n" + ft.Text);
								return;
							}
							ShowInfo(Store.PlatformName(ft.P), msg);
						}));
						ly += S(18f);
					}
				}
				return y + h;
			}

			// Enlace de texto alineado a la derecha en right; devuelve dónde empieza.
			private float Link(Graphics g, string text, float right, float y, float h, Color c, Action click)
			{
				Font f = Theme.BodySemi(8.5f);
				float tw = Theme.Measure(g, text, f).Width + S(4f);
				RectangleF r = new RectangleF(right - tw, y, tw, h);
				Theme.DrawText(g, text, f, c, r, StringAlignment.Far, StringAlignment.Center);
				hits.Add(new KeyValuePair<RectangleF, Action>(r, click));
				return r.X;
			}
		}
	}
}
