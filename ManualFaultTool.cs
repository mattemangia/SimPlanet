using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;

namespace SimPlanet;

public class ManualFaultTool : IDisposable
{
    private readonly GraphicsDevice _graphicsDevice;
    private readonly FontRenderer _font;
    private readonly PlanetMap _map;
    private Texture2D _pixelTexture;

    public bool IsActive { get; set; } = false;
    private Point? _startPoint = null;
    private Point? _endPoint = null;
    private FaultType _selectedFaultType = FaultType.Normal;
    private KeyboardState _previousKeyState;

    private MouseState _previousMouseState;

    public ManualFaultTool(GraphicsDevice graphicsDevice, FontRenderer font, PlanetMap map)
    {
        _graphicsDevice = graphicsDevice;
        _font = font;
        _map = map;

        _pixelTexture = new Texture2D(_graphicsDevice, 1, 1);
        _pixelTexture.SetData(new[] { Color.White });
    }

    public void Update(MouseState mouseState, int cameraX, int cameraY, float zoom, int screenOffsetX, int screenOffsetY, int cellSize)
    {
        if (!IsActive) return;

        var keyState = Keyboard.GetState();
        HandleInput(mouseState, keyState, cameraX, cameraY, zoom, screenOffsetX, screenOffsetY, cellSize);
        _previousMouseState = mouseState;
        _previousKeyState = keyState;
    }

    private void HandleInput(MouseState mouseState, KeyboardState keyState, int cameraX, int cameraY, float zoom, int screenOffsetX, int screenOffsetY, int cellSize)
    {
        // Cycle fault type
        if (keyState.IsKeyDown(Keys.Tab) && _previousKeyState.IsKeyUp(Keys.Tab))
        {
            _selectedFaultType = _selectedFaultType switch
            {
                FaultType.Normal => FaultType.Thrust,
                FaultType.Thrust => FaultType.Strike_Slip,
                FaultType.Strike_Slip => FaultType.Normal,
                _ => FaultType.Normal
            };
        }
        // Calculate map coordinates
        // Correct Logic: WorldPixels = (Screen - Offset) / Zoom + Camera
        // MapIndex = WorldPixels / CellSize
        float worldX = (mouseState.X - screenOffsetX) / zoom + cameraX;
        float worldY = (mouseState.Y - screenOffsetY) / zoom + cameraY;

        int mapX = (int)(worldX / cellSize);
        int mapY = (int)(worldY / cellSize);

        // Wrap X
        mapX = (mapX % _map.Width + _map.Width) % _map.Width;
        // Clamp Y
        mapY = Math.Clamp(mapY, 0, _map.Height - 1);

        // Handle dragging
        if (mouseState.LeftButton == ButtonState.Pressed && _previousMouseState.LeftButton == ButtonState.Released)
        {
            _startPoint = new Point(mapX, mapY);
        }
        else if (mouseState.LeftButton == ButtonState.Pressed && _startPoint.HasValue)
        {
            _endPoint = new Point(mapX, mapY);
            // Draw live feedback?
        }
        else if (mouseState.LeftButton == ButtonState.Released && _previousMouseState.LeftButton == ButtonState.Pressed && _startPoint.HasValue)
        {
            _endPoint = new Point(mapX, mapY);
            ApplyFaultLine(_startPoint.Value, _endPoint.Value);
            _startPoint = null;
            _endPoint = null;
        }

        // Right click to cancel or clear?
        if (mouseState.RightButton == ButtonState.Pressed)
        {
            _startPoint = null;
            _endPoint = null;
        }
    }

    private void ApplyFaultLine(Point start, Point end)
    {
        // Bresenham's line algorithm with wrapping
        int x0 = start.X;
        int y0 = start.Y;
        int x1 = end.X;
        int y1 = end.Y;

        int dx = Math.Abs(x1 - x0);
        int dy = Math.Abs(y1 - y0);

        // Check for wrapping - if distance is > half map width, go the other way
        if (dx > _map.Width / 2)
        {
            if (x0 < x1) x0 += _map.Width;
            else x1 += _map.Width;
            dx = Math.Abs(x1 - x0);
        }

        int sx = x0 < x1 ? 1 : -1;
        int sy = y0 < y1 ? 1 : -1;
        int err = dx - dy;

        while (true)
        {
            // Normalize coordinates
            int nx = (x0 % _map.Width + _map.Width) % _map.Width;
            int ny = Math.Clamp(y0, 0, _map.Height - 1);

            var cell = _map.Cells[nx, ny];
            var geo = cell.GetGeology();
            geo.IsManualFault = true;
            geo.IsFault = true; // For visualization
            geo.FaultType = _selectedFaultType;
            geo.FaultActivity = 1.0f; // Set to maximum for visibility on fault map
            geo.SeismicStress = 0.3f; // Some initial stress
            geo.BoundaryType = PlateBoundaryType.Transform; // Immediate visual feedback

            // Apply displacement based on fault type
            // Determine side of line using cross product (2D)
            // Line vector: (dx, dy), Point vector: (nx-xStart, ny-yStart)
            // "Right" side is relative to drawing direction.
            // Cross product: (x1-x0)*(ny-y0) - (y1-y0)*(nx-x0)
            // Positive = Left, Negative = Right (usually)

            // IMPROVED: Larger radius and gradient-based displacement for realistic horst/graben
            int radius = 5;
            float maxDisplacement = 0.12f; // Stronger displacement for visible effects

            for (int dy2 = -radius; dy2 <= radius; dy2++)
            {
                for (int dx2 = -radius; dx2 <= radius; dx2++)
                {
                    int nx2 = (nx + dx2 + _map.Width) % _map.Width;
                    int ny2 = Math.Clamp(ny + dy2, 0, _map.Height - 1);

                    // Calculate distance from fault line for gradient effect
                    float dist = MathF.Sqrt(dx2 * dx2 + dy2 * dy2);
                    if (dist > radius) continue;

                    // Gradient: stronger near fault, weaker at distance
                    float gradientFactor = 1.0f - (dist / radius);
                    gradientFactor = gradientFactor * gradientFactor; // Quadratic falloff

                    float cross = (x1 - start.X) * (ny2 - start.Y) - (y1 - start.Y) * (nx2 - start.X);

                    // Adjust displacement based on fault type
                    float displacement = 0.0f;
                    if (_selectedFaultType == FaultType.Normal)
                    {
                        // NORMAL FAULT: Creates graben (rift valley)
                        // Hanging wall (right/negative cross) drops down
                        // Footwall (left/positive cross) stays or rises slightly
                        if (cross < 0)
                            displacement = -maxDisplacement * gradientFactor; // Drop hanging wall
                        else
                            displacement = maxDisplacement * 0.3f * gradientFactor; // Slight uplift footwall
                    }
                    else if (_selectedFaultType == FaultType.Thrust)
                    {
                        // THRUST FAULT: Creates mountain/ridge
                        // Hanging wall (right/negative cross) is pushed up and over
                        // Footwall (left/positive cross) is depressed
                        if (cross < 0)
                            displacement = maxDisplacement * gradientFactor; // Uplift hanging wall
                        else
                            displacement = -maxDisplacement * 0.3f * gradientFactor; // Depress footwall
                    }
                    // Strike-slip faults have minimal vertical displacement

                    // Apply to cell
                    if (displacement != 0)
                    {
                        _map.Cells[nx2, ny2].Elevation = Math.Clamp(_map.Cells[nx2, ny2].Elevation + displacement, -1.0f, 1.0f);
                    }
                }
            }

            if (x0 == x1 && y0 == y1) break;
            int e2 = 2 * err;
            if (e2 > -dy)
            {
                err -= dy;
                x0 += sx;
            }
            if (e2 < dx)
            {
                err += dx;
                y0 += sy;
            }
        }
    }

    public void Draw(SpriteBatch spriteBatch, int screenWidth, int screenHeight, int cameraX, int cameraY, float zoom, int screenOffsetX, int screenOffsetY, int cellSize)
    {
        if (!IsActive) return;

        // Draw tool UI
        string text = $"Manual Fault Tool | Type: {_selectedFaultType} (TAB) | Drag to draw";
        var textSize = _font.MeasureString(text, 14);
        int panelWidth = (int)textSize.X + 20;
        int panelHeight = 40;
        int xPos = (screenWidth - panelWidth) / 2;
        int yPos = screenHeight - panelHeight - 120; // Above terraforming tool

        // Background
        spriteBatch.Draw(_pixelTexture, new Rectangle(xPos, yPos, panelWidth, panelHeight), new Color(0, 0, 0, 200));
        DrawBorder(spriteBatch, xPos, yPos, panelWidth, panelHeight, Color.Red, 2);

        _font.DrawString(spriteBatch, text, new Vector2(xPos + 10, yPos + 10), Color.White, 14);

        // Draw drag line if active
        if (_startPoint.HasValue && _endPoint.HasValue && Mouse.GetState().LeftButton == ButtonState.Pressed)
        {
            // Convert map coords back to screen
            // Screen = (Map * Zoom * CellSize) + Offset - Camera
            // This is tricky because of wrapping and camera position

            // Just draw a simple line for now, ignoring wrapping logic for the visual line (it might look weird if wrapping but functional)
            Vector2 startScreen = MapToScreen(_startPoint.Value, cameraX, cameraY, zoom, screenOffsetX, screenOffsetY, cellSize);
            Vector2 endScreen = MapToScreen(_endPoint.Value, cameraX, cameraY, zoom, screenOffsetX, screenOffsetY, cellSize);

            DrawLine(spriteBatch, startScreen, endScreen, Color.Red, 2);
        }
    }

    private Vector2 MapToScreen(Point mapPt, int cameraX, int cameraY, float zoom, int screenOffsetX, int screenOffsetY, int cellSize)
    {
        // Simple projection, doesn't handle wrapping for the visual line
        float x = (mapPt.X * cellSize * zoom) + screenOffsetX - (cameraX * zoom); // CameraX is in pixels, wait.
        // In TerrainRenderer:
        // int screenX = xStart + (int)((x * CellSize - CameraX) * ZoomLevel);
        // Wait, CameraX is handled differently in Update:
        // float mapXFloat = (mouseState.X - screenOffsetX + cameraX) / (zoom * cellSize);
        // So: mouseState.X = mapX * zoom * cellSize - cameraX + screenOffsetX? No.
        // Let's reverse:
        // mapX * zoom * cellSize = mouseState.X - screenOffsetX + cameraX
        // mouseState.X = (mapX * zoom * cellSize) + screenOffsetX - cameraX

        // Actually CameraX is likely in unzoomed pixels (or just offset).
        // Let's check Update logic again.

        float screenX = (mapPt.X * cellSize * zoom) + screenOffsetX - (cameraX);
        // No, Update used: (mouse - offset + cam) / (zoom * cell)
        // So mouse - offset + cam = map * zoom * cell
        // mouse = map * zoom * cell + offset - cam
        // Wait, is cameraX scaled by zoom?
        // SimPlanetGame: _terrainRenderer.CameraX -= dx; (pixels)
        // TerrainRenderer Draw:
        // int screenX = xStart + (int)((x * CellSize - CameraX) * ZoomLevel);
        // So yes, CameraX is subtracted BEFORE Zoom.

        float sx = screenOffsetX + (mapPt.X * cellSize - cameraX) * zoom;
        float sy = screenOffsetY + (mapPt.Y * cellSize - cameraY) * zoom;
        return new Vector2(sx, sy);
    }

    private void DrawLine(SpriteBatch spriteBatch, Vector2 start, Vector2 end, Color color, int thickness)
    {
        Vector2 edge = end - start;
        float angle = (float)Math.Atan2(edge.Y, edge.X);
        float length = edge.Length();

        spriteBatch.Draw(_pixelTexture,
            new Rectangle((int)start.X, (int)start.Y, (int)length, thickness),
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

    public void Dispose()
    {
        _pixelTexture?.Dispose();
    }
}
