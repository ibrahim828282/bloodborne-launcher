@echo off
rem Builds "Bloodborne Launcher.exe" with the C# compiler that comes with Windows,
rem puts a shortcut on the desktop and opens the launcher.
cd /d "%~dp0"
taskkill /im "Bloodborne Launcher.exe" /f >nul 2>&1
timeout /t 1 /nobreak >nul
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (echo  .NET Framework 4 compiler not found. & pause & exit /b 1)
echo  Building Bloodborne Launcher ...
"%CSC%" /nologo /codepage:65001 /target:winexe /optimize+ /out:"Bloodborne Launcher.exe" /win32icon:assets\app.ico /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll /resource:assets\bg.jpg,bg.jpg /resource:assets\normal.png,normal.png /resource:assets\seamless.png,seamless.png /resource:assets\Cinzel-Bold.ttf,Cinzel-Bold.ttf /resource:assets\lang.txt,lang.txt src\Launcher.cs > build.log 2>&1
if errorlevel 1 (
  echo.
  echo  Build failed - details in build.log:
  type build.log
  pause
  exit /b 1
)
echo  [OK] Bloodborne Launcher.exe
powershell -NoProfile -ExecutionPolicy Bypass -Command "$s=(New-Object -ComObject WScript.Shell).CreateShortcut([IO.Path]::Combine([Environment]::GetFolderPath('Desktop'),'Bloodborne Launcher.lnk')); $s.TargetPath='%~dp0Bloodborne Launcher.exe'; $s.WorkingDirectory='%~dp0'; $s.IconLocation='%~dp0Bloodborne Launcher.exe,0'; $s.Save()"
echo  [OK] desktop shortcut
start "" "%~dp0Bloodborne Launcher.exe"
