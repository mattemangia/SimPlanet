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
        private int toolbarHeight = 36;
        private int buttonSize = 28;
        private int buttonSpacing = 2;
        private int categorySpacing = 8;
        private int leftMargin = 5;
        private int topMargin = 4;

        public ToolbarUI(SimPlanetGame game, GraphicsDevice graphicsDevice, FontRenderer fontRenderer)
        {
            this.game = game;
            this.graphicsDevice = graphicsDevice;
            this.fontRenderer = fontRenderer;
            this.buttons = new List<ToolbarButton>();

            // Create 1x1 white pixel texture for drawing
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

            // Additional Views
            AddButton(ref x, y, "F10", "Biomes", "Extra", () => SetViewMode(RenderMode.Biomes));
            AddButton(ref x, y, "A", "Albedo", "Extra", () => SetViewMode(RenderMode.Albedo));
            AddButton(ref x, y, "F12", "Radiation", "Extra", () => SetViewMode(RenderMode.Radiation));
            AddButton(ref x, y, "J", "Resources", "Extra", () => SetViewMode(RenderMode.Resources));
            AddButton(ref x, y, "O", "Infrastructure", "Extra", () => SetViewMode(RenderMode.Infrastructure));

            x += categorySpacing;

            // Game Controls
            AddButton(ref x, y, "⏯", "Pause/Resume (Space)", "Control", () => game.TogglePause());
            AddButton(ref x, y, "+", "Speed Up (+)", "Control", () => game.IncreaseTimeSpeed());
            AddButton(ref x, y, "-", "Speed Down (-)", "Control", () => game.DecreaseTimeSpeed());
            AddButton(ref x, y, "F5", "Quick Save", "Control", () => game.QuickSave());
            AddButton(ref x, y, "F9", "Quick Load", "Control", () => game.QuickLoad());
            AddButton(ref x, y, "R", "Regenerate Planet", "Control", () => game.RegeneratePlanet());

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
            AddButton(ref x, y, "L", "Seed Life (L)", "Feature", () => game.SeedLife());
            AddButton(ref x, y, "G", "Civilization (G)", "Feature", () => game.ToggleCivilization());
            AddButton(ref x, y, "I", "Divine Powers (I)", "Feature", () => game.ToggleDivinePowers());
            AddButton(ref x, y, "D", "Disasters (D)", "Feature", () => game.ToggleDisasters());
            AddButton(ref x, y, "K", "Diseases (K)", "Feature", () => game.ToggleDiseases());
            AddButton(ref x, y, "T", "Plant Tool (T)", "Feature", () => game.TogglePlantTool());
            AddButton(ref x, y, "Y", "Stabilizer (Y)", "Feature", () => game.ToggleStabilizer());

            // Generate icons for all buttons
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
                Tooltip = $"{tooltip} ({label})",
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

        private Texture2D GenerateIcon(string tooltip, string category)
        {
            Texture2D icon = new Texture2D(graphicsDevice, buttonSize - 4, buttonSize - 4);
            Color[] data = new Color[(buttonSize - 4) * (buttonSize - 4)];

            // Fill with transparent background
            for (int i = 0; i < data.Length; i++)
                data[i] = Color.Transparent;

            int size = buttonSize - 4;

            // Determine icon style based on category and tooltip
            if (tooltip.Contains("Terrain"))
                DrawTerrainIcon(data, size);
            else if (tooltip.Contains("Temperature"))
                DrawTemperatureIcon(data, size);
            else if (tooltip.Contains("Rainfall"))
                DrawRainfallIcon(data, size);
            else if (tooltip.Contains("Life"))
                DrawLifeIcon(data, size);
            else if (tooltip.Contains("Oxygen"))
                DrawOxygenIcon(data, size);
            else if (tooltip.Contains("CO2"))
                DrawCO2Icon(data, size);
            else if (tooltip.Contains("Elevation"))
                DrawElevationIcon(data, size);
            else if (tooltip.Contains("Geological"))
                DrawGeologicalIcon(data, size);
            else if (tooltip.Contains("Tectonic"))
                DrawTectonicIcon(data, size);
            else if (tooltip.Contains("Volcanoes"))
                DrawVolcanoIcon(data, size);
            else if (tooltip.Contains("Clouds"))
                DrawCloudsIcon(data, size);
            else if (tooltip.Contains("Wind"))
                DrawWindIcon(data, size);
            else if (tooltip.Contains("Pressure"))
                DrawPressureIcon(data, size);
            else if (tooltip.Contains("Storms"))
                DrawStormIcon(data, size);
            else if (tooltip.Contains("Earthquakes"))
                DrawEarthquakeIcon(data, size);
            else if (tooltip.Contains("Faults"))
                DrawFaultIcon(data, size);
            else if (tooltip.Contains("Tsunamis"))
                DrawTsunamiIcon(data, size);
            else if (tooltip.Contains("Biomes"))
                DrawBiomesIcon(data, size);
            else if (tooltip.Contains("Albedo"))
                DrawAlbedoIcon(data, size);
            else if (tooltip.Contains("Radiation"))
                DrawRadiationIcon(data, size);
            else if (tooltip.Contains("Resources"))
                DrawResourcesIcon(data, size);
            else if (tooltip.Contains("Infrastructure"))
                DrawInfrastructureIcon(data, size);
            else if (tooltip.Contains("Pause"))
                DrawPauseIcon(data, size);
            else if (tooltip.Contains("Speed Up"))
                DrawSpeedUpIcon(data, size);
            else if (tooltip.Contains("Speed Down"))
                DrawSpeedDownIcon(data, size);
            else if (tooltip.Contains("Quick Save"))
                DrawSaveIcon(data, size);
            else if (tooltip.Contains("Quick Load"))
                DrawLoadIcon(data, size);
            else if (tooltip.Contains("Regenerate"))
                DrawRegenerateIcon(data, size);
            else if (tooltip.Contains("Help"))
                DrawHelpIcon(data, size);
            else if (tooltip.Contains("Map Options"))
                DrawMapIcon(data, size);
            else if (tooltip.Contains("Minimap"))
                DrawMinimapIcon(data, size);
            else if (tooltip.Contains("Day/Night"))
                DrawDayNightIcon(data, size);
            else if (tooltip.Contains("Volcano Overlay"))
                DrawVolcanoOverlayIcon(data, size);
            else if (tooltip.Contains("Rivers"))
                DrawRiversIcon(data, size);
            else if (tooltip.Contains("Plates"))
                DrawPlatesIcon(data, size);
            else if (tooltip.Contains("Seed Life"))
                DrawSeedLifeIcon(data, size);
            else if (tooltip.Contains("Civilization"))
                DrawCivilizationIcon(data, size);
            else if (tooltip.Contains("Divine Powers"))
                DrawDivineIcon(data, size);
            else if (tooltip.Contains("Disasters"))
                DrawDisasterIcon(data, size);
            else if (tooltip.Contains("Diseases"))
                DrawDiseaseIcon(data, size);
            else if (tooltip.Contains("Plant Tool"))
                DrawPlantIcon(data, size);
            else if (tooltip.Contains("Stabilizer"))
                DrawStabilizerIcon(data, size);

            icon.SetData(data);
            return icon;
        }

        // Icon drawing methods
        private void DrawTerrainIcon(Color[] data, int size)
        {
            // Draw layered terrain
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    if (y > size * 0.7f)
                        data[y * size + x] = new Color(34, 139, 34); // Green
                    else if (y > size * 0.5f)
                        data[y * size + x] = new Color(139, 69, 19); // Brown
                    else if (y > size * 0.3f)
                        data[y * size + x] = new Color(128, 128, 128); // Gray
                }
            }
        }

        private void DrawTemperatureIcon(Color[] data, int size)
        {
            // Draw thermometer
            int centerX = size / 2;
            for (int y = 2; y < size - 4; y++)
            {
                data[y * size + centerX] = Color.Red;
                data[y * size + centerX - 1] = Color.Red;
            }
            // Bulb at bottom
            for (int y = size - 5; y < size - 1; y++)
            {
                for (int x = centerX - 2; x <= centerX + 1; x++)
                {
                    if (x >= 0 && x < size)
                        data[y * size + x] = Color.Red;
                }
            }
        }

        private void DrawRainfallIcon(Color[] data, int size)
        {
            // Draw rain drops
            Color blue = new Color(30, 144, 255);
            for (int i = 0; i < 4; i++)
            {
                int x = 4 + i * 5;
                for (int y = 3 + i * 2; y < size - 2; y += 4)
                {
                    if (x < size && y < size)
                    {
                        data[y * size + x] = blue;
                        if (y + 1 < size)
                            data[(y + 1) * size + x] = blue;
                    }
                }
            }
        }

        private void DrawLifeIcon(Color[] data, int size)
        {
            // Draw tree/plant shape
            Color green = new Color(0, 200, 0);
            Color brown = new Color(101, 67, 33);
            int centerX = size / 2;

            // Trunk
            for (int y = size * 2 / 3; y < size - 2; y++)
            {
                data[y * size + centerX] = brown;
                data[y * size + centerX - 1] = brown;
            }

            // Foliage (triangle)
            for (int y = 2; y < size * 2 / 3; y++)
            {
                int width = (size * 2 / 3 - y) / 2;
                for (int x = centerX - width; x <= centerX + width; x++)
                {
                    if (x >= 0 && x < size)
                        data[y * size + x] = green;
                }
            }
        }

        private void DrawOxygenIcon(Color[] data, int size)
        {
            // Draw O2 molecule (two connected circles)
            Color cyan = new Color(0, 255, 255);
            DrawCircle(data, size, size / 3, size / 2, 4, cyan);
            DrawCircle(data, size, size * 2 / 3, size / 2, 4, cyan);
            // Connection line
            for (int x = size / 3; x <= size * 2 / 3; x++)
            {
                data[size / 2 * size + x] = cyan;
            }
        }

        private void DrawCO2Icon(Color[] data, int size)
        {
            // Draw CO2 molecule
            Color gray = new Color(180, 180, 180);
            Color red = new Color(255, 100, 100);
            DrawCircle(data, size, size / 2, size / 2, 4, gray); // Center C
            DrawCircle(data, size, size / 4, size / 2, 3, red);  // Left O
            DrawCircle(data, size, size * 3 / 4, size / 2, 3, red); // Right O
        }

        private void DrawElevationIcon(Color[] data, int size)
        {
            // Draw mountain peaks
            Color mountain = new Color(139, 137, 137);
            // First peak
            for (int y = 2; y < size - 2; y++)
            {
                int width = (size - y) / 3;
                for (int x = size / 3 - width; x <= size / 3 + width; x++)
                {
                    if (x >= 0 && x < size && y >= 0 && y < size)
                        data[y * size + x] = mountain;
                }
            }
            // Second peak
            for (int y = 5; y < size - 2; y++)
            {
                int width = (size - y - 2) / 4;
                for (int x = size * 2 / 3 - width; x <= size * 2 / 3 + width; x++)
                {
                    if (x >= 0 && x < size && y >= 0 && y < size)
                        data[y * size + x] = mountain;
                }
            }
        }

        private void DrawGeologicalIcon(Color[] data, int size)
        {
            // Draw layered rocks
            Color[] layers = { new Color(160, 82, 45), new Color(139, 69, 19), new Color(205, 133, 63) };
            int layerHeight = size / 3;
            for (int y = 0; y < size; y++)
            {
                Color layerColor = layers[y / layerHeight % layers.Length];
                for (int x = 0; x < size; x++)
                {
                    data[y * size + x] = layerColor;
                }
            }
        }

        private void DrawTectonicIcon(Color[] data, int size)
        {
            // Draw plate boundaries (zigzag line)
            Color red = Color.Red;
            int centerY = size / 2;
            for (int x = 0; x < size; x++)
            {
                int y = centerY + (x % 4 < 2 ? -2 : 2);
                if (y >= 0 && y < size)
                {
                    data[y * size + x] = red;
                    if (y + 1 < size)
                        data[(y + 1) * size + x] = red;
                }
            }
        }

        private void DrawVolcanoIcon(Color[] data, int size)
        {
            // Draw volcano
            Color brown = new Color(101, 67, 33);
            Color red = Color.Red;
            Color orange = Color.Orange;
            int centerX = size / 2;

            // Mountain
            for (int y = size / 3; y < size; y++)
            {
                int width = (y - size / 3) / 2;
                for (int x = centerX - width; x <= centerX + width; x++)
                {
                    if (x >= 0 && x < size)
                        data[y * size + x] = brown;
                }
            }

            // Lava at top
            for (int y = 2; y < size / 3; y++)
            {
                data[y * size + centerX] = y % 2 == 0 ? red : orange;
            }
        }

        private void DrawCloudsIcon(Color[] data, int size)
        {
            // Draw cloud shape
            Color white = Color.White;
            DrawCircle(data, size, size / 3, size / 2, 4, white);
            DrawCircle(data, size, size / 2, size / 3, 5, white);
            DrawCircle(data, size, size * 2 / 3, size / 2, 4, white);
        }

        private void DrawWindIcon(Color[] data, int size)
        {
            // Draw wind lines
            Color cyan = new Color(173, 216, 230);
            for (int y = 0; y < size; y += 6)
            {
                for (int x = 0; x < size - 2; x++)
                {
                    if (y + 2 < size)
                        data[(y + 2) * size + x] = cyan;
                }
                // Arrow head
                if (y + 1 < size && size - 4 >= 0)
                    data[(y + 1) * size + (size - 4)] = cyan;
                if (y + 3 < size && size - 4 >= 0)
                    data[(y + 3) * size + (size - 4)] = cyan;
            }
        }

        private void DrawPressureIcon(Color[] data, int size)
        {
            // Draw pressure gauge (circle with pointer)
            Color gray = Color.Gray;
            DrawCircleOutline(data, size, size / 2, size / 2, size / 3, gray);
            // Pointer
            int centerX = size / 2;
            int centerY = size / 2;
            for (int i = 0; i < size / 3; i++)
            {
                int x = centerX + i;
                int y = centerY - i / 2;
                if (x < size && y >= 0)
                    data[y * size + x] = Color.Red;
            }
        }

        private void DrawStormIcon(Color[] data, int size)
        {
            // Draw lightning bolt
            Color yellow = Color.Yellow;
            int x = size / 2;
            for (int y = 2; y < size / 2; y++)
            {
                data[y * size + x] = yellow;
                x--;
            }
            x = size / 2 - size / 4;
            for (int y = size / 2; y < size - 2; y++)
            {
                data[y * size + x] = yellow;
                x++;
            }
        }

        private void DrawEarthquakeIcon(Color[] data, int size)
        {
            // Draw seismic wave
            Color brown = new Color(139, 69, 19);
            for (int x = 0; x < size; x++)
            {
                int y = size / 2 + (int)(Math.Sin(x * 0.5) * 4);
                if (y >= 0 && y < size)
                {
                    data[y * size + x] = brown;
                    if (y + 1 < size)
                        data[(y + 1) * size + x] = brown;
                }
            }
        }

        private void DrawFaultIcon(Color[] data, int size)
        {
            // Draw fault line (jagged diagonal)
            Color red = Color.Red;
            for (int i = 0; i < size; i++)
            {
                int x = i;
                int y = i + (i % 3 == 0 ? 1 : -1);
                if (x < size && y >= 0 && y < size)
                    data[y * size + x] = red;
            }
        }

        private void DrawTsunamiIcon(Color[] data, int size)
        {
            // Draw wave
            Color blue = new Color(0, 105, 148);
            Color lightBlue = new Color(135, 206, 250);
            for (int x = 0; x < size; x++)
            {
                int waveHeight = (int)(Math.Sin(x * 0.3) * 6) + size / 2;
                for (int y = waveHeight; y < size; y++)
                {
                    if (y >= 0 && y < size)
                        data[y * size + x] = y < waveHeight + 3 ? lightBlue : blue;
                }
            }
        }

        private void DrawBiomesIcon(Color[] data, int size)
        {
            // Draw different colored regions
            Color[] biomes = {
                new Color(34, 139, 34),   // Forest green
                new Color(210, 180, 140), // Desert tan
                new Color(0, 100, 0),     // Dark green
                new Color(152, 251, 152)  // Light green
            };
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int index = ((x / (size / 2)) + (y / (size / 2)) * 2) % biomes.Length;
                    data[y * size + x] = biomes[index];
                }
            }
        }

        private void DrawAlbedoIcon(Color[] data, int size)
        {
            // Draw half white (ice) half dark (low albedo)
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    data[y * size + x] = x < size / 2 ? Color.White : new Color(50, 50, 50);
                }
            }
        }

        private void DrawRadiationIcon(Color[] data, int size)
        {
            // Draw radiation symbol
            Color yellow = Color.Yellow;
            DrawCircle(data, size, size / 2, size / 2, 2, yellow);
            // Radiation lines
            for (int i = 0; i < 8; i++)
            {
                double angle = i * Math.PI / 4;
                for (int r = 4; r < size / 2; r++)
                {
                    int x = size / 2 + (int)(Math.Cos(angle) * r);
                    int y = size / 2 + (int)(Math.Sin(angle) * r);
                    if (x >= 0 && x < size && y >= 0 && y < size && r % 3 != 0)
                        data[y * size + x] = yellow;
                }
            }
        }

        private void DrawResourcesIcon(Color[] data, int size)
        {
            // Draw pickaxe or gems
            Color gold = new Color(255, 215, 0);
            Color silver = new Color(192, 192, 192);
            DrawCircle(data, size, size / 3, size / 3, 3, gold);
            DrawCircle(data, size, size * 2 / 3, size / 2, 3, silver);
            DrawCircle(data, size, size / 2, size * 2 / 3, 3, gold);
        }

        private void DrawInfrastructureIcon(Color[] data, int size)
        {
            // Draw buildings
            Color gray = new Color(128, 128, 128);
            // Building 1
            for (int y = size / 3; y < size - 2; y++)
            {
                for (int x = 2; x < size / 3; x++)
                {
                    data[y * size + x] = gray;
                }
            }
            // Building 2
            for (int y = size / 2; y < size - 2; y++)
            {
                for (int x = size / 2; x < size * 2 / 3; x++)
                {
                    data[y * size + x] = gray;
                }
            }
        }

        private void DrawPauseIcon(Color[] data, int size)
        {
            // Draw pause symbol (two bars)
            Color white = Color.White;
            for (int y = 4; y < size - 4; y++)
            {
                for (int x = size / 3 - 2; x < size / 3 + 2; x++)
                    data[y * size + x] = white;
                for (int x = size * 2 / 3 - 2; x < size * 2 / 3 + 2; x++)
                    data[y * size + x] = white;
            }
        }

        private void DrawSpeedUpIcon(Color[] data, int size)
        {
            // Draw fast-forward (two triangles)
            Color green = Color.LimeGreen;
            for (int y = 0; y < size; y++)
            {
                int width = Math.Abs(y - size / 2);
                for (int x = size / 3 - width / 2; x < size / 3 + width / 2; x++)
                {
                    if (x >= 0 && x < size)
                        data[y * size + x] = green;
                }
                for (int x = size * 2 / 3 - width / 2; x < size * 2 / 3 + width / 2; x++)
                {
                    if (x >= 0 && x < size)
                        data[y * size + x] = green;
                }
            }
        }

        private void DrawSpeedDownIcon(Color[] data, int size)
        {
            // Draw rewind (two triangles pointing left)
            Color orange = Color.Orange;
            for (int y = 0; y < size; y++)
            {
                int width = Math.Abs(y - size / 2);
                for (int x = size / 3 - width / 2; x < size / 3 + width / 2; x++)
                {
                    if (x >= 0 && x < size)
                        data[y * size + (size / 3 - (x - size / 3))] = orange;
                }
                for (int x = size * 2 / 3 - width / 2; x < size * 2 / 3 + width / 2; x++)
                {
                    if (x >= 0 && x < size)
                        data[y * size + (size * 2 / 3 - (x - size * 2 / 3))] = orange;
                }
            }
        }

        private void DrawSaveIcon(Color[] data, int size)
        {
            // Draw floppy disk
            Color blue = new Color(100, 149, 237);
            Color gray = Color.Gray;
            // Outline
            for (int y = 2; y < size - 2; y++)
            {
                for (int x = 2; x < size - 2; x++)
                {
                    if (y < size / 3 || x == 2 || x == size - 3 || y == size - 3)
                        data[y * size + x] = blue;
                    else if (y > size / 3 && y < size * 2 / 3)
                        data[y * size + x] = gray;
                }
            }
        }

        private void DrawLoadIcon(Color[] data, int size)
        {
            // Draw folder
            Color yellow = new Color(255, 215, 0);
            for (int y = size / 3; y < size - 2; y++)
            {
                for (int x = 2; x < size - 2; x++)
                {
                    data[y * size + x] = yellow;
                }
            }
            // Folder tab
            for (int x = 2; x < size / 2; x++)
            {
                for (int y = size / 4; y < size / 3; y++)
                {
                    data[y * size + x] = yellow;
                }
            }
        }

        private void DrawRegenerateIcon(Color[] data, int size)
        {
            // Draw circular arrow
            Color green = Color.LimeGreen;
            DrawCircleOutline(data, size, size / 2, size / 2, size / 3, green);
            // Arrow head
            data[2 * size + size / 2] = green;
            data[2 * size + size / 2 + 1] = green;
            data[3 * size + size / 2 + 1] = green;
        }

        private void DrawHelpIcon(Color[] data, int size)
        {
            // Draw question mark
            Color white = Color.White;
            int centerX = size / 2;
            // Top curve
            for (int x = centerX - 3; x <= centerX + 3; x++)
            {
                data[4 * size + x] = white;
            }
            // Right side
            for (int y = 4; y < size / 2; y++)
            {
                data[y * size + centerX + 3] = white;
            }
            // Middle
            data[(size / 2) * size + centerX] = white;
            data[(size / 2 + 2) * size + centerX] = white;
            // Dot
            data[(size - 4) * size + centerX] = white;
        }

        private void DrawMapIcon(Color[] data, int size)
        {
            // Draw map with fold lines
            Color tan = new Color(245, 222, 179);
            Color brown = new Color(139, 69, 19);
            for (int y = 2; y < size - 2; y++)
            {
                for (int x = 2; x < size - 2; x++)
                {
                    if (x % 8 == 0)
                        data[y * size + x] = brown;
                    else
                        data[y * size + x] = tan;
                }
            }
        }

        private void DrawMinimapIcon(Color[] data, int size)
        {
            // Draw small map in corner
            Color blue = new Color(100, 149, 237);
            Color green = new Color(34, 139, 34);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    if (x < size / 2 && y < size / 2)
                        data[y * size + x] = blue;
                    else if (x >= size / 2 || y >= size / 2)
                        data[y * size + x] = green;
                }
            }
            // Border
            DrawRectOutline(data, size, 0, 0, size, size, Color.White);
        }

        private void DrawDayNightIcon(Color[] data, int size)
        {
            // Draw half sun, half moon
            Color yellow = Color.Yellow;
            Color darkBlue = new Color(25, 25, 112);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    if (x < size / 2)
                    {
                        // Sun side
                        int dx = x - size / 4;
                        int dy = y - size / 2;
                        if (dx * dx + dy * dy < (size / 4) * (size / 4))
                            data[y * size + x] = yellow;
                    }
                    else
                    {
                        // Moon side
                        int dx = x - size * 3 / 4;
                        int dy = y - size / 2;
                        if (dx * dx + dy * dy < (size / 4) * (size / 4))
                            data[y * size + x] = Color.White;
                        else
                            data[y * size + x] = darkBlue;
                    }
                }
            }
        }

        private void DrawVolcanoOverlayIcon(Color[] data, int size)
        {
            DrawVolcanoIcon(data, size);
        }

        private void DrawRiversIcon(Color[] data, int size)
        {
            // Draw winding river
            Color blue = new Color(65, 105, 225);
            for (int y = 0; y < size; y++)
            {
                int x = size / 2 + (int)(Math.Sin(y * 0.3) * 4);
                if (x >= 0 && x < size)
                {
                    data[y * size + x] = blue;
                    if (x + 1 < size)
                        data[y * size + x + 1] = blue;
                }
            }
        }

        private void DrawPlatesIcon(Color[] data, int size)
        {
            DrawTectonicIcon(data, size);
        }

        private void DrawSeedLifeIcon(Color[] data, int size)
        {
            // Draw seed/sprout
            Color brown = new Color(139, 69, 19);
            Color green = Color.LimeGreen;
            // Seed
            DrawCircle(data, size, size / 2, size * 2 / 3, 4, brown);
            // Sprout
            for (int y = size / 3; y < size * 2 / 3; y++)
            {
                data[y * size + size / 2] = green;
            }
            // Leaves
            data[(size / 3 + 2) * size + size / 2 - 2] = green;
            data[(size / 3 + 4) * size + size / 2 + 2] = green;
        }

        private void DrawCivilizationIcon(Color[] data, int size)
        {
            // Draw city skyline
            Color gray = new Color(100, 100, 100);
            int[] heights = { size / 2, size * 2 / 3, size / 3, size - 4 };
            for (int i = 0; i < 4; i++)
            {
                int startX = i * size / 4;
                int endX = (i + 1) * size / 4;
                for (int y = size - heights[i]; y < size - 2; y++)
                {
                    for (int x = startX; x < endX - 1; x++)
                    {
                        data[y * size + x] = gray;
                    }
                }
            }
        }

        private void DrawDivineIcon(Color[] data, int size)
        {
            // Draw hand/divine symbol
            Color gold = new Color(255, 215, 0);
            // Lightning bolt
            int x = size / 2;
            for (int y = 2; y < size / 2; y++)
            {
                data[y * size + x] = gold;
                x--;
            }
            x = size / 2 - size / 4;
            for (int y = size / 2; y < size - 2; y++)
            {
                data[y * size + x] = gold;
                x++;
            }
        }

        private void DrawDisasterIcon(Color[] data, int size)
        {
            // Draw explosion/burst
            Color red = Color.Red;
            Color orange = Color.Orange;
            DrawCircle(data, size, size / 2, size / 2, 4, orange);
            // Burst lines
            for (int i = 0; i < 8; i++)
            {
                double angle = i * Math.PI / 4;
                for (int r = 5; r < size / 2; r++)
                {
                    int x = size / 2 + (int)(Math.Cos(angle) * r);
                    int y = size / 2 + (int)(Math.Sin(angle) * r);
                    if (x >= 0 && x < size && y >= 0 && y < size)
                        data[y * size + x] = red;
                }
            }
        }

        private void DrawDiseaseIcon(Color[] data, int size)
        {
            // Draw virus/bacteria
            Color green = new Color(0, 255, 0);
            Color darkGreen = new Color(0, 128, 0);
            DrawCircle(data, size, size / 2, size / 2, 5, green);
            // Spikes
            for (int i = 0; i < 6; i++)
            {
                double angle = i * Math.PI / 3;
                for (int r = 6; r < 10; r++)
                {
                    int x = size / 2 + (int)(Math.Cos(angle) * r);
                    int y = size / 2 + (int)(Math.Sin(angle) * r);
                    if (x >= 0 && x < size && y >= 0 && y < size)
                        data[y * size + x] = darkGreen;
                }
            }
        }

        private void DrawPlantIcon(Color[] data, int size)
        {
            // Draw tree
            DrawLifeIcon(data, size);
        }

        private void DrawStabilizerIcon(Color[] data, int size)
        {
            // Draw balance/equilibrium symbol
            Color cyan = Color.Cyan;
            // Horizontal line
            for (int x = 4; x < size - 4; x++)
            {
                data[(size / 2) * size + x] = cyan;
            }
            // Balance points
            DrawCircle(data, size, size / 4, size / 2, 3, cyan);
            DrawCircle(data, size, size * 3 / 4, size / 2, 3, cyan);
        }

        // Helper methods for drawing shapes
        private void DrawCircle(Color[] data, int size, int centerX, int centerY, int radius, Color color)
        {
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int dx = x - centerX;
                    int dy = y - centerY;
                    if (dx * dx + dy * dy <= radius * radius)
                        data[y * size + x] = color;
                }
            }
        }

        private void DrawCircleOutline(Color[] data, int size, int centerX, int centerY, int radius, Color color)
        {
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int dx = x - centerX;
                    int dy = y - centerY;
                    int dist = dx * dx + dy * dy;
                    if (dist >= (radius - 1) * (radius - 1) && dist <= (radius + 1) * (radius + 1))
                        data[y * size + x] = color;
                }
            }
        }

        private void DrawRectOutline(Color[] data, int size, int x, int y, int width, int height, Color color)
        {
            for (int i = x; i < x + width && i < size; i++)
            {
                if (y >= 0 && y < size)
                    data[y * size + i] = color;
                if (y + height - 1 >= 0 && y + height - 1 < size)
                    data[(y + height - 1) * size + i] = color;
            }
            for (int i = y; i < y + height && i < size; i++)
            {
                if (x >= 0 && x < size)
                    data[i * size + x] = color;
                if (x + width - 1 >= 0 && x + width - 1 < size)
                    data[i * size + (x + width - 1)] = color;
            }
        }

        public void Update(MouseState mouseState)
        {
            // Update hover states
            foreach (var button in buttons)
            {
                button.IsHovered = button.Bounds.Contains(mouseState.Position);
            }

            // Handle clicks
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
            spriteBatch.Draw(pixelTexture, new Rectangle(0, 0, screenWidth, toolbarHeight),
                new Color(40, 40, 40, 230));

            // Draw separator line
            spriteBatch.Draw(pixelTexture, new Rectangle(0, toolbarHeight - 1, screenWidth, 1),
                new Color(100, 100, 100));

            // Draw buttons
            foreach (var button in buttons)
            {
                // Button background
                Color bgColor = button.IsHovered ? new Color(80, 80, 80) : new Color(60, 60, 60);
                spriteBatch.Draw(pixelTexture, button.Bounds, bgColor);

                // Button border
                DrawBorder(spriteBatch, button.Bounds, button.IsHovered ? Color.White : new Color(100, 100, 100));

                // Button icon
                if (button.Icon != null)
                {
                    Rectangle iconRect = new Rectangle(
                        button.Bounds.X + 2,
                        button.Bounds.Y + 2,
                        buttonSize - 4,
                        buttonSize - 4
                    );
                    spriteBatch.Draw(button.Icon, iconRect, Color.White);
                }
            }

            // Draw tooltip for hovered button
            var hoveredButton = buttons.Find(b => b.IsHovered);
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
            // Measure text (approximate)
            int textWidth = button.Tooltip.Length * 7;
            int textHeight = 20;
            int padding = 6;

            // Position tooltip below button
            int tooltipX = button.Bounds.X;
            int tooltipY = button.Bounds.Y + button.Bounds.Height + 2;
            int tooltipWidth = textWidth + padding * 2;
            int tooltipHeight = textHeight + padding * 2;

            // Draw tooltip background
            spriteBatch.Draw(pixelTexture,
                new Rectangle(tooltipX, tooltipY, tooltipWidth, tooltipHeight),
                new Color(20, 20, 20, 240));

            // Draw tooltip border
            DrawBorder(spriteBatch, new Rectangle(tooltipX, tooltipY, tooltipWidth, tooltipHeight),
                Color.White);

            // Draw tooltip text
            fontRenderer.DrawString(spriteBatch, button.Tooltip,
                new Vector2(tooltipX + padding, tooltipY + padding),
                Color.White, 0.8f);
        }
    }
}
