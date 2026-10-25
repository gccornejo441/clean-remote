@echo off
setlocal
where dotnet >nul 2>&1
if errorlevel 1 (
  echo .NET 10 SDK is required. Install it from https://dotnet.microsoft.com/download/dotnet/10.0
  pause
  exit /b 1
)
dotnet publish "%~dp0CleanTVRemote.csproj" -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:DebugType=none /p:DebugSymbols=false -o "%~dp0publish"
if errorlevel 1 (
  echo Build failed. Review the errors above.
  pause
  exit /b 1
)
echo.
echo Your Windows app is at: %~dp0publish\CleanTVRemote.exe
pause
