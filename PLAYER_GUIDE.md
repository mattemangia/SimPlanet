# SimPlanet - Complete Player Guide

Welcome to SimPlanet, a comprehensive planetary evolution simulator inspired by SimEarth! This guide will help you understand and master the complex systems that drive planetary evolution.

---

## Table of Contents

1. [Getting Started](#getting-started)
2. [Understanding the Interface](#understanding-the-interface)
3. [Core Concepts](#core-concepts)
4. [Simulation Systems](#simulation-systems)
5. [Playing as God - Divine Powers](#playing-as-god)
6. [Terraforming & Planetary Controls](#terraforming--planetary-controls)
7. [Visualization Modes](#visualization-modes)
8. [Tips & Strategies](#tips--strategies)
9. [Troubleshooting](#troubleshooting)

---

## Getting Started

### Your First Planet

1. **Launch the game** - You'll see a beautiful splash screen, then the main menu
2. **Start New Game** - Click "New Game" or press N
3. **Configure Your Planet** (Map Options Menu):
   - Choose a **preset** (Earth, Mars, Water World, Desert World) or customize
   - Adjust **land/water ratio** with the water level slider
   - Set **mountain height** to control terrain variation
   - Pick a **map size** (larger = more detailed but slower)
4. **Enable Auto-Stabilization** - Press E to toggle the automatic equilibrium system
   - This keeps your planet habitable without constant intervention
   - Highly recommended for beginners!
5. **Start Evolution** - Press SPACE to unpause and watch your world come alive

### Essential First Controls

| Key | Action | Why Important |
|-----|--------|---------------|
| **SPACE** | Pause/Unpause | Control simulation speed |
| **E** | Toggle Auto-Stabilizer | Keeps planet habitable |
| **1-9** | View Modes | See different planetary data |
| **Mouse Click** | Inspect Cell | View detailed cell information |
| **P** | 3D Minimap | See your planet from space |
| **M** | Map Options | Regenerate or modify planet |

---

## Understanding the Interface

### The Toolbar (Top of Screen)

The interactive toolbar gives you one-click access to all features:

- **View Mode Buttons** - Switch between 22+ visualization modes
- **Game Controls** - Pause, speed controls, save/load
- **UI Toggles** - Show/hide various panels and overlays
- **Tools** - Terraforming, disasters, divine powers
- **Hover for Tooltips** - See what each button does and its keyboard shortcut

### Information Panels

**Top-Left Corner (Game Info Panel):**
- Current year and planetary age
- Average temperature and global climate stats
- Oxygen and CO2 levels
- Active life forms and civilizations
- Auto-stabilizer status and last action

**Bottom Section (Cell Inspector):**
- When you click a cell, detailed information appears
- Shows terrain type, elevation, temperature
- Life forms present, biomass levels
- Atmospheric composition at that location
- Geological data (sediment layers, plate info)

**Right Side (3D Minimap - Press P):**
- Real-time 3D globe of your planet
- Click and drag to rotate
- See day/night cycle, clouds, and ice coverage
- Provides planetary context

---

## Core Concepts

### Understanding Planetary Habitability

Your planet needs these conditions for complex life:

1. **Temperature**: -10°C to 40°C average
   - Too cold: Everything freezes, life dies
   - Too hot: Water evaporates, proteins denature
   - **Sweet spot**: 10-25°C for thriving ecosystems

2. **Atmosphere**: Balanced gas composition
   - **Oxygen**: 15-30% (breathable for animals)
   - **CO2**: 0.01-5% (needed for plants, but toxic in excess)
   - **Pressure**: 800-1200 mb (Earth = 1013 mb)

3. **Water**: Liquid water is essential
   - Oceans provide thermal regulation
   - Rain supports land-based life
   - Ice locks water away from ecosystems

4. **Magnetic Field**: Protects from cosmic radiation
   - Core temperature must be >3000K
   - Shields atmosphere from solar wind
   - Prevents life-killing radiation

### The Simulation Loop

Every frame, the simulator runs these systems in order:

1. **Climate System** → Calculates temperature, rainfall, humidity
2. **Atmosphere System** → Updates O2, CO2, greenhouse gases, wind
3. **Weather System** → Generates storms, moves clouds, creates seasons
4. **Life System** → Evolves organisms, photosynthesis, death
5. **Geological System** → Plate tectonics, volcanoes, erosion
6. **Hydrology System** → River flow, ocean currents, water cycle
7. **Civilization System** → Technology, expansion, resource use
8. **Disasters** → Earthquakes, tsunamis, hurricanes, fires
9. **Magnetosphere** → Cosmic ray protection, auroras
10. **Planet Stabilizer** → Auto-corrects extreme conditions (if enabled)

### Time Scale

- **1 second real-time** ≈ **1 year in simulation** (at 1x speed)
- Use **- / +** keys to slow down or speed up
- Complex life takes **thousands of years** to evolve
- Civilizations develop over **tens of thousands of years**
- Geological changes occur over **millions of years**

---

## Simulation Systems

### Climate & Temperature

**How Temperature Works:**

Temperature is calculated from:
- **Latitude**: Poles are cold (-40°C), equator is hot (+30°C)
- **Elevation**: Mountains are colder (6.5°C drop per km)
- **Solar Energy**: Controlled by solar energy multiplier
- **Greenhouse Effect**: CO2, methane, N2O, and water vapor trap heat
- **Albedo**: Ice/snow reflects sunlight (cooling), dark surfaces absorb (warming)
- **Ocean Currents**: Warm currents heat coasts, cold currents cool them

**Climate Zones:**
- **Polar** (>70° latitude): -30°C to -10°C, very dry, ice sheets
- **Temperate** (40-70° latitude): -5°C to 20°C, moderate rain, seasonal
- **Subtropical** (25-40° latitude): 15-30°C, deserts common, dry
- **Tropical** (0-25° latitude): 20-35°C, heavy rainfall, jungles

**The Greenhouse Effect:**

Greenhouse gases trap heat:
- **CO2**: Baseline greenhouse gas (coefficient: 0.02)
- **Methane (CH4)**: 28x more potent than CO2 (coefficient: 0.006)
- **Nitrous Oxide (N2O)**: 265x more potent than CO2 (coefficient: 0.01)
- **Water Vapor**: Amplifies other gases through positive feedback

⚠️ **Warning**: Too much greenhouse gas creates runaway heating (Venus scenario)!

### Atmospheric Composition

**Oxygen Cycle:**
- **Production**: Photosynthesis by bacteria, algae, plants
- **Consumption**: Animals, fires, oxidation
- **Balance**: Earth-like = 21%, breathable = 15-30%

**Carbon Cycle:**
- **CO2 Sources**: Volcanoes, respiration, civilization industry
- **CO2 Sinks**: Photosynthesis, ocean absorption, weathering
- **Too Much CO2**: Greenhouse effect, acid rain, toxic atmosphere
- **Too Little CO2**: Plants die, planet freezes

**Methane & N2O:**
- **Methane Sources**: Wetlands, agriculture, decomposition, civilization
- **N2O Sources**: Soil bacteria, fertilizers, combustion
- **Breakdown**: Both gases slowly degrade in atmosphere
- **Impact**: Extremely potent greenhouse gases even in small amounts

### Weather Systems

**Wind Patterns:**
- **Trade Winds** (0-30° latitude): East to west at surface
- **Westerlies** (30-60° latitude): West to east at surface
- **Polar Easterlies** (60-90° latitude): East to west at surface
- **Coriolis Effect**: Deflects winds right (N. Hemisphere), left (S. Hemisphere)

**Atmospheric Circulation:**
- **Hadley Cells** (tropics): Rising air at equator, sinking at 30°
- **Ferrel Cells** (mid-latitudes): Complex circulation, weather fronts
- **Polar Cells** (poles): Cold dense air sinks, flows equatorward

**Storms & Hurricanes:**
- Form over warm oceans (>26°C)
- Fueled by water vapor and Coriolis effect
- Create devastating winds, flooding, storm surge
- Dissipate over land or cold water

### Life Evolution

**Evolution Timeline:**

1. **Bacteria** (Early): Appears in warm, wet areas
   - Produces oxygen through photosynthesis
   - Tolerates extreme conditions
   - Foundation of all life

2. **Algae** (After bacteria): Forms in oceans
   - Major oxygen producer
   - Creates ocean ecosystems
   - Needs: Water, temp >0°C, some O2

3. **Plant Life** (After algae): Colonizes land
   - Requires: O2 >10%, rain >0.2, temp 0-40°C
   - Produces massive oxygen
   - Creates forests and grasslands

4. **Simple Animals** (After plants): First land fauna
   - Needs: O2 >15%, plants for food
   - Low intelligence, basic behavior
   - Herbivores and simple predators

5. **Complex Animals** (After simple animals): Advanced fauna
   - Needs: O2 >18%, stable ecosystems
   - Tool use, social behavior
   - Predators, scavengers, specialists

6. **Intelligence** (After complex animals): Proto-civilizations
   - Needs: O2 >20%, moderate temperature
   - Language, basic tools, fire
   - Precursor to civilization

7. **Civilization** (Final stage): Advanced societies
   - Needs: Intelligent life, resources, stability
   - Technology, cities, environmental impact
   - Can reshape entire planet

**Biomass:**
- Represents population density and ecosystem health
- **0.0-0.2**: Sparse, struggling
- **0.2-0.5**: Moderate, stable
- **0.5-0.8**: Thriving, healthy
- **0.8-1.0**: Maximum carrying capacity

**Life Requirements:**

| Life Form | Min O2 | Temp Range | Other Needs |
|-----------|--------|------------|-------------|
| Bacteria | 0% | -20°C to 80°C | Moisture |
| Algae | 5% | 0°C to 40°C | Water |
| Plants | 10% | 0°C to 40°C | Rain >0.2 |
| Simple Animals | 15% | -10°C to 35°C | Plants present |
| Complex Animals | 18% | -5°C to 30°C | Ecosystem |
| Intelligence | 20% | 0°C to 25°C | Stable climate |
| Civilization | 21% | 5°C to 25°C | Resources |

### Geological Systems

**Plate Tectonics:**
- 8 tectonic plates with **realistic irregular boundaries**
- Boundaries generated using noise-distorted flood-fill for natural shapes
- **Convergent boundaries**: Mountains, volcanoes, subduction
- **Divergent boundaries**: Rift valleys, underwater ridges
- **Transform boundaries**: Earthquakes, lateral movement
- Creates realistic continental drift over millions of years

**Volcanoes:**
- Form at plate boundaries and hotspots
- **Types**:
  - **Effusive**: Gentle lava flows (basaltic)
  - **Explosive**: Violent eruptions (andesitic/rhyolitic)
  - **Phreatomagmatic**: Underwater/coastal explosions
- **Effects**: CO2 release, ash clouds, island building, fertile soil

**Erosion:**
- **Rainfall erosion**: Washes away soil, creates valleys
- **Temperature erosion**: Freeze-thaw cycles break rocks
- **Glacial erosion**: Ice sheets carve landscapes
- **Wind erosion**: Desert sandblasting
- Transports sediment to lowlands and oceans

**Sediment System:**
- Eroded material accumulates in layers
- Ocean floors collect sediment over time
- Continental shelves show sedimentary history
- View with Sediment Column Viewer (click on ocean)

### Hydrology

**River Formation:**
- Water flows from mountains to oceans
- Follows steepest descent (gradient)
- Carves valleys through erosion
- Creates deltas at river mouths
- Freezes during ice ages, reforms when ice melts

**Ocean Currents:**
- Wind-driven surface currents
- Coriolis effect creates gyres
- Warm currents (Gulf Stream) heat coasts
- Cold currents (Peru Current) cool coasts
- Affects climate and temperature distribution

**Water Cycle:**
- **Evaporation**: Oceans → atmosphere (temp dependent)
- **Condensation**: Water vapor → clouds
- **Precipitation**: Clouds → rain/snow
- **Runoff**: Rain → rivers → oceans
- Ice sheets lock up water, lowering sea level

### Civilization & Government

**Government Types:**

Civilizations evolve different government systems:

| Government | Stability | Tech Speed | Traits |
|------------|-----------|------------|--------|
| Tribal | Low | Slow | Simple, weak |
| Monarchy | Moderate | Moderate | Hereditary, single ruler |
| Dynasty | High | Moderate | Royal families, succession |
| Theocracy | High | Slow | Religious, high legitimacy |
| Republic | Moderate | Fast | Elected leaders |
| Democracy | High | Fast | Advanced, stable |
| Oligarchy | Moderate | Moderate | Elite rule |
| Dictatorship | Variable | Fast | Authoritarian |
| Federation | Very High | Very Fast | Advanced unified state |

**Rulers & Traits:**
- Each civilization has a named ruler
- **Wisdom**: Affects research speed and diplomacy
- **Charisma**: Improves stability and relations
- **Ambition**: Drives expansion and aggression
- **Brutality**: Warfare effectiveness, but harms relations
- **Piety**: Religious legitimacy, theocracy bonus

**Succession:**
- **Monarchies/Dynasties**: Hereditary (children inherit)
- **Republics/Democracies**: Elections when ruler dies
- **Dictatorships**: Power struggles, instability
- **Succession Crisis**: No heir = government collapse

**Diplomacy:**
- **Relations**: -100 (war) to +100 (close allies)
- **Treaties**: Trade pacts, defense pacts, alliances, non-aggression
- **Royal Marriages**: Dynasties intermarry for peace
- **Trust**: Breaking treaties destroys trust permanently
- **Wars**: Territorial expansion, resource competition

**Technology Progression:**

Civilizations advance through tech levels:
1. **Stone Age** (0-10): Basic tools, fire
2. **Bronze Age** (10-20): Metal working, agriculture
3. **Iron Age** (20-30): Advanced tools, writing
4. **Medieval** (30-50): Castles, trade networks
5. **Renaissance** (50-70): Science, exploration
6. **Industrial** (70-90): Factories, pollution begins
7. **Modern** (90-110): Advanced tech, high pollution
8. **Information Age** (110-130): Computers, global networks
9. **Space Age** (130-150): Satellites, planetary management
10. **Post-Scarcity** (150+): Clean energy, ecological harmony

**Environmental Impact:**

Civilizations affect the planet:
- **CO2 Emissions**: Industry adds greenhouse gases
- **Deforestation**: Cities replace forests
- **Pollution**: Reduces local biomass
- **Resource Depletion**: Exhausts deposits
- **Advanced Tech**: Can eventually restore ecosystems

### Magnetosphere & Radiation

**Planetary Core:**
- **Core Temperature**: Must be >3000K for dynamo
- **Magnetic Dynamo**: Generates protective field
- **Cooling**: Core slowly cools over billions of years

**Magnetic Field:**
- **Strength**: 0.0-2.0 (Earth = 1.0)
- **Protection**: Deflects cosmic rays and solar wind
- **Auroras**: Visible at high latitudes (polar lights)
- **Life Protection**: Shields DNA from radiation damage

**Without Magnetosphere:**
- Atmosphere slowly stripped by solar wind
- Cosmic rays kill surface life
- Oxygen degrades faster
- Planet becomes barren (Mars scenario)

### Auto-Stabilization System

**What It Does:**

When enabled (Press E), the stabilizer automatically maintains habitable conditions:

1. **Temperature Control** (Target: 15°C average)
   - Too cold (<10°C): Adds CO2 to warm planet
   - Too hot (>20°C): Removes greenhouse gases aggressively
   - Removes N2O first (most potent), then methane, then CO2
   - Emergency: Clamps extreme temperatures by latitude

2. **Atmospheric Balance**
   - Maintains O2 at 15-30% (breathable)
   - Keeps CO2 at 0.01-5% (safe for life)
   - Controls methane and N2O buildup
   - Boosts plant growth to increase O2

3. **Magnetosphere Protection**
   - Warms cooling cores
   - Reactivates magnetic dynamo if lost
   - Restores field strength to Earth-like levels

4. **Life Protection**
   - Moderates extreme temperatures where life exists
   - Boosts struggling populations
   - Ensures minimum oxygen for animals
   - Reduces toxic CO2 around civilizations

5. **Water & Land Balance**
   - Raises land if too much ocean (>90%)
   - Adds water if too much land (>90%)
   - Ensures adequate rainfall distribution

6. **Feedback Prevention**
   - Breaks runaway ice ages (snowball Earth)
   - Prevents runaway greenhouse effect (Venus)
   - Stops catastrophic ice-albedo feedback

**When to Use:**
- ✅ **For beginners**: Keeps planet habitable automatically
- ✅ **Long simulations**: Prevents drift into uninhabitability
- ✅ **Life observation**: Focus on evolution, not climate management
- ❌ **Challenges**: Disable for hard mode gameplay
- ❌ **Terraforming**: Disable when intentionally reshaping planet

**Monitoring:**
- Status shown in top-left info panel
- "Last Action" shows what stabilizer just did
- "Adjustments Made" counts total interventions
- Green text = active, Red text = disabled

---

## Playing as God

### Divine Powers (Press I)

Take direct control over civilizations:

**Government Manipulation:**
- **Change Government**: Overthrow and install new system
  - Can trigger revolutions and instability
  - Use to guide civilizations toward democracy or dictatorship
  - Strategic: Convert rivals to unstable governments

**Espionage Operations:**
- **Send Spies**: Choose mission type:
  - **Steal Technology**: Gain tech levels from advanced civs
  - **Sabotage**: Destroy resources and infrastructure
  - **Assassinate Ruler**: Kill leaders, trigger succession crisis
  - **Incite Revolution**: Reduce stability, cause unrest
  - **Steal Resources**: Transfer wealth between civilizations

**Divine Interventions:**
- **Bless Civilization**: Divine favor
  - +20% population
  - +50% resources
  - +30 stability
  - Use on favorite civilizations or to balance power

- **Curse Civilization**: Divine wrath
  - -30% population
  - -50% resources
  - -50 stability
  - Punish warmongers or overpowered civilizations

- **Advance Civilization**: Targeted boost
  - +10 technology
  - +10% population
  - +30% resources
  - +20 stability
  - Help struggling civilizations catch up

**Diplomatic Control:**
- **Force Alliance**: Make civilizations ally
  - Ignores current relations
  - Can create unlikely alliances
  - Strategic: Unite weak civs against strong one

- **Force War**: Trigger conflicts
  - Breaks existing treaties
  - Destroys trust and relations
  - Use to prevent one civ from dominating

- **Force Betrayal**: Break treaties, declare war
  - Civilizations violate agreements
  - Causes diplomatic chaos
  - Creates interesting scenarios

**Strategic Uses:**

1. **Balance of Power**
   - Weaken dominant civilizations
   - Strengthen weak ones
   - Prevent one-civ domination

2. **Technological Acceleration**
   - Steal tech from advanced civs
   - Distribute knowledge evenly
   - Advance all civilizations simultaneously

3. **Create Alliances**
   - Force rival civilizations to ally
   - Create trade networks
   - Promote peace

4. **Chaos Mode**
   - Force random wars
   - Assassinate all rulers
   - Create unstable governments
   - Watch the world burn

5. **Guided Evolution**
   - Bless civilizations that are peaceful
   - Curse warmongers
   - Advance civilizations that respect environment
   - Create utopia or dystopia

### Disaster Control (Press D)

**Natural Disasters:**
- **Earthquakes**: Trigger at tectonic boundaries
- **Tsunamis**: Coastal flooding, spawns from earthquakes
- **Volcanic Eruptions**: Lava, ash, CO2 release
- **Hurricanes**: Spawn over warm oceans
- **Forest Fires**: Burn vegetation, release CO2
- **Meteor Strikes**: Massive impact craters, climate effects

**Disaster Effects:**
- Destroy civilizations and infrastructure
- Kill life and reduce biomass
- Alter terrain and elevation
- Release greenhouse gases
- Create dramatic events

**Uses:**
- Reset overpopulated areas
- Test civilization resilience
- Create interesting terrain features
- Challenge advanced civilizations
- Simulate natural catastrophes

### Disease Control (Press G)

**Pathogen Types:**
- **Bacterial**: Moderate spread, curable
- **Viral**: Fast spread, mutations
- **Fungal**: Slow spread, persistent
- **Parasitic**: Affects specific biomes
- **Prion**: Rare, devastating
- **Hemorrhagic**: Extremely lethal

**Disease Parameters:**
- **Lethality**: 0-100% death rate
- **Infectivity**: How easily it spreads
- **Mutation Rate**: How fast it evolves
- **Incubation**: Time before symptoms

**Pandemic Mechanics:**
- Spreads between neighboring civilizations
- Trade routes accelerate transmission
- Civilizations research cures
- High tech civilizations develop cures faster
- Quarantine reduces spread

**Strategic Uses:**
- Population control
- Weaken civilizations before wars
- Test civilization healthcare systems
- Create "Plague Inc" scenarios

---

## Terraforming & Planetary Controls

### Manual Terraforming Tool (Press T)

**Paint Terrain Types:**

Click on cells to transform them:

1. **Forest**
   - Creates dense vegetation
   - Increases O2 production
   - Adds biomass
   - Best on moderate climate land

2. **Grassland**
   - Creates plains
   - Good for herbivores
   - Moderate O2 production
   - Ideal for civilization expansion

3. **Desert**
   - Removes vegetation
   - Reduces rainfall
   - Increases albedo (sand reflects light)
   - Creates arid zones

4. **Tundra**
   - Cold climate biome
   - Low vegetation
   - Frozen ground
   - Polar and high-altitude areas

5. **Ocean**
   - Lowers elevation below sea level
   - Creates seas
   - Increases humidity
   - Good for algae growth

6. **Mountain**
   - Raises elevation dramatically
   - Creates highlands
   - Affects weather (orographic effect)
   - Cools temperature (elevation effect)

7. **Tectonic Fault**
   - Creates plate boundary
   - Increases earthquake risk
   - Enables volcano formation
   - Geological activity

8. **Civilization**
   - Seeds a new civilization
   - Requires: O2 >20%, temp 5-25°C
   - Starts at Stone Age
   - Expands and evolves over time

**Tips:**
- Terraform strategically, not randomly
- Create diverse biomes for robust ecosystems
- Use mountains to create rain shadows (deserts)
- Seed oceans for algae blooms (O2 boost)
- Place civilization spawns in temperate zones

### Planetary Controls UI (Press X)

**Complete Manual Control** - SimEarth-style parameter adjustment:

**Climate Control:**
- **Solar Energy** (0.5x-1.5x)
  - Lower = colder planet, ice ages
  - Higher = hotter planet, greenhouse
  - Earth default = 1.0x

- **Temperature Offset** (-20°C to +20°C)
  - Direct temperature adjustment
  - Overrides natural calculations
  - Use for fine-tuning

**Atmosphere Control:**
- **Rainfall** (0.1x-3.0x): Wetter or drier climates
- **Wind Speed** (0.1x-3.0x): Atmospheric circulation strength
- **Oxygen** (0-50%): Set atmospheric O2 directly
- **CO2** (0-10%): Control greenhouse gas levels
- **Pressure** (500-1500 mb): Atmospheric density

**Geological Control:**
- **Tectonic Activity** (0.1x-3.0x): Plate movement speed
- **Volcanic Activity** (0.1x-3.0x): Eruption frequency
- **Erosion Rate** (0.1x-3.0x): Landscape smoothing speed

**Surface Control:**
- **Ice Coverage** (0-100%): Direct ice cap control
- **Ocean Level** (-1.0 to +1.0): Sea level adjustment
- **Albedo** (0.1-0.9): Planetary reflectivity

**Magnetosphere Control:**
- **Magnetic Field Strength** (0.0-2.0): Core dynamo power
- **Core Temperature** (1000-8000K): Core heat level

**Advanced Terraforming:**

1. **Create Water World**: Ocean level +0.5, ice 0%
2. **Create Desert World**: Rainfall 0.2x, ocean level -0.3
3. **Create Ice Age**: Temperature -15°C, solar 0.6x
4. **Create Greenhouse**: CO2 8%, temperature +10°C
5. **Restore Earth**: Use preset values, enable stabilizer

**Best Practices:**
- Make small adjustments, observe effects
- Changes apply in real-time
- Extreme values can crash ecosystems
- Auto-stabilizer fights your changes if enabled
- Save before major terraforming experiments

---

## Visualization Modes

### Terrain & Geography Modes

**1 - Terrain Mode** (Default)
- Realistic planetary view
- Shows biomes: forests, deserts, ice, oceans
- Cities visible as gray areas
- Best for general observation

**2 - Topographic Mode**
- Elevation visualization
- Dark blue = deep ocean (-1.0)
- Light blue = shallow water
- Green/yellow = lowlands
- Brown/red = highlands
- White = peaks (1.0)

**3 - Biome Mode**
- Color-coded ecosystem types:
  - Dark green = forest
  - Light green = grassland
  - Yellow = desert
  - White = tundra/ice
  - Blue = water
  - Brown = barren rock

### Climate Modes

**4 - Temperature Mode**
- Shows temperature distribution
- Blue = freezing (<-10°C)
- Cyan/Green = cold to moderate
- Yellow/Orange = warm
- Red = hot (>40°C)
- Visualize climate zones and heat distribution

**5 - Rainfall Mode**
- Precipitation levels
- Dark blue = arid/desert (<0.2)
- Light blue/Cyan = moderate (0.2-0.5)
- Green = wet (0.5-0.7)
- Yellow = very wet (>0.7)
- Shows where life can thrive

**6 - Humidity Mode**
- Atmospheric moisture
- Blue = dry air
- Green = moderate humidity
- Yellow = humid
- Red = saturated (near rain)

**7 - Ice & Glaciers Mode**
- Ice coverage visualization
- White = ice covered
- Blue = water
- Brown = land
- Shows polar ice caps and glaciers

### Atmospheric Modes

**8 - Oxygen Mode**
- O2 concentration
- Dark red = no oxygen (0%)
- Orange = low O2 (<10%)
- Yellow = moderate (10-20%)
- Green = breathable (20-30%)
- Cyan = high (>30%)

**9 - CO2 Mode**
- Carbon dioxide levels
- Blue = low CO2 (<1%)
- Cyan = moderate (1-3%)
- Green = elevated (3-5%)
- Yellow = high (>5%, toxic)

**10 - Greenhouse Effect Mode**
- Total greenhouse warming
- Blue = minimal greenhouse
- Green = moderate
- Yellow = strong
- Red = extreme (runaway)
- Shows where heating is concentrated

**11 - Air Pressure Mode**
- Atmospheric pressure
- Blue = low pressure (950-1000 mb) - storms
- Green = normal (1000-1020 mb)
- Red = high pressure (1020-1050 mb) - clear skies
- Shows weather patterns

**12 - Wind Patterns Mode**
- Wind direction and speed
- Arrows show wind direction
- Color shows speed (blue=calm, red=strong)
- See trade winds, westerlies, storms

**13 - Clouds Mode**
- Cloud coverage
- Dark = clear skies
- Light = cloudy
- White = dense clouds
- Shows weather systems and rain zones

### Life & Ecology Modes

**14 - Life Mode**
- Life form distribution
- Gray = no life
- Brown = bacteria
- Green = algae/plants
- Yellow = simple animals
- Orange = complex animals
- Red = intelligence
- Purple = civilization

**15 - Biomass Mode**
- Population density
- Black = barren (0.0)
- Dark green = sparse (0.1-0.3)
- Green = moderate (0.3-0.6)
- Light green = healthy (0.6-0.8)
- Yellow = thriving (0.8-1.0)

### Geological Modes

**16 - Tectonic Plates Mode**
- Shows all 8 plates
- Each plate has unique color
- Borders show plate boundaries
- See continental drift

**17 - Geological Activity Mode**
- Active zones
- Blue = stable
- Yellow = active (boundaries)
- Red = very active (volcanoes)
- Orange = earthquake zones

**18 - Volcano Distribution Mode**
- Volcanic hotspots
- Red dots = active volcanoes
- Orange = dormant but possible
- Shows ring of fire patterns

**19 - Erosion Mode**
- Erosion intensity
- Blue = low erosion (flat/stable)
- Green = moderate erosion
- Yellow = high erosion (mountains/rain)
- Shows landscape evolution

### Advanced Modes

**20 - Sediment Layers Mode**
- Ocean floor sediment accumulation
- Blue = thin sediment
- Green = moderate deposits
- Yellow = thick sediment layers
- Click ocean for detailed column view

**21 - Albedo Mode**
- Surface reflectivity
- Dark = low albedo (absorbs light)
- Light = high albedo (reflects light)
- Ice/snow = brightest (0.85)
- Ocean = darkest (0.06)

**22 - Radiation Mode**
- Cosmic ray protection
- Red = high radiation (no magnetosphere)
- Yellow = moderate protection
- Green = good protection
- Cyan = excellent shielding

### Geological Overlays (Toggle Independently)

Press keys to overlay data on any view mode:

- **R** - Rivers: Blue lines showing water flow
- **V** - Volcanoes: Red triangles at volcanic sites
- **Q** - Earthquakes: Yellow circles at seismic events
- **F** - Fires: Orange flames on burning areas
- **H** - Hurricanes: Spiral storm icons
- **A** - Auroras: Green/purple glows at poles (if magnetosphere active)

---

## Tips & Strategies

### For Beginners

**1. Start with Earth Preset**
- Pre-balanced for habitability
- 29% land, moderate mountains
- Good starting point to learn systems

**2. Enable Auto-Stabilizer Immediately**
- Press E at start
- Prevents runaway climate disasters
- Lets you learn without planet dying
- Check stabilizer status in top-left panel

**3. Use Preset Speeds**
- Start at 1x speed (SPACE to unpause)
- Speed up with + when comfortable
- Slow down with - to observe details
- Pause with SPACE for close examination

**4. Watch the Timeline**
- Life takes thousands of years
- Don't expect instant results
- Bacteria → civilization = ~50,000 years
- Fast-forward through boring parts

**5. Learn One System at a Time**
- Week 1: Climate and temperature
- Week 2: Atmosphere and life
- Week 3: Geology and tectonics
- Week 4: Civilizations and advanced features

### Cultivating Life

**Optimal Conditions for Life:**

1. **Start with Bacteria**
   - Needs: Warmth (>0°C), moisture
   - Appears naturally in oceans and wet land
   - Be patient, can take 1,000-5,000 years

2. **Boost Oxygen Production**
   - Let bacteria spread widely
   - Oceans produce oxygen fastest (algae)
   - Wait until O2 >10% before expecting plants

3. **Encourage Plant Growth**
   - Ensure rainfall >0.2 in temperate zones
   - Temperature 10-30°C ideal
   - Use terraforming to create forests
   - Plants accelerate O2 production

4. **Maintain Stable Climate**
   - Keep temperature 10-25°C average
   - Prevent ice ages (stabilizer helps)
   - Avoid greenhouse runaway (watch CO2)
   - Consistent conditions = faster evolution

5. **Protect from Disasters**
   - Don't spam meteor strikes
   - Let ecosystems stabilize
   - Allow recovery time after catastrophes

**If Life Won't Evolve:**

Check these conditions:
- ✅ Temperature >0°C somewhere on planet
- ✅ Liquid water present (oceans or rain)
- ✅ Not in permanent ice age
- ✅ Not too hot (>60°C everywhere)
- ✅ Some humidity/rainfall
- ✅ Patience (can take 10,000+ years)

### Developing Civilizations

**Prerequisites:**
- Oxygen >20%
- Temperature 5-25°C in habitable zones
- Stable climate for extended period
- Complex animal life present
- Resources available

**Speed Up Civilization Development:**

1. **Optimize Starting Conditions**
   - Temperate climate zones
   - Moderate rainfall
   - Diverse biomes nearby
   - Flat to rolling terrain (easier building)

2. **Manual Seeding** (Press T)
   - Place civilization in ideal location
   - Near coast (trade opportunities)
   - Resource-rich areas
   - Multiple biomes accessible

3. **Protect Early Civilizations**
   - No disasters on starting region
   - Stable climate (use stabilizer)
   - Let them build up population
   - Tech level 20+ before challenging

4. **Create Multiple Civilizations**
   - Seed 3-5 civilizations in different zones
   - Allow diplomatic interactions
   - Trade accelerates tech growth
   - Competition drives innovation

5. **Use Divine Powers Strategically**
   - Bless favored civilizations
   - Advance technology when stuck
   - Create alliances for peace
   - Force wars for testing

### Managing Climate

**Preventing Ice Ages:**

Signs of ice age:
- Ice coverage >50%
- Temperature <0°C average
- Ice-albedo feedback (ice reflects sun → colder → more ice)

Solutions:
- Increase solar energy (Press X, raise solar multiplier)
- Add CO2 (increases greenhouse effect)
- Auto-stabilizer will fight ice ages
- Wait it out (ice ages eventually end)

**Preventing Runaway Greenhouse:**

Signs of runaway greenhouse:
- Temperature >40°C average
- Poles >20°C (should be <10°C)
- CO2/methane rising continuously
- Water vapor feedback amplifying

Solutions:
- **CRITICAL**: Auto-stabilizer should prevent this (press E)
- Reduce solar energy
- Remove CO2 (stabilizer does automatically)
- Reduce volcanic activity
- Plant forests (consumes CO2)
- Kill polluting civilizations (harsh but effective)

**Balancing Act:**

Perfect planet has:
- Average temperature: 12-18°C
- O2: 18-25%
- CO2: 0.1-2%
- Rainfall: Moderate in most areas (>0.3)
- Ice: Only at poles (<20% coverage)
- Pressure: 950-1050 mb

### Geological Sculpting

**Creating Mountain Ranges:**
- Use tectonic fault tool (Press T)
- Create convergent plate boundaries
- Wait millions of years for uplift
- Mountains create rain shadows (deserts on one side)

**Building Islands:**
- Use volcano tool in oceans
- Underwater eruptions build elevation
- Eventually breaks surface
- Creates volcanic island chains

**Carving Rivers:**
- Rivers form naturally from mountains
- High erosion rate carves deeper valleys
- Rainfall determines river size
- Can't manually place rivers (emergent system)

**Shaping Continents:**
- Plate tectonics slowly moves continents
- Speed up tectonic activity for faster drift
- Transform boundaries create lateral movement
- Divergent boundaries split continents

### Advanced Strategies

**Gaia Hypothesis Test:**
- Create perfect Earth-like planet
- Enable auto-stabilizer
- Introduce massive disruptions:
  - Meteor strikes
  - Volcanic super-eruptions
  - Kill all civilizations
- Observe: Planet self-corrects back to equilibrium
- Demonstrates robustness of life systems

**Terraforming Challenge:**

Start with uninhabitable planet:
1. **Mars Mode**: Dry, cold, no atmosphere
2. **Your Mission**: Make it Earth-like
3. **Steps**:
   - Raise core temperature → magnetosphere
   - Add water (ocean level)
   - Increase solar energy and CO2
   - Seed bacteria when warm enough
   - Wait for oxygen production
   - Seed plants when O2 >10%
   - Continue until breathable

**Civilization Competition:**
- Seed 5 civilizations equidistant apart
- Give each unique government (Press I)
- No divine intervention allowed
- Observe which government type wins
- Test: Democracy vs Dictatorship vs Theocracy

**Disaster Gauntlet:**
- Develop advanced civilization (tech >100)
- Hit them with sequential disasters:
  - Earthquake → Tsunami → Hurricane
  - Volcanic eruption → Forest fires
  - Pandemic disease
- See if they survive and recover
- Tests civilization resilience

**Speedrun Categories:**

1. **Bacteria to Civilization**: Fastest time
2. **Terraforming**: Mars to habitable Earth
3. **Technology Race**: First to Space Age (tech 130)
4. **Extinction Prevention**: Survive 100,000 years with life

---

## Troubleshooting

### Common Problems

**Problem: Planet Too Hot**
- Symptoms: Temperature >40°C, poles melting
- Causes: Too much CO2/methane, high solar energy
- Solutions:
  - Enable auto-stabilizer (Press E)
  - Manually reduce CO2 (Press X)
  - Lower solar energy multiplier
  - Kill civilizations (reduce pollution)
  - Plant forests (consume CO2)

**Problem: Planet Too Cold**
- Symptoms: Temperature <0°C, ice everywhere
- Causes: Ice-albedo feedback, low greenhouse gases
- Solutions:
  - Enable auto-stabilizer
  - Increase solar energy (Press X)
  - Add CO2 (Press X)
  - Reduce ice coverage manually
  - Trigger volcanoes (release CO2)

**Problem: No Life Evolving**
- Symptoms: Thousands of years pass, still barren
- Causes: Extreme conditions, no liquid water
- Solutions:
  - Check temperature (needs >0°C somewhere)
  - Ensure water present (oceans or rain)
  - Check auto-stabilizer is enabled
  - Manually seed bacteria with terraforming tool
  - Verify O2 is being produced (check O2 view mode)

**Problem: Life Dying Off**
- Symptoms: Biomass declining, extinction events
- Causes: Climate change, disasters, oxygen depletion
- Solutions:
  - Stabilize climate (enable auto-stabilizer)
  - Stop spamming disasters
  - Check oxygen levels (needs >15% for animals)
  - Reduce temperature extremes
  - Give ecosystems recovery time

**Problem: Civilizations Not Appearing**
- Symptoms: Complex animals present, but no intelligence/civilization
- Causes: Insufficient conditions, unstable climate
- Solutions:
  - Check O2 >20%
  - Temperature must be 5-25°C in habitable zones
  - Ensure stable climate for extended period
  - Manually seed civilization (Press T)
  - Check resources available

**Problem: Runaway Disasters**
- Symptoms: Constant earthquakes, fires, storms
- Causes: Very high geological/weather activity
- Solutions:
  - Lower tectonic activity (Press X)
  - Reduce volcanic activity
  - Lower wind speed (reduces storms)
  - Stabilize temperature (prevents hurricanes)
  - Check disaster control UI (Press D) - might have triggered too many

**Problem: Magnetosphere Dying**
- Symptoms: Magnetic field weakening, auroras fading
- Causes: Core cooling below 3000K
- Solutions:
  - Enable auto-stabilizer (automatically warms core)
  - Manually raise core temperature (Press X)
  - Restore magnetic field strength slider
  - Critical for life protection!

**Problem: Atmosphere Leaking**
- Symptoms: O2 and CO2 slowly declining
- Causes: No magnetosphere, solar wind stripping
- Solutions:
  - Restore magnetosphere (see above)
  - Manually add gases (Press X)
  - Check for volcanic activity (replenishes atmosphere)

**Problem: Game Running Slow**
- Symptoms: Low FPS, stuttering
- Causes: Large map size, many simultaneous processes
- Solutions:
  - Reduce map size (start new game)
  - Lower simulation speed (- key)
  - Close other programs
  - Reduce number of civilizations
  - Turn off some overlays

### Understanding System Interactions

**Temperature ↔ Ice**
- Ice reflects sunlight → cools planet
- Cooling creates more ice (positive feedback)
- Can create snowball Earth if unchecked
- Stabilizer breaks this feedback loop

**CO2 ↔ Temperature**
- More CO2 → more greenhouse → hotter
- Hotter → more water vapor → even hotter (feedback)
- Too much CO2 → runaway greenhouse (Venus)
- Plants consume CO2, cooling planet

**Oxygen ↔ Life**
- Life produces oxygen (photosynthesis)
- Oxygen enables more complex life
- Complex life produces more oxygen
- Eventually reaches equilibrium

**Volcanoes ↔ Climate**
- Eruptions release CO2 → greenhouse warming
- Ash blocks sunlight → temporary cooling
- Long-term: warms planet
- Creates fertile soil → more life

**Civilizations ↔ Environment**
- Early civs: minimal impact
- Industrial civs: heavy pollution, CO2 release
- Advanced civs: can restore ecosystems
- Balance: tech progress vs environmental damage

---

## Keyboard Reference

### View Modes
| Key | Mode | Description |
|-----|------|-------------|
| 1 | Terrain | Realistic planetary view |
| 2 | Topographic | Elevation map |
| 3 | Biome | Ecosystem types |
| 4 | Temperature | Heat distribution |
| 5 | Rainfall | Precipitation levels |
| 6 | Humidity | Atmospheric moisture |
| 7 | Ice | Glacier coverage |
| 8 | Oxygen | O2 concentration |
| 9 | CO2 | Carbon dioxide levels |
| 0 | Greenhouse | Greenhouse effect strength |

### Additional Views
| Key | Mode | Description |
|-----|------|-------------|
| - | Pressure | Atmospheric pressure |
| = | Wind | Wind patterns and speed |
| [ | Clouds | Cloud coverage |
| ] | Life | Life form distribution |
| ; | Biomass | Population density |
| ' | Plates | Tectonic plates |
| , | Geology | Geological activity |
| . | Volcanoes | Volcanic distribution |
| / | Erosion | Erosion intensity |

### Overlays (Toggle On/Off)
| Key | Overlay | Description |
|-----|---------|-------------|
| R | Rivers | Water flow paths |
| V | Volcanoes | Volcanic sites |
| Q | Earthquakes | Seismic events |
| F | Fires | Forest fires |
| H | Hurricanes | Storm systems |
| A | Auroras | Polar lights |

### Game Controls
| Key | Action | Description |
|-----|--------|-------------|
| SPACE | Pause/Unpause | Toggle simulation |
| + | Speed Up | Increase time speed |
| - | Slow Down | Decrease time speed |
| E | Auto-Stabilizer | Toggle equilibrium system |
| N | New Game | Start new simulation |
| M | Map Options | Planet configuration |
| ESC | Menu | Pause menu |

### UI Toggles
| Key | Panel | Description |
|-----|-------|-------------|
| P | 3D Minimap | Toggle globe view |
| I | Divine Powers | God mode interface |
| D | Disasters | Disaster control |
| G | Diseases | Pandemic management |
| T | Terraforming | Manual planet editing |
| X | Planetary Controls | Parameter sliders |
| S | Sediment Viewer | Click ocean to view |
| L | Geological Events | Event log |

### Save/Load
| Key | Action | Description |
|-----|--------|-------------|
| F5 | Quick Save | Save current game |
| F9 | Quick Load | Load last save |
| F2 | Save Menu | Named save files |
| F3 | Load Menu | Browse saved games |

### Camera
| Key | Action | Description |
|-----|--------|-------------|
| Arrow Keys | Pan | Move camera |
| Mouse Drag | Pan | Move camera (Left or Middle button) |
| Scroll Wheel | Zoom | Zoom in/out (Map centers automatically when unzoomed) |
| Click | Inspect | View cell details |

---

## Conclusion

SimPlanet is a complex, interconnected simulation of planetary evolution. Every system affects every other system - there's no "right" way to play. Experiment, observe, learn from failures, and create your own unique worlds.

**Remember:**
- Patience is key - evolution takes time
- Small changes can have big effects
- The auto-stabilizer is your friend (for beginners)
- Save frequently before experiments
- There's always something new to discover

**Have fun creating worlds!** 🌍🚀

---

## Additional Resources

**In-Game Help:**
- Hover over toolbar buttons for tooltips
- Click cells to see detailed information
- Check info panel for global statistics
- Monitor stabilizer actions to learn

**Community:**
- Share interesting planet configurations
- Post screenshots of evolved civilizations
- Challenge others with terraforming scenarios
- Report bugs and request features on GitHub

**Advanced Topics:**
- Read the README.md for technical details
- Review source code for exact formulas
- Study scientific references in README
- Experiment with extreme parameters

**Version History:**
- Check "What's New in This Version" in README
- Recent critical bug fixes documented
- New features added regularly
- Save compatibility maintained between updates

---

*Last Updated: November 2025*
*SimPlanet Version: 1.0*
*Guide Version: 1.0*
