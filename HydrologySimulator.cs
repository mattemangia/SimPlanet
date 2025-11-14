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
    private float _tidalCycle = 0;  // 0 to 2π for tidal cycle
    private const float TidalPeriod = 12.4f; // Hours for one tidal cycle

    public List<River> Rivers => _rivers;

    public HydrologySimulator(PlanetMap map, int seed)
    {
        _map = map;
        _random = new Random(seed + 2000);
        _rivers = new List<River>();
    }

    public void Update(float deltaTime)
    {
        UpdateSoilMoisture();
        UpdateWaterFlow();
        UpdateRiverFreezing(); // Check for frozen rivers
        FormRivers();
        UpdateOceanCurrents();
        UpdateTides(deltaTime);
        UpdateFlooding(deltaTime);
    }

    private void UpdateSoilMoisture()
    {
        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                var cell = _map.Cells[x, y];
                var geo = cell.GetGeology();

                if (cell.IsWater)
                {
                    geo.SoilMoisture = 1.0f;
                    continue;
                }

                // Moisture from rainfall
                geo.SoilMoisture += cell.Rainfall * 0.1f;

                // Evaporation
                float evaporation = 0.05f;
                if (cell.Temperature > 20)
                {
                    evaporation += (cell.Temperature - 20) * 0.01f;
                }
                geo.SoilMoisture -= evaporation;

                // Plant water uptake
                if (cell.Biomass > 0.2f)
                {
                    geo.SoilMoisture -= cell.Biomass * 0.05f;
                }

                geo.SoilMoisture = Math.Clamp(geo.SoilMoisture, 0, 1);
            }
        }
    }

    private void UpdateWaterFlow()
    {
        // Calculate water flow direction for each cell
        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                var cell = _map.Cells[x, y];
                var geo = cell.GetGeology();

                if (!cell.IsLand) continue;

                // Find steepest downhill direction
                float steepestGradient = 0;
                (int x, int y) flowDir = (0, 0);

                foreach (var (nx, ny, neighbor) in _map.GetNeighbors(x, y))
                {
                    float gradient = cell.Elevation - neighbor.Elevation;
                    if (gradient > steepestGradient)
                    {
                        steepestGradient = gradient;
                        flowDir = (nx - x, ny - y);
                    }
                }

                geo.FlowDirection = flowDir;

                // Calculate water flow based on rainfall and soil moisture
                // Frozen cells have no water flow
                if (cell.IsIce || cell.Temperature < 0)
                {
                    geo.WaterFlow = 0;
                }
                else
                {
                    geo.WaterFlow = (cell.Rainfall + geo.SoilMoisture) * steepestGradient;
                }
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

    private void FormRivers()
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
                    // Calculate accumulated flow from upstream
                    float accumulatedFlow = CalculateAccumulatedFlow(x, y);

                    if (accumulatedFlow > 0.5f && _random.NextDouble() < 0.001)
                    {
                        CreateRiver(x, y);
                    }
                }
            }
        }
    }

    private float CalculateAccumulatedFlow(int x, int y)
    {
        float accumulated = _map.Cells[x, y].GetGeology().WaterFlow;
        var visited = new HashSet<(int, int)>();
        var queue = new Queue<(int x, int y)>();

        queue.Enqueue((x, y));
        visited.Add((x, y));

        // Check upstream neighbors
        while (queue.Count > 0 && visited.Count < 20)
        {
            var (cx, cy) = queue.Dequeue();

            foreach (var (nx, ny, neighbor) in _map.GetNeighbors(cx, cy))
            {
                if (visited.Contains((nx, ny))) continue;

                var nGeo = neighbor.GetGeology();
                var (fx, fy) = nGeo.FlowDirection;

                // Check if neighbor flows into current cell
                int targetX = (nx + fx + _map.Width) % _map.Width;
                int targetY = Math.Clamp(ny + fy, 0, _map.Height - 1);

                if (targetX == cx && targetY == cy)
                {
                    accumulated += nGeo.WaterFlow;
                    visited.Add((nx, ny));
                    queue.Enqueue((nx, ny));
                }
            }
        }

        return accumulated;
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

    private void UpdateOceanCurrents()
    {
        // Simplified ocean current simulation
        // Based on temperature gradients and planet rotation

        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                var cell = _map.Cells[x, y];
                if (!cell.IsWater) continue;

                var geo = cell.GetGeology();

                // Latitude-based currents (Coriolis effect)
                float latitude = Math.Abs((y - _map.Height / 2.0f) / (_map.Height / 2.0f));

                // Trade winds near equator, westerlies at mid-latitudes
                if (latitude < 0.3f)
                {
                    geo.FlowDirection = (1, 0); // Eastward
                }
                else if (latitude < 0.6f)
                {
                    geo.FlowDirection = (-1, 0); // Westward
                }
                else
                {
                    geo.FlowDirection = (1, 0); // Eastward at poles
                }

                // Current strength affects temperature distribution
                float currentStrength = 0.1f;
                var (fx, fy) = geo.FlowDirection;
                int targetX = (x + fx + _map.Width) % _map.Width;
                int targetY = Math.Clamp(y + fy, 0, _map.Height - 1);

                var targetCell = _map.Cells[targetX, targetY];
                if (targetCell.IsWater)
                {
                    // Heat transport by currents
                    float tempDiff = cell.Temperature - targetCell.Temperature;
                    cell.Temperature -= tempDiff * currentStrength * 0.1f;
                    targetCell.Temperature += tempDiff * currentStrength * 0.1f;
                }
            }
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
        for (int x = 0; x < _map.Width; x++)
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
        }
    }

    private void UpdateFlooding(float deltaTime)
    {
        // Calculate flooding based on rainfall and elevation
        var floodWater = new float[_map.Width, _map.Height];

        // Add water from rainfall
        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                var cell = _map.Cells[x, y];
                var geo = cell.GetGeology();

                if (cell.IsLand)
                {
                    // Heavy rainfall causes flooding
                    float waterInput = cell.Rainfall * 0.5f + geo.SoilMoisture * 0.2f;

                    // River overflow
                    if (geo.RiverId > 0)
                    {
                        var river = _rivers.FirstOrDefault(r => r.Id == geo.RiverId);
                        if (river != null)
                        {
                            waterInput += river.WaterVolume * 0.1f;
                        }
                    }

                    geo.FloodLevel += waterInput * deltaTime;
                }
            }
        }

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
