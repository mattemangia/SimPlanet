using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace SimPlanet;

/// <summary>
/// Interactive UI controls for player intervention
/// </summary>
public class InteractiveControls
{
    private readonly GraphicsDevice _graphicsDevice;
    private readonly FontRenderer _font;
    private readonly PlanetMap _map;
    private Texture2D _pixelTexture;
    private List<Button> _buttons;
    private MouseState _previousMouseState;

    public bool TerraformingActive { get; private set; } = false;
    public bool ShowControls { get; set; } = false;

    private float _terraformProgress = 0;
    private const float TerraformDuration = 100f; // 100 years of gradual restoration

    public InteractiveControls(GraphicsDevice graphicsDevice, FontRenderer font, PlanetMap map)
    {
        _graphicsDevice = graphicsDevice;
        _font = font;
        _map = map;

        _pixelTexture = new Texture2D(_graphicsDevice, 1, 1);
        _pixelTexture.SetData(new[] { Color.White });

        _buttons = new List<Button>(); // Initialize early to avoid warning
        InitializeButtons();
    }

    private void InitializeButtons()
    {
        // Bounds are assigned by Layout(), which Update and Draw both call.
        _buttons = new List<Button>
        {
            // Terraforming button - restore planet to habitable state
            new Button("Terraform", UITheme.Good, () => ActivateTerraforming()),
            // Cool Planet button - reduce global temperature
            new Button("Cool Planet", Color.LightBlue, () => CoolPlanet()),
            // Seed Life button - add bacteria to suitable areas
            new Button("Seed Life", Color.LightGreen, () => SeedLife()),
            // Clear Pollution button - reduce CO2
            new Button("Clear CO2", Color.Cyan, () => ClearPollution()),
        };
        Layout();
    }

    private Rectangle _panel;

    /// <summary>Small panel in the top-right corner, below the toolbar.</summary>
    private void Layout()
    {
        const int width = 176, buttonHeight = 30, spacing = 6;
        int x = _graphicsDevice.Viewport.Width - width - 12;
        int y = 100;
        int by = y + UIWidgets.HeaderHeight + 10;
        foreach (var button in _buttons)
        {
            button.Bounds = new Rectangle(x + 12, by, width - 24, buttonHeight);
            by += buttonHeight + spacing;
        }
        _panel = new Rectangle(x, y, width, by - spacing + 12 - y);
    }

    public void Update(float deltaTime)
    {
        // Handle terraforming process
        if (TerraformingActive)
        {
            _terraformProgress += deltaTime;

            // Gradual restoration over time
            float progressRatio = _terraformProgress / TerraformDuration;
            if (progressRatio <= 1.0f)
            {
                GraduallyRestorePlanet(deltaTime);
            }
            else
            {
                TerraformingActive = false;
                _terraformProgress = 0;
            }
        }

        // Handle mouse input
        var mouseState = Mouse.GetState();

        // The buttons only exist while the panel is shown
        if (ShowControls &&
            mouseState.LeftButton == ButtonState.Pressed &&
            _previousMouseState.LeftButton == ButtonState.Released)
        {
            var mousePos = new Point(mouseState.X, mouseState.Y);
            Layout();

            foreach (var button in _buttons)
            {
                if (button.Bounds.Contains(mousePos))
                {
                    button.OnClick?.Invoke();
                }
            }
        }

        _previousMouseState = mouseState;
    }

    private void ActivateTerraforming()
    {
        if (!TerraformingActive)
        {
            TerraformingActive = true;
            _terraformProgress = 0;
        }
    }

    private void GraduallyRestorePlanet(float deltaTime)
    {
        // Gradual restoration of planetary conditions
        float restorationRate = deltaTime * 0.01f;

        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                var cell = _map.Cells[x, y];

                // Restore atmosphere
                if (_map.GlobalCO2 > 1.0f)
                {
                    cell.CO2 = Math.Max(cell.CO2 - restorationRate * 5, 1.0f);
                }

                if (_map.GlobalOxygen < 21.0f && cell.LifeType == LifeForm.PlantLife)
                {
                    cell.Oxygen += restorationRate * 2;
                }

                // Moderate temperature
                if (cell.Temperature > 30)
                {
                    cell.Temperature -= restorationRate * 10;
                }
                else if (cell.Temperature < 0 && !cell.IsWater)
                {
                    cell.Temperature += restorationRate * 5;
                }

                // Restore rainfall in dry areas
                if (cell.IsLand && cell.Rainfall < 0.3f)
                {
                    cell.Rainfall += restorationRate * 0.5f;
                }

                // Restore biomass in damaged areas
                if (cell.IsLand && cell.Biomass < 0.3f && cell.Temperature > 0 && cell.Temperature < 35)
                {
                    cell.Biomass += restorationRate;
                }

                // Reduce greenhouse effect
                if (cell.Greenhouse > 0.5f)
                {
                    cell.Greenhouse -= restorationRate;
                }
            }
        }

        // Gradually reduce solar energy back to normal
        if (_map.SolarEnergy > 1.0f)
        {
            _map.SolarEnergy -= restorationRate * 0.1f;
            _map.SolarEnergy = Math.Max(_map.SolarEnergy, 1.0f);
        }
    }

    private void CoolPlanet()
    {
        // Immediate cooling effect
        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                var cell = _map.Cells[x, y];
                cell.Temperature -= 5;
                cell.CO2 *= 0.9f; // Reduce CO2 by 10%
            }
        }

        _map.SolarEnergy = Math.Max(_map.SolarEnergy - 0.05f, 0.9f);
    }

    private void SeedLife()
    {
        // Add bacteria to suitable cells
        int seeded = 0;
        for (int x = 0; x < _map.Width && seeded < 100; x++)
        {
            for (int y = 0; y < _map.Height && seeded < 100; y++)
            {
                var cell = _map.Cells[x, y];

                if (cell.IsLand && cell.LifeType == LifeForm.None &&
                    cell.Temperature > 0 && cell.Temperature < 50 &&
                    cell.Humidity > 0.2f)
                {
                    cell.LifeType = LifeForm.Bacteria;
                    cell.Biomass = 0.2f;
                    seeded++;
                }
            }
        }
    }

    private void ClearPollution()
    {
        // Reduce CO2 across the planet
        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                var cell = _map.Cells[x, y];
                cell.CO2 *= 0.7f; // Reduce by 30%
                cell.Greenhouse *= 0.8f;
            }
        }
    }

    public void Draw(SpriteBatch spriteBatch)
    {
        if (!ShowControls) return;

        Layout();
        UIWidgets.DrawWindow(spriteBatch, _panel, "QUICK ACTIONS", UITheme.Good, closeButton: false);

        // Draw buttons
        foreach (var button in _buttons)
        {
            bool running = TerraformingActive && button == _buttons[0];
            UIWidgets.DrawActionButton(spriteBatch, button.Bounds, button.Text, button.Color, selected: running);
        }

        // Draw terraforming progress
        if (TerraformingActive)
        {
            int screenHeight = _graphicsDevice.Viewport.Height;
            int screenWidth = _graphicsDevice.Viewport.Width;
            int progressWidth = Math.Min(600, screenWidth - 40);
            var box = new Rectangle((screenWidth - progressWidth) / 2, screenHeight - 138, progressWidth, 50); // above the bottom bar
            UITheme.DrawPanel(spriteBatch, box, new Color(12, 17, 28, 246), Color.Lerp(UITheme.Border, UITheme.Good, 0.45f));

            float progressRatio = Math.Min(_terraformProgress / TerraformDuration, 1.0f);
            UIWidgets.DrawKeyValue(spriteBatch, box.X + 14, box.Y + 8, box.Width - 28, "Terraforming the planet",
                $"{progressRatio * 100:F0} %", UITheme.Good);
            UITheme.DrawMeter(spriteBatch, new Rectangle(box.X + 14, box.Y + 31, box.Width - 28, 8), progressRatio, UITheme.Good);
        }
    }

    private class Button
    {
        public Rectangle Bounds { get; set; }
        public string Text { get; }
        public Color Color { get; }
        public Action? OnClick { get; }

        public Button(string text, Color color, Action? onClick)
        {
            Text = text;
            Color = color;
            OnClick = onClick;
        }
    }
}
