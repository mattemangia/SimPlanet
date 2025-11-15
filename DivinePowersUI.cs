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
        int buttonHeight = 35;
        int startX = 250;
        int startY = 100;
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
                "Close", Color.Gray, () => IsOpen = false)
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

        int buttonWidth = 300;
        int buttonHeight = 35;
        int startX = 500;
        int startY = 100;
        int spacing = 5;

        var civilizations = _civManager.Civilizations;
        for (int i = 0; i < civilizations.Count; i++)
        {
            var civ = civilizations[i];
            int index = i;

            string buttonText = $"{civ.Name} - {civ.Government?.Type ?? GovernmentType.Tribal}";
            if (civ.Government?.CurrentRuler != null)
            {
                buttonText += $" ({civ.Government.CurrentRuler.Name})";
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
        int buttonHeight = 35;
        int startX = 850;
        int startY = 100;
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
        int buttonHeight = 35;
        int startX = 850;
        int startY = 100;
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

            // Main buttons
            foreach (var button in _mainButtons)
            {
                if (button.Bounds.Contains(mousePos))
                {
                    button.OnClick?.Invoke();
                    break;
                }
            }

            // Civilization selection buttons
            if (_currentMode != DivinePowerMode.None)
            {
                foreach (var button in _civButtons)
                {
                    if (button.Bounds.Contains(mousePos))
                    {
                        button.OnClick?.Invoke();
                        break;
                    }
                }
            }

            // Government buttons
            if (_currentMode == DivinePowerMode.ChangeGovernment && _selectedCiv != null)
            {
                foreach (var button in _govButtons)
                {
                    if (button.Bounds.Contains(mousePos))
                    {
                        button.OnClick?.Invoke();
                        break;
                    }
                }
            }

            // Spy mission buttons
            if (_spyButtons.Count > 0)
            {
                foreach (var button in _spyButtons)
                {
                    if (button.Bounds.Contains(mousePos))
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

        // Background overlay
        spriteBatch.Draw(_pixelTexture,
            new Rectangle(0, 0, screenWidth, screenHeight),
            new Color(0, 0, 0, 200));

        // Title
        _font.DrawString(spriteBatch, "=== DIVINE POWERS ===",
            new Vector2(screenWidth / 2 - 120, 30), Color.Gold);

        // Instructions
        string instruction = _currentMode switch
        {
            DivinePowerMode.None => "Select a divine power to use",
            DivinePowerMode.ChangeGovernment when _selectedCiv == null => "Select civilization to change",
            DivinePowerMode.ChangeGovernment => "Select new government type",
            DivinePowerMode.SendSpies when _selectedCiv == null => "Select source civilization",
            DivinePowerMode.SendSpies when _targetCiv == null => "Select target civilization",
            DivinePowerMode.SendSpies => "Select spy mission",
            DivinePowerMode.ForceBetray when _selectedCiv == null => "Select betrayer civilization",
            DivinePowerMode.ForceBetray => "Select victim civilization",
            DivinePowerMode.Bless => "Select civilization to bless",
            DivinePowerMode.Curse => "Select civilization to curse",
            DivinePowerMode.ForceAlliance when _selectedCiv == null => "Select first civilization",
            DivinePowerMode.ForceAlliance => "Select second civilization",
            DivinePowerMode.ForceWar when _selectedCiv == null => "Select aggressor",
            DivinePowerMode.ForceWar => "Select target",
            _ => ""
        };

        _font.DrawString(spriteBatch, instruction,
            new Vector2(screenWidth / 2 - 150, 60), Color.White);

        // Draw main buttons
        foreach (var button in _mainButtons)
        {
            DrawButton(spriteBatch, button);
        }

        // Draw civilization buttons if in selection mode
        if (_currentMode != DivinePowerMode.None)
        {
            foreach (var button in _civButtons)
            {
                DrawButton(spriteBatch, button);
            }
        }

        // Draw government buttons if changing government
        if (_currentMode == DivinePowerMode.ChangeGovernment && _selectedCiv != null)
        {
            foreach (var button in _govButtons)
            {
                DrawButton(spriteBatch, button);
            }
        }

        // Draw spy mission buttons
        if (_spyButtons.Count > 0)
        {
            foreach (var button in _spyButtons)
            {
                DrawButton(spriteBatch, button);
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

    private void DrawButton(SpriteBatch spriteBatch, Button button)
    {
        // Button background
        spriteBatch.Draw(_pixelTexture, button.Bounds, new Color(button.Color, 0.7f));

        // Button border
        DrawBorder(spriteBatch, button.Bounds.X, button.Bounds.Y,
            button.Bounds.Width, button.Bounds.Height, Color.White, 2);

        // Button text
        var textSize = _font.MeasureString(button.Text);
        var textPos = new Vector2(
            button.Bounds.X + (button.Bounds.Width - textSize.X) / 2,
            button.Bounds.Y + (button.Bounds.Height - textSize.Y) / 2
        );
        _font.DrawString(spriteBatch, button.Text, textPos, Color.White);
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
    ForceWar
}
