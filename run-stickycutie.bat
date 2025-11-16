@echo off
REM Fecha qualquer instância anterior presa do app antes de compilar.
taskkill /F /IM StickyCutie.Wpf.exe >nul 2>nul

dotnet run --project clients\wpf\StickyCutie.Wpf\StickyCutie.Wpf.csproj %*
