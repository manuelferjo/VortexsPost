// Ventana principal de Vortexs Post. Igual que en Vortexs Live, la clase está repartida en un
// archivo por pestaña (partial class):
//   MainForm.cs     esta parte: arranque, cabecera, fondo, bandeja, utilidades de diseño y diálogos
//   PublishTab.cs   pestaña Publicar (vídeo, redes, textos, horas y opciones)
//   EditorTab.cs    pestaña Editor (vídeos que deja tu editor en la carpeta compartida)
//   QueueTab.cs     pestaña Cola (lo programado, lo que se está subiendo y el historial)
//   SettingsTab.cs  pestaña Ajustes (ajustes guardados y opciones generales)
//   AccountsTab.cs  pestaña Cuentas (conectar YouTube, TikTok e Instagram)
//   Updater.cs      actualización automática desde las Releases de GitHub
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace VortexsPost
{
	internal partial class MainForm : Form
	{
		private class DarkColors : ProfessionalColorTable
		{
			public override Color ToolStripDropDownBackground { get { return Theme.Elevated; } }

			public override Color MenuBorder { get { return Theme.BorderHi; } }

			public override Color MenuItemBorder { get { return Color.FromArgb(120, Theme.Violet); } }

			public override Color MenuItemSelected { get { return Color.FromArgb(52, 44, 74); } }

			public override Color ImageMarginGradientBegin { get { return Theme.Elevated; } }

			public override Color ImageMarginGradientMiddle { get { return Theme.Elevated; } }

			public override Color ImageMarginGradientEnd { get { return Theme.Elevated; } }

			public override Color SeparatorDark { get { return Theme.Border; } }

			public override Color SeparatorLight { get { return Theme.Border; } }

			public override Color CheckBackground { get { return Color.FromArgb(60, Theme.Violet); } }

			public override Color CheckSelectedBackground { get { return Color.FromArgb(90, Theme.Violet); } }

			public override Color CheckPressedBackground { get { return Color.FromArgb(90, Theme.Violet); } }
		}

		public const string Version = "1.3";

		private const int PagePublish = 0;

		private const int PageQueue = 1;

		private const int PageSettings = 2;

		private const int PageAccounts = 3;

		private const int PageEditor = 4;

		// Página de cada pestaña, en el orden en que se ven: Publicar, Editor, Cola, Ajustes, Cuentas.
		private static readonly int[] TabPages = new int[5] { PagePublish, PageEditor, PageQueue, PageSettings, PageAccounts };

		private readonly Publisher pub = new Publisher();

		private readonly List<Control>[] pages = new List<Control>[5]
		{
			new List<Control>(),
			new List<Control>(),
			new List<Control>(),
			new List<Control>(),
			new List<Control>()
		};

		private Tabs tabs;

		private PaintBox header;

		private Image logo;

		private Bitmap bg;

		private System.Windows.Forms.Timer timer;

		private int lastChanges = -1;

		private int ticks;

		private NotifyIcon tray;

		private bool exiting;

		private bool trayTipShown;

		// Arranque desde Windows: empieza escondido en la bandeja.
		private readonly bool startHidden;

		// true mientras se rellenan los controles (para no guardar a medias).
		private bool loading;

		[DllImport("kernel32.dll")]
		private static extern uint SetThreadExecutionState(uint flags);

		private const uint ES_CONTINUOUS = 0x80000000u;

		private const uint ES_SYSTEM_REQUIRED = 0x00000001u;

		public MainForm(bool hidden)
		{
			startHidden = hidden;
			Text = "Vortexs Post " + Version;
			Font = Theme.Body(9f);
			BackColor = Theme.Bg;
			ForeColor = Theme.Text;
			FormBorderStyle = FormBorderStyle.FixedSingle;
			MaximizeBox = false;
			AutoScaleMode = AutoScaleMode.None;
			ClientSize = new Size(S(800f), S(700f));
			StartPosition = FormStartPosition.CenterScreen;
			DoubleBuffered = true;
			AllowDrop = true;
			try
			{
				Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
			}
			catch
			{
			}
			try
			{
				Stream s = typeof(MainForm).Assembly.GetManifestResourceStream("VortexsPost.logo.png");
				if (s != null)
				{
					logo = Image.FromStream(s);
				}
			}
			catch
			{
			}
			Store.Load();
			pub.LoadAccounts();
			loading = true;
			header = Box(this, 0f, 0f, 800f, 70f, PaintHeader);
			tabs = new Tabs { Items = new string[5] { "Publicar", "Editor", "Cola", "Ajustes", "Cuentas" } };
			Place(this, tabs, 20f, 78f, 760f, 36f);
			tabs.SelectedChanged += delegate
			{
				ShowPage(TabPages[tabs.Selected]);
			};
			int top = 126;
			BuildPublish(top);
			BuildQueue(top);
			BuildSettings(top);
			BuildAccounts(top);
			BuildEditor(top);
			loading = false;
			ApplyDefaultPreset();
			ShowPage(PagePublish);
			EnableDrop(this);
			BuildTray();
			pub.Notify = delegate(string title, string text, bool error)
			{
				try
				{
					BeginInvoke((Action)delegate
					{
						Balloon(title, text, error);
					});
				}
				catch
				{
				}
			};
			timer = new System.Windows.Forms.Timer { Interval = 1000 };
			timer.Tick += delegate
			{
				OnTimer();
			};
			timer.Start();
			HandleCreated += delegate
			{
				Theme.DarkTitleBar(Handle);
			};
			RefreshTikTokInfo();
			StartUpdateCheck();
		}

		protected override void SetVisibleCore(bool value)
		{
			// Arrancado con Windows: la ventana no aparece (solo el icono de la bandeja).
			if (startHidden && !IsHandleCreated)
			{
				CreateHandle();
				value = false;
			}
			base.SetVisibleCore(value);
		}

		private void OnTimer()
		{
			ticks++;
			int c = pub.Changes;
			if (c != lastChanges)
			{
				lastChanges = c;
				queueList.Invalidate();
				header.Invalidate();
			}
			else if (pub.Busy)
			{
				header.Invalidate();
				queueList.Invalidate();
			}
			if (ticks % 5 == 1)
			{
				pub.Tick();
				// Que el PC no se suspenda mientras quede algo por publicar.
				bool awake;
				lock (Store.Lock)
				{
					awake = Store.D.KeepAwake;
				}
				try
				{
					SetThreadExecutionState((awake && pub.HasLocalWork()) ? (ES_CONTINUOUS | ES_SYSTEM_REQUIRED) : ES_CONTINUOUS);
				}
				catch
				{
				}
			}
			// Carpeta del editor: cada 30 s.
			if (ticks % 30 == 3)
			{
				ScanEditor(false);
			}
			int ec = editorChanges;
			if (ec != lastEditorChanges)
			{
				lastEditorChanges = ec;
				RefreshEditorTab();
			}
			if (ticks % 60 == 0)
			{
				// Cada minuto: el resumen ("mañana 15:00" cambia con la hora) y la cola.
				summaryBox.Invalidate();
				queueList.Invalidate();
			}
		}

		// ---------- Páginas ----------

		private void ShowPage(int p)
		{
			SuspendLayout();
			for (int i = 0; i < pages.Length; i++)
			{
				foreach (Control c in pages[i])
				{
					c.Visible = i == p;
				}
			}
			ResumeLayout();
			if (p == PageQueue)
			{
				queueList.Invalidate();
			}
			if (p == PageEditor)
			{
				editorList.Invalidate();
			}
		}

		private static int TabOf(int page)
		{
			return Array.IndexOf(TabPages, page);
		}

		private void GoPage(int p)
		{
			tabs.Selected = TabOf(p);
			ShowPage(p);
		}

		// ---------- Utilidades de diseño ----------

		private static int S(float v)
		{
			return Theme.S(v);
		}

		private void Place(Control parent, Control c, float x, float y, float w, float h)
		{
			Place(parent, c, x, y, w, h, -1);
		}

		private void Place(Control parent, Control c, float x, float y, float w, float h, int page)
		{
			c.SetBounds(S(x), S(y), S(w), S(h));
			parent.Controls.Add(c);
			if (page >= 0)
			{
				pages[page].Add(c);
			}
		}

		private PaintBox Box(Control parent, float x, float y, float w, float h, Action<Graphics, Rectangle> painter)
		{
			return Box(parent, x, y, w, h, painter, -1);
		}

		private PaintBox Box(Control parent, float x, float y, float w, float h, Action<Graphics, Rectangle> painter, int page)
		{
			PaintBox b = new PaintBox { Painter = painter };
			Place(parent, b, x, y, w, h, page);
			return b;
		}

		private Card NewCard(string title, string subtitle, float x, float y, float w, float h, int page)
		{
			Card c = new Card { Title = title, Subtitle = subtitle };
			Place(this, c, x, y, w, h, page);
			return c;
		}

		// Etiqueta pequeña en mayúsculas encima de un campo.
		private PaintBox Label(Control parent, string text, float x, float y, float w)
		{
			return Box(parent, x, y, w, 18f, delegate(Graphics g, Rectangle r)
			{
				Theme.DrawText(g, text, Theme.Mono(7f), Theme.Muted, r, StringAlignment.Near, StringAlignment.Center);
			});
		}

		// Texto de ayuda en gris (varias líneas). text() se lee cada vez que se dibuja.
		private PaintBox Hint(Control parent, float x, float y, float w, float h, Func<string> text)
		{
			return Box(parent, x, y, w, h, delegate(Graphics g, Rectangle r)
			{
				Theme.DrawWrapped(g, text(), Theme.Body(8.25f), Theme.Muted, r);
			});
		}

		private Field NewField(Control parent, float x, float y, float w, float h, bool multiline)
		{
			Field f = new Field();
			if (multiline)
			{
				f.Box.Multiline = true;
				f.Box.WordWrap = true;
				f.Box.AcceptsReturn = true;
				f.Box.ScrollBars = ScrollBars.None;
			}
			Place(parent, f, x, y, w, h);
			return f;
		}

		private VButton Btn(Control parent, string text, ButtonKind kind, float x, float y, float w, float h)
		{
			VButton b = new VButton { Text = text, Kind = kind };
			Place(parent, b, x, y, w, h);
			return b;
		}

		private Toggle Tg(Control parent, string text, string hint, float x, float y, float w, float h)
		{
			Toggle t = new Toggle { Text = text, Hint = hint ?? "" };
			Place(parent, t, x, y, w, h);
			return t;
		}

		private ContextMenuStrip DarkMenu()
		{
			ContextMenuStrip m = new ContextMenuStrip
			{
				Renderer = new ToolStripProfessionalRenderer(new DarkColors()),
				BackColor = Theme.Elevated,
				ForeColor = Theme.Text,
				Font = Theme.Body(9f),
				ShowImageMargin = false,
				ShowCheckMargin = true
			};
			m.Closed += delegate
			{
				m.BeginInvoke((Action)delegate
				{
					m.Dispose();
				});
			};
			return m;
		}

		private Form DarkForm(string title, int w, int h)
		{
			Form f = new Form
			{
				Text = title,
				FormBorderStyle = FormBorderStyle.FixedDialog,
				MaximizeBox = false,
				MinimizeBox = false,
				ShowInTaskbar = false,
				StartPosition = FormStartPosition.CenterParent,
				BackColor = Theme.Bg,
				ForeColor = Theme.Text,
				Font = Theme.Body(9f),
				AutoScaleMode = AutoScaleMode.None,
				ClientSize = new Size(S(w), S(h))
			};
			try
			{
				f.Icon = Icon;
			}
			catch
			{
			}
			f.HandleCreated += delegate
			{
				Theme.DarkTitleBar(f.Handle);
			};
			return f;
		}

		// Mensaje con el tema oscuro (en lugar de MessageBox).
		private void Info(string title, string text)
		{
			using (Form f = DarkForm(title, 420, 190))
			{
				Box(f, 20f, 18f, 380f, 110f, delegate(Graphics g, Rectangle r)
				{
					Theme.DrawWrapped(g, text, Theme.Body(9f), Theme.Text, r);
				});
				VButton ok = Btn(f, "Vale", ButtonKind.Primary, 280f, 138f, 120f, 34f);
				ok.Click += delegate
				{
					f.Close();
				};
				f.AcceptButton = null;
				if (Visible)
				{
					f.ShowDialog(this);
				}
				else
				{
					f.StartPosition = FormStartPosition.CenterScreen;
					f.ShowDialog();
				}
			}
		}

		// Pregunta sí/no con el tema oscuro.
		private bool Ask(string title, string text, string yes)
		{
			bool result = false;
			using (Form f = DarkForm(title, 420, 170))
			{
				Box(f, 20f, 18f, 380f, 90f, delegate(Graphics g, Rectangle r)
				{
					Theme.DrawWrapped(g, text, Theme.Body(9f), Theme.Text, r);
				});
				VButton no = Btn(f, "Cancelar", ButtonKind.Ghost, 150f, 118f, 120f, 34f);
				VButton si = Btn(f, yes, ButtonKind.Danger, 280f, 118f, 120f, 34f);
				no.Click += delegate
				{
					f.Close();
				};
				si.Click += delegate
				{
					result = true;
					f.Close();
				};
				f.ShowDialog(this);
			}
			return result;
		}

		// Pide un texto (nombre de un ajuste…). null si se cancela.
		private string AskText(string title, string label, string initial)
		{
			string result = null;
			using (Form f = DarkForm(title, 420, 160))
			{
				Label(f, label, 20f, 16f, 380f);
				Field fld = NewField(f, 20f, 36f, 380f, 36f, false);
				fld.Box.Text = initial;
				VButton no = Btn(f, "Cancelar", ButtonKind.Ghost, 150f, 108f, 120f, 34f);
				VButton ok = Btn(f, "Guardar", ButtonKind.Primary, 280f, 108f, 120f, 34f);
				no.Click += delegate
				{
					f.Close();
				};
				EventHandler save = delegate
				{
					if (fld.Box.Text.Trim().Length > 0)
					{
						result = fld.Box.Text.Trim();
						f.Close();
					}
				};
				ok.Click += save;
				fld.Box.KeyDown += delegate(object s, KeyEventArgs e)
				{
					if (e.KeyCode == Keys.Enter)
					{
						e.SuppressKeyPress = true;
						save(s, EventArgs.Empty);
					}
				};
				f.Shown += delegate
				{
					fld.Box.Focus();
					fld.Box.SelectAll();
				};
				f.ShowDialog(this);
			}
			return result;
		}

		// Hace work en otro hilo y luego done(error o null) en la ventana.
		private void Background(Action work, Action<Exception> done)
		{
			ThreadPool.QueueUserWorkItem(delegate
			{
				Exception err = null;
				try
				{
					work();
				}
				catch (Exception ex)
				{
					err = ex;
				}
				try
				{
					BeginInvoke((Action)delegate
					{
						done(err);
					});
				}
				catch
				{
				}
			});
		}

		private static void Open(string url)
		{
			Http.OpenBrowser(url);
		}

		private void OpenGuide()
		{
			string guide = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "GUIA.html");
			if (File.Exists(guide))
			{
				Open(guide);
			}
			else
			{
				Info("Guía", "No encuentro GUIA.html junto a VortexsPost.exe.");
			}
		}

		// ---------- Arrastrar vídeos a la ventana ----------

		private void EnableDrop(Control c)
		{
			c.AllowDrop = true;
			c.DragEnter += delegate(object s, DragEventArgs e)
			{
				e.Effect = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
			};
			c.DragDrop += delegate(object s, DragEventArgs e)
			{
				string[] files = e.Data.GetData(DataFormats.FileDrop) as string[];
				if (files != null && files.Length > 0)
				{
					SetVideo(files[0]);
					GoPage(PagePublish);
				}
			};
			foreach (Control child in c.Controls)
			{
				EnableDrop(child);
			}
		}

		// ---------- Fondo y cabecera ----------

		protected override void OnPaintBackground(PaintEventArgs e)
		{
			if (bg == null || bg.Size != ClientSize)
			{
				BuildBackground();
			}
			e.Graphics.DrawImageUnscaled(bg, 0, 0);
		}

		private void BuildBackground()
		{
			if (bg != null)
			{
				bg.Dispose();
			}
			bg = new Bitmap(Math.Max(1, ClientSize.Width), Math.Max(1, ClientSize.Height));
			using (Graphics g = Graphics.FromImage(bg))
			{
				Theme.Quality(g);
				g.Clear(Theme.Bg);
				float w = bg.Width;
				float h = bg.Height;
				Card.Glow(g, new RectangleF(-w * 0.35f, -h * 0.22f, w * 1.1f, h * 0.55f), Color.FromArgb(48, Theme.Violet));
				Card.Glow(g, new RectangleF(w * 0.45f, -h * 0.05f, w * 0.9f, h * 0.5f), Color.FromArgb(30, Theme.Yellow));
				Card.Glow(g, new RectangleF(-w * 0.2f, h * 0.7f, w * 1f, h * 0.45f), Color.FromArgb(22, Theme.Violet));
				float step = S(22f);
				float hh = S(70f);
				for (float y = step / 2f; y < hh; y += step)
				{
					for (float x = step / 2f; x < w; x += step)
					{
						int alpha = (int)(40f * (1f - y / hh));
						using (SolidBrush b = new SolidBrush(Color.FromArgb(Math.Max(0, alpha), Theme.Violet)))
						{
							g.FillEllipse(b, x - 1f, y - 1f, 2f, 2f);
						}
					}
				}
				using (Pen p = new Pen(Color.FromArgb(18, 255, 255, 255)))
				{
					g.DrawLine(p, 0f, hh - 1f, w, hh - 1f);
				}
			}
		}

		// Cabecera: logo, "Vortexs Post", versión y la etiqueta de estado.
		private void PaintHeader(Graphics g, Rectangle r)
		{
			float left = S(20f);
			if (logo != null)
			{
				g.DrawImage(logo, new RectangleF(left, S(16f), S(38f), S(38f)));
			}
			float textX = left + S(50f);
			Font font = Theme.Display(16f);
			float w1 = Theme.Measure(g, "Vortexs", font).Width + S(7f);
			SizeF s2 = Theme.Measure(g, "Post", font);
			using (SolidBrush b = new SolidBrush(Theme.Text))
			{
				g.DrawString("Vortexs", font, b, textX, S(10f), StringFormat.GenericTypographic);
			}
			using (LinearGradientBrush b2 = Theme.Brand(new RectangleF(textX + w1, S(10f), s2.Width, s2.Height), true))
			{
				g.DrawString("Post", font, b2, textX + w1, S(10f), StringFormat.GenericTypographic);
			}
			Theme.DrawText(g, "SUBIDAS · PROGRAMACIÓN · V" + Version, Theme.Mono(7f), Theme.Muted, new RectangleF(textX + 1f, S(39f), S(260f), S(16f)), StringAlignment.Near, StringAlignment.Center);

			// Estado: subiendo, pendientes o al día.
			string state;
			Color color;
			int pending = 0;
			int uploading = -2;
			lock (Store.Lock)
			{
				foreach (Post p in Store.D.Posts)
				{
					foreach (Target t in p.Targets)
					{
						if (t.Status == St.Uploading)
						{
							uploading = Math.Max(uploading, t.Progress);
						}
						else if (t.Status == St.Pending || t.Status == St.Ready)
						{
							pending++;
						}
					}
				}
			}
			if (uploading > -2)
			{
				state = (uploading >= 0) ? ("SUBIENDO " + uploading + " %") : "SUBIENDO";
				color = Theme.Yellow;
			}
			else if (pending > 0)
			{
				state = pending + ((pending == 1) ? " PENDIENTE" : " PENDIENTES");
				color = Theme.Violet;
			}
			else
			{
				state = "AL DÍA";
				color = Theme.Green;
			}
			Font f = Theme.Mono(7.5f);
			float tw = Theme.Measure(g, state, f).Width;
			RectangleF chip = new RectangleF(r.Width - S(20f) - tw - S(34f), S(22f), tw + S(34f), S(26f));
			using (GraphicsPath path = Theme.Round(chip, chip.Height / 2f))
			{
				using (SolidBrush b3 = new SolidBrush(Color.FromArgb(24, color)))
				{
					g.FillPath(b3, path);
				}
				using (Pen pen = new Pen(Color.FromArgb(110, color)))
				{
					g.DrawPath(pen, path);
				}
			}
			using (SolidBrush b4 = new SolidBrush(color))
			{
				g.FillEllipse(b4, chip.X + S(12f), chip.Y + chip.Height / 2f - S(3.5f), S(7f), S(7f));
			}
			Theme.DrawText(g, state, f, color, new RectangleF(chip.X + S(24f), chip.Y, tw + S(6f), chip.Height), StringAlignment.Near, StringAlignment.Center);
		}

		// ---------- Bandeja del sistema ----------

		private void BuildTray()
		{
			tray = new NotifyIcon { Text = "Vortexs Post", Visible = true };
			try
			{
				tray.Icon = Icon ?? SystemIcons.Application;
			}
			catch
			{
				tray.Icon = SystemIcons.Application;
			}
			ContextMenuStrip menu = new ContextMenuStrip
			{
				Renderer = new ToolStripProfessionalRenderer(new DarkColors()),
				BackColor = Theme.Elevated,
				ForeColor = Theme.Text,
				Font = Theme.Body(9f),
				ShowImageMargin = false
			};
			menu.Items.Add("Abrir Vortexs Post", null, delegate
			{
				ShowFromTray();
			});
			menu.Items.Add("Cola", null, delegate
			{
				ShowFromTray();
				GoPage(PageQueue);
			});
			menu.Items.Add(new ToolStripSeparator());
			menu.Items.Add("Salir", null, delegate
			{
				Quit();
			});
			tray.ContextMenuStrip = menu;
			tray.DoubleClick += delegate
			{
				ShowFromTray();
			};
			tray.BalloonTipClicked += delegate
			{
				ShowFromTray();
				GoPage(PageQueue);
			};
		}

		public void ShowFromTray()
		{
			Show();
			if (WindowState == FormWindowState.Minimized)
			{
				WindowState = FormWindowState.Normal;
			}
			Activate();
			OfferPendingUpdate();
		}

		private void Balloon(string title, string text, bool error)
		{
			if (tray == null)
			{
				return;
			}
			try
			{
				tray.ShowBalloonTip(5000, title, (text.Length > 0) ? text : " ", error ? ToolTipIcon.Error : ToolTipIcon.Info);
			}
			catch
			{
			}
		}

		private void Quit()
		{
			if (pub.Busy && !Ask("Salir", "Se está subiendo un vídeo. Si sales ahora, se volverá a subir desde el principio la próxima vez que abras el programa.", "Salir"))
			{
				return;
			}
			if (pub.HasLocalWork() && !pub.Busy && !Ask("Salir", "Hay publicaciones de TikTok o Instagram pendientes. Si cierras el programa no se publicarán hasta que lo vuelvas a abrir.", "Salir"))
			{
				return;
			}
			exiting = true;
			Close();
		}

		protected override void OnFormClosing(FormClosingEventArgs e)
		{
			bool keep;
			lock (Store.Lock)
			{
				keep = Store.D.KeepInTray;
			}
			if (!exiting && keep && e.CloseReason == CloseReason.UserClosing)
			{
				// Se queda en la bandeja para publicar lo programado.
				e.Cancel = true;
				Hide();
				if (!trayTipShown)
				{
					trayTipShown = true;
					Balloon("Vortexs Post sigue abierto", "Está en la bandeja para publicar lo programado. Para salir: clic derecho en el icono → Salir.", false);
				}
				return;
			}
			if (!exiting && e.CloseReason == CloseReason.UserClosing && pub.HasLocalWork())
			{
				if (!Ask("Salir", "Hay publicaciones pendientes. Si cierras el programa no se publicarán hasta que lo vuelvas a abrir.", "Cerrar"))
				{
					e.Cancel = true;
					return;
				}
			}
			SaveForm();
			pub.SaveAccounts();
			if (tray != null)
			{
				tray.Visible = false;
				tray.Dispose();
				tray = null;
			}
			try
			{
				SetThreadExecutionState(ES_CONTINUOUS);
			}
			catch
			{
			}
			base.OnFormClosing(e);
		}
	}
}
