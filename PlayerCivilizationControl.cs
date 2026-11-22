using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace SimPlanet;

/// <summary>
/// Allows player to take control of a civilization
/// </summary>
public class PlayerCivilizationControl
{
    private readonly GraphicsDevice _graphicsDevice;
    private readonly FontRenderer _font;
    private readonly CivilizationManager _civManager;
    private Texture2D _pixelTexture;
    private MouseState _previousMouseState;

    public Civilization? PlayerCivilization { get; private set; } = null;
    public bool ShowControlPanel { get; set; } = false;
    public bool ShowCivSelector { get; set; } = false;

    private List<Button> _controlButtons = new();
    private List<Button> _selectorButtons = new();

    public PlayerCivilizationControl(GraphicsDevice graphicsDevice, FontRenderer font, CivilizationManager civManager)
    {
        _graphicsDevice = graphicsDevice;
        _font = font;
        _civManager = civManager;

        _pixelTexture = new Texture2D(_graphicsDevice, 1, 1);
        _pixelTexture.SetData(new[] { Color.White });

        InitializeControlButtons();
    }

    private void InitializeControlButtons()
    {
        // Bounds are assigned by LayoutControlPanel(), which Update and Draw both call.
        _controlButtons = new List<Button>
        {
            new Button("Declare War", UITheme.Bad, DeclareWar),
            new Button("Make Peace", UITheme.Good, MakePeace),
            new Button("Join Climate Pact", Color.Cyan, JoinClimatePact),
            new Button("Build Nuclear", Color.Orange, BuildNuclear),
            new Button("Increase Eco", Color.LightGreen, IncreaseEcoFriendliness),
            new Button("Release Control", new Color(150, 150, 170), ReleaseControl)
        };
        LayoutControlPanel();
    }

    // ---- Layout (shared by Update and Draw) ---------------------------------------------

    private const int ControlPanelWidth = 264;
    private const int ControlPanelX = 290;     // right of the info panel
    private const int ControlContentHeight = 4 * UIWidgets.RowHeight + 8      // nation
                                           + 22 + 4 * UIWidgets.RowHeight + 8 // resources
                                           + 22 + 8                           // status badges
                                           + 22;                              // orders caption
    private Rectangle _controlPanel;

    private void LayoutControlPanel()
    {
        const int buttonHeight = 28, gap = 6;
        int rows = (_controlButtons.Count + 1) / 2;
        int height = UIWidgets.HeaderHeight + 10 + ControlContentHeight + rows * (buttonHeight + gap) + 6;
        int y = Math.Max(56, _graphicsDevice.Viewport.Height - height - 24);
        _controlPanel = new Rectangle(ControlPanelX, y, ControlPanelWidth, height);

        int x = _controlPanel.X + 12;
        int half = (ControlPanelWidth - 24 - gap) / 2;
        int by = _controlPanel.Y + UIWidgets.HeaderHeight + 10 + ControlContentHeight;
        for (int i = 0; i < _controlButtons.Count; i++)
        {
            _controlButtons[i].Bounds = new Rectangle(x + (i % 2) * (half + gap), by + (i / 2) * (buttonHeight + gap), half, buttonHeight);
        }
    }

    private Rectangle _selectorPanel;
    private Rectangle _selectorClose;

    public void Update(MouseState mouseState)
    {
        if (mouseState.LeftButton == ButtonState.Pressed &&
            _previousMouseState.LeftButton == ButtonState.Released)
        {
            var mousePos = new Point(mouseState.X, mouseState.Y);

            // Handle civilization selector
            if (ShowCivSelector)
            {
                if (_selectorClose.Contains(mousePos))
                {
                    ShowCivSelector = false;
                }
                else
                {
                    foreach (var button in _selectorButtons)
                    {
                        if (button.Bounds.Contains(mousePos))
                        {
                            button.OnClick?.Invoke();
                            break;
                        }
                    }
                }
            }

            // Handle control panel
            if (ShowControlPanel && PlayerCivilization != null)
            {
                LayoutControlPanel();
                foreach (var button in _controlButtons)
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

    public void OpenCivilizationSelector()
    {
        ShowCivSelector = true;
        _selectorButtons.Clear();

        const int panelWidth = 380, rowHeight = 40, spacing = 4;
        int screenWidth = _graphicsDevice.Viewport.Width;
        int screenHeight = _graphicsDevice.Viewport.Height;

        var civilizations = _civManager.GetAllCivilizations();
        // Only as many nations as fit on screen get a row
        int maxRows = Math.Max(1, (screenHeight - 120 - UIWidgets.HeaderHeight - 70) / (rowHeight + spacing));
        int rows = Math.Min(civilizations.Count, maxRows);

        int listHeight = Math.Max(1, rows) * (rowHeight + spacing);
        int panelHeight = UIWidgets.HeaderHeight + 12 + listHeight + 8 + 32 + 14;
        _selectorPanel = new Rectangle((screenWidth - panelWidth) / 2, Math.Max(56, (screenHeight - panelHeight) / 2 - 20), panelWidth, panelHeight);
        _selectorClose = UIWidgets.CloseButtonRect(_selectorPanel);

        int startX = _selectorPanel.X + 14;
        int startY = _selectorPanel.Y + UIWidgets.HeaderHeight + 12;
        int buttonWidth = panelWidth - 28;

        for (int i = 0; i < rows; i++)
        {
            var civ = civilizations[i];
            int index = i; // Capture for lambda

            _selectorButtons.Add(new Button(
                civ.Name,
                TerrainRenderer.GetCivPaletteColor(civ.Id),
                () => SelectCivilization(civilizations[index])
            ) { Bounds = new Rectangle(startX, startY + i * (rowHeight + spacing), buttonWidth, rowHeight), Civ = civ });
        }

        // Add cancel button
        _selectorButtons.Add(new Button(
            "Cancel",
            Color.Gray,
            () => ShowCivSelector = false
        ) { Bounds = new Rectangle(_selectorPanel.Right - 14 - 110, startY + listHeight + 8, 110, 32) });
    }

    private void SelectCivilization(Civilization civ)
    {
        PlayerCivilization = civ;
        ShowCivSelector = false;
        ShowControlPanel = true;
    }

    private void DeclareWar()
    {
        if (PlayerCivilization == null) return;

        // Attack the closest neighbour we are not already fighting
        var player = PlayerCivilization;
        var otherCivs = _civManager.GetAllCivilizations()
            .Where(c => c.Id != player.Id &&
                        !(player.DiplomaticRelations.TryGetValue(c.Id, out var r) && r.Status == DiplomaticStatus.War))
            .OrderBy(c => Math.Pow(c.CenterX - player.CenterX, 2) + Math.Pow(c.CenterY - player.CenterY, 2))
            .ToList();

        if (otherCivs.Any())
        {
            var target = otherCivs.First();
            PlayerCivilization.AtWar = true;
            PlayerCivilization.WarTargetId = target.Id;
        }
    }

    private void MakePeace()
    {
        if (PlayerCivilization == null) return;
        PlayerCivilization.AtWar = false;
        PlayerCivilization.WarTargetId = null;
    }

    private void JoinClimatePact()
    {
        if (PlayerCivilization == null) return;

        if (!PlayerCivilization.InClimateAgreement)
        {
            PlayerCivilization.InClimateAgreement = true;
            PlayerCivilization.EmissionReduction = 0.5f;
        }
    }

    private void BuildNuclear()
    {
        if (PlayerCivilization == null) return;

        if (PlayerCivilization.TechLevel >= 70)
        {
            if (!PlayerCivilization.HasNuclearWeapons)
            {
                PlayerCivilization.HasNuclearWeapons = true;
                PlayerCivilization.NuclearStockpile = 5;
            }
            else
            {
                PlayerCivilization.NuclearStockpile += 5;
            }
        }
    }

    private void IncreaseEcoFriendliness()
    {
        if (PlayerCivilization == null) return;
        PlayerCivilization.EcoFriendliness = Math.Min(PlayerCivilization.EcoFriendliness + 0.1f, 1.0f);
    }

    private void ReleaseControl()
    {
        PlayerCivilization = null;
        ShowControlPanel = false;
    }

    public void Draw(SpriteBatch spriteBatch, int screenWidth, int screenHeight)
    {
        // Draw civilization selector
        if (ShowCivSelector)
        {
            DrawCivilizationSelector(spriteBatch, screenWidth, screenHeight);
        }

        // Draw control panel
        if (ShowControlPanel && PlayerCivilization != null)
        {
            DrawControlPanel(spriteBatch, screenWidth, screenHeight);
        }
    }

    private void DrawCivilizationSelector(SpriteBatch spriteBatch, int screenWidth, int screenHeight)
    {
        UIWidgets.DrawBackdrop(spriteBatch, screenWidth, screenHeight);
        UIWidgets.DrawWindow(spriteBatch, _selectorPanel, "TAKE CONTROL OF A NATION", UITheme.Gold);

        if (_selectorButtons.Count == 1)
        {
            UITheme.DrawText(spriteBatch, "No nations exist yet.",
                new Vector2(_selectorPanel.X + 14, _selectorPanel.Y + UIWidgets.HeaderHeight + 22), UITheme.TextMuted, UITheme.FontNormal);
        }

        // Draw selector buttons: one row per nation, then Cancel
        foreach (var button in _selectorButtons)
        {
            if (button.Civ != null)
            {
                var civ = button.Civ;
                UIWidgets.DrawListRow(spriteBatch, button.Bounds, civ.Name,
                    $"{civ.CivType}  -  population {civ.Population:N0}  -  tech {civ.TechLevel}",
                    civ == PlayerCivilization, button.Color);
            }
            else
            {
                UIWidgets.DrawButton(spriteBatch, button.Bounds, button.Text);
            }
        }

        int total = _civManager.GetAllCivilizations().Count;
        int shown = _selectorButtons.Count - 1;
        if (total > shown)
        {
            UITheme.DrawText(spriteBatch, $"+ {total - shown} more not shown",
                new Vector2(_selectorPanel.X + 14, _selectorButtons[^1].Bounds.Y + 9), UITheme.TextMuted, UITheme.FontSmall);
        }
    }

    private void DrawControlPanel(SpriteBatch spriteBatch, int screenWidth, int screenHeight)
    {
        LayoutControlPanel();
        var civ = PlayerCivilization!;
        var panel = _controlPanel;
        int x = panel.X + 12;
        int width = panel.Width - 24;

        int y = UIWidgets.DrawWindow(spriteBatch, panel, "YOU RULE: " + civ.Name.ToUpperInvariant(), UITheme.Gold, closeButton: false);
        UITheme.FillRect(spriteBatch, new Rectangle(panel.X + 1, panel.Y + 8, 3, UIWidgets.HeaderHeight - 16), TerrainRenderer.GetCivPaletteColor(civ.Id));

        // Nation
        y = UIWidgets.DrawKeyValue(spriteBatch, x, y, width, "Population", civ.Population.ToString("N0"));
        y = UIWidgets.DrawKeyValue(spriteBatch, x, y, width, "Tech level", civ.TechLevel.ToString());
        y = UIWidgets.DrawKeyValue(spriteBatch, x, y, width, "Type", civ.CivType.ToString(), UIWidgets.Readable(GetCivTypeColor(civ.CivType)));
        y = UIWidgets.DrawKeyValue(spriteBatch, x, y, width, "Eco-friendliness", civ.EcoFriendliness.ToString("P0"), UITheme.Good);
        y += 8;

        // Resources: stock and yearly change
        y = UIWidgets.DrawSection(spriteBatch, x, y, width, "RESOURCES");
        float foodRate = civ.FoodProduction - civ.FoodConsumption;
        y = UIWidgets.DrawKeyValue(spriteBatch, x, y, width, "Food", $"{civ.Food:F0}  ({foodRate:+0.0;-0.0})",
            civ.Food > 50 ? UITheme.Good : UITheme.Bad);
        y = UIWidgets.DrawKeyValue(spriteBatch, x, y, width, "Wood", $"{civ.Wood:F0}  (+{civ.WoodProduction:F1})", new Color(206, 160, 110));
        y = UIWidgets.DrawKeyValue(spriteBatch, x, y, width, "Stone", $"{civ.Stone:F0}  (+{civ.StoneProduction:F1})", new Color(180, 186, 198));
        if (civ.CivType >= CivType.Industrial)
            y = UIWidgets.DrawKeyValue(spriteBatch, x, y, width, "Metal", $"{civ.Metal:F0}  (+{civ.MetalProduction:F1})", Color.Silver);
        else
            y = UIWidgets.DrawKeyValue(spriteBatch, x, y, width, "Metal", "needs industry", UITheme.TextMuted);
        y += 8;

        // Status badges
        int badgeX = x;
        if (civ.AtWar)
            badgeX = UIWidgets.DrawPill(spriteBatch, badgeX, y, "AT WAR", UITheme.Bad).Right + 6;
        if (civ.HasNuclearWeapons)
            badgeX = UIWidgets.DrawPill(spriteBatch, badgeX, y, $"NUKES {civ.NuclearStockpile}", UITheme.Warn).Right + 6;
        if (civ.InClimateAgreement)
            badgeX = UIWidgets.DrawPill(spriteBatch, badgeX, y, "CLIMATE PACT", Color.Cyan).Right + 6;
        if (badgeX == x)
            UIWidgets.DrawPill(spriteBatch, badgeX, y, "AT PEACE", UITheme.Good);
        y += 22 + 8;

        UIWidgets.DrawSection(spriteBatch, x, y, width, "ORDERS");

        // Draw control buttons
        foreach (var button in _controlButtons)
        {
            // Nuclear weapons need tech level 70
            bool enabled = button.Text != "Build Nuclear" || civ.TechLevel >= 70;
            UIWidgets.DrawActionButton(spriteBatch, button.Bounds, button.Text, button.Color, enabled: enabled, fontSize: UITheme.FontSmall);
        }
    }

    private Color GetCivTypeColor(CivType civType)
    {
        return civType switch
        {
            CivType.Tribal => new Color(139, 90, 43),
            CivType.Agricultural => new Color(100, 160, 80),
            CivType.Industrial => new Color(120, 120, 120),
            CivType.Scientific => new Color(100, 149, 237),
            CivType.Spacefaring => new Color(147, 112, 219),
            _ => Color.White
        };
    }

    private class Button
    {
        public Rectangle Bounds { get; set; }
        public string Text { get; }
        public Color Color { get; }
        public Action? OnClick { get; }
        public Civilization? Civ { get; set; }

        public Button(string text, Color color, Action? onClick)
        {
            Text = text;
            Color = color;
            OnClick = onClick;
        }
    }
}
