namespace SimPlanet;

public class MapGenerationOptions
{
    public int Seed { get; set; } = 12345;
    public int MapWidth { get; set; } = 200;           // Map dimensions
    public int MapHeight { get; set; } = 100;
    public float LandRatio { get; set; } = 0.3f;      // 30% land by default
    public float MountainLevel { get; set; } = 0.5f;   // Mountain frequency
    public float WaterLevel { get; set; } = 0.0f;      // Sea level adjustment
    public int Octaves { get; set; } = 6;
    public float Persistence { get; set; } = 0.5f;
    public float Lacunarity { get; set; } = 2.0f;
}

/// <summary>
/// The main planet map containing all terrain cells
/// </summary>
public class PlanetMap
{
    public int Width { get; }
    public int Height { get; }
    public TerrainCell[,] Cells { get; }
    public MapGenerationOptions Options { get; }

    // Global atmospheric values
    public float GlobalTemperature { get; set; } = 15.0f; // Average Earth temp
    public float GlobalOxygen { get; set; } = 21.0f;
    public float GlobalCO2 { get; set; } = 0.04f;
    public float SolarEnergy { get; set; } = 1.0f;
    public float AxisTilt { get; set; } = 23.5f; // Earth's tilt

    public PlanetMap(int width, int height, MapGenerationOptions options)
    {
        Width = width;
        Height = height;
        Options = options;
        Cells = new TerrainCell[width, height];

        // Initialize cells
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Cells[x, y] = new TerrainCell
                {
                    LifeType = LifeForm.None,
                    Oxygen = GlobalOxygen,
                    CO2 = GlobalCO2
                };
            }
        }

        GenerateTerrain();
        InitializeClimate();
    }

    private void GenerateTerrain()
    {
        var noise = new PerlinNoise(Options.Seed);
        float scale = 0.01f;

        // First pass: Generate base elevation
        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                float nx = x * scale;
                float ny = y * scale;

                // Generate height using multiple octaves
                float elevation = noise.OctaveNoise(
                    nx, ny,
                    Options.Octaves,
                    Options.Persistence,
                    Options.Lacunarity
                );

                // Map to -1 to 1 range
                elevation = elevation * 2 - 1;

                // Add mountain features BEFORE water level adjustment for better effect
                // Apply mountains to higher elevation areas
                if (elevation > -0.2f)
                {
                    float mountainNoise = noise.OctaveNoise(nx * 3, ny * 3, 3, 0.5f, 2.0f);
                    // Increased multiplier from 0.3 to 0.6 for more visible mountains
                    elevation += mountainNoise * Options.MountainLevel * 0.6f;
                }

                // Adjust based on land ratio and water level
                elevation -= (1.0f - Options.LandRatio * 2.0f) + Options.WaterLevel;

                Cells[x, y].Elevation = Math.Clamp(elevation, -1.0f, 1.0f);
            }
        }

        // Smooth edges (polar ice caps effect)
        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                float latitudeFactor = Math.Abs((y - Height / 2.0f) / (Height / 2.0f));

                // Make poles slightly more likely to be lower (ice caps)
                if (latitudeFactor > 0.8f)
                {
                    Cells[x, y].Elevation -= (latitudeFactor - 0.8f) * 0.5f;
                }
            }
        }
    }

    private void InitializeClimate()
    {
        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                var cell = Cells[x, y];

                // Temperature based on latitude (distance from equator)
                float latitude = Math.Abs((y - Height / 2.0f) / (Height / 2.0f));
                float baseTemp = 30 - (latitude * 40); // 30°C at equator, -10°C at poles

                // Elevation affects temperature (cooler at higher elevations)
                if (cell.Elevation > 0)
                {
                    baseTemp -= cell.Elevation * 15;
                }

                // Water moderates temperature
                if (cell.IsWater)
                {
                    baseTemp += 5;
                }

                cell.Temperature = baseTemp;

                // Rainfall - more at equator, less at poles
                float baseRainfall = 1.0f - latitude * 0.7f;

                // Mountains create rain shadows
                if (cell.Elevation > 0.5f)
                {
                    baseRainfall += 0.3f;
                }

                // Oceans have high humidity
                if (cell.IsWater)
                {
                    baseRainfall = 0.8f;
                }

                cell.Rainfall = Math.Clamp(baseRainfall, 0, 1);
                cell.Humidity = cell.Rainfall;

                // Initial greenhouse effect
                cell.Greenhouse = GlobalCO2 * 0.01f;
            }
        }
    }

    public TerrainCell GetCell(int x, int y)
    {
        // Wrap horizontally (sphere)
        x = (x + Width) % Width;

        // Clamp vertically
        y = Math.Clamp(y, 0, Height - 1);

        return Cells[x, y];
    }

    public IEnumerable<(int x, int y, TerrainCell cell)> GetNeighbors(int x, int y)
    {
        int[] dx = { -1, 0, 1, -1, 1, -1, 0, 1 };
        int[] dy = { -1, -1, -1, 0, 0, 1, 1, 1 };

        for (int i = 0; i < 8; i++)
        {
            int nx = x + dx[i];
            int ny = y + dy[i];

            if (ny >= 0 && ny < Height)
            {
                nx = (nx + Width) % Width; // Wrap horizontally
                yield return (nx, ny, Cells[nx, ny]);
            }
        }
    }
}
