// Actualización automática desde las Releases de GitHub (igual que en Vortexs Live).
// - Al abrirse (y con «Buscar actualizaciones» en Ajustes) mira si hay una versión más nueva.
// - Si aceptas: descarga el ZIP, comprueba tamaño y huella SHA-256, sustituye el .exe en la MISMA
//   carpeta (el viejo queda como .old por si algo falla) y se vuelve a abrir. VortexsPost.json no se
//   toca: cuentas, ajustes y cola se conservan.
// - Nunca se actualiza mientras se está subiendo un vídeo.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Threading;
using System.Windows.Forms;

namespace VortexsPost
{
	internal partial class MainForm
	{
		private const string UpdateRepo = "manuelferjo/VortexsPost";

		private volatile bool updateBusy;

		private class UpdateInfo
		{
			public string Tag = "";

			public string Notes = "";

			public string Url = "";

			public string Page = "";

			public string Sha256 = "";

			public Version Ver;

			public long Size;
		}

		private static Version ParseVersion(string s)
		{
			s = (s ?? "").Trim().TrimStart('v', 'V');
			if (s.IndexOf('.') < 0)
			{
				s += ".0";
			}
			Version v;
			return System.Version.TryParse(s, out v) ? v : null;
		}

		// Al abrir: borra el .exe viejo de una actualización anterior y mira si hay versión nueva.
		private void StartUpdateCheck()
		{
			if (Program.UpdatedTo.Length > 0)
			{
				Balloon("Vortexs Post actualizado", "Ya tienes la versión " + Program.UpdatedTo + ". Tus cuentas, ajustes y cola se han conservado.", false);
			}
			ThreadPool.QueueUserWorkItem(delegate
			{
				string old = Application.ExecutablePath + ".old";
				for (int i = 0; i < 10 && File.Exists(old); i++)
				{
					try
					{
						File.Delete(old);
					}
					catch
					{
						Thread.Sleep(1000);
					}
				}
				Thread.Sleep(5000);
				CheckForUpdate(false);
			});
		}

		// manual = true: pulsado «Buscar actualizaciones» (avisa también si no hay nada nuevo o si falla).
		private void CheckForUpdate(bool manual)
		{
			UpdateInfo info = null;
			string error = null;
			try
			{
				info = LatestRelease();
			}
			catch (Exception ex)
			{
				error = ex.Message;
			}
			try
			{
				BeginInvoke((Action)delegate
				{
					Version mine = ParseVersion(Version);
					if (info == null)
					{
						if (manual)
						{
							Info("Actualizaciones", "No se ha podido comprobar si hay versiones nuevas" + ((error != null) ? (": " + error) : "."));
						}
						return;
					}
					if (mine != null && info.Ver <= mine)
					{
						if (manual)
						{
							Info("Actualizaciones", "Tienes la última versión (" + Version + ").");
						}
						return;
					}
					string skip;
					lock (Store.Lock)
					{
						skip = Store.D.UpdateSkip ?? "";
					}
					if (!manual && info.Tag == skip)
					{
						return;
					}
					if (!Visible)
					{
						// Escondido en la bandeja: solo se avisa; al pulsar el aviso se abre.
						Balloon("Hay una versión nueva de Vortexs Post", info.Tag + ": abre el programa para actualizar.", false);
						pendingUpdate = info;
						return;
					}
					UpdateDialog(info);
				});
			}
			catch
			{
			}
		}

		// Versión encontrada mientras la ventana estaba escondida (se ofrece al abrirla).
		private UpdateInfo pendingUpdate;

		private void OfferPendingUpdate()
		{
			if (pendingUpdate != null && Visible)
			{
				UpdateInfo u = pendingUpdate;
				pendingUpdate = null;
				UpdateDialog(u);
			}
		}

		private UpdateInfo LatestRelease()
		{
			Resp r = Net.Send("GET", "https://api.github.com/repos/" + UpdateRepo + "/releases/latest", null, null, null);
			if (r.Status == 404)
			{
				throw new Exception("aún no hay ninguna versión publicada");
			}
			if (!r.Ok || r.Json == null)
			{
				throw new Exception(r.Error());
			}
			UpdateInfo info = new UpdateInfo
			{
				Tag = Http.Str(r.Json, "tag_name"),
				Notes = Http.Str(r.Json, "body").Trim(),
				Page = Http.Str(r.Json, "html_url")
			};
			info.Ver = ParseVersion(info.Tag);
			if (info.Ver == null)
			{
				throw new Exception("la última versión publicada no tiene un número válido (" + info.Tag + ")");
			}
			foreach (Dictionary<string, object> a in Http.Arr(r.Json, "assets"))
			{
				string name = Http.Str(a, "name");
				if (!name.StartsWith("VortexsPost", StringComparison.OrdinalIgnoreCase) || !name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
				{
					continue;
				}
				info.Url = Http.Str(a, "browser_download_url");
				long size;
				info.Size = long.TryParse(Http.Str(a, "size"), out size) ? size : 0L;
				string digest = Http.Str(a, "digest");
				if (digest.StartsWith("sha256:"))
				{
					info.Sha256 = digest.Substring(7).ToLowerInvariant();
				}
				break;
			}
			if (info.Url.Length == 0)
			{
				throw new Exception("la versión " + info.Tag + " aún no tiene el ZIP de descarga (GitHub lo adjunta en un par de minutos)");
			}
			return info;
		}

		private void UpdateDialog(UpdateInfo info)
		{
			if (updateBusy)
			{
				return;
			}
			using (Form f = DarkForm("Actualización de Vortexs Post", 460, 400))
			{
				Card card = new Card { Title = "Hay una versión nueva: " + info.Tag, Subtitle = "Tienes la " + Version + ". Tus cuentas, ajustes y cola se conservan." };
				Place(f, card, 20f, 18f, 420f, 290f);
				string notes = (info.Notes.Length > 0) ? info.Notes.Replace("\r", "") : "Sin notas de la versión.";
				if (notes.Length > 900)
				{
					notes = notes.Substring(0, 900) + "…";
				}
				TextBox tb = new TextBox
				{
					Multiline = true,
					ReadOnly = true,
					TabStop = false,
					BorderStyle = BorderStyle.None,
					ScrollBars = (notes.Split('\n').Length > 11 || notes.Length > 600) ? ScrollBars.Vertical : ScrollBars.None,
					BackColor = Theme.Input,
					ForeColor = Theme.Text,
					Font = Theme.Body(8.5f),
					Text = notes.Replace("\n", "\r\n")
				};
				Place(card, tb, 20f, 62f, 380f, 180f);
				VButton page = new VButton { Kind = ButtonKind.Link, Text = "Ver en GitHub →", FontSize = 8.5f };
				Place(card, page, 20f, 250f, 380f, 26f);
				page.Click += delegate
				{
					Http.OpenBrowser(info.Page);
				};
				VButton skip = new VButton { Kind = ButtonKind.Ghost, Text = "Saltar esta", FontSize = 9.5f };
				VButton later = new VButton { Kind = ButtonKind.Ghost, Text = "Más tarde", FontSize = 9.5f };
				VButton ok = new VButton { Kind = ButtonKind.Primary, Text = "Actualizar ahora", FontSize = 10f };
				Place(f, skip, 20f, 326f, 120f, 44f);
				Place(f, later, 148f, 326f, 120f, 44f);
				Place(f, ok, 276f, 326f, 164f, 44f);
				skip.Click += delegate
				{
					lock (Store.Lock)
					{
						Store.D.UpdateSkip = info.Tag;
					}
					Store.Save();
					f.Close();
				};
				later.Click += delegate
				{
					f.Close();
				};
				bool go = false;
				ok.Click += delegate
				{
					if (pub.Busy)
					{
						Info("Actualizar", "Se está subiendo un vídeo. Espera a que termine (mira la pestaña Cola) y vuelve a intentarlo.");
						return;
					}
					go = true;
					f.Close();
				};
				f.Shown += delegate
				{
					f.ActiveControl = ok;
					tb.SelectionStart = 0;
					tb.SelectionLength = 0;
				};
				f.ShowDialog(this);
				if (go)
				{
					RunUpdate(info);
				}
			}
		}

		private void RunUpdate(UpdateInfo info)
		{
			if (updateBusy)
			{
				return;
			}
			updateBusy = true;
			Balloon("Actualizando Vortexs Post", "Descargando la versión " + info.Tag + "…", false);
			ThreadPool.QueueUserWorkItem(delegate
			{
				string exe = Application.ExecutablePath;
				string err = null;
				try
				{
					InstallUpdate(info, exe);
				}
				catch (Exception ex)
				{
					err = ex.Message;
				}
				try
				{
					BeginInvoke((Action)delegate
					{
						updateBusy = false;
						if (err != null)
						{
							if (Ask("Actualizar", "No se ha podido actualizar: " + err + "\n\nTu versión actual sigue funcionando. ¿Abrir la página de descarga para hacerlo a mano?", "Abrir"))
							{
								Http.OpenBrowser(info.Page);
							}
							return;
						}
						// La versión nueva espera a que esta se cierre del todo antes de arrancar.
						SaveForm();
						pub.SaveAccounts();
						Process.Start(new ProcessStartInfo(exe, "--esperar=" + Process.GetCurrentProcess().Id + " --actualizado=" + info.Tag.TrimStart('v', 'V'))
						{
							UseShellExecute = false,
							WorkingDirectory = Path.GetDirectoryName(exe)
						});
						exiting = true;
						Close();
					});
				}
				catch
				{
				}
			});
		}

		private static void InstallUpdate(UpdateInfo info, string exe)
		{
			string dir = Path.GetDirectoryName(exe);
			// ¿Se puede escribir en la carpeta del programa? (En Archivos de programa, no.)
			string probe = Path.Combine(dir, ".actualizar-prueba");
			try
			{
				File.WriteAllText(probe, "");
				File.Delete(probe);
			}
			catch
			{
				throw new Exception("no hay permiso para escribir en la carpeta de Vortexs Post (" + dir + "). Muévela a Documentos o al Escritorio.");
			}
			string tmp = Path.Combine(Path.GetTempPath(), "VortexsPost-actualizacion-" + Guid.NewGuid().ToString("N").Substring(0, 8));
			Directory.CreateDirectory(tmp);
			try
			{
				string zip = Path.Combine(tmp, "VortexsPost.zip");
				Download(info.Url, zip);
				long len = new FileInfo(zip).Length;
				if (info.Size > 0 && len != info.Size)
				{
					throw new Exception("la descarga está incompleta (" + len + " de " + info.Size + " bytes)");
				}
				if (info.Sha256.Length > 0)
				{
					string h;
					using (SHA256 sha = SHA256.Create())
					{
						using (FileStream s = File.OpenRead(zip))
						{
							h = BitConverter.ToString(sha.ComputeHash(s)).Replace("-", "").ToLowerInvariant();
						}
					}
					if (h != info.Sha256)
					{
						throw new Exception("la huella SHA-256 del ZIP no coincide con la de GitHub");
					}
				}
				string ext = Path.Combine(tmp, "x");
				ZipFile.ExtractToDirectory(zip, ext);
				string newExe = Path.Combine(ext, "VortexsPost.exe");
				if (!File.Exists(newExe))
				{
					throw new Exception("el ZIP no contiene VortexsPost.exe");
				}
				using (FileStream s2 = File.OpenRead(newExe))
				{
					if (s2.ReadByte() != 'M' || s2.ReadByte() != 'Z')
					{
						throw new Exception("el VortexsPost.exe descargado no es válido");
					}
				}
				// Windows deja renombrar un programa abierto: el viejo pasa a .old y el nuevo ocupa su sitio.
				string old = exe + ".old";
				if (File.Exists(old))
				{
					File.Delete(old);
				}
				File.Move(exe, old);
				try
				{
					File.Copy(newExe, exe);
				}
				catch
				{
					File.Move(old, exe);
					throw;
				}
				// Guía, LEEME, logo… (nunca VortexsPost.json).
				foreach (string file in Directory.GetFiles(ext, "*", SearchOption.AllDirectories))
				{
					string rel = file.Substring(ext.Length + 1);
					if (rel.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) || rel.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
					{
						continue;
					}
					string dest = Path.Combine(dir, rel);
					try
					{
						Directory.CreateDirectory(Path.GetDirectoryName(dest));
						File.Copy(file, dest, true);
					}
					catch
					{
					}
				}
			}
			finally
			{
				try
				{
					Directory.Delete(tmp, true);
				}
				catch
				{
				}
			}
		}

		private static void Download(string url, string path)
		{
			HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
			req.UserAgent = "VortexsPost";
			req.Timeout = 30000;
			req.ReadWriteTimeout = 60000;
			using (WebResponse resp = req.GetResponse())
			{
				using (Stream src = resp.GetResponseStream())
				{
					using (FileStream dst = File.Create(path))
					{
						src.CopyTo(dst);
					}
				}
			}
		}
	}
}
