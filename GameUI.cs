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
        var lifeStats = CalculateLifeStats();
        DrawText($"Bacteria: {lifeStats[LifeForm.Bacteria]}", Color.Gray);
        DrawText($"Algae: {lifeStats[LifeForm.Algae]}", Color.LightGreen);
        DrawText($"Plants: {lifeStats[LifeForm.PlantLife]}", Color.Green);
        DrawText($"Simple Animals: {lifeStats[LifeForm.SimpleAnimals]}", Color.SandyBrown);

        // Vertebrate evolution
        if (lifeStats[LifeForm.Fish] > 0)
            DrawText($"Fish: {lifeStats[LifeForm.Fish]}", new Color(100, 120, 200));
        if (lifeStats[LifeForm.Amphibians] > 0)
            DrawText($"Amphibians: {lifeStats[LifeForm.Amphibians]}", new Color(120, 160, 80));
        if (lifeStats[LifeForm.Reptiles] > 0)
            DrawText($"Reptiles: {lifeStats[LifeForm.Reptiles]}", new Color(140, 140, 60));

        // Age of Dinosaurs
        if (lifeStats[LifeForm.Dinosaurs] > 0)
            DrawText($"DINOSAURS: {lifeStats[LifeForm.Dinosaurs]}", Color.Orange);
        if (lifeStats[LifeForm.MarineDinosaurs] > 0)
            DrawText($"Marine Dinosaurs: {lifeStats[LifeForm.MarineDinosaurs]}", new Color(100, 100, 180));
        if (lifeStats[LifeForm.Pterosaurs] > 0)
            DrawText($"Pterosaurs: {lifeStats[LifeForm.Pterosaurs]}", new Color(160, 140, 100));

        // Age of Mammals
        if (lifeStats[LifeForm.Mammals] > 0)
            DrawText($"Mammals: {lifeStats[LifeForm.Mammals]}", new Color(160, 120, 90));
        if (lifeStats[LifeForm.Birds] > 0)
            DrawText($"Birds: {lifeStats[LifeForm.Birds]}", new Color(150, 180, 150));

        if (lifeStats[LifeForm.ComplexAnimals] > 0)
            DrawText($"Complex Animals: {lifeStats[LifeForm.ComplexAnimals]}", Color.Orange);
        if (lifeStats[LifeForm.Intelligence] > 0)
            DrawText($"Intelligence: {lifeStats[LifeForm.Intelligence]}", Color.Gold);
        if (lifeStats[LifeForm.Civilization] > 0)
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
    }

    private void DrawHelpPanel()
    {
        // Position help panel in the map area (right side)
        int infoPanelWidth = 400;
        int panelX = infoPanelWidth + 20;
        int panelY = 20;
        int panelWidth = 520;
        int panelHeight = Math.Min(600, _graphicsDevice.Viewport.Height - 40);

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
        DrawText("D: Disasters  K: Pandemics", Color.White);
        DrawText("T: Manual planting  G: Control civ", Color.White);
        DrawText("Y: Auto-stabilizer", Color.Cyan);
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
