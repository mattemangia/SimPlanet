using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;

namespace SimPlanet;

/// <summary>
/// Tool for manually planting vegetation, terraforming, and seeding civilizations
/// </summary>
public class ManualPlantingTool
{
    private readonly PlanetMap _map;
    private readonly GraphicsDevice _graphicsDevice;
    private readonly FontRenderer _font;
    private Texture2D _pixelTexture;
    private MouseState _previousMouseState;

    public bool IsActive { get; set; } = false;
    public PlantingType CurrentType { get; set; } = PlantingType.Forest;
    public int BrushSize { get; set; } = 3; // Radius of planting brush

    public ManualPlantingTool(PlanetMap map, GraphicsDevice graphicsDevice, FontRenderer font)
    {
        _map = map;
        _graphicsDevice = graphicsDevice;
        _font = font;

        _pixelTexture = new Texture2D(_graphicsDevice, 1, 1);
        _pixelTexture.SetData(new[] { Color.White });
    }

    public void Update(MouseState mouseState, int cellSize, float cameraX, float cameraY, float zoomLevel,
                      CivilizationManager civManager, int currentYear, int mapRenderOffsetX, int mapRenderOffsetY)
    {
        if (!IsActive)
        {
            _previousMouseState = mouseState;
            return;
        }

        bool clicked = mouseState.LeftButton == ButtonState.Pressed &&
                      _previousMouseState.LeftButton == ButtonState.Released;

        // Adjust brush size with mouse wheel
        int scrollDelta = mouseState.ScrollWheelValue - _previousMouseState.ScrollWheelValue;
        if (scrollDelta > 0)
            BrushSize = Math.Min(BrushSize + 1, 15);
        else if (scrollDelta < 0)
            BrushSize = Math.Max(BrushSize - 1, 1);

        if (clicked)
        {
            // Convert screen coordinates to map coordinates
            float mapRelativeX = (mouseState.X - mapRenderOffsetX) + cameraX;
            float mapRelativeY = (mouseState.Y - mapRenderOffsetY) + cameraY;
            int tileX = (int)(mapRelativeX / (cellSize * zoomLevel));
            int tileY = (int)(mapRelativeY / (cellSize * zoomLevel));

            if (tileX >= 0 && tileX < _map.Width && tileY >= 0 && tileY < _map.Height)
            {
                PlantAt(tileX, tileY, civManager, currentYear);
            }
        }

        _previousMouseState = mouseState;
    }

    private void PlantAt(int x, int y, CivilizationManager civManager, int currentYear)
    {
        // Plant in a circular area based on brush size
        for (int dx = -BrushSize; dx <= BrushSize; dx++)
        {
            for (int dy = -BrushSize; dy <= BrushSize; dy++)
            {
                int nx = x + dx;
                int ny = y + dy;

                // Check bounds
                if (nx < 0 || nx >= _map.Width || ny < 0 || ny >= _map.Height)
                    continue;

                // Check circular brush
                float dist = MathF.Sqrt(dx * dx + dy * dy);
                if (dist > BrushSize)
                    continue;

                var cell = _map.Cells[nx, ny];

                switch (CurrentType)
                {
                    case PlantingType.Forest:
                        PlantForest(cell);
                        break;
                    case PlantingType.Grass:
                        PlantGrass(cell);
                        break;
                    case PlantingType.Desert:
                        PlantDesert(cell);
                        break;
                    case PlantingType.Tundra:
                        PlantTundra(cell);
                        break;
                    case PlantingType.Ocean:
                        CreateOcean(cell);
                        break;
                    case PlantingType.Mountain:
                        CreateMountain(cell);
                        break;
                    case PlantingType.Fault:
                        CreateFault(cell, nx, ny);
                        break;
                    case PlantingType.Civilization:
                        if (dx == 0 && dy == 0) // Only center cell for civilization
                            PlantCivilization(cell, nx, ny, civManager, currentYear);
                        break;
                }
            }
        }
    }

    private void PlantForest(TerrainCell cell)
    {
        if (!cell.IsLand) return;

        // Create forest conditions
        cell.LifeType = LifeForm.PlantLife;
        cell.Biomass = Math.Min(cell.Biomass + 0.4f, 0.9f); // Max 90% biomass
        cell.Rainfall = Math.Max(cell.Rainfall, 0.6f); // Ensure enough rain for forest
        cell.Temperature = Math.Clamp(cell.Temperature, 5, 30); // Temperate conditions

        // Set biome
        var biomeData = cell.GetBiomeData();
        if (cell.Temperature > 25 && cell.Rainfall > 0.7f)
            biomeData.CurrentBiome = Biome.TropicalRainforest;
        else if (cell.Temperature < 10)
            biomeData.CurrentBiome = Biome.BorealForest;
        else
            biomeData.CurrentBiome = Biome.TemperateForest;
    }

    private void PlantGrass(TerrainCell cell)
    {
        if (!cell.IsLand) return;

        cell.LifeType = LifeForm.PlantLife;
        cell.Biomass = Math.Min(cell.Biomass + 0.3f, 0.5f); // Moderate biomass
        cell.Rainfall = Math.Max(cell.Rainfall, 0.3f); // Ensure some rain

        var biomeData = cell.GetBiomeData();
        if (cell.Temperature > 20)
            biomeData.CurrentBiome = Biome.Savanna;
        else
            biomeData.CurrentBiome = Biome.Grassland;
    }

    private void PlantDesert(TerrainCell cell)
    {
        if (!cell.IsLand) return;

        cell.LifeType = LifeForm.None;
        cell.Biomass = Math.Max(cell.Biomass - 0.5f, 0.05f); // Very low biomass
        cell.Rainfall = 0.1f; // Very dry
        cell.Temperature = Math.Max(cell.Temperature, 25); // Hot

        var biomeData = cell.GetBiomeData();
        biomeData.CurrentBiome = Biome.Desert;
    }

    private void PlantTundra(TerrainCell cell)
    {
        if (!cell.IsLand) return;

        cell.LifeType = LifeForm.PlantLife;
        cell.Biomass = 0.2f; // Low biomass
        cell.Temperature = -5; // Cold
        cell.Rainfall = 0.3f;

        var biomeData = cell.GetBiomeData();
        biomeData.CurrentBiome = Biome.Tundra;
    }

    private void CreateOcean(TerrainCell cell)
    {
        cell.Elevation = -0.6f; // Deep water
        cell.Temperature = 15; // Moderate ocean temp
        cell.LifeType = LifeForm.Algae;
        cell.Biomass = 0.3f;
    }

    private void CreateMountain(TerrainCell cell)
    {
        cell.Elevation += 0.3f; // Raise elevation
        cell.Elevation = Math.Clamp(cell.Elevation, -1.0f, 1.0f);

        if (cell.Elevation > 0.7f)
        {
            var biomeData = cell.GetBiomeData();
            biomeData.CurrentBiome = cell.Temperature < 0 ? Biome.AlpineTundra : Biome.Mountain;
            cell.Biomass = 0.1f; // Rocky, little vegetation
        }
    }

    private void CreateFault(TerrainCell cell, int x, int y)
    {
        var geo = cell.GetGeology();

        // Create a fault line at this location
        geo.IsFault = true;

        // Randomly assign fault type (or use a pattern based on elevation)
        var random = new Random((x * _map.Height + y) * 137); // Deterministic random based on position
        geo.FaultType = (FaultType)random.Next(1, 6); // Skip None (0)

        // Set fault activity
        geo.FaultActivity = 0.5f + (float)random.NextDouble() * 0.5f; // 0.5-1.0

        // Increase seismic stress at fault locations
        geo.SeismicStress = 0.3f + (float)random.NextDouble() * 0.4f; // Start with some stress

        // Optionally set plate boundary type to match fault
        if (geo.BoundaryType == PlateBoundaryType.None)
        {
            geo.BoundaryType = geo.FaultType switch
            {
                FaultType.Strike_Slip => PlateBoundaryType.Transform,
                FaultType.Normal => PlateBoundaryType.Divergent,
                FaultType.Reverse or FaultType.Thrust => PlateBoundaryType.Convergent,
                _ => PlateBoundaryType.None
            };
        }
    }

    private void PlantCivilization(TerrainCell cell, int x, int y, CivilizationManager civManager, int currentYear)
    {
        if (!cell.IsLand) return;
        if (cell.Temperature < -10 || cell.Temperature > 45) return; // Uninhabitable
        if (cell.Oxygen < 18) return; // Not enough oxygen

        // Check if civilization already exists nearby
        foreach (var civ in civManager.Civilizations)
        {
            if (Math.Abs(civ.CenterX - x) < 20 && Math.Abs(civ.CenterY - y) < 20)
                return; // Too close to existing civilization
        }

        // Ensure good conditions for civilization
        cell.LifeType = LifeForm.Civilization;
        cell.Biomass = 0.5f;
        cell.Rainfall = Math.Max(cell.Rainfall, 0.3f); // Ensure water
        cell.Temperature = Math.Clamp(cell.Temperature, 0, 35); // Livable temperature

        // Note: CivilizationManager will need a method to spawn a civilization at coordinates
        // For now, we just mark the cell
    }

    public void Draw(SpriteBatch spriteBatch, int screenWidth, int screenHeight)
    {
        if (!IsActive) return;

        int panelX = screenWidth - 220;
        int panelY = screenHeight - 280;
        int panelWidth = 210;
        int panelHeight = 270;

        // Background
        spriteBatch.Draw(_pixelTexture,
            new Rectangle(panelX, panelY, panelWidth, panelHeight),
            new Color(20, 40, 20, 230));

        // Border
        DrawBorder(spriteBatch, panelX, panelY, panelWidth, panelHeight, Color.Green, 2);

        // Title
        _font.DrawString(spriteBatch, "PLANTING TOOL",
            new Vector2(panelX + 40, panelY + 5), Color.LightGreen);

        int textY = panelY + 30;
        int lineHeight = 20;

        // Current type
        _font.DrawString(spriteBatch, $"Type: {CurrentType}",
            new Vector2(panelX + 10, textY), Color.White);
        textY += lineHeight;

        _font.DrawString(spriteBatch, "(T to cycle)",
            new Vector2(panelX + 10, textY), Color.Gray);
        textY += lineHeight + 5;

        // Brush size
        _font.DrawString(spriteBatch, $"Brush: {BrushSize}",
            new Vector2(panelX + 10, textY), Color.White);
        textY += lineHeight;

        _font.DrawString(spriteBatch, "(Scroll wheel)",
            new Vector2(panelX + 10, textY), Color.Gray);
        textY += lineHeight + 10;

        // Instructions
        _font.DrawString(spriteBatch, "TYPES:",
            new Vector2(panelX + 10, textY), Color.Yellow);
        textY += lineHeight;

        string[] types = new[] { "Forest", "Grass", "Desert", "Tundra", "Ocean", "Mountain", "Fault", "Civilization" };
        foreach (var type in types)
        {
            Color color = type == CurrentType.ToString() ? Color.LightGreen : Color.Gray;
            _font.DrawString(spriteBatch, $"- {type}",
                new Vector2(panelX + 15, textY), color);
            textY += 16;
        }

        textY += 5;
        _font.DrawString(spriteBatch, "P: Toggle Tool",
            new Vector2(panelX + 10, textY), Color.Yellow);
    }

    private void DrawBorder(SpriteBatch spriteBatch, int x, int y, int width, int height, Color color, int thickness)
    {
        spriteBatch.Draw(_pixelTexture, new Rectangle(x, y, width, thickness), color);
        spriteBatch.Draw(_pixelTexture, new Rectangle(x, y + height - thickness, width, thickness), color);
        spriteBatch.Draw(_pixelTexture, new Rectangle(x, y, thickness, height), color);
        spriteBatch.Draw(_pixelTexture, new Rectangle(x + width - thickness, y, thickness, height), color);
    }

    public void CycleType()
    {
        CurrentType = CurrentType switch
        {
            PlantingType.Forest => PlantingType.Grass,
            PlantingType.Grass => PlantingType.Desert,
            PlantingType.Desert => PlantingType.Tundra,
            PlantingType.Tundra => PlantingType.Ocean,
            PlantingType.Ocean => PlantingType.Mountain,
            PlantingType.Mountain => PlantingType.Fault,
            PlantingType.Fault => PlantingType.Civilization,
            PlantingType.Civilization => PlantingType.Forest,
            _ => PlantingType.Forest
        };
    }
}

public enum PlantingType
{
    Forest,
    Grass,
    Desert,
    Tundra,
    Ocean,
    Mountain,
    Fault,
    Civilization
}
