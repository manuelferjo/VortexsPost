// Interruptor sí/no con su texto (y, si hay Hint, una segunda línea pequeña debajo).
// Se cambia con un clic o con la barra espaciadora.
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace VortexsPost
{
	internal class Toggle : VControl
	{
		private bool on;

		public string Hint = "";

		public bool Checked
		{
			get
			{
				return on;
			}
			set
			{
				if (on != value)
				{
					on = value;
					Invalidate();
					if (this.CheckedChanged != null)
					{
						this.CheckedChanged(this, EventArgs.Empty);
					}
				}
			}
		}

		public event EventHandler CheckedChanged;

		public Toggle()
		{
			Cursor = Cursors.Hand;
			base.TabStop = true;
		}

		protected override void OnClick(EventArgs e)
		{
			Focus();
			Checked = !Checked;
			base.OnClick(e);
		}

		protected override void OnKeyDown(KeyEventArgs e)
		{
			if (e.KeyCode == Keys.Space)
			{
				Checked = !Checked;
				e.Handled = true;
			}
			base.OnKeyDown(e);
		}

		protected override void OnTextChanged(EventArgs e)
		{
			Invalidate();
			base.OnTextChanged(e);
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			Graphics graphics = e.Graphics;
			Theme.Quality(graphics);
			float width = Theme.S(38f);
			float height = Theme.S(22f);
			// Con Hint, el interruptor va arriba; sin él, centrado.
			float top = ((Hint.Length > 0) ? ((float)Theme.S(2f)) : (((float)base.Height - height) / 2f));
			RectangleF track = new RectangleF(1f, top, width, height);
			using (GraphicsPath path = Theme.Round(track, height / 2f))
			{
				if (on)
				{
					using (LinearGradientBrush brush = Theme.Brand(track, false))
					{
						graphics.FillPath(brush, path);
					}
				}
				else
				{
					using (SolidBrush brush2 = new SolidBrush(hover ? Color.FromArgb(44, 44, 56) : Color.FromArgb(36, 36, 46)))
					{
						graphics.FillPath(brush2, path);
					}
					using (Pen pen = new Pen(Theme.BorderHi))
					{
						graphics.DrawPath(pen, path);
					}
				}
			}
			// Bolita: a la derecha si está activado.
			float knob = height - (float)Theme.S(6f);
			RectangleF knobRect = new RectangleF(on ? (track.Right - knob - (float)Theme.S(3f)) : (track.X + (float)Theme.S(3f)), top + (float)Theme.S(3f), knob, knob);
			using (SolidBrush brush3 = new SolidBrush(on ? Color.FromArgb(14, 14, 18) : Theme.Muted))
			{
				graphics.FillEllipse(brush3, knobRect);
			}
			// Borde de foco (al llegar con el tabulador).
			if (Focused && ShowFocusCues)
			{
				using (Pen pen2 = new Pen(Color.FromArgb(160, Theme.Violet), 1.5f))
				{
					RectangleF r = RectangleF.Inflate(track, 2f, 2f);
					using (GraphicsPath path2 = Theme.Round(r, r.Height / 2f))
					{
						graphics.DrawPath(pen2, path2);
					}
				}
			}
			float textX = width + (float)Theme.S(12f);
			if (Hint.Length == 0)
			{
				Theme.DrawText(graphics, Text, Theme.BodySemi(9f), base.Enabled ? Theme.Text : Theme.Dim, new RectangleF(textX, 0f, (float)base.Width - textX, base.Height), StringAlignment.Near, StringAlignment.Center);
				return;
			}
			Theme.DrawText(graphics, Text, Theme.BodySemi(9f), Theme.Text, new RectangleF(textX, 0f, (float)base.Width - textX, Theme.S(24f)), StringAlignment.Near, StringAlignment.Center);
			Theme.DrawText(graphics, Hint, Theme.Body(8f), Theme.Muted, new RectangleF(textX, Theme.S(22f), (float)base.Width - textX, base.Height - Theme.S(22f)), StringAlignment.Near, StringAlignment.Near);
		}
	}
}
