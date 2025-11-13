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
                int index = y * _map.Width + x;

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
        Color baseColor = cell.GetTerrainType() switch
        {
            TerrainType.DeepOcean => new Color(0, 50, 120),
            TerrainType.ShallowWater => new Color(20, 100, 180),
            TerrainType.Beach => new Color(220, 200, 150),
            TerrainType.Plains => new Color(180, 160, 100),
            TerrainType.Grassland => new Color(100, 160, 80),
            TerrainType.Forest => new Color(34, 139, 34),
            TerrainType.Desert => new Color(230, 200, 140),
            TerrainType.Mountain => new Color(140, 130, 120),
            TerrainType.Ice => new Color(240, 250, 255),
            TerrainType.Tundra => new Color(180, 200, 190),
            _ => Color.Gray
        };

        // Add life overlay
        if (cell.LifeType != LifeForm.None && cell.Biomass > 0.1f)
        {
            Color lifeColor = cell.LifeType switch
            {
                LifeForm.Bacteria => new Color(100, 100, 50),
                LifeForm.Algae => new Color(50, 150, 100),
                LifeForm.PlantLife => new Color(60, 180, 60),
                LifeForm.SimpleAnimals => new Color(150, 100, 50),
                LifeForm.ComplexAnimals => new Color(180, 120, 60),
                LifeForm.Intelligence => new Color(200, 150, 100),
                LifeForm.Civilization => new Color(255, 200, 100),
                _ => Color.Transparent
            };

            // Blend life color with terrain
            float blend = cell.Biomass * 0.5f;
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
        float normalized = Math.Clamp(cell.CO2 / 10.0f, 0, 1);
        return Color.Lerp(Color.Black, new Color(255, 100, 100), normalized);
    }

    private Color GetElevationColor(TerrainCell cell)
    {
        float normalized = (cell.Elevation + 1) / 2.0f; // Map -1 to 1 -> 0 to 1
        return Color.Lerp(Color.Black, Color.White, normalized);
    }

    private Color GetGeologicalColor(TerrainCell cell)
    {
        var geo = cell.GetGeology();

        // Show rock types
        Color rockColor;
        if (geo.VolcanicRock > 0.5f)
            rockColor = new Color(80, 40, 40); // Dark volcanic
        else if (geo.SedimentaryRock > 0.5f)
            rockColor = new Color(160, 140, 100); // Sandstone/limestone
        else
            rockColor = new Color(120, 120, 120); // Crystalline/granite

        // Overlay erosion
        if (geo.ErosionRate > 0.1f)
        {
            rockColor = Color.Lerp(rockColor, new Color(200, 180, 150), geo.ErosionRate);
        }

        // Show sediment accumulation
        if (geo.SedimentLayer > 0.1f)
        {
            rockColor = Color.Lerp(rockColor, new Color(220, 200, 150), geo.SedimentLayer);
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

        // Base terrain
        Color baseColor = GetTerrainColor(cell);

        // Highlight volcanoes
        if (geo.IsVolcano)
        {
            float activity = geo.VolcanicActivity + geo.MagmaPressure;
            Color volcanoColor = Color.Lerp(new Color(150, 50, 0), Color.Red, activity);
            baseColor = Color.Lerp(baseColor, volcanoColor, 0.7f);
        }

        // Show volcanic rock
        if (geo.VolcanicRock > 0.3f)
        {
            baseColor = Color.Lerp(baseColor, new Color(60, 30, 30), geo.VolcanicRock * 0.5f);
        }

        return baseColor;
    }

    private Color GetCloudsColor(TerrainCell cell)
    {
        var met = cell.GetMeteorology();

        // Base sky color
        Color skyColor = cell.IsWater ? new Color(100, 150, 220) : new Color(135, 206, 235);

        // Cloud coverage
        if (met.CloudCover > 0.1f)
        {
            Color cloudColor = new Color(255, 255, 255);
            if (met.CloudCover > 0.7f)
            {
                cloudColor = new Color(180, 180, 190); // Storm clouds
            }

            return Color.Lerp(skyColor, cloudColor, met.CloudCover);
        }

        return skyColor;
    }

    private Color GetWindColor(TerrainCell cell)
    {
        var met = cell.GetMeteorology();

        // Base terrain faded
        Color baseColor = Color.Lerp(GetTerrainColor(cell), Color.Gray, 0.5f);

        // Wind speed as color intensity
        float windSpeed = MathF.Sqrt(met.WindSpeedX * met.WindSpeedX + met.WindSpeedY * met.WindSpeedY);

        if (windSpeed < 0.1f)
        {
            return Color.Lerp(baseColor, new Color(200, 200, 255), 0.3f); // Calm - blue
        }
        else if (windSpeed < 0.3f)
        {
            return Color.Lerp(baseColor, Color.Green, 0.5f); // Gentle - green
        }
        else if (windSpeed < 0.6f)
        {
            return Color.Lerp(baseColor, Color.Yellow, 0.6f); // Moderate - yellow
        }
        else if (windSpeed < 1.0f)
        {
            return Color.Lerp(baseColor, Color.Orange, 0.7f); // Strong - orange
        }
        else
        {
            return Color.Lerp(baseColor, Color.Red, 0.8f); // Extreme - red
        }
    }

    private Color GetPressureColor(TerrainCell cell)
    {
        var met = cell.GetMeteorology();

        // Map pressure to color gradient
        // Low pressure (< 0.4) = Blue (storms)
        // Normal pressure (0.4 - 0.6) = Green
        // High pressure (> 0.6) = Red (fair weather)

        if (met.AirPressure < 0.3f)
        {
            return new Color(0, 0, 200); // Deep low pressure
        }
        else if (met.AirPressure < 0.4f)
        {
            return new Color(50, 50, 255); // Low pressure
        }
        else if (met.AirPressure < 0.5f)
        {
            return new Color(100, 200, 100); // Normal-low
        }
        else if (met.AirPressure < 0.6f)
        {
            return new Color(100, 255, 100); // Normal-high
        }
        else if (met.AirPressure < 0.7f)
        {
            return new Color(255, 200, 100); // High pressure
        }
        else
        {
            return new Color(255, 100, 100); // Very high pressure
        }
    }

    private Color GetStormsColor(TerrainCell cell)
    {
        var met = cell.GetMeteorology();

        // Base terrain
        Color baseColor = GetTerrainColor(cell);

        // Show precipitation intensity
        if (met.Precipitation > 0.1f)
        {
            Color rainColor = new Color(100, 100, 200);
            baseColor = Color.Lerp(baseColor, rainColor, met.Precipitation);
        }

        // Highlight areas with high wind (storms)
        float windSpeed = MathF.Sqrt(met.WindSpeedX * met.WindSpeedX + met.WindSpeedY * met.WindSpeedY);
        if (windSpeed > 0.8f)
        {
            baseColor = Color.Lerp(baseColor, Color.Red, (windSpeed - 0.8f) * 2.0f);
        }

        // Show cloud cover for storm systems
        if (met.CloudCover > 0.7f)
        {
            baseColor = Color.Lerp(baseColor, new Color(80, 80, 80), (met.CloudCover - 0.7f));
        }

        return baseColor;
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
    Biomes
}
