using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace SimPlanet;

/// <summary>
/// Displays the sediment column for a selected tile
/// </summary>
public class SedimentColumnViewer
{
    private readonly GraphicsDevice _graphicsDevice;
    private readonly FontRenderer _font;
    private readonly PlanetMap _map;
    private CivilizationManager? _civManager;
    private Texture2D _pixelTexture;
    private (int x, int y)? _selectedTile = null;
    private MouseState _previousMouseState;
    private Point? _mouseDownPosition = null;
    private const int DragThreshold = 25; // pixels (increased for trackpad users - more forgiving)
    private const int InfoPanelWidth = 280; // Don't open viewer when clicking in info panel
    private int _scrollOffset = 0; // For scrolling through sediment layers
    private const int ScrollSpeed = 20;

    public bool IsVisible { get; private set; } = false;

    public SedimentColumnViewer(GraphicsDevice graphicsDevice, FontRenderer font, PlanetMap map)
    {
        _graphicsDevice = graphicsDevice;
        _font = font;
        _map = map;

        _pixelTexture = new Texture2D(_graphicsDevice, 1, 1);
        _pixelTexture.SetData(new[] { Color.White });
    }

    public void SetCivilizationManager(CivilizationManager civManager)
    {
        _civManager = civManager;
    }

    public void Update(MouseState mouseState, int cellSize, float cameraX, float cameraY, float zoomLevel, int mapRenderOffsetX, int mapRenderOffsetY, bool toolsActive = false)
    {
        int screenWidth = _graphicsDevice.Viewport.Width;
        int panelWidth = 400;
        int panelX = screenWidth - panelWidth - 10;
        int panelY = 40; // Below toolbar (36px high)

        // Check for close button click (X in top right)
        if (IsVisible && mouseState.LeftButton == ButtonState.Released &&
            _previousMouseState.LeftButton == ButtonState.Pressed)
        {
            Rectangle closeButtonBounds = new Rectangle(panelX + panelWidth - 30, panelY + 5, 25, 25);
            if (closeButtonBounds.Contains(mouseState.Position))
            {
                IsVisible = false;
                _selectedTile = null;
                _previousMouseState = mouseState;
                return;
            }
        }

        // Track mouse down position to detect drags
        if (mouseState.LeftButton == ButtonState.Pressed &&
            _previousMouseState.LeftButton == ButtonState.Released)
        {
            _mouseDownPosition = mouseState.Position;
        }

        // Check for left mouse click on map (trigger on release, and only if not dragging)
        // Allow clicking to update tile even when panel is visible
        if (mouseState.LeftButton == ButtonState.Released &&
            _previousMouseState.LeftButton == ButtonState.Pressed &&
            _mouseDownPosition.HasValue)
        {
            // Calculate distance moved since mouse down
            int dragDistance = (int)Vector2.Distance(
                new Vector2(_mouseDownPosition.Value.X, _mouseDownPosition.Value.Y),
                new Vector2(mouseState.Position.X, mouseState.Position.Y)
            );

            // Only open/update viewer if this was a click, not a drag
            if (dragDistance <= DragThreshold)
            {
                // Don't open if clicking in the info panel area or in the viewer panel
                if (mouseState.X < InfoPanelWidth)
                {
                    _mouseDownPosition = null;
                }
                else if (IsVisible)
                {
                    // Check if clicking inside the viewer panel
                    // Use existing panelWidth, panelX, panelY variables from outer scope
                    int panelHeight = _graphicsDevice.Viewport.Height - 20; // Use almost full height
                    Rectangle panelBounds = new Rectangle(panelX, panelY, panelWidth, panelHeight);

                    if (!panelBounds.Contains(mouseState.Position))
                    {
                        // Clicking outside panel - update to new tile
                        float mapRelativeX = (mouseState.X - mapRenderOffsetX) + cameraX;
                        float mapRelativeY = (mouseState.Y - mapRenderOffsetY) + cameraY;
                        int tileX = (int)(mapRelativeX / (cellSize * zoomLevel));
                        int tileY = (int)(mapRelativeY / (cellSize * zoomLevel));

                        if (tileX >= 0 && tileX < _map.Width && tileY >= 0 && tileY < _map.Height)
                        {
                            _selectedTile = (tileX, tileY);
                            _scrollOffset = 0; // Reset scroll when changing tile
                        }
                    }
                    _mouseDownPosition = null;
                }
                else
                {
                    // Convert mouse position to tile coordinates
                    // 1. Subtract map render offset to get map-relative coordinates
                    // 2. Add camera offset to account for panning
                    // 3. Divide by (cellSize * zoomLevel) to get tile index
                    float mapRelativeX = (mouseState.X - mapRenderOffsetX) + cameraX;
                    float mapRelativeY = (mouseState.Y - mapRenderOffsetY) + cameraY;
                    int tileX = (int)(mapRelativeX / (cellSize * zoomLevel));
                    int tileY = (int)(mapRelativeY / (cellSize * zoomLevel));

                    // Check if tile is within bounds
                    // Don't open tile info panel when tools are active (they need map clicks)
                    if (tileX >= 0 && tileX < _map.Width && tileY >= 0 && tileY < _map.Height && !toolsActive)
                    {
                        _selectedTile = (tileX, tileY);
                        IsVisible = true;
                        _scrollOffset = 0;
                    }
                }
            }
            _mouseDownPosition = null;
        }

        // Reset mouse down position if button is released
        if (mouseState.LeftButton == ButtonState.Released)
        {
            _mouseDownPosition = null;
        }

        // Handle scroll wheel for sediment column scrolling
        if (IsVisible)
        {
            int scrollDelta = mouseState.ScrollWheelValue - _previousMouseState.ScrollWheelValue;
            if (scrollDelta != 0)
            {
                _scrollOffset -= (scrollDelta / 120) * ScrollSpeed; // Each wheel "click" is 120 units
                _scrollOffset = Math.Max(0, _scrollOffset); // Can't scroll above top
            }
        }

        // Close with right click or ESC
        if (mouseState.RightButton == ButtonState.Pressed || Keyboard.GetState().IsKeyDown(Keys.Escape))
        {
            IsVisible = false;
            _selectedTile = null;
            _scrollOffset = 0;
        }

        _previousMouseState = mouseState;
    }

    public void Draw(SpriteBatch spriteBatch, int screenWidth, int screenHeight)
    {
        if (!IsVisible || !_selectedTile.HasValue) return;

        var (x, y) = _selectedTile.Value;
        var cell = _map.Cells[x, y];
        var geo = cell.GetGeology();

        // Panel dimensions - use almost full screen height
        int panelWidth = 400;
        int panelHeight = screenHeight - 50; // Account for toolbar (36px) + margins
        int panelX = screenWidth - panelWidth - 10;
        int panelY = 40; // Below toolbar (36px high)

        // Draw background
        spriteBatch.Draw(_pixelTexture,
            new Rectangle(panelX, panelY, panelWidth, panelHeight),
            new Color(0, 0, 0, 220));

        // Draw border
        DrawBorder(spriteBatch, panelX, panelY, panelWidth, panelHeight, Color.White, 2);

        // Draw close button (X) in top right
        Rectangle closeButtonBounds = new Rectangle(panelX + panelWidth - 30, panelY + 5, 25, 25);
        spriteBatch.Draw(_pixelTexture, closeButtonBounds, new Color(180, 0, 0, 200));
        _font.DrawString(spriteBatch, "X", new Vector2(closeButtonBounds.X + 7, closeButtonBounds.Y + 3), Color.White, 16);

        // Set up clipping rectangle for scrollable content
        Rectangle scissorRect = new Rectangle(panelX, panelY + 35, panelWidth, panelHeight - 70);
        Rectangle oldScissorRect = spriteBatch.GraphicsDevice.ScissorRectangle;
        RasterizerState oldRasterizer = spriteBatch.GraphicsDevice.RasterizerState;

        RasterizerState rasterizerState = new RasterizerState { ScissorTestEnable = true };

        int textY = panelY + 40 - _scrollOffset; // Apply scroll offset
        int lineHeight = 20;
        int contentStartY = textY; // Track where content starts

        // Enable scissor test for scrollable content
        spriteBatch.End();
        spriteBatch.GraphicsDevice.ScissorRectangle = scissorRect;
        spriteBatch.Begin(SpriteSortMode.Deferred, null, null, null, rasterizerState);

        // Title
        _font.DrawString(spriteBatch, $"TILE INFO ({x},{y})",
            new Vector2(panelX + 10, textY), Color.Yellow);
        textY += lineHeight + 5;

        // === BIOME & TERRAIN ===
        _font.DrawString(spriteBatch, "=== TERRAIN ===",
            new Vector2(panelX + 10, textY), Color.Orange);
        textY += lineHeight;

        string biomeType = GetBiomeType(cell);
        Color biomeColor = GetBiomeColor(cell);
        _font.DrawString(spriteBatch, $"Biome: {biomeType}",
            new Vector2(panelX + 10, textY), biomeColor);
        textY += lineHeight;

        _font.DrawString(spriteBatch, $"Elevation: {cell.Elevation:F2}",
            new Vector2(panelX + 10, textY), Color.White);
        textY += lineHeight;

        _font.DrawString(spriteBatch, $"Temperature: {cell.Temperature:F1}°C",
            new Vector2(panelX + 10, textY), GetTempColor(cell.Temperature));
        textY += lineHeight;

        _font.DrawString(spriteBatch, $"Rainfall: {cell.Rainfall:F2}",
            new Vector2(panelX + 10, textY), GetRainfallColor(cell.Rainfall));
        textY += lineHeight + 3;

        // === LIFE ===
        if (cell.Biomass > 0.01f)
        {
            _font.DrawString(spriteBatch, "=== LIFE ===",
                new Vector2(panelX + 10, textY), Color.LightGreen);
            textY += lineHeight;

            _font.DrawString(spriteBatch, $"Type: {cell.LifeType}",
                new Vector2(panelX + 10, textY), Color.White);
            textY += lineHeight;

            _font.DrawString(spriteBatch, $"Biomass: {cell.Biomass:F2}",
                new Vector2(panelX + 10, textY), Color.Green);
            textY += lineHeight + 3;
        }

        // === CIVILIZATION ===
        if (_civManager != null)
        {
            // Find civilization that owns this tile
            var owningCiv = _civManager.Civilizations.FirstOrDefault(c => c.Territory.Contains((x, y)));

            // Also check if there's a city at this location
            var cityAtTile = _civManager.Civilizations
                .SelectMany(c => c.Cities)
                .FirstOrDefault(city => city.X == x && city.Y == y);

            if (owningCiv != null || cityAtTile != null)
            {
                var civ = owningCiv ?? _civManager.Civilizations.First(c => c.Id == cityAtTile!.CivilizationId);

                _font.DrawString(spriteBatch, "=== CIVILIZATION ===",
                    new Vector2(panelX + 10, textY), Color.Gold);
                textY += lineHeight;

                _font.DrawString(spriteBatch, $"Name: {civ.Name}",
                    new Vector2(panelX + 10, textY), Color.Yellow);
                textY += lineHeight;

                _font.DrawString(spriteBatch, $"Population: {civ.Population / 1000}K",
                    new Vector2(panelX + 10, textY), Color.White);
                textY += lineHeight;

                _font.DrawString(spriteBatch, $"Tech Level: {civ.TechLevel} ({civ.CivType})",
                    new Vector2(panelX + 10, textY), Color.Cyan);
                textY += lineHeight;

                // Government info
                if (civ.Government != null)
                {
                    _font.DrawString(spriteBatch, $"Government: {civ.Government.Type}",
                        new Vector2(panelX + 10, textY), Color.LightGoldenrodYellow);
                    textY += lineHeight;

                    if (civ.Government.CurrentRuler != null)
                    {
                        var ruler = civ.Government.CurrentRuler;
                        _font.DrawString(spriteBatch, $"Ruler: {ruler.Name}",
                            new Vector2(panelX + 10, textY), Color.Violet);
                        textY += lineHeight;

                        _font.DrawString(spriteBatch, $"  {ruler.Title}, Age {ruler.Age}",
                            new Vector2(panelX + 10, textY), Color.LightGray, 12);
                        textY += lineHeight;
                    }

                    _font.DrawString(spriteBatch, $"Stability: {civ.Government.Stability:P0}",
                        new Vector2(panelX + 10, textY),
                        civ.Government.Stability > 0.7f ? Color.LimeGreen :
                        civ.Government.Stability > 0.4f ? Color.Yellow : Color.OrangeRed);
                    textY += lineHeight;
                }

                // City info if there's a city here
                if (cityAtTile != null)
                {
                    _font.DrawString(spriteBatch, $"City: {cityAtTile.Name}",
                        new Vector2(panelX + 10, textY), Color.Orange);
                    textY += lineHeight;

                    _font.DrawString(spriteBatch, $"  {cityAtTile.Type}, Pop: {cityAtTile.Population / 1000}K",
                        new Vector2(panelX + 10, textY), Color.LightGray, 12);
                    textY += lineHeight;
                }

                // Resources
                _font.DrawString(spriteBatch, $"Food: {civ.Food:F0} | Metal: {civ.Metal:F0}",
                    new Vector2(panelX + 10, textY), Color.Wheat, 12);
                textY += lineHeight + 3;
            }
        }

        // === VOLCANO ===
        if (geo.IsVolcano)
        {
            _font.DrawString(spriteBatch, "=== VOLCANO ===",
                new Vector2(panelX + 10, textY), Color.Red);
            textY += lineHeight;

            _font.DrawString(spriteBatch, $"Activity: {geo.VolcanicActivity:P0}",
                new Vector2(panelX + 10, textY), Color.OrangeRed);
            textY += lineHeight;

            _font.DrawString(spriteBatch, $"Magma Pressure: {geo.MagmaPressure:P0}",
                new Vector2(panelX + 10, textY), GetPressureColor(geo.MagmaPressure));
            textY += lineHeight;

            string eruptionState = geo.MagmaPressure > 0.8f ? "ERUPTING!" :
                                   geo.MagmaPressure > 0.6f ? "Critical" :
                                   geo.MagmaPressure > 0.4f ? "Building" : "Dormant";
            _font.DrawString(spriteBatch, $"State: {eruptionState}",
                new Vector2(panelX + 10, textY),
                geo.MagmaPressure > 0.8f ? Color.Yellow : Color.Gray);
            textY += lineHeight + 3;
        }

        // === ATMOSPHERE ===
        _font.DrawString(spriteBatch, "=== ATMOSPHERE ===",
            new Vector2(panelX + 10, textY), Color.LightBlue);
        textY += lineHeight;

        _font.DrawString(spriteBatch, $"Oxygen: {cell.Oxygen:F1}%",
            new Vector2(panelX + 10, textY), Color.Cyan);
        textY += lineHeight;

        _font.DrawString(spriteBatch, $"CO2: {cell.CO2:F1}%",
            new Vector2(panelX + 10, textY), Color.Yellow);
        textY += lineHeight;

        _font.DrawString(spriteBatch, $"Humidity: {cell.Humidity:F2}",
            new Vector2(panelX + 10, textY), Color.LightCyan);
        textY += lineHeight + 3;

        // === GEOLOGY ===
        _font.DrawString(spriteBatch, "=== GEOLOGY ===",
            new Vector2(panelX + 10, textY), Color.Orange);
        textY += lineHeight;

        _font.DrawString(spriteBatch, $"Plate Boundary: {geo.BoundaryType}",
            new Vector2(panelX + 10, textY), GetBoundaryColor(geo.BoundaryType));
        textY += lineHeight;

        if (geo.TectonicStress > 0.1f)
        {
            _font.DrawString(spriteBatch, $"Tectonic Stress: {geo.TectonicStress:P0}",
                new Vector2(panelX + 10, textY), Color.Yellow);
            textY += lineHeight;
        }

        _font.DrawString(spriteBatch, $"Sediment Layer: {geo.SedimentLayer:F2}",
            new Vector2(panelX + 10, textY), Color.White);
        textY += lineHeight + 5;

        // Sediment column header
        _font.DrawString(spriteBatch, "SEDIMENT COLUMN DIAGRAM:",
            new Vector2(panelX + 10, textY), Color.Orange);
        textY += lineHeight + 5;

        if (geo.SedimentColumn.Count == 0)
        {
            _font.DrawString(spriteBatch, "No sediment layers",
                new Vector2(panelX + 10, textY), Color.Gray);
        }
        else
        {
            // Draw geological column diagram (like a real stratigraphic column)
            int columnWidth = 120;
            int columnX = panelX + 20;
            int columnStartY = textY;

            // Show all layers - scrolling allows viewing everything
            int layersToShow = geo.SedimentColumn.Count;
            var recentLayers = geo.SedimentColumn.AsEnumerable().Reverse().ToList();

            int layerHeight = 15; // Fixed height per layer for consistency

            // Draw column border
            DrawBorder(spriteBatch, columnX - 2, columnStartY - 2, columnWidth + 4, (layerHeight * layersToShow) + 4, Color.White, 2);

            // Draw "Surface" label at top
            _font.DrawString(spriteBatch, "← SURFACE", new Vector2(columnX + columnWidth + 10, columnStartY), Color.Yellow, 12);

            for (int i = 0; i < layersToShow; i++)
            {
                var sedimentType = recentLayers[i];
                Color layerColor = GetSedimentColor(sedimentType);
                int layerY = columnStartY + (i * layerHeight);

                // Draw sediment layer with pattern
                spriteBatch.Draw(_pixelTexture,
                    new Rectangle(columnX, layerY, columnWidth, layerHeight),
                    layerColor);

                // Draw horizontal lines for stratification
                spriteBatch.Draw(_pixelTexture,
                    new Rectangle(columnX, layerY, columnWidth, 1),
                    new Color(0, 0, 0, 150));

                // Draw pattern for different sediment types
                DrawSedimentPattern(spriteBatch, columnX, layerY, columnWidth, layerHeight, sedimentType);

                // Draw layer border
                spriteBatch.Draw(_pixelTexture,
                    new Rectangle(columnX, layerY + layerHeight - 1, columnWidth, 1),
                    new Color(80, 80, 80));

                // Label for key layers (every 3rd layer)
                if (i % 3 == 0 || layersToShow < 10)
                {
                    string layerLabel = GetShortSedimentName(sedimentType);
                    _font.DrawString(spriteBatch, layerLabel,
                        new Vector2(columnX + 4, layerY + 2), Color.White, 10);
                }
            }

            // Draw "Bedrock" label at bottom
            int bottomY = columnStartY + (layerHeight * layersToShow);
            _font.DrawString(spriteBatch, "← BEDROCK", new Vector2(columnX + columnWidth + 10, bottomY - 6), Color.Gray, 12);

            // Update textY for legend
            textY = columnStartY;
            int legendX = columnX + columnWidth + 90;

            // Draw legend on the right side
            _font.DrawString(spriteBatch, "LEGEND:", new Vector2(legendX, textY), Color.Orange, 14);
            textY += 25;

            var uniqueTypes = recentLayers.Distinct().ToList();
            foreach (var sedType in uniqueTypes)
            {
                Color layerColor = GetSedimentColor(sedType);

                // Draw small color box
                int boxSize = 12;
                spriteBatch.Draw(_pixelTexture,
                    new Rectangle(legendX, textY + 2, boxSize, boxSize),
                    layerColor);
                DrawBorder(spriteBatch, legendX, textY + 2, boxSize, boxSize, Color.Gray, 1);

                // Draw type name
                _font.DrawString(spriteBatch, sedType.ToString(),
                    new Vector2(legendX + boxSize + 6, textY), Color.White, 11);

                textY += 18;
            }

            // Update textY for rock composition section
            textY = Math.Max(textY, bottomY + 30);
        }

        // Rock composition
        textY += lineHeight * 2;
        _font.DrawString(spriteBatch, "ROCK COMPOSITION:",
            new Vector2(panelX + 10, textY), Color.Orange);
        textY += lineHeight;

        _font.DrawString(spriteBatch, $"Crystalline: {geo.CrystallineRock:P0}",
            new Vector2(panelX + 10, textY), new Color(150, 150, 200));
        textY += lineHeight;

        _font.DrawString(spriteBatch, $"Sedimentary: {geo.SedimentaryRock:P0}",
            new Vector2(panelX + 10, textY), new Color(200, 180, 140));
        textY += lineHeight;

        _font.DrawString(spriteBatch, $"Volcanic: {geo.VolcanicRock:P0}",
            new Vector2(panelX + 10, textY), new Color(80, 80, 80));
        textY += lineHeight * 2; // Add some padding at the bottom

        // Calculate total content height and clamp scroll offset
        int totalContentHeight = (textY + _scrollOffset) - contentStartY;
        int visibleHeight = scissorRect.Height;
        int maxScroll = Math.Max(0, totalContentHeight - visibleHeight);
        _scrollOffset = Math.Clamp(_scrollOffset, 0, maxScroll);

        // Disable scissor test for UI elements outside content area
        spriteBatch.End();
        spriteBatch.GraphicsDevice.ScissorRectangle = oldScissorRect;
        spriteBatch.Begin();

        // Draw scrollbar if content is scrollable
        if (maxScroll > 0)
        {
            int scrollbarX = panelX + panelWidth - 12;
            int scrollbarY = panelY + 35;
            int scrollbarHeight = panelHeight - 70;
            int scrollbarWidth = 8;

            // Scrollbar track
            spriteBatch.Draw(_pixelTexture,
                new Rectangle(scrollbarX, scrollbarY, scrollbarWidth, scrollbarHeight),
                new Color(50, 50, 50, 150));

            // Scrollbar thumb
            float scrollPercentage = (float)_scrollOffset / maxScroll;
            float thumbHeight = Math.Max(20, scrollbarHeight * ((float)visibleHeight / totalContentHeight));
            int thumbY = scrollbarY + (int)((scrollbarHeight - thumbHeight) * scrollPercentage);

            spriteBatch.Draw(_pixelTexture,
                new Rectangle(scrollbarX, thumbY, scrollbarWidth, (int)thumbHeight),
                new Color(150, 150, 150, 200));
        }

        // Instructions (drawn outside scrollable area)
        int instructionsY = panelY + panelHeight - 30;
        _font.DrawString(spriteBatch, "Scroll wheel to scroll | Right-click or ESC to close",
            new Vector2(panelX + 10, instructionsY), Color.Gray, 11);
    }

    private void DrawSedimentPattern(SpriteBatch spriteBatch, int x, int y, int width, int height, SedimentType type)
    {
        // Draw patterns to distinguish sediment types (like geological diagrams)
        Random patternRandom = new Random(type.GetHashCode());

        switch (type)
        {
            case SedimentType.Sand:
                // Dotted pattern for sand
                for (int i = 0; i < width / 8; i++)
                {
                    for (int j = 0; j < height / 4; j++)
                    {
                        int dotX = x + i * 8 + patternRandom.Next(4);
                        int dotY = y + j * 4 + patternRandom.Next(3);
                        if (dotY < y + height)
                            spriteBatch.Draw(_pixelTexture, new Rectangle(dotX, dotY, 1, 1), new Color(0, 0, 0, 100));
                    }
                }
                break;

            case SedimentType.Gravel:
                // Small circles for gravel
                for (int i = 0; i < width / 12; i++)
                {
                    for (int j = 0; j < height / 10; j++)
                    {
                        int circleX = x + i * 12 + patternRandom.Next(6);
                        int circleY = y + j * 10 + patternRandom.Next(5);
                        if (circleY < y + height)
                        {
                            spriteBatch.Draw(_pixelTexture, new Rectangle(circleX, circleY, 3, 3), new Color(60, 60, 60, 120));
                            spriteBatch.Draw(_pixelTexture, new Rectangle(circleX + 1, circleY + 1, 1, 1), new Color(120, 120, 120, 150));
                        }
                    }
                }
                break;

            case SedimentType.Clay:
                // Horizontal lines for clay
                for (int i = 0; i < height; i += 2)
                {
                    spriteBatch.Draw(_pixelTexture, new Rectangle(x, y + i, width, 1), new Color(0, 0, 0, 60));
                }
                break;

            case SedimentType.Limestone:
                // Cross-hatch pattern for limestone
                for (int i = 0; i < width; i += 8)
                {
                    spriteBatch.Draw(_pixelTexture, new Rectangle(x + i, y, 1, height), new Color(200, 200, 180, 40));
                }
                break;

            case SedimentType.Volcanic:
                // Irregular blocky pattern for volcanic
                for (int i = 0; i < width / 10; i++)
                {
                    for (int j = 0; j < height / 8; j++)
                    {
                        int blockX = x + i * 10 + patternRandom.Next(4);
                        int blockY = y + j * 8 + patternRandom.Next(4);
                        if (blockY < y + height)
                            spriteBatch.Draw(_pixelTexture, new Rectangle(blockX, blockY, 3, 3), new Color(255, 255, 255, 80));
                    }
                }
                break;

            case SedimentType.Organic:
                // Wavy lines for organic
                for (int i = 0; i < height; i += 3)
                {
                    for (int j = 0; j < width; j += 4)
                    {
                        spriteBatch.Draw(_pixelTexture, new Rectangle(x + j, y + i, 2, 1), new Color(0, 0, 0, 80));
                    }
                }
                break;
        }
    }

    private string GetShortSedimentName(SedimentType type)
    {
        return type switch
        {
            SedimentType.Sand => "Sand",
            SedimentType.Silt => "Silt",
            SedimentType.Clay => "Clay",
            SedimentType.Gravel => "Grvl",
            SedimentType.Organic => "Org",
            SedimentType.Volcanic => "Volc",
            SedimentType.Limestone => "Lmst",
            _ => "???"
        };
    }

    private Color GetSedimentColor(SedimentType type)
    {
        return type switch
        {
            SedimentType.Sand => new Color(240, 230, 140),      // Khaki
            SedimentType.Silt => new Color(210, 180, 140),      // Tan
            SedimentType.Clay => new Color(160, 120, 80),       // Brown
            SedimentType.Gravel => new Color(128, 128, 128),    // Gray
            SedimentType.Organic => new Color(60, 40, 20),      // Dark brown
            SedimentType.Volcanic => new Color(40, 40, 40),     // Very dark gray
            SedimentType.Limestone => new Color(245, 245, 220), // Beige
            _ => Color.White
        };
    }

    private Color GetFloodColor(float floodLevel)
    {
        if (floodLevel < 0.1f) return Color.Green;
        if (floodLevel < 0.3f) return Color.Yellow;
        if (floodLevel < 0.6f) return Color.Orange;
        return Color.Red;
    }

    private string GetBiomeType(TerrainCell cell)
    {
        if (cell.Elevation < 0.0f) return "Ocean";
        if (cell.Elevation < 0.05f) return "Coastal";
        if (cell.Temperature < -20f) return "Polar Ice";
        if (cell.Temperature < 0f && cell.Rainfall > 0.3f) return "Tundra";
        if (cell.Temperature < 0f) return "Polar Desert";
        if (cell.Rainfall < 0.1f && cell.Temperature > 25f) return "Desert";
        if (cell.Rainfall < 0.2f && cell.Temperature > 20f) return "Arid";
        if (cell.Rainfall > 0.7f && cell.Temperature > 20f) return "Rainforest";
        if (cell.Rainfall > 0.5f && cell.Temperature > 15f) return "Tropical Forest";
        if (cell.Rainfall > 0.4f && cell.Temperature > 10f) return "Temperate Forest";
        if (cell.Rainfall > 0.3f && cell.Temperature > 5f) return "Grassland";
        if (cell.Rainfall < 0.3f) return "Savanna";
        if (cell.Elevation > 0.6f) return "Mountain";
        return "Plains";
    }

    private Color GetBiomeColor(TerrainCell cell)
    {
        if (cell.Elevation < 0.0f) return new Color(50, 100, 200); // Ocean
        if (cell.Temperature < -20f) return Color.White; // Ice
        if (cell.Temperature < 0f) return Color.LightBlue; // Cold
        if (cell.Rainfall < 0.1f) return new Color(240, 200, 100); // Desert
        if (cell.Rainfall > 0.7f) return new Color(0, 150, 0); // Rainforest
        if (cell.Rainfall > 0.4f) return new Color(50, 200, 50); // Forest
        return new Color(150, 200, 100); // Grassland
    }

    private Color GetTempColor(float temp)
    {
        if (temp < -20f) return new Color(150, 200, 255); // Very cold
        if (temp < 0f) return Color.Cyan; // Cold
        if (temp < 15f) return Color.LightGreen; // Cool
        if (temp < 25f) return Color.Yellow; // Warm
        if (temp < 35f) return Color.Orange; // Hot
        return Color.Red; // Very hot
    }

    private Color GetRainfallColor(float rainfall)
    {
        if (rainfall < 0.1f) return new Color(200, 150, 100); // Arid
        if (rainfall < 0.3f) return Color.Yellow; // Dry
        if (rainfall < 0.5f) return Color.LightGreen; // Moderate
        if (rainfall < 0.7f) return Color.Green; // Wet
        return new Color(0, 100, 200); // Very wet
    }

    private Color GetPressureColor(float pressure)
    {
        if (pressure > 0.8f) return Color.Red; // Critical
        if (pressure > 0.6f) return Color.Orange; // High
        if (pressure > 0.4f) return Color.Yellow; // Medium
        return Color.Gray; // Low
    }

    private Color GetBoundaryColor(PlateBoundaryType boundaryType)
    {
        return boundaryType switch
        {
            PlateBoundaryType.Divergent => Color.Yellow,
            PlateBoundaryType.Convergent => Color.Red,
            PlateBoundaryType.Transform => Color.Orange,
            _ => Color.Gray
        };
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
}
