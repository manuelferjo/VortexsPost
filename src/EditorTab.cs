// Pestaña Editor: los vídeos que deja tu editor en la carpeta compartida (por ejemplo una carpeta de
// Google Drive sincronizada en el PC). Cada 30 s se mira si hay vídeos nuevos; de cada uno se sabe si
// es vertical (Shorts, Reels, TikTok) u horizontal (vídeo largo de YouTube). Nada se publica solo:
// «Usar» lo pasa a la pestaña Publicar con las redes ya marcadas y ahí lo revisas y lo programas.
//
// Si junto al vídeo hay un .txt con el mismo nombre, la primera línea es el título y el resto la
// descripción.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace VortexsPost
{
	internal partial class MainForm
	{
		private static readonly string[] VideoExt = new string[5] { ".mp4", ".mov", ".m4v", ".webm", ".mkv" };

		private EditorList editorList;

		private VButton btnShowDiscarded;

		private volatile bool scanning;

		private volatile int editorChanges;

		private int lastEditorChanges = -1;

		// Vídeo del editor que se está preparando en Publicar (para marcarlo como usado al programarlo).
		private string editorPath = "";

		private void BuildEditor(int top)
		{
			Card c = NewCard("Vídeos del editor", "", 20f, top, 760f, 556f, PageEditor);
			Box(c, 20f, 36f, 520f, 18f, delegate(Graphics g, Rectangle r)
			{
				string folder;
				lock (Store.Lock)
				{
					folder = Store.D.Folder ?? "";
				}
				string text = (folder.Length == 0) ? "Elige la carpeta compartida en Ajustes → Carpeta del editor" : ("Carpeta: " + folder);
				Theme.DrawText(g, text, Theme.Body(8.25f), Theme.Muted, r, StringAlignment.Near, StringAlignment.Center);
			});
			VButton scan = Btn(c, "Buscar ahora", ButtonKind.Link, 600f, 16f, 144f, 24f);
			scan.Click += delegate
			{
				ScanEditor(true);
			};
			btnShowDiscarded = Btn(c, "Ver descartados", ButtonKind.Link, 440f, 16f, 150f, 24f);
			btnShowDiscarded.Click += delegate
			{
				editorList.ShowDiscarded = !editorList.ShowDiscarded;
				btnShowDiscarded.Text = editorList.ShowDiscarded ? "Ocultar descartados" : "Ver descartados";
				editorList.Invalidate();
			};
			editorList = new EditorList();
			editorList.Use = UseEditorVideo;
			editorList.SetState = delegate(EditorItem it, string state)
			{
				lock (Store.Lock)
				{
					it.State = state;
				}
				Store.Save();
				editorChanges++;
			};
			editorList.OpenFile = delegate(string path)
			{
				try
				{
					Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
				}
				catch (Exception ex)
				{
					Info("Ver vídeo", "No se ha podido abrir: " + ex.Message);
				}
			};
			Place(c, editorList, 12f, 62f, 736f, 482f);
		}

		private void RefreshEditorTab()
		{
			int n = 0;
			lock (Store.Lock)
			{
				foreach (EditorItem it in Store.D.Editor)
				{
					if (it.State == "nuevo")
					{
						n++;
					}
				}
			}
			string label = (n > 0) ? ("Editor (" + n + ")") : "Editor";
			if (tabs.Items[TabOf(PageEditor)] != label)
			{
				tabs.Items[TabOf(PageEditor)] = label;
				tabs.Invalidate();
			}
			editorList.Invalidate();
		}

		// Mira la carpeta en otro hilo. manual: pulsado «Buscar ahora» (avisa si no hay carpeta).
		private void ScanEditor(bool manual)
		{
			string folder;
			lock (Store.Lock)
			{
				folder = Store.D.Folder ?? "";
			}
			if (folder.Length == 0 || !Directory.Exists(folder))
			{
				if (manual)
				{
					Info("Editor", (folder.Length == 0) ? "Primero elige la carpeta compartida con tu editor en Ajustes → Carpeta del editor." : ("No encuentro la carpeta " + folder + ". ¿Está abierto Google Drive para ordenador?"));
				}
				return;
			}
			if (scanning)
			{
				return;
			}
			scanning = true;
			ThreadPool.QueueUserWorkItem(delegate
			{
				int added = 0;
				try
				{
					added = ScanFolder(folder);
				}
				catch
				{
				}
				finally
				{
					scanning = false;
				}
				editorChanges++;
				if (added > 0)
				{
					try
					{
						BeginInvoke((Action)delegate
						{
							Balloon("Tu editor ha subido " + ((added == 1) ? "un vídeo nuevo" : (added + " vídeos nuevos")), "Revísalos en la pestaña Editor.", false);
						});
					}
					catch
					{
					}
				}
			});
		}

		// Devuelve cuántos vídeos nuevos ha encontrado (sin contar la primera vez que se mira una carpeta).
		private int ScanFolder(string folder)
		{
			List<string> files = new List<string>();
			try
			{
				foreach (string f in Directory.GetFiles(folder, "*", SearchOption.AllDirectories))
				{
					if (Array.IndexOf(VideoExt, Path.GetExtension(f).ToLowerInvariant()) >= 0)
					{
						files.Add(f);
					}
					if (files.Count >= 3000)
					{
						break;
					}
				}
			}
			catch
			{
				return 0;
			}
			bool firstTime;
			Dictionary<string, EditorItem> known = new Dictionary<string, EditorItem>(StringComparer.OrdinalIgnoreCase);
			lock (Store.Lock)
			{
				firstTime = (Store.D.EditorFolder ?? "") != folder;
				foreach (EditorItem it in Store.D.Editor)
				{
					known[it.Path] = it;
				}
			}
			int added = 0;
			DateTime now = DateTime.UtcNow;
			foreach (string f in files)
			{
				FileInfo fi;
				try
				{
					fi = new FileInfo(f);
				}
				catch
				{
					continue;
				}
				// Aún copiándose (cambió hace menos de 15 s): se mira en la siguiente vuelta.
				if (fi.Length == 0 || now - fi.LastWriteTimeUtc < TimeSpan.FromSeconds(15.0))
				{
					continue;
				}
				EditorItem old;
				if (known.TryGetValue(f, out old) && old.Size == fi.Length && old.Width > 0)
				{
					continue;
				}
				VideoProbe.Info v = VideoProbe.Read(f);
				lock (Store.Lock)
				{
					if (old != null)
					{
						old.Size = fi.Length;
						old.Modified = fi.LastWriteTimeUtc.Ticks;
						old.Width = v.Width;
						old.Height = v.Height;
						old.Seconds = v.Seconds;
						continue;
					}
					EditorItem it = new EditorItem
					{
						Path = f,
						Size = fi.Length,
						Modified = fi.LastWriteTimeUtc.Ticks,
						Seen = now.Ticks,
						Width = v.Width,
						Height = v.Height,
						Seconds = v.Seconds
					};
					// La primera vez, los vídeos de hace más de 14 días van a «descartados» (se pueden recuperar).
					if (firstTime && now - fi.LastWriteTimeUtc > TimeSpan.FromDays(14.0))
					{
						it.State = "descartado";
					}
					else if (!firstTime)
					{
						added++;
					}
					Store.D.Editor.Add(it);
				}
			}
			lock (Store.Lock)
			{
				// Los que ya no están en la carpeta y no se llegaron a usar se quitan de la lista.
				Store.D.Editor.RemoveAll(delegate(EditorItem it)
				{
					return it.State != "usado" && !File.Exists(it.Path);
				});
				Store.D.EditorFolder = folder;
			}
			Store.Save();
			return added;
		}

		// Título y descripción del .txt con el mismo nombre que el vídeo (si lo hay).
		private static bool ReadSidecar(string video, out string title, out string desc)
		{
			title = (desc = "");
			string txt = Path.Combine(Path.GetDirectoryName(video), Path.GetFileNameWithoutExtension(video) + ".txt");
			if (!File.Exists(txt))
			{
				return false;
			}
			try
			{
				string[] lines = File.ReadAllText(txt, Encoding.UTF8).Replace("\r", "").Split('\n');
				int i = 0;
				while (i < lines.Length && lines[i].Trim().Length == 0)
				{
					i++;
				}
				if (i < lines.Length)
				{
					title = lines[i].Trim();
					i++;
				}
				desc = string.Join("\r\n", lines, i, lines.Length - i).Trim();
				return true;
			}
			catch
			{
				return false;
			}
		}

		private void UseEditorVideo(EditorItem it)
		{
			if (!File.Exists(it.Path))
			{
				Info("Editor", "El vídeo ya no está en la carpeta.");
				return;
			}
			SetVideo(it.Path);
			editorPath = it.Path;
			if (it.Width > 0 && it.Height > 0)
			{
				bool vertical = it.Height > it.Width;
				// Vertical: Shorts, Reels y TikTok. Horizontal: vídeo largo de YouTube.
				platPills[0].Selected = true;
				platPills[1].Selected = vertical;
				platPills[2].Selected = vertical;
				foreach (VButton b in platPills)
				{
					b.Invalidate();
				}
			}
			string title, desc;
			if (ReadSidecar(it.Path, out title, out desc))
			{
				fldTitle.Box.Text = title;
				fldDesc.Box.Text = desc;
			}
			else
			{
				fldTitle.Box.Text = "";
				fldDesc.Box.Text = "";
			}
			SyncLocked();
			RefreshSummary();
			GoPage(PagePublish);
			SetView(0);
			fldTitle.Box.Focus();
		}

		// Al programar un vídeo que vino del editor, se marca como usado.
		private void EditorPublished(string file)
		{
			if (editorPath.Length == 0 || !string.Equals(editorPath, file, StringComparison.OrdinalIgnoreCase))
			{
				return;
			}
			lock (Store.Lock)
			{
				foreach (EditorItem it in Store.D.Editor)
				{
					if (string.Equals(it.Path, file, StringComparison.OrdinalIgnoreCase))
					{
						it.State = "usado";
					}
				}
			}
			editorPath = "";
			Store.Save();
			editorChanges++;
		}

		// ---------- Lista ----------

		internal class EditorList : VControl
		{
			public Action<EditorItem> Use;

			public Action<EditorItem, string> SetState;

			public Action<string> OpenFile;

			public bool ShowDiscarded;

			private int scroll;

			private int contentHeight;

			private readonly List<KeyValuePair<RectangleF, Action>> hits = new List<KeyValuePair<RectangleF, Action>>();

			public EditorList()
			{
				SetStyle(ControlStyles.Selectable, true);
				TabStop = false;
			}

			private static int S(float v)
			{
				return Theme.S(v);
			}

			protected override void OnMouseEnter(EventArgs e)
			{
				Focus();
				base.OnMouseEnter(e);
			}

			protected override void OnMouseWheel(MouseEventArgs e)
			{
				scroll -= e.Delta / 120 * S(60f);
				scroll = Math.Max(0, Math.Min(scroll, Math.Max(0, contentHeight - Height)));
				Invalidate();
				base.OnMouseWheel(e);
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

			protected override void OnPaint(PaintEventArgs e)
			{
				Graphics g = e.Graphics;
				Theme.Quality(g);
				hits.Clear();
				List<EditorItem> items = new List<EditorItem>();
				lock (Store.Lock)
				{
					foreach (EditorItem it in Store.D.Editor)
					{
						if (it.State != "descartado" || ShowDiscarded)
						{
							items.Add(it);
						}
					}
				}
				// Nuevos primero; dentro de cada grupo, lo más reciente arriba.
				items.Sort(delegate(EditorItem a, EditorItem b)
				{
					int ra = (a.State == "nuevo") ? 0 : ((a.State == "usado") ? 1 : 2);
					int rb = (b.State == "nuevo") ? 0 : ((b.State == "usado") ? 1 : 2);
					if (ra != rb)
					{
						return ra.CompareTo(rb);
					}
					return b.Modified.CompareTo(a.Modified);
				});
				float y = -scroll;
				float w = Width - S(10f);
				float rh = S(66f);
				foreach (EditorItem it in items)
				{
					if (y + rh > 0f && y < Height)
					{
						PaintItem(g, it, y, w, rh);
					}
					y += rh + S(8f);
				}
				contentHeight = (int)(y + scroll);
				if (items.Count == 0)
				{
					Theme.DrawText(g, "Aquí aparecerán los vídeos que deje tu editor en la carpeta compartida.", Theme.Body(9f), Theme.Muted, new RectangleF(0f, 0f, Width, Height), StringAlignment.Center, StringAlignment.Center);
				}
				if (contentHeight > Height)
				{
					float bar = Math.Max(S(30f), Height * (float)Height / contentHeight);
					float pos = (Height - bar) * scroll / Math.Max(1f, contentHeight - Height);
					using (GraphicsPath path = Theme.Round(new RectangleF(Width - S(5f), pos, S(4f), bar), S(2f)))
					{
						using (SolidBrush sb = new SolidBrush(Color.FromArgb(90, Theme.Violet)))
						{
							g.FillPath(sb, path);
						}
					}
				}
			}

			private void PaintItem(Graphics g, EditorItem it, float y, float w, float h)
			{
				bool discarded = it.State == "descartado";
				RectangleF box = new RectangleF(0.5f, y + 0.5f, w - 1f, h - 1f);
				using (GraphicsPath path = Theme.Round(box, S(14f)))
				{
					using (SolidBrush b = new SolidBrush(Color.FromArgb(discarded ? 110 : 200, Theme.Elevated)))
					{
						g.FillPath(b, path);
					}
					using (Pen pen = new Pen((it.State == "nuevo") ? Color.FromArgb(110, Theme.Violet) : Theme.Border))
					{
						g.DrawPath(pen, path);
					}
				}
				float pad = S(14f);
				// Formato: vertical u horizontal.
				bool known = it.Width > 0 && it.Height > 0;
				bool vertical = known && it.Height > it.Width;
				string fmt = !known ? "¿FORMATO?" : (vertical ? "VERTICAL" : "HORIZONTAL");
				Color fc = !known ? Theme.Dim : (vertical ? Theme.Violet : Theme.Yellow);
				RectangleF chip = new RectangleF(pad, y + S(12f), S(96f), S(22f));
				using (GraphicsPath path2 = Theme.Round(chip, chip.Height / 2f))
				{
					using (SolidBrush b2 = new SolidBrush(Color.FromArgb(24, fc)))
					{
						g.FillPath(b2, path2);
					}
					using (Pen pen2 = new Pen(Color.FromArgb(120, fc)))
					{
						g.DrawPath(pen2, path2);
					}
				}
				Theme.DrawText(g, fmt, Theme.Mono(7f), fc, chip, StringAlignment.Center, StringAlignment.Center);
				string dest = !known ? "elige las redes" : (vertical ? "Shorts · Reels · TikTok" : "YouTube (vídeo largo)");
				Theme.DrawText(g, dest, Theme.Body(7.5f), Theme.Muted, new RectangleF(pad, y + S(36f), S(110f), S(18f)), StringAlignment.Center, StringAlignment.Center);

				float x = pad + S(112f);
				Color nameColor = discarded ? Theme.Dim : Theme.Text;
				Theme.DrawText(g, Path.GetFileName(it.Path), Theme.BodyBold(9.5f), nameColor, new RectangleF(x, y + S(10f), w - x - S(250f), S(22f)), StringAlignment.Near, StringAlignment.Center);
				StringBuilder info = new StringBuilder();
				if (known)
				{
					info.Append(it.Width).Append("×").Append(it.Height);
				}
				string dur = VideoProbe.Duration(it.Seconds);
				if (dur.Length > 0)
				{
					info.Append((info.Length > 0) ? " · " : "").Append(dur);
				}
				info.Append((info.Length > 0) ? " · " : "").Append((it.Size / 1048576.0).ToString("0.0")).Append(" MB");
				info.Append(" · ").Append(Store.Nice(it.Modified));
				string t, d;
				if (ReadSidecar(it.Path, out t, out d) && t.Length > 0)
				{
					info.Append(" · «").Append(t).Append("»");
				}
				Theme.DrawText(g, info.ToString(), Theme.Body(8f), Theme.Muted, new RectangleF(x, y + S(34f), w - x - S(250f), S(20f)), StringAlignment.Near, StringAlignment.Center);

				// Acciones a la derecha.
				float right = w - pad;
				float ly = y + S(10f);
				float lh = h - S(20f);
				EditorItem fit = it;
				if (discarded)
				{
					right = Link(g, "Recuperar", right, ly, lh, Theme.Violet, delegate
					{
						SetState(fit, "nuevo");
					}) - S(16f);
				}
				else
				{
					right = Link(g, "Descartar", right, ly, lh, Theme.Muted, delegate
					{
						SetState(fit, "descartado");
					}) - S(16f);
					right = Link(g, (it.State == "usado") ? "Usar otra vez" : "Usar  →", right, ly, lh, Theme.VioletLight, delegate
					{
						Use(fit);
					}) - S(16f);
				}
				right = Link(g, "Ver", right, ly, lh, Theme.Violet, delegate
				{
					OpenFile(fit.Path);
				}) - S(16f);
				if (it.State == "usado")
				{
					Theme.DrawText(g, "PROGRAMADO", Theme.Mono(7f), Theme.Green, new RectangleF(right - S(90f), ly, S(90f), lh), StringAlignment.Far, StringAlignment.Center);
				}
				else if (it.State == "nuevo")
				{
					Theme.DrawText(g, "NUEVO", Theme.Mono(7f), Theme.VioletLight, new RectangleF(right - S(60f), ly, S(60f), lh), StringAlignment.Far, StringAlignment.Center);
				}
			}

			private float Link(Graphics g, string text, float right, float y, float h, Color c, Action click)
			{
				Font f = Theme.BodySemi(9f);
				float tw = Theme.Measure(g, text, f).Width + S(4f);
				RectangleF r = new RectangleF(right - tw, y, tw, h);
				Theme.DrawText(g, text, f, c, r, StringAlignment.Far, StringAlignment.Center);
				hits.Add(new KeyValuePair<RectangleF, Action>(r, click));
				return r.X;
			}
		}
	}
}
