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

    // Progress reporting
    public static float GenerationProgress { get; set; } = 0f;
    public static string GenerationTask { get; set; } = "";

    public PlanetMap(int width, int height, MapGenerationOptions options)
    {
        Width = width;
        Height = height;
        Options = options;
        Cells = new TerrainCell[width, height];

        GenerationProgress = 0f;
        GenerationTask = "Initializing terrain cells...";

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

        GenerationProgress = 0.1f;
        GenerationTask = "Generating terrain elevation...";
        GenerateTerrain();

        GenerationProgress = 0.4f;
        GenerationTask = "Initializing geological layers...";
        InitializeGeology();

        GenerationProgress = 0.6f;
        GenerationTask = "Initializing climate systems...";
        InitializeClimate();

        GenerationProgress = 0.8f;
        GenerationTask = "Generating natural resources...";
        // Generate natural resources after terrain and climate are set
        var resourceGenerator = new ResourceGenerator(this, options.Seed);
        resourceGenerator.GenerateResources();

        GenerationProgress = 1.0f;
        GenerationTask = "Complete!";
    }

    private void GenerateTerrain()
    {
        var noise = new PerlinNoise(Options.Seed);
        float scale = 0.01f;

        // Step 1: Generate base elevation values
        float[,] baseElevation = new float[Width, Height];
        List<float> allValues = new List<float>(Width * Height);

        // For seamless wrapping, map X to a cylinder (wraps horizontally like a sphere)
        // Y stays linear (latitude from pole to pole)
        float circumference = Width * scale;
        float radius = circumference / (2.0f * MathF.PI);

        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                // Map x to circular coordinates for seamless horizontal wrapping
                float angle = (x / (float)Width) * 2.0f * MathF.PI;
                float nx = MathF.Cos(angle) * radius;
                float nz = MathF.Sin(angle) * radius;
                float ny = y * scale;

                // Generate height using 3D noise for seamless wrapping
                // Use nx, ny, nz where nx/nz form a circle
                float elevation = noise.OctaveNoise3D(
                    nx, ny, nz,
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
                // Use same cylindrical coordinates for mountains
                float angle = (x / (float)Width) * 2.0f * MathF.PI;
                float nx = MathF.Cos(angle) * radius;
                float nz = MathF.Sin(angle) * radius;
                float ny = y * scale;

                float elevation = baseElevation[x, y];

                // Shift so sea level threshold becomes 0
                elevation = (elevation - seaLevelThreshold) * 4.0f; // Scale for better range

                // Add mountains to elevated land areas
                if (elevation > 0.1f && Options.MountainLevel > 0.01f)
                {
                    float mountainNoise = noise.OctaveNoise3D(nx * 2.5f, ny * 2.5f, nz * 2.5f, 4, 0.65f, 2.2f);

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

    private void InitializeGeology()
    {
        var random = new Random(Options.Seed + 5000);

        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                var cell = Cells[x, y];
                var geo = cell.GetGeology();

                // Initialize sediment layers based on terrain type
                int layerCount = random.Next(3, 12); // 3-12 initial layers

                for (int i = 0; i < layerCount; i++)
                {
                    SedimentType sedimentType;

                    if (cell.Elevation < 0.0f)
                    {
                        // Ocean floor - marine sediments
                        if (cell.Elevation < -0.5f)
                        {
                            // Deep ocean - fine clay and ooze
                            sedimentType = random.NextDouble() < 0.7 ? SedimentType.Clay : SedimentType.Limestone;
                        }
                        else
                        {
                            // Shallow ocean - more variety
                            double roll = random.NextDouble();
                            if (roll < 0.4) sedimentType = SedimentType.Limestone; // Carbonate platform
                            else if (roll < 0.7) sedimentType = SedimentType.Sand;
                            else sedimentType = SedimentType.Clay;
                        }
                    }
                    else if (cell.Elevation < 0.2f)
                    {
                        // Lowlands and alluvial plains - sedimentary deposits
                        double roll = random.NextDouble();
                        if (roll < 0.35) sedimentType = SedimentType.Sand; // River deposits
                        else if (roll < 0.65) sedimentType = SedimentType.Silt; // Floodplain
                        else if (roll < 0.85) sedimentType = SedimentType.Clay; // Fine sediments
                        else sedimentType = SedimentType.Gravel; // Coarse deposits
                    }
                    else if (cell.Elevation < 0.6f)
                    {
                        // Hills and uplands - weathered rock and colluvium
                        double roll = random.NextDouble();
                        if (roll < 0.5) sedimentType = SedimentType.Gravel; // Weathered rock
                        else if (roll < 0.75) sedimentType = SedimentType.Sand;
                        else sedimentType = SedimentType.Silt; // Ancient deposits
                    }
                    else
                    {
                        // Mountains - minimal sediment, mostly exposed bedrock
                        double roll = random.NextDouble();
                        if (roll < 0.6) sedimentType = SedimentType.Gravel; // Talus and weathered rock
                        else if (roll < 0.8) sedimentType = SedimentType.Silt; // Fine glacial flour
                        else sedimentType = SedimentType.Volcanic; // Volcanic ash from ancient eruptions
                    }

                    geo.SedimentColumn.Add(sedimentType);
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
