using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;

namespace SimPlanet;

public class LifePainterUI
{
    private readonly GraphicsDevice _graphicsDevice;
    private readonly FontRenderer _font;
    private readonly PlanetMap _map;
    private readonly LifeSimulator _lifeSimulator;
    private Texture2D _pixelTexture;

    public bool IsVisible { get; set; } = false;
    private LifeForm _selectedLifeForm = LifeForm.Bacteria;
    private int _brushSize = 5;

    private MouseState _previousMouseState;

    public LifePainterUI(GraphicsDevice graphicsDevice, FontRenderer font, PlanetMap map, LifeSimulator lifeSimulator)
    {
        _graphicsDevice = graphicsDevice;
        _font = font;
        _map = map;
        _lifeSimulator = lifeSimulator;

        _pixelTexture = new Texture2D(_graphicsDevice, 1, 1);
        _pixelTexture.SetData(new[] { Color.White });
    }

    public void Update(MouseState mouseState, int cameraX, int cameraY, float zoom)
    {
        if (!IsVisible) return;

        HandleInput(mouseState, cameraX, cameraY, zoom);
        _previousMouseState = mouseState;
    }

    private void HandleInput(MouseState mouseState, int cameraX, int cameraY, float zoom)
    {
        if (mouseState.LeftButton == ButtonState.Pressed)
        {
            PaintLife(mouseState.X, mouseState.Y, cameraX, cameraY, zoom);
        }

        // Cycle through life forms with right click
        if (mouseState.RightButton == ButtonState.Pressed && _previousMouseState.RightButton == ButtonState.Released)
        {
            _selectedLifeForm = (LifeForm)(((int)_selectedLifeForm + 1) % Enum.GetValues(typeof(LifeForm)).Length);
        }

        // Adjust brush size with scroll wheel
        int scrollDelta = mouseState.ScrollWheelValue - _previousMouseState.ScrollWheelValue;
        if (scrollDelta != 0)
        {
            _brushSize = Math.Clamp(_brushSize + (scrollDelta > 0 ? 1 : -1), 1, 20);
        }
    }

    private void PaintLife(int mouseX, int mouseY, int cameraX, int cameraY, float zoom)
    {
        int mapX = (int)((mouseX - cameraX) / (4 * zoom));
        int mapY = (int)((mouseY - cameraY) / (4 * zoom));

        for (int y = mapY - _brushSize; y <= mapY + _brushSize; y++)
        {
            for (int x = mapX - _brushSize; x <= mapX + _brushSize; x++)
            {
                if (x >= 0 && x < _map.Width && y >= 0 && y < _map.Height)
                {
                    var cell = _map.Cells[x, y];
                    cell.LifeType = _selectedLifeForm;
                    cell.Biomass = 1.0f;
                }
            }
        }
    }

    public void Draw(SpriteBatch spriteBatch, int screenWidth, int screenHeight)
    {
        if (!IsVisible) return;

        string text = $"Life Painter | Brush: {_brushSize} | Species: {_selectedLifeForm}";
        var textSize = _font.MeasureString(text, 14);
        int panelWidth = (int)textSize.X + 20;
        int panelHeight = 40;
        int xPos = (screenWidth - panelWidth) / 2;
        int yPos = screenHeight - panelHeight - 10;

        // Background
        spriteBatch.Draw(_pixelTexture, new Rectangle(xPos, yPos, panelWidth, panelHeight), new Color(0, 0, 0, 200));
        // Border
        DrawBorder(spriteBatch, xPos, yPos, panelWidth, panelHeight, Color.White, 2);

        _font.DrawString(spriteBatch, text, new Vector2(xPos + 10, yPos + 10), Color.White, 14);
    }

    private void DrawBorder(SpriteBatch spriteBatch, int x, int y, int width, int height, Color color, int thickness)
    {
        spriteBatch.Draw(_pixelTexture, new Rectangle(x, y, width, thickness), color);
        spriteBatch.Draw(_pixelTexture, new Rectangle(x, y + height - thickness, width, thickness), color);
        spriteBatch.Draw(_pixelTexture, new Rectangle(x, y, thickness, height), color);
        spriteBatch.Draw(_pixelTexture, new Rectangle(x + width - thickness, y, thickness, height), color);
    }
}
