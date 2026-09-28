using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace SimPlanet;

/// <summary>
/// Renders the planet terrain using procedural colors
/// </summary>
public class TerrainRenderer
{
    private readonly PlanetMap _map;
    private readonly GraphicsDevice _graphicsDevice;
    private Texture2D _pixelTexture;
    private Texture2D _terrainTexture;
    private Color[] _terrainColors;
    private CivilizationManager? _civilizationManager;
    private int[] _civOwnerMap;
    private List<(Color, string)>? _cachedCivLegend;

    // --- High resolution ("detail") terrain texture ---------------------
    // Every cell is expanded to _detailScale x _detailScale texels with smooth,
    // coastline-aware interpolation, hill shading, political borders and roads.
    private readonly int _detailScale;
    private Texture2D? _detailTexture;
    private Color[] _detailColors = Array.Empty<Color>();
    private readonly float[] _cellField;      // signed land/water field per cell (water < 0)
    private readonly float[] _cellShade;      // hill-shade multiplier per cell
    private readonly bool[] _cellRoad;        // road present on the cell
    private readonly bool[] _cellIce;         // ice-covered cell (sea ice or glacier)
    private readonly float[] _detailNoise;    // static per-texel noise in [-1, 1]
    private bool _hasTerritory;
    private DateTime _lastTextureUpdate = DateTime.MinValue;
    private RenderMode _lastRenderedMode = (RenderMode)(-1);
    private const double TextureRefreshIntervalMs = 180; // Cap expensive re-renders to ~5/s

    // --- Night side overlay (cell resolution, drawn with linear filtering) ---
    private Texture2D _nightTexture;
    private Color[] _nightColors;
    private float[] _cellDarkness;
    private DateTime _lastNightUpdate = DateTime.MinValue;

    // --- Background & sprites ---
    private Texture2D _starfield;
    private Texture2D _grain;
    private MapIcons? _icons;
    private bool _initialFitDone;

    // --- Snapshot of civilization data used for drawing (see CivRenderData) ---
    public CivRenderData CivData { get; private set; } = CivRenderData.Empty;
    private DateTime _lastCivSnapshot = DateTime.MinValue;

    public void SetCivilizationManager(CivilizationManager manager)
    {
        _civilizationManager = manager;
    }

    public int CellSize { get; set; } = 4;

    private RenderMode _mode = RenderMode.Terrain;
    public RenderMode Mode
    {
        get => _mode;
        set
        {
            if (_mode != value)
            {
                _mode = value;
                _isDirty = true; // Mark for redraw when mode changes
            }
        }
    }

    // Performance optimization: only update texture when needed
    private bool _isDirty = true;
    public void MarkDirty() => _isDirty = true;

    // Camera controls
    public float CameraX { get; set; } = 0;
    public float CameraY { get; set; } = 0;
    public float ZoomLevel { get; set; } = 1.0f;

    // Day/night cycle
    public float DayNightTime { get; set; } = 0; // 0-24 hours
    public bool ShowDayNight { get; set; } = false;
    public bool ShowCityLights { get; set; } = true;

    /// <summary>Show city/army/battle markers and names on the map.</summary>
    public bool ShowSettlementLabels { get; set; } = true;

    public TerrainRenderer(PlanetMap map, GraphicsDevice graphicsDevice)
    {
        if (map == null)
            throw new ArgumentNullException(nameof(map), "PlanetMap cannot be null");
        if (graphicsDevice == null)
            throw new ArgumentNullException(nameof(graphicsDevice), "GraphicsDevice cannot be null");

        _map = map;
        _graphicsDevice = graphicsDevice;

        // Create a 1x1 white pixel for drawing
        _pixelTexture = new Texture2D(_graphicsDevice, 1, 1);
        _pixelTexture.SetData(new[] { Color.White });

        // Create terrain texture
        _terrainTexture = new Texture2D(_graphicsDevice, map.Width, map.Height);
        _terrainColors = new Color[map.Width * map.Height];
        _civOwnerMap = new int[map.Width * map.Height];
        _cellField = new float[map.Width * map.Height];
        _cellShade = new float[map.Width * map.Height];
        _cellRoad = new bool[map.Width * map.Height];
        _cellIce = new bool[map.Width * map.Height];

        // Pick the detail scale so the texture stays around half a megapixel and
        // within the Reach profile limit of 2048 texels per side.
        int cells = map.Width * map.Height;
        int scale = (int)MathF.Floor(MathF.Sqrt(600000f / Math.Max(1, cells)));
        scale = Math.Clamp(scale, 1, 6);
        while (scale > 1 && (map.Width * scale > 2048 || map.Height * scale > 2048)) scale--;
        _detailScale = scale;
        if (_detailScale > 1)
        {
            _detailTexture = new Texture2D(_graphicsDevice, map.Width * _detailScale, map.Height * _detailScale);
            _detailColors = new Color[map.Width * _detailScale * map.Height * _detailScale];
        }
        _detailNoise = BuildDetailNoise(map.Width * _detailScale, map.Height * _detailScale);

        _nightTexture = new Texture2D(_graphicsDevice, map.Width, map.Height);
        _nightColors = new Color[map.Width * map.Height];
        _cellDarkness = new float[map.Width * map.Height];

        _starfield = BuildStarfield(_graphicsDevice, 256);
        _grain = BuildGrain(_graphicsDevice, 128);
        try
        {
            _icons = MapIcons.GetShared(_graphicsDevice);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to build map icons: {ex.Message}");
        }

        UpdateTerrainTexture();
    }

    public void UpdateTerrainTexture()
    {
        // Civilization snapshot for markers/UI (cheap, throttled). This method is called
        // while the simulation lock is held, so reading the live collections is safe here.
        if ((DateTime.Now - _lastCivSnapshot).TotalMilliseconds > 150)
        {
            CivData = CivRenderData.Capture(_civilizationManager);
            _lastCivSnapshot = DateTime.Now;
        }

        if (ShowDayNight && (DateTime.Now - _lastNightUpdate).TotalMilliseconds > 50)
        {
            UpdateNightOverlay();
            _lastNightUpdate = DateTime.Now;
        }

        // Performance optimization: only update when data has changed
        if (!_isDirty)
            return;

        // Throttle full re-renders while the simulation is running; a mode change is immediate
        bool modeChanged = _lastRenderedMode != Mode;
        if (!modeChanged && (DateTime.Now - _lastTextureUpdate).TotalMilliseconds < TextureRefreshIntervalMs)
            return;

        _isDirty = false; // Clear dirty flag immediately to catch updates during processing
        _lastTextureUpdate = DateTime.Now;
        _lastRenderedMode = Mode;

        // Territory ownership (used by the political map and by the borders drawn on terrain)
        Array.Fill(_civOwnerMap, 0);
        _hasTerritory = false;
        if (_civilizationManager != null)
        {
            try
            {
                var civs = _civilizationManager.GetAllCivilizations();

                // Update legend cache
                var legend = new List<(Color, string)>();
                foreach (var civ in civs)
                {
                    legend.Add((GetCivColor(civ.Id), civ.Name));
                    foreach (var (cx, cy) in civ.Territory)
                    {
                        int idx = cy * _map.Width + cx;
                        if (idx >= 0 && idx < _civOwnerMap.Length)
                        {
                            _civOwnerMap[idx] = civ.Id;
                            _hasTerritory = true;
                        }
                    }
                }
                legend.Add((new Color(80, 80, 80), "Unclaimed Land"));
                legend.Add((new Color(20, 40, 80), "Water"));
                _cachedCivLegend = legend;
            }
            catch (InvalidOperationException)
            {
                // Territory modified concurrently; the next refresh will pick it up
                _isDirty = true;
            }
        }

        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                var cell = _map.Cells[x, y];
                if (cell == null)
                {
                    Console.WriteLine($"ERROR: Null cell at ({x}, {y})!");
                    continue;
                }

                int index = y * _map.Width + x;
                if (index < 0 || index >= _terrainColors.Length)
                {
                    Console.WriteLine($"ERROR: Index {index} out of bounds at ({x}, {y})!");
                    continue;
                }


                Color baseColor = Mode switch
                {
                    RenderMode.Terrain => GetTerrainColor(cell),
                    RenderMode.TerrainClean => GetTerrainColor(cell),
                    RenderMode.Temperature => GetTemperatureColor(cell),
                    RenderMode.Rainfall => GetRainfallColor(cell),
                    RenderMode.Life => GetLifeColor(cell),
                    RenderMode.Oxygen => GetOxygenColor(cell),
                    RenderMode.CO2 => GetCO2Color(cell),
                    RenderMode.Elevation => GetElevationColor(cell),
                    RenderMode.Geological => GetGeologicalColor(cell),
                    RenderMode.TectonicPlates => GetTectonicPlateColor(cell),
                    RenderMode.Volcanoes => GetVolcanoColor(cell),
                    RenderMode.Clouds => GetCloudsColor(cell),
                    RenderMode.Wind => GetWindColor(cell),
                    RenderMode.Pressure => GetPressureColor(cell),
                    RenderMode.Storms => GetStormsColor(cell),
                    RenderMode.Biomes => GetBiomeColor(cell),
                    RenderMode.Resources => GetResourcesColor(cell),
                    RenderMode.Albedo => GetAlbedoColor(cell),
                    RenderMode.Radiation => GetRadiationColor(cell),
                    RenderMode.Earthquakes => GetEarthquakesColor(cell),
                    RenderMode.Faults => GetFaultsColor(cell),
                    RenderMode.Tsunamis => GetTsunamisColor(cell),
                    RenderMode.Infrastructure => GetInfrastructureColor(cell),
                    RenderMode.Electricity => GetElectricityColor(cell),
                    RenderMode.SpectralBands => GetSpectralBandsColor(cell),
                    RenderMode.Civilizations => GetCivilizationColor(cell, x, y),
                    RenderMode.Auroras => GetAuroraColor(cell),
                    _ => Color.Black
                };

                _terrainColors[index] = baseColor;

                // Signed land/water field used for smooth coastlines. Dry basins sit
                // below sea level but are drawn as land.
                var geo = cell.GetGeology();
                bool dryBasin = cell.IsWater && (geo.IsDryBasin || (!geo.IsConnectedToOcean && geo.AccumulatedWater < 0.1f));
                bool renderedAsWater = cell.IsWater && !dryBasin;
                _cellField[index] = renderedAsWater ? Math.Min(cell.Elevation, -0.002f) : Math.Max(cell.Elevation, 0.002f);
                _cellRoad[index] = geo.HasRoad && cell.IsLand;
                _cellIce[index] = cell.IsIce;
            }
        }

        _terrainTexture.SetData(_terrainColors);

        if (_detailTexture != null)
        {
            ComputeHillShade();
            BuildDetailTexture();
            _detailTexture.SetData(_detailColors);
        }
    }

    // ------------------------------------------------------------------
    // Detail texture
    // ------------------------------------------------------------------

    private static bool IsShadedMode(RenderMode mode) => mode is RenderMode.Terrain or RenderMode.TerrainClean
        or RenderMode.Elevation or RenderMode.Biomes or RenderMode.Civilizations or RenderMode.Resources
        or RenderMode.Infrastructure or RenderMode.Geological or RenderMode.Life;

    private static bool IsDiscreteMode(RenderMode mode) => mode is RenderMode.TectonicPlates or RenderMode.Civilizations
        or RenderMode.Biomes or RenderMode.Resources or RenderMode.Infrastructure or RenderMode.Geological
        or RenderMode.Faults or RenderMode.Electricity or RenderMode.Earthquakes;

    private void ComputeHillShade()
    {
        int w = _map.Width, h = _map.Height;
        for (int y = 0; y < h; y++)
        {
            int yu = Math.Max(0, y - 1), yd = Math.Min(h - 1, y + 1);
            for (int x = 0; x < w; x++)
            {
                int xl = (x - 1 + w) % w, xr = (x + 1) % w;
                float eL = Math.Max(0f, _cellField[y * w + xl]);
                float eR = Math.Max(0f, _cellField[y * w + xr]);
                float eU = Math.Max(0f, _cellField[yu * w + x]);
                float eD = Math.Max(0f, _cellField[yd * w + x]);
                // Light from the north-west
                float slope = (eL - eR) + (eU - eD);
                _cellShade[y * w + x] = Math.Clamp(1f + slope * 2.2f, 0.62f, 1.32f);
            }
        }
    }

    private void BuildDetailTexture()
    {
        int s = _detailScale;
        int w = _map.Width, h = _map.Height;
        int dw = w * s, dh = h * s;
        bool terrainLike = Mode is RenderMode.Terrain or RenderMode.TerrainClean;
        bool shaded = IsShadedMode(Mode);
        bool discrete = IsDiscreteMode(Mode);
        bool political = Mode == RenderMode.Terrain && _hasTerritory;
        bool roads = (Mode == RenderMode.Terrain || Mode == RenderMode.Civilizations) && s >= 3;
        int mid = s / 2;

        Parallel.For(0, dh, py =>
        {
            float fy = (py + 0.5f) / s - 0.5f;
            int y0 = (int)MathF.Floor(fy);
            float ty = fy - y0;
            int y1 = Math.Min(h - 1, y0 + 1);
            y0 = Math.Max(0, y0);
            int cy = py / s, ly = py % s;

            for (int px = 0; px < dw; px++)
            {
                float fx = (px + 0.5f) / s - 0.5f;
                int x0 = (int)MathF.Floor(fx);
                float tx = fx - x0;
                int x1 = x0 + 1;
                if (x0 < 0) x0 += w;
                if (x1 >= w) x1 -= w;
                int cx = px / s, lx = px % s;

                int i00 = y0 * w + x0, i10 = y0 * w + x1, i01 = y1 * w + x0, i11 = y1 * w + x1;
                float w00 = (1 - tx) * (1 - ty), w10 = tx * (1 - ty), w01 = (1 - tx) * ty, w11 = tx * ty;

                float f00 = _cellField[i00], f10 = _cellField[i10], f01 = _cellField[i01], f11 = _cellField[i11];
                bool wa = f00 < 0, wb = f10 < 0, wc = f01 < 0, wd = f11 < 0;
                bool mixed = !(wa == wb && wb == wc && wc == wd);
                float noise = _detailNoise[py * dw + px];

                float field = f00 * w00 + f10 * w10 + f01 * w01 + f11 * w11;
                if (mixed) field += noise * 0.03f; // natural, slightly ragged coastlines
                bool water = field < 0;

                // Ice is a second class boundary (pack ice edges, glaciers)
                bool ia = _cellIce[i00], ib = _cellIce[i10], ic = _cellIce[i01], id = _cellIce[i11];
                bool iceMixed = !(ia == ib && ib == ic && ic == id);
                bool ice = ia;
                if (iceMixed)
                {
                    float iceField = (ia ? w00 : -w00) + (ib ? w10 : -w10) + (ic ? w01 : -w01) + (id ? w11 : -w11);
                    ice = iceField + noise * 0.45f > 0;
                }

                // Blend only the corners that belong to the same class (land vs water)
                float r = 0, g = 0, b = 0, wsum = 0;
                if (discrete)
                {
                    int best = -1; float bw = -1;
                    if (wa == water && ia == ice && w00 > bw) { bw = w00; best = i00; }
                    if (wb == water && ib == ice && w10 > bw) { bw = w10; best = i10; }
                    if (wc == water && ic == ice && w01 > bw) { bw = w01; best = i01; }
                    if (wd == water && id == ice && w11 > bw) { bw = w11; best = i11; }
                    if (best < 0) best = cy * w + cx;
                    var c = _terrainColors[best];
                    r = c.R; g = c.G; b = c.B; wsum = 1;
                }
                else
                {
                    Accumulate(i00, w00, wa == water && ia == ice, ref r, ref g, ref b, ref wsum);
                    Accumulate(i10, w10, wb == water && ib == ice, ref r, ref g, ref b, ref wsum);
                    Accumulate(i01, w01, wc == water && ic == ice, ref r, ref g, ref b, ref wsum);
                    Accumulate(i11, w11, wd == water && id == ice, ref r, ref g, ref b, ref wsum);
                    if (wsum <= 0.0001f)
                    {
                        // No corner of the exact class: fall back to the land/water class only
                        Accumulate(i00, w00, wa == water, ref r, ref g, ref b, ref wsum);
                        Accumulate(i10, w10, wb == water, ref r, ref g, ref b, ref wsum);
                        Accumulate(i01, w01, wc == water, ref r, ref g, ref b, ref wsum);
                        Accumulate(i11, w11, wd == water, ref r, ref g, ref b, ref wsum);
                    }
                    if (wsum <= 0.0001f)
                    {
                        var c = _terrainColors[cy * w + cx];
                        r = c.R; g = c.G; b = c.B; wsum = 1;
                    }
                }
                r /= wsum; g /= wsum; b /= wsum;

                if (!water)
                {
                    if (shaded)
                    {
                        float shade = _cellShade[i00] * w00 + _cellShade[i10] * w10 + _cellShade[i01] * w01 + _cellShade[i11] * w11;
                        r *= shade; g *= shade; b *= shade;
                    }

                    int cellIdx = cy * w + cx;

                    if (political)
                    {
                        int owner = _civOwnerMap[cellIdx];
                        if (owner > 0)
                        {
                            Color cc = GetCivColor(owner);
                            // Light wash of the owner's colour over its land
                            r = r * 0.86f + cc.R * 0.14f;
                            g = g * 0.86f + cc.G * 0.14f;
                            b = b * 0.86f + cc.B * 0.14f;

                            // Border on the cell edges facing other owners (not facing the sea)
                            bool edge =
                                (lx == 0 && IsForeignLand(cx - 1, cy, owner)) ||
                                (lx == s - 1 && IsForeignLand(cx + 1, cy, owner)) ||
                                (ly == 0 && IsForeignLand(cx, cy - 1, owner)) ||
                                (ly == s - 1 && IsForeignLand(cx, cy + 1, owner));
                            if (edge)
                            {
                                r = r * 0.15f + cc.R * 0.85f;
                                g = g * 0.15f + cc.G * 0.85f;
                                b = b * 0.15f + cc.B * 0.85f;
                            }
                        }
                    }

                    if (roads && _cellRoad[cellIdx] && IsRoadTexel(cx, cy, lx, ly, mid))
                    {
                        r = r * 0.3f + 118 * 0.7f;
                        g = g * 0.3f + 96 * 0.7f;
                        b = b * 0.3f + 66 * 0.7f;
                    }

                    if (terrainLike)
                    {
                        // Dark rim along the shore gives crisp coastlines
                        if (mixed && field < 0.012f)
                        {
                            r *= 0.72f; g *= 0.72f; b *= 0.72f;
                        }
                        float grain = 1f + noise * 0.05f;
                        r *= grain; g *= grain; b *= grain;
                    }
                }
                else if (terrainLike)
                {
                    // Surf: light water close to the shore
                    if (mixed && field > -0.03f)
                    {
                        float t = (1f + field / 0.03f) * 0.5f;
                        r += (170 - r) * t; g += (215 - g) * t; b += (230 - b) * t;
                    }
                    float grain = 1f + noise * 0.025f;
                    r *= grain; g *= grain; b *= grain;
                }

                _detailColors[py * dw + px] = new Color(
                    (int)Math.Clamp(r, 0f, 255f),
                    (int)Math.Clamp(g, 0f, 255f),
                    (int)Math.Clamp(b, 0f, 255f));
            }
        });
    }

    private void Accumulate(int idx, float weight, bool sameClass, ref float r, ref float g, ref float b, ref float wsum)
    {
        if (!sameClass || weight <= 0) return;
        var c = _terrainColors[idx];
        r += c.R * weight;
        g += c.G * weight;
        b += c.B * weight;
        wsum += weight;
    }

    private bool IsForeignLand(int x, int y, int owner)
    {
        if (y < 0 || y >= _map.Height) return false;
        x = (x + _map.Width) % _map.Width;
        int idx = y * _map.Width + x;
        if (_cellField[idx] < 0) return false; // coastline: no border line
        return _civOwnerMap[idx] != owner;
    }

    private bool HasRoad(int x, int y)
    {
        if (y < 0 || y >= _map.Height) return false;
        x = (x + _map.Width) % _map.Width;
        return _cellRoad[y * _map.Width + x];
    }

    /// <summary>Road texels form thin lines joining the centres of neighbouring road cells.</summary>
    private bool IsRoadTexel(int cx, int cy, int lx, int ly, int mid)
    {
        if (lx == mid && ly == mid) return true;
        if (ly == mid && ((lx > mid && HasRoad(cx + 1, cy)) || (lx < mid && HasRoad(cx - 1, cy)))) return true;
        if (lx == mid && ((ly > mid && HasRoad(cx, cy + 1)) || (ly < mid && HasRoad(cx, cy - 1)))) return true;
        int dx = lx - mid, dy = ly - mid;
        if (dx == dy && dx != 0 && HasRoad(cx + Math.Sign(dx), cy + Math.Sign(dy))) return true;
        if (dx == -dy && dx != 0 && HasRoad(cx + Math.Sign(dx), cy + Math.Sign(dy))) return true;
        return false;
    }

    private static float[] BuildDetailNoise(int w, int h)
    {
        var noise = new float[Math.Max(1, w * h)];
        const int grid = 3;
        int gw = w / grid + 2, gh = h / grid + 2;
        var lattice = new float[gw * gh];
        for (int i = 0; i < lattice.Length; i++)
            lattice[i] = ((Hash(i % gw, i / gw, 4242) & 1023) / 1023f) * 2f - 1f;

        for (int y = 0; y < h; y++)
        {
            float gy = (float)y / grid;
            int y0 = (int)gy;
            float ty = gy - y0;
            ty = ty * ty * (3 - 2 * ty);
            for (int x = 0; x < w; x++)
            {
                float gx = (float)x / grid;
                int x0 = (int)gx;
                float tx = gx - x0;
                tx = tx * tx * (3 - 2 * tx);
                float a = lattice[y0 * gw + x0], b = lattice[y0 * gw + x0 + 1];
                float c = lattice[(y0 + 1) * gw + x0], d = lattice[(y0 + 1) * gw + x0 + 1];
                float smooth = a + (b - a) * tx + (c - a) * ty + (a - b - c + d) * tx * ty;
                float fine = ((Hash(x, y, 777) & 255) / 255f) * 2f - 1f;
                noise[y * w + x] = Math.Clamp(smooth * 0.75f + fine * 0.25f, -1f, 1f);
            }
        }
        return noise;
    }

    /// <summary>Tileable monochrome noise: dark and light specks around transparent.</summary>
    private static Texture2D BuildGrain(GraphicsDevice device, int size)
    {
        var data = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float n = ((Hash(x, y, 999) & 1023) / 1023f) * 2f - 1f;
                // Premultiplied: dark specks darken, light specks brighten
                data[y * size + x] = n < 0 ? new Color(0, 0, 0) * (-n) : new Color(255, 255, 255) * (n * 0.6f);
            }
        }
        var tex = new Texture2D(device, size, size);
        tex.SetData(data);
        return tex;
    }

    private static Texture2D BuildStarfield(GraphicsDevice device, int size)
    {
        var data = new Color[size * size];
        for (int i = 0; i < data.Length; i++) data[i] = Color.Transparent;
        for (int i = 0; i < 140; i++)
        {
            int h = Hash(i, 91, 1337);
            int x = (h & 0x7fffffff) % size;
            int y = ((h >> 8) & 0x7fffffff) % size;
            int bright = 90 + ((h >> 3) & 127);
            float tint = ((h >> 12) & 31) / 31f;
            var c = new Color((int)(bright * (0.85f + tint * 0.15f)), bright, (int)(bright * (1.1f - tint * 0.2f)));
            data[y * size + x] = c;
            if (((h >> 20) & 15) == 0)
            {
                // A few brighter stars with a soft halo
                var halo = c * 0.35f;
                if (x + 1 < size) data[y * size + x + 1] = halo;
                if (x > 0) data[y * size + x - 1] = halo;
                if (y + 1 < size) data[(y + 1) * size + x] = halo;
                if (y > 0) data[(y - 1) * size + x] = halo;
            }
        }
        var tex = new Texture2D(device, size, size);
        tex.SetData(data);
        return tex;
    }

    // ------------------------------------------------------------------
    // Day / night
    // ------------------------------------------------------------------

    private float ComputeLighting(int x, int y)
    {
        float longitude = (float)x / _map.Width;
        float latitude = ((float)y / _map.Height - 0.5f) * 2.0f;
        float sunLongitude = DayNightTime / 24.0f;
        float axialTilt = 23.5f * (MathF.PI / 180f);
        float solarDeclination = MathF.Sin(DayNightTime * MathF.PI / 12f) * axialTilt;
        float latitudeInRadians = latitude * MathF.PI / 2f;

        float hourAngle = (longitude - sunLongitude) * 2f * MathF.PI;
        if (hourAngle > MathF.PI) hourAngle -= 2f * MathF.PI;
        if (hourAngle < -MathF.PI) hourAngle += 2f * MathF.PI;

        float solarElevation = MathF.Sin(latitudeInRadians) * MathF.Sin(solarDeclination) +
                               MathF.Cos(latitudeInRadians) * MathF.Cos(solarDeclination) * MathF.Cos(hourAngle);

        // Soft terminator: fully lit above +0.15, fully dark below -0.2
        float t = Math.Clamp((solarElevation + 0.2f) / 0.35f, 0f, 1f);
        return t * t * (3f - 2f * t);
    }

    private void UpdateNightOverlay()
    {
        int w = _map.Width, h = _map.Height;
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int i = y * w + x;
                float light = ComputeLighting(x, y);
                float dark = 1f - light;
                _cellDarkness[i] = dark;

                var cell = _map.Cells[x, y];
                bool lit = ShowCityLights && cell.LifeType == LifeForm.Civilization && cell.Biomass > 0.3f;
                if (lit && dark > 0.3f)
                {
                    // Warm glow of settlements on the night side (premultiplied colour)
                    float a = Math.Min(0.75f, dark * 0.55f + cell.Biomass * 0.2f);
                    _nightColors[i] = new Color(255, 196, 96) * a;
                }
                else
                {
                    float a = dark * 0.78f;
                    // Dusk band tinted slightly warm, deep night bluish
                    float dusk = dark * light * 4f;
                    var nightCol = new Color((int)(6 + 60 * dusk), (int)(10 + 20 * dusk), (int)(34 - 10 * dusk));
                    _nightColors[i] = new Color(nightCol.R, nightCol.G, nightCol.B) * a;
                }
            }
        }
        _nightTexture.SetData(_nightColors);
    }

    /// <summary>Darkness (0 = daylight, 1 = night) at a cell, when day/night is shown.</summary>
    public float GetDarknessAt(int x, int y)
    {
        if (!ShowDayNight) return 0f;
        x = ((x % _map.Width) + _map.Width) % _map.Width;
        y = Math.Clamp(y, 0, _map.Height - 1);
        return _cellDarkness[y * _map.Width + x];
    }

    // ------------------------------------------------------------------
    // Drawing
    // ------------------------------------------------------------------

    /// <summary>
    /// Chooses a zoom level that makes the whole map fill the available area.
    /// </summary>
    public void FitToView(int areaWidth, int areaHeight)
    {
        float mapW = _map.Width * CellSize;
        float mapH = _map.Height * CellSize;
        if (mapW <= 0 || mapH <= 0 || areaWidth <= 0 || areaHeight <= 0) return;
        float fit = Math.Min(areaWidth / mapW, areaHeight / mapH);
        ZoomLevel = Math.Clamp(fit, 0.5f, 4.0f);
        CameraX = 0;
        CameraY = 0;
    }

    public void Draw(SpriteBatch spriteBatch, int offsetX, int offsetY)
    {
        var viewport = _graphicsDevice.Viewport;

        // Fit the map to the viewport the first time it is shown
        if (!_initialFitDone)
        {
            _initialFitDone = true;
            FitToView(viewport.Width - offsetX, viewport.Height - offsetY);
        }

        // Calculate zoomed size
        int zoomedWidth = (int)(_map.Width * CellSize * ZoomLevel);
        int zoomedHeight = (int)(_map.Height * CellSize * ZoomLevel);

        // Apply camera offset
        int camX = offsetX - (int)CameraX;
        int camY = offsetY - (int)CameraY;
        var mapRect = new Rectangle(camX, camY, zoomedWidth, zoomedHeight);

        // Space backdrop behind the map
        var area = new Rectangle(offsetX, offsetY, Math.Max(0, viewport.Width - offsetX), Math.Max(0, viewport.Height - offsetY));
        spriteBatch.Draw(_pixelTexture, area, new Color(6, 9, 16));
        spriteBatch.End();
        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointWrap);
        spriteBatch.Draw(_starfield, area, new Rectangle(area.X, area.Y, area.Width, area.Height), Color.White);
        spriteBatch.End();

        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp);

        // Soft glow/shadow around the planet map
        for (int i = 1; i <= 4; i++)
        {
            var glow = new Rectangle(mapRect.X - i * 2, mapRect.Y - i * 2, mapRect.Width + i * 4, mapRect.Height + i * 4);
            spriteBatch.Draw(_pixelTexture, glow, new Color(40, 90, 160) * (0.07f / i));
        }

        spriteBatch.Draw(_detailTexture ?? _terrainTexture, mapRect, Color.White);

        // Fine surface grain when zoomed in, so close-ups don't look blurry
        float cellPx = CellSize * ZoomLevel;
        float grainAlpha = Math.Clamp((cellPx - 7f) / 10f, 0f, 1f) * 0.16f;
        if (grainAlpha > 0.005f)
        {
            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearWrap);
            float texel = Math.Max(1.5f, cellPx / 6f);
            var src = new Rectangle((int)(CameraX / texel), (int)(CameraY / texel),
                (int)(mapRect.Width / texel), (int)(mapRect.Height / texel));
            spriteBatch.Draw(_grain, mapRect, src, Color.White * grainAlpha);
            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp);
        }

        if (ShowDayNight)
        {
            spriteBatch.Draw(_nightTexture, mapRect, Color.White);
        }

        spriteBatch.End();
        spriteBatch.Begin(samplerState: SamplerState.PointClamp);
    }

    // ------------------------------------------------------------------
    // Settlements, armies and battles
    // ------------------------------------------------------------------

    private static readonly int[] SettlementPixelSize = { 21, 25, 28, 32 };
    private static readonly string[] SettlementTypeNames = { "Village", "Town", "City", "Metropolis" };

    /// <summary>Screen position of the centre of a cell.</summary>
    public Vector2 CellToScreen(float cellX, float cellY, int offsetX, int offsetY)
    {
        float cs = CellSize * ZoomLevel;
        return new Vector2(offsetX - CameraX + (cellX + 0.5f) * cs, offsetY - CameraY + (cellY + 0.5f) * cs);
    }

    /// <summary>
    /// Draw city icons, labels, armies and battle effects on top of the terrain
    /// </summary>
    public void DrawCityMarkers(SpriteBatch spriteBatch, int offsetX, int offsetY)
    {
        var data = CivData;
        if (data.Cities.Count == 0 && data.Armies.Count == 0 && data.Battles.Count == 0) return;

        var viewport = _graphicsDevice.Viewport;
        var clip = new Rectangle(offsetX, offsetY, viewport.Width - offsetX, viewport.Height - offsetY);
        float cellPx = CellSize * ZoomLevel;
        float iconScale = Math.Clamp(cellPx / 5.5f, 0.7f, 1.7f);
        float time = (float)(DateTime.Now.TimeOfDay.TotalSeconds % 3600);

        spriteBatch.End();
        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp);

        // Trade/war context: which civs are at war (for label colouring)
        var civColors = new Dictionary<int, Color>();
        foreach (var civ in data.Civs) civColors[civ.Id] = GetCivColor(civ.Id);

        // --- Army movement lines (under everything else) ---
        foreach (var army in data.Armies)
        {
            if (!army.State.Contains("March") && !army.State.Contains("Retreat")) continue;
            var from = CellToScreen(army.X, army.Y, offsetX, offsetY);
            var to = CellToScreen(army.TargetX, army.TargetY, offsetX, offsetY);
            if (Vector2.Distance(from, to) > viewport.Width) continue; // wrapped around the map
            Color c = civColors.GetValueOrDefault(army.CivId, Color.White);
            DrawDashedLine(spriteBatch, from, to, c * 0.8f, time);
            DrawArrowHead(spriteBatch, from, to, c);
        }

        // --- Settlements, sorted so big cities are drawn last (on top) ---
        var ordered = new List<CivRenderData.CityInfo>(data.Cities);
        ordered.Sort((a, b) => (a.Type * 2 + (a.IsCapital ? 1 : 0)).CompareTo(b.Type * 2 + (b.IsCapital ? 1 : 0)));

        foreach (var city in ordered)
        {
            var pos = CellToScreen(city.X, city.Y, offsetX, offsetY);
            if (!clip.Contains(pos.ToPoint())) continue;

            int type = Math.Clamp(city.Type, 0, 3);
            int size = (int)(SettlementPixelSize[type] * iconScale);
            Color civColor = civColors.GetValueOrDefault(city.CivId, Color.White);

            // Night lights
            float dark = GetDarknessAt(city.X, city.Y);
            if (dark > 0.2f && ShowCityLights)
            {
                UITheme.DrawGlow(spriteBatch, pos, size * (0.8f + type * 0.25f), new Color(255, 190, 90, 0) * (dark * 0.55f));
            }

            // Ground shadow
            UITheme.DrawGlow(spriteBatch, pos + new Vector2(1, size * 0.28f), size * 0.55f, new Color(0, 0, 0) * 0.45f);

            // City walls: a flattened ring around the base of the settlement
            if (city.HasWalls && _icons != null)
            {
                int ww = (int)(size * 1.2f);
                int wh = (int)(size * 0.62f);
                int baseY = (int)(pos.Y + size * 0.22f);
                // Back half now, front half after the buildings (see below)
                int half = _icons.WallRing.Height / 2;
                spriteBatch.Draw(_icons.WallRing, new Rectangle((int)pos.X - ww / 2, baseY - wh / 2, ww, wh / 2),
                    new Rectangle(0, 0, _icons.WallRing.Width, half), Color.White);
            }

            // Siege: pulsing red ring
            if (city.UnderSiege)
            {
                float pulse = 0.5f + 0.5f * MathF.Sin(time * 6f);
                UITheme.DrawGlow(spriteBatch, pos, size * (0.9f + pulse * 0.3f), new Color(255, 40, 20) * (0.35f + pulse * 0.25f));
            }

            var iconRect = new Rectangle((int)(pos.X - size / 2f), (int)(pos.Y - size * 0.62f), size, size);
            if (_icons != null)
            {
                spriteBatch.Draw(_icons.SettlementBase[type], iconRect, Color.White);
                spriteBatch.Draw(_icons.SettlementMask[type], iconRect, civColor);
            }
            else
            {
                spriteBatch.Draw(_pixelTexture, iconRect, civColor);
            }

            if (city.HasWalls && _icons != null)
            {
                int ww = (int)(size * 1.2f);
                int wh = (int)(size * 0.62f);
                int baseY = (int)(pos.Y + size * 0.22f);
                int half = _icons.WallRing.Height / 2;
                spriteBatch.Draw(_icons.WallRing, new Rectangle((int)pos.X - ww / 2, baseY - wh / 2 + wh / 2, ww, wh - wh / 2),
                    new Rectangle(0, half, _icons.WallRing.Width, _icons.WallRing.Height - half), Color.White);
            }

            if (_icons != null)
            {
                if (city.IsCapital)
                {
                    int cs = Math.Max(10, (int)(size * 0.5f));
                    spriteBatch.Draw(_icons.Crown, new Rectangle((int)pos.X - cs / 2, iconRect.Y - cs + 3, cs, cs), Color.White);
                }
                if (city.UnderSiege)
                {
                    int fs = Math.Max(10, (int)(size * 0.5f));
                    float flicker = 0.85f + 0.15f * MathF.Sin(time * 11f + city.X);
                    spriteBatch.Draw(_icons.Fire, new Rectangle(iconRect.Right - fs / 2, iconRect.Y, fs, fs), Color.White * flicker);
                }
                if (city.Starving)
                {
                    int hs = Math.Max(10, (int)(size * 0.45f));
                    spriteBatch.Draw(_icons.Hunger, new Rectangle(iconRect.X - hs / 2, iconRect.Y, hs, hs), Color.White);
                }
            }
        }

        // --- Armies ---
        foreach (var army in data.Armies)
        {
            var pos = CellToScreen(army.X, army.Y, offsetX, offsetY);
            if (!clip.Contains(pos.ToPoint())) continue;
            Color c = civColors.GetValueOrDefault(army.CivId, Color.White);
            float strength = MathF.Log10(Math.Max(10, army.Soldiers));
            int size = (int)((12 + Math.Clamp(strength - 1, 0, 4) * 3f) * iconScale);
            float alpha = army.State.Contains("Retreat") ? 0.6f : 1f;
            float bob = army.State.Contains("March") ? MathF.Sin(time * 5f + army.Id) * 1.2f : 0f;

            UITheme.DrawGlow(spriteBatch, pos + new Vector2(0, size * 0.35f), size * 0.5f, Color.Black * 0.45f);
            var rect = new Rectangle((int)(pos.X - size * 0.35f), (int)(pos.Y - size * 0.8f + bob), size, size);
            if (_icons != null)
            {
                spriteBatch.Draw(_icons.ArmyBase, rect, Color.White * alpha);
                spriteBatch.Draw(_icons.ArmyMask, rect, c * alpha);
                if (army.State.Contains("Besieg"))
                {
                    int ss = (int)(size * 0.6f);
                    spriteBatch.Draw(_icons.CrossedSwords, new Rectangle(rect.Right - ss / 2, rect.Bottom - ss, ss, ss), Color.White);
                }
            }
            else
            {
                spriteBatch.Draw(_pixelTexture, rect, c * alpha);
            }
        }

        // --- Battles: flash + crossed swords that fade out ---
        foreach (var battle in data.Battles)
        {
            if (battle.Age > 3.5f) continue;
            var pos = CellToScreen(battle.X, battle.Y, offsetX, offsetY);
            if (!clip.Contains(pos.ToPoint())) continue;
            float t = Math.Clamp(battle.Age / 3f, 0f, 1f);
            float fade = 1f - t;
            float radius = (10 + t * 18) * iconScale;
            UITheme.DrawGlow(spriteBatch, pos, radius * 1.6f, new Color(255, 120, 40, 0) * (fade * 0.8f));
            UITheme.DrawGlow(spriteBatch, pos, radius * 0.7f, new Color(255, 240, 200, 0) * (fade * fade));
            if (_icons != null)
            {
                float pop = t < 0.1f ? t / 0.1f : 1f;
                int ss = (int)(22 * iconScale * (0.6f + 0.4f * pop));
                spriteBatch.Draw(_icons.CrossedSwords, new Rectangle((int)pos.X - ss / 2, (int)pos.Y - ss / 2, ss, ss), Color.White * Math.Min(1f, fade * 1.5f));
            }
        }

        spriteBatch.End();
        spriteBatch.Begin(samplerState: SamplerState.PointClamp);

        // --- Labels (point sampling keeps text crisp) ---
        if (ShowSettlementLabels && UITheme.Font != null)
        {
            DrawSettlementLabels(spriteBatch, ordered, civColors, offsetX, offsetY, clip, cellPx, iconScale);
        }
    }

    private void DrawSettlementLabels(SpriteBatch spriteBatch, List<CivRenderData.CityInfo> ordered,
        Dictionary<int, Color> civColors, int offsetX, int offsetY, Rectangle clip, float cellPx, float iconScale)
    {
        var placed = new List<Rectangle>();
        // Most important first so they win label collisions
        for (int i = ordered.Count - 1; i >= 0; i--)
        {
            var city = ordered[i];
            int type = Math.Clamp(city.Type, 0, 3);
            float minCellPx = city.IsCapital ? 3.5f : type switch { 3 => 3.5f, 2 => 5f, 1 => 8f, _ => 11f };
            if (cellPx < minCellPx || string.IsNullOrEmpty(city.Name)) continue;

            var pos = CellToScreen(city.X, city.Y, offsetX, offsetY);
            if (!clip.Contains(pos.ToPoint())) continue;

            float fontSize = type >= 2 || city.IsCapital ? 13f : 12f;
            var textSize = UITheme.Measure(city.Name, fontSize);
            int size = (int)(SettlementPixelSize[type] * iconScale);
            var labelPos = new Vector2(pos.X - textSize.X / 2f, pos.Y + size * 0.42f);
            var rect = new Rectangle((int)labelPos.X - 3, (int)labelPos.Y, (int)textSize.X + 6, (int)textSize.Y);

            bool overlaps = false;
            foreach (var r in placed)
                if (r.Intersects(rect)) { overlaps = true; break; }
            if (overlaps) continue;
            placed.Add(rect);

            Color civColor = civColors.GetValueOrDefault(city.CivId, Color.White);
            // Pill background tinted with the owner's colour
            spriteBatch.Draw(_pixelTexture, rect, new Color(10, 12, 18) * 0.55f);
            spriteBatch.Draw(_pixelTexture, new Rectangle(rect.X, rect.Bottom - 2, rect.Width, 2), civColor * 0.9f);
            Color textColor = city.IsCapital ? UITheme.Gold : Color.White;
            UITheme.DrawTextOutlined(spriteBatch, city.Name, labelPos, textColor, fontSize);

            if (city.UnderSiege && city.SiegeProgress > 0)
            {
                var bar = new Rectangle(rect.X, rect.Bottom + 1, rect.Width, 3);
                spriteBatch.Draw(_pixelTexture, bar, Color.Black * 0.7f);
                spriteBatch.Draw(_pixelTexture, new Rectangle(bar.X, bar.Y, (int)(bar.Width * Math.Clamp(city.SiegeProgress, 0f, 1f)), bar.Height), new Color(255, 70, 40));
            }
        }
    }

    /// <summary>
    /// Returns the settlement under the given screen position, if any (for tooltips).
    /// </summary>
    public CivRenderData.CityInfo? FindCityAt(Point screen, int offsetX, int offsetY)
    {
        float cellPx = CellSize * ZoomLevel;
        float iconScale = Math.Clamp(cellPx / 5.5f, 0.7f, 1.7f);
        CivRenderData.CityInfo? best = null;
        float bestDist = float.MaxValue;
        foreach (var city in CivData.Cities)
        {
            var pos = CellToScreen(city.X, city.Y, offsetX, offsetY);
            int size = (int)(SettlementPixelSize[Math.Clamp(city.Type, 0, 3)] * iconScale);
            float d = Vector2.Distance(pos - new Vector2(0, size * 0.15f), screen.ToVector2());
            if (d < size * 0.55f && d < bestDist)
            {
                best = city;
                bestDist = d;
            }
        }
        return best;
    }

    public static string GetSettlementTypeName(int type) => SettlementTypeNames[Math.Clamp(type, 0, 3)];

    private void DrawDashedLine(SpriteBatch spriteBatch, Vector2 from, Vector2 to, Color color, float time)
    {
        Vector2 d = to - from;
        float len = d.Length();
        if (len < 2) return;
        Vector2 dir = d / len;
        const float dash = 5f, gap = 4f;
        float phase = (time * 12f) % (dash + gap);
        for (float t = -phase; t < len; t += dash + gap)
        {
            float a = Math.Max(0, t), b = Math.Min(len, t + dash);
            if (b <= a) continue;
            UITheme.DrawLine(spriteBatch, from + dir * a, from + dir * b, Color.Black * 0.5f, 3f);
            UITheme.DrawLine(spriteBatch, from + dir * a, from + dir * b, color, 1.5f);
        }
    }

    private void DrawArrowHead(SpriteBatch spriteBatch, Vector2 from, Vector2 to, Color color)
    {
        Vector2 d = to - from;
        float len = d.Length();
        if (len < 6) return;
        Vector2 dir = d / len;
        Vector2 n = new Vector2(-dir.Y, dir.X);
        Vector2 tip = to;
        UITheme.DrawLine(spriteBatch, tip, tip - dir * 7 + n * 4, color, 2f);
        UITheme.DrawLine(spriteBatch, tip, tip - dir * 7 - n * 4, color, 2f);
    }

    // Simple hash function for procedural variation
    private static int Hash(int x, int y, int seed = 0)
    {
        int h = x * 374761393 + y * 668265263 + seed;
        h = (h ^ (h >> 13)) * 1274126177;
        return h ^ (h >> 16);
    }

    private Color GetTerrainColor(TerrainCell cell)
    {
        int x = cell.X;
        int y = cell.Y;

        // Get procedural variation for this cell
        float variation = (Hash(x, y, 12345) & 255) / 255.0f; // 0-1
        float variation2 = (Hash(x, y, 67890) & 255) / 255.0f;

        // Base terrain colors with procedural variation
        Color baseColor = cell.GetTerrainType() switch
        {
            TerrainType.DeepOcean => GetDeepOceanColor(cell, variation),
            TerrainType.ShallowWater => GetShallowWaterColor(cell, variation),
            TerrainType.Beach => GetBeachColor(cell, x, y, variation, variation2),
            TerrainType.Plains => GetPlainsColor(cell, variation),
            TerrainType.Grassland => GetGrasslandColor(cell, x, y, variation),
            TerrainType.Forest => GetForestColor(cell, x, y, variation, variation2),
            TerrainType.Desert => GetDesertColor(cell, x, y, variation, variation2),
            TerrainType.Mountain => GetMountainColor(cell, x, y, variation),
            TerrainType.Ice => GetIceColor(cell, variation),
            TerrainType.Tundra => GetTundraColor(cell, variation),
            _ => Color.Gray
        };

        // If Clean mode, return base color immediately (skipping overlays)
        if (Mode == RenderMode.TerrainClean)
            return baseColor;

        // Volcanic Rock Overlay - darkens terrain
        var geo = cell.GetGeology();
        if (geo.VolcanicRock > 0.1f && cell.IsLand && !cell.IsIce)
        {
            // Blend with dark basalt color
            Color basaltColor = new Color(40, 35, 35);
            baseColor = Color.Lerp(baseColor, basaltColor, Math.Min(geo.VolcanicRock, 0.85f));
        }

        // Add life overlay with improved colors
        if (cell.LifeType != LifeForm.None && cell.Biomass > 0.1f)
        {
            Color lifeColor = cell.LifeType switch
            {
                LifeForm.Bacteria => new Color(120, 120, 60),           // Yellowish microbes
                LifeForm.Algae => new Color(60, 170, 120),              // Teal-green algae
                LifeForm.PlantLife => new Color(70, 200, 70),           // Bright green plants
                LifeForm.SimpleAnimals => new Color(160, 110, 60),      // Brown creatures
                LifeForm.Fish => new Color(120, 140, 220),              // Blue-ish aquatic
                LifeForm.Amphibians => new Color(130, 180, 90),         // Green amphibians
                LifeForm.Reptiles => new Color(150, 150, 70),           // Olive reptiles
                LifeForm.Dinosaurs => new Color(200, 90, 50),           // Orange-red dinosaurs
                LifeForm.MarineDinosaurs => new Color(110, 110, 200),   // Deep blue marine
                LifeForm.Pterosaurs => new Color(180, 160, 120),        // Tan flying reptiles
                LifeForm.Mammals => new Color(170, 130, 100),           // Brown mammals
                LifeForm.Birds => new Color(160, 200, 160),             // Light green birds
                LifeForm.ComplexAnimals => new Color(190, 130, 70),     // Golden-brown
                LifeForm.Intelligence => new Color(220, 170, 120),      // Warm intelligent life
                LifeForm.Civilization => new Color(255, 220, 120),      // Golden civilization
                _ => Color.Transparent
            };

            // Blend life color with terrain - more prominent for civilizations
            float blend = cell.LifeType == LifeForm.Civilization ?
                Math.Min(cell.Biomass * 0.7f, 0.6f) :
                cell.Biomass * 0.5f;
            // Keep the sea readable: marine life only tints the water slightly
            if (cell.IsWater) blend *= 0.3f;
            baseColor = Color.Lerp(baseColor, lifeColor, blend);
        }

        // Active Lava Overlay (on top of life)
        if (geo.IsVolcano)
        {
            float activity = geo.VolcanicActivity + geo.MagmaPressure;
            if (activity > 0.2f)
            {
                Color lavaColor = new Color(255, 69, 0); // OrangeRed
                if (activity > 1.0f) lavaColor = Color.Yellow; // Hotter

                float lavaBlend = Math.Clamp(activity * 0.5f, 0f, 1f);
                baseColor = Color.Lerp(baseColor, lavaColor, lavaBlend);
            }
        }

        // DISASTER EFFECTS OVERLAY
        // Only apply scorching/burning effects to LAND cells
        // Water cells are not scorched - water fills craters and protects from burning
        bool isWaterCell = cell.IsWater;

        // Impact craters - dark scorched earth (ONLY on land)
        if (geo.IsInCrater && !isWaterCell)
        {
            Color craterColor = new Color(30, 25, 25);  // Dark scorched
            float craterBlend = Math.Min(geo.CraterDepth * 2, 0.8f);
            baseColor = Color.Lerp(baseColor, craterColor, craterBlend);
        }

        // Blast damage - burned/charred terrain (ONLY on land)
        if (geo.BlastDamage > 0.1f && !isWaterCell)
        {
            Color blastColor = geo.BlastDamage > 0.7f
                ? new Color(20, 20, 20)     // Severely burned - almost black
                : new Color(60, 50, 40);     // Moderately burned - dark brown
            baseColor = Color.Lerp(baseColor, blastColor, geo.BlastDamage * 0.7f);
        }

        // Impact scorching - orange/red burn marks (ONLY on land)
        if (geo.ImpactScorching > 0.1f && !isWaterCell)
        {
            Color scorchColor = geo.ImpactScorching > 0.8f
                ? new Color(80, 30, 10)      // Fresh scorch - dark red
                : new Color(100, 70, 40);     // Old scorch - brown
            baseColor = Color.Lerp(baseColor, scorchColor, geo.ImpactScorching * 0.5f);
        }

        // Radioactive contamination - sickly green/yellow glow
        // This CAN affect water (contaminated water turns green)
        if (geo.RadioactiveContamination > 0.1f)
        {
            Color radColor = geo.RadioactiveContamination > 0.7f
                ? new Color(80, 120, 40)     // High radiation - yellow-green
                : new Color(100, 110, 80);    // Low radiation - dull greenish
            // Less visible on water (diluted)
            float radBlend = isWaterCell
                ? geo.RadioactiveContamination * 0.2f
                : geo.RadioactiveContamination * 0.4f;
            baseColor = Color.Lerp(baseColor, radColor, radBlend);
        }

        return baseColor;
    }

    private Color GetTemperatureColor(TerrainCell cell)
    {
        // Use a more visually appealing and informative color gradient
        float temp = cell.Temperature;
        if (temp < -30) return new Color(150, 150, 255); // Deep Blue/Purple for extreme cold
        if (temp < -10) return Color.Lerp(new Color(150, 150, 255), Color.Cyan, (temp + 30) / 20f);
        if (temp < 0) return Color.Lerp(Color.Cyan, Color.LightBlue, (temp + 10) / 10f);
        if (temp < 10) return Color.Lerp(Color.LightBlue, Color.LightGreen, temp / 10f);
        if (temp < 20) return Color.Lerp(Color.LightGreen, Color.Yellow, (temp - 10) / 10f);
        if (temp < 30) return Color.Lerp(Color.Yellow, Color.Orange, (temp - 20) / 10f);
        if (temp < 40) return Color.Lerp(Color.Orange, Color.Red, (temp - 30) / 10f);
        return new Color(255, 50, 50); // Bright Red for extreme heat
    }

    private Color GetRainfallColor(TerrainCell cell)
    {
        // Use a more intuitive color gradient
        float rainfall = cell.Rainfall;
        if (rainfall < 0.1f) return Color.Lerp(Color.SandyBrown, Color.LightGreen, rainfall / 0.1f);
        if (rainfall < 0.5f) return Color.Lerp(Color.LightGreen, Color.Green, (rainfall - 0.1f) / 0.4f);
        if (rainfall < 0.8f) return Color.Lerp(Color.Green, Color.Blue, (rainfall - 0.5f) / 0.3f);
        return Color.DarkBlue;
    }

    private Color GetLifeColor(TerrainCell cell)
    {
        if (cell.LifeType == LifeForm.None)
            return new Color(139, 69, 19); // Barren brown

        // Use a gradient from brown to green based on biomass
        return Color.Lerp(new Color(139, 69, 19), new Color(0, 100, 0), cell.Biomass);
    }

    private Color GetOxygenColor(TerrainCell cell)
    {
        float normalized = Math.Clamp(cell.Oxygen / 30.0f, 0, 1);
        return Color.Lerp(Color.Black, new Color(100, 200, 255), normalized);
    }

    private Color GetCO2Color(TerrainCell cell)
    {
        // Match legend gradient: dark blue (low CO2) to yellow (high CO2)
        float normalized = Math.Clamp(cell.CO2 / 10.0f, 0, 1);
        return Color.Lerp(new Color(50, 50, 100), new Color(255, 200, 50), normalized);
    }

    private Color GetElevationColor(TerrainCell cell)
    {
        float normalized = (cell.Elevation + 1) / 2.0f; // Map -1 to 1 -> 0 to 1
        return Color.Lerp(Color.Black, Color.White, normalized);
    }

    private Color GetGeologicalColor(TerrainCell cell)
    {
        var geo = cell.GetGeology();

        // Show dominant rock type clearly
        Color rockColor;

        // Determine dominant rock type (max of the three)
        float maxVolcanic = geo.VolcanicRock;
        float maxSedimentary = geo.SedimentaryRock;
        float maxCrystalline = geo.CrystallineRock;

        if (maxVolcanic >= maxSedimentary && maxVolcanic >= maxCrystalline)
        {
            // Volcanic (basalt) - dark gray/black
            rockColor = Color.Lerp(new Color(60, 60, 60), new Color(90, 70, 60), maxVolcanic);
        }
        else if (maxSedimentary > maxCrystalline)
        {
            // Sedimentary (sandstone, limestone) - tan/beige
            rockColor = Color.Lerp(new Color(140, 120, 90), new Color(200, 180, 140), maxSedimentary);
        }
        else
        {
            // Crystalline (granite) - light gray
            rockColor = Color.Lerp(new Color(100, 100, 100), new Color(160, 160, 160), maxCrystalline);
        }

        return rockColor;
    }

    private Color GetTectonicPlateColor(TerrainCell cell)
    {
        var geo = cell.GetGeology();

        // Different color per plate
        Color[] plateColors = new[]
        {
            new Color(255, 100, 100),
            new Color(100, 255, 100),
            new Color(100, 100, 255),
            new Color(255, 255, 100),
            new Color(255, 100, 255),
            new Color(100, 255, 255),
            new Color(255, 200, 100),
            new Color(200, 100, 255)
        };

        Color baseColor = plateColors[geo.PlateId % plateColors.Length];

        // Highlight boundaries
        if (geo.BoundaryType == PlateBoundaryType.Convergent)
        {
            baseColor = Color.Lerp(baseColor, Color.Red, 0.5f);
        }
        else if (geo.BoundaryType == PlateBoundaryType.Divergent)
        {
            baseColor = Color.Lerp(baseColor, Color.Yellow, 0.5f);
        }
        else if (geo.BoundaryType == PlateBoundaryType.Transform)
        {
            baseColor = Color.Lerp(baseColor, Color.Orange, 0.5f);
        }

        return baseColor;
    }

    private Color GetVolcanoColor(TerrainCell cell)
    {
        var geo = cell.GetGeology();

        // Base color: darker background
        Color baseColor = cell.IsWater ? new Color(20, 40, 70) : new Color(40, 40, 40);

        // Highlight volcanoes with activity level
        if (geo.IsVolcano)
        {
            float activity = Math.Clamp(geo.VolcanicActivity + geo.MagmaPressure, 0, 2);

            if (activity > 1.5f)
            {
                // Erupting - bright red/orange
                baseColor = Color.Lerp(new Color(255, 100, 0), new Color(255, 255, 0), (activity - 1.5f) * 2);
            }
            else if (activity > 0.5f)
            {
                // Active - orange to red
                baseColor = Color.Lerp(new Color(150, 50, 0), new Color(255, 100, 0), (activity - 0.5f));
            }
            else
            {
                // Dormant - dark red
                baseColor = Color.Lerp(new Color(60, 30, 30), new Color(120, 40, 0), activity * 2);
            }
        }
        // Show volcanic rock areas (even if not currently a volcano)
        else if (geo.VolcanicRock > 0.5f)
        {
            baseColor = Color.Lerp(baseColor, new Color(80, 60, 60), geo.VolcanicRock);
        }

        return baseColor;
    }

    private Color GetCloudsColor(TerrainCell cell)
    {
        var met = cell.GetMeteorology();

        // Satellite view: show terrain underneath with cloud overlay
        Color terrainColor = GetTerrainColor(cell);

        // Darken terrain slightly for satellite effect
        terrainColor = Color.Lerp(terrainColor, Color.Black, 0.2f);

        // Cloud coverage overlay (like satellite imagery)
        // Clouds should be white - if they're showing other colors, it's a bug
        if (met.CloudCover > 0.05f)
        {
            // Pure white clouds, intensity based on coverage
            Color cloudColor = new Color(255, 255, 255);

            // Blend clouds over terrain based on coverage
            float cloudAlpha = Math.Clamp(met.CloudCover, 0, 0.95f);
            return Color.Lerp(terrainColor, cloudColor, cloudAlpha);
        }

        return terrainColor;
    }

    private Color GetWindColor(TerrainCell cell)
    {
        var met = cell.GetMeteorology();

        // Wind speed magnitude
        float windSpeed = MathF.Sqrt(met.WindSpeedX * met.WindSpeedX + met.WindSpeedY * met.WindSpeedY);

        // Normalize wind speed (typical range: 0-15, extreme can go higher)
        // Map to 0-1 range for color interpolation
        float normalized = Math.Clamp(windSpeed / 15.0f, 0, 1);

        // Gradient from green (calm) to red (extreme), matching legend
        return Color.Lerp(new Color(200, 255, 200), new Color(255, 50, 50), normalized);
    }

    private Color GetPressureColor(TerrainCell cell)
    {
        var met = cell.GetMeteorology();

        // Air pressure in millibars: typical range 950-1050, standard = 1013.25
        // Normalize to 0-1: (pressure - 950) / 100
        float normalized = Math.Clamp((met.AirPressure - 950f) / 100f, 0, 1);

        // Gradient: Blue (low pressure/storms) to Red (high pressure/fair weather)
        return Color.Lerp(new Color(50, 100, 255), new Color(255, 50, 50), normalized);
    }

    private Color GetStormsColor(TerrainCell cell)
    {
        var met = cell.GetMeteorology();

        // Calculate storm intensity from precipitation + wind speed
        float windSpeed = MathF.Sqrt(met.WindSpeedX * met.WindSpeedX + met.WindSpeedY * met.WindSpeedY);

        // Storm intensity combines precipitation (0-1) and high wind (normalized)
        float stormIntensity = Math.Clamp(met.Precipitation * 0.5f + (windSpeed / 15.0f) * 0.5f, 0, 1);

        // Gradient from clear (light blue) to severe storm (purple), matching legend
        return Color.Lerp(new Color(150, 200, 255), new Color(100, 0, 100), stormIntensity);
    }

    private Color GetBiomeColor(TerrainCell cell)
    {
        // Water cells
        if (cell.IsWater)
        {
            if (cell.Elevation < -0.5f)
                return new Color(0, 50, 120); // Deep ocean
            return new Color(20, 100, 180); // Shallow water
        }

        // Get biome for land cells
        var biomeData = cell.GetBiomeData();
        Color color = biomeData.CurrentBiome switch
        {
            // Frozen biomes
            Biome.Glacier => new Color(240, 250, 255),          // White/light blue
            Biome.AlpineTundra => new Color(200, 210, 220),     // Gray-white
            Biome.Tundra => new Color(180, 190, 160),           // Gray-green

            // Forest biomes
            Biome.TropicalRainforest => new Color(10, 100, 20), // Dark green
            Biome.TemperateForest => new Color(34, 139, 34),    // Forest green
            Biome.BorealForest => new Color(20, 80, 40),        // Dark green

            // Grassland biomes
            Biome.Savanna => new Color(200, 180, 100),          // Tan/yellow
            Biome.Grassland => new Color(100, 160, 80),         // Light green
            Biome.Shrubland => new Color(140, 140, 80),         // Olive

            // Arid biomes
            Biome.Desert => new Color(230, 200, 140),           // Sand color

            // Other
            Biome.Mountain => new Color(140, 130, 120),         // Gray
            Biome.Wetland => new Color(60, 120, 90),            // Swamp green

            _ => new Color(180, 160, 100)                        // Default
        };

        // Darken based on biomass for more detail
        if (cell.Biomass > 0.5f && biomeData.CurrentBiome != Biome.Desert && biomeData.CurrentBiome != Biome.Glacier)
        {
            float darken = Math.Min(cell.Biomass - 0.5f, 0.3f);
            color = Color.Lerp(color, new Color(0, 40, 0), darken);
        }

        return color;
    }

    private Color GetResourcesColor(TerrainCell cell)
    {
        var resources = cell.GetResources();
        if (resources.Count == 0)
        {
            return cell.IsWater ? new Color(30, 60, 100) : new Color(60, 50, 40);
        }

        ResourceDeposit? dominantResource = resources.OrderByDescending(r => r.Amount * r.Concentration).FirstOrDefault();
        if (dominantResource == null)
        {
            return new Color(60, 50, 40);
        }

        Color resourceColor = ResourceExtensions.GetResourceColor(dominantResource.Type);
        float intensity = Math.Clamp(dominantResource.Amount * dominantResource.Concentration, 0.3f, 1.0f);
        Color baseColor = cell.IsWater ? new Color(30, 60, 100) : new Color(60, 50, 40);
        return Color.Lerp(baseColor, resourceColor, intensity);
    }

    private Color GetAlbedoColor(TerrainCell cell)
    {
        // Calculate albedo (surface reflectivity) - same logic as ClimateSimulator.CalculateAlbedo()
        float albedo = 0.0f;

        // Ice and snow (highest albedo - 85%)
        if (cell.IsIce)
        {
            albedo = 0.85f;
        }
        // Water (low albedo)
        else if (cell.IsWater)
        {
            if (cell.Elevation < -0.3f)  // Fixed: was -0.5f, now matches ClimateSimulator
                albedo = 0.06f; // Deep ocean (very dark)
            else
                albedo = 0.08f; // Shallow water
        }
        // Desert (medium-high albedo - 35%)
        else if (cell.IsDesert)
        {
            albedo = 0.35f;
        }
        // Forest (low-medium albedo - 17%)
        else if (cell.IsForest)
        {
            albedo = 0.17f;
        }
        // Grassland (medium albedo - 23%)
        else if (cell.Rainfall > 0.3f && cell.IsLand)  // Fixed: was 0.4f, now matches ClimateSimulator
        {
            albedo = 0.23f;
        }
        // Bare rock/mountains (low-medium albedo - 15%)
        else if (cell.Elevation > 0.5f)  // Fixed: was 0.6f, now matches ClimateSimulator
        {
            albedo = 0.15f;
        }
        // Default land (20%)
        else
        {
            albedo = 0.20f;
        }

        // Urban areas (civilizations) have different albedo (20%)
        if (cell.LifeType == LifeForm.Civilization)
        {
            albedo = 0.20f;
        }

        // Roads modify albedo (asphalt/concrete is dark)
        var geo = cell.GetGeology();
        if (geo.HasRoad)
        {
            // Roads: 0.08-0.12 depending on type (asphalt is very dark)
            albedo = geo.RoadType switch
            {
                RoadType.Highway => 0.08f,   // Fresh asphalt (darkest)
                RoadType.Road => 0.10f,      // Paved road
                RoadType.DirtPath => 0.18f,  // Dirt path (lighter)
                _ => albedo
            };
        }

        // Solar panels have very low albedo (absorb sunlight for energy)
        if (geo.HasSolarFarm)
        {
            albedo = 0.10f; // Dark solar panels
        }

        // Color gradient: Dark (low albedo/absorbs heat) to White (high albedo/reflects heat)
        // Low albedo (0-20%): Dark blue/green (absorbs solar energy)
        // Medium albedo (20-40%): Yellow/tan (moderate reflection)
        // High albedo (40-85%): White (high reflection - ice)
        if (albedo < 0.20f)
        {
            // Low albedo - dark colors (ocean, forest)
            float t = albedo / 0.20f;
            return Color.Lerp(new Color(10, 20, 40), new Color(40, 80, 40), t);
        }
        else if (albedo < 0.40f)
        {
            // Medium albedo - earth tones (desert, grassland)
            float t = (albedo - 0.20f) / 0.20f;
            return Color.Lerp(new Color(40, 80, 40), new Color(200, 180, 100), t);
        }
        else
        {
            // High albedo - bright white (ice, snow)
            float t = (albedo - 0.40f) / 0.45f;
            return Color.Lerp(new Color(200, 180, 100), new Color(255, 255, 255), t);
        }
    }

    private Color GetRadiationColor(TerrainCell cell)
    {
        // Get radiation level from magnetic data
        var magneticData = cell.Magnetic;
        float radiation = magneticData.RadiationLevel;

        // Radiation levels: 0 (safe) to 5+ (deadly)
        // Clamp for visualization
        radiation = Math.Clamp(radiation, 0, 5.0f);
        float t = radiation / 5.0f;

        // Color gradient: Safe (green) → Warning (yellow) → Danger (orange) → Deadly (red/purple)
        if (t < 0.2f)
        {
            // Safe: Dark green to bright green
            return Color.Lerp(new Color(20, 40, 20), new Color(50, 200, 50), t * 5.0f);
        }
        else if (t < 0.4f)
        {
            // Low: Green to yellow
            return Color.Lerp(new Color(50, 200, 50), new Color(200, 200, 50), (t - 0.2f) * 5.0f);
        }
        else if (t < 0.6f)
        {
            // Medium: Yellow to orange
            return Color.Lerp(new Color(200, 200, 50), new Color(255, 150, 50), (t - 0.4f) * 5.0f);
        }
        else if (t < 0.8f)
        {
            // High: Orange to red
            return Color.Lerp(new Color(255, 150, 50), new Color(255, 50, 50), (t - 0.6f) * 5.0f);
        }
        else
        {
            // Deadly: Red to purple
            return Color.Lerp(new Color(255, 50, 50), new Color(150, 0, 150), (t - 0.8f) * 5.0f);
        }
    }

    private Color GetEarthquakesColor(TerrainCell cell)
    {
        var geo = cell.Geology;

        // Epicenter (bright red/orange pulse)
        if (geo.IsEpicenter)
        {
            // Magnitude-based brightness: M2.0 (dim) to M9.0+ (bright)
            float magnitudeNormalized = Math.Clamp((geo.EarthquakeMagnitude - 2.0f) / 7.0f, 0, 1);
            return Color.Lerp(new Color(255, 200, 100), new Color(255, 50, 0), magnitudeNormalized);
        }

        // Active earthquake intensity (seismic waves)
        if (geo.EarthquakeIntensity > 0)
        {
            float intensity = Math.Clamp(geo.EarthquakeIntensity, 0, 1);
            // Color gradient: Faint yellow → Orange → Red
            if (intensity < 0.3f)
            {
                return Color.Lerp(new Color(100, 100, 60), new Color(200, 200, 100), intensity / 0.3f);
            }
            else if (intensity < 0.7f)
            {
                return Color.Lerp(new Color(200, 200, 100), new Color(255, 150, 50), (intensity - 0.3f) / 0.4f);
            }
            else
            {
                return Color.Lerp(new Color(255, 150, 50), new Color(255, 100, 0), (intensity - 0.7f) / 0.3f);
            }
        }

        // Seismic stress buildup (cool colors for accumulating stress)
        if (geo.SeismicStress > 0.3f)
        {
            float stress = Math.Clamp(geo.SeismicStress, 0.3f, 1.5f);
            float t = (stress - 0.3f) / 1.2f; // Normalize 0.3-1.5 to 0-1
            // Blue (low stress) → Purple (high stress, ready to rupture)
            return Color.Lerp(new Color(80, 80, 150), new Color(150, 50, 150), t);
        }

        // Base color (dark)
        return new Color(40, 40, 50);
    }

    private Color GetFaultsColor(TerrainCell cell)
    {
        var geo = cell.Geology;

        if (!geo.IsFault)
        {
            // Non-fault areas: dark gray
            return new Color(50, 50, 50);
        }

        // Keep interior faults minimal: only show major continental ones, unless user placed them
        bool isBoundaryFault = geo.BoundaryType != PlateBoundaryType.None;
        if (!isBoundaryFault && !geo.IsManualFault)
        {
            bool isContinental = cell.IsLand && geo.CrustType != CrustType.Oceanic;
            bool isMajor = geo.FaultActivity >= 0.6f || geo.SeismicStress >= 0.75f;

            if (!isContinental || !isMajor)
            {
                return new Color(50, 50, 50);
            }
        }

        // Fault color based on type and activity
        Color baseColor = geo.FaultType switch
        {
            FaultType.Strike_Slip => new Color(255, 200, 50),    // Yellow-orange (San Andreas)
            FaultType.Normal => new Color(100, 200, 255),        // Light blue (extension)
            FaultType.Reverse => new Color(255, 100, 100),       // Red-pink (compression)
            FaultType.Thrust => new Color(200, 50, 50),          // Dark red (major compression)
            FaultType.Oblique => new Color(200, 150, 255),       // Purple (mixed)
            _ => Color.Gray
        };

        // Brightness based on fault activity (0-1)
        float activity = Math.Clamp(geo.FaultActivity, 0, 1);
        baseColor = Color.Lerp(new Color(baseColor.R / 3, baseColor.G / 3, baseColor.B / 3), baseColor, activity);

        // Brighten if stress is high (about to rupture)
        if (geo.SeismicStress > 0.7f)
        {
            baseColor = Color.Lerp(baseColor, Color.White, (geo.SeismicStress - 0.7f) / 0.3f * 0.5f);
        }

        return baseColor;
    }

    private Color GetTsunamisColor(TerrainCell cell)
    {
        var geo = cell.Geology;

        // Tsunami wave height visualization
        // Safety check against NaN/Infinity which would cause white screen
        if (!float.IsNaN(geo.TsunamiWaveHeight) && !float.IsInfinity(geo.TsunamiWaveHeight) && geo.TsunamiWaveHeight > 0.0f)
        {
            // Wave height: 0m (calm) to 30m+ (catastrophic)
            float height = Math.Clamp(geo.TsunamiWaveHeight, 0, 30);
            float t = height / 30.0f;

            // Color gradient: Light blue → Cyan → Aqua → White (massive wave)
            if (t < 0.2f)
            {
                // Small wave: Light blue
                return Color.Lerp(new Color(100, 150, 255), new Color(100, 200, 255), t * 5.0f);
            }
            else if (t < 0.5f)
            {
                // Medium wave: Cyan
                return Color.Lerp(new Color(100, 200, 255), new Color(0, 255, 255), (t - 0.2f) / 0.3f);
            }
            else if (t < 0.8f)
            {
                // Large wave: Bright cyan/white
                return Color.Lerp(new Color(0, 255, 255), new Color(200, 255, 255), (t - 0.5f) / 0.3f);
            }
            else
            {
                // Catastrophic wave: White
                return Color.Lerp(new Color(200, 255, 255), Color.White, (t - 0.8f) / 0.2f);
            }
        }

        // Coastal flooding (land areas with flood water)
        if (cell.IsLand && !float.IsNaN(geo.FloodLevel) && geo.FloodLevel > 0)
        {
            float floodLevel = Math.Clamp(geo.FloodLevel, 0, 5);
            float t = floodLevel / 5.0f;
            // Muddy water color: Brown to dark blue
            return Color.Lerp(new Color(120, 100, 60), new Color(80, 120, 150), t);
        }

        // Ocean color for reference
        if (cell.IsWater)
        {
            return new Color(30, 50, 120); // Dark blue
        }

        // Land (no tsunami): Gray-green
        return new Color(60, 80, 60);
    }

    private Color GetInfrastructureColor(TerrainCell cell)
    {
        var geo = cell.GetGeology();

        // Priority visualization: show most important infrastructure

        // Nuclear power plants (red - highest priority for safety)
        if (geo.HasNuclearPlant)
        {
            // Color intensity based on meltdown risk
            float risk = geo.MeltdownRisk;
            if (risk > 0.2f)
                return Color.Lerp(new Color(255, 100, 0), Color.Red, (risk - 0.2f) / 0.3f); // Orange to red (dangerous)
            else
                return Color.Lerp(new Color(150, 0, 150), new Color(255, 100, 0), risk / 0.2f); // Purple to orange (safe to warning)
        }

        // Solar farms (bright yellow/gold)
        if (geo.HasSolarFarm)
        {
            return new Color(255, 215, 0); // Gold (solar panels)
        }

        // Wind turbines (cyan/light blue)
        if (geo.HasWindTurbine)
        {
            return new Color(100, 200, 255); // Light blue (wind)
        }

        // Roads with tunnels (bright green)
        if (geo.HasTunnel)
        {
            return new Color(0, 255, 100); // Bright green (major infrastructure)
        }

        // Highways (dark gray/black)
        if (geo.HasRoad && geo.RoadType == RoadType.Highway)
        {
            return new Color(50, 50, 50); // Dark gray
        }

        // Paved roads (medium gray)
        if (geo.HasRoad && geo.RoadType == RoadType.Road)
        {
            return new Color(120, 120, 120); // Medium gray
        }

        // Dirt paths (light brown)
        if (geo.HasRoad && geo.RoadType == RoadType.DirtPath)
        {
            return new Color(160, 140, 100); // Light brown
        }

        // Civilization territory (light gray base)
        if (cell.LifeType == LifeForm.Civilization)
        {
            return new Color(80, 80, 80); // Dark gray background
        }

        // Water (dark blue)
        if (cell.IsWater)
        {
            return new Color(20, 40, 80); // Dark blue
        }

        // Uninhabited land (dark green)
        return new Color(40, 60, 40); // Dark green
    }

    private Color GetElectricityColor(TerrainCell cell)
    {
        var geo = cell.GetGeology();

        // Water has no power grid
        if (cell.IsWater)
        {
            return new Color(30, 30, 50); // Dark blue-gray
        }

        // EMP affected areas - red warning
        if (geo.IsEMPAffected)
        {
            // Pulse effect based on infrastructure
            if (geo.HasNuclearPlant || geo.HasSolarFarm || geo.HasWindTurbine || geo.HasPowerStation)
            {
                return new Color(255, 50, 50);  // Bright red - disabled infrastructure
            }
            return new Color(150, 50, 50);  // Dark red - EMP affected zone
        }

        // Power generation sources (brightest)
        if (geo.HasNuclearPlant)
        {
            // Nuclear plant - yellow/orange based on output
            float output = Math.Clamp(geo.PowerOutput / 1000f, 0, 1);
            return Color.Lerp(new Color(255, 150, 0), new Color(255, 255, 100), output);
        }

        if (geo.HasSolarFarm)
        {
            // Solar farm - bright yellow
            return new Color(255, 220, 50);
        }

        if (geo.HasWindTurbine)
        {
            // Wind turbine - cyan
            return new Color(100, 200, 255);
        }

        // Power distribution
        if (geo.HasPowerStation)
        {
            return new Color(255, 150, 0);  // Orange - distribution hub
        }

        if (geo.HasPowerLine)
        {
            return new Color(200, 200, 50);  // Yellow - transmission line
        }

        // Powered areas (civilization with electricity)
        if (geo.IsPowered && cell.LifeType == LifeForm.Civilization)
        {
            // Green - has power, intensity based on consumption
            float consumption = Math.Clamp(geo.PowerConsumption / 100f, 0, 1);
            return Color.Lerp(new Color(50, 150, 50), new Color(100, 255, 100), consumption);
        }

        // Unpowered civilization
        if (cell.LifeType == LifeForm.Civilization)
        {
            return new Color(100, 50, 50);  // Dark red - no power
        }

        // Rural/unpopulated land
        if (cell.IsLand)
        {
            return new Color(40, 40, 40);  // Very dark gray
        }

        return new Color(30, 30, 50);
    }

    private Color GetSpectralBandsColor(TerrainCell cell)
    {
        // Visualize net radiation budget: shortwave + longwave fluxes
        // Shows energy balance at surface - positive = warming, negative = cooling
        var met = cell.GetMeteorology();
        var column = met.Column;

        // Net shortwave (absorbed solar energy)
        float netShortwave = column.ShortwaveDownSurface - column.ShortwaveUpSurface;

        // Net longwave (thermal infrared balance)
        float netLongwave = column.LongwaveDownSurface - column.LongwaveUpSurface;

        // Total net radiation budget (W/m²)
        float netRadiation = netShortwave + netLongwave;

        // Normalize to color range
        // Typical range: -200 to +400 W/m²
        // Negative = net cooling (blue), Positive = net heating (red/yellow)

        if (netRadiation > 0)
        {
            // Heating (red/yellow gradient)
            float intensity = Math.Clamp(netRadiation / 400f, 0, 1);

            if (intensity < 0.5f)
            {
                // Yellow to orange
                float t = intensity * 2f;
                return Color.Lerp(Color.Yellow, new Color(255, 128, 0), t);
            }
            else
            {
                // Orange to red
                float t = (intensity - 0.5f) * 2f;
                return Color.Lerp(new Color(255, 128, 0), Color.Red, t);
            }
        }
        else
        {
            // Cooling (blue gradient)
            float intensity = Math.Clamp(-netRadiation / 200f, 0, 1);

            if (intensity < 0.5f)
            {
                // Light blue to medium blue
                float t = intensity * 2f;
                return Color.Lerp(Color.LightBlue, Color.Blue, t);
            }
            else
            {
                // Medium blue to dark blue
                float t = (intensity - 0.5f) * 2f;
                return Color.Lerp(Color.Blue, new Color(0, 0, 100), t);
            }
        }
    }

    private Color GetCivilizationColor(TerrainCell cell, int x, int y)
    {
        int index = y * _map.Width + x;
        if (index >= 0 && index < _civOwnerMap.Length)
        {
            int civId = _civOwnerMap[index];

            if (civId > 0)
            {
                // Owned territory
                Color color = GetCivColor(civId);

                // Darken slightly for terrain texture
                // We can blend with terrain or elevation to show features under the color
                float elevationFactor = (cell.Elevation + 1f) / 2f; // 0-1
                // Modulate brightness by elevation to show mountains etc
                float brightness = 0.8f + elevationFactor * 0.4f;

                return new Color(
                    Math.Min(255, (int)(color.R * brightness)),
                    Math.Min(255, (int)(color.G * brightness)),
                    Math.Min(255, (int)(color.B * brightness))
                );
            }
        }

        if (cell.IsWater) return new Color(20, 40, 80);
        return new Color(80, 80, 80); // Unclaimed land
    }

    private Color[] _civPalette = new[]
    {
        new Color(65, 105, 225), // Royal Blue
        new Color(220, 20, 60),  // Crimson
        new Color(255, 215, 0),  // Gold
        new Color(50, 205, 50),  // Lime Green
        new Color(138, 43, 226), // Blue Violet
        new Color(255, 140, 0),  // Dark Orange
        new Color(0, 206, 209),  // Dark Turquoise
        new Color(255, 105, 180), // Hot Pink
        new Color(139, 69, 19),  // Saddle Brown
        new Color(128, 128, 128) // Gray
    };

    private Color GetCivColor(int civId)
    {
        return _civPalette[Math.Abs(civId - 1) % _civPalette.Length];
    }

    public void DrawLegend(SpriteBatch spriteBatch, FontRenderer font, int screenWidth, int screenHeight)
    {
        // Don't show legend for Terrain mode (it's self-explanatory)
        if (Mode == RenderMode.Terrain || Mode == RenderMode.TerrainClean)
            return;

        int legendWidth = 250; // Increased width for better spacing
        bool isGeologicalLegend = Mode == RenderMode.Geological;
        var discreteEntries = GetDiscreteLegendEntries();
        int legendHeight = isGeologicalLegend
            ? 200
            : (discreteEntries != null && discreteEntries.Count > 0)
                ? Math.Min(320, 60 + discreteEntries.Count * 22)
                : 200; // Increased height for more labels
        // Position legend in bottom-right corner (empty space)
        int legendX = screenWidth - legendWidth - 10;
        int legendY = screenHeight - legendHeight - 10;

        // Background
        spriteBatch.Draw(_pixelTexture,
            new Rectangle(legendX, legendY, legendWidth, legendHeight),
            new Color(0, 0, 0, 220)); // Darker background

        // Border
        DrawBorder(spriteBatch, legendX, legendY, legendWidth, legendHeight, Color.Gray, 1);

        // Title
        string title = GetLegendTitle();
        font.DrawString(spriteBatch, title, new Vector2(legendX + 10, legendY + 10), Color.White, 16);

        if (isGeologicalLegend)
        {
            DrawGeologicalLegend(spriteBatch, font, legendX, legendY);
            return;
        }

        if (discreteEntries != null && discreteEntries.Count > 0)
        {
            DrawDiscreteLegend(spriteBatch, font, legendX, legendY, discreteEntries);
            return;
        }

        // Draw color gradient and labels
        int gradientX = legendX + 15;
        int gradientY = legendY + 45;
        int gradientWidth = legendWidth - 30;
        int gradientHeight = 25; // Thicker gradient bar

        DrawGradientBar(spriteBatch, gradientX, gradientY, gradientWidth, gradientHeight);

        // Labels with more detail
        var labels = GetLegendLabels();
        int labelY = gradientY + gradientHeight + 10;

        if (labels.Count >= 2)
        {
            // Min, Max, and Mid labels for continuous data
            font.DrawString(spriteBatch, labels[0], new Vector2(gradientX, labelY), Color.White, 12);
            var midLabelSize = font.MeasureString(labels[1], 12);
            font.DrawString(spriteBatch, labels[1], new Vector2(gradientX + (gradientWidth - midLabelSize.X) / 2, labelY), Color.White, 12);
            var maxSize = font.MeasureString(labels[2], 12);
            font.DrawString(spriteBatch, labels[2], new Vector2(gradientX + gradientWidth - maxSize.X, labelY), Color.White, 12);
        }
    }

    private void DrawGeologicalLegend(SpriteBatch spriteBatch, FontRenderer font, int legendX, int legendY)
    {
        int swatchX = legendX + 15;
        int swatchY = legendY + 40;
        int swatchSize = 24;

        var entries = new (Color Color, string Label)[]
        {
            (new Color(75, 65, 65), "Volcanic (Basalt/Lava)"),
            (new Color(180, 160, 120), "Sedimentary (Sandstone/Limestone)"),
            (new Color(135, 135, 145), "Crystalline (Granite/Metamorphic)")
        };

        foreach (var entry in entries)
        {
            var swatchRect = new Rectangle(swatchX, swatchY, swatchSize, swatchSize);
            spriteBatch.Draw(_pixelTexture, swatchRect, entry.Color);
            DrawBorder(spriteBatch, swatchRect.X, swatchRect.Y, swatchRect.Width, swatchRect.Height, Color.White, 1);

            font.DrawString(spriteBatch, entry.Label, new Vector2(swatchX + swatchSize + 10, swatchY + 4), Color.White, 11);

            swatchY += swatchSize + 15;
        }

        // Small note tying colors to gameplay data
        font.DrawString(
            spriteBatch,
            "Colors reflect dominant rock type per tile",
            new Vector2(swatchX, swatchY + 5),
            new Color(200, 200, 200),
            9);
    }

    private void DrawDiscreteLegend(SpriteBatch spriteBatch, FontRenderer font, int legendX, int legendY,
        List<(Color Color, string Label)> entries)
    {
        int swatchX = legendX + 15;
        int swatchY = legendY + 40;
        int swatchSize = 18;
        int startY = swatchY;
        int columnWidth = 140;
        bool useTwoColumns = entries.Count > 8; // Auto-split if too many items (like Infrastructure)

        for (int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];

            // Handle column wrapping
            if (useTwoColumns && i == (entries.Count + 1) / 2)
            {
                swatchX += columnWidth;
                swatchY = startY;
            }

            var rect = new Rectangle(swatchX, swatchY, swatchSize, swatchSize);
            spriteBatch.Draw(_pixelTexture, rect, entry.Color);
            DrawBorder(spriteBatch, rect.X, rect.Y, rect.Width, rect.Height, Color.White, 1);

            font.DrawString(spriteBatch, entry.Label, new Vector2(swatchX + swatchSize + 10, swatchY + 2), Color.White, 11);

            swatchY += swatchSize + 8;
        }
    }

    private string GetLegendTitle()
    {
        return Mode switch
        {
            RenderMode.Temperature => "TEMPERATURE",
            RenderMode.Rainfall => "RAINFALL",
            RenderMode.Life => "LIFE / BIOMASS",
            RenderMode.Oxygen => "OXYGEN LEVELS",
            RenderMode.CO2 => "CO2 LEVELS",
            RenderMode.Elevation => "ELEVATION",
            RenderMode.Geological => "ROCK TYPES",
            RenderMode.TectonicPlates => "TECTONIC PLATES",
            RenderMode.Volcanoes => "VOLCANIC ACTIVITY",
            RenderMode.Clouds => "CLOUD COVER",
            RenderMode.Wind => "WIND SPEED",
            RenderMode.Pressure => "AIR PRESSURE",
            RenderMode.Storms => "STORM INTENSITY",
            RenderMode.Biomes => "BIOMES",
            RenderMode.Resources => "RESOURCES",
            RenderMode.Albedo => "SURFACE ALBEDO",
            RenderMode.Radiation => "RADIATION LEVELS",
            RenderMode.Infrastructure => "CIVILIZATION INFRASTRUCTURE",
            RenderMode.Electricity => "POWER GRID & ENERGY",
            RenderMode.SpectralBands => "NET RADIATION BUDGET",
            RenderMode.Civilizations => "POLITICAL MAP",
            RenderMode.Auroras => "AURORA INTENSITY",
            RenderMode.Tsunamis => "TSUNAMI WAVE HEIGHT",
            _ => "LEGEND"
        };
    }

    private List<string> GetLegendLabels()
    {
        return Mode switch
        {
            RenderMode.Temperature => new List<string> { "-50 C", "0 C", "50 C" },
            RenderMode.Rainfall => new List<string> { "Arid", "Moderate", "Wet" },
            RenderMode.Life => new List<string> { "None", "Plants", "Animals" },
            RenderMode.Oxygen => new List<string> { "0%", "15%", "30%" },
            RenderMode.CO2 => new List<string> { "0%", "5%", "10%" },
            RenderMode.Elevation => new List<string> { "Ocean Floor", "Sea Level", "Mountain" },
            RenderMode.Geological => new List<string> { "Volcanic", "Sedimentary", "Crystalline" },
            RenderMode.TectonicPlates => new List<string> { "Plate 1", "Plate 4", "Plate 8" },
            RenderMode.Volcanoes => new List<string> { "Dormant", "Active", "Erupting" },
            RenderMode.Clouds => new List<string> { "Clear", "Partly Cloudy", "Overcast" },
            RenderMode.Wind => new List<string> { "Calm", "Breezy", "Gale Force" },
            RenderMode.Pressure => new List<string> { "Low", "Normal", "High" },
            RenderMode.Storms => new List<string> { "Clear", "Rain", "Severe Storm" },
            RenderMode.Biomes => new List<string> { "Desert", "Grassland", "Forest" },
            RenderMode.Resources => new List<string> { "Few", "Moderate", "Abundant" },
            RenderMode.Albedo => new List<string> { "Absorptive", "Neutral", "Reflective" },
            RenderMode.Radiation => new List<string> { "Safe", "Warning", "Deadly" },
            RenderMode.Infrastructure => new List<string> { "Roads", "Energy", "Hubs" },
            RenderMode.Electricity => new List<string> { "No Power", "Powered", "EMP Disabled" },
            RenderMode.SpectralBands => new List<string> { "Cooling", "Balanced", "Heating" },
            RenderMode.Auroras => new List<string> { "None", "Weak", "Strong" },
            RenderMode.Tsunamis => new List<string> { "Calm", "High Wave", "Catastrophic" },
            _ => new List<string>()
        };
    }

    private List<(Color Color, string Label)>? GetDiscreteLegendEntries()
    {
        return Mode switch
        {
            RenderMode.Biomes => new List<(Color, string)>
            {
                (new Color(240, 250, 255), "Glacier"),
                (new Color(200, 210, 220), "Alpine Tundra"),
                (new Color(180, 190, 160), "Tundra"),
                (new Color(10, 100, 20), "Tropical Rainforest"),
                (new Color(34, 139, 34), "Temperate Forest"),
                (new Color(20, 80, 40), "Boreal Forest"),
                (new Color(200, 180, 100), "Savanna"),
                (new Color(100, 160, 80), "Grassland"),
                (new Color(140, 140, 80), "Shrubland"),
                (new Color(230, 200, 140), "Desert"),
                (new Color(140, 130, 120), "Mountain"),
                (new Color(60, 120, 90), "Wetland"),
                (new Color(20, 100, 180), "Shallow Water"),
                (new Color(0, 50, 120), "Deep Ocean"),
            },
            RenderMode.Resources => new List<(Color, string)>
            {
                (ResourceExtensions.GetResourceColor(ResourceType.Iron), "Iron"),
                (ResourceExtensions.GetResourceColor(ResourceType.Copper), "Copper"),
                (ResourceExtensions.GetResourceColor(ResourceType.Coal), "Coal"),
                (ResourceExtensions.GetResourceColor(ResourceType.Gold), "Gold"),
                (ResourceExtensions.GetResourceColor(ResourceType.Silver), "Silver"),
                (ResourceExtensions.GetResourceColor(ResourceType.Oil), "Oil"),
                (ResourceExtensions.GetResourceColor(ResourceType.NaturalGas), "Natural Gas"),
                (ResourceExtensions.GetResourceColor(ResourceType.Uranium), "Uranium"),
                (ResourceExtensions.GetResourceColor(ResourceType.Platinum), "Platinum"),
                (ResourceExtensions.GetResourceColor(ResourceType.Diamond), "Diamond"),
            },
            RenderMode.Infrastructure => new List<(Color, string)>
            {
                (new Color(150, 0, 150), "Nuclear Plant (Stable)"),
                (Color.Red, "Nuclear Plant (High Risk)"),
                (new Color(255, 215, 0), "Solar Farm"),
                (new Color(100, 200, 255), "Wind Turbine"),
                (new Color(0, 255, 100), "Tunnel / Major Link"),
                (new Color(50, 50, 50), "Highway"),
                (new Color(120, 120, 120), "Paved Road"),
                (new Color(160, 140, 100), "Dirt Path"),
                (new Color(80, 80, 80), "Civilization Territory"),
                (new Color(20, 40, 80), "Water / Ocean")
            },
            RenderMode.Electricity => new List<(Color, string)>
            {
                (new Color(255, 200, 0), "Nuclear Plant"),
                (new Color(255, 220, 50), "Solar Farm"),
                (new Color(100, 200, 255), "Wind Turbine"),
                (new Color(255, 150, 0), "Power Station"),
                (new Color(200, 200, 50), "Power Line"),
                (new Color(50, 200, 50), "Powered Area"),
                (new Color(100, 50, 50), "No Power"),
                (new Color(255, 50, 50), "EMP Disabled"),
                (new Color(150, 0, 150), "High Power Output"),
                (new Color(30, 30, 50), "Unpowered / Water")
            },
            RenderMode.Earthquakes => new List<(Color, string)>
            {
                (new Color(255, 50, 0), "Epicenter (High Mag)"),
                (new Color(255, 200, 100), "Epicenter (Low Mag)"),
                (new Color(255, 100, 0), "Active Wave (Strong)"),
                (new Color(100, 100, 60), "Active Wave (Weak)"),
                (new Color(150, 50, 150), "High Stress Buildup"),
                (new Color(80, 80, 150), "Low Stress Buildup"),
                (new Color(40, 40, 50), "Stable Ground")
            },
            RenderMode.Faults => new List<(Color, string)>
            {
                (new Color(255, 200, 50), "Strike-Slip"),
                (new Color(100, 200, 255), "Normal (Extension)"),
                (new Color(255, 100, 100), "Reverse (Compression)"),
                (new Color(200, 50, 50), "Thrust (Major)"),
                (new Color(200, 150, 255), "Oblique (Mixed)"),
                (Color.White, "Critical Stress"),
                (new Color(50, 50, 50), "Stable Crust")
            },
            RenderMode.Civilizations => GetCivilizationLegendEntries(),
            _ => null
        };
    }

    private List<(Color, string)>? GetCivilizationLegendEntries()
    {
        if (_civilizationManager == null) return null;
        return _cachedCivLegend;
    }

    private void DrawGradientBar(SpriteBatch spriteBatch, int x, int y, int width, int height)
    {
        // Draw a color gradient representing the current view mode
        for (int i = 0; i < width; i++)
        {
            float t = i / (float)width;
            Color color = GetGradientColor(t);
            spriteBatch.Draw(_pixelTexture, new Rectangle(x + i, y, 1, height), color);
        }

        // Border around gradient
        DrawBorder(spriteBatch, x, y, width, height, Color.Gray, 1);
    }

    private Color GetGradientColor(float t)
    {
        // t ranges from 0 to 1
        return Mode switch
        {
            // Fixed to match actual GetTemperatureColor: Blue → Green → Red
            RenderMode.Temperature => GetTemperatureGradientColor(t),
            // Fixed to match actual GetRainfallColor: Brown → Blue
            RenderMode.Rainfall => LerpColor(new Color(139, 90, 43), new Color(0, 100, 200), t),
            RenderMode.Life => GetLifeGradientColor(t),
            // Fixed to match actual GetOxygenColor: Black → Light Blue
            RenderMode.Oxygen => LerpColor(Color.Black, new Color(100, 200, 255), t),
            RenderMode.CO2 => LerpColor(new Color(50, 50, 100), new Color(255, 200, 50), t),
            // Fixed to match actual GetElevationColor: Black → White
            RenderMode.Elevation => LerpColor(Color.Black, Color.White, t),
            RenderMode.Geological => GetGeologicalGradientColor(t),
            RenderMode.Volcanoes => LerpColor(new Color(60, 60, 60), new Color(255, 100, 0), t),
            RenderMode.Clouds => LerpColor(new Color(50, 100, 150), Color.White, t),
            RenderMode.Wind => LerpColor(new Color(200, 255, 200), new Color(255, 50, 50), t),
            RenderMode.Pressure => LerpColor(new Color(50, 100, 255), new Color(255, 50, 50), t),
            RenderMode.Storms => LerpColor(new Color(150, 200, 255), new Color(100, 0, 100), t),
            RenderMode.Albedo => GetAlbedoGradientColor(t),
            RenderMode.Radiation => GetRadiationGradientColor(t),
            RenderMode.SpectralBands => GetSpectralBandsGradientColor(t),
            RenderMode.Auroras => GetAuroraGradientColor(t),
            RenderMode.Tsunamis => LerpColor(new Color(100, 150, 255), Color.White, t),
            _ => Color.Gray
        };
    }

    private Color GetAuroraColor(TerrainCell cell)
    {
        // Base color (night sky / dark terrain)
        Color baseColor = new Color(10, 10, 20);

        // Get magnetic data
        var mag = cell.Magnetic;
        float intensity = mag.AuroraIntensity;

        if (intensity <= 0.01f) return baseColor;

        // Aurora colors: Green (common) -> Purple/Red (rare/strong)
        Color auroraColor;
        if (intensity < 0.5f)
        {
            // Weak to moderate: Green
            auroraColor = Color.Lerp(new Color(0, 100, 50), new Color(50, 255, 100), intensity * 2);
        }
        else
        {
            // Strong: Green to Purple/Red
            auroraColor = Color.Lerp(new Color(50, 255, 100), new Color(255, 50, 255), (intensity - 0.5f) * 2);
        }

        // Blend additively for glow effect
        return new Color(
            Math.Min(255, baseColor.R + auroraColor.R),
            Math.Min(255, baseColor.G + auroraColor.G),
            Math.Min(255, baseColor.B + auroraColor.B)
        );
    }

    private Color GetAuroraGradientColor(float t)
    {
        if (t < 0.5f)
        {
            return Color.Lerp(new Color(10, 10, 20), new Color(50, 255, 100), t * 2);
        }
        else
        {
            return Color.Lerp(new Color(50, 255, 100), new Color(255, 50, 255), (t - 0.5f) * 2);
        }
    }

    private Color GetTemperatureGradientColor(float t)
    {
        // Match GetTemperatureColor: Blue → Green → Red
        if (t < 0.5f)
        {
            // Blue to green
            return Color.Lerp(new Color(0, 0, 255), new Color(0, 255, 0), t * 2);
        }
        else
        {
            // Green to red
            return Color.Lerp(new Color(0, 255, 0), new Color(255, 0, 0), (t - 0.5f) * 2);
        }
    }

    private Color GetSpectralBandsGradientColor(float t)
    {
        // Gradient from cooling (blue) to heating (yellow/red)
        // t=0: -200 W/m² (strong cooling, dark blue)
        // t=0.5: 0 W/m² (neutral, light blue/yellow transition)
        // t=1: +400 W/m² (strong heating, red)

        if (t < 0.25f)
        {
            // Dark blue to blue (strong to moderate cooling)
            return Color.Lerp(new Color(0, 0, 100), Color.Blue, t * 4);
        }
        else if (t < 0.5f)
        {
            // Blue to light blue (moderate to weak cooling)
            return Color.Lerp(Color.Blue, Color.LightBlue, (t - 0.25f) * 4);
        }
        else if (t < 0.75f)
        {
            // Light blue/yellow to orange (weak heating to moderate heating)
            return Color.Lerp(Color.Yellow, new Color(255, 128, 0), (t - 0.5f) * 4);
        }
        else
        {
            // Orange to red (moderate to strong heating)
            return Color.Lerp(new Color(255, 128, 0), Color.Red, (t - 0.75f) * 4);
        }
    }

    private Color GetAlbedoGradientColor(float t)
    {
        // Dark (low albedo) to White (high albedo)
        if (t < 0.25f)
        {
            // Ocean/forest: dark blue to dark green
            return Color.Lerp(new Color(10, 20, 40), new Color(40, 80, 40), t * 4);
        }
        else if (t < 0.5f)
        {
            // Grassland: green to yellow
            return Color.Lerp(new Color(40, 80, 40), new Color(200, 180, 100), (t - 0.25f) * 4);
        }
        else
        {
            // Desert/ice: yellow to white
            return Color.Lerp(new Color(200, 180, 100), Color.White, (t - 0.5f) * 2);
        }
    }

    private Color GetRadiationGradientColor(float t)
    {
        // Safe (green) → Warning (yellow) → Danger (orange) → Deadly (red/purple)
        if (t < 0.2f)
        {
            return Color.Lerp(new Color(20, 40, 20), new Color(50, 200, 50), t * 5);
        }
        else if (t < 0.4f)
        {
            return Color.Lerp(new Color(50, 200, 50), new Color(200, 200, 50), (t - 0.2f) * 5);
        }
        else if (t < 0.6f)
        {
            return Color.Lerp(new Color(200, 200, 50), new Color(255, 150, 50), (t - 0.4f) * 5);
        }
        else if (t < 0.8f)
        {
            return Color.Lerp(new Color(255, 150, 50), new Color(255, 50, 50), (t - 0.6f) * 5);
        }
        else
        {
            return Color.Lerp(new Color(255, 50, 50), new Color(150, 0, 150), (t - 0.8f) * 5);
        }
    }

    private Color GetLifeGradientColor(float t)
    {
        if (t < 0.5f) return Color.Lerp(new Color(139, 69, 19), new Color(0, 255, 0), t * 2);
        return Color.Lerp(new Color(0, 255, 0), new Color(0, 100, 0), (t - 0.5f) * 2);
    }

    private Color GetGeologicalGradientColor(float t)
    {
        if (t < 0.33f) return Color.Lerp(new Color(80, 80, 80), new Color(120, 100, 80), t * 3);
        if (t < 0.66f) return Color.Lerp(new Color(120, 100, 80), new Color(200, 180, 140), (t - 0.33f) * 3);
        return Color.Lerp(new Color(200, 180, 140), new Color(150, 150, 200), (t - 0.66f) * 3);
    }

    private Color LerpColor(Color start, Color end, float t)
    {
        return Color.Lerp(start, end, t);
    }

    private void DrawBorder(SpriteBatch spriteBatch, int x, int y, int width, int height, Color color, int thickness)
    {
        // Top
        spriteBatch.Draw(_pixelTexture, new Rectangle(x, y, width, thickness), color);
        // Bottom
        spriteBatch.Draw(_pixelTexture, new Rectangle(x, y + height - thickness, width, thickness), color);
        // Left
        spriteBatch.Draw(_pixelTexture, new Rectangle(x, y, thickness, height), color);
        // Right
        spriteBatch.Draw(_pixelTexture, new Rectangle(x + width - thickness, y, thickness, height), color);
    }

    #region Procedural Terrain Colors

    private Color GetDeepOceanColor(TerrainCell cell, float variation)
    {
        var geo = cell.GetGeology();

        // Check if this is a dry basin (depression without water)
        if (geo.IsDryBasin || (!geo.IsConnectedToOcean && geo.AccumulatedWater < 0.1f))
        {
            // Dry basin - show as salt flat / dried lake bed
            return GetDryBasinColor(cell, variation);
        }

        return GetOceanGradientColor(cell, variation);
    }

    private Color GetShallowWaterColor(TerrainCell cell, float variation)
    {
        var geo = cell.GetGeology();

        // Check if this is a dry basin or partially filled
        if (geo.IsDryBasin || (!geo.IsConnectedToOcean && geo.AccumulatedWater < 0.1f))
        {
            // Dry basin - show as salt flat / dried lake bed
            return GetDryBasinColor(cell, variation);
        }

        // Partial water - forming lake (blend water and dry basin)
        if (!geo.IsConnectedToOcean && geo.AccumulatedWater > 0)
        {
            float waterRatio = Math.Clamp(geo.AccumulatedWater / Math.Abs(cell.Elevation), 0, 1);
            Color dryColor = GetDryBasinColor(cell, variation);
            Color wetColor = GetLakeColor(cell, variation);
            return Color.Lerp(dryColor, wetColor, waterRatio);
        }

        return GetOceanGradientColor(cell, variation);
    }

    /// <summary>
    /// Continuous bathymetry palette: turquoise shelves fading to deep navy abyss.
    /// </summary>
    private static Color GetOceanGradientColor(TerrainCell cell, float variation)
    {
        float d = Math.Clamp(-cell.Elevation, 0f, 1f);
        Color shelf = new Color(52, 160, 200);
        Color shallow = new Color(28, 110, 174);
        Color deep = new Color(14, 54, 120);
        Color abyss = new Color(6, 24, 68);
        Color c = d < 0.12f ? Color.Lerp(shelf, shallow, d / 0.12f)
                : d < 0.45f ? Color.Lerp(shallow, deep, (d - 0.12f) / 0.33f)
                : Color.Lerp(deep, abyss, Math.Min(1f, (d - 0.45f) / 0.5f));
        float v = (variation - 0.5f) * 8f;
        return new Color(
            (int)Math.Clamp(c.R + v, 0, 255),
            (int)Math.Clamp(c.G + v, 0, 255),
            (int)Math.Clamp(c.B + v * 1.5f, 0, 255));
    }

    private Color GetDryBasinColor(TerrainCell cell, float variation)
    {
        // Dry lake bed / salt flat appearance
        Color saltFlat = new Color(220, 210, 190);    // White salt crust
        Color dryClay = new Color(180, 160, 140);      // Dry cracked clay
        Color redDirt = new Color(170, 130, 100);      // Red/brown dirt

        // Mix based on depth (deeper = more salt accumulation)
        float depth = Math.Abs(cell.Elevation);
        Color result;

        if (depth > 0.3f)
        {
            // Deep depression - salt flat
            result = Color.Lerp(dryClay, saltFlat, depth * 1.5f);
        }
        else if (cell.Temperature > 25)
        {
            // Hot climate - red/orange tones
            result = Color.Lerp(dryClay, redDirt, 0.5f);
        }
        else
        {
            result = dryClay;
        }

        // Add cracked texture variation
        return new Color(
            (byte)Math.Clamp(result.R + (int)((variation - 0.5f) * 25), 0, 255),
            (byte)Math.Clamp(result.G + (int)((variation - 0.5f) * 20), 0, 255),
            (byte)Math.Clamp(result.B + (int)((variation - 0.5f) * 20), 0, 255)
        );
    }

    private Color GetLakeColor(TerrainCell cell, float variation)
    {
        // Freshwater lake - slightly greenish blue
        int r = (int)(30 + variation * 15);
        int g = (int)(120 + variation * 25);
        int b = (int)(160 + variation * 30);
        return new Color(r, g, b);
    }

    private Color GetBeachColor(TerrainCell cell, int x, int y, float variation, float variation2)
    {
        // Sandy beach with shells and pebbles
        Color sandBase = new Color(238, 214, 175);
        Color wetSand = new Color(194, 178, 128);
        Color shellColor = new Color(255, 250, 240);
        Color pebbleColor = new Color(140, 130, 120);

        // Wet sand near water
        float wetness = Math.Max(0, 1.0f - cell.Elevation * 20);
        Color result = Color.Lerp(sandBase, wetSand, wetness * 0.5f);

        // Add variation for texture
        result = new Color(
            (byte)Math.Clamp(result.R + (int)((variation - 0.5f) * 30), 0, 255),
            (byte)Math.Clamp(result.G + (int)((variation - 0.5f) * 25), 0, 255),
            (byte)Math.Clamp(result.B + (int)((variation - 0.5f) * 20), 0, 255)
        );

        // Occasional shells (bright spots)
        if (variation2 > 0.92f)
        {
            result = Color.Lerp(result, shellColor, 0.5f);
        }
        // Occasional pebbles (dark spots)
        else if (variation2 < 0.08f)
        {
            result = Color.Lerp(result, pebbleColor, 0.4f);
        }

        return result;
    }

    private Color GetPlainsColor(TerrainCell cell, float variation)
    {
        // Golden plains with grass patches
        Color dryGrass = new Color(200, 180, 120);
        Color greenPatch = new Color(150, 170, 100);

        Color result = Color.Lerp(dryGrass, greenPatch, cell.Rainfall * 0.5f);

        // Add variation
        return new Color(
            (byte)Math.Clamp(result.R + (int)((variation - 0.5f) * 25), 0, 255),
            (byte)Math.Clamp(result.G + (int)((variation - 0.5f) * 20), 0, 255),
            (byte)Math.Clamp(result.B + (int)((variation - 0.5f) * 15), 0, 255)
        );
    }

    private Color GetGrasslandColor(TerrainCell cell, int x, int y, float variation)
    {
        // Grassland with varying shades
        Color lightGrass = new Color(100, 180, 90);
        Color darkGrass = new Color(60, 140, 60);
        Color yellowGrass = new Color(140, 170, 80);

        // Mix based on conditions
        Color result = Color.Lerp(lightGrass, darkGrass, cell.Biomass * 0.5f);
        if (cell.Temperature > 25)
        {
            result = Color.Lerp(result, yellowGrass, 0.3f);
        }

        // Checkerboard-like pattern for field effect
        int pattern = (x / 3 + y / 3) % 2;
        if (pattern == 0)
        {
            result = new Color(
                (byte)Math.Clamp(result.R + 10, 0, 255),
                (byte)Math.Clamp(result.G + 15, 0, 255),
                (byte)result.B
            );
        }

        // Add variation
        return new Color(
            (byte)Math.Clamp(result.R + (int)((variation - 0.5f) * 20), 0, 255),
            (byte)Math.Clamp(result.G + (int)((variation - 0.5f) * 25), 0, 255),
            (byte)Math.Clamp(result.B + (int)((variation - 0.5f) * 15), 0, 255)
        );
    }

    private Color GetForestColor(TerrainCell cell, int x, int y, float variation, float variation2)
    {
        // Forest with tree canopy variation
        Color darkForest = new Color(25, 100, 30);
        Color lightForest = new Color(50, 150, 50);
        Color tropicalForest = new Color(20, 120, 40);

        Color result;
        if (cell.Temperature > 25 && cell.Rainfall > 0.7f)
        {
            // Tropical rainforest - darker, denser
            result = Color.Lerp(tropicalForest, darkForest, variation * 0.5f);
        }
        else
        {
            result = Color.Lerp(darkForest, lightForest, variation);
        }

        // Tree shadows effect
        if (variation2 > 0.7f)
        {
            result = new Color(
                (byte)Math.Max(0, result.R - 15),
                (byte)Math.Max(0, result.G - 20),
                (byte)Math.Max(0, result.B - 10)
            );
        }
        // Occasional clearings
        else if (variation2 < 0.1f)
        {
            result = new Color(
                (byte)Math.Min(255, result.R + 20),
                (byte)Math.Min(255, result.G + 30),
                (byte)Math.Min(255, result.B + 15)
            );
        }

        return result;
    }

    private Color GetDesertColor(TerrainCell cell, int x, int y, float variation, float variation2)
    {
        // Desert with dune patterns
        Color sandDune = new Color(237, 201, 160);
        Color darkSand = new Color(200, 170, 130);
        Color redRock = new Color(180, 120, 90);

        // Create dune wave pattern
        float dune = MathF.Sin(x * 0.3f + y * 0.1f) * 0.5f + 0.5f;
        Color result = Color.Lerp(darkSand, sandDune, dune);

        // Hot desert is more orange/red
        if (cell.Temperature > 35)
        {
            result = Color.Lerp(result, redRock, 0.2f);
        }

        // Rocky outcrops
        if (variation2 > 0.9f && cell.Elevation > 0.1f)
        {
            result = Color.Lerp(result, redRock, 0.5f);
        }

        // Add variation
        return new Color(
            (byte)Math.Clamp(result.R + (int)((variation - 0.5f) * 20), 0, 255),
            (byte)Math.Clamp(result.G + (int)((variation - 0.5f) * 15), 0, 255),
            (byte)Math.Clamp(result.B + (int)((variation - 0.5f) * 15), 0, 255)
        );
    }

    private Color GetMountainColor(TerrainCell cell, int x, int y, float variation)
    {
        // Mountain with rocky textures and snow caps
        Color rock = new Color(120, 110, 100);
        Color darkRock = new Color(80, 75, 70);
        Color snow = new Color(250, 255, 255);
        Color alpine = new Color(100, 120, 100);

        float altitude = cell.Elevation;
        Color result;

        // Snow caps at high altitude or low temperature
        if (altitude > 0.85f || cell.Temperature < -5)
        {
            float snowAmount = Math.Max(0, (altitude - 0.8f) * 5);
            if (cell.Temperature < -5) snowAmount += 0.3f;
            result = Color.Lerp(rock, snow, Math.Min(snowAmount, 1.0f));
        }
        // Alpine zone
        else if (altitude > 0.75f)
        {
            result = Color.Lerp(alpine, rock, (altitude - 0.75f) * 10);
        }
        else
        {
            result = Color.Lerp(darkRock, rock, variation);
        }

        // Rocky texture variation
        return new Color(
            (byte)Math.Clamp(result.R + (int)((variation - 0.5f) * 30), 0, 255),
            (byte)Math.Clamp(result.G + (int)((variation - 0.5f) * 25), 0, 255),
            (byte)Math.Clamp(result.B + (int)((variation - 0.5f) * 20), 0, 255)
        );
    }

    private Color GetIceColor(TerrainCell cell, float variation)
    {
        // Ice with cracks and snow
        Color pureIce = new Color(230, 245, 255);
        Color blueIce = new Color(200, 230, 250);
        Color snow = new Color(255, 255, 255);

        Color result = Color.Lerp(blueIce, pureIce, variation);

        // Fresh snow on top
        if (cell.Rainfall > 0.3f)
        {
            result = Color.Lerp(result, snow, 0.3f);
        }

        // Ice cracks (darker lines)
        if (variation > 0.85f || variation < 0.15f)
        {
            result = new Color(
                (byte)Math.Max(0, result.R - 30),
                (byte)Math.Max(0, result.G - 20),
                (byte)Math.Max(0, result.B - 10)
            );
        }

        return result;
    }

    private Color GetTundraColor(TerrainCell cell, float variation)
    {
        // Tundra with mossy rocks and permafrost
        Color permafrost = new Color(180, 190, 185);
        Color moss = new Color(140, 160, 130);
        Color rock = new Color(160, 155, 150);

        Color result = Color.Lerp(permafrost, moss, cell.Biomass);

        // Rocky patches
        if (variation > 0.7f)
        {
            result = Color.Lerp(result, rock, 0.4f);
        }

        // Add variation
        return new Color(
            (byte)Math.Clamp(result.R + (int)((variation - 0.5f) * 20), 0, 255),
            (byte)Math.Clamp(result.G + (int)((variation - 0.5f) * 25), 0, 255),
            (byte)Math.Clamp(result.B + (int)((variation - 0.5f) * 20), 0, 255)
        );
    }

    #endregion

    public void Dispose()
    {
        _pixelTexture?.Dispose();
        _terrainTexture?.Dispose();
        _detailTexture?.Dispose();
        _nightTexture?.Dispose();
        _starfield?.Dispose();
        _grain?.Dispose();
        // _icons is shared across renderers and lives for the whole session
    }
}

public enum RenderMode
{
    Terrain,
    Temperature,
    Rainfall,
    Life,
    Oxygen,
    CO2,
    Elevation,
    Geological,
    TectonicPlates,
    Volcanoes,
    Clouds,
    Wind,
    Pressure,
    Storms,
    Biomes,
    Resources,
    Albedo,         // Surface reflectivity (ice-albedo feedback)
    Radiation,      // Cosmic rays and solar radiation
    Earthquakes,    // Seismic activity and epicenters
    Faults,         // Fault lines and fault types
    Tsunamis,       // Tsunami waves and coastal flooding
    Infrastructure, // Civilization infrastructure (roads, energy, etc.)
    SpectralBands,             // Radiative transfer with shortwave/longwave fluxes
    Civilizations,             // Political map showing civilization territories
    Auroras,                   // Visualizes aurora intensity and magnetic field protection
    Electricity,               // Power grid: plants, lines, consumption, EMP damage
    TerrainClean               // Base terrain only (no overlays)
}
