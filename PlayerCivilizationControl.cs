using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace SimPlanet;

/// <summary>
/// Allows player to take control of a civilization
/// </summary>
public class PlayerCivilizationControl
{
    private readonly GraphicsDevice _graphicsDevice;
    private readonly SimpleFont _font;
    private readonly CivilizationManager _civManager;
    private Texture2D _pixelTexture;
    private MouseState _previousMouseState;

    public Civilization? PlayerCivilization { get; private set; } = null;
    public bool ShowControlPanel { get; set; } = false;
    public bool ShowCivSelector { get; set; } = false;

    private List<Button> _controlButtons = new();
    private List<Button> _selectorButtons = new();

    public PlayerCivilizationControl(GraphicsDevice graphicsDevice, SimpleFont font, CivilizationManager civManager)
    {
        _graphicsDevice = graphicsDevice;
        _font = font;
        _civManager = civManager;

        _pixelTexture = new Texture2D(_graphicsDevice, 1, 1);
        _pixelTexture.SetData(new[] { Color.White });

        InitializeControlButtons();
    }

    private void InitializeControlButtons()
    {
        int buttonWidth = 180;
        int buttonHeight = 35;
        int startX = 10;
        int startY = 400;
        int spacing = 5;

        _controlButtons = new List<Button>
        {
            new Button(new Rectangle(startX, startY, buttonWidth, buttonHeight),
                "Declare War", Color.Red, DeclareWar),

            new Button(new Rectangle(startX, startY + (buttonHeight + spacing), buttonWidth, buttonHeight),
                "Make Peace", Color.Green, MakePeace),

            new Button(new Rectangle(startX, startY + 2 * (buttonHeight + spacing), buttonWidth, buttonHeight),
                "Join Climate Pact", Color.Cyan, JoinClimatePact),

            new Button(new Rectangle(startX, startY + 3 * (buttonHeight + spacing), buttonWidth, buttonHeight),
                "Build Nuclear", Color.Orange, BuildNuclear),

            new Button(new Rectangle(startX, startY + 4 * (buttonHeight + spacing), buttonWidth, buttonHeight),
                "Increase Eco", Color.LightGreen, IncreaseEcoFriendliness),

            new Button(new Rectangle(startX, startY + 5 * (buttonHeight + spacing), buttonWidth, buttonHeight),
                "Release Control", Color.Gray, ReleaseControl)
        };
    }

    public void Update(MouseState mouseState)
    {
        if (mouseState.LeftButton == ButtonState.Pressed &&
            _previousMouseState.LeftButton == ButtonState.Released)
        {
            var mousePos = new Point(mouseState.X, mouseState.Y);

            // Handle civilization selector
            if (ShowCivSelector)
            {
                foreach (var button in _selectorButtons)
                {
                    if (button.Bounds.Contains(mousePos))
                    {
                        button.OnClick?.Invoke();
                        break;
                    }
                }
            }

            // Handle control panel
            if (ShowControlPanel && PlayerCivilization != null)
            {
                foreach (var button in _controlButtons)
                {
                    if (button.Bounds.Contains(mousePos))
                    {
                        button.OnClick?.Invoke();
                        break;
                    }
                }
            }
        }

        _previousMouseState = mouseState;
    }

    public void OpenCivilizationSelector()
    {
        ShowCivSelector = true;
        _selectorButtons.Clear();

        int buttonWidth = 300;
        int buttonHeight = 40;
        int startX = 400;
        int startY = 100;
        int spacing = 5;

        var civilizations = _civManager.GetAllCivilizations();
        for (int i = 0; i < civilizations.Count; i++)
        {
            var civ = civilizations[i];
            int index = i; // Capture for lambda

            _selectorButtons.Add(new Button(
                new Rectangle(startX, startY + i * (buttonHeight + spacing), buttonWidth, buttonHeight),
                $"{civ.Name} (Pop: {civ.Population}, Tech: {civ.TechLevel})",
                GetCivTypeColor(civ.CivType),
                () => SelectCivilization(civilizations[index])
            ));
        }

        // Add cancel button
        _selectorButtons.Add(new Button(
            new Rectangle(startX, startY + civilizations.Count * (buttonHeight + spacing), buttonWidth, buttonHeight),
            "Cancel",
            Color.Gray,
            () => ShowCivSelector = false
        ));
    }

    private void SelectCivilization(Civilization civ)
    {
        PlayerCivilization = civ;
        ShowCivSelector = false;
        ShowControlPanel = true;
    }

    private void DeclareWar()
    {
        if (PlayerCivilization == null) return;

        var otherCivs = _civManager.GetAllCivilizations()
            .Where(c => c.Id != PlayerCivilization.Id && !c.AtWar)
            .ToList();

        if (otherCivs.Any())
        {
            var target = otherCivs.First();
            PlayerCivilization.AtWar = true;
            PlayerCivilization.WarTargetId = target.Id;
        }
    }

    private void MakePeace()
    {
        if (PlayerCivilization == null) return;
        PlayerCivilization.AtWar = false;
        PlayerCivilization.WarTargetId = null;
    }

    private void JoinClimatePact()
    {
        if (PlayerCivilization == null) return;

        if (!PlayerCivilization.InClimateAgreement)
        {
            PlayerCivilization.InClimateAgreement = true;
            PlayerCivilization.EmissionReduction = 0.5f;
        }
    }

    private void BuildNuclear()
    {
        if (PlayerCivilization == null) return;

        if (PlayerCivilization.TechLevel >= 70)
        {
            if (!PlayerCivilization.HasNuclearWeapons)
            {
                PlayerCivilization.HasNuclearWeapons = true;
                PlayerCivilization.NuclearStockpile = 5;
            }
            else
            {
                PlayerCivilization.NuclearStockpile += 5;
            }
        }
    }

    private void IncreaseEcoFriendliness()
    {
        if (PlayerCivilization == null) return;
        PlayerCivilization.EcoFriendliness = Math.Min(PlayerCivilization.EcoFriendliness + 0.1f, 1.0f);
    }

    private void ReleaseControl()
    {
        PlayerCivilization = null;
        ShowControlPanel = false;
    }

    public void Draw(SpriteBatch spriteBatch, int screenWidth, int screenHeight)
    {
        // Draw civilization selector
        if (ShowCivSelector)
        {
            DrawCivilizationSelector(spriteBatch, screenWidth, screenHeight);
        }

        // Draw control panel
        if (ShowControlPanel && PlayerCivilization != null)
        {
            DrawControlPanel(spriteBatch, screenWidth, screenHeight);
        }
    }

    private void DrawCivilizationSelector(SpriteBatch spriteBatch, int screenWidth, int screenHeight)
    {
        // Background overlay
        spriteBatch.Draw(_pixelTexture,
            new Rectangle(0, 0, screenWidth, screenHeight),
            new Color(0, 0, 0, 180));

        // Title
        _font.DrawString(spriteBatch, "SELECT CIVILIZATION TO CONTROL",
            new Vector2(screenWidth / 2 - 150, 50), Color.Yellow);

        // Draw selector buttons
        foreach (var button in _selectorButtons)
        {
            DrawButton(spriteBatch, button);
        }
    }

    private void DrawControlPanel(SpriteBatch spriteBatch, int screenWidth, int screenHeight)
    {
        int panelWidth = 200;
        int panelHeight = 350;
        int panelX = 10;
        int panelY = 390;

        // Background
        spriteBatch.Draw(_pixelTexture,
            new Rectangle(panelX, panelY, panelWidth, panelHeight),
            new Color(0, 0, 30, 220));

        // Border
        DrawBorder(spriteBatch, panelX, panelY, panelWidth, panelHeight, Color.Gold, 2);

        // Title
        _font.DrawString(spriteBatch, $"CONTROLLING: {PlayerCivilization!.Name}",
            new Vector2(panelX + 10, panelY + 5), Color.Gold);

        // Stats
        int textY = panelY + 30;
        int lineHeight = 18;

        _font.DrawString(spriteBatch, $"Population: {PlayerCivilization.Population:N0}",
            new Vector2(panelX + 10, textY), Color.White);
        textY += lineHeight;

        _font.DrawString(spriteBatch, $"Tech Level: {PlayerCivilization.TechLevel}",
            new Vector2(panelX + 10, textY), Color.White);
        textY += lineHeight;

        _font.DrawString(spriteBatch, $"Type: {PlayerCivilization.CivType}",
            new Vector2(panelX + 10, textY), GetCivTypeColor(PlayerCivilization.CivType));
        textY += lineHeight;

        _font.DrawString(spriteBatch, $"Eco: {PlayerCivilization.EcoFriendliness:P0}",
            new Vector2(panelX + 10, textY), Color.LightGreen);
        textY += lineHeight;

        if (PlayerCivilization.AtWar)
        {
            _font.DrawString(spriteBatch, "AT WAR",
                new Vector2(panelX + 10, textY), Color.Red);
            textY += lineHeight;
        }

        if (PlayerCivilization.HasNuclearWeapons)
        {
            _font.DrawString(spriteBatch, $"Nukes: {PlayerCivilization.NuclearStockpile}",
                new Vector2(panelX + 10, textY), Color.Orange);
            textY += lineHeight;
        }

        if (PlayerCivilization.InClimateAgreement)
        {
            _font.DrawString(spriteBatch, "In Climate Pact",
                new Vector2(panelX + 10, textY), Color.Cyan);
        }

        // Draw control buttons
        foreach (var button in _controlButtons)
        {
            DrawButton(spriteBatch, button);
        }
    }

    private void DrawButton(SpriteBatch spriteBatch, Button button)
    {
        // Button background
        spriteBatch.Draw(_pixelTexture, button.Bounds, new Color(button.Color, 0.7f));

        // Button border
        DrawBorder(spriteBatch, button.Bounds.X, button.Bounds.Y,
            button.Bounds.Width, button.Bounds.Height, Color.White, 2);

        // Button text
        var textSize = _font.MeasureString(button.Text);
        var textPos = new Vector2(
            button.Bounds.X + (button.Bounds.Width - textSize.X) / 2,
            button.Bounds.Y + (button.Bounds.Height - textSize.Y) / 2
        );
        _font.DrawString(spriteBatch, button.Text, textPos, Color.White);
    }

    private void DrawBorder(SpriteBatch spriteBatch, int x, int y, int width, int height, Color color, int thickness)
    {
        // Top
        spriteBatch.Draw(_pixelTexture, new Rectangle(x, y, width, thickness), color);
        // Bottom
        spriteBatch.Draw(_pixelTexture, new Rectangle(x, y + height - thickness, width, thickness), color);
        // Left
        spriteBatch.Draw(_pixelTexture, new Rectangle(x, y, thickness, height), color);
        // Right
        spriteBatch.Draw(_pixelTexture, new Rectangle(x + width - thickness, y, thickness, height), color);
    }

    private Color GetCivTypeColor(CivType civType)
    {
        return civType switch
        {
            CivType.Tribal => new Color(139, 90, 43),
            CivType.Agricultural => new Color(100, 160, 80),
            CivType.Industrial => new Color(120, 120, 120),
            CivType.Scientific => new Color(100, 149, 237),
            CivType.Spacefaring => new Color(147, 112, 219),
            _ => Color.White
        };
    }

    private class Button
    {
        public Rectangle Bounds { get; }
        public string Text { get; }
        public Color Color { get; }
        public Action? OnClick { get; }

        public Button(Rectangle bounds, string text, Color color, Action? onClick)
        {
            Bounds = bounds;
            Text = text;
            Color = color;
            OnClick = onClick;
        }
    }
}
