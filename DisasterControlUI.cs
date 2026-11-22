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
        // Bounds are assigned by Layout(), which Update and Draw both call.
        _toggleButtons.Add(new ToggleButton("Random disasters",
            () => _disasterManager.RandomDisastersEnabled,
            (val) => _disasterManager.RandomDisastersEnabled = val));

        _toggleButtons.Add(new ToggleButton("Asteroids",
            () => _disasterManager.AsteroidsEnabled,
            (val) => _disasterManager.AsteroidsEnabled = val));

        _toggleButtons.Add(new ToggleButton("Earthquakes",
            () => _disasterManager.EarthquakesEnabled,
            (val) => _disasterManager.EarthquakesEnabled = val));

        _toggleButtons.Add(new ToggleButton("Nuclear accidents",
            () => _disasterManager.NuclearAccidentsEnabled,
            (val) => _disasterManager.NuclearAccidentsEnabled = val));

        _toggleButtons.Add(new ToggleButton("Acid rain",
            () => _disasterManager.AcidRainEnabled,
            (val) => _disasterManager.AcidRainEnabled = val));

        _toggleButtons.Add(new ToggleButton("Tornadoes",
            () => _disasterManager.TornadoesEnabled,
            (val) => _disasterManager.TornadoesEnabled = val));

        _toggleButtons.Add(new ToggleButton("Heavy rains",
            () => _disasterManager.HeavyRainsEnabled,
            (val) => _disasterManager.HeavyRainsEnabled = val));

        // Manual trigger buttons
        _actionButtons.Add(new ActionButton("Asteroid", DisasterType.Asteroid,
            () => StartDisasterSelection(DisasterType.Asteroid)));
        _actionButtons.Add(new ActionButton("Earthquake", DisasterType.Earthquake,
            () => StartDisasterSelection(DisasterType.Earthquake)));
        _actionButtons.Add(new ActionButton("Nuclear", DisasterType.NuclearAccident,
            () => StartDisasterSelection(DisasterType.NuclearAccident)));
        _actionButtons.Add(new ActionButton("Acid rain", DisasterType.AcidRain,
            () => StartDisasterSelection(DisasterType.AcidRain)));
        _actionButtons.Add(new ActionButton("Tornado", DisasterType.Tornado,
            () => StartDisasterSelection(DisasterType.Tornado)));
        _actionButtons.Add(new ActionButton("Heavy rain", DisasterType.HeavyRain,
            () => StartDisasterSelection(DisasterType.HeavyRain)));

        Layout();
    }

    // ---- Layout (single source of truth for Update and Draw) ----------------------------

    private const int PanelX = 290;   // right of the info panel
    private const int PanelY = 56;    // below the toolbar
    private const int PanelWidth = 236;
    private const int RecentRows = 6;

    private Rectangle _panel;
    private Rectangle _closeRect;
    private int _typesSectionY;
    private int _triggerSectionY;
    private int _recentSectionY;

    private void Layout()
    {
        int x = PanelX + 12;
        int width = PanelWidth - 24;
        int y = PanelY + UIWidgets.HeaderHeight + 10;

        // Master switch
        _toggleButtons[0].Bounds = new Rectangle(x, y, width, 30);
        y += 30 + 12;

        _typesSectionY = y;
        y += 22;
        for (int i = 1; i < _toggleButtons.Count; i++)
        {
            _toggleButtons[i].Bounds = new Rectangle(x, y, width, 26);
            y += 27;
        }
        y += 10;

        _triggerSectionY = y;
        y += 22;
        int half = (width - 6) / 2;
        for (int i = 0; i < _actionButtons.Count; i++)
        {
            _actionButtons[i].Bounds = new Rectangle(x + (i % 2) * (half + 6), y + (i / 2) * 34, half, 28);
        }
        y += ((_actionButtons.Count + 1) / 2) * 34 + 8;

        _recentSectionY = y;
        y += 22 + RecentRows * 18 + 10;

        _panel = new Rectangle(PanelX, PanelY, PanelWidth, y - PanelY);
        _closeRect = UIWidgets.CloseButtonRect(_panel);
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
            Layout();
            if (_closeRect.Contains(mousePos))
            {
                IsVisible = false;
                _previousMouseState = mouseState;
                return;
            }

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
                _disasterManager.TriggerEarthquake(x, y, (float)_selectedDisasterSize, year);
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

        Layout();
        int x = _panel.X + 12;
        int width = _panel.Width - 24;

        UIWidgets.DrawWindow(spriteBatch, _panel, "DISASTER CONTROL", UITheme.Bad);

        // Master switch, then one switch per kind (dimmed while random disasters are off)
        bool master = _toggleButtons[0].GetValue();
        UIWidgets.DrawToggleRow(spriteBatch, _toggleButtons[0].Bounds, _toggleButtons[0].Text, master, emphasised: true);

        UIWidgets.DrawSection(spriteBatch, x, _typesSectionY, width, "ALLOWED KINDS");
        for (int i = 1; i < _toggleButtons.Count; i++)
        {
            var button = _toggleButtons[i];
            UIWidgets.DrawToggleRow(spriteBatch, button.Bounds, button.Text, button.GetValue());
        }

        UIWidgets.DrawSection(spriteBatch, x, _triggerSectionY, width, "STRIKE THE MAP");
        foreach (var button in _actionButtons)
        {
            bool selected = IsSelectingTarget && _selectedDisasterType == button.Type;
            UIWidgets.DrawActionButton(spriteBatch, button.Bounds, button.Text, GetDisasterColor(button.Type), selected,
                fontSize: UITheme.FontSmall);
        }

        DrawRecentDisasters(spriteBatch, x, width);

        // Placement instructions while a target is being chosen
        if (IsSelectingTarget && _selectedDisasterType.HasValue)
        {
            var type = _selectedDisasterType.Value;
            var items = new List<(string, string, Color?)>();
            if (type == DisasterType.Asteroid)
                items.Add(("Size", _selectedDisasterSize.ToString(), UITheme.Gold));
            else if (type == DisasterType.Earthquake)
                items.Add(("Magnitude", $"{_selectedDisasterSize}.0", UITheme.Gold));
            bool sized = items.Count > 0;
            string hint = sized
                ? "Click the map to strike  -  wheel: change  -  right-click: cancel"
                : "Click the map to strike  -  right-click: cancel";
            UIWidgets.DrawToolBar(spriteBatch, screenWidth / 2, screenHeight - 115, screenWidth,
                "Place " + DisasterName(type), GetDisasterColor(type), items.ToArray(), hint);
        }
    }

    private void DrawRecentDisasters(SpriteBatch spriteBatch, int x, int width)
    {
        UIWidgets.DrawSection(spriteBatch, x, _recentSectionY, width, "RECENT DISASTERS");
        int textY = _recentSectionY + 22;

        var disasters = _disasterManager.GetAllDisasters();
        if (disasters.Count == 0)
        {
            UITheme.DrawText(spriteBatch, "None so far", new Vector2(x, textY), UITheme.TextMuted, UITheme.FontSmall);
            return;
        }

        // Newest first
        int shown = 0;
        for (int i = disasters.Count - 1; i >= 0 && shown < RecentRows; i--, shown++)
        {
            var disaster = disasters[i];
            UITheme.FillRounded(spriteBatch, new Rectangle(x, textY + 4, 7, 7), GetDisasterColor(disaster.Type));
            UIWidgets.DrawKeyValue(spriteBatch, x + 14, textY, width - 14, DisasterName(disaster.Type), $"year {disaster.Year}",
                UITheme.TextMuted, UITheme.FontSmall);
            textY += 18;
        }
    }

    private static string DisasterName(DisasterType type)
    {
        return type switch
        {
            DisasterType.NuclearAccident => "Nuclear accident",
            DisasterType.AcidRain => "Acid rain",
            DisasterType.HeavyRain => "Heavy rain",
            DisasterType.VolcanicEruption => "Volcanic eruption",
            _ => type.ToString()
        };
    }

    private Color GetDisasterColor(DisasterType type)
    {
        return type switch
        {
            DisasterType.Asteroid => new Color(255, 150, 0),
            DisasterType.Earthquake => new Color(196, 140, 84),
            DisasterType.VolcanicEruption => new Color(255, 80, 30),
            DisasterType.NuclearAccident => new Color(240, 60, 60),
            DisasterType.AcidRain => new Color(150, 200, 50),
            DisasterType.Tornado => new Color(170, 176, 190),
            DisasterType.HeavyRain => new Color(100, 150, 255),
            _ => Color.White
        };
    }

    private class ToggleButton
    {
        public Rectangle Bounds { get; set; }
        public string Text { get; }
        private Func<bool> _getValue;
        private Action<bool> _setValue;

        public ToggleButton(string text, Func<bool> getValue, Action<bool> setValue)
        {
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
        public Rectangle Bounds { get; set; }
        public string Text { get; }
        public DisasterType Type { get; }
        public Action? OnClick { get; }

        public ActionButton(string text, DisasterType type, Action? onClick)
        {
            Text = text;
            Type = type;
            OnClick = onClick;
        }
    }
}
