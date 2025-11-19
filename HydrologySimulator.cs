using System.Threading.Tasks;
using System.Collections.Concurrent;

namespace SimPlanet;

/// <summary>
/// Simulates rivers, water flow, and ocean currents
/// </summary>
public class HydrologySimulator
{
    private readonly PlanetMap _map;
    private readonly Random _random;
    private List<River> _rivers;
    private int _nextRiverId = 1;
    private float _tidalCycle = 0;  // 0 to 2Ï€ for tidal cycle
    private const float TidalPeriod = 12.4f; // Hours for one tidal cycle
    private float[,] _accumulatedFlowMap;

    public List<River> Rivers => _rivers;

    public HydrologySimulator(PlanetMap map, int seed)
    {
        _map = map;
        _random = new Random(seed + 2000);
        _rivers = new List<River>();
        _accumulatedFlowMap = new float[map.Width, map.Height];
    }

    public void Update(float deltaTime)
    {
        UpdateSoilMoisture();
        UpdateWaterFlow();
        UpdateAccumulatedFlow();
        UpdateRiverFreezing(); // Check for frozen rivers
        FormRivers(deltaTime);
        UpdateSalinity(deltaTime);
        UpdateWaterDensity();
        UpdateOceanCurrents();
        UpdateThermohalineCirculation(deltaTime);
        UpdateTides(deltaTime);
        UpdateFlooding(deltaTime);
    }

    private void UpdateSoilMoisture()
    {
        var newMoisture = new float[_map.Width, _map.Height];
        var newHumidity = new float[_map.Width, _map.Height];

        Parallel.For(0, _map.Width, x =>
        {
            for (int y = 0; y < _map.Height; y++)
            {
                var cell = _map.Cells[x, y];
                var geo = cell.GetGeology();

                if (cell.IsWater)
                {
                    newMoisture[x, y] = 1.0f;
                    newHumidity[x, y] = cell.Humidity;
                    continue;
                }

                float moisture = geo.SoilMoisture;
                float humidity = cell.Humidity;

                // Moisture from rainfall
                moisture += cell.Rainfall * 0.1f;

                // Evaporation
                float evaporation = 0.05f;
                if (cell.Temperature > 20)
                {
                    evaporation += (cell.Temperature - 20) * 0.01f;
                }
                float evaporatedWater = Math.Min(moisture, evaporation);
                moisture -= evaporatedWater;
                humidity += evaporatedWater * 0.5f; // Add evaporated water back to atmosphere

                // Plant water uptake (transpiration)
                if (cell.Biomass > 0.2f)
                {
                    float transpiredWater = Math.Min(moisture, cell.Biomass * 0.05f);
                    moisture -= transpiredWater;
                    humidity += transpiredWater * 0.5f; // Add transpired water back to atmosphere
                }

                newMoisture[x, y] = Math.Clamp(moisture, 0, 1);
                newHumidity[x, y] = Math.Clamp(humidity, 0, 1);
            }
        });

        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                _map.Cells[x, y].GetGeology().SoilMoisture = newMoisture[x, y];
                _map.Cells[x, y].Humidity = newHumidity[x, y];
            }
        }
    }

    private void UpdateAccumulatedFlow()
    {
        var cellsByElevation = new List<TerrainCell>();
        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                cellsByElevation.Add(_map.Cells[x, y]);
            }
        }
        cellsByElevation.Sort((a, b) => b.Elevation.CompareTo(a.Elevation));

        Array.Clear(_accumulatedFlowMap, 0, _accumulatedFlowMap.Length);

        foreach (var cell in cellsByElevation)
        {
            var geo = cell.GetGeology();
            if (!cell.IsLand || geo.WaterFlow <= 0) continue;

            var (flowX, flowY) = geo.FlowDirection;
            if (flowX == 0 && flowY == 0) continue;

            int nextX = (cell.X + flowX + _map.Width) % _map.Width;
            int nextY = Math.Clamp(cell.Y + flowY, 0, _map.Height - 1);

            _accumulatedFlowMap[nextX, nextY] += geo.WaterFlow + _accumulatedFlowMap[cell.X, cell.Y];
        }
    }

    private void UpdateWaterFlow()
    {
        var newFlow = new float[_map.Width, _map.Height];
        var newFlowDir = new (int, int)[_map.Width, _map.Height];

        // Calculate water flow direction for each cell
        Parallel.For(0, _map.Width, x =>
        {
            for (int y = 0; y < _map.Height; y++)
            {
                var cell = _map.Cells[x, y];
                var geo = cell.GetGeology();

                if (!cell.IsLand) continue;

                // Find steepest downhill direction
                float steepestGradient = 0;
                (int, int) flowDir = (0, 0);

                foreach (var (nx, ny, neighbor) in _map.GetNeighbors(x, y))
                {
                    float gradient = cell.Elevation - neighbor.Elevation;
                    if (gradient > steepestGradient)
                    {
                        steepestGradient = gradient;
                        flowDir = (nx - x, ny - y);
                    }
                }

                newFlowDir[x, y] = flowDir;

                // Calculate water flow based on rainfall and soil moisture
                // Frozen cells have no water flow
                if (cell.IsIce || cell.Temperature < 0)
                {
                    newFlow[x, y] = 0;
                }
                else
                {
                    // Validate all inputs to prevent NaN/Infinity propagation
                    float rainfall = cell.Rainfall;
                    if (float.IsNaN(rainfall) || float.IsInfinity(rainfall))
                        rainfall = 0;
                    rainfall = Math.Clamp(rainfall, 0f, 1f);

                    float soilMoisture = geo.SoilMoisture;
                    if (float.IsNaN(soilMoisture) || float.IsInfinity(soilMoisture))
                        soilMoisture = 0;
                    soilMoisture = Math.Clamp(soilMoisture, 0f, 1f);

                    if (float.IsNaN(steepestGradient) || float.IsInfinity(steepestGradient))
                        steepestGradient = 0;
                    steepestGradient = Math.Clamp(steepestGradient, 0f, 10f);

                    float waterFlow = (rainfall + soilMoisture) * steepestGradient;

                    // Validate result and clamp to reasonable range
                    if (float.IsNaN(waterFlow) || float.IsInfinity(waterFlow))
                        waterFlow = 0;
                    newFlow[x, y] = Math.Clamp(waterFlow, 0f, 10f);
                }
            }
        });

        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                var geo = _map.Cells[x, y].GetGeology();
                geo.WaterFlow = newFlow[x, y];
                geo.FlowDirection = newFlowDir[x, y];
            }
        }
    }

    private void UpdateRiverFreezing()
    {
        // Check existing rivers for freezing conditions
        var frozenRivers = new List<River>();

        foreach (var river in _rivers)
        {
            bool isFrozen = false;
            int frozenSegments = 0;

            // Check if significant portion of river is frozen
            foreach (var (x, y) in river.Path)
            {
                var cell = _map.Cells[x, y];
                if (cell.IsIce || cell.Temperature < 0)
                {
                    frozenSegments++;
                }
            }

            // If more than 50% of river is frozen, consider it frozen
            if (river.Path.Count > 0 && frozenSegments > river.Path.Count / 2)
            {
                isFrozen = true;
            }

            if (isFrozen)
            {
                // Clear river data from cells
                foreach (var (x, y) in river.Path)
                {
                    var cell = _map.Cells[x, y];
                    var geo = cell.GetGeology();
                    geo.RiverId = 0;
                    geo.IsRiverSource = false;
                }
                frozenRivers.Add(river);
            }
        }

        // Remove frozen rivers
        foreach (var frozenRiver in frozenRivers)
        {
            _rivers.Remove(frozenRiver);
        }
    }

    private void FormRivers(float deltaTime)
    {
        // Form rivers in areas with high water accumulation
        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                var cell = _map.Cells[x, y];
                var geo = cell.GetGeology();

                if (!cell.IsLand || geo.RiverId > 0) continue;

                // Don't form rivers in frozen areas
                if (cell.IsIce || cell.Temperature < 0) continue;

                // Check if this could be a river source
                if (cell.Elevation > 0.3f && cell.Rainfall > 0.5f && geo.WaterFlow > 0.1f)
                {
                    // Use the cached accumulated flow map for high performance
                    float accumulatedFlow = _accumulatedFlowMap[x, y];

                    if (accumulatedFlow > 0.5f && _random.NextDouble() < 0.001)
                    {
                        CreateRiver(x, y);
                    }
                }
            }
        }
    }

    private void CreateRiver(int sourceX, int sourceY)
    {
        var river = new River
        {
            Id = _nextRiverId++,
            SourceX = sourceX,
            SourceY = sourceY,
            Path = new List<(int x, int y)>()
        };

        var cell = _map.Cells[sourceX, sourceY];
        var geo = cell.GetGeology();
        geo.IsRiverSource = true;
        geo.RiverId = river.Id;

        // Trace river path downhill to ocean
        int x = sourceX;
        int y = sourceY;
        var visited = new HashSet<(int, int)>();

        while (visited.Count < 200) // Max river length
        {
            if (visited.Contains((x, y))) break;
            visited.Add((x, y));

            var currentCell = _map.Cells[x, y];
            var currentGeo = currentCell.GetGeology();

            // Stop if river encounters ice
            if (currentCell.IsIce || currentCell.Temperature < 0)
            {
                break;
            }

            river.Path.Add((x, y));
            currentGeo.RiverId = river.Id;

            // River reaches ocean
            if (currentCell.IsWater)
            {
                river.MouthX = x;
                river.MouthY = y;
                break;
            }

            // Follow flow direction
            var (fx, fy) = currentGeo.FlowDirection;
            if (fx == 0 && fy == 0) break; // No flow

            x = (x + fx + _map.Width) % _map.Width;
            y = Math.Clamp(y + fy, 0, _map.Height - 1);

            // River carves valley
            currentCell.Elevation -= 0.001f;
        }

        if (river.Path.Count > 5) // Minimum river length
        {
            _rivers.Add(river);
        }
    }

    private void UpdateSalinity(float deltaTime)
    {
        // Salinity affected by evaporation, precipitation, river input, and ice formation
        Parallel.For(0, _map.Width, x =>
        {
            for (int y = 0; y < _map.Height; y++)
            {
                var cell = _map.Cells[x, y];
                if (!cell.IsWater) continue;

                var geo = cell.GetGeology();
                float salinityChange = 0;

                // Evaporation increases salinity (water leaves, salt stays)
                if (cell.Temperature > 15)
                {
                    float evaporationRate = (cell.Temperature - 15) * 0.001f;
                    salinityChange += evaporationRate * deltaTime;
                }

                // Precipitation decreases salinity (dilution)
                salinityChange -= cell.Rainfall * 0.5f * deltaTime;

                // River input decreases salinity (freshwater input)
                if (geo.RiverId > 0)
                {
                    var river = _rivers.FirstOrDefault(r => r.Id == geo.RiverId);
                    if (river != null && river.MouthX == x && river.MouthY == y)
                    {
                        salinityChange -= 2.0f * deltaTime; // Strong freshwater input at river mouths
                    }
                }

                // Ice formation concentrates salt in remaining water (brine rejection)
                if (cell.Temperature < -1.8f) // Seawater freezing point
                {
                    salinityChange += 0.3f * deltaTime;
                }

                // Ice melting dilutes salinity
                if (cell.IsIce && cell.Temperature > 0)
                {
                    salinityChange -= 0.2f * deltaTime;
                }

                geo.Salinity = Math.Clamp(geo.Salinity + salinityChange, 0, 42); // 0-42 ppt range
            }
        });

        // Salinity mixing through diffusion
        var newSalinity = new float[_map.Width][];
        for (int i = 0; i < _map.Width; i++)
        {
            newSalinity[i] = new float[_map.Height];
        }
        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                var cell = _map.Cells[x, y];
                if (!cell.IsWater)
                {
                    newSalinity[x][y] = cell.GetGeology().Salinity;
                    continue;
                }

                float neighborSalinity = 0;
                int waterNeighbors = 0;

                foreach (var (nx, ny, neighbor) in _map.GetNeighbors(x, y))
                {
                    if (neighbor.IsWater)
                    {
                        neighborSalinity += neighbor.GetGeology().Salinity;
                        waterNeighbors++;
                    }
                }

                if (waterNeighbors > 0)
                {
                    neighborSalinity /= waterNeighbors;
                    float currentSalinity = cell.GetGeology().Salinity;
                    newSalinity[x][y] = currentSalinity + (neighborSalinity - currentSalinity) * 0.1f * deltaTime;
                }
                else
                {
                    newSalinity[x][y] = cell.GetGeology().Salinity;
                }
            }
        }

        // Apply new salinity
        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                if (_map.Cells[x, y].IsWater)
                {
                    _map.Cells[x, y].GetGeology().Salinity = newSalinity[x][y];
                }
            }
        }
    }

    private void UpdateWaterDensity()
    {
        // Water density calculation based on temperature and salinity
        // Density = Ï(T, S) where T = temperature, S = salinity
        // Approximate equation of state for seawater
        // Source: UNESCO equation of state (simplified)

        Parallel.For(0, _map.Width, x =>
        {
            for (int y = 0; y < _map.Height; y++)
            {
                var cell = _map.Cells[x, y];
                if (!cell.IsWater) continue;

                var geo = cell.GetGeology();

                // Base density at 0°C, 0 ppt = 999.8 kg/m³ = 0.9998 g/cm³
                float baseDensity = 0.9998f;

                // Temperature effect: density decreases with temperature
                // ΔρT ≈ -0.0002 g/cm³ per °C (simplified)
                float tempEffect = -0.0002f * cell.Temperature;

                // Salinity effect: density increases with salinity
                // Δρs ≈ 0.0008 g/cm³ per ppt (simplified)
                float salinityEffect = 0.0008f * (geo.Salinity - 35);

                // Total density
                geo.WaterDensity = baseDensity + tempEffect + salinityEffect;
                geo.WaterDensity = Math.Clamp(geo.WaterDensity, 0.95f, 1.05f);
            }
        });
    }

    private void UpdateOceanCurrents()
    {
        var tempChanges = new ConcurrentDictionary<(int, int), float>();
        var saltChanges = new ConcurrentDictionary<(int, int), float>();

        // Wind-driven surface ocean currents
        // Based on atmospheric circulation and Coriolis effect
        Parallel.For(0, _map.Width, x =>
        {
            for (int y = 0; y < _map.Height; y++)
            {
                var cell = _map.Cells[x, y];
                if (!cell.IsWater) continue;

                var geo = cell.GetGeology();

                // Only affect surface waters (shallow depths)
                if (cell.Elevation > -0.5f)
                {
                    // Latitude-based currents (Coriolis effect)
                    float latitude = Math.Abs((y - _map.Height / 2.0f) / (_map.Height / 2.0f));

                    // Smooth ocean current transitions based on latitude
                    // Trade winds influence (0-35°)
                    float tradeWindInfluence = Math.Max(0, 1.0f - (latitude / 0.35f));
                    // Westerlies influence (25-65°)
                    float westerliesInfluence = 0;
                    if (latitude >= 0.25f && latitude <= 0.65f)
                    {
                        if (latitude < 0.45f)
                            westerliesInfluence = (latitude - 0.25f) / 0.2f;
                        else
                            westerliesInfluence = 1.0f - ((latitude - 0.45f) / 0.2f);
                    }
                    // Polar influence (55°+)
                    float polarInfluence = Math.Max(0, (latitude - 0.55f) / 0.45f);
                    
                    // Blend current directions smoothly
                    float flowX = -1.0f * tradeWindInfluence + 1.0f * westerliesInfluence + 0.5f * polarInfluence;
                    float flowY = 0;
                    
                    // Normalize and set flow direction
                    float flowMag = Math.Max(0.1f, Math.Abs(flowX));
                    geo.FlowDirection = ((int x, int y))(flowX / flowMag, flowY);

                    // Western intensification (stronger currents on western boundaries)
                    // Check if near a continental boundary
                    bool hasLandToWest = false;
                    for (int dx = -2; dx <= 0; dx++)
                    {
                        int checkX = (x + dx + _map.Width) % _map.Width;
                        if (_map.Cells[checkX, y].IsLand)
                        {
                            hasLandToWest = true;
                            break;
                        }
                    }

                    float currentStrength = hasLandToWest ? 0.25f : 0.1f;

                    // Heat transport by surface currents
                    var (fx, fy) = geo.FlowDirection;
                    int targetX = (x + fx + _map.Width) % _map.Width;
                    int targetY = Math.Clamp(y + fy, 0, _map.Height - 1);

                    var targetCell = _map.Cells[targetX, targetY];
                    if (targetCell.IsWater)
                    {
                        // Heat and salt transport
                        float tempDiff = cell.Temperature - targetCell.Temperature;
                        tempChanges.AddOrUpdate((x, y), -tempDiff * currentStrength * 0.05f, (key, old) => old - tempDiff * currentStrength * 0.05f);
                        tempChanges.AddOrUpdate((targetX, targetY), tempDiff * currentStrength * 0.05f, (key, old) => old + tempDiff * currentStrength * 0.05f);

                        float saltDiff = geo.Salinity - targetCell.GetGeology().Salinity;
                        saltChanges.AddOrUpdate((x, y), -saltDiff * currentStrength * 0.03f, (key, old) => old - saltDiff * currentStrength * 0.03f);
                        saltChanges.AddOrUpdate((targetX, targetY), saltDiff * currentStrength * 0.03f, (key, old) => old + saltDiff * currentStrength * 0.03f);
                    }
                }
            }
        });

        foreach (var change in tempChanges)
        {
            _map.Cells[change.Key.Item1, change.Key.Item2].Temperature += change.Value;
        }
        foreach (var change in saltChanges)
        {
            _map.Cells[change.Key.Item1, change.Key.Item2].GetGeology().Salinity += change.Value;
        }
    }

    private void UpdateThermohalineCirculation(float deltaTime)
    {
        var tempChanges = new ConcurrentDictionary<(int, int), float>();
        var saltChanges = new ConcurrentDictionary<(int, int), float>();

        // Thermohaline circulation: density-driven deep ocean currents
        // Dense water sinks at high latitudes (North Atlantic, Antarctica)
        // Forms global "conveyor belt" circulation
        // Source: Broecker (1991), The Great Ocean Conveyor
        Parallel.For(0, _map.Width, x =>
        {
            for (int y = 0; y < _map.Height; y++)
            {
                var cell = _map.Cells[x, y];
                if (!cell.IsWater) continue;

                var geo = cell.GetGeology();
                float latitude = Math.Abs((y - _map.Height / 2.0f) / (_map.Height / 2.0f));

                // Deep water formation at high latitudes (polar regions)
                // Use smooth probability for formation
                float deepWaterProbability = Math.Max(0, (latitude - 0.65f) / 0.25f);
                if (deepWaterProbability > 0 && cell.Elevation < -0.3f && _random.NextDouble() < deepWaterProbability)
                {
                    // Cold, salty water is dense and sinks
                    if (cell.Temperature < 5 && geo.Salinity > 34)
                    {
                        // Downwelling region - deep water formation
                        // Find neighbors at lower depths to transport heat and salt
                        foreach (var (nx, ny, neighbor) in _map.GetNeighbors(x, y))
                        {
                            if (neighbor.IsWater && neighbor.Elevation < cell.Elevation)
                            {
                                var nGeo = neighbor.GetGeology();

                                // Transport cold, salty water downward
                                float densityDiff = geo.WaterDensity - nGeo.WaterDensity;

                                if (densityDiff > 0) // Current cell is denser
                                {
                                    float sinkingRate = densityDiff * 0.5f * deltaTime;

                                    // Cool the deeper water
                                    tempChanges.AddOrUpdate((nx, ny), -sinkingRate * 2.0f, (key, old) => old - sinkingRate * 2.0f);

                                    // Increase salinity in deeper water
                                    saltChanges.AddOrUpdate((nx, ny), sinkingRate * 0.5f, (key, old) => old + sinkingRate * 0.5f);
                                }
                            }
                        }
                    }
                }

                // Deep ocean currents (slow, density-driven)
                if (cell.Elevation < -0.5f)
                {
                    // Find direction of highest density gradient
                    float maxDensityGradient = 0;
                    (int, int) flowDir = (0, 0);

                    foreach (var (nx, ny, neighbor) in _map.GetNeighbors(x, y))
                    {
                        if (neighbor.IsWater)
                        {
                            float densityGradient = geo.WaterDensity - neighbor.GetGeology().WaterDensity;

                            // Dense water flows toward less dense water
                            if (densityGradient > maxDensityGradient)
                            {
                                maxDensityGradient = densityGradient;
                                flowDir = (nx - x, ny - y);
                            }
                        }
                    }

                    // Deep current transport (very slow)
                    if (flowDir != (0, 0))
                    {
                        int targetX = (x + flowDir.Item1 + _map.Width) % _map.Width;
                        int targetY = Math.Clamp(y + flowDir.Item2, 0, _map.Height - 1);

                        var targetCell = _map.Cells[targetX, targetY];
                        if (targetCell.IsWater)
                        {
                            float deepCurrentStrength = 0.02f * maxDensityGradient * deltaTime;

                            // Transport heat and salt
                            float tempDiff = cell.Temperature - targetCell.Temperature;
                            tempChanges.AddOrUpdate((x, y), -tempDiff * deepCurrentStrength, (key, old) => old - tempDiff * deepCurrentStrength);
                            tempChanges.AddOrUpdate((targetX, targetY), tempDiff * deepCurrentStrength, (key, old) => old + tempDiff * deepCurrentStrength);

                            float saltDiff = geo.Salinity - targetCell.GetGeology().Salinity;
                            saltChanges.AddOrUpdate((x, y), -saltDiff * deepCurrentStrength, (key, old) => old - saltDiff * deepCurrentStrength);
                            saltChanges.AddOrUpdate((targetX, targetY), saltDiff * deepCurrentStrength, (key, old) => old + saltDiff * deepCurrentStrength);
                        }
                    }
                }

                // Upwelling regions (low latitudes, coastal areas)
                // Smooth probability based on latitude
                float upwellingProbability = Math.Max(0, 1.0f - (latitude / 0.35f));
                if (upwellingProbability > 0 && cell.Elevation > -0.3f && cell.Elevation < 0 && _random.NextDouble() < upwellingProbability)
                {
                    // Nutrient-rich deep water rises to surface
                    foreach (var (nx, ny, neighbor) in _map.GetNeighbors(x, y))
                    {
                        if (neighbor.IsWater && neighbor.Elevation < cell.Elevation)
                        {
                            // Bring up cooler, nutrient-rich water
                            float upwellingRate = 0.01f * deltaTime;
                            tempChanges.AddOrUpdate((x, y), -upwellingRate * 0.5f, (key, old) => old - upwellingRate * 0.5f);
                        }
                    }
                }
            }
        });

        foreach (var change in tempChanges)
        {
            _map.Cells[change.Key.Item1, change.Key.Item2].Temperature += change.Value;
        }
        foreach (var change in saltChanges)
        {
            _map.Cells[change.Key.Item1, change.Key.Item2].GetGeology().Salinity += change.Value;
        }
    }

    private void UpdateTides(float deltaTime)
    {
        // Update tidal cycle (completes every 12.4 hours)
        _tidalCycle += deltaTime * (2 * MathF.PI / TidalPeriod);
        if (_tidalCycle > 2 * MathF.PI)
            _tidalCycle -= 2 * MathF.PI;

        float tidalHeight = MathF.Sin(_tidalCycle) * 0.05f; // ±0.05 elevation units

        // Apply tides to coastal and ocean cells
        Parallel.For(0, _map.Width, x =>
        {
            for (int y = 0; y < _map.Height; y++)
            {
                var cell = _map.Cells[x, y];
                var geo = cell.GetGeology();

                if (cell.IsWater)
                {
                    // Ocean tides
                    geo.TideLevel = tidalHeight;

                    // Check adjacent land cells for tidal flooding
                    foreach (var (nx, ny, neighbor) in _map.GetNeighbors(x, y))
                    {
                        if (neighbor.IsLand && neighbor.Elevation < 0.1f) // Low-lying coastal areas
                        {
                            var nGeo = neighbor.GetGeology();
                            nGeo.TideLevel = Math.Max(0, tidalHeight);
                        }
                    }
                }
            }
        });
    }

    private void UpdateFlooding(float deltaTime)
    {
        // Calculate flooding based on rainfall and elevation
        var floodWater = new float[_map.Width, _map.Height];

        // Add water from rainfall
        Parallel.For(0, _map.Width, x =>
        {
            for (int y = 0; y < _map.Height; y++)
            {
                var cell = _map.Cells[x, y];
                var geo = cell.GetGeology();

                if (cell.IsLand)
                {
                    // Validate deltaTime to prevent astronomical values
                    float safeDeltaTime = deltaTime;
                    if (float.IsNaN(safeDeltaTime) || float.IsInfinity(safeDeltaTime) || safeDeltaTime < 0 || safeDeltaTime > 10)
                        safeDeltaTime = 0.01f; // Safe default

                    // Validate inputs to prevent NaN/Infinity
                    float rainfall = cell.Rainfall;
                    if (float.IsNaN(rainfall) || float.IsInfinity(rainfall))
                        rainfall = 0;
                    rainfall = Math.Clamp(rainfall, 0f, 1f);

                    float soilMoisture = geo.SoilMoisture;
                    if (float.IsNaN(soilMoisture) || float.IsInfinity(soilMoisture))
                        soilMoisture = 0;
                    soilMoisture = Math.Clamp(soilMoisture, 0f, 1f);

                    // Heavy rainfall causes flooding
                    float waterInput = rainfall * 0.5f + soilMoisture * 0.2f;

                    // River overflow
                    if (geo.RiverId > 0)
                    {
                        var river = _rivers.FirstOrDefault(r => r.Id == geo.RiverId);
                        if (river != null)
                        {
                            float riverVolume = river.WaterVolume;
                            if (float.IsNaN(riverVolume) || float.IsInfinity(riverVolume))
                                riverVolume = 0;
                            riverVolume = Math.Clamp(riverVolume, 0f, 100f);

                            waterInput += riverVolume * 0.1f;
                        }
                    }

                    // Validate waterInput
                    if (float.IsNaN(waterInput) || float.IsInfinity(waterInput))
                        waterInput = 0;
                    waterInput = Math.Clamp(waterInput, 0f, 10f);

                    // Validate current flood level
                    if (float.IsNaN(geo.FloodLevel) || float.IsInfinity(geo.FloodLevel))
                        geo.FloodLevel = 0;

                    geo.FloodLevel += waterInput * safeDeltaTime;

                    // Clamp flood level to reasonable maximum
                    geo.FloodLevel = Math.Clamp(geo.FloodLevel, 0f, 10f);
                }
            }
        });

        // Water flows downhill
        for (int iteration = 0; iteration < 3; iteration++)
        {
            for (int x = 0; x < _map.Width; x++)
            {
                for (int y = 0; y < _map.Height; y++)
                {
                    var cell = _map.Cells[x, y];
                    var geo = cell.GetGeology();

                    if (geo.FloodLevel > 0.01f)
                    {
                        // Find lowest neighbor
                        float lowestElevation = cell.Elevation + geo.FloodLevel;
                        (int x, int y) lowestNeighbor = (x, y);

                        foreach (var (nx, ny, neighbor) in _map.GetNeighbors(x, y))
                        {
                            float neighborLevel = neighbor.Elevation;
                            if (neighbor.IsLand)
                            {
                                neighborLevel += neighbor.GetGeology().FloodLevel;
                            }

                            if (neighborLevel < lowestElevation)
                            {
                                lowestElevation = neighborLevel;
                                lowestNeighbor = (nx, ny);
                            }
                        }

                        // Flow water to lower neighbor
                        if (lowestNeighbor != (x, y))
                        {
                            float flowAmount = geo.FloodLevel * 0.3f * deltaTime;
                            geo.FloodLevel -= flowAmount;

                            var targetCell = _map.Cells[lowestNeighbor.x, lowestNeighbor.y];
                            if (targetCell.IsLand)
                            {
                                targetCell.GetGeology().FloodLevel += flowAmount;
                            }
                        }
                    }

                    // Evaporation and infiltration
                    geo.FloodLevel *= (1.0f - 0.1f * deltaTime);
                    geo.FloodLevel = Math.Max(0, geo.FloodLevel);
                }
            }
        }
    }

    public void ClearRivers()
    {
        _rivers.Clear();
        _nextRiverId = 1;
    }

    public void LoadRivers(List<River> rivers)
    {
        _rivers = rivers;
        _nextRiverId = rivers.Any() ? rivers.Max(r => r.Id) + 1 : 1;
    }
}

public class River
{
    public int Id { get; set; }
    public int SourceX { get; set; }
    public int SourceY { get; set; }
    public int MouthX { get; set; }
    public int MouthY { get; set; }
    public List<(int x, int y)> Path { get; set; } = new();
    public float WaterVolume { get; set; }
}