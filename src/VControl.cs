// Base de los controles dibujados a mano (botones, interruptores, campos…): fondo transparente,
// doble búfer y estado del ratón (hover = encima, pressed = pulsado) para dibujarse distinto.
using System;
using System.Drawing;
using System.Windows.Forms;

namespace VortexsPost
{
	internal class VControl : Control
	{
		protected bool hover;

		protected bool pressed;

		public VControl()
		{
			SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor | ControlStyles.UserPaint, true);
			BackColor = Color.Transparent;
			ForeColor = Theme.Text;
		}

		protected override void OnMouseEnter(EventArgs e)
		{
			hover = true;
			Invalidate();
			base.OnMouseEnter(e);
		}

		protected override void OnMouseLeave(EventArgs e)
		{
			hover = false;
			pressed = false;
			Invalidate();
			base.OnMouseLeave(e);
		}

		protected override void OnMouseDown(MouseEventArgs e)
		{
			if (e.Button == MouseButtons.Left)
			{
				pressed = true;
				Invalidate();
			}
			base.OnMouseDown(e);
		}

		protected override void OnMouseUp(MouseEventArgs e)
		{
			pressed = false;
			Invalidate();
			base.OnMouseUp(e);
		}

		protected override void OnEnabledChanged(EventArgs e)
		{
			Invalidate();
			base.OnEnabledChanged(e);
		}

		protected override void OnGotFocus(EventArgs e)
		{
			Invalidate();
			base.OnGotFocus(e);
		}

		protected override void OnLostFocus(EventArgs e)
		{
			Invalidate();
			base.OnLostFocus(e);
		}
	}
}
