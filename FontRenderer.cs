using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using FontStashSharp;
using System.IO;

namespace SimPlanet;

/// <summary>
/// Font renderer using FontStashSharp for proper TrueType font rendering
/// </summary>
public class FontRenderer
{
    private readonly FontSystem _fontSystem;
    private readonly int _defaultFontSize;

    public FontRenderer(GraphicsDevice graphicsDevice, int defaultFontSize = 16)
    {
        _defaultFontSize = defaultFontSize;
        _fontSystem = new FontSystem();

        // Load the font file
        string fontPath = Path.Combine("Content", "Fonts", "Roboto-Regular.ttf");

        if (!File.Exists(fontPath))
        {
            throw new FileNotFoundException($"Font file not found at: {fontPath}");
        }

        byte[] fontData = File.ReadAllBytes(fontPath);
        _fontSystem.AddFont(fontData);
    }

    public void DrawString(SpriteBatch spriteBatch, string text, Vector2 position, Color color, float fontSize = 0)
    {
        if (string.IsNullOrEmpty(text))
            return;

        float size = fontSize > 0 ? fontSize : _defaultFontSize;
        var font = _fontSystem.GetFont(size);
        font.DrawText(spriteBatch, text, position, color);
    }

    public Vector2 MeasureString(string text, float fontSize = 0)
    {
        if (string.IsNullOrEmpty(text))
            return Vector2.Zero;

        float size = fontSize > 0 ? fontSize : _defaultFontSize;
        var font = _fontSystem.GetFont(size);
        var bounds = font.MeasureString(text);
        return bounds;
    }

    public void Dispose()
    {
        // FontSystem doesn't require explicit disposal
    }
}
