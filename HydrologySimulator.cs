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
        FormRivers();
        UpdateOceanCurrents();
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
                geo.WaterFlow = (cell.Rainfall + geo.SoilMoisture) * steepestGradient;
            }
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

    public void ClearRivers()
    {
        _rivers.Clear();
        _nextRiverId = 1;
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
