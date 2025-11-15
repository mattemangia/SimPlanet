using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using FontStashSharp;
using System.IO;
using System.Reflection;

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

        // Load the font from embedded resources
        var assembly = Assembly.GetExecutingAssembly();
        string resourceName = "SimPlanet.Content.Fonts.Roboto-Regular.ttf";

        using (Stream? stream = assembly.GetManifestResourceStream(resourceName))
        {
            if (stream == null)
            {
                throw new FileNotFoundException($"Font resource not found: {resourceName}");
            }

            using (var memoryStream = new MemoryStream())
            {
                stream.CopyTo(memoryStream);
                byte[] fontData = memoryStream.ToArray();
                _fontSystem.AddFont(fontData);
            }
        }
    }

    public void DrawString(SpriteBatch spriteBatch, string text, Vector2 position, Color color, float fontSize)
    {
        if (string.IsNullOrEmpty(text))
            return;

        float size = fontSize > 0 ? fontSize : _defaultFontSize;
        var font = _fontSystem.GetFont(size);
        font.DrawText(spriteBatch, text, position, color);
    }

    public void DrawString(SpriteBatch spriteBatch, string text, Vector2 position, Color color)
    {
        DrawString(spriteBatch, text, position, color, _defaultFontSize);
    }

    public Vector2 MeasureString(string text, float fontSize)
    {
        if (string.IsNullOrEmpty(text))
            return Vector2.Zero;

        float size = fontSize > 0 ? fontSize : _defaultFontSize;
        var font = _fontSystem.GetFont(size);
        var bounds = font.MeasureString(text);
        return bounds;
    }

    public Vector2 MeasureString(string text)
    {
        return MeasureString(text, _defaultFontSize);
    }

    public void Dispose()
    {
        // FontSystem doesn't require explicit disposal
    }
}
