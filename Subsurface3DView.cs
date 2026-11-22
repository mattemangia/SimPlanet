using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace SimPlanet;

/// <summary>
/// "Subsurface 3D": an isometric block cut out of the crust under a region of the map.
/// The top is the living landscape; the sides are geological sections showing sediment beds,
/// crust, Moho, mantle, subducting slabs, magma, faults, oil and gas fields and wells.
/// Everything is read from the running simulation, so plate motion, fault ruptures, seismic
/// waves, eruptions, impacts and sedimentation show up as they happen.
/// </summary>
public sealed partial class Subsurface3DView : IDisposable
{
    private readonly GraphicsDevice _device;
    private readonly PlanetMap _map;
    private GeologicalSimulator? _geo;
    private PetroleumSystem? _oil;
    private SeismologyManager? _seismo;
    private GeothermalSystem? _geothermal;

    /// <summary>Asks the game to cause an event in the block: "quake", "erupt" or "impact".</summary>
    public Action<string>? RequestEvent;

    /// <summary>Shows a line in the event list (for example when a request cannot be met).</summary>
    public void Notify(string text) => AddTicker(text, UITheme.TextDim);

    public bool IsOpen { get; private set; }
    public bool IsSelecting { get; private set; }
    public bool IsMouseOver { get; private set; }

    // Region (in map cells): centre and edge length
    public int CenterX { get; private set; }
    public int CenterY { get; private set; }
    public int Size { get; private set; } = 28;
    private const int MinSize = 12, MaxSize = 44;
    private int _selectSize = 28;
    private (int x, int y)? _hoverCell;

    // Camera
    private float _yaw = MathHelper.ToRadians(38f);
    private float _pitch = MathHelper.ToRadians(30f);
    private float _zoom = 1f;
    private int _cut;                 // Rows kept (the section face is at this row); Size = no cut
    private float _time;

    // Options
    private bool _showArrows = true, _showWaves = true, _showLabels = true;
    private int _tomoMode;            // 0 off, 1 recovered image, 2 true model
    private int? _focusWell;          // Well id the section is cut through

    // Event ticker
    private readonly List<(string text, Color color, float age)> _ticker = new();
    private long _lastEventId;

    private Rectangle _panel;
    private readonly List<(Rectangle rect, string label, Action action, Func<bool>? active, string tip)> _buttons = new();
    private MouseState _prevMouse;
    private KeyboardState _prevKeys;
    private bool _dragging, _panning;
    private float _panAccumX, _panAccumY;
    private string _hoverTip = "";

    public Subsurface3DView(GraphicsDevice device, PlanetMap map)
    {
        _device = device;
        _map = map;
        CreateResources();
    }

    public void SetSystems(GeologicalSimulator geo, PetroleumSystem oil, SeismologyManager seismo, GeothermalSystem geothermal)
    {
        _geothermal = geothermal;
        _geo = geo;
        _oil = oil;
        _seismo = seismo;
    }

    /// <summary>Starts picking a region on the 2D map.</summary>
    public void BeginSelection()
    {
        IsOpen = false;
        IsSelecting = true;
    }

    public void Toggle()
    {
        if (IsOpen || IsSelecting)
        {
            Close();
        }
        else
        {
            BeginSelection();
        }
    }

    public void Close()
    {
        IsOpen = false;
        IsSelecting = false;
        _focusWell = null;
    }

    /// <summary>Opens the block centred on a cell.</summary>
    public void Open(int cellX, int cellY, int size)
    {
        Size = Math.Clamp(size, MinSize, Math.Min(MaxSize, _map.Height - 2));
        CenterX = ((cellX % _map.Width) + _map.Width) % _map.Width;
        CenterY = ClampCenterY(cellY);
        _cut = Size;
        _focusWell = null;
        IsSelecting = false;
        IsOpen = true;
        _snapshotTimer = 0f;
        _hasSnapshot = false;
        _particles.Clear();
        _lastEventId = GeoEvents.Snapshot().Select(e => e.Id).DefaultIfEmpty(0).Max();
    }

    /// <summary>Opens the block around a well with the section cut through the borehole.</summary>
    public void OpenOnWell(OilWell well)
    {
        Open(well.X, well.Y, 24);
        _focusWell = well.Id;
        _cut = (well.Y - OriginY) + 1;
        _yaw = MathHelper.ToRadians(18f);
        _pitch = MathHelper.ToRadians(20f);
    }

    /// <summary>Opens the block with the section cut through a cell (a geothermal plant, say).</summary>
    public void OpenOnCell(int x, int y)
    {
        Open(x, y, 20);
        _cut = (y - OriginY) + 1;
        _yaw = MathHelper.ToRadians(18f);
        _pitch = MathHelper.ToRadians(20f);
    }

    private int ClampCenterY(int y) => Math.Clamp(y, Size / 2 + 1, _map.Height - 1 - (Size - Size / 2));

    private int OriginX => CenterX - Size / 2;
    private int OriginY => CenterY - Size / 2;

    // ---------------------------------------------------------------------------------------
    // Input
    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// Handles input. Returns true while the view owns the mouse and keyboard.
    /// </summary>
    /// <param name="cellUnderMouse">Map cell under the cursor on the 2D map, if any.</param>
    public bool Update(MouseState mouse, KeyboardState keys, Rectangle mapArea, (int x, int y)? cellUnderMouse, float deltaTime)
    {
        _time += deltaTime;
        bool consumed = false;
        bool click = mouse.LeftButton == ButtonState.Released && _prevMouse.LeftButton == ButtonState.Pressed;
        int wheel = mouse.ScrollWheelValue - _prevMouse.ScrollWheelValue;

        if (IsSelecting)
        {
            consumed = true;
            _hoverCell = mapArea.Contains(mouse.Position) ? cellUnderMouse : null;
            if (wheel != 0) _selectSize = Math.Clamp(_selectSize + (wheel > 0 ? 2 : -2), MinSize, Math.Min(MaxSize, _map.Height - 2));
            if (click && _hoverCell.HasValue)
            {
                Open(_hoverCell.Value.x, _hoverCell.Value.y, _selectSize);
            }
            if (Pressed(keys, Keys.Escape) || (mouse.RightButton == ButtonState.Released && _prevMouse.RightButton == ButtonState.Pressed))
            {
                IsSelecting = false;
            }
        }
        else if (IsOpen)
        {
            consumed = true;
            _panel = PanelRect(mapArea);
            IsMouseOver = _panel.Contains(mouse.Position);
            LayoutButtons();

            _hoverTip = "";
            bool overButton = false;
            foreach (var (rect, _, action, _, tip) in _buttons)
            {
                if (!rect.Contains(mouse.Position)) continue;
                overButton = true;
                _hoverTip = tip;
                if (click && !_dragging) action();
            }

            // Left drag orbits, right drag slides the block across the planet
            if (mouse.LeftButton == ButtonState.Pressed && _prevMouse.LeftButton == ButtonState.Released)
            {
                _dragging = IsMouseOver && !overButton && mouse.Y > _panel.Y + HeaderHeight;
            }
            if (mouse.LeftButton == ButtonState.Released) _dragging = false;
            if (_dragging)
            {
                _yaw -= (mouse.X - _prevMouse.X) * 0.008f;
                _pitch = Math.Clamp(_pitch + (mouse.Y - _prevMouse.Y) * 0.006f, MathHelper.ToRadians(8f), MathHelper.ToRadians(75f));
            }

            if (mouse.RightButton == ButtonState.Pressed && _prevMouse.RightButton == ButtonState.Released) _panning = IsMouseOver;
            if (mouse.RightButton == ButtonState.Released) _panning = false;
            if (_panning)
            {
                // Screen motion to map motion, taking the camera heading into account
                float sx = (mouse.X - _prevMouse.X) * 0.06f, sy = (mouse.Y - _prevMouse.Y) * 0.06f;
                float cos = MathF.Cos(_yaw), sin = MathF.Sin(_yaw);
                _panAccumX -= sx * cos + sy * sin;
                _panAccumY -= -sx * sin + sy * cos;
            }

            float keyPan = 14f * deltaTime;
            if (keys.IsKeyDown(Keys.Left) || keys.IsKeyDown(Keys.A)) _panAccumX -= keyPan;
            if (keys.IsKeyDown(Keys.Right)) _panAccumX += keyPan;
            if (keys.IsKeyDown(Keys.Up) || keys.IsKeyDown(Keys.W)) _panAccumY -= keyPan;
            if (keys.IsKeyDown(Keys.Down) || keys.IsKeyDown(Keys.S)) _panAccumY += keyPan;
            int moveX = (int)_panAccumX, moveY = (int)_panAccumY;
            if (moveX != 0 || moveY != 0)
            {
                _panAccumX -= moveX;
                _panAccumY -= moveY;
                CenterX = ((CenterX + moveX) % _map.Width + _map.Width) % _map.Width;
                CenterY = ClampCenterY(CenterY + moveY);
                _focusWell = null;
                _snapshotTimer = 0f;
            }

            if (wheel != 0 && IsMouseOver) _zoom = Math.Clamp(_zoom * (wheel > 0 ? 1.1f : 0.9f), 0.5f, 6f);

            if (Pressed(keys, Keys.Q)) _yaw += MathHelper.PiOver2;
            if (Pressed(keys, Keys.E)) _yaw -= MathHelper.PiOver2;
            if (Pressed(keys, Keys.OemOpenBrackets)) MoveCut(-1);
            if (Pressed(keys, Keys.OemCloseBrackets)) MoveCut(1);
            if (Pressed(keys, Keys.Escape)) Close();
        }
        else
        {
            IsMouseOver = false;
        }

        _prevMouse = mouse;
        _prevKeys = keys;
        return consumed;
    }

    private bool Pressed(KeyboardState keys, Keys key) => keys.IsKeyDown(key) && _prevKeys.IsKeyUp(key);

    private void MoveCut(int delta)
    {
        _cut = Math.Clamp(_cut + delta, 2, Size);
        _focusWell = null;
        _snapshotTimer = 0f;
    }

    private void Resize(int delta)
    {
        int size = Math.Clamp(Size + delta, MinSize, Math.Min(MaxSize, _map.Height - 2));
        bool uncut = _cut >= Size;
        Size = size;
        CenterY = ClampCenterY(CenterY);
        _cut = uncut ? Size : Math.Clamp(_cut, 2, Size);
        _snapshotTimer = 0f;
        _hasSnapshot = false;
    }

    private const int HeaderHeight = 40;

    private static Rectangle PanelRect(Rectangle mapArea) =>
        new Rectangle(mapArea.X + 8, mapArea.Y + 8, mapArea.Width - 16, mapArea.Height - 76);

    private void LayoutButtons()
    {
        _buttons.Clear();
        int y = _panel.Y + 7, h = 26;
        int x = _panel.Right - 10;

        void Add(string label, int width, Action action, Func<bool>? active, string tip)
        {
            x -= width;
            _buttons.Add((new Rectangle(x, y, width, h), label, action, active, tip));
            x -= 5;
        }

        Add("X", 28, Close, null, "Close (Esc)");
        x -= 8;
        Add("Impact", 60, () => RequestEvent?.Invoke("impact"), null, "Drop a small asteroid on the middle of the block");
        Add("Erupt", 54, () => RequestEvent?.Invoke("erupt"), null, "Make the nearest volcano in the block erupt");
        Add("Quake", 56, () => RequestEvent?.Invoke("quake"), null, "Break the nearest fault in the block");
        x -= 8;
        Add("Tomo", 54, () => _tomoMode = (_tomoMode + 1) % 3, () => _tomoMode != 0, "Seismic tomography on the sections: off / recovered by the networks / true crust");
        Add("Labels", 62, () => _showLabels = !_showLabels, () => _showLabels, "Names of the structures");
        Add("Waves", 58, () => _showWaves = !_showWaves, () => _showWaves, "Ground motion from seismic waves, ruptures and impacts");
        Add("Forces", 62, () => _showArrows = !_showArrows, () => _showArrows, "Plate motion, uplift and subsidence arrows");
        x -= 8;
        Add(">", 26, () => MoveCut(1), null, "Move the section forward ( ] )");
        Add("Cut", 40, () => { _cut = _cut >= Size ? Size / 2 : Size; _focusWell = null; _snapshotTimer = 0f; }, () => _cut < Size, "Slice the block open");
        Add("<", 26, () => MoveCut(-1), null, "Move the section back ( [ )");
        x -= 8;
        Add("+", 26, () => Resize(4), null, "Larger region");
        Add("-", 26, () => Resize(-4), null, "Smaller region");
        x -= 8;
        Add("E", 26, () => _yaw -= MathHelper.PiOver2, null, "Rotate right (E)");
        Add("Q", 26, () => _yaw += MathHelper.PiOver2, null, "Rotate left (Q)");
    }

    // ---------------------------------------------------------------------------------------
    // 2D drawing (panel, labels, readouts)
    // ---------------------------------------------------------------------------------------

    /// <summary>Square outline that follows the cursor while a region is being picked.</summary>
    public void DrawSelection(SpriteBatch sb, Func<float, float, Vector2> cellToScreen, float cellPixels, Rectangle mapArea)
    {
        if (!IsSelecting) return;

        string hint = "SUBSURFACE 3D   click a region  -  wheel: size  -  right click / Esc: cancel";
        var size = UITheme.Measure(hint, UITheme.FontNormal);
        var box = new Rectangle(mapArea.Center.X - (int)size.X / 2 - 14, mapArea.Y + 14, (int)size.X + 28, 30);
        UITheme.DrawPanel(sb, box, UITheme.HeaderBg, UITheme.Gold);
        UITheme.DrawText(sb, hint, new Vector2(box.X + 14, box.Y + 7), UITheme.Gold, UITheme.FontNormal);

        if (!_hoverCell.HasValue) return;
        int half = _selectSize / 2;
        int cy = Math.Clamp(_hoverCell.Value.y, half + 1, _map.Height - 1 - (_selectSize - half));
        Vector2 topLeft = cellToScreen(_hoverCell.Value.x - half, cy - half);
        int pixels = (int)(_selectSize * cellPixels);
        var rect = new Rectangle((int)topLeft.X, (int)topLeft.Y, pixels, pixels);
        float pulse = 0.6f + 0.4f * MathF.Sin(_time * 5f);
        UITheme.FillRect(sb, rect, UITheme.Gold * 0.12f);
        UITheme.DrawRectOutline(sb, rect, UITheme.Gold * pulse, 2);
        UITheme.DrawRectOutline(sb, new Rectangle(rect.X - 2, rect.Y - 2, rect.Width + 4, rect.Height + 4), Color.Black * 0.5f, 1);
        UITheme.DrawTextOutlined(sb, $"{_selectSize} x {_selectSize}", new Vector2(rect.X + 4, rect.Bottom + 3), UITheme.Gold, UITheme.FontSmall);
    }

    /// <summary>Outline of the open block on the 2D map (drawn under the panel edge areas).</summary>
    public void DrawPanel(SpriteBatch sb, Rectangle mapArea)
    {
        if (!IsOpen || _target == null) return;
        _panel = PanelRect(mapArea);

        UITheme.DrawShadow(sb, _panel, 10);
        sb.Draw(_target, _panel, Color.White);
        UITheme.DrawRectOutline(sb, _panel, UITheme.Border, 1);

        // Header
        var header = new Rectangle(_panel.X, _panel.Y, _panel.Width, HeaderHeight);
        UITheme.FillRect(sb, header, new Color(14, 20, 32, 235));
        UITheme.FillRect(sb, new Rectangle(header.X, header.Bottom - 1, header.Width, 1), UITheme.Gold * 0.7f);
        UITheme.DrawText(sb, "SUBSURFACE 3D", new Vector2(header.X + 14, header.Y + 10), UITheme.Gold, UITheme.FontMedium);

        float lon = (CenterX / (float)_map.Width) * 360f - 180f;
        float lat = 90f - (CenterY / (float)_map.Height) * 180f;
        float km = 40000f / _map.Width * Size;
        string where = $"{MathF.Abs(lat):F0}{(lat >= 0 ? "N" : "S")} {MathF.Abs(lon):F0}{(lon >= 0 ? "E" : "W")}   {km:N0} km across   {ComputeBackend.Description}";
        UITheme.DrawText(sb, where, new Vector2(header.X + 150, header.Y + 13), UITheme.TextDim, UITheme.FontSmall);

        var mouse = Mouse.GetState().Position;
        foreach (var (rect, label, _, active, _) in _buttons)
        {
            UITheme.DrawButton(sb, rect, label, rect.Contains(mouse), active != null && active());
        }

        if (_showLabels) DrawLabels(sb);
        DrawLegend(sb);
        DrawTicker(sb);
        DrawInstruments(sb);
        DrawWellLog(sb);

        string help = "drag: orbit   right drag / WASD: move across the planet   wheel: zoom   [ ]: section";
        UITheme.DrawText(sb, help, new Vector2(_panel.X + 14, _panel.Bottom - 22), UITheme.TextMuted, UITheme.FontSmall);

        if (_hoverTip.Length > 0)
        {
            UITheme.DrawTooltip(sb, _hoverTip, mouse, _device.Viewport.Width, _device.Viewport.Height);
        }
    }

    private void DrawLabels(SpriteBatch sb)
    {
        var used = new List<Rectangle>();
        foreach (var (position, text, color, priority) in _labels.OrderByDescending(l => l.priority))
        {
            if (!TryProject(position, out var screen)) continue;
            var size = UITheme.Measure(text, UITheme.FontSmall);
            var rect = new Rectangle((int)(screen.X - size.X / 2) - 5, (int)(screen.Y - size.Y / 2) - 2, (int)size.X + 10, (int)size.Y + 4);
            if (rect.X < _panel.X + 4 || rect.Right > _panel.Right - 4 || rect.Y < _panel.Y + HeaderHeight + 2 || rect.Bottom > _panel.Bottom - 26) continue;
            if (used.Any(u => u.Intersects(rect))) continue;
            used.Add(rect);
            UITheme.FillRounded(sb, rect, new Color(8, 12, 20, 190));
            UITheme.FillRect(sb, new Rectangle(rect.X, rect.Y + 2, 2, rect.Height - 4), color);
            UITheme.DrawText(sb, text, new Vector2(rect.X + 6, rect.Y + 2), UITheme.Text, UITheme.FontSmall);
        }
    }

    private static (string name, Color color)[] LegendEntries => _legend ??= new (string name, Color color)[]
    {
        ("Clay", BedColor(SedimentType.Clay)), ("Silt", BedColor(SedimentType.Silt)), ("Sand", BedColor(SedimentType.Sand)),
        ("Gravel", BedColor(SedimentType.Gravel)), ("Organic", BedColor(SedimentType.Organic)), ("Ash / lava", BedColor(SedimentType.Volcanic)),
        ("Limestone", BedColor(SedimentType.Limestone)), ("Granite crust", GraniteColor), ("Basalt crust", BasaltColor),
        ("Mantle", MantleTop), ("Magma", MagmaColor), ("Oil", OilColor), ("Gas", GasColor)
    };
    private static (string name, Color color)[]? _legend;

    private void DrawLegend(SpriteBatch sb)
    {
        int rows = (LegendEntries.Length + 1) / 2;
        var box = new Rectangle(_panel.X + 10, _panel.Bottom - 34 - rows * 17 - 14, 214, rows * 17 + 14);
        UITheme.FillRounded(sb, box, new Color(8, 12, 20, 170));
        for (int i = 0; i < LegendEntries.Length; i++)
        {
            int col = i / rows, row = i % rows;
            int x = box.X + 9 + col * 104, y = box.Y + 8 + row * 17;
            UITheme.FillRect(sb, new Rectangle(x, y + 2, 12, 10), LegendEntries[i].color);
            UITheme.DrawRectOutline(sb, new Rectangle(x, y + 2, 12, 10), Color.Black * 0.5f);
            UITheme.DrawText(sb, LegendEntries[i].name, new Vector2(x + 18, y), UITheme.TextDim, UITheme.FontSmall);
        }

        if (_tomoMode != 0)
        {
            var bar = new Rectangle(box.X, box.Y - 46, box.Width, 40);
            UITheme.FillRounded(sb, bar, new Color(8, 12, 20, 170));
            string title = _tomoMode == 1
                ? $"TOMOGRAPHY  {_seismo?.TomoRays ?? 0} rays  r={_seismo?.TomoCorrelation ?? 0:F2}"
                : "TRUE CRUSTAL P VELOCITY";
            UITheme.DrawText(sb, title, new Vector2(bar.X + 9, bar.Y + 5), UITheme.Accent, UITheme.FontSmall);
            for (int i = 0; i < 60; i++)
            {
                UITheme.FillRect(sb, new Rectangle(bar.X + 34 + i * 2, bar.Y + 24, 2, 8), VelocityColor(-1f + i / 30f));
            }
            UITheme.DrawText(sb, "slow", new Vector2(bar.X + 6, bar.Y + 21), UITheme.TextDim, UITheme.FontSmall);
            UITheme.DrawText(sb, "fast", new Vector2(bar.X + 160, bar.Y + 21), UITheme.TextDim, UITheme.FontSmall);
        }
    }

    private void DrawTicker(SpriteBatch sb)
    {
        int y = _panel.Y + HeaderHeight + 10;
        foreach (var (text, color, age) in _ticker)
        {
            float alpha = Math.Clamp(1.2f - age / 14f, 0f, 1f);
            if (alpha <= 0) continue;
            var size = UITheme.Measure(text, UITheme.FontNormal);
            var rect = new Rectangle(_panel.X + 10, y, (int)size.X + 22, 22);
            UITheme.FillRounded(sb, rect, new Color(8, 12, 20) * (0.75f * alpha));
            UITheme.FillRect(sb, new Rectangle(rect.X, rect.Y + 3, 3, rect.Height - 6), color * alpha);
            UITheme.DrawText(sb, text, new Vector2(rect.X + 10, rect.Y + 3), UITheme.Text * alpha, UITheme.FontNormal);
            y += 26;
        }
    }

    /// <summary>Seismograms of the nearest station and the reflection section of the focused field.</summary>
    private void DrawInstruments(SpriteBatch sb)
    {
        int right = _panel.Right - 10;
        int bottom = _panel.Bottom - 30;

        // Reflection seismic section
        var survey = FocusSurvey();
        if (survey != null && survey.Completed && survey.Section.Length > 0)
        {
            EnsureSectionTexture(survey);
            if (_sectionTexture != null)
            {
                var box = new Rectangle(right - 330, bottom - 232, 330, 232);
                UITheme.DrawPanel(sb, box, new Color(10, 14, 22, 235), UITheme.Border);
                var reservoir = _oil!.Reservoirs[survey.ReservoirId];
                UITheme.DrawText(sb, $"REFLECTION SEISMIC  line {survey.Id}  ({(survey.Marine ? "marine" : "land")})", new Vector2(box.X + 8, box.Y + 5), UITheme.Gold, UITheme.FontSmall);
                var image = new Rectangle(box.X + 30, box.Y + 24, box.Width - 38, box.Height - 46);
                sb.Draw(_sectionTexture, image, Color.White);
                UITheme.DrawRectOutline(sb, image, UITheme.Border);
                for (int s = 0; s <= 4; s++)
                {
                    int ty = image.Y + image.Height * s / 4;
                    UITheme.DrawText(sb, $"{s}s", new Vector2(box.X + 8, ty - 7), UITheme.TextMuted, UITheme.FontSmall);
                }
                string what = reservoir.HasGas ? "bright spot: gas" : "soft reflector: oil";
                UITheme.DrawText(sb, $"{reservoir.Trap} trap, {what}, top {reservoir.DepthKm:F1} km", new Vector2(box.X + 8, box.Bottom - 19), UITheme.TextDim, UITheme.FontSmall);
                bottom = box.Y - 8;
            }
        }

        // Seismograms
        if (_seismo != null && _seismo.Stations.Count > 0)
        {
            var station = NearestStation();
            if (station != null && station.Samples > 4 && (_seismo.IsPropagating || _seismogramHold > 0f))
            {
                var box = new Rectangle(right - 330, bottom - 132, 330, 132);
                UITheme.DrawPanel(sb, box, new Color(10, 14, 22, 235), UITheme.Border);
                UITheme.DrawText(sb, $"SEISMOGRAPH ({station.X},{station.Y})   M{_seismo.EventMagnitude:F1} at {station.DistanceKm:N0} km", new Vector2(box.X + 8, box.Y + 5), UITheme.Gold, UITheme.FontSmall);
                DrawTrace(sb, new Rectangle(box.X + 22, box.Y + 26, box.Width - 30, 40), station.TraceP, station.Samples, station.PickP, new Color(120, 200, 255), "P");
                DrawTrace(sb, new Rectangle(box.X + 22, box.Y + 70, box.Width - 30, 40), station.TraceS, station.Samples, station.PickS, new Color(255, 130, 110), "S");
                string picks = station.PickP >= 0
                    ? $"P {station.PickP:F0} s" + (station.PickS >= 0 ? $"   S {station.PickS:F0} s   S-P {station.PickS - station.PickP:F0} s" : "")
                    : "waiting for the first arrival...";
                UITheme.DrawText(sb, picks, new Vector2(box.X + 8, box.Bottom - 19), UITheme.TextDim, UITheme.FontSmall);
            }
        }
    }

    private void DrawTrace(SpriteBatch sb, Rectangle rect, float[] trace, int samples, float pick, Color color, string name)
    {
        UITheme.FillRect(sb, rect, new Color(4, 7, 12, 200));
        UITheme.FillRect(sb, new Rectangle(rect.X, rect.Center.Y, rect.Width, 1), Color.White * 0.12f);
        UITheme.DrawText(sb, name, new Vector2(rect.X - 14, rect.Y + 12), color, UITheme.FontSmall);

        int total = Math.Max(samples, 1);
        int length = Math.Max(total, trace.Length > 0 ? Math.Min(trace.Length, total + total / 2) : total);
        float peak = MathF.Max(ComputeBackend.MaxAbs(trace.AsSpan(0, Math.Min(samples, trace.Length))), SeismologyManager.NoiseFloor * 6f);
        Vector2 previous = default;
        for (int px = 0; px < rect.Width; px++)
        {
            int i = px * length / rect.Width;
            if (i >= samples) break;
            // Instrument noise on top of the simulated ground motion
            float noise = (Hash(i * 7 + name[0]) - 0.5f) * SeismologyManager.NoiseFloor * 2f;
            float v = Math.Clamp((trace[i] + noise) / peak, -1f, 1f);
            var point = new Vector2(rect.X + px, rect.Center.Y - v * (rect.Height / 2 - 2));
            if (px > 0) UITheme.DrawLine(sb, previous, point, color, 1.2f);
            previous = point;
        }

        if (pick >= 0 && _seismo != null)
        {
            int px = (int)(pick / _seismo.SampleSeconds * rect.Width / length);
            if (px < rect.Width)
            {
                UITheme.FillRect(sb, new Rectangle(rect.X + px, rect.Y, 1, rect.Height), UITheme.Gold);
            }
        }
    }

    private static float Hash(int n)
    {
        unchecked
        {
            uint h = (uint)n * 2654435761u;
            h ^= h >> 15;
            h *= 2246822519u;
            return ((h ^ (h >> 13)) & 0xFFFF) / 65535f;
        }
    }

    private SeismicStation? NearestStation()
    {
        if (_seismo == null) return null;
        SeismicStation? best = null;
        float bestDistance = float.MaxValue;
        foreach (var station in _seismo.Stations)
        {
            float d = _seismo.DistanceCells(station.X, station.Y, CenterX, CenterY);
            if (d < bestDistance)
            {
                bestDistance = d;
                best = station;
            }
        }
        return best;
    }

    private SeismicSurvey? FocusSurvey()
    {
        if (_oil == null) return null;
        Reservoir? reservoir = null;
        if (_focusWell.HasValue)
        {
            var well = _oil.Wells.FirstOrDefault(w => w.Id == _focusWell.Value);
            if (well != null && well.ReservoirId >= 0) reservoir = _oil.Reservoirs[well.ReservoirId];
        }

        SeismicSurvey? best = null;
        float bestDistance = Size * 0.6f;
        foreach (var survey in _oil.Surveys)
        {
            if (!survey.Completed) continue;
            if (reservoir != null && survey.ReservoirId == reservoir.Id) return survey;
            if (reservoir != null) continue;
            var target = _oil.Reservoirs[survey.ReservoirId];
            float dx = MathF.Abs(target.CenterX - CenterX);
            dx = MathF.Min(dx, _map.Width - dx);
            float d = MathF.Max(dx, MathF.Abs(target.CenterY - CenterY));
            if (d < bestDistance)
            {
                bestDistance = d;
                best = survey;
            }
        }
        return best;
    }

    private Texture2D? _sectionTexture;
    private int _sectionSurveyId = -1;

    /// <summary>Paints a survey's section in the usual red-white-blue amplitude scale.</summary>
    private void EnsureSectionTexture(SeismicSurvey survey)
    {
        if (_sectionSurveyId == survey.Id && _sectionTexture != null) return;
        _sectionSurveyId = survey.Id;
        _sectionTexture?.Dispose();
        // Rendered at display size with interpolation between traces
        int width = survey.Traces * 3, height = survey.Samples;
        _sectionTexture = new Texture2D(_device, width, height);
        var pixels = new Color[width * height];
        float gain = 1f / MathF.Max(0.02f, ComputeBackend.MaxAbs(survey.Section) * 0.45f);
        for (int x = 0; x < width; x++)
        {
            float ft = x / (float)(width - 1) * (survey.Traces - 1);
            int t0 = (int)ft, t1 = Math.Min(survey.Traces - 1, t0 + 1);
            float blend = ft - t0;
            for (int sample = 0; sample < height; sample++)
            {
                float a = survey.Section[t0 * survey.Samples + sample] * (1f - blend) + survey.Section[t1 * survey.Samples + sample] * blend;
                a = Math.Clamp(a * gain, -1f, 1f);
                Color c = a >= 0
                    ? Color.Lerp(new Color(236, 232, 224), new Color(20, 40, 150), a)
                    : Color.Lerp(new Color(236, 232, 224), new Color(170, 20, 20), -a);
                pixels[sample * width + x] = c;
            }
        }
        _sectionTexture.SetData(pixels);
    }

    public void Dispose()
    {
        _target?.Dispose();
        _sectionTexture?.Dispose();
        _effect?.Dispose();
        _grain?.Dispose();
        _dot?.Dispose();
        _white?.Dispose();
    }
}
