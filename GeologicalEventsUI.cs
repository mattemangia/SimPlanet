using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace SimPlanet;

/// <summary>
/// Displays geological events like volcanoes, earthquakes, and rivers
/// </summary>
public class GeologicalEventsUI
{
    private readonly FontRenderer _font;
    private readonly SpriteBatch _spriteBatch;
    private readonly GraphicsDevice _graphicsDevice;
    private Texture2D _pixelTexture;
    private GeologicalSimulator _geologicalSim;
    private HydrologySimulator _hydrologySim;

    private Queue<string> _eventLog = new();
    private const int MaxLogEntries = 5;

    public bool ShowEvents { get; set; } = true;
    public bool ShowRivers { get; set; } = true;
    public bool ShowPlates { get; set; } = false;
    public bool ShowVolcanoes { get; set; } = true;

    public GeologicalEventsUI(SpriteBatch spriteBatch, FontRenderer font,
                              GraphicsDevice graphicsDevice)
    {
        _spriteBatch = spriteBatch;
        _font = font;
        _graphicsDevice = graphicsDevice;

        _pixelTexture = new Texture2D(_graphicsDevice, 1, 1);
        _pixelTexture.SetData(new[] { Color.White });
    }

    public void SetSimulators(GeologicalSimulator geoSim, HydrologySimulator hydroSim)
    {
        _geologicalSim = geoSim;
        _hydrologySim = hydroSim;
    }

    public void Update(int currentYear)
    {
        // Check for new events
        if (_geologicalSim != null)
        {
            // Check for recent eruptions
            var recentEruption = _geologicalSim.RecentEruptions
                .FirstOrDefault(e => e.year == currentYear);

            if (recentEruption != default)
            {
                LogEvent($"ERUPTION at ({recentEruption.x}, {recentEruption.y})!");
            }

            // Check for earthquakes
            if (_geologicalSim.Earthquakes.Count > 0)
            {
                var earthquake = _geologicalSim.Earthquakes.Last();
                LogEvent($"Earthquake M{earthquake.magnitude:F1} at ({earthquake.x}, {earthquake.y})");
            }
        }

        // Check for new rivers
        if (_hydrologySim != null && _hydrologySim.Rivers.Count > 0)
        {
            // Periodically log river formation
            if (currentYear % 10 == 0 && _hydrologySim.Rivers.Count > 0)
            {
                LogEvent($"{_hydrologySim.Rivers.Count} rivers flowing");
            }
        }
    }

    private void LogEvent(string message)
    {
        _eventLog.Enqueue(message);
        if (_eventLog.Count > MaxLogEntries)
        {
            _eventLog.Dequeue();
        }
    }

    public void DrawOverlay(PlanetMap map, int offsetX, int offsetY, int cellSize, float zoomLevel = 1.0f)
    {
        if (_geologicalSim == null) return;

        // Apply zoom to cell size for proper scaling
        int zoomedCellSize = (int)(cellSize * zoomLevel);

        // Draw volcanoes
        if (ShowVolcanoes)
        {
            for (int x = 0; x < map.Width; x++)
            {
                for (int y = 0; y < map.Height; y++)
                {
                    var geo = map.Cells[x, y].GetGeology();
                    if (geo.IsVolcano)
                    {
                        int screenX = offsetX + x * zoomedCellSize;
                        int screenY = offsetY + y * zoomedCellSize;

                        // Draw volcano symbol (scale with zoom)
                        Color volcanoColor = geo.VolcanicActivity > 0.5f
                            ? Color.Red : new Color(180, 60, 0);

                        DrawTriangle(_spriteBatch, screenX + zoomedCellSize / 2,
                                   screenY + zoomedCellSize / 2, zoomedCellSize / 2, volcanoColor);

                        // Active eruption
                        if (geo.MagmaPressure > 0.8f)
                        {
                            DrawStar(_spriteBatch, screenX + zoomedCellSize / 2,
                                   screenY, zoomedCellSize / 3, Color.Yellow);
                        }
                    }
                }
            }
        }

        // Draw rivers
        if (ShowRivers && _hydrologySim != null)
        {
            foreach (var river in _hydrologySim.Rivers)
            {
                for (int i = 0; i < river.Path.Count - 1; i++)
                {
                    var (x1, y1) = river.Path[i];
                    var (x2, y2) = river.Path[i + 1];

                    int screenX1 = offsetX + x1 * zoomedCellSize + zoomedCellSize / 2;
                    int screenY1 = offsetY + y1 * zoomedCellSize + zoomedCellSize / 2;
                    int screenX2 = offsetX + x2 * zoomedCellSize + zoomedCellSize / 2;
                    int screenY2 = offsetY + y2 * zoomedCellSize + zoomedCellSize / 2;

                    // Scale river line width with zoom
                    int lineWidth = Math.Max(1, (int)(2 * zoomLevel));
                    DrawLine(_spriteBatch, screenX1, screenY1, screenX2, screenY2,
                           new Color(100, 150, 255), lineWidth);
                }
            }
        }

        // Draw plate boundaries
        if (ShowPlates)
        {
            for (int x = 0; x < map.Width; x++)
            {
                for (int y = 0; y < map.Height; y++)
                {
                    var geo = map.Cells[x, y].GetGeology();
                    if (geo.BoundaryType != PlateBoundaryType.None)
                    {
                        int screenX = offsetX + x * zoomedCellSize;
                        int screenY = offsetY + y * zoomedCellSize;

                        Color boundaryColor = geo.BoundaryType switch
                        {
                            PlateBoundaryType.Divergent => Color.Yellow,
                            PlateBoundaryType.Convergent => Color.Red,
                            PlateBoundaryType.Transform => Color.Orange,
                            _ => Color.White
                        };

                        _spriteBatch.Draw(_pixelTexture,
                            new Rectangle(screenX, screenY, zoomedCellSize, zoomedCellSize),
                            boundaryColor * 0.5f);
                    }
                }
            }
        }
    }

    public void DrawEventLog(int screenWidth)
    {
        if (!ShowEvents || _eventLog.Count == 0) return;

        int panelWidth = 340;
        int panelHeight = 160;
        int panelX = screenWidth - panelWidth - 10;
        int panelY = 10;

        // Background
        _spriteBatch.Draw(_pixelTexture,
            new Rectangle(panelX, panelY, panelWidth, panelHeight),
            new Color(10, 15, 30, 230));

        // Border
        DrawRectangleOutline(panelX, panelY, panelWidth, panelHeight, new Color(255, 150, 50), 2);

        // Header bar
        _spriteBatch.Draw(_pixelTexture,
            new Rectangle(panelX, panelY, panelWidth, 28),
            new Color(80, 40, 10, 220));

        // Title
        _font.DrawString(_spriteBatch, "GEOLOGICAL EVENTS",
            new Vector2(panelX + 70, panelY + 7), new Color(255, 200, 100), 15);

        // Events
        int textY = panelY + 35;
        foreach (var eventText in _eventLog)
        {
            _font.DrawString(_spriteBatch, eventText,
                new Vector2(panelX + 10, textY), new Color(255, 255, 200), 13);
            textY += 22;
        }
    }

    public void DrawLegend(int screenHeight)
    {
        int panelX = 10;
        int panelY = screenHeight - 125;
        int panelWidth = 200;
        int panelHeight = 115;

        // Background
        _spriteBatch.Draw(_pixelTexture,
            new Rectangle(panelX, panelY, panelWidth, panelHeight),
            new Color(10, 15, 30, 230));

        // Border
        DrawRectangleOutline(panelX, panelY, panelWidth, panelHeight, new Color(100, 150, 200), 2);

        // Header
        _spriteBatch.Draw(_pixelTexture,
            new Rectangle(panelX, panelY, panelWidth, 25),
            new Color(30, 50, 80, 220));

        int y = panelY + 5;

        _font.DrawString(_spriteBatch, "LEGEND",
            new Vector2(panelX + 60, y), new Color(200, 220, 255), 14);
        y += 27;

        // Volcano symbol
        DrawTriangle(_spriteBatch, panelX + 15, y + 5, 5, Color.Red);
        _font.DrawString(_spriteBatch, " Volcano",
            new Vector2(panelX + 25, y), Color.White);
        y += 20;

        // River symbol
        DrawLine(_spriteBatch, panelX + 10, y + 5, panelX + 20, y + 5,
               new Color(100, 150, 255), 2);
        _font.DrawString(_spriteBatch, " River",
            new Vector2(panelX + 25, y), Color.White);
        y += 20;

        // Plate boundary
        _spriteBatch.Draw(_pixelTexture,
            new Rectangle(panelX + 10, y, 10, 10), Color.Red * 0.5f);
        _font.DrawString(_spriteBatch, " Plate Edge",
            new Vector2(panelX + 25, y), Color.White);
    }

    private void DrawTriangle(SpriteBatch sb, int centerX, int centerY, int size, Color color)
    {
        // Simple filled triangle (volcano shape)
        for (int y = 0; y < size; y++)
        {
            int width = (size - y) * 2;
            int x = centerX - (size - y);
            sb.Draw(_pixelTexture,
                new Rectangle(x, centerY + y - size, width, 1), color);
        }
    }

    private void DrawStar(SpriteBatch sb, int centerX, int centerY, int size, Color color)
    {
        // Simple star shape
        sb.Draw(_pixelTexture, new Rectangle(centerX - size, centerY, size * 2, 1), color);
        sb.Draw(_pixelTexture, new Rectangle(centerX, centerY - size, 1, size * 2), color);
    }

    private void DrawLine(SpriteBatch sb, int x1, int y1, int x2, int y2, Color color, int thickness)
    {
        float distance = MathF.Sqrt((x2 - x1) * (x2 - x1) + (y2 - y1) * (y2 - y1));
        float angle = MathF.Atan2(y2 - y1, x2 - x1);

        sb.Draw(_pixelTexture,
            new Rectangle(x1, y1 - thickness / 2, (int)distance, thickness),
            null, color, angle, Vector2.Zero, SpriteEffects.None, 0);
    }

    private void DrawRectangleOutline(int x, int y, int width, int height, Color color, int thickness)
    {
        // Top
        _spriteBatch.Draw(_pixelTexture, new Rectangle(x, y, width, thickness), color);
        // Bottom
        _spriteBatch.Draw(_pixelTexture, new Rectangle(x, y + height - thickness, width, thickness), color);
        // Left
        _spriteBatch.Draw(_pixelTexture, new Rectangle(x, y, thickness, height), color);
        // Right
        _spriteBatch.Draw(_pixelTexture, new Rectangle(x + width - thickness, y, thickness, height), color);
    }
}
