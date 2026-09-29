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
    private Texture2D? _splashBackground;

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
                    MainMenu.RemoveCornerMarks(_splashBackground);
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

        _spriteBatch.End();
        _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp);

        // Backdrop
        _spriteBatch.Draw(_pixel, new Rectangle(0, 0, screenWidth, screenHeight), new Color(4, 6, 14));
        if (_splashBackground != null)
        {
            float scale = Math.Max((float)screenWidth / _splashBackground.Width, (float)screenHeight / _splashBackground.Height);
            int displayWidth = (int)(_splashBackground.Width * scale);
            int displayHeight = (int)(_splashBackground.Height * scale);
            _spriteBatch.Draw(_splashBackground,
                new Rectangle((screenWidth - displayWidth) / 2, (screenHeight - displayHeight) / 2, displayWidth, displayHeight),
                Color.White * 0.22f);
        }

        // Card
        int cardWidth = Math.Min(640, screenWidth - 40);
        int cardHeight = 190;
        var card = new Rectangle((screenWidth - cardWidth) / 2, (screenHeight - cardHeight) / 2, cardWidth, cardHeight);
        UITheme.DrawPanel(_spriteBatch, card, new Color(12, 18, 30, 235), UITheme.Border);

        UITheme.DrawTextCentered(_spriteBatch, "GENERATING PLANET", new Rectangle(card.X, card.Y + 22, card.Width, 36), UITheme.Gold, UITheme.FontLarge + 4);

        // Progress bar with animated sheen
        var bar = new Rectangle(card.X + 40, card.Y + 82, card.Width - 80, 22);
        UITheme.FillRounded(_spriteBatch, bar, new Color(4, 8, 16));
        float progress = Math.Clamp(Progress, 0f, 1f);
        int fillWidth = (int)(bar.Width * progress);
        if (fillWidth > 4)
        {
            var fill = new Rectangle(bar.X, bar.Y, fillWidth, bar.Height);
            UITheme.FillRounded(_spriteBatch, fill, new Color(40, 140, 220));
            UITheme.FillGradient(_spriteBatch, new Rectangle(fill.X + 1, fill.Y + 1, fill.Width - 2, fill.Height / 2), Color.White * 0.25f);
            float t = (float)(DateTime.Now.TimeOfDay.TotalSeconds % 1.6) / 1.6f;
            int sheenX = fill.X + (int)(t * (fill.Width + 60)) - 60;
            for (int i = 0; i < 60; i++)
            {
                int sx = sheenX + i;
                if (sx < fill.X || sx >= fill.Right) continue;
                float a = 1f - Math.Abs(i - 30) / 30f;
                _spriteBatch.Draw(_pixel, new Rectangle(sx, fill.Y + 2, 1, fill.Height - 4), Color.White * (0.18f * a));
            }
        }
        UITheme.OutlineRounded(_spriteBatch, bar, UITheme.BorderBright);

        // Percentage and current task
        string percentage = $"{(int)(progress * 100)}%";
        UITheme.DrawTextCentered(_spriteBatch, percentage, bar, Color.White, UITheme.FontNormal);

        string task = string.IsNullOrEmpty(CurrentTask) ? "Loading..." : CurrentTask;
        UITheme.DrawTextCentered(_spriteBatch, UITheme.Ellipsize(task, card.Width - 60, UITheme.FontNormal),
            new Rectangle(card.X, bar.Bottom + 16, card.Width, 22), UITheme.TextDim, UITheme.FontNormal);

        _spriteBatch.End();
        _spriteBatch.Begin(samplerState: SamplerState.PointClamp);
    }

    public void Dispose()
    {
        _pixel?.Dispose();
        _splashBackground?.Dispose();
    }
}
