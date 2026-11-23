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
    /// <summary>Set once small oil and gas deposits have been widened into fields (see PetroleumSystem).</summary>
    public bool FieldsExpanded { get; set; }

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

        // Detailed progress reporting
        UpdateProgress(0.1f, "Generating terrain elevation...");
        GenerateTerrain();

        UpdateProgress(0.4f, "Initializing geological layers...");
        InitializeGeology();

        UpdateProgress(0.6f, "Initializing climate systems...");
        InitializeClimate();

        UpdateProgress(0.8f, "Generating natural resources...");
        var resourceGenerator = new ResourceGenerator(this, options.Seed);
        resourceGenerator.GenerateResources();

        UpdateProgress(0.9f, "Detecting coastal zones...");
        UpdateCoastalZones();

        UpdateProgress(1.0f, "Complete!");
    }

    private void UpdateProgress(float progress, string task)
    {
        GenerationProgress = progress;
        GenerationTask = task;
    }

    private void GenerateTerrain()
    {
        var noise = new PerlinNoise(Options.Seed);
        float scale = 0.01f;

        // Step 1: Generate base elevation values
        UpdateProgress(0.15f, "Generating noise map...");
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
                float angle = (x / (float)Width) * 2.0f * MathF.PI; // normalised so previews cover the whole planet
                float nx = MathF.Cos(angle) * radius;
                float nz = MathF.Sin(angle) * radius;
                // Use ReferenceHeight for consistent vertical scale
                float ny = (y / (float)Height) * Options.ReferenceHeight * scale;

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
        UpdateProgress(0.25f, "Calibrating sea level...");
        // Sort all elevation values to find the percentile
        allValues.Sort();
        int seaLevelIndex = (int)((1.0f - Options.LandRatio) * allValues.Count);
        seaLevelIndex = Math.Clamp(seaLevelIndex, 0, allValues.Count - 1);
        float seaLevelThreshold = allValues[seaLevelIndex];

        // Step 3: Apply elevations with proper land/water distribution
        UpdateProgress(0.3f, "Raising mountains...");
        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                // Use same cylindrical coordinates for mountains (using ReferenceWidth for consistency)
                float angle = (x / (float)Width) * 2.0f * MathF.PI; // normalised so previews cover the whole planet
                float nx = MathF.Cos(angle) * radius;
                float nz = MathF.Sin(angle) * radius;
                float ny = (y / (float)Height) * Options.ReferenceHeight * scale;

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
        UpdateProgress(0.35f, "Forming polar caps...");
        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                float latitudeFactor = Math.Abs((y - Height / 2.0f) / (Height / 2.0f));

                // Make poles gradually lower (ice caps) with CONTINUOUS transition
                // Use GAUSSIAN distribution with geographic variation to avoid banding
                float geoVariation = MathF.Sin(x * 0.3f + y * 0.15f) * 0.06f;
                float effectiveLat = Math.Clamp(latitudeFactor + geoVariation, 0f, 1f);

                // Gaussian-based polar depression - smooth continuous falloff
                // Centered at pole (1.0) with wide sigma for gradual transition
                float polarFactor = MathF.Exp(-MathF.Pow(effectiveLat - 1.0f, 2) / (2 * 0.2f * 0.2f));
                float loweringAmount = polarFactor * 0.25f;
                Cells[x, y].Elevation -= loweringAmount;
            }
        }
    }

    private void InitializeGeology()
    {
        var random = new Random(Options.Seed + 5000);
        var strataNoise = new PerlinNoise(Options.Seed + 6001);
        float seaPhase = (float)random.NextDouble() * 6.28f;
        const int StrataSlices = 12;
        int totalLayers = 0; // Debug: count total layers added

        UpdateProgress(0.45f, "Building crust...");
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

                // The column records the history of the place: through time the sea rose and
                // fell and the land moved, so each spot saw a succession of environments
                // (deep sea, shelf, shore, river plain, alluvial fan) and kept a bed from each.
                // Everything comes from smooth fields, so beds can be followed from cell to
                // cell and change sideways the way real formations do.
                float latitude = Math.Abs((y - Height / 2.0f) / (Height / 2.0f));
                bool warm = latitude < 0.42f;
                float wetness = strataNoise.OctaveNoise(x * 0.03f + 50f, y * 0.03f, 2, 0.5f, 2.0f);
                float subsidence = 0.5f + 0.5f * strataNoise.OctaveNoise(x * 0.02f, y * 0.02f + 90f, 2, 0.5f, 2.0f);

                for (int k = 0; k < StrataSlices; k++)
                {
                    float age = (StrataSlices - 1 - k) / (float)(StrataSlices - 1); // 1 = oldest
                    float seaLevel = 0.22f * MathF.Sin(k * 0.9f + seaPhase) + 0.10f * MathF.Sin(k * 2.3f + 1.7f);
                    float drift = strataNoise.OctaveNoise(x * 0.025f + k * 7.3f, y * 0.025f - k * 3.1f, 2, 0.5f, 2.0f);
                    float local = strataNoise.OctaveNoise(x * 0.12f + k * 5.0f, y * 0.12f + k * 2.0f, 1, 0.5f, 2.0f);
                    float e = cell.Elevation - seaLevel + age * 0.38f * drift;

                    SedimentType sedimentType;
                    // How much each interval left behind varies with place and time
                    float rate = Math.Clamp(subsidence, 0f, 1f) * (0.35f + 1.3f * (0.5f + 0.5f * strataNoise.OctaveNoise(x * 0.05f - k * 13f, y * 0.05f + k * 4f, 1, 0.5f, 2.0f)));
                    int beds = Math.Clamp((int)MathF.Round(rate * 4.5f), 1, 5);

                    // Regional ash falls: the same bed over a wide area, a time marker
                    if ((k == 4 || k == 9) && strataNoise.OctaveNoise(x * 0.04f + k * 11f, y * 0.04f, 1, 0.5f, 2.0f) > 0.28f)
                    {
                        sedimentType = SedimentType.Volcanic;
                        beds = 1;
                    }
                    else if (e < -0.55f)
                    {
                        // Deep sea: clay, with chalky ooze in warm water and black shale in stagnant basins
                        sedimentType = local > 0.42f ? SedimentType.Organic : (warm && k % 3 == 0) ? SedimentType.Limestone : SedimentType.Clay;
                    }
                    else if (e < -0.08f)
                    {
                        // Shelf: carbonate platforms in the tropics, mud and silt elsewhere, sand tongues
                        if (local > 0.35f) sedimentType = SedimentType.Sand;
                        else if (warm) sedimentType = SedimentType.Limestone;
                        else sedimentType = k % 2 == 0 ? SedimentType.Silt : SedimentType.Clay;
                    }
                    else if (e < 0.04f)
                    {
                        // Shoreface and beach
                        sedimentType = local > 0.45f ? SedimentType.Gravel : SedimentType.Sand;
                    }
                    else if (e < 0.28f)
                    {
                        // Coastal and river plains: channels, floodplains, swamps; dunes where dry
                        if (wetness < -0.15f) sedimentType = local > 0.2f ? SedimentType.Silt : SedimentType.Sand;
                        else if (local < -0.2f) sedimentType = SedimentType.Clay;
                        else if (local < 0.1f) sedimentType = SedimentType.Silt;
                        else if (local < 0.35f) sedimentType = SedimentType.Sand;
                        else sedimentType = wetness > 0.1f ? SedimentType.Organic : SedimentType.Gravel;
                    }
                    else if (e < 0.5f)
                    {
                        // Uplands: alluvial fans in places, erosion elsewhere (no bed: a gap in the record)
                        if (local < 0f) continue;
                        sedimentType = local > 0.3f ? SedimentType.Gravel : SedimentType.Sand;
                        beds = 1;
                    }
                    else
                    {
                        // Mountains shed sediment instead of keeping it
                        if (local < 0.4f) continue;
                        sedimentType = SedimentType.Gravel;
                        beds = 1;
                    }

                    for (int b = 0; b < beds; b++)
                    {
                        geo.SedimentColumn.Add(sedimentType);
                        totalLayers++;
                    }
                }

                if (cell.Elevation < 0f && cell.Elevation > -0.3f && warm)
                {
                    geo.IsCarbonatePlatform = true;
                }

                // High ground is being worn down: its surface is an erosion surface
                if (cell.Elevation > 0.3f)
                {
                    geo.UnconformityBeds = 0;
                }

                // Ensure geo data is properly initialized
                if (geo.SedimentColumn.Count == 0)
                {
                    // Bare rock with a thin cover of debris
                    geo.SedimentColumn.Add(SedimentType.Gravel);
                    totalLayers += 1;
                }
            }
        }

        // Debug output
        Console.WriteLine($"[InitializeGeology] Added {totalLayers} sediment layers across {Width}x{Height} cells");
        Console.WriteLine($"[InitializeGeology] Average {totalLayers / (Width * Height)} layers per cell");
    }

    private void InitializeClimate()
    {
        UpdateProgress(0.65f, "Calculating temperatures...");
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

    /// <summary>
    /// The 8 surrounding cells (wrapping horizontally). Returns an allocation-free
    /// enumerable when used with foreach; LINQ still works on it.
    /// </summary>
    public NeighborEnumerable GetNeighbors(int x, int y) => new(this, x, y);

    public readonly struct NeighborEnumerable : IEnumerable<(int x, int y, TerrainCell cell)>
    {
        private readonly PlanetMap _map;
        private readonly int _x;
        private readonly int _y;

        public NeighborEnumerable(PlanetMap map, int x, int y)
        {
            _map = map;
            _x = x;
            _y = y;
        }

        public Enumerator GetEnumerator() => new(_map, _x, _y);
        IEnumerator<(int x, int y, TerrainCell cell)> IEnumerable<(int x, int y, TerrainCell cell)>.GetEnumerator() => GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

        public struct Enumerator : IEnumerator<(int x, int y, TerrainCell cell)>
        {
            private readonly PlanetMap _map;
            private readonly int _x;
            private readonly int _y;
            private int _index;

            public Enumerator(PlanetMap map, int x, int y)
            {
                _map = map;
                _x = x;
                _y = y;
                _index = -1;
                Current = default;
            }

            public (int x, int y, TerrainCell cell) Current { get; private set; }
            object System.Collections.IEnumerator.Current => Current;

            public bool MoveNext()
            {
                while (++_index < 8)
                {
                    int ny = _y + NeighborDy[_index];
                    if (ny < 0 || ny >= _map.Height) continue;

                    int nx = (_x + NeighborDx[_index] + _map.Width) % _map.Width; // Wrap horizontally
                    Current = (nx, ny, _map.Cells[nx, ny]);
                    return true;
                }
                return false;
            }

            public void Reset() => _index = -1;
            public void Dispose() { }
        }
    }

    // Expanded neighborhood search (inclusive of radius distance in a square window)
    public IEnumerable<(int x, int y, TerrainCell cell)> GetNeighbors(int x, int y, int radius)
    {
        for (int dx = -radius; dx <= radius; dx++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                if (dx == 0 && dy == 0) continue;

                int nx = (x + dx + Width) % Width;
                int ny = y + dy;

                if (ny >= 0 && ny < Height)
                {
                    yield return (nx, ny, Cells[nx, ny]);
                }
            }
        }
    }

    /// <summary>
    /// Updates coastal zone detection for all cells.
    /// A cell is coastal if it's land and has water neighbors.
    /// </summary>
    public void UpdateCoastalZones()
    {
        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                var cell = Cells[x, y];
                cell.IsCoastal = false;

                // Only check land cells
                if (!cell.IsLand) continue;

                // Check if any neighbor is water
                foreach (var (nx, ny, neighbor) in GetNeighbors(x, y))
                {
                    if (neighbor.IsWater)
                    {
                        cell.IsCoastal = true;
                        break;
                    }
                }
            }
        }
    }
}