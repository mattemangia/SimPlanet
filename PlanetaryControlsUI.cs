using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;

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

    private const float OceanLevelElevationScale = 0.2f;
    private const int SlidersPerColumn = 5;

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
        public Func<float>? ValueGetter { get; set; }
        public Vector2 LabelPosition { get; set; }
        public bool HasLabelPosition { get; set; }

        public Slider(string label, float min, float max, float initial, string unit, Action<float> onChanged, Func<float>? valueGetter = null)
        {
            Label = label;
            Min = min;
            Max = max;
            Value = initial;
            Unit = unit;
            OnValueChanged = onChanged;
            ValueGetter = valueGetter;
        }

        public float GetNormalizedValue() => (Value - Min) / (Max - Min);
        public void SetNormalizedValue(float normalized) => Value = Math.Clamp(Min + normalized * (Max - Min), Min, Max);

        // Update slider value from external source without triggering OnValueChanged
        public void UpdateFromValue(float value)
        {
            Value = Math.Clamp(value, Min, Max);
        }
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
        ApplyStoredGeologyControls();
    }

    private void ApplyStoredGeologyControls()
    {
        if (_geologicalSimulator == null) return;

        _geologicalSimulator.TectonicActivityLevel = _map.PlanetaryControls.TectonicActivityMultiplier;
        _geologicalSimulator.VolcanicActivityLevel = _map.PlanetaryControls.VolcanicActivityMultiplier;
        _geologicalSimulator.ErosionRate = _map.PlanetaryControls.ErosionRateMultiplier;
    }

    private void InitializeControls()
    {
        _sliders.Clear();

        // Climate & energy
        _sliders.Add(CreateSlider("Solar Energy", 0.5f, 1.5f, "x",
            () => _map.SolarEnergy,
            SetSolarEnergy));

        _sliders.Add(CreateSlider("Temperature Offset", -20f, 20f, "°C",
            () => _map.PlanetaryControls.TemperatureOffsetCelsius,
            SetTemperatureOffset));

        _sliders.Add(CreateSlider("Rainfall Multiplier", 0.1f, 3.0f, "x",
            () => _map.PlanetaryControls.RainfallMultiplier,
            SetRainfallMultiplier));

        _sliders.Add(CreateSlider("Wind Strength", 0.1f, 3.0f, "x",
            () => _map.PlanetaryControls.WindStrengthMultiplier,
            SetWindStrength));

        // Atmospheric Controls
        _sliders.Add(CreateSlider("Oxygen Level", 0f, 40f, "%",
            GetAverageOxygen,
            SetGlobalOxygen));

        _sliders.Add(CreateSlider("CO2 Level", 0f, 10f, "%",
            GetAverageCO2,
            SetGlobalCO2));

        _sliders.Add(CreateSlider("Atmospheric Pressure", 0.5f, 2.0f, "atm",
            () => _map.PlanetaryControls.AtmosphericPressureMultiplier,
            SetAtmosphericPressure));

        // Geological Controls
        _sliders.Add(CreateSlider("Tectonic Activity", 0f, 2.0f, "x",
            () => _geologicalSimulator?.TectonicActivityLevel ?? _map.PlanetaryControls.TectonicActivityMultiplier,
            SetTectonicActivity));

        _sliders.Add(CreateSlider("Volcanic Activity", 0f, 2.0f, "x",
            () => _geologicalSimulator?.VolcanicActivityLevel ?? _map.PlanetaryControls.VolcanicActivityMultiplier,
            SetVolcanicActivity));

        _sliders.Add(CreateSlider("Erosion Rate", 0f, 2.0f, "x",
            () => _geologicalSimulator?.ErosionRate ?? _map.PlanetaryControls.ErosionRateMultiplier,
            SetErosionRate));

        // Surface Controls
        _sliders.Add(CreateSlider("Albedo (Reflectivity)", 0.1f, 0.9f, string.Empty,
            () => _map.PlanetaryControls.SurfaceAlbedo,
            SetGlobalAlbedo));

        _sliders.Add(CreateSlider("Ice Coverage", 0f, 1.0f, "%",
            GetIceCoverage,
            SetIceCoverage));

        _sliders.Add(CreateSlider("Ocean Level", -0.5f, 0.5f, "m",
            () => _map.PlanetaryControls.OceanLevelOffset,
            AdjustOceanLevel));

        // Magnetic Field
        _sliders.Add(CreateSlider("Magnetic Field", 0f, 2.0f, "x",
            () => _map.PlanetaryControls.MagneticFieldStrength,
            SetMagneticFieldStrength));

        _sliders.Add(CreateSlider("Core Temperature", 1000f, 7000f, "K",
            () => _map.PlanetaryControls.CoreTemperatureKelvin,
            SetCoreTemperature));
    }

    private Slider CreateSlider(string label, float min, float max, string unit, Func<float> getter, Action<float> setter)
    {
        float initial = Math.Clamp(getter(), min, max);
        return new Slider(label, min, max, initial, unit, setter, getter);
    }

    private Rectangle GetPanelBounds()
    {
        int screenWidth = _graphicsDevice.Viewport.Width;
        int screenHeight = _graphicsDevice.Viewport.Height;
        int panelWidth = 1100;
        int panelHeight = Math.Max(300, screenHeight - 100);
        int panelX = (screenWidth - panelWidth) / 2;
        int panelY = 50;
        return new Rectangle(panelX, panelY, panelWidth, panelHeight);
    }

    public void Update(MouseState mouseState)
    {
        if (!IsVisible) return;

        bool clicked = mouseState.LeftButton == ButtonState.Pressed &&
                      _previousMouseState.LeftButton == ButtonState.Released;
        bool released = mouseState.LeftButton == ButtonState.Released &&
                       _previousMouseState.LeftButton == ButtonState.Pressed;

        // Update slider positions and sync values with the current simulation state
        PositionSliders();
        UpdateSlidersFromMapState();

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

    private void UpdateSlidersFromMapState()
    {
        foreach (var slider in _sliders)
        {
            if (slider.IsDragging || slider.ValueGetter == null) continue;
            slider.UpdateFromValue(slider.ValueGetter());
        }
    }

    private float CalculateAverageOxygen()
    {
        float total = 0f;
        int count = 0;
        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                total += _map.Cells[x, y].Oxygen;
                count++;
            }
        }
        return count > 0 ? total / count : 0f;
    }

    private float CalculateAverageCO2()
    {
        float total = 0f;
        int count = 0;
        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                total += _map.Cells[x, y].CO2;
                count++;
            }
        }
        return count > 0 ? total / count : 0f;
    }

    private void PositionSliders()
    {
        var panelBounds = GetPanelBounds();
        int sliderWidth = 240;
        int sliderHeight = 22;
        int rowSpacing = 70;
        int labelSpacing = 18;
        int columnSpacing = 340;

        int startX = panelBounds.X + 30;
        int startY = panelBounds.Y + 110;

        for (int i = 0; i < _sliders.Count; i++)
        {
            int column = i / SlidersPerColumn;
            int row = i % SlidersPerColumn;

            int x = startX + (column * columnSpacing);
            int y = startY + (row * rowSpacing);

            var slider = _sliders[i];
            slider.Bounds = new Rectangle(x, y + labelSpacing, sliderWidth, sliderHeight);
            slider.LabelPosition = new Vector2(x, y);
            slider.HasLabelPosition = true;
        }

        // Position buttons at bottom
        _buttons.Clear();
        int buttonY = panelBounds.Bottom - 50;
        int buttonWidth = 190;
        int buttonHeight = 35;

        _buttons.Add(new UIButton(
            new Rectangle(panelBounds.X + 30, buttonY, buttonWidth, buttonHeight),
            "Restore Stable",
            () => RestorePlanet()));

        _buttons.Add(new UIButton(
            new Rectangle(panelBounds.X + 250, buttonY, buttonWidth, buttonHeight),
            "Destabilize",
            () => DestabilizePlanet()));

        _buttons.Add(new UIButton(
            new Rectangle(panelBounds.X + 470, buttonY, buttonWidth, buttonHeight),
            _stabilizer.IsActive ? "Stabilizer: ON" : "Stabilizer: OFF",
            () => _stabilizer.IsActive = !_stabilizer.IsActive));

        _buttons.Add(new UIButton(
            new Rectangle(panelBounds.Right - 210, buttonY, buttonWidth, buttonHeight),
            "Close (X)",
            () => IsVisible = false));
    }

    public void Draw(SpriteBatch spriteBatch)
    {
        if (!IsVisible) return;

        int screenWidth = _graphicsDevice.Viewport.Width;
        int screenHeight = _graphicsDevice.Viewport.Height;
        var panelBounds = GetPanelBounds();

        // Semi-transparent background
        spriteBatch.Draw(_pixelTexture, new Rectangle(0, 0, screenWidth, screenHeight),
            new Color(0, 0, 0, 180));

        // Panel background
        spriteBatch.Draw(_pixelTexture, panelBounds,
            new Color(20, 30, 50, 240));

        // Panel border
        DrawBorder(spriteBatch, panelBounds.X, panelBounds.Y, panelBounds.Width, panelBounds.Height, new Color(100, 150, 255), 3);

        // Title
        _font.DrawString(spriteBatch, "PLANETARY CONTROLS",
            new Vector2(panelBounds.X + panelBounds.Width / 2 - 150, panelBounds.Y + 15),
            new Color(255, 200, 50), 1.5f);

        // Subtitle
        _font.DrawString(spriteBatch, "SimEarth-Style Global Parameter Adjustment",
            new Vector2(panelBounds.X + panelBounds.Width / 2 - 180, panelBounds.Y + 45),
            new Color(150, 200, 255), 0.9f);

        // Section headers
        int headerY = panelBounds.Y + 90;
        int headerSpacing = 340;
        _font.DrawString(spriteBatch, "=== CLIMATE ===",
            new Vector2(panelBounds.X + 30, headerY - 25), Color.Orange, 1.0f);
        _font.DrawString(spriteBatch, "=== ATMOSPHERE / GEOLOGY ===",
            new Vector2(panelBounds.X + 30 + headerSpacing, headerY - 25), Color.Cyan, 1.0f);
        _font.DrawString(spriteBatch, "=== SURFACE & MAGNETOSPHERE ===",
            new Vector2(panelBounds.X + 30 + (headerSpacing * 2), headerY - 25), Color.Red, 1.0f);

        // Draw sliders
        foreach (var slider in _sliders)
        {
            DrawSlider(spriteBatch, slider);
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
            new Vector2(panelBounds.X + 20, panelBounds.Bottom - 85),
            _stabilizer.IsActive ? Color.LightGreen : Color.Gray, 0.8f);
    }

    private void DrawSlider(SpriteBatch spriteBatch, Slider slider)
    {
        Vector2 labelPosition = slider.HasLabelPosition
            ? slider.LabelPosition
            : new Vector2(slider.Bounds.X, slider.Bounds.Y - 20);

        _font.DrawString(spriteBatch, slider.Label,
            labelPosition, Color.White, 12);

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
        string valueText = FormatSliderValue(slider);
        Vector2 valuePos = new Vector2(slider.Bounds.X + slider.Bounds.Width + 12, slider.Bounds.Y - 2);
        var valueSize = _font.MeasureString(valueText, 12);
        var valueRect = new Rectangle((int)valuePos.X - 4, (int)valuePos.Y - 2,
            (int)valueSize.X + 8, slider.Bounds.Height + 4);
        spriteBatch.Draw(_pixelTexture, valueRect, new Color(10, 20, 35, 220));
        _font.DrawString(spriteBatch, valueText, valuePos, Color.LightGreen, 12);
    }

    private static string FormatSliderValue(Slider slider)
    {
        string format = slider.Max >= 1000f ? "F0" : "F2";
        return $"{slider.Value.ToString(format)}{slider.Unit}";
    }

    private void DrawBorder(SpriteBatch spriteBatch, int x, int y, int width, int height, Color color, int thickness)
    {
        spriteBatch.Draw(_pixelTexture, new Rectangle(x, y, width, thickness), color);
        spriteBatch.Draw(_pixelTexture, new Rectangle(x, y + height - thickness, width, thickness), color);
        spriteBatch.Draw(_pixelTexture, new Rectangle(x, y, thickness, height), color);
        spriteBatch.Draw(_pixelTexture, new Rectangle(x + width - thickness, y, thickness, height), color);
    }

    // Control implementation methods
    private void SetSolarEnergy(float multiplier)
    {
        _map.SolarEnergy = multiplier;
        _map.PlanetaryControls.SolarEnergyMultiplier = multiplier;
    }

    private void SetTemperatureOffset(float offset)
    {
        _map.PlanetaryControls.TemperatureOffsetCelsius = offset;
    }

    private void SetRainfallMultiplier(float multiplier)
    {
        _map.PlanetaryControls.RainfallMultiplier = multiplier;
    }

    private void SetWindStrength(float multiplier)
    {
        _map.PlanetaryControls.WindStrengthMultiplier = multiplier;
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
        _map.GlobalOxygen = targetLevel;
        _map.PlanetaryControls.GlobalOxygenPercent = targetLevel;
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
        _map.GlobalCO2 = targetLevel;
        _map.PlanetaryControls.GlobalCO2Percent = targetLevel;
    }

    private void SetAtmosphericPressure(float pressure)
    {
        _map.PlanetaryControls.AtmosphericPressureMultiplier = pressure;
    }

    private void SetTectonicActivity(float level)
    {
        if (_geologicalSimulator != null)
        {
            _geologicalSimulator.TectonicActivityLevel = level;
        }
        _map.PlanetaryControls.TectonicActivityMultiplier = level;
    }

    private void SetVolcanicActivity(float level)
    {
        if (_geologicalSimulator != null)
        {
            _geologicalSimulator.VolcanicActivityLevel = level;
        }
        _map.PlanetaryControls.VolcanicActivityMultiplier = level;
    }

    private void SetErosionRate(float level)
    {
        if (_geologicalSimulator != null)
        {
            _geologicalSimulator.ErosionRate = level;
        }
        _map.PlanetaryControls.ErosionRateMultiplier = level;
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
        _map.PlanetaryControls.SurfaceAlbedo = albedo;
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
        _map.PlanetaryControls.TargetIceCoverage = coverage;
    }

    private void AdjustOceanLevel(float adjustment)
    {
        float previous = _map.PlanetaryControls.OceanLevelOffset;
        float delta = adjustment - previous;
        if (Math.Abs(delta) < 0.0001f) return;

        _map.PlanetaryControls.OceanLevelOffset = adjustment;
        float elevationDelta = delta * OceanLevelElevationScale;

        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                _map.Cells[x, y].Elevation += elevationDelta;
            }
        }
    }

    private void SetMagneticFieldStrength(float strength)
    {
        _map.PlanetaryControls.MagneticFieldStrength = strength;
        _map.PlanetaryControls.ManualMagneticField = true;
        _magnetosphere.MagneticFieldStrength = strength;
        _magnetosphere.HasDynamo = strength > 0.05f;
    }

    private void SetCoreTemperature(float temperature)
    {
        _map.PlanetaryControls.CoreTemperatureKelvin = temperature;
        _map.PlanetaryControls.ManualCoreTemperature = true;
        _magnetosphere.CoreTemperature = temperature;
        if (temperature < 3000f)
        {
            _magnetosphere.HasDynamo = false;
        }
        else if (!_map.PlanetaryControls.ManualMagneticField)
        {
            _magnetosphere.HasDynamo = true;
        }
    }

    private void ReleaseMagnetosphereOverrides()
    {
        _map.PlanetaryControls.ManualMagneticField = false;
        _map.PlanetaryControls.ManualCoreTemperature = false;
        _map.PlanetaryControls.MagneticFieldStrength = _magnetosphere.MagneticFieldStrength;
        _map.PlanetaryControls.CoreTemperatureKelvin = _magnetosphere.CoreTemperature;
    }

    private void RestorePlanet()
    {
        // Restore to Earth-like stable conditions
        SetSolarEnergy(1.0f);
        _magnetosphere.MagneticFieldStrength = 1.0f;
        _magnetosphere.CoreTemperature = 5000f;
        _magnetosphere.HasDynamo = true;
        ReleaseMagnetosphereOverrides();

        _map.PlanetaryControls.TemperatureOffsetCelsius = 0f;
        _map.PlanetaryControls.RainfallMultiplier = 1f;
        _map.PlanetaryControls.WindStrengthMultiplier = 1f;
        _map.PlanetaryControls.AtmosphericPressureMultiplier = 1f;

        SetGlobalOxygen(21f);
        SetGlobalCO2(0.04f);
        SetGlobalAlbedo(0.3f);
        AdjustOceanLevel(0f);

        SetTectonicActivity(1.0f);
        SetVolcanicActivity(1.0f);
        SetErosionRate(1.0f);

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
        SetSolarEnergy(0.5f + Random.Shared.NextSingle() * 1.0f);
        _magnetosphere.MagneticFieldStrength = Random.Shared.NextSingle() * 2.0f;
        _magnetosphere.CoreTemperature = 1000f + Random.Shared.NextSingle() * 6000f;
        _magnetosphere.HasDynamo = _magnetosphere.CoreTemperature >= 3000f && _magnetosphere.MagneticFieldStrength > 0.05f;
        ReleaseMagnetosphereOverrides();

        SetGlobalOxygen(Random.Shared.NextSingle() * 40f);
        SetGlobalCO2(Random.Shared.NextSingle() * 10f);
        SetGlobalAlbedo(0.1f + Random.Shared.NextSingle() * 0.8f);
        SetIceCoverage(Random.Shared.NextSingle());
        AdjustOceanLevel(Random.Shared.NextSingle() - 0.5f);

        _map.PlanetaryControls.TemperatureOffsetCelsius = -20f + Random.Shared.NextSingle() * 40f;
        _map.PlanetaryControls.RainfallMultiplier = 0.1f + Random.Shared.NextSingle() * 2.9f;
        _map.PlanetaryControls.WindStrengthMultiplier = 0.1f + Random.Shared.NextSingle() * 2.9f;
        _map.PlanetaryControls.AtmosphericPressureMultiplier = 0.5f + Random.Shared.NextSingle() * 1.5f;

        SetTectonicActivity(Random.Shared.NextSingle() * 2.0f);
        SetVolcanicActivity(Random.Shared.NextSingle() * 2.0f);
        SetErosionRate(Random.Shared.NextSingle() * 2.0f);

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
