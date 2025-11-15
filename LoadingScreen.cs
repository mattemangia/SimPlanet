using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Reflection;

namespace SimPlanet;

/// <summary>
/// Displays a loading progress bar during world generation
/// </summary>
public class LoadingScreen
{
    private readonly SpriteBatch _spriteBatch;
    private readonly FontRenderer _font;
    private readonly GraphicsDevice _graphics;
    private Texture2D _pixel;
    private Texture2D _splashBackground;

    public float Progress { get; set; } = 0f; // 0.0 to 1.0
    public string CurrentTask { get; set; } = "Loading...";
    public bool IsVisible { get; set; } = false;

    public LoadingScreen(SpriteBatch spriteBatch, FontRenderer font, GraphicsDevice graphics)
    {
        _spriteBatch = spriteBatch;
        _font = font;
        _graphics = graphics;

        // Create a 1x1 white pixel texture for drawing rectangles
        _pixel = new Texture2D(graphics, 1, 1);
        _pixel.SetData(new[] { Color.White });

        // Load splash background from embedded resource
        LoadSplashBackground();
    }

    private void LoadSplashBackground()
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            using (var stream = assembly.GetManifestResourceStream("SimPlanet.splash.png"))
            {
                if (stream != null)
                {
                    _splashBackground = Texture2D.FromStream(_graphics, stream);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to load splash background: {ex.Message}");
        }
    }

    public void Draw()
    {
        if (!IsVisible) return;

        int screenWidth = _graphics.Viewport.Width;
        int screenHeight = _graphics.Viewport.Height;

        // Draw black background first
        _spriteBatch.Draw(_pixel,
            new Rectangle(0, 0, screenWidth, screenHeight),
            Color.Black);

        // Draw splash background with low alpha for subtle effect
        if (_splashBackground != null)
        {
            // Scale splash to fit screen while maintaining aspect ratio
            float scaleX = (float)screenWidth / _splashBackground.Width;
            float scaleY = (float)screenHeight / _splashBackground.Height;
            float scale = Math.Max(scaleX, scaleY);

            int displayWidth = (int)(_splashBackground.Width * scale);
            int displayHeight = (int)(_splashBackground.Height * scale);
            int x = (screenWidth - displayWidth) / 2;
            int y = (screenHeight - displayHeight) / 2;

            _spriteBatch.Draw(_splashBackground,
                new Rectangle(x, y, displayWidth, displayHeight),
                Color.White * 0.15f); // Very subtle transparency
        }

        // Calculate dimensions
        int barWidth = 600;
        int barHeight = 40;
        int barX = (screenWidth - barWidth) / 2;
        int barY = screenHeight / 2;

        // Draw title
        string title = "GENERATING PLANET";
        Vector2 titleSize = _font.MeasureString(title, 1.5f);
        Vector2 titlePos = new Vector2(
            (screenWidth - titleSize.X) / 2,
            barY - 80
        );
        _font.DrawString(_spriteBatch, title, titlePos, Color.Cyan, 1.5f);

        // Draw progress bar background (dark)
        _spriteBatch.Draw(_pixel,
            new Rectangle(barX, barY, barWidth, barHeight),
            Color.DarkGray);

        // Draw progress bar fill (cyan)
        int fillWidth = (int)(barWidth * Progress);
        if (fillWidth > 0)
        {
            _spriteBatch.Draw(_pixel,
                new Rectangle(barX, barY, fillWidth, barHeight),
                Color.Cyan);
        }

        // Draw progress bar border
        int borderThickness = 2;
        // Top
        _spriteBatch.Draw(_pixel,
            new Rectangle(barX, barY, barWidth, borderThickness),
            Color.White);
        // Bottom
        _spriteBatch.Draw(_pixel,
            new Rectangle(barX, barY + barHeight - borderThickness, barWidth, borderThickness),
            Color.White);
        // Left
        _spriteBatch.Draw(_pixel,
            new Rectangle(barX, barY, borderThickness, barHeight),
            Color.White);
        // Right
        _spriteBatch.Draw(_pixel,
            new Rectangle(barX + barWidth - borderThickness, barY, borderThickness, barHeight),
            Color.White);

        // Draw current task text
        Vector2 taskSize = _font.MeasureString(CurrentTask, 1.0f);
        Vector2 taskPos = new Vector2(
            (screenWidth - taskSize.X) / 2,
            barY + barHeight + 20
        );
        _font.DrawString(_spriteBatch, CurrentTask, taskPos, Color.White, 1.0f);

        // Draw percentage
        string percentage = $"{(int)(Progress * 100)}%";
        Vector2 percentSize = _font.MeasureString(percentage, 1.2f);
        Vector2 percentPos = new Vector2(
            (screenWidth - percentSize.X) / 2,
            barY + (barHeight - percentSize.Y) / 2
        );
        _font.DrawString(_spriteBatch, percentage, percentPos, Color.Black, 1.2f);
    }

    public void Dispose()
    {
        _pixel?.Dispose();
        _splashBackground?.Dispose();
    }
}
