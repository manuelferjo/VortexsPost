// Pestaña Ajustes: ajustes guardados (crear desde Publicar, usar, marcar el que se aplica al abrir,
// borrar) y opciones generales (abrir con Windows, bandeja, no suspender, carpeta de vídeos).
using System;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Win32;

namespace VortexsPost
{
	internal partial class MainForm
	{
		private const int MaxPresets = 6;

		private readonly PaintBox[] presetRows = new PaintBox[MaxPresets];

		private readonly VButton[] presetUse = new VButton[MaxPresets];

		private readonly VButton[] presetDef = new VButton[MaxPresets];

		private readonly VButton[] presetDel = new VButton[MaxPresets];

		private PaintBox presetEmpty;

		private Toggle tgStartup;

		private Toggle tgTray;

		private Toggle tgAwake;

		private Field fldFolder;

		private void BuildSettings(int top)
		{
			Card list = NewCard("Ajustes guardados", "Prepara la pestaña Publicar como quieras y pulsa «Guardar como ajuste». El de ★ se aplica al abrir.", 20f, top, 760f, 330f, PageSettings);
			presetEmpty = Hint(list, 20f, 70f, 720f, 40f, delegate
			{
				return "Aún no hay ajustes guardados.";
			});
			for (int i = 0; i < MaxPresets; i++)
			{
				int idx = i;
				float y = 62f + i * 42f;
				presetRows[i] = Box(list, 16f, y, 470f, 38f, delegate(Graphics g, Rectangle r)
				{
					PaintPresetRow(g, r, idx);
				});
				presetUse[i] = Btn(list, "Usar", ButtonKind.Ghost, 496f, y + 5f, 74f, 28f);
				presetDef[i] = Btn(list, "★ Al abrir", ButtonKind.Pill, 576f, y + 5f, 92f, 28f);
				presetDef[i].FontSize = 8f;
				presetDel[i] = Btn(list, "Borrar", ButtonKind.Danger, 674f, y + 5f, 70f, 28f);
				presetUse[i].Click += delegate
				{
					Preset p = PresetAt(idx);
					if (p != null)
					{
						ApplyPreset(p);
						GoPage(PagePublish);
					}
				};
				presetDef[i].Click += delegate
				{
					Preset p = PresetAt(idx);
					if (p == null)
					{
						return;
					}
					lock (Store.Lock)
					{
						Store.D.DefaultPreset = (Store.D.DefaultPreset == p.Name) ? "" : p.Name;
					}
					Store.Save();
					RefreshPresetList();
				};
				presetDel[i].Click += delegate
				{
					Preset p = PresetAt(idx);
					if (p == null || !Ask("Borrar ajuste", "¿Borrar el ajuste «" + p.Name + "»?", "Borrar"))
					{
						return;
					}
					lock (Store.Lock)
					{
						Store.D.Presets.Remove(p);
						if (Store.D.DefaultPreset == p.Name)
						{
							Store.D.DefaultPreset = "";
						}
					}
					if (currentPreset == p.Name)
					{
						currentPreset = "";
						btnPreset.Text = "Ninguno  ▾";
					}
					Store.Save();
					RefreshPresetList();
				};
			}

			Card gen = NewCard("General", "", 20f, top + 340, 760f, 216f, PageSettings);
			tgStartup = Tg(gen, "Abrir con Windows", "Arranca escondido en la bandeja, listo para publicar", 16f, 52f, 350f, 44f);
			tgTray = Tg(gen, "Seguir en la bandeja al cerrar", "Para que se publique lo programado", 16f, 104f, 350f, 44f);
			tgAwake = Tg(gen, "Que el PC no se suspenda", "Mientras quede algo por subir o publicar", 390f, 52f, 354f, 44f);
			Label(gen, "CARPETA DE VÍDEOS", 390f, 100f, 260f);
			fldFolder = NewField(gen, 390f, 118f, 260f, 34f, false);
			VButton pick = Btn(gen, "Elegir", ButtonKind.Ghost, 658f, 118f, 86f, 34f);
			Hint(gen, 16f, 162f, 728f, 44f, delegate
			{
				return "La carpeta de vídeos es donde se abre «Elegir vídeo»: por ejemplo, la carpeta de Google Drive que compartes con tu editor o la de los clips de Vortexs Live.";
			});
			lock (Store.Lock)
			{
				tgStartup.Checked = Store.D.StartWithWindows;
				tgTray.Checked = Store.D.KeepInTray;
				tgAwake.Checked = Store.D.KeepAwake;
				fldFolder.Box.Text = Store.D.Folder ?? "";
			}
			tgStartup.CheckedChanged += delegate
			{
				if (loading)
				{
					return;
				}
				SetStartup(tgStartup.Checked);
				SaveForm();
			};
			tgTray.CheckedChanged += delegate
			{
				if (!loading)
				{
					SaveForm();
				}
			};
			tgAwake.CheckedChanged += delegate
			{
				if (!loading)
				{
					SaveForm();
				}
			};
			fldFolder.Box.Leave += delegate
			{
				SaveForm();
			};
			pick.Click += delegate
			{
				using (FolderBrowserDialog d = new FolderBrowserDialog())
				{
					d.Description = "Carpeta donde están tus vídeos";
					if (fldFolder.Box.Text.Length > 0 && System.IO.Directory.Exists(fldFolder.Box.Text))
					{
						d.SelectedPath = fldFolder.Box.Text;
					}
					if (d.ShowDialog(this) == DialogResult.OK)
					{
						fldFolder.Box.Text = d.SelectedPath;
						SaveForm();
					}
				}
			};
			RefreshPresetList();
		}

		private void SaveForm()
		{
			if (tgTray == null)
			{
				return;
			}
			lock (Store.Lock)
			{
				Store.D.StartWithWindows = tgStartup.Checked;
				Store.D.KeepInTray = tgTray.Checked;
				Store.D.KeepAwake = tgAwake.Checked;
				Store.D.Folder = fldFolder.Box.Text.Trim();
			}
			Store.Save();
		}

		// Abrir con Windows: entrada en HKCU\...\Run con "--bandeja".
		private void SetStartup(bool on)
		{
			try
			{
				using (RegistryKey k = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run", true))
				{
					if (k == null)
					{
						return;
					}
					if (on)
					{
						k.SetValue("VortexsPost", "\"" + Application.ExecutablePath + "\" --bandeja");
					}
					else if (k.GetValue("VortexsPost") != null)
					{
						k.DeleteValue("VortexsPost", false);
					}
				}
			}
			catch (Exception ex)
			{
				Info("Abrir con Windows", "No se ha podido cambiar: " + ex.Message);
			}
		}

		// ---------- Lista de ajustes ----------

		private Preset PresetAt(int i)
		{
			lock (Store.Lock)
			{
				return (i < Store.D.Presets.Count) ? Store.D.Presets[i] : null;
			}
		}

		private Preset FindPreset(string name)
		{
			if (string.IsNullOrEmpty(name))
			{
				return null;
			}
			lock (Store.Lock)
			{
				foreach (Preset p in Store.D.Presets)
				{
					if (p.Name == name)
					{
						return p;
					}
				}
			}
			return null;
		}

		private void RefreshPresetList()
		{
			int n;
			string def;
			lock (Store.Lock)
			{
				n = Store.D.Presets.Count;
				def = Store.D.DefaultPreset ?? "";
			}
			presetEmpty.Visible = n == 0;
			for (int i = 0; i < MaxPresets; i++)
			{
				bool show = i < n;
				presetRows[i].Visible = show;
				presetUse[i].Visible = show;
				presetDef[i].Visible = show;
				presetDel[i].Visible = show;
				Preset p = PresetAt(i);
				presetDef[i].Selected = p != null && p.Name == def;
				presetRows[i].Invalidate();
				presetDef[i].Invalidate();
			}
		}

		private void PaintPresetRow(Graphics g, Rectangle r, int i)
		{
			Preset p = PresetAt(i);
			if (p == null)
			{
				return;
			}
			Theme.DrawText(g, p.Name, Theme.BodyBold(9.5f), Theme.Text, new RectangleF(0f, 0f, r.Width, S(20f)), StringAlignment.Near, StringAlignment.Center);
			Theme.DrawText(g, Describe(p), Theme.Body(8f), Theme.Muted, new RectangleF(0f, S(19f), r.Width, S(18f)), StringAlignment.Near, StringAlignment.Center);
		}

		private static string WhenText(int mode, int days, string time)
		{
			if (mode == 0)
			{
				return "ahora";
			}
			if (mode == 1)
			{
				return "hoy " + time;
			}
			if (mode == 2)
			{
				return "mañana " + time;
			}
			return "en " + days + " días, " + time;
		}

		// "YouTube, TikTok · mañana 15:00 · #valorant"
		private static string Describe(Preset p)
		{
			System.Collections.Generic.List<string> nets = new System.Collections.Generic.List<string>();
			if (p.Yt)
			{
				nets.Add("YouTube" + (p.OwnYt ? (" (" + WhenText(p.ModeYt, p.DaysYt, p.TimeYt) + ")") : ""));
			}
			if (p.Tt)
			{
				nets.Add("TikTok" + (p.OwnTt ? (" (" + WhenText(p.ModeTt, p.DaysTt, p.TimeTt) + ")") : ""));
			}
			if (p.Ig)
			{
				nets.Add("Instagram" + (p.OwnIg ? (" (" + WhenText(p.ModeIg, p.DaysIg, p.TimeIg) + ")") : ""));
			}
			string s = string.Join(", ", nets.ToArray()) + " · " + WhenText(p.Mode, p.Days, p.Time);
			if ((p.Hashtags ?? "").Trim().Length > 0)
			{
				s += " · " + p.Hashtags.Trim();
			}
			return s;
		}

		// ---------- Aplicar y guardar ajustes ----------

		private void PresetMenu()
		{
			ContextMenuStrip m = DarkMenu();
			lock (Store.Lock)
			{
				foreach (Preset p in Store.D.Presets)
				{
					Preset fp = p;
					ToolStripMenuItem item = new ToolStripMenuItem(p.Name) { Checked = p.Name == currentPreset };
					item.Click += delegate
					{
						ApplyPreset(fp);
					};
					m.Items.Add(item);
				}
			}
			if (m.Items.Count > 0)
			{
				m.Items.Add(new ToolStripSeparator());
			}
			ToolStripMenuItem none = new ToolStripMenuItem("Ninguno");
			none.Click += delegate
			{
				currentPreset = "";
				btnPreset.Text = "Ninguno  ▾";
			};
			m.Items.Add(none);
			ToolStripMenuItem manage = new ToolStripMenuItem("Gestionar ajustes…");
			manage.Click += delegate
			{
				GoPage(PageSettings);
			};
			m.Items.Add(manage);
			m.Show(btnPreset, new Point(0, btnPreset.Height));
		}

		private void ApplyDefaultPreset()
		{
			string def;
			lock (Store.Lock)
			{
				def = Store.D.DefaultPreset ?? "";
			}
			Preset p = FindPreset(def);
			if (p != null)
			{
				ApplyPreset(p);
			}
		}

		private void ApplyPreset(Preset p)
		{
			currentPreset = p.Name;
			btnPreset.Text = p.Name + "  ▾";
			platPills[0].Selected = p.Yt;
			platPills[1].Selected = p.Tt;
			platPills[2].Selected = p.Ig;
			foreach (VButton b in platPills)
			{
				b.Invalidate();
			}
			SetSched(schedAll, p.Mode, p.Days, p.Time);
			tgYtTime.Checked = p.OwnYt;
			tgTtTime.Checked = p.OwnTt;
			tgIgTime.Checked = p.OwnIg;
			SetSched(schedYt, p.ModeYt, p.DaysYt, p.TimeYt);
			SetSched(schedTt, p.ModeTt, p.DaysTt, p.TimeTt);
			SetSched(schedIg, p.ModeIg, p.DaysIg, p.TimeIg);
			SyncScheds();
			fldTags.Box.Text = p.Hashtags ?? "";
			ytPrivacy = (p.YtPrivacy ?? "").Length > 0 ? p.YtPrivacy : "public";
			ytCategory = (p.YtCategory ?? "").Length > 0 ? p.YtCategory : "20";
			tgYtKids.Checked = p.YtKids;
			ttPrivacy = p.TtPrivacy ?? "";
			tgTtComments.Checked = p.TtComments;
			tgTtDuet.Checked = p.TtDuet;
			tgTtStitch.Checked = p.TtStitch;
			tgTtDraft.Checked = p.TtDraft;
			tgIgFeed.Checked = p.IgFeed;
			SyncPills();
			SyncLocked();
			RefreshSummary();
		}

		private void SaveAsPreset()
		{
			string name = AskText("Guardar como ajuste", "NOMBRE DEL AJUSTE", (currentPreset.Length > 0) ? currentPreset : WhenText(schedAll.Mode, DaysAhead(schedAll), schedAll.Time.Box.Text.Trim()));
			if (name == null)
			{
				return;
			}
			Preset p = new Preset
			{
				Name = name,
				Yt = Selected(0),
				Tt = Selected(1),
				Ig = Selected(2),
				Mode = schedAll.Mode,
				Days = DaysAhead(schedAll),
				Time = schedAll.Time.Box.Text.Trim(),
				OwnYt = tgYtTime.Checked,
				ModeYt = schedYt.Mode,
				DaysYt = DaysAhead(schedYt),
				TimeYt = schedYt.Time.Box.Text.Trim(),
				OwnTt = tgTtTime.Checked,
				ModeTt = schedTt.Mode,
				DaysTt = DaysAhead(schedTt),
				TimeTt = schedTt.Time.Box.Text.Trim(),
				OwnIg = tgIgTime.Checked,
				ModeIg = schedIg.Mode,
				DaysIg = DaysAhead(schedIg),
				TimeIg = schedIg.Time.Box.Text.Trim(),
				Hashtags = fldTags.Box.Text.Trim(),
				YtPrivacy = ytPrivacy,
				YtCategory = ytCategory,
				YtKids = tgYtKids.Checked,
				TtPrivacy = ttPrivacy,
				TtComments = tgTtComments.Checked,
				TtDuet = tgTtDuet.Checked,
				TtStitch = tgTtStitch.Checked,
				TtDraft = tgTtDraft.Checked,
				IgFeed = tgIgFeed.Checked
			};
			lock (Store.Lock)
			{
				int at = -1;
				for (int i = 0; i < Store.D.Presets.Count; i++)
				{
					if (Store.D.Presets[i].Name == name)
					{
						at = i;
					}
				}
				if (at >= 0)
				{
					Store.D.Presets[at] = p;
				}
				else if (Store.D.Presets.Count >= MaxPresets)
				{
					p = null;
				}
				else
				{
					Store.D.Presets.Add(p);
				}
			}
			if (p == null)
			{
				Info("Guardar como ajuste", "Puedes tener hasta " + MaxPresets + " ajustes. Borra alguno en la pestaña Ajustes o guárdalo con el nombre de uno que ya tengas para cambiarlo.");
				return;
			}
			Store.Save();
			currentPreset = name;
			btnPreset.Text = name + "  ▾";
			RefreshPresetList();
		}
	}
}
