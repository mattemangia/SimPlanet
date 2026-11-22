using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace SimPlanet;

/// <summary>
/// Scene building and rendering for the subsurface block.
/// </summary>
public sealed partial class Subsurface3DView
{
    // ---- GPU resources -------------------------------------------------------------------
    private RenderTarget2D? _target;
    private BasicEffect _effect = null!;
    private Texture2D _grain = null!, _dot = null!, _white = null!;
    private Matrix _viewProjection;
    private const float SuperSample = 1.5f;

    // ---- Snapshot of the region ----------------------------------------------------------
    private const int Sub = 3;                    // Surface vertices per cell edge
    private int _n;                               // Size the arrays were built for
    private float _snapshotTimer;
    private bool _hasSnapshot;
    private float[] _elev = Array.Empty<float>(), _uplift = Array.Empty<float>(), _depo = Array.Empty<float>();
    private float[] _activity = Array.Empty<float>(), _pressure = Array.Empty<float>(), _stress = Array.Empty<float>();
    private float[] _slab = Array.Empty<float>(), _velocity = Array.Empty<float>();
    private int[] _plate = Array.Empty<int>();
    private byte[] _boundary = Array.Empty<byte>(), _faultType = Array.Empty<byte>(), _flags = Array.Empty<byte>();
    private Color[] _cellColor = Array.Empty<Color>();
    private StrataColumn[] _columns = Array.Empty<StrataColumn>();
    private const byte FlagIce = 1, FlagVolcano = 2, FlagHotSpot = 4, FlagFault = 8, FlagSubducting = 16, FlagOceanic = 32;

    // ---- Per-vertex surface data ---------------------------------------------------------
    private int _g;                               // Vertices per side
    private float[] _baseY = Array.Empty<float>(), _y = Array.Empty<float>();
    private float[] _waveP = Array.Empty<float>(), _waveS = Array.Empty<float>(), _bumpV = Array.Empty<float>();
    private Color[] _vertexColor = Array.Empty<Color>();

    // ---- Transient effects ---------------------------------------------------------------
    private float[] _bump = Array.Empty<float>(), _flash = Array.Empty<float>(), _erupting = Array.Empty<float>();
    private float[] _ash = Array.Empty<float>(), _lava = Array.Empty<float>();       // Fresh deposits of an eruption
    private float[] _ashV = Array.Empty<float>(), _lavaV = Array.Empty<float>();
    private readonly List<(int cell, float timer, int steps)> _lavaFronts = new();

    private struct CityModel
    {
        public int I, J, Id, Population;
        public CityType Type;
        public CityStyle Style;
        public string Name;
    }
    private readonly List<CityModel> _cities = new();
    private CivilizationManager? _civilizations;

    public void SetCivilizations(CivilizationManager civilizations) => _civilizations = civilizations;
    private readonly List<(int cell, float delay, float bump)> _pendingSlip = new();
    private readonly List<(Vector3 position, float age, float life, Color color, float speed)> _rings = new();
    private float _seismogramHold;
    private float _impactFlash;

    private struct Particle
    {
        public Vector3 Position, Velocity;
        public float Life, MaxLife, Size, Growth, Gravity, Floor;
        public Color Color;
    }
    private readonly List<Particle> _particles = new();
    private readonly Random _random = new();
    private readonly List<(Vector3 position, string text, Color color, int priority)> _labels = new();

    // ---- Colours -------------------------------------------------------------------------
    private static readonly Color GraniteColor = new(196, 150, 150);
    private static readonly Color BasaltColor = new(74, 78, 92);
    private static readonly Color MantleTop = new(196, 96, 44);
    private static readonly Color MantleBottom = new(120, 34, 22);
    private static readonly Color MagmaColor = new(255, 150, 40);
    private static readonly Color OilColor = new(34, 28, 24);
    private static readonly Color GasColor = new(250, 226, 120);
    private static readonly Color SlabColor = new(52, 58, 84);

    private static Color BedColor(SedimentType type) => BedColor(StrataColumn.FromSediment(type));

    private static Color BedColor(RockKind kind) => kind switch
    {
        RockKind.Clay => new Color(150, 118, 96),
        RockKind.Silt => new Color(178, 150, 112),
        RockKind.Sand => new Color(226, 200, 130),
        RockKind.Gravel => new Color(160, 150, 138),
        RockKind.Organic => new Color(58, 50, 44),
        RockKind.Volcanic => new Color(112, 70, 78),
        RockKind.Limestone => new Color(206, 214, 196),
        RockKind.Salt => new Color(240, 226, 232),
        RockKind.Basalt => BasaltColor,
        _ => GraniteColor
    };

    /// <summary>Blue = fast (cold, old), red = slow (hot, soft). Input -1..1.</summary>
    private static Color VelocityColor(float anomaly)
    {
        anomaly = Math.Clamp(anomaly, -1f, 1f);
        return anomaly >= 0
            ? Color.Lerp(new Color(232, 232, 226), new Color(40, 80, 200), anomaly)
            : Color.Lerp(new Color(232, 232, 226), new Color(214, 50, 40), -anomaly);
    }

    private void CreateResources()
    {
        _effect = new BasicEffect(_device) { VertexColorEnabled = true, TextureEnabled = true, LightingEnabled = false };

        _white = new Texture2D(_device, 1, 1);
        _white.SetData(new[] { Color.White });

        // Rock grain: fine speckle with faint horizontal streaks
        const int size = 64;
        var pixels = new Color[size * size];
        var rnd = new Random(7);
        var rows = new float[size];
        for (int y = 0; y < size; y++) rows[y] = (float)rnd.NextDouble();
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float v = 0.86f + 0.09f * (float)rnd.NextDouble() + 0.05f * rows[y];
                byte b = (byte)(255 * Math.Clamp(v, 0f, 1f));
                pixels[y * size + x] = new Color(b, b, b);
            }
        }
        _grain = new Texture2D(_device, size, size);
        _grain.SetData(pixels);

        // Soft round particle
        const int dot = 32;
        var dots = new Color[dot * dot];
        for (int y = 0; y < dot; y++)
        {
            for (int x = 0; x < dot; x++)
            {
                float dx = (x + 0.5f) / dot * 2f - 1f, dy = (y + 0.5f) / dot * 2f - 1f;
                float a = Math.Clamp(1f - MathF.Sqrt(dx * dx + dy * dy), 0f, 1f);
                a = a * a * (3f - 2f * a);
                dots[y * dot + x] = new Color(a, a, a, a);
            }
        }
        _dot = new Texture2D(_device, dot, dot);
        _dot.SetData(dots);
    }

    // ---- Vertical scale ------------------------------------------------------------------
    // Depth is not to scale: the first 6 km (sediments, reservoirs) are magnified, the rest of
    // the crust and the mantle compressed, or the beds would be invisible next to the Moho.
    private float Unit => Size * 0.09f;
    private const float KmPerElevation = 4.4f;
    private const float BottomKm = 100f;

    private float DepthY(float km)
    {
        float u = Unit;
        return km <= 6f ? km / 6f * u : u + MathF.Min(km - 6f, BottomKm - 6f) / 74f * 1.6f * u;
    }

    /// <summary>Screen height of a depth below sea level (negative = above sea level).</summary>
    private float AbsY(float km) => km >= 0 ? -DepthY(km) : -km / KmPerElevation * 0.9f * Unit;

    private float ElevY(float elevation) => AbsY(-elevation * KmPerElevation);

    private float BottomY => -DepthY(BottomKm) - 0.05f;

    // ---------------------------------------------------------------------------------------
    // Snapshot
    // ---------------------------------------------------------------------------------------

    private void EnsureArrays()
    {
        if (_n == Size) return;
        _n = Size;
        int cells = _n * _n;
        _elev = new float[cells]; _uplift = new float[cells]; _depo = new float[cells];
        _activity = new float[cells]; _pressure = new float[cells]; _stress = new float[cells];
        _slab = new float[cells]; _velocity = new float[cells];
        _plate = new int[cells]; _boundary = new byte[cells]; _faultType = new byte[cells]; _flags = new byte[cells];
        _cellColor = new Color[cells];
        _columns = new StrataColumn[cells];
        for (int i = 0; i < cells; i++) _columns[i] = new StrataColumn();
        _bump = new float[cells]; _flash = new float[cells]; _erupting = new float[cells];
        _ash = new float[cells]; _lava = new float[cells];
        _lavaFronts.Clear();

        _g = _n * Sub + 1;
        int vertices = _g * _g;
        _baseY = new float[vertices]; _y = new float[vertices];
        _waveP = new float[vertices]; _waveS = new float[vertices]; _bumpV = new float[vertices];
        _vertexColor = new Color[vertices];
        _ashV = new float[vertices]; _lavaV = new float[vertices];
        _pendingSlip.Clear();
        _hasSnapshot = false;
    }

    private int MapX(int i) => ((OriginX + i) % _map.Width + _map.Width) % _map.Width;
    private int MapY(int j) => Math.Clamp(OriginY + j, 0, _map.Height - 1);

    private void TakeSnapshot()
    {
        int n = _n;
        for (int j = 0; j < n; j++)
        {
            for (int i = 0; i < n; i++)
            {
                int mx = MapX(i), my = MapY(j);
                var cell = _map.Cells[mx, my];
                var geo = cell.Geology;
                int c = j * n + i;
                _elev[c] = cell.Elevation;
                _uplift[c] = geo.UpliftRate;
                _depo[c] = geo.DepositionRate;
                _activity[c] = geo.VolcanicActivity;
                _pressure[c] = geo.MagmaPressure;
                _stress[c] = geo.SeismicStress;
                _plate[c] = geo.PlateId;
                _boundary[c] = (byte)geo.BoundaryType;
                _faultType[c] = (byte)geo.FaultType;
                byte flags = 0;
                if (cell.IsIce) flags |= FlagIce;
                if (geo.IsVolcano) flags |= FlagVolcano;
                if (geo.IsHotSpot) flags |= FlagHotSpot;
                if (geo.IsFault) flags |= FlagFault;
                if (geo.IsSubducting) flags |= FlagSubducting;
                if (geo.CrustType == CrustType.Oceanic) flags |= FlagOceanic;
                _flags[c] = flags;
                _cellColor[c] = TerrainColor(cell);
                Stratigraphy.Build(_map, _oil, mx, my, _columns[c]);
                if (_seismo != null) _velocity[c] = _seismo.VelocityP[my * _map.Width + mx];
            }
        }

        ComputeSlab();

        _cities.Clear();
        if (_civilizations != null)
        {
            foreach (var civ in _civilizations.Civilizations)
            {
                foreach (var city in civ.Cities)
                {
                    if (!ToRegion(city.X, city.Y, out int ci, out int cj)) continue;
                    _cities.Add(new CityModel { I = ci, J = cj, Id = city.Id, Population = city.Population, Type = city.Type, Style = city.Style, Name = city.Name });
                }
            }
        }

        // Smooth surface: bilinear between cell centres
        int g = _g;
        for (int vj = 0; vj < g; vj++)
        {
            for (int vi = 0; vi < g; vi++)
            {
                float fx = Math.Clamp(vi / (float)Sub - 0.5f, 0f, n - 1f);
                float fy = Math.Clamp(vj / (float)Sub - 0.5f, 0f, n - 1f);
                int x0 = (int)fx, y0 = (int)fy;
                int x1 = Math.Min(n - 1, x0 + 1), y1 = Math.Min(n - 1, y0 + 1);
                float tx = fx - x0, ty = fy - y0;
                float e = Lerp(Lerp(_elev[y0 * n + x0], _elev[y0 * n + x1], tx), Lerp(_elev[y1 * n + x0], _elev[y1 * n + x1], tx), ty);
                _baseY[vj * g + vi] = ElevY(e);
                _vertexColor[vj * g + vi] = Color.Lerp(
                    Color.Lerp(_cellColor[y0 * n + x0], _cellColor[y0 * n + x1], tx),
                    Color.Lerp(_cellColor[y1 * n + x0], _cellColor[y1 * n + x1], tx), ty);
            }
        }
        _hasSnapshot = true;
    }

    private static float Lerp(float a, float b, float t) => a + (b - a) * t;

    /// <summary>
    /// Depth of the top of a subducting slab under each cell: it starts at the trench and dips
    /// under the overriding plate.
    /// </summary>
    private void ComputeSlab()
    {
        int n = _n;
        Array.Clear(_slab);
        var queue = new Queue<int>();
        for (int c = 0; c < n * n; c++)
        {
            if ((_flags[c] & FlagSubducting) == 0) continue;
            _slab[c] = 9f;
            queue.Enqueue(c);
        }
        while (queue.Count > 0)
        {
            int c = queue.Dequeue();
            float next = _slab[c] + 14f;
            if (next > BottomKm - 6f) continue;
            int ci = c % n, cj = c / n;
            for (int dj = -1; dj <= 1; dj++)
            {
                for (int di = -1; di <= 1; di++)
                {
                    int i = ci + di, j = cj + dj;
                    if (i < 0 || j < 0 || i >= n || j >= n) continue;
                    int o = j * n + i;
                    // The slab goes under the other plate, not back under its own ocean floor
                    if (_slab[o] != 0f || (_flags[o] & FlagSubducting) != 0) continue;
                    if (_slab[c] <= 9f && _plate[o] == _plate[c]) continue;
                    _slab[o] = next;
                    queue.Enqueue(o);
                }
            }
        }
    }

    private static Color TerrainColor(TerrainCell cell)
    {
        float e = cell.Elevation;
        if (e < 0)
        {
            return Color.Lerp(new Color(196, 182, 136), new Color(52, 66, 92), Math.Clamp(-e * 1.6f, 0f, 1f));
        }

        float rain = Math.Clamp(cell.Rainfall, 0f, 1f);
        float biomass = Math.Clamp(cell.Biomass, 0f, 1f);
        float vegetation = Math.Clamp(biomass * 1.1f + rain * 0.5f - 0.15f, 0f, 1f);
        Color green = Color.Lerp(new Color(124, 158, 84), new Color(44, 104, 56), biomass);
        Color land = Color.Lerp(new Color(204, 178, 120), green, vegetation);
        if (e < 0.03f) land = Color.Lerp(new Color(222, 206, 156), land, e / 0.03f);

        float rock = Math.Clamp((e - 0.45f) / 0.35f, 0f, 1f);
        land = Color.Lerp(land, new Color(128, 118, 110), rock);

        float snow = Math.Clamp((e - 0.85f) / 0.2f, 0f, 1f);
        if (cell.Temperature < -8f) snow = MathF.Max(snow, Math.Clamp((-8f - cell.Temperature) / 10f, 0f, 1f));
        if (cell.IsIce) snow = 1f;
        land = Color.Lerp(land, new Color(240, 246, 252), snow);

        if (cell.Geology.IsVolcano) land = Color.Lerp(land, new Color(72, 56, 52), 0.75f);
        return land;
    }

    // ---------------------------------------------------------------------------------------
    // Events and transient effects
    // ---------------------------------------------------------------------------------------

    private bool ToRegion(int mapX, int mapY, out int i, out int j)
    {
        int dx = mapX - OriginX;
        dx = ((dx % _map.Width) + _map.Width) % _map.Width;
        i = dx;
        j = mapY - OriginY;
        return i >= 0 && i < _n && j >= 0 && j < _n;
    }

    private Vector3 CellTop(int i, int j, float lift = 0f)
    {
        int vi = Math.Clamp(i * Sub + Sub / 2 + 1, 0, _g - 1), vj = Math.Clamp(j * Sub + Sub / 2 + 1, 0, _g - 1);
        float y = _y.Length > 0 ? _y[vj * _g + vi] : 0f;
        return new Vector3(i + 0.5f - _n / 2f, y + lift, j + 0.5f - _n / 2f);
    }

    private void ProcessEvents()
    {
        foreach (var e in GeoEvents.Snapshot())
        {
            if (e.Id <= _lastEventId) continue;
            _lastEventId = e.Id;
            if (!ToRegion(e.X, e.Y, out int i, out int j)) continue;
            float unit = Unit;

            switch (e.Kind)
            {
                case GeoEventKind.Earthquake:
                {
                    string kind = e.Fault switch
                    {
                        FaultType.Thrust or FaultType.Reverse => "thrust",
                        FaultType.Normal => "normal",
                        FaultType.Strike_Slip => "strike-slip",
                        _ => "oblique"
                    };
                    if (e.Magnitude >= 3.5f)
                    {
                        AddTicker($"M{e.Magnitude:F1} earthquake  -  {kind} fault ruptures over {e.Rupture.Length * 40000 / _map.Width:N0} km",
                            e.Magnitude >= 6.5f ? UITheme.Bad : UITheme.Warn);
                    }

                    // The rupture front runs along the fault; each patch slips as it passes
                    float sign = e.Fault == FaultType.Normal ? -1f : e.Fault == FaultType.Strike_Slip ? 0.25f : 1f;
                    float slip = sign * Math.Clamp(0.05f * MathF.Pow(10f, (e.Magnitude - 5f) / 2.5f), 0.02f, 0.45f) * unit;
                    for (int k = 0; k < e.Rupture.Length; k++)
                    {
                        if (!ToRegion(e.Rupture[k].x, e.Rupture[k].y, out int ri, out int rj)) continue;
                        _pendingSlip.Add((rj * _n + ri, k * 0.07f, slip));
                    }
                    _rings.Add((CellTop(i, j, 0.05f), 0f, 3.5f, new Color(255, 210, 90), 3f + e.Magnitude));
                    break;
                }
                case GeoEventKind.Eruption:
                {
                    AddTicker($"{e.Eruption} eruption  -  VEI {e.Magnitude:F0}", new Color(255, 140, 60));
                    _erupting[j * _n + i] = 5f + e.Magnitude;
                    if (e.Eruption is EruptionType.Effusive or EruptionType.Strombolian) _lavaFronts.Add((j * _n + i, 0f, 0));
                    break;
                }
                case GeoEventKind.Impact:
                {
                    AddTicker($"Asteroid impact  -  crater {e.Magnitude * 10 * 40000 / _map.Width:N0} km wide", UITheme.Bad);
                    _impactFlash = 1f;
                    var centre = CellTop(i, j);
                    _rings.Add((centre + new Vector3(0, 0.05f, 0), 0f, 4f, new Color(255, 240, 200), 9f));
                    for (int k = 0; k < 260; k++)
                    {
                        double a = _random.NextDouble() * Math.PI * 2;
                        float speed = (0.4f + (float)_random.NextDouble() * 1.6f) * unit;
                        _particles.Add(new Particle
                        {
                            Position = centre,
                            Velocity = new Vector3((float)Math.Cos(a) * speed * 0.8f, speed * (0.6f + (float)_random.NextDouble()), (float)Math.Sin(a) * speed * 0.8f),
                            Life = 0f, MaxLife = 2.5f + (float)_random.NextDouble() * 2f, Size = 0.12f + 0.12f * (float)_random.NextDouble(),
                            Gravity = 1.4f * unit, Floor = BottomY, Color = k % 3 == 0 ? new Color(255, 190, 90) : new Color(120, 104, 92)
                        });
                    }
                    // The ground is punched down before the simulation's own crater shows
                    int radius = (int)MathF.Max(2, e.Magnitude * 2);
                    for (int dj = -radius; dj <= radius; dj++)
                    {
                        for (int di = -radius; di <= radius; di++)
                        {
                            int bi = i + di, bj = j + dj;
                            if (bi < 0 || bj < 0 || bi >= _n || bj >= _n) continue;
                            float d = MathF.Sqrt(di * di + dj * dj) / radius;
                            if (d > 1f) continue;
                            _bump[bj * _n + bi] -= (1f - d * d) * 0.5f * unit;
                        }
                    }
                    break;
                }
            }
        }
    }

    private void AddTicker(string text, Color color)
    {
        _ticker.Insert(0, (text, color, 0f));
        if (_ticker.Count > 5) _ticker.RemoveAt(_ticker.Count - 1);
    }

    private void UpdateEffects(float dt)
    {
        int n = _n;
        float unit = Unit;

        for (int k = _ticker.Count - 1; k >= 0; k--)
        {
            var t = _ticker[k];
            t.age += dt;
            if (t.age > 16f) _ticker.RemoveAt(k); else _ticker[k] = t;
        }

        for (int k = _pendingSlip.Count - 1; k >= 0; k--)
        {
            var s = _pendingSlip[k];
            s.delay -= dt;
            if (s.delay > 0f)
            {
                _pendingSlip[k] = s;
                continue;
            }
            _pendingSlip.RemoveAt(k);
            if (s.cell >= _bump.Length) continue;
            _bump[s.cell] += s.bump;
            _flash[s.cell] = 1f;
            var p = CellTop(s.cell % n, s.cell / n, 0.05f);
            for (int d = 0; d < 3; d++)
            {
                _particles.Add(new Particle
                {
                    Position = p, Velocity = new Vector3(Rand(-0.3f, 0.3f), Rand(0.2f, 0.6f), Rand(-0.3f, 0.3f)) * unit * 0.4f,
                    MaxLife = 1.6f, Size = 0.18f, Growth = 0.3f, Floor = BottomY, Color = new Color(170, 150, 120) * 0.6f
                });
            }
        }

        // Transient offsets relax (the lasting change comes from the simulation itself)
        float bumpDecay = MathF.Exp(-dt * 0.5f), flashDecay = MathF.Exp(-dt * 1.2f);
        float ashDecay = MathF.Exp(-dt / 45f), lavaCooling = MathF.Exp(-dt / 9f);
        for (int c = 0; c < _bump.Length; c++)
        {
            _bump[c] *= bumpDecay;
            _flash[c] *= flashDecay;
            _ash[c] *= ashDecay;
            _lava[c] *= lavaCooling;
            if (_erupting[c] <= 0f) continue;
            _erupting[c] -= dt;

            // Tephra falls out of the plume and blankets the ground, thickest near the vent
            int ci = c % n, cj = c / n;
            const int reach = 5;
            for (int dj = -reach; dj <= reach; dj++)
            {
                for (int di = -reach; di <= reach; di++)
                {
                    int oi = ci + di, oj = cj + dj;
                    if (oi < 0 || oj < 0 || oi >= n || oj >= n) continue;
                    float d = MathF.Sqrt(di * di + dj * dj);
                    if (d > reach) continue;
                    _ash[oj * n + oi] = MathF.Min(1f, _ash[oj * n + oi] + dt * 0.22f * (1f - d / reach) * (1f - d / reach));
                }
            }
            if (_random.NextDouble() < dt * 30f)
            {
                var from = CellTop(ci, cj, 2.2f * unit) + new Vector3(Rand(-reach, reach) * 0.7f, Rand(-0.4f, 0.4f) * unit, Rand(-reach, reach) * 0.7f);
                _particles.Add(new Particle
                {
                    Position = from, Velocity = new Vector3(0.1f, -0.5f * unit, 0.05f), MaxLife = 5f, Size = 0.06f,
                    Gravity = 0.25f * unit, Floor = ElevY(_elev[c]) - 0.5f * unit, Color = new Color(70, 66, 70)
                });
            }
        }

        // Lava runs downhill from the vent, one cell at a time, and cools behind the front
        for (int k = _lavaFronts.Count - 1; k >= 0; k--)
        {
            var front = _lavaFronts[k];
            if (front.cell >= _lava.Length) { _lavaFronts.RemoveAt(k); continue; }
            _lava[front.cell] = 1f;
            front.timer += dt;
            if (front.timer > 0.9f)
            {
                front.timer = 0f;
                int ci = front.cell % n, cj = front.cell / n, next = -1;
                float lowest = _elev[front.cell];
                for (int dj = -1; dj <= 1; dj++)
                {
                    for (int di = -1; di <= 1; di++)
                    {
                        int oi = ci + di, oj = cj + dj;
                        if (oi < 0 || oj < 0 || oi >= n || oj >= n) continue;
                        if (_elev[oj * n + oi] < lowest) { lowest = _elev[oj * n + oi]; next = oj * n + oi; }
                    }
                }
                front.steps++;
                if (next < 0 || front.steps > 6 || _elev[next] < -0.05f) { _lavaFronts.RemoveAt(k); continue; }
                front.cell = next;
            }
            _lavaFronts[k] = front;
        }

        for (int k = _rings.Count - 1; k >= 0; k--)
        {
            var r = _rings[k];
            r.age += dt;
            if (r.age > r.life) _rings.RemoveAt(k); else _rings[k] = r;
        }
        _impactFlash = MathF.Max(0f, _impactFlash - dt * 1.5f);
        if (_seismo != null && _seismo.IsPropagating) _seismogramHold = 10f; else _seismogramHold -= dt;

        if (!_hasSnapshot) return;

        // Volcanoes, sedimentation
        for (int j = 0; j < Math.Min(n, _cut); j++)
        {
            for (int i = 0; i < n; i++)
            {
                int c = j * n + i;
                if ((_flags[c] & FlagVolcano) != 0)
                {
                    var vent = CellTop(i, j, 0.05f);
                    if (_erupting[c] > 0f)
                    {
                        for (int k = 0; k < 3; k++)
                        {
                            _particles.Add(new Particle
                            {
                                Position = vent, Velocity = new Vector3(Rand(-0.25f, 0.25f), Rand(0.9f, 1.7f), Rand(-0.25f, 0.25f)) * unit * 0.6f,
                                MaxLife = Rand(2.5f, 4.5f), Size = 0.35f, Growth = 0.6f, Floor = BottomY,
                                Color = new Color(96, 90, 92) * 0.75f
                            });
                        }
                        _particles.Add(new Particle
                        {
                            Position = vent, Velocity = new Vector3(Rand(-0.7f, 0.7f), Rand(0.8f, 1.6f), Rand(-0.7f, 0.7f)) * unit * 0.5f,
                            MaxLife = Rand(0.9f, 1.6f), Size = 0.14f, Gravity = 1.6f * unit, Floor = BottomY, Color = new Color(255, 150, 40)
                        });
                    }
                    else if (_random.NextDouble() < dt * (0.6f + 2f * _pressure[c]))
                    {
                        _particles.Add(new Particle
                        {
                            Position = vent, Velocity = new Vector3(Rand(-0.08f, 0.08f), 0.35f, Rand(-0.08f, 0.08f)) * unit,
                            MaxLife = 2.5f, Size = 0.22f, Growth = 0.35f, Floor = BottomY, Color = new Color(220, 220, 226) * 0.35f
                        });
                    }
                }
                else if (_elev[c] < 0f && _depo[c] > 0.02f && _random.NextDouble() < dt * 6f * MathF.Min(1f, _depo[c] * 3f))
                {
                    // Sediment raining out of the water column onto the sea floor
                    _particles.Add(new Particle
                    {
                        Position = new Vector3(i + Rand(0f, 1f) - n / 2f, -0.02f, j + Rand(0f, 1f) - n / 2f),
                        Velocity = new Vector3(0, -0.22f * unit, 0), MaxLife = 6f, Size = 0.09f,
                        Floor = ElevY(_elev[c]), Color = new Color(214, 188, 130) * 0.9f
                    });
                }
            }
        }

        for (int k = _particles.Count - 1; k >= 0; k--)
        {
            var p = _particles[k];
            p.Life += dt;
            p.Velocity.Y -= p.Gravity * dt;
            p.Position += p.Velocity * dt;
            p.Size += p.Growth * dt;
            if (p.Life >= p.MaxLife || p.Position.Y < p.Floor) _particles.RemoveAt(k); else _particles[k] = p;
        }
        if (_particles.Count > 2500) _particles.RemoveRange(0, _particles.Count - 2500);
    }

    private float Rand(float a, float b) => a + (float)_random.NextDouble() * (b - a);

    // ---------------------------------------------------------------------------------------
    // Frame
    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// Renders the block into its off-screen target. Call before the back buffer is cleared.
    /// </summary>
    public void PreDraw(SpriteBatch sb, Rectangle mapArea, object mapLock, float deltaTime)
    {
        if (!IsOpen) return;
        _panel = PanelRect(mapArea);
        if (_panel.Width < 64 || _panel.Height < 64) return;

        EnsureArrays();
        _cut = Math.Clamp(_cut, 2, Size);

        _snapshotTimer -= deltaTime;
        if (_snapshotTimer <= 0f || !_hasSnapshot)
        {
            if (Monitor.TryEnter(mapLock, _hasSnapshot ? 2 : 80))
            {
                try
                {
                    TakeSnapshot();
                    _snapshotTimer = 0.25f;
                }
                catch (Exception)
                {
                    // The simulation changed a list mid-read; try again next frame
                }
                finally
                {
                    Monitor.Exit(mapLock);
                }
            }
        }
        if (!_hasSnapshot) return;

        ProcessEvents();
        UpdateEffects(deltaTime);
        UpdateSurfaceHeights();

        int width = Math.Min(2600, (int)(_panel.Width * SuperSample)), height = (int)(width * (float)_panel.Height / _panel.Width);
        if (_target == null || _target.Width != width || _target.Height != height)
        {
            _target?.Dispose();
            _target = new RenderTarget2D(_device, width, height, false, SurfaceFormat.Color, DepthFormat.Depth24);
        }

        // Camera: orthographic, orbiting the block
        float unit = Unit;
        var target = new Vector3(0f, -1.0f * unit, (_cut - Size) * 0.25f);
        var direction = new Vector3(MathF.Cos(_pitch) * MathF.Sin(_yaw), MathF.Sin(_pitch), MathF.Cos(_pitch) * MathF.Cos(_yaw));
        var view = Matrix.CreateLookAt(target + direction * 200f, target, Vector3.Up);
        float viewWidth = Size * 1.95f / _zoom;
        var projection = Matrix.CreateOrthographic(viewWidth, viewWidth * height / width, 1f, 500f);
        _viewProjection = view * projection;

        _device.SetRenderTarget(_target);
        _device.Clear(ClearOptions.Target | ClearOptions.DepthBuffer, new Color(9, 13, 22), 1f, 0);

        sb.Begin();
        UITheme.FillGradient(sb, new Rectangle(0, 0, width, height), new Color(30, 44, 70));
        if (_impactFlash > 0f) UITheme.FillRect(sb, new Rectangle(0, 0, width, height), new Color(255, 236, 200) * (_impactFlash * 0.5f));
        sb.End();

        _device.DepthStencilState = DepthStencilState.Default;
        _device.RasterizerState = RasterizerState.CullNone;
        _device.SamplerStates[0] = SamplerState.LinearWrap;
        _effect.World = Matrix.Identity;
        _effect.View = view;
        _effect.Projection = projection;

        _labels.Clear();
        _count = 0;

        // Opaque: landscape and the four section faces
        BuildSurface();
        BuildFaces();
        Flush(_grain, BlendState.Opaque, true);

        BuildCities();
        Flush(_white, BlendState.Opaque, true);

        // Translucent overlays drawn over the solid block
        BuildFaceOverlays();
        BuildSurfaceOverlays();
        BuildWater();
        Flush(_white, BlendState.NonPremultiplied, false);

        BuildParticles(view);
        Flush(_dot, BlendState.AlphaBlend, false);

        _device.SetRenderTarget(null);
        _device.DepthStencilState = DepthStencilState.None;
        _device.SamplerStates[0] = SamplerState.PointClamp;
    }

    private bool TryProject(Vector3 world, out Vector2 screen)
    {
        var p = Vector3.Transform(world, _viewProjection);
        screen = new Vector2(_panel.X + (p.X * 0.5f + 0.5f) * _panel.Width, _panel.Y + (1f - (p.Y * 0.5f + 0.5f)) * _panel.Height);
        return p.X > -1.05f && p.X < 1.05f && p.Y > -1.05f && p.Y < 1.05f;
    }

    // ---- Vertex batching -----------------------------------------------------------------
    private readonly VertexPositionColorTexture[] _vertices = new VertexPositionColorTexture[60000];
    private int _count;
    private Texture2D? _batchTexture;
    private BlendState _batchBlend = BlendState.Opaque;
    private bool _batchDepthWrite = true;

    private void Flush(Texture2D texture, BlendState blend, bool depthWrite)
    {
        _batchTexture = texture;
        _batchBlend = blend;
        _batchDepthWrite = depthWrite;
        FlushNow();
    }

    private void FlushNow()
    {
        if (_count < 3 || _batchTexture == null)
        {
            _count = 0;
            return;
        }
        _device.BlendState = _batchBlend;
        _device.DepthStencilState = _batchDepthWrite ? DepthStencilState.Default : DepthStencilState.DepthRead;
        _effect.Texture = _batchTexture;
        foreach (var pass in _effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            _device.DrawUserPrimitives(PrimitiveType.TriangleList, _vertices, 0, _count / 3);
        }
        _count = 0;
    }

    private void Reserve(int vertices)
    {
        if (_count + vertices > _vertices.Length)
        {
            // Mid-pass overflow: draw what we have with the state of the pass in progress
            var texture = _batchTexture ?? _grain;
            _batchTexture = _pendingTexture ?? texture;
            _batchBlend = _pendingBlend;
            _batchDepthWrite = _pendingDepthWrite;
            FlushNow();
        }
    }

    // State the geometry currently being added will be flushed with (for overflow flushes)
    private Texture2D? _pendingTexture;
    private BlendState _pendingBlend = BlendState.Opaque;
    private bool _pendingDepthWrite = true;

    private void Pass(Texture2D texture, BlendState blend, bool depthWrite)
    {
        _pendingTexture = texture;
        _pendingBlend = blend;
        _pendingDepthWrite = depthWrite;
    }

    private void Tri(Vector3 a, Vector3 b, Vector3 c, Color ca, Color cb, Color cc, Vector2 ta, Vector2 tb, Vector2 tc)
    {
        Reserve(3);
        _vertices[_count++] = new VertexPositionColorTexture(a, ca, ta);
        _vertices[_count++] = new VertexPositionColorTexture(b, cb, tb);
        _vertices[_count++] = new VertexPositionColorTexture(c, cc, tc);
    }

    private void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color ca, Color cb, Color cc, Color cd, Vector2 ta, Vector2 tb, Vector2 tc, Vector2 td)
    {
        Reserve(6);
        _vertices[_count++] = new VertexPositionColorTexture(a, ca, ta);
        _vertices[_count++] = new VertexPositionColorTexture(b, cb, tb);
        _vertices[_count++] = new VertexPositionColorTexture(c, cc, tc);
        _vertices[_count++] = new VertexPositionColorTexture(a, ca, ta);
        _vertices[_count++] = new VertexPositionColorTexture(c, cc, tc);
        _vertices[_count++] = new VertexPositionColorTexture(d, cd, td);
    }

    private void FlatQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color) =>
        Quad(a, b, c, d, color, color, color, color, Vector2.Zero, Vector2.UnitX, Vector2.One, Vector2.UnitY);

    /// <summary>A flat ribbon between two points, lying in the ground plane.</summary>
    private void Ribbon(Vector3 a, Vector3 b, float width, Color color)
    {
        var along = b - a;
        var side = new Vector3(-along.Z, 0, along.X);
        if (side.LengthSquared() < 1e-8f) side = new Vector3(1, 0, 0);
        side = Vector3.Normalize(side) * (width * 0.5f);
        FlatQuad(a - side, a + side, b + side, b - side, color);
    }

    /// <summary>A thin line that faces the camera whatever its direction.</summary>
    private void Line(Vector3 a, Vector3 b, float width, Color color)
    {
        var toCamera = new Vector3(MathF.Cos(_pitch) * MathF.Sin(_yaw), MathF.Sin(_pitch), MathF.Cos(_pitch) * MathF.Cos(_yaw));
        var side = Vector3.Cross(b - a, toCamera);
        if (side.LengthSquared() < 1e-8f) return;
        side = Vector3.Normalize(side) * (width * 0.5f);
        FlatQuad(a - side, a + side, b + side, b - side, color);
    }

    private static Color Shade(Color c, float f) =>
        new Color((byte)Math.Clamp(c.R * f, 0, 255), (byte)Math.Clamp(c.G * f, 0, 255), (byte)Math.Clamp(c.B * f, 0, 255), c.A);

    // ---------------------------------------------------------------------------------------
    // Landscape
    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// Adds the moving part of the ground to the resting surface: seismic wave fields sampled
    /// from the wave solver and the slip and crater offsets of recent events.
    /// </summary>
    private void UpdateSurfaceHeights()
    {
        int g = _g, n = _n;
        float unit = Unit;
        Array.Copy(_baseY, _y, _y.Length);

        // Slip and crater offsets (per cell, smoothed to the vertices)
        for (int vj = 0; vj < g; vj++)
        {
            float fy = Math.Clamp(vj / (float)Sub - 0.5f, 0f, n - 1f);
            int y0 = (int)fy, y1 = Math.Min(n - 1, y0 + 1);
            float ty = fy - y0;
            for (int vi = 0; vi < g; vi++)
            {
                float fx = Math.Clamp(vi / (float)Sub - 0.5f, 0f, n - 1f);
                int x0 = (int)fx, x1 = Math.Min(n - 1, x0 + 1);
                float tx = fx - x0;
                _bumpV[vj * g + vi] = Lerp(Lerp(_bump[y0 * n + x0], _bump[y0 * n + x1], tx), Lerp(_bump[y1 * n + x0], _bump[y1 * n + x1], tx), ty);
                _ashV[vj * g + vi] = Lerp(Lerp(_ash[y0 * n + x0], _ash[y0 * n + x1], tx), Lerp(_ash[y1 * n + x0], _ash[y1 * n + x1], tx), ty);
                _lavaV[vj * g + vi] = Lerp(Lerp(_lava[y0 * n + x0], _lava[y0 * n + x1], tx), Lerp(_lava[y1 * n + x0], _lava[y1 * n + x1], tx), ty);
            }
        }
        if (_showWaves) ComputeBackend.AddScaled(_y, _bumpV, 1f);
        ComputeBackend.AddScaled(_y, _ashV, 0.05f * unit);   // The ash blanket has thickness
        ComputeBackend.AddScaled(_y, _lavaV, 0.03f * unit);

        Array.Clear(_waveP);
        Array.Clear(_waveS);
        var fieldP = _seismo?.FieldP;
        var fieldS = _seismo?.FieldS;
        if (_showWaves && _seismo != null && _seismo.IsPropagating && fieldP != null && fieldS != null)
        {
            int w = _map.Width, h = _map.Height;
            // Amplitudes span orders of magnitude: compress so the far field still shows
            float reference = 1f / MathF.Max(1e-6f, _seismo.EventAmplitude * 0.04f);
            for (int vj = 0; vj < g; vj++)
            {
                float my = Math.Clamp(OriginY + vj / (float)Sub - 0.5f, 0f, h - 1.001f);
                int y0 = (int)my;
                float ty = my - y0;
                for (int vi = 0; vi < g; vi++)
                {
                    float mx = OriginX + vi / (float)Sub - 0.5f;
                    mx = ((mx % w) + w) % w;
                    int x0 = (int)mx, x1 = (x0 + 1) % w;
                    float tx = mx - x0;
                    int a = y0 * w + x0, b = y0 * w + x1, c = (y0 + 1) * w + x0, d = (y0 + 1) * w + x1;
                    float p = Lerp(Lerp(fieldP[a], fieldP[b], tx), Lerp(fieldP[c], fieldP[d], tx), ty) * reference;
                    float s = Lerp(Lerp(fieldS[a], fieldS[b], tx), Lerp(fieldS[c], fieldS[d], tx), ty) * reference;
                    _waveP[vj * g + vi] = p / (1f + MathF.Abs(p));
                    _waveS[vj * g + vi] = s / (1f + MathF.Abs(s));
                }
            }
            // P waves compress, S waves shear and shake harder
            ComputeBackend.AddScaled(_y, _waveP, 0.10f * unit);
            ComputeBackend.AddScaled(_y, _waveS, 0.20f * unit);
        }
    }

    private void BuildSurface()
    {
        Pass(_grain, BlendState.Opaque, true);
        int g = _g, n = _n;
        int rows = Math.Min(g - 1, _cut * Sub);
        float half = n / 2f, step = 1f / Sub;
        var light = Vector3.Normalize(new Vector3(-0.45f, 0.8f, -0.35f));

        for (int vj = 0; vj < rows; vj++)
        {
            for (int vi = 0; vi < g - 1; vi++)
            {
                int a = vj * g + vi, b = a + 1, c = a + g + 1, d = a + g;
                float x0 = vi * step - half, x1 = x0 + step, z0 = vj * step - half, z1 = z0 + step;

                // One normal per quad is enough at this density
                var normal = Vector3.Normalize(new Vector3(_y[a] - _y[b] + _y[d] - _y[c], 2f * step, _y[a] - _y[d] + _y[b] - _y[c]));
                float lit = 0.42f + 0.8f * MathF.Max(0f, Vector3.Dot(normal, light));

                Quad(new Vector3(x0, _y[a], z0), new Vector3(x1, _y[b], z0), new Vector3(x1, _y[c], z1), new Vector3(x0, _y[d], z1),
                    SurfaceColor(a, lit), SurfaceColor(b, lit), SurfaceColor(c, lit), SurfaceColor(d, lit),
                    new Vector2(x0, z0) * 0.6f, new Vector2(x1, z0) * 0.6f, new Vector2(x1, z1) * 0.6f, new Vector2(x0, z1) * 0.6f);
            }
        }
    }

    private Color SurfaceColor(int vertex, float lit)
    {
        var c = _vertexColor[vertex];
        float ash = _ashV[vertex], lava = _lavaV[vertex];
        if (ash > 0.01f) c = Color.Lerp(c, new Color(104, 100, 104), MathF.Min(0.9f, ash * 1.6f));
        c = Shade(c, lit);
        if (lava > 0.03f)
        {
            // Glowing while hot, black basalt once cooled
            Color rock = lava > 0.45f ? Color.Lerp(new Color(200, 50, 10), new Color(255, 210, 80), (lava - 0.45f) / 0.55f)
                                      : Color.Lerp(new Color(40, 34, 36), new Color(200, 50, 10), lava / 0.45f);
            c = Color.Lerp(c, rock, MathF.Min(1f, lava * 3f));
        }
        float p = MathF.Abs(_waveP[vertex]), s = MathF.Abs(_waveS[vertex]);
        if (p > 0.02f) c = Color.Lerp(c, new Color(120, 210, 255), MathF.Min(0.4f, p * 0.9f));   // P front
        if (s > 0.02f) c = Color.Lerp(c, new Color(255, 110, 90), MathF.Min(0.45f, s * 0.9f));   // S front
        return c;
    }

    private void BuildWater()
    {
        Pass(_white, BlendState.NonPremultiplied, false);
        int g = _g, n = _n;
        int rows = Math.Min(g - 1, _cut * Sub);
        float half = n / 2f, step = 1f / Sub;

        for (int vj = 0; vj < rows; vj++)
        {
            for (int vi = 0; vi < g - 1; vi++)
            {
                int a = vj * g + vi, b = a + 1, c = a + g + 1, d = a + g;
                float lowest = MathF.Min(MathF.Min(_baseY[a], _baseY[b]), MathF.Min(_baseY[c], _baseY[d]));
                if (lowest >= 0f) continue;
                float x0 = vi * step - half, x1 = x0 + step, z0 = vj * step - half, z1 = z0 + step;
                int cell = Math.Min(n - 1, vj / Sub) * n + Math.Min(n - 1, vi / Sub);
                bool ice = (_flags[cell] & FlagIce) != 0;

                float depth = Math.Clamp(-lowest / Unit, 0f, 1f);
                Color color = ice
                    ? new Color(232, 240, 248, 235)
                    : Color.Lerp(new Color(70, 150, 200, 110), new Color(18, 60, 130, 190), depth);
                float ripple = ice ? 0f : 0.012f * Unit;
                float ya = ripple * MathF.Sin(_time * 1.6f + x0 * 1.3f + z0), yb = ripple * MathF.Sin(_time * 1.6f + x1 * 1.3f + z0);
                float yc = ripple * MathF.Sin(_time * 1.6f + x1 * 1.3f + z1), yd = ripple * MathF.Sin(_time * 1.6f + x0 * 1.3f + z1);
                FlatQuad(new Vector3(x0, ya, z0), new Vector3(x1, yb, z0), new Vector3(x1, yc, z1), new Vector3(x0, yd, z1), color);
            }
        }
    }

    // ---------------------------------------------------------------------------------------
    // Section faces
    // ---------------------------------------------------------------------------------------

    private enum Side { South, East, North, West }

    private int SideLength(Side side) => side is Side.South or Side.North ? _n : Math.Min(_n, _cut);

    /// <summary>Cell on a face at position k along it.</summary>
    private int SideCell(Side side, int k)
    {
        int n = _n;
        k = Math.Clamp(k, 0, SideLength(side) - 1);
        return side switch
        {
            Side.South => (Math.Min(n, _cut) - 1) * n + k,
            Side.North => k,
            Side.East => k * n + (n - 1),
            _ => k * n
        };
    }

    private Vector3 SidePoint(Side side, float u, float y, float outward = 0f)
    {
        float half = _n / 2f;
        return side switch
        {
            Side.South => new Vector3(u - half, y, Math.Min(_n, _cut) - half + outward),
            Side.North => new Vector3(u - half, y, -half - outward),
            Side.East => new Vector3(half + outward, y, u - half),
            _ => new Vector3(-half - outward, y, u - half)
        };
    }

    private int SideVertex(Side side, int s)
    {
        int g = _g;
        return side switch
        {
            Side.South => Math.Min(g - 1, _cut * Sub) * g + s,
            Side.North => s,
            Side.East => s * g + (g - 1),
            _ => s * g
        };
    }

    private static float SideShade(Side side) => side switch
    {
        Side.South => 0.95f,
        Side.East => 0.80f,
        Side.North => 0.62f,
        _ => 0.70f
    };

    // Interfaces of a column on a face: surface, bed tops, basement, Moho, bottom
    private const int Interfaces = StrataColumn.Beds + 3;
    private readonly float[] _ya = new float[Interfaces + 1], _yb = new float[Interfaces + 1];

    /// <summary>
    /// Heights of the layer boundaries at a point along a face, blending the two nearest
    /// columns so the beds run smoothly from cell to cell.
    /// </summary>
    private void FaceColumn(Side side, int s, float[] ys, out int cellA, out int cellB, out float t)
    {
        float u = s / (float)Sub - 0.5f;
        int length = SideLength(side);
        u = Math.Clamp(u, 0f, length - 1f);
        int k = (int)u;
        t = u - k;
        cellA = SideCell(side, k);
        cellB = SideCell(side, k + 1);
        var a = _columns[cellA];
        var b = _columns[cellB];
        float surfaceKm = Lerp(-_elev[cellA] * KmPerElevation, -_elev[cellB] * KmPerElevation, t);

        ys[0] = _y[SideVertex(side, s)];
        for (int i = 1; i <= StrataColumn.Beds; i++)
        {
            ys[i] = MathF.Min(ys[0], AbsY(surfaceKm + Lerp(a.BedTop[i], b.BedTop[i], t)));
        }
        float moho = MathF.Max(Lerp(a.CrustKm, b.CrustKm, t), Lerp(a.SedimentKm, b.SedimentKm, t) + 2f);
        ys[StrataColumn.Beds + 1] = AbsY(surfaceKm + moho);
        ys[StrataColumn.Beds + 2] = BottomY;
        // Mountain roots can reach below the block: keep the order
        if (ys[StrataColumn.Beds + 1] < BottomY + 0.1f) ys[StrataColumn.Beds + 1] = BottomY + 0.1f;
    }

    private Color LayerColor(int cell, int layer, bool top)
    {
        var column = _columns[cell];
        if (layer < StrataColumn.Beds)
        {
            var color = BedColor(column.BedKind[layer]);
            // Alternate beds read as bands; deeper beds are a little darker
            return Shade(color, (layer % 2 == 0 ? 1.0f : 0.9f) * (1f - 0.012f * layer));
        }
        if (layer == StrataColumn.Beds)
        {
            if (_tomoMode != 0) return TomographyColor(cell);
            var crust = (_flags[cell] & FlagOceanic) != 0 ? BasaltColor : GraniteColor;
            return Shade(crust, top ? 1f : 0.62f);
        }
        return top ? MantleTop : MantleBottom;
    }

    private Color TomographyColor(int cell)
    {
        if (_seismo == null) return new Color(70, 74, 84);
        if (_tomoMode == 2) return VelocityColor((_velocity[cell] - 6.6f) / 1.2f);

        int mx = MapX(cell % _n), my = MapY(cell / _n);
        int block = (my / SeismologyManager.TomoBlock) * _seismo.TomoWidth + mx / SeismologyManager.TomoBlock;
        if (block < 0 || block >= _seismo.TomoVelocity.Length || _seismo.TomoCoverage[block] <= 0f)
        {
            return new Color(70, 74, 84); // No ray has crossed this block yet
        }
        return VelocityColor((_seismo.TomoVelocity[block] - _seismo.TomoReference) / 1.2f);
    }

    private void BuildFaces()
    {
        Pass(_grain, BlendState.Opaque, true);
        foreach (Side side in new[] { Side.North, Side.West, Side.East, Side.South })
        {
            int segments = SideLength(side) * Sub;
            float shade = SideShade(side);
            FaceColumn(side, 0, _ya, out int a0, out int a1, out float ta);

            for (int s = 0; s < segments; s++)
            {
                FaceColumn(side, s + 1, _yb, out int b0, out int b1, out float tb);
                float ua = s / (float)Sub, ub = (s + 1) / (float)Sub;

                for (int layer = 0; layer < Interfaces - 1; layer++)
                {
                    float topA = _ya[layer], bottomA = _ya[layer + 1], topB = _yb[layer], bottomB = _yb[layer + 1];
                    if (topA - bottomA < 0.0005f && topB - bottomB < 0.0005f) continue;

                    Color topLeft = Shade(Color.Lerp(LayerColor(a0, layer, true), LayerColor(a1, layer, true), ta), shade);
                    Color topRight = Shade(Color.Lerp(LayerColor(b0, layer, true), LayerColor(b1, layer, true), tb), shade);
                    Color bottomLeft = Shade(Color.Lerp(LayerColor(a0, layer, false), LayerColor(a1, layer, false), ta), shade);
                    Color bottomRight = Shade(Color.Lerp(LayerColor(b0, layer, false), LayerColor(b1, layer, false), tb), shade);

                    Quad(SidePoint(side, ua, topA), SidePoint(side, ub, topB), SidePoint(side, ub, bottomB), SidePoint(side, ua, bottomA),
                        topLeft, topRight, bottomRight, bottomLeft,
                        new Vector2(ua * 0.5f, topA * 2.2f), new Vector2(ub * 0.5f, topB * 2.2f), new Vector2(ub * 0.5f, bottomB * 2.2f), new Vector2(ua * 0.5f, bottomA * 2.2f));
                }

                Array.Copy(_yb, _ya, _ya.Length);
                a0 = b0; a1 = b1; ta = tb;
            }
        }

        // Underside
        float half = _n / 2f, front = Math.Min(_n, _cut) - half;
        FlatQuad(new Vector3(-half, BottomY, -half), new Vector3(half, BottomY, -half), new Vector3(half, BottomY, front), new Vector3(-half, BottomY, front), Shade(MantleBottom, 0.5f));
    }

    /// <summary>
    /// Everything drawn on top of the section faces: sea water, bed boundaries, reservoirs,
    /// salt, slabs, magma, faults and boreholes.
    /// </summary>
    private void BuildFaceOverlays()
    {
        Pass(_white, BlendState.NonPremultiplied, false);
        float unit = Unit;
        const float skin = 0.03f;

        foreach (Side side in new[] { Side.North, Side.West, Side.East, Side.South })
        {
            int length = SideLength(side);
            int segments = length * Sub;
            bool front = side is Side.South or Side.East;
            FaceColumn(side, 0, _ya, out int a0, out int a1, out float ta);

            for (int s = 0; s < segments; s++)
            {
                FaceColumn(side, s + 1, _yb, out int b0, out int b1, out float tb);
                float ua = s / (float)Sub, ub = (s + 1) / (float)Sub;

                // Sea
                if (_ya[0] < 0f || _yb[0] < 0f)
                {
                    var sea = new Color(30, 90, 160, 150);
                    FlatQuad(SidePoint(side, ua, 0f, skin), SidePoint(side, ub, 0f, skin), SidePoint(side, ub, MathF.Min(0f, _yb[0]), skin), SidePoint(side, ua, MathF.Min(0f, _ya[0]), skin), sea);
                }

                // Bed boundaries, the basement top and the Moho as thin lines
                for (int i = 1; i <= StrataColumn.Beds + 1; i++)
                {
                    bool major = i >= StrataColumn.Beds;
                    float w = (major ? 0.022f : 0.008f) * unit;
                    Color line = i == StrataColumn.Beds + 1 ? new Color(255, 236, 170, 230) : major ? new Color(20, 16, 14, 220) : new Color(30, 22, 16, 110);
                    if (!major && _ya[i - 1] - _ya[i] < 0.01f * unit) continue;
                    FlatQuad(SidePoint(side, ua, _ya[i] + w, skin), SidePoint(side, ub, _yb[i] + w, skin), SidePoint(side, ub, _yb[i] - w, skin), SidePoint(side, ua, _ya[i] - w, skin), line);
                }

                // Fresh ash and lava from an eruption in progress: a new bed on top of the column
                float ashA = Lerp(_ash[a0], _ash[a1], ta) + Lerp(_lava[a0], _lava[a1], ta);
                float ashB = Lerp(_ash[b0], _ash[b1], tb) + Lerp(_lava[b0], _lava[b1], tb);
                if (ashA > 0.03f || ashB > 0.03f)
                {
                    Color fresh = new Color(120, 60, 70, 250);
                    FlatQuad(SidePoint(side, ua, _ya[0], skin * 1.2f), SidePoint(side, ub, _yb[0], skin * 1.2f),
                        SidePoint(side, ub, _yb[0] - MathF.Min(1f, ashB) * 0.07f * unit, skin * 1.2f), SidePoint(side, ua, _ya[0] - MathF.Min(1f, ashA) * 0.07f * unit, skin * 1.2f), fresh);
                }

                DrawBand(side, ua, ub, a0, a1, ta, b0, b1, tb, skin * 1.5f, BandKind.Salt);
                DrawBand(side, ua, ub, a0, a1, ta, b0, b1, tb, skin * 1.8f, BandKind.Unconformity);
                DrawBand(side, ua, ub, a0, a1, ta, b0, b1, tb, skin * 2f, BandKind.Brine);
                DrawBand(side, ua, ub, a0, a1, ta, b0, b1, tb, skin * 2.5f, BandKind.Hydrocarbon);
                DrawBand(side, ua, ub, a0, a1, ta, b0, b1, tb, skin * 2f, BandKind.Slab);

                Array.Copy(_yb, _ya, _ya.Length);
                a0 = b0; a1 = b1; ta = tb;
            }

            // Per-cell features
            for (int k = 0; k < length; k++)
            {
                int cell = SideCell(side, k);
                var column = _columns[cell];
                float u = k + 0.5f;
                float surfaceKm = -_elev[cell] * KmPerElevation;
                float top = _y[SideVertex(side, Math.Min(k * Sub + Sub / 2 + 1, SideLength(side) * Sub))];

                // Faults take the shape of their tectonic setting
                if ((_flags[cell] & FlagFault) != 0)
                {
                    DrawFaultOnFace(side, k, cell, top, surfaceKm, skin, front);
                }

                // Accretionary wedge: sediment scraped off the down-going plate, stacked in slices
                if ((_flags[cell] & FlagSubducting) != 0 && _elev[cell] < -0.3f)
                {
                    int toward = SlabDirection(side, k);
                    if (toward != 0)
                    {
                        int next = SideCell(side, k + toward * 2);
                        float tipY = top;
                        float backTop = _y[SideVertex(side, Math.Clamp((k + toward * 2) * Sub + Sub / 2 + 1, 0, SideLength(side) * Sub))];
                        float backBase = AbsY(-_elev[next] * KmPerElevation + MathF.Max(8f, _slab[next]));
                        Color wedge = new Color(150, 128, 96, 235);
                        Tri(SidePoint(side, u, tipY, skin * 2.2f), SidePoint(side, u + toward * 2f, backTop, skin * 2.2f), SidePoint(side, u + toward * 2f, backBase, skin * 2.2f),
                            wedge, wedge, Shade(wedge, 0.7f), Vector2.Zero, Vector2.Zero, Vector2.Zero);
                        for (int slice = 1; slice <= 3; slice++)
                        {
                            float f = slice / 4f;
                            Line(SidePoint(side, u + toward * 2f * f, Lerp(tipY, backTop, f), skin * 2.6f),
                                 SidePoint(side, u + toward * 2f * (f + 0.22f), Lerp(tipY, backBase, f + 0.22f), skin * 2.6f), 0.02f * unit, new Color(60, 30, 24, 230));
                        }
                        if (front) _labels.Add((SidePoint(side, u + toward * 1.4f, Lerp(backTop, backBase, 0.45f), 0.1f), "Accretionary wedge", new Color(214, 188, 130), 7));
                    }
                }

                // Magma: chamber under a volcano, plume under a hot spot
                if ((_flags[cell] & FlagVolcano) != 0)
                {
                    float pulse = 0.75f + 0.25f * MathF.Sin(_time * 3f + cell);
                    float pressure = Math.Clamp(_pressure[cell], 0f, 1.2f);
                    float chamberTop = AbsY(surfaceKm + 3.5f), chamberBottom = AbsY(surfaceKm + 9f + 3f * pressure);
                    float centreY = (chamberTop + chamberBottom) * 0.5f, radiusY = (chamberTop - chamberBottom) * 0.5f, radiusX = 0.55f + 0.35f * pressure;
                    Color hot = Color.Lerp(new Color(200, 60, 20, 230), new Color(255, 200, 70, 255), pressure * pulse);

                    if ((_flags[cell] & FlagHotSpot) != 0)
                    {
                        Color plume = new Color(255, 120, 40, 120);
                        FlatQuad(SidePoint(side, u - 1.3f, BottomY, skin * 3f), SidePoint(side, u + 1.3f, BottomY, skin * 3f),
                            SidePoint(side, u + 0.4f, centreY, skin * 3f), SidePoint(side, u - 0.4f, centreY, skin * 3f), plume);
                        if (front) _labels.Add((SidePoint(side, u, Lerp(BottomY, centreY, 0.45f), 0.1f), "Mantle plume", MagmaColor, 6));
                    }
                    else if (_slab[cell] > 0f)
                    {
                        // Water driven off the slab melts the mantle above it; the melt rises to the arc
                        float source = AbsY(MathF.Min(BottomKm - 4f, surfaceKm + _slab[cell]));
                        FlatQuad(SidePoint(side, u - 0.18f, source, skin * 3f), SidePoint(side, u + 0.18f, source, skin * 3f),
                            SidePoint(side, u + 0.1f, centreY, skin * 3f), SidePoint(side, u - 0.1f, centreY, skin * 3f), new Color(255, 130, 40, 150));
                    }

                    // Conduit and chamber
                    FlatQuad(SidePoint(side, u - 0.07f, top, skin * 3.5f), SidePoint(side, u + 0.07f, top, skin * 3.5f),
                        SidePoint(side, u + 0.1f, centreY, skin * 3.5f), SidePoint(side, u - 0.1f, centreY, skin * 3.5f), hot);
                    const int fan = 18;
                    var centre = SidePoint(side, u, centreY, skin * 4f);
                    for (int f = 0; f < fan; f++)
                    {
                        float aa = f * MathHelper.TwoPi / fan, ab = (f + 1) * MathHelper.TwoPi / fan;
                        Tri(centre, SidePoint(side, u + MathF.Cos(aa) * radiusX, centreY + MathF.Sin(aa) * radiusY, skin * 4f),
                            SidePoint(side, u + MathF.Cos(ab) * radiusX, centreY + MathF.Sin(ab) * radiusY, skin * 4f),
                            Color.Lerp(hot, Color.White, 0.25f), Shade(hot, 0.7f), Shade(hot, 0.7f), Vector2.Zero, Vector2.Zero, Vector2.Zero);
                    }
                    if (front) _labels.Add((SidePoint(side, u + radiusX + 0.2f, centreY, 0.1f), "Magma chamber", MagmaColor, 7));
                }

                if (front && column.HasReservoir && (k == 0 || !_columns[SideCell(side, k - 1)].HasReservoir))
                {
                    string fluid = column.ReservoirFluid == PoreFluid.Gas ? "Gas" : "Oil";
                    _labels.Add((SidePoint(side, u + 0.6f, AbsY(surfaceKm + column.ReservoirTop) + 0.12f * unit, 0.1f), $"{fluid} field  {column.ReservoirTop:F1} km", GasColor, 8));
                }
                if (front && column.HasSalt && (k == 0 || !_columns[SideCell(side, k - 1)].HasSalt))
                {
                    _labels.Add((SidePoint(side, u, AbsY(surfaceKm + (column.SaltTop + column.SedimentKm) * 0.5f), 0.1f), "Salt dome", Color.White, 5));
                }
            }

            if (front) AddStructureLabels(side);
        }

        BuildBoreholes(skin);
    }

    private enum BandKind { Hydrocarbon, Brine, Salt, Slab, Unconformity }

    /// <summary>A band between two depths on a face, where both neighbouring columns have it.</summary>
    private void DrawBand(Side side, float ua, float ub, int a0, int a1, float ta, int b0, int b1, float tb, float outward, BandKind kind)
    {
        if (!BandAt(a0, a1, ta, kind, out float topA, out float bottomA, out Color color)) return;
        if (!BandAt(b0, b1, tb, kind, out float topB, out float bottomB, out _)) return;
        if (topA - bottomA < 0.0005f && topB - bottomB < 0.0005f) return;
        FlatQuad(SidePoint(side, ua, topA, outward), SidePoint(side, ub, topB, outward), SidePoint(side, ub, bottomB, outward), SidePoint(side, ua, bottomA, outward), color);

        if (kind == BandKind.Hydrocarbon)
        {
            // Cap rock outline
            float w = 0.012f * Unit;
            FlatQuad(SidePoint(side, ua, topA + w, outward * 1.1f), SidePoint(side, ub, topB + w, outward * 1.1f), SidePoint(side, ub, topB - w, outward * 1.1f), SidePoint(side, ua, topA - w, outward * 1.1f), new Color(20, 14, 10, 255));
        }
    }

    private bool BandAt(int cellA, int cellB, float t, BandKind kind, out float top, out float bottom, out Color color)
    {
        top = bottom = 0f;
        color = Color.White;
        // Use whichever of the two columns has the feature; blend when both do
        bool hasA = Has(cellA, kind), hasB = Has(cellB, kind);
        if (!hasA && !hasB) return false;
        if (!hasA) { cellA = cellB; t = 0f; }
        if (!hasB) { cellB = cellA; t = 0f; }

        BandDepths(cellA, kind, out float topKmA, out float bottomKmA, out color);
        BandDepths(cellB, kind, out float topKmB, out float bottomKmB, out _);
        float surfaceKm = Lerp(-_elev[cellA] * KmPerElevation, -_elev[cellB] * KmPerElevation, t);
        top = AbsY(surfaceKm + Lerp(topKmA, topKmB, t));
        bottom = AbsY(surfaceKm + Lerp(bottomKmA, bottomKmB, t));
        return true;
    }

    private bool Has(int cell, BandKind kind) => kind switch
    {
        BandKind.Hydrocarbon => _columns[cell].HasReservoir && _columns[cell].FluidContact > _columns[cell].ReservoirTop,
        BandKind.Brine => _columns[cell].HasReservoir && _columns[cell].ReservoirBottom > _columns[cell].FluidContact,
        BandKind.Salt => _columns[cell].HasSalt,
        BandKind.Unconformity => _columns[cell].UnconformityKm > 0.02f,
        _ => _slab[cell] > 0f
    };

    private void BandDepths(int cell, BandKind kind, out float top, out float bottom, out Color color)
    {
        var column = _columns[cell];
        switch (kind)
        {
            case BandKind.Hydrocarbon:
                top = column.ReservoirTop;
                bottom = MathF.Min(column.FluidContact, column.ReservoirBottom);
                color = column.ReservoirFluid == PoreFluid.Gas ? GasColor : OilColor;
                break;
            case BandKind.Brine:
                top = MathF.Max(column.ReservoirTop, column.FluidContact);
                bottom = column.ReservoirBottom;
                color = new Color(150, 190, 214, 235);
                break;
            case BandKind.Salt:
                top = column.SaltTop;
                bottom = column.SedimentKm;
                color = BedColor(RockKind.Salt);
                break;
            case BandKind.Unconformity:
                // Buried erosion surface: older beds are cut off below this line
                top = column.UnconformityKm - 0.012f;
                bottom = column.UnconformityKm + 0.012f;
                color = new Color(230, 60, 50, 235);
                break;
            default:
                top = _slab[cell];
                bottom = _slab[cell] + 9f;
                color = SlabColor;
                break;
        }
    }

    /// <summary>Which way along a face the slab deepens (the side of the overriding plate); 0 if no slab.</summary>
    private int SlabDirection(Side side, int k)
    {
        int length = SideLength(side);
        float here = _slab[SideCell(side, k)];
        float right = k + 1 < length ? _slab[SideCell(side, k + 1)] : 0f;
        float left = k > 0 ? _slab[SideCell(side, k - 1)] : 0f;
        if (right > here && right >= left) return 1;
        if (left > here) return -1;
        return 0;
    }

    /// <summary>Direction along a face towards the nearest spreading axis within a few cells; 0 if none.</summary>
    private int RiftDirection(Side side, int k)
    {
        int length = SideLength(side);
        for (int d = 1; d <= 4; d++)
        {
            if (k + d < length && _boundary[SideCell(side, k + d)] == (byte)PlateBoundaryType.Divergent) return 1;
            if (k - d >= 0 && _boundary[SideCell(side, k - d)] == (byte)PlateBoundaryType.Divergent) return -1;
        }
        return 0;
    }

    /// <summary>
    /// A fault in section. Extension: listric normal faults, steep at the surface and
    /// flattening into a detachment, dipping towards the rift. Compression: thrusts dipping
    /// under the overriding plate and soling into the plate interface or a decollement.
    /// Strike-slip: vertical.
    /// </summary>
    private void DrawFaultOnFace(Side side, int k, int cell, float top, float surfaceKm, float skin, bool front)
    {
        float unit = Unit;
        float u = k + 0.5f;
        var type = (FaultType)_faultType[cell];
        float glow = MathF.Max(_flash[cell], Math.Clamp((_stress[cell] - 0.4f) * 1.2f, 0f, 0.8f));
        Color color = Color.Lerp(new Color(150, 20, 20, 240), new Color(255, 230, 120, 255), glow);
        float width = (0.05f + 0.08f * _flash[cell]) * unit;
        int hash = (int)((uint)(cell * 2654435761u) >> 31) == 0 ? 1 : -1;
        string name;
        Vector3 labelAt;
        Vector3 slipAt;       // Where to put the slip arrows
        float hangingSide;    // Which side of the fault is the hanging wall (+1 / -1 along the face)
        float hangingMove;    // +1 the hanging wall goes up, -1 down

        if (type == FaultType.Normal)
        {
            int toward = RiftDirection(side, k);
            if (toward == 0) toward = hash;
            const float detachmentKm = 13f, reach = 2.4f;
            const int steps = 10;
            var previous = SidePoint(side, u, top, skin * 3f);
            slipAt = previous;
            for (int i = 1; i <= steps; i++)
            {
                float f = i / (float)steps;
                // Listric: the dip shallows with depth
                var point = SidePoint(side, u + toward * reach * MathF.Pow(f, 2.2f), AbsY(surfaceKm + detachmentKm * (1f - MathF.Pow(1f - f, 1.6f))), skin * 3f);
                Line(previous, point, width, color);
                if (i == 3) slipAt = point;
                previous = point;
            }
            // Detachment the fault merges into
            var end = SidePoint(side, u + toward * (reach + 1.6f), AbsY(surfaceKm + detachmentKm + 0.6f), skin * 3f);
            Line(previous, end, width * 0.8f, Shade(color, 0.85f));
            // A smaller fault dipping the other way bounds the tilted block
            Line(SidePoint(side, u + toward * 1.5f, top, skin * 3f), SidePoint(side, u + toward * 0.9f, AbsY(surfaceKm + 5f), skin * 3f), width * 0.6f, Shade(color, 0.8f));
            name = "Listric normal fault";
            labelAt = SidePoint(side, u + toward * 1.1f, AbsY(surfaceKm + 9f), 0.1f);
            hangingSide = toward;
            hangingMove = -1f;
            if (front && k % 4 == 1) _labels.Add((end, "Detachment", new Color(255, 170, 150), 4));
        }
        else if (type is FaultType.Thrust or FaultType.Reverse)
        {
            int toward = SlabDirection(side, k);
            bool megathrust = toward != 0;
            if (toward == 0) toward = hash;
            float soleKm = megathrust ? 0f : (type == FaultType.Thrust ? 16f : 22f);
            float reach = type == FaultType.Thrust ? 3.2f : 1.8f;
            const int steps = 8;
            var previous = SidePoint(side, u, top, skin * 3f);
            slipAt = previous;
            for (int i = 1; i <= steps; i++)
            {
                float f = i / (float)steps;
                float along = u + toward * reach * f;
                float depth;
                if (megathrust)
                {
                    // Follows the top of the down-going slab
                    int under = SideCell(side, (int)MathF.Round(along - 0.5f));
                    depth = MathF.Max(2f * f, MathF.Min(_slab[under] > 0f ? _slab[under] : 40f, 45f) * f);
                }
                else
                {
                    depth = soleKm * MathF.Pow(f, 0.7f); // Ramp, then flat
                }
                var point = SidePoint(side, along, MathF.Min(top, AbsY(surfaceKm + depth)), skin * 3f);
                Line(previous, point, width, color);
                if (i == 3) slipAt = point;
                previous = point;
            }
            name = megathrust ? "Megathrust" : type == FaultType.Thrust ? "Thrust fault" : "Reverse fault";
            labelAt = SidePoint(side, u + toward * reach * 0.55f, AbsY(surfaceKm + 9f), 0.1f);
            hangingSide = toward;
            hangingMove = 1f;
        }
        else
        {
            float bottom = AbsY(surfaceKm + 26f);
            Line(SidePoint(side, u, top, skin * 3f), SidePoint(side, u + 0.08f * hash, bottom, skin * 3f), width, color);
            // Motion in and out of the section: dot (towards) and cross (away)
            float my = Lerp(top, bottom, 0.25f), r = 0.07f * unit;
            var dot = SidePoint(side, u - 0.4f, my, skin * 4f);
            var cross = SidePoint(side, u + 0.4f, my, skin * 4f);
            Line(dot - new Vector3(0, r * 0.4f, 0), dot + new Vector3(0, r * 0.4f, 0), r * 0.8f, Color.White);
            Line(cross + new Vector3(0, r, 0), cross - new Vector3(0, r, 0), r * 0.35f, Color.White);
            Line(SidePoint(side, u + 0.4f - 0.12f, my, skin * 4f), SidePoint(side, u + 0.4f + 0.12f, my, skin * 4f), r * 0.35f, Color.White);
            name = "Strike-slip fault";
            labelAt = SidePoint(side, u + 0.5f, Lerp(top, bottom, 0.5f), 0.1f);
            slipAt = default;
            hangingSide = 0f;
            hangingMove = 0f;
        }

        // Slip arrows when the fault has just broken: hanging wall one way, footwall the other
        if (_flash[cell] > 0.15f && hangingSide != 0f)
        {
            var offset = (SidePoint(side, 1f, 0f) - SidePoint(side, 0f, 0f)) * (0.45f * hangingSide);
            Arrow(slipAt + offset, new Vector3(0, hangingMove * 0.5f * unit, 0), 0.07f * unit, Color.White * _flash[cell]);
            Arrow(slipAt - offset, new Vector3(0, -hangingMove * 0.5f * unit, 0), 0.07f * unit, Color.White * _flash[cell]);
        }
        if (front && k % 3 == 1) _labels.Add((labelAt, name, new Color(255, 110, 96), 3));
    }

    // ---------------------------------------------------------------------------------------
    // Cities
    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// Small models of the settlements in the block, each built from its own seed in the style
    /// of its people. Seismic waves and ruptures make the buildings sway.
    /// </summary>
    private void BuildCities()
    {
        Pass(_white, BlendState.Opaque, true);
        int rows = Math.Min(_n, _cut);
        float unit = Unit;

        foreach (var city in _cities)
        {
            if (city.J >= rows) continue;
            int cell = city.J * _n + city.I;
            var ground = CellTop(city.I, city.J);
            int vertex = Math.Clamp(city.J * Sub + Sub / 2 + 1, 0, _g - 1) * _g + Math.Clamp(city.I * Sub + Sub / 2 + 1, 0, _g - 1);

            // Ground motion under the city: S waves sway, P waves jolt, a nearby rupture rattles
            float sway = _waveS[vertex] * 0.16f + _waveP[vertex] * 0.05f;
            float rattle = _flash[cell] * 0.06f;
            float shaking = MathF.Abs(_waveS[vertex]) + MathF.Abs(_waveP[vertex]) * 0.5f + _flash[cell];

            var rnd = new Random(city.Id * 7919 + 13);
            int count = city.Type switch { CityType.Village => 5, CityType.Town => 9, CityType.City => 16, _ => 26 };
            float radius = city.Type switch { CityType.Village => 0.42f, CityType.Town => 0.55f, CityType.City => 0.75f, _ => 0.95f };
            float tall = city.Type switch { CityType.Village => 0.10f, CityType.Town => 0.15f, CityType.City => 0.26f, _ => 0.45f };
            bool modern = city.Style == CityStyle.Modern;
            if (modern) tall *= 1.5f;

            for (int b = 0; b < count; b++)
            {
                double angle = rnd.NextDouble() * Math.PI * 2;
                float distance = (float)Math.Sqrt(rnd.NextDouble()) * radius;
                float centrality = 1f - distance / radius;
                float w = 0.07f + 0.07f * (float)rnd.NextDouble(), d = 0.07f + 0.07f * (float)rnd.NextDouble();
                float h = unit * tall * (0.45f + 0.9f * (float)rnd.NextDouble()) * (0.5f + centrality);
                float phase = (float)rnd.NextDouble() * 6.28f;
                var basePoint = ground + new Vector3((float)Math.Cos(angle) * distance, 0, (float)Math.Sin(angle) * distance);

                // Taller buildings swing further; each has its own rhythm
                float lean = (sway + rattle * MathF.Sin(_time * 34f + phase)) * (0.6f + h / (unit * 0.2f));
                var topShift = new Vector3(lean, 0, lean * 0.6f * MathF.Cos(phase));

                Color wall, roof;
                bool pitched = true, tiers = false, stilts = false;
                switch (city.Style)
                {
                    case CityStyle.Timber: wall = new Color(150, 104, 66); roof = new Color(70, 48, 36); break;
                    case CityStyle.Adobe: wall = new Color(214, 178, 128); roof = new Color(190, 150, 104); pitched = false; break;
                    case CityStyle.Stilt: wall = new Color(196, 168, 104); roof = new Color(150, 120, 60); stilts = true; break;
                    case CityStyle.Pagoda: wall = new Color(196, 60, 50); roof = new Color(60, 70, 64); tiers = true; break;
                    case CityStyle.Terraced: wall = new Color(206, 196, 176); roof = new Color(150, 84, 60); break;
                    case CityStyle.Modern: wall = new Color(150, 176, 200); roof = new Color(96, 110, 128); pitched = false; break;
                    default: wall = new Color(196, 190, 180); roof = new Color(176, 84, 56); break;
                }
                wall = Shade(wall, 0.85f + 0.3f * (float)rnd.NextDouble());

                if (stilts)
                {
                    float legs = h * 0.4f;
                    Box(basePoint, w * 0.25f, d * 0.25f, legs, Vector3.Zero, new Color(90, 70, 50), new Color(90, 70, 50));
                    basePoint.Y += legs;
                    h *= 0.6f;
                }

                Box(basePoint, w, d, h, topShift, wall, pitched ? wall : roof);
                if (tiers)
                {
                    // Stacked, shrinking storeys with wide eaves
                    Box(basePoint + topShift + new Vector3(0, h, 0), w * 1.35f, d * 1.35f, h * 0.08f, Vector3.Zero, roof, roof);
                    Box(basePoint + topShift + new Vector3(0, h * 1.08f, 0), w * 0.7f, d * 0.7f, h * 0.5f, topShift * 0.5f, wall, roof);
                    Box(basePoint + topShift * 1.5f + new Vector3(0, h * 1.58f, 0), w * 1.0f, d * 1.0f, h * 0.07f, Vector3.Zero, roof, roof);
                }
                else if (pitched)
                {
                    Roof(basePoint + topShift + new Vector3(0, h, 0), w * 1.1f, d * 1.1f, MathF.Min(h * 0.6f, 0.08f * unit), roof);
                }
                else if (modern && h > unit * 0.12f)
                {
                    // Lit window bands
                    for (float level = 0.25f; level < 0.95f; level += 0.22f)
                    {
                        var at = basePoint + topShift * level + new Vector3(0, h * level, 0);
                        Box(at, w * 1.03f, d * 1.03f, h * 0.035f, Vector3.Zero, new Color(255, 230, 150), new Color(255, 230, 150));
                    }
                }
            }

            if (shaking > 0.35f && _random.NextDouble() < 0.3)
            {
                _particles.Add(new Particle
                {
                    Position = ground + new Vector3(Rand(-radius, radius), 0.02f * unit, Rand(-radius, radius)),
                    Velocity = new Vector3(Rand(-0.1f, 0.1f), 0.2f, Rand(-0.1f, 0.1f)) * unit, MaxLife = 1.5f, Size = 0.12f, Growth = 0.25f,
                    Floor = BottomY, Color = new Color(180, 168, 150) * 0.5f
                });
            }

            string note = shaking > 0.35f ? "  -  shaking" : "";
            _labels.Add((ground + new Vector3(0, tall * unit * 1.9f + 0.1f * unit, 0), $"{city.Name}  ({city.Type}, {city.Population:N0}){note}",
                shaking > 0.35f ? UITheme.Bad : UITheme.Gold, shaking > 0.35f ? 16 : 10));
        }
    }

    /// <summary>A box standing on a point; the top can be shifted sideways (a building swaying).</summary>
    private void Box(Vector3 basePoint, float halfWidth, float halfDepth, float height, Vector3 topShift, Color wall, Color top)
    {
        Vector3 a = basePoint + new Vector3(-halfWidth, 0, -halfDepth), b = basePoint + new Vector3(halfWidth, 0, -halfDepth);
        Vector3 c = basePoint + new Vector3(halfWidth, 0, halfDepth), d = basePoint + new Vector3(-halfWidth, 0, halfDepth);
        var up = new Vector3(0, height, 0) + topShift;
        FlatQuad(a, b, b + up, a + up, Shade(wall, 0.62f));
        FlatQuad(b, c, c + up, b + up, Shade(wall, 0.80f));
        FlatQuad(c, d, d + up, c + up, wall);
        FlatQuad(d, a, a + up, d + up, Shade(wall, 0.72f));
        FlatQuad(a + up, b + up, c + up, d + up, Shade(top, 1.08f));
    }

    private void Roof(Vector3 centre, float halfWidth, float halfDepth, float height, Color color)
    {
        Vector3 a = centre + new Vector3(-halfWidth, 0, -halfDepth), b = centre + new Vector3(halfWidth, 0, -halfDepth);
        Vector3 c = centre + new Vector3(halfWidth, 0, halfDepth), d = centre + new Vector3(-halfWidth, 0, halfDepth);
        var apex = centre + new Vector3(0, height, 0);
        Tri(a, b, apex, Shade(color, 0.65f), Shade(color, 0.65f), Shade(color, 0.65f), Vector2.Zero, Vector2.Zero, Vector2.Zero);
        Tri(b, c, apex, Shade(color, 0.85f), Shade(color, 0.85f), Shade(color, 0.85f), Vector2.Zero, Vector2.Zero, Vector2.Zero);
        Tri(c, d, apex, color, color, color, Vector2.Zero, Vector2.Zero, Vector2.Zero);
        Tri(d, a, apex, Shade(color, 0.75f), Shade(color, 0.75f), Shade(color, 0.75f), Vector2.Zero, Vector2.Zero, Vector2.Zero);
    }

    private static string FaultName(FaultType type) => type switch
    {
        FaultType.Thrust => "Thrust",
        FaultType.Reverse => "Reverse",
        FaultType.Normal => "Normal",
        FaultType.Strike_Slip => "Strike-slip",
        _ => "Oblique"
    };

    private void AddStructureLabels(Side side)
    {
        int length = SideLength(side);
        if (length < 4) return;

        // Name the layers once per face, where they are thickest
        int k = length / 5;
        int cell = SideCell(side, k);
        float surfaceKm = -_elev[cell] * KmPerElevation;
        var column = _columns[cell];
        bool oceanic = (_flags[cell] & FlagOceanic) != 0;
        float u = k + 0.5f;
        float moho = MathF.Max(column.CrustKm, column.SedimentKm + 2f);

        _labels.Add((SidePoint(side, u, AbsY(surfaceKm + column.SedimentKm * 0.5f), 0.1f), "Sediments", BedColor(RockKind.Sand), 4));
        _labels.Add((SidePoint(side, u, AbsY(surfaceKm + (column.SedimentKm + moho) * 0.5f), 0.1f), oceanic ? $"Oceanic crust  {moho:F0} km" : $"Continental crust  {moho:F0} km", oceanic ? BasaltColor : GraniteColor, 5));
        _labels.Add((SidePoint(side, u + 2.5f, AbsY(surfaceKm + moho), 0.1f), "Moho", new Color(255, 236, 170), 5));
        _labels.Add((SidePoint(side, length * 0.5f, Lerp(AbsY(BottomKm * 0.75f), BottomY, 0.3f), 0.1f), "Mantle", MantleTop, 4));

        for (int i = 0; i < length; i++)
        {
            int c = SideCell(side, i);
            if (_slab[c] > 20f && _slab[c] < 60f)
            {
                _labels.Add((SidePoint(side, i + 0.5f, AbsY(-_elev[c] * KmPerElevation + _slab[c] + 4f), 0.1f), "Subducting slab", new Color(150, 170, 230), 7));
                break;
            }
        }
        for (int i = 0; i < length; i++)
        {
            int c = SideCell(side, i);
            if ((_flags[c] & FlagSubducting) != 0 && _elev[c] < -0.6f)
            {
                _labels.Add((SidePoint(side, i + 0.5f, 0.12f * Unit, 0.1f), "Trench", new Color(120, 180, 255), 6));
                break;
            }
        }
    }

    /// <summary>
    /// Geothermal doublets in the plane of the front section: hot water comes up one well,
    /// gives its heat to the plant and goes back down the other.
    /// </summary>
    private void BuildGeothermalWells(float skin)
    {
        if (_geothermal == null) return;
        int faceRow = Math.Min(_n, _cut) - 1;
        float unit = Unit;
        foreach (var plant in _geothermal.Plants)
        {
            if (!ToRegion(plant.X, plant.Y, out int i, out int j) || j != faceRow) continue;
            int cell = j * _n + i;
            float u = i + 0.5f;
            float surfaceKm = -_elev[cell] * KmPerElevation;
            float top = _y[SideVertex(Side.South, Math.Min((int)(u * Sub), _n * Sub))];
            float reached = plant.DepthKm * Math.Clamp(plant.Progress, 0f, 1f);
            float bottom = AbsY(surfaceKm + reached);

            // Hot fractured rock the wells tap
            if (plant.Producing)
            {
                float glow = 0.6f + 0.25f * MathF.Sin(_time * 2f + plant.Id);
                FlatQuad(SidePoint(Side.South, u - 0.75f, bottom + 0.14f * unit, skin * 4f), SidePoint(Side.South, u + 0.75f, bottom + 0.14f * unit, skin * 4f),
                    SidePoint(Side.South, u + 0.75f, bottom - 0.16f * unit, skin * 4f), SidePoint(Side.South, u - 0.75f, bottom - 0.16f * unit, skin * 4f), new Color(255, 120, 40) * (0.5f * glow));
            }

            Line(SidePoint(Side.South, u - 0.28f, top, skin * 5f), SidePoint(Side.South, u - 0.45f, bottom, skin * 5f), 0.045f * unit, new Color(255, 90, 60));   // Production: hot
            Line(SidePoint(Side.South, u + 0.28f, top, skin * 5f), SidePoint(Side.South, u + 0.45f, bottom, skin * 5f), 0.045f * unit, new Color(90, 170, 255));  // Injection: cool

            if (plant.Producing)
            {
                // Water circulating: up the hot well, down the cold one
                for (int k = 0; k < 4; k++)
                {
                    float t = (_time * 0.5f + k * 0.25f) % 1f;
                    var up = Vector3.Lerp(SidePoint(Side.South, u - 0.45f, bottom, skin * 6f), SidePoint(Side.South, u - 0.28f, top, skin * 6f), t);
                    var down = Vector3.Lerp(SidePoint(Side.South, u + 0.28f, top, skin * 6f), SidePoint(Side.South, u + 0.45f, bottom, skin * 6f), t);
                    Line(up - new Vector3(0, 0.03f * unit, 0), up + new Vector3(0, 0.03f * unit, 0), 0.09f * unit, new Color(255, 220, 160));
                    Line(down - new Vector3(0, 0.03f * unit, 0), down + new Vector3(0, 0.03f * unit, 0), 0.09f * unit, new Color(200, 235, 255));
                }
            }
            string state = plant.Producing ? $"{plant.ReservoirTempC:F0} C at {plant.DepthKm:F1} km" : $"drilling {reached:F1} / {plant.DepthKm:F1} km";
            _labels.Add((SidePoint(Side.South, u, top + 0.8f * unit, 0.1f), $"Geothermal plant  -  {state}", new Color(255, 150, 90), 18));
        }
    }

    /// <summary>Wells whose borehole lies in the plane of the front section.</summary>
    private void BuildBoreholes(float skin)
    {
        BuildGeothermalWells(skin);
        if (_oil == null) return;
        int faceRow = Math.Min(_n, _cut) - 1;
        foreach (var well in _oil.Wells)
        {
            if (!ToRegion(well.X, well.Y, out int i, out int j)) continue;
            Side side;
            float u;
            if (j == faceRow) { side = Side.South; u = i + 0.5f; }
            else if (i == _n - 1 && j < faceRow) { side = Side.East; u = j + 0.5f; }
            else continue;

            int cell = j * _n + i;
            float surfaceKm = -_elev[cell] * KmPerElevation;
            float top = _y[SideVertex(side, Math.Min((int)(u * Sub), SideLength(side) * Sub))];
            float reached = well.TargetDepthKm * Math.Clamp(well.Progress, 0f, 1f);
            float bottom = AbsY(surfaceKm + reached);
            float w = 0.03f * Unit;

            // Riser through the sea, then casing and open hole
            if (_elev[cell] < 0f) Line(SidePoint(side, u, 0.1f * Unit, skin * 5f), SidePoint(side, u, top, skin * 5f), w * 0.8f, new Color(250, 200, 80));
            Line(SidePoint(side, u, top, skin * 5f), SidePoint(side, u, bottom, skin * 5f), w * 1.6f, new Color(20, 20, 24));
            Line(SidePoint(side, u, top, skin * 5.5f), SidePoint(side, u, bottom, skin * 5.5f), w * 0.6f,
                well.Status == WellStatus.Producing ? new Color(120, 230, 140) : well.Status == WellStatus.Dry ? new Color(170, 170, 170) : new Color(250, 220, 120));

            if (well.Status == WellStatus.Drilling)
            {
                // Drill bit
                float spark = 0.6f + 0.4f * MathF.Sin(_time * 18f);
                var tip = SidePoint(side, u, bottom, skin * 6f);
                Line(tip + new Vector3(0, w * 2, 0), tip - new Vector3(0, w * 2, 0), w * 4f, new Color(255, 240, 160) * spark);
            }

            string state = well.Status switch
            {
                WellStatus.Drilling => $"drilling {reached:F1} / {well.TargetDepthKm:F1} km",
                WellStatus.Producing => "producing",
                WellStatus.Dry => "dry hole",
                _ => "depleted"
            };
            _labels.Add((SidePoint(side, u, top + 0.75f * Unit, 0.1f), $"{(well.Offshore ? "Platform" : "Well")}  -  {state}", UITheme.Gold, well.Id == _focusWell ? 20 : 9));
        }
    }

    // ---------------------------------------------------------------------------------------
    // Things on the landscape
    // ---------------------------------------------------------------------------------------

    private void Arrow(Vector3 from, Vector3 delta, float width, Color color)
    {
        var to = from + delta;
        var shaftEnd = from + delta * 0.62f;
        Line(from, shaftEnd, width, color);
        var toCamera = new Vector3(MathF.Cos(_pitch) * MathF.Sin(_yaw), MathF.Sin(_pitch), MathF.Cos(_pitch) * MathF.Cos(_yaw));
        var side = Vector3.Cross(delta, toCamera);
        if (side.LengthSquared() < 1e-8f) return;
        side = Vector3.Normalize(side) * width * 1.6f;
        Tri(shaftEnd - side, shaftEnd + side, to, color, color, color, Vector2.Zero, Vector2.Zero, Vector2.Zero);
    }

    private void BuildSurfaceOverlays()
    {
        Pass(_white, BlendState.NonPremultiplied, false);
        int n = _n, rows = Math.Min(n, _cut);
        float unit = Unit;

        // Fault traces
        for (int j = 0; j < rows; j++)
        {
            for (int i = 0; i < n; i++)
            {
                int c = j * n + i;
                if ((_flags[c] & FlagFault) == 0) continue;
                Color color = (FaultType)_faultType[c] switch
                {
                    FaultType.Normal => new Color(90, 170, 255),
                    FaultType.Strike_Slip => new Color(255, 170, 60),
                    _ => new Color(240, 70, 60)
                };
                color = Color.Lerp(color, Color.White, _flash[c]);
                float width = (0.07f + 0.12f * _flash[c]);
                // A plate boundary is a fault on both sides: draw its trace once, along its strike
                float sx = 0f, sz = 0f;
                bool twin = false;
                for (int dj = -1; dj <= 1; dj++)
                {
                    for (int di = -1; di <= 1; di++)
                    {
                        int oi = i + di, oj = j + dj;
                        if ((di == 0 && dj == 0) || oi < 0 || oj < 0 || oi >= n || oj >= n) continue;
                        int o = oj * n + oi;
                        if ((_flags[o] & FlagFault) == 0) continue;
                        if (_plate[o] < _plate[c]) twin = true;
                        if (_plate[o] != _plate[c]) continue;
                        // Fold opposite directions together so they add up instead of cancelling
                        float fx = di, fz = dj;
                        if (fx < 0 || (fx == 0 && fz < 0)) { fx = -fx; fz = -fz; }
                        sx += fx;
                        sz += fz;
                    }
                }
                if (twin) continue;
                var strike = new Vector3(sx, 0, sz);
                strike = strike.LengthSquared() < 0.01f ? new Vector3(0.5f, 0, 0) : Vector3.Normalize(strike) * 0.62f;
                var centre = CellTop(i, j, 0.04f * unit);
                Ribbon(centre - strike, centre + strike, width, color);
            }
        }

        // Expanding rings from epicentres and impacts
        foreach (var (position, age, life, color, speed) in _rings)
        {
            float radius = age * speed, fade = 1f - age / life;
            const int segments = 40;
            for (int s = 0; s < segments; s++)
            {
                float a = s * MathHelper.TwoPi / segments, b = (s + 1) * MathHelper.TwoPi / segments;
                Ribbon(position + new Vector3(MathF.Cos(a), 0, MathF.Sin(a)) * radius, position + new Vector3(MathF.Cos(b), 0, MathF.Sin(b)) * radius, 0.12f, color * fade);
            }
        }

        // Volcano craters
        for (int j = 0; j < rows; j++)
        {
            for (int i = 0; i < n; i++)
            {
                int c = j * n + i;
                if ((_flags[c] & FlagVolcano) == 0) continue;
                float heat = Math.Clamp(_pressure[c], 0.15f, 1f) * (0.7f + 0.3f * MathF.Sin(_time * 4f + c));
                if (_erupting[c] > 0f) heat = 1f;
                var p = CellTop(i, j, 0.03f * unit);
                FlatQuad(p + new Vector3(-0.2f, 0, -0.2f), p + new Vector3(0.2f, 0, -0.2f), p + new Vector3(0.2f, 0, 0.2f), p + new Vector3(-0.2f, 0, 0.2f),
                    Color.Lerp(new Color(120, 30, 10, 220), new Color(255, 200, 60, 255), heat));
                _labels.Add((p + new Vector3(0, 0.45f * unit, 0), (_flags[c] & FlagHotSpot) != 0 ? "Hot spot volcano" : "Volcano", MagmaColor, _erupting[c] > 0f ? 12 : 3));
            }
        }

        if (_showArrows) BuildForces(rows);
        BuildInstallations(rows);
    }

    /// <summary>Plate motion and vertical movements.</summary>
    private void BuildForces(int rows)
    {
        int n = _n;
        float unit = Unit;

        // One motion arrow per plate, at the middle of its part of the block
        var sums = new Dictionary<int, (float x, float z, float top, int count)>();
        for (int j = 0; j < rows; j++)
        {
            for (int i = 0; i < n; i++)
            {
                int c = j * n + i;
                var s = sums.GetValueOrDefault(_plate[c]);
                var top = CellTop(i, j);
                sums[_plate[c]] = (s.x + top.X, s.z + top.Z, MathF.Max(s.top, top.Y), s.count + 1);
            }
        }
        foreach (var (plate, s) in sums)
        {
            if (s.count < 12 || _geo == null) continue;
            var (vx, vy) = _geo.GetPlateVelocity(plate);
            float speed = MathF.Sqrt(vx * vx + vy * vy);
            if (speed < 0.02f) continue;
            var direction = new Vector3(vx, 0, vy) / speed;
            var origin = new Vector3(s.x / s.count, MathF.Max(0f, s.top) + 0.45f * unit, s.z / s.count);
            float length = 2.2f + 9f * speed;

            // Chevrons streaming along the direction of motion
            for (int k = 0; k < 4; k++)
            {
                float phase = ((_time * 0.6f + k * 0.25f) % 1f);
                var at = origin + direction * ((phase - 0.5f) * length);
                var side = new Vector3(-direction.Z, 0, direction.X) * 0.45f;
                Color color = new Color(255, 255, 255) * (0.9f * MathF.Sin(phase * MathF.PI));
                Ribbon(at - side - direction * 0.35f, at, 0.14f, color);
                Ribbon(at + side - direction * 0.35f, at, 0.14f, color);
            }
            _labels.Add((origin + new Vector3(0, 0.3f * unit, 0), $"Plate {plate + 1}  {speed * 12f:F1} cm/yr", Color.White, 6));
        }

        // Stress across plate boundaries: squeezing (red, inward) or stretching (blue, outward)
        for (int j = 1; j < rows - 1; j += 2)
        {
            for (int i = 1; i < n - 1; i += 2)
            {
                int c = j * n + i;
                var type = (PlateBoundaryType)_boundary[c];
                if (type != PlateBoundaryType.Convergent && type != PlateBoundaryType.Divergent) continue;
                if ((i + j) % 4 != 0) continue;

                // Normal to the boundary: towards the neighbouring plate
                Vector3 normal = Vector3.Zero;
                foreach (var (di, dj) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    int o = (j + dj) * n + (i + di);
                    if (_plate[o] > _plate[c]) normal += new Vector3(di, 0, dj);
                }
                if (normal.LengthSquared() < 0.5f) continue;
                normal.Normalize();

                var centre = CellTop(i, j, 0.3f * unit) + normal * 0.5f;
                float pulse = 0.3f * MathF.Sin(_time * 2.5f + c);
                if (type == PlateBoundaryType.Convergent)
                {
                    Color squeeze = new Color(255, 90, 80);
                    Arrow(centre - normal * (2.3f + pulse), normal * 1.5f, 0.09f * unit, squeeze);
                    Arrow(centre + normal * (2.3f + pulse), -normal * 1.5f, 0.09f * unit, squeeze);
                }
                else
                {
                    Color stretch = new Color(110, 180, 255);
                    Arrow(centre - normal * (0.5f + pulse), -normal * 1.5f, 0.09f * unit, stretch);
                    Arrow(centre + normal * (0.5f + pulse), normal * 1.5f, 0.09f * unit, stretch);
                }
            }
        }

        // Uplift (green, up) and subsidence (blue, down)
        for (int j = 1; j < rows; j += 3)
        {
            for (int i = 1; i < n; i += 3)
            {
                int c = j * n + i;
                float rate = _uplift[c];
                if (MathF.Abs(rate) < 0.00025f) continue;
                float bob = ((_time * 0.8f + (i * 7 + j * 3) * 0.13f) % 1f);
                float length = Math.Clamp(MathF.Abs(rate) / 0.003f, 0.25f, 1f) * 0.6f * unit;
                var basePoint = CellTop(i, j, 0.1f * unit);
                if (rate > 0)
                {
                    Arrow(basePoint + new Vector3(0, bob * 0.3f * unit, 0), new Vector3(0, length, 0), 0.07f * unit, new Color(120, 240, 130) * (1f - bob * 0.6f));
                }
                else
                {
                    Arrow(basePoint + new Vector3(0, length + (1f - bob) * 0.3f * unit, 0), new Vector3(0, -length, 0), 0.07f * unit, new Color(110, 180, 255) * (0.4f + bob * 0.6f));
                }
            }
        }
    }

    /// <summary>Wells, platforms, seismic crews and seismograph stations.</summary>
    private void BuildInstallations(int rows)
    {
        float unit = Unit;
        Color steel = new Color(40, 44, 54), yellow = new Color(250, 200, 70);

        if (_oil != null)
        {
            foreach (var well in _oil.Wells)
            {
                if (!ToRegion(well.X, well.Y, out int i, out int j) || j >= rows) continue;
                var ground = CellTop(i, j);
                float h = 0.55f * unit, w = 0.2f;

                if (well.Offshore)
                {
                    // Platform: legs through the water, deck, derrick and flare
                    float deck = 0.16f * unit;
                    foreach (var (dx, dz) in new[] { (-w, -w), (w, -w), (w, w), (-w, w) })
                    {
                        Line(new Vector3(ground.X + dx, ground.Y, ground.Z + dz), new Vector3(ground.X + dx, deck, ground.Z + dz), 0.035f * unit, steel);
                    }
                    FlatQuad(new Vector3(ground.X - w * 1.4f, deck, ground.Z - w * 1.4f), new Vector3(ground.X + w * 1.4f, deck, ground.Z - w * 1.4f),
                        new Vector3(ground.X + w * 1.4f, deck, ground.Z + w * 1.4f), new Vector3(ground.X - w * 1.4f, deck, ground.Z + w * 1.4f), yellow);
                    ground.Y = deck;
                    h *= 0.7f;
                }

                var apex = ground + new Vector3(0, h, 0);
                Color tower = well.Status == WellStatus.Dry || well.Status == WellStatus.Depleted ? new Color(120, 120, 124) : steel;
                foreach (var (dx, dz) in new[] { (-w, -w), (w, -w), (w, w), (-w, w) })
                {
                    Line(ground + new Vector3(dx * 0.8f, 0, dz * 0.8f), apex, 0.03f * unit, tower);
                }
                Line(ground + new Vector3(-w * 0.5f, h * 0.45f, 0), ground + new Vector3(w * 0.5f, h * 0.45f, 0), 0.025f * unit, tower);
                if (well.Status == WellStatus.Producing && _random.NextDouble() < 0.25)
                {
                    _particles.Add(new Particle
                    {
                        Position = apex, Velocity = new Vector3(Rand(-0.05f, 0.05f), 0.4f, Rand(-0.05f, 0.05f)) * unit,
                        MaxLife = 0.7f, Size = 0.12f, Floor = BottomY, Color = new Color(255, 170, 50)
                    });
                }
                if (j != rows - 1)
                {
                    _labels.Add((apex + new Vector3(0, 0.12f * unit, 0), well.Offshore ? "Platform" : "Oil well", UITheme.Gold, 5));
                }
            }

            foreach (var survey in _oil.Surveys)
            {
                if (survey.Completed) continue;
                if (!ToRegion((int)MathF.Round((survey.X1 + survey.X2) / 2f) % _map.Width, (int)((survey.Y1 + survey.Y2) / 2f), out _, out _)) continue;

                Vector3 At(float t)
                {
                    float x = survey.X1 + (survey.X2 - survey.X1) * t - OriginX, z = survey.Y1 + (survey.Y2 - survey.Y1) * t - OriginY;
                    int ci = Math.Clamp((int)x, 0, _n - 1), cj = Math.Clamp((int)z, 0, _n - 1);
                    return new Vector3(x + 0.5f - _n / 2f, MathF.Max(0f, CellTop(ci, cj).Y) + 0.05f * unit, z + 0.5f - _n / 2f);
                }

                // Line already shot, crew position, and the pulse going down and echoing back
                const int pieces = 24;
                for (int k = 0; k < pieces; k++)
                {
                    float t0 = k / (float)pieces, t1 = (k + 0.6f) / pieces;
                    Ribbon(At(t0), At(t1), 0.1f, t0 < survey.Progress ? new Color(120, 240, 255) : new Color(120, 240, 255) * 0.3f);
                }
                var crew = At(Math.Clamp(survey.Progress, 0f, 1f));
                float ping = (_time * 1.5f) % 1f;
                Line(crew, crew + new Vector3(0, 0.35f * unit, 0), 0.05f * unit, yellow);
                const int ring = 20;
                for (int s = 0; s < ring; s++)
                {
                    float a = s * MathHelper.TwoPi / ring, b = (s + 1) * MathHelper.TwoPi / ring;
                    Ribbon(crew + new Vector3(MathF.Cos(a), 0, MathF.Sin(a)) * ping * 1.5f, crew + new Vector3(MathF.Cos(b), 0, MathF.Sin(b)) * ping * 1.5f, 0.06f, new Color(120, 240, 255) * (1f - ping));
                }
                _labels.Add((crew + new Vector3(0, 0.5f * unit, 0), survey.Marine ? "Seismic vessel" : "Seismic crew", new Color(120, 240, 255), 9));
            }
        }

        if (_geothermal != null)
        {
            foreach (var plant in _geothermal.Plants)
            {
                if (!ToRegion(plant.X, plant.Y, out int i, out int j) || j >= rows) continue;
                var ground = CellTop(i, j);
                float w = 0.3f, h = 0.2f * unit;
                Color wall = plant.Producing ? new Color(232, 236, 244) : new Color(150, 150, 156);
                // Turbine hall and two cooling towers
                FlatQuad(ground + new Vector3(-w, h, -w * 0.6f), ground + new Vector3(w, h, -w * 0.6f), ground + new Vector3(w, h, w * 0.6f), ground + new Vector3(-w, h, w * 0.6f), wall);
                FlatQuad(ground + new Vector3(-w, 0, w * 0.6f), ground + new Vector3(w, 0, w * 0.6f), ground + new Vector3(w, h, w * 0.6f), ground + new Vector3(-w, h, w * 0.6f), Shade(wall, 0.8f));
                FlatQuad(ground + new Vector3(w, 0, -w * 0.6f), ground + new Vector3(w, 0, w * 0.6f), ground + new Vector3(w, h, w * 0.6f), ground + new Vector3(w, h, -w * 0.6f), Shade(wall, 0.65f));
                foreach (float side in new[] { -0.18f, 0.18f })
                {
                    var tower = ground + new Vector3(side, 0, -w * 0.9f);
                    Line(tower, tower + new Vector3(0, h * 2.2f, 0), 0.09f * unit, new Color(200, 204, 212));
                    if (plant.Producing && _random.NextDouble() < 0.2)
                    {
                        _particles.Add(new Particle
                        {
                            Position = tower + new Vector3(0, h * 2.2f, 0), Velocity = new Vector3(Rand(-0.06f, 0.06f), 0.3f, Rand(-0.06f, 0.06f)) * unit,
                            MaxLife = 2.2f, Size = 0.14f, Growth = 0.3f, Floor = BottomY, Color = Color.White * 0.5f
                        });
                    }
                }
                if (j != rows - 1) _labels.Add((ground + new Vector3(0, h * 3f, 0), "Geothermal plant", new Color(255, 150, 90), 5));
            }
        }

        if (_seismo != null)
        {
            foreach (var station in _seismo.Stations)
            {
                if (!ToRegion(station.X, station.Y, out int i, out int j) || j >= rows) continue;
                var ground = CellTop(i, j);
                float shake = 0f;
                if (_seismo.IsPropagating && station.Samples > 0)
                {
                    shake = Math.Clamp(station.TraceS[station.Samples - 1] / MathF.Max(1e-5f, _seismo.EventAmplitude * 0.05f), -1f, 1f);
                }
                var top = ground + new Vector3(shake * 0.12f, 0.4f * unit, 0);
                Line(ground, top, 0.03f * unit, new Color(230, 230, 240));
                Line(ground + new Vector3(-0.15f, 0, 0), ground + new Vector3(0.15f, 0, 0), 0.05f * unit, new Color(230, 230, 240));
                Color lamp = station.PickS >= 0 ? new Color(255, 110, 90) : station.PickP >= 0 ? new Color(120, 210, 255) : new Color(110, 220, 130);
                FlatQuad(top + new Vector3(-0.08f, 0, -0.08f), top + new Vector3(0.08f, 0, -0.08f), top + new Vector3(0.08f, 0, 0.08f), top + new Vector3(-0.08f, 0, 0.08f), lamp);
                _labels.Add((top + new Vector3(0, 0.12f * unit, 0), "Seismograph", lamp, 4));
            }
        }
    }

    private void BuildParticles(Matrix view)
    {
        Pass(_dot, BlendState.AlphaBlend, false);
        // Camera-facing quads
        var inverse = Matrix.Invert(view);
        var right = inverse.Right;
        var up = inverse.Up;
        foreach (var p in _particles)
        {
            float t = p.Life / p.MaxLife;
            float alpha = MathF.Min(1f, 4f * (1f - t)) * MathF.Min(1f, t * 8f + 0.2f);
            var color = p.Color * alpha;
            var r = right * p.Size;
            var u = up * p.Size;
            Quad(p.Position - r + u, p.Position + r + u, p.Position + r - u, p.Position - r - u,
                color, color, color, color, new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1));
        }
    }

    // ---------------------------------------------------------------------------------------
    // Well log
    // ---------------------------------------------------------------------------------------

    private readonly StrataColumn _logColumn = new();

    /// <summary>
    /// Wireline log of the focused well: lithology, gamma ray, resistivity and sonic against
    /// depth, read from the same column the section shows and the seismic line imaged.
    /// </summary>
    private void DrawWellLog(SpriteBatch sb)
    {
        if (_oil == null || !_focusWell.HasValue) return;
        var well = _oil.Wells.FirstOrDefault(w => w.Id == _focusWell.Value);
        if (well == null) return;

        Stratigraphy.Build(_map, _oil, well.X, well.Y, _logColumn);
        float logged = well.TargetDepthKm * Math.Clamp(well.Progress, 0f, 1f);
        float maxDepth = MathF.Max(1f, MathF.Min(_logColumn.SedimentKm + 0.3f, well.TargetDepthKm + 0.6f));

        int tickerHeight = _ticker.Count * 26;
        var box = new Rectangle(_panel.X + 10, _panel.Y + HeaderHeight + 14 + tickerHeight, 262, 330);
        UITheme.DrawPanel(sb, box, new Color(10, 14, 22, 235), UITheme.Border);
        UITheme.DrawText(sb, $"WELL LOG  -  {(well.Offshore ? "platform" : "well")} {well.Id}", new Vector2(box.X + 8, box.Y + 5), UITheme.Gold, UITheme.FontSmall);

        int top = box.Y + 42, height = box.Height - 66;
        var lith = new Rectangle(box.X + 34, top, 26, height);
        var tracks = new[]
        {
            (rect: new Rectangle(box.X + 66, top, 58, height), name: "GR", color: new Color(110, 220, 130)),
            (rect: new Rectangle(box.X + 130, top, 58, height), name: "Res", color: new Color(255, 130, 110)),
            (rect: new Rectangle(box.X + 194, top, 58, height), name: "Sonic", color: new Color(120, 200, 255))
        };
        foreach (var track in tracks)
        {
            UITheme.FillRect(sb, track.rect, new Color(4, 7, 12, 210));
            UITheme.DrawRectOutline(sb, track.rect, UITheme.Border * 0.7f);
            UITheme.DrawText(sb, track.name, new Vector2(track.rect.X + 3, top - 16), track.color, UITheme.FontSmall);
        }
        UITheme.DrawText(sb, "km", new Vector2(box.X + 8, top - 16), UITheme.TextMuted, UITheme.FontSmall);

        var previous = new Vector2[3];
        for (int py = 0; py < height; py++)
        {
            float depth = (py + 0.5f) / height * maxDepth;
            var rock = _logColumn.Sample(depth);
            bool drilled = depth <= logged;

            Color fill = rock.Fluid == PoreFluid.Gas ? GasColor : rock.Fluid == PoreFluid.Oil ? OilColor : BedColor(rock.Kind);
            UITheme.FillRect(sb, new Rectangle(lith.X, top + py, lith.Width, 1), drilled ? fill : fill * 0.35f);
            if (!drilled) continue;

            // Tool response: a little measurement noise on top of the rock's own values
            float wiggle = (Hash(py * 13 + well.Id) - 0.5f) * 0.06f;
            float gamma = Math.Clamp(rock.Gamma / 180f + wiggle, 0f, 1f);
            float resistivity = Math.Clamp(MathF.Log10(MathF.Max(0.5f, rock.Resistivity)) / 3.6f + wiggle, 0f, 1f);
            float sonic = Math.Clamp(1f - (rock.Velocity - 1.5f) / 5f + wiggle * 0.5f, 0f, 1f);
            float[] values = { gamma, resistivity, sonic };
            for (int t = 0; t < 3; t++)
            {
                var point = new Vector2(tracks[t].rect.X + 2 + values[t] * (tracks[t].rect.Width - 4), top + py);
                if (py > 0 && previous[t] != default) UITheme.DrawLine(sb, previous[t], point, tracks[t].color, 1.2f);
                previous[t] = point;
            }
        }
        UITheme.DrawRectOutline(sb, lith, UITheme.Border);

        for (int k = 0; k <= 4; k++)
        {
            float depth = maxDepth * k / 4f;
            int y = top + height * k / 4;
            UITheme.DrawText(sb, $"{depth:F1}", new Vector2(box.X + 6, y - 7), UITheme.TextMuted, UITheme.FontSmall);
        }

        // Pay zone marker
        if (_logColumn.HasReservoir && _logColumn.FluidContact > _logColumn.ReservoirTop && logged >= _logColumn.ReservoirTop)
        {
            int y0 = top + (int)(_logColumn.ReservoirTop / maxDepth * height), y1 = top + (int)(MathF.Min(_logColumn.FluidContact, logged) / maxDepth * height);
            UITheme.FillRect(sb, new Rectangle(box.X + 30, y0, 3, Math.Max(2, y1 - y0)), UITheme.Good);
        }

        string status = well.Status switch
        {
            WellStatus.Drilling => $"drilling: {logged:F2} of {well.TargetDepthKm:F1} km",
            WellStatus.Producing => _logColumn.HasReservoir ? $"pay: {_logColumn.ReservoirFluid} at {_logColumn.ReservoirTop:F2} km" : "producing",
            WellStatus.Dry => "dry hole: no reservoir here",
            _ => "depleted"
        };
        UITheme.DrawText(sb, status, new Vector2(box.X + 8, box.Bottom - 19), UITheme.TextDim, UITheme.FontSmall);
    }
}
