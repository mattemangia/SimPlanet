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

    private bool _isVisible = false;
    public bool IsVisible
    {
        get => _isVisible;
        set
        {
            if (value && !_isVisible)
            {
                // Force preview generation when showing
                NeedsPreviewUpdate = true;
            }
            _isVisible = value;
        }
    }
    public bool NeedsPreviewUpdate { get; set; } = true;
    public bool GenerateRequested { get; private set; } = false;

    // Slider tracking
    private string? _activeSlider = null;
    private List<UIButton> _buttons = new();
    private List<UISlider> _sliders = new();

    // Seed input
    private bool _seedInputActive = false;
    private string _seedInputText = "";
    private KeyboardState _previousKeyState;

    // Performance: Throttle preview updates to prevent lag
    private DateTime _lastPreviewUpdate = DateTime.MinValue;
    private const double PreviewThrottleMs = 150; // Update preview max every 150ms

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
            _previousKeyState = Keyboard.GetState();
            return false;
        }

        int panelX = 300;
        int panelY = 50;
        int panelWidth = 680;
        int panelHeight = 620;

        // Update UI elements positions
        UpdateUIElements(panelX, panelY, panelWidth, options);

        // Handle seed text input
        var keyState = Keyboard.GetState();
        if (_seedInputActive)
        {
            HandleSeedTextInput(keyState, options);
        }
        _previousKeyState = keyState;

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

            // Handle seed control buttons
            int seedY = panelY + panelHeight - 60;
            Rectangle seedInputBox = new Rectangle(panelX + 80, seedY - 2, 110, 20);
            Rectangle decreaseSeedBtn = new Rectangle(panelX + 195, seedY - 2, 30, 20);
            Rectangle increaseSeedBtn = new Rectangle(panelX + 230, seedY - 2, 30, 20);
            Rectangle randomSeedBtn = new Rectangle(panelX + 265, seedY - 2, 80, 20);

            // Check if clicking on seed input box
            if (seedInputBox.Contains(mouseState.Position))
            {
                _seedInputActive = true;
                _seedInputText = options.Seed.ToString();
            }
            // Check if clicking outside to deactivate
            else if (!seedInputBox.Contains(mouseState.Position))
            {
                if (_seedInputActive)
                {
                    // Try to parse the input
                    if (int.TryParse(_seedInputText, out int newSeed))
                    {
                        options.Seed = Math.Max(0, newSeed);
                        NeedsPreviewUpdate = true;
                    }
                }
                _seedInputActive = false;
            }

            if (decreaseSeedBtn.Contains(mouseState.Position))
            {
                options.Seed = Math.Max(0, options.Seed - 1);
                NeedsPreviewUpdate = true;
                _seedInputActive = false;
            }
            else if (increaseSeedBtn.Contains(mouseState.Position))
            {
                options.Seed++;
                NeedsPreviewUpdate = true;
                _seedInputActive = false;
            }
            else if (randomSeedBtn.Contains(mouseState.Position))
            {
                options.Seed = new Random().Next();
                NeedsPreviewUpdate = true;
                _seedInputActive = false;
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

        // Performance: Throttle preview updates to prevent lag during slider dragging
        var timeSinceLastUpdate = (DateTime.Now - _lastPreviewUpdate).TotalMilliseconds;
        if (timeSinceLastUpdate < PreviewThrottleMs)
        {
            return; // Skip this update, too soon since last one
        }

        try
        {
            // Generate preview map at half resolution but sampling at full resolution for accuracy
            int previewWidth = options.MapWidth / 2;
            int previewHeight = options.MapHeight / 2;

            var previewOptions = new MapGenerationOptions
            {
                Seed = options.Seed,
                MapWidth = previewWidth,
                MapHeight = previewHeight,
                LandRatio = options.LandRatio,
                MountainLevel = options.MountainLevel,
                WaterLevel = options.WaterLevel,
                Persistence = options.Persistence,
                Lacunarity = options.Lacunarity,
                Octaves = options.Octaves,
                // CRITICAL: Set reference dimensions to match actual map so noise sampling is identical
                ReferenceWidth = options.MapWidth,
                ReferenceHeight = options.MapHeight
            };

            // Create preview map - reference dimensions are already set in previewOptions
            _previewMap = new PlanetMap(previewWidth, previewHeight, previewOptions);

            // Create preview texture
            if (_previewTexture == null || _previewTexture.Width != previewWidth || _previewTexture.Height != previewHeight)
            {
                _previewTexture?.Dispose();
                _previewTexture = new Texture2D(_graphicsDevice, previewWidth, previewHeight);
            }

            // Generate preview colors
            var colors = new Color[previewWidth * previewHeight];
            for (int x = 0; x < previewWidth; x++)
            {
                for (int y = 0; y < previewHeight; y++)
                {
                    var cell = _previewMap.Cells[x, y];
                    colors[y * previewWidth + x] = GetPreviewColor(cell);
                }
            }

            _previewTexture.SetData(colors);
            NeedsPreviewUpdate = false;
            _lastPreviewUpdate = DateTime.Now;
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

        // Seed controls
        int seedY = panelY + panelHeight - 60;
        _font.DrawString(_spriteBatch, "Seed:", new Vector2(panelX + 20, seedY), Color.Cyan, 14);

        // Seed input box
        Rectangle seedInputBox = new Rectangle(panelX + 80, seedY - 2, 110, 20);
        Color inputBgColor = _seedInputActive ? new Color(80, 80, 120) : new Color(40, 40, 60);
        Color inputBorderColor = _seedInputActive ? new Color(150, 200, 255) : Color.White;
        DrawRectangle(seedInputBox.X, seedInputBox.Y, seedInputBox.Width, seedInputBox.Height, inputBgColor);
        DrawRectangleBorder(seedInputBox.X, seedInputBox.Y, seedInputBox.Width, seedInputBox.Height, inputBorderColor, _seedInputActive ? 2 : 1);

        // Draw seed text (either editing or current value)
        string seedText = _seedInputActive ? _seedInputText : options.Seed.ToString();
        _font.DrawString(_spriteBatch, seedText, new Vector2(seedInputBox.X + 4, seedInputBox.Y + 2), Color.White, 12);

        // Draw cursor if active
        if (_seedInputActive && (DateTime.Now.Millisecond / 500) % 2 == 0)
        {
            var textSize = _font.MeasureString(seedText, 12);
            DrawRectangle((int)(seedInputBox.X + 4 + textSize.X), seedInputBox.Y + 3, 2, 14, Color.White);
        }

        // Seed buttons
        Rectangle decreaseSeedBtn = new Rectangle(panelX + 195, seedY - 2, 30, 20);
        Rectangle increaseSeedBtn = new Rectangle(panelX + 230, seedY - 2, 30, 20);
        Rectangle randomSeedBtn = new Rectangle(panelX + 265, seedY - 2, 80, 20);

        // Draw seed buttons
        DrawRectangle(decreaseSeedBtn.X, decreaseSeedBtn.Y, decreaseSeedBtn.Width, decreaseSeedBtn.Height,
            decreaseSeedBtn.Contains(mousePos) ? new Color(100, 100, 150) : new Color(60, 60, 100));
        DrawRectangleBorder(decreaseSeedBtn.X, decreaseSeedBtn.Y, decreaseSeedBtn.Width, decreaseSeedBtn.Height, Color.White, 1);
        _font.DrawString(_spriteBatch, "-", new Vector2(decreaseSeedBtn.X + 10, decreaseSeedBtn.Y + 2), Color.White, 14);

        DrawRectangle(increaseSeedBtn.X, increaseSeedBtn.Y, increaseSeedBtn.Width, increaseSeedBtn.Height,
            increaseSeedBtn.Contains(mousePos) ? new Color(100, 100, 150) : new Color(60, 60, 100));
        DrawRectangleBorder(increaseSeedBtn.X, increaseSeedBtn.Y, increaseSeedBtn.Width, increaseSeedBtn.Height, Color.White, 1);
        _font.DrawString(_spriteBatch, "+", new Vector2(increaseSeedBtn.X + 9, increaseSeedBtn.Y + 2), Color.White, 14);

        DrawRectangle(randomSeedBtn.X, randomSeedBtn.Y, randomSeedBtn.Width, randomSeedBtn.Height,
            randomSeedBtn.Contains(mousePos) ? new Color(100, 150, 100) : new Color(60, 100, 60));
        DrawRectangleBorder(randomSeedBtn.X, randomSeedBtn.Y, randomSeedBtn.Width, randomSeedBtn.Height, Color.White, 1);
        _font.DrawString(_spriteBatch, "Random", new Vector2(randomSeedBtn.X + 10, randomSeedBtn.Y + 2), Color.White, 12);

        // Instructions
        string info = $"Size: {options.MapWidth}x{options.MapHeight}";
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

    private void HandleSeedTextInput(KeyboardState keyState, MapGenerationOptions options)
    {
        // Handle number keys
        var keys = new[]
        {
            (Keys.D0, '0'), (Keys.D1, '1'), (Keys.D2, '2'), (Keys.D3, '3'), (Keys.D4, '4'),
            (Keys.D5, '5'), (Keys.D6, '6'), (Keys.D7, '7'), (Keys.D8, '8'), (Keys.D9, '9'),
            (Keys.NumPad0, '0'), (Keys.NumPad1, '1'), (Keys.NumPad2, '2'), (Keys.NumPad3, '3'),
            (Keys.NumPad4, '4'), (Keys.NumPad5, '5'), (Keys.NumPad6, '6'), (Keys.NumPad7, '7'),
            (Keys.NumPad8, '8'), (Keys.NumPad9, '9')
        };

        foreach (var (key, character) in keys)
        {
            if (keyState.IsKeyDown(key) && _previousKeyState.IsKeyUp(key))
            {
                if (_seedInputText.Length < 10) // Limit to 10 digits
                {
                    _seedInputText += character;
                }
            }
        }

        // Handle backspace
        if (keyState.IsKeyDown(Keys.Back) && _previousKeyState.IsKeyUp(Keys.Back))
        {
            if (_seedInputText.Length > 0)
            {
                _seedInputText = _seedInputText.Substring(0, _seedInputText.Length - 1);
            }
        }

        // Handle Enter to confirm
        if (keyState.IsKeyDown(Keys.Enter) && _previousKeyState.IsKeyUp(Keys.Enter))
        {
            if (int.TryParse(_seedInputText, out int newSeed))
            {
                options.Seed = Math.Max(0, newSeed);
                NeedsPreviewUpdate = true;
            }
            _seedInputActive = false;
        }

        // Handle Escape to cancel
        if (keyState.IsKeyDown(Keys.Escape) && _previousKeyState.IsKeyUp(Keys.Escape))
        {
            _seedInputActive = false;
        }
    }

    public static void ApplyEarthPreset(MapGenerationOptions options)
    {
        options.LandRatio = 0.29f;      // 29% land (Earth-like)
        options.MountainLevel = 0.6f;   // Moderate mountains
        options.WaterLevel = 0.0f;      // Balanced sea level
        options.Persistence = 0.5f;     // Smooth continents
        options.Lacunarity = 2.0f;      // Normal detail
        options.Octaves = 6;
    }

    public static void ApplyMarsPreset(MapGenerationOptions options)
    {
        options.LandRatio = 0.95f;      // Almost all land (Mars has no oceans, only low areas)
        options.MountainLevel = 0.8f;   // Very high mountains (Olympus Mons!)
        options.WaterLevel = -0.2f;     // Lower "sea level" to create basins
        options.Persistence = 0.55f;    // Rough terrain
        options.Lacunarity = 2.2f;      // High detail
        options.Octaves = 7;
    }

    public static void ApplyWaterWorldPreset(MapGenerationOptions options)
    {
        options.LandRatio = 0.08f;      // Only 8% land (small islands)
        options.MountainLevel = 0.3f;   // Low mountains on islands
        options.WaterLevel = 0.15f;     // High sea level
        options.Persistence = 0.4f;     // Smooth ocean floor
        options.Lacunarity = 1.8f;      // Less detail (smoother)
        options.Octaves = 5;
    }

    public static void ApplyDesertWorldPreset(MapGenerationOptions options)
    {
        options.LandRatio = 0.75f;      // 75% land (dry planet)
        options.MountainLevel = 0.5f;   // Moderate dunes and plateaus
        options.WaterLevel = -0.15f;    // Lower sea level (small seas/lakes)
        options.Persistence = 0.45f;    // Sandy, smooth terrain
        options.Lacunarity = 2.3f;      // Fine detail for dunes
        options.Octaves = 7;
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
