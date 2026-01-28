@echo off
cd /d "%~dp0"
echo Starting LOTUS - Eli's Edition...
dotnet run --project Lotus.csproj -r win-x64 --no-build
pause
