@echo off
setlocal
cd /d "%~dp0.."
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
"%CSC%" /nologo /out:CloudChecks.exe /reference:LumaDesk.exe /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Web.Extensions.dll /reference:System.Security.dll Tests\CloudChecks.cs
if errorlevel 1 exit /b 1
CloudChecks.exe
if errorlevel 1 exit /b 1
"%CSC%" /nologo /out:NotesChecks.exe /reference:LumaDesk.exe /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Web.Extensions.dll /reference:System.Security.dll Tests\NotesChecks.cs
if errorlevel 1 exit /b 1
NotesChecks.exe
if errorlevel 1 exit /b 1
"%CSC%" /nologo /out:PlacementChecks.exe /reference:LumaDesk.exe /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Web.Extensions.dll Tests\PlacementChecks.cs
if errorlevel 1 exit /b 1
PlacementChecks.exe
if errorlevel 1 exit /b 1
"%CSC%" /nologo /out:ThemeChecks.exe /reference:LumaDesk.exe /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Web.Extensions.dll Tests\ThemeChecks.cs
if errorlevel 1 exit /b 1
ThemeChecks.exe
if errorlevel 1 exit /b 1
if /i not "%~1"=="transport" exit /b 0
"%CSC%" /nologo /out:TransportChecks.exe /reference:LumaDesk.exe /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Web.Extensions.dll /reference:System.Security.dll Tests\TransportChecks.cs
if errorlevel 1 exit /b 1
TransportChecks.exe
