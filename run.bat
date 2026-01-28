@echo off
cd /d "%~dp0"
echo ========================================
echo        LOTUS - Eli's Edition
echo ========================================
echo.

echo Ensuring clean state...
:: Force delete residual files that break the build
if exist "Models\ThemeChangedEventArgs.cs" del /f /q "Models\ThemeChangedEventArgs.cs"
if exist "Models\MascotState.cs" del /f /q "Models\MascotState.cs"
if exist "Services\AudioGuardianService.cs" del /f /q "Services\AudioGuardianService.cs"
if exist "Services\ConfigurationService.cs" del /f /q "Services\ConfigurationService.cs"
if exist "Services\MascotBrain.cs" del /f /q "Services\MascotBrain.cs"
if exist "Services\ThemeService.cs" del /f /q "Services\ThemeService.cs"
if exist "Services\TimeService.cs" del /f /q "Services\TimeService.cs"
if exist "App.xaml" del /f /q "App.xaml"
if exist "App.xaml.cs" del /f /q "App.xaml.cs"
if exist "MainWindow.xaml" del /f /q "MainWindow.xaml"
if exist "MainWindow.xaml.cs" del /f /q "MainWindow.xaml.cs"

echo Cleaning previous build artifacts...
if exist bin rmdir /s /q bin >nul 2>&1
if exist obj rmdir /s /q obj >nul 2>&1
echo.

echo Restoring packages...
dotnet restore --verbosity quiet
if errorlevel 1 (
    echo ERROR: Failed to restore packages
    pause
    exit /b 1
)
echo.

echo Building...
dotnet build --verbosity quiet
if errorlevel 1 (
    echo ERROR: Build failed
    pause
    exit /b 1
)
echo.

echo Starting LOTUS...
start "" "bin\Debug\net9.0-windows\win-x64\Lotus.exe"
exit
