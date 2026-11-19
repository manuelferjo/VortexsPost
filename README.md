# Vortexs Post

**Sube y programa tus vídeos en YouTube, TikTok e Instagram a la vez**, con el mismo estilo que [Vortexs Live](https://github.com/manuelferjo/VortexsLive).

- Un vídeo → YouTube (Shorts o vídeo), TikTok e Instagram (Reels) de una vez.
- **Mismo título en todas o uno distinto en cada red** («Texto propio»).
- **Programar**: ahora, hoy, mañana u otro día, a la hora que quieras; y si quieres, **una hora distinta por red**.
- **Ajustes guardados** («Mañana a las 15:00», «Solo TikTok + Reels ahora»…), con uno que se aplica solo al abrir.
- **Cola** con el progreso de cada subida, cancelar, reintentar y abrir lo publicado.
- Se queda en la bandeja para publicar lo programado, puede abrirse con Windows y evita que el PC se suspenda si hay algo pendiente.
- Usa solo las **APIs oficiales**. Tus tokens se guardan cifrados (DPAPI) en `VortexsPost.json`.

La guía completa (cómo conectar cada red y las auditorías de YouTube y TikTok) está en [`GUIA.html`](GUIA.html).

## Compilar

No hace falta instalar nada: `build.bat` usa el compilador de C# (.NET Framework 4) que ya trae Windows y crea `VortexsPost.exe`. GitHub también lo compila con cada cambio (pestaña **Actions**) y deja el ZIP para descargar.

El código está en `src/`:

| Archivo | Qué hace |
|---|---|
| `MainForm.cs` | Ventana, cabecera, bandeja y utilidades (el resto de pestañas en `PublishTab.cs`, `QueueTab.cs`, `SettingsTab.cs`, `AccountsTab.cs`) |
| `Publisher.cs` | El programador: decide qué toca subir o publicar y lo hace en otro hilo |
| `YouTube.cs`, `TikTok.cs`, `Instagram.cs` | Conexión y subida a cada red |
| `Model.cs`, `Store.cs` | Datos y `VortexsPost.json` |
| `Theme.cs`, `VButton.cs`, `Toggle.cs`, `Field.cs`, `Tabs.cs`, `Card.cs`… | Los mismos colores, fuentes y controles que Vortexs Live |

## Publicar una versión (actualización automática)

1. Cambia `Version` en `src/MainForm.cs` (por ejemplo a `1.1`) y añade las novedades al `LEEME.txt`.
2. Sube los cambios a GitHub.
3. En **Releases → Draft a new release**, crea la etiqueta `v1.1` (la misma versión con una `v` delante), escribe las novedades y pulsa **Publish release**.

GitHub compila, comprueba que la versión coincide con la etiqueta y adjunta `VortexsPost.zip` a la Release. Al abrirse, Vortexs Post ve la versión nueva, enseña las novedades y se actualiza solo (conserva `VortexsPost.json`).
