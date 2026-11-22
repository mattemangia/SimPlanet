using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SimPlanet;

/// <summary>
/// Displays a 2D geological subsurface profile along a selected path.
/// </summary>
public class GeologicalProfileViewer : IDisposable
{
    private readonly GraphicsDevice _graphicsDevice;
    private readonly FontRenderer _font;
    private readonly PlanetMap _map;
    private Texture2D _pixelTexture;

    public bool IsVisible { get; set; } = false;
    public Point StartPoint { get; private set; }
    public Point EndPoint { get; private set; }

    private MouseState _previousMouseState;

    // UI Constants
    private const int PanelMargin = 50;
    private const int TopBarHeight = UIWidgets.HeaderHeight;
    private const int LegendWidth = 214;
    private const int BottomAxisHeight = 30;
    private const int LeftAxisWidth = 78;

    public GeologicalProfileViewer(GraphicsDevice graphicsDevice, FontRenderer font, PlanetMap map)
    {
        _graphicsDevice = graphicsDevice;
        _font = font;
        _map = map;

        _pixelTexture = new Texture2D(_graphicsDevice, 1, 1);
        _pixelTexture.SetData(new[] { Color.White });
    }

    public void SetProfile(Point start, Point end)
    {
        StartPoint = start;
        EndPoint = end;
        IsVisible = true;
    }

    public void Dispose()
    {
        _pixelTexture?.Dispose();
    }

    /// <summary>Window rectangle, shared by Update (close button) and Draw.</summary>
    private static Rectangle GetPanel(int screenWidth, int screenHeight)
    {
        return new Rectangle(PanelMargin, PanelMargin, screenWidth - PanelMargin * 2, screenHeight - PanelMargin * 2);
    }

    public void Update(MouseState mouseState)
    {
        if (!IsVisible) return;

        // Close with the button in the title bar (ESC is handled in the Game class)
        var panel = GetPanel(_graphicsDevice.Viewport.Width, _graphicsDevice.Viewport.Height);

        if (mouseState.LeftButton == ButtonState.Released && _previousMouseState.LeftButton == ButtonState.Pressed)
        {
            if (UIWidgets.CloseButtonRect(panel).Contains(mouseState.Position))
            {
                IsVisible = false;
            }
        }

        _previousMouseState = mouseState;
    }

    private static readonly RasterizerState ScissorRasterizer = new RasterizerState { ScissorTestEnable = true };

    public void Draw(SpriteBatch spriteBatch, int screenWidth, int screenHeight)
    {
        if (!IsVisible) return;

        // 1. Draw Window Frame
        var panel = GetPanel(screenWidth, screenHeight);
        int panelX = panel.X;
        int panelY = panel.Y;
        int panelWidth = panel.Width;
        int panelHeight = panel.Height;

        UIWidgets.DrawBackdrop(spriteBatch, screenWidth, screenHeight, 0.45f);
        UIWidgets.DrawWindow(spriteBatch, panel, "GEOLOGICAL SECTION", UITheme.Gold, closeButton: true,
            note: $"from ({StartPoint.X}, {StartPoint.Y}) to ({EndPoint.X}, {EndPoint.Y})");

        // 2. Sample Data
        var samples = SampleProfile(StartPoint, EndPoint);
        if (samples.Count < 2)
        {
            UITheme.DrawTextCentered(spriteBatch, "The two points are the same cell: pick two different points.",
                new Rectangle(panelX, panelY + TopBarHeight, panelWidth, panelHeight - TopBarHeight), UITheme.TextDim, UITheme.FontNormal);
            return;
        }

        float maxElevation = float.MinValue, minElevation = float.MaxValue;
        int faultCount = 0;
        foreach (var sample in samples)
        {
            maxElevation = Math.Max(maxElevation, sample.Elevation);
            minElevation = Math.Min(minElevation, sample.Elevation);
            if (sample.GeoData.IsFault) faultCount++;
        }

        // 3. Define Drawing Area for Graph
        int graphX = panelX + LeftAxisWidth;
        int graphY = panelY + TopBarHeight + 14;
        int graphWidth = panelWidth - LeftAxisWidth - LegendWidth - 14 - 14;
        int graphHeight = panelHeight - TopBarHeight - BottomAxisHeight - 28;

        Rectangle graphRect = new Rectangle(graphX, graphY, graphWidth, graphHeight);

        // Sky behind the section
        UITheme.FillRect(spriteBatch, graphRect, new Color(6, 9, 15));
        UITheme.FillGradient(spriteBatch, new Rectangle(graphX, graphY, graphWidth, graphHeight / 3), new Color(40, 70, 120) * 0.35f);

        // Clip to graph area
        var originalScissor = spriteBatch.GraphicsDevice.ScissorRectangle;
        spriteBatch.End();
        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, ScissorRasterizer);
        spriteBatch.GraphicsDevice.ScissorRectangle = graphRect; // Note: Needs intersection with viewport if viewport < graphRect

        // 4. Calculate Scales
        // Y-Axis: Elevation goes from roughly -1.0 (Deep Ocean) to +1.0 (High Mountain)
        // But we also need subsurface.
        // Let's say Top of graph is +2.0 (Atmosphere/Sky)
        // Bottom of graph is -10.0 (Deep Crust/Mantle)
        // Elevation in game is normalized.
        // Crust thickness is in km, roughly 5-50km.
        // Let's map arbitrary units.
        // Standard elevation 1.0 ~= 8km (Everest).
        // Sea level 0.0.
        // Deep ocean -1.0 ~= -11km (Mariana).
        // Crust bottom ~ 30km deep. 30km ~= 3.75 units.
        // So we show range from +1.5 to -5.0.

        float yMax = 1.5f;
        float yMin = -5.0f;
        float yRange = yMax - yMin;

        float xStep = (float)graphWidth / (samples.Count - 1);

        // 5. Draw Layers (Bottom-up painter's algorithm, or polygon meshes?)
        // Simple column drawing for now.

        for (int i = 0; i < samples.Count - 1; i++)
        {
            var s1 = samples[i];
            var s2 = samples[i + 1];

            float screenX1 = graphX + i * xStep;
            float screenX2 = graphX + (i + 1) * xStep;
            float pixelWidth = screenX2 - screenX1;

            // We can simply draw columns at i.
            // Better: Draw quads between i and i+1 for smoothness.
            // Since we don't have a primitive batcher handy, we'll use vertical strips or simple interpolation.
            // Let's just draw vertical strips at pixel width + 1 to avoid gaps.

            // Calculate Y positions
            float surf1 = GetScreenY(s1.Elevation, yMax, yRange, graphY, graphHeight);
            float surf2 = GetScreenY(s2.Elevation, yMax, yRange, graphY, graphHeight);

            // Water
            float waterLevel = 0.0f;
            float waterY1 = GetScreenY(waterLevel, yMax, yRange, graphY, graphHeight);
            float waterY2 = GetScreenY(waterLevel, yMax, yRange, graphY, graphHeight);

            // Draw Sky/Air
            // Above surface

            // Draw Water (if elevation < 0)
            if (s1.Elevation < 0)
            {
                 // Draw from water surface (Top) down to seabed (Bottom)
                 DrawQuad(spriteBatch, screenX1, waterY1, screenX2, waterY2, screenX1, surf1, screenX2, surf2, new Color(0, 100, 200, 150));
            }

            // Geology Layers
            // Total Crust Thickness (normalized units)
            // Continental ~ 35km ~ 4.0 units?
            // In code: Continental ~ 30-45. Oceanic ~ 7-10.
            // Let's scale: 1.0 elevation unit = 8km.
            // So 30km = 3.75 units.
            // Elevation is surface. Moho is Elevation - (CrustThickness / 8.0).

            float crustThickness1 = s1.GeoData.CrustThickness / 8.0f;
            float crustThickness2 = s2.GeoData.CrustThickness / 8.0f;

            // BUGFIX: Moho calculation must account for sediment!
            // Surface Elevation = Bedrock Elevation + Sediment
            // Moho = Bedrock Elevation - Crust Thickness
            // So Moho = (Surface - Sediment) - Crust Thickness
            float bedrockElev1 = s1.Elevation - s1.GeoData.SedimentLayer;
            float bedrockElev2 = s2.Elevation - s2.GeoData.SedimentLayer;

            float moho1 = bedrockElev1 - crustThickness1;
            float moho2 = bedrockElev2 - crustThickness2;

            float mohoScreenY1 = GetScreenY(moho1, yMax, yRange, graphY, graphHeight);
            float mohoScreenY2 = GetScreenY(moho2, yMax, yRange, graphY, graphHeight);

            // --- 1. MANTLE (Below Moho) ---
            DrawQuad(spriteBatch, screenX1, mohoScreenY1, screenX2, mohoScreenY2, screenX1, graphY + graphHeight, screenX2, graphY + graphHeight, new Color(50, 0, 0)); // Dark red mantle

            float currentY1 = s1.Elevation;
            float currentY2 = s2.Elevation;

            float currentScreenY1 = surf1;
            float currentScreenY2 = surf2;

            // --- ICE ---
            if (s1.IsIce)
            {
                float iceThick = 0.2f;
                float iceBottomY1 = currentY1 - (s1.IsIce ? iceThick : 0);
                float iceBottomY2 = currentY2 - (s2.IsIce ? iceThick : 0);

                float iceScreenBottomY1 = GetScreenY(iceBottomY1, yMax, yRange, graphY, graphHeight);
                float iceScreenBottomY2 = GetScreenY(iceBottomY2, yMax, yRange, graphY, graphHeight);

                DrawQuad(spriteBatch, screenX1, currentScreenY1, screenX2, currentScreenY2, screenX1, iceScreenBottomY1, screenX2, iceScreenBottomY2, Color.White);

                currentY1 = iceBottomY1;
                currentY2 = iceBottomY2;
                currentScreenY1 = iceScreenBottomY1;
                currentScreenY2 = iceScreenBottomY2;
            }

            // --- SEDIMENTS ---
            // Draw from Current (Surface/Ice base) down to Bedrock
            // Note: SedimentLayer is in Elevation Units
            // BUGFIX: Clamp sediment layer to prevent infinite drawing
            float clampedSediment1 = Math.Min(s1.GeoData.SedimentLayer, 2.0f); // Max 2 units (~16km)
            float clampedSediment2 = Math.Min(s2.GeoData.SedimentLayer, 2.0f);

            float sedBottomY1 = currentY1 - clampedSediment1;
            float sedBottomY2 = currentY2 - clampedSediment2;

            // Also ensure sediment doesn't go below moho
            sedBottomY1 = Math.Max(sedBottomY1, moho1);
            sedBottomY2 = Math.Max(sedBottomY2, moho2);

            float sedScreenBottomY1 = GetScreenY(sedBottomY1, yMax, yRange, graphY, graphHeight);
            float sedScreenBottomY2 = GetScreenY(sedBottomY2, yMax, yRange, graphY, graphHeight);

            // Only draw if sediment has thickness
            if (clampedSediment1 > 0.01f || clampedSediment2 > 0.01f)
            {
                DrawQuad(spriteBatch, screenX1, currentScreenY1, screenX2, currentScreenY2, screenX1, sedScreenBottomY1, screenX2, sedScreenBottomY2, new Color(180, 160, 120)); // Tan/Sand color
            }

            currentY1 = sedBottomY1;
            currentY2 = sedBottomY2;
            currentScreenY1 = sedScreenBottomY1;
            currentScreenY2 = sedScreenBottomY2;

            // --- VOLCANIC LAYER ---
            // BUGFIX: Clamp volcanic layer and ensure it doesn't go below moho
            float volcThick1 = Math.Min(s1.GeoData.VolcanicRock / 8.0f, 1.0f);
            float volcThick2 = Math.Min(s2.GeoData.VolcanicRock / 8.0f, 1.0f);

            float volcBottomY1 = currentY1 - volcThick1;
            float volcBottomY2 = currentY2 - volcThick2;

            // Ensure volcanic layer doesn't go below moho
            volcBottomY1 = Math.Max(volcBottomY1, moho1);
            volcBottomY2 = Math.Max(volcBottomY2, moho2);

            float volcScreenBottomY1 = GetScreenY(volcBottomY1, yMax, yRange, graphY, graphHeight);
            float volcScreenBottomY2 = GetScreenY(volcBottomY2, yMax, yRange, graphY, graphHeight);

            // Only draw if volcanic layer has thickness
            if (volcThick1 > 0.01f || volcThick2 > 0.01f)
            {
                DrawQuad(spriteBatch, screenX1, currentScreenY1, screenX2, currentScreenY2, screenX1, volcScreenBottomY1, screenX2, volcScreenBottomY2, new Color(60, 60, 60)); // Dark Grey
            }

            currentY1 = volcBottomY1;
            currentY2 = volcBottomY2;
            currentScreenY1 = volcScreenBottomY1;
            currentScreenY2 = volcScreenBottomY2;

            // --- CRYSTALLINE BASEMENT ---
            // Fill remaining down to Moho
            // BUGFIX: Always draw crystalline basement to Moho (prevents flickering)
            // Use min of 1 pixel difference to detect if there's space to draw
            float crystalGap1 = mohoScreenY1 - currentScreenY1;
            float crystalGap2 = mohoScreenY2 - currentScreenY2;

            if (crystalGap1 > 1.0f || crystalGap2 > 1.0f)
            {
                // Ensure we don't draw inverted (top below bottom)
                float drawTopY1 = Math.Min(currentScreenY1, mohoScreenY1);
                float drawTopY2 = Math.Min(currentScreenY2, mohoScreenY2);
                float drawBotY1 = Math.Max(currentScreenY1, mohoScreenY1);
                float drawBotY2 = Math.Max(currentScreenY2, mohoScreenY2);

                DrawQuad(spriteBatch, screenX1, drawTopY1, screenX2, drawTopY2, screenX1, drawBotY1, screenX2, drawBotY2, new Color(100, 80, 80)); // Granite/Basalt mix color
            }

            // --- FEATURES ---
            // Volcano Shaft
            if (s1.GeoData.IsVolcano)
            {
                float midX = (screenX1 + screenX2) / 2;
                // Draw magma conduit
                spriteBatch.Draw(_pixelTexture, new Rectangle((int)midX - 2, (int)surf1, 4, (int)(mohoScreenY1 - surf1)), Color.Red);
                // Draw Magma Chamber
                int chamberSize = (int)(s1.GeoData.MagmaPressure * 20) + 5;
                spriteBatch.Draw(_pixelTexture, new Rectangle((int)midX - chamberSize/2, (int)mohoScreenY1 - chamberSize, chamberSize, chamberSize), Color.OrangeRed);
            }

            // Surface line
            DrawLine(spriteBatch, new Vector2(screenX1, surf1), new Vector2(screenX2, surf2), Color.White, 1);

            // Draw Fault Indicator (Vertical line)
            if (s1.GeoData.IsFault)
            {
                float midX = (screenX1 + screenX2) / 2;
                float yTop = surf1 - 20;
                float yBot = mohoScreenY1 + 20;

                Color faultColor = s1.GeoData.FaultType switch {
                    FaultType.Normal => Color.Yellow,
                    FaultType.Thrust => Color.Magenta,
                    FaultType.Reverse => Color.Magenta,
                    FaultType.Strike_Slip => Color.Cyan,
                    _ => Color.Red
                };

                // Draw dashed or solid line
                DrawLine(spriteBatch, new Vector2(midX, yTop), new Vector2(midX, yBot), faultColor, 2);

                // Draw arrows/symbols
                if (s1.GeoData.FaultType == FaultType.Normal)
                {
                    // Arrows pointing away
                    UITheme.DrawTextOutlined(spriteBatch, "<- ->", new Vector2(midX - 15, yTop - 16), faultColor, UITheme.FontSmall);
                }
                else if (s1.GeoData.FaultType == FaultType.Thrust || s1.GeoData.FaultType == FaultType.Reverse)
                {
                    // Arrows pointing together
                    UITheme.DrawTextOutlined(spriteBatch, "-> <-", new Vector2(midX - 15, yTop - 16), faultColor, UITheme.FontSmall);
                }
                else if (s1.GeoData.FaultType == FaultType.Strike_Slip)
                {
                    UITheme.DrawTextOutlined(spriteBatch, "O X", new Vector2(midX - 10, yTop - 16), faultColor, UITheme.FontSmall);
                }
            }
        }

        spriteBatch.End();
        spriteBatch.GraphicsDevice.ScissorRectangle = originalScissor;
        spriteBatch.Begin();

        // 6. Axes, grid and legend
        UITheme.DrawRectOutline(spriteBatch, graphRect, UITheme.Border);

        // Y axis: depth in km (1.0 elevation unit = 8 km), faint grid lines across the plot
        for (float y = MathF.Floor(yMax); y >= MathF.Ceiling(yMin); y -= 1.0f)
        {
            float sy = GetScreenY(y, yMax, yRange, graphY, graphHeight);
            if (sy >= graphY && sy <= graphY + graphHeight)
            {
                bool seaLevel = y == 0f;
                UITheme.FillRect(spriteBatch, new Rectangle(graphX + 1, (int)sy, graphWidth - 2, 1),
                    seaLevel ? new Color(120, 190, 255) * 0.45f : Color.White * 0.07f);
                UITheme.FillRect(spriteBatch, new Rectangle(graphX - 5, (int)sy, 5, 1), UITheme.TextMuted);
                string label = seaLevel ? "sea level" : $"{y * 8:+0;-0} km";
                var size = UITheme.Measure(label, UITheme.FontSmall);
                UITheme.DrawText(spriteBatch, label, new Vector2(graphX - 9 - size.X, sy - size.Y / 2f),
                    seaLevel ? new Color(120, 190, 255) : UITheme.TextMuted, UITheme.FontSmall);
            }
        }

        // X axis: distance along the section in map cells, with the two ends named A and B
        int axisY = graphY + graphHeight;
        int cells = samples.Count - 1;
        for (int t = 0; t <= 4; t++)
        {
            int tx = graphX + (int)(graphWidth * t / 4f);
            UITheme.FillRect(spriteBatch, new Rectangle(Math.Min(tx, graphX + graphWidth - 1), axisY, 1, 5), UITheme.TextMuted);
            string label = t == 0 ? $"A  ({StartPoint.X}, {StartPoint.Y})"
                         : t == 4 ? $"({EndPoint.X}, {EndPoint.Y})  B"
                         : $"{cells * t / 4} cells";
            var size = UITheme.Measure(label, UITheme.FontSmall);
            float lx = t == 0 ? tx : t == 4 ? tx - size.X : tx - size.X / 2f;
            UITheme.DrawText(spriteBatch, label, new Vector2(lx, axisY + 8), t == 0 || t == 4 ? UITheme.Gold : UITheme.TextMuted, UITheme.FontSmall);
        }

        // Legend card
        var legend = new Rectangle(graphX + graphWidth + 14, graphY, LegendWidth, graphHeight);
        UIWidgets.DrawCard(spriteBatch, legend);
        int legendX = legend.X + 12;
        int legendWidth = legend.Width - 24;
        int legendY = UIWidgets.DrawSection(spriteBatch, legendX, legend.Y + 10, legendWidth, "LAYERS");
        legendY = UIWidgets.DrawLegendRow(spriteBatch, legendX, legendY, legendWidth, Color.White, "Surface / ice");
        legendY = UIWidgets.DrawLegendRow(spriteBatch, legendX, legendY, legendWidth, new Color(0, 100, 200), "Ocean");
        legendY = UIWidgets.DrawLegendRow(spriteBatch, legendX, legendY, legendWidth, new Color(180, 160, 120), "Sediment");
        legendY = UIWidgets.DrawLegendRow(spriteBatch, legendX, legendY, legendWidth, new Color(60, 60, 60), "Volcanic rock");
        legendY = UIWidgets.DrawLegendRow(spriteBatch, legendX, legendY, legendWidth, new Color(100, 80, 80), "Crystalline crust");
        legendY = UIWidgets.DrawLegendRow(spriteBatch, legendX, legendY, legendWidth, new Color(50, 0, 0), "Mantle");
        legendY = UIWidgets.DrawLegendRow(spriteBatch, legendX, legendY, legendWidth, Color.OrangeRed, "Magma / volcano");

        legendY = UIWidgets.DrawSection(spriteBatch, legendX, legendY + 10, legendWidth, "FAULTS");
        legendY = UIWidgets.DrawLegendRow(spriteBatch, legendX, legendY, legendWidth, Color.Yellow, "Normal (graben)");
        legendY = UIWidgets.DrawLegendRow(spriteBatch, legendX, legendY, legendWidth, Color.Magenta, "Thrust / reverse");
        legendY = UIWidgets.DrawLegendRow(spriteBatch, legendX, legendY, legendWidth, Color.Cyan, "Strike-slip");

        legendY = UIWidgets.DrawSection(spriteBatch, legendX, legendY + 10, legendWidth, "SECTION");
        legendY = UIWidgets.DrawKeyValue(spriteBatch, legendX, legendY, legendWidth, "Length", $"{cells} cells", UITheme.Text, UITheme.FontSmall) - 3;
        legendY = UIWidgets.DrawKeyValue(spriteBatch, legendX, legendY, legendWidth, "Highest", $"{maxElevation * 8f:+0.0;-0.0} km", UITheme.Text, UITheme.FontSmall) - 3;
        legendY = UIWidgets.DrawKeyValue(spriteBatch, legendX, legendY, legendWidth, "Lowest", $"{minElevation * 8f:+0.0;-0.0} km", UITheme.Text, UITheme.FontSmall) - 3;
        legendY = UIWidgets.DrawKeyValue(spriteBatch, legendX, legendY, legendWidth, "Faults crossed", faultCount.ToString(), faultCount > 0 ? UITheme.Warn : UITheme.TextDim, UITheme.FontSmall);
        if (legendY + 40 < legend.Bottom)
            UIWidgets.DrawParagraph(spriteBatch, legendX, legendY + 6, legendWidth, "Vertical scale is exaggerated.", UITheme.TextMuted);
    }

    private void DrawQuad(SpriteBatch sb, float x1_top, float y1_top, float x2_top, float y2_top, float x1_bot, float y1_bot, float x2_bot, float y2_bot, Color c)
    {
        // MonoGame SpriteBatch doesn't easily draw quads.
        // We can approximate with two triangles if we had a primitive batch.
        // Or simpler: Draw vertical strips for each pixel column? Too slow.
        // Or simpler: Just draw a rect between average top and average bottom? (Blocky)
        // Or: Rotate a texture?

        // Let's try drawing vertical slices. Since x1 -> x2 is likely small (1-2 pixels if high res, or large if low res).
        // Start with simple Rect approximation for the segment.

        float w = x2_top - x1_top;
        // Rect approach:
        // sb.Draw(_pixelTexture, new Rectangle((int)x1_top, (int)y1_top, (int)w, (int)(y1_bot - y1_top)), c);

        // Better approach for sloped layers:
        // Decompose into 1px vertical columns if width is small.
        // One column per screen pixel between the two samples, so no pixel column is skipped
        // (stepping by the truncated width left a 1px black gap every few samples).
        int xStart = (int)MathF.Floor(x1_top);
        int xEnd = Math.Max(xStart + 1, (int)MathF.Floor(x2_top));
        for (int px = xStart; px < xEnd; px++)
        {
            float t = w > 0.001f ? Math.Clamp((px - x1_top) / w, 0f, 1f) : 0f;
            float curYTop = y1_top + (y2_top - y1_top) * t;
            float curYBot = y1_bot + (y2_bot - y1_bot) * t;

            sb.Draw(_pixelTexture, new Rectangle(px, (int)curYTop, 1, (int)(curYBot - curYTop + 1)), c);
        }
    }

    private void DrawLine(SpriteBatch spriteBatch, Vector2 start, Vector2 end, Color color, int thickness)
    {
        Vector2 edge = end - start;
        float angle = (float)Math.Atan2(edge.Y, edge.X);
        spriteBatch.Draw(_pixelTexture,
            new Rectangle((int)start.X, (int)start.Y, (int)edge.Length(), thickness),
            null,
            color,
            angle,
            Vector2.Zero,
            SpriteEffects.None,
            0);
    }

    private void DrawBorder(SpriteBatch spriteBatch, int x, int y, int width, int height, Color color, int thickness)
    {
        spriteBatch.Draw(_pixelTexture, new Rectangle(x, y, width, thickness), color);
        spriteBatch.Draw(_pixelTexture, new Rectangle(x, y + height - thickness, width, thickness), color);
        spriteBatch.Draw(_pixelTexture, new Rectangle(x, y, thickness, height), color);
        spriteBatch.Draw(_pixelTexture, new Rectangle(x + width - thickness, y, thickness, height), color);
    }

    private float GetScreenY(float elevation, float yMax, float yRange, int graphY, int graphHeight)
    {
        float norm = (yMax - elevation) / yRange;
        return graphY + norm * graphHeight;
    }

    private struct ProfileSample
    {
        public int X;
        public int Y;
        public float Elevation;
        public bool IsIce;
        public GeologicalData GeoData;
        // Add others as needed
    }

    private List<ProfileSample> SampleProfile(Point start, Point end)
    {
        var list = new List<ProfileSample>();

        // Bresenham-ish line algorithm or simple step interpolation.
        // Since map wraps on X, we need to find shortest path.

        int dx = end.X - start.X;
        int w = _map.Width;

        // Check wrapping
        if (Math.Abs(dx) > w / 2)
        {
            if (dx > 0) dx -= w;
            else dx += w;
        }

        int dy = end.Y - start.Y;

        int steps = Math.Max(Math.Abs(dx), Math.Abs(dy));
        if (steps == 0) return list;

        for (int i = 0; i <= steps; i++)
        {
            float t = i / (float)steps;
            int curX = (int)(start.X + dx * t);
            int curY = (int)(start.Y + dy * t);

            // Handle wrap
            curX = (curX % w + w) % w;
            curY = Math.Clamp(curY, 0, _map.Height - 1);

            var cell = _map.Cells[curX, curY];
            list.Add(new ProfileSample
            {
                X = curX,
                Y = curY,
                Elevation = cell.Elevation,
                IsIce = cell.IsIce,
                GeoData = cell.GetGeology() // Note: Ref copy, careful if modifying
            });
        }

        return list;
    }
}
