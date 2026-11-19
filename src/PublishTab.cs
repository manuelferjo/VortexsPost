// Pestaña Publicar: el vídeo, en qué redes, el ajuste guardado y, a la derecha, los detalles con
// cuatro vistas: «Todas» (título, descripción, hashtags y hora comunes) y una por red (texto
// propio, hora propia y opciones de esa red).
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace VortexsPost
{
	internal partial class MainForm
	{
		// Selector de hora: Ahora / Hoy / Mañana / Otro día + día + hora.
		private class SchedUi
		{
			public VButton[] Pills = new VButton[4];

			public Field Date;

			public Field Time;

			public int Mode;

			public bool On = true;

			// Fecha elegida en "Otro día" (se recuerda al cambiar de modo).
			public string Other = "";
		}

		private static readonly string[] Keys3 = new string[3] { "yt", "tt", "ig" };

		private static readonly string[] YtPrivCodes = new string[3] { "public", "unlisted", "private" };

		private static readonly string[] TtPrivCodes = new string[4] { "PUBLIC_TO_EVERYONE", "MUTUAL_FOLLOW_FRIENDS", "FOLLOWER_OF_CREATOR", "SELF_ONLY" };

		private string videoPath = "";

		private string currentPreset = "";

		private PaintBox dropBox;

		private VButton btnPreset;

		private readonly VButton[] platPills = new VButton[3];

		private PaintBox platHint;

		private PaintBox summaryBox;

		private VButton btnPublish;

		private readonly VButton[] viewPills = new VButton[4];

		private readonly List<Control>[] views = new List<Control>[4]
		{
			new List<Control>(),
			new List<Control>(),
			new List<Control>(),
			new List<Control>()
		};

		private int view;

		// Todas
		private Field fldTitle;

		private Field fldDesc;

		private Field fldTags;

		private SchedUi schedAll;

		// YouTube
		private Toggle tgYtOwn;

		private Field fldYtTitle;

		private Field fldYtDesc;

		private Toggle tgYtTime;

		private SchedUi schedYt;

		private readonly VButton[] ytPriv = new VButton[3];

		private string ytPrivacy = "public";

		private VButton btnYtCat;

		private string ytCategory = "20";

		private Toggle tgYtKids;

		// TikTok
		private Toggle tgTtOwn;

		private Field fldTtText;

		private Toggle tgTtTime;

		private SchedUi schedTt;

		private readonly VButton[] ttPriv = new VButton[4];

		private string ttPrivacy = "";

		private Toggle tgTtComments;

		private Toggle tgTtDuet;

		private Toggle tgTtStitch;

		private Toggle tgTtBrand;

		private Toggle tgTtDraft;

		private readonly VButton[] ttBrand = new VButton[2];

		private bool ttBrandOther;

		// Instagram
		private Toggle tgIgOwn;

		private Field fldIgText;

		private Toggle tgIgTime;

		private SchedUi schedIg;

		private Toggle tgIgFeed;

		private void BuildPublish(int top)
		{
			// ---------- Columna izquierda ----------
			Card video = NewCard("Vídeo", "MP4 vertical para Shorts, TikTok y Reels", 20f, top, 370f, 150f, PagePublish);
			dropBox = Box(video, 16f, 56f, 338f, 48f, PaintDrop);
			dropBox.Cursor = Cursors.Hand;
			dropBox.Click += delegate
			{
				ChooseVideo();
			};
			VButton choose = Btn(video, "Elegir vídeo", ButtonKind.Ghost, 16f, 112f, 200f, 28f);
			choose.Click += delegate
			{
				ChooseVideo();
			};
			VButton clear = Btn(video, "Quitar", ButtonKind.Link, 224f, 112f, 130f, 28f);
			clear.Click += delegate
			{
				videoPath = "";
				editorPath = "";
				dropBox.Invalidate();
				RefreshSummary();
			};

			Card preset = NewCard("Ajuste guardado", "Redes, hora y opciones de un clic", 20f, top + 160, 370f, 96f, PagePublish);
			btnPreset = Btn(preset, "Ninguno  ▾", ButtonKind.Ghost, 16f, 58f, 210f, 28f);
			btnPreset.Click += delegate
			{
				PresetMenu();
			};
			VButton savePreset = Btn(preset, "Guardar como ajuste", ButtonKind.Link, 230f, 58f, 124f, 28f);
			savePreset.Click += delegate
			{
				SaveAsPreset();
			};

			Card whereCard = NewCard("Dónde se publica", "", 20f, top + 262, 370f, 110f, PagePublish);
			string[] names = new string[3] { "YouTube", "TikTok", "Instagram" };
			for (int i = 0; i < 3; i++)
			{
				int idx = i;
				platPills[i] = Btn(whereCard, names[i], ButtonKind.Pill, 16f + i * 114f, 48f, 110f, 32f);
				platPills[i].Selected = true;
				platPills[i].Click += delegate
				{
					platPills[idx].Selected = !platPills[idx].Selected;
					platPills[idx].Invalidate();
					RefreshSummary();
				};
			}
			platHint = Hint(whereCard, 16f, 86f, 338f, 18f, PlatHintText);

			Card sum = NewCard("Resumen", "", 20f, top + 382, 370f, 118f, PagePublish);
			summaryBox = Box(sum, 16f, 42f, 338f, 70f, PaintSummary);

			btnPublish = new VButton { Text = "Publicar", Kind = ButtonKind.Primary, FontSize = 10.5f };
			Place(this, btnPublish, 20f, top + 510, 370f, 46f, PagePublish);
			btnPublish.Click += delegate
			{
				DoPublish();
			};

			// ---------- Columna derecha: detalles ----------
			Card det = NewCard("", "", 410f, top, 370f, 556f, PagePublish);
			string[] vnames = new string[4] { "Todas", "YouTube", "TikTok", "Instagram" };
			for (int i = 0; i < 4; i++)
			{
				int idx = i;
				viewPills[i] = Btn(det, vnames[i], ButtonKind.Pill, 16f + i * 86f, 16f, 80f, 30f);
				viewPills[i].Click += delegate
				{
					SetView(idx);
				};
			}

			// Todas
			List<Control> v = views[0];
			v.Add(Label(det, "TÍTULO", 16f, 60f, 200f));
			v.Add(Counter(det, 254f, 60f, delegate
			{
				return fldTitle.Box.Text.Length + " / 100";
			}, delegate
			{
				return fldTitle.Box.Text.Length > 100;
			}));
			fldTitle = NewField(det, 16f, 78f, 338f, 34f, false);
			v.Add(fldTitle);
			v.Add(Label(det, "DESCRIPCIÓN", 16f, 120f, 200f));
			fldDesc = NewField(det, 16f, 138f, 338f, 104f, true);
			v.Add(fldDesc);
			v.Add(Label(det, "HASHTAGS", 16f, 250f, 200f));
			fldTags = NewField(det, 16f, 268f, 338f, 34f, false);
			v.Add(fldTags);
			v.Add(Label(det, "CUÁNDO", 16f, 312f, 200f));
			schedAll = NewSched(det, 16f, 330f, v);
			v.Add(Hint(det, 16f, 428f, 338f, 112f, delegate
			{
				return "YouTube: el título va de título y la descripción y los hashtags, abajo (los hashtags también como etiquetas).\nTikTok e Instagram: título, descripción y hashtags juntos en el texto.\n\n¿Quieres otro texto u otra hora en una red? Elígela arriba y activa «Texto propio» u «Hora propia».";
			}));
			fldTitle.Box.TextChanged += delegate
			{
				SyncLocked();
			};
			fldDesc.Box.TextChanged += delegate
			{
				SyncLocked();
			};
			fldTags.Box.TextChanged += delegate
			{
				SyncLocked();
			};

			// YouTube
			v = views[1];
			tgYtOwn = Tg(det, "Título y descripción propios", null, 16f, 60f, 338f, 26f);
			v.Add(tgYtOwn);
			v.Add(Label(det, "TÍTULO", 16f, 92f, 200f));
			v.Add(Counter(det, 254f, 92f, delegate
			{
				return fldYtTitle.Box.Text.Length + " / 100";
			}, delegate
			{
				return fldYtTitle.Box.Text.Length > 100;
			}));
			fldYtTitle = NewField(det, 16f, 110f, 338f, 34f, false);
			v.Add(fldYtTitle);
			v.Add(Label(det, "DESCRIPCIÓN", 16f, 152f, 200f));
			fldYtDesc = NewField(det, 16f, 170f, 338f, 70f, true);
			v.Add(fldYtDesc);
			tgYtTime = Tg(det, "Hora propia para YouTube", null, 16f, 250f, 338f, 26f);
			v.Add(tgYtTime);
			schedYt = NewSched(det, 16f, 282f, v);
			v.Add(Label(det, "VISIBILIDAD", 16f, 380f, 200f));
			string[] yp = new string[3] { "Público", "Oculto", "Privado" };
			for (int i = 0; i < 3; i++)
			{
				int idx = i;
				ytPriv[i] = Btn(det, yp[i], ButtonKind.Pill, 16f + i * 115f, 398f, 108f, 28f);
				ytPriv[i].Click += delegate
				{
					ytPrivacy = YtPrivCodes[idx];
					SyncPills();
					RefreshSummary();
				};
				v.Add(ytPriv[i]);
			}
			v.Add(Label(det, "CATEGORÍA", 16f, 436f, 200f));
			btnYtCat = Btn(det, "Videojuegos  ▾", ButtonKind.Ghost, 16f, 454f, 200f, 30f);
			btnYtCat.Click += delegate
			{
				ContextMenuStrip m = DarkMenu();
				foreach (string[] c in YouTube.Categories)
				{
					string id = c[0];
					ToolStripMenuItem item = new ToolStripMenuItem(c[1]) { Checked = id == ytCategory };
					item.Click += delegate
					{
						ytCategory = id;
						SyncPills();
					};
					m.Items.Add(item);
				}
				m.Show(btnYtCat, new Point(0, btnYtCat.Height));
			};
			v.Add(btnYtCat);
			tgYtKids = Tg(det, "Para niños", null, 226f, 456f, 128f, 26f);
			v.Add(tgYtKids);
			v.Add(Hint(det, 16f, 494f, 338f, 52f, delegate
			{
				return "Si lo programas, se sube ya como privado y YouTube lo hace público solo a esa hora, aunque apagues el PC.";
			}));

			// TikTok
			v = views[2];
			tgTtOwn = Tg(det, "Texto propio para TikTok", null, 16f, 60f, 338f, 26f);
			v.Add(tgTtOwn);
			v.Add(Label(det, "TEXTO", 16f, 92f, 200f));
			v.Add(Counter(det, 254f, 92f, delegate
			{
				return TtText().Length + " / 2200";
			}, delegate
			{
				return TtText().Length > 2200;
			}));
			fldTtText = NewField(det, 16f, 110f, 338f, 70f, true);
			v.Add(fldTtText);
			tgTtTime = Tg(det, "Hora propia para TikTok", null, 16f, 188f, 338f, 26f);
			v.Add(tgTtTime);
			schedTt = NewSched(det, 16f, 220f, v);
			v.Add(Label(det, "QUIÉN PUEDE VERLO", 16f, 316f, 200f));
			for (int i = 0; i < 4; i++)
			{
				int idx = i;
				ttPriv[i] = Btn(det, TikTok.PrivacyName(TtPrivCodes[i]), ButtonKind.Pill, 16f + i * 86f, 334f, 80f, 28f);
				ttPriv[i].FontSize = 8.5f;
				ttPriv[i].Click += delegate
				{
					ttPrivacy = TtPrivCodes[idx];
					SyncPills();
					RefreshSummary();
				};
				v.Add(ttPriv[i]);
			}
			tgTtComments = Tg(det, "Comentarios", null, 16f, 370f, 128f, 28f);
			tgTtDuet = Tg(det, "Dúos", null, 150f, 370f, 92f, 28f);
			tgTtStitch = Tg(det, "Stitch", null, 248f, 370f, 106f, 28f);
			tgTtComments.Checked = (tgTtDuet.Checked = (tgTtStitch.Checked = true));
			v.Add(tgTtComments);
			v.Add(tgTtDuet);
			v.Add(tgTtStitch);
			tgTtDraft = Tg(det, "Enviar a mis borradores de TikTok", "Lo publicas tú desde la app (no hace falta la cuenta en privado)", 16f, 402f, 338f, 40f);
			v.Add(tgTtDraft);
			tgTtDraft.CheckedChanged += delegate
			{
				SyncPills();
				RefreshSummary();
			};
			tgTtBrand = Tg(det, "Contenido comercial (publicidad)", null, 16f, 440f, 338f, 26f);
			v.Add(tgTtBrand);
			string[] bn = new string[2] { "De mi marca", "De una marca (colaboración)" };
			for (int i = 0; i < 2; i++)
			{
				int idx = i;
				ttBrand[i] = Btn(det, bn[i], ButtonKind.Pill, (i == 0) ? 16f : 140f, 470f, (i == 0) ? 118f : 214f, 24f);
				ttBrand[i].FontSize = 8.5f;
				ttBrand[i].Click += delegate
				{
					ttBrandOther = idx == 1;
					SyncPills();
				};
				v.Add(ttBrand[i]);
			}
			tgTtBrand.CheckedChanged += delegate
			{
				SyncPills();
			};
			v.Add(Hint(det, 16f, 502f, 338f, 48f, delegate
			{
				if (!pub.Tt.Connected)
				{
					return "Conecta TikTok en la pestaña Cuentas.";
				}
				if (tgTtDraft.Checked)
				{
					return "A la hora elegida el vídeo llega a tus borradores de TikTok (notificación en el móvil). El texto lo copias desde la pestaña Cola.";
				}
				string who = (pub.Tt.Info != null && pub.Tt.Info.Nickname.Length > 0) ? pub.Tt.Info.Nickname : pub.Tt.Account;
				return "Publicas como " + who + ". Al publicar aceptas la Music Usage Confirmation de TikTok." + (ttBrandOther && tgTtBrand.Checked ? " Y su Branded Content Policy." : "");
			}));

			// Instagram
			v = views[3];
			tgIgOwn = Tg(det, "Texto propio para Instagram", null, 16f, 60f, 338f, 26f);
			v.Add(tgIgOwn);
			v.Add(Label(det, "TEXTO", 16f, 92f, 200f));
			v.Add(Counter(det, 254f, 92f, delegate
			{
				return IgText().Length + " / 2200";
			}, delegate
			{
				return IgText().Length > 2200;
			}));
			fldIgText = NewField(det, 16f, 110f, 338f, 120f, true);
			v.Add(fldIgText);
			tgIgTime = Tg(det, "Hora propia para Instagram", null, 16f, 240f, 338f, 26f);
			v.Add(tgIgTime);
			schedIg = NewSched(det, 16f, 272f, v);
			tgIgFeed = Tg(det, "Mostrar también en el feed", "Si lo apagas, el reel solo sale en la pestaña Reels", 16f, 372f, 338f, 44f);
			tgIgFeed.Checked = true;
			v.Add(tgIgFeed);
			v.Add(Hint(det, 16f, 428f, 338f, 110f, delegate
			{
				return "Instagram no deja programar desde fuera: Vortexs Post sube el reel 10 minutos antes y lo publica a su hora. Para eso el PC tiene que estar encendido con el programa abierto (vale minimizado en la bandeja).";
			}));

			// Interruptores de texto y hora propios.
			Toggle[] owns = new Toggle[3] { tgYtOwn, tgTtOwn, tgIgOwn };
			foreach (Toggle t in owns)
			{
				t.CheckedChanged += delegate
				{
					SyncLocked();
					RefreshSummary();
				};
			}
			Toggle[] times = new Toggle[3] { tgYtTime, tgTtTime, tgIgTime };
			foreach (Toggle t in times)
			{
				t.CheckedChanged += delegate
				{
					SyncScheds();
					RefreshSummary();
				};
			}
			fldYtTitle.Box.TextChanged += delegate
			{
				InvalidateCounters();
			};
			fldTtText.Box.TextChanged += delegate
			{
				InvalidateCounters();
			};
			fldIgText.Box.TextChanged += delegate
			{
				InvalidateCounters();
			};
			SyncLocked();
			SyncScheds();
			SyncPills();
			SetView(0);
		}

		// Contador "12 / 100" alineado a la derecha (en rojo si se pasa).
		private PaintBox Counter(Control parent, float x, float y, Func<string> text, Func<bool> over)
		{
			PaintBox b = Box(parent, x, y, 100f, 18f, delegate(Graphics g, Rectangle r)
			{
				Theme.DrawText(g, text(), Theme.Mono(7f), over() ? Theme.Red : Theme.Dim, r, StringAlignment.Far, StringAlignment.Center);
			});
			b.Tag = "counter";
			return b;
		}

		private void InvalidateCounters()
		{
			foreach (List<Control> list in views)
			{
				foreach (Control c in list)
				{
					if (c is PaintBox && (string)c.Tag == "counter")
					{
						c.Invalidate();
					}
				}
			}
		}

		private void SetView(int i)
		{
			view = i;
			for (int k = 0; k < 4; k++)
			{
				viewPills[k].Selected = k == i;
				viewPills[k].Invalidate();
				foreach (Control c in views[k])
				{
					c.Visible = k == i;
				}
			}
			if (i == 2)
			{
				RefreshTikTokInfo();
			}
		}

		// ---------- Selector de hora ----------

		private SchedUi NewSched(Control parent, float x, float y, List<Control> view)
		{
			SchedUi s = new SchedUi();
			string[] names = new string[4] { "Ahora", "Hoy", "Mañana", "Otro día" };
			for (int i = 0; i < 4; i++)
			{
				int idx = i;
				s.Pills[i] = Btn(parent, names[i], ButtonKind.Pill, x + i * 86f, y, 80f, 28f);
				s.Pills[i].FontSize = 8.5f;
				s.Pills[i].Click += delegate
				{
					if (!s.On)
					{
						return;
					}
					SetMode(s, idx);
				};
				view.Add(s.Pills[i]);
			}
			view.Add(Label(parent, "DÍA", x, y + 36f, 150f));
			view.Add(Label(parent, "HORA", x + 160f, y + 36f, 100f));
			s.Date = NewField(parent, x, y + 54f, 150f, 34f, false);
			s.Time = NewField(parent, x + 160f, y + 54f, 100f, 34f, false);
			s.Time.Box.Text = "15:00";
			s.Other = DateTime.Today.AddDays(2.0).ToString("dd/MM/yyyy");
			view.Add(s.Date);
			view.Add(s.Time);
			s.Date.Box.TextChanged += delegate
			{
				if (s.Mode == 3 && s.On)
				{
					s.Other = s.Date.Box.Text;
				}
				if (s == schedAll)
				{
					SyncScheds();
				}
				RefreshSummary();
			};
			s.Time.Box.TextChanged += delegate
			{
				if (s == schedAll)
				{
					SyncScheds();
				}
				RefreshSummary();
			};
			SetMode(s, 0);
			return s;
		}

		private void SetMode(SchedUi s, int mode)
		{
			s.Mode = mode;
			UpdateSched(s);
			if (s == schedAll)
			{
				SyncScheds();
			}
			RefreshSummary();
		}

		private void UpdateSched(SchedUi s)
		{
			for (int i = 0; i < 4; i++)
			{
				s.Pills[i].Selected = i == s.Mode;
				s.Pills[i].Enabled = s.On;
				s.Pills[i].Invalidate();
			}
			string date;
			if (s.Mode == 0)
			{
				date = "en cuanto se suba";
			}
			else if (s.Mode == 1)
			{
				date = DateTime.Today.ToString("dd/MM/yyyy");
			}
			else if (s.Mode == 2)
			{
				date = DateTime.Today.AddDays(1.0).ToString("dd/MM/yyyy");
			}
			else
			{
				date = s.Other;
			}
			if (s.Date.Box.Text != date)
			{
				s.Date.Box.Text = date;
			}
			s.Date.Locked = !s.On || s.Mode != 3;
			s.Time.Locked = !s.On || s.Mode == 0;
		}

		private Sched GetSched(SchedUi s)
		{
			return new Sched { Mode = s.Mode, Date = (s.Mode == 3) ? s.Date.Box.Text.Trim() : "", Time = s.Time.Box.Text.Trim() };
		}

		private void SetSched(SchedUi s, int mode, int days, string time)
		{
			if (mode == 3)
			{
				s.Other = DateTime.Today.AddDays(Math.Max(1, days)).ToString("dd/MM/yyyy");
			}
			s.Time.Box.Text = (time ?? "").Length > 0 ? time : "15:00";
			SetMode(s, Math.Max(0, Math.Min(3, mode)));
		}

		// Días desde hoy de la fecha de "Otro día" (para guardarlo en un ajuste).
		private static int DaysAhead(SchedUi s)
		{
			if (s.Mode == 1)
			{
				return 0;
			}
			if (s.Mode == 2)
			{
				return 1;
			}
			DateTime d;
			if (s.Mode == 3 && Store.ParseDate(s.Date.Box.Text, out d))
			{
				return Math.Max(1, (int)(d.Date - DateTime.Today).TotalDays);
			}
			return 1;
		}

		// La hora propia de cada red sigue a «Todas» mientras esté apagada.
		private void SyncScheds()
		{
			Toggle[] times = new Toggle[3] { tgYtTime, tgTtTime, tgIgTime };
			SchedUi[] s = new SchedUi[3] { schedYt, schedTt, schedIg };
			for (int i = 0; i < 3; i++)
			{
				if (s[i] == null)
				{
					continue;
				}
				s[i].On = times[i].Checked;
				if (!s[i].On && schedAll != null)
				{
					// Apagada: enseña (en gris) la hora de «Todas».
					s[i].Mode = schedAll.Mode;
					if (schedAll.Mode == 3)
					{
						s[i].Other = schedAll.Date.Box.Text;
					}
					if (s[i].Time.Box.Text != schedAll.Time.Box.Text)
					{
						s[i].Time.Box.Text = schedAll.Time.Box.Text;
					}
				}
				UpdateSched(s[i]);
			}
		}

		private SchedUi Effective(int p)
		{
			if (p == 0 && tgYtTime.Checked)
			{
				return schedYt;
			}
			if (p == 1 && tgTtTime.Checked)
			{
				return schedTt;
			}
			if (p == 2 && tgIgTime.Checked)
			{
				return schedIg;
			}
			return schedAll;
		}

		// ---------- Textos ----------

		private static string Join(params string[] parts)
		{
			StringBuilder sb = new StringBuilder();
			foreach (string p in parts)
			{
				string t = (p ?? "").Trim();
				if (t.Length > 0)
				{
					if (sb.Length > 0)
					{
						sb.Append("\r\n\r\n");
					}
					sb.Append(t);
				}
			}
			return sb.ToString();
		}

		private string SharedText()
		{
			return Join(fldTitle.Box.Text, fldDesc.Box.Text, fldTags.Box.Text);
		}

		private string YtTitle()
		{
			string t = tgYtOwn.Checked ? fldYtTitle.Box.Text.Trim() : fldTitle.Box.Text.Trim();
			if (t.Length == 0 && videoPath.Length > 0)
			{
				t = Path.GetFileNameWithoutExtension(videoPath);
			}
			return t;
		}

		private string YtDesc()
		{
			return tgYtOwn.Checked ? fldYtDesc.Box.Text : Join(fldDesc.Box.Text, fldTags.Box.Text);
		}

		private string TtText()
		{
			return tgTtOwn.Checked ? fldTtText.Box.Text : SharedText();
		}

		private string IgText()
		{
			return tgIgOwn.Checked ? fldIgText.Box.Text : SharedText();
		}

		// Etiquetas de YouTube: las palabras con # de los hashtags (y de la descripción propia).
		private string YtTags()
		{
			List<string> tags = new List<string>();
			string src = fldTags.Box.Text + " " + (tgYtOwn.Checked ? fldYtDesc.Box.Text : "");
			foreach (string w in src.Split(new char[5] { ' ', ',', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries))
			{
				if (w.StartsWith("#") && w.Length > 1)
				{
					string tag = w.TrimStart('#');
					if (!tags.Contains(tag))
					{
						tags.Add(tag);
					}
				}
			}
			return string.Join(",", tags.ToArray());
		}

		// Los textos propios apagados enseñan (en gris) lo que se publicará con «Todas».
		private void SyncLocked()
		{
			if (tgIgOwn == null)
			{
				return;
			}
			fldYtTitle.Locked = !tgYtOwn.Checked;
			fldYtDesc.Locked = !tgYtOwn.Checked;
			fldTtText.Locked = !tgTtOwn.Checked;
			fldIgText.Locked = !tgIgOwn.Checked;
			if (!tgYtOwn.Checked)
			{
				SetIfDifferent(fldYtTitle, fldTitle.Box.Text.Trim());
				SetIfDifferent(fldYtDesc, Join(fldDesc.Box.Text, fldTags.Box.Text));
			}
			string shared = SharedText();
			if (!tgTtOwn.Checked)
			{
				SetIfDifferent(fldTtText, shared);
			}
			if (!tgIgOwn.Checked)
			{
				SetIfDifferent(fldIgText, shared);
			}
			InvalidateCounters();
		}

		private static void SetIfDifferent(Field f, string text)
		{
			if (f.Box.Text != text)
			{
				f.Box.Text = text;
			}
		}

		// Pastillas elegidas (privacidad, categoría, marca) y las que TikTok no deja usar.
		private void SyncPills()
		{
			for (int i = 0; i < 3; i++)
			{
				ytPriv[i].Selected = YtPrivCodes[i] == ytPrivacy;
				ytPriv[i].Invalidate();
			}
			btnYtCat.Text = YouTube.CategoryName(ytCategory) + "  ▾";
			TikTok.CreatorInfo info = pub.Tt.Info;
			for (int i = 0; i < 4; i++)
			{
				ttPriv[i].Selected = TtPrivCodes[i] == ttPrivacy;
				ttPriv[i].Enabled = !tgTtDraft.Checked && (info == null || info.Privacy.Count == 0 || info.Privacy.Contains(TtPrivCodes[i]));
				ttPriv[i].Invalidate();
			}
			if (info != null)
			{
				tgTtComments.Enabled = !info.CommentOff;
				tgTtDuet.Enabled = !info.DuetOff;
				tgTtStitch.Enabled = !info.StitchOff;
			}
			for (int i = 0; i < 2; i++)
			{
				ttBrand[i].Enabled = tgTtBrand.Checked;
				ttBrand[i].Selected = tgTtBrand.Checked && ((i == 1) == ttBrandOther);
				ttBrand[i].Invalidate();
			}
		}

		// Lo que TikTok deja hacer ahora con la cuenta (privacidades, nombre).
		private void RefreshTikTokInfo()
		{
			if (!pub.Tt.Connected)
			{
				return;
			}
			Background(delegate
			{
				pub.Tt.ReadInfo();
			}, delegate
			{
				SyncPills();
				foreach (Control c in views[2])
				{
					c.Invalidate();
				}
				platHint.Invalidate();
			});
		}

		// ---------- Vídeo ----------

		private void ChooseVideo()
		{
			using (OpenFileDialog d = new OpenFileDialog())
			{
				d.Title = "Elige el vídeo";
				d.Filter = "Vídeos (*.mp4, *.mov, *.m4v, *.webm)|*.mp4;*.mov;*.m4v;*.webm|Todos los archivos|*.*";
				string folder;
				lock (Store.Lock)
				{
					folder = Store.D.Folder ?? "";
				}
				if (videoPath.Length > 0 && Directory.Exists(Path.GetDirectoryName(videoPath)))
				{
					d.InitialDirectory = Path.GetDirectoryName(videoPath);
				}
				else if (folder.Length > 0 && Directory.Exists(folder))
				{
					d.InitialDirectory = folder;
				}
				if (d.ShowDialog(this) == DialogResult.OK)
				{
					SetVideo(d.FileName);
				}
			}
		}

		private void SetVideo(string path)
		{
			if (!File.Exists(path))
			{
				return;
			}
			videoPath = path;
			dropBox.Invalidate();
			SyncLocked();
			RefreshSummary();
		}

		private void PaintDrop(Graphics g, Rectangle r)
		{
			RectangleF box = new RectangleF(0.5f, 0.5f, r.Width - 1.5f, r.Height - 1.5f);
			using (GraphicsPath path = Theme.Round(box, S(10f)))
			{
				using (SolidBrush b = new SolidBrush(Theme.Input))
				{
					g.FillPath(b, path);
				}
				using (Pen p = new Pen(videoPath.Length > 0 ? Color.FromArgb(140, Theme.Violet) : Theme.BorderHi, 1.2f) { DashStyle = videoPath.Length > 0 ? DashStyle.Solid : DashStyle.Dash })
				{
					g.DrawPath(p, path);
				}
			}
			if (videoPath.Length == 0)
			{
				Theme.DrawText(g, "Arrastra aquí el vídeo o pulsa «Elegir vídeo»", Theme.Body(9f), Theme.Muted, new RectangleF(S(12f), 0f, r.Width - S(24f), r.Height), StringAlignment.Center, StringAlignment.Center);
				return;
			}
			string size = "";
			try
			{
				size = (new FileInfo(videoPath).Length / 1048576.0).ToString("0.0", CultureInfo.GetCultureInfo("es-ES")) + " MB";
			}
			catch
			{
				size = "no se encuentra";
			}
			Theme.DrawText(g, Path.GetFileName(videoPath), Theme.BodyBold(9.5f), Theme.Text, new RectangleF(S(12f), S(5f), r.Width - S(24f), S(20f)), StringAlignment.Near, StringAlignment.Center);
			Theme.DrawText(g, size + " · " + Path.GetDirectoryName(videoPath), Theme.Body(8f), Theme.Muted, new RectangleF(S(12f), S(24f), r.Width - S(24f), S(18f)), StringAlignment.Near, StringAlignment.Center);
		}

		// ---------- Resumen ----------

		private bool Selected(int p)
		{
			return platPills[p].Selected;
		}

		private bool IsConnected(int p)
		{
			if (p == 0)
			{
				return pub.Yt.Connected;
			}
			if (p == 1)
			{
				return pub.Tt.Connected;
			}
			return pub.Ig.Connected;
		}

		private string PlatHintText()
		{
			List<string> off = new List<string>();
			for (int i = 0; i < 3; i++)
			{
				if (Selected(i) && !IsConnected(i))
				{
					off.Add(Store.PlatformName(Keys3[i]));
				}
			}
			if (off.Count == 0)
			{
				return "Elige una o varias.";
			}
			return "Sin conectar: " + string.Join(", ", off.ToArray()) + " (pestaña Cuentas)";
		}

		private static string YtPrivName(string code)
		{
			if (code == "unlisted")
			{
				return "Oculto";
			}
			if (code == "private")
			{
				return "Privado";
			}
			return "Público";
		}

		private void RefreshSummary()
		{
			if (summaryBox == null || btnPublish == null || tgIgTime == null)
			{
				return;
			}
			bool any = false;
			bool later = false;
			for (int i = 0; i < 3; i++)
			{
				if (Selected(i))
				{
					any = true;
					if (Effective(i).Mode != 0)
					{
						later = true;
					}
				}
			}
			btnPublish.Text = !any ? "Elige dónde publicar" : (later ? "Programar" : "Publicar ahora");
			summaryBox.Invalidate();
			platHint.Invalidate();
		}

		private void PaintSummary(Graphics g, Rectangle r)
		{
			Color[] dots = new Color[3] { Color.FromArgb(255, 70, 85), Color.FromArgb(37, 244, 238), Color.FromArgb(225, 48, 108) };
			float y = 0f;
			float lh = S(22f);
			int lines = 0;
			for (int i = 0; i < 3; i++)
			{
				if (!Selected(i))
				{
					continue;
				}
				lines++;
				using (SolidBrush b = new SolidBrush(dots[i]))
				{
					g.FillEllipse(b, 0f, y + lh / 2f - S(3.5f), S(7f), S(7f));
				}
				Theme.DrawText(g, Store.PlatformName(Keys3[i]), Theme.BodySemi(9f), Theme.Text, new RectangleF(S(14f), y, S(80f), lh), StringAlignment.Near, StringAlignment.Center);
				string text;
				Color c = Theme.Muted;
				string err;
				DateTime when = Store.Resolve(GetSched(Effective(i)), out err);
				if (!IsConnected(i))
				{
					text = "sin conectar";
					c = Theme.Red;
				}
				else if (err != null)
				{
					text = err;
					c = Theme.Red;
				}
				else
				{
					text = (when == DateTime.MinValue) ? "ahora" : Store.Nice(when.ToUniversalTime().Ticks);
					if (i == 0)
					{
						text += " · " + ((when == DateTime.MinValue) ? YtPrivName(ytPrivacy) : "Público a esa hora");
					}
					else if (i == 1)
					{
						text += " · " + (tgTtDraft.Checked ? "a tus borradores" : (ttPrivacy.Length > 0 ? TikTok.PrivacyName(ttPrivacy) : "elige quién puede verlo"));
						if (ttPrivacy.Length == 0 && !tgTtDraft.Checked)
						{
							c = Theme.Yellow;
						}
					}
					else
					{
						text += tgIgFeed.Checked ? " · Reels y feed" : " · solo Reels";
					}
				}
				Theme.DrawText(g, text, Theme.Body(8.5f), c, new RectangleF(S(96f), y, r.Width - S(96f), lh), StringAlignment.Near, StringAlignment.Center);
				y += lh;
			}
			if (lines == 0)
			{
				Theme.DrawText(g, "Elige al menos una red.", Theme.Body(8.5f), Theme.Muted, new RectangleF(0f, 0f, r.Width, lh), StringAlignment.Near, StringAlignment.Center);
			}
		}

		// ---------- Publicar ----------

		private void DoPublish()
		{
			List<string> problems = new List<string>();
			if (videoPath.Length == 0 || !File.Exists(videoPath))
			{
				problems.Add("Elige el vídeo.");
			}
			bool any = false;
			DateTime[] when = new DateTime[3];
			for (int i = 0; i < 3; i++)
			{
				if (!Selected(i))
				{
					continue;
				}
				any = true;
				string name = Store.PlatformName(Keys3[i]);
				if (!IsConnected(i))
				{
					problems.Add(name + " no está conectado (pestaña Cuentas).");
				}
				string err;
				when[i] = Store.Resolve(GetSched(Effective(i)), out err);
				if (err != null)
				{
					problems.Add(name + ": " + err + ".");
				}
			}
			if (!any)
			{
				problems.Add("Elige al menos una red.");
			}
			if (Selected(0))
			{
				if (YtTitle().Length > 100)
				{
					problems.Add("YouTube: el título pasa de 100 caracteres.");
				}
				if (when[0] != DateTime.MinValue && ytPrivacy != "public")
				{
					problems.Add("YouTube: para programar elige «Público» (YouTube lo hace público a esa hora). Si lo quieres oculto o privado, publícalo «Ahora».");
				}
			}
			if (Selected(1))
			{
				if (ttPrivacy.Length == 0 && !tgTtDraft.Checked)
				{
					problems.Add("TikTok: elige quién puede verlo.");
				}
				else if (tgTtBrand.Checked && ttPrivacy == "SELF_ONLY" && !tgTtDraft.Checked)
				{
					problems.Add("TikTok: el contenido comercial no puede ser «Solo yo».");
				}
				if (TtText().Length > 2200)
				{
					problems.Add("TikTok: el texto pasa de 2200 caracteres.");
				}
			}
			if (Selected(2) && IgText().Length > 2200)
			{
				problems.Add("Instagram: el texto pasa de 2200 caracteres.");
			}
			if (problems.Count > 0)
			{
				Info("Falta algo", string.Join("\n", problems.ToArray()));
				return;
			}
			Post post = new Post { Id = Guid.NewGuid().ToString("N"), File = videoPath, Created = DateTime.UtcNow.Ticks };
			for (int i = 0; i < 3; i++)
			{
				if (!Selected(i))
				{
					continue;
				}
				Target t = new Target { P = Keys3[i], When = (when[i] == DateTime.MinValue) ? 0L : when[i].ToUniversalTime().Ticks };
				if (i == 0)
				{
					t.Title = YtTitle();
					t.Text = YtDesc();
					t.Tags = YtTags();
					t.Privacy = ytPrivacy;
					t.Category = ytCategory;
					t.Kids = tgYtKids.Checked;
				}
				else if (i == 1)
				{
					t.Text = TtText();
					t.Privacy = ttPrivacy;
					t.Comments = tgTtComments.Checked;
					t.Duet = tgTtDuet.Checked;
					t.Stitch = tgTtStitch.Checked;
					t.Brand = tgTtBrand.Checked;
					t.BrandOther = ttBrandOther;
					t.Draft = tgTtDraft.Checked;
				}
				else
				{
					t.Text = IgText();
					t.Feed = tgIgFeed.Checked;
				}
				post.Targets.Add(t);
			}
			pub.Add(post);
			pub.Tick();
			EditorPublished(post.File);
			// Formulario listo para el siguiente vídeo (se queda el ajuste y los hashtags).
			videoPath = "";
			fldTitle.Box.Text = "";
			fldDesc.Box.Text = "";
			tgYtOwn.Checked = (tgTtOwn.Checked = (tgIgOwn.Checked = false));
			dropBox.Invalidate();
			Preset p = FindPreset(currentPreset);
			if (p != null)
			{
				ApplyPreset(p);
			}
			SyncLocked();
			RefreshSummary();
			GoPage(PageQueue);
		}
	}
}
