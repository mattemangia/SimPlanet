using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;

namespace SimPlanet;

/// <summary>
/// UI for controlling disasters
/// </summary>
public class DisasterControlUI
{
    private readonly GraphicsDevice _graphicsDevice;
    private readonly FontRenderer _font;
    private readonly DisasterManager _disasterManager;
    private readonly PlanetMap _map;
    private Texture2D _pixelTexture;
    private MouseState _previousMouseState;
    private Point? _mouseDownPosition = null;
    private const int DragThreshold = 25; // pixels (increased for trackpad users - more forgiving)

    public bool IsVisible { get; set; } = false;
    public bool IsSelectingTarget { get; private set; } = false;
    private DisasterType? _selectedDisasterType = null;
    private int _selectedDisasterSize = 3;

    private List<ToggleButton> _toggleButtons = new();
    private List<ActionButton> _actionButtons = new();

    public DisasterControlUI(GraphicsDevice graphicsDevice, FontRenderer font,
        DisasterManager disasterManager, PlanetMap map)
    {
        _graphicsDevice = graphicsDevice;
        _font = font;
        _disasterManager = disasterManager;
        _map = map;

        _pixelTexture = new Texture2D(_graphicsDevice, 1, 1);
        _pixelTexture.SetData(new[] { Color.White });

        InitializeButtons();
    }

    private void InitializeButtons()
    {
        int panelX = 220;
        int panelY = 10;
        int buttonWidth = 180;
        int buttonHeight = 30;
        int spacing = 5;
        int currentY = panelY + 40;

        // Main toggle
        _toggleButtons.Add(new ToggleButton(
            new Rectangle(panelX, currentY, buttonWidth, buttonHeight),
            "Random Disasters",
            () => _disasterManager.RandomDisastersEnabled,
            (val) => _disasterManager.RandomDisastersEnabled = val));
        currentY += buttonHeight + spacing;

        currentY += 10; // Section spacing

        // Individual disaster toggles
        _toggleButtons.Add(new ToggleButton(
            new Rectangle(panelX, currentY, buttonWidth, buttonHeight),
            "Asteroids",
            () => _disasterManager.AsteroidsEnabled,
            (val) => _disasterManager.AsteroidsEnabled = val));
        currentY += buttonHeight + spacing;

        _toggleButtons.Add(new ToggleButton(
            new Rectangle(panelX, currentY, buttonWidth, buttonHeight),
            "Earthquakes",
            () => _disasterManager.EarthquakesEnabled,
            (val) => _disasterManager.EarthquakesEnabled = val));
        currentY += buttonHeight + spacing;

        _toggleButtons.Add(new ToggleButton(
            new Rectangle(panelX, currentY, buttonWidth, buttonHeight),
            "Nuclear Accidents",
            () => _disasterManager.NuclearAccidentsEnabled,
            (val) => _disasterManager.NuclearAccidentsEnabled = val));
        currentY += buttonHeight + spacing;

        _toggleButtons.Add(new ToggleButton(
            new Rectangle(panelX, currentY, buttonWidth, buttonHeight),
            "Acid Rain",
            () => _disasterManager.AcidRainEnabled,
            (val) => _disasterManager.AcidRainEnabled = val));
        currentY += buttonHeight + spacing;

        _toggleButtons.Add(new ToggleButton(
            new Rectangle(panelX, currentY, buttonWidth, buttonHeight),
            "Tornadoes",
            () => _disasterManager.TornadoesEnabled,
            (val) => _disasterManager.TornadoesEnabled = val));
        currentY += buttonHeight + spacing;

        _toggleButtons.Add(new ToggleButton(
            new Rectangle(panelX, currentY, buttonWidth, buttonHeight),
            "Heavy Rains",
            () => _disasterManager.HeavyRainsEnabled,
            (val) => _disasterManager.HeavyRainsEnabled = val));
        currentY += buttonHeight + spacing;

        currentY += 15; // Section spacing

        // Manual trigger buttons
        _actionButtons.Add(new ActionButton(
            new Rectangle(panelX, currentY, buttonWidth, buttonHeight),
            "Trigger Asteroid",
            new Color(100, 50, 0),
            () => StartDisasterSelection(DisasterType.Asteroid)));
        currentY += buttonHeight + spacing;

        _actionButtons.Add(new ActionButton(
            new Rectangle(panelX, currentY, buttonWidth, buttonHeight),
            "Trigger Earthquake",
            new Color(100, 80, 60),
            () => StartDisasterSelection(DisasterType.Earthquake)));
        currentY += buttonHeight + spacing;

        _actionButtons.Add(new ActionButton(
            new Rectangle(panelX, currentY, buttonWidth, buttonHeight),
            "Trigger Nuclear",
            new Color(150, 0, 0),
            () => StartDisasterSelection(DisasterType.NuclearAccident)));
        currentY += buttonHeight + spacing;

        _actionButtons.Add(new ActionButton(
            new Rectangle(panelX, currentY, buttonWidth, buttonHeight),
            "Trigger Acid Rain",
            new Color(100, 150, 50),
            () => StartDisasterSelection(DisasterType.AcidRain)));
        currentY += buttonHeight + spacing;

        _actionButtons.Add(new ActionButton(
            new Rectangle(panelX, currentY, buttonWidth, buttonHeight),
            "Trigger Tornado",
            new Color(80, 80, 80),
            () => StartDisasterSelection(DisasterType.Tornado)));
        currentY += buttonHeight + spacing;

        _actionButtons.Add(new ActionButton(
            new Rectangle(panelX, currentY, buttonWidth, buttonHeight),
            "Trigger Heavy Rain",
            new Color(50, 100, 180),
            () => StartDisasterSelection(DisasterType.HeavyRain)));
    }

    private void StartDisasterSelection(DisasterType type)
    {
        IsSelectingTarget = true;
        _selectedDisasterType = type;
    }

    public void Update(MouseState mouseState, int currentYear, int cellSize, float cameraX, float cameraY, float zoomLevel, int mapRenderOffsetX, int mapRenderOffsetY)
    {
        if (!IsVisible)
        {
            _previousMouseState = mouseState;
            return;
        }

        // Track mouse down position to detect drags
        if (mouseState.LeftButton == ButtonState.Pressed &&
            _previousMouseState.LeftButton == ButtonState.Released)
        {
            _mouseDownPosition = mouseState.Position;
        }

        // Check for click (on release, and only if not dragging)
        bool clicked = false;
        if (mouseState.LeftButton == ButtonState.Released &&
            _previousMouseState.LeftButton == ButtonState.Pressed &&
            _mouseDownPosition.HasValue)
        {
            int dragDistance = (int)Vector2.Distance(
                new Vector2(_mouseDownPosition.Value.X, _mouseDownPosition.Value.Y),
                new Vector2(mouseState.Position.X, mouseState.Position.Y)
            );
            clicked = dragDistance <= DragThreshold;
            _mouseDownPosition = null;
        }

        // Reset mouse down position if button is released
        if (mouseState.LeftButton == ButtonState.Released)
        {
            _mouseDownPosition = null;
        }

        var mousePos = new Point(mouseState.X, mouseState.Y);

        // Handle disaster target selection
        if (IsSelectingTarget && clicked)
        {
            // Convert screen coordinates to map coordinates
            float mapRelativeX = (mouseState.X - mapRenderOffsetX) + cameraX;
            float mapRelativeY = (mouseState.Y - mapRenderOffsetY) + cameraY;
            int tileX = (int)(mapRelativeX / (cellSize * zoomLevel));
            int tileY = (int)(mapRelativeY / (cellSize * zoomLevel));

            if (tileX >= 0 && tileX < _map.Width && tileY >= 0 && tileY < _map.Height)
            {
                TriggerDisasterAt(tileX, tileY, currentYear);
                IsSelectingTarget = false;
                _selectedDisasterType = null;
            }
        }

        // Cancel selection on right click
        if (IsSelectingTarget && mouseState.RightButton == ButtonState.Pressed &&
            _previousMouseState.RightButton == ButtonState.Released)
        {
            IsSelectingTarget = false;
            _selectedDisasterType = null;
        }

        // Don't process UI clicks if selecting target on map
        if (!IsSelectingTarget && clicked)
        {
            // Toggle buttons
            foreach (var button in _toggleButtons)
            {
                if (button.Bounds.Contains(mousePos))
                {
                    button.Toggle();
                    break;
                }
            }

            // Action buttons
            foreach (var button in _actionButtons)
            {
                if (button.Bounds.Contains(mousePos))
                {
                    button.OnClick?.Invoke();
                    break;
                }
            }
        }

        // Mouse wheel to adjust disaster size
        if (IsSelectingTarget)
        {
            int scrollDelta = mouseState.ScrollWheelValue - _previousMouseState.ScrollWheelValue;
            if (scrollDelta > 0)
                _selectedDisasterSize = Math.Min(_selectedDisasterSize + 1, 10);
            else if (scrollDelta < 0)
                _selectedDisasterSize = Math.Max(_selectedDisasterSize - 1, 1);
        }

        _previousMouseState = mouseState;
    }

    private void TriggerDisasterAt(int x, int y, int year)
    {
        if (!_selectedDisasterType.HasValue) return;

        switch (_selectedDisasterType.Value)
        {
            case DisasterType.Asteroid:
                _disasterManager.TriggerAsteroid(x, y, _selectedDisasterSize, year);
                break;
            case DisasterType.Earthquake:
                _disasterManager.TriggerEarthquake(x, y, 5.0f + _selectedDisasterSize, year);
                break;
            case DisasterType.NuclearAccident:
                _disasterManager.TriggerNuclearAccident(x, y, year);
                break;
            case DisasterType.AcidRain:
                _disasterManager.TriggerAcidRain(x, y, year);
                break;
            case DisasterType.Tornado:
                _disasterManager.TriggerTornado(x, y, year);
                break;
            case DisasterType.HeavyRain:
                _disasterManager.TriggerHeavyRain(x, y, year);
                break;
        }
    }

    public void Draw(SpriteBatch spriteBatch, int screenWidth, int screenHeight)
    {
        if (!IsVisible) return;

        int panelX = 220;
        int panelY = 10;
        int panelWidth = 200;
        int panelHeight = 600;

        // Background
        spriteBatch.Draw(_pixelTexture,
            new Rectangle(panelX - 5, panelY - 5, panelWidth, panelHeight),
            new Color(20, 20, 40, 230));

        // Border
        DrawBorder(spriteBatch, panelX - 5, panelY - 5, panelWidth, panelHeight, Color.DarkRed, 2);

        // Title
        _font.DrawString(spriteBatch, "DISASTER CONTROL",
            new Vector2(panelX + 20, panelY + 5), Color.Red);

        // Draw toggle buttons
        foreach (var button in _toggleButtons)
        {
            DrawToggleButton(spriteBatch, button);
        }

        // Draw action buttons
        foreach (var button in _actionButtons)
        {
            DrawActionButton(spriteBatch, button);
        }

        // Draw selection instructions
        if (IsSelectingTarget)
        {
            string instruction = $"Click on map to place {_selectedDisasterType}";
            if (_selectedDisasterType == DisasterType.Asteroid)
            {
                instruction += $"\nSize: {_selectedDisasterSize} (scroll to change)";
            }
            instruction += "\nRight-click to cancel";

            _font.DrawString(spriteBatch, instruction,
                new Vector2(panelX + 10, panelY + panelHeight - 60), Color.Yellow);
        }

        // Draw recent disasters list
        DrawRecentDisasters(spriteBatch, screenWidth);
    }

    private void DrawRecentDisasters(SpriteBatch spriteBatch, int screenWidth)
    {
        int listX = screenWidth - 220;
        int listY = 10;
        int listWidth = 210;
        int listHeight = 200;

        // Background
        spriteBatch.Draw(_pixelTexture,
            new Rectangle(listX, listY, listWidth, listHeight),
            new Color(20, 20, 40, 230));

        // Border
        DrawBorder(spriteBatch, listX, listY, listWidth, listHeight, Color.DarkRed, 2);

        // Title
        _font.DrawString(spriteBatch, "RECENT DISASTERS",
            new Vector2(listX + 10, listY + 5), Color.Red);

        // List disasters
        var disasters = _disasterManager.GetAllDisasters();
        int displayCount = Math.Min(disasters.Count, 8);
        int startIndex = Math.Max(0, disasters.Count - displayCount);

        int textY = listY + 30;
        for (int i = startIndex; i < disasters.Count; i++)
        {
            var disaster = disasters[i];
            string text = $"Y{disaster.Year}: {disaster.Type}";
            Color color = GetDisasterColor(disaster.Type);

            _font.DrawString(spriteBatch, text,
                new Vector2(listX + 10, textY), color);
            textY += 18;
        }
    }

    private void DrawToggleButton(SpriteBatch spriteBatch, ToggleButton button)
    {
        bool isOn = button.GetValue();
        Color bgColor = isOn ? new Color(0, 100, 0) : new Color(100, 0, 0);

        // Background
        spriteBatch.Draw(_pixelTexture, button.Bounds, new Color(bgColor, 0.7f));

        // Border
        DrawBorder(spriteBatch, button.Bounds.X, button.Bounds.Y,
            button.Bounds.Width, button.Bounds.Height, Color.White, 2);

        // Text
        string text = button.Text + (isOn ? ": ON" : ": OFF");
        _font.DrawString(spriteBatch, text,
            new Vector2(button.Bounds.X + 10, button.Bounds.Y + 8), Color.White);
    }

    private void DrawActionButton(SpriteBatch spriteBatch, ActionButton button)
    {
        // Background
        spriteBatch.Draw(_pixelTexture, button.Bounds, new Color(button.Color, 0.7f));

        // Border
        DrawBorder(spriteBatch, button.Bounds.X, button.Bounds.Y,
            button.Bounds.Width, button.Bounds.Height, Color.White, 2);

        // Text
        var textSize = _font.MeasureString(button.Text);
        var textPos = new Vector2(
            button.Bounds.X + (button.Bounds.Width - textSize.X) / 2,
            button.Bounds.Y + (button.Bounds.Height - textSize.Y) / 2
        );
        _font.DrawString(spriteBatch, button.Text, textPos, Color.White);
    }

    private void DrawBorder(SpriteBatch spriteBatch, int x, int y, int width, int height, Color color, int thickness)
    {
        spriteBatch.Draw(_pixelTexture, new Rectangle(x, y, width, thickness), color);
        spriteBatch.Draw(_pixelTexture, new Rectangle(x, y + height - thickness, width, thickness), color);
        spriteBatch.Draw(_pixelTexture, new Rectangle(x, y, thickness, height), color);
        spriteBatch.Draw(_pixelTexture, new Rectangle(x + width - thickness, y, thickness, height), color);
    }

    private Color GetDisasterColor(DisasterType type)
    {
        return type switch
        {
            DisasterType.Asteroid => new Color(255, 150, 0),
            DisasterType.Earthquake => new Color(150, 100, 50),
            DisasterType.VolcanicEruption => new Color(255, 50, 0),
            DisasterType.NuclearAccident => new Color(200, 0, 0),
            DisasterType.AcidRain => new Color(150, 200, 50),
            DisasterType.Tornado => new Color(100, 100, 100),
            DisasterType.HeavyRain => new Color(100, 150, 255),
            _ => Color.White
        };
    }

    private class ToggleButton
    {
        public Rectangle Bounds { get; }
        public string Text { get; }
        private Func<bool> _getValue;
        private Action<bool> _setValue;

        public ToggleButton(Rectangle bounds, string text, Func<bool> getValue, Action<bool> setValue)
        {
            Bounds = bounds;
            Text = text;
            _getValue = getValue;
            _setValue = setValue;
        }

        public bool GetValue() => _getValue();

        public void Toggle()
        {
            _setValue(!_getValue());
        }
    }

    private class ActionButton
    {
        public Rectangle Bounds { get; }
        public string Text { get; }
        public Color Color { get; }
        public Action? OnClick { get; }

        public ActionButton(Rectangle bounds, string text, Color color, Action? onClick)
        {
            Bounds = bounds;
            Text = text;
            Color = color;
            OnClick = onClick;
        }
    }
}
