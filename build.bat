@echo off
REM Builds Blooms_Mods.dll and copies it into the game's Mods folder.
REM
REM Usage:
REM   build.bat                 build + install into the game
REM   build.bat --no-install    build only
REM
setlocal
set "DOTNET=C:\Program Files\dotnet\dotnet.exe"
set "GAME_DIR=C:\Users\131fgh\Desktop\PvZ.Fusion.Multi-lang.4.0.5.2.Alpha.by.Blooms\Game Files"
set "CONFIG=Release"

cd /d "%~dp0"

echo ==^> Building Blooms_Mods (%CONFIG%)
"%DOTNET%" build "%~dp0" -c %CONFIG%
if errorlevel 1 exit /b 1

if /i "%~1"=="--no-install" goto :done

if not exist "%GAME_DIR%\Mods" (
  echo Game Mods folder not found: %GAME_DIR%\Mods
  exit /b 1
)

copy /y "bin\%CONFIG%\net6.0\Blooms_Mods.dll" "%GAME_DIR%\Mods\"
echo ==^> Installed to %GAME_DIR%\Mods\

:done
echo ==^> Done
