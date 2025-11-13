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

    public bool IsVisible { get; set; } = false;

    public MapOptionsUI(SpriteBatch spriteBatch, SimpleFont font, GraphicsDevice graphicsDevice)
    {
        _spriteBatch = spriteBatch;
        _font = font;
        _graphicsDevice = graphicsDevice;

        _pixelTexture = new Texture2D(_graphicsDevice, 1, 1);
        _pixelTexture.SetData(new[] { Color.White });
    }

    public void Draw(MapGenerationOptions options)
    {
        if (!IsVisible) return;

        int panelX = 400;
        int panelY = 200;
        int panelWidth = 480;
        int panelHeight = 320;

        // Draw background
        DrawRectangle(panelX, panelY, panelWidth, panelHeight, new Color(20, 20, 40, 240));
        DrawRectangle(panelX, panelY, panelWidth, 3, new Color(100, 150, 255, 255)); // Top border

        int textY = panelY + 15;
        int lineHeight = 22;

        void DrawText(string text, Color color, int offsetX = 10)
        {
            _font.DrawString(_spriteBatch, text, new Vector2(panelX + offsetX, textY), color);
            textY += lineHeight;
        }

        DrawText("=== MAP GENERATION OPTIONS ===", Color.Yellow, 80);
        textY += 10;

        DrawText($"Seed: {options.Seed}", Color.White);
        DrawText($"  Use R to randomize", Color.Gray, 20);
        textY += 5;

        DrawText($"Land Ratio: {options.LandRatio:P0}", Color.Cyan);
        DrawText($"  Q/W: Decrease/Increase", Color.Gray, 20);
        DrawBar(panelX + 200, textY - 40, 200, options.LandRatio, Color.Green);
        textY += 5;

        DrawText($"Mountain Level: {options.MountainLevel:P0}", Color.Orange);
        DrawText($"  A/S: Decrease/Increase", Color.Gray, 20);
        DrawBar(panelX + 200, textY - 40, 200, options.MountainLevel, Color.SaddleBrown);
        textY += 5;

        DrawText($"Water Level: {options.WaterLevel:F2}", Color.LightBlue);
        DrawText($"  Z/X: Decrease/Increase", Color.Gray, 20);
        float waterNorm = (options.WaterLevel + 1.0f) / 2.0f;
        DrawBar(panelX + 200, textY - 40, 200, waterNorm, Color.Blue);
        textY += 5;

        DrawText($"Octaves: {options.Octaves}", Color.Magenta);
        DrawText($"  (Detail level)", Color.Gray, 20);
        textY += 10;

        DrawText("Press M to close this menu", Color.Yellow, 100);
        DrawText("Press R to generate new planet", Color.LightGreen, 80);
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
