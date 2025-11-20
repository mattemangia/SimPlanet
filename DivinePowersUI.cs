using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace SimPlanet;

/// <summary>
/// UI for divine powers - allows player to interfere with civilizations
/// </summary>
public class DivinePowersUI
{
    private readonly GraphicsDevice _graphicsDevice;
    private readonly FontRenderer _font;
    private readonly CivilizationManager _civManager;
    private Texture2D _pixelTexture;
    private MouseState _previousMouseState;

    public bool IsOpen { get; set; } = false;
    private Civilization? _selectedCiv = null;
    private Civilization? _targetCiv = null;
    private DivinePowerMode _currentMode = DivinePowerMode.None;

    private List<Button> _mainButtons = new();
    private List<Button> _civButtons = new();
    private List<Button> _govButtons = new();
    private List<Button> _spyButtons = new();

    private string _statusMessage = "";
    private float _messageTimer = 0f;

    // UI Style
    private readonly Color _panelBgColor = new Color(20, 20, 40, 230);
    private readonly Color _borderColor = Color.Gold;
    private const int PanelWidth = 250;
    private const int PanelHeight = 600;

    public DivinePowersUI(GraphicsDevice graphicsDevice, FontRenderer font, CivilizationManager civManager)
    {
        _graphicsDevice = graphicsDevice;
        _font = font;
        _civManager = civManager;

        _pixelTexture = new Texture2D(_graphicsDevice, 1, 1);
        _pixelTexture.SetData(new[] { Color.White });

        InitializeMainButtons();
    }

    private void InitializeMainButtons()
    {
        int buttonWidth = 200;
        int buttonHeight = 30;
        // These coordinates are now relative to the panel
        int startX = 25; // Panel padding
        int startY = 50;
        int spacing = 5;

        _mainButtons = new List<Button>
        {
            new Button(new Rectangle(startX, startY, buttonWidth, buttonHeight),
                "Change Government", Color.Purple, () => OpenMode(DivinePowerMode.ChangeGovernment)),

            new Button(new Rectangle(startX, startY + (buttonHeight + spacing), buttonWidth, buttonHeight),
                "Send Spies", Color.DarkRed, () => OpenMode(DivinePowerMode.SendSpies)),

            new Button(new Rectangle(startX, startY + 2 * (buttonHeight + spacing), buttonWidth, buttonHeight),
                "Force Betrayal", Color.OrangeRed, () => OpenMode(DivinePowerMode.ForceBetray)),

            new Button(new Rectangle(startX, startY + 3 * (buttonHeight + spacing), buttonWidth, buttonHeight),
                "Bless Civilization", Color.Gold, () => OpenMode(DivinePowerMode.Bless)),

            new Button(new Rectangle(startX, startY + 4 * (buttonHeight + spacing), buttonWidth, buttonHeight),
                "Curse Civilization", Color.DarkGray, () => OpenMode(DivinePowerMode.Curse)),

            new Button(new Rectangle(startX, startY + 5 * (buttonHeight + spacing), buttonWidth, buttonHeight),
                "Force Alliance", Color.Green, () => OpenMode(DivinePowerMode.ForceAlliance)),

            new Button(new Rectangle(startX, startY + 6 * (buttonHeight + spacing), buttonWidth, buttonHeight),
                "Force War", Color.Red, () => OpenMode(DivinePowerMode.ForceWar)),

            new Button(new Rectangle(startX, startY + 7 * (buttonHeight + spacing), buttonWidth, buttonHeight),
                "Advance Civilization", Color.CornflowerBlue, () => OpenMode(DivinePowerMode.AdvanceCivilization)),

            new Button(new Rectangle(startX, startY + 8 * (buttonHeight + spacing), buttonWidth, buttonHeight),
                "Close Menu", Color.Gray, () => IsOpen = false)
        };
    }

    private void OpenMode(DivinePowerMode mode)
    {
        _currentMode = mode;
        _selectedCiv = null;
        _targetCiv = null;

        // Build civilization selection buttons
        BuildCivButtons();
    }

    private void BuildCivButtons()
    {
        _civButtons.Clear();

        int buttonWidth = 220;
        int buttonHeight = 30;
        int startX = 25 + PanelWidth + 10; // Next to main panel
        int startY = 50;
        int spacing = 5;

        var civilizations = _civManager.Civilizations;
        for (int i = 0; i < civilizations.Count; i++)
        {
            var civ = civilizations[i];
            int index = i;

            string buttonText = $"{civ.Name}";
            if (civ.Government?.CurrentRuler != null)
            {
                // Simplify text to fit button
                buttonText = $"{civ.Name} ({civ.Government.Type})";
            }

            _civButtons.Add(new Button(
                new Rectangle(startX, startY + i * (buttonHeight + spacing), buttonWidth, buttonHeight),
                buttonText,
                GetCivColor(civ),
                () => SelectCivilization(civilizations[index])
            ));
        }
    }

    private void SelectCivilization(Civilization civ)
    {
        if (_currentMode == DivinePowerMode.ChangeGovernment)
        {
            _selectedCiv = civ;
            BuildGovernmentButtons();
        }
        else if (_currentMode == DivinePowerMode.Bless)
        {
            _civManager.DivinePowers.BlessCivilization(civ);
            ShowMessage($"Blessed {civ.Name}!");
            _currentMode = DivinePowerMode.None;
        }
        else if (_currentMode == DivinePowerMode.Curse)
        {
            _civManager.DivinePowers.CurseCivilization(civ);
            ShowMessage($"Cursed {civ.Name}!");
            _currentMode = DivinePowerMode.None;
        }
        else if (_currentMode == DivinePowerMode.AdvanceCivilization)
        {
            _civManager.DivinePowers.AdvanceCivilization(civ);
            ShowMessage($"Advanced {civ.Name}!");
            _currentMode = DivinePowerMode.None;
        }
        else if (_selectedCiv == null)
        {
            _selectedCiv = civ;
            BuildCivButtons(); // Rebuild to select target
        }
        else
        {
            _targetCiv = civ;
            ExecuteAction();
        }
    }

    private void BuildGovernmentButtons()
    {
        _govButtons.Clear();

        int buttonWidth = 200;
        int buttonHeight = 30;
        int startX = 25 + PanelWidth * 2 + 20; // Next to civ panel
        int startY = 50;
        int spacing = 5;

        var govTypes = Enum.GetValues<GovernmentType>();
        for (int i = 0; i < govTypes.Length; i++)
        {
            var govType = govTypes[i];
            int index = i;

            _govButtons.Add(new Button(
                new Rectangle(startX, startY + i * (buttonHeight + spacing), buttonWidth, buttonHeight),
                govType.ToString(),
                GetGovernmentColor(govType),
                () => ChangeGovernment(govType)
            ));
        }
    }

    private void ChangeGovernment(GovernmentType newType)
    {
        if (_selectedCiv == null) return;

        _civManager.DivinePowers.ForceGovernmentChange(_selectedCiv, newType, 0);
        ShowMessage($"Changed {_selectedCiv.Name} to {newType}!");
        _currentMode = DivinePowerMode.None;
        _selectedCiv = null;
    }

    private void ExecuteAction()
    {
        if (_selectedCiv == null || _targetCiv == null) return;

        switch (_currentMode)
        {
            case DivinePowerMode.SendSpies:
                OpenSpyMissionMenu();
                break;

            case DivinePowerMode.ForceBetray:
                var relation = _selectedCiv.DiplomaticRelations.GetValueOrDefault(_targetCiv.Id);
                if (relation != null)
                {
                    _civManager.DivinePowers.ForceBetray(_selectedCiv, _targetCiv, relation);
                    ShowMessage($"{_selectedCiv.Name} betrayed {_targetCiv.Name}!");
                }
                _currentMode = DivinePowerMode.None;
                break;

            case DivinePowerMode.ForceAlliance:
                var allianceRel = _selectedCiv.DiplomaticRelations.GetValueOrDefault(_targetCiv.Id);
                if (allianceRel != null)
                {
                    _civManager.DivinePowers.ManipulateRelations(allianceRel, 100);
                    allianceRel.Status = DiplomaticStatus.Allied;
                    var treaty = new Treaty(TreatyType.MilitaryAlliance, 0);
                    allianceRel.AddTreaty(treaty);
                    ShowMessage($"{_selectedCiv.Name} allied with {_targetCiv.Name}!");
                }
                _currentMode = DivinePowerMode.None;
                break;

            case DivinePowerMode.ForceWar:
                var warRel = _selectedCiv.DiplomaticRelations.GetValueOrDefault(_targetCiv.Id);
                if (warRel != null)
                {
                    warRel.DeclareWar();
                    _selectedCiv.AtWar = true;
                    _selectedCiv.WarTargetId = _targetCiv.Id;
                    _targetCiv.AtWar = true;
                    _targetCiv.WarTargetId = _selectedCiv.Id;
                    ShowMessage($"{_selectedCiv.Name} declared war on {_targetCiv.Name}!");
                }
                _currentMode = DivinePowerMode.None;
                break;
        }

        _selectedCiv = null;
        _targetCiv = null;
    }

    private void OpenSpyMissionMenu()
    {
        _spyButtons.Clear();

        int buttonWidth = 200;
        int buttonHeight = 30;
        int startX = 25 + PanelWidth * 2 + 20;
        int startY = 50;
        int spacing = 5;

        var missions = Enum.GetValues<SpyMission>();
        for (int i = 0; i < missions.Length; i++)
        {
            var mission = missions[i];
            int index = i;

            _spyButtons.Add(new Button(
                new Rectangle(startX, startY + i * (buttonHeight + spacing), buttonWidth, buttonHeight),
                mission.ToString(),
                Color.DarkRed,
                () => ExecuteSpyMission(mission)
            ));
        }
    }

    private void ExecuteSpyMission(SpyMission mission)
    {
        if (_selectedCiv == null || _targetCiv == null) return;

        var result = _civManager.DivinePowers.SendSpies(_selectedCiv, _targetCiv, mission);
        ShowMessage(result.Message);

        _currentMode = DivinePowerMode.None;
        _selectedCiv = null;
        _targetCiv = null;
    }

    private void ShowMessage(string message)
    {
        _statusMessage = message;
        _messageTimer = 5.0f; // Show for 5 seconds
    }

    public void Update(MouseState mouseState, float deltaTime)
    {
        // Update message timer
        if (_messageTimer > 0)
        {
            _messageTimer -= deltaTime;
        }

        if (mouseState.LeftButton == ButtonState.Pressed &&
            _previousMouseState.LeftButton == ButtonState.Released)
        {
            var mousePos = new Point(mouseState.X, mouseState.Y);

            // Update button positions based on panel position
            int panelX = 440; // Similar offset to disaster control
            int panelY = 45;  // Below toolbar

            // We need to offset the button bounds for hit testing because we draw them relative to panel
            Point relativeMousePos = new Point(mousePos.X - panelX, mousePos.Y - panelY);

            // Main buttons
            foreach (var button in _mainButtons)
            {
                 // Check absolute bounds since buttons are stored with relative coords?
                 // Actually, simpler to just store absolute bounds or adjust here.
                 // Let's assume button bounds are stored relative to screen for now in previous code,
                 // but I changed them to be relative to 0,0 in Initialize.
                 // Wait, the previous code had hardcoded screen coordinates.
                 // Let's adjust the bounds in Draw and hit test against those adjusted bounds.
                 // Better: Update button bounds to be correct screen coordinates in Initialize/Update.

                 // Correction: I will update button bounds dynamically in Draw/Update or make them relative.
                 // Let's stick to absolute coordinates for simplicity in hit testing, but calculate them based on panel pos.

                 Rectangle absBounds = new Rectangle(button.Bounds.X + panelX, button.Bounds.Y + panelY, button.Bounds.Width, button.Bounds.Height);
                 if (absBounds.Contains(mousePos))
                 {
                     button.OnClick?.Invoke();
                     break;
                 }
            }

            // Civilization selection buttons (Panel 2)
            if (_currentMode != DivinePowerMode.None)
            {
                foreach (var button in _civButtons)
                {
                    Rectangle absBounds = new Rectangle(button.Bounds.X + panelX, button.Bounds.Y + panelY, button.Bounds.Width, button.Bounds.Height);
                    if (absBounds.Contains(mousePos))
                    {
                        button.OnClick?.Invoke();
                        break;
                    }
                }
            }

            // Sub-menu buttons (Panel 3)
            if ((_currentMode == DivinePowerMode.ChangeGovernment && _selectedCiv != null) ||
                (_spyButtons.Count > 0 && _currentMode == DivinePowerMode.SendSpies)) // Fix logic here
            {
                var buttonsToCheck = _currentMode == DivinePowerMode.ChangeGovernment ? _govButtons : _spyButtons;
                foreach (var button in buttonsToCheck)
                {
                    Rectangle absBounds = new Rectangle(button.Bounds.X + panelX, button.Bounds.Y + panelY, button.Bounds.Width, button.Bounds.Height);
                    if (absBounds.Contains(mousePos))
                    {
                        button.OnClick?.Invoke();
                        break;
                    }
                }
            }
        }

        _previousMouseState = mouseState;
    }

    public void Draw(SpriteBatch spriteBatch, int screenWidth, int screenHeight)
    {
        if (!IsOpen) return;

        int panelX = 440; // Offset from left (info panel + gap)
        int panelY = 45;  // Below toolbar

        // Draw Main Panel
        DrawPanel(spriteBatch, panelX, panelY, PanelWidth, PanelHeight, "DIVINE POWERS");

        // Draw main buttons
        foreach (var button in _mainButtons)
        {
            // Offset button position by panel position
            Rectangle drawRect = new Rectangle(button.Bounds.X + panelX, button.Bounds.Y + panelY, button.Bounds.Width, button.Bounds.Height);
            DrawButton(spriteBatch, drawRect, button.Text, button.Color);
        }

        // Draw Secondary Panel (Civ Selection)
        if (_currentMode != DivinePowerMode.None)
        {
            int secPanelX = panelX + PanelWidth + 10;
            DrawPanel(spriteBatch, secPanelX, panelY, PanelWidth, PanelHeight, "SELECT TARGET");

            foreach (var button in _civButtons)
            {
                Rectangle drawRect = new Rectangle(button.Bounds.X + panelX, button.Bounds.Y + panelY, button.Bounds.Width, button.Bounds.Height);
                DrawButton(spriteBatch, drawRect, button.Text, button.Color);
            }
        }

        // Draw Tertiary Panel (Details/Actions)
        if ((_currentMode == DivinePowerMode.ChangeGovernment && _selectedCiv != null) ||
            (_currentMode == DivinePowerMode.SendSpies && _selectedCiv != null && _targetCiv != null))
        {
            int terPanelX = panelX + (PanelWidth + 10) * 2;
            string title = _currentMode == DivinePowerMode.ChangeGovernment ? "NEW GOV" : "MISSION";
            DrawPanel(spriteBatch, terPanelX, panelY, PanelWidth, PanelHeight, title);

            var buttonsToDraw = _currentMode == DivinePowerMode.ChangeGovernment ? _govButtons : _spyButtons;
            foreach (var button in buttonsToDraw)
            {
                Rectangle drawRect = new Rectangle(button.Bounds.X + panelX, button.Bounds.Y + panelY, button.Bounds.Width, button.Bounds.Height);
                DrawButton(spriteBatch, drawRect, button.Text, button.Color);
            }
        }

        // Status message
        if (_messageTimer > 0 && !string.IsNullOrEmpty(_statusMessage))
        {
            var msgSize = _font.MeasureString(_statusMessage);
            var msgPos = new Vector2((screenWidth - msgSize.X) / 2, screenHeight - 100);

            // Background
            spriteBatch.Draw(_pixelTexture,
                new Rectangle((int)msgPos.X - 10, (int)msgPos.Y - 5, (int)msgSize.X + 20, (int)msgSize.Y + 10),
                new Color(0, 0, 0, 200));

            _font.DrawString(spriteBatch, _statusMessage, msgPos, Color.Yellow);
        }
    }

    private void DrawPanel(SpriteBatch spriteBatch, int x, int y, int width, int height, string title)
    {
        // Background
        spriteBatch.Draw(_pixelTexture, new Rectangle(x, y, width, height), _panelBgColor);

        // Border
        DrawBorder(spriteBatch, x, y, width, height, _borderColor, 2);

        // Title
        _font.DrawString(spriteBatch, title, new Vector2(x + 15, y + 10), _borderColor);

        // Separator
        spriteBatch.Draw(_pixelTexture, new Rectangle(x + 5, y + 35, width - 10, 1), _borderColor);
    }

    private void DrawButton(SpriteBatch spriteBatch, Rectangle bounds, string text, Color color)
    {
        // Button background
        spriteBatch.Draw(_pixelTexture, bounds, new Color(color, 0.7f));

        // Button border
        DrawBorder(spriteBatch, bounds.X, bounds.Y, bounds.Width, bounds.Height, Color.White, 1);

        // Button text
        var textSize = _font.MeasureString(text);
        // Scale down text if too wide
        float scale = 1.0f;
        if (textSize.X > bounds.Width - 10)
        {
            // Simple workaround since font renderer doesn't support scaling yet: truncate
            // Or just let it overflow slightly/clip
        }

        var textPos = new Vector2(
            bounds.X + (bounds.Width - textSize.X) / 2,
            bounds.Y + (bounds.Height - textSize.Y) / 2
        );
        _font.DrawString(spriteBatch, text, textPos, Color.White);
    }

    private void DrawBorder(SpriteBatch spriteBatch, int x, int y, int width, int height, Color color, int thickness)
    {
        spriteBatch.Draw(_pixelTexture, new Rectangle(x, y, width, thickness), color);
        spriteBatch.Draw(_pixelTexture, new Rectangle(x, y + height - thickness, width, thickness), color);
        spriteBatch.Draw(_pixelTexture, new Rectangle(x, y, thickness, height), color);
        spriteBatch.Draw(_pixelTexture, new Rectangle(x + width - thickness, y, thickness, height), color);
    }

    private Color GetCivColor(Civilization civ)
    {
        return civ.CivType switch
        {
            CivType.Tribal => new Color(139, 90, 43),
            CivType.Agricultural => new Color(100, 160, 80),
            CivType.Industrial => new Color(120, 120, 120),
            CivType.Scientific => new Color(100, 149, 237),
            CivType.Spacefaring => new Color(147, 112, 219),
            _ => Color.White
        };
    }

    private Color GetGovernmentColor(GovernmentType type)
    {
        return type switch
        {
            GovernmentType.Tribal => Color.Brown,
            GovernmentType.Monarchy => Color.Purple,
            GovernmentType.Dynasty => Color.Gold,
            GovernmentType.Theocracy => Color.Cyan,
            GovernmentType.Republic => Color.Blue,
            GovernmentType.Democracy => Color.Green,
            GovernmentType.Oligarchy => Color.Orange,
            GovernmentType.Dictatorship => Color.Red,
            GovernmentType.Federation => Color.LightBlue,
            _ => Color.White
        };
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

public enum DivinePowerMode
{
    None,
    ChangeGovernment,
    SendSpies,
    ForceBetray,
    Bless,
    Curse,
    ForceAlliance,
    ForceWar,
    AdvanceCivilization
}
