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

    // Overlay textures for perfect synchronization with terrain
    private Texture2D _overlayTexture;
    private Color[] _overlayColors;
    private bool _overlayDirty = true;
    private PlanetMap _map;

    private Queue<string> _eventLog = new();
    private const int MaxLogEntries = 5;

    // Track last logged events to prevent spam
    private (int x, int y, float magnitude) _lastLoggedEarthquake = (-1, -1, 0);

    public bool ShowEvents { get; set; } = true;
    public bool ShowRivers { get; set; } = true;
    public bool ShowPlates { get; set; } = false;
    public bool ShowVolcanoes { get; set; } = true;
    private bool _riversAllowedInCurrentView = true;

    public void MarkOverlayDirty() => _overlayDirty = true;

    public void SetRiverDisplayMode(bool allowRivers)
    {
        if (_riversAllowedInCurrentView != allowRivers)
        {
            _riversAllowedInCurrentView = allowRivers;
            _overlayDirty = true;
        }
    }

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
        _overlayDirty = true;
    }

    public void InitializeOverlayTexture(PlanetMap map)
    {
        _map = map;
        _overlayTexture = new Texture2D(_graphicsDevice, map.Width, map.Height);
        _overlayColors = new Color[map.Width * map.Height];
        _overlayDirty = true;
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

                // Only log if it's a new earthquake event (different from last one)
                if (earthquake != _lastLoggedEarthquake)
                {
                    LogEvent($"Earthquake M{earthquake.magnitude:F1} at ({earthquake.x}, {earthquake.y})");
                    _lastLoggedEarthquake = earthquake;
                }
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

    private void UpdateOverlayTexture()
    {
        if (!_overlayDirty || _map == null || _geologicalSim == null)
            return;

        // Clear overlay
        for (int i = 0; i < _overlayColors.Length; i++)
            _overlayColors[i] = Color.Transparent;

        // Render volcanoes to texture at 1:1 scale with triangle shapes for better visual
        if (ShowVolcanoes)
        {
            for (int x = 0; x < _map.Width; x++)
            {
                for (int y = 0; y < _map.Height; y++)
                {
                    var geo = _map.Cells[x, y].GetGeology();
                    if (geo.IsVolcano)
                    {
                        Color volcanoColor = geo.VolcanicActivity > 0.5f
                            ? Color.Red : new Color(180, 60, 0);

                        // Draw triangle shape (classic volcano icon)
                        DrawTextureTriangle(x, y, volcanoColor, 2);
                    }
                }
            }
        }

        // Render rivers to texture at 1:1 scale with thickness for visibility
        if (ShowRivers && _hydrologySim != null && _riversAllowedInCurrentView)
        {
            // BUGFIX: Create a copy of the collection to prevent modification during enumeration.
            foreach (var river in _hydrologySim.Rivers.ToList())
            {
                foreach (var (x, y) in river.Path)
                {
                    if (x >= 0 && x < _map.Width && y >= 0 && y < _map.Height)
                    {
                        // Draw 2-pixel thick rivers for better visibility
                        SetTexturePixelThick(x, y, new Color(100, 150, 255), 1);
                    }
                }
            }
        }

        // Render plate boundaries to texture at 1:1 scale
        if (ShowPlates)
        {
            for (int x = 0; x < _map.Width; x++)
            {
                for (int y = 0; y < _map.Height; y++)
                {
                    var geo = _map.Cells[x, y].GetGeology();
                    if (geo.BoundaryType != PlateBoundaryType.None)
                    {
                        Color boundaryColor = geo.BoundaryType switch
                        {
                            PlateBoundaryType.Divergent => Color.Yellow,
                            PlateBoundaryType.Convergent => Color.Red,
                            PlateBoundaryType.Transform => Color.Orange,
                            _ => Color.White
                        };

                        SetTexturePixelThick(x, y, boundaryColor * 0.6f, 0);
                    }
                }
            }
        }

        _overlayTexture.SetData(_overlayColors);
        _overlayDirty = false;
    }

    // Helper to set pixel with thickness for better visibility when scaled
    private void SetTexturePixelThick(int x, int y, Color color, int thickness)
    {
        for (int dy = -thickness; dy <= thickness; dy++)
        {
            for (int dx = -thickness; dx <= thickness; dx++)
            {
                int nx = x + dx;
                int ny = y + dy;

                if (nx >= 0 && nx < _map.Width && ny >= 0 && ny < _map.Height)
                {
                    int index = ny * _map.Width + nx;
                    // Blend with existing color (don't overwrite if already set)
                    if (_overlayColors[index].A < color.A)
                    {
                        _overlayColors[index] = color;
                    }
                }
            }
        }
    }

    // Helper to draw triangle shape into texture (for volcano icons)
    private void DrawTextureTriangle(int centerX, int centerY, Color color, int size)
    {
        // Draw filled triangle pointing upward (classic volcano/mountain shape)
        for (int row = 0; row < size; row++)
        {
            // Each row gets narrower as we go up
            int halfWidth = size - row;
            for (int dx = -halfWidth; dx <= halfWidth; dx++)
            {
                int nx = centerX + dx;
                int ny = centerY - row; // Negative to point upward

                if (nx >= 0 && nx < _map.Width && ny >= 0 && ny < _map.Height)
                {
                    int index = ny * _map.Width + nx;
                    if (_overlayColors[index].A < color.A)
                    {
                        _overlayColors[index] = color;
                    }
                }
            }
        }

        // Add base for stability
        for (int dx = -size; dx <= size; dx++)
        {
            int nx = centerX + dx;
            int ny = centerY;

            if (nx >= 0 && nx < _map.Width && ny >= 0 && ny < _map.Height)
            {
                int index = ny * _map.Width + nx;
                if (_overlayColors[index].A < color.A)
                {
                    _overlayColors[index] = color;
                }
            }
        }
    }

    public void DrawOverlay(PlanetMap map, int offsetX, int offsetY, int cellSize, float zoomLevel = 1.0f)
    {
        if (_geologicalSim == null) return;

        // Update overlay texture if needed
        UpdateOverlayTexture();

        // Draw base overlay texture using SAME scaling as terrain - guaranteed perfect alignment
        int zoomedWidth = (int)(map.Width * cellSize * zoomLevel);
        int zoomedHeight = (int)(map.Height * cellSize * zoomLevel);

        _spriteBatch.Draw(
            _overlayTexture,
            new Rectangle(offsetX, offsetY, zoomedWidth, zoomedHeight),
            Color.White
        );

        // Draw LOD enhancement effects on top of base texture
        // Only draw zoom-dependent visual enhancements, not base shapes (those are in texture)
        float pixelScale = cellSize * zoomLevel;

        // Volcano LOD effects (glows, particles, craters)
        if (ShowVolcanoes && zoomLevel > 1.5f)
        {
            for (int x = 0; x < map.Width; x++)
            {
                for (int y = 0; y < map.Height; y++)
                {
                    var geo = map.Cells[x, y].GetGeology();
                    if (geo.IsVolcano)
                    {
                        float screenX = offsetX + x * pixelScale;
                        float screenY = offsetY + y * pixelScale;
                        int centerX = (int)(screenX + pixelScale * 0.5f);
                        int centerY = (int)(screenY + pixelScale * 0.5f);
                        int size = (int)(pixelScale * 0.5f);

                        // Glow effects at zoom > 2x
                        if (zoomLevel > 2.0f && geo.VolcanicActivity > 0.3f)
                        {
                            int glowRadius = (int)(size * 2.0f);
                            DrawCircleFilled(_spriteBatch, centerX, centerY, glowRadius,
                                Color.OrangeRed * (0.25f * geo.VolcanicActivity));

                            if (zoomLevel > 3.0f && geo.VolcanicActivity > 0.4f)
                            {
                                int shimmerRadius = (int)(size * 1.6f);
                                DrawCircleOutline(_spriteBatch, centerX, centerY, shimmerRadius,
                                    Color.Yellow * (0.4f * geo.VolcanicActivity), 2);
                            }
                        }

                        // Eruption effects
                        if (geo.MagmaPressure > 0.8f)
                        {
                            DrawStar(_spriteBatch, centerX, centerY - size, size / 2, Color.Yellow);

                            if (zoomLevel > 3.5f)
                            {
                                for (int i = 0; i < 6; i++)
                                {
                                    float angle = (i / 6.0f) * MathF.PI * 2;
                                    int px = centerX + (int)(MathF.Cos(angle) * size * 1.2f);
                                    int py = centerY - size + (int)(MathF.Sin(angle) * size * 0.8f);
                                    DrawCircleFilled(_spriteBatch, px, py, Math.Max(1, size / 6), Color.Orange);
                                }
                            }

                            if (zoomLevel > 2.0f)
                            {
                                float pulse = (MathF.Sin((float)DateTime.Now.TimeOfDay.TotalSeconds * 3) + 1) * 0.5f;
                                DrawCircleOutline(_spriteBatch, centerX, centerY, (int)(size * 1.3f),
                                    Color.Red * (0.6f * pulse), 2);
                            }
                        }
                    }
                }
            }
        }

        // River LOD effects (shimmer, flow indicators, source markers)
        if (ShowRivers && _hydrologySim != null && _riversAllowedInCurrentView && zoomLevel > 2.0f)
        {
            // BUGFIX: Create a copy of the collection to prevent modification during enumeration.
            foreach (var river in _hydrologySim.Rivers.ToList())
            {
                if (river.Path.Count < 2) continue;

                // Flow indicator dots at very high zoom
                if (zoomLevel > 3.5f)
                {
                    for (int i = 0; i < river.Path.Count - 1; i += 3)
                    {
                        var (x, y) = river.Path[i];
                        float screenX = offsetX + x * pixelScale + pixelScale * 0.5f;
                        float screenY = offsetY + y * pixelScale + pixelScale * 0.5f;

                        // Animated pulse
                        float phase = ((float)DateTime.Now.TimeOfDay.TotalSeconds * 2 + i * 0.3f) % 1.0f;
                        if (phase < 0.5f)
                        {
                            DrawCircleFilled(_spriteBatch, (int)screenX, (int)screenY, 2,
                                Color.White * (0.8f * (1.0f - phase * 2)));
                        }
                    }
                }

                // River source marker at high zoom
                if (zoomLevel > 3.0f && river.Path.Count > 0)
                {
                    var (x, y) = river.Path[0];
                    int screenX = (int)(offsetX + x * pixelScale + pixelScale * 0.5f);
                    int screenY = (int)(offsetY + y * pixelScale + pixelScale * 0.5f);
                    int radius = (int)(pixelScale * 0.6f);
                    DrawCircleFilled(_spriteBatch, screenX, screenY, radius, new Color(100, 180, 255) * 0.5f);
                    DrawCircleOutline(_spriteBatch, screenX, screenY, radius + 2, Color.Cyan, 1);
                }
            }
        }

        // Plate boundary LOD effects (arrows and stress visualization)
        if (ShowPlates && zoomLevel > 2.5f)
        {
            for (int x = 0; x < map.Width; x++)
            {
                for (int y = 0; y < map.Height; y++)
                {
                    var geo = map.Cells[x, y].GetGeology();
                    if (geo.BoundaryType != PlateBoundaryType.None)
                    {
                        float screenX = offsetX + x * pixelScale;
                        float screenY = offsetY + y * pixelScale;
                        int centerX = (int)(screenX + pixelScale * 0.5f);
                        int centerY = (int)(screenY + pixelScale * 0.5f);

                        int indicatorSize = Math.Max(2, (int)(pixelScale * 0.3f));

                        // Movement arrows
                        switch (geo.BoundaryType)
                        {
                            case PlateBoundaryType.Divergent:
                                DrawArrow(_spriteBatch, centerX - indicatorSize, centerY, indicatorSize / 2, -1, Color.Yellow);
                                DrawArrow(_spriteBatch, centerX + indicatorSize, centerY, indicatorSize / 2, 1, Color.Yellow);
                                break;

                            case PlateBoundaryType.Convergent:
                                DrawArrow(_spriteBatch, centerX - indicatorSize, centerY, indicatorSize / 2, 1, Color.Red);
                                DrawArrow(_spriteBatch, centerX + indicatorSize, centerY, indicatorSize / 2, -1, Color.Red);
                                break;

                            case PlateBoundaryType.Transform:
                                DrawArrow(_spriteBatch, centerX, centerY - indicatorSize, indicatorSize / 2, 0, Color.Orange, true);
                                DrawArrow(_spriteBatch, centerX, centerY + indicatorSize, indicatorSize / 2, 0, Color.Orange, true, true);
                                break;
                        }

                        // Stress visualization at very high zoom
                        if (zoomLevel > 3.5f && geo.TectonicStress > 0.5f)
                        {
                            float pulse = (MathF.Sin((float)DateTime.Now.TimeOfDay.TotalSeconds * 4) + 1) * 0.5f;
                            int stressRadius = (int)(pixelScale * 0.8f);
                            DrawCircleOutline(_spriteBatch, centerX, centerY, stressRadius,
                                Color.White * (0.3f * geo.TectonicStress * pulse), 1);
                        }
                    }
                }
            }
        }
    }

    public void DrawEventLog(int screenWidth, int toolbarHeight = 0)
    {
        if (!ShowEvents || _eventLog.Count == 0) return;

        int panelWidth = 340;
        int panelHeight = 160;
        int panelX = screenWidth - panelWidth - 10;
        int panelY = toolbarHeight + 10;

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