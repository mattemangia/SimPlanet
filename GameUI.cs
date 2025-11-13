using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Text;

namespace SimPlanet;

/// <summary>
/// Handles UI rendering and information display
/// </summary>
public class GameUI
{
    private readonly SimpleFont _font;
    private readonly SpriteBatch _spriteBatch;
    private readonly PlanetMap _map;
    private Texture2D _pixelTexture;
    private CivilizationManager? _civilizationManager;
    private WeatherSystem? _weatherSystem;

    public bool ShowHelp { get; set; } = true;

    public GameUI(SpriteBatch spriteBatch, SimpleFont font, PlanetMap map, GraphicsDevice graphicsDevice)
    {
        _spriteBatch = spriteBatch;
        _font = font;
        _map = map;

        _pixelTexture = new Texture2D(graphicsDevice, 1, 1);
        _pixelTexture.SetData(new[] { Color.White });
    }

    public void SetManagers(CivilizationManager civilizationManager, WeatherSystem weatherSystem)
    {
        _civilizationManager = civilizationManager;
        _weatherSystem = weatherSystem;
    }

    public void Draw(GameState state, RenderMode renderMode)
    {
        DrawInfoPanel(state, renderMode);

        if (ShowHelp)
        {
            DrawHelpPanel();
        }
    }

    private void DrawInfoPanel(GameState state, RenderMode renderMode)
    {
        int panelX = 10;
        int panelY = 10;
        int panelWidth = 320;
        int panelHeight = 580;

        // Draw semi-transparent background
        DrawRectangle(panelX, panelY, panelWidth, panelHeight, new Color(0, 0, 0, 180));

        int textY = panelY + 10;
        int lineHeight = 20;

        void DrawText(string text, Color color)
        {
            _font.DrawString(_spriteBatch, text, new Vector2(panelX + 10, textY), color);
            textY += lineHeight;
        }

        DrawText("=== SIM PLANET ===", Color.Yellow);
        DrawText($"Year: {state.Year}", Color.White);
        DrawText($"Speed: {state.TimeSpeed}x", Color.White);
        DrawText($"Paused: {state.IsPaused}", Color.White);
        textY += 5;

        DrawText("=== Global Stats ===", Color.Cyan);
        DrawText($"Oxygen: {_map.GlobalOxygen:F1}%", GetOxygenColor(_map.GlobalOxygen));
        DrawText($"CO2: {_map.GlobalCO2:F2}%", GetCO2Color(_map.GlobalCO2));
        DrawText($"Avg Temp: {_map.GlobalTemperature:F1}C", GetTempColor(_map.GlobalTemperature));
        DrawText($"Solar: {_map.SolarEnergy:F2}", Color.Yellow);
        textY += 5;

        DrawText("=== Life Statistics ===", Color.Green);
        var lifeStats = CalculateLifeStats();
        DrawText($"Bacteria: {lifeStats[LifeForm.Bacteria]}", Color.Gray);
        DrawText($"Algae: {lifeStats[LifeForm.Algae]}", Color.LightGreen);
        DrawText($"Plants: {lifeStats[LifeForm.PlantLife]}", Color.Green);
        DrawText($"Simple Animals: {lifeStats[LifeForm.SimpleAnimals]}", Color.SandyBrown);
        DrawText($"Complex Animals: {lifeStats[LifeForm.ComplexAnimals]}", Color.Orange);
        DrawText($"Intelligence: {lifeStats[LifeForm.Intelligence]}", Color.Gold);
        DrawText($"Civilization: {lifeStats[LifeForm.Civilization]}", Color.Yellow);
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

        DrawText($"View Mode: {renderMode}", Color.Magenta);
    }

    private void DrawHelpPanel()
    {
        int panelX = 340;
        int panelY = 10;
        int panelWidth = 480;
        int panelHeight = 480;

        DrawRectangle(panelX, panelY, panelWidth, panelHeight, new Color(0, 0, 0, 180));

        int textY = panelY + 10;
        int lineHeight = 20;

        void DrawText(string text, Color color)
        {
            _font.DrawString(_spriteBatch, text, new Vector2(panelX + 10, textY), color);
            textY += lineHeight;
        }

        DrawText("=== CONTROLS ===", Color.Yellow);
        DrawText("SPACE: Pause/Resume", Color.White);
        DrawText("1-0: View modes", Color.White);
        DrawText("  1: Terrain  2: Temp  3: Rain", Color.Gray);
        DrawText("  4: Life  5: O2  6: CO2  7: Elev", Color.Gray);
        DrawText("  8: Geology  9: Plates  0: Volc", Color.Gray);
        DrawText("F1-F4: Meteorology views", Color.White);
        DrawText("  F1: Clouds  F2: Wind", Color.Gray);
        DrawText("  F3: Pressure  F4: Storms", Color.Gray);
        DrawText("+/-: Time speed  L: Seed life", Color.White);
        DrawText("C: Day/Night cycle (auto at <0.5x)", Color.Cyan);
        DrawText("Mouse Wheel: Zoom", Color.Cyan);
        DrawText("Middle Click+Drag: Pan camera", Color.Cyan);
        DrawText("P: 3D Minimap  M: Map options", Color.White);
        DrawText("V/B/N: Volc/Rivers/Plates", Color.White);
        DrawText("R: Regenerate  H: Help", Color.White);
        DrawText("F5: Quick Save  F9: Quick Load", Color.Cyan);
        DrawText("ESC: Pause/Menu", Color.White);
        textY += 5;

        DrawText("=== INTERACTIVE CONTROLS ===", Color.Yellow);
        DrawText("Use buttons at bottom of screen:", Color.White);
        DrawText("  Terraform: Restore habitats", Color.Green);
        DrawText("  Cool: Reduce global temp", Color.LightBlue);
        DrawText("  Seed Life: Add bacteria", Color.LightGreen);
        DrawText("  Clear Pollution: Reduce CO2", Color.Cyan);
        textY += 5;

        DrawText("=== GAME INFO ===", Color.Cyan);
        DrawText("Watch your planet evolve!", Color.White);
        DrawText("Life emerges and evolves through", Color.White);
        DrawText("bacteria to civilizations.", Color.White);
        DrawText("Weather systems, plate tectonics,", Color.White);
        DrawText("and geological events shape the", Color.White);
        DrawText("world. Civilizations develop tech", Color.White);
        DrawText("and interact with the environment.", Color.White);
        textY += 5;

        DrawText("Press H to hide this panel", Color.Yellow);
    }

    private void DrawRectangle(int x, int y, int width, int height, Color color)
    {
        _spriteBatch.Draw(_pixelTexture, new Rectangle(x, y, width, height), color);
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
