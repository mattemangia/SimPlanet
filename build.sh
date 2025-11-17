#!/bin/bash

echo "========================================="
echo "SimPlanet - Build Script"
echo "========================================="
echo ""

# Check if dotnet is installed
if ! command -v dotnet &> /dev/null
then
    echo "❌ .NET 8 SDK is not installed!"
    echo "-----------------------------------------------"
    echo "Please install .NET 8 from:"
    echo "  - Linux/macOS: https://dot.net/v1/dotnet-install.sh"
    echo "  - Or visit: https://dotnet.microsoft.com/download/dotnet/8.0"
    echo ""
    exit 1
fi

echo "✓ .NET SDK found: $(dotnet --version)"
echo ""

# Navigate to project directory
cd SimPlanet

echo "📦 Restoring NuGet packages..."
dotnet restore

if [ $? -ne 0 ]; then
    echo "❌ Failed to restore packages"
    exit 1
fi

echo ""
echo "🔨 Building project..."
dotnet build -c Release

if [ $? -ne 0 ]; then
    echo "❌ Build failed"
    exit 1
fi

echo ""
echo "========================================="
echo "✅ Build successful!"
echo "========================================="
echo ""
echo "To run the game:"
echo "  cd SimPlanet && dotnet run"
echo ""
echo "Or to run the optimized release build:"
echo "  cd SimPlanet/bin/Release/net8.0 && ./SimPlanet"
echo ""
