# SimPlanet - Planetary Evolution Simulator

A SimEarth-like planetary simulation game built with C# and MonoGame, featuring:
- Procedural planet generation with Perlin noise
- Climate simulation (temperature, rainfall, humidity)
- Atmospheric simulation (oxygen, CO2, greenhouse effects)
- Life evolution from bacteria to civilization
- Multiple visualization modes
- Real-time planetary evolution

## Features

### Core Mechanics (SimEarth-like)
- **Terrain Generation**: Procedural height maps with configurable land/water ratios
- **Climate System**: Temperature gradients, rainfall patterns, humidity simulation
- **Atmosphere**: Oxygen and CO2 cycles, greenhouse effect modeling
- **Life Evolution**:
  - Bacteria → Algae → Plants → Simple Animals → Complex Animals → Intelligence → Civilization
  - Life spreads and adapts based on environmental conditions
  - Biomass dynamics and ecosystem interactions
- **Time Control**: Adjustable simulation speed (0.25x to 32x)

### Visualization Modes
1. **Terrain**: See the planet surface (oceans, land, forests, deserts, mountains)
2. **Temperature**: Heat map showing temperature distribution
3. **Rainfall**: Precipitation patterns across the planet
4. **Life**: Visualization of life forms and biomass
5. **Oxygen**: Atmospheric oxygen levels
6. **CO2**: Carbon dioxide concentration
7. **Elevation**: Height map view

## Requirements

- .NET 8.0 SDK or later
- Works on Linux, macOS, and Windows
- OpenGL-compatible graphics

## Building and Running

### On Linux/macOS:

```bash
# Install .NET 8 if not already installed
# For Ubuntu/Debian:
wget https://dot.net/v1/dotnet-install.sh
chmod +x dotnet-install.sh
./dotnet-install.sh --channel 8.0

# Build and run
cd SimPlanet
dotnet restore
dotnet build
dotnet run
```

### On Windows:

```powershell
# Install .NET 8 from https://dotnet.microsoft.com/download/dotnet/8.0

# Build and run
cd SimPlanet
dotnet restore
dotnet build
dotnet run
```

## Controls

| Key | Action |
|-----|--------|
| **SPACE** | Pause/Resume simulation |
| **1-7** | Change view mode (Terrain, Temperature, Rainfall, Life, Oxygen, CO2, Elevation) |
| **+/-** | Increase/Decrease time speed |
| **L** | Seed new life forms |
| **M** | Open map generation options menu |
| **R** | Regenerate planet with current settings |
| **H** | Toggle help panel |
| **ESC** | Quit game |

### Map Options Menu (Press M)

When the map options menu is open, use these keys to customize the planet:

| Key | Action |
|-----|--------|
| **Q/W** | Decrease/Increase land ratio (10% - 90%) |
| **A/S** | Decrease/Increase mountain level (0% - 100%) |
| **Z/X** | Decrease/Increase water level (-1.0 to 1.0) |
| **M** | Close menu |
| **R** | Regenerate planet with new settings |

## Map Generation Options

The game uses configurable map generation parameters:

- **Seed**: Random seed for reproducible maps (default: 12345)
- **Land Ratio**: Percentage of land vs water (default: 0.3 = 30% land)
- **Mountain Level**: How mountainous the terrain is (default: 0.5)
- **Water Level**: Sea level adjustment (default: 0.0)
- **Octaves**: Perlin noise detail level (default: 6)

To modify map generation, edit the `_mapOptions` in `SimPlanetGame.cs` or press **R** to regenerate with a random seed.

## How It Works

### Planetary Simulation

The simulation runs multiple interconnected systems:

1. **Climate System**:
   - Temperature based on latitude, elevation, and greenhouse effect
   - Rainfall influenced by ocean proximity and elevation
   - Heat and humidity diffusion across the planet

2. **Atmospheric System**:
   - Oxygen produced by photosynthetic life (algae, plants)
   - CO2 consumed by plants, produced by animals and civilization
   - Greenhouse effect influences global temperature

3. **Life System**:
   - Life emerges in suitable conditions (temperature, humidity, oxygen)
   - Evolution occurs when biomass is high and conditions are favorable
   - Life spreads to neighboring cells
   - Death occurs in extreme conditions

### Evolution Progression

Life evolves through stages when conditions are met:

```
Bacteria (warm, wet areas)
    ↓
Algae (in water, produces oxygen)
    ↓
Plant Life (on land with rain, produces more oxygen)
    ↓
Simple Animals (requires 15%+ oxygen, eats plants)
    ↓
Complex Animals (requires 18%+ oxygen)
    ↓
Intelligence (requires 20%+ oxygen, diverse ecosystem)
    ↓
Civilization (produces more CO2, can adapt to various climates)
```

## Game Architecture

- **TerrainCell.cs**: Individual cell data structure
- **PlanetMap.cs**: Planet grid and map generation
- **PerlinNoise.cs**: Procedural noise generation
- **ClimateSimulator.cs**: Temperature, rainfall, humidity simulation
- **AtmosphereSimulator.cs**: Atmospheric gas cycles
- **LifeSimulator.cs**: Life evolution and biomass dynamics
- **TerrainRenderer.cs**: Rendering with procedural colors
- **GameUI.cs**: User interface and information display
- **SimpleFont.cs**: Procedural font rendering (no external assets needed)
- **SimPlanetGame.cs**: Main game loop and orchestration

## Tips for Playing

1. **Start Slowly**: Begin at 1x speed to watch initial life emergence
2. **Seed Life**: Press L to add bacteria in suitable areas
3. **Monitor Oxygen**: Plants must establish before complex life can evolve
4. **Watch for Runaway Effects**: High CO2 from civilization can cause warming
5. **Experiment**: Press R to generate new planets with different characteristics

## Technical Details

- **Map Size**: 200x100 cells for good performance
- **Cell Size**: 4 pixels per cell (scalable)
- **Update Rate**: Real-time with variable time speed
- **Rendering**: Procedural texture generation, no external assets required
- **Cross-Platform**: Uses MonoGame DesktopGL for maximum compatibility

## Offline Operation

The game is completely self-contained:
- No internet connection required
- All sprites and graphics are procedurally generated
- No external asset downloads needed
- Font rendering is built-in

## License

This is a fan project inspired by SimEarth. All code is original.

## Future Enhancements

Potential additions (not yet implemented):
- Terraforming tools (add/remove water, heat/cool areas)
- Tectonic plate movement
- Asteroid impacts
- Ice age cycles
- Save/load functionality
- Configurable map generation UI

Enjoy watching your planet evolve!
