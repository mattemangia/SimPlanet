using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace SimPlanet;

/// <summary>
/// UI for displaying and adjusting map generation options
/// </summary>
public class MapOptionsUI
{
    private readonly SimpleFont _font;
    private readonly SpriteBatch _spriteBatch;
    private readonly GraphicsDevice _graphicsDevice;
    private Texture2D _pixelTexture;
    private Texture2D? _previewTexture;
    private PlanetMap? _previewMap;

    public bool IsVisible { get; set; } = false;
    public bool NeedsPreviewUpdate { get; set; } = true;

    public MapOptionsUI(SpriteBatch spriteBatch, SimpleFont font, GraphicsDevice graphicsDevice)
    {
        _spriteBatch = spriteBatch;
        _font = font;
        _graphicsDevice = graphicsDevice;

        _pixelTexture = new Texture2D(_graphicsDevice, 1, 1);
        _pixelTexture.SetData(new[] { Color.White });
    }

    public void UpdatePreview(MapGenerationOptions options)
    {
        if (!NeedsPreviewUpdate) return;

        // Generate small preview map (100x50 for performance)
        _previewMap = new PlanetMap(100, 50, options);

        // Create preview texture
        if (_previewTexture == null || _previewTexture.Width != 100)
        {
            _previewTexture?.Dispose();
            _previewTexture = new Texture2D(_graphicsDevice, 100, 50);
        }

        // Generate preview colors
        var colors = new Color[100 * 50];
        for (int x = 0; x < 100; x++)
        {
            for (int y = 0; y < 50; y++)
            {
                var cell = _previewMap.Cells[x, y];
                colors[y * 100 + x] = GetPreviewColor(cell);
            }
        }

        _previewTexture.SetData(colors);
        NeedsPreviewUpdate = false;
    }

    private Color GetPreviewColor(TerrainCell cell)
    {
        if (cell.IsIce)
            return new Color(240, 250, 255);

        if (cell.IsWater)
        {
            if (cell.Elevation < -0.5f)
                return new Color(10, 50, 120); // Deep ocean
            else
                return new Color(50, 100, 180); // Shallow water
        }

        if (cell.Elevation > 0.7f)
            return new Color(140, 130, 120); // Mountains
        if (cell.Elevation > 0.4f)
            return new Color(100, 150, 80); // Hills
        if (cell.IsDesert)
            return new Color(230, 200, 140); // Desert

        return new Color(80, 140, 60); // Grassland/forest
    }

    public void Draw(MapGenerationOptions options)
    {
        if (!IsVisible) return;

        int panelX = 300;
        int panelY = 50;
        int panelWidth = 680;
        int panelHeight = 620;

        // Draw background
        DrawRectangle(panelX, panelY, panelWidth, panelHeight, new Color(20, 20, 40, 240));
        DrawRectangle(panelX, panelY, panelWidth, 3, new Color(100, 150, 255, 255)); // Top border

        int textY = panelY + 15;
        int lineHeight = 20;

        void DrawText(string text, Color color, int offsetX = 10)
        {
            _font.DrawString(_spriteBatch, text, new Vector2(panelX + offsetX, textY), color);
            textY += lineHeight;
        }

        DrawText("=== MAP GENERATION OPTIONS ===", Color.Yellow, 160);
        textY += 5;

        // Draw preview
        if (_previewTexture != null)
        {
            int previewWidth = 400;
            int previewHeight = 200;
            int previewX = panelX + (panelWidth - previewWidth) / 2;
            int previewY = textY;

            _spriteBatch.Draw(_previewTexture,
                new Rectangle(previewX, previewY, previewWidth, previewHeight),
                Color.White);

            // Preview border
            DrawRectangle(previewX - 2, previewY - 2, previewWidth + 4, 2, Color.White);
            DrawRectangle(previewX - 2, previewY + previewHeight, previewWidth + 4, 2, Color.White);
            DrawRectangle(previewX - 2, previewY, 2, previewHeight, Color.White);
            DrawRectangle(previewX + previewWidth, previewY, 2, previewHeight, Color.White);

            textY += previewHeight + 15;
        }

        DrawText($"Seed: {options.Seed}", Color.White);
        DrawText($"  R: Randomize", Color.Gray, 20);
        textY += 3;

        DrawText($"Map Size: {options.MapWidth}x{options.MapHeight}", Color.Cyan);
        DrawText($"  1/2: Small/Large", Color.Gray, 20);
        textY += 3;

        DrawText($"Land Ratio: {options.LandRatio:P0}", Color.LightGreen);
        DrawText($"  Q/W: Less/More Land", Color.Gray, 20);
        DrawBar(panelX + 250, textY - 38, 200, options.LandRatio, Color.Green);
        textY += 3;

        DrawText($"Mountain Level: {options.MountainLevel:P0}", Color.Orange);
        DrawText($"  A/S: Flatter/Mountainous", Color.Gray, 20);
        DrawBar(panelX + 250, textY - 38, 200, options.MountainLevel, Color.SaddleBrown);
        textY += 3;

        DrawText($"Water Level: {options.WaterLevel:F2}", Color.LightBlue);
        DrawText($"  Z/X: Lower/Higher Seas", Color.Gray, 20);
        float waterNorm = (options.WaterLevel + 1.0f) / 2.0f;
        DrawBar(panelX + 250, textY - 38, 200, waterNorm, Color.Blue);
        textY += 3;

        DrawText($"Persistence: {options.Persistence:F2}", Color.Magenta);
        DrawText($"  E/D: Smoother/Rougher", Color.Gray, 20);
        DrawBar(panelX + 250, textY - 38, 200, options.Persistence, Color.Purple);
        textY += 3;

        DrawText($"Lacunarity: {options.Lacunarity:F2}", Color.Yellow);
        DrawText($"  C/V: Less/More Detail", Color.Gray, 20);
        DrawBar(panelX + 250, textY - 38, 200, (options.Lacunarity - 1.0f) / 2.0f, Color.Gold);
        textY += 5;

        DrawText("M: Close  |  ENTER: Generate New Planet  |  ESC: Cancel", Color.LightGreen, 60);
    }

    private void DrawRectangle(int x, int y, int width, int height, Color color)
    {
        _spriteBatch.Draw(_pixelTexture, new Rectangle(x, y, width, height), color);
    }

    private void DrawBar(int x, int y, int width, float value, Color color)
    {
        // Background
        DrawRectangle(x, y, width, 16, new Color(50, 50, 50, 200));

        // Fill
        int fillWidth = (int)(width * Math.Clamp(value, 0, 1));
        DrawRectangle(x, y, fillWidth, 16, color);

        // Border
        DrawRectangle(x, y, width, 1, Color.White);
        DrawRectangle(x, y + 15, width, 1, Color.White);
        DrawRectangle(x, y, 1, 16, Color.White);
        DrawRectangle(x + width - 1, y, 1, 16, Color.White);
    }
}
