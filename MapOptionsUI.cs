using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;

namespace SimPlanet;

/// <summary>
/// Interactive UI for map generation with mouse controls
/// </summary>
public class MapOptionsUI
{
    private readonly FontRenderer _font;
    private readonly SpriteBatch _spriteBatch;
    private readonly GraphicsDevice _graphicsDevice;
    private Texture2D _pixelTexture;
    private Texture2D? _previewTexture;
    private PlanetMap? _previewMap;
    private MouseState _previousMouseState;

    public bool IsVisible { get; set; } = false;
    public bool NeedsPreviewUpdate { get; set; } = true;
    public bool GenerateRequested { get; private set; } = false;

    // Slider tracking
    private string? _activeSlider = null;
    private List<UIButton> _buttons = new();
    private List<UISlider> _sliders = new();

    public MapOptionsUI(SpriteBatch spriteBatch, FontRenderer font, GraphicsDevice graphicsDevice)
    {
        _spriteBatch = spriteBatch;
        _font = font;
        _graphicsDevice = graphicsDevice;

        _pixelTexture = new Texture2D(_graphicsDevice, 1, 1);
        _pixelTexture.SetData(new[] { Color.White });
        _previousMouseState = Mouse.GetState();
    }

    public bool Update(MouseState mouseState, MapGenerationOptions options)
    {
        bool closeButtonClicked = false;
        GenerateRequested = false;

        if (!IsVisible)
        {
            _previousMouseState = mouseState;
            return false;
        }

        int panelX = 300;
        int panelY = 50;
        int panelWidth = 680;

        // Update UI elements positions
        UpdateUIElements(panelX, panelY, panelWidth, options);

        // Handle close button
        Rectangle closeButtonBounds = new Rectangle(panelX + panelWidth - 30, panelY + 5, 25, 25);
        if (mouseState.LeftButton == ButtonState.Released &&
            _previousMouseState.LeftButton == ButtonState.Pressed)
        {
            if (closeButtonBounds.Contains(mouseState.Position))
            {
                IsVisible = false;
                closeButtonClicked = true;
            }
        }

        // Handle slider dragging
        if (mouseState.LeftButton == ButtonState.Pressed)
        {
            foreach (var slider in _sliders)
            {
                if (_activeSlider == slider.Name || slider.Bounds.Contains(mouseState.Position))
                {
                    _activeSlider = slider.Name;
                    float newValue = (mouseState.X - slider.Bounds.X) / (float)slider.Bounds.Width;
                    newValue = Math.Clamp(newValue, 0f, 1f);

                    ApplySliderValue(slider.Name, newValue, options);
                    NeedsPreviewUpdate = true;
                }
            }
        }
        else
        {
            _activeSlider = null;
        }

        // Handle button clicks
        if (mouseState.LeftButton == ButtonState.Released &&
            _previousMouseState.LeftButton == ButtonState.Pressed)
        {
            foreach (var button in _buttons)
            {
                if (button.Bounds.Contains(mouseState.Position))
                {
                    button.OnClick(options);
                    if (button.Name == "Generate")
                    {
                        GenerateRequested = true;
                    }
                    NeedsPreviewUpdate = true;
                }
            }
        }

        _previousMouseState = mouseState;
        return closeButtonClicked;
    }

    private void UpdateUIElements(int panelX, int panelY, int panelWidth, MapGenerationOptions options)
    {
        _buttons.Clear();
        _sliders.Clear();

        int buttonY = panelY + 250;
        int buttonWidth = 150;
        int buttonHeight = 35;
        int buttonSpacing = 10;

        // Preset buttons
        int startX = panelX + (panelWidth - (buttonWidth * 4 + buttonSpacing * 3)) / 2;

        _buttons.Add(new UIButton("Earth", new Rectangle(startX, buttonY, buttonWidth, buttonHeight),
            new Color(50, 150, 255), (opt) => ApplyEarthPreset(opt)));

        _buttons.Add(new UIButton("Mars", new Rectangle(startX + buttonWidth + buttonSpacing, buttonY, buttonWidth, buttonHeight),
            new Color(200, 100, 50), (opt) => ApplyMarsPreset(opt)));

        _buttons.Add(new UIButton("Water World", new Rectangle(startX + (buttonWidth + buttonSpacing) * 2, buttonY, buttonWidth, buttonHeight),
            new Color(50, 100, 200), (opt) => ApplyWaterWorldPreset(opt)));

        _buttons.Add(new UIButton("Desert", new Rectangle(startX + (buttonWidth + buttonSpacing) * 3, buttonY, buttonWidth, buttonHeight),
            new Color(220, 180, 100), (opt) => ApplyDesertWorldPreset(opt)));

        // Action buttons
        buttonY += buttonHeight + 15;
        int actionButtonWidth = (panelWidth - 60) / 2;

        _buttons.Add(new UIButton("Randomize Seed", new Rectangle(panelX + 20, buttonY, actionButtonWidth, buttonHeight),
            new Color(150, 100, 200), (opt) => opt.Seed = new Random().Next()));

        _buttons.Add(new UIButton("Generate", new Rectangle(panelX + panelWidth - actionButtonWidth - 20, buttonY, actionButtonWidth, buttonHeight),
            new Color(50, 200, 50), (opt) => { }));

        // Sliders
        int sliderY = buttonY + buttonHeight + 25;
        int sliderX = panelX + 200;
        int sliderWidth = panelWidth - 250;
        int sliderHeight = 20;
        int sliderSpacing = 35;

        _sliders.Add(new UISlider("LandRatio", new Rectangle(sliderX, sliderY, sliderWidth, sliderHeight)));
        sliderY += sliderSpacing;

        _sliders.Add(new UISlider("MountainLevel", new Rectangle(sliderX, sliderY, sliderWidth, sliderHeight)));
        sliderY += sliderSpacing;

        _sliders.Add(new UISlider("WaterLevel", new Rectangle(sliderX, sliderY, sliderWidth, sliderHeight)));
        sliderY += sliderSpacing;

        _sliders.Add(new UISlider("Persistence", new Rectangle(sliderX, sliderY, sliderWidth, sliderHeight)));
        sliderY += sliderSpacing;

        _sliders.Add(new UISlider("Lacunarity", new Rectangle(sliderX, sliderY, sliderWidth, sliderHeight)));
    }

    private void ApplySliderValue(string sliderName, float normalizedValue, MapGenerationOptions options)
    {
        switch (sliderName)
        {
            case "LandRatio":
                options.LandRatio = normalizedValue;
                break;
            case "MountainLevel":
                options.MountainLevel = normalizedValue;
                break;
            case "WaterLevel":
                options.WaterLevel = normalizedValue * 2f - 1f; // -1 to 1
                break;
            case "Persistence":
                options.Persistence = normalizedValue;
                break;
            case "Lacunarity":
                options.Lacunarity = 1f + normalizedValue * 3f; // 1 to 4
                break;
        }
    }

    private float GetSliderValue(string sliderName, MapGenerationOptions options)
    {
        return sliderName switch
        {
            "LandRatio" => options.LandRatio,
            "MountainLevel" => options.MountainLevel,
            "WaterLevel" => (options.WaterLevel + 1f) / 2f,
            "Persistence" => options.Persistence,
            "Lacunarity" => (options.Lacunarity - 1f) / 3f,
            _ => 0f
        };
    }

    public void UpdatePreview(MapGenerationOptions options)
    {
        if (!NeedsPreviewUpdate) return;

        try
        {
            // Generate small preview map (100x50 for performance)
            _previewMap = new PlanetMap(100, 50, options);

            // Create preview texture
            if (_previewTexture == null || _previewTexture.Width != 100)
            {
                _previewTexture?.Dispose();
                _previewTexture = new Texture2D(_graphicsDevice, 100, 50);
            }

            // Generate preview colors
            var colors = new Color[100 * 50];
            for (int x = 0; x < 100; x++)
            {
                for (int y = 0; y < 50; y++)
                {
                    var cell = _previewMap.Cells[x, y];
                    colors[y * 100 + x] = GetPreviewColor(cell);
                }
            }

            _previewTexture.SetData(colors);
            NeedsPreviewUpdate = false;
        }
        catch
        {
            // If preview generation fails, mark for retry
            NeedsPreviewUpdate = true;
        }
    }

    private Color GetPreviewColor(TerrainCell cell)
    {
        if (cell.IsIce)
            return new Color(240, 250, 255);

        if (cell.IsWater)
        {
            if (cell.Elevation < -0.5f)
                return new Color(10, 50, 120); // Deep ocean
            else
                return new Color(50, 100, 180); // Shallow water
        }

        if (cell.Elevation > 0.7f)
            return new Color(140, 130, 120); // Mountains
        if (cell.Elevation > 0.4f)
            return new Color(100, 150, 80); // Hills
        if (cell.IsDesert)
            return new Color(230, 200, 140); // Desert

        return new Color(80, 140, 60); // Grassland/forest
    }

    public void Draw(MapGenerationOptions options)
    {
        if (!IsVisible) return;

        int panelX = 300;
        int panelY = 50;
        int panelWidth = 680;
        int panelHeight = 620;

        // Draw background with gradient
        DrawRectangle(panelX, panelY, panelWidth, panelHeight, new Color(20, 20, 40, 250));
        DrawRectangle(panelX, panelY, panelWidth, 4, new Color(100, 150, 255, 255)); // Top border
        DrawRectangle(panelX, panelY + panelHeight - 4, panelWidth, 4, new Color(100, 150, 255, 255)); // Bottom border

        // Draw close button (X)
        Rectangle closeButtonBounds = new Rectangle(panelX + panelWidth - 30, panelY + 5, 25, 25);
        var mousePos = Mouse.GetState().Position;
        Color closeColor = closeButtonBounds.Contains(mousePos) ? new Color(255, 50, 50) : new Color(180, 0, 0, 200);
        DrawRectangle(closeButtonBounds.X, closeButtonBounds.Y, closeButtonBounds.Width, closeButtonBounds.Height, closeColor);
        _font.DrawString(_spriteBatch, "X", new Vector2(closeButtonBounds.X + 7, closeButtonBounds.Y + 3), Color.White, 16);

        // Title
        _font.DrawString(_spriteBatch, "WORLD GENERATOR", new Vector2(panelX + 230, panelY + 15), Color.Yellow, 20);

        // Draw preview
        int previewWidth = 500;
        int previewHeight = 200;
        int previewX = panelX + (panelWidth - previewWidth) / 2;
        int previewY = panelY + 45;

        if (_previewTexture != null)
        {
            _spriteBatch.Draw(_previewTexture,
                new Rectangle(previewX, previewY, previewWidth, previewHeight),
                Color.White);
        }
        else
        {
            // Show loading text
            DrawRectangle(previewX, previewY, previewWidth, previewHeight, new Color(30, 30, 50));
            _font.DrawString(_spriteBatch, "Generating Preview...",
                new Vector2(previewX + 160, previewY + 90), Color.Gray, 18);
        }

        // Preview border
        DrawRectangleBorder(previewX - 2, previewY - 2, previewWidth + 4, previewHeight + 4, Color.White, 2);

        // Draw preset buttons
        foreach (var button in _buttons)
        {
            bool hover = button.Bounds.Contains(mousePos);
            Color bgColor = hover ? Color.Lerp(button.Color, Color.White, 0.3f) : button.Color;

            DrawRectangle(button.Bounds.X, button.Bounds.Y, button.Bounds.Width, button.Bounds.Height, bgColor);
            DrawRectangleBorder(button.Bounds.X, button.Bounds.Y, button.Bounds.Width, button.Bounds.Height, Color.White, 2);

            var textSize = _font.MeasureString(button.Name, 16);
            float textX = button.Bounds.X + (button.Bounds.Width - textSize.X) / 2;
            float textY = button.Bounds.Y + (button.Bounds.Height - textSize.Y) / 2;
            _font.DrawString(_spriteBatch, button.Name, new Vector2(textX, textY), Color.White, 16);
        }

        // Draw sliders
        int labelX = panelX + 20;
        foreach (var slider in _sliders)
        {
            float value = GetSliderValue(slider.Name, options);

            // Label
            string label = slider.Name switch
            {
                "LandRatio" => $"Land Ratio: {value:P0}",
                "MountainLevel" => $"Mountains: {value:P0}",
                "WaterLevel" => $"Water: {(value * 2f - 1f):F2}",
                "Persistence" => $"Smoothness: {value:F2}",
                "Lacunarity" => $"Detail: {(1f + value * 3f):F2}",
                _ => slider.Name
            };

            Color labelColor = slider.Name switch
            {
                "LandRatio" => Color.LightGreen,
                "MountainLevel" => Color.Orange,
                "WaterLevel" => Color.LightBlue,
                "Persistence" => Color.Magenta,
                "Lacunarity" => Color.Yellow,
                _ => Color.White
            };

            _font.DrawString(_spriteBatch, label, new Vector2(labelX, slider.Bounds.Y), labelColor, 14);

            // Slider background
            DrawRectangle(slider.Bounds.X, slider.Bounds.Y, slider.Bounds.Width, slider.Bounds.Height, new Color(40, 40, 40, 200));

            // Slider fill
            int fillWidth = (int)(slider.Bounds.Width * value);
            DrawRectangle(slider.Bounds.X, slider.Bounds.Y, fillWidth, slider.Bounds.Height, labelColor);

            // Slider border
            DrawRectangleBorder(slider.Bounds.X, slider.Bounds.Y, slider.Bounds.Width, slider.Bounds.Height, Color.White, 1);

            // Slider handle
            int handleX = slider.Bounds.X + fillWidth - 5;
            DrawRectangle(handleX, slider.Bounds.Y - 2, 10, slider.Bounds.Height + 4, Color.White);
        }

        // Instructions
        string info = $"Seed: {options.Seed} | Size: {options.MapWidth}x{options.MapHeight}";
        _font.DrawString(_spriteBatch, info, new Vector2(panelX + 20, panelY + panelHeight - 30), Color.Gray, 12);
    }

    private void DrawRectangle(int x, int y, int width, int height, Color color)
    {
        _spriteBatch.Draw(_pixelTexture, new Rectangle(x, y, width, height), color);
    }

    private void DrawRectangleBorder(int x, int y, int width, int height, Color color, int thickness)
    {
        DrawRectangle(x, y, width, thickness, color); // Top
        DrawRectangle(x, y + height - thickness, width, thickness, color); // Bottom
        DrawRectangle(x, y, thickness, height, color); // Left
        DrawRectangle(x + width - thickness, y, thickness, height, color); // Right
    }

    public static void ApplyEarthPreset(MapGenerationOptions options)
    {
        options.LandRatio = 0.29f;
        options.MountainLevel = 0.5f;
        options.WaterLevel = 0.0f;
        options.Persistence = 0.55f;
        options.Lacunarity = 2.1f;
        options.Octaves = 6;
    }

    public static void ApplyMarsPreset(MapGenerationOptions options)
    {
        options.LandRatio = 1.0f;
        options.MountainLevel = 0.7f;
        options.WaterLevel = -0.5f;
        options.Persistence = 0.6f;
        options.Lacunarity = 2.0f;
        options.Octaves = 7;
    }

    public static void ApplyWaterWorldPreset(MapGenerationOptions options)
    {
        options.LandRatio = 0.1f;
        options.MountainLevel = 0.3f;
        options.WaterLevel = 0.3f;
        options.Persistence = 0.45f;
        options.Lacunarity = 1.8f;
        options.Octaves = 5;
    }

    public static void ApplyDesertWorldPreset(MapGenerationOptions options)
    {
        options.LandRatio = 0.85f;
        options.MountainLevel = 0.4f;
        options.WaterLevel = -0.3f;
        options.Persistence = 0.5f;
        options.Lacunarity = 2.5f;
        options.Octaves = 8;
    }

    private class UIButton
    {
        public string Name { get; set; }
        public Rectangle Bounds { get; set; }
        public Color Color { get; set; }
        public Action<MapGenerationOptions> OnClick { get; set; }

        public UIButton(string name, Rectangle bounds, Color color, Action<MapGenerationOptions> onClick)
        {
            Name = name;
            Bounds = bounds;
            Color = color;
            OnClick = onClick;
        }
    }

    private class UISlider
    {
        public string Name { get; set; }
        public Rectangle Bounds { get; set; }

        public UISlider(string name, Rectangle bounds)
        {
            Name = name;
            Bounds = bounds;
        }
    }
}
