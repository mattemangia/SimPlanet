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
    private readonly DiseaseManager _diseaseManager;
    private readonly PlanetMap _map;
    private readonly CivilizationManager _civManager;

    private bool _isVisible = false;
    private Disease? _selectedDisease = null;

    // UI panels (computed by ComputeLayout, used by both Update and Draw)
    private Rectangle _mainPanel;
    private Rectangle _statsPanel;
    private Rectangle _evolutionPanel;
    private Rectangle _evolutionView;
    private Rectangle _diseaseSelectorPanel;
    private Rectangle _closeRect;
    private Rectangle _createRect;

    // Evolution traits
    private readonly List<TraitButton> _evolutionButtons = new();
    private readonly List<(string Title, int Y)> _evolutionCategories = new();
    private int _evolutionContentHeight;

    // Scroll for evolution traits (in rows)
    private int _evolutionScrollOffset = 0;
    private const int EvolutionRowHeight = 34;

    // Create disease modal
    private bool _showCreateDiseaseModal = false;
    private string _newDiseaseName = "Plague";
    private PathogenType _newDiseaseType = PathogenType.Virus;
    private int _selectedOriginCivId = -1;

    private static readonly Color DiseaseAccent = new Color(255, 110, 110);
    private static readonly string[] PathogenNames = { "Bacteria", "Virus", "Fungus", "Parasite", "Prion", "Bioweapon" };
    private static readonly string[] PathogenNotes =
    {
        "fast, easily cured", "very fast, hard to cure", "slow, mild", "hard to detect", "slow, extremely lethal", "engineered"
    };

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
    }

    // ---- Layout ------------------------------------------------------------------------

    private void ComputeLayout(int screenWidth, int screenHeight)
    {
        int width = Math.Min(900, screenWidth - 40);
        int height = Math.Min(590, screenHeight - 130);
        _mainPanel = new Rectangle((screenWidth - width) / 2, Math.Max(50, (screenHeight - height) / 2 - 10), width, height);
        _closeRect = UIWidgets.CloseButtonRect(_mainPanel);

        int top = _mainPanel.Y + UIWidgets.HeaderHeight + 12;
        int bottom = _mainPanel.Bottom - 12;
        _diseaseSelectorPanel = new Rectangle(_mainPanel.X + 12, top, 250, bottom - top);
        _createRect = new Rectangle(_diseaseSelectorPanel.X + 10, _diseaseSelectorPanel.Bottom - 42, _diseaseSelectorPanel.Width - 20, 32);

        int rightX = _diseaseSelectorPanel.Right + 12;
        int rightWidth = _mainPanel.Right - 12 - rightX;
        _statsPanel = new Rectangle(rightX, top, rightWidth, 190);
        _evolutionPanel = new Rectangle(rightX, _statsPanel.Bottom + 10, rightWidth, bottom - (_statsPanel.Bottom + 10));
        _evolutionView = new Rectangle(_evolutionPanel.X + 12, _evolutionPanel.Y + 8, _evolutionPanel.Width - 30, _evolutionPanel.Height - 16);
    }

    private Rectangle DiseaseRowRect(int index)
    {
        return new Rectangle(_diseaseSelectorPanel.X + 10, _diseaseSelectorPanel.Y + 36 + index * 42, _diseaseSelectorPanel.Width - 20, 38);
    }

    /// <summary>Rows of the disease list that fit above the create button.</summary>
    private int VisibleDiseaseRows => Math.Max(0, (_createRect.Y - 8 - (_diseaseSelectorPanel.Y + 36)) / 42);

    private readonly struct ModalLayout
    {
        public Rectangle Panel { get; init; }
        public Rectangle Close { get; init; }
        public Rectangle[] Types { get; init; }
        public Rectangle[] Civs { get; init; }
        public Rectangle Create { get; init; }
        public Rectangle Cancel { get; init; }
        public int LabelY { get; init; }
    }

    private const int MaxOriginCivs = 6;

    private ModalLayout GetModalLayout(int screenWidth, int screenHeight)
    {
        const int width = 500, rowHeight = 30, rowGap = 4;
        int rows = PathogenNames.Length;
        int height = UIWidgets.HeaderHeight + 12 + 22 + rows * (rowHeight + rowGap) + 14 + 34 + 14;
        var panel = new Rectangle((screenWidth - width) / 2, (screenHeight - height) / 2, width, height);
        int columnWidth = (width - 16 * 2 - 14) / 2;
        int labelY = panel.Y + UIWidgets.HeaderHeight + 12;
        int rowsY = labelY + 22;

        var types = new Rectangle[rows];
        for (int i = 0; i < rows; i++)
            types[i] = new Rectangle(panel.X + 16, rowsY + i * (rowHeight + rowGap), columnWidth, rowHeight);

        int civCount = Math.Min(MaxOriginCivs, _civManager.Civilizations.Count);
        var civs = new Rectangle[civCount];
        for (int i = 0; i < civCount; i++)
            civs[i] = new Rectangle(panel.X + 16 + columnWidth + 14, rowsY + i * (rowHeight + rowGap), columnWidth, rowHeight);

        int buttonY = panel.Bottom - 14 - 34;
        return new ModalLayout
        {
            Panel = panel,
            Close = UIWidgets.CloseButtonRect(panel),
            Types = types,
            Civs = civs,
            Create = new Rectangle(panel.Right - 16 - 190, buttonY, 190, 34),
            Cancel = new Rectangle(panel.Right - 16 - 190 - 10 - 110, buttonY, 110, 34),
            LabelY = labelY
        };
    }

    // ---- Update ------------------------------------------------------------------------

    public void Update(MouseState mouseState, MouseState previousMouseState, KeyboardState keyState)
    {
        if (!_isVisible) return;

        int screenWidth = _graphicsDevice.Viewport.Width;
        int screenHeight = _graphicsDevice.Viewport.Height;
        ComputeLayout(screenWidth, screenHeight);

        bool clicked = mouseState.LeftButton == ButtonState.Released &&
                      previousMouseState.LeftButton == ButtonState.Pressed;

        // Create disease modal (drawn on top, so it takes all the input)
        if (_showCreateDiseaseModal)
        {
            UpdateCreateDiseaseModal(mouseState, clicked, screenWidth, screenHeight);
            return;
        }

        // Close button
        if (clicked && _closeRect.Contains(mouseState.Position))
        {
            _isVisible = false;
            return;
        }

        // Create disease button
        if (clicked && _createRect.Contains(mouseState.Position))
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
            BuildEvolutionButtons();
            foreach (var btn in _evolutionButtons)
            {
                if (clicked && btn.Visible && btn.Bounds.Contains(mouseState.Position) && btn.IsEnabled)
                {
                    // Evolve trait
                    _diseaseManager.EvolveTrait(_selectedDisease, btn.Tag);
                }
            }

            // Scroll
            int scrollDelta = mouseState.ScrollWheelValue - previousMouseState.ScrollWheelValue;
            if (scrollDelta != 0 && _evolutionPanel.Contains(mouseState.Position))
            {
                _evolutionScrollOffset -= scrollDelta / 120; // Standard mouse wheel unit
                _evolutionScrollOffset = Math.Clamp(_evolutionScrollOffset, 0, MaxEvolutionScroll);
            }
        }
    }

    private int MaxEvolutionScroll =>
        Math.Max(0, (int)Math.Ceiling((_evolutionContentHeight - _evolutionView.Height) / (float)EvolutionRowHeight));

    private void UpdateCreateDiseaseModal(MouseState mouseState, bool clicked, int screenWidth, int screenHeight)
    {
        if (!clicked) return;

        var layout = GetModalLayout(screenWidth, screenHeight);

        // Click outside the modal, on its close button or on Cancel closes it
        if (!layout.Panel.Contains(mouseState.Position) ||
            layout.Close.Contains(mouseState.Position) ||
            layout.Cancel.Contains(mouseState.Position))
        {
            _showCreateDiseaseModal = false;
            return;
        }

        // Pathogen type buttons
        for (int i = 0; i < layout.Types.Length; i++)
        {
            if (layout.Types[i].Contains(mouseState.Position))
            {
                _newDiseaseType = (PathogenType)i;
            }
        }

        // Civilization selector
        int civIndex = 0;
        foreach (var civ in _civManager.Civilizations.Take(layout.Civs.Length))
        {
            if (layout.Civs[civIndex].Contains(mouseState.Position))
            {
                _selectedOriginCivId = civ.Id;
            }
            civIndex++;
        }

        // Create button
        if (layout.Create.Contains(mouseState.Position))
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

    private void UpdateDiseaseSelector(MouseState mouseState, bool clicked)
    {
        if (!clicked) return;
        int index = 0;
        int visible = VisibleDiseaseRows;
        foreach (var disease in _diseaseManager.Diseases)
        {
            if (index >= visible) break;
            if (DiseaseRowRect(index).Contains(mouseState.Position))
            {
                _selectedDisease = disease;
                _evolutionScrollOffset = 0;
            }
            index++;
        }
    }

    // ---- Draw --------------------------------------------------------------------------

    public void Draw(SpriteBatch spriteBatch, int screenWidth, int screenHeight)
    {
        if (!_isVisible) return;

        ComputeLayout(screenWidth, screenHeight);

        UIWidgets.DrawBackdrop(spriteBatch, screenWidth, screenHeight);
        UIWidgets.DrawWindow(spriteBatch, _mainPanel, "DISEASE CONTROL CENTER", DiseaseAccent, closeButton: true,
            note: "K to close");

        DrawDiseaseSelector(spriteBatch);

        if (_selectedDisease != null)
        {
            DrawStatsPanel(spriteBatch);
            DrawEvolutionPanel(spriteBatch, screenWidth, screenHeight);
        }
        else
        {
            // No disease selected - show instructions
            var area = new Rectangle(_statsPanel.X, _statsPanel.Y, _statsPanel.Width, _evolutionPanel.Bottom - _statsPanel.Y);
            UIWidgets.DrawCard(spriteBatch, area);
            var icons = MapIcons.GetShared(_graphicsDevice);
            int cy = area.Y + area.Height / 2;
            spriteBatch.Draw(icons.Biohazard, new Rectangle(area.X + area.Width / 2 - 28, cy - 78, 56, 56), DiseaseAccent * 0.55f);
            UITheme.DrawTextCentered(spriteBatch, "No disease selected",
                new Rectangle(area.X, cy - 10, area.Width, 24), UITheme.Text, UITheme.FontLarge);
            string msg = _diseaseManager.Diseases.Count == 0
                ? "Create a new pandemic with the button on the left."
                : "Pick an active disease on the left, or create a new pandemic.";
            UITheme.DrawTextCentered(spriteBatch, UITheme.Ellipsize(msg, area.Width - 30), new Rectangle(area.X, cy + 20, area.Width, 20),
                UITheme.TextDim, UITheme.FontNormal);
        }

        if (_showCreateDiseaseModal)
        {
            DrawCreateDiseaseModal(spriteBatch, screenWidth, screenHeight);
        }
    }

    private static (string Text, Color Color) StatusOf(Disease disease)
    {
        if (disease.CureDeployed) return ("CURE DEPLOYED", UITheme.Good);
        if (disease.IsActive) return ("ACTIVE", UITheme.Bad);
        return ("ERADICATED", UITheme.TextMuted);
    }

    private void DrawDiseaseSelector(SpriteBatch spriteBatch)
    {
        var panel = _diseaseSelectorPanel;
        UIWidgets.DrawCard(spriteBatch, panel);
        UIWidgets.DrawSection(spriteBatch, panel.X + 10, panel.Y + 10, panel.Width - 20, "ACTIVE DISEASES");

        // List diseases
        int index = 0;
        int visible = VisibleDiseaseRows;
        int total = _diseaseManager.Diseases.Count;
        foreach (var disease in _diseaseManager.Diseases)
        {
            if (index >= visible) break;
            var status = StatusOf(disease);
            UIWidgets.DrawListRow(spriteBatch, DiseaseRowRect(index), disease.Name,
                $"{disease.Type}  -  {status.Text.ToLowerInvariant()}", disease == _selectedDisease, status.Color);
            index++;
        }

        if (total == 0)
        {
            UIWidgets.DrawParagraph(spriteBatch, panel.X + 10, panel.Y + 38, panel.Width - 20,
                "The world is healthy. No pathogen has been released yet.", UITheme.TextMuted);
        }
        else if (total > visible)
        {
            UITheme.DrawText(spriteBatch, $"+ {total - visible} more", new Vector2(panel.X + 12, _createRect.Y - 20), UITheme.TextMuted, UITheme.FontSmall);
        }

        UIWidgets.DrawActionButton(spriteBatch, _createRect, "+ Create Pandemic", DiseaseAccent);
    }

    private void DrawStatsPanel(SpriteBatch spriteBatch)
    {
        if (_selectedDisease == null) return;
        var disease = _selectedDisease;
        var panel = _statsPanel;
        UIWidgets.DrawCard(spriteBatch, panel);

        int x = panel.X + 14;
        int width = panel.Width - 28;
        int y = panel.Y + 12;

        // Name, pathogen type and status badge
        var status = StatusOf(disease);
        var pillSize = UITheme.Measure(status.Text, 11f);
        int pillWidth = (int)pillSize.X + 16;
        string name = UITheme.Ellipsize(disease.Name, width - pillWidth - 120, UITheme.FontLarge);
        UITheme.DrawTextShadowed(spriteBatch, name, new Vector2(x, y), DiseaseAccent, UITheme.FontLarge);
        float nameWidth = UITheme.Measure(name, UITheme.FontLarge).X;
        UITheme.DrawText(spriteBatch, disease.Type.ToString(), new Vector2(x + nameWidth + 10, y + 6), UITheme.TextDim, UITheme.FontSmall);
        UIWidgets.DrawPill(spriteBatch, panel.Right - 14 - pillWidth, y + 3, status.Text, status.Color);
        y += 34;

        // Headline numbers in three tiles
        int tileGap = 10;
        int tileWidth = (width - tileGap * 2) / 3;
        DrawStatTile(spriteBatch, new Rectangle(x, y, tileWidth, 50), "INFECTED", disease.TotalInfected.ToString("N0"), UITheme.Warn);
        DrawStatTile(spriteBatch, new Rectangle(x + tileWidth + tileGap, y, tileWidth, 50), "DEAD", disease.TotalDeaths.ToString("N0"), UITheme.Bad);
        DrawStatTile(spriteBatch, new Rectangle(x + (tileWidth + tileGap) * 2, y, tileWidth, 50), "HEALTHY", disease.HealthyRemaining.ToString("N0"), UITheme.Good);
        y += 62;

        // Traits and cure progress, two per line
        int half = (width - 24) / 2;
        UIWidgets.DrawMeterRow(spriteBatch, x, y, half, "Infectivity", $"{disease.Infectivity * 100:F0} %", disease.Infectivity, new Color(255, 200, 100));
        UIWidgets.DrawMeterRow(spriteBatch, x + half + 24, y, half, "Severity", $"{disease.Severity * 100:F0} %", disease.Severity, new Color(255, 150, 50));
        y += 34;
        UIWidgets.DrawMeterRow(spriteBatch, x, y, half, "Lethality", $"{disease.Lethality * 100:F0} %", disease.Lethality, new Color(255, 80, 70));
        if (disease.CureDeployed)
            UIWidgets.DrawMeterRow(spriteBatch, x + half + 24, y, half, "Cure", "deployed", 1f, UITheme.Good);
        else
            UIWidgets.DrawMeterRow(spriteBatch, x + half + 24, y, half, "Cure research", $"{disease.GlobalCureProgress:F1} %",
                disease.GlobalCureProgress / 100f, new Color(100, 230, 200));
    }

    private static void DrawStatTile(SpriteBatch spriteBatch, Rectangle rect, string label, string value, Color color)
    {
        UITheme.FillRounded(spriteBatch, rect, new Color(20, 27, 42, 230));
        UITheme.OutlineRounded(spriteBatch, rect, UITheme.Border * 0.8f);
        UITheme.FillRect(spriteBatch, new Rectangle(rect.X + 1, rect.Y + 6, 3, rect.Height - 12), color);
        UITheme.DrawText(spriteBatch, label, new Vector2(rect.X + 12, rect.Y + 6), UITheme.TextDim, 11f);
        UITheme.DrawText(spriteBatch, UITheme.Ellipsize(value, rect.Width - 20, UITheme.FontLarge), new Vector2(rect.X + 12, rect.Y + 21), color, UITheme.FontLarge);
    }

    /// <summary>
    /// Lays the trait buttons out in a three-column grid under their category captions.
    /// Called from both Update and Draw so clicks land on what is drawn.
    /// </summary>
    private void BuildEvolutionButtons()
    {
        _evolutionButtons.Clear();
        _evolutionCategories.Clear();
        if (_selectedDisease == null) return;
        var d = _selectedDisease;

        const int columns = 3, gap = 8;
        int x0 = _evolutionView.X;
        int buttonWidth = (_evolutionView.Width - gap * (columns - 1)) / columns;
        int start = _evolutionView.Y - _evolutionScrollOffset * EvolutionRowHeight;
        int y = start;
        int column = 0;

        void Category(string name)
        {
            if (column != 0) { y += EvolutionRowHeight; column = 0; }
            if (_evolutionCategories.Count > 0) y += 6;
            _evolutionCategories.Add((name, y));
            y += 22;
        }

        void Trait(string name, string description, bool available)
        {
            var bounds = new Rectangle(x0 + column * (buttonWidth + gap), y, buttonWidth, EvolutionRowHeight - 6);
            _evolutionButtons.Add(new TraitButton
            {
                Bounds = bounds,
                Label = name.Replace("_", " "),
                Tag = name,
                Description = description,
                IsEnabled = available,
                Visible = bounds.Y >= _evolutionView.Y && bounds.Bottom <= _evolutionView.Bottom
            });
            column++;
            if (column == columns) { column = 0; y += EvolutionRowHeight; }
        }

        Category("TRANSMISSION");
        Trait("Air", "Airborne transmission", !d.TransmissionMethods.HasFlag(TransmissionMethod.Air));
        Trait("Water", "Waterborne transmission", !d.TransmissionMethods.HasFlag(TransmissionMethod.Water));
        Trait("Blood", "Blood transmission", !d.TransmissionMethods.HasFlag(TransmissionMethod.Blood));
        Trait("Livestock", "Animal transmission", !d.TransmissionMethods.HasFlag(TransmissionMethod.Livestock));
        Trait("Insects", "Insect vectors", !d.TransmissionMethods.HasFlag(TransmissionMethod.Insects));

        Category("SYMPTOMS");
        Trait("Coughing", "Mild symptoms, increases spread", !d.Symptoms.HasFlag(DiseaseSymptoms.Coughing));
        Trait("Fever", "Noticeable symptoms", !d.Symptoms.HasFlag(DiseaseSymptoms.Fever));
        Trait("Pneumonia", "Severe respiratory issues", !d.Symptoms.HasFlag(DiseaseSymptoms.Pneumonia));
        Trait("Organ_Failure", "Critical organ damage", !d.Symptoms.HasFlag(DiseaseSymptoms.OrganFailure));
        Trait("Total_Organ_Failure", "Complete system shutdown", !d.Symptoms.HasFlag(DiseaseSymptoms.TotalOrganFailure));

        Category("RESISTANCES");
        Trait("Cold_Resistance", "Survive in cold climates", d.ColdResistance < 0.99f);
        Trait("Heat_Resistance", "Survive in hot climates", d.HeatResistance < 0.99f);
        Trait("Drug_Resistance", "Resist medical treatment", d.DrugResistance < 0.99f);

        Category("ABILITIES");
        Trait("Hardened_Resurgence", "Can re-infect cured people", !d.HardenedResurgence);
        Trait("Genetic_Reshuffle", "Delays cure research", !d.GeneticReShuffle);
        Trait("Total_Organ_Shutdown", "Massively increases lethality", !d.TotalOrganShutdown);

        if (column != 0) y += EvolutionRowHeight;
        _evolutionContentHeight = y - start;
    }

    private void DrawEvolutionPanel(SpriteBatch spriteBatch, int screenWidth, int screenHeight)
    {
        if (_selectedDisease == null) return;

        UIWidgets.DrawCard(spriteBatch, _evolutionPanel);

        if (_selectedDisease.CureDeployed)
        {
            UIWidgets.DrawSection(spriteBatch, _evolutionView.X, _evolutionView.Y, _evolutionView.Width, "EVOLUTION", UITheme.TextDim);
            UITheme.DrawText(spriteBatch, "The cure is out: this pathogen can no longer evolve.",
                new Vector2(_evolutionView.X, _evolutionView.Y + 26), UITheme.TextMuted, UITheme.FontNormal);
            return;
        }

        _evolutionScrollOffset = Math.Clamp(_evolutionScrollOffset, 0, MaxEvolutionScroll);
        BuildEvolutionButtons();

        foreach (var category in _evolutionCategories)
        {
            if (category.Y >= _evolutionView.Y && category.Y + 18 <= _evolutionView.Bottom)
                UIWidgets.DrawSection(spriteBatch, _evolutionView.X, category.Y, _evolutionView.Width, category.Title, UITheme.Warn);
        }

        TraitButton? hovered = null;
        foreach (var btn in _evolutionButtons)
        {
            if (!btn.Visible) continue;
            DrawEvolutionButton(spriteBatch, btn);
            if (!_showCreateDiseaseModal && btn.Bounds.Contains(Mouse.GetState().Position)) hovered = btn;
        }

        int maxScroll = MaxEvolutionScroll;
        UIWidgets.DrawScrollBar(spriteBatch,
            new Rectangle(_evolutionPanel.Right - 9, _evolutionView.Y, 4, _evolutionView.Height),
            _evolutionScrollOffset, maxScroll, _evolutionView.Height, _evolutionContentHeight);

        if (hovered != null)
        {
            string state = hovered.IsEnabled ? "Click to evolve" : "Already evolved";
            UITheme.DrawTooltip(spriteBatch, $"{hovered.Label}\n{hovered.Description}\n{state}",
                new Point(hovered.Bounds.Center.X, hovered.Bounds.Y), screenWidth, screenHeight, preferAbove: true);
        }
    }

    private void DrawEvolutionButton(SpriteBatch spriteBatch, TraitButton btn)
    {
        if (btn.IsEnabled)
        {
            UIWidgets.DrawActionButton(spriteBatch, btn.Bounds, btn.Label, UITheme.Accent, centered: false, fontSize: 13f);
            return;
        }

        // Already evolved: lit, with a tick, and no longer a button
        var rect = btn.Bounds;
        UITheme.FillRounded(spriteBatch, rect, new Color(26, 58, 44, 235));
        UITheme.OutlineRounded(spriteBatch, rect, UITheme.Good * 0.7f);
        var c = new Vector2(rect.Right - 16, rect.Y + rect.Height / 2f);
        UITheme.DrawLine(spriteBatch, c + new Vector2(-5, 0), c + new Vector2(-2, 4), UITheme.Good, 2f);
        UITheme.DrawLine(spriteBatch, c + new Vector2(-2, 4), c + new Vector2(5, -4), UITheme.Good, 2f);
        string fitted = UITheme.Ellipsize(btn.Label, rect.Width - 40, 13f);
        var size = UITheme.Measure(fitted, 13f);
        UITheme.DrawText(spriteBatch, fitted, new Vector2(rect.X + 14, rect.Y + (rect.Height - size.Y) / 2f), new Color(170, 230, 190), 13f);
    }

    private void DrawCreateDiseaseModal(SpriteBatch spriteBatch, int screenWidth, int screenHeight)
    {
        var layout = GetModalLayout(screenWidth, screenHeight);

        UIWidgets.DrawBackdrop(spriteBatch, screenWidth, screenHeight, 0.5f);
        UIWidgets.DrawWindow(spriteBatch, layout.Panel, "CREATE NEW PANDEMIC", DiseaseAccent);

        // Pathogen type selection
        int columnWidth = layout.Types[0].Width;
        UIWidgets.DrawSection(spriteBatch, layout.Types[0].X, layout.LabelY, columnWidth, "PATHOGEN");
        for (int i = 0; i < layout.Types.Length; i++)
        {
            UIWidgets.DrawListRow(spriteBatch, layout.Types[i], PathogenNames[i], PathogenNotes[i], (PathogenType)i == _newDiseaseType);
        }

        // Origin civilization
        int civX = layout.Types[0].Right + 14;
        UIWidgets.DrawSection(spriteBatch, civX, layout.LabelY, columnWidth, "STARTS IN");
        int civIndex = 0;
        bool canCreate = false;
        foreach (var civ in _civManager.Civilizations.Take(layout.Civs.Length))
        {
            bool selected = civ.Id == _selectedOriginCivId;
            if (selected && civ.Territory.Any()) canCreate = true;
            UIWidgets.DrawListRow(spriteBatch, layout.Civs[civIndex], civ.Name, null, selected);
            civIndex++;
        }
        if (civIndex == 0)
        {
            UIWidgets.DrawParagraph(spriteBatch, civX, layout.LabelY + 26, columnWidth,
                "No civilization exists yet. A pandemic needs people to infect.", UITheme.TextMuted);
        }

        UIWidgets.DrawButton(spriteBatch, layout.Cancel, "Cancel");
        UIWidgets.DrawActionButton(spriteBatch, layout.Create, "Create Pandemic", DiseaseAccent, enabled: canCreate);
    }

    private class TraitButton
    {
        public Rectangle Bounds { get; set; }
        public string Label { get; set; } = "";
        public string Tag { get; set; } = "";
        public string Description { get; set; } = "";
        public bool IsEnabled { get; set; } = true;
        public bool Visible { get; set; } = true;
    }
}
