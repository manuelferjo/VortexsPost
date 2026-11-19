// Arranque de Vortexs Post. Solo puede haber uno abierto: si ya lo está, se le pide que enseñe su
// ventana y este se cierra. Con "--bandeja" (al abrirse con Windows) empieza escondido en la bandeja.
using System;
using System.Threading;
using System.Windows.Forms;

namespace VortexsPost
{
	internal static class Program
	{
		[STAThread]
		private static void Main(string[] args)
		{
			bool hidden = false;
			foreach (string a in args)
			{
				if (a == "--bandeja" || a == "--tray")
				{
					hidden = true;
				}
			}
			bool created;
			using (Mutex mutex = new Mutex(true, "VortexsPost_unico", out created))
			{
				EventWaitHandle showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, "VortexsPost_mostrar");
				if (!created)
				{
					// Ya está abierto: que se enseñe.
					showEvent.Set();
					return;
				}
				Application.EnableVisualStyles();
				Application.SetCompatibleTextRenderingDefault(false);
				Theme.Init();
				MainForm form = new MainForm(hidden);
				Thread watcher = new Thread((ThreadStart)delegate
				{
					while (true)
					{
						showEvent.WaitOne();
						try
						{
							form.BeginInvoke((Action)delegate
							{
								form.ShowFromTray();
							});
						}
						catch
						{
						}
					}
				});
				watcher.IsBackground = true;
				watcher.Start();
				Application.Run(form);
				GC.KeepAlive(mutex);
			}
		}
	}
}
