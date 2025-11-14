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

        // Calculate zoomed cell size for proper positioning
        int zoomedCellSize = (int)(cellSize * zoomLevel);

        // Draw volcanoes with level-of-detail enhancement
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
                        int centerX = screenX + zoomedCellSize / 2;
                        int centerY = screenY + zoomedCellSize / 2;

                        // Scale volcano triangle size with zoom level
                        int volcanoSize = Math.Max(cellSize / 2, (int)(cellSize / 2 * (1 + (zoomLevel - 1) * 0.5f)));

                        // HIGH ZOOM: Enhanced visual details (> 2x zoom)
                        if (zoomLevel > 2.0f)
                        {
                            // Draw outer glow for active volcanoes
                            if (geo.VolcanicActivity > 0.3f)
                            {
                                int glowRadius = (int)(volcanoSize * 2.0f);
                                Color glowColor = Color.OrangeRed * (0.25f * geo.VolcanicActivity);
                                DrawCircleFilled(_spriteBatch, centerX, centerY, glowRadius, glowColor);
                            }

                            // Draw heat shimmer ring at very high zoom
                            if (zoomLevel > 3.0f && geo.VolcanicActivity > 0.4f)
                            {
                                int shimmerRadius = (int)(volcanoSize * 1.6f);
                                Color shimmerColor = Color.Yellow * (0.4f * geo.VolcanicActivity);
                                DrawCircleOutline(_spriteBatch, centerX, centerY, shimmerRadius, shimmerColor, 2);
                            }
                        }

                        // Draw main volcano body with gradient-like effect
                        Color volcanoColor = geo.VolcanicActivity > 0.5f
                            ? Color.Red : new Color(180, 60, 0);

                        // Add darker base for depth at high zoom
                        if (zoomLevel > 2.5f)
                        {
                            DrawTriangle(_spriteBatch, centerX, centerY + 1, volcanoSize + 1, Color.Black * 0.4f);
                        }

                        DrawTriangle(_spriteBatch, centerX, centerY, volcanoSize, volcanoColor);

                        // Draw crater detail at high zoom
                        if (zoomLevel > 2.5f)
                        {
                            int craterSize = Math.Max(2, volcanoSize / 3);
                            Color craterColor = geo.MagmaPressure > 0.6f
                                ? Color.Orange
                                : new Color(60, 30, 10);
                            DrawCircleFilled(_spriteBatch, centerX, centerY - volcanoSize / 2, craterSize, craterColor);
                        }

                        // Active eruption effects
                        if (geo.MagmaPressure > 0.8f)
                        {
                            // Eruption burst at top
                            int starSize = Math.Max(cellSize / 3, (int)(cellSize / 3 * (1 + (zoomLevel - 1) * 0.5f)));
                            DrawStar(_spriteBatch, centerX, centerY - volcanoSize, starSize, Color.Yellow);

                            // Lava particles at very high zoom
                            if (zoomLevel > 3.5f)
                            {
                                // Draw lava spray particles
                                for (int i = 0; i < 6; i++)
                                {
                                    float angle = (i / 6.0f) * MathF.PI * 2;
                                    int particleX = centerX + (int)(MathF.Cos(angle) * volcanoSize * 1.2f);
                                    int particleY = centerY - volcanoSize + (int)(MathF.Sin(angle) * volcanoSize * 0.8f);
                                    int particleSize = Math.Max(1, volcanoSize / 6);
                                    DrawCircleFilled(_spriteBatch, particleX, particleY, particleSize, Color.Orange);
                                }
                            }

                            // Pulsing glow for high activity
                            if (zoomLevel > 2.0f)
                            {
                                float pulseIntensity = (MathF.Sin((float)DateTime.Now.TimeOfDay.TotalSeconds * 3) + 1) * 0.5f;
                                int pulseRadius = (int)(volcanoSize * 1.3f);
                                DrawCircleOutline(_spriteBatch, centerX, centerY, pulseRadius,
                                    Color.Red * (0.6f * pulseIntensity), 2);
                            }
                        }
                    }
                }
            }
        }

        // Draw rivers with enhanced detail when zoomed
        if (ShowRivers && _hydrologySim != null)
        {
            foreach (var river in _hydrologySim.Rivers)
            {
                if (river.Path.Count < 2) continue;

                // Generate meandering curve points from the river path
                var curvePoints = GenerateMeanderingPath(river.Path, zoomLevel);

                // River width increases moderately with zoom for better visibility
                int lineWidth = (int)Math.Clamp(2 + (zoomLevel - 1) * 1.3f, 2, 6);

                // Enhanced color at higher zoom levels (more vibrant blue)
                Color riverColor = zoomLevel > 2.5f
                    ? new Color(80, 140, 255)  // Brighter blue when zoomed
                    : new Color(100, 150, 255); // Standard blue

                // Draw the meandering river path
                for (int i = 0; i < curvePoints.Count - 1; i++)
                {
                    var (fx1, fy1) = curvePoints[i];
                    var (fx2, fy2) = curvePoints[i + 1];

                    int screenX1 = offsetX + (int)(fx1 * zoomedCellSize) + zoomedCellSize / 2;
                    int screenY1 = offsetY + (int)(fy1 * zoomedCellSize) + zoomedCellSize / 2;
                    int screenX2 = offsetX + (int)(fx2 * zoomedCellSize) + zoomedCellSize / 2;
                    int screenY2 = offsetY + (int)(fy2 * zoomedCellSize) + zoomedCellSize / 2;

                    // HIGH ZOOM: Add shimmer/reflection effects
                    if (zoomLevel > 2.5f)
                    {
                        // Draw lighter "reflection" line on top
                        Color shimmerColor = new Color(150, 200, 255) * 0.6f;
                        int shimmerWidth = Math.Max(1, lineWidth / 2);
                        DrawLine(_spriteBatch, screenX1, screenY1 - 1, screenX2, screenY2 - 1,
                               shimmerColor, shimmerWidth);
                    }

                    // Draw main river
                    DrawLine(_spriteBatch, screenX1, screenY1, screenX2, screenY2,
                           riverColor, lineWidth);

                    // VERY HIGH ZOOM: Add flow indicators (dots along the path)
                    if (zoomLevel > 3.5f && i % 3 == 0)
                    {
                        float distance = MathF.Sqrt((screenX2 - screenX1) * (screenX2 - screenX1) +
                                                    (screenY2 - screenY1) * (screenY2 - screenY1));
                        if (distance > 8)
                        {
                            // Animated flow dots
                            float flowOffset = ((float)DateTime.Now.TimeOfDay.TotalSeconds * 10) % distance;
                            float dotX = screenX1 + (screenX2 - screenX1) * (flowOffset / distance);
                            float dotY = screenY1 + (screenY2 - screenY1) * (flowOffset / distance);
                            DrawCircleFilled(_spriteBatch, (int)dotX, (int)dotY, 2, Color.White * 0.8f);
                        }
                    }
                }

                // River source indicator at very high zoom
                if (zoomLevel > 3.0f && curvePoints.Count > 0)
                {
                    var (fx, fy) = curvePoints[0];
                    int screenX = offsetX + (int)(fx * zoomedCellSize) + zoomedCellSize / 2;
                    int screenY = offsetY + (int)(fy * zoomedCellSize) + zoomedCellSize / 2;
                    DrawCircleFilled(_spriteBatch, screenX, screenY, lineWidth + 2, new Color(100, 180, 255));
                    DrawCircleOutline(_spriteBatch, screenX, screenY, lineWidth + 4, Color.Cyan, 1);
                }
            }
        }

        // Draw plate boundaries with level-of-detail
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
                        int centerX = screenX + zoomedCellSize / 2;
                        int centerY = screenY + zoomedCellSize / 2;

                        Color boundaryColor = geo.BoundaryType switch
                        {
                            PlateBoundaryType.Divergent => Color.Yellow,
                            PlateBoundaryType.Convergent => Color.Red,
                            PlateBoundaryType.Transform => Color.Orange,
                            _ => Color.White
                        };

                        // Increase opacity moderately when zoomed for better visibility
                        float alpha = Math.Clamp(0.5f + (zoomLevel - 1) * 0.1f, 0.5f, 0.8f);

                        // Draw base boundary cell
                        _spriteBatch.Draw(_pixelTexture,
                            new Rectangle(screenX, screenY, zoomedCellSize, zoomedCellSize),
                            boundaryColor * alpha);

                        // HIGH ZOOM: Add boundary type indicators
                        if (zoomLevel > 2.5f)
                        {
                            int indicatorSize = Math.Max(2, cellSize / 3);

                            switch (geo.BoundaryType)
                            {
                                case PlateBoundaryType.Divergent:
                                    // Divergent - arrows pointing apart (<<  >>)
                                    DrawArrow(_spriteBatch, centerX - indicatorSize, centerY, indicatorSize / 2, -1, Color.Yellow);
                                    DrawArrow(_spriteBatch, centerX + indicatorSize, centerY, indicatorSize / 2, 1, Color.Yellow);
                                    break;

                                case PlateBoundaryType.Convergent:
                                    // Convergent - arrows pointing together (>>  <<)
                                    DrawArrow(_spriteBatch, centerX - indicatorSize, centerY, indicatorSize / 2, 1, Color.Red);
                                    DrawArrow(_spriteBatch, centerX + indicatorSize, centerY, indicatorSize / 2, -1, Color.Red);
                                    break;

                                case PlateBoundaryType.Transform:
                                    // Transform - arrows sliding past each other
                                    DrawArrow(_spriteBatch, centerX, centerY - indicatorSize, indicatorSize / 2, 0, Color.Orange, true);
                                    DrawArrow(_spriteBatch, centerX, centerY + indicatorSize, indicatorSize / 2, 0, Color.Orange, true, true);
                                    break;
                            }
                        }

                        // VERY HIGH ZOOM: Add stress visualization
                        if (zoomLevel > 3.5f && geo.TectonicStress > 0.5f)
                        {
                            // Pulsing stress indicator
                            float pulseIntensity = (MathF.Sin((float)DateTime.Now.TimeOfDay.TotalSeconds * 4) + 1) * 0.5f;
                            int stressRadius = (int)(cellSize * 0.8f);
                            Color stressColor = Color.White * (0.3f * geo.TectonicStress * pulseIntensity);
                            DrawCircleOutline(_spriteBatch, centerX, centerY, stressRadius, stressColor, 1);
                        }
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

    private void DrawCircleFilled(SpriteBatch sb, int centerX, int centerY, int radius, Color color)
    {
        // Draw a filled circle using the midpoint circle algorithm
        for (int y = -radius; y <= radius; y++)
        {
            for (int x = -radius; x <= radius; x++)
            {
                if (x * x + y * y <= radius * radius)
                {
                    sb.Draw(_pixelTexture,
                        new Rectangle(centerX + x, centerY + y, 1, 1),
                        color);
                }
            }
        }
    }

    private void DrawCircleOutline(SpriteBatch sb, int centerX, int centerY, int radius, Color color, int thickness)
    {
        // Draw a circle outline using the midpoint circle algorithm
        int innerRadius = Math.Max(0, radius - thickness);
        for (int y = -radius; y <= radius; y++)
        {
            for (int x = -radius; x <= radius; x++)
            {
                int distSq = x * x + y * y;
                if (distSq <= radius * radius && distSq >= innerRadius * innerRadius)
                {
                    sb.Draw(_pixelTexture,
                        new Rectangle(centerX + x, centerY + y, 1, 1),
                        color);
                }
            }
        }
    }

    private void DrawArrow(SpriteBatch sb, int x, int y, int size, int direction, Color color,
                          bool vertical = false, bool reverse = false)
    {
        // Draw a simple arrow indicator
        // direction: -1 = left/up, 1 = right/down, 0 = horizontal for vertical arrows
        if (vertical)
        {
            // Vertical arrow (up or down)
            int dirY = reverse ? 1 : -1;
            // Arrow line
            sb.Draw(_pixelTexture, new Rectangle(x, y - size, 1, size * 2), color);
            // Arrow head
            sb.Draw(_pixelTexture, new Rectangle(x - size / 2, y + dirY * size, size, 1), color);
            sb.Draw(_pixelTexture, new Rectangle(x - size / 3, y + dirY * (size - 1), size / 3 * 2, 1), color);
        }
        else
        {
            // Horizontal arrow (left or right)
            int dirX = direction;
            // Arrow line
            sb.Draw(_pixelTexture, new Rectangle(x - size, y, size * 2, 1), color);
            // Arrow head
            sb.Draw(_pixelTexture, new Rectangle(x + dirX * size, y - size / 2, 1, size), color);
            sb.Draw(_pixelTexture, new Rectangle(x + dirX * (size - 1), y - size / 3, 1, size / 3 * 2), color);
        }
    }

    private List<(float x, float y)> GenerateMeanderingPath(List<(int x, int y)> path, float zoomLevel)
    {
        var result = new List<(float x, float y)>();

        if (path.Count < 2)
        {
            foreach (var p in path)
                result.Add((p.x, p.y));
            return result;
        }

        // More subdivision for higher zoom levels
        int subdivisions = Math.Max(2, (int)(2 + zoomLevel * 1.5f));

        // Generate smooth curves using Catmull-Rom splines with meandering
        for (int i = 0; i < path.Count - 1; i++)
        {
            var p0 = i > 0 ? path[i - 1] : path[i];
            var p1 = path[i];
            var p2 = path[i + 1];
            var p3 = (i + 2 < path.Count) ? path[i + 2] : path[i + 1];

            // Calculate perpendicular offset for meandering
            float dx = p2.x - p1.x;
            float dy = p2.y - p1.y;
            float length = MathF.Sqrt(dx * dx + dy * dy);

            if (length > 0)
            {
                // Perpendicular vector
                float perpX = -dy / length;
                float perpY = dx / length;

                // Create meandering effect based on position along river
                // Use hash-like function for deterministic but random-looking meanders
                int seed = p1.x * 1000 + p1.y;
                float meander1 = MathF.Sin(seed * 0.1f) * 0.3f;
                float meander2 = MathF.Cos(seed * 0.15f) * 0.2f;

                for (int t = 0; t < subdivisions; t++)
                {
                    float u = t / (float)subdivisions;

                    // Catmull-Rom spline interpolation
                    float u2 = u * u;
                    float u3 = u2 * u;

                    float x = 0.5f * (
                        (2 * p1.x) +
                        (-p0.x + p2.x) * u +
                        (2 * p0.x - 5 * p1.x + 4 * p2.x - p3.x) * u2 +
                        (-p0.x + 3 * p1.x - 3 * p2.x + p3.x) * u3
                    );

                    float y = 0.5f * (
                        (2 * p1.y) +
                        (-p0.y + p2.y) * u +
                        (2 * p0.y - 5 * p1.y + 4 * p2.y - p3.y) * u2 +
                        (-p0.y + 3 * p1.y - 3 * p2.y + p3.y) * u3
                    );

                    // Add meandering offset (stronger at curve midpoints)
                    float meanderStrength = MathF.Sin(u * MathF.PI); // Peaks at middle of segment
                    float offsetAmount = (meander1 + meander2 * MathF.Sin(u * 2 * MathF.PI)) * meanderStrength;

                    x += perpX * offsetAmount;
                    y += perpY * offsetAmount;

                    result.Add((x, y));
                }
            }
            else
            {
                // Straight segment (no length), just add points
                for (int t = 0; t < subdivisions; t++)
                {
                    float u = t / (float)subdivisions;
                    float x = p1.x + (p2.x - p1.x) * u;
                    float y = p1.y + (p2.y - p1.y) * u;
                    result.Add((x, y));
                }
            }
        }

        // Add final point
        result.Add((path[path.Count - 1].x, path[path.Count - 1].y));

        return result;
    }
}
