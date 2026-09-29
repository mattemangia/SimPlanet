using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;

namespace SimPlanet
{
    /// <summary>
    /// Top toolbar: labelled drop-down menus for view modes, tools and overlays.
    /// </summary>
    public class ToolbarUI
    {
        private class ToolbarButton
        {
            public Rectangle Bounds { get; set; }
            // Tooltip doubles as the key used to pick a procedural icon (see GenerateIcon)
            public required string Tooltip { get; set; }
            public string Label { get; set; } = "";
            public string Hotkey { get; set; } = "";
            public RenderMode? Mode { get; set; }
            public Action? OnClick { get; set; }
            public Texture2D? Icon { get; set; }
            public bool IsHovered { get; set; }
            public required string Category { get; set; }
            // Dropdown support
            public bool IsGroup { get; set; }
            public bool IsGroupOpen { get; set; }
            public List<ToolbarButton> SubButtons { get; set; } = new List<ToolbarButton>();
        }

        private List<ToolbarButton> buttons;
        private ToolbarButton? activeGroup = null;
        private Texture2D pixelTexture;
        private GraphicsDevice graphicsDevice;
        private SimPlanetGame game;
        private FontRenderer fontRenderer;
        private MouseState previousMouseState;
        private int toolbarHeight = 44;
        private int buttonSize = 36;
        private int buttonHeight = 34;
        private int buttonSpacing = 4;
        private int categorySpacing = 14;
        private int leftMargin = 8;
        private int topMargin = 5;
        private const int IconDrawSize = 22;
        private const int MenuRowHeight = 30;
        private const int MenuWidthMin = 220;
        private Rectangle _menuRect = Rectangle.Empty;

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

        /// <summary>True while the mouse is over the toolbar or an open menu.</summary>
        public bool IsMouseOver { get; private set; }

        /// <summary>
        /// True while a click that started on the toolbar/menu is in progress (including the
        /// release frame), so the map underneath does not also react to it.
        /// </summary>
        public bool IsCapturingMouse => IsMouseOver || _pressStartedOnToolbar;
        private bool _pressStartedOnToolbar;

        private void InitializeButtons()
        {
            // --- 1. TERRAIN GROUP (Key 1) ---
            var terrainGroup = CreateGroupButton("Terrain Group (1)", "Terrain", "Terrain", "1");
            AddModeButton(terrainGroup, "Terrain View", "Terrain", RenderMode.Terrain);
            AddModeButton(terrainGroup, "Terrain Clean", "Terrain (no overlays)", RenderMode.TerrainClean);
            AddModeButton(terrainGroup, "Elevation View", "Elevation", RenderMode.Elevation);
            AddModeButton(terrainGroup, "Biomes", "Biomes", RenderMode.Biomes);
            buttons.Add(terrainGroup);

            // --- 2. WEATHER GROUP (Key 2) ---
            var weatherGroup = CreateGroupButton("Weather Group (2)", "Weather", "Weather", "2");
            AddModeButton(weatherGroup, "Temperature", "Temperature", RenderMode.Temperature);
            AddModeButton(weatherGroup, "Rainfall", "Rainfall", RenderMode.Rainfall);
            AddModeButton(weatherGroup, "Pressure", "Air Pressure", RenderMode.Pressure);
            AddModeButton(weatherGroup, "Wind", "Wind", RenderMode.Wind);
            AddModeButton(weatherGroup, "Clouds", "Clouds", RenderMode.Clouds);
            AddModeButton(weatherGroup, "Storms", "Storms", RenderMode.Storms);
            buttons.Add(weatherGroup);

            // --- 3. ATMOSPHERE GROUP (Key 3) ---
            var atmoGroup = CreateGroupButton("Atmosphere Group (3)", "Atmosphere", "Atmosphere", "3");
            AddModeButton(atmoGroup, "Oxygen", "Oxygen", RenderMode.Oxygen);
            AddModeButton(atmoGroup, "CO2", "Carbon Dioxide", RenderMode.CO2);
            AddModeButton(atmoGroup, "Radiation", "Radiation", RenderMode.Radiation);
            AddModeButton(atmoGroup, "Albedo", "Albedo", RenderMode.Albedo);
            AddModeButton(atmoGroup, "Spectral Bands", "Spectral Bands", RenderMode.SpectralBands);
            AddModeButton(atmoGroup, "Auroras", "Auroras", RenderMode.Auroras);
            buttons.Add(atmoGroup);

            // --- 4. GEOLOGY GROUP (Key 4) ---
            var geoGroup = CreateGroupButton("Geology Group (4)", "Geology", "Geology", "4");
            AddModeButton(geoGroup, "Geological View", "Rock Types", RenderMode.Geological);
            AddModeButton(geoGroup, "Tectonic Plates", "Tectonic Plates", RenderMode.TectonicPlates);
            AddModeButton(geoGroup, "Volcanoes", "Volcanoes", RenderMode.Volcanoes);
            AddModeButton(geoGroup, "Faults", "Faults", RenderMode.Faults);
            AddModeButton(geoGroup, "Earthquakes", "Earthquakes", RenderMode.Earthquakes);
            AddModeButton(geoGroup, "Tsunamis", "Tsunamis", RenderMode.Tsunamis);
            buttons.Add(geoGroup);

            // --- 5. LIFE & CIV GROUP (Key 5) ---
            var lifeGroup = CreateGroupButton("Life Group (5)", "Life", "Life & Civ", "5");
            AddModeButton(lifeGroup, "Life View", "Life / Biomass", RenderMode.Life);
            AddModeButton(lifeGroup, "Civilizations", "Nations (political)", RenderMode.Civilizations);
            AddModeButton(lifeGroup, "Resources", "Resources", RenderMode.Resources);
            buttons.Add(lifeGroup);

            // --- 6. SOCIETY GROUP (Key 6) ---
            var societyGroup = CreateGroupButton("Society Group (6)", "Society", "Society", "6");
            AddModeButton(societyGroup, "Society: Power grid", "Power grid", RenderMode.Electricity);
            AddModeButton(societyGroup, "Society: Energy", "Energy sources", RenderMode.Energy);
            AddModeButton(societyGroup, "Society: Armaments", "Armaments", RenderMode.Armaments);
            AddModeButton(societyGroup, "Society: Government", "Governments", RenderMode.Governments);
            AddModeButton(societyGroup, "Society: Internet", "Internet", RenderMode.Internet);
            AddModeButton(societyGroup, "Society: Infrastructure", "Infrastructure", RenderMode.Infrastructure);
            AddModeButton(societyGroup, "Society: Epidemics", "Epidemics", RenderMode.Epidemics);
            AddModeButton(societyGroup, "Society: Migrations", "Migrations", RenderMode.Migrations);
            AddModeButton(societyGroup, "Society: Spy networks", "Spy networks", RenderMode.SpyNetworks);
            AddSubButton(societyGroup, "Society: Nation details", "Feature", "Nation details...", "", () => game.OpenNationPanel());
            buttons.Add(societyGroup);

            // --- TOOLS & FEATURES GROUP ---
            var toolsGroup = CreateGroupButton("Tools & Features", "Tools", "Tools", "", categorySpacing);
            AddSubButton(toolsGroup, "Life Painter (L)", "Feature", "Life Painter", "L", () => game.ToggleLifePainter());
            AddSubButton(toolsGroup, "Plant Tool", "Feature", "Plant / Seed Civilization", "", () => game.TogglePlantTool());
            AddSubButton(toolsGroup, "Civilization (G)", "Feature", "Civilization Control", "G", () => game.ToggleCivilization());
            AddSubButton(toolsGroup, "Divine Powers (I)", "Feature", "Divine Powers", "I", () => game.ToggleDivinePowers());
            AddSubButton(toolsGroup, "Disasters (D)", "Feature", "Disasters", "D", () => game.ToggleDisasters());
            AddSubButton(toolsGroup, "Diseases (K)", "Feature", "Diseases", "K", () => game.ToggleDiseases());
            AddSubButton(toolsGroup, "Terraforming Tool (T)", "Feature", "Terraforming", "T", () => game.ToggleTerraformingTool());
            AddSubButton(toolsGroup, "Manual Fault Tool", "Feature", "Draw Faults", "U", () => game.ToggleManualFaultTool());
            AddSubButton(toolsGroup, "Geological Profile (J)", "Feature", "Geological Profile", "J", () => game.ToggleProfileTool());
            AddSubButton(toolsGroup, "Planet Controls (X)", "Feature", "Planet Controls", "X", () => game.TogglePlanetControls());
            buttons.Add(toolsGroup);

            // --- OVERLAYS & UI GROUP ---
            var uiGroup = CreateGroupButton("Overlays & UI", "Overlays", "Overlays", "");
            AddSubButton(uiGroup, "Chronicle", "UI", "World Chronicle", "O", () => game.ToggleChronicle());
            AddSubButton(uiGroup, "Geological Log (E)", "UI", "Geological Event Log", "E", () => game.ToggleGeologicalEvents());
            AddSubButton(uiGroup, "Graphs (Y)", "UI", "Graphs", "Y", () => game.ToggleGraphs());
            AddSubButton(uiGroup, "Minimap (P)", "UI", "3D Globe", "P", () => game.ToggleMinimap());
            AddSubButton(uiGroup, "Day/Night (C)", "UI", "Day / Night", "C", () => game.ToggleDayNight());
            AddSubButton(uiGroup, "Volcano Overlay (V)", "UI", "Volcanoes", "V", () => game.ToggleVolcanoes());
            AddSubButton(uiGroup, "Rivers (B)", "UI", "Rivers", "B", () => game.ToggleRivers());
            AddSubButton(uiGroup, "Wildlife herds", "UI", "Wildlife herds", "", () => game.ToggleWildlife());
            AddSubButton(uiGroup, "Plates (N)", "UI", "Plate Boundaries", "N", () => game.TogglePlates());
            AddSubButton(uiGroup, "Earthquakes Overlay (.)", "UI", "Earthquakes", ".", () => game.ToggleEarthquakes());
            AddSubButton(uiGroup, "Stabilizer (\\)", "UI", "Auto-Stabilizer", "\\", () => game.ToggleStabilizer());
            buttons.Add(uiGroup);

            LayoutButtons();

            // Generate icons for all buttons (society entries reuse the anti-aliased map sprites)
            foreach (var button in buttons)
            {
                button.Icon = GetSpriteIcon(button.Tooltip) ?? GenerateIcon(button.Tooltip, button.Category);
                foreach (var sub in button.SubButtons)
                {
                    sub.Icon = GetSpriteIcon(sub.Tooltip) ?? GenerateIcon(sub.Tooltip, sub.Category);
                }
            }
        }

        private int _groupGapBefore = 0;
        private readonly Dictionary<ToolbarButton, int> _gaps = new();

        private ToolbarButton CreateGroupButton(string tooltip, string category, string label, string hotkey, int gapBefore = 0)
        {
            var button = new ToolbarButton
            {
                Tooltip = tooltip,
                Category = category,
                Label = label,
                Hotkey = hotkey,
                IsGroup = true,
                OnClick = null // Handled in Update
            };
            // Set group click action to toggle itself
            button.OnClick = () => ToggleGroup(button);
            _gaps[button] = gapBefore;
            return button;
        }

        /// <summary>Sizes the group buttons from their labels.</summary>
        private void LayoutButtons()
        {
            int x = leftMargin;
            foreach (var button in buttons)
            {
                x += _gaps.GetValueOrDefault(button, _groupGapBefore);
                int textW = (int)UITheme.Measure(button.Label, UITheme.FontNormal).X;
                int w = 8 + IconDrawSize + 6 + textW + 18;
                button.Bounds = new Rectangle(x, topMargin, w, buttonHeight);
                x += w + buttonSpacing;
            }
        }

        private void AddModeButton(ToolbarButton group, string tooltip, string label, RenderMode mode)
        {
            AddSubButton(group, tooltip, group.Category, label, "", () => SetViewMode(mode));
            group.SubButtons[^1].Mode = mode;
        }

        private void AddSubButton(ToolbarButton group, string tooltip, string category, string label, string hotkey, Action onClick)
        {
            // Sub-buttons position will be calculated when group opens
            var button = new ToolbarButton
            {
                Bounds = Rectangle.Empty,
                Tooltip = tooltip,
                Category = category,
                Label = label,
                Hotkey = hotkey,
                OnClick = () => {
                    onClick?.Invoke();
                    CloseActiveGroup(); // Close group after selection
                }
            };
            group.SubButtons.Add(button);
        }

        private void ToggleGroup(ToolbarButton group)
        {
            if (activeGroup == group)
            {
                CloseActiveGroup();
            }
            else
            {
                CloseActiveGroup();
                activeGroup = group;
                activeGroup.IsGroupOpen = true;

                // Lay out the drop-down menu as a vertical list below the group button
                int menuW = MenuWidthMin;
                foreach (var sub in activeGroup.SubButtons)
                {
                    int w = 12 + IconDrawSize + 10 + (int)UITheme.Measure(sub.Label, UITheme.FontNormal).X + 40;
                    menuW = Math.Max(menuW, w);
                }
                int menuX = Math.Min(group.Bounds.X, graphicsDevice.Viewport.Width - menuW - 4);
                int menuY = toolbarHeight + 2;
                int y = menuY + 4;
                foreach (var sub in activeGroup.SubButtons)
                {
                    sub.Bounds = new Rectangle(menuX + 4, y, menuW - 8, MenuRowHeight);
                    y += MenuRowHeight;
                }
                _menuRect = new Rectangle(menuX, menuY, menuW, y - menuY + 4);
            }
        }

        private void CloseActiveGroup()
        {
            if (activeGroup != null)
            {
                activeGroup.IsGroupOpen = false;
                activeGroup = null;
            }
            _menuRect = Rectangle.Empty;
        }

        private Texture2D? GetSpriteIcon(string tooltip)
        {
            MapIcons icons;
            try { icons = MapIcons.GetShared(graphicsDevice); }
            catch (Exception) { return null; }
            if (tooltip.StartsWith("Society Group")) return icons.Capitol;
            if (tooltip.StartsWith("Wildlife")) return icons.Herd;
            if (!tooltip.StartsWith("Society:")) return null;
            if (tooltip.Contains("Power grid")) return icons.Bolt;
            if (tooltip.Contains("Energy")) return icons.SolarPanel;
            if (tooltip.Contains("Armaments")) return icons.Trefoil;
            if (tooltip.Contains("Government")) return icons.Capitol;
            if (tooltip.Contains("Internet")) return icons.Network;
            if (tooltip.Contains("Infrastructure")) return icons.Plane;
            if (tooltip.Contains("Epidemics")) return icons.Biohazard;
            if (tooltip.Contains("Migrations")) return icons.Migration;
            if (tooltip.Contains("Spy networks")) return icons.Eye;
            if (tooltip.Contains("Nation details")) return icons.Crown;
            return null;
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
            else if (tooltip.Contains("Geological") || tooltip.Contains("Geology Group")) DrawGeologicalIcon(data, size);
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
            else if (tooltip.Contains("Manual Fault")) DrawFaultIcon(data, size);
            else if (tooltip.Contains("Geological Profile")) DrawProfileIcon(data, size);
            else if (tooltip.Contains("Planet Controls")) DrawPlanetControlsIcon(data, size);
            else if (tooltip.Contains("Spectral")) DrawSpectralIcon(data, size);
            else if (tooltip.Contains("Auroras")) DrawAuroraIcon(data, size);
            else if (tooltip.Contains("Geological Log")) DrawGeologicalLogIcon(data, size);
            else if (tooltip.Contains("Chronicle")) DrawChronicleIcon(data, size);
            // New Group Icons
            else if (tooltip.Contains("Weather Group")) DrawWeatherGroupIcon(data, size);
            else if (tooltip.Contains("Atmosphere Group")) DrawAtmosphereGroupIcon(data, size);
            else if (tooltip.Contains("Tools")) DrawToolsGroupIcon(data, size);
            else if (tooltip.Contains("Overlays")) DrawOverlaysGroupIcon(data, size);

            icon.SetData(data);
            return icon;
        }

        // ... Include all previous Draw*Icon methods here ...
        private void DrawAuroraIcon(Color[] data, int size)
        {
            Color green = new Color(50, 255, 100);
            Color purple = new Color(200, 50, 255);
            for (int x = 0; x < size; x++)
            {
                int y = size / 2 + (int)(Math.Sin(x * 0.2) * 5);
                if (y >= 0 && y < size)
                {
                    data[y * size + x] = green;
                    if (y > 0) data[(y - 1) * size + x] = purple;
                }
            }
        }

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

        private void DrawWeatherGroupIcon(Color[] data, int size)
        {
            // Sun (top left)
            Color yellow = Color.Yellow;
            DrawCircle(data, size, size / 3, size / 3, size / 5, yellow);

            // Cloud (center-ish)
            Color white = Color.White;
            DrawCircle(data, size, size / 2, size / 2, size / 5, white);
            DrawCircle(data, size, size * 2 / 3, size / 2, size / 6, white);
            DrawCircle(data, size, size / 2 + size / 6, size / 2 - size / 8, size / 6, white);
        }

        private void DrawAtmosphereGroupIcon(Color[] data, int size)
        {
            Color skyBlue = new Color(135, 206, 235);
            Color spaceBlue = new Color(25, 25, 112);

            // Draw gradient background or planet edge
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int dist = (x - size / 2) * (x - size / 2) + (y - size) * (y - size);
                    if (dist < (size * 0.8) * (size * 0.8))
                    {
                        data[y * size + x] = skyBlue; // Planet/Atmo
                    }
                    else if (dist < size * size)
                    {
                        data[y * size + x] = Color.Lerp(skyBlue, spaceBlue, 0.5f); // Fade
                    }
                }
            }
        }

        private void DrawToolsGroupIcon(Color[] data, int size)
        {
            Color silver = Color.Silver;
            // Draw a wrench shape (handle + head)

            // Handle (diagonal)
            for (int i = 0; i < size / 2; i++)
            {
                int x = size / 4 + i;
                int y = size * 3 / 4 - i;

                // Draw thick line
                for(int w = -2; w <= 2; w++)
                {
                    int px = x + w;
                    int py = y + w;
                    if(px >= 0 && px < size && py >= 0 && py < size)
                        data[py * size + px] = silver;
                }
            }

            // Head (C shape at top right)
            int headX = size * 3 / 4;
            int headY = size / 4;
            DrawCircleOutline(data, size, headX, headY, size / 6, silver);
        }

        private void DrawOverlaysGroupIcon(Color[] data, int size)
        {
            Color layer1 = new Color(200, 200, 200, 200);
            Color layer2 = new Color(100, 150, 255, 200);

            // Back square
            DrawRectOutline(data, size, size/4, size/4, size/2, size/2, layer1);

            // Front square (offset)
            DrawRectOutline(data, size, size/3, size/3, size/2, size/2, layer2);
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
        private void DrawProfileIcon(Color[] data, int size) {
            Color yellow = Color.Yellow; Color brown = new Color(139, 69, 19);
            // Draw a line (surface)
            for(int x=0; x<size; x++) {
                int y = size/2;
                if(y >= 0 && y < size) data[y*size+x] = Color.White;
            }
            // Draw layers below
            for(int x=0; x<size; x++) {
                for(int y=size/2+1; y<size; y++) {
                   data[y*size+x] = brown;
                }
            }
            // Draw vertical cut line
            for(int y=size/4; y<size*3/4; y++) {
                 data[y*size+size/2] = yellow;
            }
        }
        private void DrawPlanetControlsIcon(Color[] data, int size) {
            Color gray = Color.Gray; Color blue = Color.Blue; for (int y = 4; y < size - 4; y += 6) { for (int x = 4; x < size - 4; x++) data[y * size + x] = gray; data[y * size + size / 2] = blue; }
        }
        private void DrawGeologicalLogIcon(Color[] data, int size) {
            Color white = Color.White; Color gray = Color.Gray;
            // Draw list background
            DrawRectOutline(data, size, 4, 4, size - 8, size - 8, gray);
            // Draw lines
            for (int y = 8; y < size - 8; y += 4) {
                for (int x = 8; x < size - 8; x++) {
                    data[y * size + x] = white;
                }
            }
        }
        private void DrawChronicleIcon(Color[] data, int size) {
            Color parchment = new Color(230, 210, 160); Color ink = new Color(110, 80, 40);
            for (int y = 4; y < size - 4; y++) for (int x = 6; x < size - 6; x++) data[y * size + x] = parchment;
            for (int x = 4; x < size - 4; x++) { data[4 * size + x] = ink; data[(size - 5) * size + x] = ink; }
            for (int y = 9; y < size - 8; y += 4) for (int x = 9; x < size - 9; x++) data[y * size + x] = ink;
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
            bool clickHandled = false;

            // Update active group sub-buttons first
            if (activeGroup != null)
            {
                foreach (var sub in activeGroup.SubButtons)
                {
                    sub.IsHovered = sub.Bounds.Contains(mouseState.Position);
                }
            }

            // Update main buttons
            foreach (var button in buttons)
            {
                button.IsHovered = button.Bounds.Contains(mouseState.Position);
            }

            // Hovering another group while a menu is open switches menus (classic menu bar behaviour)
            if (activeGroup != null)
            {
                var hoveredGroup = buttons.Find(b => b.IsHovered);
                if (hoveredGroup != null && hoveredGroup != activeGroup)
                {
                    ToggleGroup(hoveredGroup);
                }
            }

            IsMouseOver = mouseState.Y < toolbarHeight || (!_menuRect.IsEmpty && _menuRect.Contains(mouseState.Position));

            // Keep capturing until one frame after the button is released
            if (_pressStartedOnToolbar && mouseState.LeftButton == ButtonState.Released &&
                previousMouseState.LeftButton == ButtonState.Released)
            {
                _pressStartedOnToolbar = false;
            }
            if (mouseState.LeftButton == ButtonState.Pressed && previousMouseState.LeftButton == ButtonState.Released && IsMouseOver)
            {
                _pressStartedOnToolbar = true;
            }

            if (mouseState.LeftButton == ButtonState.Pressed &&
                previousMouseState.LeftButton == ButtonState.Released)
            {
                // Check active group sub-buttons
                if (activeGroup != null)
                {
                    foreach (var sub in activeGroup.SubButtons)
                    {
                        if (sub.IsHovered)
                        {
                            sub.OnClick?.Invoke();
                            clickHandled = true;
                            break;
                        }
                    }
                }

                // Check main buttons if not handled
                if (!clickHandled)
                {
                    foreach (var button in buttons)
                    {
                        if (button.IsHovered)
                        {
                            button.OnClick?.Invoke();
                            clickHandled = true;
                            break;
                        }
                    }
                }

                // Click outside closes group
                if (!clickHandled)
                {
                    CloseActiveGroup();
                }
            }

            previousMouseState = mouseState;
        }

        public void Draw(SpriteBatch spriteBatch, int screenWidth)
        {
            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp);

            // Toolbar background with a subtle vertical gradient and bottom accent line
            spriteBatch.Draw(pixelTexture, new Rectangle(0, 0, screenWidth, toolbarHeight), new Color(18, 24, 38, 252));
            UITheme.FillGradient(spriteBatch, new Rectangle(0, 0, screenWidth, toolbarHeight), Color.White * ((12) / 255f));
            spriteBatch.Draw(pixelTexture, new Rectangle(0, toolbarHeight - 1, screenWidth, 1), UITheme.Border);
            UITheme.FillGradient(spriteBatch, new Rectangle(0, toolbarHeight, screenWidth, 6), new Color(0, 0, 0, 90));

            RenderMode currentMode = game.CurrentRenderMode;

            foreach (var button in buttons)
            {
                if (button.Bounds.Right > screenWidth - 150) continue;

                bool containsCurrentMode = button.SubButtons.Exists(s => s.Mode == currentMode);
                bool highlighted = button.IsGroupOpen || button.IsHovered;
                Color bg = button.IsGroupOpen ? UITheme.ButtonActive :
                           button.IsHovered ? UITheme.ButtonHover :
                           containsCurrentMode ? new Color(34, 52, 82) : new Color(28, 36, 54);
                UITheme.FillRounded(spriteBatch, button.Bounds, bg);
                UITheme.FillGradient(spriteBatch, new Rectangle(button.Bounds.X + 1, button.Bounds.Y + 1, button.Bounds.Width - 2, button.Bounds.Height / 2),
                    Color.White * ((highlighted ? 24 : 12) / 255f));
                UITheme.OutlineRounded(spriteBatch, button.Bounds, highlighted ? UITheme.BorderBright : UITheme.Border);

                // Accent bar on the group that contains the active view
                if (containsCurrentMode)
                {
                    spriteBatch.Draw(pixelTexture, new Rectangle(button.Bounds.X + 6, button.Bounds.Bottom - 3, button.Bounds.Width - 12, 2), UITheme.Accent);
                }

                if (button.Icon != null)
                {
                    var iconRect = new Rectangle(button.Bounds.X + 7, button.Bounds.Y + (button.Bounds.Height - IconDrawSize) / 2, IconDrawSize, IconDrawSize);
                    spriteBatch.Draw(button.Icon, iconRect, Color.White);
                }

                var textSize = UITheme.Measure(button.Label, UITheme.FontNormal);
                float tx = button.Bounds.X + 7 + IconDrawSize + 6;
                float ty = button.Bounds.Y + (button.Bounds.Height - textSize.Y) / 2f;
                UITheme.DrawTextShadowed(spriteBatch, button.Label, new Vector2(tx, ty), UITheme.Text, UITheme.FontNormal);

                // Drop-down caret
                DrawCaret(spriteBatch, button.Bounds.Right - 11, button.Bounds.Center.Y, button.IsGroupOpen);
            }

            // Current view on the right side
            string viewName = GetModeLabel(currentMode);
            string viewText = "View: " + viewName;
            var viewSize = UITheme.Measure(viewText, UITheme.FontNormal);
            var chip = new Rectangle(screenWidth - (int)viewSize.X - 30, topMargin + 3, (int)viewSize.X + 20, buttonHeight - 6);
            UITheme.FillRounded(spriteBatch, chip, new Color(12, 18, 30, 220));
            UITheme.OutlineRounded(spriteBatch, chip, UITheme.AccentDim);
            UITheme.DrawTextCentered(spriteBatch, viewText, chip, UITheme.Accent, UITheme.FontNormal);

            // Drop-down menu
            if (activeGroup != null && !_menuRect.IsEmpty)
            {
                UITheme.DrawPanel(spriteBatch, _menuRect, new Color(18, 24, 38, 250), UITheme.BorderBright);

                foreach (var sub in activeGroup.SubButtons)
                {
                    bool isCurrent = sub.Mode.HasValue && sub.Mode.Value == currentMode;
                    if (sub.IsHovered)
                    {
                        UITheme.FillRounded(spriteBatch, sub.Bounds, UITheme.ButtonHover);
                    }
                    if (isCurrent)
                    {
                        spriteBatch.Draw(pixelTexture, new Rectangle(sub.Bounds.X, sub.Bounds.Y + 5, 3, sub.Bounds.Height - 10), UITheme.Accent);
                    }

                    if (sub.Icon != null)
                    {
                        var iconRect = new Rectangle(sub.Bounds.X + 10, sub.Bounds.Y + (sub.Bounds.Height - IconDrawSize) / 2, IconDrawSize, IconDrawSize);
                        spriteBatch.Draw(sub.Icon, iconRect, Color.White);
                    }

                    var ts = UITheme.Measure(sub.Label, UITheme.FontNormal);
                    UITheme.DrawTextShadowed(spriteBatch, sub.Label,
                        new Vector2(sub.Bounds.X + 10 + IconDrawSize + 10, sub.Bounds.Y + (sub.Bounds.Height - ts.Y) / 2f),
                        isCurrent ? UITheme.Accent : UITheme.Text, UITheme.FontNormal);

                    if (!string.IsNullOrEmpty(sub.Hotkey))
                    {
                        var hk = new Rectangle(sub.Bounds.Right - 28, sub.Bounds.Y + 6, 22, sub.Bounds.Height - 12);
                        UITheme.FillRounded(spriteBatch, hk, new Color(40, 52, 76));
                        UITheme.DrawTextCentered(spriteBatch, sub.Hotkey, hk, UITheme.TextDim, UITheme.FontSmall);
                    }
                }
            }

            // Tooltip for hovered group button (menu rows are self-describing)
            if (activeGroup == null)
            {
                var hovered = buttons.Find(b => b.IsHovered);
                if (hovered != null)
                {
                    string tip = hovered.Label + (string.IsNullOrEmpty(hovered.Hotkey) ? "" : $"  (key {hovered.Hotkey} cycles)") +
                                 "\nClick to open the menu";
                    UITheme.DrawTooltip(spriteBatch, tip, new Point(hovered.Bounds.Center.X, hovered.Bounds.Bottom + 2),
                        graphicsDevice.Viewport.Width, graphicsDevice.Viewport.Height);
                }
            }

            spriteBatch.End();
            spriteBatch.Begin(samplerState: SamplerState.PointClamp);
        }

        private void DrawCaret(SpriteBatch spriteBatch, int cx, int cy, bool open)
        {
            for (int i = 0; i < 4; i++)
            {
                int w = (4 - i) * 2 - 1;
                int y = open ? cy + 2 - i : cy - 2 + i;
                spriteBatch.Draw(pixelTexture, new Rectangle(cx - w / 2, y, w, 1), UITheme.TextDim);
            }
        }

        private string GetModeLabel(RenderMode mode)
        {
            foreach (var group in buttons)
                foreach (var sub in group.SubButtons)
                    if (sub.Mode == mode) return sub.Label;
            return mode.ToString();
        }
    }
}
