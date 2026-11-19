// Datos que se guardan en VortexsPost.json (junto al .exe): publicaciones (cola e historial),
// ajustes guardados, opciones generales y cuentas (tokens y secretos cifrados con Secret).
// Son clases sencillas con campos públicos para que JavaScriptSerializer las lea y escriba tal cual.
using System;
using System.Collections.Generic;

namespace VortexsPost
{
	// Estados de cada destino de una publicación.
	internal static class St
	{
		public const string Pending = "pendiente";   // esperando su hora (o en cola para subir ya)
		public const string Uploading = "subiendo";   // subiendo o procesándose ahora mismo
		public const string Ready = "listo";          // Instagram: subido y esperando a la hora de publicar
		public const string Scheduled = "programado"; // YouTube: subido; YouTube lo publica solo a su hora
		public const string Done = "publicado";
		public const string Error = "error";
		public const string Cancelled = "cancelado";
	}

	// Cuándo: 0 ahora, 1 hoy, 2 mañana, 3 otro día (Date). Time es "HH:mm".
	public class Sched
	{
		public int Mode;

		public string Date = "";

		public string Time = "15:00";

		public Sched Copy()
		{
			return new Sched { Mode = Mode, Date = Date, Time = Time };
		}
	}

	// Un destino de una publicación (YouTube, TikTok o Instagram) con su texto, su hora y su estado.
	public class Target
	{
		public string P = "";        // "yt", "tt", "ig"

		public string Status = St.Pending;

		public long When;            // hora de publicar (ticks UTC); 0 = en cuanto se pueda

		public string Title = "";    // YouTube: título

		public string Text = "";     // YouTube: descripción · TikTok e Instagram: texto de la publicación

		public string Tags = "";     // YouTube: etiquetas (sin #), separadas por comas

		// YouTube
		public string Privacy = "public";   // public, unlisted, private · TikTok: PUBLIC_TO_EVERYONE…

		public string Category = "20";

		public bool Kids;

		// TikTok
		public bool Comments = true;

		public bool Duet = true;

		public bool Stitch = true;

		public bool Brand;           // contenido comercial

		public bool BrandOther;      // de una marca de terceros (si no, de tu propia marca)

		public bool Draft;           // enviar a los borradores de TikTok (lo publicas tú desde la app)

		// Instagram
		public bool Feed = true;     // mostrar el reel también en el feed

		// Resultado
		public string Msg = "";

		public string Url = "";

		public string Extra = "";    // Instagram: id del contenedor ya subido

		public int Progress = -1;    // % de la subida (-1 = no se sabe)

		public long DoneAt;
	}

	public class Post
	{
		public string Id = "";

		public string File = "";

		public long Created;

		public List<Target> Targets = new List<Target>();
	}

	// Ajuste guardado ("Mañana a las 15:00 en YouTube y TikTok…"). Days: para "otro día", cuántos días después de hoy.
	public class Preset
	{
		public string Name = "";

		public bool Yt = true;

		public bool Tt = true;

		public bool Ig = true;

		public int Mode;

		public int Days = 1;

		public string Time = "15:00";

		// Hora propia de cada red (si OwnX: modo, días y hora de esa red).
		public bool OwnYt;

		public int ModeYt;

		public int DaysYt = 1;

		public string TimeYt = "15:00";

		public bool OwnTt;

		public int ModeTt;

		public int DaysTt = 1;

		public string TimeTt = "15:00";

		public bool OwnIg;

		public int ModeIg;

		public int DaysIg = 1;

		public string TimeIg = "15:00";

		public string Hashtags = "";

		public string YtPrivacy = "public";

		public string YtCategory = "20";

		public bool YtKids;

		public string TtPrivacy = "";

		public bool TtComments = true;

		public bool TtDuet = true;

		public bool TtStitch = true;

		public bool TtDraft;

		public bool IgFeed = true;
	}

	public class Accounts
	{
		public string YtClient = "";

		public string YtSecret = "";   // cifrado

		public string YtToken = "";    // cifrado: acceso \n renovación \n caducidad

		public string YtAccount = "";

		public string TtClient = "";

		public string TtSecret = "";   // cifrado

		public string TtToken = "";    // cifrado

		public string TtAccount = "";

		public string TtOpenId = "";

		public string IgToken = "";    // cifrado

		public string IgUser = "";

		public string IgAccount = "";

		public string IgHost = "";

		public long IgSince;

		public long IgExpires;
	}

	public class Data
	{
		public int Version = 1;

		public List<Post> Posts = new List<Post>();

		public List<Preset> Presets = new List<Preset>();

		public string DefaultPreset = "";

		public Accounts Acc = new Accounts();

		// Opciones generales (pestaña Ajustes).
		public bool StartWithWindows;

		public bool KeepInTray = true;

		public bool KeepAwake = true;

		public string Folder = "";

		public string UpdateSkip = "";   // versión que decidiste saltarte
	}
}
