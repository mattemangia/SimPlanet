@echo off
echo ===========================================
echo SimPlanet - Build Script
echo ===========================================
echo.
REM This is the main building script for Windows
REM Check if dotnet is installed
dotnet --version >nul 2>&1
if errorlevel 1 (
    echo X .NET 10 SDK is not installed!
    echo.
    echo Please install .NET 10 from:
    echo   https://dotnet.microsoft.com/download/dotnet/10.0
    echo.
    pause
    exit /b 1
)

echo + .NET SDK found
dotnet --version
echo.

REM Navigate to project directory
cd SimPlanet

echo Restoring NuGet packages...
dotnet restore

if errorlevel 1 (
    echo X Failed to restore packages
    pause
    exit /b 1
)

echo.
echo Building project...
dotnet build -c Release

if errorlevel 1 (
    echo X Build failed
    pause
    exit /b 1
)

echo.
echo ==========================================
echo + Build successful!
echo ==========================================
echo.
echo To run the game:
echo   cd SimPlanet ^&^& dotnet run
echo.
echo Or to run the optimized release build:
echo   cd SimPlanet\bin\Release\net10.0 ^&^& SimPlanet.exe
echo.
pause
