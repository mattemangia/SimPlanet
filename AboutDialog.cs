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
    private const string Version = "2.0.0";
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

        Layout(_graphics.Viewport.Width, _graphics.Viewport.Height);

        // Check if mouse is over GitHub link
        _githubLinkHovered = _githubLinkBounds.Contains(mouseState.Position);

        // Check for click on close button
        if (mouseState.LeftButton == ButtonState.Released &&
            previousMouseState.LeftButton == ButtonState.Pressed)
        {
            if (_closeButtonBounds.Contains(mouseState.Position) || _closeCrossBounds.Contains(mouseState.Position))
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

    // ---- Layout (shared by Update and Draw) ---------------------------------------------

    private const int DialogWidth = 520;
    private const int DialogHeight = 318;
    private const float LinkFontSize = UITheme.FontNormal;

    private Rectangle _dialog;
    private Rectangle _closeCrossBounds;

    private void Layout(int screenWidth, int screenHeight)
    {
        _dialog = new Rectangle((screenWidth - DialogWidth) / 2, (screenHeight - DialogHeight) / 2, DialogWidth, DialogHeight);
        _closeCrossBounds = UIWidgets.CloseButtonRect(_dialog);

        // The link is as wide as its text
        var linkSize = _font.MeasureString(GitHubUrl, LinkFontSize);
        _githubLinkBounds = new Rectangle(
            _dialog.X + (DialogWidth - (int)linkSize.X) / 2 - 8,
            _dialog.Y + 196,
            (int)linkSize.X + 16,
            (int)linkSize.Y + 10);

        const int buttonWidth = 130, buttonHeight = 34;
        _closeButtonBounds = new Rectangle(_dialog.X + (DialogWidth - buttonWidth) / 2, _dialog.Bottom - buttonHeight - 18, buttonWidth, buttonHeight);
    }

    public void Draw(SpriteBatch spriteBatch, int screenWidth, int screenHeight)
    {
        if (!IsVisible) return;

        // Ensure we have valid resources
        if (_pixel == null || _font == null) return;

        Layout(screenWidth, screenHeight);

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
                Color.White * 0.22f);
        }

        UIWidgets.DrawWindow(spriteBatch, _dialog, "ABOUT", UITheme.Gold);

        // Name, tagline and version, centred
        int cx = _dialog.X;
        UITheme.DrawTextCentered(spriteBatch, "SIMPLANET", new Rectangle(cx, _dialog.Y + 56, DialogWidth, 34), UITheme.Gold, UITheme.FontTitle);
        UITheme.DrawTextCentered(spriteBatch, "Planetary Evolution Simulator", new Rectangle(cx, _dialog.Y + 94, DialogWidth, 20),
            UITheme.TextDim, UITheme.FontMedium);

        string versionText = $"VERSION {Version}";
        int pillWidth = (int)UITheme.Measure(versionText, 11f).X + 16;
        UIWidgets.DrawPill(spriteBatch, cx + (DialogWidth - pillWidth) / 2, _dialog.Y + 126, versionText, UITheme.Accent);

        UIWidgets.DrawDivider(spriteBatch, _dialog.X + 40, _dialog.Y + 164, DialogWidth - 80);

        // Source code link
        UITheme.DrawTextCentered(spriteBatch, "Source code and updates", new Rectangle(cx, _dialog.Y + 174, DialogWidth, 18),
            UITheme.TextMuted, UITheme.FontSmall);
        Color linkColor = _githubLinkHovered ? UITheme.Gold : UITheme.Accent;
        if (_githubLinkHovered) UITheme.FillRounded(spriteBatch, _githubLinkBounds, UITheme.ButtonHover * 0.6f);
        var linkSize = UITheme.Measure(GitHubUrl, LinkFontSize);
        var linkPos = new Vector2(_githubLinkBounds.X + 8, _githubLinkBounds.Y + 4);
        UITheme.DrawText(spriteBatch, GitHubUrl, linkPos, linkColor, LinkFontSize);
        UITheme.FillRect(spriteBatch, new Rectangle((int)linkPos.X, (int)(linkPos.Y + linkSize.Y) + 1, (int)linkSize.X, 1),
            linkColor * (_githubLinkHovered ? 1f : 0.45f));

        UIWidgets.DrawButton(spriteBatch, _closeButtonBounds, "Close", fontSize: UITheme.FontMedium);
    }

    public void Dispose()
    {
        _pixel?.Dispose();
        _splashBackground?.Dispose();
    }
}