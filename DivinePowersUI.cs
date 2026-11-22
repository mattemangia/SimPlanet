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

    // UI layout (screen coordinates, shared by Update and Draw)
    private const int PanelX = 530;      // right of the info panel and the disaster panel
    private const int PanelY = 56;       // below the toolbar
    private const int PanelWidth = 240;
    private const int PanelGap = 10;
    private const int ButtonHeight = 28;
    private const int ButtonSpacing = 4;
    private const int ContentPadding = 12;
    private const int HintHeight = 40;   // room for the two-line hint above a list of choices

    private static readonly (string Title, int First, int Count)[] MainSections =
    {
        ("ONE NATION", 0, 4),
        ("BETWEEN TWO NATIONS", 4, 4),
        ("THE WHOLE WORLD", 8, 2),
    };

    private readonly List<(string Title, int Y)> _mainSectionHeaders = new();
    private Rectangle _mainPanel;
    private Rectangle _closeRect;

    public DivinePowersUI(GraphicsDevice graphicsDevice, FontRenderer font, CivilizationManager civManager)
    {
        _graphicsDevice = graphicsDevice;
        _font = font;
        _civManager = civManager;

        InitializeMainButtons();
    }

    private void InitializeMainButtons()
    {
        // Bounds are assigned by LayoutMain(); the order matches MainSections.
        _mainButtons = new List<Button>
        {
            new Button("Change Government", Color.MediumPurple, () => OpenMode(DivinePowerMode.ChangeGovernment), DivinePowerMode.ChangeGovernment),
            new Button("Bless Civilization", Color.Gold, () => OpenMode(DivinePowerMode.Bless), DivinePowerMode.Bless),
            new Button("Curse Civilization", new Color(150, 150, 170), () => OpenMode(DivinePowerMode.Curse), DivinePowerMode.Curse),
            new Button("Advance Civilization", Color.CornflowerBlue, () => OpenMode(DivinePowerMode.AdvanceCivilization), DivinePowerMode.AdvanceCivilization),

            new Button("Send Spies", new Color(200, 70, 70), () => OpenMode(DivinePowerMode.SendSpies), DivinePowerMode.SendSpies),
            new Button("Force Betrayal", Color.OrangeRed, () => OpenMode(DivinePowerMode.ForceBetray), DivinePowerMode.ForceBetray),
            new Button("Force Alliance", new Color(90, 200, 110), () => OpenMode(DivinePowerMode.ForceAlliance), DivinePowerMode.ForceAlliance),
            new Button("Force War", new Color(255, 70, 60), () => OpenMode(DivinePowerMode.ForceWar), DivinePowerMode.ForceWar),

            new Button("Instant Peace", new Color(120, 200, 255), () =>
                {
                    _currentMode = DivinePowerMode.None;
                    _civButtons.Clear();
                    PeaceRequested?.Invoke();
                    ShowMessage("All wars end. The nations swear 30 years of peace.");
                }),
            new Button("Armageddon", new Color(230, 50, 40), () => OpenMode(DivinePowerMode.Armageddon), DivinePowerMode.Armageddon),
        };
        LayoutMain();
    }

    private void LayoutMain()
    {
        int x = PanelX + ContentPadding;
        int width = PanelWidth - ContentPadding * 2;
        int y = PanelY + UIWidgets.HeaderHeight + 10;

        _mainSectionHeaders.Clear();
        foreach (var section in MainSections)
        {
            _mainSectionHeaders.Add((section.Title, y));
            y += 22;
            for (int i = section.First; i < section.First + section.Count; i++)
            {
                _mainButtons[i].Bounds = new Rectangle(x, y, width, ButtonHeight);
                y += ButtonHeight + ButtonSpacing;
            }
            y += 8;
        }

        _mainPanel = new Rectangle(PanelX, PanelY, PanelWidth, y + 2 - PanelY);
        _closeRect = UIWidgets.CloseButtonRect(_mainPanel);
    }

    /// <summary>X of the second (target) and third (details) panels.</summary>
    private static int SecondPanelX => PanelX + PanelWidth + PanelGap;
    private static int ThirdPanelX => PanelX + (PanelWidth + PanelGap) * 2;

    /// <summary>Where the first choice button of a side panel goes.</summary>
    private static int ListTop => PanelY + UIWidgets.HeaderHeight + 10 + HintHeight;

    private static Rectangle ListButtonRect(int panelX, int index)
    {
        return new Rectangle(panelX + ContentPadding, ListTop + index * (ButtonHeight + ButtonSpacing),
            PanelWidth - ContentPadding * 2, ButtonHeight);
    }

    private static Rectangle ListPanelRect(int panelX, int rows)
    {
        int height = ListTop - PanelY + Math.Max(1, rows) * (ButtonHeight + ButtonSpacing) + 10;
        return new Rectangle(panelX, PanelY, PanelWidth, height);
    }

    private const int ArmageddonButtonsOffset = 262;
    private const int ArmageddonButtonsY = PanelY + ArmageddonButtonsOffset;
    private static Rectangle ArmageddonPanelRect => new Rectangle(SecondPanelX, PanelY, PanelWidth, ArmageddonButtonsOffset + 34 + 6 + 28 + 14);

    /// <summary>
    /// Invoked when the player confirms Armageddon (global nuclear war). The game runs it
    /// under the simulation lock.
    /// </summary>
    public Action? ArmageddonRequested { get; set; }

    /// <summary>
    /// Invoked when the player grants instant world peace. The game runs it under the simulation lock.
    /// </summary>
    public Action? PeaceRequested { get; set; }

    private void OpenMode(DivinePowerMode mode)
    {
        _currentMode = mode;
        _selectedCiv = null;
        _targetCiv = null;

        if (mode == DivinePowerMode.Armageddon)
        {
            // Confirmation step instead of a target list
            int x = SecondPanelX + ContentPadding;
            int width = PanelWidth - ContentPadding * 2;
            _civButtons.Clear();
            _civButtons.Add(new Button("LAUNCH EVERYTHING", new Color(230, 50, 40), () =>
            {
                ArmageddonRequested?.Invoke();
                ShowMessage("The missiles fly. May the survivors forgive you.");
                _currentMode = DivinePowerMode.None;
                _civButtons.Clear();
            }) { Bounds = new Rectangle(x, ArmageddonButtonsY, width, 34) });
            _civButtons.Add(new Button("Cancel", new Color(150, 150, 170), () =>
            {
                _currentMode = DivinePowerMode.None;
                _civButtons.Clear();
            }) { Bounds = new Rectangle(x, ArmageddonButtonsY + 34 + 6, width, 28) });
            return;
        }

        // Build civilization selection buttons
        BuildCivButtons();
    }

    private void BuildCivButtons()
    {
        _civButtons.Clear();

        // Only as many nations as fit on screen get a button
        int maxRows = Math.Max(1, (_graphicsDevice.Viewport.Height - 20 - ListTop) / (ButtonHeight + ButtonSpacing));
        var civilizations = _civManager.Civilizations;
        for (int i = 0; i < civilizations.Count && i < maxRows; i++)
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
                buttonText,
                TerrainRenderer.GetCivPaletteColor(civ.Id),
                () => SelectCivilization(civilizations[index])
            ) { Bounds = ListButtonRect(SecondPanelX, i), Civ = civ });
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

        var govTypes = Enum.GetValues<GovernmentType>();
        for (int i = 0; i < govTypes.Length; i++)
        {
            var govType = govTypes[i];

            _govButtons.Add(new Button(
                govType.ToString(),
                GetGovernmentColor(govType),
                () => ChangeGovernment(govType)
            ) { Bounds = ListButtonRect(ThirdPanelX, i) });
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

        var missions = Enum.GetValues<SpyMission>();
        for (int i = 0; i < missions.Length; i++)
        {
            var mission = missions[i];

            _spyButtons.Add(new Button(
                SplitWords(mission.ToString()),
                new Color(200, 70, 70),
                () => ExecuteSpyMission(mission)
            ) { Bounds = ListButtonRect(ThirdPanelX, i) });
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

        // A closed panel has no buttons to press
        if (IsOpen &&
            mouseState.LeftButton == ButtonState.Pressed &&
            _previousMouseState.LeftButton == ButtonState.Released)
        {
            var mousePos = new Point(mouseState.X, mouseState.Y);
            LayoutMain();

            if (_closeRect.Contains(mousePos))
            {
                IsOpen = false;
                _previousMouseState = mouseState;
                return;
            }

            // Main buttons
            foreach (var button in _mainButtons)
            {
                if (button.Bounds.Contains(mousePos))
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
                    if (button.Bounds.Contains(mousePos))
                    {
                        button.OnClick?.Invoke();
                        break;
                    }
                }
            }

            // Sub-menu buttons (Panel 3)
            if (ThirdPanelVisible)
            {
                var buttonsToCheck = _currentMode == DivinePowerMode.ChangeGovernment ? _govButtons : _spyButtons;
                foreach (var button in buttonsToCheck)
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

    private bool ThirdPanelVisible =>
        (_currentMode == DivinePowerMode.ChangeGovernment && _selectedCiv != null) ||
        (_currentMode == DivinePowerMode.SendSpies && _selectedCiv != null && _targetCiv != null);

    private static bool NeedsTwoNations(DivinePowerMode mode) =>
        mode == DivinePowerMode.SendSpies || mode == DivinePowerMode.ForceBetray ||
        mode == DivinePowerMode.ForceAlliance || mode == DivinePowerMode.ForceWar;

    private static string ModeName(DivinePowerMode mode)
    {
        return mode switch
        {
            DivinePowerMode.ChangeGovernment => "Change government",
            DivinePowerMode.SendSpies => "Send spies",
            DivinePowerMode.ForceBetray => "Force betrayal",
            DivinePowerMode.Bless => "Bless",
            DivinePowerMode.Curse => "Curse",
            DivinePowerMode.ForceAlliance => "Force alliance",
            DivinePowerMode.ForceWar => "Force war",
            DivinePowerMode.AdvanceCivilization => "Advance",
            _ => mode.ToString()
        };
    }

    /// <summary>"StealTechnology" -> "Steal Technology".</summary>
    private static string SplitWords(string name)
    {
        var sb = new System.Text.StringBuilder(name.Length + 4);
        for (int i = 0; i < name.Length; i++)
        {
            if (i > 0 && char.IsUpper(name[i]) && !char.IsUpper(name[i - 1])) sb.Append(' ');
            sb.Append(name[i]);
        }
        return sb.ToString();
    }

    public void Draw(SpriteBatch spriteBatch, int screenWidth, int screenHeight)
    {
        if (!IsOpen) return;

        LayoutMain();

        // Main panel
        UIWidgets.DrawWindow(spriteBatch, _mainPanel, "DIVINE POWERS", UITheme.Gold);
        foreach (var header in _mainSectionHeaders)
            UIWidgets.DrawSection(spriteBatch, PanelX + ContentPadding, header.Y, PanelWidth - ContentPadding * 2, header.Title);
        foreach (var button in _mainButtons)
        {
            bool active = button.Mode != DivinePowerMode.None && button.Mode == _currentMode;
            UIWidgets.DrawActionButton(spriteBatch, button.Bounds, button.Text, button.Color, active);
        }

        // Secondary panel (target selection or the Armageddon confirmation)
        if (_currentMode != DivinePowerMode.None)
        {
            int x = SecondPanelX + ContentPadding;
            int width = PanelWidth - ContentPadding * 2;
            int hintY = PanelY + UIWidgets.HeaderHeight + 8;

            if (_currentMode == DivinePowerMode.Armageddon)
            {
                UIWidgets.DrawWindow(spriteBatch, ArmageddonPanelRect, "ARMAGEDDON?", UITheme.Bad, closeButton: false);
                DrawArmageddonWarning(spriteBatch, SecondPanelX, PanelY);
                UIWidgets.DrawActionButton(spriteBatch, _civButtons[0].Bounds, _civButtons[0].Text, _civButtons[0].Color);
                UIWidgets.DrawButton(spriteBatch, _civButtons[1].Bounds, _civButtons[1].Text);
            }
            else
            {
                bool two = NeedsTwoNations(_currentMode);
                bool pickingTarget = two && _selectedCiv != null;
                string title = !two ? "CHOOSE A NATION" : pickingTarget ? "AGAINST WHOM?" : "WHO ACTS?";
                UIWidgets.DrawWindow(spriteBatch, ListPanelRect(SecondPanelX, _civButtons.Count), title, UITheme.Gold, closeButton: false);

                string hint = !two ? ModeName(_currentMode) + ": pick the nation it falls on."
                    : pickingTarget ? $"{_selectedCiv!.Name} acts. Now pick the other nation."
                    : ModeName(_currentMode) + ": first pick the nation that acts.";
                DrawHint(spriteBatch, x, hintY, width, hint);

                if (_civButtons.Count == 0)
                    UITheme.DrawText(spriteBatch, "No nations exist yet", new Vector2(x, ListTop + 4), UITheme.TextMuted, UITheme.FontSmall);

                foreach (var button in _civButtons)
                {
                    bool chosen = button.Civ != null && (button.Civ == _selectedCiv || button.Civ == _targetCiv);
                    UIWidgets.DrawActionButton(spriteBatch, button.Bounds, button.Text, button.Color, chosen, centered: false, fontSize: 13f);
                }
            }
        }

        // Tertiary panel (new government or spy mission)
        if (ThirdPanelVisible)
        {
            bool government = _currentMode == DivinePowerMode.ChangeGovernment;
            var buttonsToDraw = government ? _govButtons : _spyButtons;
            UIWidgets.DrawWindow(spriteBatch, ListPanelRect(ThirdPanelX, buttonsToDraw.Count),
                government ? "NEW GOVERNMENT" : "SPY MISSION", UITheme.Gold, closeButton: false);
            string hint = government
                ? $"How shall {_selectedCiv!.Name} be ruled from now on?"
                : $"What should the spies of {_selectedCiv!.Name} do in {_targetCiv!.Name}?";
            DrawHint(spriteBatch, ThirdPanelX + ContentPadding, PanelY + UIWidgets.HeaderHeight + 8, PanelWidth - ContentPadding * 2, hint);

            foreach (var button in buttonsToDraw)
            {
                bool current = government && _selectedCiv?.Government != null && button.Text == _selectedCiv.Government.Type.ToString();
                UIWidgets.DrawActionButton(spriteBatch, button.Bounds, button.Text, button.Color, current, centered: false);
            }
        }

        // Status message
        if (_messageTimer > 0 && !string.IsNullOrEmpty(_statusMessage))
        {
            UIWidgets.DrawToast(spriteBatch, _statusMessage, screenWidth, screenHeight - 150);
        }
    }

    /// <summary>Two-line hint above a list of choices (extra text is cut with an ellipsis).</summary>
    private static void DrawHint(SpriteBatch spriteBatch, int x, int y, int width, string text)
    {
        var lines = UITheme.WrapText(text, width, UITheme.FontSmall);
        for (int i = 0; i < lines.Count && i < 2; i++)
        {
            string line = i == 1 && lines.Count > 2 ? UITheme.Ellipsize(lines[1] + " " + lines[2], width, UITheme.FontSmall) : lines[i];
            UITheme.DrawText(spriteBatch, line, new Vector2(x, y + i * 16), UITheme.TextDim, UITheme.FontSmall);
        }
    }

    private void DrawArmageddonWarning(SpriteBatch spriteBatch, int x, int y)
    {
        var data = CivRenderData.Latest;
        int powers = data.Civs.Count(c => c.HasNuclearWeapons && c.NuclearWarheads > 0);
        int warheads = data.Civs.Sum(c => c.NuclearWarheads);
        int left = x + ContentPadding;
        int width = PanelWidth - ContentPadding * 2;
        int ty = y + UIWidgets.HeaderHeight + 10;
        var icons = MapIcons.GetShared(_graphicsDevice);
        spriteBatch.Draw(icons.Trefoil, new Rectangle(x + PanelWidth / 2 - 22, ty, 44, 44), Color.White);
        ty += 52;
        string text = "Every nuclear power declares war on its rivals and launches its whole arsenal. " +
                      "Cities burn, fallout spreads and soot darkens the sky for years (nuclear winter). This cannot be undone.";
        int limit = y + ArmageddonButtonsOffset - 8 - 2 * UIWidgets.RowHeight - 18;
        foreach (var line in UITheme.WrapText(text, width, UITheme.FontSmall))
        {
            if (ty + 16 > limit) break;
            UITheme.DrawText(spriteBatch, line, new Vector2(left, ty), UITheme.Text, UITheme.FontSmall);
            ty += 16;
        }
        ty = limit + 4;
        Color c = powers > 0 ? UITheme.Bad : UITheme.TextMuted;
        ty = UIWidgets.DrawKeyValue(spriteBatch, left, ty, width, "Nuclear powers", powers.ToString(), c, UITheme.FontSmall) - 3;
        ty = UIWidgets.DrawKeyValue(spriteBatch, left, ty, width, "Warheads", warheads.ToString("N0"), c, UITheme.FontSmall) - 3;
        if (powers == 0)
            UITheme.DrawText(spriteBatch, "No nation has the bomb yet", new Vector2(left, ty), UITheme.TextMuted, UITheme.FontSmall);
    }

    private Color GetGovernmentColor(GovernmentType type)
    {
        return type switch
        {
            GovernmentType.Tribal => new Color(190, 130, 80),
            GovernmentType.Monarchy => Color.MediumPurple,
            GovernmentType.Dynasty => Color.Gold,
            GovernmentType.Theocracy => Color.Cyan,
            GovernmentType.Republic => Color.CornflowerBlue,
            GovernmentType.Democracy => new Color(90, 200, 110),
            GovernmentType.Oligarchy => Color.Orange,
            GovernmentType.Dictatorship => new Color(255, 70, 60),
            GovernmentType.Federation => Color.LightBlue,
            _ => Color.White
        };
    }

    private class Button
    {
        public Rectangle Bounds { get; set; }
        public string Text { get; }
        public Color Color { get; }
        public Action? OnClick { get; }
        public DivinePowerMode Mode { get; }
        public Civilization? Civ { get; set; }

        public Button(string text, Color color, Action? onClick, DivinePowerMode mode = DivinePowerMode.None)
        {
            Text = text;
            Color = color;
            OnClick = onClick;
            Mode = mode;
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
    AdvanceCivilization,
    Armageddon
}
