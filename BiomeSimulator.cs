namespace SimPlanet;

/// <summary>
/// Simulates dynamic biome transitions and ecosystems
/// </summary>
public class BiomeSimulator
{
    private readonly PlanetMap _map;
    private readonly Random _random;
    private float _biomeUpdateTimer = 0;
    private const float BiomeUpdateInterval = 5.0f; // Update biomes every 5 game-seconds

    public BiomeSimulator(PlanetMap map, int seed)
    {
        _map = map;
        _random = new Random(seed + 5000);
    }

    public void Update(float deltaTime)
    {
        _biomeUpdateTimer += deltaTime;

        // Update biomes periodically (not every frame for performance)
        if (_biomeUpdateTimer >= BiomeUpdateInterval)
        {
            UpdateBiomes();
            _biomeUpdateTimer = 0;
        }
    }

    private void UpdateBiomes()
    {
        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                var cell = _map.Cells[x, y];

                if (!cell.IsLand) continue;

                Biome previousBiome = cell.GetBiomeData().CurrentBiome;
                Biome targetBiome = DetermineBiome(cell, x, y);

                // Gradual transition: allow neighbors to influence if close to boundary
                targetBiome = BlendWithNeighbors(x, y, targetBiome);

                // Biome transition (gradual)
                if (targetBiome != previousBiome)
                {
                    // Gradual succession - check if transition is allowed
                    if (CanTransitionTo(previousBiome, targetBiome))
                    {
                        TransitionBiome(cell, previousBiome, targetBiome, x, y);
                    }
                }

                // Biome-specific processes
                ApplyBiomeEffects(cell, cell.GetBiomeData().CurrentBiome, x, y);

                // Ecological succession on new land
                ApplySuccession(cell, x, y);
            }
        }
    }

    private Biome DetermineBiome(TerrainCell cell)
    {
        // Ice biomes
        if (cell.Temperature < -10 || cell.IsIce)
            return Biome.Glacier;

        // High elevation biomes
        if (cell.Elevation > 0.7f)
        {
            if (cell.Temperature < 0)
                return Biome.AlpineTundra;
            return Biome.Mountain;
        }

        // Temperature-based biomes
        if (cell.Temperature < 0)
            return Biome.Tundra;

        // Tropical biomes (hot)
        if (cell.Temperature > 25)
        {
            if (cell.Rainfall > 0.7f)
                return Biome.TropicalRainforest;
            if (cell.Rainfall > 0.4f)
                return Biome.Savanna;
            return Biome.Desert;
        }

        // Temperate biomes
        if (cell.Temperature > 10)
        {
            if (cell.Rainfall > 0.6f && cell.Biomass > 0.3f)
                return Biome.TemperateForest;
            if (cell.Rainfall > 0.4f)
                return Biome.Grassland;
            if (cell.Rainfall < 0.25f)
                return Biome.Desert;
            return Biome.Shrubland;
        }

        // Boreal biomes (cold but above freezing)
        if (cell.Rainfall > 0.4f)
            return Biome.BorealForest;

        return Biome.Tundra;
    }

    private void TransitionBiome(TerrainCell cell, Biome oldBiome, Biome newBiome, int x, int y)
    {
        var biomeData = cell.GetBiomeData();
        biomeData.CurrentBiome = newBiome;
        biomeData.YearsSinceBiomeChange = 0;

        // Handle biomass changes during transition
        if (IsForestBiome(newBiome) && !IsForestBiome(oldBiome))
        {
            // Forestation - gradual biomass increase
            if (cell.Biomass < 0.5f)
                cell.Biomass += 0.1f;
        }
        else if (!IsForestBiome(newBiome) && IsForestBiome(oldBiome))
        {
            // Deforestation (natural) - gradual biomass decrease
            if (cell.Biomass > 0.1f)
                cell.Biomass -= 0.2f;
        }

        // Desert expansion
        if (newBiome == Biome.Desert && oldBiome != Biome.Desert)
        {
            cell.Biomass *= 0.3f; // Lose most biomass
            cell.Rainfall *= 0.8f; // Further reduce rainfall (positive feedback)
        }

        // Glaciation
        if (newBiome == Biome.Glacier && oldBiome != Biome.Glacier)
        {
            cell.Biomass = 0; // No life on glaciers
            cell.Elevation += 0.05f; // Ice builds up
        }

        // Glacier retreat
        if (oldBiome == Biome.Glacier && newBiome != Biome.Glacier)
        {
            cell.Elevation -= 0.03f; // Ice melts
            // Leave barren land that can be recolonized
        }
    }

    private void ApplyBiomeEffects(TerrainCell cell, Biome biome, int x, int y)
    {
        var biomeData = cell.GetBiomeData();
        biomeData.YearsSinceBiomeChange++;

        // Each biome has feedback effects on local climate
        switch (biome)
        {
            case Biome.TropicalRainforest:
                // Rainforests increase local humidity and rainfall
                cell.Humidity = Math.Min(cell.Humidity + 0.01f, 1.0f);
                if (cell.Rainfall < 0.9f)
                    cell.Rainfall += 0.005f;
                // Rainforests absorb CO2
                cell.CO2 = Math.Max(cell.CO2 - 0.01f, 0.5f);
                // Increase biomass over time
                if (cell.Biomass < 0.9f)
                    cell.Biomass += 0.01f;
                break;

            case Biome.Desert:
                // Deserts have low humidity
                cell.Humidity = Math.Max(cell.Humidity - 0.01f, 0.05f);
                // Desert expansion (positive feedback)
                if (_random.NextDouble() < 0.01)
                {
                    SpreadDesertification(x, y);
                }
                break;

            case Biome.Glacier:
                // Glaciers reflect sunlight (albedo effect)
                cell.Temperature = Math.Max(cell.Temperature - 0.1f, -50);
                // Spread ice to neighbors if cold enough
                if (_random.NextDouble() < 0.005)
                {
                    SpreadGlaciation(x, y);
                }
                break;

            case Biome.TemperateForest:
            case Biome.BorealForest:
                // Forests moderate climate and increase humidity
                cell.Humidity = Math.Min(cell.Humidity + 0.005f, 0.8f);
                cell.CO2 = Math.Max(cell.CO2 - 0.005f, 0.5f);
                if (cell.Biomass < 0.7f)
                    cell.Biomass += 0.005f;
                break;

            case Biome.Grassland:
            case Biome.Savanna:
                // Grasslands have moderate biomass
                if (cell.Biomass < 0.4f)
                    cell.Biomass += 0.003f;
                break;

            case Biome.Tundra:
                // Tundra has very slow biomass accumulation
                if (cell.Biomass < 0.2f && cell.Temperature > -5)
                    cell.Biomass += 0.001f;
                break;
        }

        // Biome maturity - some properties improve over time
        if (biomeData.YearsSinceBiomeChange > 50 && IsForestBiome(biome))
        {
            // Old growth forests have maximum biomass
            if (cell.Biomass < 0.95f)
                cell.Biomass = Math.Min(cell.Biomass + 0.002f, 0.95f);
        }
    }

    private void SpreadDesertification(int x, int y)
    {
        // Desert can spread to neighboring dry areas
        foreach (var (nx, ny, neighbor) in _map.GetNeighbors(x, y))
        {
            if (neighbor.IsLand && neighbor.Rainfall < 0.3f && neighbor.Temperature > 15)
            {
                neighbor.Rainfall *= 0.95f; // Reduce rainfall
                neighbor.Biomass *= 0.9f; // Reduce biomass
            }
        }
    }

    private void SpreadGlaciation(int x, int y)
    {
        // Ice can spread to neighboring cold areas
        foreach (var (nx, ny, neighbor) in _map.GetNeighbors(x, y))
        {
            if (neighbor.Temperature < -5 && neighbor.IsLand)
            {
                neighbor.Temperature -= 1; // Cool neighbor
            }
        }
    }

    private bool IsForestBiome(Biome biome)
    {
        return biome == Biome.TropicalRainforest ||
               biome == Biome.TemperateForest ||
               biome == Biome.BorealForest;
    }
}

public enum Biome
{
    // Frozen
    Glacier,
    AlpineTundra,
    Tundra,

    // Forests
    TropicalRainforest,
    TemperateForest,
    BorealForest,

    // Grasslands
    Savanna,
    Grassland,
    Shrubland,

    // Arid
    Desert,

    // Other
    Mountain,
    Wetland
}

/// <summary>
/// Extension methods for biome data storage
/// </summary>
public static class BiomeExtensions
{
    private static Dictionary<TerrainCell, BiomeData> _biomeData = new();

    public static BiomeData GetBiomeData(this TerrainCell cell)
    {
        if (!_biomeData.ContainsKey(cell))
        {
            _biomeData[cell] = new BiomeData();
        }
        return _biomeData[cell];
    }

    public static void ClearBiomeData()
    {
        _biomeData.Clear();
    }
}

public class BiomeData
{
    public Biome CurrentBiome { get; set; } = Biome.Grassland;
    public int YearsSinceBiomeChange { get; set; } = 0;
    public float BiomeSuitability { get; set; } = 1.0f; // How well-suited the conditions are for this biome
}
