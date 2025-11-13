using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Text;

namespace SimPlanet;

/// <summary>
/// Simple procedural font renderer that doesn't require external font files
/// </summary>
public class SimpleFont
{
    private readonly Texture2D _fontTexture;
    private readonly GraphicsDevice _graphicsDevice;
    private const int CharWidth = 8;
    private const int CharHeight = 12;
    private const int CharsPerRow = 16;

    public SimpleFont(GraphicsDevice graphicsDevice)
    {
        _graphicsDevice = graphicsDevice;
        _fontTexture = GenerateFontTexture();
    }

    private Texture2D GenerateFontTexture()
    {
        // Create a simple bitmap font texture (96 printable ASCII characters)
        int texWidth = CharsPerRow * CharWidth;
        int texHeight = 6 * CharHeight; // 6 rows for 96 characters

        var texture = new Texture2D(_graphicsDevice, texWidth, texHeight);
        var data = new Color[texWidth * texHeight];

        // Fill with transparent
        for (int i = 0; i < data.Length; i++)
            data[i] = Color.Transparent;

        // Draw simple character glyphs
        DrawSimpleCharacters(data, texWidth);

        texture.SetData(data);
        return texture;
    }

    private void DrawSimpleCharacters(Color[] data, int texWidth)
    {
        // Draw a few essential characters with simple pixel patterns
        // This is a minimal implementation - just enough to display text

        // Space (32) - already transparent

        // Numbers 0-9 (48-57)
        DrawChar(data, texWidth, '0', new bool[,] {
            {false, true, true, true, false},
            {true, false, false, false, true},
            {true, false, false, true, true},
            {true, false, true, false, true},
            {true, true, false, false, true},
            {true, false, false, false, true},
            {false, true, true, true, false}
        });

        DrawChar(data, texWidth, '1', new bool[,] {
            {false, false, true, false, false},
            {false, true, true, false, false},
            {false, false, true, false, false},
            {false, false, true, false, false},
            {false, false, true, false, false},
            {false, false, true, false, false},
            {false, true, true, true, false}
        });

        DrawChar(data, texWidth, '2', new bool[,] {
            {false, true, true, true, false},
            {true, false, false, false, true},
            {false, false, false, false, true},
            {false, false, true, true, false},
            {false, true, false, false, false},
            {true, false, false, false, false},
            {true, true, true, true, true}
        });

        // Add more numbers (simplified approach - use number 0 pattern for all)
        for (int i = 3; i <= 9; i++)
        {
            DrawChar(data, texWidth, (char)('0' + i), new bool[,] {
                {false, true, true, true, false},
                {true, false, false, false, true},
                {true, false, false, false, true},
                {true, false, false, false, true},
                {true, false, false, false, true},
                {true, false, false, false, true},
                {false, true, true, true, false}
            });
        }

        // Letters A-Z (simplified - using blocky patterns)
        DrawChar(data, texWidth, 'A', new bool[,] {
            {false, true, true, true, false},
            {true, false, false, false, true},
            {true, false, false, false, true},
            {true, true, true, true, true},
            {true, false, false, false, true},
            {true, false, false, false, true},
            {true, false, false, false, true}
        });

        // For simplicity, create basic patterns for common letters
        string commonLetters = "BCDEFGHIJKLMNOPQRSTUVWXYZ";
        foreach (char c in commonLetters)
        {
            // Use a simple rectangle pattern for all letters
            DrawChar(data, texWidth, c, new bool[,] {
                {true, true, true, true, true},
                {true, false, false, false, true},
                {true, false, false, false, true},
                {true, true, true, true, true},
                {true, false, false, false, true},
                {true, false, false, false, true},
                {true, true, true, true, true}
            });
        }

        // Lowercase (use uppercase patterns)
        for (char c = 'a'; c <= 'z'; c++)
        {
            DrawChar(data, texWidth, c, new bool[,] {
                {false, false, false, false, false},
                {false, true, true, true, false},
                {false, false, false, false, true},
                {false, true, true, true, true},
                {true, false, false, false, true},
                {true, false, false, false, true},
                {false, true, true, true, true}
            });
        }

        // Special characters
        DrawChar(data, texWidth, ':', new bool[,] {
            {false, false, false, false, false},
            {false, false, true, false, false},
            {false, false, false, false, false},
            {false, false, false, false, false},
            {false, false, false, false, false},
            {false, false, true, false, false},
            {false, false, false, false, false}
        });

        DrawChar(data, texWidth, '.', new bool[,] {
            {false, false, false, false, false},
            {false, false, false, false, false},
            {false, false, false, false, false},
            {false, false, false, false, false},
            {false, false, false, false, false},
            {false, false, true, false, false},
            {false, false, false, false, false}
        });

        DrawChar(data, texWidth, '-', new bool[,] {
            {false, false, false, false, false},
            {false, false, false, false, false},
            {false, false, false, false, false},
            {true, true, true, true, true},
            {false, false, false, false, false},
            {false, false, false, false, false},
            {false, false, false, false, false}
        });

        DrawChar(data, texWidth, '+', new bool[,] {
            {false, false, false, false, false},
            {false, false, true, false, false},
            {false, false, true, false, false},
            {true, true, true, true, true},
            {false, false, true, false, false},
            {false, false, true, false, false},
            {false, false, false, false, false}
        });

        DrawChar(data, texWidth, '/', new bool[,] {
            {false, false, false, false, true},
            {false, false, false, true, false},
            {false, false, true, false, false},
            {false, true, false, false, false},
            {true, false, false, false, false},
            {false, false, false, false, false},
            {false, false, false, false, false}
        });

        DrawChar(data, texWidth, '%', new bool[,] {
            {true, true, false, false, true},
            {true, true, false, true, false},
            {false, false, true, false, false},
            {false, true, false, false, false},
            {true, false, false, true, true},
            {false, false, false, true, true},
            {false, false, false, false, false}
        });

        DrawChar(data, texWidth, '=', new bool[,] {
            {false, false, false, false, false},
            {false, false, false, false, false},
            {true, true, true, true, true},
            {false, false, false, false, false},
            {true, true, true, true, true},
            {false, false, false, false, false},
            {false, false, false, false, false}
        });

        DrawChar(data, texWidth, '(', new bool[,] {
            {false, false, true, false, false},
            {false, true, false, false, false},
            {true, false, false, false, false},
            {true, false, false, false, false},
            {true, false, false, false, false},
            {false, true, false, false, false},
            {false, false, true, false, false}
        });

        DrawChar(data, texWidth, ')', new bool[,] {
            {false, false, true, false, false},
            {false, false, false, true, false},
            {false, false, false, false, true},
            {false, false, false, false, true},
            {false, false, false, false, true},
            {false, false, false, true, false},
            {false, false, true, false, false}
        });
    }

    private void DrawChar(Color[] data, int texWidth, char character, bool[,] pattern)
    {
        int charCode = (int)character;
        if (charCode < 32 || charCode >= 128) return;

        int charIndex = charCode - 32;
        int charX = (charIndex % CharsPerRow) * CharWidth;
        int charY = (charIndex / CharsPerRow) * CharHeight;

        int patternHeight = pattern.GetLength(0);
        int patternWidth = pattern.GetLength(1);

        for (int y = 0; y < patternHeight && y < CharHeight; y++)
        {
            for (int x = 0; x < patternWidth && x < CharWidth; x++)
            {
                if (pattern[y, x])
                {
                    int pixelX = charX + x + 1; // Add 1 pixel offset
                    int pixelY = charY + y + 2; // Add 2 pixel offset
                    int index = pixelY * texWidth + pixelX;
                    if (index >= 0 && index < data.Length)
                    {
                        data[index] = Color.White;
                    }
                }
            }
        }
    }

    public void DrawString(SpriteBatch spriteBatch, string text, Vector2 position, Color color)
    {
        float x = position.X;
        float y = position.Y;

        foreach (char c in text)
        {
            if (c == '\n')
            {
                y += CharHeight;
                x = position.X;
                continue;
            }

            int charCode = (int)c;
            if (charCode >= 32 && charCode < 128)
            {
                int charIndex = charCode - 32;
                int srcX = (charIndex % CharsPerRow) * CharWidth;
                int srcY = (charIndex / CharsPerRow) * CharHeight;

                spriteBatch.Draw(
                    _fontTexture,
                    new Rectangle((int)x, (int)y, CharWidth, CharHeight),
                    new Rectangle(srcX, srcY, CharWidth, CharHeight),
                    color
                );
            }

            x += CharWidth;
        }
    }

    public Vector2 MeasureString(string text)
    {
        if (string.IsNullOrEmpty(text))
            return Vector2.Zero;

        int maxWidth = 0;
        int currentWidth = 0;
        int lines = 1;

        foreach (char c in text)
        {
            if (c == '\n')
            {
                lines++;
                maxWidth = Math.Max(maxWidth, currentWidth);
                currentWidth = 0;
            }
            else
            {
                currentWidth += CharWidth;
            }
        }

        maxWidth = Math.Max(maxWidth, currentWidth);

        return new Vector2(maxWidth, lines * CharHeight);
    }

    public void Dispose()
    {
        _fontTexture?.Dispose();
    }
}
