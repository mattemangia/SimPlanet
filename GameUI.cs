using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Text;

namespace SimPlanet;

/// <summary>
/// Handles UI rendering and information display
/// </summary>
public class GameUI
{
    private readonly FontRenderer _font;
    private readonly SpriteBatch _spriteBatch;
    private readonly PlanetMap _map;
    private readonly GraphicsDevice _graphicsDevice;
    private Texture2D _pixelTexture;
    private CivilizationManager? _civilizationManager;
    private WeatherSystem? _weatherSystem;
    private AnimalEvolutionSimulator? _animalEvolutionSimulator;
    private PlanetStabilizer? _planetStabilizer;

    // Cached UI data to prevent per-frame cell scanning
    private Dictionary<LifeForm, int> _cachedLifeStats = new();
    private DateTime _lastStatsUpdate = DateTime.MinValue;
    private const double StatsUpdateIntervalMs = 100; // Update stats every 100ms

    public bool ShowHelp { get; set; } = false;
    public bool IsFastForwarding { get; set; } = false;
    public float FastForwardProgress { get; set; } = 0f;
    public int FastForwardCurrentYear { get; set; } = 0;

    // UI Theme Colors
    private readonly Color _panelBgColor = new Color(12, 18, 28, 245);
    private readonly Color _panelBorderColor = new Color(60, 90, 140);
    private readonly Color _headerBgColor = new Color(25, 35, 55, 220);
    private readonly Color _subHeaderBgColor = new Color(20, 30, 45, 180);
    private readonly Color _textLabelColor = new Color(170, 185, 205);
    private readonly Color _textValueColor = new Color(220, 235, 255);
    private readonly Color _accentColor = new Color(100, 200, 255);
    private readonly Color _alertColor = new Color(255, 100, 100);
    private readonly Color _goodColor = new Color(100, 220, 120);
    private readonly Color _goldColor = new Color(255, 215, 80);

    public GameUI(SpriteBatch spriteBatch, FontRenderer font, PlanetMap map, GraphicsDevice graphicsDevice)
    {
        _spriteBatch = spriteBatch;
        _font = font;
        _map = map;
        _graphicsDevice = graphicsDevice;

        _pixelTexture = new Texture2D(graphicsDevice, 1, 1);
        _pixelTexture.SetData(new[] { Color.White });
    }

    public void SetManagers(CivilizationManager civilizationManager, WeatherSystem weatherSystem)
    {
        _civilizationManager = civilizationManager;
        _weatherSystem = weatherSystem;
    }

    public void SetAnimalEvolutionSimulator(AnimalEvolutionSimulator animalEvolutionSimulator)
    {
        _animalEvolutionSimulator = animalEvolutionSimulator;
    }

    public void SetPlanetStabilizer(PlanetStabilizer planetStabilizer)
    {
        _planetStabilizer = planetStabilizer;
    }

    public void Draw(GameState state, RenderMode renderMode, float zoomLevel = 1.0f, bool showVolcanoes = false, bool showRivers = false, bool showPlates = false, bool showEarthquakes = false, int toolbarHeight = 0)
    {
        DrawInfoPanel(state, renderMode, zoomLevel, showVolcanoes, showRivers, showPlates, showEarthquakes, toolbarHeight);

        if (ShowHelp)
        {
            DrawHelpPanel(toolbarHeight);
        }

        if (IsFastForwarding)
        {
            DrawFastForwardProgressBar();
        }
    }

    private void DrawFastForwardProgressBar()
    {
        int barWidth = 600;
        int barHeight = 40;
        int barX = (_graphicsDevice.Viewport.Width - barWidth) / 2;
        int barY = _graphicsDevice.Viewport.Height - barHeight - 75; // Moved up to avoid bottom button bar

        // Shadow
        DrawRectangle(barX + 4, barY + 4, barWidth, barHeight, new Color(0, 0, 0, 150));

        // Main bar
        DrawRectangle(barX, barY, barWidth, barHeight, _panelBgColor);
        DrawBorder(barX, barY, barWidth, barHeight, _accentColor, 1);

        int progressWidth = (int)(barWidth * FastForwardProgress);
        DrawRectangle(barX + 2, barY + 2, Math.Max(0, progressWidth - 4), barHeight - 4, new Color(0, 100, 0, 200));

        string text = $"Fast Forwarding... {FastForwardProgress:P0} (Year: {FastForwardCurrentYear}) - Press ESC to cancel";
        var textSize = _font.MeasureString(text);
        _font.DrawString(_spriteBatch, text, new Vector2(barX + (barWidth - textSize.X) / 2, barY + (barHeight - textSize.Y) / 2), _textValueColor);
    }

    private void DrawInfoPanel(GameState state, RenderMode renderMode, float zoomLevel, bool showVolcanoes, bool showRivers, bool showPlates, bool showEarthquakes, int toolbarHeight)
    {
        // Update cached stats if needed (throttled to prevent lag)
        var timeSinceUpdate = (DateTime.Now - _lastStatsUpdate).TotalMilliseconds;
        if (timeSinceUpdate >= StatsUpdateIntervalMs)
        {
            _cachedLifeStats = CalculateLifeStats();
            _lastStatsUpdate = DateTime.Now;
        }

        // Use full left side of screen, below toolbar
        int panelX = 0;
        int panelY = toolbarHeight; // Start strictly below toolbar
        int panelWidth = 280;
        int panelHeight = _graphicsDevice.Viewport.Height - toolbarHeight;

        // Draw background with border
        DrawRectangle(panelX, panelY, panelWidth, panelHeight, _panelBgColor);

        // Right border only
        DrawRectangle(panelX + panelWidth - 1, panelY, 1, panelHeight, _panelBorderColor);

        // Top Header
        DrawRectangle(panelX, panelY, panelWidth, 40, _headerBgColor);
        DrawRectangle(panelX, panelY + 39, panelWidth, 1, _panelBorderColor);

        int textX = panelX + 15;
        int textY = panelY + 12;
        int lineHeight = 22;

        void DrawText(string text, Color color, int fontSize = 14, int offsetX = 0)
        {
            _font.DrawString(_spriteBatch, text, new Vector2(textX + offsetX, textY), color, fontSize);
            textY += lineHeight;
        }

        void DrawLabelValue(string label, string value, Color valueColor)
        {
            _font.DrawString(_spriteBatch, label, new Vector2(textX, textY), _textLabelColor, 14);
            float labelWidth = _font.MeasureString(label).X;
            _font.DrawString(_spriteBatch, value, new Vector2(textX + labelWidth + 5, textY), valueColor, 14);
            textY += lineHeight;
        }

        void DrawSectionHeader(string text)
        {
            textY += 8;
            // Subtle background for section header
            DrawRectangle(panelX + 5, textY - 2, panelWidth - 10, 24, _subHeaderBgColor);

            // Accent line on left
            DrawRectangle(panelX + 5, textY - 2, 3, 24, _accentColor);

            _font.DrawString(_spriteBatch, text, new Vector2(textX, textY), _accentColor, 14);
            textY += 26;
        }

        // Title and Game State
        _font.DrawString(_spriteBatch, "SIMPLANET", new Vector2(textX, textY), _goldColor, 16);
        textY += 30;

        float fractionalYear = state.Year + state.TimeAccumulator / GameState.SecondsPerGameYear;
        DrawLabelValue("Year:", $"{fractionalYear:N1}", _textValueColor);

        if (_animalEvolutionSimulator != null)
        {
            string eraName = _animalEvolutionSimulator.GetCurrentEraName();
            Color eraColor = _animalEvolutionSimulator.DinosaursDominant ? new Color(255, 160, 60) :
                           _animalEvolutionSimulator.MammalsDominant ? new Color(160, 210, 255) :
                           new Color(200, 200, 200);
            DrawText($"{eraName}", eraColor, 14);
        }

        DrawLabelValue("Speed:", $"{state.TimeSpeed}x", state.IsPaused ? _alertColor : _goodColor);
        if (state.IsPaused) DrawText("[||] PAUSED", _goldColor);

        // Atmosphere Section
        DrawSectionHeader("ATMOSPHERE");
        DrawLabelValue("Oxygen:", $"{_map.GlobalOxygen:F1}%", GetOxygenColor(_map.GlobalOxygen));
        DrawLabelValue("CO2:", $"{_map.GlobalCO2:F2}%", GetCO2Color(_map.GlobalCO2));
        DrawLabelValue("Temp:", $"{_map.GlobalTemperature:F1}C", GetTempColor(_map.GlobalTemperature));
        DrawLabelValue("Solar:", $"{_map.SolarEnergy:F2}", Color.Yellow);

        // Life Section
        DrawSectionHeader("BIOSPHERE");

        // Helper to draw life stats compactly
        void DrawLifeStat(string name, LifeForm type, Color color)
        {
            int count = _cachedLifeStats.GetValueOrDefault(type, 0);
            if (count > 0)
            {
                DrawLabelValue(name + ":", count.ToString(), color);
            }
        }

        DrawLifeStat("Bacteria", LifeForm.Bacteria, Color.Gray);
        DrawLifeStat("Algae", LifeForm.Algae, Color.LightGreen);
        DrawLifeStat("Plants", LifeForm.PlantLife, Color.Green);
        DrawLifeStat("Simple Animals", LifeForm.SimpleAnimals, Color.SandyBrown);
        DrawLifeStat("Fish", LifeForm.Fish, new Color(100, 120, 200));
        DrawLifeStat("Amphibians", LifeForm.Amphibians, new Color(120, 160, 80));
        DrawLifeStat("Reptiles", LifeForm.Reptiles, new Color(140, 140, 60));
        DrawLifeStat("DINOSAURS", LifeForm.Dinosaurs, Color.Orange);
        DrawLifeStat("Marine Dinos", LifeForm.MarineDinosaurs, new Color(100, 100, 180));
        DrawLifeStat("Pterosaurs", LifeForm.Pterosaurs, new Color(160, 140, 100));
        DrawLifeStat("Mammals", LifeForm.Mammals, new Color(160, 120, 90));
        DrawLifeStat("Birds", LifeForm.Birds, new Color(150, 180, 150));
        DrawLifeStat("Complex", LifeForm.ComplexAnimals, Color.Orange);
        DrawLifeStat("Intelligent", LifeForm.Intelligence, Color.Gold);
        DrawLifeStat("Civilization", LifeForm.Civilization, Color.Yellow);

        // Civilization Section
        if (_civilizationManager != null && _civilizationManager.Civilizations.Count > 0)
        {
            DrawSectionHeader("CIVILIZATIONS");
            int civCount = Math.Min(3, _civilizationManager.Civilizations.Count);
            for (int i = 0; i < civCount; i++)
            {
                var civ = _civilizationManager.Civilizations[i];
                Color nameColor = civ.AtWar ? _alertColor : _goldColor;
                string warStatus = civ.AtWar ? " [WAR]" : "";

                DrawText($"{civ.Name}{warStatus}", nameColor);

                int popK = civ.Population / 1000;
                DrawText($"Type: {civ.CivType} | Pop: {popK}K", _textLabelColor, 12, 10);

                // Icons
                string icons = "";
                if (civ.HasAirTransport) icons += "[Air] ";
                if (civ.HasSeaTransport) icons += "[Sea] ";
                if (civ.HasNuclearWeapons) icons += "[Nuke] ";

                if(!string.IsNullOrEmpty(icons))
                     DrawText(icons, Color.Cyan, 12, 10);

                if (civ.InClimateAgreement)
                {
                    DrawText($"Climate Pact (-{(int)(civ.EmissionReduction * 100)}%)", _goodColor, 12, 10);
                }
                textY += 2; // Extra spacing between civs
            }

            if (_civilizationManager.Civilizations.Count > 3)
            {
                DrawText($"...and {_civilizationManager.Civilizations.Count - 3} more", Color.Gray, 12);
            }
        }

        // Weather Alerts
        if (_weatherSystem != null)
        {
            var activeStorms = _weatherSystem.GetActiveStorms();
            if (activeStorms.Count > 0)
            {
                DrawSectionHeader("ALERTS");
                DrawText($"Active Storms: {activeStorms.Count}", _alertColor);
                int stormCount = Math.Min(2, activeStorms.Count);
                for (int i = 0; i < stormCount; i++)
                {
                    var storm = activeStorms[i];
                    DrawText($"{storm.Type} ({storm.Intensity:F1})", Color.Orange, 12);
                }
            }
        }

        // Stabilizer
        if (_planetStabilizer != null)
        {
            textY += 10;
            if (_planetStabilizer.IsActive)
            {
                DrawText("AUTO-STABILIZER ACTIVE", _accentColor);
                DrawText($"Adj: {_planetStabilizer.AdjustmentsMade} | Last: {_planetStabilizer.LastAction}", _textLabelColor, 12);
            }
            else
            {
                DrawText("Stabilizer: OFF", Color.Gray);
            }
        }

        // Footer View Info
        // Use dynamic height to ensure it sticks to bottom even if window is resized
        textY = panelY + panelHeight - 60;
        DrawRectangle(panelX + 10, textY - 5, panelWidth - 20, 1, _subHeaderBgColor);
        DrawLabelValue("View:", $"{renderMode}", Color.Magenta);
        DrawLabelValue("Zoom:", $"{zoomLevel:F1}x", Color.Cyan);

        // Mini icons for overlays
        string overlays = "";
        if (showVolcanoes) overlays += "V ";
        if (showRivers) overlays += "R ";
        if (showPlates) overlays += "P ";
        if (showEarthquakes) overlays += "E ";
        if (!string.IsNullOrEmpty(overlays))
        {
             DrawLabelValue("Overlays:", overlays, Color.Yellow);
        }
    }

    private void DrawHelpPanel(int toolbarHeight)
    {
        // Position help panel in the map area (centered with 2 columns)
        int infoPanelWidth = 280;
        int panelX = infoPanelWidth + 20; // Tighter margin
        int panelY = toolbarHeight + 20;
        int panelWidth = Math.Min(1000, _graphicsDevice.Viewport.Width - panelX - 20); // Adapt width
        // Ensure panel height doesn't overlap with bottom control bar (height ~55px)
        int bottomMargin = 70;
        int panelHeight = Math.Min(700, _graphicsDevice.Viewport.Height - toolbarHeight - bottomMargin); // Fit height

        // Shadow
        DrawRectangle(panelX + 6, panelY + 6, panelWidth, panelHeight, new Color(0, 0, 0, 150));

        // Main BG
        DrawRectangle(panelX, panelY, panelWidth, panelHeight, new Color(15, 20, 30, 250));
        DrawBorder(panelX, panelY, panelWidth, panelHeight, _goldColor, 2);

        // Header
        DrawRectangle(panelX, panelY, panelWidth, 40, new Color(40, 40, 60, 255));
        string title = "SIMPLANET COMMAND REFERENCE";
        var titleSize = _font.MeasureString(title);
        _font.DrawString(_spriteBatch, title, new Vector2(panelX + (panelWidth - titleSize.X) / 2, panelY + 10), _goldColor);

        int lineHeight = 18; // Slightly more compact line height
        int columnWidth = (panelWidth - 60) / 2;
        int leftColX = panelX + 20;
        int rightColX = panelX + columnWidth + 40;
        int startY = panelY + 50;

        // Helper to draw text in columns
        void DrawTextAt(string text, Color color, int x, int y)
        {
            if (y < panelY + panelHeight - 30) // Simple culling
            {
                _font.DrawString(_spriteBatch, text, new Vector2(x, y), color);
            }
        }

        int leftY = startY;
        int rightY = startY;

        // Left Column - Main Controls
        DrawTextAt("=== KEYBOARD CONTROLS ===", _accentColor, leftColX, leftY);
        leftY += lineHeight + 5;
        DrawTextAt("SPACE: Pause/Resume", _textValueColor, leftColX, leftY); leftY += lineHeight;
        DrawTextAt("+/-: Time speed", _textValueColor, leftColX, leftY); leftY += lineHeight;
        DrawTextAt("ESC: Pause/Menu", _textValueColor, leftColX, leftY); leftY += lineHeight;
        DrawTextAt("H: Toggle Help", _textValueColor, leftColX, leftY); leftY += lineHeight;
        DrawTextAt("R: Regenerate planet", _textValueColor, leftColX, leftY); leftY += lineHeight;
        DrawTextAt("L: Seed life (or Life Painter)", _textValueColor, leftColX, leftY); leftY += lineHeight;
        DrawTextAt("F: Fast Forward 10,000 years", Color.Cyan, leftColX, leftY); leftY += lineHeight;
        DrawTextAt("ESC: Cancel Fast Forward", Color.Cyan, leftColX, leftY); leftY += lineHeight;
        leftY += 8;

        DrawTextAt("=== VIEW MODES (1-0) ===", _accentColor, leftColX, leftY);
        leftY += lineHeight + 5;
        DrawTextAt("1: Terrain  2: Temperature", _textLabelColor, leftColX, leftY); leftY += lineHeight;
        DrawTextAt("3: Rainfall  4: Life forms", _textLabelColor, leftColX, leftY); leftY += lineHeight;
        DrawTextAt("5: Oxygen  6: CO2", _textLabelColor, leftColX, leftY); leftY += lineHeight;
        DrawTextAt("7: Elevation  8: Geology", _textLabelColor, leftColX, leftY); leftY += lineHeight;
        DrawTextAt("9: Plates  0: Volcanoes", _textLabelColor, leftColX, leftY); leftY += lineHeight;
        leftY += 8;

        DrawTextAt("=== WEATHER (F1-F4) ===", _accentColor, leftColX, leftY);
        leftY += lineHeight + 5;
        DrawTextAt("F1: Clouds  F2: Wind", _textLabelColor, leftColX, leftY); leftY += lineHeight;
        DrawTextAt("F3: Pressure  F4: Storms", _textLabelColor, leftColX, leftY); leftY += lineHeight;
        leftY += 8;

        DrawTextAt("=== GEOLOGICAL HAZARDS ===", _accentColor, leftColX, leftY);
        leftY += lineHeight + 5;
        DrawTextAt("E: Earthquakes (Discrete)", _textLabelColor, leftColX, leftY); leftY += lineHeight;
        DrawTextAt("Q: Faults (Discrete)", _textLabelColor, leftColX, leftY); leftY += lineHeight;
        DrawTextAt("U: Tsunamis", _textLabelColor, leftColX, leftY); leftY += lineHeight;
        leftY += 8;

        DrawTextAt("=== ADVANCED VIEWS ===", _accentColor, leftColX, leftY);
        leftY += lineHeight + 5;
        DrawTextAt("F10: Biomes  A: Albedo", _textLabelColor, leftColX, leftY); leftY += lineHeight;
        DrawTextAt("F12: Radiation  J: Resources", _textLabelColor, leftColX, leftY); leftY += lineHeight;
        DrawTextAt("S: Spectral Band Energy", _textLabelColor, leftColX, leftY); leftY += lineHeight;
        DrawTextAt("O: Infrastructure (Civ)", _textLabelColor, leftColX, leftY); leftY += lineHeight;
        leftY += 8;

        DrawTextAt("=== SAVE/LOAD ===", _accentColor, leftColX, leftY);
        leftY += lineHeight + 5;
        DrawTextAt("F5: Quick Save", Color.Cyan, leftColX, leftY); leftY += lineHeight;
        DrawTextAt("F9: Quick Load", Color.Cyan, leftColX, leftY); leftY += lineHeight;

        // Right Column - Mouse & Advanced Controls
        DrawTextAt("=== MOUSE CONTROLS ===", _accentColor, rightColX, rightY);
        rightY += lineHeight + 5;
        DrawTextAt("Mouse Wheel: Zoom in/out", Color.Cyan, rightColX, rightY); rightY += lineHeight;
        DrawTextAt("Left Click+Drag: Pan camera", Color.Cyan, rightColX, rightY); rightY += lineHeight;
        DrawTextAt("Middle Click+Drag: Pan camera", Color.Cyan, rightColX, rightY); rightY += lineHeight;
        DrawTextAt("Click Tile: View detailed info", _textValueColor, rightColX, rightY); rightY += lineHeight;
        rightY += 8;

        DrawTextAt("=== OVERLAYS & FEATURES ===", _accentColor, rightColX, rightY);
        rightY += lineHeight + 5;
        DrawTextAt("V: Toggle volcanoes", _textValueColor, rightColX, rightY); rightY += lineHeight;
        DrawTextAt("B: Toggle rivers", _textValueColor, rightColX, rightY); rightY += lineHeight;
        DrawTextAt("N: Toggle plate boundaries", _textValueColor, rightColX, rightY); rightY += lineHeight;
        DrawTextAt(".: Toggle earthquakes circles", _textValueColor, rightColX, rightY); rightY += lineHeight;
        DrawTextAt("P: Toggle 3D minimap", _textValueColor, rightColX, rightY); rightY += lineHeight;
        DrawTextAt("C: Day/Night cycle", Color.Cyan, rightColX, rightY); rightY += lineHeight;
        rightY += 8;

        DrawTextAt("=== TOOLS & EDITORS ===", _accentColor, rightColX, rightY);
        rightY += lineHeight + 5;
        DrawTextAt("L: Life Painter - Paint life on map", Color.Cyan, rightColX, rightY); rightY += lineHeight;
        DrawTextAt("  - Left Click: Paint, Right Click: Cycle Life", Color.Gray, rightColX, rightY); rightY += lineHeight;
        DrawTextAt("  - Scroll: Brush Size", Color.Gray, rightColX, rightY); rightY += lineHeight;
        DrawTextAt("T: Terraforming - Raise/Lower land", Color.Cyan, rightColX, rightY); rightY += lineHeight;
        DrawTextAt("  - Left Click: Apply, Right Click: Toggle Mode", Color.Gray, rightColX, rightY); rightY += lineHeight;
        DrawTextAt("D: Disaster Control - Trigger events", _textValueColor, rightColX, rightY); rightY += lineHeight;
        DrawTextAt("I: Divine Powers - Influence Civs", _textValueColor, rightColX, rightY); rightY += lineHeight;
        DrawTextAt("M: Map Options / Generator", _textValueColor, rightColX, rightY); rightY += lineHeight;
        rightY += 8;

        DrawTextAt("=== CIVILIZATION TOOLS ===", _accentColor, rightColX, rightY);
        rightY += lineHeight + 5;
        DrawTextAt("G: Control civilization", _textValueColor, rightColX, rightY); rightY += lineHeight;
        DrawTextAt("I: Divine powers menu", _goldColor, rightColX, rightY); rightY += lineHeight;
        DrawTextAt("K: Pandemic controls", _textValueColor, rightColX, rightY); rightY += lineHeight;
        DrawTextAt("Y: Graphs", Color.Cyan, rightColX, rightY); rightY += lineHeight;
        DrawTextAt("\\: Auto-stabilizer", Color.Cyan, rightColX, rightY); rightY += lineHeight;

        // Footer
        int footerY = panelY + panelHeight - 20;
        DrawTextAt("Use 'H' to toggle this help menu.", Color.LightGray, leftColX, footerY);
    }

    private void DrawRectangle(int x, int y, int width, int height, Color color)
    {
        _spriteBatch.Draw(_pixelTexture, new Rectangle(x, y, width, height), color);
    }

    private void DrawBorder(int x, int y, int width, int height, Color color, int thickness)
    {
        _spriteBatch.Draw(_pixelTexture, new Rectangle(x, y, width, thickness), color); // Top
        _spriteBatch.Draw(_pixelTexture, new Rectangle(x, y + height - thickness, width, thickness), color); // Bottom
        _spriteBatch.Draw(_pixelTexture, new Rectangle(x, y, thickness, height), color); // Left
        _spriteBatch.Draw(_pixelTexture, new Rectangle(x + width - thickness, y, thickness, height), color); // Right
    }

    private Dictionary<LifeForm, int> CalculateLifeStats()
    {
        var stats = new Dictionary<LifeForm, int>();
        foreach (LifeForm lifeForm in Enum.GetValues<LifeForm>())
        {
            stats[lifeForm] = 0;
        }

        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                var cell = _map.Cells[x, y];
                if (cell.Biomass > 0.1f)
                {
                    stats[cell.LifeType]++;
                }
            }
        }

        return stats;
    }

    private Color GetOxygenColor(float oxygen)
    {
        if (oxygen < 10) return _alertColor;
        if (oxygen < 15) return Color.Orange;
        if (oxygen < 25) return _goodColor;
        return _alertColor; // Too much oxygen
    }

    private Color GetCO2Color(float co2)
    {
        if (co2 < 0.1f) return _goodColor;
        if (co2 < 1.0f) return Color.Yellow;
        return _alertColor;
    }

    private Color GetTempColor(float temp)
    {
        if (temp < 0) return Color.LightBlue;
        if (temp < 10) return Color.Cyan;
        if (temp < 25) return _goodColor;
        if (temp < 35) return Color.Yellow;
        return _alertColor;
    }
}

public class GameState
{
    public const float SecondsPerGameYear = 10.0f;
    public int Year { get; set; }
    public float TimeSpeed { get; set; } = 1.0f;
    public bool IsPaused { get; set; }
    public float TimeAccumulator { get; set; }
}
