using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System.Diagnostics;
using System.Reflection;

namespace SimPlanet;

/// <summary>
/// Displays version information and GitHub link
/// </summary>
public class AboutDialog
{
    private readonly SpriteBatch _spriteBatch;
    private readonly FontRenderer _font;
    private readonly GraphicsDevice _graphics;
    private Texture2D _pixel;
    private Texture2D _splashBackground;

    public bool IsVisible { get; set; } = false;

    // Version information
    private const string Version = "1.0";
    private const string GitHubUrl = "https://github.com/mattemangia/SimPlanet";

    private Rectangle _closeButtonBounds;
    private Rectangle _githubLinkBounds;
    private bool _githubLinkHovered = false;

    public AboutDialog(SpriteBatch spriteBatch, FontRenderer font, GraphicsDevice graphics)
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

    public void Update(MouseState mouseState, MouseState previousMouseState)
    {
        if (!IsVisible) return;

        // Check if mouse is over GitHub link
        _githubLinkHovered = _githubLinkBounds.Contains(mouseState.Position);

        // Check for click on close button
        if (mouseState.LeftButton == ButtonState.Released &&
            previousMouseState.LeftButton == ButtonState.Pressed)
        {
            if (_closeButtonBounds.Contains(mouseState.Position))
            {
                IsVisible = false;
            }

            // Check for click on GitHub link
            if (_githubLinkBounds.Contains(mouseState.Position))
            {
                OpenGitHubLink();
            }
        }
    }

    private void OpenGitHubLink()
    {
        try
        {
            // Open URL in default browser
            Process.Start(new ProcessStartInfo
            {
                FileName = GitHubUrl,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to open GitHub link: {ex.Message}");
        }
    }

    public void Draw(int screenWidth, int screenHeight)
    {
        if (!IsVisible) return;

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

        // Semi-transparent overlay for contrast
        _spriteBatch.Draw(_pixel, new Rectangle(0, 0, screenWidth, screenHeight),
            new Color(0, 0, 0, 150));

        // Calculate dialog dimensions
        int dialogWidth = 500;
        int dialogHeight = 300;
        int dialogX = (screenWidth - dialogWidth) / 2;
        int dialogY = (screenHeight - dialogHeight) / 2;

        // Draw dialog background
        _spriteBatch.Draw(_pixel,
            new Rectangle(dialogX, dialogY, dialogWidth, dialogHeight),
            new Color(20, 30, 50, 230));

        // Draw dialog border
        int borderThickness = 2;
        Color borderColor = new Color(100, 150, 200);
        DrawBorder(dialogX, dialogY, dialogWidth, dialogHeight, borderColor, borderThickness);

        // Draw title
        string title = "ABOUT SIMPLANET";
        Vector2 titleSize = _font.MeasureString(title, 1.5f);
        Vector2 titlePos = new Vector2(
            dialogX + (dialogWidth - titleSize.X) / 2,
            dialogY + 30
        );
        _font.DrawString(_spriteBatch, title, titlePos, new Color(255, 200, 50), 1.5f);

        // Draw subtitle
        string subtitle = "Planetary Evolution Simulator";
        Vector2 subtitleSize = _font.MeasureString(subtitle, 1.0f);
        Vector2 subtitlePos = new Vector2(
            dialogX + (dialogWidth - subtitleSize.X) / 2,
            dialogY + 75
        );
        _font.DrawString(_spriteBatch, subtitle, subtitlePos, new Color(150, 200, 255), 1.0f);

        // Draw version
        string versionText = $"Version {Version}";
        Vector2 versionSize = _font.MeasureString(versionText, 1.2f);
        Vector2 versionPos = new Vector2(
            dialogX + (dialogWidth - versionSize.X) / 2,
            dialogY + 120
        );
        _font.DrawString(_spriteBatch, versionText, versionPos, Color.White, 1.2f);

        // Draw GitHub link
        string githubText = "GitHub: " + GitHubUrl;
        Vector2 githubSize = _font.MeasureString(githubText, 1.0f);
        Vector2 githubPos = new Vector2(
            dialogX + (dialogWidth - githubSize.X) / 2,
            dialogY + 170
        );

        // Store GitHub link bounds for click detection
        _githubLinkBounds = new Rectangle(
            (int)githubPos.X - 5,
            (int)githubPos.Y - 5,
            (int)githubSize.X + 10,
            (int)githubSize.Y + 10
        );

        // Draw GitHub link with hover effect
        Color githubColor = _githubLinkHovered ? new Color(255, 255, 100) : new Color(100, 200, 255);
        _font.DrawString(_spriteBatch, githubText, githubPos, githubColor, 1.0f);

        // Draw underline for GitHub link if hovered
        if (_githubLinkHovered)
        {
            _spriteBatch.Draw(_pixel,
                new Rectangle((int)githubPos.X, (int)(githubPos.Y + githubSize.Y), (int)githubSize.X, 1),
                new Color(255, 255, 100));
        }

        // Draw close button
        int buttonWidth = 120;
        int buttonHeight = 40;
        int buttonX = dialogX + (dialogWidth - buttonWidth) / 2;
        int buttonY = dialogY + dialogHeight - 70;

        _closeButtonBounds = new Rectangle(buttonX, buttonY, buttonWidth, buttonHeight);

        // Draw button background
        Color buttonBg = _closeButtonBounds.Contains(Mouse.GetState().Position)
            ? new Color(70, 140, 255, 200)
            : new Color(30, 60, 100, 180);
        _spriteBatch.Draw(_pixel, _closeButtonBounds, buttonBg);

        // Draw button border
        Color buttonBorder = _closeButtonBounds.Contains(Mouse.GetState().Position)
            ? new Color(120, 200, 255)
            : new Color(80, 120, 160);
        DrawBorder(buttonX, buttonY, buttonWidth, buttonHeight, buttonBorder, 2);

        // Draw button text
        string buttonText = "Close";
        Vector2 buttonTextSize = _font.MeasureString(buttonText, 1.0f);
        Vector2 buttonTextPos = new Vector2(
            buttonX + (buttonWidth - buttonTextSize.X) / 2,
            buttonY + (buttonHeight - buttonTextSize.Y) / 2
        );
        _font.DrawString(_spriteBatch, buttonText, buttonTextPos, Color.White, 1.0f);
    }

    private void DrawBorder(int x, int y, int width, int height, Color color, int thickness)
    {
        // Top
        _spriteBatch.Draw(_pixel, new Rectangle(x, y, width, thickness), color);
        // Bottom
        _spriteBatch.Draw(_pixel, new Rectangle(x, y + height - thickness, width, thickness), color);
        // Left
        _spriteBatch.Draw(_pixel, new Rectangle(x, y, thickness, height), color);
        // Right
        _spriteBatch.Draw(_pixel, new Rectangle(x + width - thickness, y, thickness, height), color);
    }

    public void Dispose()
    {
        _pixel?.Dispose();
        _splashBackground?.Dispose();
    }
}
