# Changelog

All notable changes to this project will be documented in this file.

### New Features

- **Coastal/Beach Biome**: Added proper coastal environment detection and rendering. Beach terrain now displays correctly along coastlines with sandy textures, shells, pebbles, and wet sand near water.
- **Procedural Terrain Textures**: Completely rewritten terrain rendering with procedural patterns for all biome types:
  - Deep ocean with depth-based coloring
  - Shallow water with wave patterns
  - Beaches with shells and pebbles
  - Grasslands with field patterns
  - Forests with tree canopy variation and clearings
  - Deserts with dune wave patterns and rocky outcrops
  - Mountains with rocky textures and snow caps
  - Tundra with mossy rocks and permafrost
  - Ice with cracks and snow variation
- **Catastrophic Asteroid Impacts**: Completely overhauled asteroid impacts with realistic effects:
  - Multi-phase destruction: crater formation, thermal blast, shockwave
  - Crater sizes now 5x larger with proper ejecta rings
  - Incinerates area and destroys all life/infrastructure in blast zone
  - Triggers widespread fires in surrounding regions
  - Impact winter effect (reduced solar energy)
  - Triggers massive tsunamis if impact is in/near water
  - Extinction-level events for size 5 asteroids
- **Enhanced Nuclear Effects**: Nuclear explosions now have realistic multi-phase effects:
  - Fireball vaporization zone (for weapons)
  - Thermal blast zone with burns and infrastructure destruction
  - Radiation zone with contamination and life mutation/death
  - Fallout zone with persistent radioactive contamination
  - Nuclear winter effect for weapons
  - Triggers tsunamis if detonated in/near water
- **Tsunami System Enhancements**: Added new tsunami triggers:
  - `InitiateTsunamiFromImpact()` for asteroid impacts
  - `InitiateTsunamiFromNuke()` for nuclear explosions
  - Proper wave propagation from impact point
- **EMP (Electromagnetic Pulse) Effect**: Nuclear weapons now generate realistic EMP:
  - Massive radius (80 cells) - much larger than blast
  - Disables all electronics and power infrastructure
  - Increases meltdown risk for nuclear plants
  - Solar farms and wind turbines lose power output
  - Power lines and stations disabled
  - 5-year recovery time for affected areas
- **Electricity/Power Grid View**: New render mode to visualize power infrastructure:
  - Shows power generation (nuclear plants, solar farms, wind turbines)
  - Power distribution (stations, transmission lines)
  - Powered vs unpowered civilization areas
  - EMP-disabled zones highlighted in red
  - Power consumption intensity visualization
- **Realistic Water Formation**: Water no longer automatically fills depressions:
  - Ocean connectivity tracking (flood-fill algorithm)
  - Depressions only fill if connected to ocean OR receive rainfall/rivers
  - Isolated basins remain dry and display as salt flats
  - Gradual lake formation from rainfall accumulation
  - Evaporation in hot climates
- **Dry Basin Visualization**: Isolated depressions without water sources now display as:
  - Salt flats (deep depressions)
  - Dry cracked clay
  - Red/brown dirt in hot climates

### UI & Visuals

- **Disaster Effects Overlay**: Visual indicators for disaster damage:
  - Impact craters shown as dark scorched earth
  - Blast damage shown as burned/charred terrain
  - Impact scorching with orange/red burn marks
  - Radioactive contamination with sickly green/yellow glow
- **Toolbar Cleanup**: Removed duplicate game control buttons from the top toolbar to keep core controls in the bottom bar.
- **Lake Formation**: Partially filled basins show gradual transition from dry to water

### Improvements & Technical

- **Tundra Terrain Type**: Now properly detected based on temperature and elevation
- **Ocean Connectivity**: Flood-fill algorithm determines which depressions are connected to ocean
- **Disaster Recovery**: Improved recovery tracking for long-term disaster effects
- **Render Thread Performance**: Terrain texture updates no longer block the render thread, reducing stutters during map refreshes.
- **Map Texture Stability**: Resolved rendering artifacts and lost updates during map texture generation after recent thread-safety changes.

### Fixes

- Fixed coastal/beach terrain type never being returned by `GetTerrainType()`
- Fixed asteroids and nukes having minimal visual/environmental impact
- Fixed water appearing instantly in any depression regardless of water source
- Prevented map zoom from triggering while mouse-wheel-driven tools (Life Painter, Terraforming, Disaster targeting) are active, restoring zoom control when the tool closes.

---

- **Headless Mode**: Added `HeadlessSimulation` to support running the simulation without a GUI using the `--no-gui` command-line argument. Ideal for performance testing or server environments.
- **Geological Profile Tool (J)**: Added a new tool to draw a cross-section line on the map and view a detailed 2D subsurface profile window, visualizing crust, sediment layers, magma chambers, and water depth.
- **Graphing System (Y)**: Implemented a real-time graphing overlay to track planetary metrics over time, including Global Temperature, Oxygen, CO2, Population, and Biomass.
- **Life Painter Tool (L)**: Added a creative tool allowing users to paint specific life forms (Bacteria, Algae, Plants, Animals, Civilizations) directly onto the map with adjustable brush sizes.
- **Terraforming Tool (T)**: Introduced a dedicated height modification tool to directly raise or lower terrain elevation.
- **Manual Fault Tool (U)**: Added a tool to manually draw tectonic faults on the map, integrating with the geological simulation.
- **Ecosystem Simulator**: Implemented a new `EcosystemSimulator` to refine biological interactions and ecosystem stability.
- **Update Manager**: Created a centralized `UpdateManager` to orchestrate the simulation loop, improving thread management and allowing for staged updates of simulation systems.
- **Planetary Control State**: Added a new system to manage state for planetary control parameters.

### UI & Visuals

- **UI Restyling**: Implemented a new grouped icon system for better organization and visual clarity in the toolbar.
- **Bottom Control Bar**: Added a new bottom bar providing easy access to essential game controls (speed, pause, save/load, map options).

### Improvements & Technical

- **Performance**: Optimized simulation orchestration with the new `UpdateManager`.
- **Code Quality**: Fixed various compilation warnings and null safety issues (PR #58).
- **Documentation**: Major updates to `PLAYER_GUIDE.md` and `README.md` to reflect all new tools, keybindings, and systems.
- **Build System**: Removed legacy `build.sh` script.

### Fixes

- ADDRESSED compilation warnings and null reference risks throughout the codebase.
- FIXED visual glitches.

## Features

### Core Simulation
- **Planetary Evolution**: Real-time simulation of climate, atmosphere, and life from bacteria to civilization.
- **Geology**: Tectonic plates, earthquakes, volcanoes, erosion, and sedimentation.
- **Climate & Weather**: Temperature, rainfall, humidity, ice cycles, and realistic storm systems (hurricanes).
- **Atmosphere**: Oxygen/CO2 cycles, greenhouse effect, and magnetosphere radiation protection.
- **Life**: 7-stage evolution, biomass dynamics, and ecosystem interactions.
- **Civilization**: City building, technology progression, diplomacy, and war.

### Interactive Tools
- **Terraforming**: Manual tools for planting life, raising mountains, and creating oceans.
- **God Mode**: Divine powers to bless/curse civilizations or force diplomatic outcomes.
- **Disasters**: Trigger earthquakes, meteors, and pandemics.
- **Analysis**: Graphs, geological profiles, and detailed cell inspection.

### Visualization
- **22+ View Modes**: Visualize everything from temperature and wind to radiation and political borders.
- **3D Minimap**: Interactive rotating globe with synchronized weather.
- **Overlays**: Real-time visualization of faults, rivers, and disasters.
- **UI**: Interactive toolbar with grouped icons and comprehensive control panels.
