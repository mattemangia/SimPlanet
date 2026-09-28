using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System.Text;

namespace SimPlanet;

/// <summary>
/// Handles UI rendering and information display
/// </summary>
public class GameUI
{
    private readonly FontRenderer _font;
    private readonly SpriteBatch _spriteBatch;
    private readonly PlanetMap _map;
    private readonly GraphicsDevice _graphicsDevice;
    private Texture2D _pixelTexture;
    private CivilizationManager? _civilizationManager;
    private WeatherSystem? _weatherSystem;
    private AnimalEvolutionSimulator? _animalEvolutionSimulator;
    private PlanetStabilizer? _planetStabilizer;

    // Cached UI data to prevent per-frame cell scanning
    private Dictionary<LifeForm, int> _cachedLifeStats = new();
    private DateTime _lastStatsUpdate = DateTime.MinValue;
    private const double StatsUpdateIntervalMs = 100; // Update stats every 100ms

    // Scrollbar state
    private int _scrollOffset = 0;
    private int _contentHeight = 0;
    private bool _isDraggingScrollbar = false;
    private int _dragStartY = 0;
    private int _dragStartScrollOffset = 0;
    private Rectangle _lastScrollbarRect = Rectangle.Empty;

    public bool IsMouseOver { get; private set; }
    public bool ShowHelp { get; set; } = false;
    public bool IsFastForwarding { get; set; } = false;
    public float FastForwardProgress { get; set; } = 0f;
    public int FastForwardCurrentYear { get; set; } = 0;

    // UI Theme Colors
    private readonly Color _panelBgColor = new Color(12, 18, 28, 245);
    private readonly Color _panelBorderColor = new Color(60, 90, 140);
    private readonly Color _headerBgColor = new Color(25, 35, 55, 220);
    private readonly Color _subHeaderBgColor = new Color(20, 30, 45, 180);
    private readonly Color _textLabelColor = new Color(170, 185, 205);
    private readonly Color _textValueColor = new Color(220, 235, 255);
    private readonly Color _accentColor = new Color(100, 200, 255);
    private readonly Color _alertColor = new Color(255, 100, 100);
    private readonly Color _goodColor = new Color(100, 220, 120);
    private readonly Color _goldColor = new Color(255, 215, 80);

    public GameUI(SpriteBatch spriteBatch, FontRenderer font, PlanetMap map, GraphicsDevice graphicsDevice)
    {
        _spriteBatch = spriteBatch;
        _font = font;
        _map = map;
        _graphicsDevice = graphicsDevice;

        _pixelTexture = new Texture2D(graphicsDevice, 1, 1);
        _pixelTexture.SetData(new[] { Color.White });
    }

    public void SetManagers(CivilizationManager civilizationManager, WeatherSystem weatherSystem)
    {
        _civilizationManager = civilizationManager;
        _weatherSystem = weatherSystem;
    }

    public void SetAnimalEvolutionSimulator(AnimalEvolutionSimulator animalEvolutionSimulator)
    {
        _animalEvolutionSimulator = animalEvolutionSimulator;
    }

    public void SetPlanetStabilizer(PlanetStabilizer planetStabilizer)
    {
        _planetStabilizer = planetStabilizer;
    }

    public bool IsMouseOverPanel(MouseState mouseState, int toolbarHeight)
    {
        // Calculate panel dimensions (same as in Draw)
        int panelX = 0;
        int panelY = toolbarHeight;
        int panelWidth = 280;
        int panelHeight = _graphicsDevice.Viewport.Height - toolbarHeight;

        return mouseState.X >= panelX && mouseState.X <= panelX + panelWidth &&
               mouseState.Y >= panelY && mouseState.Y <= panelY + panelHeight;
    }

    public void Update(GameTime gameTime, MouseState mouseState, MouseState previousMouseState, int toolbarHeight)
    {
        // Calculate panel dimensions (same as in Draw)
        int panelX = 0;
        int panelY = toolbarHeight;
        int panelWidth = 280;
        int panelHeight = _graphicsDevice.Viewport.Height - toolbarHeight;

        // Check if mouse is over the panel
        IsMouseOver = IsMouseOverPanel(mouseState, toolbarHeight);

        // Handle Mouse Wheel Scrolling
        if (IsMouseOver)
        {
            int scrollDelta = mouseState.ScrollWheelValue - previousMouseState.ScrollWheelValue;
            if (scrollDelta != 0)
            {
                _scrollOffset -= scrollDelta / 2; // Adjust scroll speed
                ClampScroll(panelHeight);
            }
        }

        // Handle Scrollbar Dragging
        // Check if we clicked on the scrollbar
        if (_lastScrollbarRect.Contains(mouseState.Position) && mouseState.LeftButton == ButtonState.Pressed && previousMouseState.LeftButton == ButtonState.Released)
        {
            _isDraggingScrollbar = true;
            _dragStartY = mouseState.Y;
            _dragStartScrollOffset = _scrollOffset;
        }

        // Update dragging
        if (_isDraggingScrollbar && mouseState.LeftButton == ButtonState.Pressed)
        {
            int deltaY = mouseState.Y - _dragStartY;

            // Map scrollbar movement to content movement
            // Scrollbar travel range: visibleHeight - thumbHeight
            // Content travel range: contentHeight - visibleHeight
            // Ratio = ContentRange / ScrollbarRange

            int visibleHeight = panelHeight - 58; // Subtract header height
            if (_contentHeight > visibleHeight)
            {
                int scrollbarHeight = visibleHeight;
                int thumbHeight = Math.Max(20, (int)((float)visibleHeight / _contentHeight * scrollbarHeight));
                int trackHeight = scrollbarHeight - thumbHeight;

                if (trackHeight > 0)
                {
                    int maxScroll = _contentHeight - visibleHeight;
                    float ratio = (float)maxScroll / trackHeight;

                    _scrollOffset = _dragStartScrollOffset + (int)(deltaY * ratio);
                    ClampScroll(panelHeight);
                }
            }
        }
        else
        {
            _isDraggingScrollbar = false;
        }
    }

    private void ClampScroll(int panelHeight)
    {
        int visibleHeight = panelHeight - 58; // Visible area below header
        int maxScroll = Math.Max(0, _contentHeight - visibleHeight + 60); // Extra padding at bottom
        _scrollOffset = Math.Clamp(_scrollOffset, 0, maxScroll);
    }

    public void Draw(GameState state, RenderMode renderMode, float zoomLevel = 1.0f, bool showVolcanoes = false, bool showRivers = false, bool showPlates = false, bool showEarthquakes = false, bool showDisasterZones = false, int toolbarHeight = 0)
    {
        DrawInfoPanel(state, renderMode, zoomLevel, showVolcanoes, showRivers, showPlates, showEarthquakes, showDisasterZones, toolbarHeight);

        if (ShowHelp)
        {
            DrawHelpPanel(toolbarHeight);
        }

        if (IsFastForwarding)
        {
            DrawFastForwardProgressBar();
        }
    }

    private void DrawFastForwardProgressBar()
    {
        int barWidth = 600;
        int barHeight = 40;
        int barX = (_graphicsDevice.Viewport.Width - barWidth) / 2;
        int barY = _graphicsDevice.Viewport.Height - barHeight - 75; // Moved up to avoid bottom button bar

        // Shadow
        DrawRectangle(barX + 4, barY + 4, barWidth, barHeight, new Color(0, 0, 0, 150));

        // Main bar
        DrawRectangle(barX, barY, barWidth, barHeight, _panelBgColor);
        DrawBorder(barX, barY, barWidth, barHeight, _accentColor, 1);

        int progressWidth = (int)(barWidth * FastForwardProgress);
        DrawRectangle(barX + 2, barY + 2, Math.Max(0, progressWidth - 4), barHeight - 4, new Color(0, 100, 0, 200));

        string text = $"Fast Forwarding... {FastForwardProgress:P0} (Year: {FastForwardCurrentYear}) - Press ESC to cancel";
        var textSize = _font.MeasureString(text);
        _font.DrawString(_spriteBatch, text, new Vector2(barX + (barWidth - textSize.X) / 2, barY + (barHeight - textSize.Y) / 2), _textValueColor);
    }

    private void DrawInfoPanel(GameState state, RenderMode renderMode, float zoomLevel, bool showVolcanoes, bool showRivers, bool showPlates, bool showEarthquakes, bool showDisasterZones, int toolbarHeight)
    {
        // Update cached stats if needed (throttled to prevent lag)
        var timeSinceUpdate = (DateTime.Now - _lastStatsUpdate).TotalMilliseconds;
        if (timeSinceUpdate >= StatsUpdateIntervalMs)
        {
            _cachedLifeStats = CalculateLifeStats();
            _lastStatsUpdate = DateTime.Now;
        }

        // Use full left side of screen, below toolbar
        int panelX = 0;
        int panelY = toolbarHeight; // Start strictly below toolbar
        int panelWidth = 280;
        int panelHeight = _graphicsDevice.Viewport.Height - toolbarHeight;
        const int headerHeight = 58;

        // Background: vertical gradient and a crisp right edge
        DrawRectangle(panelX, panelY, panelWidth, panelHeight, _panelBgColor);
        UITheme.FillGradient(_spriteBatch, new Rectangle(panelX, panelY, panelWidth, panelHeight), Color.White * (8 / 255f));
        DrawRectangle(panelX + panelWidth - 1, panelY, 1, panelHeight, _panelBorderColor);

        // --- Static header: planet clock ---
        bool hasCivilization = _civilizationManager != null && _civilizationManager.Civilizations.Count > 0;
        DrawRectangle(panelX, panelY, panelWidth - 1, headerHeight, _headerBgColor);
        DrawRectangle(panelX, panelY + headerHeight - 1, panelWidth - 1, 1, _panelBorderColor);

        string clock;
        string clockLabel;
        if (!hasCivilization)
        {
            // Geological Time Scale (MYA - Million Years Ago)
            // Assuming 1 Game Year approx 5 Million Years in early simulation
            int startMya = 4600; // Earth formed 4.6 BYA
            int mya = Math.Max(0, startMya - (state.Year * 5));
            clock = mya > 0 ? $"{mya:N0} MYA" : "Present Day";
            clockLabel = "GEOLOGICAL TIME";
        }
        else
        {
            // Civilization Time Scale (Standard Years)
            float fractionalYear = state.Year + state.TimeAccumulator / GameState.SecondsPerGameYear;
            clock = $"Year {fractionalYear:N1}";
            clockLabel = "CALENDAR";
        }
        UITheme.DrawText(_spriteBatch, clockLabel, new Vector2(panelX + 14, panelY + 8), UITheme.TextMuted, 11f);
        UITheme.DrawTextShadowed(_spriteBatch, clock, new Vector2(panelX + 14, panelY + 22), _goldColor, UITheme.FontLarge);

        // Speed chip on the right of the header
        string speedText = state.IsPaused ? "PAUSED" : $"{state.TimeSpeed:0.##}x";
        var speedSize = UITheme.Measure(speedText, UITheme.FontSmall);
        var chip = new Rectangle(panelX + panelWidth - (int)speedSize.X - 30, panelY + 24, (int)speedSize.X + 16, 20);
        UITheme.FillRounded(_spriteBatch, chip, state.IsPaused ? new Color(90, 70, 20) : new Color(24, 64, 40));
        UITheme.DrawTextCentered(_spriteBatch, speedText, chip, state.IsPaused ? _goldColor : _goodColor, UITheme.FontSmall);

        // --- Content Rendering with Clipping ---

        // Define the content area (below header)
        int contentAreaY = panelY + headerHeight;
        int contentAreaHeight = panelHeight - headerHeight;

        // Save current scissor rectangle
        Rectangle currentScissor = _spriteBatch.GraphicsDevice.ScissorRectangle;

        // Enable scissor test for clipping
        _spriteBatch.End();
        _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, null, ScissorRasterizer);

        // Set scissor rectangle to content area
        _spriteBatch.GraphicsDevice.ScissorRectangle = new Rectangle(panelX, contentAreaY, panelWidth, contentAreaHeight);

        int textX = panelX + 14;
        int valueRight = panelX + panelWidth - 16;
        int textStartY = contentAreaY + 8;
        int textY = textStartY - _scrollOffset; // Apply scroll offset
        int lineHeight = 21;

        bool Visible(int h) => textY + h > contentAreaY && textY < contentAreaY + contentAreaHeight;

        void DrawText(string text, Color color, int fontSize = 14, int offsetX = 0)
        {
            // Culling optimization: don't draw if outside view
            if (Visible(lineHeight))
            {
                string fitted = UITheme.Ellipsize(text, valueRight - textX - offsetX, fontSize);
                UITheme.DrawText(_spriteBatch, fitted, new Vector2(textX + offsetX, textY), color, fontSize);
            }
            textY += fontSize <= 12 ? lineHeight - 3 : lineHeight;
        }

        void DrawLabelValue(string label, string value, Color valueColor, Color? dot = null)
        {
            if (Visible(lineHeight))
            {
                int lx = textX;
                if (dot.HasValue)
                {
                    UITheme.FillRounded(_spriteBatch, new Rectangle(lx, textY + 4, 9, 9), dot.Value);
                    lx += 15;
                }
                UITheme.DrawText(_spriteBatch, label, new Vector2(lx, textY), _textLabelColor, 14);
                var vs = UITheme.Measure(value, 14);
                UITheme.DrawText(_spriteBatch, value, new Vector2(valueRight - vs.X, textY), valueColor, 14);
            }
            textY += lineHeight;
        }

        void DrawMeterRow(string label, string value, float fill, Color color)
        {
            if (Visible(lineHeight + 10))
            {
                UITheme.DrawText(_spriteBatch, label, new Vector2(textX, textY), _textLabelColor, 14);
                var vs = UITheme.Measure(value, 14);
                UITheme.DrawText(_spriteBatch, value, new Vector2(valueRight - vs.X, textY), color, 14);
                var bar = new Rectangle(textX, textY + 19, valueRight - textX, 4);
                _spriteBatch.Draw(_pixelTexture, bar, new Color(0, 0, 0, 140));
                _spriteBatch.Draw(_pixelTexture, new Rectangle(bar.X, bar.Y, (int)(bar.Width * Math.Clamp(fill, 0f, 1f)), bar.Height), color * 0.9f);
            }
            textY += lineHeight + 8;
        }

        void DrawSectionHeader(string text)
        {
            textY += 10;
            if (Visible(24))
            {
                UITheme.DrawText(_spriteBatch, text, new Vector2(textX, textY), _accentColor, 12f);
                var ts = UITheme.Measure(text, 12f);
                DrawRectangle((int)(textX + ts.X + 8), textY + 8, (int)(valueRight - textX - ts.X - 8), 1, _panelBorderColor);
            }
            textY += 22;
        }

        if (_animalEvolutionSimulator != null)
        {
            string eraName = _animalEvolutionSimulator.GetCurrentEraName();
            Color eraColor = _animalEvolutionSimulator.DinosaursDominant ? new Color(255, 160, 60) :
                           _animalEvolutionSimulator.MammalsDominant ? new Color(160, 210, 255) :
                           new Color(200, 200, 200);
            DrawText(eraName, eraColor, 14);
        }

        // Atmosphere Section
        DrawSectionHeader("ATMOSPHERE");
        DrawMeterRow("Oxygen", $"{_map.GlobalOxygen:F1}%", _map.GlobalOxygen / 35f, GetOxygenColor(_map.GlobalOxygen));
        DrawMeterRow("CO2", $"{_map.GlobalCO2:F2}%", MathF.Log10(1 + _map.GlobalCO2 * 10f) / 2f, GetCO2Color(_map.GlobalCO2));
        DrawMeterRow("Temperature", $"{_map.GlobalTemperature:F1} C", (_map.GlobalTemperature + 30f) / 80f, GetTempColor(_map.GlobalTemperature));
        DrawMeterRow("Solar energy", $"{_map.SolarEnergy:F2}", _map.SolarEnergy / 2f, new Color(255, 220, 90));

        // Life Section
        DrawSectionHeader("BIOSPHERE");

        int lifeRows = 0;
        // Helper to draw life stats compactly
        void DrawLifeStat(string name, LifeForm type, Color color)
        {
            int count = _cachedLifeStats.GetValueOrDefault(type, 0);
            if (count > 0)
            {
                DrawLabelValue(name, count.ToString("N0"), _textValueColor, color);
                lifeRows++;
            }
        }

        DrawLifeStat("Bacteria", LifeForm.Bacteria, Color.Gray);
        DrawLifeStat("Algae", LifeForm.Algae, Color.LightGreen);
        DrawLifeStat("Plants", LifeForm.PlantLife, Color.Green);
        DrawLifeStat("Simple Animals", LifeForm.SimpleAnimals, Color.SandyBrown);
        DrawLifeStat("Fish", LifeForm.Fish, new Color(100, 120, 200));
        DrawLifeStat("Amphibians", LifeForm.Amphibians, new Color(120, 160, 80));
        DrawLifeStat("Reptiles", LifeForm.Reptiles, new Color(140, 140, 60));
        DrawLifeStat("Dinosaurs", LifeForm.Dinosaurs, Color.Orange);
        DrawLifeStat("Marine Dinos", LifeForm.MarineDinosaurs, new Color(100, 100, 180));
        DrawLifeStat("Pterosaurs", LifeForm.Pterosaurs, new Color(160, 140, 100));
        DrawLifeStat("Mammals", LifeForm.Mammals, new Color(160, 120, 90));
        DrawLifeStat("Birds", LifeForm.Birds, new Color(150, 180, 150));
        DrawLifeStat("Complex", LifeForm.ComplexAnimals, Color.Orange);
        DrawLifeStat("Intelligent", LifeForm.Intelligence, Color.Gold);
        DrawLifeStat("Civilization", LifeForm.Civilization, Color.Yellow);
        if (lifeRows == 0) DrawText("No life yet - press L to seed", UITheme.TextMuted, 12);

        // Civilization Section (from the thread-safe render snapshot)
        var civData = CivRenderData.Latest;
        if (civData.Civs.Count > 0)
        {
            DrawSectionHeader($"NATIONS ({civData.Civs.Count})");
            var civs = new List<CivRenderData.CivInfo>(civData.Civs);
            civs.Sort((a, b) => b.Population.CompareTo(a.Population));
            int civCount = Math.Min(5, civs.Count);
            for (int i = 0; i < civCount; i++)
            {
                var civ = civs[i];
                if (Visible(44))
                {
                    Color civColor = TerrainRenderer.GetCivPaletteColor(civ.Id);
                    UITheme.FillRounded(_spriteBatch, new Rectangle(textX, textY + 3, 4, 34), civColor);
                    string name = UITheme.Ellipsize(civ.Name, 150, 14);
                    UITheme.DrawText(_spriteBatch, name, new Vector2(textX + 10, textY), _textValueColor, 14);
                    if (civ.AtWar)
                    {
                        var war = new Rectangle(valueRight - 38, textY + 1, 38, 17);
                        UITheme.FillRounded(_spriteBatch, war, new Color(120, 30, 30));
                        UITheme.DrawTextCentered(_spriteBatch, "WAR", war, new Color(255, 200, 190), 11f);
                    }
                    string details = $"{civ.CivType} - {FormatPopulation(civ.Population)} - {civ.CityCount} cities";
                    if (civ.Gold > 0) details += $" - {civ.Gold:N0} gold";
                    UITheme.DrawText(_spriteBatch, UITheme.Ellipsize(details, valueRight - textX - 10, 12f),
                        new Vector2(textX + 10, textY + 20), _textLabelColor, 12f);
                }
                textY += 44;
            }

            if (civs.Count > civCount)
            {
                DrawText($"+ {civs.Count - civCount} more (G: civilization view)", UITheme.TextMuted, 12);
            }
        }

        // Weather Alerts
        if (_weatherSystem != null)
        {
            var activeStorms = _weatherSystem.GetActiveStorms();
            if (activeStorms.Count > 0)
            {
                DrawSectionHeader("ALERTS");
                DrawLabelValue("Active storms", activeStorms.Count.ToString(), _alertColor, _alertColor);
                int stormCount = Math.Min(2, activeStorms.Count);
                for (int i = 0; i < stormCount; i++)
                {
                    var storm = activeStorms[i];
                    DrawText($"{storm.Type} (intensity {storm.Intensity:F1})", Color.Orange, 12, 15);
                }
            }
        }

        // Stabilizer
        if (_planetStabilizer != null)
        {
            DrawSectionHeader("STABILIZER");
            if (_planetStabilizer.IsActive)
            {
                DrawLabelValue("Auto-stabilizer", "ON", _goodColor, _goodColor);
                DrawText($"{_planetStabilizer.AdjustmentsMade} adjustments", _textLabelColor, 12, 15);
                DrawText(_planetStabilizer.LastAction, UITheme.TextMuted, 12, 15);
            }
            else
            {
                DrawLabelValue("Auto-stabilizer", "OFF", Color.Gray, Color.Gray);
            }
        }

        // Footer View Info
        DrawSectionHeader("VIEW");
        DrawLabelValue("Mode", $"{renderMode}", _accentColor);
        DrawLabelValue("Zoom", $"{zoomLevel:F1}x", _textValueColor);

        // Mini icons for overlays
        var overlays = new List<string>();
        if (showVolcanoes) overlays.Add("Volcanoes");
        if (showRivers) overlays.Add("Rivers");
        if (showPlates) overlays.Add("Plates");
        if (showEarthquakes) overlays.Add("Quakes");
        if (showDisasterZones) overlays.Add("Disasters");
        if (overlays.Count > 0)
        {
            DrawText("Overlays: " + string.Join(", ", overlays), _textLabelColor, 12);
        }

        // Store total content height for scrolling
        _contentHeight = (textY - textStartY) + _scrollOffset;

        // End Clipping
        _spriteBatch.End();
        _spriteBatch.GraphicsDevice.ScissorRectangle = currentScissor;
        _spriteBatch.Begin(samplerState: SamplerState.PointClamp);

        // --- Draw Scrollbar ---
        if (_contentHeight > contentAreaHeight)
        {
            int scrollbarWidth = 5;
            int scrollbarX = panelX + panelWidth - scrollbarWidth - 3;
            int scrollbarY = contentAreaY + 4;
            int scrollbarHeight = contentAreaHeight - 8;

            // Draw Track
            DrawRectangle(scrollbarX, scrollbarY, scrollbarWidth, scrollbarHeight, new Color(0, 0, 0, 100));

            // Calculate Thumb
            float viewRatio = Math.Min(1.0f, (float)contentAreaHeight / _contentHeight);
            int thumbHeight = Math.Max(20, (int)(scrollbarHeight * viewRatio));

            float scrollRatio = (float)_scrollOffset / (_contentHeight - contentAreaHeight);
            if (float.IsNaN(scrollRatio)) scrollRatio = 0;
            scrollRatio = Math.Clamp(scrollRatio, 0f, 1f);

            int thumbY = scrollbarY + (int)((scrollbarHeight - thumbHeight) * scrollRatio);

            // Draw Thumb
            Color thumbColor = _isDraggingScrollbar ? _accentColor : _panelBorderColor;
            DrawRectangle(scrollbarX, thumbY, scrollbarWidth, thumbHeight, thumbColor);

            // Update scrollbar rect for hit testing (a bit wider than drawn for easier grabbing)
            _lastScrollbarRect = new Rectangle(scrollbarX - 4, scrollbarY, scrollbarWidth + 6, scrollbarHeight);
        }
        else
        {
            _lastScrollbarRect = Rectangle.Empty;
            _scrollOffset = 0; // Reset scroll if content fits
        }
    }

    private static readonly RasterizerState ScissorRasterizer = new RasterizerState { ScissorTestEnable = true };

    private static string FormatPopulation(int population)
    {
        if (population >= 1_000_000) return $"{population / 1_000_000f:0.#}M";
        if (population >= 1_000) return $"{population / 1_000f:0.#}K";
        return population.ToString();
    }

    private static readonly (string Title, (string Key, string Text)[] Items)[] HelpSections =
    {
        ("TIME", new[]
        {
            ("Space", "Pause / resume"),
            ("+  -", "Faster / slower"),
            ("F", "Fast forward 10,000 years"),
            ("Esc", "Pause menu / cancel fast forward"),
        }),
        ("VIEW MODES", new[]
        {
            ("1", "Terrain, elevation, biomes"),
            ("2", "Weather: temperature, rain, wind..."),
            ("3", "Atmosphere: O2, CO2, radiation..."),
            ("4", "Geology: plates, volcanoes, faults..."),
            ("5", "Life, nations, infrastructure"),
        }),
        ("MAP", new[]
        {
            ("Wheel", "Zoom (towards the cursor)"),
            ("Drag", "Pan the map (left or middle button)"),
            ("Click", "Tile information"),
            ("C", "Day / night cycle"),
            ("P", "3D globe"),
        }),
        ("OVERLAYS", new[]
        {
            ("V", "Volcanoes"),
            ("B", "Rivers"),
            ("N", "Plate boundaries"),
            (".", "Earthquake rings"),
            (",", "Disaster zones"),
            ("E", "Geological event log"),
        }),
        ("CIVILIZATION", new[]
        {
            ("O", "World chronicle (history)"),
            ("G", "Control a civilization"),
            ("I", "Divine powers"),
            ("K", "Pandemics"),
            ("Y", "Graphs"),
        }),
        ("TOOLS", new[]
        {
            ("L", "Life painter (right click: cycle life)"),
            ("T", "Terraforming (right click: raise/lower)"),
            ("U", "Draw faults (Tab: fault type)"),
            ("J", "Geological cross-section"),
            ("D", "Trigger disasters"),
            ("X", "Planet controls"),
            ("\\", "Auto-stabilizer"),
        }),
        ("WORLD", new[]
        {
            ("M", "World generator"),
            ("R", "Regenerate planet"),
            ("F5 / F9", "Quick save / quick load"),
            ("H", "This help"),
        }),
    };

    private void DrawHelpPanel(int toolbarHeight)
    {
        // Centered in the map area (right of the info panel), above the time bar
        int infoPanelWidth = 280;
        int areaX = infoPanelWidth + 20;
        int areaW = _graphicsDevice.Viewport.Width - areaX - 20;
        int panelWidth = Math.Min(980, areaW);
        int panelX = areaX + (areaW - panelWidth) / 2;
        int panelY = toolbarHeight + 16;
        int panelHeight = Math.Min(640, _graphicsDevice.Viewport.Height - toolbarHeight - 90);
        var panel = new Rectangle(panelX, panelY, panelWidth, panelHeight);

        _spriteBatch.End();
        _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp);

        int contentY = UITheme.DrawTitledPanel(_spriteBatch, panel, "COMMAND REFERENCE", _goldColor, 40);

        // Lay the sections out in columns, filling each column top to bottom
        int rowH = 22;
        int bottomLimit = panel.Bottom - 34;
        // Use the fewest columns (2..4) that fit everything vertically
        int columns = 2;
        for (; columns < 4; columns++)
        {
            int cx = 0, cy = contentY + 4;
            foreach (var (_, sectionItems) in HelpSections)
            {
                int h = 26 + sectionItems.Length * rowH + 10;
                if (cy + h > bottomLimit && cx < columns - 1) { cx++; cy = contentY + 4; }
                cy += h;
            }
            if (cy <= bottomLimit) break;
        }
        if (columns < 3 && panelWidth >= 900) columns = 3;
        int colWidth = (panelWidth - 30) / columns;
        int keyW = 58;
        int x = panelX + 16, y = contentY + 4;
        int col = 0;
        int bottom = bottomLimit;

        foreach (var (title, items) in HelpSections)
        {
            int sectionH = 26 + items.Length * rowH + 10;
            if (y + sectionH > bottom && col < columns - 1)
            {
                col++;
                x = panelX + 16 + col * colWidth;
                y = contentY + 4;
            }

            UITheme.DrawText(_spriteBatch, title, new Vector2(x, y), _accentColor, 12f);
            var ts = UITheme.Measure(title, 12f);
            DrawRectangle((int)(x + ts.X + 8), y + 8, (int)(colWidth - ts.X - 30), 1, _panelBorderColor);
            y += 24;

            foreach (var (key, text) in items)
            {
                var ks = UITheme.Measure(key, UITheme.FontSmall);
                var chip = new Rectangle(x, y + 1, Math.Max(26, (int)ks.X + 12), rowH - 4);
                UITheme.FillRounded(_spriteBatch, chip, new Color(40, 54, 80));
                UITheme.OutlineRounded(_spriteBatch, chip, UITheme.Border);
                UITheme.DrawTextCentered(_spriteBatch, key, chip, _textValueColor, UITheme.FontSmall);
                string fitted = UITheme.Ellipsize(text, colWidth - keyW - 20, 13f);
                UITheme.DrawText(_spriteBatch, fitted, new Vector2(x + keyW + 6, y + 1), _textLabelColor, 13f);
                y += rowH;
            }
            y += 10;
        }

        UITheme.DrawText(_spriteBatch, "Press H to close", new Vector2(panelX + 16, panel.Bottom - 26), UITheme.TextMuted, UITheme.FontSmall);

        _spriteBatch.End();
        _spriteBatch.Begin(samplerState: SamplerState.PointClamp);
    }

    private void DrawRectangle(int x, int y, int width, int height, Color color)
    {
        _spriteBatch.Draw(_pixelTexture, new Rectangle(x, y, width, height), color);
    }

    private void DrawBorder(int x, int y, int width, int height, Color color, int thickness)
    {
        _spriteBatch.Draw(_pixelTexture, new Rectangle(x, y, width, thickness), color); // Top
        _spriteBatch.Draw(_pixelTexture, new Rectangle(x, y + height - thickness, width, thickness), color); // Bottom
        _spriteBatch.Draw(_pixelTexture, new Rectangle(x, y, thickness, height), color); // Left
        _spriteBatch.Draw(_pixelTexture, new Rectangle(x + width - thickness, y, thickness, height), color); // Right
    }

    private Dictionary<LifeForm, int> CalculateLifeStats()
    {
        var stats = new Dictionary<LifeForm, int>();
        foreach (LifeForm lifeForm in Enum.GetValues<LifeForm>())
        {
            stats[lifeForm] = 0;
        }

        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                var cell = _map.Cells[x, y];
                if (cell.Biomass > 0.1f)
                {
                    stats[cell.LifeType]++;
                }
            }
        }

        return stats;
    }

    private Color GetOxygenColor(float oxygen)
    {
        if (oxygen < 10) return _alertColor;
        if (oxygen < 15) return Color.Orange;
        if (oxygen < 25) return _goodColor;
        return _alertColor; // Too much oxygen
    }

    private Color GetCO2Color(float co2)
    {
        if (co2 < 0.1f) return _goodColor;
        if (co2 < 1.0f) return Color.Yellow;
        return _alertColor;
    }

    private Color GetTempColor(float temp)
    {
        if (temp < 0) return Color.LightBlue;
        if (temp < 10) return Color.Cyan;
        if (temp < 25) return _goodColor;
        if (temp < 35) return Color.Yellow;
        return _alertColor;
    }
}

public class GameState
{
    public const float SecondsPerGameYear = 10.0f;
    public int Year { get; set; }
    public float TimeSpeed { get; set; } = 1.0f;
    public bool IsPaused { get; set; }
    public float TimeAccumulator { get; set; }
}
