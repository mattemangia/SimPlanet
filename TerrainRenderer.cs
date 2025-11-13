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
    public RenderMode Mode { get; set; } = RenderMode.Terrain;

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
        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                var cell = _map.Cells[x, y];
                int index = y * _map.Width + x;

                _terrainColors[index] = Mode switch
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
                    _ => Color.Black
                };
            }
        }

        _terrainTexture.SetData(_terrainColors);
    }

    public void Draw(SpriteBatch spriteBatch, int offsetX, int offsetY)
    {
        spriteBatch.Draw(
            _terrainTexture,
            new Rectangle(offsetX, offsetY, _map.Width * CellSize, _map.Height * CellSize),
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
    Volcanoes
}
