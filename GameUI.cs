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

    public bool ShowHelp { get; set; } = true;

    public GameUI(SpriteBatch spriteBatch, SimpleFont font, PlanetMap map, GraphicsDevice graphicsDevice)
    {
        _spriteBatch = spriteBatch;
        _font = font;
        _map = map;

        _pixelTexture = new Texture2D(graphicsDevice, 1, 1);
        _pixelTexture.SetData(new[] { Color.White });
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
        int panelWidth = 300;
        int panelHeight = 400;

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

        DrawText($"View Mode: {renderMode}", Color.Magenta);
    }

    private void DrawHelpPanel()
    {
        int panelX = 320;
        int panelY = 10;
        int panelWidth = 450;
        int panelHeight = 380;

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
        DrawText("1-7: Change view mode", Color.White);
        DrawText("  1: Terrain  2: Temperature", Color.Gray);
        DrawText("  3: Rainfall  4: Life", Color.Gray);
        DrawText("  5: Oxygen  6: CO2  7: Elevation", Color.Gray);
        DrawText("+/-: Change time speed", Color.White);
        DrawText("L: Seed initial life", Color.White);
        DrawText("M: Map generation options", Color.White);
        DrawText("R: Regenerate planet", Color.White);
        DrawText("H: Toggle this help", Color.White);
        DrawText("ESC: Quit", Color.White);
        textY += 5;

        DrawText("=== GAME INFO ===", Color.Cyan);
        DrawText("Watch your planet evolve!", Color.White);
        DrawText("Life will emerge in suitable", Color.White);
        DrawText("conditions and evolve over time.", Color.White);
        DrawText("Monitor oxygen, CO2, and", Color.White);
        DrawText("temperature for habitability.", Color.White);
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
