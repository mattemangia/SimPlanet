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

        // Update UI elements positions (shared with Draw)
        ComputeLayout(options);

        // Handle seed text input
        var keyState = Keyboard.GetState();
        if (_seedInputActive)
        {
            HandleSeedTextInput(keyState, options);
        }
        _previousKeyState = keyState;

        // Handle close button
        if (mouseState.LeftButton == ButtonState.Released &&
            _previousMouseState.LeftButton == ButtonState.Pressed)
        {
            if (_closeRect.Contains(mouseState.Position))
            {
                IsVisible = false;
                closeButtonClicked = true;
            }
        }

        // Handle slider dragging
        if (mouseState.LeftButton == ButtonState.Pressed)
        {
            // Only start a drag on a fresh press, then keep dragging that slider
            if (_activeSlider == null && _previousMouseState.LeftButton == ButtonState.Released)
            {
                foreach (var slider in _sliders)
                {
                    if (slider.Bounds.Contains(mouseState.Position))
                    {
                        _activeSlider = slider.Name;
                        break;
                    }
                }
            }

            foreach (var slider in _sliders)
            {
                if (_activeSlider == slider.Name)
                {
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

            // Check if clicking on seed input box
            if (_seedBox.Contains(mouseState.Position))
            {
                _seedInputActive = true;
                _seedInputText = options.Seed.ToString();
            }
            // Check if clicking outside to deactivate
            else
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

            if (_seedMinus.Contains(mouseState.Position))
            {
                options.Seed = Math.Max(0, options.Seed - 1);
                NeedsPreviewUpdate = true;
                _seedInputActive = false;
            }
            else if (_seedPlus.Contains(mouseState.Position))
            {
                options.Seed++;
                NeedsPreviewUpdate = true;
                _seedInputActive = false;
            }
            else if (_seedRandom.Contains(mouseState.Position))
            {
                options.Seed = new Random().Next();
                NeedsPreviewUpdate = true;
                _seedInputActive = false;
            }
        }

        _previousMouseState = mouseState;
        return closeButtonClicked;
    }

    // Layout rectangles (computed once per frame, used by Update and Draw)
    private Rectangle _panelRect, _closeRect, _previewRect, _seedBox, _seedMinus, _seedPlus, _seedRandom;
    private int _sizeLabelY, _presetLabelY, _shapeLabelY, _seedLabelY;
    private const int PanelWidth = 920;
    private const int PanelHeight = 548;

    private void ComputeLayout(MapGenerationOptions options)
    {
        _buttons.Clear();
        _sliders.Clear();

        var vp = _graphicsDevice.Viewport;
        int panelX = Math.Max(10, (vp.Width - PanelWidth) / 2);
        int panelY = Math.Max(10, (vp.Height - PanelHeight) / 2);
        _panelRect = new Rectangle(panelX, panelY, PanelWidth, PanelHeight);
        _closeRect = new Rectangle(panelX + PanelWidth - 32, panelY + 10, 22, 22);

        // ---- Left column: preview, size and presets ----
        int leftX = panelX + 20;
        int leftW = 440;
        _previewRect = new Rectangle(leftX, panelY + 58, leftW, leftW / 2);

        _sizeLabelY = _previewRect.Bottom + 14;
        int sizeY = _sizeLabelY + 20;
        int spacing = 8;
        int sizeW = (leftW - spacing * 3) / 4;
        var sizes = new (string Label, int W, int H)[] { ("Small", 128, 64), ("Standard", 240, 120), ("Large", 512, 256), ("Huge", 1024, 512) };
        for (int i = 0; i < sizes.Length; i++)
        {
            var sz = sizes[i];
            _buttons.Add(new UIButton(sz.Label, new Rectangle(leftX + i * (sizeW + spacing), sizeY, sizeW, 44),
                UITheme.Accent, (opt) => { opt.MapWidth = sz.W; opt.MapHeight = sz.H; })
            {
                Label = $"{sz.Label}\n{sz.W} x {sz.H}",
                Selected = options.MapWidth == sz.W
            });
        }

        _presetLabelY = sizeY + 44 + 14;
        int presetY = _presetLabelY + 20;
        var presets = new (string Name, Color Color, Action<MapGenerationOptions> Apply)[]
        {
            ("Earth", new Color(70, 160, 255), ApplyEarthPreset),
            ("Mars", new Color(220, 110, 60), ApplyMarsPreset),
            ("Water World", new Color(60, 120, 220), ApplyWaterWorldPreset),
            ("Desert", new Color(225, 185, 100), ApplyDesertWorldPreset)
        };
        for (int i = 0; i < presets.Length; i++)
        {
            var pr = presets[i];
            _buttons.Add(new UIButton(pr.Name, new Rectangle(leftX + i * (sizeW + spacing), presetY, sizeW, 36), pr.Color, pr.Apply));
        }

        // ---- Right column: shape sliders, seed and actions ----
        int rightX = panelX + 490;
        int rightW = PanelWidth - 490 - 24;
        _shapeLabelY = panelY + 58;
        int sliderY = _shapeLabelY + 24;
        foreach (var name in new[] { "LandRatio", "MountainLevel", "WaterLevel", "Persistence", "Lacunarity" })
        {
            // Bounds cover the label row and the track so the whole row is easy to grab
            _sliders.Add(new UISlider(name, new Rectangle(rightX, sliderY + 20, rightW, 18)));
            sliderY += 50;
        }

        _seedLabelY = sliderY + 4;
        int seedY = _seedLabelY + 20;
        _seedBox = new Rectangle(rightX, seedY, 150, 32);
        _seedMinus = new Rectangle(_seedBox.Right + 8, seedY, 32, 32);
        _seedPlus = new Rectangle(_seedMinus.Right + 6, seedY, 32, 32);
        _seedRandom = new Rectangle(_seedPlus.Right + 8, seedY, rightX + rightW - (_seedPlus.Right + 8), 32);

        // Actions (bottom right)
        int actionY = panelY + PanelHeight - 64;
        int generateW = 230;
        _buttons.Add(new UIButton("Generate", new Rectangle(rightX + rightW - generateW, actionY, generateW, 44),
            UITheme.Good, (opt) => { })
        { Label = "Generate World", Primary = true });
        _buttons.Add(new UIButton("Randomize Seed", new Rectangle(rightX, actionY, rightW - generateW - 10, 44),
            new Color(170, 120, 230), (opt) => opt.Seed = new Random().Next()));
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
                Octaves = options.Octaves
            };

            // CRITICAL: Pass reference dimensions as constructor params so noise sampling matches the actual map
            _previewMap = new PlanetMap(previewWidth, previewHeight, previewOptions, options.MapWidth, options.MapHeight);

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
            return new Color(236, 244, 250);

        if (cell.IsWater)
        {
            // Same bathymetry palette as the main map
            float d = Math.Clamp(-cell.Elevation, 0f, 1f);
            return d < 0.12f ? Color.Lerp(new Color(52, 160, 200), new Color(28, 110, 174), d / 0.12f)
                 : d < 0.45f ? Color.Lerp(new Color(28, 110, 174), new Color(14, 54, 120), (d - 0.12f) / 0.33f)
                 : Color.Lerp(new Color(14, 54, 120), new Color(6, 24, 68), Math.Min(1f, (d - 0.45f) / 0.5f));
        }

        float e = Math.Clamp(cell.Elevation, 0f, 1f);
        if (cell.IsDesert && e < 0.6f)
            return Color.Lerp(new Color(226, 200, 140), new Color(190, 150, 100), e / 0.6f);
        if (e < 0.08f) return new Color(214, 200, 150);                                  // coast
        if (e < 0.45f) return Color.Lerp(new Color(86, 150, 64), new Color(58, 110, 46), (e - 0.08f) / 0.37f);
        if (e < 0.7f) return Color.Lerp(new Color(120, 120, 80), new Color(130, 110, 90), (e - 0.45f) / 0.25f);
        return Color.Lerp(new Color(140, 132, 124), new Color(235, 235, 240), (e - 0.7f) / 0.3f);
    }

    public void Draw(MapGenerationOptions options)
    {
        if (!IsVisible) return;

        ComputeLayout(options);
        var mousePos = Mouse.GetState().Position;

        _spriteBatch.End();
        _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp);

        // Dim whatever is behind the dialog
        var vp = _graphicsDevice.Viewport;
        DrawRectangle(0, 0, vp.Width, vp.Height, new Color(4, 6, 12) * 0.55f);

        UITheme.DrawTitledPanel(_spriteBatch, _panelRect, "WORLD GENERATOR", UITheme.Gold, 44);

        // Close button (X)
        bool hoverClose = _closeRect.Contains(mousePos);
        UITheme.FillRounded(_spriteBatch, _closeRect, hoverClose ? new Color(200, 60, 60) : new Color(60, 70, 92));
        var cc = _closeRect.Center.ToVector2();
        UITheme.DrawLine(_spriteBatch, cc + new Vector2(-5, -5), cc + new Vector2(5, 5), Color.White, 1.8f);
        UITheme.DrawLine(_spriteBatch, cc + new Vector2(-5, 5), cc + new Vector2(5, -5), Color.White, 1.8f);

        // Preview
        UITheme.FillRounded(_spriteBatch, new Rectangle(_previewRect.X - 3, _previewRect.Y - 3, _previewRect.Width + 6, _previewRect.Height + 6), new Color(4, 8, 16));
        if (_previewTexture != null)
        {
            _spriteBatch.Draw(_previewTexture, _previewRect, Color.White);
        }
        else
        {
            DrawRectangle(_previewRect.X, _previewRect.Y, _previewRect.Width, _previewRect.Height, new Color(20, 28, 44));
            UITheme.DrawTextCentered(_spriteBatch, "Generating preview...", _previewRect, UITheme.TextDim, UITheme.FontMedium);
        }
        UITheme.OutlineRounded(_spriteBatch, new Rectangle(_previewRect.X - 3, _previewRect.Y - 3, _previewRect.Width + 6, _previewRect.Height + 6), UITheme.BorderBright);
        // Caption over the preview
        string caption = $"Seed {options.Seed}   {options.MapWidth} x {options.MapHeight}";
        var capSize = UITheme.Measure(caption, UITheme.FontSmall);
        var capRect = new Rectangle(_previewRect.X + 6, _previewRect.Bottom - (int)capSize.Y - 10, (int)capSize.X + 14, (int)capSize.Y + 6);
        UITheme.FillRounded(_spriteBatch, capRect, new Color(0, 0, 0) * 0.6f);
        UITheme.DrawText(_spriteBatch, caption, new Vector2(capRect.X + 7, capRect.Y + 3), UITheme.Text, UITheme.FontSmall);

        void SectionLabel(string text, int x, int y) => UITheme.DrawText(_spriteBatch, text, new Vector2(x, y), UITheme.Accent, 12f);
        SectionLabel("MAP SIZE", _previewRect.X, _sizeLabelY);
        SectionLabel("PRESETS", _previewRect.X, _presetLabelY);
        int rightX = _sliders.Count > 0 ? _sliders[0].Bounds.X : _panelRect.X + 490;
        SectionLabel("TERRAIN SHAPE", rightX, _shapeLabelY);
        SectionLabel("SEED", rightX, _seedLabelY);

        // Buttons
        foreach (var button in _buttons)
        {
            bool hover = button.Bounds.Contains(mousePos);
            if (button.Primary)
            {
                var r = button.Bounds;
                UITheme.FillRounded(_spriteBatch, r, hover ? new Color(60, 170, 90) : new Color(44, 140, 72));
                UITheme.FillGradient(_spriteBatch, new Rectangle(r.X + 1, r.Y + 1, r.Width - 2, r.Height / 2), Color.White * 0.12f);
                UITheme.OutlineRounded(_spriteBatch, r, new Color(140, 240, 160));
                UITheme.DrawTextCentered(_spriteBatch, button.Label, r, Color.White, UITheme.FontMedium + 1);
                continue;
            }

            UITheme.DrawButton(_spriteBatch, button.Bounds, button.Label, hover, button.Selected, button.Color, UITheme.FontNormal);
            // Colour key along the bottom edge (presets / selected size)
            _spriteBatch.Draw(_pixelTexture, new Rectangle(button.Bounds.X + 8, button.Bounds.Bottom - 3, button.Bounds.Width - 16, 2),
                button.Color * (hover || button.Selected ? 1f : 0.55f));
        }

        // Sliders
        foreach (var slider in _sliders)
        {
            float value = GetSliderValue(slider.Name, options);

            (string label, string valueText, Color color) = slider.Name switch
            {
                "LandRatio" => ("Land coverage", $"{value:P0}", new Color(120, 220, 120)),
                "MountainLevel" => ("Mountains", $"{value:P0}", new Color(230, 160, 80)),
                "WaterLevel" => ("Sea level", $"{(value * 2f - 1f):+0.00;-0.00;0.00}", new Color(100, 180, 255)),
                "Persistence" => ("Roughness", $"{value:F2}", new Color(210, 130, 230)),
                "Lacunarity" => ("Detail", $"{(1f + value * 3f):F2}", new Color(250, 220, 90)),
                _ => (slider.Name, $"{value:F2}", Color.White)
            };

            int labelY = slider.Bounds.Y - 20;
            UITheme.DrawText(_spriteBatch, label, new Vector2(slider.Bounds.X, labelY), UITheme.Text, UITheme.FontNormal);
            var vs = UITheme.Measure(valueText, UITheme.FontNormal);
            UITheme.DrawText(_spriteBatch, valueText, new Vector2(slider.Bounds.Right - vs.X, labelY), color, UITheme.FontNormal);

            var track = new Rectangle(slider.Bounds.X, slider.Bounds.Y + 6, slider.Bounds.Width, 6);
            UITheme.FillRounded(_spriteBatch, new Rectangle(track.X, track.Y - 1, track.Width, track.Height + 2), new Color(0, 0, 0, 150));
            int fillWidth = (int)(track.Width * value);
            if (fillWidth > 0)
                DrawRectangle(track.X, track.Y, fillWidth, track.Height, color * 0.9f);

            bool active = _activeSlider == slider.Name || slider.Bounds.Contains(mousePos);
            var handle = new Vector2(track.X + fillWidth, track.Y + track.Height / 2f);
            UITheme.DrawGlow(_spriteBatch, handle, active ? 14 : 10, color * 0.35f);
            UITheme.FillRounded(_spriteBatch, new Rectangle((int)handle.X - 7, (int)handle.Y - 7, 14, 14), Color.White);
            UITheme.FillRounded(_spriteBatch, new Rectangle((int)handle.X - 4, (int)handle.Y - 4, 8, 8), color);
        }

        // Seed input
        Color inputBg = _seedInputActive ? new Color(40, 56, 88) : new Color(10, 14, 24);
        UITheme.FillRounded(_spriteBatch, _seedBox, inputBg);
        UITheme.OutlineRounded(_spriteBatch, _seedBox, _seedInputActive ? UITheme.Accent : UITheme.Border);
        string seedText = _seedInputActive ? _seedInputText : options.Seed.ToString();
        var st = UITheme.Measure(seedText.Length > 0 ? seedText : "0", UITheme.FontMedium);
        UITheme.DrawText(_spriteBatch, seedText, new Vector2(_seedBox.X + 10, _seedBox.Y + (_seedBox.Height - st.Y) / 2f), Color.White, UITheme.FontMedium);
        if (_seedInputActive && (DateTime.Now.Millisecond / 500) % 2 == 0)
        {
            var textSize = UITheme.Measure(seedText, UITheme.FontMedium);
            DrawRectangle((int)(_seedBox.X + 11 + textSize.X), _seedBox.Y + 7, 2, _seedBox.Height - 14, Color.White);
        }
        if (!_seedInputActive && _seedBox.Contains(mousePos))
        {
            UITheme.DrawTooltip(_spriteBatch, "Click to type a seed", new Point(_seedBox.Center.X, _seedBox.Bottom), vp.Width, vp.Height);
        }

        UITheme.DrawButton(_spriteBatch, _seedMinus, "-", _seedMinus.Contains(mousePos), false, null, UITheme.FontMedium);
        UITheme.DrawButton(_spriteBatch, _seedPlus, "+", _seedPlus.Contains(mousePos), false, null, UITheme.FontMedium);
        UITheme.DrawButton(_spriteBatch, _seedRandom, "Random", _seedRandom.Contains(mousePos), false, null, UITheme.FontNormal);

        // Footer hint
        UITheme.DrawText(_spriteBatch, "ESC: back   -   tip: presets set the sliders, then fine-tune",
            new Vector2(_panelRect.X + 20, _panelRect.Bottom - 26), UITheme.TextMuted, UITheme.FontSmall);

        _spriteBatch.End();
        _spriteBatch.Begin(samplerState: SamplerState.PointClamp);
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
        public string Label { get; set; }
        public bool Selected { get; set; }
        public bool Primary { get; set; }
        public Rectangle Bounds { get; set; }
        public Color Color { get; set; }
        public Action<MapGenerationOptions> OnClick { get; set; }

        public UIButton(string name, Rectangle bounds, Color color, Action<MapGenerationOptions> onClick)
        {
            Name = name;
            Label = name;
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
