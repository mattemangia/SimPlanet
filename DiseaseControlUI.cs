using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace SimPlanet;

/// <summary>
/// UI for controlling diseases and pandemics (Plague Inc style)
/// </summary>
public class DiseaseControlUI
{
    private readonly GraphicsDevice _graphicsDevice;
    private readonly FontRenderer _font;
    private Texture2D _pixelTexture;
    private readonly DiseaseManager _diseaseManager;
    private readonly PlanetMap _map;
    private readonly CivilizationManager _civManager;

    private bool _isVisible = false;
    private Disease? _selectedDisease = null;

    // UI panels
    private Rectangle _mainPanel;
    private Rectangle _statsPanel;
    private Rectangle _evolutionPanel;
    private Rectangle _diseaseSelectorPanel;

    // Buttons
    private List<UIButton> _evolutionButtons = new();
    private List<UIButton> _actionButtons = new();
    private UIButton? _closeButton;
    private UIButton? _createDiseaseButton;

    // Scroll for evolution traits
    private int _evolutionScrollOffset = 0;

    // Create disease modal
    private bool _showCreateDiseaseModal = false;
    private string _newDiseaseName = "Plague";
    private PathogenType _newDiseaseType = PathogenType.Virus;
    private int _selectedOriginCivId = -1;

    public bool IsVisible
    {
        get => _isVisible;
        set => _isVisible = value;
    }

    public DiseaseControlUI(GraphicsDevice graphicsDevice, FontRenderer font, DiseaseManager diseaseManager, PlanetMap map, CivilizationManager civManager)
    {
        _graphicsDevice = graphicsDevice;
        _font = font;
        _diseaseManager = diseaseManager;
        _map = map;
        _civManager = civManager;

        // Create pixel texture
        _pixelTexture = new Texture2D(_graphicsDevice, 1, 1);
        _pixelTexture.SetData(new[] { Color.White });
    }

    public void Update(MouseState mouseState, MouseState previousMouseState, KeyboardState keyState)
    {
        if (!_isVisible) return;

        bool clicked = mouseState.LeftButton == ButtonState.Released &&
                      previousMouseState.LeftButton == ButtonState.Pressed;

        // Close button
        if (_closeButton != null && clicked && _closeButton.Bounds.Contains(mouseState.Position))
        {
            _isVisible = false;
            return;
        }

        // Create disease modal
        if (_showCreateDiseaseModal)
        {
            UpdateCreateDiseaseModal(mouseState, clicked);
            return;
        }

        // Create disease button
        if (_createDiseaseButton != null && clicked && _createDiseaseButton.Bounds.Contains(mouseState.Position))
        {
            _showCreateDiseaseModal = true;
            _selectedOriginCivId = _civManager.Civilizations.FirstOrDefault()?.Id ?? -1;
            return;
        }

        // Disease selector
        UpdateDiseaseSelector(mouseState, clicked);

        // Evolution buttons
        if (_selectedDisease != null && !_selectedDisease.CureDeployed)
        {
            foreach (var btn in _evolutionButtons)
            {
                if (clicked && btn.Bounds.Contains(mouseState.Position) && btn.IsEnabled)
                {
                    // Evolve trait
                    _diseaseManager.EvolveTrait(_selectedDisease, btn.Tag ?? "");
                }
            }
        }

        // Scroll
        int scrollDelta = mouseState.ScrollWheelValue - previousMouseState.ScrollWheelValue;
        if (scrollDelta != 0 && _evolutionPanel.Contains(mouseState.Position))
        {
            _evolutionScrollOffset -= scrollDelta / 120; // Standard mouse wheel unit
            _evolutionScrollOffset = Math.Max(0, _evolutionScrollOffset);
        }
    }

    private void UpdateCreateDiseaseModal(MouseState mouseState, bool clicked)
    {
        // Simple modal with pathogen type selection and civilization origin
        // For now, just create with defaults
        if (clicked)
        {
            // Check if clicked outside modal to close
            var modalBounds = new Rectangle(400, 200, 480, 320);
            if (!modalBounds.Contains(mouseState.Position))
            {
                _showCreateDiseaseModal = false;
                return;
            }

            // Pathogen type buttons
            for (int i = 0; i < 6; i++)
            {
                var btnBounds = new Rectangle(420, 280 + i * 35, 200, 30);
                if (btnBounds.Contains(mouseState.Position))
                {
                    _newDiseaseType = (PathogenType)i;
                }
            }

            // Civilization selector
            int civIndex = 0;
            foreach (var civ in _civManager.Civilizations.Take(5))
            {
                var civBounds = new Rectangle(640, 280 + civIndex * 35, 200, 30);
                if (civBounds.Contains(mouseState.Position))
                {
                    _selectedOriginCivId = civ.Id;
                }
                civIndex++;
            }

            // Create button
            var createBounds = new Rectangle(500, 470, 180, 35);
            if (createBounds.Contains(mouseState.Position))
            {
                // Create the disease
                var originCiv = _civManager.Civilizations.FirstOrDefault(c => c.Id == _selectedOriginCivId);
                if (originCiv != null && originCiv.Territory.Any())
                {
                    var originCell = originCiv.Territory.First();
                    _selectedDisease = _diseaseManager.CreateDisease(_newDiseaseName, _newDiseaseType, originCell.x, originCell.y);
                    _showCreateDiseaseModal = false;
                }
            }
        }
    }

    private void UpdateDiseaseSelector(MouseState mouseState, bool clicked)
    {
        int index = 0;
        foreach (var disease in _diseaseManager.Diseases)
        {
            var bounds = new Rectangle(_diseaseSelectorPanel.X + 10, _diseaseSelectorPanel.Y + 50 + index * 40, _diseaseSelectorPanel.Width - 20, 35);
            if (clicked && bounds.Contains(mouseState.Position))
            {
                _selectedDisease = disease;
            }
            index++;
        }
    }

    public void Draw(SpriteBatch spriteBatch, int screenWidth, int screenHeight)
    {
        if (!_isVisible) return;

        // Semi-transparent overlay
        spriteBatch.Draw(_pixelTexture, new Rectangle(0, 0, screenWidth, screenHeight), new Color(0, 0, 0, 180));

        // Calculate panel positions
        _mainPanel = new Rectangle(50, 50, screenWidth - 100, screenHeight - 100);
        _diseaseSelectorPanel = new Rectangle(_mainPanel.X + 10, _mainPanel.Y + 10, 280, _mainPanel.Height - 20);
        _statsPanel = new Rectangle(_diseaseSelectorPanel.Right + 10, _mainPanel.Y + 10, 400, 250);
        _evolutionPanel = new Rectangle(_statsPanel.X, _statsPanel.Bottom + 10, _statsPanel.Width, _mainPanel.Height - _statsPanel.Height - 40);

        // Draw create disease modal
        if (_showCreateDiseaseModal)
        {
            DrawCreateDiseaseModal(spriteBatch, screenWidth, screenHeight);
            return;
        }

        // Draw main panel background
        spriteBatch.Draw(_pixelTexture, _mainPanel, new Color(10, 20, 40, 240));
        DrawBorder(spriteBatch, _mainPanel.X, _mainPanel.Y, _mainPanel.Width, _mainPanel.Height, new Color(100, 150, 200), 3);

        // Title
        _font.DrawString(spriteBatch, "DISEASE CONTROL CENTER", new Vector2(_mainPanel.X + 20, _mainPanel.Y + 15), new Color(255, 100, 100), 24);

        // Close button
        _closeButton = new UIButton
        {
            Bounds = new Rectangle(_mainPanel.Right - 40, _mainPanel.Y + 10, 30, 30),
            Label = "X"
        };
        DrawButton(spriteBatch, _closeButton, Color.Red);

        // Disease selector panel
        DrawDiseaseSelector(spriteBatch);

        // Selected disease info
        if (_selectedDisease != null)
        {
            DrawStatsPanel(spriteBatch);
            DrawEvolutionPanel(spriteBatch);
        }
        else
        {
            // No disease selected - show instructions
            var msg = "Create a new pandemic or select an active disease";
            var msgSize = _font.MeasureString(msg, 16);
            _font.DrawString(spriteBatch, msg,
                new Vector2(_statsPanel.X + (_statsPanel.Width - msgSize.X) / 2, _statsPanel.Y + 100),
                new Color(200, 200, 200, 255), 16);
        }
    }

    private void DrawDiseaseSelector(SpriteBatch spriteBatch)
    {
        // Panel background
        spriteBatch.Draw(_pixelTexture, _diseaseSelectorPanel, new Color(20, 30, 50, 220));
        DrawBorder(spriteBatch, _diseaseSelectorPanel.X, _diseaseSelectorPanel.Y, _diseaseSelectorPanel.Width, _diseaseSelectorPanel.Height, new Color(80, 120, 160), 2);

        // Title
        _font.DrawString(spriteBatch, "Active Diseases", new Vector2(_diseaseSelectorPanel.X + 10, _diseaseSelectorPanel.Y + 10), Color.White, 18);

        // Create disease button
        _createDiseaseButton = new UIButton
        {
            Bounds = new Rectangle(_diseaseSelectorPanel.X + 10, _diseaseSelectorPanel.Y + _diseaseSelectorPanel.Height - 45, _diseaseSelectorPanel.Width - 20, 35),
            Label = "+ Create Pandemic"
        };
        DrawButton(spriteBatch, _createDiseaseButton, new Color(150, 50, 50));

        // List diseases
        int index = 0;
        foreach (var disease in _diseaseManager.Diseases)
        {
            var bounds = new Rectangle(_diseaseSelectorPanel.X + 10, _diseaseSelectorPanel.Y + 50 + index * 40, _diseaseSelectorPanel.Width - 20, 35);

            bool isSelected = disease == _selectedDisease;
            Color bgColor = isSelected ? new Color(80, 40, 40, 200) : new Color(40, 50, 70, 180);
            Color borderColor = isSelected ? new Color(200, 100, 100) : new Color(100, 120, 140);

            if (disease.IsActive)
            {
                borderColor = new Color(255, 100, 100);
            }
            if (disease.CureDeployed)
            {
                borderColor = new Color(100, 255, 100);
            }

            spriteBatch.Draw(_pixelTexture, bounds, bgColor);
            DrawBorder(spriteBatch, bounds.X, bounds.Y, bounds.Width, bounds.Height, borderColor, 1);

            // Disease name and type
            _font.DrawString(spriteBatch, disease.Name, new Vector2(bounds.X + 5, bounds.Y + 2), Color.White, 14);
            _font.DrawString(spriteBatch, disease.Type.ToString(), new Vector2(bounds.X + 5, bounds.Y + 18), new Color(180, 180, 180), 11);

            index++;
        }
    }

    private void DrawStatsPanel(SpriteBatch spriteBatch)
    {
        if (_selectedDisease == null) return;

        // Panel background
        spriteBatch.Draw(_pixelTexture, _statsPanel, new Color(20, 30, 50, 220));
        DrawBorder(spriteBatch, _statsPanel.X, _statsPanel.Y, _statsPanel.Width, _statsPanel.Height, new Color(200, 100, 100), 2);

        int y = _statsPanel.Y + 10;

        // Disease name
        _font.DrawString(spriteBatch, _selectedDisease.Name, new Vector2(_statsPanel.X + 10, y), new Color(255, 150, 150), 20);
        y += 30;

        // Status
        string status = _selectedDisease.CureDeployed ? "CURE DEPLOYED" :
                       _selectedDisease.IsActive ? "ACTIVE" : "ERADICATED";
        Color statusColor = _selectedDisease.CureDeployed ? Color.Green :
                           _selectedDisease.IsActive ? Color.Red : Color.Gray;
        _font.DrawString(spriteBatch, status, new Vector2(_statsPanel.X + 10, y), statusColor, 16);
        y += 25;

        // Statistics
        _font.DrawString(spriteBatch, $"Infected: {_selectedDisease.TotalInfected:N0}", new Vector2(_statsPanel.X + 10, y), new Color(255, 200, 100), 14);
        y += 20;
        _font.DrawString(spriteBatch, $"Dead: {_selectedDisease.TotalDeaths:N0}", new Vector2(_statsPanel.X + 10, y), new Color(255, 100, 100), 14);
        y += 20;
        _font.DrawString(spriteBatch, $"Healthy: {_selectedDisease.HealthyRemaining:N0}", new Vector2(_statsPanel.X + 10, y), new Color(100, 255, 100), 14);
        y += 25;

        // Disease traits
        _font.DrawString(spriteBatch, $"Infectivity: {(_selectedDisease.Infectivity * 100):F0}%", new Vector2(_statsPanel.X + 10, y), Color.White, 12);
        DrawBar(spriteBatch, new Rectangle(_statsPanel.X + 150, y + 2, 200, 12), _selectedDisease.Infectivity, new Color(255, 200, 100));
        y += 18;

        _font.DrawString(spriteBatch, $"Severity: {(_selectedDisease.Severity * 100):F0}%", new Vector2(_statsPanel.X + 10, y), Color.White, 12);
        DrawBar(spriteBatch, new Rectangle(_statsPanel.X + 150, y + 2, 200, 12), _selectedDisease.Severity, new Color(255, 150, 50));
        y += 18;

        _font.DrawString(spriteBatch, $"Lethality: {(_selectedDisease.Lethality * 100):F0}%", new Vector2(_statsPanel.X + 10, y), Color.White, 12);
        DrawBar(spriteBatch, new Rectangle(_statsPanel.X + 150, y + 2, 200, 12), _selectedDisease.Lethality, new Color(255, 50, 50));
        y += 20;

        // Cure progress
        if (!_selectedDisease.CureDeployed)
        {
            _font.DrawString(spriteBatch, $"Cure Progress: {_selectedDisease.GlobalCureProgress:F1}%", new Vector2(_statsPanel.X + 10, y), new Color(100, 255, 200), 14);
            DrawBar(spriteBatch, new Rectangle(_statsPanel.X + 150, y + 2, 200, 12), _selectedDisease.GlobalCureProgress / 100f, new Color(100, 255, 200));
        }
    }

    private void DrawEvolutionPanel(SpriteBatch spriteBatch)
    {
        if (_selectedDisease == null) return;

        // Panel background
        spriteBatch.Draw(_pixelTexture, _evolutionPanel, new Color(20, 30, 50, 220));
        DrawBorder(spriteBatch, _evolutionPanel.X, _evolutionPanel.Y, _evolutionPanel.Width, _evolutionPanel.Height, new Color(150, 100, 200), 2);

        // Title
        _font.DrawString(spriteBatch, "Evolution", new Vector2(_evolutionPanel.X + 10, _evolutionPanel.Y + 10), new Color(200, 150, 255), 18);

        if (_selectedDisease.CureDeployed)
        {
            _font.DrawString(spriteBatch, "Cannot evolve - cure deployed", new Vector2(_evolutionPanel.X + 10, _evolutionPanel.Y + 40), Color.Gray, 14);
            return;
        }

        _evolutionButtons.Clear();

        int y = _evolutionPanel.Y + 45 - _evolutionScrollOffset * 30;
        int x = _evolutionPanel.X + 10;

        // Transmission traits
        AddEvolutionCategory(spriteBatch, "TRANSMISSION", ref y);

        AddEvolutionButton("Air", "Airborne transmission", !_selectedDisease.TransmissionMethods.HasFlag(TransmissionMethod.Air), ref x, ref y);
        AddEvolutionButton("Water", "Waterborne transmission", !_selectedDisease.TransmissionMethods.HasFlag(TransmissionMethod.Water), ref x, ref y);
        AddEvolutionButton("Blood", "Blood transmission", !_selectedDisease.TransmissionMethods.HasFlag(TransmissionMethod.Blood), ref x, ref y);
        AddEvolutionButton("Livestock", "Animal transmission", !_selectedDisease.TransmissionMethods.HasFlag(TransmissionMethod.Livestock), ref x, ref y);
        AddEvolutionButton("Insects", "Insect vectors", !_selectedDisease.TransmissionMethods.HasFlag(TransmissionMethod.Insects), ref x, ref y);

        y += 10;
        AddEvolutionCategory(spriteBatch, "SYMPTOMS", ref y);

        AddEvolutionButton("Coughing", "Mild symptoms, increases spread", !_selectedDisease.Symptoms.HasFlag(DiseaseSymptoms.Coughing), ref x, ref y);
        AddEvolutionButton("Fever", "Noticeable symptoms", !_selectedDisease.Symptoms.HasFlag(DiseaseSymptoms.Fever), ref x, ref y);
        AddEvolutionButton("Pneumonia", "Severe respiratory issues", !_selectedDisease.Symptoms.HasFlag(DiseaseSymptoms.Pneumonia), ref x, ref y);
        AddEvolutionButton("Organ_Failure", "Critical organ damage", !_selectedDisease.Symptoms.HasFlag(DiseaseSymptoms.OrganFailure), ref x, ref y);
        AddEvolutionButton("Total_Organ_Failure", "Complete system shutdown", !_selectedDisease.Symptoms.HasFlag(DiseaseSymptoms.TotalOrganFailure), ref x, ref y);

        y += 10;
        AddEvolutionCategory(spriteBatch, "RESISTANCES", ref y);

        AddEvolutionButton("Cold_Resistance", "Survive in cold climates", _selectedDisease.ColdResistance < 0.99f, ref x, ref y);
        AddEvolutionButton("Heat_Resistance", "Survive in hot climates", _selectedDisease.HeatResistance < 0.99f, ref x, ref y);
        AddEvolutionButton("Drug_Resistance", "Resist medical treatment", _selectedDisease.DrugResistance < 0.99f, ref x, ref y);

        y += 10;
        AddEvolutionCategory(spriteBatch, "ABILITIES", ref y);

        AddEvolutionButton("Hardened_Resurgence", "Can re-infect cured people", !_selectedDisease.HardenedResurgence, ref x, ref y);
        AddEvolutionButton("Genetic_Reshuffle", "Delays cure research", !_selectedDisease.GeneticReShuffle, ref x, ref y);
        AddEvolutionButton("Total_Organ_Shutdown", "Massively increases lethality", !_selectedDisease.TotalOrganShutdown, ref x, ref y);

        // Draw all evolution buttons
        foreach (var btn in _evolutionButtons)
        {
            if (btn.Bounds.Bottom > _evolutionPanel.Y + 40 && btn.Bounds.Y < _evolutionPanel.Bottom - 10)
            {
                DrawEvolutionButton(spriteBatch, btn);
            }
        }
    }

    private void AddEvolutionCategory(SpriteBatch spriteBatch, string name, ref int y)
    {
        if (y > _evolutionPanel.Y + 35)
        {
            _font.DrawString(spriteBatch, name, new Vector2(_evolutionPanel.X + 10, y), new Color(255, 200, 100), 15);
        }
        y += 25;
    }

    private void AddEvolutionButton(string name, string description, bool available, ref int x, ref int y)
    {
        var btn = new UIButton
        {
            Bounds = new Rectangle(x, y, 180, 30),
            Label = name.Replace("_", " "),
            Tag = name,
            IsEnabled = available
        };
        _evolutionButtons.Add(btn);
        y += 35;
    }

    private void DrawEvolutionButton(SpriteBatch spriteBatch, UIButton btn)
    {
        Color bgColor = btn.IsEnabled ? new Color(60, 80, 120, 200) : new Color(40, 40, 40, 150);
        Color borderColor = btn.IsEnabled ? new Color(120, 150, 200) : new Color(80, 80, 80);
        Color textColor = btn.IsEnabled ? Color.White : new Color(120, 120, 120);

        spriteBatch.Draw(_pixelTexture, btn.Bounds, bgColor);
        DrawBorder(spriteBatch, btn.Bounds.X, btn.Bounds.Y, btn.Bounds.Width, btn.Bounds.Height, borderColor, 1);

        _font.DrawString(spriteBatch, btn.Label, new Vector2(btn.Bounds.X + 5, btn.Bounds.Y + 8), textColor, 14);
    }

    private void DrawCreateDiseaseModal(SpriteBatch spriteBatch, int screenWidth, int screenHeight)
    {
        var modalBounds = new Rectangle(screenWidth / 2 - 240, screenHeight / 2 - 160, 480, 320);

        // Modal background
        spriteBatch.Draw(_pixelTexture, modalBounds, new Color(30, 40, 60, 250));
        DrawBorder(spriteBatch, modalBounds.X, modalBounds.Y, modalBounds.Width, modalBounds.Height, new Color(150, 100, 100), 3);

        // Title
        _font.DrawString(spriteBatch, "Create New Pandemic", new Vector2(modalBounds.X + 20, modalBounds.Y + 15), new Color(255, 150, 150), 20);

        int y = modalBounds.Y + 60;

        // Pathogen type selection
        _font.DrawString(spriteBatch, "Pathogen Type:", new Vector2(modalBounds.X + 20, y), Color.White, 14);
        y += 25;

        string[] types = { "Bacteria", "Virus", "Fungus", "Parasite", "Prion", "Bioweapon" };
        for (int i = 0; i < types.Length; i++)
        {
            var btnBounds = new Rectangle(modalBounds.X + 20, y, 200, 30);
            bool isSelected = (PathogenType)i == _newDiseaseType;

            Color bgColor = isSelected ? new Color(100, 60, 60, 220) : new Color(50, 60, 80, 180);
            Color borderColor = isSelected ? new Color(200, 120, 120) : new Color(100, 120, 140);

            spriteBatch.Draw(_pixelTexture, btnBounds, bgColor);
            DrawBorder(spriteBatch, btnBounds.X, btnBounds.Y, btnBounds.Width, btnBounds.Height, borderColor, 1);
            _font.DrawString(spriteBatch, types[i], new Vector2(btnBounds.X + 10, btnBounds.Y + 8), Color.White, 12);

            y += 35;
        }

        // Origin civilization
        y = modalBounds.Y + 60;
        _font.DrawString(spriteBatch, "Origin:", new Vector2(modalBounds.X + 240, y), Color.White, 14);
        y += 25;

        int civIndex = 0;
        foreach (var civ in _civManager.Civilizations.Take(5))
        {
            var civBounds = new Rectangle(modalBounds.X + 240, y, 200, 30);
            bool isSelected = civ.Id == _selectedOriginCivId;

            Color bgColor = isSelected ? new Color(100, 60, 60, 220) : new Color(50, 60, 80, 180);
            Color borderColor = isSelected ? new Color(200, 120, 120) : new Color(100, 120, 140);

            spriteBatch.Draw(_pixelTexture, civBounds, bgColor);
            DrawBorder(spriteBatch, civBounds.X, civBounds.Y, civBounds.Width, civBounds.Height, borderColor, 1);
            _font.DrawString(spriteBatch, civ.Name, new Vector2(civBounds.X + 10, civBounds.Y + 8), Color.White, 12);

            y += 35;
            civIndex++;
        }

        // Create button
        var createBounds = new Rectangle(modalBounds.X + (modalBounds.Width - 180) / 2, modalBounds.Bottom - 50, 180, 35);
        spriteBatch.Draw(_pixelTexture, createBounds, new Color(150, 50, 50, 220));
        DrawBorder(spriteBatch, createBounds.X, createBounds.Y, createBounds.Width, createBounds.Height, new Color(200, 100, 100), 2);
        var createText = _font.MeasureString("CREATE PANDEMIC", 16);
        _font.DrawString(spriteBatch, "CREATE PANDEMIC",
            new Vector2(createBounds.X + (createBounds.Width - createText.X) / 2, createBounds.Y + 8),
            Color.White, 16);
    }

    private void DrawButton(SpriteBatch spriteBatch, UIButton btn, Color color)
    {
        spriteBatch.Draw(_pixelTexture, btn.Bounds, new Color(color.R, color.G, color.B, (byte)200));
        DrawBorder(spriteBatch, btn.Bounds.X, btn.Bounds.Y, btn.Bounds.Width, btn.Bounds.Height, color, 2);

        var textSize = _font.MeasureString(btn.Label, 14);
        _font.DrawString(spriteBatch, btn.Label,
            new Vector2(btn.Bounds.X + (btn.Bounds.Width - textSize.X) / 2, btn.Bounds.Y + (btn.Bounds.Height - textSize.Y) / 2),
            Color.White, 14);
    }

    private void DrawBorder(SpriteBatch spriteBatch, int x, int y, int width, int height, Color color, int thickness)
    {
        spriteBatch.Draw(_pixelTexture, new Rectangle(x, y, width, thickness), color);
        spriteBatch.Draw(_pixelTexture, new Rectangle(x, y + height - thickness, width, thickness), color);
        spriteBatch.Draw(_pixelTexture, new Rectangle(x, y, thickness, height), color);
        spriteBatch.Draw(_pixelTexture, new Rectangle(x + width - thickness, y, thickness, height), color);
    }

    private void DrawBar(SpriteBatch spriteBatch, Rectangle bounds, float value, Color color)
    {
        // Background
        spriteBatch.Draw(_pixelTexture, bounds, new Color(40, 40, 40));
        // Fill
        int fillWidth = (int)(bounds.Width * Math.Clamp(value, 0f, 1f));
        spriteBatch.Draw(_pixelTexture, new Rectangle(bounds.X, bounds.Y, fillWidth, bounds.Height), color);
        // Border
        DrawBorder(spriteBatch, bounds.X, bounds.Y, bounds.Width, bounds.Height, new Color(100, 100, 100), 1);
    }

    private class UIButton
    {
        public Rectangle Bounds { get; set; }
        public string Label { get; set; } = "";
        public string? Tag { get; set; }
        public bool IsEnabled { get; set; } = true;
    }
}
