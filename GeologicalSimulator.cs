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
        InitializeVolcanicHotspots();
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
        // Create volcanic hotspots (like Hawaii, Yellowstone)
        int numHotspots = 5 + _random.Next(5);

        for (int i = 0; i < numHotspots; i++)
        {
            int x = _random.Next(_map.Width);
            int y = _random.Next(_map.Height);

            var cell = _map.Cells[x, y];
            var geo = cell.GetGeology();
            geo.IsVolcano = true;
            geo.MagmaPressure = (float)_random.NextDouble() * 0.5f;
            geo.VolcanicActivity = 0.3f;
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

            _geologicalTime = 0;

            // Clean up old events
            RecentEruptions.RemoveAll(e => currentYear - e.year > 10);
            if (Earthquakes.Count > 20) Earthquakes.Clear();
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

                                if (_random.NextDouble() < 0.01) // Increased from 0.001
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

                                if (_random.NextDouble() < 0.01)
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
                                if (_random.NextDouble() < 0.005) // Increased from 0.0005
                                {
                                    cell.Elevation += 0.03f;
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
                            if (cell.IsWater && _random.NextDouble() < 0.003) // Increased from 0.0003
                            {
                                geo.IsVolcano = true;
                                geo.VolcanicActivity = 0.4f;
                                geo.MagmaPressure = 0.2f;
                                cell.Elevation += 0.01f; // Underwater volcanoes
                            }

                            // Continental rifts (East African Rift)
                            if (cell.IsLand && _random.NextDouble() < 0.002)
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

        RecentEruptions.Add((x, y, year));

        // Raise elevation with lava
        cell.Elevation += 0.05f;
        geo.VolcanicRock += 0.2f;

        // Heat the area
        cell.Temperature += 50;

        // Emit CO2
        cell.CO2 += 2.0f;

        // Affect surroundings
        foreach (var (nx, ny, neighbor) in _map.GetNeighbors(x, y))
        {
            neighbor.GetGeology().VolcanicRock += 0.05f;
            neighbor.CO2 += 0.5f;
            neighbor.Temperature += 10;

            // Kill nearby life
            if (_random.NextDouble() < 0.3)
            {
                neighbor.Biomass *= 0.5f;
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
