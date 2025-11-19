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

        _graphData.Add("Temperature", new GraphData("Global Temp (°C)", Color.Red));
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
        if (!IsVisible) return;

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

        long totalPopulation = 0;
        if (_civManager.Civilizations != null)
        {
            totalPopulation = _civManager.Civilizations.Sum(c => (long)c.Population);
        }
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

        int graphWidth = 800;
        int graphHeight = 500;
        int xPos = (screenWidth - graphWidth) / 2;
        int yPos = (screenHeight - graphHeight) / 2;

        // Background
        spriteBatch.Draw(_backgroundTexture, new Rectangle(xPos, yPos, graphWidth, graphHeight), Color.White);

        // Title
        _font.DrawString(spriteBatch, "Planetary Statistics Over Time", new Vector2(xPos + 10, yPos + 10), Color.White);

        // Draw each graph
        int graphAreaX = xPos + 60;
        int graphAreaY = yPos + 50;
        int graphAreaWidth = graphWidth - 80;
        int graphAreaHeight = graphHeight - 70;

        DrawGrid(spriteBatch, graphAreaX, graphAreaY, graphAreaWidth, graphAreaHeight);

        foreach (var data in _graphData.Values)
        {
            DrawGraphLine(spriteBatch, data, graphAreaX, graphAreaY, graphAreaWidth, graphAreaHeight);
        }

        DrawLegend(spriteBatch, xPos + graphWidth - 150, yPos + 50);
    }

    private void DrawGrid(SpriteBatch spriteBatch, int x, int y, int width, int height)
    {
        // Horizontal lines
        for (int i = 0; i <= 10; i++)
        {
            int lineY = y + (int)(i / 10f * height);
            spriteBatch.Draw(_pixelTexture, new Rectangle(x, lineY, width, 1), new Color(Color.White, 0.2f));
        }

        // Vertical lines
        for (int i = 0; i <= 10; i++)
        {
            int lineX = x + (int)(i / 10f * width);
            spriteBatch.Draw(_pixelTexture, new Rectangle(lineX, y, 1, height), new Color(Color.White, 0.2f));
        }
    }

    private void DrawGraphLine(SpriteBatch spriteBatch, GraphData data, int x, int y, int width, int height)
    {
        if (data.Values.Count == 0) return;

        float min = data.Values.Min();
        float max = data.Values.Max();
        if (max - min < 0.001f)
        {
            max += 1;
        }

        for (int i = 0; i < data.Values.Count - 1; i++)
        {
            float x1 = x + (float)i / (data.Values.Count - 1) * width;
            float y1 = y + height - (data.Values[i] - min) / (max - min) * height;
            float x2 = x + (float)(i + 1) / (data.Values.Count - 1) * width;
            float y2 = y + height - (data.Values[i + 1] - min) / (max - min) * height;

            DrawLine(spriteBatch, _pixelTexture, new Vector2(x1, y1), new Vector2(x2, y2), data.GraphColor, 2);
        }
    }

    private void DrawLegend(SpriteBatch spriteBatch, int x, int y)
    {
        int yOffset = 0;
        foreach (var data in _graphData.Values)
        {
            spriteBatch.Draw(_pixelTexture, new Rectangle(x, y + yOffset, 10, 10), data.GraphColor);
            _font.DrawString(spriteBatch, data.Name, new Vector2(x + 15, y + yOffset - 2), Color.White);
            yOffset += 20;
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
