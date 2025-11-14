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

        // Step 1: Generate base elevation values
        float[,] baseElevation = new float[Width, Height];
        List<float> allValues = new List<float>(Width * Height);

        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                float nx = x * scale;
                float ny = y * scale;

                // Generate height using multiple octaves (returns 0-1)
                float elevation = noise.OctaveNoise(
                    nx, ny,
                    Options.Octaves,
                    Options.Persistence,
                    Options.Lacunarity
                );

                baseElevation[x, y] = elevation;
                allValues.Add(elevation);
            }
        }

        // Step 2: Calculate sea level threshold based on LandRatio
        // Sort all elevation values to find the percentile
        allValues.Sort();
        int seaLevelIndex = (int)((1.0f - Options.LandRatio) * allValues.Count);
        seaLevelIndex = Math.Clamp(seaLevelIndex, 0, allValues.Count - 1);
        float seaLevelThreshold = allValues[seaLevelIndex];

        // Step 3: Apply elevations with proper land/water distribution
        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                float nx = x * scale;
                float ny = y * scale;
                float elevation = baseElevation[x, y];

                // Shift so sea level threshold becomes 0
                elevation = (elevation - seaLevelThreshold) * 4.0f; // Scale for better range

                // Add mountains to elevated land areas
                if (elevation > 0.1f && Options.MountainLevel > 0.01f)
                {
                    float mountainNoise = noise.OctaveNoise(nx * 2.5f, ny * 2.5f, 4, 0.65f, 2.2f);

                    // Square for sharper peaks
                    mountainNoise = mountainNoise * mountainNoise;

                    // Mountain height scales with MountainLevel slider
                    float mountainHeight = mountainNoise * Options.MountainLevel * 1.5f;
                    elevation += mountainHeight;
                }

                // Apply water level offset
                // Positive = raise sea level (more water), Negative = lower sea level (less water)
                elevation -= Options.WaterLevel;

                // Clamp to valid range
                Cells[x, y].Elevation = Math.Clamp(elevation, -1.0f, 1.0f);
            }
        }

        // Step 4: Smooth polar regions (ice caps effect)
        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                float latitudeFactor = Math.Abs((y - Height / 2.0f) / (Height / 2.0f));

                // Make poles slightly lower (ice caps)
                if (latitudeFactor > 0.8f)
                {
                    Cells[x, y].Elevation -= (latitudeFactor - 0.8f) * 0.3f;
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
