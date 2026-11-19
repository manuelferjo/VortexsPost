// Aspecto de la ventana: colores, fuentes (incluidas dentro del .exe), escala según los ppp de la
// pantalla y ayudas de dibujo (rectángulos redondeados, degradado de la marca, textos).
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace VortexsPost
{
	internal static class Theme
	{
		// ---------- Colores (los mismos que Vortexs Live y la web) ----------

		public static readonly Color Bg = Color.FromArgb(9, 9, 12);

		public static readonly Color Surface = Color.FromArgb(17, 17, 22);

		public static readonly Color Elevated = Color.FromArgb(23, 23, 32);

		public static readonly Color Input = Color.FromArgb(14, 14, 19);

		public static readonly Color Border = Color.FromArgb(38, 38, 46);

		public static readonly Color BorderHi = Color.FromArgb(56, 54, 70);

		public static readonly Color Text = Color.FromArgb(244, 244, 245);

		public static readonly Color Muted = Color.FromArgb(161, 161, 170);

		public static readonly Color Dim = Color.FromArgb(113, 113, 122);

		public static readonly Color Violet = Color.FromArgb(192, 154, 253);

		public static readonly Color VioletLight = Color.FromArgb(214, 191, 255);

		public static readonly Color VioletPale = Color.FromArgb(227, 213, 255);

		public static readonly Color Yellow = Color.FromArgb(237, 225, 3);

		public static readonly Color Red = Color.FromArgb(255, 70, 85);

		public static readonly Color Green = Color.FromArgb(52, 211, 153);

		// 1 a 96 ppp (100 %), 1,5 a 144 ppp (150 %)…
		public static float Scale = 1f;

		private static readonly PrivateFontCollection pfc = new PrivateFontCollection();

		private static readonly Dictionary<string, Font> cache = new Dictionary<string, Font>();

		// Medida de diseño → píxeles de esta pantalla.
		public static int S(float v)
		{
			return (int)Math.Round(v * Scale);
		}

		// Al arrancar: lee la escala de la pantalla y carga las fuentes .ttf que van dentro del .exe.
		public static void Init()
		{
			try
			{
				using (Graphics graphics = Graphics.FromHwnd(IntPtr.Zero))
				{
					Scale = graphics.DpiX / 96f;
				}
			}
			catch
			{
				Scale = 1f;
			}
			if (Scale < 1f)
			{
				Scale = 1f;
			}
			Assembly assembly = typeof(Theme).Assembly;
			foreach (string resource in assembly.GetManifestResourceNames())
			{
				if (!resource.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase))
				{
					continue;
				}
				try
				{
					byte[] font;
					using (Stream stream = assembly.GetManifestResourceStream(resource))
					{
						font = new byte[stream.Length];
						int got;
						for (int j = 0; j < font.Length; j += got)
						{
							got = stream.Read(font, j, font.Length - j);
							if (got <= 0)
							{
								break;
							}
						}
					}
					// AddMemoryFont necesita que la memoria siga ahí mientras se use la fuente: no se libera.
					IntPtr memory = Marshal.AllocCoTaskMem(font.Length);
					Marshal.Copy(font, 0, memory, font.Length);
					pfc.AddMemoryFont(memory, font.Length);
				}
				catch
				{
				}
			}
		}

		private static FontFamily Family(string name)
		{
			foreach (FontFamily family in pfc.Families)
			{
				if (string.Equals(family.Name, name, StringComparison.OrdinalIgnoreCase))
				{
					return family;
				}
			}
			return null;
		}

		// Fuente incluida (o la de Windows indicada si no está), a tamaño en puntos y ya escalada.
		// Se guardan para no crear la misma fuente cada vez que se dibuja.
		private static Font Make(string family, float size, FontStyle style, string fallback)
		{
			string key = family + "|" + size + "|" + (int)style;
			Font value;
			if (cache.TryGetValue(key, out value))
			{
				return value;
			}
			float emSize = size * 96f / 72f * Scale;
			try
			{
				FontFamily found = Family(family);
				if (found != null)
				{
					// Si la familia no tiene ese estilo, se usa el que tenga.
					FontStyle available = style;
					if (!found.IsStyleAvailable(available))
					{
						available = ((!found.IsStyleAvailable(FontStyle.Regular)) ? FontStyle.Bold : FontStyle.Regular);
					}
					value = new Font(found, emSize, available, GraphicsUnit.Pixel);
				}
			}
			catch
			{
				value = null;
			}
			if (value == null)
			{
				value = new Font(fallback, emSize, (style != FontStyle.Regular) ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel);
			}
			cache[key] = value;
			return value;
		}

		// Títulos y cifras grandes.
		public static Font Display(float size)
		{
			return Make("Outfit", size, FontStyle.Bold, "Segoe UI Semibold");
		}

		public static Font Body(float size)
		{
			return Make("Plus Jakarta Sans", size, FontStyle.Regular, "Segoe UI");
		}

		public static Font BodySemi(float size)
		{
			return Make("Plus Jakarta Sans SemiBold", size, FontStyle.Regular, "Segoe UI Semibold");
		}

		public static Font BodyBold(float size)
		{
			return Make("Plus Jakarta Sans", size, FontStyle.Bold, "Segoe UI Semibold");
		}

		// Etiquetas en mayúsculas ("DELAY EN DIRECTO").
		public static Font Mono(float size)
		{
			return Make("JetBrains Mono Medium", size, FontStyle.Regular, "Consolas");
		}

		public static Font Native(float size)
		{
			return Make("", size, FontStyle.Regular, "Segoe UI");
		}

		// ---------- Dibujo ----------

		// Rectángulo con las esquinas redondeadas.
		public static GraphicsPath Round(RectangleF r, float radius)
		{
			GraphicsPath path = new GraphicsPath();
			float d = Math.Min(radius * 2f, Math.Min(r.Width, r.Height));
			if (d <= 1f)
			{
				path.AddRectangle(r);
				return path;
			}
			path.AddArc(r.X, r.Y, d, d, 180f, 90f);
			path.AddArc(r.Right - d, r.Y, d, d, 270f, 90f);
			path.AddArc(r.Right - d, r.Bottom - d, d, d, 0f, 90f);
			path.AddArc(r.X, r.Bottom - d, d, d, 90f, 90f);
			path.CloseFigure();
			return path;
		}

		// Degradado de la marca: violeta → amarillo (con withPale, pasando por violeta claro).
		public static LinearGradientBrush Brand(RectangleF r, bool withPale)
		{
			if (r.Width < 1f)
			{
				r.Width = 1f;
			}
			if (r.Height < 1f)
			{
				r.Height = 1f;
			}
			LinearGradientBrush brush = new LinearGradientBrush(new RectangleF(r.X - 1f, r.Y - 1f, r.Width + 2f, r.Height + 2f), Violet, Yellow, 20f);
			if (withPale)
			{
				ColorBlend blend = new ColorBlend(3);
				blend.Colors = new Color[3] { Violet, VioletPale, Yellow };
				blend.Positions = new float[3] { 0f, 0.45f, 1f };
				brush.InterpolationColors = blend;
			}
			return brush;
		}

		public static void Quality(Graphics g)
		{
			g.SmoothingMode = SmoothingMode.AntiAlias;
			g.PixelOffsetMode = PixelOffsetMode.HighQuality;
			g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
			g.InterpolationMode = InterpolationMode.HighQualityBicubic;
		}

		// Texto en una línea (con "…" si no cabe).
		public static void DrawText(Graphics g, string s, Font f, Color c, RectangleF r, StringAlignment h, StringAlignment v)
		{
			using (StringFormat format = new StringFormat())
			{
				using (SolidBrush brush = new SolidBrush(c))
				{
					format.Alignment = h;
					format.LineAlignment = v;
					format.Trimming = StringTrimming.EllipsisCharacter;
					format.FormatFlags = StringFormatFlags.NoWrap;
					g.DrawString(s, f, brush, r, format);
				}
			}
		}

		// Texto en varias líneas.
		public static void DrawWrapped(Graphics g, string s, Font f, Color c, RectangleF r)
		{
			using (StringFormat format = new StringFormat())
			{
				using (SolidBrush brush = new SolidBrush(c))
				{
					format.Trimming = StringTrimming.EllipsisWord;
					g.DrawString(s, f, brush, r, format);
				}
			}
		}

		public static SizeF Measure(Graphics g, string s, Font f)
		{
			return g.MeasureString(s, f, new PointF(0f, 0f), StringFormat.GenericTypographic);
		}

		// Altura de la fuente sobre la línea base, en píxeles (para alinear textos de distinto tamaño).
		public static float Ascent(Graphics g, Font f)
		{
			FontFamily family = f.FontFamily;
			float pixels = ((f.Unit == GraphicsUnit.Pixel) ? f.Size : (f.SizeInPoints * g.DpiY / 72f));
			return pixels * (float)family.GetCellAscent(f.Style) / (float)family.GetEmHeight(f.Style);
		}

		// Mezcla de dos colores (t = 0 → a, t = 1 → b).
		public static Color Mix(Color a, Color b, float t)
		{
			return Color.FromArgb((int)((float)(int)a.A + (float)(b.A - a.A) * t), (int)((float)(int)a.R + (float)(b.R - a.R) * t), (int)((float)(int)a.G + (float)(b.G - a.G) * t), (int)((float)(int)a.B + (float)(b.B - a.B) * t));
		}

		[DllImport("dwmapi.dll")]
		private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

		// Barra de título oscura (Windows 10/11): modo oscuro (atributo 20, o 19 en Windows 10 antiguos),
		// color de la barra (35) y del borde (34). En versiones que no lo admiten no pasa nada.
		public static void DarkTitleBar(IntPtr hwnd)
		{
			try
			{
				int dark = 1;
				if (DwmSetWindowAttribute(hwnd, 20, ref dark, 4) != 0)
				{
					DwmSetWindowAttribute(hwnd, 19, ref dark, 4);
				}
				// Los colores van como 0x00BBGGRR.
				int caption = Bg.R | (Bg.G << 8) | (Bg.B << 16);
				DwmSetWindowAttribute(hwnd, 35, ref caption, 4);
				int border = 3811891;
				DwmSetWindowAttribute(hwnd, 34, ref border, 4);
			}
			catch
			{
			}
		}
	}
}
