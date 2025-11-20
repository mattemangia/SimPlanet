using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;

namespace SimPlanet;

/// <summary>
/// Bottom control bar for time and system controls
/// </summary>
public class BottomControlUI
{
    private class ControlButton
    {
        public Rectangle Bounds { get; set; }
        public string Tooltip { get; set; }
        public string Text { get; set; }
        public Action OnClick { get; set; }
        public bool IsHovered { get; set; }
        public Color BaseColor { get; set; }
    }

    private readonly SimPlanetGame _game;
    private readonly GraphicsDevice _graphicsDevice;
    private readonly FontRenderer _font;
    private Texture2D _pixelTexture;

    private List<ControlButton> _buttons = new();
    private MouseState _previousMouseState;

    // Dimensions
    private const int PanelHeight = 45;
    private const int ButtonWidth = 40;
    private const int ButtonHeight = 35;
    private const int Spacing = 8;

    // Theme
    private readonly Color _panelBgColor = new Color(20, 25, 35, 240);
    private readonly Color _borderColor = new Color(60, 80, 120);
    private readonly Color _buttonNormal = new Color(50, 60, 80);
    private readonly Color _buttonHover = new Color(80, 100, 140);

    public BottomControlUI(SimPlanetGame game, GraphicsDevice graphicsDevice, FontRenderer font)
    {
        _game = game;
        _graphicsDevice = graphicsDevice;
        _font = font;

        _pixelTexture = new Texture2D(_graphicsDevice, 1, 1);
        _pixelTexture.SetData(new[] { Color.White });

        InitializeButtons();
    }

    private void InitializeButtons()
    {
        // We'll calculate positions dynamically in Draw/Update based on screen width
        // Just add them to the list here

        AddButton("<<", "Slower (-)", () => _game.DecreaseTimeSpeed(), Color.CornflowerBlue);
        AddButton("||", "Pause/Resume (Space)", () => _game.TogglePause(), Color.Gold);
        AddButton(">>", "Faster (+)", () => _game.IncreaseTimeSpeed(), Color.CornflowerBlue);
        AddButton(">>>", "Fast Forward 10k Years (F)", () => _game.ToggleFastForward(), Color.Orange);

        // Separator logic will be visual

        AddButton("Save", "Quick Save (F5)", () => _game.QuickSave(), Color.Green);
        AddButton("Load", "Quick Load (F9)", () => _game.QuickLoad(), Color.Teal);
        AddButton("Map", "Map Options (M)", () => _game.ToggleMapOptions(), Color.Purple);
        AddButton("Help", "Toggle Help (H)", () => _game.ToggleHelp(), Color.Cyan);
        AddButton("R", "Regenerate Planet", () => _game.RegeneratePlanet(), Color.Red);
    }

    private void AddButton(string text, string tooltip, Action onClick, Color color)
    {
        _buttons.Add(new ControlButton
        {
            Text = text,
            Tooltip = tooltip,
            OnClick = onClick,
            BaseColor = color
        });
    }

    public void Update(MouseState mouseState)
    {
        // Calculate panel position for hit testing
        int screenWidth = _graphicsDevice.Viewport.Width;
        int screenHeight = _graphicsDevice.Viewport.Height;

        int totalWidth = _buttons.Count * (ButtonWidth + Spacing) + Spacing;
        int panelX = (screenWidth - totalWidth) / 2;
        int panelY = screenHeight - PanelHeight - 10;

        int currentX = panelX + Spacing;
        int currentY = panelY + (PanelHeight - ButtonHeight) / 2;

        foreach (var button in _buttons)
        {
            // Update dynamic bounds
            button.Bounds = new Rectangle(currentX, currentY, ButtonWidth, ButtonHeight);
            button.IsHovered = button.Bounds.Contains(mouseState.Position);

            currentX += ButtonWidth + Spacing;
        }

        if (mouseState.LeftButton == ButtonState.Pressed &&
            _previousMouseState.LeftButton == ButtonState.Released)
        {
            foreach (var button in _buttons)
            {
                if (button.IsHovered)
                {
                    button.OnClick?.Invoke();
                    break;
                }
            }
        }

        _previousMouseState = mouseState;
    }

    public void Draw(SpriteBatch spriteBatch)
    {
        int screenWidth = _graphicsDevice.Viewport.Width;
        int screenHeight = _graphicsDevice.Viewport.Height;

        int totalWidth = _buttons.Count * (ButtonWidth + Spacing) + Spacing;
        int panelX = (screenWidth - totalWidth) / 2;
        int panelY = screenHeight - PanelHeight - 10;

        // Draw Panel Background
        // Shadow
        spriteBatch.Draw(_pixelTexture,
            new Rectangle(panelX + 4, panelY + 4, totalWidth, PanelHeight),
            new Color(0, 0, 0, 100));

        // Main BG
        spriteBatch.Draw(_pixelTexture,
            new Rectangle(panelX, panelY, totalWidth, PanelHeight),
            _panelBgColor);

        // Border
        DrawBorder(spriteBatch, new Rectangle(panelX, panelY, totalWidth, PanelHeight), _borderColor, 2);

        // Draw Buttons
        foreach (var button in _buttons)
        {
            Color color = button.IsHovered ? _buttonHover : _buttonNormal;

            // Highlight color accent
            Color accentColor = button.BaseColor;
            if (!button.IsHovered) accentColor *= 0.8f;

            spriteBatch.Draw(_pixelTexture, button.Bounds, color);

            // Accent bottom bar
            spriteBatch.Draw(_pixelTexture,
                new Rectangle(button.Bounds.X, button.Bounds.Bottom - 4, button.Bounds.Width, 4),
                accentColor);

            // Border
            DrawBorder(spriteBatch, button.Bounds, Color.Gray, 1);

            // Text
            var textSize = _font.MeasureString(button.Text);
            // Simple scaling if text is too wide (manual logic since FontRenderer is simple)
            // For now assume short text fits
            Vector2 textPos = new Vector2(
                button.Bounds.X + (button.Bounds.Width - textSize.X) / 2,
                button.Bounds.Y + (button.Bounds.Height - textSize.Y) / 2 - 2
            );
            _font.DrawString(spriteBatch, button.Text, textPos, Color.White);
        }

        // Draw Tooltip
        var hoveredButton = _buttons.Find(b => b.IsHovered);
        if (hoveredButton != null)
        {
            DrawTooltip(spriteBatch, hoveredButton);
        }
    }

    private void DrawTooltip(SpriteBatch spriteBatch, ControlButton button)
    {
        string text = button.Tooltip;
        var size = _font.MeasureString(text);
        int padding = 8;
        int w = (int)size.X + padding * 2;
        int h = (int)size.Y + padding * 2;

        int x = button.Bounds.Center.X - w / 2;
        int y = button.Bounds.Top - h - 10;

        // Background
        spriteBatch.Draw(_pixelTexture, new Rectangle(x, y, w, h), new Color(20, 25, 35, 240));
        DrawBorder(spriteBatch, new Rectangle(x, y, w, h), Color.Yellow, 1);

        _font.DrawString(spriteBatch, text, new Vector2(x + padding, y + padding), Color.White);
    }

    private void DrawBorder(SpriteBatch spriteBatch, Rectangle rect, Color color, int thickness)
    {
        spriteBatch.Draw(_pixelTexture, new Rectangle(rect.X, rect.Y, rect.Width, thickness), color); // Top
        spriteBatch.Draw(_pixelTexture, new Rectangle(rect.X, rect.Y + rect.Height - thickness, rect.Width, thickness), color); // Bottom
        spriteBatch.Draw(_pixelTexture, new Rectangle(rect.X, rect.Y, thickness, rect.Height), color); // Left
        spriteBatch.Draw(_pixelTexture, new Rectangle(rect.X + rect.Width - thickness, rect.Y, thickness, rect.Height), color); // Right
    }
}
