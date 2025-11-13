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
    private readonly SimpleFont _font;
    private readonly PlanetMap _map;
    private Texture2D _pixelTexture;
    private (int x, int y)? _selectedTile = null;
    private MouseState _previousMouseState;

    public bool IsVisible { get; private set; } = false;

    public SedimentColumnViewer(GraphicsDevice graphicsDevice, SimpleFont font, PlanetMap map)
    {
        _graphicsDevice = graphicsDevice;
        _font = font;
        _map = map;

        _pixelTexture = new Texture2D(_graphicsDevice, 1, 1);
        _pixelTexture.SetData(new[] { Color.White });
    }

    public void Update(MouseState mouseState, int cellSize, float cameraX, float cameraY, float zoomLevel)
    {
        // Check for left mouse click
        if (mouseState.LeftButton == ButtonState.Pressed &&
            _previousMouseState.LeftButton == ButtonState.Released)
        {
            // Convert mouse position to tile coordinates
            int tileX = (int)((mouseState.X / zoomLevel + cameraX) / cellSize);
            int tileY = (int)((mouseState.Y / zoomLevel + cameraY) / cellSize);

            // Check if tile is within bounds
            if (tileX >= 0 && tileX < _map.Width && tileY >= 0 && tileY < _map.Height)
            {
                _selectedTile = (tileX, tileY);
                IsVisible = true;
            }
        }

        // Close with right click or ESC
        if (mouseState.RightButton == ButtonState.Pressed || Keyboard.GetState().IsKeyDown(Keys.Escape))
        {
            IsVisible = false;
            _selectedTile = null;
        }

        _previousMouseState = mouseState;
    }

    public void Draw(SpriteBatch spriteBatch, int screenWidth, int screenHeight)
    {
        if (!IsVisible || !_selectedTile.HasValue) return;

        var (x, y) = _selectedTile.Value;
        var cell = _map.Cells[x, y];
        var geo = cell.GetGeology();

        // Panel dimensions
        int panelWidth = 400;
        int panelHeight = 600;
        int panelX = screenWidth - panelWidth - 10;
        int panelY = 10;

        // Draw background
        spriteBatch.Draw(_pixelTexture,
            new Rectangle(panelX, panelY, panelWidth, panelHeight),
            new Color(0, 0, 0, 220));

        // Draw border
        DrawBorder(spriteBatch, panelX, panelY, panelWidth, panelHeight, Color.White, 2);

        int textY = panelY + 10;
        int lineHeight = 20;

        // Title
        _font.DrawString(spriteBatch, $"SEDIMENT COLUMN ({x},{y})",
            new Vector2(panelX + 10, textY), Color.Yellow);
        textY += lineHeight * 2;

        // Cell info
        _font.DrawString(spriteBatch, $"Elevation: {cell.Elevation:F3}",
            new Vector2(panelX + 10, textY), Color.White);
        textY += lineHeight;

        _font.DrawString(spriteBatch, $"Sediment Layer: {geo.SedimentLayer:F3}",
            new Vector2(panelX + 10, textY), Color.White);
        textY += lineHeight;

        _font.DrawString(spriteBatch, $"Flood Level: {geo.FloodLevel:F3}",
            new Vector2(panelX + 10, textY), GetFloodColor(geo.FloodLevel));
        textY += lineHeight;

        _font.DrawString(spriteBatch, $"Tide Level: {geo.TideLevel:F3}",
            new Vector2(panelX + 10, textY), Color.Cyan);
        textY += lineHeight;

        _font.DrawString(spriteBatch, $"Water Flow: {geo.WaterFlow:F3}",
            new Vector2(panelX + 10, textY), Color.LightBlue);
        textY += lineHeight * 2;

        // Sediment column header
        _font.DrawString(spriteBatch, "SEDIMENT LAYERS (Top to Bottom):",
            new Vector2(panelX + 10, textY), Color.Orange);
        textY += lineHeight;

        if (geo.SedimentColumn.Count == 0)
        {
            _font.DrawString(spriteBatch, "No sediment layers",
                new Vector2(panelX + 10, textY), Color.Gray);
        }
        else
        {
            // Display most recent layers (top of column)
            int layersToShow = Math.Min(20, geo.SedimentColumn.Count);
            var recentLayers = geo.SedimentColumn.Skip(geo.SedimentColumn.Count - layersToShow).Reverse().ToList();

            for (int i = 0; i < layersToShow && textY < panelY + panelHeight - 40; i++)
            {
                var sedimentType = recentLayers[i];
                Color layerColor = GetSedimentColor(sedimentType);

                // Draw sediment layer bar
                int barWidth = 30;
                int barHeight = 15;
                spriteBatch.Draw(_pixelTexture,
                    new Rectangle(panelX + 10, textY, barWidth, barHeight),
                    layerColor);

                // Draw sediment type name
                _font.DrawString(spriteBatch, $"{i + 1}. {sedimentType}",
                    new Vector2(panelX + 50, textY), Color.White);

                textY += lineHeight;
            }

            if (geo.SedimentColumn.Count > layersToShow)
            {
                textY += lineHeight;
                _font.DrawString(spriteBatch, $"... {geo.SedimentColumn.Count - layersToShow} more layers below",
                    new Vector2(panelX + 10, textY), Color.Gray);
            }
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

        // Instructions
        textY = panelY + panelHeight - 30;
        _font.DrawString(spriteBatch, "Right-click or ESC to close",
            new Vector2(panelX + 10, textY), Color.Gray);
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
