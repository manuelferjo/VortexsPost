// Recuadro que se dibuja con una función (Painter): textos, etiquetas, cifras y paneles de estado.
using System;
using System.Drawing;
using System.Windows.Forms;

namespace VortexsPost
{
	internal class PaintBox : VControl
	{
		public Action<Graphics, Rectangle> Painter;

		public PaintBox()
		{
			SetStyle(ControlStyles.Selectable, false);
			base.TabStop = false;
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			Theme.Quality(e.Graphics);
			if (Painter != null)
			{
				Painter(e.Graphics, base.ClientRectangle);
			}
		}
	}
}
