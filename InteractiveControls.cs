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
    private readonly SimpleFont _font;
    private readonly PlanetMap _map;
    private Texture2D _pixelTexture;
    private List<Button> _buttons;
    private MouseState _previousMouseState;

    public bool TerraformingActive { get; private set; } = false;
    public bool ShowControls { get; set; } = true;

    private float _terraformProgress = 0;
    private const float TerraformDuration = 100f; // 100 years of gradual restoration

    public InteractiveControls(GraphicsDevice graphicsDevice, SimpleFont font, PlanetMap map)
    {
        _graphicsDevice = graphicsDevice;
        _font = font;
        _map = map;

        _pixelTexture = new Texture2D(_graphicsDevice, 1, 1);
        _pixelTexture.SetData(new[] { Color.White });

        InitializeButtons();
    }

    private void InitializeButtons()
    {
        _buttons = new List<Button>();

        // Terraforming button - restore planet to habitable state
        _buttons.Add(new Button(
            new Rectangle(10, 600, 200, 40),
            "Terraform Planet",
            Color.Green,
            () => ActivateTerraforming()
        ));

        // Cool Planet button - reduce global temperature
        _buttons.Add(new Button(
            new Rectangle(220, 600, 180, 40),
            "Cool Planet",
            Color.LightBlue,
            () => CoolPlanet()
        ));

        // Seed Life button - add bacteria to suitable areas
        _buttons.Add(new Button(
            new Rectangle(410, 600, 180, 40),
            "Seed Life",
            Color.LightGreen,
            () => SeedLife()
        ));

        // Clear Pollution button - reduce CO2
        _buttons.Add(new Button(
            new Rectangle(600, 600, 180, 40),
            "Clear Pollution",
            Color.Cyan,
            () => ClearPollution()
        ));
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

        if (mouseState.LeftButton == ButtonState.Pressed &&
            _previousMouseState.LeftButton == ButtonState.Released)
        {
            var mousePos = new Point(mouseState.X, mouseState.Y);

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

        // Draw buttons
        foreach (var button in _buttons)
        {
            DrawButton(spriteBatch, button);
        }

        // Draw terraforming progress
        if (TerraformingActive)
        {
            int progressX = 10;
            int progressY = 650;
            int progressWidth = 780;
            int progressHeight = 30;

            // Background
            spriteBatch.Draw(_pixelTexture,
                new Rectangle(progressX, progressY, progressWidth, progressHeight),
                new Color(0, 0, 0, 180));

            // Progress bar
            float progressRatio = _terraformProgress / TerraformDuration;
            int filledWidth = (int)(progressWidth * Math.Min(progressRatio, 1.0f));
            spriteBatch.Draw(_pixelTexture,
                new Rectangle(progressX, progressY, filledWidth, progressHeight),
                new Color(0, 255, 0, 180));

            // Progress text
            _font.DrawString(spriteBatch,
                $"Terraforming: {(progressRatio * 100):F0}%",
                new Vector2(progressX + 10, progressY + 5),
                Color.White);
        }
    }

    private void DrawButton(SpriteBatch spriteBatch, Button button)
    {
        // Button background
        spriteBatch.Draw(_pixelTexture, button.Bounds, new Color(button.Color, 0.7f));

        // Button border
        DrawRectangleBorder(spriteBatch, button.Bounds, Color.White, 2);

        // Button text
        var textSize = _font.MeasureString(button.Text);
        var textPos = new Vector2(
            button.Bounds.X + (button.Bounds.Width - textSize.X) / 2,
            button.Bounds.Y + (button.Bounds.Height - textSize.Y) / 2
        );
        _font.DrawString(spriteBatch, button.Text, textPos, Color.White);
    }

    private void DrawRectangleBorder(SpriteBatch spriteBatch, Rectangle rect, Color color, int thickness)
    {
        // Top
        spriteBatch.Draw(_pixelTexture, new Rectangle(rect.X, rect.Y, rect.Width, thickness), color);
        // Bottom
        spriteBatch.Draw(_pixelTexture, new Rectangle(rect.X, rect.Bottom - thickness, rect.Width, thickness), color);
        // Left
        spriteBatch.Draw(_pixelTexture, new Rectangle(rect.X, rect.Y, thickness, rect.Height), color);
        // Right
        spriteBatch.Draw(_pixelTexture, new Rectangle(rect.Right - thickness, rect.Y, thickness, rect.Height), color);
    }

    private class Button
    {
        public Rectangle Bounds { get; }
        public string Text { get; }
        public Color Color { get; }
        public Action? OnClick { get; }

        public Button(Rectangle bounds, string text, Color color, Action? onClick)
        {
            Bounds = bounds;
            Text = text;
            Color = color;
            OnClick = onClick;
        }
    }
}
