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
    private readonly object _mapDataLock;
    private readonly Action? _onMapModified;

    public bool IsActive { get; set; } = false;
    public PlantingType CurrentType { get; set; } = PlantingType.Forest;
    public int BrushSize { get; set; } = 3; // Radius of planting brush

    public ManualPlantingTool(PlanetMap map, GraphicsDevice graphicsDevice, FontRenderer font,
                              object mapDataLock, Action? onMapModified = null)
    {
        _map = map;
        _graphicsDevice = graphicsDevice;
        _font = font;
        _mapDataLock = mapDataLock;
        _onMapModified = onMapModified;

        _pixelTexture = new Texture2D(_graphicsDevice, 1, 1);
        _pixelTexture.SetData(new[] { Color.White });
    }

    public void Update(MouseState mouseState, int cellSize, float cameraX, float cameraY, float zoomLevel,
                      CivilizationManager civManager, int currentYear, int mapRenderOffsetX, int mapRenderOffsetY,
                      LifeSimulator lifeSimulator = null, PlanetStabilizer planetStabilizer = null)
    {
        if (!IsActive)
        {
            _previousMouseState = mouseState;
            return;
        }

        bool clicked = mouseState.LeftButton == ButtonState.Released &&
                      _previousMouseState.LeftButton == ButtonState.Pressed;

        // Check UI button clicks first (panel on left side)
        int screenWidth = _graphicsDevice.Viewport.Width;
        int screenHeight = _graphicsDevice.Viewport.Height;
        int panelX = 10;
        int panelY = screenHeight / 2 - 200;

        if (clicked)
        {
            // Type selection buttons (8 buttons)
            int buttonY = panelY + 65;
            int buttonHeight = 22;
            int buttonWidth = 180;
            int buttonSpacing = 2;

            PlantingType[] types = new[] { PlantingType.Forest, PlantingType.Grass, PlantingType.Desert,
                                          PlantingType.Tundra, PlantingType.Ocean, PlantingType.Mountain,
                                          PlantingType.Fault, PlantingType.Civilization };

            for (int i = 0; i < types.Length; i++)
            {
                Rectangle buttonRect = new Rectangle(panelX + 10, buttonY + i * (buttonHeight + buttonSpacing), buttonWidth, buttonHeight);
                if (buttonRect.Contains(mouseState.Position))
                {
                    CurrentType = types[i];
                    _previousMouseState = mouseState;
                    return; // Don't plant when clicking UI
                }
            }

            // Brush size buttons
            int brushButtonY = panelY + 45;
            Rectangle minusButton = new Rectangle(panelX + 95, brushButtonY, 25, 20);
            Rectangle plusButton = new Rectangle(panelX + 165, brushButtonY, 25, 20);

            if (minusButton.Contains(mouseState.Position))
            {
                BrushSize = Math.Max(BrushSize - 1, 1);
                _previousMouseState = mouseState;
                return;
            }
            else if (plusButton.Contains(mouseState.Position))
            {
                BrushSize = Math.Min(BrushSize + 1, 15);
                _previousMouseState = mouseState;
                return;
            }

            // Only plant if not clicking UI panel area
            Rectangle panelRect = new Rectangle(panelX, panelY, 200, 265);
            if (!panelRect.Contains(mouseState.Position))
            {
                // Convert screen coordinates to map coordinates
                float mapRelativeX = (mouseState.X - mapRenderOffsetX) + cameraX;
                float mapRelativeY = (mouseState.Y - mapRenderOffsetY) + cameraY;
                int tileX = (int)(mapRelativeX / (cellSize * zoomLevel));
                int tileY = (int)(mapRelativeY / (cellSize * zoomLevel));

                if (tileX >= 0 && tileX < _map.Width && tileY >= 0 && tileY < _map.Height)
                {
                    PlantAt(tileX, tileY, civManager, currentYear, lifeSimulator, planetStabilizer);
                }
            }
        }

        _previousMouseState = mouseState;
    }

    private void PlantAt(int x, int y, CivilizationManager civManager, int currentYear, LifeSimulator lifeSimulator, PlanetStabilizer planetStabilizer)
    {
        lock (_mapDataLock)
        {
            // Store the cell types that were planted
            bool plantedLife = false;
            
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
                            plantedLife = true;
                            break;
                        case PlantingType.Grass:
                            PlantGrass(cell);
                            plantedLife = true;
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
                            {
                                PlantCivilization(cell, nx, ny, civManager, currentYear);
                                plantedLife = true;
                            }
                            break;
                    }
                }
            }
            
            // If we planted life, activate all protection systems
            if (plantedLife)
            {
                // Activate grace period in life simulator
                if (lifeSimulator != null)
                {
                    lifeSimulator.ActivatePlantingGracePeriod();
                }
                
                // Activate emergency mode in planet stabilizer
                if (planetStabilizer != null)
                {
                    planetStabilizer.ActivateEmergencyLifeProtection();
                }
                
                Console.WriteLine($"[ManualPlantingTool] Planted {CurrentType} at ({x}, {y}) with full protection activated");
            }
        }

        _onMapModified?.Invoke();
    }

    private void PlantForest(TerrainCell cell)
    {
        if (!cell.IsLand) return;

        // Create forest conditions with proper environmental parameters
        cell.LifeType = LifeForm.PlantLife;
        cell.Biomass = Math.Min(cell.Biomass + 0.4f, 0.9f); // Max 90% biomass
        cell.Rainfall = Math.Max(cell.Rainfall, 0.6f); // Ensure enough rain for forest
        cell.Temperature = Math.Clamp(cell.Temperature, 5, 30); // Temperate conditions
        
        // Ensure minimum oxygen for plant survival (plants need some O2 for respiration)
        if (cell.Oxygen < 10f)
        {
            cell.Oxygen = 15f; // Set minimum oxygen level
        }
        
        // Ensure CO2 is not too high
        if (cell.CO2 > 5f)
        {
            cell.CO2 = 2f; // Reasonable CO2 level
        }

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
        
        // Ensure minimum oxygen
        if (cell.Oxygen < 10f)
        {
            cell.Oxygen = 15f;
        }
        
        // Ensure reasonable CO2
        if (cell.CO2 > 5f)
        {
            cell.CO2 = 2f;
        }

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
        if (civManager != null && civManager.TryCreateCivilizationAt(x, y, currentYear))
        {
            return;
        }

        if (!cell.IsLand) return;
        if (cell.Temperature < -10 || cell.Temperature > 45) return; // Uninhabitable
        
        // Ensure proper oxygen level for civilization
        if (cell.Oxygen < 15) // Reduced from 18
        {
            cell.Oxygen = 18f; // Set to minimum civilization requirement
        }

        if (civManager != null)
        {
            // Check if civilization already exists nearby
            foreach (var civ in civManager.Civilizations)
            {
                if (Math.Abs(civ.CenterX - x) < 20 && Math.Abs(civ.CenterY - y) < 20)
                    return; // Too close to existing civilization
            }
        }

        // Ensure good conditions for civilization
        cell.LifeType = LifeForm.Civilization;
        cell.Biomass = 0.5f;
        cell.Rainfall = Math.Max(cell.Rainfall, 0.3f); // Ensure water
        cell.Temperature = Math.Clamp(cell.Temperature, 0, 35); // Livable temperature
        
        // Ensure reasonable CO2
        if (cell.CO2 > 5f)
        {
            cell.CO2 = 2f;
        }

        // Note: CivilizationManager will need a method to spawn a civilization at coordinates
        // For now, we just mark the cell
    }

    public void Draw(SpriteBatch spriteBatch, int screenWidth, int screenHeight)
    {
        if (!IsActive) return;

        int panelX = 10;  // Left side
        int panelY = screenHeight / 2 - 200;  // Vertically centered
        int panelWidth = 200;
        int panelHeight = 265; // 8 buttons * 24px + margins

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

        // Brush size with +/- buttons
        _font.DrawString(spriteBatch, $"Brush: ",
            new Vector2(panelX + 10, textY), Color.White);

        // - button
        Rectangle minusBtn = new Rectangle(panelX + 95, textY, 25, 20);
        spriteBatch.Draw(_pixelTexture, minusBtn, new Color(80, 80, 80));
        DrawBorder(spriteBatch, minusBtn.X, minusBtn.Y, minusBtn.Width, minusBtn.Height, Color.White, 1);
        _font.DrawString(spriteBatch, "-", new Vector2(minusBtn.X + 8, minusBtn.Y + 2), Color.White);

        // Size display
        _font.DrawString(spriteBatch, $"{BrushSize}",
            new Vector2(panelX + 125, textY), Color.Yellow);

        // + button
        Rectangle plusBtn = new Rectangle(panelX + 165, textY, 25, 20);
        spriteBatch.Draw(_pixelTexture, plusBtn, new Color(80, 80, 80));
        DrawBorder(spriteBatch, plusBtn.X, plusBtn.Y, plusBtn.Width, plusBtn.Height, Color.White, 1);
        _font.DrawString(spriteBatch, "+", new Vector2(plusBtn.X + 7, plusBtn.Y + 2), Color.White);

        textY += lineHeight + 10;

        // Instructions
        _font.DrawString(spriteBatch, "SELECT TYPE:",
            new Vector2(panelX + 10, textY), Color.Yellow);
        textY += lineHeight;

        // Type selection buttons
        PlantingType[] types = new[] { PlantingType.Forest, PlantingType.Grass, PlantingType.Desert,
                                      PlantingType.Tundra, PlantingType.Ocean, PlantingType.Mountain,
                                      PlantingType.Fault, PlantingType.Civilization };
        int buttonHeight = 22;
        int buttonWidth = 180;
        int buttonSpacing = 2;

        for (int i = 0; i < types.Length; i++)
        {
            Rectangle buttonRect = new Rectangle(panelX + 10, textY, buttonWidth, buttonHeight);

            // Button background (highlight if selected)
            Color bgColor = types[i] == CurrentType ? new Color(100, 200, 100) : new Color(60, 60, 60);
            spriteBatch.Draw(_pixelTexture, buttonRect, bgColor);

            // Button border
            Color borderColor = types[i] == CurrentType ? Color.LightGreen : Color.Gray;
            DrawBorder(spriteBatch, buttonRect.X, buttonRect.Y, buttonRect.Width, buttonRect.Height, borderColor, 1);

            // Button text
            Color textColor = types[i] == CurrentType ? Color.White : Color.LightGray;
            _font.DrawString(spriteBatch, types[i].ToString(),
                new Vector2(buttonRect.X + 5, buttonRect.Y + 4), textColor);

            textY += buttonHeight + buttonSpacing;
        }

        textY += 5;
        _font.DrawString(spriteBatch, "T: Toggle Tool",
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