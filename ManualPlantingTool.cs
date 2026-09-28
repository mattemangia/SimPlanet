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
                      LifeSimulator? lifeSimulator = null, PlanetStabilizer? planetStabilizer = null)
    {
        if (!IsActive)
        {
            _previousMouseState = mouseState;
            return;
        }

        bool clicked = mouseState.LeftButton == ButtonState.Released &&
                      _previousMouseState.LeftButton == ButtonState.Pressed;

        // Check UI button clicks first (same layout as Draw)
        var layout = GetLayout();

        if (clicked)
        {
            for (int i = 0; i < PlantingTypes.Length; i++)
            {
                if (layout.TypeButtons[i].Contains(mouseState.Position))
                {
                    CurrentType = PlantingTypes[i];
                    _previousMouseState = mouseState;
                    return; // Don't plant when clicking UI
                }
            }

            if (layout.Minus.Contains(mouseState.Position))
            {
                BrushSize = Math.Max(BrushSize - 1, 1);
                _previousMouseState = mouseState;
                return;
            }
            else if (layout.Plus.Contains(mouseState.Position))
            {
                BrushSize = Math.Min(BrushSize + 1, 15);
                _previousMouseState = mouseState;
                return;
            }

            // Only plant if not clicking UI panel area
            if (!layout.Panel.Contains(mouseState.Position))
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

    private void PlantAt(int x, int y, CivilizationManager civManager, int currentYear, LifeSimulator? lifeSimulator, PlanetStabilizer? planetStabilizer)
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

    private static readonly PlantingType[] PlantingTypes =
    {
        PlantingType.Forest, PlantingType.Grass, PlantingType.Desert, PlantingType.Tundra,
        PlantingType.Ocean, PlantingType.Mountain, PlantingType.Fault, PlantingType.Civilization
    };

    private static readonly Color[] PlantingSwatches =
    {
        new Color(40, 120, 50), new Color(110, 180, 80), new Color(220, 190, 120), new Color(150, 160, 150),
        new Color(40, 100, 180), new Color(130, 120, 110), new Color(200, 60, 50), new Color(255, 205, 90)
    };

    private readonly struct PanelLayout
    {
        public Rectangle Panel { get; init; }
        public Rectangle Minus { get; init; }
        public Rectangle Plus { get; init; }
        public Rectangle[] TypeButtons { get; init; }
        public int BrushRowY { get; init; }
        public int TypesLabelY { get; init; }
        public int FooterY { get; init; }
    }

    /// <summary>Single source of truth for the panel geometry (used by Update and Draw).</summary>
    private PanelLayout GetLayout()
    {
        const int width = 214;
        const int rowH = 26, gap = 3;
        int x = 290; // right of the info panel
        int y = 56;  // below the toolbar
        int brushY = y + 44;
        int typesLabelY = brushY + 34;
        int typesY = typesLabelY + 22;
        var buttons = new Rectangle[PlantingTypes.Length];
        for (int i = 0; i < buttons.Length; i++)
            buttons[i] = new Rectangle(x + 12, typesY + i * (rowH + gap), width - 24, rowH);
        int footerY = typesY + buttons.Length * (rowH + gap) + 6;
        return new PanelLayout
        {
            Panel = new Rectangle(x, y, width, footerY - y + 28),
            Minus = new Rectangle(x + width - 94, brushY - 3, 26, 24),
            Plus = new Rectangle(x + width - 38, brushY - 3, 26, 24),
            TypeButtons = buttons,
            BrushRowY = brushY,
            TypesLabelY = typesLabelY,
            FooterY = footerY
        };
    }

    public void Draw(SpriteBatch spriteBatch, int screenWidth, int screenHeight)
    {
        if (!IsActive) return;

        var layout = GetLayout();
        var mouse = Mouse.GetState();

        UITheme.DrawTitledPanel(spriteBatch, layout.Panel, "PLANTING TOOL", UITheme.Good);

        // Brush size with +/- buttons
        UITheme.DrawText(spriteBatch, "Brush size", new Vector2(layout.Panel.X + 12, layout.BrushRowY), UITheme.TextDim);
        UITheme.DrawButton(spriteBatch, layout.Minus, "-", layout.Minus.Contains(mouse.Position));
        UITheme.DrawButton(spriteBatch, layout.Plus, "+", layout.Plus.Contains(mouse.Position));
        var valueRect = new Rectangle(layout.Minus.Right, layout.Minus.Y, layout.Plus.X - layout.Minus.Right, layout.Minus.Height);
        UITheme.DrawTextCentered(spriteBatch, BrushSize.ToString(), valueRect, UITheme.Gold, UITheme.FontMedium);

        UITheme.DrawText(spriteBatch, "What to plant", new Vector2(layout.Panel.X + 12, layout.TypesLabelY), UITheme.TextDim);

        for (int i = 0; i < PlantingTypes.Length; i++)
        {
            var rect = layout.TypeButtons[i];
            bool selected = PlantingTypes[i] == CurrentType;
            bool hovered = rect.Contains(mouse.Position);
            UITheme.DrawButton(spriteBatch, rect, "", hovered, selected, UITheme.Good);
            spriteBatch.Draw(_pixelTexture, new Rectangle(rect.X + 8, rect.Y + 7, 12, 12), PlantingSwatches[i]);
            string label = PlantingTypes[i] == PlantingType.Civilization ? "Civilization (seed)" : PlantingTypes[i].ToString();
            UITheme.DrawTextShadowed(spriteBatch, label, new Vector2(rect.X + 28, rect.Y + 5), selected ? Color.White : UITheme.Text);
        }

        UITheme.DrawText(spriteBatch, "Click on the map to plant", new Vector2(layout.Panel.X + 12, layout.FooterY), UITheme.TextMuted, UITheme.FontSmall);
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