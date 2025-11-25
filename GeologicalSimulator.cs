using System.Collections.Concurrent;

namespace SimPlanet;

public struct EarthquakeEvent
{
    public int X;
    public int Y;
    public float Magnitude;
    public int Year;
}

/// <summary>
/// Simulates plate tectonics, volcanism, erosion, and sedimentation
/// </summary>
public class GeologicalSimulator
{
    private readonly PlanetMap _map;
    private readonly Random _random;
    private readonly Random _simulationRandom;
    private List<TectonicPlate> _plates;
    private int[,] _plateMap;
    private const int NumPlates = 8;
    private readonly PerlinNoise _boundaryNoise;

    // Control parameters for planetary controls UI
    public float TectonicActivityLevel { get; set; } = 1.0f;
    public float VolcanicActivityLevel { get; set; } = 1.0f;
    public float ErosionRate { get; set; } = 1.0f;

    private float TectonicScale => Math.Clamp(TectonicActivityLevel, 0.1f, 3.0f);
    private float VolcanicScale => Math.Clamp(VolcanicActivityLevel, 0.1f, 3.0f);
    private float ErosionScale => Math.Clamp(ErosionRate, 0.1f, 3.0f);

    public List<(int x, int y, int year)> RecentEruptions { get; } = new();
    public ConcurrentQueue<EarthquakeEvent> Earthquakes { get; } = new();

    public GeologicalSimulator(PlanetMap map, int seed)
    {
        _map = map;
        _random = new Random(seed + 1000);
        _simulationRandom = new Random(); // Non-deterministic for simulation events
        _boundaryNoise = new PerlinNoise(seed + 12345);
        _plateMap = new int[map.Width, map.Height];
        _plates = new List<TectonicPlate>(); // Initialize to avoid CS8618

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
                    // Scale rock amounts by thickness
                    geo.VolcanicRock = geo.Basalt * geo.CrustThickness;
                    geo.CrystallineRock = 0.2f * geo.CrustThickness; // Lower oceanic crust (gabbro)
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
                    // Scale CrystallineRock to represent the massive basement
                    geo.CrystallineRock = geo.Granite * geo.CrustThickness;
                    // Sedimentary rock is a thin layer on top initially
                    geo.SedimentaryRock = (geo.Limestone + geo.Sandstone + geo.Shale) * 2.0f; // Init with small layer
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
                    geo.CrystallineRock = geo.Granite * geo.CrustThickness;
                    geo.VolcanicRock = geo.Basalt * (geo.CrustThickness * 0.5f);
                }

                // Initialize carbonate platforms in shallow tropical seas
                if (cell.IsWater && cell.Elevation > -0.3f && cell.Elevation < 0) // Shallow sea
                {
                    float latitude = Math.Abs((float)y / _map.Height - 0.5f);
                    // Smooth tropical probability for carbonate formation
                    float tropicalProbability = Math.Max(0, 1.0f - (latitude / 0.35f));
                    if (tropicalProbability > 0 && cell.Temperature > 20) // Tropical zone
                    {
                        // Probability-based formation to avoid hard boundary
                        if (_random.NextDouble() < tropicalProbability)
                        {
                            geo.IsCarbonatePlatform = true;
                            geo.Limestone = Math.Min(geo.Limestone + 0.3f * tropicalProbability, 0.8f);
                        }
                    }
                }
            }
        }
    }

    private void InitializePlates()
    {
        _plates.Clear();

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
        // Use a flood-fill algorithm with noise to create irregular plate boundaries
        // This is more realistic than simple Voronoi cells
        var noise = new PerlinNoise(_random.Next());

        // Priority queue stores (x, y, plateId) ordered by accumulated cost
        // We use a custom tuple because PriorityQueue is available in .NET 6+
        var queue = new PriorityQueue<(int x, int y, int plateId), float>();
        var costs = new float[_map.Width, _map.Height];
        var assigned = new bool[_map.Width, _map.Height];

        // Initialize costs to infinity
        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                costs[x, y] = float.MaxValue;
                _plateMap[x, y] = -1; // Unassigned
            }
        }

        // Place random seeds for each plate
        for (int i = 0; i < NumPlates; i++)
        {
            int sx = _random.Next(_map.Width);
            int sy = _random.Next(_map.Height);

            // Ensure we don't pick the same seed twice (unlikely but possible)
            while (assigned[sx, sy])
            {
                sx = _random.Next(_map.Width);
                sy = _random.Next(_map.Height);
            }

            queue.Enqueue((sx, sy, i), 0);
            costs[sx, sy] = 0;
            assigned[sx, sy] = true;
            _plateMap[sx, sy] = i;
            _plates[i].Cells.Add((sx, sy));
            _map.Cells[sx, sy].GetGeology().PlateId = i;
        }

        // Flood fill
        while (queue.Count > 0)
        {
            if (!queue.TryDequeue(out var current, out float cost))
                break;

            var (x, y, plateId) = current;

            // Check 8 neighbors
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue;

                    // Calculate neighbor coordinates with wrapping
                    int nx = (x + dx + _map.Width) % _map.Width;
                    int ny = y + dy;

                    // Check vertical bounds (no wrapping for Y)
                    if (ny < 0 || ny >= _map.Height) continue;

                    // Calculate movement cost
                    // Base cost is distance (1 for cardinal, 1.414 for diagonal)
                    float moveCost = (dx == 0 || dy == 0) ? 1.0f : 1.414f;

                    // Add noise influence to create irregular shapes
                    // Noise scale needs to be relative to map size for consistent shapes
                    // We use a fixed low frequency to get large chaotic lobes
                    // IMPROVED: Use higher frequency noise mixed with low frequency to create more jagged edges
                    float noiseValLow = noise.OctaveNoise(nx * 0.04f, ny * 0.04f, 2, 0.5f, 2.0f);
                    float noiseValHigh = noise.OctaveNoise(nx * 0.15f, ny * 0.15f, 3, 0.6f, 2.0f);
                    float noiseVal = noiseValLow * 0.7f + noiseValHigh * 0.3f;

                    // The noise acts as "terrain difficulty" - high noise = harder to traverse
                    // This warps the distance field
                    // FIX: Must ensure factor is always positive to prevent negative edge weights!
                    // INCREASED NOISE IMPACT to prevent straight lines
                    float randomFactor = 3.0f + (noiseVal * 4.0f);
                    float newCost = cost + moveCost * Math.Max(0.1f, randomFactor);

                    if (newCost < costs[nx, ny])
                    {
                        costs[nx, ny] = newCost;

                        // If unassigned, assign it
                        if (!assigned[nx, ny])
                        {
                            assigned[nx, ny] = true;
                            _plateMap[nx, ny] = plateId;
                            _plates[plateId].Cells.Add((nx, ny));
                            _map.Cells[nx, ny].GetGeology().PlateId = plateId;
                            queue.Enqueue((nx, ny, plateId), newCost);
                        }
                    }
                }
            }
        }

        // Fill any gaps (though the queue approach should cover everything)
        // Just in case of unreachable islands (unlikely with 8-way connectivity)
        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                if (_plateMap[x, y] == -1)
                {
                    // Assign to nearest neighbor's plate
                    int bestPlate = 0;
                    foreach (var neighbor in _map.GetNeighbors(x, y))
                    {
                        int p = _plateMap[neighbor.x, neighbor.y];
                        if (p != -1)
                        {
                            bestPlate = p;
                            break;
                        }
                    }

                    _plateMap[x, y] = bestPlate;
                    _plates[bestPlate].Cells.Add((x, y));
                    _map.Cells[x, y].GetGeology().PlateId = bestPlate;
                }
            }
        }
    }

    private void InitializeVolcanicHotspots()
    {
        // Create volcanic hotspots (like Hawaii, Yellowstone, Galapagos, Iceland)
        // Hot spots are mantle plumes independent of plate boundaries
        // ONE volcano per hot spot - no chains to avoid too many aligned volcanoes
        int numHotspots = 4 + _random.Next(3); // 4-6 hot spots

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

            // No chains - just one volcano per hot spot
        }
    }

    // Plate movement accumulator for gradual continental drift
    private float _plateMovementAccumulator = 0.0f;
    private const float PlateMovementThreshold = 50.0f; // Years between plate cell movements

    public void Update(float deltaTime, int currentYear)
    {
        // Geological processes are slow, but must scale with simulation time.
        // The deltaTime passed in is already scaled by TimeSpeed in the main loop.
        UpdatePlateTectonics(currentYear, deltaTime);
        UpdateVolcanicActivity(currentYear, deltaTime);
        UpdateErosionAndSedimentation(deltaTime);
        UpdateCarbonatePlatforms(deltaTime);
        UpdateTurbidites(deltaTime);
        UpdateFiningUpwardSequences(deltaTime);

        // Actual plate movement - continents drift very slowly
        // Accumulate time and only move plates every ~50 years
        _plateMovementAccumulator += deltaTime;
        if (_plateMovementAccumulator >= PlateMovementThreshold)
        {
            UpdatePlateMovement();
            _plateMovementAccumulator = 0.0f;
        }

        // Normalize rock composition and clamp sediment
        UpdateRockComposition();

        // Clean up old events
        RecentEruptions.RemoveAll(e => currentYear - e.year > 10);

        // Clean up old earthquakes (limit queue size)
        while (Earthquakes.Count > 100)
        {
            Earthquakes.TryDequeue(out _);
        }
    }
    
    private void UpdateRockComposition()
    {
        // Safety check to ensure no sediment values exceed maximum
        const float MaxSediment = 10f;
        
        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                var geo = _map.Cells[x, y].GetGeology();
                
                // Clamp sediment layer
                if (geo.SedimentLayer > MaxSediment || float.IsNaN(geo.SedimentLayer) || float.IsInfinity(geo.SedimentLayer))
                {
                    geo.SedimentLayer = Math.Min(MaxSediment, Math.Max(0f, geo.SedimentLayer));
                }
                
                // Ensure no negative values for key properties
                if (geo.SedimentLayer < 0f) geo.SedimentLayer = 0f;
                if (geo.SedimentaryRock < 0f) geo.SedimentaryRock = 0f;
                if (geo.VolcanicRock < 0f) geo.VolcanicRock = 0f;
                if (geo.CrystallineRock < 0f) geo.CrystallineRock = 0f;

                // NOTE: We do NOT normalize rock composition anymore.
                // We treat CrystallineRock as the "base crust thickness" which should remain constant/dominant,
                // while SedimentaryRock and VolcanicRock accumulate on top.
                // This prevents sedimentary layers from mathematically "erasing" the bedrock via normalization.

                // Ensure CrystallineRock doesn't drop to zero (bedrock persists)
                if (geo.CrystallineRock < 0.1f && geo.CrustThickness > 1.0f)
                {
                    geo.CrystallineRock = geo.CrustThickness * 0.5f;
                }
            }
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
                    if (_simulationRandom.NextDouble() < productionRate)
                    {
                        geo.SedimentColumn.Add(SedimentType.Limestone);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Updates plate movements and boundary interactions.
    /// Scientific Basis:
    /// - Plate Tectonics: Wilson Cycle (Wilson, 1966).
    /// - Plate Motions: McKenzie, D. P., & Parker, R. L. (1967). "The North Pacific: an example of tectonics on a sphere". Nature.
    /// </summary>
    private void UpdatePlateTectonics(int currentYear, float deltaTime)
    {
        float tectonicScale = TectonicScale * deltaTime;
        float volcanicScale = VolcanicScale * deltaTime;

        // Identify plate boundaries and calculate interactions
        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                var cell = _map.Cells[x, y];
                var geo = cell.GetGeology();
                int plateId = _plateMap[x, y];

                // Check neighbors for plate boundaries
                var neighbors = _map.GetNeighbors(x, y).ToList();

                // User-created faults override plate boundaries
                if (geo.IsManualFault)
                {
                    geo.BoundaryType = PlateBoundaryType.Transform;
                    geo.TectonicStress += 0.05f * tectonicScale; // Higher stress buildup
                    geo.IsFault = true;

                    // Earthquakes on manual faults
                    if (geo.TectonicStress > 1.0f && _simulationRandom.NextDouble() < 0.05 * tectonicScale)
                    {
                        Earthquakes.Enqueue(new EarthquakeEvent { X = x, Y = y, Magnitude = geo.TectonicStress, Year = currentYear });
                        geo.TectonicStress = 0;
                    }

                    // Trigger neighbors to also be faults? No, just the line.
                }

                foreach (var (nx, ny, neighbor) in neighbors)
                {
                    int neighborPlate = _plateMap[nx, ny];
                    if (neighborPlate != plateId && !geo.IsManualFault) // Don't override manual fault
                    {
                        // Determine boundary type
                        var plate1 = _plates[plateId];
                        var plate2 = _plates[neighborPlate];

                        // Calculate relative velocity
                        float relVelX = (plate1.VelocityX - plate2.VelocityX) * tectonicScale;
                        float relVelY = (plate1.VelocityY - plate2.VelocityY) * tectonicScale;
                        float relVel = MathF.Sqrt(relVelX * relVelX + relVelY * relVelY);

                        // Determine boundary type
                        float convergence = -(relVelX * (nx - x) + relVelY * (ny - y));

                        if (convergence > 0.1f) // Converging
                        {
                            geo.BoundaryType = PlateBoundaryType.Convergent;

                            // Mountain building or subduction
                            double arcChance = 0.00002 * volcanicScale;

                            // PATCHY UPLIFT FIX: Use Perlin noise to break up the "continuous wall"
                            // This makes trenches and mountains form in segments rather than perfectly continuous lines
                            float boundaryNoise = _boundaryNoise.OctaveNoise(x * 0.1f, y * 0.1f, 2, 0.5f, 2.0f);
                            float upliftRate = 0.005f * tectonicScale * (boundaryNoise * 0.5f + 0.5f); // Modulate rate

                            // Threshold for uplift to happen at all (creates gaps)
                            bool activeSegment = boundaryNoise > -0.2f;

                            if (plate1.IsOceanic && !plate2.IsOceanic)
                            {
                                // Oceanic subducts under continental (Andes)
                                // Trench on oceanic side (this cell)

                                if (activeSegment)
                                {
                                    // Logic: I am Oceanic. Neighbor is Continental. I subduct.
                                    cell.Elevation -= upliftRate * 0.5f; // Form Trench
                                }

                                geo.TectonicStress += 0.02f * tectonicScale;
                                geo.SubductionRate = relVel * 0.01f;
                            }
                            else if (!plate1.IsOceanic && plate2.IsOceanic)
                            {
                                // Continental over Oceanic
                                // I am Continental. Neighbor is Oceanic.
                                // I get uplift (mountains) and volcanoes.

                                if (activeSegment)
                                {
                                    cell.Elevation += upliftRate; // Mountain building
                                }

                                // Volcanic arc on continental side - use noise for patchiness
                                if (!geo.IsVolcano && activeSegment && _simulationRandom.NextDouble() < arcChance)
                                {
                                    geo.IsVolcano = true;
                                    geo.VolcanicActivity = 0.6f;
                                    geo.MagmaPressure = 0.3f;
                                }

                                geo.TectonicStress += 0.02f * tectonicScale;
                            }
                            else if (!plate1.IsOceanic && !plate2.IsOceanic)
                            {
                                // Continent-Continent Collision (Himalayas)
                                if (activeSegment)
                                {
                                    // Massive uplift for both sides
                                    cell.Elevation += upliftRate * 1.5f;
                                }
                                geo.TectonicStress += 0.03f * tectonicScale;

                                // Occasional volcanism (tibetan plateau style)
                                if (!geo.IsVolcano && cell.Elevation > 0.6f && activeSegment && _simulationRandom.NextDouble() < 0.002 * volcanicScale)
                                {
                                    geo.IsVolcano = true;
                                    geo.VolcanicActivity = 0.3f;
                                }
                            }
                            else if (plate1.IsOceanic && plate2.IsOceanic)
                            {
                                // Ocean-Ocean Convergence (Island Arcs)
                                if (plate1.Density > plate2.Density)
                                {
                                    // I am denser -> I subduct -> Trench
                                    if (activeSegment)
                                        cell.Elevation -= upliftRate * 0.5f;
                                }
                                else
                                {
                                    // I am lighter -> I ride over -> Island Arc
                                    if (activeSegment)
                                        cell.Elevation += upliftRate * 0.8f;

                                    // Island arc volcanism - patchy
                                    if (!geo.IsVolcano && activeSegment && _simulationRandom.NextDouble() < 0.00005 * volcanicScale)
                                    {
                                        cell.Elevation += 0.05f; // Build island base
                                        geo.IsVolcano = true;
                                        geo.VolcanicActivity = 0.7f;
                                        geo.MagmaPressure = 0.4f;
                                    }
                                }
                            }
                        }
                        else if (convergence < -0.1f) // Diverging
                        {
                            geo.BoundaryType = PlateBoundaryType.Divergent;

                            // Divergent Boundary - Rift Valley or Mid-Ocean Ridge
                            // Consistent uplift to form ridges
                            float ridgeUplift = 0.004f * tectonicScale;

                            if (cell.IsWater)
                            {
                                // Mid-ocean ridge
                                // Build ridge, but don't necessarily breach surface immediately
                                if (cell.Elevation < -0.2f)
                                {
                                    cell.Elevation += ridgeUplift;
                                }

                                // Ridge volcanism is common
                                if (!geo.IsVolcano && _simulationRandom.NextDouble() < 0.0001 * volcanicScale)
                                {
                                    geo.IsVolcano = true;
                                    geo.VolcanicActivity = 0.4f;
                                    geo.MagmaPressure = 0.2f;
                                    cell.Elevation += 0.02f;
                                }
                            }
                            else
                            {
                                // Continental Rift (East Africa)
                                // Rift valley formation (subsidence in center, uplift on flanks - hard to simulate flanks here)
                                // We simulate the subsidence
                                cell.Elevation -= 0.002f * tectonicScale;

                                // Rift volcanism
                                if (!geo.IsVolcano && _simulationRandom.NextDouble() < 0.00005 * volcanicScale)
                                {
                                    geo.IsVolcano = true;
                                    geo.VolcanicActivity = 0.5f;
                                }
                            }
                        }
                        else // Transform
                        {
                            geo.BoundaryType = PlateBoundaryType.Transform;
                            geo.TectonicStress += 0.02f * tectonicScale;

                            // Earthquakes
                            if (geo.TectonicStress > 1.0f && _simulationRandom.NextDouble() < 0.01 * tectonicScale)
                            {
                                Earthquakes.Enqueue(new EarthquakeEvent { X = x, Y = y, Magnitude = geo.TectonicStress, Year = currentYear });
                                geo.TectonicStress = 0;
                            }
                        }
                    }
                }

                // Stress relief through earthquakes
                if (geo.TectonicStress > 1.5f && _simulationRandom.NextDouble() < 0.005 * tectonicScale)
                {
                    Earthquakes.Enqueue(new EarthquakeEvent { X = x, Y = y, Magnitude = geo.TectonicStress, Year = currentYear });
                    geo.TectonicStress *= 0.1f;
                }

                // CRITICAL: Validate and clamp TectonicStress to prevent overflow
                if (float.IsNaN(geo.TectonicStress) || float.IsInfinity(geo.TectonicStress))
                    geo.TectonicStress = 0;

                // Clamp to reasonable maximum (0-10 range, with normal values 0-2)
                geo.TectonicStress = Math.Clamp(geo.TectonicStress, 0f, 10f);

                // Cleanup inactive faults to prevent performance degradation and crashes
                // Only clean up faults that are not manually placed and not on active plate boundaries
                if (geo.IsFault && !geo.IsManualFault && geo.BoundaryType == PlateBoundaryType.None)
                {
                    // Decay activity over time
                    // Use tectonicScale to ensure it scales with simulation speed
                    // Default decay: 0.1 per second at 1x speed (takes ~10s to clear)
                    float decayRate = 0.1f * tectonicScale;

                    geo.FaultActivity -= decayRate;

                    // Also slowly decay stress on inactive faults so they don't just re-trigger immediately
                    geo.SeismicStress -= decayRate * 0.5f;

                    // If activity drops to zero, remove the fault
                    if (geo.FaultActivity <= 0.0f)
                    {
                        geo.IsFault = false;
                        geo.FaultType = FaultType.None;
                        geo.FaultActivity = 0f;
                        geo.SeismicStress = Math.Max(0f, geo.SeismicStress);
                    }
                }
            }
        }
    }

    private void UpdateVolcanicActivity(int currentYear, float deltaTime)
    {
        float volcanicScale = VolcanicScale * deltaTime;

        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                var cell = _map.Cells[x, y];
                var geo = cell.GetGeology();

                if (!geo.IsVolcano) continue;

                // Build magma pressure
                geo.MagmaPressure += geo.VolcanicActivity * 0.01f * volcanicScale;

                // Eruption threshold
                double eruptionChance = Math.Clamp(0.02 * volcanicScale, 0.0, 0.5);
                if (geo.MagmaPressure > 1.0f && _simulationRandom.NextDouble() < eruptionChance)
                {
                    // ERUPTION!
                    VolcanicEruption(x, y, currentYear);
                    geo.MagmaPressure = 0;
                    geo.LastEruptionYear = currentYear;
                }

                // Dormant volcanoes cool down
                if (currentYear - geo.LastEruptionYear > 100)
                {
                    geo.VolcanicActivity *= MathF.Max(0.9f, 1.0f - 0.01f * volcanicScale);
                }

                if (ShouldExtinguishVolcano(geo, currentYear))
                {
                    ExtinguishVolcano(geo);
                }
            }
        }

        // Spawn brand new volcanoes as plates evolve
        if (_simulationRandom.NextDouble() < 0.001 * volcanicScale)
        {
            TrySpawnNewVolcano(currentYear);
        }
    }

    private bool ShouldExtinguishVolcano(GeologicalData geo, int currentYear)
    {
        if (!geo.IsVolcano)
            return false;

        int lastActivityYear = geo.LastEruptionYear <= 0 ? 0 : geo.LastEruptionYear;

        int dormantYears = currentYear - lastActivityYear;
        if (dormantYears < 400)
            return false;

        return geo.VolcanicActivity < 0.05f && geo.MagmaPressure < 0.1f;
    }

    private void ExtinguishVolcano(GeologicalData geo)
    {
        geo.IsVolcano = false;
        geo.IsHotSpot = false;
        geo.VolcanicActivity = 0;
        geo.MagmaPressure = 0;
        geo.EruptionIntensity = 0;
        geo.LastEruptionType = EruptionType.Effusive;
    }

    private bool TrySpawnNewVolcano(int currentYear)
    {
        for (int attempt = 0; attempt < 20; attempt++)
        {
            int x = _simulationRandom.Next(_map.Width);
            int y = _simulationRandom.Next(_map.Height);
            var cell = _map.Cells[x, y];
            var geo = cell.GetGeology();

            if (geo.IsVolcano)
                continue;

            bool tectonicTrigger = geo.BoundaryType == PlateBoundaryType.Convergent ||
                                   geo.BoundaryType == PlateBoundaryType.Divergent;
            bool hasHighRelief = cell.Elevation > 0.65f;

            if (!tectonicTrigger && !geo.IsHotSpot && !hasHighRelief)
                continue;

            if (cell.IsWater && _simulationRandom.NextDouble() > 0.35)
                continue;

            geo.IsVolcano = true;
            geo.VolcanicActivity = 0.25f + (float)_simulationRandom.NextDouble() * 0.4f;
            geo.MagmaPressure = 0.1f + (float)_simulationRandom.NextDouble() * 0.2f;
            geo.LastEruptionYear = currentYear;
            geo.LastEruptionType = EruptionType.Effusive;
            geo.EruptionIntensity = 1;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Simulates a volcanic eruption.
    /// Scientific Basis:
    /// - Volcanic Explosivity Index (VEI): Newhall, C. G., & Self, S. (1982).
    ///   "The volcanic explosivity index (VEI): an estimate of explosive magnitude for historical volcanism". JGR.
    /// </summary>
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

        TryTriggerVolcanicTsunami(x, y, eruptionType, vei, year);
    }

    private EruptionType DetermineEruptionType(int x, int y, GeologicalData geo)
    {
        // Water nearby = phreatomagmatic
        bool hasWaterNeighbor = _map.GetNeighbors(x, y)
            .Any(n => n.cell.IsWater);

        if (hasWaterNeighbor && _simulationRandom.NextDouble() < 0.3)
            return EruptionType.Phreatomagmatic;

        // High magma pressure = more explosive
        if (geo.MagmaPressure > 2.0f && _simulationRandom.NextDouble() < 0.2)
            return EruptionType.Plinian;

        if (geo.MagmaPressure > 1.5f && _simulationRandom.NextDouble() < 0.4)
            return EruptionType.Vulcanian;

        if (_simulationRandom.NextDouble() < 0.3)
            return EruptionType.Strombolian;

        return EruptionType.Effusive; // Default
    }

    private int DetermineVEI(GeologicalData geo, EruptionType type)
    {
        // VEI scale: 0 (non-explosive) to 8 (mega-colossal)
        int baseVEI = type switch
        {
            EruptionType.Effusive => 0 + _simulationRandom.Next(2),        // 0-1
            EruptionType.Strombolian => 1 + _simulationRandom.Next(2),     // 1-2
            EruptionType.Vulcanian => 2 + _simulationRandom.Next(2),       // 2-3
            EruptionType.Plinian => 4 + _simulationRandom.Next(3),         // 4-6
            EruptionType.Phreatomagmatic => 2 + _simulationRandom.Next(3), // 2-4
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
        geo.VolcanicRock += 0.3f; // Direct lava adds to volcanic rock
        cell.Temperature += 20 * (vei + 1);
        cell.CO2 += 1.0f * (vei + 1);

        // Island-building: lava deltas extend nearby shallow waters into new land
        if (cell.IsLand)
        {
            foreach (var (nx, ny, neighbor) in _map.GetNeighbors(x, y))
            {
                if (neighbor.IsWater && neighbor.Elevation > -0.25f)
                {
                    neighbor.Elevation += elevationIncrease * 0.4f; // Build up shoreline
                    neighbor.GetGeology().VolcanicRock = MathF.Min(1.0f, neighbor.GetGeology().VolcanicRock + 0.05f);
                }
            }
        }

        // Lava flows to lower neighbors
        var neighbors = _map.GetNeighbors(x, y).OrderBy(n => n.cell.Elevation).Take(3);
        foreach (var (nx, ny, neighbor) in neighbors)
        {
            // BUGFIX: Lava adds to volcanic rock (direct contact), but we should be careful not to overfill
            // neighbor.GetGeology().VolcanicRock += 0.1f;
            // Instead, assume lava cools into new rock layer on top
            neighbor.GetGeology().VolcanicRock = Math.Min(1.0f, neighbor.GetGeology().VolcanicRock + 0.1f);
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
                // BUGFIX: Distant fallout is ash/tephra (sediment), not solid volcanic rock
                // Only very close neighbors get solid rock
                if (distance <= 1.5f)
                {
                    neighbor.GetGeology().VolcanicRock = Math.Min(1.0f, neighbor.GetGeology().VolcanicRock + 0.05f);
                }
                else
                {
                    neighbor.GetGeology().SedimentLayer += 0.05f * (1.0f - distance / radius);
                    neighbor.GetGeology().SedimentColumn.Add(SedimentType.Volcanic);
                }
                neighbor.Biomass *= (0.8f - distance / radius * 0.2f);
            }
        }
    }

    private void TryTriggerVolcanicTsunami(int x, int y, EruptionType eruptionType, int vei, int year)
    {
        // Only highly explosive eruptions in deep/oceanic settings should trigger tsunamis
        if (vei < 4) return; // Need a VEI 4+ event
        if (eruptionType != EruptionType.Plinian && eruptionType != EruptionType.Phreatomagmatic && eruptionType != EruptionType.Vulcanian)
            return;

        var cell = _map.Cells[x, y];

        if (!cell.IsWater)
            return; // Focus on mid-ocean seamounts

        // Must be far from coastlines to represent "middle of the ocean"
        bool nearLand = _map.GetNeighbors(x, y, radius: 4).Any(n => n.cell.IsLand);
        if (nearLand)
            return;

        // Deeper water and higher VEI increase odds, but not guaranteed
        float depthFactor = Math.Clamp(-cell.Elevation, 0.1f, 2.0f); // 0.1-2.0 for depth boost
        float probability = Math.Clamp(0.18f + 0.06f * (vei - 4) + 0.05f * depthFactor, 0.0f, 0.7f);

        if (_simulationRandom.NextDouble() > probability)
            return;

        // Convert VEI to an equivalent seismic magnitude for wave sizing
        float pseudoMagnitude = 6.0f + 0.35f * vei + 0.15f * depthFactor;
        TsunamiSystem.InitiateTsunamiFromVolcano(_map, x, y, pseudoMagnitude, year);
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

                // BUGFIX: Ash cloud deposits SEDIMENT, not solid volcanic rock
                // This prevents the "Dark Brown World" bug where ash turns the entire crust into basalt
                neighbor.GetGeology().SedimentLayer += 0.08f * effect;
                neighbor.GetGeology().SedimentaryRock += 0.01f * effect; // Some compacts immediately

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
                    // Close range gets some solid rock
                    neighbor.GetGeology().VolcanicRock = Math.Min(1.0f, neighbor.GetGeology().VolcanicRock + 0.15f * effect);
                }
                else
                {
                    neighbor.Biomass *= (1.0f - 0.8f * effect);
                    neighbor.Temperature += 30 * effect;

                    // BUGFIX: Distant fallout is purely sediment
                    neighbor.GetGeology().SedimentLayer += 0.15f * effect;
                }

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

                // BUGFIX: Ash is sediment, not solid rock
                neighbor.GetGeology().SedimentLayer += 0.1f * effect;

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
        // Validate deltaTime to prevent NaN propagation
        if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime < 0)
        {
            return;
        }

        float erosionScale = ErosionScale * 0.1f; // Reduce erosion scale by 90%
        
        // Add sediment compaction and removal mechanism
        float compactionRate = 0.02f; // Sediment compacts over time (increased from 0.01f)

        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                var cell = _map.Cells[x, y];
                var geo = cell.GetGeology();

                // Apply sediment compaction - converts sediment to rock over time
                if (geo.SedimentLayer > 2.0f)
                {
                    float compaction = (geo.SedimentLayer - 2.0f) * compactionRate * deltaTime;
                    geo.SedimentLayer -= compaction;
                    geo.SedimentaryRock += compaction * 0.5f; // 50% becomes rock
                    geo.SedimentaryRock = Math.Clamp(geo.SedimentaryRock, 0f, 5f);
                }
                
                // Ocean sediment removal at subduction zones and deep burial
                if (!cell.IsLand && cell.Elevation < -0.5f)
                {
                    // Deep ocean sediments get subducted or deeply buried
                    if (geo.BoundaryType == PlateBoundaryType.Convergent || geo.SedimentLayer > 5.0f)
                    {
                        float removalRate = 0.02f * deltaTime;
                        geo.SedimentLayer *= (1.0f - removalRate);
                    }
                }

                if (!cell.IsLand) continue;

                // Fix any existing NaN values AND clamp to reasonable ranges
                if (float.IsNaN(geo.SedimentLayer) || float.IsInfinity(geo.SedimentLayer))
                {
                    geo.SedimentLayer = 0.0f;
                }
                geo.SedimentLayer = Math.Clamp(geo.SedimentLayer, 0f, 10f); // Max 10 units of sediment

                if (float.IsNaN(geo.ErosionRate) || float.IsInfinity(geo.ErosionRate))
                {
                    geo.ErosionRate = 0.0f;
                }
                geo.ErosionRate = Math.Clamp(geo.ErosionRate, 0f, 100f); // Max erosion rate

                // Erosion rate based on rainfall, temperature, and slope
                float slope = CalculateSlope(x, y);

                // Validate inputs before calculation
                float rainfall = float.IsNaN(cell.Rainfall) ? 0.0f : Math.Max(0, cell.Rainfall);
                rainfall = Math.Clamp(rainfall, 0f, 1f); // Rainfall should be 0-1
                float temperature = float.IsNaN(cell.Temperature) ? 0.0f : cell.Temperature;
                temperature = Math.Clamp(temperature, -100f, 100f); // Physically possible range
                slope = float.IsNaN(slope) ? 0.0f : Math.Max(0, slope);
                slope = Math.Clamp(slope, 0f, 10f); // Max slope

                geo.ErosionRate = rainfall * 0.05f * (1.0f + slope * 1.5f); // Reduced from 0.1f and 2.0f
                geo.ErosionRate *= erosionScale;

                // Temperature affects weathering
                if (temperature > 20)
                {
                    geo.ErosionRate *= 1.2f; // Reduced from 1.5f
                }

                // Ice erosion
                if (cell.IsIce)
                {
                    geo.ErosionRate *= 1.5f; // Reduced from 2.0f
                }

                geo.ErosionRate = Math.Clamp(geo.ErosionRate, 0f, 10f);

                // Apply erosion - reduced rate
                float erosion = geo.ErosionRate * deltaTime * 0.00005f; // Reduced from 0.0001f

                // Validate erosion value and clamp
                if (float.IsNaN(erosion) || float.IsInfinity(erosion) || erosion < 0)
                    erosion = 0;

                erosion = Math.Clamp(erosion, 0f, 0.1f); // Max erosion per update

                if (erosion > 0)
                {
                    // Validate elevation before modification
                    if (float.IsNaN(cell.Elevation) || float.IsInfinity(cell.Elevation))
                        cell.Elevation = 0;

                    cell.Elevation -= erosion;
                    geo.SedimentLayer += erosion;
                    geo.SedimentLayer = Math.Clamp(geo.SedimentLayer, 0f, 10f); // Clamp after erosion addition

                    // Clamp elevation and sediment to reasonable ranges
                    cell.Elevation = Math.Clamp(cell.Elevation, -2f, 2f); // Keep in reasonable range
                    geo.SedimentLayer = Math.Clamp(geo.SedimentLayer, 0f, 10f);
                }

                // Transport sediment downhill (by rivers and flooding)
                var lowestNeighbor = GetLowestNeighbor(x, y);
                if (lowestNeighbor.HasValue && geo.SedimentLayer > 0.01f)
                {
                    var (lx, ly) = lowestNeighbor.Value;
                    var targetGeo = _map.Cells[lx, ly].GetGeology();

                    // Fix any NaN values in target cell AND clamp
                    if (float.IsNaN(targetGeo.SedimentLayer) || float.IsInfinity(targetGeo.SedimentLayer))
                    {
                        targetGeo.SedimentLayer = 0.0f;
                    }
                    targetGeo.SedimentLayer = Math.Clamp(targetGeo.SedimentLayer, 0f, 10f);

                    // Transport rate depends on water flow (rainfall + flooding)
                    float floodLevel = float.IsNaN(geo.FloodLevel) ? 0.0f : Math.Max(0, geo.FloodLevel);
                    floodLevel = Math.Clamp(floodLevel, 0f, 10f); // Clamp flood level

                    float waterFlow = float.IsNaN(geo.WaterFlow) ? 0.0f : Math.Max(0, geo.WaterFlow);
                    waterFlow = Math.Clamp(waterFlow, 0f, 10f); // Clamp water flow

                    float waterCurrent = (rainfall + floodLevel + waterFlow) * erosionScale;

                    // Validate waterCurrent AND CLAMP to prevent astronomical values
                    if (float.IsNaN(waterCurrent) || float.IsInfinity(waterCurrent))
                    {
                        waterCurrent = 0.0f;
                    }
                    waterCurrent = Math.Clamp(waterCurrent, 0f, 20f); // Max reasonable water current

                    float transport = geo.SedimentLayer * 0.05f * waterCurrent; // Reduced from 0.1f to 0.05f

                    // Validate transport value AND CLAMP to prevent astronomical values
                    if (float.IsNaN(transport) || float.IsInfinity(transport) || transport < 0)
                    {
                        continue; // Skip this transport if invalid
                    }
                    transport = Math.Clamp(transport, 0f, 1f); // Max sediment transport per update

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
                    if (!float.IsNaN(geo.VolcanicRock) && geo.VolcanicRock > 0.5f)
                    {
                        sedimentType = SedimentType.Volcanic;
                    }

                    // Organic sediments from biomass
                    float biomass = float.IsNaN(cell.Biomass) ? 0.0f : cell.Biomass;
                    if (biomass > 0.5f && waterCurrent < 0.4f)
                    {
                        sedimentType = SedimentType.Organic;
                    }

                    geo.SedimentLayer -= transport;

                    // Deposit sediment in target cell
                    if (waterCurrent < 0.5f && transport > 0.01f) // Low current = deposition
                    {
                        targetGeo.SedimentLayer += transport;
                        targetGeo.SedimentLayer = Math.Clamp(targetGeo.SedimentLayer, 0f, 10f); // Clamp after addition
                        targetGeo.SedimentColumn.Add(sedimentType); // Add to sediment column
                        targetGeo.SedimentaryRock += transport * 0.1f;
                        targetGeo.SedimentaryRock = Math.Clamp(targetGeo.SedimentaryRock, 0f, 5f); // Also clamp rock accumulation

                        // Sediment builds up elevation in lowlands
                        if (_map.Cells[lx, ly].IsWater || _map.Cells[lx, ly].Elevation < 0.1f)
                        {
                            float elevationIncrease = transport * 0.5f;
                            if (!float.IsNaN(elevationIncrease) && !float.IsInfinity(elevationIncrease))
                            {
                                _map.Cells[lx, ly].Elevation += elevationIncrease;
                                _map.Cells[lx, ly].Elevation = Math.Clamp(_map.Cells[lx, ly].Elevation, -2f, 2f); // Clamp elevation
                            }
                        }
                    }
                    else // High current = sediment keeps moving
                    {
                        targetGeo.SedimentLayer += transport;
                        targetGeo.SedimentLayer = Math.Clamp(targetGeo.SedimentLayer, 0f, 10f); // Clamp after addition
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

    /// <summary>
    /// Simulates turbidity currents and turbidite deposition.
    /// Scientific Basis:
    /// - Bouma Sequence: Bouma, A. H. (1962). "Sedimentology of some Flysch deposits: A graphic approach to facies interpretation".
    ///   Describes the classic fining-upward sequence (Ta-Te) in turbidites.
    /// </summary>
    private void UpdateTurbidites(float deltaTime)
    {
        // Validate deltaTime to prevent NaN propagation
        if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime < 0)
        {
            return;
        }

        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                var cell = _map.Cells[x, y];
                var geo = cell.GetGeology();

                // Turbidites occur in ocean environments, especially on slopes
                if (!cell.IsWater) continue;

                // Fix any existing NaN values
                if (float.IsNaN(geo.SedimentLayer) || float.IsInfinity(geo.SedimentLayer))
                {
                    geo.SedimentLayer = 0.0f;
                }

                // Calculate slope to determine if turbidity currents can form
                float slope = CalculateSlope(x, y);
                slope = float.IsNaN(slope) ? 0.0f : Math.Max(0, slope);

                // Turbidites are more likely on continental slopes and submarine canyons
                // They require: (1) steep slope, (2) available sediment, (3) deep water
                bool isContinentalSlope = cell.Elevation < -0.1f && cell.Elevation > -0.5f && slope > 0.05f;
                bool isDeepOcean = cell.Elevation < -0.5f;
                bool hasSediment = geo.SedimentLayer > 0.05f;

                // Turbidity currents are triggered by sediment instability on slopes
                if ((isContinentalSlope || isDeepOcean) && hasSediment && slope > 0.02f)
                {
                    // Probability of turbidity current increases with slope and sediment load
                    float turbidityProbability = slope * geo.SedimentLayer * 0.1f;

                    if (_simulationRandom.NextDouble() < turbidityProbability * deltaTime)
                    {
                        // Create a turbidite deposit with classic fining upward Bouma sequence
                        // Bouma sequence: Gravel -> Sand -> Silt -> Clay (coarse to fine)

                        float turbiditeThickness = geo.SedimentLayer * 0.3f; // Use 30% of available sediment

                        // Validate turbidite thickness
                        if (float.IsNaN(turbiditeThickness) || float.IsInfinity(turbiditeThickness))
                        {
                            continue;
                        }

                        // Find downslope neighbor for deposition
                        var lowestNeighbor = GetLowestNeighbor(x, y);
                        if (lowestNeighbor.HasValue)
                        {
                            var (lx, ly) = lowestNeighbor.Value;
                            var targetCell = _map.Cells[lx, ly];
                            var targetGeo = targetCell.GetGeology();

                            // Fix any NaN values in target cell
                            if (float.IsNaN(targetGeo.SedimentLayer) || float.IsInfinity(targetGeo.SedimentLayer))
                            {
                                targetGeo.SedimentLayer = 0.0f;
                            }

                            // Only deposit in deeper water
                            if (targetCell.IsWater)
                            {
                                // Create fining upward sequence (Bouma sequence)
                                // Ta: Gravel/coarse sand (base of flow, high energy)
                                if (turbiditeThickness > 0.15f)
                                {
                                    targetGeo.SedimentColumn.Add(SedimentType.Gravel);
                                }

                                // Tb: Medium to coarse sand (parallel lamination)
                                if (turbiditeThickness > 0.10f)
                                {
                                    targetGeo.SedimentColumn.Add(SedimentType.Sand);
                                }

                                // Tc: Fine sand to silt (ripple cross-lamination)
                                if (turbiditeThickness > 0.05f)
                                {
                                    targetGeo.SedimentColumn.Add(SedimentType.Silt);
                                }

                                // Td-Te: Clay and organic matter (suspension settling)
                                targetGeo.SedimentColumn.Add(SedimentType.Clay);

                                // Update sediment layers
                                geo.SedimentLayer -= turbiditeThickness;
                                geo.SedimentLayer = Math.Clamp(geo.SedimentLayer, 0f, 10f); // Clamp source after removal
                                targetGeo.SedimentLayer += turbiditeThickness;
                                targetGeo.SedimentLayer = Math.Clamp(targetGeo.SedimentLayer, 0f, 10f); // Clamp target after addition
                                targetGeo.SedimentaryRock += turbiditeThickness * 0.15f;
                                targetGeo.SedimentaryRock = Math.Clamp(targetGeo.SedimentaryRock, 0f, 5f); // Clamp rock accumulation

                                // Turbidites can build up abyssal plains
                                if (targetCell.Elevation < -0.3f && !float.IsNaN(turbiditeThickness))
                                {
                                    targetCell.Elevation += turbiditeThickness * 0.02f;
                                }

                                // Limit sediment column size
                                if (targetGeo.SedimentColumn.Count > 100)
                                {
                                    targetGeo.SedimentColumn.RemoveRange(0, targetGeo.SedimentColumn.Count - 100);
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    private void UpdateFiningUpwardSequences(float deltaTime)
    {
        // Validate deltaTime to prevent NaN propagation
        if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime < 0)
        {
            return;
        }

        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                var cell = _map.Cells[x, y];
                var geo = cell.GetGeology();

                // Fix any existing NaN values
                if (float.IsNaN(geo.SedimentLayer) || float.IsInfinity(geo.SedimentLayer))
                {
                    geo.SedimentLayer = 0.0f;
                }

                // Validate inputs
                float rainfall = float.IsNaN(cell.Rainfall) ? 0.0f : Math.Max(0, cell.Rainfall);
                float waterFlow = float.IsNaN(geo.WaterFlow) ? 0.0f : Math.Max(0, geo.WaterFlow);
                float elevation = float.IsNaN(cell.Elevation) ? 0.0f : cell.Elevation;

                // Fining upward sequences form in:
                // 1. River channels (point bars, channel fills)
                // 2. Delta distributary channels
                // 3. Floodplains

                bool isRiverChannel = cell.IsLand && waterFlow > 0.3f && rainfall > 0.4f;
                bool isDelta = cell.IsLand && elevation < 0.1f && elevation > -0.05f && rainfall > 0.5f;
                bool isFloodplain = cell.IsLand && elevation < 0.2f && rainfall > 0.3f && waterFlow > 0.1f;

                if ((isRiverChannel || isDelta || isFloodplain) && geo.SedimentLayer > 0.08f)
                {
                    // Rivers and deltas periodically create fining upward sequences
                    // Probability increases with water flow and sediment availability
                    float sequenceProbability = waterFlow * geo.SedimentLayer * 0.05f;

                    if (_simulationRandom.NextDouble() < sequenceProbability * deltaTime)
                    {
                        // Create a fining upward sequence
                        float sequenceThickness = geo.SedimentLayer * 0.4f; // Use 40% of available sediment

                        // Validate sequence thickness
                        if (float.IsNaN(sequenceThickness) || float.IsInfinity(sequenceThickness))
                        {
                            continue;
                        }

                        // Determine the grain sizes based on flow strength
                        if (waterFlow > 0.7f) // High energy channel
                        {
                            // Classic channel lag -> point bar sequence
                            // Base: Gravel (channel lag, erosive base)
                            geo.SedimentColumn.Add(SedimentType.Gravel);

                            // Middle: Coarse to medium sand (point bar accretion)
                            geo.SedimentColumn.Add(SedimentType.Sand);

                            // Upper: Fine sand to silt (upper point bar, low flow)
                            geo.SedimentColumn.Add(SedimentType.Silt);

                            // Top: Clay (overbank/floodplain deposits)
                            if (isFloodplain || _simulationRandom.NextDouble() < 0.5)
                            {
                                geo.SedimentColumn.Add(SedimentType.Clay);
                            }
                        }
                        else if (waterFlow > 0.4f) // Moderate energy
                        {
                            // Delta distributary or meandering river
                            // Base: Medium sand
                            geo.SedimentColumn.Add(SedimentType.Sand);

                            // Middle: Fine sand to silt
                            geo.SedimentColumn.Add(SedimentType.Silt);

                            // Top: Clay (abandoned channel fill)
                            geo.SedimentColumn.Add(SedimentType.Clay);

                            // Organic-rich if delta
                            if (isDelta)
                            {
                                geo.SedimentColumn.Add(SedimentType.Organic);
                            }
                        }
                        else // Low energy floodplain
                        {
                            // Crevasse splay or levee deposits
                            // Base: Silt (initial flood)
                            geo.SedimentColumn.Add(SedimentType.Silt);

                            // Top: Clay (waning flood, suspension)
                            geo.SedimentColumn.Add(SedimentType.Clay);

                            // Organic matter (swamp/marsh)
                            if (_simulationRandom.NextDouble() < 0.6)
                            {
                                geo.SedimentColumn.Add(SedimentType.Organic);
                            }
                        }

                        // Update sediment properties
                        geo.SedimentLayer -= sequenceThickness;
                        geo.SedimentaryRock += sequenceThickness * 0.2f;

                        // Build up elevation in low-lying areas (delta progradation)
                        if (isDelta && !float.IsNaN(sequenceThickness))
                        {
                            cell.Elevation += sequenceThickness * 0.3f;
                        }

                        // Limit sediment column size
                        if (geo.SedimentColumn.Count > 100)
                        {
                            geo.SedimentColumn.RemoveRange(0, geo.SedimentColumn.Count - 100);
                        }
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

    /// <summary>
    /// Implements actual tectonic plate movement - continents drift over time.
    /// This method gradually moves cells from one plate to another based on plate velocities.
    /// </summary>
    private void UpdatePlateMovement()
    {
        // For each plate, move a small number of boundary cells in the direction of movement
        // This simulates continental drift very slowly over geological time

        foreach (var plate in _plates)
        {
            // Skip plates with very low velocity
            float speed = MathF.Sqrt(plate.VelocityX * plate.VelocityX + plate.VelocityY * plate.VelocityY);
            if (speed < 0.01f) continue;

            // Normalize velocity to get direction
            float dirX = plate.VelocityX / speed;
            float dirY = plate.VelocityY / speed;

            // Find boundary cells that are on the leading edge of the plate
            var leadingEdgeCells = new List<(int x, int y, int targetX, int targetY)>();

            foreach (var (x, y) in plate.Cells)
            {
                // Check if this cell is on the leading edge (has neighbor in direction of movement)
                int targetX = x + (int)MathF.Round(dirX * 2);
                int targetY = y + (int)MathF.Round(dirY * 2);

                // Wrap X coordinate (cylindrical world)
                targetX = (targetX + _map.Width) % _map.Width;

                // Clamp Y coordinate (no wrapping at poles)
                if (targetY < 0 || targetY >= _map.Height)
                    continue;

                // Check if target is on a different plate
                int targetPlateId = _plateMap[targetX, targetY];
                if (targetPlateId != plate.Id)
                {
                    leadingEdgeCells.Add((x, y, targetX, targetY));
                }
            }

            // Only move a small percentage of leading edge cells per update
            // This makes the movement gradual and realistic
            int cellsToMove = Math.Max(1, leadingEdgeCells.Count / 200); // ~0.5% of boundary
            cellsToMove = (int)(cellsToMove * TectonicScale); // Scale by tectonic activity

            // Randomly select cells to move
            for (int i = 0; i < cellsToMove && leadingEdgeCells.Count > 0; i++)
            {
                int index = _random.Next(leadingEdgeCells.Count);
                var (x, y, targetX, targetY) = leadingEdgeCells[index];
                leadingEdgeCells.RemoveAt(index);

                // Move the target cell to this plate
                int oldPlateId = _plateMap[targetX, targetY];
                var oldPlate = _plates[oldPlateId];

                // Update plate membership
                oldPlate.Cells.Remove((targetX, targetY));
                plate.Cells.Add((targetX, targetY));
                _plateMap[targetX, targetY] = plate.Id;

                // Update geological data
                var targetCell = _map.Cells[targetX, targetY];
                var targetGeo = targetCell.GetGeology();
                targetGeo.PlateId = plate.Id;

                // Gradually adjust properties to match new plate
                // This simulates accretion and modification
                if (plate.IsOceanic != oldPlate.IsOceanic)
                {
                    // Plate type is changing - this is rare but can happen
                    // Don't instantly change, let natural processes handle it
                    targetGeo.BoundaryType = PlateBoundaryType.Transform;
                }
            }
        }

        // After moving cells, ensure plate boundaries are updated
        // This will be handled automatically by UpdatePlateTectonics on the next frame
    }

    public TectonicPlate GetPlate(int plateId)
    {
        return _plates[plateId];
    }

    public int GetPlateCount() => NumPlates;
}