// Recuadro redondeado donde van los controles de cada sección, con título y subtítulo opcionales.
// Accent: con brillos violeta y amarillo y borde degradado (el recuadro del delay).
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace VortexsPost
{
	internal class Card : VControl
	{
		public string Title = "";

		public string Subtitle = "";

		public bool Accent;

		public Card()
		{
			SetStyle(ControlStyles.Selectable, false);
			base.TabStop = false;
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			Graphics graphics = e.Graphics;
			Theme.Quality(graphics);
			RectangleF frame = new RectangleF(0.5f, 0.5f, (float)base.Width - 1.5f, (float)base.Height - 1.5f);
			float radius = Theme.S(18f);
			using (GraphicsPath path = Theme.Round(frame, radius))
			{
				using (SolidBrush brush = new SolidBrush(Color.FromArgb(225, Theme.Surface)))
				{
					graphics.FillPath(brush, path);
				}
				if (Accent)
				{
					graphics.SetClip(path);
					Glow(graphics, new RectangleF((float)(-base.Width) * 0.3f, (float)(-base.Height) * 0.6f, (float)base.Width * 0.9f, (float)base.Height * 1.1f), Color.FromArgb(40, Theme.Violet));
					Glow(graphics, new RectangleF((float)base.Width * 0.55f, (float)base.Height * 0.35f, (float)base.Width * 0.7f, (float)base.Height * 1f), Color.FromArgb(26, Theme.Yellow));
					graphics.ResetClip();
					using (LinearGradientBrush brush2 = new LinearGradientBrush(frame, Color.FromArgb(120, Theme.Violet), Color.FromArgb(80, Theme.Yellow), 20f))
					{
						using (Pen pen = new Pen(brush2, 1.2f))
						{
							graphics.DrawPath(pen, path);
						}
					}
				}
				else
				{
					using (Pen pen2 = new Pen(Theme.Border, 1f))
					{
						graphics.DrawPath(pen2, path);
					}
				}
			}
			if (Title.Length > 0)
			{
				Theme.DrawText(graphics, Title, Theme.Display(11.5f), Theme.Text, new RectangleF(Theme.S(20f), Theme.S(14f), base.Width - Theme.S(40f), Theme.S(24f)), StringAlignment.Near, StringAlignment.Center);
				if (Subtitle.Length > 0)
				{
					Theme.DrawText(graphics, Subtitle, Theme.Body(8.25f), Theme.Muted, new RectangleF(Theme.S(20f), Theme.S(36f), base.Width - Theme.S(40f), Theme.S(18f)), StringAlignment.Near, StringAlignment.Center);
				}
			}
			base.OnPaint(e);
		}

		// Brillo difuminado: elipse que se desvanece del color c en el centro a transparente en el borde.
		public static void Glow(Graphics g, RectangleF r, Color c)
		{
			using (GraphicsPath path = new GraphicsPath())
			{
				path.AddEllipse(r);
				using (PathGradientBrush brush = new PathGradientBrush(path))
				{
					brush.CenterColor = c;
					brush.SurroundColors = new Color[1] { Color.FromArgb(0, c) };
					g.FillPath(brush, path);
				}
			}
		}
	}
}
