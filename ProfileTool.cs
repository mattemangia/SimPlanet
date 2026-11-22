using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;

namespace SimPlanet;

public class ProfileTool : IDisposable
{
    private readonly GraphicsDevice _graphicsDevice;
    private readonly FontRenderer _font;
    private readonly PlanetMap _map;
    private Texture2D _pixelTexture;

    public bool IsActive { get; set; } = false;
    public Point? StartPoint { get; private set; }
    public Point? EndPoint { get; private set; }

    public bool HasProfile => StartPoint.HasValue && EndPoint.HasValue;

    private MouseState _previousMouseState;

    public event Action<Point, Point>? OnProfileCreated;

    public ProfileTool(GraphicsDevice graphicsDevice, FontRenderer font, PlanetMap map)
    {
        _graphicsDevice = graphicsDevice;
        _font = font;
        _map = map;

        _pixelTexture = new Texture2D(_graphicsDevice, 1, 1);
        _pixelTexture.SetData(new[] { Color.White });
    }

    public void Reset()
    {
        StartPoint = null;
        EndPoint = null;
    }

    public void Update(MouseState mouseState, int cameraX, int cameraY, float zoom, int screenOffsetX, int screenOffsetY, int cellSize)
    {
        if (!IsActive) return;

        // Handle input
        if (mouseState.LeftButton == ButtonState.Pressed && _previousMouseState.LeftButton == ButtonState.Released)
        {
            int mapX = (int)((mouseState.X - screenOffsetX + cameraX) / (cellSize * zoom));
            int mapY = (int)((mouseState.Y - screenOffsetY + cameraY) / (cellSize * zoom));

            // Check bounds (X wraps, Y clamps, but for selection we might want strict bounds or wrap logic)
            // SimPlanetGame seems to handle cameraX/Y such that mapX might be > width or < 0?
            // PlanetMap.GetCell wraps X.

            // Normalize X to [0, Width)
            if (mapX < 0) mapX = (mapX % _map.Width + _map.Width) % _map.Width;
            else if (mapX >= _map.Width) mapX = mapX % _map.Width;

            if (mapY >= 0 && mapY < _map.Height)
            {
                Point clickedPoint = new Point(mapX, mapY);

                if (!StartPoint.HasValue)
                {
                    StartPoint = clickedPoint;
                    EndPoint = null; // Reset end point if restarting
                }
                else
                {
                    EndPoint = clickedPoint;
                    // Trigger event
                    OnProfileCreated?.Invoke(StartPoint.Value, EndPoint.Value);
                    IsActive = false; // Deactivate after creating
                }
            }
        }

        // Right click to cancel/reset
        if (mouseState.RightButton == ButtonState.Pressed)
        {
            Reset();
        }

        _previousMouseState = mouseState;
    }

    public void Draw(SpriteBatch spriteBatch, int screenWidth, int screenHeight, int cameraX, int cameraY, float zoom, int screenOffsetX, int screenOffsetY, int cellSize)
    {
        if (!IsActive) return;

        // Draw instructions
        bool started = StartPoint.HasValue;
        var items = new (string, string, Color?)[]
        {
            ("Step", started ? "2 of 2" : "1 of 2", UITheme.Gold),
            ("From", started ? $"{StartPoint!.Value.X}, {StartPoint.Value.Y}" : "-", started ? UITheme.Text : UITheme.TextMuted),
        };
        string hint = started
            ? "Click the map where the section ends  -  right-click: start over"
            : "Click the map where the section starts";
        UIWidgets.DrawToolBar(spriteBatch, screenWidth / 2, 100, screenWidth, "Geological Section", UITheme.Accent, items, hint);

        // Draw Start Point
        if (StartPoint.HasValue)
        {
            Vector2 startScreenPos = MapToScreen(StartPoint.Value, cameraX, cameraY, zoom, screenOffsetX, screenOffsetY, cellSize);

            // Draw line to mouse if EndPoint not set
            Vector2 mousePos = new Vector2(Mouse.GetState().X, Mouse.GetState().Y);

            DrawLine(spriteBatch, startScreenPos + new Vector2(1, 1), mousePos + new Vector2(1, 1), Color.Black * 0.6f, 3);
            DrawLine(spriteBatch, startScreenPos, mousePos, UITheme.Gold, 2);
            UITheme.FillRounded(spriteBatch, new Rectangle((int)startScreenPos.X - 6, (int)startScreenPos.Y - 6, 12, 12), new Color(8, 11, 18));
            UITheme.FillRounded(spriteBatch, new Rectangle((int)startScreenPos.X - 4, (int)startScreenPos.Y - 4, 8, 8), UITheme.Gold);
            UITheme.DrawTextOutlined(spriteBatch, "A", startScreenPos + new Vector2(8, -18), UITheme.Gold, UITheme.FontNormal);
        }
    }

    private Vector2 MapToScreen(Point mapPoint, int cameraX, int cameraY, float zoom, int screenOffsetX, int screenOffsetY, int cellSize)
    {
        // Need to handle wrapping for drawing the line correctly?
        // For now simple projection.
        float x = (mapPoint.X * cellSize * zoom) - cameraX + screenOffsetX;
        float y = (mapPoint.Y * cellSize * zoom) - cameraY + screenOffsetY;
        return new Vector2(x, y);
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

    public void Dispose()
    {
        _pixelTexture?.Dispose();
    }
}
