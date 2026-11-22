namespace SimPlanet;

public sealed class GeothermalPlant
{
    public int Id { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int CivId { get; set; }
    public int YearBuilt { get; set; }
    public float Progress { get; set; }        // Drilling 0-1
    public bool Producing { get; set; }
    public float DepthKm { get; set; }         // Depth of the production and injection wells
    public float ReservoirTempC { get; set; }  // Temperature of the rock they tap
    public float HeatFlow { get; set; }        // mW/m2 at the site
}

/// <summary>
/// Geothermal energy. The crust is hot where magma is close: around volcanoes, above mantle
/// plumes and along rifts and ridges. Nations with the technology drill pairs of wells there
/// (hot water up, cooled water back down) and run power plants on the steam.
/// </summary>
public sealed class GeothermalSystem
{
    private readonly PlanetMap _map;
    private readonly Random _random;
    private int _nextId = 1;
    private int _lastYear = int.MinValue;
    private long _lastEventId;

    public List<GeothermalPlant> Plants { get; } = new();
    public Action<int, string, int, int, int>? Chronicle;

    public const int TechLevel = 45;
    public const float ViableHeatFlow = 130f;

    public GeothermalSystem(PlanetMap map, int seed)
    {
        _map = map;
        _random = new Random(seed + 991);
    }

    /// <summary>
    /// Surface heat flow in mW/m2 (continental average about 65, volcanic fields several hundred).
    /// </summary>
    public float HeatFlow(int x, int y)
    {
        var geo = _map.Cells[x, y].Geology;
        float heat = geo.CrustType == CrustType.Oceanic ? 80f : 62f;
        if (geo.CrustType == CrustType.Continental && geo.CrustAge > 2000) heat -= 15f;      // Cold old craton
        if (geo.BoundaryType == PlateBoundaryType.Divergent) heat += 90f;                    // Rift or ridge
        heat += Math.Clamp((30f - geo.CrustThickness) * 1.5f, 0f, 30f);                      // Thin crust

        // Magma bodies heat the rock around them
        for (int dy = -2; dy <= 2; dy++)
        {
            int ny = y + dy;
            if (ny < 0 || ny >= _map.Height) continue;
            for (int dx = -2; dx <= 2; dx++)
            {
                var near = _map.Cells[(x + dx + _map.Width) % _map.Width, ny].Geology;
                if (!near.IsVolcano) continue;
                float distance = MathF.Sqrt(dx * dx + dy * dy);
                float source = 120f + 160f * Math.Clamp(near.VolcanicActivity, 0f, 1f) + (near.IsHotSpot ? 80f : 0f);
                heat += source / (1f + distance * distance);
            }
        }
        return heat;
    }

    /// <summary>Rock temperature at depth from the local gradient (conductive geotherm).</summary>
    public float TemperatureAt(int x, int y, float depthKm) => 15f + HeatFlow(x, y) / 2.5f * depthKm;

    public GeothermalPlant? PlantAt(int x, int y, int radius = 0)
    {
        foreach (var plant in Plants)
        {
            int dx = Math.Abs(plant.X - x);
            dx = Math.Min(dx, _map.Width - dx);
            if (Math.Max(dx, Math.Abs(plant.Y - y)) <= radius) return plant;
        }
        return null;
    }

    public void Restore(List<GeothermalPlant>? plants)
    {
        if (plants == null) return;
        Plants.Clear();
        foreach (var plant in plants)
        {
            if (plant.X < 0 || plant.X >= _map.Width || plant.Y < 0 || plant.Y >= _map.Height) continue;
            Plants.Add(plant);
            _nextId = Math.Max(_nextId, plant.Id + 1);
        }
    }

    public void Update(float deltaTime, int year, IReadOnlyList<Civilization> civilizations)
    {
        foreach (var plant in Plants)
        {
            if (plant.Producing) continue;
            plant.Progress += deltaTime / (10f + 4f * plant.DepthKm);
            if (plant.Progress >= 1f)
            {
                plant.Progress = 1f;
                plant.Producing = true;
                if (Perf.Enabled) Console.WriteLine($"[geothermal] plant {plant.Id} producing at {plant.X},{plant.Y}");
                var owner = civilizations.FirstOrDefault(c => c.Id == plant.CivId);
                Chronicle?.Invoke(year, $"{owner?.Name ?? "A nation"} opens a geothermal power plant on {plant.ReservoirTempC:F0} C rock", plant.X, plant.Y, plant.CivId);
            }
        }

        // An eruption next door destroys the plant
        foreach (var e in GeoEvents.Snapshot())
        {
            if (e.Id <= _lastEventId) continue;
            _lastEventId = e.Id;
            if (e.Kind != GeoEventKind.Eruption || e.Magnitude < 2f) continue;
            var hit = PlantAt(e.X, e.Y, 1);
            if (hit == null) continue;
            Plants.Remove(hit);
            _map.Cells[hit.X, hit.Y].Geology.InducedSeismicity = false;
            Chronicle?.Invoke(year, "A geothermal plant is destroyed by the eruption it lived on", hit.X, hit.Y, hit.CivId);
        }

        if (year == _lastYear) return;
        _lastYear = year;

        Plants.RemoveAll(p => civilizations.All(c => c.Id != p.CivId));

        foreach (var civ in civilizations)
        {
            int own = 0;
            foreach (var plant in Plants)
            {
                if (plant.CivId != civ.Id) continue;
                own++;
                // Injecting cold water into hot faulted rock sets off small earthquakes
                _map.Cells[plant.X, plant.Y].Geology.InducedSeismicity = plant.Producing;
            }
            civ.GeothermalPlants = Plants.Count(p => p.CivId == civ.Id && p.Producing);

            if (civ.TechLevel < TechLevel || civ.Territory.Count == 0) continue;
            if (own >= 1 + civ.TechLevel / 25 || _random.NextDouble() > 0.3) continue;

            // Prospect a sample of the territory for the hottest ground
            (int x, int y) best = default;
            float bestHeat = ViableHeatFlow;
            int samples = Math.Min(120, civ.Territory.Count);
            for (int k = 0; k < samples; k++)
            {
                var (x, y) = civ.Territory.ElementAt(_random.Next(civ.Territory.Count));
                var cell = _map.Cells[x, y];
                if (!cell.IsLand || cell.Geology.IsVolcano || PlantAt(x, y, 3) != null) continue;
                float heat = HeatFlow(x, y);
                if (heat > bestHeat)
                {
                    bestHeat = heat;
                    best = (x, y);
                }
            }
            if (bestHeat <= ViableHeatFlow) continue;

            float depth = Math.Clamp(180f / (bestHeat / 2.5f), 1.2f, 4f); // Deep enough for about 200 C
            Plants.Add(new GeothermalPlant
            {
                Id = _nextId++, X = best.x, Y = best.y, CivId = civ.Id, YearBuilt = year,
                DepthKm = depth, HeatFlow = bestHeat, ReservoirTempC = 15f + bestHeat / 2.5f * depth
            });
        }
    }
}
