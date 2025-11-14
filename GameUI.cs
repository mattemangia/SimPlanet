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

    public void Draw(GameState state, RenderMode renderMode, float zoomLevel = 1.0f, bool showVolcanoes = false, bool showRivers = false, bool showPlates = false)
    {
        DrawInfoPanel(state, renderMode, zoomLevel, showVolcanoes, showRivers, showPlates);

        if (ShowHelp)
        {
            DrawHelpPanel();
        }
    }

    private void DrawInfoPanel(GameState state, RenderMode renderMode, float zoomLevel, bool showVolcanoes, bool showRivers, bool showPlates)
    {
        // Update cached stats if needed (throttled to prevent lag)
        var timeSinceUpdate = (DateTime.Now - _lastStatsUpdate).TotalMilliseconds;
        if (timeSinceUpdate >= StatsUpdateIntervalMs)
        {
            _cachedLifeStats = CalculateLifeStats();
            _lastStatsUpdate = DateTime.Now;
        }

        // Use full left side of screen
        int panelX = 0;
        int panelY = 0;
        int panelWidth = 400;
        int panelHeight = _graphicsDevice.Viewport.Height;

        // Draw background with border
        DrawRectangle(panelX, panelY, panelWidth, panelHeight, new Color(10, 15, 30, 230));
        DrawBorder(panelX, panelY, panelWidth, panelHeight, new Color(80, 120, 200), 2);

        // Header bar
        DrawRectangle(panelX, panelY, panelWidth, 30, new Color(30, 60, 120, 220));

        int textY = panelY + 8;
        int lineHeight = 20;

        void DrawText(string text, Color color, int fontSize = 14)
        {
            _font.DrawString(_spriteBatch, text, new Vector2(panelX + 12, textY), color, fontSize);
            textY += lineHeight;
        }

        void DrawSectionHeader(string text)
        {
            textY += 3;
            DrawRectangle(panelX + 5, textY - 2, panelWidth - 10, 22, new Color(20, 40, 80, 150));
            DrawText(text, new Color(255, 220, 100), 15);
            textY += 3;
        }

        DrawText("SIMPLANET", new Color(255, 200, 50), 16);
        textY = panelY + 35;
        DrawText($"Year: {state.Year:N0}", new Color(200, 220, 255));
        if (_animalEvolutionSimulator != null)
        {
            string eraName = _animalEvolutionSimulator.GetCurrentEraName();
            Color eraColor = _animalEvolutionSimulator.DinosaursDominant ? new Color(255, 150, 50) :
                           _animalEvolutionSimulator.MammalsDominant ? new Color(150, 200, 255) :
                           new Color(180, 180, 180);
            DrawText($"Era: {eraName}", eraColor);
        }
        DrawText($"Speed: {state.TimeSpeed}x", state.IsPaused ? new Color(255, 100, 100) : new Color(100, 255, 100));
        if (state.IsPaused) DrawText("⏸ PAUSED", new Color(255, 200, 100));

        DrawSectionHeader("ATMOSPHERE");
        DrawText($"Oxygen: {_map.GlobalOxygen:F1}%", GetOxygenColor(_map.GlobalOxygen));
        DrawText($"CO2: {_map.GlobalCO2:F2}%", GetCO2Color(_map.GlobalCO2));
        DrawText($"Avg Temp: {_map.GlobalTemperature:F1}C", GetTempColor(_map.GlobalTemperature));
        DrawText($"Solar: {_map.SolarEnergy:F2}", Color.Yellow);
        textY += 5;

        DrawSectionHeader("LIFE");
        // Use cached stats to prevent per-frame cell scanning (20,000 cells)
        DrawText($"Bacteria: {_cachedLifeStats.GetValueOrDefault(LifeForm.Bacteria, 0)}", Color.Gray);
        DrawText($"Algae: {_cachedLifeStats.GetValueOrDefault(LifeForm.Algae, 0)}", Color.LightGreen);
        DrawText($"Plants: {_cachedLifeStats.GetValueOrDefault(LifeForm.PlantLife, 0)}", Color.Green);
        DrawText($"Simple Animals: {_cachedLifeStats.GetValueOrDefault(LifeForm.SimpleAnimals, 0)}", Color.SandyBrown);

        // Vertebrate evolution
        int fishCount = _cachedLifeStats.GetValueOrDefault(LifeForm.Fish, 0);
        if (fishCount > 0)
            DrawText($"Fish: {fishCount}", new Color(100, 120, 200));

        int amphibiansCount = _cachedLifeStats.GetValueOrDefault(LifeForm.Amphibians, 0);
        if (amphibiansCount > 0)
            DrawText($"Amphibians: {amphibiansCount}", new Color(120, 160, 80));

        int reptilesCount = _cachedLifeStats.GetValueOrDefault(LifeForm.Reptiles, 0);
        if (reptilesCount > 0)
            DrawText($"Reptiles: {reptilesCount}", new Color(140, 140, 60));

        // Age of Dinosaurs
        int dinosaursCount = _cachedLifeStats.GetValueOrDefault(LifeForm.Dinosaurs, 0);
        if (dinosaursCount > 0)
            DrawText($"DINOSAURS: {dinosaursCount}", Color.Orange);

        int marineDinosaursCount = _cachedLifeStats.GetValueOrDefault(LifeForm.MarineDinosaurs, 0);
        if (marineDinosaursCount > 0)
            DrawText($"Marine Dinosaurs: {marineDinosaursCount}", new Color(100, 100, 180));

        int pterosaursCount = _cachedLifeStats.GetValueOrDefault(LifeForm.Pterosaurs, 0);
        if (pterosaursCount > 0)
            DrawText($"Pterosaurs: {pterosaursCount}", new Color(160, 140, 100));

        // Age of Mammals
        int mammalsCount = _cachedLifeStats.GetValueOrDefault(LifeForm.Mammals, 0);
        if (mammalsCount > 0)
            DrawText($"Mammals: {mammalsCount}", new Color(160, 120, 90));

        int birdsCount = _cachedLifeStats.GetValueOrDefault(LifeForm.Birds, 0);
        if (birdsCount > 0)
            DrawText($"Birds: {birdsCount}", new Color(150, 180, 150));

        int complexAnimalsCount = _cachedLifeStats.GetValueOrDefault(LifeForm.ComplexAnimals, 0);
        if (complexAnimalsCount > 0)
            DrawText($"Complex Animals: {complexAnimalsCount}", Color.Orange);

        int intelligenceCount = _cachedLifeStats.GetValueOrDefault(LifeForm.Intelligence, 0);
        if (intelligenceCount > 0)
            DrawText($"Intelligence: {intelligenceCount}", Color.Gold);

        int civilizationCount = _cachedLifeStats.GetValueOrDefault(LifeForm.Civilization, 0);
        if (civilizationCount > 0)
            DrawText($"Civilization: {civilizationCount}", Color.Yellow);
        textY += 5;

        // Civilization statistics
        if (_civilizationManager != null && _civilizationManager.Civilizations.Count > 0)
        {
            DrawText("=== Civilizations ===", Color.Gold);
            int civCount = Math.Min(3, _civilizationManager.Civilizations.Count);
            for (int i = 0; i < civCount; i++)
            {
                var civ = _civilizationManager.Civilizations[i];
                Color nameColor = civ.AtWar ? Color.Red : Color.Yellow;
                string warStatus = civ.AtWar ? " [WAR]" : "";
                DrawText($"{civ.Name}{warStatus}", nameColor);
                int popK = civ.Population / 1000;
                DrawText($"  {civ.CivType} - Pop: {popK}K", Color.White);

                // Transportation icons
                string transport = "  Transport: ";
                if (civ.HasAirTransport) transport += "[Planes] ";
                else if (civ.HasSeaTransport) transport += "[Ships] ";
                else if (civ.HasLandTransport) transport += "[Land] ";
                else transport += "[None]";

                if (civ.HasAirTransport || civ.HasSeaTransport || civ.HasLandTransport)
                {
                    DrawText(transport, Color.Cyan);
                }

                // Nuclear weapons
                if (civ.HasNuclearWeapons)
                {
                    DrawText($"  Nukes: {civ.NuclearStockpile}", Color.Red);
                }

                // Climate agreements
                if (civ.InClimateAgreement)
                {
                    int reduction = (int)(civ.EmissionReduction * 100);
                    DrawText($"  Climate: -{reduction}% emissions", Color.LightGreen);
                }
            }
            if (_civilizationManager.Civilizations.Count > 3)
            {
                int moreCivs = _civilizationManager.Civilizations.Count - 3;
                DrawText($"...and {moreCivs} more", Color.Gray);
            }
            textY += 5;
        }

        // Weather statistics
        if (_weatherSystem != null)
        {
            var activeStorms = _weatherSystem.GetActiveStorms();
            if (activeStorms.Count > 0)
            {
                DrawText($"=== Weather Alerts ===", Color.Orange);
                DrawText($"Active Storms: {activeStorms.Count}", Color.Red);
                int stormCount = Math.Min(2, activeStorms.Count);
                for (int i = 0; i < stormCount; i++)
                {
                    var storm = activeStorms[i];
                    DrawText($"{storm.Type} - Intensity {storm.Intensity:F1}", Color.Orange);
                }
                textY += 5;
            }
        }

        // Planet Stabilizer status
        if (_planetStabilizer != null)
        {
            if (_planetStabilizer.IsActive)
            {
                DrawText("=== AUTO-STABILIZER ===", Color.Cyan);
                DrawText("[ACTIVE] Maintaining equilibrium", Color.LightGreen);
                DrawText($"Adjustments: {_planetStabilizer.AdjustmentsMade}", Color.White);
                DrawText($"Last: {_planetStabilizer.LastAction}", Color.Gray);
                DrawText("Press Y to disable", Color.DarkGray);
            }
            else
            {
                DrawText("Auto-Stabilizer: OFF (Press Y)", Color.DarkGray);
            }
            textY += 5;
        }

        DrawText($"View Mode: {renderMode}", Color.Magenta);
        DrawText($"Zoom: {zoomLevel:F1}x", Color.Cyan);

        // Show active overlays
        string overlays = "";
        if (showVolcanoes) overlays += "[Volcanoes] ";
        if (showRivers) overlays += "[Rivers] ";
        if (showPlates) overlays += "[Plates] ";
        if (!string.IsNullOrEmpty(overlays))
        {
            DrawText($"Overlays: {overlays.Trim()}", Color.Yellow);
        }
    }

    private void DrawHelpPanel()
    {
        // Position help panel in the map area (centered with 2 columns)
        int infoPanelWidth = 280;
        int panelX = infoPanelWidth + 20;
        int panelY = 20;
        int panelWidth = 780;
        int panelHeight = Math.Min(400, _graphicsDevice.Viewport.Height - 40);

        DrawRectangle(panelX, panelY, panelWidth, panelHeight, new Color(0, 0, 0, 200));
        DrawBorder(panelX, panelY, panelWidth, panelHeight, Color.Yellow, 2);

        int lineHeight = 18;
        int columnWidth = (panelWidth - 30) / 2;
        int leftColX = panelX + 10;
        int rightColX = panelX + columnWidth + 20;

        // Helper to draw text in columns
        void DrawTextAt(string text, Color color, int x, int y)
        {
            _font.DrawString(_spriteBatch, text, new Vector2(x, y), color);
        }

        int leftY = panelY + 10;
        int rightY = panelY + 10;

        // Left Column - Main Controls
        DrawTextAt("=== KEYBOARD CONTROLS ===", Color.Yellow, leftColX, leftY);
        leftY += lineHeight + 3;
        DrawTextAt("SPACE: Pause/Resume", Color.White, leftColX, leftY); leftY += lineHeight;
        DrawTextAt("+/-: Time speed", Color.White, leftColX, leftY); leftY += lineHeight;
        DrawTextAt("ESC: Pause/Menu", Color.White, leftColX, leftY); leftY += lineHeight;
        DrawTextAt("H: Toggle Help", Color.White, leftColX, leftY); leftY += lineHeight;
        DrawTextAt("R: Regenerate planet", Color.White, leftColX, leftY); leftY += lineHeight;
        DrawTextAt("L: Seed life", Color.White, leftColX, leftY); leftY += lineHeight;
        leftY += 5;

        DrawTextAt("=== VIEW MODES (1-0) ===", Color.Yellow, leftColX, leftY);
        leftY += lineHeight + 3;
        DrawTextAt("1: Terrain  2: Temperature", Color.Gray, leftColX, leftY); leftY += lineHeight;
        DrawTextAt("3: Rainfall  4: Life forms", Color.Gray, leftColX, leftY); leftY += lineHeight;
        DrawTextAt("5: Oxygen  6: CO2", Color.Gray, leftColX, leftY); leftY += lineHeight;
        DrawTextAt("7: Elevation  8: Geology", Color.Gray, leftColX, leftY); leftY += lineHeight;
        DrawTextAt("9: Plates  0: Volcanoes", Color.Gray, leftColX, leftY); leftY += lineHeight;
        leftY += 5;

        DrawTextAt("=== WEATHER (F1-F4) ===", Color.Yellow, leftColX, leftY);
        leftY += lineHeight + 3;
        DrawTextAt("F1: Clouds  F2: Wind", Color.Gray, leftColX, leftY); leftY += lineHeight;
        DrawTextAt("F3: Pressure  F4: Storms", Color.Gray, leftColX, leftY); leftY += lineHeight;
        leftY += 5;

        DrawTextAt("=== ADVANCED VIEWS ===", Color.Yellow, leftColX, leftY);
        leftY += lineHeight + 3;
        DrawTextAt("F10: Biomes  F11: Albedo", Color.Gray, leftColX, leftY); leftY += lineHeight;
        DrawTextAt("F12: Radiation  J: Resources", Color.Gray, leftColX, leftY); leftY += lineHeight;
        leftY += 5;

        DrawTextAt("=== SAVE/LOAD ===", Color.Yellow, leftColX, leftY);
        leftY += lineHeight + 3;
        DrawTextAt("F5: Quick Save", Color.Cyan, leftColX, leftY); leftY += lineHeight;
        DrawTextAt("F9: Quick Load", Color.Cyan, leftColX, leftY); leftY += lineHeight;

        // Right Column - Mouse & Advanced Controls
        DrawTextAt("=== MOUSE CONTROLS ===", Color.Yellow, rightColX, rightY);
        rightY += lineHeight + 3;
        DrawTextAt("Mouse Wheel: Zoom in/out", Color.Cyan, rightColX, rightY); rightY += lineHeight;
        DrawTextAt("Left Click+Drag: Pan camera", Color.Cyan, rightColX, rightY); rightY += lineHeight;
        DrawTextAt("Middle Click+Drag: Pan camera", Color.Cyan, rightColX, rightY); rightY += lineHeight;
        DrawTextAt("Click Tile: View detailed info", Color.White, rightColX, rightY); rightY += lineHeight;
        rightY += 5;

        DrawTextAt("=== OVERLAYS & FEATURES ===", Color.Yellow, rightColX, rightY);
        rightY += lineHeight + 3;
        DrawTextAt("V: Toggle volcanoes", Color.White, rightColX, rightY); rightY += lineHeight;
        DrawTextAt("B: Toggle rivers", Color.White, rightColX, rightY); rightY += lineHeight;
        DrawTextAt("N: Toggle plate boundaries", Color.White, rightColX, rightY); rightY += lineHeight;
        DrawTextAt("P: Toggle 3D minimap", Color.White, rightColX, rightY); rightY += lineHeight;
        DrawTextAt("C: Day/Night cycle", Color.Cyan, rightColX, rightY); rightY += lineHeight;
        rightY += 5;

        DrawTextAt("=== MAP EDITOR (M key) ===", Color.Yellow, rightColX, rightY);
        rightY += lineHeight + 3;
        DrawTextAt("F6: Earth preset", Color.White, rightColX, rightY); rightY += lineHeight;
        DrawTextAt("F7: Mars preset", Color.White, rightColX, rightY); rightY += lineHeight;
        DrawTextAt("F8: Water World preset", Color.White, rightColX, rightY); rightY += lineHeight;
        rightY += 5;

        DrawTextAt("=== ADVANCED TOOLS ===", Color.Yellow, rightColX, rightY);
        rightY += lineHeight + 3;
        DrawTextAt("D: Disaster controls", Color.White, rightColX, rightY); rightY += lineHeight;
        DrawTextAt("K: Pandemic controls", Color.White, rightColX, rightY); rightY += lineHeight;
        DrawTextAt("T: Manual planting tool", Color.White, rightColX, rightY); rightY += lineHeight;
        DrawTextAt("G: Control civilization", Color.White, rightColX, rightY); rightY += lineHeight;
        DrawTextAt("Y: Auto-stabilizer", Color.Cyan, rightColX, rightY); rightY += lineHeight;

        // Footer
        int footerY = panelY + panelHeight - 25;
        DrawTextAt("Watch your planet evolve from bacteria to civilizations with realistic geology and climate!",
            Color.LightGray, leftColX, footerY);
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
        if (oxygen < 10) return Color.Red;
        if (oxygen < 15) return Color.Orange;
        if (oxygen < 25) return Color.LightGreen;
        return Color.Red; // Too much oxygen
    }

    private Color GetCO2Color(float co2)
    {
        if (co2 < 0.1f) return Color.LightGreen;
        if (co2 < 1.0f) return Color.Yellow;
        return Color.Red;
    }

    private Color GetTempColor(float temp)
    {
        if (temp < 0) return Color.LightBlue;
        if (temp < 10) return Color.Cyan;
        if (temp < 25) return Color.LightGreen;
        if (temp < 35) return Color.Yellow;
        return Color.Red;
    }
}

public class GameState
{
    public int Year { get; set; }
    public float TimeSpeed { get; set; } = 1.0f;
    public bool IsPaused { get; set; }
    public float TimeAccumulator { get; set; }
}
