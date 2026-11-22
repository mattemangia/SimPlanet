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

        _geologicalSimulator = null!; // Will be set later via SetGeologicalSimulator
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

    // ---- Layout (shared by Update and Draw) ---------------------------------------------

    private const int ColumnWidth = 320;
    private const int ColumnGap = 26;
    private const int PanelPadding = 22;
    private const int SliderRowHeight = 46;
    private const int SectionHeaderHeight = 24;
    private const int SectionGap = 12;

    // Which sliders (by index in _sliders) belong to which titled group, and in which column.
    private static readonly (string Title, int Column, int First, int Count)[] SliderSections =
    {
        ("CLIMATE", 0, 0, 4),
        ("MAGNETOSPHERE", 0, 13, 2),
        ("ATMOSPHERE", 1, 4, 3),
        ("GEOLOGY", 1, 7, 3),
        ("SURFACE", 2, 10, 3),
    };

    private readonly List<(string Title, Rectangle Area)> _sectionHeaders = new();
    private Rectangle _stabilizerCard;
    private Rectangle _footerDivider;

    private static int ColumnHeight => SectionHeaderHeight * 2 + SliderRowHeight * 6 + SectionGap;

    private Rectangle GetPanelBounds()
    {
        int screenWidth = _graphicsDevice.Viewport.Width;
        int screenHeight = _graphicsDevice.Viewport.Height;
        int panelWidth = PanelPadding * 2 + ColumnWidth * 3 + ColumnGap * 2;
        int panelHeight = UIWidgets.HeaderHeight + 16 + ColumnHeight + 16 + 15 + 32 + 16;
        int panelX = (screenWidth - panelWidth) / 2;
        int panelY = Math.Max(50, (screenHeight - panelHeight) / 2 - 10);
        return new Rectangle(panelX, panelY, panelWidth, panelHeight);
    }

    public void Update(MouseState mouseState)
    {
        if (!IsVisible) return;

        bool clicked = mouseState.LeftButton == ButtonState.Pressed &&
                      _previousMouseState.LeftButton == ButtonState.Released;

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

    private void PositionSliders()
    {
        var panelBounds = GetPanelBounds();
        int contentTop = panelBounds.Y + UIWidgets.HeaderHeight + 16;
        var columnY = new int[] { contentTop, contentTop, contentTop };

        _sectionHeaders.Clear();
        foreach (var section in SliderSections)
        {
            int x = panelBounds.X + PanelPadding + section.Column * (ColumnWidth + ColumnGap);
            int y = columnY[section.Column];
            _sectionHeaders.Add((section.Title, new Rectangle(x, y, ColumnWidth, SectionHeaderHeight)));
            y += SectionHeaderHeight;

            for (int i = section.First; i < section.First + section.Count && i < _sliders.Count; i++)
            {
                var slider = _sliders[i];
                // The label and value sit on the first line; the track (and its hit area) below
                slider.Bounds = new Rectangle(x, y + 20, ColumnWidth, 18);
                slider.LabelPosition = new Vector2(x, y);
                slider.HasLabelPosition = true;
                y += SliderRowHeight;
            }
            columnY[section.Column] = y + SectionGap;
        }

        // Stabilizer card fills the rest of the third column
        int cardX = panelBounds.X + PanelPadding + 2 * (ColumnWidth + ColumnGap);
        int cardY = columnY[2];
        _stabilizerCard = new Rectangle(cardX, cardY, ColumnWidth, contentTop + ColumnHeight - cardY);

        // Footer buttons
        _buttons.Clear();
        int footerY = contentTop + ColumnHeight + 16;
        _footerDivider = new Rectangle(panelBounds.X + PanelPadding, footerY, panelBounds.Width - PanelPadding * 2, 1);
        int buttonY = footerY + 15;
        int buttonWidth = 170;
        int buttonHeight = 32;

        _buttons.Add(new UIButton(
            new Rectangle(panelBounds.X + PanelPadding, buttonY, buttonWidth, buttonHeight),
            "Restore Stable",
            () => RestorePlanet()));

        _buttons.Add(new UIButton(
            new Rectangle(panelBounds.X + PanelPadding + buttonWidth + 12, buttonY, buttonWidth, buttonHeight),
            "Destabilize",
            () => DestabilizePlanet()));

        // Stabilizer switch: the whole row inside the card is clickable
        _buttons.Add(new UIButton(
            new Rectangle(_stabilizerCard.X + 8, _stabilizerCard.Y + 34, _stabilizerCard.Width - 16, 30),
            "Auto-stabilizer",
            () => _stabilizer.IsActive = !_stabilizer.IsActive));

        _buttons.Add(new UIButton(
            new Rectangle(panelBounds.Right - PanelPadding - 120, buttonY, 120, buttonHeight),
            "Close",
            () => IsVisible = false));

        // Close cross in the title bar
        _buttons.Add(new UIButton(
            UIWidgets.CloseButtonRect(panelBounds),
            "",
            () => IsVisible = false));
    }

    public void Draw(SpriteBatch spriteBatch)
    {
        if (!IsVisible) return;

        int screenWidth = _graphicsDevice.Viewport.Width;
        int screenHeight = _graphicsDevice.Viewport.Height;
        PositionSliders();
        var panelBounds = GetPanelBounds();

        UIWidgets.DrawBackdrop(spriteBatch, screenWidth, screenHeight);
        UIWidgets.DrawWindow(spriteBatch, panelBounds, "PLANETARY CONTROLS", UITheme.Gold, closeButton: true,
            note: "Drag a slider to change the whole planet");

        foreach (var header in _sectionHeaders)
            UIWidgets.DrawSection(spriteBatch, header.Area.X, header.Area.Y, header.Area.Width, header.Title);

        foreach (var slider in _sliders)
            DrawSlider(spriteBatch, slider);

        DrawStabilizerCard(spriteBatch);

        // Footer
        UIWidgets.DrawDivider(spriteBatch, _footerDivider.X, _footerDivider.Y, _footerDivider.Width);
        UIWidgets.DrawActionButton(spriteBatch, _buttons[0].Bounds, _buttons[0].Label, UITheme.Good);
        UIWidgets.DrawActionButton(spriteBatch, _buttons[1].Bounds, _buttons[1].Label, UITheme.Bad);
        UIWidgets.DrawButton(spriteBatch, _buttons[3].Bounds, _buttons[3].Label);

        string hint = "Restore: Earth-like values, stabilizer on.  Destabilize: random values, stabilizer off.";
        int hintX = _buttons[1].Bounds.Right + 16;
        int hintWidth = _buttons[3].Bounds.X - 16 - hintX;
        hint = UITheme.Ellipsize(hint, hintWidth, UITheme.FontSmall);
        var hintSize = UITheme.Measure(hint, UITheme.FontSmall);
        UITheme.DrawText(spriteBatch, hint,
            new Vector2(hintX, _buttons[0].Bounds.Y + (_buttons[0].Bounds.Height - hintSize.Y) / 2f),
            UITheme.TextMuted, UITheme.FontSmall);
    }

    private void DrawStabilizerCard(SpriteBatch spriteBatch)
    {
        var card = _stabilizerCard;
        UIWidgets.DrawCard(spriteBatch, card);
        int x = card.X + 12;
        int width = card.Width - 24;
        UIWidgets.DrawSection(spriteBatch, x, card.Y + 10, width, "AUTO-STABILIZER", _stabilizer.IsActive ? UITheme.Good : UITheme.TextDim);
        UIWidgets.DrawToggleRow(spriteBatch, _buttons[2].Bounds,
            _stabilizer.IsActive ? "Keeping the planet habitable" : "Switched off", _stabilizer.IsActive, emphasised: true);

        int y = _buttons[2].Bounds.Bottom + 10;
        y = UIWidgets.DrawKeyValue(spriteBatch, x, y, width, "Adjustments made", _stabilizer.AdjustmentsMade.ToString("N0"),
            _stabilizer.IsActive ? UITheme.Text : UITheme.TextDim, UITheme.FontSmall) - 3;
        UITheme.DrawText(spriteBatch, "Last action", new Vector2(x, y), UITheme.TextDim, UITheme.FontSmall);
        y += 17;
        string action = string.IsNullOrEmpty(_stabilizer.LastAction) ? "None yet" : _stabilizer.LastAction;
        int maxLines = Math.Max(1, (card.Bottom - 6 - y) / 16);
        var lines = UITheme.WrapText(action, width, UITheme.FontSmall);
        for (int i = 0; i < lines.Count && i < maxLines; i++)
        {
            string line = i == maxLines - 1 && lines.Count > maxLines ? UITheme.Ellipsize(lines[i] + " more text", width, UITheme.FontSmall) : lines[i];
            UITheme.DrawText(spriteBatch, line, new Vector2(x, y), _stabilizer.IsActive ? UITheme.Good : UITheme.TextMuted, UITheme.FontSmall);
            y += 16;
        }
    }

    private void DrawSlider(SpriteBatch spriteBatch, Slider slider)
    {
        Vector2 labelPosition = slider.HasLabelPosition
            ? slider.LabelPosition
            : new Vector2(slider.Bounds.X, slider.Bounds.Y - 20);

        bool active = slider.IsDragging || slider.Bounds.Contains(Mouse.GetState().Position);
        UITheme.DrawText(spriteBatch, slider.Label, labelPosition, active ? UITheme.Text : UITheme.TextDim, UITheme.FontNormal);

        // Value, right-aligned on the label line
        string valueText = FormatSliderValue(slider);
        var valueSize = UITheme.Measure(valueText, UITheme.FontNormal);
        UITheme.DrawText(spriteBatch, valueText,
            new Vector2(slider.Bounds.Right - valueSize.X, labelPosition.Y),
            slider.IsDragging ? UITheme.Gold : UITheme.Text, UITheme.FontNormal);

        UIWidgets.DrawSlider(spriteBatch, slider.Bounds, slider.GetNormalizedValue(), slider.IsDragging);
    }

    private static string FormatSliderValue(Slider slider)
    {
        // Ice coverage is stored as a 0..1 fraction
        if (slider.Unit == "%" && slider.Max <= 1f) return $"{slider.Value * 100f:F0} %";
        string format = slider.Max >= 1000f ? "F0" : "F2";
        string number = slider.Value.ToString(format);
        return slider.Unit switch
        {
            "" => number,
            "x" => number + "x",
            "%" => number + " %",
            _ => number + " " + slider.Unit
        };
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
