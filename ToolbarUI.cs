using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;

namespace SimPlanet
{
    public class ToolbarUI
    {
        private class ToolbarButton
        {
            public Rectangle Bounds { get; set; }
            public string Tooltip { get; set; }
            public Action OnClick { get; set; }
            public Texture2D Icon { get; set; }
            public bool IsHovered { get; set; }
            public string Category { get; set; }
        }

        private List<ToolbarButton> buttons;
        private Texture2D pixelTexture;
        private GraphicsDevice graphicsDevice;
        private SimPlanetGame game;
        private FontRenderer fontRenderer;
        private MouseState previousMouseState;
        private int toolbarHeight = 44;
        private int buttonSize = 36;
        private int buttonSpacing = 4;
        private int categorySpacing = 12;
        private int leftMargin = 8;
        private int topMargin = 4;

        private readonly Color _toolbarBgColor = new Color(25, 30, 45, 250);
        private readonly Color _buttonNormalColor = new Color(50, 60, 80);
        private readonly Color _buttonHoverColor = new Color(80, 100, 140);
        private readonly Color _buttonBorderColor = new Color(100, 120, 160);
        private readonly Color _separatorColor = new Color(60, 80, 120);

        public ToolbarUI(SimPlanetGame game, GraphicsDevice graphicsDevice, FontRenderer fontRenderer)
        {
            this.game = game;
            this.graphicsDevice = graphicsDevice;
            this.fontRenderer = fontRenderer;
            this.buttons = new List<ToolbarButton>();

            pixelTexture = new Texture2D(graphicsDevice, 1, 1);
            pixelTexture.SetData(new[] { Color.White });

            InitializeButtons();
        }

        public int ToolbarHeight => toolbarHeight;

        private void InitializeButtons()
        {
            int x = leftMargin;
            int y = topMargin;

            // View Modes (Numeric Keys 1-0)
            AddButton(ref x, y, "1", "Terrain View", "Terrain", () => SetViewMode(RenderMode.Terrain));
            AddButton(ref x, y, "2", "Temperature View", "Terrain", () => SetViewMode(RenderMode.Temperature));
            AddButton(ref x, y, "3", "Rainfall View", "Terrain", () => SetViewMode(RenderMode.Rainfall));
            AddButton(ref x, y, "4", "Life View", "Terrain", () => SetViewMode(RenderMode.Life));
            AddButton(ref x, y, "5", "Oxygen View", "Terrain", () => SetViewMode(RenderMode.Oxygen));
            AddButton(ref x, y, "6", "CO2 View", "Terrain", () => SetViewMode(RenderMode.CO2));
            AddButton(ref x, y, "7", "Elevation View", "Terrain", () => SetViewMode(RenderMode.Elevation));
            AddButton(ref x, y, "8", "Geological View", "Terrain", () => SetViewMode(RenderMode.Geological));
            AddButton(ref x, y, "9", "Tectonic Plates", "Terrain", () => SetViewMode(RenderMode.TectonicPlates));
            AddButton(ref x, y, "0", "Volcanoes", "Terrain", () => SetViewMode(RenderMode.Volcanoes));

            x += categorySpacing;

            // Meteorology (F1-F4)
            AddButton(ref x, y, "F1", "Clouds", "Weather", () => SetViewMode(RenderMode.Clouds));
            AddButton(ref x, y, "F2", "Wind", "Weather", () => SetViewMode(RenderMode.Wind));
            AddButton(ref x, y, "F3", "Pressure", "Weather", () => SetViewMode(RenderMode.Pressure));
            AddButton(ref x, y, "F4", "Storms", "Weather", () => SetViewMode(RenderMode.Storms));

            x += categorySpacing;

            // Geological Hazards
            AddButton(ref x, y, "E", "Earthquakes", "Hazards", () => SetViewMode(RenderMode.Earthquakes));
            AddButton(ref x, y, "Q", "Faults", "Hazards", () => SetViewMode(RenderMode.Faults));
            AddButton(ref x, y, "U", "Tsunamis", "Hazards", () => SetViewMode(RenderMode.Tsunamis));

            x += categorySpacing;

            // UI Toggles
            AddButton(ref x, y, "H", "Toggle Help (H)", "UI", () => game.ToggleHelp());
            AddButton(ref x, y, "M", "Map Options (M)", "UI", () => game.ToggleMapOptions());
            AddButton(ref x, y, "P", "Minimap (P)", "UI", () => game.ToggleMinimap());
            AddButton(ref x, y, "C", "Day/Night (C)", "UI", () => game.ToggleDayNight());
            AddButton(ref x, y, "V", "Volcano Overlay (V)", "UI", () => game.ToggleVolcanoes());
            AddButton(ref x, y, "B", "Rivers (B)", "UI", () => game.ToggleRivers());
            AddButton(ref x, y, "N", "Plates (N)", "UI", () => game.TogglePlates());

            x += categorySpacing;

            // Game Features
            AddButton(ref x, y, "L", "Life Painter (L)", "Feature", () => game.ToggleLifePainter());
            AddButton(ref x, y, "G", "Civilization (G)", "Feature", () => game.ToggleCivilization());
            AddButton(ref x, y, "I", "Divine Powers (I)", "Feature", () => game.ToggleDivinePowers());
            AddButton(ref x, y, "D", "Disasters (D)", "Feature", () => game.ToggleDisasters());
            AddButton(ref x, y, "K", "Diseases (K)", "Feature", () => game.ToggleDiseases());
            AddButton(ref x, y, "T", "Terraforming Tool (T)", "Feature", () => game.ToggleTerraformingTool());
            AddButton(ref x, y, "Y", "Graphs (Y)", "Feature", () => game.ToggleGraphs());
            AddButton(ref x, y, "X", "Planet Controls (X)", "Feature", () => game.TogglePlanetControls());
            AddButton(ref x, y, "Ctrl+Y", "Stabilizer (Ctrl+Y)", "Feature", () => game.ToggleStabilizer());

            x += categorySpacing;

            AddButton(ref x, y, "F10", "Biomes", "Extra", () => SetViewMode(RenderMode.Biomes));
            AddButton(ref x, y, "A", "Albedo", "Extra", () => SetViewMode(RenderMode.Albedo));
            AddButton(ref x, y, "F12", "Radiation", "Extra", () => SetViewMode(RenderMode.Radiation));
            AddButton(ref x, y, "J", "Resources", "Extra", () => SetViewMode(RenderMode.Resources));
            AddButton(ref x, y, "O", "Infrastructure", "Extra", () => SetViewMode(RenderMode.Infrastructure));
            AddButton(ref x, y, "S", "Spectral Bands", "Extra", () => SetViewMode(RenderMode.SpectralBands));

            foreach (var button in buttons)
            {
                button.Icon = GenerateIcon(button.Tooltip, button.Category);
            }
        }

        private void AddButton(ref int x, int y, string label, string tooltip, string category, Action onClick)
        {
            var button = new ToolbarButton
            {
                Bounds = new Rectangle(x, y, buttonSize, buttonSize),
                Tooltip = $"{tooltip}",
                OnClick = onClick,
                Category = category
            };

            buttons.Add(button);
            x += buttonSize + buttonSpacing;
        }

        private void SetViewMode(RenderMode mode)
        {
            game.SetRenderMode(mode);
        }

        // ... GenerateIcon and Draw...Icon methods (omitted for brevity but must be kept)
        // Re-using the exact same icon generation code structure as before, just updated the list.

        private Texture2D GenerateIcon(string tooltip, string category)
        {
            Texture2D icon = new Texture2D(graphicsDevice, buttonSize - 8, buttonSize - 8);
            Color[] data = new Color[(buttonSize - 8) * (buttonSize - 8)];
            for (int i = 0; i < data.Length; i++) data[i] = Color.Transparent;
            int size = buttonSize - 8;

            // Icon logic (simplified for brevity in this update, assuming previous implementation logic)
            if (tooltip.Contains("Terrain")) DrawTerrainIcon(data, size);
            else if (tooltip.Contains("Temperature")) DrawTemperatureIcon(data, size);
            else if (tooltip.Contains("Rainfall")) DrawRainfallIcon(data, size);
            else if (tooltip.Contains("Life")) DrawLifeIcon(data, size);
            else if (tooltip.Contains("Oxygen")) DrawOxygenIcon(data, size);
            else if (tooltip.Contains("CO2")) DrawCO2Icon(data, size);
            else if (tooltip.Contains("Elevation")) DrawElevationIcon(data, size);
            else if (tooltip.Contains("Geological")) DrawGeologicalIcon(data, size);
            else if (tooltip.Contains("Tectonic")) DrawTectonicIcon(data, size);
            else if (tooltip.Contains("Volcanoes")) DrawVolcanoIcon(data, size);
            else if (tooltip.Contains("Clouds")) DrawCloudsIcon(data, size);
            else if (tooltip.Contains("Wind")) DrawWindIcon(data, size);
            else if (tooltip.Contains("Pressure")) DrawPressureIcon(data, size);
            else if (tooltip.Contains("Storms")) DrawStormIcon(data, size);
            else if (tooltip.Contains("Earthquakes")) DrawEarthquakeIcon(data, size);
            else if (tooltip.Contains("Faults")) DrawFaultIcon(data, size);
            else if (tooltip.Contains("Tsunamis")) DrawTsunamiIcon(data, size);
            else if (tooltip.Contains("Biomes")) DrawBiomesIcon(data, size);
            else if (tooltip.Contains("Albedo")) DrawAlbedoIcon(data, size);
            else if (tooltip.Contains("Radiation")) DrawRadiationIcon(data, size);
            else if (tooltip.Contains("Resources")) DrawResourcesIcon(data, size);
            else if (tooltip.Contains("Infrastructure")) DrawInfrastructureIcon(data, size);
            else if (tooltip.Contains("Pause")) DrawPauseIcon(data, size);
            else if (tooltip.Contains("Speed Up")) DrawSpeedUpIcon(data, size);
            else if (tooltip.Contains("Speed Down")) DrawSpeedDownIcon(data, size);
            else if (tooltip.Contains("Quick Save")) DrawSaveIcon(data, size);
            else if (tooltip.Contains("Quick Load")) DrawLoadIcon(data, size);
            else if (tooltip.Contains("Regenerate")) DrawRegenerateIcon(data, size);
            else if (tooltip.Contains("Help")) DrawHelpIcon(data, size);
            else if (tooltip.Contains("Map Options")) DrawMapIcon(data, size);
            else if (tooltip.Contains("Minimap")) DrawMinimapIcon(data, size);
            else if (tooltip.Contains("Day/Night")) DrawDayNightIcon(data, size);
            else if (tooltip.Contains("Volcano Overlay")) DrawVolcanoOverlayIcon(data, size);
            else if (tooltip.Contains("Rivers")) DrawRiversIcon(data, size);
            else if (tooltip.Contains("Plates")) DrawPlatesIcon(data, size);
            else if (tooltip.Contains("Seed Life")) DrawSeedLifeIcon(data, size);
            else if (tooltip.Contains("Civilization")) DrawCivilizationIcon(data, size);
            else if (tooltip.Contains("Divine Powers")) DrawDivineIcon(data, size);
            else if (tooltip.Contains("Disasters")) DrawDisasterIcon(data, size);
            else if (tooltip.Contains("Diseases")) DrawDiseaseIcon(data, size);
            else if (tooltip.Contains("Plant Tool")) DrawPlantIcon(data, size);
            else if (tooltip.Contains("Stabilizer")) DrawStabilizerIcon(data, size);
            else if (tooltip.Contains("Graphs")) DrawGraphIcon(data, size);
            else if (tooltip.Contains("Terraforming")) DrawTerraformingIcon(data, size);
            else if (tooltip.Contains("Planet Controls")) DrawPlanetControlsIcon(data, size);
            else if (tooltip.Contains("Spectral")) DrawSpectralIcon(data, size);

            icon.SetData(data);
            return icon;
        }

        // ... Include all previous Draw*Icon methods here ...
        private void DrawSpectralIcon(Color[] data, int size)
        {
            Color[] spectrum = { Color.Red, Color.Orange, Color.Yellow, Color.Green, Color.Blue, Color.Indigo, Color.Violet };
            for (int x = 0; x < size; x++)
            {
                int colorIndex = (x * spectrum.Length) / size;
                Color c = spectrum[Math.Clamp(colorIndex, 0, spectrum.Length - 1)];
                for (int y = 0; y < size; y++) data[y * size + x] = c;
            }
        }
        private void DrawTerrainIcon(Color[] data, int size) { /* Implementation from previous step */
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++) if (y > size * 0.7f) data[y * size + x] = new Color(34, 139, 34); else if (y > size * 0.5f) data[y * size + x] = new Color(139, 69, 19); else if (y > size * 0.3f) data[y * size + x] = new Color(128, 128, 128);
        }
        private void DrawTemperatureIcon(Color[] data, int size) {
            int centerX = size / 2; for (int y = 2; y < size - 4; y++) { data[y * size + centerX] = Color.Red; data[y * size + centerX - 1] = Color.Red; } for (int y = size - 5; y < size - 1; y++) for (int x = centerX - 2; x <= centerX + 1; x++) if (x >= 0 && x < size) data[y * size + x] = Color.Red;
        }
        private void DrawRainfallIcon(Color[] data, int size) {
            Color blue = new Color(30, 144, 255); for (int i = 0; i < 4; i++) { int x = 4 + i * 5; for (int y = 3 + i * 2; y < size - 2; y += 4) { if (x < size && y < size) { data[y * size + x] = blue; if (y + 1 < size) data[(y + 1) * size + x] = blue; } } }
        }
        private void DrawLifeIcon(Color[] data, int size) {
            Color green = new Color(0, 200, 0); Color brown = new Color(101, 67, 33); int centerX = size / 2; for (int y = size * 2 / 3; y < size - 2; y++) { data[y * size + centerX] = brown; data[y * size + centerX - 1] = brown; } for (int y = 2; y < size * 2 / 3; y++) { int width = (size * 2 / 3 - y) / 2; for (int x = centerX - width; x <= centerX + width; x++) { if (x >= 0 && x < size) data[y * size + x] = green; } }
        }
        private void DrawOxygenIcon(Color[] data, int size) {
            Color cyan = new Color(0, 255, 255); DrawCircle(data, size, size / 3, size / 2, 4, cyan); DrawCircle(data, size, size * 2 / 3, size / 2, 4, cyan); for (int x = size / 3; x <= size * 2 / 3; x++) data[size / 2 * size + x] = cyan;
        }
        private void DrawCO2Icon(Color[] data, int size) {
            Color gray = new Color(180, 180, 180); Color red = new Color(255, 100, 100); DrawCircle(data, size, size / 2, size / 2, 4, gray); DrawCircle(data, size, size / 4, size / 2, 3, red); DrawCircle(data, size, size * 3 / 4, size / 2, 3, red);
        }
        private void DrawElevationIcon(Color[] data, int size) {
            Color mountain = new Color(139, 137, 137); for (int y = 2; y < size - 2; y++) { int width = (size - y) / 3; for (int x = size / 3 - width; x <= size / 3 + width; x++) { if (x >= 0 && x < size && y >= 0 && y < size) data[y * size + x] = mountain; } } for (int y = 5; y < size - 2; y++) { int width = (size - y - 2) / 4; for (int x = size * 2 / 3 - width; x <= size * 2 / 3 + width; x++) { if (x >= 0 && x < size && y >= 0 && y < size) data[y * size + x] = mountain; } }
        }
        private void DrawGeologicalIcon(Color[] data, int size) {
            Color[] layers = { new Color(160, 82, 45), new Color(139, 69, 19), new Color(205, 133, 63) }; int layerHeight = size / 3; for (int y = 0; y < size; y++) { Color layerColor = layers[y / layerHeight % layers.Length]; for (int x = 0; x < size; x++) data[y * size + x] = layerColor; }
        }
        private void DrawTectonicIcon(Color[] data, int size) {
            Color red = Color.Red; int centerY = size / 2; for (int x = 0; x < size; x++) { int y = centerY + (x % 4 < 2 ? -2 : 2); if (y >= 0 && y < size) { data[y * size + x] = red; if (y + 1 < size) data[(y + 1) * size + x] = red; } }
        }
        private void DrawVolcanoIcon(Color[] data, int size) {
            Color brown = new Color(101, 67, 33); Color red = Color.Red; Color orange = Color.Orange; int centerX = size / 2; for (int y = size / 3; y < size; y++) { int width = (y - size / 3) / 2; for (int x = centerX - width; x <= centerX + width; x++) { if (x >= 0 && x < size) data[y * size + x] = brown; } } for (int y = 2; y < size / 3; y++) data[y * size + centerX] = y % 2 == 0 ? red : orange;
        }
        private void DrawCloudsIcon(Color[] data, int size) {
            Color white = Color.White; DrawCircle(data, size, size / 3, size / 2, 4, white); DrawCircle(data, size, size / 2, size / 3, 5, white); DrawCircle(data, size, size * 2 / 3, size / 2, 4, white);
        }
        private void DrawWindIcon(Color[] data, int size) {
            Color cyan = new Color(173, 216, 230); for (int y = 0; y < size; y += 6) { for (int x = 0; x < size - 2; x++) { if (y + 2 < size) data[(y + 2) * size + x] = cyan; } if (y + 1 < size && size - 4 >= 0) data[(y + 1) * size + (size - 4)] = cyan; if (y + 3 < size && size - 4 >= 0) data[(y + 3) * size + (size - 4)] = cyan; }
        }
        private void DrawPressureIcon(Color[] data, int size) {
            Color gray = Color.Gray; DrawCircleOutline(data, size, size / 2, size / 2, size / 3, gray); int centerX = size / 2; int centerY = size / 2; for (int i = 0; i < size / 3; i++) { int x = centerX + i; int y = centerY - i / 2; if (x < size && y >= 0) data[y * size + x] = Color.Red; }
        }
        private void DrawStormIcon(Color[] data, int size) {
            Color yellow = Color.Yellow; int x = size / 2; for (int y = 2; y < size / 2; y++) { data[y * size + x] = yellow; x--; } x = size / 2 - size / 4; for (int y = size / 2; y < size - 2; y++) { data[y * size + x] = yellow; x++; }
        }
        private void DrawEarthquakeIcon(Color[] data, int size) {
            Color brown = new Color(139, 69, 19); for (int x = 0; x < size; x++) { int y = size / 2 + (int)(Math.Sin(x * 0.5) * 4); if (y >= 0 && y < size) { data[y * size + x] = brown; if (y + 1 < size) data[(y + 1) * size + x] = brown; } }
        }
        private void DrawFaultIcon(Color[] data, int size) {
            Color red = Color.Red; for (int i = 0; i < size; i++) { int x = i; int y = i + (i % 3 == 0 ? 1 : -1); if (x < size && y >= 0 && y < size) data[y * size + x] = red; }
        }
        private void DrawTsunamiIcon(Color[] data, int size) {
            Color blue = new Color(0, 105, 148); Color lightBlue = new Color(135, 206, 250); for (int x = 0; x < size; x++) { int waveHeight = (int)(Math.Sin(x * 0.3) * 6) + size / 2; for (int y = waveHeight; y < size; y++) { if (y >= 0 && y < size) data[y * size + x] = y < waveHeight + 3 ? lightBlue : blue; } }
        }
        private void DrawBiomesIcon(Color[] data, int size) {
            Color[] biomes = { new Color(34, 139, 34), new Color(210, 180, 140), new Color(0, 100, 0), new Color(152, 251, 152) }; for (int y = 0; y < size; y++) { for (int x = 0; x < size; x++) { int index = ((x / (size / 2)) + (y / (size / 2)) * 2) % biomes.Length; data[y * size + x] = biomes[index]; } }
        }
        private void DrawAlbedoIcon(Color[] data, int size) {
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++) data[y * size + x] = x < size / 2 ? Color.White : new Color(50, 50, 50);
        }
        private void DrawRadiationIcon(Color[] data, int size) {
            Color yellow = Color.Yellow; DrawCircle(data, size, size / 2, size / 2, 2, yellow); for (int i = 0; i < 8; i++) { double angle = i * Math.PI / 4; for (int r = 4; r < size / 2; r++) { int x = size / 2 + (int)(Math.Cos(angle) * r); int y = size / 2 + (int)(Math.Sin(angle) * r); if (x >= 0 && x < size && y >= 0 && y < size && r % 3 != 0) data[y * size + x] = yellow; } }
        }
        private void DrawResourcesIcon(Color[] data, int size) {
            Color gold = new Color(255, 215, 0); Color silver = new Color(192, 192, 192); DrawCircle(data, size, size / 3, size / 3, 3, gold); DrawCircle(data, size, size * 2 / 3, size / 2, 3, silver); DrawCircle(data, size, size / 2, size * 2 / 3, 3, gold);
        }
        private void DrawInfrastructureIcon(Color[] data, int size) {
            Color gray = new Color(128, 128, 128); for (int y = size / 3; y < size - 2; y++) for (int x = 2; x < size / 3; x++) data[y * size + x] = gray; for (int y = size / 2; y < size - 2; y++) for (int x = size / 2; x < size * 2 / 3; x++) data[y * size + x] = gray;
        }
        private void DrawPauseIcon(Color[] data, int size) {
            Color white = Color.White; for (int y = 4; y < size - 4; y++) { for (int x = size / 3 - 2; x < size / 3 + 2; x++) data[y * size + x] = white; for (int x = size * 2 / 3 - 2; x < size * 2 / 3 + 2; x++) data[y * size + x] = white; }
        }
        private void DrawSpeedUpIcon(Color[] data, int size) {
            Color green = Color.LimeGreen; for (int y = 0; y < size; y++) { int width = Math.Abs(y - size / 2); for (int x = size / 3 - width / 2; x < size / 3 + width / 2; x++) { if (x >= 0 && x < size) data[y * size + x] = green; } for (int x = size * 2 / 3 - width / 2; x < size * 2 / 3 + width / 2; x++) { if (x >= 0 && x < size) data[y * size + x] = green; } }
        }
        private void DrawSpeedDownIcon(Color[] data, int size) {
            Color orange = Color.Orange; for (int y = 0; y < size; y++) { int width = Math.Abs(y - size / 2); for (int x = size / 3 - width / 2; x < size / 3 + width / 2; x++) { if (x >= 0 && x < size) data[y * size + (size / 3 - (x - size / 3))] = orange; } for (int x = size * 2 / 3 - width / 2; x < size * 2 / 3 + width / 2; x++) { if (x >= 0 && x < size) data[y * size + (size * 2 / 3 - (x - size * 2 / 3))] = orange; } }
        }
        private void DrawSaveIcon(Color[] data, int size) {
            Color blue = new Color(100, 149, 237); Color gray = Color.Gray; for (int y = 2; y < size - 2; y++) for (int x = 2; x < size - 2; x++) { if (y < size / 3 || x == 2 || x == size - 3 || y == size - 3) data[y * size + x] = blue; else if (y > size / 3 && y < size * 2 / 3) data[y * size + x] = gray; }
        }
        private void DrawLoadIcon(Color[] data, int size) {
            Color yellow = new Color(255, 215, 0); for (int y = size / 3; y < size - 2; y++) for (int x = 2; x < size - 2; x++) data[y * size + x] = yellow; for (int x = 2; x < size / 2; x++) for (int y = size / 4; y < size / 3; y++) data[y * size + x] = yellow;
        }
        private void DrawRegenerateIcon(Color[] data, int size) {
            Color green = Color.LimeGreen; DrawCircleOutline(data, size, size / 2, size / 2, size / 3, green); data[2 * size + size / 2] = green; data[2 * size + size / 2 + 1] = green; data[3 * size + size / 2 + 1] = green;
        }
        private void DrawHelpIcon(Color[] data, int size) {
            Color white = Color.White; int centerX = size / 2; for (int x = centerX - 3; x <= centerX + 3; x++) data[4 * size + x] = white; for (int y = 4; y < size / 2; y++) data[y * size + centerX + 3] = white; data[(size / 2) * size + centerX] = white; data[(size / 2 + 2) * size + centerX] = white; data[(size - 4) * size + centerX] = white;
        }
        private void DrawMapIcon(Color[] data, int size) {
            Color tan = new Color(245, 222, 179); Color brown = new Color(139, 69, 19); for (int y = 2; y < size - 2; y++) for (int x = 2; x < size - 2; x++) { if (x % 8 == 0) data[y * size + x] = brown; else data[y * size + x] = tan; }
        }
        private void DrawMinimapIcon(Color[] data, int size) {
            Color blue = new Color(100, 149, 237); Color green = new Color(34, 139, 34); for (int y = 0; y < size; y++) for (int x = 0; x < size; x++) { if (x < size / 2 && y < size / 2) data[y * size + x] = blue; else if (x >= size / 2 || y >= size / 2) data[y * size + x] = green; } DrawRectOutline(data, size, 0, 0, size, size, Color.White);
        }
        private void DrawDayNightIcon(Color[] data, int size) {
            Color yellow = Color.Yellow; Color darkBlue = new Color(25, 25, 112); for (int y = 0; y < size; y++) for (int x = 0; x < size; x++) { if (x < size / 2) { int dx = x - size / 4; int dy = y - size / 2; if (dx * dx + dy * dy < (size / 4) * (size / 4)) data[y * size + x] = yellow; } else { int dx = x - size * 3 / 4; int dy = y - size / 2; if (dx * dx + dy * dy < (size / 4) * (size / 4)) data[y * size + x] = Color.White; else data[y * size + x] = darkBlue; } }
        }
        private void DrawVolcanoOverlayIcon(Color[] data, int size) { DrawVolcanoIcon(data, size); }
        private void DrawRiversIcon(Color[] data, int size) {
            Color blue = new Color(65, 105, 225); for (int y = 0; y < size; y++) { int x = size / 2 + (int)(Math.Sin(y * 0.3) * 4); if (x >= 0 && x < size) { data[y * size + x] = blue; if (x + 1 < size) data[y * size + x + 1] = blue; } }
        }
        private void DrawPlatesIcon(Color[] data, int size) { DrawTectonicIcon(data, size); }
        private void DrawSeedLifeIcon(Color[] data, int size) {
            Color brown = new Color(139, 69, 19); Color green = Color.LimeGreen; DrawCircle(data, size, size / 2, size * 2 / 3, 4, brown); for (int y = size / 3; y < size * 2 / 3; y++) data[y * size + size / 2] = green; data[(size / 3 + 2) * size + size / 2 - 2] = green; data[(size / 3 + 4) * size + size / 2 + 2] = green;
        }
        private void DrawCivilizationIcon(Color[] data, int size) {
            Color gray = new Color(100, 100, 100); int[] heights = { size / 2, size * 2 / 3, size / 3, size - 4 }; for (int i = 0; i < 4; i++) { int startX = i * size / 4; int endX = (i + 1) * size / 4; for (int y = size - heights[i]; y < size - 2; y++) for (int x = startX; x < endX - 1; x++) data[y * size + x] = gray; }
        }
        private void DrawDivineIcon(Color[] data, int size) {
            Color gold = new Color(255, 215, 0); int x = size / 2; for (int y = 2; y < size / 2; y++) { data[y * size + x] = gold; x--; } x = size / 2 - size / 4; for (int y = size / 2; y < size - 2; y++) { data[y * size + x] = gold; x++; }
        }
        private void DrawDisasterIcon(Color[] data, int size) {
            Color red = Color.Red; Color orange = Color.Orange; DrawCircle(data, size, size / 2, size / 2, 4, orange); for (int i = 0; i < 8; i++) { double angle = i * Math.PI / 4; for (int r = 5; r < size / 2; r++) { int x = size / 2 + (int)(Math.Cos(angle) * r); int y = size / 2 + (int)(Math.Sin(angle) * r); if (x >= 0 && x < size && y >= 0 && y < size) data[y * size + x] = red; } }
        }
        private void DrawDiseaseIcon(Color[] data, int size) {
            Color green = new Color(0, 255, 0); Color darkGreen = new Color(0, 128, 0); DrawCircle(data, size, size / 2, size / 2, 5, green); for (int i = 0; i < 6; i++) { double angle = i * Math.PI / 3; for (int r = 6; r < 10; r++) { int x = size / 2 + (int)(Math.Cos(angle) * r); int y = size / 2 + (int)(Math.Sin(angle) * r); if (x >= 0 && x < size && y >= 0 && y < size) data[y * size + x] = darkGreen; } }
        }
        private void DrawPlantIcon(Color[] data, int size) { DrawLifeIcon(data, size); }
        private void DrawStabilizerIcon(Color[] data, int size) {
            Color cyan = Color.Cyan; for (int x = 4; x < size - 4; x++) data[(size / 2) * size + x] = cyan; DrawCircle(data, size, size / 4, size / 2, 3, cyan); DrawCircle(data, size, size * 3 / 4, size / 2, 3, cyan);
        }
        private void DrawGraphIcon(Color[] data, int size) {
            Color green = Color.LimeGreen; for (int x = 2; x < size - 2; x++) { int y = size - 4 - (int)(Math.Sin(x * 0.5) * 4); if (y >= 0 && y < size) data[y * size + x] = green; }
        }
        private void DrawTerraformingIcon(Color[] data, int size) {
            Color brown = new Color(139, 69, 19); Color green = Color.LimeGreen; for (int y = size / 2; y < size; y++) { int width = (y - size / 2) / 2; for (int x = size / 2 - width; x <= size / 2 + width; x++) if (x >= 0 && x < size) data[y * size + x] = brown; } for (int y = 2; y < size / 2; y++) { data[y * size + size / 2] = green; } data[2 * size + size / 2 - 2] = green; data[2 * size + size / 2 + 2] = green;
        }
        private void DrawPlanetControlsIcon(Color[] data, int size) {
            Color gray = Color.Gray; Color blue = Color.Blue; for (int y = 4; y < size - 4; y += 6) { for (int x = 4; x < size - 4; x++) data[y * size + x] = gray; data[y * size + size / 2] = blue; }
        }
        private void DrawCircle(Color[] data, int size, int centerX, int centerY, int radius, Color color) {
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++) { int dx = x - centerX; int dy = y - centerY; if (dx * dx + dy * dy <= radius * radius) data[y * size + x] = color; }
        }
        private void DrawCircleOutline(Color[] data, int size, int centerX, int centerY, int radius, Color color) {
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++) { int dx = x - centerX; int dy = y - centerY; int dist = dx * dx + dy * dy; if (dist >= (radius - 1) * (radius - 1) && dist <= (radius + 1) * (radius + 1)) data[y * size + x] = color; }
        }
        private void DrawRectOutline(Color[] data, int size, int x, int y, int width, int height, Color color) {
            for (int i = x; i < x + width && i < size; i++) { if (y >= 0 && y < size) data[y * size + i] = color; if (y + height - 1 >= 0 && y + height - 1 < size) data[(y + height - 1) * size + i] = color; } for (int i = y; i < y + height && i < size; i++) { if (x >= 0 && x < size) data[i * size + x] = color; if (x + width - 1 >= 0 && x + width - 1 < size) data[i * size + (x + width - 1)] = color; }
        }

        public void Update(MouseState mouseState)
        {
            foreach (var button in buttons)
            {
                button.IsHovered = button.Bounds.Contains(mouseState.Position);
            }

            if (mouseState.LeftButton == ButtonState.Pressed &&
                previousMouseState.LeftButton == ButtonState.Released)
            {
                foreach (var button in buttons)
                {
                    if (button.IsHovered)
                    {
                        button.OnClick?.Invoke();
                        break;
                    }
                }
            }

            previousMouseState = mouseState;
        }

        public void Draw(SpriteBatch spriteBatch, int screenWidth)
        {
            // Draw toolbar background
            spriteBatch.Draw(pixelTexture, new Rectangle(0, 0, screenWidth, toolbarHeight), _toolbarBgColor);

            // Draw bottom border
            spriteBatch.Draw(pixelTexture, new Rectangle(0, toolbarHeight - 1, screenWidth, 1), _separatorColor);

            // Draw buttons
            foreach (var button in buttons)
            {
                // Don't draw buttons that are off-screen
                if (button.Bounds.Right > screenWidth) continue;

                // Button background
                Color bgColor = button.IsHovered ? _buttonHoverColor : _buttonNormalColor;
                spriteBatch.Draw(pixelTexture, button.Bounds, bgColor);

                // Button border (highlighted on hover)
                Color borderColor = button.IsHovered ? Color.White : _buttonBorderColor;
                DrawBorder(spriteBatch, button.Bounds, borderColor);

                // Button icon
                if (button.Icon != null)
                {
                    Rectangle iconRect = new Rectangle(
                        button.Bounds.X + 4,
                        button.Bounds.Y + 4,
                        buttonSize - 8,
                        buttonSize - 8
                    );
                    spriteBatch.Draw(button.Icon, iconRect, Color.White);
                }
            }

            // Draw overflow indicator if needed
            if (buttons.Count > 0 && buttons[buttons.Count - 1].Bounds.Right > screenWidth)
            {
                int indicatorX = screenWidth - 20;
                int indicatorY = toolbarHeight / 2 - 10;
                fontRenderer.DrawString(spriteBatch, ">>", new Vector2(indicatorX, indicatorY), Color.Yellow);
            }

            // Draw tooltip for hovered button
            var hoveredButton = buttons.Find(b => b.IsHovered && b.Bounds.Right <= screenWidth);
            if (hoveredButton != null)
            {
                DrawTooltip(spriteBatch, hoveredButton);
            }
        }

        private void DrawBorder(SpriteBatch spriteBatch, Rectangle rect, Color color)
        {
            // Top
            spriteBatch.Draw(pixelTexture, new Rectangle(rect.X, rect.Y, rect.Width, 1), color);
            // Bottom
            spriteBatch.Draw(pixelTexture, new Rectangle(rect.X, rect.Y + rect.Height - 1, rect.Width, 1), color);
            // Left
            spriteBatch.Draw(pixelTexture, new Rectangle(rect.X, rect.Y, 1, rect.Height), color);
            // Right
            spriteBatch.Draw(pixelTexture, new Rectangle(rect.X + rect.Width - 1, rect.Y, 1, rect.Height), color);
        }

        private void DrawTooltip(SpriteBatch spriteBatch, ToolbarButton button)
        {
            int textWidth = button.Tooltip.Length * 8;
            int textHeight = 20;
            int padding = 8;

            int tooltipWidth = textWidth + padding * 2;
            int tooltipHeight = textHeight + padding * 2;
            int tooltipX = button.Bounds.X;
            int tooltipY = button.Bounds.Y + button.Bounds.Height + 4;

            if (tooltipX + tooltipWidth > graphicsDevice.Viewport.Width)
            {
                tooltipX = graphicsDevice.Viewport.Width - tooltipWidth - 5;
            }
            if (tooltipX < 0)
            {
                tooltipX = 5;
            }

            spriteBatch.Draw(pixelTexture,
                new Rectangle(tooltipX + 2, tooltipY + 2, tooltipWidth, tooltipHeight),
                new Color(0, 0, 0, 100));

            spriteBatch.Draw(pixelTexture,
                new Rectangle(tooltipX, tooltipY, tooltipWidth, tooltipHeight),
                new Color(20, 25, 35, 255));

            DrawBorder(spriteBatch, new Rectangle(tooltipX, tooltipY, tooltipWidth, tooltipHeight),
                new Color(255, 215, 80));

            fontRenderer.DrawString(spriteBatch, button.Tooltip,
                new Vector2(tooltipX + padding, tooltipY + padding),
                Color.White, 14);
        }
    }
}
