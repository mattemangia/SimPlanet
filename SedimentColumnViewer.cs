using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace SimPlanet;

/// <summary>
/// Tile inspector: everything known about a clicked cell, with its stratigraphic log
/// </summary>
public class SedimentColumnViewer
{
    private readonly GraphicsDevice _graphicsDevice;
    private readonly FontRenderer _font;
    private readonly PlanetMap _map;
    private CivilizationManager? _civManager;
    private Texture2D _pixelTexture;
    private (int x, int y)? _selectedTile = null;
    private MouseState _previousMouseState;
    private Point? _mouseDownPosition = null;
    private const int DragThreshold = 25; // pixels (increased for trackpad users - more forgiving)
    private const int InfoPanelWidth = 280; // Don't open viewer when clicking in info panel
    private int _scrollOffset = 0; // For scrolling through sediment layers
    private const int ScrollSpeed = 20;

    public bool IsVisible { get; private set; } = false;

    public SedimentColumnViewer(GraphicsDevice graphicsDevice, FontRenderer font, PlanetMap map)
    {
        _graphicsDevice = graphicsDevice;
        _font = font;
        _map = map;

        _pixelTexture = new Texture2D(_graphicsDevice, 1, 1);
        _pixelTexture.SetData(new[] { Color.White });
    }

    public void SetCivilizationManager(CivilizationManager civManager)
    {
        _civManager = civManager;
    }

    private PetroleumSystem? _oil;
    private GeothermalSystem? _geothermal;

    public void SetGeoscience(PetroleumSystem oil, GeothermalSystem geothermal)
    {
        _oil = oil;
        _geothermal = geothermal;
    }

    public void Update(MouseState mouseState, int cellSize, float cameraX, float cameraY, float zoomLevel, int mapRenderOffsetX, int mapRenderOffsetY, bool toolsActive = false)
    {
        int screenWidth = _graphicsDevice.Viewport.Width;
        int panelWidth = PanelWidth;
        int panelX = screenWidth - panelWidth - 10;
        int panelY = 40; // Below toolbar (36px high)

        // Check for close button click (X in top right)
        if (IsVisible && mouseState.LeftButton == ButtonState.Released &&
            _previousMouseState.LeftButton == ButtonState.Pressed)
        {
            Rectangle closeButtonBounds = new Rectangle(panelX + panelWidth - 30, panelY + 5, 25, 25);
            if (closeButtonBounds.Contains(mouseState.Position))
            {
                IsVisible = false;
                _selectedTile = null;
                _previousMouseState = mouseState;
                return;
            }
        }

        // Track mouse down position to detect drags
        if (mouseState.LeftButton == ButtonState.Pressed &&
            _previousMouseState.LeftButton == ButtonState.Released)
        {
            // A press that lands on another tool/panel must not become a tile click on release
            _mouseDownPosition = toolsActive ? null : mouseState.Position;
        }

        // Check for left mouse click on map (trigger on release, and only if not dragging)
        // Allow clicking to update tile even when panel is visible
        if (mouseState.LeftButton == ButtonState.Released &&
            _previousMouseState.LeftButton == ButtonState.Pressed &&
            _mouseDownPosition.HasValue)
        {
            // Calculate distance moved since mouse down
            int dragDistance = (int)Vector2.Distance(
                new Vector2(_mouseDownPosition.Value.X, _mouseDownPosition.Value.Y),
                new Vector2(mouseState.Position.X, mouseState.Position.Y)
            );

            // Only open/update viewer if this was a click, not a drag
            if (dragDistance <= DragThreshold)
            {
                // Don't open if clicking in the info panel area or in the viewer panel
                if (mouseState.X < InfoPanelWidth)
                {
                    _mouseDownPosition = null;
                }
                else if (IsVisible)
                {
                    // Check if clicking inside the viewer panel
                    // Use existing panelWidth, panelX, panelY variables from outer scope
                    int panelHeight = _graphicsDevice.Viewport.Height - 20; // Use almost full height
                    Rectangle panelBounds = new Rectangle(panelX, panelY, panelWidth, panelHeight);

                    if (!panelBounds.Contains(mouseState.Position))
                    {
                        // Clicking outside panel - update to new tile
                        float mapRelativeX = (mouseState.X - mapRenderOffsetX) + cameraX;
                        float mapRelativeY = (mouseState.Y - mapRenderOffsetY) + cameraY;
                        int tileX = (int)(mapRelativeX / (cellSize * zoomLevel));
                        int tileY = (int)(mapRelativeY / (cellSize * zoomLevel));

                        if (tileX >= 0 && tileX < _map.Width && tileY >= 0 && tileY < _map.Height)
                        {
                            _selectedTile = (tileX, tileY);
                            _scrollOffset = 0; // Reset scroll when changing tile
                        }
                    }
                    _mouseDownPosition = null;
                }
                else
                {
                    // Convert mouse position to tile coordinates
                    // 1. Subtract map render offset to get map-relative coordinates
                    // 2. Add camera offset to account for panning
                    // 3. Divide by (cellSize * zoomLevel) to get tile index
                    float mapRelativeX = (mouseState.X - mapRenderOffsetX) + cameraX;
                    float mapRelativeY = (mouseState.Y - mapRenderOffsetY) + cameraY;
                    int tileX = (int)(mapRelativeX / (cellSize * zoomLevel));
                    int tileY = (int)(mapRelativeY / (cellSize * zoomLevel));

                    // Check if tile is within bounds
                    // Don't open tile info panel when tools are active (they need map clicks)
                    if (tileX >= 0 && tileX < _map.Width && tileY >= 0 && tileY < _map.Height && !toolsActive)
                    {
                        _selectedTile = (tileX, tileY);
                        IsVisible = true;
                        _scrollOffset = 0;
                    }
                }
            }
            _mouseDownPosition = null;
        }

        // Reset mouse down position if button is released
        if (mouseState.LeftButton == ButtonState.Released)
        {
            _mouseDownPosition = null;
        }

        // Handle scroll wheel for sediment column scrolling
        if (IsVisible)
        {
            int scrollDelta = mouseState.ScrollWheelValue - _previousMouseState.ScrollWheelValue;
            if (scrollDelta != 0)
            {
                _scrollOffset -= (scrollDelta / 120) * ScrollSpeed; // Each wheel "click" is 120 units
                _scrollOffset = Math.Max(0, _scrollOffset); // Can't scroll above top
            }
        }

        // Close with right click or ESC
        if (mouseState.RightButton == ButtonState.Pressed || Keyboard.GetState().IsKeyDown(Keys.Escape))
        {
            IsVisible = false;
            _selectedTile = null;
            _scrollOffset = 0;
        }

        _previousMouseState = mouseState;
    }

    private static readonly RasterizerState ScissorRasterizer = new RasterizerState { ScissorTestEnable = true };
    private const int PanelWidth = 430;
    private readonly StrataColumn _column = new();
    private int _contentHeight;

    // Layout cursor
    private SpriteBatch _sb = null!;
    private int _x, _w, _cy;

    public void Draw(SpriteBatch spriteBatch, int screenWidth, int screenHeight)
    {
        if (!IsVisible || !_selectedTile.HasValue) return;

        var (x, y) = _selectedTile.Value;
        var cell = _map.Cells[x, y];
        var geo = cell.GetGeology();

        int panelHeight = screenHeight - 50;
        int panelX = screenWidth - PanelWidth - 10;
        int panelY = 40;
        var panel = new Rectangle(panelX, panelY, PanelWidth, panelHeight);
        const int headerHeight = 58;

        UITheme.DrawPanel(spriteBatch, panel, new Color(12, 17, 28, 250), UITheme.BorderBright);

        // Header: biome swatch, name, position
        var header = new Rectangle(panelX + 1, panelY + 1, PanelWidth - 2, headerHeight);
        UITheme.FillRect(spriteBatch, header, UITheme.HeaderBg);
        UITheme.FillRect(spriteBatch, new Rectangle(header.X, header.Bottom - 1, header.Width, 1), UITheme.Gold * 0.7f);
        var swatch = new Rectangle(panelX + 14, panelY + 13, 32, 32);
        UITheme.FillRounded(spriteBatch, swatch, GetBiomeColor(cell));
        UITheme.OutlineRounded(spriteBatch, swatch, Color.Black * 0.5f);
        UITheme.DrawText(spriteBatch, GetBiomeType(cell), new Vector2(panelX + 58, panelY + 9), UITheme.Gold, UITheme.FontLarge);
        float lon = x / (float)_map.Width * 360f - 180f, lat = 90f - y / (float)_map.Height * 180f;
        UITheme.DrawText(spriteBatch, $"cell {x}, {y}    {MathF.Abs(lat):F0}{(lat >= 0 ? "N" : "S")} {MathF.Abs(lon):F0}{(lon >= 0 ? "E" : "W")}",
            new Vector2(panelX + 58, panelY + 35), UITheme.TextDim, UITheme.FontSmall);

        Rectangle closeButtonBounds = new Rectangle(panelX + PanelWidth - 30, panelY + 5, 25, 25);
        bool hoverClose = closeButtonBounds.Contains(Mouse.GetState().Position);
        UITheme.FillRounded(spriteBatch, closeButtonBounds, hoverClose ? new Color(200, 60, 60) : new Color(60, 70, 92));
        UITheme.DrawTextCentered(spriteBatch, "X", closeButtonBounds, Color.White, UITheme.FontNormal);

        // Scrollable content
        var view = new Rectangle(panelX + 1, panelY + headerHeight + 2, PanelWidth - 2, panelHeight - headerHeight - 6);
        int maxScroll = Math.Max(0, _contentHeight - view.Height);
        _scrollOffset = Math.Clamp(_scrollOffset, 0, maxScroll);

        var oldScissor = spriteBatch.GraphicsDevice.ScissorRectangle;
        spriteBatch.End();
        spriteBatch.GraphicsDevice.ScissorRectangle = view;
        spriteBatch.Begin(SpriteSortMode.Deferred, null, null, null, ScissorRasterizer);

        _sb = spriteBatch;
        _x = panelX + 16;
        _w = PanelWidth - 32 - (maxScroll > 0 ? 8 : 0);
        _cy = view.Y + 10 - _scrollOffset;
        int start = _cy;

        // ---- Surface ----
        Section("SURFACE");
        float altitude = cell.Elevation * 4400f;
        Row("Altitude", altitude >= 0 ? $"{altitude:N0} m" : $"{-altitude:N0} m below sea level", altitude >= 0 ? UITheme.Text : new Color(120, 190, 255));
        Row("Temperature", $"{cell.Temperature:F1} C", GetTempColor(cell.Temperature));
        Meter("Rainfall", Math.Clamp(cell.Rainfall, 0f, 1f), new Color(90, 160, 255), $"{cell.Rainfall:P0}");
        Meter("Humidity", Math.Clamp(cell.Humidity, 0f, 1f), new Color(120, 200, 230), $"{cell.Humidity:P0}");
        Row("Air", $"O2 {cell.Oxygen:F1}%    CO2 {cell.CO2:F2}%", UITheme.TextDim);

        if (cell.Biomass > 0.01f && cell.LifeType != LifeForm.None)
        {
            Section("LIFE");
            Row("Dominant", cell.LifeType.ToString(), UITheme.Good);
            Meter("Biomass", Math.Clamp(cell.Biomass, 0f, 1f), UITheme.Good, $"{cell.Biomass:P0}");
        }

        DrawNation(x, y);

        // ---- Tectonics ----
        Section("TECTONICS");
        string setting = geo.BoundaryType switch
        {
            PlateBoundaryType.Convergent => geo.IsSubducting ? "Convergent: this plate subducts" : "Convergent: overriding plate",
            PlateBoundaryType.Divergent => cell.IsWater ? "Divergent: spreading ridge" : "Divergent: continental rift",
            PlateBoundaryType.Transform => "Transform: plates slide past",
            _ => "Plate interior"
        };
        Row("Setting", setting, GetBoundaryColor(geo.BoundaryType));
        if (geo.IsForedeep) Row("Basin", "Foredeep: sinking, filling with sediment", new Color(226, 200, 130));
        Row("Crust", $"{geo.CrustType}, {geo.CrustThickness:F0} km thick", UITheme.Text);
        string motion = geo.UpliftRate > 0.0002f ? "rising" : geo.UpliftRate < -0.0002f ? "sinking" : "stable";
        Row("Vertical motion", motion, geo.UpliftRate > 0.0002f ? UITheme.Good : geo.UpliftRate < -0.0002f ? new Color(120, 190, 255) : UITheme.TextDim);
        if (geo.IsFault) Row("Fault", geo.FaultType.ToString().Replace('_', '-'), new Color(255, 120, 100));
        Meter("Elastic strain", Math.Clamp(geo.SeismicStress / 1.5f, 0f, 1f), geo.SeismicStress > 0.6f ? UITheme.Bad : UITheme.Warn,
            geo.SeismicStress > 0.6f ? "near failure" : "loading");
        if (geo.IsVolcano)
        {
            string state = geo.MagmaPressure > 0.8f ? "about to erupt" : geo.VolcanicActivity > 0.3f ? "active" : "dormant";
            Row(geo.IsHotSpot ? "Hot spot volcano" : "Volcano", state, new Color(255, 150, 60));
            Meter("Magma pressure", Math.Clamp(geo.MagmaPressure, 0f, 1f), new Color(255, 120, 40), $"{Math.Clamp(geo.MagmaPressure, 0f, 1f):P0}");
        }
        if (_geothermal != null)
        {
            float heat = _geothermal.HeatFlow(x, y);
            Row("Heat flow", $"{heat:F0} mW/m2    {heat / 2.5f:F0} C per km", heat >= GeothermalSystem.ViableHeatFlow ? new Color(255, 150, 60) : UITheme.TextDim);
        }

        // ---- Surface processes ----
        Section("EROSION AND DEPOSITION");
        float erosion = Math.Clamp(geo.ErosionRate / 0.6f, 0f, 1f);
        float deposition = Math.Clamp(geo.DepositionRate, 0f, 1f);
        Meter("Erosion", erosion, new Color(230, 120, 80), erosion > 0.01f ? "active" : cell.IsWater ? "none under water" : "slight");
        Meter("Deposition", deposition, new Color(214, 188, 130), deposition > 0.02f ? "active" : "none");
        string balance = erosion > deposition + 0.03f ? "wearing down" : deposition > erosion + 0.03f ? "building up" : "in balance";
        Row("Balance", balance, erosion > deposition + 0.03f ? new Color(230, 120, 80) : deposition > erosion + 0.03f ? new Color(214, 188, 130) : UITheme.TextDim);
        Row("Removed so far", $"{geo.CumulativeErosion * 4400f:N0} m", UITheme.TextDim);
        Row("Laid down so far", $"{geo.CumulativeDeposition * 4400f:N0} m", UITheme.TextDim);
        Row("Loose sediment in transit", $"{geo.SedimentLayer * 100f:F0}", UITheme.TextDim);

        // ---- Stratigraphic log ----
        Section("STRATIGRAPHIC LOG");
        DrawLog(cell, x, y);

        // ---- Resources ----
        var resources = cell.GetResources();
        if (resources.Count > 0)
        {
            Section("RESOURCES");
            foreach (var deposit in resources)
            {
                if (deposit.Amount <= 0.0005f) continue;
                UITheme.FillRounded(_sb, new Rectangle(_x, _cy + 3, 12, 12), ResourceExtensions.GetResourceColor(deposit.Type));
                UITheme.OutlineRounded(_sb, new Rectangle(_x, _cy + 3, 12, 12), Color.White * 0.35f);
                UITheme.DrawText(_sb, deposit.Type.ToString(), new Vector2(_x + 20, _cy), UITheme.Text, UITheme.FontNormal);
                string note = (deposit.Discovered ? "known" : "undiscovered") + $", depth {deposit.Depth:P0}";
                var size = UITheme.Measure(note, UITheme.FontSmall);
                UITheme.DrawText(_sb, note, new Vector2(_x + _w - size.X, _cy + 2), UITheme.TextMuted, UITheme.FontSmall);
                _cy += 20;
            }
        }

        _cy += 14;
        _contentHeight = _cy - start + 10;

        spriteBatch.End();
        spriteBatch.GraphicsDevice.ScissorRectangle = oldScissor;
        spriteBatch.Begin(samplerState: SamplerState.PointClamp);

        // Scroll bar
        if (maxScroll > 0)
        {
            var track = new Rectangle(panel.Right - 8, view.Y + 4, 4, view.Height - 8);
            UITheme.FillRect(spriteBatch, track, Color.White * 0.06f);
            int thumb = Math.Max(24, track.Height * view.Height / _contentHeight);
            int thumbY = track.Y + (track.Height - thumb) * _scrollOffset / maxScroll;
            UITheme.FillRounded(spriteBatch, new Rectangle(track.X, thumbY, 4, thumb), UITheme.BorderBright);
        }
    }

    // ---- Layout helpers ------------------------------------------------------------------

    private void Section(string title)
    {
        _cy += 12;
        UITheme.DrawText(_sb, title, new Vector2(_x, _cy), UITheme.Accent, UITheme.FontSmall);
        var size = UITheme.Measure(title, UITheme.FontSmall);
        UITheme.FillRect(_sb, new Rectangle(_x + (int)size.X + 10, _cy + 8, _w - (int)size.X - 10, 1), UITheme.Border);
        _cy += 24;
    }

    private void Row(string label, string value, Color valueColor)
    {
        UITheme.DrawText(_sb, label, new Vector2(_x, _cy), UITheme.TextDim, UITheme.FontNormal);
        value = UITheme.Ellipsize(value, _w - 150, UITheme.FontNormal);
        var size = UITheme.Measure(value, UITheme.FontNormal);
        UITheme.DrawText(_sb, value, new Vector2(_x + _w - size.X, _cy), valueColor, UITheme.FontNormal);
        _cy += 21;
    }

    private void Meter(string label, float value, Color color, string text)
    {
        UITheme.DrawText(_sb, label, new Vector2(_x, _cy), UITheme.TextDim, UITheme.FontNormal);
        var size = UITheme.Measure(text, UITheme.FontSmall);
        UITheme.DrawText(_sb, text, new Vector2(_x + _w - size.X, _cy + 1), color, UITheme.FontSmall);
        var bar = new Rectangle(_x + 130, _cy + 6, _w - 130 - 96, 7);
        UITheme.FillRounded(_sb, bar, new Color(4, 7, 12));
        int fill = (int)(bar.Width * Math.Clamp(value, 0f, 1f));
        if (fill > 1) UITheme.FillRounded(_sb, new Rectangle(bar.X, bar.Y, Math.Max(3, fill), bar.Height), color);
        _cy += 21;
    }

    private void DrawNation(int x, int y)
    {
        if (_civManager == null) return;
        Civilization? owner;
        City? city;
        try
        {
            owner = _civManager.Civilizations.FirstOrDefault(c => c.Territory.Contains((x, y)));
            city = _civManager.Civilizations.SelectMany(c => c.Cities).FirstOrDefault(c => c.X == x && c.Y == y);
            if (owner == null && city != null) owner = _civManager.Civilizations.FirstOrDefault(c => c.Id == city.CivilizationId);
        }
        catch (InvalidOperationException)
        {
            return; // Lists changed while reading; next frame
        }
        if (owner == null) return;

        Section("NATION");
        Row("Name", owner.Name, UITheme.Gold);
        if (city != null) Row(city.Type.ToString(), $"{city.Name}, {city.Population:N0} people", UITheme.Text);
        Row("People", $"{owner.Population:N0}", UITheme.Text);
        Row("Technology", $"{owner.TechLevel} ({owner.CivType})", UITheme.Accent);
        if (owner.Government != null)
        {
            Row("Government", owner.Government.Type.ToString(), UITheme.Text);
            if (owner.Government.CurrentRuler != null) Row("Ruler", $"{owner.Government.CurrentRuler.Title} {owner.Government.CurrentRuler.Name}", UITheme.TextDim);
            Meter("Stability", Math.Clamp(owner.Government.Stability, 0f, 1f), owner.Government.Stability > 0.5f ? UITheme.Good : UITheme.Warn, $"{owner.Government.Stability:P0}");
        }
    }

    // ---- Stratigraphic log ---------------------------------------------------------------

    /// <summary>Grain size as the width of the bed in the log, the way sedimentologists draw it.</summary>
    private static float GrainWidth(RockKind kind) => kind switch
    {
        RockKind.Organic => 0.34f,
        RockKind.Clay => 0.40f,
        RockKind.Silt => 0.52f,
        RockKind.Limestone => 0.66f,
        RockKind.Sand => 0.72f,
        RockKind.Volcanic => 0.80f,
        RockKind.Gravel => 0.94f,
        RockKind.Salt => 0.60f,
        _ => 1f
    };

    private static string RockName(RockKind kind) => kind switch
    {
        RockKind.Clay => "Claystone",
        RockKind.Silt => "Siltstone",
        RockKind.Sand => "Sandstone",
        RockKind.Gravel => "Conglomerate",
        RockKind.Organic => "Coal / black shale",
        RockKind.Volcanic => "Tuff / lava",
        RockKind.Limestone => "Limestone",
        RockKind.Salt => "Salt",
        RockKind.Basalt => "Basalt",
        _ => "Granite"
    };

    private static Color RockColor(RockKind kind) => kind switch
    {
        RockKind.Clay => new Color(150, 118, 96),
        RockKind.Silt => new Color(178, 150, 112),
        RockKind.Sand => new Color(226, 200, 130),
        RockKind.Gravel => new Color(160, 150, 138),
        RockKind.Organic => new Color(58, 50, 44),
        RockKind.Volcanic => new Color(112, 70, 78),
        RockKind.Limestone => new Color(206, 214, 196),
        RockKind.Salt => new Color(240, 226, 232),
        RockKind.Basalt => new Color(74, 78, 92),
        _ => new Color(196, 150, 150)
    };

    /// <summary>
    /// The column under the cell, drawn to scale from the same model the 3D sections, the
    /// seismic lines and the well logs use: bed thickness to scale, bed width by grain size,
    /// a pattern per rock, the sea on top, erosion surfaces, pay zones and the basement.
    /// </summary>
    private void DrawLog(TerrainCell cell, int x, int y)
    {
        Stratigraphy.Build(_map, _oil, x, y, _column);
        var column = _column;

        const int logHeight = 330, scaleWidth = 46, logWidth = 150;
        int waterPixels = column.WaterKm > 0 ? 26 : 0;
        float basementKm = MathF.Max(0.25f, column.SedimentKm * 0.18f);
        float totalKm = column.SedimentKm + basementKm;
        int top = _cy + waterPixels;
        int rockHeight = logHeight - waterPixels;
        int logX = _x + scaleWidth;
        int Y(float km) => top + (int)(km / totalKm * rockHeight);

        // Sea
        if (waterPixels > 0)
        {
            var sea = new Rectangle(logX, _cy, logWidth, waterPixels);
            UITheme.FillRect(_sb, sea, new Color(30, 90, 160));
            for (int wx = 4; wx < logWidth - 8; wx += 16) UITheme.DrawLine(_sb, new Vector2(logX + wx, _cy + 9), new Vector2(logX + wx + 8, _cy + 6), new Color(150, 200, 240), 1f);
            UITheme.DrawText(_sb, $"sea, {column.WaterKm * 1000f:N0} m deep", new Vector2(logX + logWidth + 12, _cy + 5), new Color(120, 190, 255), UITheme.FontSmall);
        }

        // Track background and depth scale
        UITheme.FillRect(_sb, new Rectangle(logX, top, logWidth, rockHeight), new Color(4, 7, 12));
        float tick = totalKm > 4f ? 1f : totalKm > 1.6f ? 0.5f : totalKm > 0.6f ? 0.2f : 0.1f;
        for (float d = 0f; d <= totalKm + 0.001f; d += tick)
        {
            int ty = Y(d);
            UITheme.FillRect(_sb, new Rectangle(logX - 5, ty, 5, 1), UITheme.TextMuted);
            string label = tick >= 0.5f ? $"{d:F1} km" : $"{d * 1000f:F0} m";
            var size = UITheme.Measure(label, UITheme.FontSmall);
            UITheme.DrawText(_sb, label, new Vector2(logX - 8 - size.X, ty - 7), UITheme.TextMuted, UITheme.FontSmall);
        }

        // Beds
        int labelY = top;
        for (int b = 0; b < StrataColumn.Beds; b++)
        {
            float from = column.BedTop[b], to = column.BedTop[b + 1];
            if (to - from < 0.0005f) continue;
            int y0 = Y(from), y1 = Y(to);
            if (y1 <= y0) y1 = y0 + 1;
            var kind = column.BedKind[b];
            var bed = new Rectangle(logX, y0, (int)(logWidth * GrainWidth(kind)), y1 - y0);
            UITheme.FillRect(_sb, bed, RockColor(kind));
            DrawPattern(bed, kind);
            UITheme.FillRect(_sb, new Rectangle(bed.X, bed.Bottom - 1, bed.Width, 1), Color.Black * 0.55f);

            // Name and thickness, when there is room
            if (y1 - y0 >= 13 && y0 >= labelY)
            {
                float thickness = to - from;
                string name = $"{RockName(kind)}  {(thickness >= 1f ? $"{thickness:F1} km" : $"{thickness * 1000f:F0} m")}";
                UITheme.DrawText(_sb, name, new Vector2(logX + logWidth + 12, (y0 + y1) / 2 - 7), UITheme.TextDim, UITheme.FontSmall);
                UITheme.FillRect(_sb, new Rectangle(bed.Right, (y0 + y1) / 2, logX + logWidth + 8 - bed.Right, 1), Color.White * 0.12f);
                labelY = (y0 + y1) / 2 + 9;
            }
        }

        // Salt body
        if (column.HasSalt)
        {
            var salt = new Rectangle(logX, Y(column.SaltTop), (int)(logWidth * GrainWidth(RockKind.Salt)), Y(column.SedimentKm) - Y(column.SaltTop));
            UITheme.FillRect(_sb, salt, RockColor(RockKind.Salt));
            DrawPattern(salt, RockKind.Salt);
        }

        // Pay zone: gas or oil above the contact, salt water below
        if (column.HasReservoir)
        {
            int r0 = Y(column.ReservoirTop), rc = Y(MathF.Min(column.FluidContact, column.ReservoirBottom)), r1 = Y(column.ReservoirBottom);
            int width = (int)(logWidth * GrainWidth(RockKind.Sand));
            bool gas = column.ReservoirFluid == PoreFluid.Gas;
            if (rc > r0) UITheme.FillRect(_sb, new Rectangle(logX, r0, width, rc - r0), gas ? new Color(250, 226, 120) : new Color(34, 28, 24));
            if (r1 > rc) UITheme.FillRect(_sb, new Rectangle(logX, rc, width, r1 - rc), new Color(150, 190, 214));
            UITheme.DrawRectOutline(_sb, new Rectangle(logX, r0, width, Math.Max(2, r1 - r0)), UITheme.Good, 1);
            UITheme.FillRect(_sb, new Rectangle(logX - 4, r0, 3, Math.Max(3, rc - r0)), UITheme.Good);
            UITheme.DrawTextOutlined(_sb, gas ? "GAS" : "OIL", new Vector2(logX + width + 6, r0 - 2), UITheme.Good, UITheme.FontSmall);
        }

        // Basement
        var basement = new Rectangle(logX, Y(column.SedimentKm), logWidth, top + rockHeight - Y(column.SedimentKm));
        UITheme.FillRect(_sb, basement, RockColor(column.Basement));
        DrawPattern(basement, column.Basement);
        UITheme.FillRect(_sb, new Rectangle(basement.X, basement.Y, basement.Width, 2), Color.Black * 0.7f);
        UITheme.DrawText(_sb, $"{RockName(column.Basement)} basement", new Vector2(logX + logWidth + 12, basement.Y + 4), RockColor(column.Basement), UITheme.FontSmall);
        UITheme.DrawText(_sb, $"Moho at {column.CrustKm:F0} km", new Vector2(logX + logWidth + 12, basement.Y + 19), UITheme.TextMuted, UITheme.FontSmall);

        // Erosion surface: beds below it are cut off; time is missing from the record here
        if (column.UnconformityKm >= 0f && column.UnconformityKm < column.SedimentKm)
        {
            int uy = Y(column.UnconformityKm);
            var previous = new Vector2(logX - 2, uy);
            for (int wx = 4; wx <= logWidth + 2; wx += 4)
            {
                var next = new Vector2(logX - 2 + wx, uy + ((wx / 4) % 2 == 0 ? -2 : 2));
                UITheme.DrawLine(_sb, previous, next, new Color(240, 70, 60), 2f);
                previous = next;
            }
            string label = column.UnconformityKm <= 0.0001f ? "eroding surface" : "unconformity (old erosion surface)";
            UITheme.DrawTextOutlined(_sb, label, new Vector2(logX + 6, uy - (column.UnconformityKm <= 0.0001f ? -4 : 17)), new Color(255, 120, 110), UITheme.FontSmall);
        }

        UITheme.DrawRectOutline(_sb, new Rectangle(logX, _cy, logWidth, logHeight), UITheme.Border, 1);

        // Grain size axis
        _cy += logHeight + 4;
        UITheme.DrawText(_sb, "mud", new Vector2(logX + logWidth * 0.34f, _cy), UITheme.TextMuted, UITheme.FontSmall);
        UITheme.DrawText(_sb, "sand", new Vector2(logX + logWidth * 0.62f, _cy), UITheme.TextMuted, UITheme.FontSmall);
        UITheme.DrawText(_sb, "gravel", new Vector2(logX + logWidth * 0.86f, _cy), UITheme.TextMuted, UITheme.FontSmall);
        UITheme.DrawText(_sb, "grain size", new Vector2(_x - 4, _cy), UITheme.TextMuted, UITheme.FontSmall);
        _cy += 20;
        Row("Sediment pile", column.SedimentKm >= 1f ? $"{column.SedimentKm:F1} km in {column.BedCount} beds" : $"{column.SedimentKm * 1000f:F0} m in {column.BedCount} beds", UITheme.Text);
    }

    /// <summary>Standard lithology symbols, drawn sparsely over the bed colour.</summary>
    private void DrawPattern(Rectangle r, RockKind kind)
    {
        if (r.Height < 4 || r.Width < 6) return;
        Color ink = Color.Black * 0.35f;
        switch (kind)
        {
            case RockKind.Sand: // Dots
                for (int py = r.Y + 3; py < r.Bottom - 1; py += 6)
                    for (int px = r.X + 4 + ((py / 6) % 2) * 4; px < r.Right - 2; px += 8)
                        UITheme.FillRect(_sb, new Rectangle(px, py, 2, 2), ink);
                break;
            case RockKind.Gravel: // Pebbles
                for (int py = r.Y + 3; py < r.Bottom - 4; py += 9)
                    for (int px = r.X + 5 + ((py / 9) % 2) * 7; px < r.Right - 6; px += 14)
                        UITheme.DrawRectOutline(_sb, new Rectangle(px, py, 6, 4), ink, 1);
                break;
            case RockKind.Clay: // Short dashes
                for (int py = r.Y + 3; py < r.Bottom - 1; py += 5)
                    for (int px = r.X + 3 + ((py / 5) % 2) * 6; px < r.Right - 7; px += 12)
                        UITheme.FillRect(_sb, new Rectangle(px, py, 6, 1), ink);
                break;
            case RockKind.Silt: // Dash-dot
                for (int py = r.Y + 3; py < r.Bottom - 1; py += 6)
                    for (int px = r.X + 3 + ((py / 6) % 2) * 5; px < r.Right - 8; px += 13)
                    {
                        UITheme.FillRect(_sb, new Rectangle(px, py, 5, 1), ink);
                        UITheme.FillRect(_sb, new Rectangle(px + 8, py, 1, 1), ink);
                    }
                break;
            case RockKind.Limestone: // Bricks
                for (int py = r.Y + 7; py < r.Bottom; py += 8)
                {
                    UITheme.FillRect(_sb, new Rectangle(r.X, py, r.Width, 1), ink);
                    for (int px = r.X + 8 + ((py / 8) % 2) * 9; px < r.Right - 1; px += 18)
                        UITheme.FillRect(_sb, new Rectangle(px, py - 7, 1, 7), ink);
                }
                break;
            case RockKind.Volcanic: // v
                for (int py = r.Y + 3; py < r.Bottom - 4; py += 8)
                    for (int px = r.X + 5 + ((py / 8) % 2) * 6; px < r.Right - 6; px += 12)
                    {
                        UITheme.DrawLine(_sb, new Vector2(px, py), new Vector2(px + 2, py + 4), Color.White * 0.4f, 1f);
                        UITheme.DrawLine(_sb, new Vector2(px + 2, py + 4), new Vector2(px + 4, py), Color.White * 0.4f, 1f);
                    }
                break;
            case RockKind.Organic: // Solid with light partings
                for (int py = r.Y + 4; py < r.Bottom - 1; py += 5)
                    UITheme.FillRect(_sb, new Rectangle(r.X + 2, py, r.Width - 4, 1), Color.White * 0.12f);
                break;
            case RockKind.Salt: // Crosshatch
                for (int px = r.X; px < r.Right + r.Height; px += 9)
                    UITheme.DrawLine(_sb, new Vector2(Math.Min(px, r.Right), r.Y + Math.Max(0, px - r.Right)), new Vector2(Math.Max(r.X, px - r.Height), Math.Min(r.Bottom, r.Y + px - r.X)), ink, 1f);
                break;
            case RockKind.Granite: // Plus signs
                for (int py = r.Y + 5; py < r.Bottom - 4; py += 11)
                    for (int px = r.X + 6 + ((py / 11) % 2) * 7; px < r.Right - 6; px += 14)
                    {
                        UITheme.FillRect(_sb, new Rectangle(px - 2, py, 5, 1), ink);
                        UITheme.FillRect(_sb, new Rectangle(px, py - 2, 1, 5), ink);
                    }
                break;
            case RockKind.Basalt: // Columns
                for (int px = r.X + 7; px < r.Right - 2; px += 10)
                    UITheme.FillRect(_sb, new Rectangle(px, r.Y + 2, 1, r.Height - 3), Color.White * 0.14f);
                break;
        }
    }

    // ---- Classification helpers ------------------------------------------------------------

    private string GetBiomeType(TerrainCell cell)
    {
        if (cell.IsIce) return cell.IsWater ? "Sea ice" : "Ice sheet";
        if (cell.IsWater) return cell.Elevation < -0.5f ? "Deep ocean" : cell.Elevation < -0.15f ? "Ocean" : "Shallow sea";
        if (cell.Geology.IsVolcano) return "Volcano";
        return cell.GetTerrainType() switch
        {
            TerrainType.Beach => "Coast",
            TerrainType.Plains => "Plains",
            TerrainType.Grassland => "Grassland",
            TerrainType.Forest => "Forest",
            TerrainType.Desert => "Desert",
            TerrainType.Mountain => "Mountains",
            TerrainType.Tundra => "Tundra",
            _ => "Land"
        };
    }

    private Color GetBiomeColor(TerrainCell cell)
    {
        if (cell.IsIce) return new Color(230, 240, 250);
        if (cell.IsWater) return cell.Elevation < -0.5f ? new Color(20, 50, 120) : new Color(50, 120, 200);
        if (cell.Geology.IsVolcano) return new Color(150, 60, 40);
        return cell.GetTerrainType() switch
        {
            TerrainType.Beach => new Color(226, 206, 150),
            TerrainType.Forest => new Color(40, 110, 56),
            TerrainType.Desert => new Color(214, 180, 110),
            TerrainType.Mountain => new Color(140, 130, 124),
            TerrainType.Tundra => new Color(160, 170, 160),
            _ => new Color(110, 160, 80)
        };
    }

    private Color GetTempColor(float temp)
    {
        if (temp < 0) return new Color(140, 200, 255);
        if (temp < 18) return new Color(170, 230, 190);
        if (temp < 30) return new Color(255, 220, 130);
        return new Color(255, 130, 100);
    }

    private Color GetBoundaryColor(PlateBoundaryType boundaryType) => boundaryType switch
    {
        PlateBoundaryType.Convergent => new Color(255, 120, 100),
        PlateBoundaryType.Divergent => new Color(120, 190, 255),
        PlateBoundaryType.Transform => new Color(255, 190, 90),
        _ => UITheme.TextDim
    };
}
