using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace SimPlanet;

/// <summary>
/// UI for displaying and adjusting map generation options
/// </summary>
public class MapOptionsUI
{
    private readonly FontRenderer _font;
    private readonly SpriteBatch _spriteBatch;
    private readonly GraphicsDevice _graphicsDevice;
    private Texture2D _pixelTexture;
    private Texture2D? _previewTexture;
    private PlanetMap? _previewMap;
    private MouseState _previousMouseState;

    public bool IsVisible { get; set; } = false;
    public bool NeedsPreviewUpdate { get; set; } = true;

    public MapOptionsUI(SpriteBatch spriteBatch, FontRenderer font, GraphicsDevice graphicsDevice)
    {
        _spriteBatch = spriteBatch;
        _font = font;
        _graphicsDevice = graphicsDevice;

        _pixelTexture = new Texture2D(_graphicsDevice, 1, 1);
        _pixelTexture.SetData(new[] { Color.White });
        _previousMouseState = Mouse.GetState();
    }

    public bool Update(MouseState mouseState)
    {
        bool closeButtonClicked = false;

        if (IsVisible)
        {
            int panelX = 300;
            int panelY = 50;
            int panelWidth = 680;

            // Check for close button click (X in top right)
            if (mouseState.LeftButton == ButtonState.Released &&
                _previousMouseState.LeftButton == ButtonState.Pressed)
            {
                Rectangle closeButtonBounds = new Rectangle(panelX + panelWidth - 30, panelY + 5, 25, 25);
                if (closeButtonBounds.Contains(mouseState.Position))
                {
                    IsVisible = false;
                    closeButtonClicked = true;
                }
            }
        }

        _previousMouseState = mouseState;
        return closeButtonClicked;
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

        // Draw close button (X) in top right
        Rectangle closeButtonBounds = new Rectangle(panelX + panelWidth - 30, panelY + 5, 25, 25);
        DrawRectangle(closeButtonBounds.X, closeButtonBounds.Y, closeButtonBounds.Width, closeButtonBounds.Height, new Color(180, 0, 0, 200));
        _font.DrawString(_spriteBatch, "X", new Vector2(closeButtonBounds.X + 7, closeButtonBounds.Y + 3), Color.White, 16);

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

        // Draw preset buttons
        DrawText("PRESETS:", Color.Gold);
        DrawText($"  F6: Earth  |  F7: Mars  |  F8: Water World  |  F9: Desert", Color.Gray, 20);
        textY += 8;

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

    public static void ApplyEarthPreset(MapGenerationOptions options)
    {
        // Earth-like planet: 29% land, 71% water
        options.LandRatio = 0.29f;
        options.MountainLevel = 0.5f; // Moderate mountains
        options.WaterLevel = 0.0f; // Sea level at zero
        options.Persistence = 0.55f; // Realistic terrain variation
        options.Lacunarity = 2.1f; // Good detail level
        options.Octaves = 6;
    }

    public static void ApplyMarsPreset(MapGenerationOptions options)
    {
        // Mars: Dry, barren, higher mountains (Olympus Mons)
        options.LandRatio = 1.0f; // All land (dry)
        options.MountainLevel = 0.7f; // High mountains
        options.WaterLevel = -0.5f; // Very low valleys
        options.Persistence = 0.6f; // Varied terrain
        options.Lacunarity = 2.0f;
        options.Octaves = 7;
    }

    public static void ApplyWaterWorldPreset(MapGenerationOptions options)
    {
        // Ocean planet: 90% water, small islands
        options.LandRatio = 0.1f;
        options.MountainLevel = 0.3f; // Low islands
        options.WaterLevel = 0.3f; // High sea level
        options.Persistence = 0.45f; // Smooth terrain
        options.Lacunarity = 1.8f;
        options.Octaves = 5;
    }

    public static void ApplyDesertWorldPreset(MapGenerationOptions options)
    {
        // Desert planet (like Dune): Lots of land, low water
        options.LandRatio = 0.85f;
        options.MountainLevel = 0.4f; // Moderate dunes/mountains
        options.WaterLevel = -0.3f; // Low sea level
        options.Persistence = 0.5f;
        options.Lacunarity = 2.5f; // Fine sand detail
        options.Octaves = 8;
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
