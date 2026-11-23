using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;

namespace SimPlanet;

public class TerraformingTool : IDisposable
{
    private readonly GraphicsDevice _graphicsDevice;
    private readonly FontRenderer _font;
    private readonly PlanetMap _map;
    private Texture2D _pixelTexture;

    public bool IsVisible { get; set; } = false;
    private int _brushSize = 5;
    private float _strength = 0.1f;
    private bool _isRaising = true;

    private MouseState _previousMouseState;

    public TerraformingTool(GraphicsDevice graphicsDevice, FontRenderer font, PlanetMap map)
    {
        _graphicsDevice = graphicsDevice;
        _font = font;
        _map = map;

        _pixelTexture = new Texture2D(_graphicsDevice, 1, 1);
        _pixelTexture.SetData(new[] { Color.White });
    }

    public void Update(MouseState mouseState, int cameraX, int cameraY, float zoom, int screenOffsetX, int screenOffsetY)
    {
        if (!IsVisible) return;

        HandleInput(mouseState, cameraX, cameraY, zoom, screenOffsetX, screenOffsetY);
        _previousMouseState = mouseState;
    }

    private void HandleInput(MouseState mouseState, int cameraX, int cameraY, float zoom, int screenOffsetX, int screenOffsetY)
    {
        if (mouseState.LeftButton == ButtonState.Pressed)
        {
            ApplyTerraforming(mouseState.X, mouseState.Y, cameraX, cameraY, zoom, screenOffsetX, screenOffsetY);
        }

        // Toggle between raising and lowering with right click
        if (mouseState.RightButton == ButtonState.Pressed && _previousMouseState.RightButton == ButtonState.Released)
        {
            _isRaising = !_isRaising;
        }

        // Adjust brush size with scroll wheel
        int scrollDelta = mouseState.ScrollWheelValue - _previousMouseState.ScrollWheelValue;
        if (scrollDelta != 0)
        {
            _brushSize = Math.Clamp(_brushSize + (scrollDelta > 0 ? 1 : -1), 1, 20);
        }
    }

    private void ApplyTerraforming(int mouseX, int mouseY, int cameraX, int cameraY, float zoom, int screenOffsetX, int screenOffsetY)
    {
        int mapX = (int)((mouseX - screenOffsetX + cameraX) / (4 * zoom));
        int mapY = (int)((mouseY - screenOffsetY + cameraY) / (4 * zoom));

        for (int y = mapY - _brushSize; y <= mapY + _brushSize; y++)
        {
            for (int x = mapX - _brushSize; x <= mapX + _brushSize; x++)
            {
                if (x >= 0 && x < _map.Width && y >= 0 && y < _map.Height)
                {
                    var cell = _map.Cells[x, y];
                    if (_isRaising)
                    {
                        cell.Elevation = Math.Min(1.0f, cell.Elevation + _strength);
                    }
                    else
                    {
                        cell.Elevation = Math.Max(-1.0f, cell.Elevation - _strength);
                    }
                }
            }
        }
    }

    public void Draw(SpriteBatch spriteBatch, int screenWidth, int screenHeight)
    {
        if (!IsVisible) return;

        // Status bar above the bottom button bar
        var items = new (string, string, Color?)[]
        {
            ("Mode", _isRaising ? "Raise land" : "Lower land", _isRaising ? UITheme.Good : UITheme.Warn),
            ("Brush", _brushSize.ToString(), UITheme.Gold),
        };
        UIWidgets.DrawToolBar(spriteBatch, screenWidth / 2, screenHeight - 115, screenWidth, "Terraforming",
            _isRaising ? UITheme.Good : UITheme.Warn, items,
            "Hold left button: sculpt  -  right-click: raise/lower  -  wheel: brush size");
    }

    public void Dispose()
    {
        _pixelTexture?.Dispose();
    }
}
