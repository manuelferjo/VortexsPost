@echo off
rem Crea VortexsPost.exe con el compilador de C# que ya trae Windows (.NET Framework 4). No instala nada.
cd /d "%~dp0"
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
if not exist "%CSC%" (
  echo No se encuentra el compilador de .NET Framework 4 en este Windows.
  pause
  exit /b 1
)
if not exist "res\logo.png" (
  echo Falta la carpeta "res" junto a build.bat. Descomprime el ZIP completo y vuelve a probar.
  pause
  exit /b 1
)
"%CSC%" /nologo /codepage:65001 /optimize+ /target:winexe /out:VortexsPost.exe /win32icon:res\app.ico /r:System.dll /r:System.Security.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll /resource:res\logo.png,VortexsPost.logo.png /resource:res\Outfit-Bold.ttf,VortexsPost.Outfit-Bold.ttf /resource:res\PlusJakartaSans-Regular.ttf,VortexsPost.PlusJakartaSans-Regular.ttf /resource:res\PlusJakartaSans-SemiBold.ttf,VortexsPost.PlusJakartaSans-SemiBold.ttf /resource:res\PlusJakartaSans-Bold.ttf,VortexsPost.PlusJakartaSans-Bold.ttf /resource:res\JetBrainsMono-Medium.ttf,VortexsPost.JetBrainsMono-Medium.ttf src\*.cs
if errorlevel 1 (
  echo.
  echo No se pudo crear VortexsPost.exe. Copia el texto de arriba si pides ayuda.
  pause
  exit /b 1
)
echo.
echo Listo: VortexsPost.exe creado en esta carpeta. Ya puedes abrirlo.
pause
