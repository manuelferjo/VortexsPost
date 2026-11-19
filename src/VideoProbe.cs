// Lee de un MP4/MOV el ancho, el alto (teniendo en cuenta si el vídeo va girado) y la duración, sin
// programas externos: recorre las cajas del archivo (moov → mvhd y trak → tkhd) saltando lo demás,
// así que aunque el archivo pese mucho solo se leen unos pocos KB.
using System;
using System.IO;

namespace VortexsPost
{
	internal static class VideoProbe
	{
		public class Info
		{
			public int Width;

			public int Height;

			public double Seconds;

			public bool Vertical
			{
				get
				{
					return Height > Width;
				}
			}

			public bool Known
			{
				get
				{
					return Width > 0 && Height > 0;
				}
			}
		}

		public static Info Read(string file)
		{
			Info info = new Info();
			try
			{
				using (FileStream f = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
				{
					Boxes(f, 0L, f.Length, info, 0);
				}
			}
			catch
			{
			}
			return info;
		}

		private static uint U32(byte[] b, int o)
		{
			return (uint)((b[o] << 24) | (b[o + 1] << 16) | (b[o + 2] << 8) | b[o + 3]);
		}

		private static ulong U64(byte[] b, int o)
		{
			return ((ulong)U32(b, o) << 32) | U32(b, o + 4);
		}

		private static byte[] ReadAt(FileStream f, long pos, int count)
		{
			f.Seek(pos, SeekOrigin.Begin);
			byte[] b = new byte[count];
			int got = 0;
			while (got < count)
			{
				int n = f.Read(b, got, count - got);
				if (n <= 0)
				{
					break;
				}
				got += n;
			}
			if (got < count)
			{
				throw new EndOfStreamException();
			}
			return b;
		}

		// Recorre las cajas entre start y end. Entra en moov, trak, mdia (contenedores).
		private static void Boxes(FileStream f, long start, long end, Info info, int depth)
		{
			long pos = start;
			int guard = 0;
			while (pos + 8 <= end && guard++ < 10000)
			{
				byte[] h = ReadAt(f, pos, 8);
				long size = U32(h, 0);
				string type = new string(new char[4] { (char)h[4], (char)h[5], (char)h[6], (char)h[7] });
				int header = 8;
				if (size == 1)
				{
					size = (long)U64(ReadAt(f, pos + 8, 8), 0);
					header = 16;
				}
				else if (size == 0)
				{
					size = end - pos;
				}
				if (size < header || pos + size > end + 8)
				{
					return;
				}
				long body = pos + header;
				long bodyLen = Math.Min(size - header, end - body);
				if ((type == "moov" || type == "trak") && depth < 4)
				{
					Boxes(f, body, body + bodyLen, info, depth + 1);
				}
				else if (type == "mvhd" && bodyLen >= 32)
				{
					byte[] b = ReadAt(f, body, (int)Math.Min(bodyLen, 40L));
					int version = b[0];
					uint scale;
					double duration;
					if (version == 1)
					{
						scale = U32(b, 20);
						duration = U64(b, 24);
					}
					else
					{
						scale = U32(b, 12);
						duration = U32(b, 16);
					}
					if (scale > 0)
					{
						info.Seconds = duration / scale;
					}
				}
				else if (type == "tkhd" && bodyLen >= 84 && info.Width == 0)
				{
					byte[] b = ReadAt(f, body, (int)Math.Min(bodyLen, 100L));
					int version = b[0];
					// Matriz y medidas después de los campos de tiempo (más largos en la versión 1).
					int m = (version == 1) ? 52 : 40;
					if (b.Length >= m + 44)
					{
						int a = (int)U32(b, m);
						int bb = (int)U32(b, m + 4);
						int w = (int)(U32(b, m + 36) >> 16);
						int hh = (int)(U32(b, m + 40) >> 16);
						if (w > 0 && hh > 0)
						{
							// Girado 90° o 270°: a = 0 y b = ±1.
							bool rotated = a == 0 && bb != 0;
							info.Width = rotated ? hh : w;
							info.Height = rotated ? w : hh;
						}
					}
				}
				pos += size;
			}
		}

		// "0:45", "12:03", "1:02:10"
		public static string Duration(double s)
		{
			if (s <= 0)
			{
				return "";
			}
			TimeSpan t = TimeSpan.FromSeconds(Math.Round(s));
			if (t.TotalHours >= 1.0)
			{
				return (int)t.TotalHours + ":" + t.Minutes.ToString("00") + ":" + t.Seconds.ToString("00");
			}
			return (int)t.TotalMinutes + ":" + t.Seconds.ToString("00");
		}
	}
}
