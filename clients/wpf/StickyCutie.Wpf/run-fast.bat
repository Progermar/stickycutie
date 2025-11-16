@echo off
taskkill /IM StickyCutie.Wpf.exe /F >nul 2>&1
dotnet build
start "" ".\bin\Debug\net8.0-windows\StickyCutie.Wpf.exe"