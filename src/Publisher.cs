// El programador: cada pocos segundos mira la cola y lanza lo que toca, de uno en uno, en otro hilo.
//   · YouTube: se sube en cuanto se crea la publicación (con publishAt si está programada; YouTube la
//     publica sola a su hora).
//   · TikTok: se sube y publica a su hora.
//   · Instagram: se sube y procesa 10 minutos antes y se publica a su hora.
// Si el PC estaba apagado a la hora, se publica al abrir el programa (y se apunta el retraso).
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace VortexsPost
{
	internal class Publisher
	{
		public readonly YouTube Yt = new YouTube();

		public readonly TikTok Tt = new TikTok();

		public readonly Instagram Ig = new Instagram();

		// Avisos para la ventana (título, texto, ¿error?). Llegan desde el hilo de subida.
		public Action<string, string, bool> Notify;

		// Algo ha cambiado (para volver a dibujar la cola).
		public volatile int Changes;

		private volatile bool busy;

		private volatile Target cancelTarget;

		private static readonly TimeSpan IgEarly = TimeSpan.FromMinutes(10.0);

		public bool Busy
		{
			get
			{
				return busy;
			}
		}

		public void LoadAccounts()
		{
			lock (Store.Lock)
			{
				Yt.Load(Store.D.Acc);
				Tt.Load(Store.D.Acc);
				Ig.Load(Store.D.Acc);
			}
		}

		public void SaveAccounts()
		{
			lock (Store.Lock)
			{
				Yt.Save(Store.D.Acc);
				Tt.Save(Store.D.Acc);
				Ig.Save(Store.D.Acc);
			}
			Store.Save();
		}

		public void Cancel(Target t)
		{
			cancelTarget = t;
		}

		// ¿Hay algo que el PC tenga que hacer más adelante? (para no dejar que se suspenda)
		public bool HasLocalWork()
		{
			lock (Store.Lock)
			{
				foreach (Post p in Store.D.Posts)
				{
					foreach (Target t in p.Targets)
					{
						if (t.Status == St.Pending || t.Status == St.Uploading || t.Status == St.Ready)
						{
							return true;
						}
					}
				}
			}
			return false;
		}

		// Lo llama la ventana cada pocos segundos.
		public void Tick()
		{
			if (busy)
			{
				return;
			}
			Post post = null;
			Target job = null;
			bool publishOnly = false;
			DateTime now = DateTime.UtcNow;
			lock (Store.Lock)
			{
				long best = long.MaxValue;
				foreach (Post p in Store.D.Posts)
				{
					foreach (Target t in p.Targets)
					{
						long due;
						bool onlyPublish = false;
						if (t.Status == St.Pending)
						{
							if (t.P == "yt")
							{
								due = 0L;
							}
							else if (t.P == "ig")
							{
								due = (t.When == 0) ? 0L : (t.When - IgEarly.Ticks);
							}
							else
							{
								due = t.When;
							}
						}
						else if (t.Status == St.Ready)
						{
							due = t.When;
							onlyPublish = true;
						}
						else
						{
							continue;
						}
						if (due <= now.Ticks && due < best)
						{
							best = due;
							post = p;
							job = t;
							publishOnly = onlyPublish;
						}
					}
				}
				if (job == null)
				{
					return;
				}
				job.Status = St.Uploading;
				job.Progress = publishOnly ? 99 : 0;
				busy = true;
			}
			Changes++;
			Post fp = post;
			Target ft = job;
			bool fo = publishOnly;
			Thread th = new Thread((ThreadStart)delegate
			{
				Run(fp, ft, fo);
			});
			th.IsBackground = true;
			th.Start();
		}

		private void Run(Post post, Target t, bool publishOnly)
		{
			string name = Store.PlatformName(t.P);
			string file = post.File;
			Func<bool> cancel = delegate
			{
				return cancelTarget == t;
			};
			Action<int> progress = delegate(int pct)
			{
				if (pct != t.Progress)
				{
					t.Progress = pct;
					Changes++;
				}
			};
			lock (Store.Lock)
			{
				t.Msg = "";
			}
			try
			{
				if (!publishOnly && !File.Exists(file))
				{
					throw new ApiException(0, "no encuentro el vídeo: " + file);
				}
				string late = "";
				if (t.When > 0 && t.P != "yt" && DateTime.UtcNow.Ticks - t.When > TimeSpan.FromMinutes(5.0).Ticks)
				{
					late = " (con " + Late(DateTime.UtcNow - new DateTime(t.When, DateTimeKind.Utc)) + " de retraso: el programa no estaba abierto a la hora)";
				}
				if (t.P == "yt")
				{
					if (t.When > 0 && t.When <= DateTime.UtcNow.AddMinutes(1.0).Ticks)
					{
						// La hora ya ha pasado (el PC estaba apagado): se publica ya.
						t.When = 0;
					}
					string id = Yt.Upload(t, file, progress, cancel);
					Finish(t, (t.When > 0) ? St.Scheduled : St.Done, "https://youtu.be/" + id, t.Msg);
					Say((t.When > 0) ? ("YouTube: programado para " + Store.Nice(t.When)) : "YouTube: publicado", Path.GetFileName(file), false);
				}
				else if (t.P == "tt")
				{
					string url = Tt.Publish(t, file, progress, cancel);
					Finish(t, St.Done, url, t.Msg + late);
					Say(t.Draft ? "TikTok: en tus borradores" : "TikTok: publicado", Path.GetFileName(file), false);
				}
				else
				{
					if (!publishOnly)
					{
						t.Extra = Ig.Prepare(t, file, progress, cancel);
					}
					if (t.When > DateTime.UtcNow.Ticks)
					{
						// Ya subido: espera a su hora.
						lock (Store.Lock)
						{
							t.Status = St.Ready;
							t.Progress = -1;
							t.Msg = "Subido: se publica " + Store.Nice(t.When);
						}
					}
					else
					{
						string url = Ig.Publish(t.Extra);
						Finish(t, St.Done, url, late.Trim());
						Say("Instagram: publicado", Path.GetFileName(file), false);
					}
				}
			}
			catch (OperationCanceledException)
			{
				Finish(t, St.Cancelled, "", "Cancelado");
			}
			catch (Exception ex)
			{
				string msg = ex.Message;
				if (msg.Length > 0)
				{
					msg = char.ToUpperInvariant(msg[0]) + msg.Substring(1);
				}
				// Instagram: si el contenedor ya no vale, se vuelve a subir al reintentar.
				if (t.P == "ig" && publishOnly)
				{
					t.Extra = "";
				}
				Finish(t, St.Error, "", msg);
				Say(name + ": no se ha podido publicar", msg, true);
			}
			finally
			{
				if (cancelTarget == t)
				{
					cancelTarget = null;
				}
				// Los tokens pueden haberse renovado.
				SaveAccounts();
				busy = false;
				Changes++;
			}
		}

		private void Finish(Target t, string status, string url, string msg)
		{
			lock (Store.Lock)
			{
				t.Status = status;
				t.Progress = -1;
				if (url.Length > 0)
				{
					t.Url = url;
				}
				t.Msg = (msg ?? "").Trim();
				t.DoneAt = DateTime.UtcNow.Ticks;
			}
		}

		private void Say(string title, string text, bool error)
		{
			if (Notify != null)
			{
				try
				{
					Notify(title, text, error);
				}
				catch
				{
				}
			}
		}

		private static string Late(TimeSpan d)
		{
			if (d.TotalHours >= 1.0)
			{
				return (int)d.TotalHours + " h " + d.Minutes + " min";
			}
			return (int)d.TotalMinutes + " min";
		}

		// ---------- Acciones de la cola ----------

		public void Retry(Target t)
		{
			lock (Store.Lock)
			{
				if (t.Status == St.Error || t.Status == St.Cancelled)
				{
					t.Status = (t.P == "ig" && t.Extra.Length > 0) ? St.Ready : St.Pending;
					t.Msg = "";
					t.Progress = -1;
				}
			}
			Store.Save();
			Changes++;
		}

		// Quita un destino que aún no se ha subido (o cancela la subida en curso).
		public void Drop(Target t)
		{
			lock (Store.Lock)
			{
				if (t.Status == St.Uploading)
				{
					cancelTarget = t;
					return;
				}
				if (t.Status == St.Pending || t.Status == St.Ready)
				{
					t.Status = St.Cancelled;
					t.Msg = "Cancelado";
					t.Progress = -1;
				}
			}
			Store.Save();
			Changes++;
		}

		public void Remove(Post p)
		{
			lock (Store.Lock)
			{
				foreach (Target t in p.Targets)
				{
					if (t.Status == St.Uploading)
					{
						return;
					}
				}
				Store.D.Posts.Remove(p);
			}
			Store.Save();
			Changes++;
		}

		public void Add(Post p)
		{
			lock (Store.Lock)
			{
				Store.D.Posts.Add(p);
			}
			Store.Save();
			Changes++;
		}
	}
}
