using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;

namespace SimPlanet;

/// <summary>
/// SimEarth-style planetary controls panel for adjusting all planetary parameters
/// Control climate, atmosphere, geology, surface properties, and AI stabilizer
/// </summary>
public class PlanetaryControlsUI
{
    private readonly GraphicsDevice _graphicsDevice;
    private readonly FontRenderer _font;
    private readonly PlanetMap _map;
    private readonly MagnetosphereSimulator _magnetosphere;
    private readonly PlanetStabilizer _stabilizer;
    private GeologicalSimulator _geologicalSimulator;
    private Texture2D _pixelTexture;

    public bool IsVisible { get; set; } = false;

    private MouseState _previousMouseState;

    // Slider for each parameter
    private class Slider
    {
        public string Label { get; set; }
        public Rectangle Bounds { get; set; }
        public float Value { get; set; }
        public float Min { get; set; }
        public float Max { get; set; }
        public string Unit { get; set; }
        public bool IsDragging { get; set; }
        public Action<float> OnValueChanged { get; set; }

        public Slider(string label, float min, float max, float initial, string unit, Action<float> onChanged)
        {
            Label = label;
            Min = min;
            Max = max;
            Value = initial;
            Unit = unit;
            OnValueChanged = onChanged;
        }

        public float GetNormalizedValue() => (Value - Min) / (Max - Min);
        public void SetNormalizedValue(float normalized) => Value = Math.Clamp(Min + normalized * (Max - Min), Min, Max);
    }

    private List<Slider> _sliders = new();
    private List<UIButton> _buttons = new();

    public PlanetaryControlsUI(GraphicsDevice graphicsDevice, FontRenderer font, PlanetMap map,
        MagnetosphereSimulator magnetosphere, PlanetStabilizer stabilizer)
    {
        _graphicsDevice = graphicsDevice;
        _font = font;
        _map = map;
        _magnetosphere = magnetosphere;
        _stabilizer = stabilizer;

        _pixelTexture = new Texture2D(_graphicsDevice, 1, 1);
        _pixelTexture.SetData(new[] { Color.White });

        InitializeControls();
    }

    public void SetGeologicalSimulator(GeologicalSimulator geologicalSimulator)
    {
        _geologicalSimulator = geologicalSimulator;
    }

    private void InitializeControls()
    {
        _sliders.Clear();

        // Climate Controls
        _sliders.Add(new Slider("Solar Energy", 0.5f, 1.5f, _map.SolarEnergy, "x",
            v => _map.SolarEnergy = v));

        _sliders.Add(new Slider("Temperature Offset", -20f, 20f, 0f, "°C",
            v => AdjustGlobalTemperature(v)));

        _sliders.Add(new Slider("Rainfall Multiplier", 0.1f, 3.0f, 1.0f, "x",
            v => AdjustGlobalRainfall(v)));

        _sliders.Add(new Slider("Wind Strength", 0.1f, 3.0f, 1.0f, "x",
            v => AdjustWindStrength(v)));

        // Atmospheric Controls
        _sliders.Add(new Slider("Oxygen Level", 0f, 40f, GetAverageOxygen(), "%",
            v => SetGlobalOxygen(v)));

        _sliders.Add(new Slider("CO2 Level", 0f, 10f, GetAverageCO2(), "%",
            v => SetGlobalCO2(v)));

        _sliders.Add(new Slider("Atmospheric Pressure", 0.5f, 2.0f, 1.0f, "atm",
            v => SetAtmosphericPressure(v)));

        // Geological Controls
        _sliders.Add(new Slider("Tectonic Activity", 0f, 2.0f, 1.0f, "x",
            v => SetTectonicActivity(v)));

        _sliders.Add(new Slider("Volcanic Activity", 0f, 2.0f, 1.0f, "x",
            v => SetVolcanicActivity(v)));

        _sliders.Add(new Slider("Erosion Rate", 0f, 2.0f, 1.0f, "x",
            v => SetErosionRate(v)));

        // Surface Controls
        _sliders.Add(new Slider("Albedo (Reflectivity)", 0.1f, 0.9f, 0.3f, "",
            v => SetGlobalAlbedo(v)));

        _sliders.Add(new Slider("Ice Coverage", 0f, 1.0f, GetIceCoverage(), "%",
            v => SetIceCoverage(v)));

        _sliders.Add(new Slider("Ocean Level", -0.5f, 0.5f, 0f, "m",
            v => AdjustOceanLevel(v)));

        // Magnetic Field
        _sliders.Add(new Slider("Magnetic Field", 0f, 2.0f, _magnetosphere.MagneticFieldStrength, "x",
            v => _magnetosphere.MagneticFieldStrength = v));

        _sliders.Add(new Slider("Core Temperature", 1000f, 7000f, _magnetosphere.CoreTemperature, "K",
            v => _magnetosphere.CoreTemperature = v));
    }

    public void Update(MouseState mouseState)
    {
        if (!IsVisible) return;

        bool clicked = mouseState.LeftButton == ButtonState.Pressed &&
                      _previousMouseState.LeftButton == ButtonState.Released;
        bool released = mouseState.LeftButton == ButtonState.Released &&
                       _previousMouseState.LeftButton == ButtonState.Pressed;

        // Update slider positions
        PositionSliders();

        // Handle slider interaction
        foreach (var slider in _sliders)
        {
            if (slider.IsDragging)
            {
                if (mouseState.LeftButton == ButtonState.Pressed)
                {
                    // Update slider value based on mouse X position
                    float normalizedX = (float)(mouseState.X - slider.Bounds.X) / slider.Bounds.Width;
                    normalizedX = Math.Clamp(normalizedX, 0f, 1f);
                    slider.SetNormalizedValue(normalizedX);
                    slider.OnValueChanged?.Invoke(slider.Value);
                }
                else
                {
                    slider.IsDragging = false;
                }
            }
            else if (clicked && slider.Bounds.Contains(mouseState.Position))
            {
                slider.IsDragging = true;
            }
        }

        // Handle button clicks
        foreach (var button in _buttons)
        {
            if (clicked && button.Bounds.Contains(mouseState.Position))
            {
                button.OnClick?.Invoke();
            }
        }

        _previousMouseState = mouseState;
    }

    private void PositionSliders()
    {
        int screenWidth = _graphicsDevice.Viewport.Width;
        int screenHeight = _graphicsDevice.Viewport.Height;

        int panelWidth = 1000;  // Increased to fit all controls comfortably
        int panelHeight = screenHeight - 100;
        int panelX = (screenWidth - panelWidth) / 2;
        int panelY = 50;

        int sliderWidth = 180;  // Reduced from 250
        int sliderHeight = 20;
        int labelWidth = 180;   // Reduced from 200
        int spacing = 35;
        int columnSpacing = 280; // Reduced from 320 to fit within panel

        int leftColumnX = panelX + 20;
        int middleColumnX = leftColumnX + columnSpacing;
        int rightColumnX = middleColumnX + columnSpacing;

        int currentY = panelY + 80;

        // Position sliders in 3 columns
        for (int i = 0; i < _sliders.Count; i++)
        {
            int column = i / 5; // 5 sliders per column
            int row = i % 5;

            int x = leftColumnX + (column * columnSpacing);
            int y = currentY + (row * spacing);

            _sliders[i].Bounds = new Rectangle(x + labelWidth, y, sliderWidth, sliderHeight);
        }

        // Position buttons at bottom
        _buttons.Clear();
        int buttonY = panelY + panelHeight - 50;
        int buttonWidth = 180;
        int buttonHeight = 35;

        _buttons.Add(new UIButton(
            new Rectangle(panelX + 20, buttonY, buttonWidth, buttonHeight),
            "Restore Stable",
            () => RestorePlanet()));

        _buttons.Add(new UIButton(
            new Rectangle(panelX + 220, buttonY, buttonWidth, buttonHeight),
            "Destabilize",
            () => DestabilizePlanet()));

        _buttons.Add(new UIButton(
            new Rectangle(panelX + 420, buttonY, buttonWidth, buttonHeight),
            _stabilizer.IsActive ? "Stabilizer: ON" : "Stabilizer: OFF",
            () => _stabilizer.IsActive = !_stabilizer.IsActive));

        _buttons.Add(new UIButton(
            new Rectangle(panelX + panelWidth - 200, buttonY, buttonWidth, buttonHeight),
            "Close (X)",
            () => IsVisible = false));
    }

    public void Draw(SpriteBatch spriteBatch)
    {
        if (!IsVisible) return;

        int screenWidth = _graphicsDevice.Viewport.Width;
        int screenHeight = _graphicsDevice.Viewport.Height;

        int panelWidth = 1000;  // Increased to fit all controls comfortably
        int panelHeight = screenHeight - 100;
        int panelX = (screenWidth - panelWidth) / 2;
        int panelY = 50;

        // Semi-transparent background
        spriteBatch.Draw(_pixelTexture, new Rectangle(0, 0, screenWidth, screenHeight),
            new Color(0, 0, 0, 180));

        // Panel background
        spriteBatch.Draw(_pixelTexture, new Rectangle(panelX, panelY, panelWidth, panelHeight),
            new Color(20, 30, 50, 240));

        // Panel border
        DrawBorder(spriteBatch, panelX, panelY, panelWidth, panelHeight, new Color(100, 150, 255), 3);

        // Title
        _font.DrawString(spriteBatch, "PLANETARY CONTROLS",
            new Vector2(panelX + panelWidth / 2 - 150, panelY + 15),
            new Color(255, 200, 50), 1.5f);

        // Subtitle
        _font.DrawString(spriteBatch, "SimEarth-Style Global Parameter Adjustment",
            new Vector2(panelX + panelWidth / 2 - 180, panelY + 45),
            new Color(150, 200, 255), 0.9f);

        // Section headers
        int headerY = panelY + 80;
        _font.DrawString(spriteBatch, "=== CLIMATE ===",
            new Vector2(panelX + 20, headerY - 25), Color.Orange, 1.0f);
        _font.DrawString(spriteBatch, "=== ATMOSPHERE ===",
            new Vector2(panelX + 340, headerY - 25), Color.Cyan, 1.0f);
        _font.DrawString(spriteBatch, "=== GEOLOGY ===",
            new Vector2(panelX + 660, headerY - 25), Color.Red, 1.0f);

        // Draw sliders
        int columnSpacing = 320;
        int spacing = 35;

        for (int i = 0; i < _sliders.Count; i++)
        {
            int column = i / 5;
            int row = i % 5;

            int x = panelX + 20 + (column * columnSpacing);
            int y = headerY + (row * spacing);

            DrawSlider(spriteBatch, _sliders[i], x, y);
        }

        // Draw buttons
        foreach (var button in _buttons)
        {
            Color bgColor = button.Bounds.Contains(Mouse.GetState().Position) ?
                new Color(70, 110, 180) : new Color(40, 60, 100);

            spriteBatch.Draw(_pixelTexture, button.Bounds, bgColor);
            DrawBorder(spriteBatch, button.Bounds.X, button.Bounds.Y,
                button.Bounds.Width, button.Bounds.Height, Color.White, 2);

            var textSize = _font.MeasureString(button.Label, 14);
            _font.DrawString(spriteBatch, button.Label,
                new Vector2(button.Bounds.X + (button.Bounds.Width - textSize.X) / 2,
                           button.Bounds.Y + (button.Bounds.Height - textSize.Y) / 2),
                Color.White, 14);
        }

        // Stabilizer status
        string stabilizerStatus = $"AI Stabilizer: {(_stabilizer.IsActive ? "ACTIVE" : "INACTIVE")} | " +
                                 $"Last Action: {_stabilizer.LastAction} | Adjustments: {_stabilizer.AdjustmentsMade}";
        _font.DrawString(spriteBatch, stabilizerStatus,
            new Vector2(panelX + 20, panelY + panelHeight - 85),
            _stabilizer.IsActive ? Color.LightGreen : Color.Gray, 0.8f);
    }

    private void DrawSlider(SpriteBatch spriteBatch, Slider slider, int x, int y)
    {
        // Label
        _font.DrawString(spriteBatch, slider.Label,
            new Vector2(x, y + 3), Color.White, 12);

        // Slider track
        spriteBatch.Draw(_pixelTexture, slider.Bounds, new Color(60, 60, 80));
        DrawBorder(spriteBatch, slider.Bounds.X, slider.Bounds.Y,
            slider.Bounds.Width, slider.Bounds.Height, Color.Gray, 1);

        // Slider fill
        float normalizedValue = slider.GetNormalizedValue();
        int fillWidth = (int)(slider.Bounds.Width * normalizedValue);
        if (fillWidth > 0)
        {
            spriteBatch.Draw(_pixelTexture,
                new Rectangle(slider.Bounds.X, slider.Bounds.Y, fillWidth, slider.Bounds.Height),
                new Color(100, 150, 255, 180));
        }

        // Slider handle
        int handleX = slider.Bounds.X + (int)(slider.Bounds.Width * normalizedValue) - 5;
        spriteBatch.Draw(_pixelTexture,
            new Rectangle(handleX, slider.Bounds.Y - 2, 10, slider.Bounds.Height + 4),
            slider.IsDragging ? Color.Yellow : Color.White);

        // Value display
        string valueText = $"{slider.Value:F2}{slider.Unit}";
        _font.DrawString(spriteBatch, valueText,
            new Vector2(slider.Bounds.X + slider.Bounds.Width + 10, y + 3),
            Color.LightGreen, 12);
    }

    private void DrawBorder(SpriteBatch spriteBatch, int x, int y, int width, int height, Color color, int thickness)
    {
        spriteBatch.Draw(_pixelTexture, new Rectangle(x, y, width, thickness), color);
        spriteBatch.Draw(_pixelTexture, new Rectangle(x, y + height - thickness, width, thickness), color);
        spriteBatch.Draw(_pixelTexture, new Rectangle(x, y, thickness, height), color);
        spriteBatch.Draw(_pixelTexture, new Rectangle(x + width - thickness, y, thickness, height), color);
    }

    // Control implementation methods
    private void AdjustGlobalTemperature(float offset)
    {
        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                _map.Cells[x, y].Temperature += offset * 0.01f;
            }
        }
    }

    private void AdjustGlobalRainfall(float multiplier)
    {
        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                _map.Cells[x, y].Rainfall = Math.Clamp(_map.Cells[x, y].Rainfall * multiplier, 0f, 2f);
            }
        }
    }

    private void AdjustWindStrength(float multiplier)
    {
        // Store this in PlanetMap for the weather simulator to use
        // This will be used by WeatherSimulator
    }

    private void SetGlobalOxygen(float targetLevel)
    {
        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                _map.Cells[x, y].Oxygen = targetLevel;
            }
        }
    }

    private void SetGlobalCO2(float targetLevel)
    {
        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                _map.Cells[x, y].CO2 = targetLevel;
                _map.Cells[x, y].Greenhouse = targetLevel * 10f;
            }
        }
    }

    private void SetAtmosphericPressure(float pressure)
    {
        // This affects weather and climate
    }

    private void SetTectonicActivity(float level)
    {
        if (_geologicalSimulator != null)
        {
            _geologicalSimulator.TectonicActivityLevel = level;
        }
    }

    private void SetVolcanicActivity(float level)
    {
        if (_geologicalSimulator != null)
        {
            _geologicalSimulator.VolcanicActivityLevel = level;
        }
    }

    private void SetErosionRate(float level)
    {
        if (_geologicalSimulator != null)
        {
            _geologicalSimulator.ErosionRate = level;
        }
    }

    private void SetGlobalAlbedo(float albedo)
    {
        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                _map.Cells[x, y].Albedo = albedo;
            }
        }
    }

    private void SetIceCoverage(float coverage)
    {
        int targetIceCells = (int)(_map.Width * _map.Height * coverage);
        int currentIceCells = 0;

        // Count current ice
        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                if (_map.Cells[x, y].IsIce) currentIceCells++;
            }
        }

        // Adjust ice coverage
        if (currentIceCells < targetIceCells)
        {
            // Add ice to coldest areas
            for (int x = 0; x < _map.Width; x++)
            {
                for (int y = 0; y < _map.Height; y++)
                {
                    if (!_map.Cells[x, y].IsIce && _map.Cells[x, y].Temperature < 10f)
                    {
                        _map.Cells[x, y].IsIce = true;
                        _map.Cells[x, y].Temperature = -10f;
                        currentIceCells++;
                        if (currentIceCells >= targetIceCells) break;
                    }
                }
                if (currentIceCells >= targetIceCells) break;
            }
        }
        else if (currentIceCells > targetIceCells)
        {
            // Remove ice from warmest areas
            for (int x = 0; x < _map.Width; x++)
            {
                for (int y = 0; y < _map.Height; y++)
                {
                    if (_map.Cells[x, y].IsIce && _map.Cells[x, y].Temperature > -20f)
                    {
                        _map.Cells[x, y].IsIce = false;
                        _map.Cells[x, y].Temperature = 5f;
                        currentIceCells--;
                        if (currentIceCells <= targetIceCells) break;
                    }
                }
                if (currentIceCells <= targetIceCells) break;
            }
        }
    }

    private void AdjustOceanLevel(float adjustment)
    {
        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                _map.Cells[x, y].Elevation += adjustment * 0.01f;
            }
        }
    }

    private void RestorePlanet()
    {
        // Restore to Earth-like stable conditions
        _map.SolarEnergy = 1.0f;
        _magnetosphere.MagneticFieldStrength = 1.0f;
        _magnetosphere.CoreTemperature = 5000f;
        _magnetosphere.HasDynamo = true;

        SetGlobalOxygen(21f);
        SetGlobalCO2(0.04f);

        if (_geologicalSimulator != null)
        {
            _geologicalSimulator.TectonicActivityLevel = 1.0f;
            _geologicalSimulator.VolcanicActivityLevel = 1.0f;
            _geologicalSimulator.ErosionRate = 1.0f;
        }

        // Moderate temperature
        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                var cell = _map.Cells[x, y];
                if (cell.IsLand)
                {
                    cell.Temperature = 15f + (Random.Shared.NextSingle() - 0.5f) * 30f;
                    cell.Rainfall = 0.4f + (Random.Shared.NextSingle() - 0.5f) * 0.4f;
                }
            }
        }

        _stabilizer.IsActive = true;
        _stabilizer.Reset();

        InitializeControls(); // Refresh slider values
    }

    private void DestabilizePlanet()
    {
        // Random chaos mode - randomize all parameters
        _map.SolarEnergy = 0.5f + Random.Shared.NextSingle() * 1.0f;
        _magnetosphere.MagneticFieldStrength = Random.Shared.NextSingle() * 2.0f;
        _magnetosphere.CoreTemperature = 1000f + Random.Shared.NextSingle() * 6000f;

        SetGlobalOxygen(Random.Shared.NextSingle() * 40f);
        SetGlobalCO2(Random.Shared.NextSingle() * 10f);

        if (_geologicalSimulator != null)
        {
            _geologicalSimulator.TectonicActivityLevel = Random.Shared.NextSingle() * 2.0f;
            _geologicalSimulator.VolcanicActivityLevel = Random.Shared.NextSingle() * 2.0f;
            _geologicalSimulator.ErosionRate = Random.Shared.NextSingle() * 2.0f;
        }

        _stabilizer.IsActive = false;

        InitializeControls(); // Refresh slider values
    }

    private float GetAverageOxygen()
    {
        float total = 0;
        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                total += _map.Cells[x, y].Oxygen;
            }
        }
        return total / (_map.Width * _map.Height);
    }

    private float GetAverageCO2()
    {
        float total = 0;
        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                total += _map.Cells[x, y].CO2;
            }
        }
        return total / (_map.Width * _map.Height);
    }

    private float GetIceCoverage()
    {
        int iceCells = 0;
        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                if (_map.Cells[x, y].IsIce) iceCells++;
            }
        }
        return (float)iceCells / (_map.Width * _map.Height);
    }
}

public class UIButton
{
    public Rectangle Bounds { get; set; }
    public string Label { get; set; }
    public Action OnClick { get; set; }

    public UIButton(Rectangle bounds, string label, Action onClick)
    {
        Bounds = bounds;
        Label = label;
        OnClick = onClick;
    }
}
