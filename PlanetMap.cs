namespace SimPlanet;

public class MapGenerationOptions
{
    public int Seed { get; set; } = 12345;
    public int MapWidth { get; set; } = 240;           // Map dimensions (increased for better detail)
    public int MapHeight { get; set; } = 120;
    public float LandRatio { get; set; } = 0.3f;      // 30% land by default
    public float MountainLevel { get; set; } = 0.5f;   // Mountain frequency
    public float WaterLevel { get; set; } = 0.0f;      // Sea level adjustment
    public int Octaves { get; set; } = 6;
    public float Persistence { get; set; } = 0.5f;
    public float Lacunarity { get; set; } = 2.0f;

    // For preview generation: allows sampling noise at full-map scale while generating fewer cells
    public int ReferenceWidth { get; set; } = 240;
    public int ReferenceHeight { get; set; } = 120;
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
    public float GlobalCO2 { get; set; } = 2.5f; // Early Earth-like: high CO2 from volcanic outgassing
    public float SolarEnergy { get; set; } = 1.0f;
    public float AxisTilt { get; set; } = 23.5f; // Earth's tilt

    // Real-time overrides from the planetary controls panel
    public PlanetaryControlState PlanetaryControls { get; } = new();

    // Progress reporting
    public static float GenerationProgress { get; set; } = 0f;
    public static string GenerationTask { get; set; } = "";

    public PlanetMap(int width, int height, MapGenerationOptions options, int? referenceWidth = null, int? referenceHeight = null)
    {
        Width = width;
        Height = height;
        Options = options;
        Options.ReferenceWidth = referenceWidth ?? width;
        Options.ReferenceHeight = referenceHeight ?? height;
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
                    X = x,
                    Y = y,
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
        // Use ReferenceWidth for consistent noise sampling (allows accurate previews)
        float circumference = Options.ReferenceWidth * scale;
        float radius = circumference / (2.0f * MathF.PI);

        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                // Map x to circular coordinates for seamless horizontal wrapping
                // Use ReferenceWidth so preview samples the same noise as full map
                float angle = (x / (float)Options.ReferenceWidth) * 2.0f * MathF.PI;
                float nx = MathF.Cos(angle) * radius;
                float nz = MathF.Sin(angle) * radius;
                // Use ReferenceHeight for consistent vertical scale
                float ny = (y / (float)Options.ReferenceHeight) * Options.ReferenceHeight * scale;

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
                // Use same cylindrical coordinates for mountains (using ReferenceWidth for consistency)
                float angle = (x / (float)Options.ReferenceWidth) * 2.0f * MathF.PI;
                float nx = MathF.Cos(angle) * radius;
                float nz = MathF.Sin(angle) * radius;
                float ny = (y / (float)Options.ReferenceHeight) * Options.ReferenceHeight * scale;

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

                // Make poles gradually lower (ice caps) with smooth transition
                // Start gradual lowering from 70° latitude
                if (latitudeFactor > 0.7f)
                {
                    // Smooth cubic curve for polar depression
                    float polarFactor = (latitudeFactor - 0.7f) / 0.3f;
                    float loweringAmount = polarFactor * polarFactor * 0.3f;
                    Cells[x, y].Elevation -= loweringAmount;
                }
            }
        }
    }

    private void InitializeGeology()
    {
        var random = new Random(Options.Seed + 5000);
        int totalLayers = 0; // Debug: count total layers added

        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                var cell = Cells[x, y];
                if (cell == null) continue; // Safety check

                var geo = cell.GetGeology();
                if (geo == null) continue; // Safety check

                // Ensure sediment column exists
                if (geo.SedimentColumn == null)
                {
                    geo.SedimentColumn = new List<SedimentType>();
                }

                // Initialize sediment layers based on terrain type and environment
                int layerCount = random.Next(5, 15); // 5-15 initial layers (increased from 3-12)

                // Check if this is a coastal area (near water transition)
                bool isCoastal = false;
                bool isDelta = false;
                if (cell.Elevation >= 0 && cell.Elevation < 0.15f)
                {
                    // Check neighbors for water
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        for (int dy = -1; dy <= 1; dy++)
                        {
                            int nx = (x + dx + Width) % Width;
                            int ny = Math.Clamp(y + dy, 0, Height - 1);
                            if (Cells[nx, ny].Elevation < 0)
                            {
                                isCoastal = true;
                                // Delta: high rainfall + coastal + low elevation
                                if (cell.Rainfall > 0.5f && cell.Elevation < 0.1f)
                                {
                                    isDelta = true;
                                }
                                break;
                            }
                        }
                        if (isCoastal) break;
                    }
                }

                for (int i = 0; i < layerCount; i++)
                {
                    SedimentType sedimentType;

                    // DELTAS - river sediment deposition at coast
                    if (isDelta)
                    {
                        double roll = random.NextDouble();
                        if (roll < 0.4) sedimentType = SedimentType.Silt; // Fine river sediments
                        else if (roll < 0.7) sedimentType = SedimentType.Sand; // Coarser deposits
                        else if (roll < 0.85) sedimentType = SedimentType.Clay; // Floodplain muds
                        else sedimentType = SedimentType.Organic; // Marsh deposits
                    }
                    // OCEAN FLOOR - marine sediments
                    else if (cell.Elevation < 0.0f)
                    {
                        if (cell.Elevation < -0.5f)
                        {
                            // Deep ocean - fine clay and ooze
                            double roll = random.NextDouble();
                            if (roll < 0.6) sedimentType = SedimentType.Clay;
                            else if (roll < 0.85) sedimentType = SedimentType.Limestone; // Pelagic ooze
                            else sedimentType = SedimentType.Organic; // Organic ooze
                        }
                        else
                        {
                            // Shallow ocean - carbonate platforms and reefs
                            double roll = random.NextDouble();
                            if (roll < 0.5) sedimentType = SedimentType.Limestone; // Carbonate platform
                            else if (roll < 0.75) sedimentType = SedimentType.Sand; // Carbonate sand
                            else if (roll < 0.9) sedimentType = SedimentType.Clay;
                            else sedimentType = SedimentType.Organic; // Reef debris

                            // Mark carbonate platforms
                            if (cell.Elevation > -0.3f && cell.Temperature > 15f)
                            {
                                geo.IsCarbonatePlatform = true;
                            }
                        }
                    }
                    // COASTAL ZONES - beach and nearshore
                    else if (isCoastal)
                    {
                        double roll = random.NextDouble();
                        if (roll < 0.6) sedimentType = SedimentType.Sand; // Beach sand
                        else if (roll < 0.8) sedimentType = SedimentType.Gravel; // Beach gravel
                        else sedimentType = SedimentType.Silt; // Tidal flats
                    }
                    // LOWLANDS - alluvial plains, floodplains
                    else if (cell.Elevation < 0.2f)
                    {
                        // Desert vs fluvial environment
                        if (cell.Rainfall < 0.2f && cell.Temperature > 20f)
                        {
                            // Desert - aeolian (wind-blown) sediments
                            double roll = random.NextDouble();
                            if (roll < 0.7) sedimentType = SedimentType.Sand; // Dune sand
                            else if (roll < 0.9) sedimentType = SedimentType.Silt; // Loess (wind-blown silt)
                            else sedimentType = SedimentType.Gravel; // Desert pavement
                        }
                        else
                        {
                            // Fluvial plains - river deposits
                            double roll = random.NextDouble();
                            if (roll < 0.35) sedimentType = SedimentType.Sand; // River channel
                            else if (roll < 0.65) sedimentType = SedimentType.Silt; // Floodplain
                            else if (roll < 0.85) sedimentType = SedimentType.Clay; // Backswamp
                            else sedimentType = SedimentType.Gravel; // Coarse channel deposits
                        }
                    }
                    // HILLS AND UPLANDS - weathered rock and colluvium
                    else if (cell.Elevation < 0.6f)
                    {
                        double roll = random.NextDouble();
                        if (roll < 0.5) sedimentType = SedimentType.Gravel; // Weathered rock fragments
                        else if (roll < 0.75) sedimentType = SedimentType.Sand; // Weathered sand
                        else if (roll < 0.9) sedimentType = SedimentType.Silt; // Ancient deposits
                        else sedimentType = SedimentType.Volcanic; // Volcanic ash layers
                    }
                    // MOUNTAINS - minimal sediment, mostly bedrock
                    else
                    {
                        // Cold vs warm mountains
                        if (cell.Temperature < 0f)
                        {
                            // Glacial environment
                            double roll = random.NextDouble();
                            if (roll < 0.5) sedimentType = SedimentType.Gravel; // Glacial till
                            else if (roll < 0.8) sedimentType = SedimentType.Silt; // Glacial flour
                            else sedimentType = SedimentType.Clay; // Glacial lake deposits
                        }
                        else
                        {
                            // Alpine weathering
                            double roll = random.NextDouble();
                            if (roll < 0.6) sedimentType = SedimentType.Gravel; // Talus and scree
                            else if (roll < 0.8) sedimentType = SedimentType.Silt; // Mountain soil
                            else sedimentType = SedimentType.Volcanic; // Volcanic ash
                        }
                    }

                    geo.SedimentColumn.Add(sedimentType);
                    totalLayers++;
                }

                // Ensure geo data is properly initialized
                if (geo.SedimentColumn.Count == 0)
                {
                    // Failsafe: add at least some sediment
                    geo.SedimentColumn.Add(SedimentType.Sand);
                    geo.SedimentColumn.Add(SedimentType.Silt);
                    geo.SedimentColumn.Add(SedimentType.Clay);
                    totalLayers += 3;
                }
            }
        }

        // Debug output
        Console.WriteLine($"[InitializeGeology] Added {totalLayers} sediment layers across {Width}x{Height} cells");
        Console.WriteLine($"[InitializeGeology] Average {totalLayers / (Width * Height)} layers per cell");
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

                // Initialize ice based on temperature
                // Ice can form on both land (ice sheets, glaciers) and water (sea ice)
                cell.IsIce = cell.Temperature < -10;

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

    // Cached neighbor offset arrays (avoid allocation on every call - PERFORMANCE OPTIMIZATION)
    private static readonly int[] NeighborDx = { -1, 0, 1, -1, 1, -1, 0, 1 };
    private static readonly int[] NeighborDy = { -1, -1, -1, 0, 0, 1, 1, 1 };

    public IEnumerable<(int x, int y, TerrainCell cell)> GetNeighbors(int x, int y)
    {
        for (int i = 0; i < 8; i++)
        {
            int nx = x + NeighborDx[i];
            int ny = y + NeighborDy[i];

            if (ny >= 0 && ny < Height)
            {
                nx = (nx + Width) % Width; // Wrap horizontally
                yield return (nx, ny, Cells[nx, ny]);
            }
        }
    }
}