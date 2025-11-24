# Changelog

All notable changes to this project will be documented in this file.

## [Unreleased]

### New Features

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
