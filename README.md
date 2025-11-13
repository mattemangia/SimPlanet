# SimPlanet - Planetary Evolution Simulator

A SimEarth-like planetary simulation game built with C# and MonoGame, featuring:
- Procedural planet generation with Perlin noise
- **Full geological simulation** (plate tectonics, volcanoes, erosion, sedimentation)
- **Hydrology system** (rivers, water flow, ocean currents)
- **Weather systems** (storms, seasons, air pressure, wind patterns)
- **Civilization development** (technology advancement, territorial expansion, environmental impact)
- **Rotating 3D planet minimap** (just like SimEarth!)
- **Save/load game system** with quick save/load (F5/F9)
- **Main menu** with new game, load game, and pause functionality
- Climate simulation (temperature, rainfall, humidity)
- Atmospheric simulation (oxygen, CO2, greenhouse effects)
- Life evolution from bacteria to civilization with full environmental reactivity
- 10 visualization modes with geological overlays
- Real-time planetary evolution and geological events

## Features

### Core Mechanics (SimEarth-like)
- **Terrain Generation**: Procedural height maps with configurable land/water ratios
- **Geological Systems**:
  - 8 tectonic plates with continental drift
  - Plate boundaries (convergent, divergent, transform)
  - Mountain building and subduction zones
  - Volcanic hotspots and eruption mechanics
  - Earthquakes from tectonic stress
  - Erosion (rainfall, temperature, glacial)
  - Sediment transport and deposition
- **Hydrology System**:
  - River formation from mountains to oceans
  - Water flow and valley carving
  - Ocean currents with Coriolis effect
  - Soil moisture dynamics
- **Climate System**: Temperature gradients, rainfall patterns, humidity simulation
- **Atmosphere**: Oxygen and CO2 cycles, greenhouse effect modeling
- **Weather Systems**:
  - Dynamic meteorology with seasons (4 seasons per year, hemisphere-aware)
  - Wind patterns (trade winds, westerlies, polar easterlies)
  - Air pressure systems affected by temperature and elevation
  - Storm generation and tracking (thunderstorms, hurricanes, blizzards, tornadoes)
  - Seasonal temperature variations based on latitude
- **Civilization Development**:
  - Technology progression: Tribal → Agricultural → Industrial → Scientific → Spacefaring
  - Population growth and territorial expansion
  - Environmental impact (pollution, deforestation, CO2 emissions)
  - Advanced civilizations can terraform and restore ecosystems
  - Inter-civilization interactions (war, cooperation, technology sharing)
- **Life Evolution**:
  - Bacteria → Algae → Plants → Simple Animals → Complex Animals → Intelligence → Civilization
  - Life spreads and adapts based on environmental conditions
  - **Full reactivity to planetary events**:
    - Volcanic eruptions cause mass extinctions in affected areas
    - Earthquakes damage life based on magnitude
    - Storms (hurricanes, tornadoes, blizzards) affect biomass
    - Climate stress drives evolution and adaptation
    - Sedimentation and environmental changes impact survival
  - Biomass dynamics and ecosystem interactions
- **Save/Load System**:
  - Quick save with F5, quick load with F9
  - Full menu system with save slots and timestamps
  - Serializes entire game state (terrain, life, civilizations, weather, geology)
- **Time Control**: Adjustable simulation speed (0.25x to 32x)
- **3D Minimap**: Rotating sphere view of your planet (SimEarth-style!)

### Visualization Modes
**Standard Views (1-0 keys):**
1. **Terrain**: See the planet surface (oceans, land, forests, deserts, mountains) with day/night cycle and city lights
2. **Temperature**: Heat map showing temperature distribution
3. **Rainfall**: Precipitation patterns across the planet
4. **Life**: Visualization of life forms and biomass
5. **Oxygen**: Atmospheric oxygen levels
6. **CO2**: Carbon dioxide concentration
7. **Elevation**: Height map view
8. **Geological**: Rock types (volcanic, sedimentary, crystalline), erosion, sedimentation
9. **Tectonic Plates**: See all 8 plates with boundaries highlighted
10. **Volcanoes**: Volcanic activity and lava flows

**Meteorology Views (F1-F4 keys):**
11. **Clouds (F1)**: Cloud cover visualization with storm clouds
12. **Wind (F2)**: Wind speed and direction patterns (calm to extreme)
13. **Pressure (F3)**: Air pressure systems (low pressure = blue, high pressure = red)
14. **Storms (F4)**: Active storms with precipitation and wind intensity

### Geological Overlays (Toggle On/Off)
- **Volcanoes**: Red triangles showing active volcanoes
- **Rivers**: Blue lines showing river networks
- **Plate Boundaries**: Highlighted convergent, divergent, and transform zones
- **3D Minimap**: Rotating globe in bottom-left corner

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
| **1-0** | Change view modes |
| **1** | Terrain view |
| **2** | Temperature view |
| **3** | Rainfall view |
| **4** | Life view |
| **5** | Oxygen view |
| **6** | CO2 view |
| **7** | Elevation view |
| **8** | Geological view (rock types, erosion) |
| **9** | Tectonic Plates view |
| **0** | Volcanoes view |
| **F1** | Clouds view (meteorology) |
| **F2** | Wind view (meteorology) |
| **F3** | Air Pressure view (meteorology) |
| **F4** | Storms view (meteorology) |
| **+/-** | Increase/Decrease time speed |
| **C** | Toggle day/night cycle (auto-enabled at <0.5x speed) |
| **L** | Seed new life forms |
| **M** | Open map generation options menu |
| **P** | Toggle 3D rotating minimap |
| **V** | Toggle volcano overlay |
| **B** | Toggle river overlay |
| **N** | Toggle plate boundary overlay |
| **R** | Regenerate planet with current settings |
| **H** | Toggle help panel |
| **F5** | Quick save game |
| **F9** | Quick load game |
| **ESC** | Pause menu / Back to main menu |
| **Mouse Wheel** | Zoom in/out (0.5x to 4.0x) |
| **Middle Click + Drag** | Pan camera around the map |

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

3. **Weather System**:
   - Seasonal progression with hemisphere-aware temperature variations
   - Wind patterns driven by temperature gradients and Coriolis effect
   - Air pressure systems that influence weather formation
   - Storm generation based on atmospheric conditions (temperature, humidity, pressure)
   - Storm types: Thunderstorms, Hurricanes, Blizzards, Tornadoes
   - Storms move and dissipate over time, affecting local environment

4. **Geological System**:
   - Tectonic plates move and interact at boundaries
   - Volcanic eruptions release heat, CO2, and reshape terrain
   - Erosion wears down mountains, transports sediment
   - Rivers carve valleys and deposit sediment in lowlands
   - Earthquakes occur from tectonic stress buildup

5. **Life System**:
   - Life emerges in suitable conditions (temperature, humidity, oxygen)
   - Evolution occurs when biomass is high and conditions are favorable
   - Life spreads to neighboring cells
   - Death occurs in extreme conditions
   - **Life reacts to all planetary events**:
     - Volcanic eruptions destroy nearby life
     - Earthquakes cause damage based on magnitude
     - Storms reduce biomass and kill organisms
     - Temperature extremes, oxygen levels, and CO2 toxicity affect survival
     - Environmental stress triggers evolutionary adaptations

6. **Civilization System**:
   - Civilizations emerge from Intelligence-level life
   - Technology advances through 5 stages (Tribal → Spacefaring)
   - Population grows and territory expands
   - Environmental impact: pollution, deforestation, CO2 emissions
   - Advanced civilizations can terraform and restore ecosystems
   - Civilizations can interact, cooperate, or compete

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

### Core Data and Generation
- **TerrainCell.cs**: Individual cell data structure with extensions for geology and meteorology
- **PlanetMap.cs**: Planet grid and map generation (200x100 cells)
- **PerlinNoise.cs**: Procedural noise generation for terrain

### Simulation Systems
- **ClimateSimulator.cs**: Temperature, rainfall, humidity simulation
- **AtmosphereSimulator.cs**: Atmospheric gas cycles (O2, CO2, greenhouse effect)
- **LifeSimulator.cs**: Life evolution, biomass dynamics, and event reactivity
- **GeologicalSimulator.cs**: Plate tectonics, volcanoes, erosion, sedimentation
- **HydrologySimulator.cs**: Rivers, water flow, ocean currents, soil moisture
- **WeatherSystem.cs**: Seasons, storms, wind patterns, air pressure
- **CivilizationManager.cs**: Civilization emergence, technology, expansion, interactions

### Rendering and UI
- **TerrainRenderer.cs**: Rendering with 10 view modes and procedural colors
- **GameUI.cs**: Information panels showing stats, civilizations, weather alerts
- **MapOptionsUI.cs**: Map generation configuration interface
- **PlanetMinimap3D.cs**: Rotating 3D sphere visualization
- **GeologicalEventsUI.cs**: Event log and overlays (volcanoes, rivers, plate boundaries)
- **SimpleFont.cs**: Procedural font rendering (no external assets needed)

### Game Management
- **SimPlanetGame.cs**: Main game loop and orchestration
- **MainMenu.cs**: Menu system (main menu, load game, pause menu)
- **SaveLoadManager.cs**: Save/load game state with JSON serialization
- **SaveGameData.cs**: Serializable data structures for save files

## Tips for Playing

1. **Start from Menu**: Launch the game to see the main menu, then select "New Game" to begin
2. **Start Slowly**: Begin at 1x speed to watch initial life emergence
3. **Seed Life**: Press L to add bacteria in suitable areas if life hasn't emerged naturally
4. **Monitor Oxygen**: Plants must establish before complex life can evolve (15%+ for animals, 20%+ for intelligence)
5. **Watch for Planetary Events**:
   - Volcanic eruptions will devastate local ecosystems
   - Major storms can damage life and reshape coastlines
   - Civilizations will begin polluting and altering the environment
6. **Save Often**: Use F5 to quick save your progress, especially before major experiments
7. **Track Civilizations**: Watch the info panel for civilization emergence and development
8. **Weather Alerts**: Pay attention to storm warnings in the UI - they can cause significant damage
9. **Experiment**: Press R to generate new planets with different characteristics
10. **Use View Modes**: Switch between different views (1-0 keys) to understand your planet better

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
- Interactive terraforming tools (add/remove water, heat/cool areas, seed specific life forms)
- Asteroid and comet impacts with extinction events
- Long-term ice age and warming cycles
- Player-controllable disasters and events
- More civilization interactions (diplomacy, trade, warfare)
- Advanced civilization technologies (space stations, planetary shields)
- Multiple planet simulations running simultaneously

## What's New in This Version

### Latest Update - Visual & Interaction Enhancements
- ✅ **Day/Night Cycle** - Cities light up at night! Auto-enables when time speed drops below 0.5x
- ✅ **4 New Meteorology Views** - Clouds (F1), Wind (F2), Air Pressure (F3), Storms (F4)
- ✅ **Mouse Pan & Zoom** - Mouse wheel to zoom (0.5x-4x), middle-click drag to pan
- ✅ **Civilization Warfare** - Aggressive civs declare war, conduct battles based on military strength
- ✅ **Transportation Systems** - Civilizations unlock land transport (horses/cars), ships, and planes as they advance
- ✅ **Enhanced Expansion** - Civs with ships can colonize islands, planes enable rapid global expansion
- ✅ **Trade Routes** - Peaceful civilizations establish trade for economic benefits
- ✅ **War Casualties** - Populations decrease during conflicts, stalemates cause attrition
- ✅ **Transport-Based Growth** - Expansion rates increase with better transportation
- ✅ **UI Enhancements** - Shows civilization war status, transportation tech, and population in thousands

### Previous Update - Complete Simulation
- ✅ **Full save/load system** with quick save (F5) and quick load (F9)
- ✅ **Main menu and pause menu** for better game management
- ✅ **Weather simulation** with storms, seasons, and meteorology
- ✅ **Civilization mechanics** with technology advancement and environmental impact
- ✅ **Life reactivity** - organisms now respond to all planetary events (volcanoes, earthquakes, storms, climate)
- ✅ **Enhanced UI** showing civilization info and weather alerts
- ✅ **Complete geological systems** (plate tectonics fully integrated with life)
- ✅ **Tectonic plate movement and interactions** with realistic boundary types

Enjoy watching your planet evolve!
