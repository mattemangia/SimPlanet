using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;

namespace SimPlanet;

public class LifePainterUI : IDisposable
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
            PaintLife(mouseState.X, mouseState.Y, cameraX, cameraY, zoom, screenOffsetX, screenOffsetY);
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

    private void PaintLife(int mouseX, int mouseY, int cameraX, int cameraY, float zoom, int screenOffsetX, int screenOffsetY)
    {
        // Correct mapping: mapX = (mouseX - screenOffsetX + cameraX) / (CellSize * Zoom)
        // CellSize is hardcoded as 4 in original code, consistent here
        int mapX = (int)((mouseX - screenOffsetX + cameraX) / (4 * zoom));
        int mapY = (int)((mouseY - screenOffsetY + cameraY) / (4 * zoom));

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

        // Status bar above the bottom button bar
        var items = new (string, string, Color?)[]
        {
            ("Species", SpeciesName(_selectedLifeForm), UITheme.Good),
            ("Brush", _brushSize.ToString(), UITheme.Gold),
        };
        UIWidgets.DrawToolBar(spriteBatch, screenWidth / 2, screenHeight - 115, screenWidth, "Life Painter",
            UITheme.Good, items,
            "Hold left button: paint  -  right-click: next species  -  wheel: brush size");
    }

    /// <summary>"SimpleAnimals" -> "Simple Animals"; None paints life away.</summary>
    private static string SpeciesName(LifeForm form)
    {
        if (form == LifeForm.None) return "None (erase)";
        string name = form.ToString();
        var sb = new System.Text.StringBuilder(name.Length + 4);
        for (int i = 0; i < name.Length; i++)
        {
            if (i > 0 && char.IsUpper(name[i]) && !char.IsUpper(name[i - 1])) sb.Append(' ');
            sb.Append(name[i]);
        }
        return sb.ToString();
    }

    public void Dispose()
    {
        _pixelTexture?.Dispose();
    }
}
