namespace SimPlanet;

/// <summary>
/// Simulates plate tectonics, volcanism, erosion, and sedimentation
/// </summary>
public class GeologicalSimulator
{
    private readonly PlanetMap _map;
    private readonly Random _random;
    private List<TectonicPlate> _plates;
    private int[,] _plateMap;
    private const int NumPlates = 8;
    private float _geologicalTime = 0;

    public List<(int x, int y, int year)> RecentEruptions { get; } = new();
    public List<(int x, int y, float magnitude)> Earthquakes { get; } = new();

    public GeologicalSimulator(PlanetMap map, int seed)
    {
        _map = map;
        _random = new Random(seed + 1000);
        _plateMap = new int[map.Width, map.Height];

        InitializePlates();
        AssignCellsToPlates();
        InitializeCrustTypes();
        InitializeVolcanicHotspots();
    }

    private void InitializeCrustTypes()
    {
        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                var cell = _map.Cells[x, y];
                var geo = cell.GetGeology();
                var plate = _plates[geo.PlateId];

                // Determine crust type based on plate type and elevation
                if (cell.IsWater && plate.IsOceanic)
                {
                    // Oceanic crust - basaltic
                    geo.CrustType = CrustType.Oceanic;
                    geo.CrustThickness = 7.0f + (float)_random.NextDouble() * 3.0f; // 7-10 km
                    geo.PrimaryRock = RockType.Basalt;
                    geo.Basalt = 0.7f + (float)_random.NextDouble() * 0.2f; // 70-90% basalt
                    geo.Granite = 0.05f;
                    geo.Limestone = 0.0f;
                    geo.CrustAge = _random.Next(0, 200); // Oceanic crust is young (<200 My)
                    geo.VolcanicRock = geo.Basalt;
                    geo.CrystallineRock = 0.2f; // Lower oceanic crust (gabbro)
                }
                else if (cell.IsLand || (!plate.IsOceanic))
                {
                    // Continental crust - granitic
                    geo.CrustType = CrustType.Continental;
                    geo.CrustThickness = 30.0f + (float)_random.NextDouble() * 15.0f; // 30-45 km
                    geo.PrimaryRock = RockType.Granite;
                    geo.Granite = 0.5f + (float)_random.NextDouble() * 0.3f; // 50-80% granite
                    geo.Basalt = 0.1f;
                    geo.Limestone = 0.1f;
                    geo.Sandstone = 0.15f;
                    geo.Shale = 0.15f;
                    geo.CrustAge = _random.Next(500, 4000); // Continental crust is old (500-4000 My)
                    geo.CrystallineRock = geo.Granite;
                    geo.SedimentaryRock = geo.Limestone + geo.Sandstone + geo.Shale;
                }
                else
                {
                    // Transitional (continental shelf, island arcs)
                    geo.CrustType = CrustType.Transitional;
                    geo.CrustThickness = 15.0f + (float)_random.NextDouble() * 10.0f; // 15-25 km
                    geo.PrimaryRock = RockType.Basalt;
                    geo.Basalt = 0.4f;
                    geo.Granite = 0.3f;
                    geo.Limestone = 0.15f;
                    geo.Sandstone = 0.1f;
                    geo.CrustAge = _random.Next(100, 1000);
                }

                // Initialize carbonate platforms in shallow tropical seas
                if (cell.IsWater && cell.Elevation > -0.3f && cell.Elevation < 0) // Shallow sea
                {
                    float latitude = Math.Abs((float)y / _map.Height - 0.5f);
                    if (latitude < 0.3f && cell.Temperature > 20) // Tropical
                    {
                        geo.IsCarbonatePlatform = true;
                        geo.Limestone = Math.Min(geo.Limestone + 0.3f, 0.8f);
                    }
                }
            }
        }
    }

    private void InitializePlates()
    {
        _plates = new List<TectonicPlate>();

        for (int i = 0; i < NumPlates; i++)
        {
            var plate = new TectonicPlate(i)
            {
                VelocityX = (float)(_random.NextDouble() - 0.5) * 0.5f,
                VelocityY = (float)(_random.NextDouble() - 0.5) * 0.5f,
                IsOceanic = _random.NextDouble() < 0.6, // 60% oceanic
                Density = _random.NextDouble() < 0.6 ? 3.0f : 2.7f // Oceanic vs Continental
            };
            _plates.Add(plate);
        }
    }

    private void AssignCellsToPlates()
    {
        // Use Voronoi regions for plate distribution
        var plateSeeds = new (int x, int y)[NumPlates];
        for (int i = 0; i < NumPlates; i++)
        {
            plateSeeds[i] = (_random.Next(_map.Width), _random.Next(_map.Height));
        }

        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                // Find nearest plate seed
                int nearestPlate = 0;
                float minDist = float.MaxValue;

                for (int i = 0; i < NumPlates; i++)
                {
                    float dx = Math.Min(Math.Abs(x - plateSeeds[i].x),
                                       _map.Width - Math.Abs(x - plateSeeds[i].x));
                    float dy = y - plateSeeds[i].y;
                    float dist = dx * dx + dy * dy;

                    if (dist < minDist)
                    {
                        minDist = dist;
                        nearestPlate = i;
                    }
                }

                _plateMap[x, y] = nearestPlate;
                _plates[nearestPlate].Cells.Add((x, y));
                _map.Cells[x, y].GetGeology().PlateId = nearestPlate;
            }
        }
    }

    private void InitializeVolcanicHotspots()
    {
        // Create volcanic hotspots (like Hawaii, Yellowstone, Galapagos, Iceland)
        // Hot spots are mantle plumes independent of plate boundaries
        int numHotspots = 4 + _random.Next(5); // 4-8 hot spots

        for (int i = 0; i < numHotspots; i++)
        {
            int x = _random.Next(_map.Width);
            int y = _random.Next(_map.Height);

            var cell = _map.Cells[x, y];
            var geo = cell.GetGeology();
            geo.IsVolcano = true;
            geo.IsHotSpot = true; // Mark as mantle plume hot spot
            geo.MagmaPressure = (float)_random.NextDouble() * 0.5f;
            geo.VolcanicActivity = 0.4f + (float)_random.NextDouble() * 0.3f; // Higher activity (0.4-0.7)

            // Hot spots can create volcanic chains (like Hawaiian islands)
            // Create 2-5 additional volcanoes in a line (plate motion over stationary hot spot)
            if (_random.NextDouble() < 0.7) // 70% chance of volcanic chain
            {
                int chainLength = 2 + _random.Next(4); // 2-5 volcanoes in chain
                int dirX = _random.Next(-1, 2); // -1, 0, or 1
                int dirY = _random.Next(-1, 2);

                if (dirX == 0 && dirY == 0) dirX = 1; // Ensure some direction

                for (int j = 1; j <= chainLength; j++)
                {
                    int cx = (x + dirX * j * 3 + _map.Width) % _map.Width;
                    int cy = y + dirY * j * 3;

                    if (cy < 0 || cy >= _map.Height) continue;

                    var chainCell = _map.Cells[cx, cy];
                    var chainGeo = chainCell.GetGeology();
                    chainGeo.IsVolcano = true;
                    chainGeo.IsHotSpot = true;
                    chainGeo.MagmaPressure = (float)_random.NextDouble() * 0.3f;
                    // Older volcanoes in chain are less active
                    chainGeo.VolcanicActivity = 0.5f / (j + 1);
                }
            }
        }
    }

    public void Update(float deltaTime, int currentYear)
    {
        _geologicalTime += deltaTime;

        // Geological processes are slow - update less frequently
        if (_geologicalTime > 1.0f) // Every second of real time
        {
            UpdatePlateTectonics(currentYear);
            UpdateVolcanicActivity(currentYear);
            UpdateErosionAndSedimentation(deltaTime);
            UpdateCarbonatePlatforms(deltaTime);

            _geologicalTime = 0;

            // Clean up old events
            RecentEruptions.RemoveAll(e => currentYear - e.year > 10);
            if (Earthquakes.Count > 20) Earthquakes.Clear();
        }
    }

    private void UpdateCarbonatePlatforms(float deltaTime)
    {
        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                var cell = _map.Cells[x, y];
                var geo = cell.GetGeology();

                // Carbonate accumulation in warm shallow seas with life
                if (geo.IsCarbonatePlatform && cell.IsWater)
                {
                    // Carbonate production from marine organisms
                    float productionRate = 0.0f;

                    if (cell.Biomass > 0.3f) // Coral reefs and shelly organisms
                    {
                        productionRate = 0.005f * cell.Biomass; // Higher with more life
                    }

                    // Temperature affects carbonate solubility
                    if (cell.Temperature > 15 && cell.Temperature < 30)
                    {
                        productionRate *= 1.5f; // Optimal temperature
                    }

                    // Accumulate carbonate
                    geo.CarbonateLayer += productionRate * deltaTime;
                    geo.Limestone += productionRate * deltaTime * 0.1f;
                    geo.SedimentaryRock += productionRate * deltaTime * 0.1f;

                    // Carbonates can build up to shallow platforms
                    if (geo.CarbonateLayer > 0.5f)
                    {
                        cell.Elevation += 0.001f * deltaTime; // Very slow uplift from accumulation
                    }

                    // Add limestone to sediment column
                    if (_random.NextDouble() < productionRate)
                    {
                        geo.SedimentColumn.Add(SedimentType.Limestone);
                    }
                }
            }
        }
    }

    private void UpdatePlateTectonics(int currentYear)
    {
        // Identify plate boundaries and calculate interactions
        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                var cell = _map.Cells[x, y];
                var geo = cell.GetGeology();
                int plateId = _plateMap[x, y];

                // Check neighbors for plate boundaries
                bool isBoundary = false;
                var neighbors = _map.GetNeighbors(x, y).ToList();

                foreach (var (nx, ny, neighbor) in neighbors)
                {
                    int neighborPlate = _plateMap[nx, ny];
                    if (neighborPlate != plateId)
                    {
                        isBoundary = true;

                        // Determine boundary type
                        var plate1 = _plates[plateId];
                        var plate2 = _plates[neighborPlate];

                        // Calculate relative velocity
                        float relVelX = plate1.VelocityX - plate2.VelocityX;
                        float relVelY = plate1.VelocityY - plate2.VelocityY;
                        float relVel = MathF.Sqrt(relVelX * relVelX + relVelY * relVelY);

                        // Determine boundary type
                        float convergence = -(relVelX * (nx - x) + relVelY * (ny - y));

                        if (convergence > 0.1f) // Converging
                        {
                            geo.BoundaryType = PlateBoundaryType.Convergent;

                            // Mountain building or subduction
                            if (plate1.IsOceanic && !plate2.IsOceanic)
                            {
                                // Oceanic subducts under continental - volcanic mountain chain
                                // Creates Andes-like or Cascades-like volcanic arcs

                                // Subduction: oceanic plate sinks
                                if (cell.IsWater)
                                {
                                    cell.Elevation -= 0.001f * relVel; // Trench formation
                                }
                                else
                                {
                                    cell.Elevation += 0.003f * relVel; // Mountains build up on continental side
                                }

                                if (_random.NextDouble() < 0.00002) // Very rare volcanic arcs (10x reduction)
                                {
                                    geo.IsVolcano = true;
                                    geo.VolcanicActivity = 0.6f;
                                    geo.MagmaPressure = 0.3f;
                                }
                                geo.TectonicStress += 0.02f;
                                geo.SubductionRate = relVel * 0.01f; // Track subduction rate
                            }
                            else if (!plate1.IsOceanic && plate2.IsOceanic)
                            {
                                // Continental over oceanic - subduction
                                if (neighbor.IsWater)
                                {
                                    neighbor.Elevation -= 0.001f * relVel; // Trench in ocean
                                }

                                cell.Elevation += 0.003f * relVel; // Mountains on continental side
                                geo.TectonicStress += 0.02f;

                                if (_random.NextDouble() < 0.00002) // Very rare volcanic arcs (10x reduction)
                                {
                                    geo.IsVolcano = true;
                                    geo.VolcanicActivity = 0.6f;
                                    geo.MagmaPressure = 0.3f;
                                }
                            }
                            else if (!plate1.IsOceanic && !plate2.IsOceanic)
                            {
                                // Continental collision - massive mountain ranges (Himalayas-like)
                                cell.Elevation += 0.005f * relVel; // Increased from 0.001f
                                geo.TectonicStress += 0.02f;

                                // Occasional volcanism from crustal melting
                                if (cell.Elevation > 0.6f && _random.NextDouble() < 0.002)
                                {
                                    geo.IsVolcano = true;
                                    geo.VolcanicActivity = 0.3f;
                                }
                            }
                            else if (plate1.IsOceanic && plate2.IsOceanic)
                            {
                                // Oceanic-oceanic convergence - island arcs (Japan, Philippines)
                                if (_random.NextDouble() < 0.00003) // Very rare island chains (10x reduction)
                                {
                                    cell.Elevation += 0.01f; // Gradual island building (reduced from 0.08f to prevent instant islands)
                                    geo.IsVolcano = true;
                                    geo.VolcanicActivity = 0.7f;
                                    geo.MagmaPressure = 0.4f;
                                }
                            }
                        }
                        else if (convergence < -0.1f) // Diverging
                        {
                            geo.BoundaryType = PlateBoundaryType.Divergent;

                            // Mid-ocean ridge volcanism (Iceland-like)
                            if (cell.IsWater && _random.NextDouble() < 0.00001) // Very rare mid-ocean ridge volcanoes (10x reduction)
                            {
                                geo.IsVolcano = true;
                                geo.VolcanicActivity = 0.4f;
                                geo.MagmaPressure = 0.2f;
                                cell.Elevation += 0.02f; // Build underwater volcanoes higher
                            }

                            // Continental rifts (East African Rift)
                            if (cell.IsLand && _random.NextDouble() < 0.00001) // Very rare rift volcanoes (10x reduction)
                            {
                                geo.IsVolcano = true;
                                geo.VolcanicActivity = 0.5f;
                                cell.Elevation -= 0.002f; // Rift valleys sink
                            }
                        }
                        else // Transform
                        {
                            geo.BoundaryType = PlateBoundaryType.Transform;
                            geo.TectonicStress += 0.02f;

                            // Earthquakes
                            if (geo.TectonicStress > 1.0f && _random.NextDouble() < 0.01)
                            {
                                Earthquakes.Add((x, y, geo.TectonicStress));
                                geo.TectonicStress = 0;
                            }
                        }
                    }
                }

                // Stress relief through earthquakes
                if (geo.TectonicStress > 1.5f && _random.NextDouble() < 0.005)
                {
                    Earthquakes.Add((x, y, geo.TectonicStress));
                    geo.TectonicStress *= 0.1f;
                }
            }
        }
    }

    private void UpdateVolcanicActivity(int currentYear)
    {
        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                var cell = _map.Cells[x, y];
                var geo = cell.GetGeology();

                if (!geo.IsVolcano) continue;

                // Build magma pressure
                geo.MagmaPressure += geo.VolcanicActivity * 0.01f;

                // Eruption threshold
                if (geo.MagmaPressure > 1.0f && _random.NextDouble() < 0.02)
                {
                    // ERUPTION!
                    VolcanicEruption(x, y, currentYear);
                    geo.MagmaPressure = 0;
                    geo.LastEruptionYear = currentYear;
                }

                // Dormant volcanoes cool down
                if (currentYear - geo.LastEruptionYear > 100)
                {
                    geo.VolcanicActivity *= 0.99f;
                }
            }
        }
    }

    private void VolcanicEruption(int x, int y, int year)
    {
        var cell = _map.Cells[x, y];
        var geo = cell.GetGeology();

        // Determine eruption type and intensity
        EruptionType eruptionType = DetermineEruptionType(x, y, geo);
        int vei = DetermineVEI(geo, eruptionType); // Volcanic Explosivity Index 0-8

        geo.LastEruptionType = eruptionType;
        geo.EruptionIntensity = vei;

        RecentEruptions.Add((x, y, year));

        // Apply eruption effects based on type
        switch (eruptionType)
        {
            case EruptionType.Effusive:
                EffusiveEruption(x, y, vei);
                break;
            case EruptionType.Strombolian:
                StrombolianEruption(x, y, vei);
                break;
            case EruptionType.Vulcanian:
                VulcanianEruption(x, y, vei);
                break;
            case EruptionType.Plinian:
                PlinianEruption(x, y, vei);
                break;
            case EruptionType.Phreatomagmatic:
                PhreatomagmaticEruption(x, y, vei);
                break;
        }
    }

    private EruptionType DetermineEruptionType(int x, int y, GeologicalData geo)
    {
        // Water nearby = phreatomagmatic
        bool hasWaterNeighbor = _map.GetNeighbors(x, y)
            .Any(n => n.cell.IsWater);

        if (hasWaterNeighbor && _random.NextDouble() < 0.3)
            return EruptionType.Phreatomagmatic;

        // High magma pressure = more explosive
        if (geo.MagmaPressure > 2.0f && _random.NextDouble() < 0.2)
            return EruptionType.Plinian;

        if (geo.MagmaPressure > 1.5f && _random.NextDouble() < 0.4)
            return EruptionType.Vulcanian;

        if (_random.NextDouble() < 0.3)
            return EruptionType.Strombolian;

        return EruptionType.Effusive; // Default
    }

    private int DetermineVEI(GeologicalData geo, EruptionType type)
    {
        // VEI scale: 0 (non-explosive) to 8 (mega-colossal)
        int baseVEI = type switch
        {
            EruptionType.Effusive => 0 + _random.Next(2),        // 0-1
            EruptionType.Strombolian => 1 + _random.Next(2),     // 1-2
            EruptionType.Vulcanian => 2 + _random.Next(2),       // 2-3
            EruptionType.Plinian => 4 + _random.Next(3),         // 4-6
            EruptionType.Phreatomagmatic => 2 + _random.Next(3), // 2-4
            _ => 1
        };

        // Volcanic activity boosts intensity
        if (geo.VolcanicActivity > 0.8f)
            baseVEI++;

        return Math.Min(baseVEI, 8);
    }

    private void EffusiveEruption(int x, int y, int vei)
    {
        // Gentle lava flows - builds shield volcano
        var cell = _map.Cells[x, y];
        var geo = cell.GetGeology();

        float elevationIncrease = 0.02f * (vei + 1);

        // Underwater volcanoes build up faster to form islands (like Hawaii)
        if (cell.IsWater)
        {
            elevationIncrease *= 2.5f; // Faster buildup underwater

            // Submarine lava cools quickly into pillow basalts
            geo.Basalt += 0.2f;
        }

        cell.Elevation += elevationIncrease;
        geo.VolcanicRock += 0.3f;
        cell.Temperature += 20 * (vei + 1);
        cell.CO2 += 1.0f * (vei + 1);

        // Lava flows to lower neighbors
        var neighbors = _map.GetNeighbors(x, y).OrderBy(n => n.cell.Elevation).Take(3);
        foreach (var (nx, ny, neighbor) in neighbors)
        {
            neighbor.GetGeology().VolcanicRock += 0.1f;
            neighbor.Biomass *= 0.7f; // Lava destroys life
        }
    }

    private void StrombolianEruption(int x, int y, int vei)
    {
        // Lava fountains and minor explosions
        var cell = _map.Cells[x, y];
        var geo = cell.GetGeology();

        float elevationIncrease = 0.04f * (vei + 1);

        // Underwater volcanoes build into islands
        if (cell.IsWater)
        {
            elevationIncrease *= 2.0f;
        }

        cell.Elevation += elevationIncrease;
        geo.VolcanicRock += 0.25f;
        cell.Temperature += 35 * (vei + 1);
        cell.CO2 += 1.5f * (vei + 1);

        // Affects nearby cells with tephra
        int radius = 1 + vei;
        for (int dx = -radius; dx <= radius; dx++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                int nx = (x + dx + _map.Width) % _map.Width;
                int ny = y + dy;
                if (ny < 0 || ny >= _map.Height) continue;

                float distance = MathF.Sqrt(dx * dx + dy * dy);
                if (distance > radius) continue;

                var neighbor = _map.Cells[nx, ny];
                neighbor.GetGeology().VolcanicRock += 0.05f * (1.0f - distance / radius);
                neighbor.Biomass *= (0.8f - distance / radius * 0.2f);
            }
        }
    }

    private void VulcanianEruption(int x, int y, int vei)
    {
        // Moderate explosive with ash clouds
        var cell = _map.Cells[x, y];
        var geo = cell.GetGeology();

        cell.Elevation += 0.03f * (vei + 1);
        geo.VolcanicRock += 0.2f;
        cell.Temperature += 50 * (vei + 1);
        cell.CO2 += 3.0f * (vei + 1);

        // Large ash cloud affects wider area
        int radius = 3 + vei;
        for (int dx = -radius; dx <= radius; dx++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                int nx = (x + dx + _map.Width) % _map.Width;
                int ny = y + dy;
                if (ny < 0 || ny >= _map.Height) continue;

                float distance = MathF.Sqrt(dx * dx + dy * dy);
                if (distance > radius) continue;

                var neighbor = _map.Cells[nx, ny];
                float effect = 1.0f - distance / radius;

                neighbor.GetGeology().VolcanicRock += 0.08f * effect;
                neighbor.Temperature += 15 * effect;
                neighbor.Biomass *= (1.0f - 0.4f * effect); // Ash kills plants

                // Volcanic ash sediment
                neighbor.GetGeology().SedimentColumn.Add(SedimentType.Volcanic);
            }
        }
    }

    private void PlinianEruption(int x, int y, int vei)
    {
        // Massive explosive eruption - regional catastrophe
        var cell = _map.Cells[x, y];
        var geo = cell.GetGeology();

        // Can create calderas (collapse)
        if (vei >= 6)
        {
            cell.Elevation -= 0.1f; // Caldera formation
        }
        else
        {
            cell.Elevation += 0.06f * (vei + 1);
        }

        geo.VolcanicRock += 0.4f;
        cell.Temperature += 80 * (vei + 1);
        cell.CO2 += 10.0f * (vei + 1); // Massive CO2 release

        // Enormous radius of devastation
        int radius = 8 + vei * 2;
        for (int dx = -radius; dx <= radius; dx++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                int nx = (x + dx + _map.Width) % _map.Width;
                int ny = y + dy;
                if (ny < 0 || ny >= _map.Height) continue;

                float distance = MathF.Sqrt(dx * dx + dy * dy);
                if (distance > radius) continue;

                var neighbor = _map.Cells[nx, ny];
                float effect = 1.0f - distance / radius;

                // Pyroclastic flows destroy everything nearby
                if (distance < 5)
                {
                    neighbor.Biomass = 0;
                    neighbor.Temperature += 200;
                }
                else
                {
                    neighbor.Biomass *= (1.0f - 0.8f * effect);
                    neighbor.Temperature += 30 * effect;
                }

                neighbor.GetGeology().VolcanicRock += 0.15f * effect;
                neighbor.CO2 += 2.0f * effect;

                // Heavy volcanic ash layers
                for (int i = 0; i < (int)(5 * effect); i++)
                {
                    neighbor.GetGeology().SedimentColumn.Add(SedimentType.Volcanic);
                }
            }
        }

        // Global climate effect for VEI 6+
        if (vei >= 6)
        {
            _map.SolarEnergy -= 0.05f; // Volcanic winter
        }
    }

    private void PhreatomagmaticEruption(int x, int y, int vei)
    {
        // Water-magma interaction - very explosive but less lava
        var cell = _map.Cells[x, y];
        var geo = cell.GetGeology();

        // Creates explosion craters
        cell.Elevation -= 0.05f * (vei + 1);
        geo.VolcanicRock += 0.15f;
        cell.Temperature += 60 * (vei + 1);
        cell.CO2 += 2.5f * (vei + 1);

        // Violent explosion with steam and ash
        int radius = 4 + vei;
        for (int dx = -radius; dx <= radius; dx++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                int nx = (x + dx + _map.Width) % _map.Width;
                int ny = y + dy;
                if (ny < 0 || ny >= _map.Height) continue;

                float distance = MathF.Sqrt(dx * dx + dy * dy);
                if (distance > radius) continue;

                var neighbor = _map.Cells[nx, ny];
                float effect = 1.0f - distance / radius;

                neighbor.GetGeology().VolcanicRock += 0.1f * effect;
                neighbor.Temperature += 25 * effect;
                neighbor.Biomass *= (1.0f - 0.6f * effect);
                neighbor.Humidity += 0.2f * effect; // Steam adds moisture

                // Fine ash and steam deposits
                if (_random.NextDouble() < effect)
                {
                    neighbor.GetGeology().SedimentColumn.Add(SedimentType.Volcanic);
                }
            }
        }
    }

    private void UpdateErosionAndSedimentation(float deltaTime)
    {
        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                var cell = _map.Cells[x, y];
                var geo = cell.GetGeology();

                if (!cell.IsLand) continue;

                // Erosion rate based on rainfall, temperature, and slope
                float slope = CalculateSlope(x, y);
                geo.ErosionRate = cell.Rainfall * 0.1f * (1.0f + slope * 2.0f);

                // Temperature affects weathering
                if (cell.Temperature > 20)
                {
                    geo.ErosionRate *= 1.5f; // Chemical weathering in warm climates
                }

                // Ice erosion
                if (cell.IsIce)
                {
                    geo.ErosionRate *= 2.0f; // Glacial erosion is powerful
                }

                // Apply erosion
                float erosion = geo.ErosionRate * deltaTime * 0.0001f;
                cell.Elevation -= erosion;
                geo.SedimentLayer += erosion;

                // Transport sediment downhill (by rivers and flooding)
                var lowestNeighbor = GetLowestNeighbor(x, y);
                if (lowestNeighbor.HasValue && geo.SedimentLayer > 0.01f)
                {
                    var (lx, ly) = lowestNeighbor.Value;
                    var targetGeo = _map.Cells[lx, ly].GetGeology();

                    // Transport rate depends on water flow (rainfall + flooding)
                    float waterCurrent = cell.Rainfall + geo.FloodLevel + geo.WaterFlow;
                    float transport = geo.SedimentLayer * 0.1f * waterCurrent;

                    // Determine sediment type based on source material and current strength
                    SedimentType sedimentType;
                    if (waterCurrent > 0.8f)
                    {
                        sedimentType = SedimentType.Gravel; // High current carries coarse material
                    }
                    else if (waterCurrent > 0.5f)
                    {
                        sedimentType = SedimentType.Sand;
                    }
                    else if (waterCurrent > 0.3f)
                    {
                        sedimentType = SedimentType.Silt;
                    }
                    else
                    {
                        sedimentType = SedimentType.Clay; // Fine particles settle in calm water
                    }

                    // Volcanic sediments
                    if (geo.VolcanicRock > 0.5f)
                    {
                        sedimentType = SedimentType.Volcanic;
                    }

                    // Organic sediments from biomass
                    if (cell.Biomass > 0.5f && waterCurrent < 0.4f)
                    {
                        sedimentType = SedimentType.Organic;
                    }

                    geo.SedimentLayer -= transport;

                    // Deposit sediment in target cell
                    if (waterCurrent < 0.5f && transport > 0.01f) // Low current = deposition
                    {
                        targetGeo.SedimentLayer += transport;
                        targetGeo.SedimentColumn.Add(sedimentType); // Add to sediment column
                        targetGeo.SedimentaryRock += transport * 0.1f;

                        // Sediment builds up elevation in lowlands
                        if (_map.Cells[lx, ly].IsWater || _map.Cells[lx, ly].Elevation < 0.1f)
                        {
                            _map.Cells[lx, ly].Elevation += transport * 0.5f;
                        }
                    }
                    else // High current = sediment keeps moving
                    {
                        targetGeo.SedimentLayer += transport;
                    }

                    // Limit sediment column size (keep only recent layers)
                    if (targetGeo.SedimentColumn.Count > 100)
                    {
                        targetGeo.SedimentColumn.RemoveAt(0);
                    }
                }
            }
        }
    }

    private float CalculateSlope(int x, int y)
    {
        var cell = _map.Cells[x, y];
        float maxDiff = 0;

        foreach (var (nx, ny, neighbor) in _map.GetNeighbors(x, y))
        {
            float diff = Math.Abs(cell.Elevation - neighbor.Elevation);
            maxDiff = Math.Max(maxDiff, diff);
        }

        return maxDiff;
    }

    private (int x, int y)? GetLowestNeighbor(int x, int y)
    {
        var cell = _map.Cells[x, y];
        float lowestElevation = cell.Elevation;
        (int x, int y)? lowest = null;

        foreach (var (nx, ny, neighbor) in _map.GetNeighbors(x, y))
        {
            if (neighbor.Elevation < lowestElevation)
            {
                lowestElevation = neighbor.Elevation;
                lowest = (nx, ny);
            }
        }

        return lowest;
    }

    public TectonicPlate GetPlate(int plateId)
    {
        return _plates[plateId];
    }

    public int GetPlateCount() => NumPlates;
}
