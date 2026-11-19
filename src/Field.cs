// Campo de texto con el tema oscuro: un TextBox normal (Box) dentro de un recuadro redondeado
// que se ilumina en violeta al escribir.
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace VortexsPost
{
	internal class Field : VControl
	{
		public readonly TextBox Box;

		private bool focused;

		// ReadOnly: siempre de solo lectura (en violeta, como el servidor de OBS).
		private bool fixedRo;

		// Locked: bloqueado por ahora (en gris, como los canales con el chat encendido).
		private bool locked;

		public bool ReadOnly
		{
			get
			{
				return fixedRo;
			}
			set
			{
				fixedRo = value;
				Sync();
			}
		}

		public bool Locked
		{
			get
			{
				return locked;
			}
			set
			{
				if (locked != value)
				{
					locked = value;
					Sync();
					Invalidate();
				}
			}
		}

		public Field()
		{
			SetStyle(ControlStyles.Selectable, false);
			base.TabStop = false;
			Box = new TextBox
			{
				BorderStyle = BorderStyle.None,
				BackColor = Theme.Input,
				ForeColor = Theme.Text,
				Font = Theme.Native(10f)
			};
			base.Controls.Add(Box);
			TextBox box = Box;
			EventHandler onFocus = delegate
			{
				focused = true;
				Invalidate();
			};
			box.GotFocus += onFocus;
			Box.LostFocus += delegate
			{
				focused = false;
				Invalidate();
			};
			Cursor = Cursors.IBeam;
		}

		private void Sync()
		{
			Box.ReadOnly = fixedRo || locked;
			Box.BackColor = Theme.Input;
			Box.ForeColor = (locked ? Theme.Dim : (fixedRo ? Theme.VioletLight : Theme.Text));
		}

		protected override void OnClick(EventArgs e)
		{
			Box.Focus();
			base.OnClick(e);
		}

		protected override void OnLayout(LayoutEventArgs e)
		{
			base.OnLayout(e);
			// El TextBox va dentro con 12 de margen (centrado en vertical si es de una línea).
			int pad = Theme.S(12f);
			if (Box.Multiline)
			{
				Box.SetBounds(pad, Theme.S(8f), base.Width - 2 * pad, base.Height - Theme.S(16f));
				return;
			}
			Box.SetBounds(pad, (base.Height - Box.Height) / 2, base.Width - 2 * pad, Box.Height);
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			Graphics graphics = e.Graphics;
			Theme.Quality(graphics);
			RectangleF r = new RectangleF(0.5f, 0.5f, (float)base.Width - 1.5f, (float)base.Height - 1.5f);
			using (GraphicsPath path = Theme.Round(r, Theme.S(10f)))
			{
				using (SolidBrush brush = new SolidBrush(Theme.Input))
				{
					graphics.FillPath(brush, path);
				}
				using (Pen pen = new Pen(focused ? Theme.Violet : (hover ? Theme.BorderHi : Theme.Border), focused ? 1.5f : 1f))
				{
					graphics.DrawPath(pen, path);
				}
			}
		}
	}
}
