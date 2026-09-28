using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using System.Linq;

namespace SimPlanet;

public class GraphData
{
    public List<float> Values { get; } = new List<float>();
    public string Name { get; }
    public Color GraphColor { get; }
    private int _maxDataPoints;

    public GraphData(string name, Color color, int maxDataPoints = 500)
    {
        Name = name;
        GraphColor = color;
        _maxDataPoints = maxDataPoints;
    }

    public void AddValue(float value)
    {
        Values.Add(value);
        if (Values.Count > _maxDataPoints)
        {
            Values.RemoveAt(0);
        }
    }
}

public class Graphs
{
    private readonly GraphicsDevice _graphicsDevice;
    private readonly FontRenderer _font;
    private readonly PlanetMap _map;
    private readonly CivilizationManager _civManager;
    private readonly Dictionary<string, GraphData> _graphData = new Dictionary<string, GraphData>();
    private float _updateTimer = 0f;
    private const float UPDATE_INTERVAL = 2.0f; // Update graphs every 2 seconds
    private Texture2D _pixelTexture;
    private Texture2D _backgroundTexture;

    public bool IsVisible { get; set; } = false;

    public Graphs(GraphicsDevice graphicsDevice, FontRenderer font, PlanetMap map, CivilizationManager civManager)
    {
        _graphicsDevice = graphicsDevice;
        _font = font;
        _map = map;
        _civManager = civManager;

        _graphData.Add("Temperature", new GraphData("Global Temp (deg C)", Color.Red));
        _graphData.Add("Oxygen", new GraphData("Oxygen (%)", Color.Cyan));
        _graphData.Add("CO2", new GraphData("CO2 (ppm)", Color.Gray));
        _graphData.Add("Population", new GraphData("Total Population", Color.LawnGreen));
        _graphData.Add("Biomass", new GraphData("Total Biomass", Color.Orange));

        _pixelTexture = new Texture2D(_graphicsDevice, 1, 1);
        _pixelTexture.SetData(new[] { Color.White });

        _backgroundTexture = new Texture2D(_graphicsDevice, 1, 1);
        _backgroundTexture.SetData(new[] { new Color(0, 0, 0, 200) });
    }

    public void Update(float deltaTime)
    {
        // Keep sampling while hidden so the history is there when the panel opens
        _updateTimer += deltaTime;
        if (_updateTimer >= UPDATE_INTERVAL)
        {
            _updateTimer = 0f;
            UpdateGraphData();
        }
    }

    private void UpdateGraphData()
    {
        _graphData["Temperature"].AddValue(_map.GlobalTemperature);
        _graphData["Oxygen"].AddValue(_map.GlobalOxygen);
        _graphData["CO2"].AddValue(_map.GlobalCO2);

        // Use the renderer's thread-safe snapshot instead of the live civilization list
        long totalPopulation = CivRenderData.Latest.Civs.Sum(c => (long)c.Population);
        _graphData["Population"].AddValue(totalPopulation);

        float totalBiomass = 0;
        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                totalBiomass += _map.Cells[x, y].Biomass;
            }
        }
        _graphData["Biomass"].AddValue(totalBiomass / (_map.Width * _map.Height));
    }

    public void Draw(SpriteBatch spriteBatch, int screenWidth, int screenHeight)
    {
        if (!IsVisible) return;

        // Small multiples: one chart per measure, each with its own y-scale
        // (different units must never share an axis).
        int panelWidth = Math.Min(860, screenWidth - 40);
        int panelHeight = Math.Min(600, screenHeight - 140);
        var panel = new Rectangle((screenWidth - panelWidth) / 2, (screenHeight - panelHeight) / 2, panelWidth, panelHeight);

        spriteBatch.End();
        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp);

        int contentY = UITheme.DrawTitledPanel(spriteBatch, panel, "PLANETARY STATISTICS", UITheme.Gold, 40);
        UITheme.DrawText(spriteBatch, "Sampled every 2 s  -  Y to close", new Vector2(panel.Right - 220, panel.Y + 12), UITheme.TextMuted, UITheme.FontSmall);

        var series = _graphData.Values.ToList();
        int cols = 2;
        int rows = (series.Count + cols - 1) / cols;
        int gap = 12;
        int cellW = (panelWidth - 24 - gap * (cols - 1)) / cols;
        int cellH = (panel.Bottom - 12 - contentY - gap * (rows - 1)) / rows;
        var mouse = Microsoft.Xna.Framework.Input.Mouse.GetState();

        for (int i = 0; i < series.Count; i++)
        {
            var rect = new Rectangle(panel.X + 12 + (i % cols) * (cellW + gap), contentY + (i / cols) * (cellH + gap), cellW, cellH);
            DrawSmallChart(spriteBatch, series[i], rect, mouse.Position);
        }

        spriteBatch.End();
        spriteBatch.Begin(samplerState: SamplerState.PointClamp);
    }

    private static string FormatValue(float v)
    {
        float a = Math.Abs(v);
        if (a >= 1_000_000) return $"{v / 1_000_000f:0.##}M";
        if (a >= 1_000) return $"{v / 1_000f:0.#}K";
        if (a >= 10) return $"{v:0.#}";
        return $"{v:0.###}";
    }

    private void DrawSmallChart(SpriteBatch spriteBatch, GraphData data, Rectangle rect, Point mouse)
    {
        UITheme.FillRounded(spriteBatch, rect, new Color(8, 12, 20, 220));
        UITheme.OutlineRounded(spriteBatch, rect, UITheme.Border);

        // Title names the series; the current value is the headline
        UITheme.DrawText(spriteBatch, data.Name, new Vector2(rect.X + 10, rect.Y + 8), UITheme.TextDim, UITheme.FontSmall);
        string current = data.Values.Count > 0 ? FormatValue(data.Values[^1]) : "-";
        var cs = UITheme.Measure(current, UITheme.FontMedium);
        UITheme.DrawText(spriteBatch, current, new Vector2(rect.Right - cs.X - 10, rect.Y + 6), UITheme.Text, UITheme.FontMedium);

        var plot = new Rectangle(rect.X + 44, rect.Y + 32, rect.Width - 56, rect.Height - 44);
        Color line = UITheme.Accent;

        if (data.Values.Count < 2)
        {
            UITheme.DrawTextCentered(spriteBatch, "Collecting data...", plot, UITheme.TextMuted, UITheme.FontSmall);
            return;
        }

        float min = data.Values.Min();
        float max = data.Values.Max();
        if (max - min < 1e-4f) { max += 0.5f; min -= 0.5f; }
        float pad = (max - min) * 0.08f;
        min -= pad; max += pad;

        // Recessive grid with three y labels
        for (int g = 0; g <= 2; g++)
        {
            int gy = plot.Y + (int)(g / 2f * plot.Height);
            spriteBatch.Draw(_pixelTexture, new Rectangle(plot.X, gy, plot.Width, 1), Color.White * 0.07f);
            float v = max - g / 2f * (max - min);
            string label = FormatValue(v);
            var ls = UITheme.Measure(label, 10f);
            UITheme.DrawText(spriteBatch, label, new Vector2(plot.X - ls.X - 6, gy - ls.Y / 2f), UITheme.TextMuted, 10f);
        }

        int n = data.Values.Count;
        Vector2 P(int i) => new Vector2(plot.X + (float)i / (n - 1) * plot.Width,
                                        plot.Bottom - (data.Values[i] - min) / (max - min) * plot.Height);

        // Soft area under the line, then the 2px line
        for (int i = 0; i < n - 1; i++)
        {
            var p1 = P(i);
            var p2 = P(i + 1);
            int x1 = (int)p1.X, x2 = Math.Max(x1 + 1, (int)p2.X);
            int top = (int)Math.Min(p1.Y, p2.Y);
            spriteBatch.Draw(_pixelTexture, new Rectangle(x1, top, x2 - x1, plot.Bottom - top), line * 0.10f);
        }
        for (int i = 0; i < n - 1; i++)
        {
            DrawLine(spriteBatch, _pixelTexture, P(i), P(i + 1), line, 2);
        }

        // Hover crosshair with the value under the cursor
        if (plot.Contains(mouse))
        {
            int idx = Math.Clamp((int)MathF.Round((mouse.X - plot.X) / (float)plot.Width * (n - 1)), 0, n - 1);
            var p = P(idx);
            spriteBatch.Draw(_pixelTexture, new Rectangle((int)p.X, plot.Y, 1, plot.Height), Color.White * 0.35f);
            UITheme.FillRounded(spriteBatch, new Rectangle((int)p.X - 4, (int)p.Y - 4, 8, 8), UITheme.Text);
            UITheme.FillRounded(spriteBatch, new Rectangle((int)p.X - 2, (int)p.Y - 2, 4, 4), line);
            int ago = (n - 1 - idx) * (int)UPDATE_INTERVAL;
            UITheme.DrawTooltip(spriteBatch, FormatValue(data.Values[idx]) + "\n" + (ago == 0 ? "now" : ago + " s ago"),
                new Point((int)p.X, (int)p.Y), _graphicsDevice.Viewport.Width, _graphicsDevice.Viewport.Height, preferAbove: true);
        }
        else
        {
            // Mark the latest sample
            var last = P(n - 1);
            UITheme.FillRounded(spriteBatch, new Rectangle((int)last.X - 4, (int)last.Y - 4, 8, 8), line);
        }
    }

    private void DrawLine(SpriteBatch spriteBatch, Texture2D texture, Vector2 point1, Vector2 point2, Color color, float thickness)
    {
        if (point1 == point2) return;
        float distance = Vector2.Distance(point1, point2);
        float angle = (float)System.Math.Atan2(point2.Y - point1.Y, point2.X - point1.X);
        spriteBatch.Draw(texture, point1, null, color, angle, Vector2.Zero, new Vector2(distance, thickness), SpriteEffects.None, 0);
    }
}
