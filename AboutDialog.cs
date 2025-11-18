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
    private readonly FontRenderer _font;
    private readonly GraphicsDevice _graphics;
    private Texture2D _pixel;
    private Texture2D _splashBackground;

    public bool IsVisible { get; set; } = false;

    // Version information
    private const string Version = "1.0.0";
    private const string GitHubUrl = "https://github.com/mattemangia/SimPlanet";

    private Rectangle _closeButtonBounds;
    private Rectangle _githubLinkBounds;
    private bool _githubLinkHovered = false;

    public AboutDialog(FontRenderer font, GraphicsDevice graphics)
    {
        _font = font ?? throw new ArgumentNullException(nameof(font));
        _graphics = graphics ?? throw new ArgumentNullException(nameof(graphics));

        // Create a 1x1 white pixel texture for drawing rectangles
        _pixel = new Texture2D(graphics, 1, 1);
        _pixel.SetData(new[] { Color.White });

        // Load splash background from embedded resource
        LoadSplashBackground();
        
        // Debug: Confirm font is working
        try
        {
            var testSize = _font.MeasureString("Test", 16f);
            if (testSize == Vector2.Zero)
            {
                Console.WriteLine("WARNING: Font appears to not be measuring text correctly!");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Font test failed: {ex.Message}");
        }
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

    public void Draw(SpriteBatch spriteBatch, int screenWidth, int screenHeight)
    {
        if (!IsVisible) return;
        
        // Ensure we have valid resources
        if (_pixel == null || _font == null) return;

        // Draw black background first
        spriteBatch.Draw(_pixel,
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

            spriteBatch.Draw(_splashBackground,
                new Rectangle(x, y, displayWidth, displayHeight),
                Color.White * 0.15f); // Very subtle transparency
        }

        // Semi-transparent overlay for contrast
        spriteBatch.Draw(_pixel, new Rectangle(0, 0, screenWidth, screenHeight),
            new Color(0, 0, 0, 150));

        // Calculate dialog dimensions
        int dialogWidth = 500;
        int dialogHeight = 300;
        int dialogX = (screenWidth - dialogWidth) / 2;
        int dialogY = (screenHeight - dialogHeight) / 2;

        // Draw dialog background
        spriteBatch.Draw(_pixel,
            new Rectangle(dialogX, dialogY, dialogWidth, dialogHeight),
            new Color(20, 30, 50, 230));

        // Draw dialog border
        int borderThickness = 2;
        Color borderColor = new Color(100, 150, 200);
        DrawBorder(spriteBatch, dialogX, dialogY, dialogWidth, dialogHeight, borderColor, borderThickness);

        // Draw title with debug background
        string title = "ABOUT SIMPLANET";
        float titleFontSize = 24f; // Actual pixel size, not scale
        Vector2 titleSize = _font.MeasureString(title, titleFontSize);
        Vector2 titlePos = new Vector2(
            dialogX + (dialogWidth - titleSize.X) / 2,
            dialogY + 30
        );
        
        // Debug: Draw background rectangle to see where text should be
        if (titleSize != Vector2.Zero)
        {
            spriteBatch.Draw(_pixel, 
                new Rectangle((int)titlePos.X - 2, (int)titlePos.Y - 2, 
                              (int)titleSize.X + 4, (int)titleSize.Y + 4), 
                new Color(50, 50, 50, 100));
        }
        
        _font.DrawString(spriteBatch, title, titlePos, Color.Yellow, titleFontSize); // Use bright yellow

        // Draw subtitle
        string subtitle = "Planetary Evolution Simulator";
        float subtitleFontSize = 16f; // Actual pixel size
        Vector2 subtitleSize = _font.MeasureString(subtitle, subtitleFontSize);
        Vector2 subtitlePos = new Vector2(
            dialogX + (dialogWidth - subtitleSize.X) / 2,
            dialogY + 75
        );
        _font.DrawString(spriteBatch, subtitle, subtitlePos, new Color(150, 200, 255), subtitleFontSize);

        // Draw version
        string versionText = $"Version {Version}";
        float versionFontSize = 20f; // Actual pixel size
        Vector2 versionSize = _font.MeasureString(versionText, versionFontSize);
        Vector2 versionPos = new Vector2(
            dialogX + (dialogWidth - versionSize.X) / 2,
            dialogY + 120
        );
        _font.DrawString(spriteBatch, versionText, versionPos, Color.White, versionFontSize);

        // Draw GitHub link
        string githubText = "GitHub: " + GitHubUrl;
        float githubFontSize = 16f; // Actual pixel size
        Vector2 githubSize = _font.MeasureString(githubText, githubFontSize);
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
        _font.DrawString(spriteBatch, githubText, githubPos, githubColor, githubFontSize);

        // Draw underline for GitHub link if hovered
        if (_githubLinkHovered)
        {
            spriteBatch.Draw(_pixel,
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
        spriteBatch.Draw(_pixel, _closeButtonBounds, buttonBg);

        // Draw button border
        Color buttonBorder = _closeButtonBounds.Contains(Mouse.GetState().Position)
            ? new Color(120, 200, 255)
            : new Color(80, 120, 160);
        DrawBorder(spriteBatch, buttonX, buttonY, buttonWidth, buttonHeight, buttonBorder, 2);

        // Draw button text
        string buttonText = "Close";
        float buttonFontSize = 16f; // Actual pixel size
        Vector2 buttonTextSize = _font.MeasureString(buttonText, buttonFontSize);
        Vector2 buttonTextPos = new Vector2(
            buttonX + (buttonWidth - buttonTextSize.X) / 2,
            buttonY + (buttonHeight - buttonTextSize.Y) / 2
        );
        _font.DrawString(spriteBatch, buttonText, buttonTextPos, Color.White, buttonFontSize);
    }

    private void DrawBorder(SpriteBatch spriteBatch, int x, int y, int width, int height, Color color, int thickness)
    {
        // Top
        spriteBatch.Draw(_pixel, new Rectangle(x, y, width, thickness), color);
        // Bottom
        spriteBatch.Draw(_pixel, new Rectangle(x, y + height - thickness, width, thickness), color);
        // Left
        spriteBatch.Draw(_pixel, new Rectangle(x, y, thickness, height), color);
        // Right
        spriteBatch.Draw(_pixel, new Rectangle(x + width - thickness, y, thickness, height), color);
    }

    public void Dispose()
    {
        _pixel?.Dispose();
        _splashBackground?.Dispose();
    }
}