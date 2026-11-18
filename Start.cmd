@echo off
setlocal
cd /d "%~dp0"
if exist "LumaDesk.exe" goto launch
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
 echo Microsoft .NET Framework 4.x is required. Windows 10/11 normally includes it.
 pause
 exit /b 1
)
"%CSC%" /nologo /target:winexe /optimize+ /platform:anycpu /win32icon:Source\LumaDesk.ico /out:LumaDesk.exe /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Web.Extensions.dll /reference:System.Security.dll Source\LumaDesk.cs Source\Scene.cs Source\NativeSurface.cs Source\Melodies.cs Source\EditorUi.cs Source\ChromeForm.cs Source\EventListUi.cs Source\SnapLayout.cs Source\WidgetPlacement.cs Source\AppTheme.cs Source\WeatherProviders.cs Source\CloudSync.cs Source\AccountUi.cs Source\NotesModel.cs Source\NotesUi.cs /resource:Source\Cities.tsv.gz,LumaDesk.Cities > build.log 2>&1
if errorlevel 1 (
 type build.log
 echo Build failed. Please send build.log to the developer.
 pause
 exit /b 1
)
:launch
start "" "%~dp0LumaDesk.exe"
