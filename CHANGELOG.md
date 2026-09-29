# Changelog

All notable changes to this project will be documented in this file.

## Latest Changes

### Living Civilizations: villages, economy, armies and history

- **Settlements that grow**: every people starts as a single tribal village. Settlements work
  the land around them (hunting, gathering, farming, fishing, forestry, quarrying, mining),
  grow into towns, cities and metropolises, and send settlers to found new villages on fertile
  river valleys and coasts. Overcrowded settlements lose people to emigration; failed harvests
  bring famine.
- **Economy**: food, wood, stone, metal and gold stockpiles. Trade routes and markets bring gold,
  armies cost upkeep, and trading cities import the realm's surplus harvest.
- **City buildings**: granaries, walls, temples, markets, barracks, harbours, workshops and
  universities, chosen according to each city's needs (famine, threats, unrest).
- **Real wars**: wars start from border friction, hunger for land, ambition and relative strength
  (nuclear powers deter attacks). Allies honour defensive pacts. Armies are levied from cities,
  march across the map with pathfinding (ships once seafaring is known), fight battles
  influenced by technology, morale, experience, weapons and terrain, besiege and capture cities,
  leave garrisons and annex peoples that lose all their cities. War weariness leads to peace
  treaties with reparations.
- **Rebellions**: unhappy, distant provinces of unstable realms can break away and form new
  nations; unstable governments can fall to revolutions.
- **Diplomacy**: opinions drift with shared borders, trade, royal marriages and forms of
  government; trade pacts and defensive alliances are signed and renewed.
- **Biosphere feedback**: hunting thins wildlife (and can wipe it out locally), fishing depletes
  coastal waters, farming clears forests, tires the soil and releases methane, and harvests
  depend on each region's rainfall and temperature, so climate change causes famines.
- **Names and history**: each people has its own naming culture; its formal name follows its
  government (Kingdom, Republic, Empire...). A chronicle records foundings, wars, battles,
  conquests, famines, revolutions and treaties.
- **Save games** now keep settlements, resources and diplomatic relations.
- **Headless testing**: `--no-gui --years N --civs N --size WxH --seed N [--civ-only]` runs long
  simulations and prints the chronicle.

### Strategic AI, weapons of mass destruction, space and politics

- **Strategic AI**: every year each nation assesses threats (neighbours' strength and hostility),
  opportunities (weak, isolated neighbours with fertile land), needs (famine, instability) and the
  personality of its ruler and ruling party, then picks a posture — develop, expand, research,
  fortify, militarize, conquer, space race or recover — with a matching budget. Wars are deliberate
  decisions against a chosen target, armed nations keep standing armies, and armies avoid fighting
  near nuclear power plants.
- **Weapons of mass destruction, used as a last resort**: nuclear weapons require a national
  programme, pursued mostly when a rival already has them. First use needs an existential threat
  and ruthless, unaccountable leaders, and is strongly deterred by the enemy's ability to retaliate;
  a nuclear attack is almost always answered. Missile defence can intercept warheads. Each strike
  devastates a city (blast, fallout, EMP, tsunamis), melts down nearby reactors, lofts soot that
  causes a nuclear winter (less sunlight, failed harvests, famine) and outrages the whole world.
  Brutal regimes may gas besieged cities; engineered plagues are a reckless last gamble.
  Battles around nuclear power plants can cause meltdowns and fallout.
- **Space**: rocketry, satellites (better disaster preparedness), crewed flight, space stations,
  lunar bases and off-world colonies. When a nation is destroyed its crews survive in space and,
  once the skies clear, return to the surface to found a new nation with their knowledge.
- **National programmes**: research (Great Library, Printing Press, Scientific Academy,
  Electrification, Computer Revolution, Genome Project, Artificial Intelligence, Fusion Power),
  economic (Irrigation, Roads, Central Bank, Industrialization, Green Revolution, Global Trade),
  environmental (National Parks, Renewable Transition, Carbon Capture), space and military ones.
- **Energy, grid and internet**: energy mix evolving from biomass to coal, oil, hydro, nuclear,
  wind, solar and fusion (emissions follow the mix), power grids with blackouts, internet with
  backbone and undersea cables, airports and spaceports.
- **Peoples and geography**: every nation has an ethnicity and a homeland climate; temperate river
  valleys develop fastest, deserts, jungles, highlands and cold lands more slowly.
- **Epidemics**: crowded cities, livestock, trade and tropical climates breed plagues that spread
  between nations; medicine and the Genome Project make them rarer; cures are researched.
  Diseases now run on game years and can wipe out whole peoples.
- **Politics**: republics and democracies have parties (conservative, liberal, socialist, green,
  nationalist, technocratic, religious) and elections; voters punish famine, war and hardship, and
  the ruling ideology steers the nation. Monarchies have royal families, primogeniture lines of
  succession, regnal numbers, regencies for child monarchs, succession crises and wars of
  succession. Rulers are named in their people's language.
- **Headless**: `--doomsday N` starts a global nuclear war in year N.

### The planet strikes back

- Volcanic eruptions, asteroid impacts, tornadoes, floods, torrential rain, acid rain,
  landslides and civil nuclear accidents now kill and demoralise the people of the settlements
  they hit; EMPs black out cities. Strong ones are recorded in the chronicle.
- Settlements swallowed by the sea or buried by advancing ice are evacuated to the nearest safe
  settlement of the same nation.
- Refugees: famine and sieges drive people to neighbouring nations with food to spare; friendly
  hosts grow closer, others resentful.
- **Instant Peace** divine power: every war ends, armies go home, and no nation may start a war
  on its own for 30 years.

### Climate and geology fixes

- **Runaway nitrous oxide**: an always-on ocean source and a negligible sink made N2O climb toward
  its cap on every planet, heating it even without life. N2O now has a ~120-year lifetime and
  sources calibrated to the pre-industrial level; an empty planet keeps a stable temperature.
- Industrial civilizations no longer raise the solar energy directly (a hidden extra greenhouse
  effect that also stopped nuclear winter and the stabilizer from dimming the sun); warming now
  comes only from the greenhouse gases they emit.
- Volcanoes need space between them and their number is capped, so plate boundaries no longer
  fill up with volcano chains; volcano sprites are decluttered on screen.
- After loading a game or starting a new one, civilizations are again connected to the weather,
  disaster and geology systems (hurricanes, meltdowns and eruptions affected them only in the
  first game of a session).

### Performance

- Faster atmosphere mixing, ocean currents, thermohaline circulation, cyclone eddies and
  neighbour lookups (about 25-30% less time per simulation step, same physics).
- Societies update once per game year instead of every frame.

### New Features

- **City Icons and Markers**: Cities are now visually represented on the map with distinct icons:
  - Capital cities displayed as gold stars with white outlines
  - Regular cities shown as colored circles matching their civilization's color
  - Icon size scales with zoom level and city population
  - Metropolises have slightly larger icons than smaller settlements
  - Icons visible in all view modes except geological overlays (for clarity)

- **Actual Tectonic Plate Movement**: Tectonic plates now genuinely drift over geological time:
  - Continents gradually move based on plate velocities
  - Cells migrate from one plate to another at leading edges
  - Movement rate: ~0.5% of boundary cells per 50 simulation years
  - Movement scales with Tectonic Activity Level control
  - Visible continental drift over thousands of simulation years
  - Realistic plate accretion and boundary evolution

### Improvements & Technical

- **Enhanced Civilization Visualization**: Cities are no longer just colored terrain cells - they have proper visual markers
- **Geological Realism**: Plate tectonics now includes actual plate motion, not just boundary interactions
- **Continental Drift**: You can now watch continents slowly drift apart and collide over geological time scales

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
