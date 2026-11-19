// Lee y guarda VortexsPost.json junto al .exe. Se guarda primero en un archivo temporal y luego se
// cambia por el bueno (así un corte de luz a mitad no deja el archivo roto); la copia anterior
// queda como VortexsPost.json.bak. Todo acceso a los datos va con lock (Store.Lock), porque las
// subidas trabajan en otro hilo.
using System;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace VortexsPost
{
	internal static class Store
	{
		public static readonly object Lock = new object();

		public static Data D = new Data();

		private static readonly object fileLock = new object();

		public static readonly string Path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "VortexsPost.json");

		private static JavaScriptSerializer Json()
		{
			return new JavaScriptSerializer { MaxJsonLength = 67108864 };
		}

		public static void Load()
		{
			lock (Lock)
			{
				Data d = Read(Path) ?? Read(Path + ".bak");
				bool fresh = d == null;
				D = d ?? new Data();
				if (D.Posts == null)
				{
					D.Posts = new System.Collections.Generic.List<Post>();
				}
				if (D.Presets == null)
				{
					D.Presets = new System.Collections.Generic.List<Preset>();
				}
				if (D.Editor == null)
				{
					D.Editor = new System.Collections.Generic.List<EditorItem>();
				}
				if (D.Acc == null)
				{
					D.Acc = new Accounts();
				}
				foreach (Post p in D.Posts)
				{
					if (p.Targets == null)
					{
						p.Targets = new System.Collections.Generic.List<Target>();
					}
					// Si el programa se cerró a mitad de una subida, se vuelve a intentar.
					foreach (Target t in p.Targets)
					{
						if (t.Status == St.Uploading)
						{
							t.Status = St.Pending;
							t.Progress = -1;
							t.Msg = "Se cerró a mitad de la subida: se vuelve a subir";
						}
					}
				}
				if (fresh && D.Presets.Count == 0)
				{
					// Ejemplo para empezar: mañana a las 15:00 en las tres.
					D.Presets.Add(new Preset { Name = "Mañana a las 15:00", Mode = 2, Time = "15:00" });
					D.DefaultPreset = "Mañana a las 15:00";
				}
			}
		}

		private static Data Read(string file)
		{
			try
			{
				if (!File.Exists(file))
				{
					return null;
				}
				return Json().Deserialize<Data>(File.ReadAllText(file, Encoding.UTF8));
			}
			catch
			{
				return null;
			}
		}

		public static void Save()
		{
			string text;
			lock (Lock)
			{
				text = Json().Serialize(D);
			}
			lock (fileLock)
			{
				try
				{
					string tmp = Path + ".tmp";
					File.WriteAllText(tmp, text, new UTF8Encoding(false));
					if (File.Exists(Path))
					{
						File.Replace(tmp, Path, Path + ".bak", true);
					}
					else
					{
						File.Move(tmp, Path);
					}
				}
				catch
				{
					try
					{
						File.WriteAllText(Path, text, new UTF8Encoding(false));
					}
					catch
					{
					}
				}
			}
		}

		// ---------- Horas ----------

		// "HH:mm" → horas y minutos (false si no es una hora válida).
		public static bool ParseTime(string s, out int h, out int m)
		{
			h = (m = 0);
			s = (s ?? "").Trim().Replace('.', ':').Replace('h', ':');
			if (s.EndsWith(":"))
			{
				s = s.TrimEnd(':');
			}
			string[] parts = s.Split(':');
			if (parts.Length == 1)
			{
				if (!int.TryParse(parts[0], out h))
				{
					return false;
				}
			}
			else if (parts.Length != 2 || !int.TryParse(parts[0], out h) || !int.TryParse(parts[1], out m))
			{
				return false;
			}
			if (h >= 0 && h <= 23 && m >= 0)
			{
				return m <= 59;
			}
			return false;
		}

		// "dd/mm/aaaa" (o dd/mm, o con guiones) → fecha.
		public static bool ParseDate(string s, out DateTime d)
		{
			d = DateTime.MinValue;
			string[] parts = (s ?? "").Trim().Replace('-', '/').Replace('.', '/').Split('/');
			int day, month, year = DateTime.Now.Year;
			if (parts.Length < 2 || !int.TryParse(parts[0], out day) || !int.TryParse(parts[1], out month))
			{
				return false;
			}
			if (parts.Length >= 3 && (!int.TryParse(parts[2], out year) || year < 0))
			{
				return false;
			}
			if (year < 100)
			{
				year += 2000;
			}
			try
			{
				d = new DateTime(year, month, day);
				return true;
			}
			catch
			{
				return false;
			}
		}

		// Hora local en la que toca publicar (DateTime.MinValue = ahora). error = qué falla, o null.
		public static DateTime Resolve(Sched s, out string error)
		{
			error = null;
			if (s.Mode == 0)
			{
				return DateTime.MinValue;
			}
			int h, m;
			if (!ParseTime(s.Time, out h, out m))
			{
				error = "La hora «" + s.Time + "» no es válida (ejemplo: 15:00)";
				return DateTime.MinValue;
			}
			DateTime day;
			if (s.Mode == 1)
			{
				day = DateTime.Today;
			}
			else if (s.Mode == 2)
			{
				day = DateTime.Today.AddDays(1.0);
			}
			else if (!ParseDate(s.Date, out day))
			{
				error = "La fecha «" + s.Date + "» no es válida (ejemplo: " + DateTime.Today.AddDays(2.0).ToString("dd/MM/yyyy") + ")";
				return DateTime.MinValue;
			}
			DateTime when = day.Date.AddHours(h).AddMinutes(m);
			if (when <= DateTime.Now.AddMinutes(1.0))
			{
				error = "La hora " + when.ToString("dd/MM HH:mm") + " ya ha pasado";
				return DateTime.MinValue;
			}
			return when;
		}

		// "mañana 15:00", "hoy 21:30", "vie 12/12 15:00" o "ahora".
		public static string Nice(long utcTicks)
		{
			if (utcTicks == 0)
			{
				return "ahora";
			}
			DateTime local = new DateTime(utcTicks, DateTimeKind.Utc).ToLocalTime();
			string time = local.ToString("HH:mm");
			if (local.Date == DateTime.Today)
			{
				return "hoy " + time;
			}
			if (local.Date == DateTime.Today.AddDays(1.0))
			{
				return "mañana " + time;
			}
			if (local.Date == DateTime.Today.AddDays(-1.0))
			{
				return "ayer " + time;
			}
			string[] days = new string[7] { "dom", "lun", "mar", "mié", "jue", "vie", "sáb" };
			return days[(int)local.DayOfWeek] + " " + local.ToString("dd/MM") + " " + time;
		}

		public static string PlatformName(string p)
		{
			if (p == "yt")
			{
				return "YouTube";
			}
			if (p == "tt")
			{
				return "TikTok";
			}
			return "Instagram";
		}
	}
}
