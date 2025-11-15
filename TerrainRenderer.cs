using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace SimPlanet;

/// <summary>
/// Renders the planet terrain using procedural colors
/// </summary>
public class TerrainRenderer
{
    private readonly PlanetMap _map;
    private readonly GraphicsDevice _graphicsDevice;
    private Texture2D _pixelTexture;
    private Texture2D _terrainTexture;
    private Color[] _terrainColors;

    public int CellSize { get; set; } = 4;

    private RenderMode _mode = RenderMode.Terrain;
    public RenderMode Mode
    {
        get => _mode;
        set
        {
            if (_mode != value)
            {
                _mode = value;
                _isDirty = true; // Mark for redraw when mode changes
            }
        }
    }

    // Performance optimization: only update texture when needed
    private bool _isDirty = true;
    public void MarkDirty() => _isDirty = true;

    // Camera controls
    public float CameraX { get; set; } = 0;
    public float CameraY { get; set; } = 0;
    public float ZoomLevel { get; set; } = 1.0f;

    // Day/night cycle
    public float DayNightTime { get; set; } = 0; // 0-24 hours
    public bool ShowDayNight { get; set; } = false;
    public bool ShowCityLights { get; set; } = true;

    public TerrainRenderer(PlanetMap map, GraphicsDevice graphicsDevice)
    {
        if (map == null)
            throw new ArgumentNullException(nameof(map), "PlanetMap cannot be null");
        if (graphicsDevice == null)
            throw new ArgumentNullException(nameof(graphicsDevice), "GraphicsDevice cannot be null");

        _map = map;
        _graphicsDevice = graphicsDevice;

        // Create a 1x1 white pixel for drawing
        _pixelTexture = new Texture2D(_graphicsDevice, 1, 1);
        _pixelTexture.SetData(new[] { Color.White });

        // Create terrain texture
        _terrainTexture = new Texture2D(_graphicsDevice, map.Width, map.Height);
        _terrainColors = new Color[map.Width * map.Height];

        UpdateTerrainTexture();
    }

    public void UpdateTerrainTexture()
    {
        // Performance optimization: only update when data has changed
        if (!_isDirty && !ShowDayNight)
            return;

        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                var cell = _map.Cells[x, y];
                if (cell == null)
                {
                    Console.WriteLine($"ERROR: Null cell at ({x}, {y})!");
                    continue;
                }

                int index = y * _map.Width + x;
                if (index < 0 || index >= _terrainColors.Length)
                {
                    Console.WriteLine($"ERROR: Index {index} out of bounds at ({x}, {y})!");
                    continue;
                }


                Color baseColor = Mode switch
                {
                    RenderMode.Terrain => GetTerrainColor(cell),
                    RenderMode.Temperature => GetTemperatureColor(cell),
                    RenderMode.Rainfall => GetRainfallColor(cell),
                    RenderMode.Life => GetLifeColor(cell),
                    RenderMode.Oxygen => GetOxygenColor(cell),
                    RenderMode.CO2 => GetCO2Color(cell),
                    RenderMode.Elevation => GetElevationColor(cell),
                    RenderMode.Geological => GetGeologicalColor(cell),
                    RenderMode.TectonicPlates => GetTectonicPlateColor(cell),
                    RenderMode.Volcanoes => GetVolcanoColor(cell),
                    RenderMode.Clouds => GetCloudsColor(cell),
                    RenderMode.Wind => GetWindColor(cell),
                    RenderMode.Pressure => GetPressureColor(cell),
                    RenderMode.Storms => GetStormsColor(cell),
                    RenderMode.Biomes => GetBiomeColor(cell),
                    RenderMode.Resources => GetResourcesColor(cell),
                    RenderMode.Albedo => GetAlbedoColor(cell),
                    RenderMode.Radiation => GetRadiationColor(cell),
                    RenderMode.Earthquakes => GetEarthquakesColor(cell),
                    RenderMode.Faults => GetFaultsColor(cell),
                    RenderMode.Tsunamis => GetTsunamisColor(cell),
                    RenderMode.Infrastructure => GetInfrastructureColor(cell),
                    _ => Color.Black
                };

                // Apply day/night cycle if enabled
                if (ShowDayNight && Mode == RenderMode.Terrain)
                {
                    baseColor = ApplyDayNightCycle(baseColor, cell, x);
                }

                _terrainColors[index] = baseColor;
            }
        }

        _terrainTexture.SetData(_terrainColors);
        _isDirty = false; // Clear dirty flag after update
    }

    private Color ApplyDayNightCycle(Color baseColor, TerrainCell cell, int x)
    {
        // Calculate longitude-based lighting (simulate planet rotation)
        // DayNightTime goes from 0-24, representing hours
        float longitude = (float)x / _map.Width; // 0 to 1
        float sunPosition = (DayNightTime / 24.0f); // 0 to 1

        // Calculate how far this cell is from the "noon" position
        float distanceFromNoon = Math.Abs(longitude - sunPosition);
        if (distanceFromNoon > 0.5f) distanceFromNoon = 1.0f - distanceFromNoon; // Wrap around

        // Convert to lighting (0 = midnight, 0.25 = noon)
        float lighting = 1.0f - (distanceFromNoon * 4.0f); // 0 to 1, where 1 is full daylight
        lighting = Math.Clamp(lighting, 0.0f, 1.0f);

        // Create dusk/dawn effect
        Color nightColor = new Color(20, 20, 40); // Dark blue for night
        Color dayColor = baseColor;

        // Blend between night and day
        Color litColor = Color.Lerp(nightColor, dayColor, lighting);

        // Add city lights at night if civilization is present
        if (ShowCityLights && cell.LifeType == LifeForm.Civilization && cell.Biomass > 0.5f && lighting < 0.3f)
        {
            // Brighter lights in darker areas
            float cityLight = (0.3f - lighting) / 0.3f; // 0 to 1, brighter when darker
            Color lightColor = new Color(255, 220, 100); // Warm city light
            litColor = Color.Lerp(litColor, lightColor, cityLight * cell.Biomass * 0.6f);
        }

        return litColor;
    }

    public void Draw(SpriteBatch spriteBatch, int offsetX, int offsetY)
    {
        // Calculate zoomed size
        int zoomedWidth = (int)(_map.Width * CellSize * ZoomLevel);
        int zoomedHeight = (int)(_map.Height * CellSize * ZoomLevel);

        // Apply camera offset
        int camX = offsetX - (int)CameraX;
        int camY = offsetY - (int)CameraY;

        spriteBatch.Draw(
            _terrainTexture,
            new Rectangle(camX, camY, zoomedWidth, zoomedHeight),
            Color.White
        );
    }

    private Color GetTerrainColor(TerrainCell cell)
    {
        // More vibrant and realistic terrain colors
        Color baseColor = cell.GetTerrainType() switch
        {
            TerrainType.DeepOcean => new Color(10, 40, 100),        // Deeper, richer blue
            TerrainType.ShallowWater => new Color(30, 120, 200),    // Bright ocean blue
            TerrainType.Beach => new Color(238, 214, 175),          // Sandy beige
            TerrainType.Plains => new Color(200, 180, 120),         // Golden plains
            TerrainType.Grassland => new Color(85, 170, 85),        // Vibrant green
            TerrainType.Forest => new Color(34, 139, 34),           // Deep forest green
            TerrainType.Desert => new Color(237, 201, 175),         // Sandy desert
            TerrainType.Mountain => new Color(120, 110, 100),       // Rocky gray-brown
            TerrainType.Ice => new Color(245, 255, 255),            // Bright white ice
            TerrainType.Tundra => new Color(195, 215, 205),         // Pale blue-green
            _ => Color.Gray
        };

        // Add life overlay with improved colors
        if (cell.LifeType != LifeForm.None && cell.Biomass > 0.1f)
        {
            Color lifeColor = cell.LifeType switch
            {
                LifeForm.Bacteria => new Color(120, 120, 60),           // Yellowish microbes
                LifeForm.Algae => new Color(60, 170, 120),              // Teal-green algae
                LifeForm.PlantLife => new Color(70, 200, 70),           // Bright green plants
                LifeForm.SimpleAnimals => new Color(160, 110, 60),      // Brown creatures
                LifeForm.Fish => new Color(120, 140, 220),              // Blue-ish aquatic
                LifeForm.Amphibians => new Color(130, 180, 90),         // Green amphibians
                LifeForm.Reptiles => new Color(150, 150, 70),           // Olive reptiles
                LifeForm.Dinosaurs => new Color(200, 90, 50),           // Orange-red dinosaurs
                LifeForm.MarineDinosaurs => new Color(110, 110, 200),   // Deep blue marine
                LifeForm.Pterosaurs => new Color(180, 160, 120),        // Tan flying reptiles
                LifeForm.Mammals => new Color(170, 130, 100),           // Brown mammals
                LifeForm.Birds => new Color(160, 200, 160),             // Light green birds
                LifeForm.ComplexAnimals => new Color(190, 130, 70),     // Golden-brown
                LifeForm.Intelligence => new Color(220, 170, 120),      // Warm intelligent life
                LifeForm.Civilization => new Color(255, 220, 120),      // Golden civilization
                _ => Color.Transparent
            };

            // Blend life color with terrain - more prominent for civilizations
            float blend = cell.LifeType == LifeForm.Civilization ?
                Math.Min(cell.Biomass * 0.7f, 0.6f) :
                cell.Biomass * 0.5f;
            baseColor = Color.Lerp(baseColor, lifeColor, blend);
        }

        return baseColor;
    }

    private Color GetTemperatureColor(TerrainCell cell)
    {
        // Map temperature to color gradient
        // Blue (cold) -> Green (moderate) -> Red (hot)
        float temp = cell.Temperature;
        float normalized = Math.Clamp((temp + 30) / 80.0f, 0, 1); // -30 to 50

        if (normalized < 0.5f)
        {
            // Blue to cyan to green
            return Color.Lerp(new Color(0, 0, 255), new Color(0, 255, 0), normalized * 2);
        }
        else
        {
            // Green to yellow to red
            return Color.Lerp(new Color(0, 255, 0), new Color(255, 0, 0), (normalized - 0.5f) * 2);
        }
    }

    private Color GetRainfallColor(TerrainCell cell)
    {
        // Brown (dry) to Blue (wet)
        float rainfall = cell.Rainfall;
        return Color.Lerp(new Color(139, 90, 43), new Color(0, 100, 200), rainfall);
    }

    private Color GetLifeColor(TerrainCell cell)
    {
        if (cell.LifeType == LifeForm.None)
            return Color.Black;

        Color lifeColor = cell.LifeType switch
        {
            LifeForm.Bacteria => new Color(100, 100, 50),
            LifeForm.Algae => new Color(50, 150, 100),
            LifeForm.PlantLife => new Color(60, 200, 60),
            LifeForm.SimpleAnimals => new Color(150, 150, 50),
            LifeForm.Fish => new Color(100, 120, 200),
            LifeForm.Amphibians => new Color(120, 160, 80),
            LifeForm.Reptiles => new Color(140, 140, 60),
            LifeForm.Dinosaurs => new Color(200, 100, 50),       // Bright red-orange for dinosaurs
            LifeForm.MarineDinosaurs => new Color(120, 120, 220),
            LifeForm.Pterosaurs => new Color(180, 160, 120),
            LifeForm.Mammals => new Color(180, 140, 110),
            LifeForm.Birds => new Color(170, 200, 170),
            LifeForm.ComplexAnimals => new Color(200, 150, 60),
            LifeForm.Intelligence => new Color(250, 200, 100),
            LifeForm.Civilization => new Color(255, 255, 100),
            _ => Color.Gray
        };

        // Modulate by biomass
        return Color.Lerp(Color.Black, lifeColor, cell.Biomass);
    }

    private Color GetOxygenColor(TerrainCell cell)
    {
        float normalized = Math.Clamp(cell.Oxygen / 30.0f, 0, 1);
        return Color.Lerp(Color.Black, new Color(100, 200, 255), normalized);
    }

    private Color GetCO2Color(TerrainCell cell)
    {
        // Match legend gradient: dark blue (low CO2) to yellow (high CO2)
        float normalized = Math.Clamp(cell.CO2 / 10.0f, 0, 1);
        return Color.Lerp(new Color(50, 50, 100), new Color(255, 200, 50), normalized);
    }

    private Color GetElevationColor(TerrainCell cell)
    {
        float normalized = (cell.Elevation + 1) / 2.0f; // Map -1 to 1 -> 0 to 1
        return Color.Lerp(Color.Black, Color.White, normalized);
    }

    private Color GetGeologicalColor(TerrainCell cell)
    {
        var geo = cell.GetGeology();

        // Show dominant rock type clearly
        Color rockColor;

        // Determine dominant rock type (max of the three)
        float maxVolcanic = geo.VolcanicRock;
        float maxSedimentary = geo.SedimentaryRock;
        float maxCrystalline = geo.CrystallineRock;

        if (maxVolcanic >= maxSedimentary && maxVolcanic >= maxCrystalline)
        {
            // Volcanic (basalt) - dark gray/black
            rockColor = Color.Lerp(new Color(60, 60, 60), new Color(90, 70, 60), maxVolcanic);
        }
        else if (maxSedimentary > maxCrystalline)
        {
            // Sedimentary (sandstone, limestone) - tan/beige
            rockColor = Color.Lerp(new Color(140, 120, 90), new Color(200, 180, 140), maxSedimentary);
        }
        else
        {
            // Crystalline (granite) - light gray
            rockColor = Color.Lerp(new Color(100, 100, 100), new Color(160, 160, 160), maxCrystalline);
        }

        return rockColor;
    }

    private Color GetTectonicPlateColor(TerrainCell cell)
    {
        var geo = cell.GetGeology();

        // Different color per plate
        Color[] plateColors = new[]
        {
            new Color(255, 100, 100),
            new Color(100, 255, 100),
            new Color(100, 100, 255),
            new Color(255, 255, 100),
            new Color(255, 100, 255),
            new Color(100, 255, 255),
            new Color(255, 200, 100),
            new Color(200, 100, 255)
        };

        Color baseColor = plateColors[geo.PlateId % plateColors.Length];

        // Highlight boundaries
        if (geo.BoundaryType == PlateBoundaryType.Convergent)
        {
            baseColor = Color.Lerp(baseColor, Color.Red, 0.5f);
        }
        else if (geo.BoundaryType == PlateBoundaryType.Divergent)
        {
            baseColor = Color.Lerp(baseColor, Color.Yellow, 0.5f);
        }
        else if (geo.BoundaryType == PlateBoundaryType.Transform)
        {
            baseColor = Color.Lerp(baseColor, Color.Orange, 0.5f);
        }

        return baseColor;
    }

    private Color GetVolcanoColor(TerrainCell cell)
    {
        var geo = cell.GetGeology();

        // Base color: darker background
        Color baseColor = cell.IsWater ? new Color(20, 40, 70) : new Color(40, 40, 40);

        // Highlight volcanoes with activity level
        if (geo.IsVolcano)
        {
            float activity = Math.Clamp(geo.VolcanicActivity + geo.MagmaPressure, 0, 2);

            if (activity > 1.5f)
            {
                // Erupting - bright red/orange
                baseColor = Color.Lerp(new Color(255, 100, 0), new Color(255, 255, 0), (activity - 1.5f) * 2);
            }
            else if (activity > 0.5f)
            {
                // Active - orange to red
                baseColor = Color.Lerp(new Color(150, 50, 0), new Color(255, 100, 0), (activity - 0.5f));
            }
            else
            {
                // Dormant - dark red
                baseColor = Color.Lerp(new Color(60, 30, 30), new Color(120, 40, 0), activity * 2);
            }
        }
        // Show volcanic rock areas (even if not currently a volcano)
        else if (geo.VolcanicRock > 0.5f)
        {
            baseColor = Color.Lerp(baseColor, new Color(80, 60, 60), geo.VolcanicRock);
        }

        return baseColor;
    }

    private Color GetCloudsColor(TerrainCell cell)
    {
        var met = cell.GetMeteorology();

        // Satellite view: show terrain underneath with cloud overlay
        Color terrainColor = GetTerrainColor(cell);

        // Darken terrain slightly for satellite effect
        terrainColor = Color.Lerp(terrainColor, Color.Black, 0.2f);

        // Cloud coverage overlay (like satellite imagery)
        // Clouds should be white - if they're showing other colors, it's a bug
        if (met.CloudCover > 0.05f)
        {
            // Pure white clouds, intensity based on coverage
            Color cloudColor = new Color(255, 255, 255);

            // Blend clouds over terrain based on coverage
            float cloudAlpha = Math.Clamp(met.CloudCover, 0, 0.95f);
            return Color.Lerp(terrainColor, cloudColor, cloudAlpha);
        }

        return terrainColor;
    }

    private Color GetWindColor(TerrainCell cell)
    {
        var met = cell.GetMeteorology();

        // Wind speed magnitude
        float windSpeed = MathF.Sqrt(met.WindSpeedX * met.WindSpeedX + met.WindSpeedY * met.WindSpeedY);

        // Normalize wind speed (typical range: 0-15, extreme can go higher)
        // Map to 0-1 range for color interpolation
        float normalized = Math.Clamp(windSpeed / 15.0f, 0, 1);

        // Gradient from green (calm) to red (extreme), matching legend
        return Color.Lerp(new Color(200, 255, 200), new Color(255, 50, 50), normalized);
    }

    private Color GetPressureColor(TerrainCell cell)
    {
        var met = cell.GetMeteorology();

        // Air pressure in millibars: typical range 950-1050, standard = 1013.25
        // Normalize to 0-1: (pressure - 950) / 100
        float normalized = Math.Clamp((met.AirPressure - 950f) / 100f, 0, 1);

        // Gradient: Blue (low pressure/storms) to Red (high pressure/fair weather)
        return Color.Lerp(new Color(50, 100, 255), new Color(255, 50, 50), normalized);
    }

    private Color GetStormsColor(TerrainCell cell)
    {
        var met = cell.GetMeteorology();

        // Calculate storm intensity from precipitation + wind speed
        float windSpeed = MathF.Sqrt(met.WindSpeedX * met.WindSpeedX + met.WindSpeedY * met.WindSpeedY);

        // Storm intensity combines precipitation (0-1) and high wind (normalized)
        float stormIntensity = Math.Clamp(met.Precipitation * 0.5f + (windSpeed / 15.0f) * 0.5f, 0, 1);

        // Gradient from clear (light blue) to severe storm (purple), matching legend
        return Color.Lerp(new Color(150, 200, 255), new Color(100, 0, 100), stormIntensity);
    }

    private Color GetBiomeColor(TerrainCell cell)
    {
        // Water cells
        if (cell.IsWater)
        {
            if (cell.Elevation < -0.5f)
                return new Color(0, 50, 120); // Deep ocean
            return new Color(20, 100, 180); // Shallow water
        }

        // Get biome for land cells
        var biomeData = cell.GetBiomeData();
        Color color = biomeData.CurrentBiome switch
        {
            // Frozen biomes
            Biome.Glacier => new Color(240, 250, 255),          // White/light blue
            Biome.AlpineTundra => new Color(200, 210, 220),     // Gray-white
            Biome.Tundra => new Color(180, 190, 160),           // Gray-green

            // Forest biomes
            Biome.TropicalRainforest => new Color(10, 100, 20), // Dark green
            Biome.TemperateForest => new Color(34, 139, 34),    // Forest green
            Biome.BorealForest => new Color(20, 80, 40),        // Dark green

            // Grassland biomes
            Biome.Savanna => new Color(200, 180, 100),          // Tan/yellow
            Biome.Grassland => new Color(100, 160, 80),         // Light green
            Biome.Shrubland => new Color(140, 140, 80),         // Olive

            // Arid biomes
            Biome.Desert => new Color(230, 200, 140),           // Sand color

            // Other
            Biome.Mountain => new Color(140, 130, 120),         // Gray
            Biome.Wetland => new Color(60, 120, 90),            // Swamp green

            _ => new Color(180, 160, 100)                        // Default
        };

        // Darken based on biomass for more detail
        if (cell.Biomass > 0.5f && biomeData.CurrentBiome != Biome.Desert && biomeData.CurrentBiome != Biome.Glacier)
        {
            float darken = Math.Min(cell.Biomass - 0.5f, 0.3f);
            color = Color.Lerp(color, new Color(0, 40, 0), darken);
        }

        return color;
    }

    private Color GetResourcesColor(TerrainCell cell)
    {
        // Show all resources at this location
        var resources = cell.GetResources();

        if (resources.Count == 0)
        {
            // No resources - show terrain base color
            if (cell.IsWater)
                return new Color(30, 60, 100);
            else
                return new Color(60, 50, 40);
        }

        // Find the most valuable/abundant resource
        ResourceDeposit? dominantResource = null;
        float maxValue = 0;

        foreach (var deposit in resources)
        {
            float value = deposit.Amount * deposit.Concentration;
            if (value > maxValue)
            {
                maxValue = value;
                dominantResource = deposit;
            }
        }

        if (dominantResource == null)
            return new Color(60, 50, 40);

        // Get resource color and blend with amount/concentration
        Color resourceColor = ResourceExtensions.GetResourceColor(dominantResource.Type);

        // Intensity based on amount and concentration
        float intensity = dominantResource.Amount * dominantResource.Concentration;
        intensity = Math.Clamp(intensity, 0.3f, 1.0f);

        // Blend with base terrain
        Color baseColor = cell.IsWater ? new Color(30, 60, 100) : new Color(60, 50, 40);
        return Color.Lerp(baseColor, resourceColor, intensity);
    }

    private Color GetAlbedoColor(TerrainCell cell)
    {
        // Calculate albedo (surface reflectivity) - same logic as ClimateSimulator.CalculateAlbedo()
        float albedo = 0.0f;

        // Ice and snow (highest albedo - 85%)
        if (cell.IsIce)
        {
            albedo = 0.85f;
        }
        // Water (low albedo)
        else if (cell.IsWater)
        {
            if (cell.Elevation < -0.3f)  // Fixed: was -0.5f, now matches ClimateSimulator
                albedo = 0.06f; // Deep ocean (very dark)
            else
                albedo = 0.08f; // Shallow water
        }
        // Desert (medium-high albedo - 35%)
        else if (cell.IsDesert)
        {
            albedo = 0.35f;
        }
        // Forest (low-medium albedo - 17%)
        else if (cell.IsForest)
        {
            albedo = 0.17f;
        }
        // Grassland (medium albedo - 23%)
        else if (cell.Rainfall > 0.3f && cell.IsLand)  // Fixed: was 0.4f, now matches ClimateSimulator
        {
            albedo = 0.23f;
        }
        // Bare rock/mountains (low-medium albedo - 15%)
        else if (cell.Elevation > 0.5f)  // Fixed: was 0.6f, now matches ClimateSimulator
        {
            albedo = 0.15f;
        }
        // Default land (20%)
        else
        {
            albedo = 0.20f;
        }

        // Urban areas (civilizations) have different albedo (20%)
        if (cell.LifeType == LifeForm.Civilization)
        {
            albedo = 0.20f;
        }

        // Roads modify albedo (asphalt/concrete is dark)
        var geo = cell.GetGeology();
        if (geo.HasRoad)
        {
            // Roads: 0.08-0.12 depending on type (asphalt is very dark)
            albedo = geo.RoadType switch
            {
                RoadType.Highway => 0.08f,   // Fresh asphalt (darkest)
                RoadType.Road => 0.10f,      // Paved road
                RoadType.DirtPath => 0.18f,  // Dirt path (lighter)
                _ => albedo
            };
        }

        // Solar panels have very low albedo (absorb sunlight for energy)
        if (geo.HasSolarFarm)
        {
            albedo = 0.10f; // Dark solar panels
        }

        // Color gradient: Dark (low albedo/absorbs heat) to White (high albedo/reflects heat)
        // Low albedo (0-20%): Dark blue/green (absorbs solar energy)
        // Medium albedo (20-40%): Yellow/tan (moderate reflection)
        // High albedo (40-85%): White (high reflection - ice)
        if (albedo < 0.20f)
        {
            // Low albedo - dark colors (ocean, forest)
            float t = albedo / 0.20f;
            return Color.Lerp(new Color(10, 20, 40), new Color(40, 80, 40), t);
        }
        else if (albedo < 0.40f)
        {
            // Medium albedo - earth tones (desert, grassland)
            float t = (albedo - 0.20f) / 0.20f;
            return Color.Lerp(new Color(40, 80, 40), new Color(200, 180, 100), t);
        }
        else
        {
            // High albedo - bright white (ice, snow)
            float t = (albedo - 0.40f) / 0.45f;
            return Color.Lerp(new Color(200, 180, 100), new Color(255, 255, 255), t);
        }
    }

    private Color GetRadiationColor(TerrainCell cell)
    {
        // Get radiation level from magnetic data
        var magneticData = cell.Magnetic;
        float radiation = magneticData.RadiationLevel;

        // Radiation levels: 0 (safe) to 5+ (deadly)
        // Clamp for visualization
        radiation = Math.Clamp(radiation, 0, 5.0f);
        float t = radiation / 5.0f;

        // Color gradient: Safe (green) → Warning (yellow) → Danger (orange) → Deadly (red/purple)
        if (t < 0.2f)
        {
            // Safe: Dark green to bright green
            return Color.Lerp(new Color(20, 40, 20), new Color(50, 200, 50), t * 5.0f);
        }
        else if (t < 0.4f)
        {
            // Low: Green to yellow
            return Color.Lerp(new Color(50, 200, 50), new Color(200, 200, 50), (t - 0.2f) * 5.0f);
        }
        else if (t < 0.6f)
        {
            // Medium: Yellow to orange
            return Color.Lerp(new Color(200, 200, 50), new Color(255, 150, 50), (t - 0.4f) * 5.0f);
        }
        else if (t < 0.8f)
        {
            // High: Orange to red
            return Color.Lerp(new Color(255, 150, 50), new Color(255, 50, 50), (t - 0.6f) * 5.0f);
        }
        else
        {
            // Deadly: Red to purple
            return Color.Lerp(new Color(255, 50, 50), new Color(150, 0, 150), (t - 0.8f) * 5.0f);
        }
    }

    private Color GetEarthquakesColor(TerrainCell cell)
    {
        var geo = cell.Geology;

        // Epicenter (bright red/orange pulse)
        if (geo.IsEpicenter)
        {
            // Magnitude-based brightness: M2.0 (dim) to M9.0+ (bright)
            float magnitudeNormalized = Math.Clamp((geo.EarthquakeMagnitude - 2.0f) / 7.0f, 0, 1);
            return Color.Lerp(new Color(255, 200, 100), new Color(255, 50, 0), magnitudeNormalized);
        }

        // Active earthquake intensity (seismic waves)
        if (geo.EarthquakeIntensity > 0)
        {
            float intensity = Math.Clamp(geo.EarthquakeIntensity, 0, 1);
            // Color gradient: Faint yellow → Orange → Red
            if (intensity < 0.3f)
            {
                return Color.Lerp(new Color(100, 100, 60), new Color(200, 200, 100), intensity / 0.3f);
            }
            else if (intensity < 0.7f)
            {
                return Color.Lerp(new Color(200, 200, 100), new Color(255, 150, 50), (intensity - 0.3f) / 0.4f);
            }
            else
            {
                return Color.Lerp(new Color(255, 150, 50), new Color(255, 100, 0), (intensity - 0.7f) / 0.3f);
            }
        }

        // Seismic stress buildup (cool colors for accumulating stress)
        if (geo.SeismicStress > 0.3f)
        {
            float stress = Math.Clamp(geo.SeismicStress, 0.3f, 1.5f);
            float t = (stress - 0.3f) / 1.2f; // Normalize 0.3-1.5 to 0-1
            // Blue (low stress) → Purple (high stress, ready to rupture)
            return Color.Lerp(new Color(80, 80, 150), new Color(150, 50, 150), t);
        }

        // Base color (dark)
        return new Color(40, 40, 50);
    }

    private Color GetFaultsColor(TerrainCell cell)
    {
        var geo = cell.Geology;

        if (!geo.IsFault)
        {
            // Non-fault areas: dark gray
            return new Color(50, 50, 50);
        }

        // Fault color based on type and activity
        Color baseColor = geo.FaultType switch
        {
            FaultType.Strike_Slip => new Color(255, 200, 50),    // Yellow-orange (San Andreas)
            FaultType.Normal => new Color(100, 200, 255),        // Light blue (extension)
            FaultType.Reverse => new Color(255, 100, 100),       // Red-pink (compression)
            FaultType.Thrust => new Color(200, 50, 50),          // Dark red (major compression)
            FaultType.Oblique => new Color(200, 150, 255),       // Purple (mixed)
            _ => Color.Gray
        };

        // Brightness based on fault activity (0-1)
        float activity = Math.Clamp(geo.FaultActivity, 0, 1);
        baseColor = Color.Lerp(new Color(baseColor.R / 3, baseColor.G / 3, baseColor.B / 3), baseColor, activity);

        // Brighten if stress is high (about to rupture)
        if (geo.SeismicStress > 0.7f)
        {
            baseColor = Color.Lerp(baseColor, Color.White, (geo.SeismicStress - 0.7f) / 0.3f * 0.5f);
        }

        return baseColor;
    }

    private Color GetTsunamisColor(TerrainCell cell)
    {
        var geo = cell.Geology;

        // Tsunami wave height visualization
        if (geo.TsunamiWaveHeight > 0.1f)
        {
            // Wave height: 0m (calm) to 30m+ (catastrophic)
            float height = Math.Clamp(geo.TsunamiWaveHeight, 0, 30);
            float t = height / 30.0f;

            // Color gradient: Light blue → Cyan → Aqua → White (massive wave)
            if (t < 0.2f)
            {
                // Small wave: Light blue
                return Color.Lerp(new Color(100, 150, 255), new Color(100, 200, 255), t * 5.0f);
            }
            else if (t < 0.5f)
            {
                // Medium wave: Cyan
                return Color.Lerp(new Color(100, 200, 255), new Color(0, 255, 255), (t - 0.2f) / 0.3f);
            }
            else if (t < 0.8f)
            {
                // Large wave: Bright cyan/white
                return Color.Lerp(new Color(0, 255, 255), new Color(200, 255, 255), (t - 0.5f) / 0.3f);
            }
            else
            {
                // Catastrophic wave: White
                return Color.Lerp(new Color(200, 255, 255), Color.White, (t - 0.8f) / 0.2f);
            }
        }

        // Coastal flooding (land areas with flood water)
        if (cell.IsLand && geo.FloodLevel > 0)
        {
            float floodLevel = Math.Clamp(geo.FloodLevel, 0, 5);
            float t = floodLevel / 5.0f;
            // Muddy water color: Brown to dark blue
            return Color.Lerp(new Color(120, 100, 60), new Color(80, 120, 150), t);
        }

        // Ocean color for reference
        if (cell.IsWater)
        {
            return new Color(30, 50, 120); // Dark blue
        }

        // Land (no tsunami): Gray-green
        return new Color(60, 80, 60);
    }

    private Color GetInfrastructureColor(TerrainCell cell)
    {
        var geo = cell.GetGeology();

        // Priority visualization: show most important infrastructure

        // Nuclear power plants (red - highest priority for safety)
        if (geo.HasNuclearPlant)
        {
            // Color intensity based on meltdown risk
            float risk = geo.MeltdownRisk;
            if (risk > 0.2f)
                return Color.Lerp(new Color(255, 100, 0), Color.Red, (risk - 0.2f) / 0.3f); // Orange to red (dangerous)
            else
                return Color.Lerp(new Color(150, 0, 150), new Color(255, 100, 0), risk / 0.2f); // Purple to orange (safe to warning)
        }

        // Solar farms (bright yellow/gold)
        if (geo.HasSolarFarm)
        {
            return new Color(255, 215, 0); // Gold (solar panels)
        }

        // Wind turbines (cyan/light blue)
        if (geo.HasWindTurbine)
        {
            return new Color(100, 200, 255); // Light blue (wind)
        }

        // Roads with tunnels (bright green)
        if (geo.HasTunnel)
        {
            return new Color(0, 255, 100); // Bright green (major infrastructure)
        }

        // Highways (dark gray/black)
        if (geo.HasRoad && geo.RoadType == RoadType.Highway)
        {
            return new Color(50, 50, 50); // Dark gray
        }

        // Paved roads (medium gray)
        if (geo.HasRoad && geo.RoadType == RoadType.Road)
        {
            return new Color(120, 120, 120); // Medium gray
        }

        // Dirt paths (light brown)
        if (geo.HasRoad && geo.RoadType == RoadType.DirtPath)
        {
            return new Color(160, 140, 100); // Light brown
        }

        // Civilization territory (light gray base)
        if (cell.LifeType == LifeForm.Civilization)
        {
            return new Color(80, 80, 80); // Dark gray background
        }

        // Water (dark blue)
        if (cell.IsWater)
        {
            return new Color(20, 40, 80); // Dark blue
        }

        // Uninhabited land (dark green)
        return new Color(40, 60, 40); // Dark green
    }

    public void DrawLegend(SpriteBatch spriteBatch, FontRenderer font, int screenWidth, int screenHeight)
    {
        // Don't show legend for Terrain mode (it's self-explanatory)
        if (Mode == RenderMode.Terrain)
            return;

        int legendWidth = 220;
        int legendHeight = 180;
        // Position legend in bottom-right corner (empty space)
        int legendX = screenWidth - legendWidth - 10;
        int legendY = screenHeight - legendHeight - 10;

        // Background
        spriteBatch.Draw(_pixelTexture,
            new Rectangle(legendX, legendY, legendWidth, legendHeight),
            new Color(0, 0, 0, 200));

        // Border
        DrawBorder(spriteBatch, legendX, legendY, legendWidth, legendHeight, Color.White, 2);

        // Title
        string title = GetLegendTitle();
        font.DrawString(spriteBatch, title, new Vector2(legendX + 10, legendY + 10), Color.Yellow, 14);

        // Draw color gradient and labels
        int gradientX = legendX + 15;
        int gradientY = legendY + 35;
        int gradientWidth = legendWidth - 30;
        int gradientHeight = 20;

        DrawGradientBar(spriteBatch, gradientX, gradientY, gradientWidth, gradientHeight);

        // Labels below gradient
        var labels = GetLegendLabels();
        int labelY = gradientY + gradientHeight + 5;

        if (labels.Count == 2)
        {
            // Min and Max labels
            font.DrawString(spriteBatch, labels[0], new Vector2(gradientX, labelY), Color.White, 11);
            var maxSize = font.MeasureString(labels[1], 11);
            font.DrawString(spriteBatch, labels[1], new Vector2(gradientX + gradientWidth - maxSize.X, labelY), Color.White, 11);
        }
        else if (labels.Count > 2)
        {
            // Category labels
            int currentY = labelY;
            foreach (var label in labels)
            {
                font.DrawString(spriteBatch, label, new Vector2(gradientX, currentY), Color.White, 10);
                currentY += 15;
            }
        }
    }

    private string GetLegendTitle()
    {
        return Mode switch
        {
            RenderMode.Temperature => "TEMPERATURE",
            RenderMode.Rainfall => "RAINFALL",
            RenderMode.Life => "LIFE / BIOMASS",
            RenderMode.Oxygen => "OXYGEN LEVELS",
            RenderMode.CO2 => "CO2 LEVELS",
            RenderMode.Elevation => "ELEVATION",
            RenderMode.Geological => "ROCK TYPES",
            RenderMode.TectonicPlates => "TECTONIC PLATES",
            RenderMode.Volcanoes => "VOLCANIC ACTIVITY",
            RenderMode.Clouds => "CLOUD COVER",
            RenderMode.Wind => "WIND SPEED",
            RenderMode.Pressure => "AIR PRESSURE",
            RenderMode.Storms => "STORM INTENSITY",
            RenderMode.Biomes => "BIOMES",
            RenderMode.Resources => "RESOURCES",
            RenderMode.Albedo => "SURFACE ALBEDO",
            RenderMode.Radiation => "RADIATION LEVELS",
            RenderMode.Infrastructure => "CIVILIZATION INFRASTRUCTURE",
            _ => "LEGEND"
        };
    }

    private List<string> GetLegendLabels()
    {
        return Mode switch
        {
            RenderMode.Temperature => new List<string> { "-50°C (Cold)", "50°C (Hot)" },
            RenderMode.Rainfall => new List<string> { "0% (Arid)", "100% (Wet)" },
            RenderMode.Life => new List<string> { "None", "Bacteria", "Plants", "Animals", "Intelligence" },
            RenderMode.Oxygen => new List<string> { "0%", "25%" },
            RenderMode.CO2 => new List<string> { "0%", "10%" },
            RenderMode.Elevation => new List<string> { "- 1.0 (Ocean)", "1.0 (Mountain)" },
            RenderMode.Geological => new List<string> { "Volcanic", "Sedimentary", "Crystalline" },
            RenderMode.TectonicPlates => new List<string> { "8 Tectonic Plates", "Boundaries Highlighted" },
            RenderMode.Volcanoes => new List<string> { "Inactive", "Active", "Erupting" },
            RenderMode.Clouds => new List<string> { "Clear", "Cloudy" },
            RenderMode.Wind => new List<string> { "Calm", "Extreme" },
            RenderMode.Pressure => new List<string> { "Low", "High" },
            RenderMode.Storms => new List<string> { "Clear", "Severe" },
            RenderMode.Biomes => new List<string> { "Ocean", "Desert", "Forest", "Ice", "Mountain" },
            RenderMode.Resources => new List<string> { "Coal", "Iron", "Oil", "Uranium", "Rare" },
            RenderMode.Albedo => new List<string> { "0% (Dark/Absorbs)", "85% (Bright/Reflects)" },
            RenderMode.Radiation => new List<string> { "0 (Safe)", "5+ (Deadly)" },
            RenderMode.Infrastructure => new List<string> { "Roads", "Nuclear Plants", "Wind Turbines", "Solar Farms", "Tunnels" },
            _ => new List<string>()
        };
    }

    private void DrawGradientBar(SpriteBatch spriteBatch, int x, int y, int width, int height)
    {
        // Draw a color gradient representing the current view mode
        for (int i = 0; i < width; i++)
        {
            float t = i / (float)width;
            Color color = GetGradientColor(t);
            spriteBatch.Draw(_pixelTexture, new Rectangle(x + i, y, 1, height), color);
        }

        // Border around gradient
        DrawBorder(spriteBatch, x, y, width, height, Color.Gray, 1);
    }

    private Color GetGradientColor(float t)
    {
        // t ranges from 0 to 1
        return Mode switch
        {
            RenderMode.Temperature => LerpColor(new Color(100, 150, 255), new Color(255, 100, 50), t),
            RenderMode.Rainfall => LerpColor(new Color(220, 180, 100), new Color(50, 100, 255), t),
            RenderMode.Life => GetLifeGradientColor(t),
            RenderMode.Oxygen => LerpColor(new Color(100, 50, 50), new Color(100, 255, 100), t),
            RenderMode.CO2 => LerpColor(new Color(50, 50, 100), new Color(255, 200, 50), t),
            RenderMode.Elevation => LerpColor(new Color(10, 50, 120), new Color(200, 200, 200), t),
            RenderMode.Geological => GetGeologicalGradientColor(t),
            RenderMode.Volcanoes => LerpColor(new Color(60, 60, 60), new Color(255, 100, 0), t),
            RenderMode.Clouds => LerpColor(new Color(50, 100, 150), Color.White, t),
            RenderMode.Wind => LerpColor(new Color(200, 255, 200), new Color(255, 50, 50), t),
            RenderMode.Pressure => LerpColor(new Color(50, 100, 255), new Color(255, 50, 50), t),
            RenderMode.Storms => LerpColor(new Color(150, 200, 255), new Color(100, 0, 100), t),
            RenderMode.Albedo => GetAlbedoGradientColor(t),
            RenderMode.Radiation => GetRadiationGradientColor(t),
            _ => Color.Gray
        };
    }

    private Color GetAlbedoGradientColor(float t)
    {
        // Dark (low albedo) to White (high albedo)
        if (t < 0.25f)
        {
            // Ocean/forest: dark blue to dark green
            return Color.Lerp(new Color(10, 20, 40), new Color(40, 80, 40), t * 4);
        }
        else if (t < 0.5f)
        {
            // Grassland: green to yellow
            return Color.Lerp(new Color(40, 80, 40), new Color(200, 180, 100), (t - 0.25f) * 4);
        }
        else
        {
            // Desert/ice: yellow to white
            return Color.Lerp(new Color(200, 180, 100), Color.White, (t - 0.5f) * 2);
        }
    }

    private Color GetRadiationGradientColor(float t)
    {
        // Safe (green) → Warning (yellow) → Danger (orange) → Deadly (red/purple)
        if (t < 0.2f)
        {
            return Color.Lerp(new Color(20, 40, 20), new Color(50, 200, 50), t * 5);
        }
        else if (t < 0.4f)
        {
            return Color.Lerp(new Color(50, 200, 50), new Color(200, 200, 50), (t - 0.2f) * 5);
        }
        else if (t < 0.6f)
        {
            return Color.Lerp(new Color(200, 200, 50), new Color(255, 150, 50), (t - 0.4f) * 5);
        }
        else if (t < 0.8f)
        {
            return Color.Lerp(new Color(255, 150, 50), new Color(255, 50, 50), (t - 0.6f) * 5);
        }
        else
        {
            return Color.Lerp(new Color(255, 50, 50), new Color(150, 0, 150), (t - 0.8f) * 5);
        }
    }

    private Color GetLifeGradientColor(float t)
    {
        if (t < 0.25f) return Color.Lerp(new Color(80, 80, 80), new Color(150, 100, 50), t * 4);
        if (t < 0.5f) return Color.Lerp(new Color(150, 100, 50), new Color(100, 200, 100), (t - 0.25f) * 4);
        if (t < 0.75f) return Color.Lerp(new Color(100, 200, 100), new Color(255, 150, 50), (t - 0.5f) * 4);
        return Color.Lerp(new Color(255, 150, 50), new Color(255, 255, 100), (t - 0.75f) * 4);
    }

    private Color GetGeologicalGradientColor(float t)
    {
        if (t < 0.33f) return Color.Lerp(new Color(80, 80, 80), new Color(120, 100, 80), t * 3);
        if (t < 0.66f) return Color.Lerp(new Color(120, 100, 80), new Color(200, 180, 140), (t - 0.33f) * 3);
        return Color.Lerp(new Color(200, 180, 140), new Color(150, 150, 200), (t - 0.66f) * 3);
    }

    private Color LerpColor(Color start, Color end, float t)
    {
        return Color.Lerp(start, end, t);
    }

    private void DrawBorder(SpriteBatch spriteBatch, int x, int y, int width, int height, Color color, int thickness)
    {
        // Top
        spriteBatch.Draw(_pixelTexture, new Rectangle(x, y, width, thickness), color);
        // Bottom
        spriteBatch.Draw(_pixelTexture, new Rectangle(x, y + height - thickness, width, thickness), color);
        // Left
        spriteBatch.Draw(_pixelTexture, new Rectangle(x, y, thickness, height), color);
        // Right
        spriteBatch.Draw(_pixelTexture, new Rectangle(x + width - thickness, y, thickness, height), color);
    }

    public void Dispose()
    {
        _pixelTexture?.Dispose();
        _terrainTexture?.Dispose();
    }
}

public enum RenderMode
{
    Terrain,
    Temperature,
    Rainfall,
    Life,
    Oxygen,
    CO2,
    Elevation,
    Geological,
    TectonicPlates,
    Volcanoes,
    Clouds,
    Wind,
    Pressure,
    Storms,
    Biomes,
    Resources,
    Albedo,         // Surface reflectivity (ice-albedo feedback)
    Radiation,      // Cosmic rays and solar radiation
    Earthquakes,    // Seismic activity and epicenters
    Faults,         // Fault lines and fault types
    Tsunamis,       // Tsunami waves and coastal flooding
    Infrastructure  // Civilization infrastructure (roads, energy, etc.)
}
