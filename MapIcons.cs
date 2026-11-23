using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace SimPlanet;

/// <summary>
/// Procedurally generated, anti-aliased map sprites (settlements, armies, battles...).
///
/// Every sprite is rendered once at load time with 4x4 supersampling and a dark outline,
/// so it reads well on any terrain colour. Sprites that should carry a civilization colour
/// come in two layers: a neutral base and a white "tint mask" that is drawn on top
/// multiplied by the civilization colour (roofs, flags, banners).
/// </summary>
public partial class MapIcons : IDisposable
{
    /// <summary>Design grid of the settlement sprites (their textures are <see cref="SettlementScale"/> times larger).</summary>
    public const int SettlementSize = 40;
    public const int SmallIconSize = 24;

    // Sprites that only appear on the map are rasterized larger than their design grid, to a
    // power-of-two size with mipmaps: sharp when zoomed in, clean when zoomed out. Icons shared
    // with the panels and toolbar stay at 24 px, where they are drawn 1:1.
    private const float SettlementScale = 1.6f;      // 40 -> 64 px
    private const float MapSpriteScale = 4f / 3f;    // 24 -> 32 px

    public Texture2D[] SettlementBase { get; } = new Texture2D[4];
    public Texture2D[] SettlementMask { get; } = new Texture2D[4];
    public Texture2D Crown { get; }
    public Texture2D WallRing { get; }
    public Texture2D ArmyBase { get; }
    public Texture2D ArmyMask { get; }
    public Texture2D CrossedSwords { get; }
    public Texture2D Fire { get; }
    public Texture2D Hunger { get; }
    public Texture2D Volcano { get; }
    public Texture2D Smoke { get; }

    // Society views: power, networks, strategic forces, transport and space
    public Texture2D CoolingTower { get; }
    public Texture2D SolarPanel { get; }
    public Texture2D WindTurbine { get; }
    public Texture2D Factory { get; }
    public Texture2D Dam { get; }
    public Texture2D FusionCore { get; }
    public Texture2D Trefoil { get; }
    public Texture2D Flask { get; }
    public Texture2D Biohazard { get; }
    public Texture2D Silo { get; }
    public Texture2D Anchor { get; }
    public Texture2D Plane { get; }
    public Texture2D Rocket { get; }
    public Texture2D Satellite { get; }
    public Texture2D Station { get; }
    public Texture2D MoonBase { get; }
    public Texture2D Bolt { get; }
    public Texture2D Shield { get; }
    public Texture2D Capitol { get; }
    public Texture2D Network { get; }

    private static MapIcons? _shared;

    /// <summary>Lazily created instance shared by all renderers (lives for the whole session).</summary>
    public static MapIcons GetShared(GraphicsDevice device)
    {
        if (_shared == null || _shared.Crown.IsDisposed)
            _shared = new MapIcons(device);
        return _shared;
    }

    public MapIcons(GraphicsDevice device)
    {
        for (int i = 0; i < 4; i++)
        {
            var (b, m) = BuildSettlement(device, i);
            SettlementBase[i] = b;
            SettlementMask[i] = m;
        }
        Crown = BuildCrown(device);
        WallRing = BuildWallRing(device);
        (ArmyBase, ArmyMask) = BuildArmy(device);
        CrossedSwords = BuildSwords(device);
        Fire = BuildFire(device);
        Hunger = BuildHunger(device);
        Volcano = BuildVolcano(device);
        Smoke = BuildSmoke(device);

        CoolingTower = BuildCoolingTower(device);
        SolarPanel = BuildSolarPanel(device);
        WindTurbine = BuildWindTurbine(device);
        Factory = BuildFactory(device);
        Dam = BuildDam(device);
        FusionCore = BuildFusion(device);
        Trefoil = BuildTrefoil(device);
        Flask = BuildFlask(device);
        Biohazard = BuildBiohazard(device);
        Silo = BuildSilo(device);
        Anchor = BuildAnchor(device);
        Plane = BuildPlane(device);
        Rocket = BuildRocket(device);
        Satellite = BuildSatellite(device);
        Station = BuildStation(device);
        MoonBase = BuildMoonBase(device);
        Bolt = BuildBolt(device);
        Shield = BuildShield(device);
        Capitol = BuildCapitol(device);
        Network = BuildNetwork(device);

        BuildStyledSettlements(device);
        BuildVehicles(device);
    }

    /// <summary>Power plant sprite for an energy source.</summary>
    public Texture2D PowerPlantIcon(EnergySource source) => source switch
    {
        EnergySource.Nuclear => CoolingTower,
        EnergySource.Solar => SolarPanel,
        EnergySource.Wind => WindTurbine,
        EnergySource.Hydro => Dam,
        EnergySource.Fusion => FusionCore,
        _ => Factory
    };

    public void Dispose()
    {
        foreach (var t in SettlementBase) t?.Dispose();
        foreach (var t in SettlementMask) t?.Dispose();
        Crown.Dispose();
        WallRing.Dispose();
        ArmyBase.Dispose();
        ArmyMask.Dispose();
        CrossedSwords.Dispose();
        Fire.Dispose();
        Hunger.Dispose();
        Volcano.Dispose();
        Smoke.Dispose();
        foreach (var t in new[] { CoolingTower, SolarPanel, WindTurbine, Factory, Dam, FusionCore, Trefoil, Flask,
                     Biohazard, Silo, Anchor, Plane, Rocket, Satellite, Station, MoonBase, Bolt, Shield, Capitol, Network })
            t.Dispose();
        DisposeStyled();
        DisposeVehicles();
    }

    // ------------------------------------------------------------------
    // Tiny software rasterizer working in a supersampled buffer.
    // Coordinates are expressed in final-pixel units (0..size).
    // ------------------------------------------------------------------
    internal sealed class Canvas
    {
        private const int SS = 4;
        /// <summary>Logical size: the coordinate space of every drawing call (0..Size).</summary>
        public readonly int Size;
        /// <summary>Size of the produced texture in pixels (Size x scale).</summary>
        public readonly int PixelSize;
        private readonly float _k;      // supersampled pixels per logical unit
        private readonly int _hi;
        private readonly Vector4[] _px; // premultiplied RGBA

        /// <param name="size">Logical size; drawing coordinates run from 0 to this value.</param>
        /// <param name="scale">Texture pixels per logical unit: sprites designed on a small grid
        /// can be rasterized at a higher resolution so they stay sharp when zoomed in.</param>
        public Canvas(int size, float scale = 1f)
        {
            Size = size;
            PixelSize = Math.Max(1, (int)MathF.Round(size * scale));
            _hi = PixelSize * SS;
            _k = _hi / (float)size;
            _px = new Vector4[_hi * _hi];
        }

        private void Blend(int i, Color c)
        {
            float a = c.A / 255f;
            var src = new Vector4(c.R / 255f * a, c.G / 255f * a, c.B / 255f * a, a);
            _px[i] = src + _px[i] * (1f - a);
        }

        public void Rect(float x, float y, float w, float h, Color c)
        {
            int x0 = (int)MathF.Round(x * _k), y0 = (int)MathF.Round(y * _k);
            int x1 = (int)MathF.Round((x + w) * _k), y1 = (int)MathF.Round((y + h) * _k);
            for (int py = Math.Max(0, y0); py < Math.Min(_hi, y1); py++)
                for (int px = Math.Max(0, x0); px < Math.Min(_hi, x1); px++)
                    Blend(py * _hi + px, c);
        }

        public void Circle(float cx, float cy, float r, Color c) => Ellipse(cx, cy, r, r, c);

        public void Ring(float cx, float cy, float rOuter, float rInner, Color c)
        {
            float o2 = rOuter * rOuter, i2 = rInner * rInner;
            int x0 = Math.Max(0, (int)((cx - rOuter) * _k) - 1), x1 = Math.Min(_hi, (int)((cx + rOuter) * _k) + 1);
            int y0 = Math.Max(0, (int)((cy - rOuter) * _k) - 1), y1 = Math.Min(_hi, (int)((cy + rOuter) * _k) + 1);
            for (int py = y0; py < y1; py++)
            {
                for (int px = x0; px < x1; px++)
                {
                    float fx = (px + 0.5f) / _k - cx, fy = (py + 0.5f) / _k - cy;
                    float d = fx * fx + fy * fy;
                    if (d <= o2 && d >= i2) Blend(py * _hi + px, c);
                }
            }
        }

        /// <summary>Filled ellipse (domes, contact shadows).</summary>
        public void Ellipse(float cx, float cy, float rx, float ry, Color c)
        {
            if (rx <= 0 || ry <= 0) return;
            int x0 = (int)((cx - rx) * _k) - 1, x1 = (int)((cx + rx) * _k) + 1;
            int y0 = (int)((cy - ry) * _k) - 1, y1 = (int)((cy + ry) * _k) + 1;
            for (int py = Math.Max(0, y0); py < Math.Min(_hi, y1); py++)
            {
                for (int px = Math.Max(0, x0); px < Math.Min(_hi, x1); px++)
                {
                    float fx = ((px + 0.5f) / _k - cx) / rx, fy = ((py + 0.5f) / _k - cy) / ry;
                    if (fx * fx + fy * fy <= 1f) Blend(py * _hi + px, c);
                }
            }
        }

        public void Poly(Color c, params Vector2[] pts) => PolyImpl(c, false, pts);

        /// <summary>Clears (makes transparent) everything inside the polygon.</summary>
        public void ErasePoly(params Vector2[] pts) => PolyImpl(Color.Transparent, true, pts);

        private void PolyImpl(Color c, bool erase, Vector2[] pts)
        {
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            foreach (var p in pts) { minX = Math.Min(minX, p.X); minY = Math.Min(minY, p.Y); maxX = Math.Max(maxX, p.X); maxY = Math.Max(maxY, p.Y); }
            int x0 = Math.Max(0, (int)(minX * _k) - 1), x1 = Math.Min(_hi, (int)(maxX * _k) + 1);
            int y0 = Math.Max(0, (int)(minY * _k) - 1), y1 = Math.Min(_hi, (int)(maxY * _k) + 1);
            for (int py = y0; py < y1; py++)
            {
                float fy = (py + 0.5f) / _k;
                for (int px = x0; px < x1; px++)
                {
                    float fx = (px + 0.5f) / _k;
                    bool inside = false;
                    for (int i = 0, j = pts.Length - 1; i < pts.Length; j = i++)
                    {
                        if ((pts[i].Y > fy) != (pts[j].Y > fy) &&
                            fx < (pts[j].X - pts[i].X) * (fy - pts[i].Y) / (pts[j].Y - pts[i].Y) + pts[i].X)
                            inside = !inside;
                    }
                    if (inside)
                    {
                        if (erase) _px[py * _hi + px] = Vector4.Zero;
                        else Blend(py * _hi + px, c);
                    }
                }
            }
        }

        public void Line(Vector2 a, Vector2 b, float width, Color c)
        {
            Vector2 d = b - a;
            float len = d.Length();
            if (len < 0.001f) return;
            Vector2 n = new Vector2(-d.Y, d.X) / len * (width / 2f);
            Poly(c, a + n, b + n, b - n, a - n);
        }

        /// <summary>
        /// Gives everything drawn so far some volume: lit from the top-left and darker towards
        /// the bottom-right, so flat-coloured shapes don't look like paper cut-outs.
        /// </summary>
        public void Light(float strength = 0.16f)
        {
            for (int y = 0; y < _hi; y++)
            {
                float ty = y / (float)(_hi - 1);
                for (int x = 0; x < _hi; x++)
                {
                    int i = y * _hi + x;
                    float a = _px[i].W;
                    if (a <= 0f) continue;
                    float tx = x / (float)(_hi - 1);
                    float f = 1f + strength * (0.85f - 1.5f * ty - 0.5f * tx);
                    var p = _px[i];
                    _px[i] = new Vector4(Math.Min(a, p.X * f), Math.Min(a, p.Y * f), Math.Min(a, p.Z * f), a);
                }
            }
        }

        /// <summary>Adds a dark outline of the given thickness around everything drawn so far.</summary>
        public void Outline(float thickness, Color c)
        {
            int r = Math.Max(1, (int)MathF.Round(thickness * _k));
            var alpha = new float[_hi * _hi];
            for (int i = 0; i < _px.Length; i++) alpha[i] = _px[i].W;

            // Separable max filter (square dilation)
            var tmp = new float[_hi * _hi];
            for (int y = 0; y < _hi; y++)
                for (int x = 0; x < _hi; x++)
                {
                    float m = 0;
                    for (int k = -r; k <= r; k++)
                    {
                        int xx = x + k;
                        if (xx >= 0 && xx < _hi) m = Math.Max(m, alpha[y * _hi + xx]);
                    }
                    tmp[y * _hi + x] = m;
                }
            var dil = new float[_hi * _hi];
            for (int y = 0; y < _hi; y++)
                for (int x = 0; x < _hi; x++)
                {
                    float m = 0;
                    for (int k = -r; k <= r; k++)
                    {
                        int yy = y + k;
                        if (yy >= 0 && yy < _hi) m = Math.Max(m, tmp[yy * _hi + x]);
                    }
                    dil[y * _hi + x] = m;
                }

            float ca = c.A / 255f;
            var outline = new Vector4(c.R / 255f, c.G / 255f, c.B / 255f, 1f) * ca;
            for (int i = 0; i < _px.Length; i++)
            {
                float oa = Math.Min(1f, dil[i]);
                // Outline goes *under* the existing content
                _px[i] = _px[i] + outline * oa * (1f - _px[i].W);
            }
        }

        /// <summary>
        /// Produces the texture. Power-of-two textures get a full mipmap chain, so sprites
        /// drawn much smaller than their source (zoomed-out map) stay clean instead of shimmering.
        /// </summary>
        public Texture2D ToTexture(GraphicsDevice device)
        {
            int n = PixelSize;
            var level = new Vector4[n * n];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    Vector4 acc = Vector4.Zero;
                    for (int sy = 0; sy < SS; sy++)
                        for (int sx = 0; sx < SS; sx++)
                            acc += _px[(y * SS + sy) * _hi + x * SS + sx];
                    level[y * n + x] = acc / (SS * SS);
                }
            }

            bool mips = n >= 32 && (n & (n - 1)) == 0;
            var tex = new Texture2D(device, n, n, mips, SurfaceFormat.Color);
            tex.SetData(0, null, Pack(level), 0, n * n);
            if (mips)
            {
                int mip = 1;
                while (n > 1)
                {
                    int h = n / 2;
                    var next = new Vector4[h * h];
                    for (int y = 0; y < h; y++)
                        for (int x = 0; x < h; x++)
                            next[y * h + x] = (level[(y * 2) * n + x * 2] + level[(y * 2) * n + x * 2 + 1] +
                                               level[(y * 2 + 1) * n + x * 2] + level[(y * 2 + 1) * n + x * 2 + 1]) * 0.25f;
                    tex.SetData(mip++, null, Pack(next), 0, h * h);
                    level = next;
                    n = h;
                }
            }
            return tex;
        }

        private static Color[] Pack(Vector4[] src)
        {
            // SpriteBatch default blend expects premultiplied alpha
            var data = new Color[src.Length];
            for (int i = 0; i < src.Length; i++)
                data[i] = new Color(Math.Clamp(src[i].X, 0, 1), Math.Clamp(src[i].Y, 0, 1), Math.Clamp(src[i].Z, 0, 1), Math.Clamp(src[i].W, 0, 1));
            return data;
        }
    }

    private static readonly Color Outline = new Color(18, 16, 20, 235);
    private static readonly Color WallLight = new Color(232, 222, 198);
    private static readonly Color WallShade = new Color(186, 172, 146);
    private static readonly Color Stone = new Color(170, 170, 176);
    private static readonly Color StoneDark = new Color(122, 122, 132);
    private static readonly Color Window = new Color(255, 226, 120);
    private static readonly Color Door = new Color(92, 62, 40);
    private static readonly Color Glass = new Color(150, 186, 214);
    private static readonly Color GlassDark = new Color(98, 130, 162);
    private static readonly Color MaskLight = new Color(255, 255, 255);
    private static readonly Color MaskShade = new Color(175, 175, 175);

    private static Vector2 V(float x, float y) => new Vector2(x, y);

    /// <summary>A pitched-roof house. Roof goes into the tint mask.</summary>
    private static void House(Canvas b, Canvas m, float x, float baseY, float w, float h, float roofH, bool window = true)
    {
        b.Rect(x, baseY - h, w, h, WallLight);
        b.Rect(x + w * 0.62f, baseY - h, w * 0.38f, h, WallShade);
        if (window && w >= 5)
        {
            b.Rect(x + w * 0.18f, baseY - h * 0.7f, w * 0.22f, h * 0.3f, Window);
            b.Rect(x + w * 0.56f, baseY - h * 0.55f, w * 0.2f, h * 0.55f, Door);
        }
        var roof = new[] { V(x - 1f, baseY - h + 0.3f), V(x + w / 2f, baseY - h - roofH), V(x + w + 1f, baseY - h + 0.3f) };
        b.Poly(new Color(120, 120, 120), roof);
        m.Poly(MaskLight, roof);
        m.Poly(MaskShade, V(x + w / 2f, baseY - h - roofH), V(x + w + 1f, baseY - h + 0.3f), V(x + w / 2f, baseY - h + 0.3f));
    }

    private static (Texture2D, Texture2D) BuildSettlement(GraphicsDevice device, int type)
    {
        int s = SettlementSize;
        var b = new Canvas(s, SettlementScale);
        var m = new Canvas(s, SettlementScale);
        float ground = s - 6;

        switch (type)
        {
            case 0: // Village: two huts and a little field
                b.Rect(3, ground - 1, 34, 4, new Color(150, 120, 70));
                House(b, m, 4, ground, 15, 12, 9);
                House(b, m, 21, ground + 1, 14, 10, 8);
                break;

            case 1: // Town: houses around a church tower
                b.Rect(2, ground - 1, 36, 4, new Color(150, 130, 100));
                House(b, m, 2, ground + 1, 12, 11, 7);
                // Church tower
                b.Rect(15, ground - 22, 9, 22, Stone);
                b.Rect(20, ground - 22, 4, 22, StoneDark);
                b.Rect(17.5f, ground - 16, 3.5f, 5, Window);
                var spire = new[] { V(14, ground - 21.5f), V(19.5f, ground - 32), V(25, ground - 21.5f) };
                b.Poly(new Color(120, 120, 120), spire);
                m.Poly(MaskLight, spire);
                m.Poly(MaskShade, V(19.5f, ground - 32), V(25, ground - 21.5f), V(19.5f, ground - 21.5f));
                House(b, m, 25, ground + 1, 13, 12, 7);
                break;

            case 2: // City: stone wall, keep and dense houses
                House(b, m, 3, ground - 2, 8, 9, 4, false);
                House(b, m, 28, ground - 3, 9, 10, 4, false);
                // Keep
                b.Rect(13, ground - 22, 14, 22, Stone);
                b.Rect(21, ground - 22, 6, 22, StoneDark);
                for (int i = 0; i < 4; i++) b.Rect(13 + i * 4, ground - 25, 2.4f, 3, Stone);
                b.Rect(16, ground - 17, 2.5f, 4, Window);
                b.Rect(22, ground - 17, 2.5f, 4, Window);
                b.Rect(18.5f, ground - 7, 3.5f, 7, Door);
                // Banner on the keep (tinted)
                b.Rect(19.6f, ground - 33, 1.2f, 9, new Color(60, 50, 40));
                var flag = new[] { V(20.8f, ground - 33), V(28, ground - 31), V(20.8f, ground - 29) };
                b.Poly(new Color(120, 120, 120), flag);
                m.Poly(MaskLight, flag);
                // Front wall with crenellations
                b.Rect(1, ground - 5, 38, 6, Stone);
                b.Rect(1, ground - 2, 38, 3, StoneDark);
                for (int i = 0; i < 10; i++) b.Rect(1.5f + i * 3.8f, ground - 7, 2, 2, Stone);
                break;

            default: // Metropolis: skyline of towers
                float[] xs = { 2, 9, 15, 23, 30 };
                float[] ws = { 7, 6, 8, 7, 8 };
                float[] hs = { 14, 24, 32, 20, 12 };
                for (int i = 0; i < xs.Length; i++)
                {
                    float top = ground - hs[i];
                    b.Rect(xs[i], top, ws[i], hs[i], Glass);
                    b.Rect(xs[i] + ws[i] * 0.6f, top, ws[i] * 0.4f, hs[i], GlassDark);
                    for (float wy = top + 3; wy < ground - 2; wy += 3)
                        for (float wx = xs[i] + 1.2f; wx < xs[i] + ws[i] - 1.5f; wx += 2.2f)
                            b.Rect(wx, wy, 1.1f, 1.3f, new Color(255, 236, 150, 220));
                    // Tinted roof band
                    b.Rect(xs[i], top, ws[i], 2.2f, new Color(120, 120, 120));
                    m.Rect(xs[i], top, ws[i], 2.2f, MaskLight);
                }
                // Antenna on the tallest tower
                b.Rect(18.6f, ground - 38, 0.9f, 6, new Color(80, 80, 90));
                b.Circle(19, ground - 38, 1f, new Color(255, 80, 80));
                b.Rect(0, ground, 40, 2, new Color(90, 90, 96));
                break;
        }

        b.Light();
        b.Outline(1.0f, Outline);
        return (b.ToTexture(device), m.ToTexture(device));
    }

    private static Texture2D BuildCrown(GraphicsDevice device)
    {
        var c = new Canvas(SmallIconSize);
        var gold = new Color(255, 208, 64);
        var goldDark = new Color(214, 150, 36);
        c.Poly(gold, V(3, 18), V(2.5f, 8), V(8, 13), V(12, 4.5f), V(16, 13), V(21.5f, 8), V(21, 18));
        // Shaded right half and a jewelled band
        c.Poly(goldDark, V(12, 4.5f), V(16, 13), V(21.5f, 8), V(21, 18), V(12, 18));
        c.Poly(new Color(255, 232, 130), V(4.2f, 10.5f), V(7.6f, 13.6f), V(6.2f, 15.5f), V(4.4f, 15.5f));
        c.Rect(3, 16.5f, 18, 3.5f, new Color(232, 172, 44));
        c.Rect(3, 16.5f, 18, 1f, new Color(255, 232, 150));
        c.Circle(12, 4.6f, 1.7f, new Color(255, 244, 190));
        c.Circle(2.8f, 8, 1.5f, new Color(255, 244, 190));
        c.Circle(21.2f, 8, 1.5f, new Color(255, 244, 190));
        c.Circle(12, 18.2f, 1.5f, new Color(226, 44, 66));
        c.Circle(7, 18.2f, 1.1f, new Color(70, 150, 240));
        c.Circle(17, 18.2f, 1.1f, new Color(70, 200, 120));
        c.Outline(1.1f, Outline);
        return c.ToTexture(device);
    }

    private static Texture2D BuildWallRing(GraphicsDevice device)
    {
        // Drawn squashed into an ellipse around the foot of a walled settlement: the top half
        // behind the buildings, the bottom half in front. Kept slim so it frames the town
        // instead of hiding it.
        const int s = 64;
        var c = new Canvas(s);
        c.Ring(32, 32, 29, 26.2f, Stone);
        c.Ring(32, 32, 27.2f, 26.2f, StoneDark);
        c.Ring(32, 32, 29, 28.3f, new Color(206, 206, 212));
        for (int i = 0; i < 8; i++)
        {
            float a = i * MathF.PI / 4f + MathF.PI / 8f;
            float tx = 32 + MathF.Cos(a) * 27.6f, ty = 32 + MathF.Sin(a) * 27.6f;
            c.Circle(tx, ty, 3.3f, Stone);
            c.Circle(tx + 0.7f, ty + 0.7f, 2f, StoneDark);
            c.Circle(tx - 0.5f, ty - 0.6f, 1.1f, new Color(214, 214, 220));
        }
        // Gate facing the viewer
        c.Rect(28.5f, 56.4f, 7, 4.6f, Stone);
        c.Rect(30.6f, 58, 2.8f, 3f, Door);
        c.Outline(1.1f, Outline);
        return c.ToTexture(device);
    }

    private static (Texture2D, Texture2D) BuildArmy(GraphicsDevice device)
    {
        int s = SmallIconSize;
        var b = new Canvas(s, MapSpriteScale);
        var m = new Canvas(s, MapSpriteScale);
        var steel = new Color(214, 220, 232);
        var steelDark = new Color(150, 158, 176);
        var skin = new Color(236, 196, 156);
        var tintBase = new Color(128, 128, 128);

        // Spears behind the troop
        foreach (float x in new[] { 12.6f, 17.2f, 21.6f })
        {
            b.Rect(x, 10, 0.8f, 12, new Color(110, 80, 50));
            b.Poly(steel, V(x - 0.8f, 10.4f), V(x + 0.4f, 7.4f), V(x + 1.6f, 10.4f));
        }

        // Standard: pole with a swallow-tail banner in the nation's colour
        b.Rect(4.6f, 2, 1.7f, 20.5f, new Color(104, 74, 44));
        b.Circle(5.45f, 2.2f, 1.5f, new Color(240, 206, 96));
        var flag = new[] { V(6.3f, 2.4f), V(18.5f, 3.2f), V(15.2f, 6.2f), V(18.5f, 9.2f), V(6.3f, 10f) };
        b.Poly(tintBase, flag);
        m.Poly(MaskLight, flag);
        m.Poly(MaskShade, V(6.3f, 7f), V(16f, 7f), V(18.5f, 9.2f), V(6.3f, 10f));
        b.Rect(6.3f, 5.5f, 9.4f, 1.3f, new Color(250, 246, 232));
        m.Rect(6.3f, 5.5f, 9.4f, 1.3f, new Color(255, 255, 255, 40));

        // Three soldiers: helmet, face, tunic in the nation's colour and a round shield
        foreach (float x in new[] { 10f, 14.6f, 19.2f })
        {
            b.Rect(x - 2f, 16.6f, 4f, 5.6f, tintBase);
            m.Rect(x - 2f, 16.6f, 4f, 5.6f, MaskShade);
            b.Circle(x, 14.6f, 2f, skin);
            b.Poly(steel, V(x - 2.2f, 14.4f), V(x - 1.6f, 12.6f), V(x, 11.9f), V(x + 1.6f, 12.6f), V(x + 2.2f, 14.4f));
            b.Rect(x - 0.3f, 14.2f, 0.6f, 1.5f, steelDark);
            b.Circle(x - 0.7f, 19.4f, 1.5f, new Color(226, 190, 96));
            b.Circle(x - 0.7f, 19.4f, 0.6f, new Color(150, 110, 50));
        }
        b.Outline(0.9f, Outline);
        return (b.ToTexture(device), m.ToTexture(device));
    }

    private static Texture2D BuildSwords(GraphicsDevice device)
    {
        var c = new Canvas(SmallIconSize, MapSpriteScale);
        var steel = new Color(232, 236, 246);
        var steelDark = new Color(168, 176, 196);
        var hilt = new Color(236, 184, 64);
        var grip = new Color(120, 70, 40);
        void Sword(float dir)
        {
            // Blade from the pommel corner up to the opposite corner
            var tip = V(12 + dir * 8.6f, 2.6f);
            var guard = V(12 - dir * 3.4f, 14.6f);
            var d = Vector2.Normalize(tip - guard);
            var n = new Vector2(-d.Y, d.X);
            c.Poly(steel, guard + n * 1.5f, tip - d * 2.2f + n * 1.5f, tip, tip - d * 2.2f - n * 1.5f, guard - n * 1.5f);
            c.Poly(steelDark, guard, tip, tip - d * 2.2f - n * 1.5f, guard - n * 1.5f);
            c.Line(guard - n * 3.6f, guard + n * 3.6f, 1.9f, hilt);
            c.Line(guard, guard - d * 4.6f, 1.7f, grip);
            var pommel = guard - d * 5.4f;
            c.Circle(pommel.X, pommel.Y, 1.6f, hilt);
        }
        Sword(1f);
        Sword(-1f);
        c.Outline(1.0f, Outline);
        return c.ToTexture(device);
    }

    private static Texture2D BuildFire(GraphicsDevice device)
    {
        var c = new Canvas(SmallIconSize);
        c.Poly(new Color(230, 70, 30), V(12, 2), V(19, 12), V(18, 19), V(12, 22), V(6, 19), V(5, 12), V(9, 9));
        c.Poly(new Color(255, 170, 40), V(12, 8), V(16, 14), V(15, 19), V(12, 21), V(9, 19), V(8, 14));
        c.Poly(new Color(255, 240, 150), V(12, 13), V(14, 17), V(12, 20), V(10, 17));
        c.Outline(1.0f, Outline);
        return c.ToTexture(device);
    }

    private static Texture2D BuildVolcano(GraphicsDevice device)
    {
        var c = new Canvas(SmallIconSize);
        var rock = new Color(104, 84, 72);
        var rockDark = new Color(70, 56, 50);
        c.Poly(rock, V(1, 21), V(9, 7), V(15, 7), V(23, 21));
        c.Poly(rockDark, V(12, 7), V(15, 7), V(23, 21), V(14, 21));
        // Lava in the crater and running down the flank
        c.Poly(new Color(255, 90, 30), V(9, 7), V(15, 7), V(13.5f, 9), V(10.5f, 9));
        c.Poly(new Color(255, 140, 40), V(11, 8.5f), V(13, 8.5f), V(12.8f, 15), V(11.4f, 17), V(11.2f, 12));
        c.Circle(12, 7.4f, 1.6f, new Color(255, 220, 120));
        c.Outline(1.0f, Outline);
        return c.ToTexture(device);
    }

    private static Texture2D BuildSmoke(GraphicsDevice device)
    {
        var c = new Canvas(SmallIconSize);
        c.Circle(9, 15, 5.5f, new Color(90, 90, 96, 200));
        c.Circle(15, 10, 6.5f, new Color(110, 110, 118, 200));
        c.Circle(11, 7, 4.5f, new Color(135, 135, 142, 200));
        return c.ToTexture(device);
    }

    private static Texture2D BuildHunger(GraphicsDevice device)
    {
        var c = new Canvas(SmallIconSize);
        // Empty bowl with a red warning dot
        c.Poly(new Color(170, 120, 70), V(3, 11), V(21, 11), V(18, 18), V(6, 18));
        c.Rect(3, 10, 18, 2, new Color(200, 150, 90));
        c.Circle(18, 6, 4.2f, new Color(220, 50, 50));
        c.Rect(17.4f, 3.4f, 1.4f, 3.4f, Color.White);
        c.Rect(17.4f, 7.6f, 1.4f, 1.3f, Color.White);
        c.Outline(1.0f, Outline);
        return c.ToTexture(device);
    }

    // ------------------------------------------------------------------
    // Society sprites (24 px)
    // ------------------------------------------------------------------

    private static Texture2D BuildCoolingTower(GraphicsDevice device)
    {
        var c = new Canvas(SmallIconSize);
        var concrete = new Color(214, 214, 220);
        var shade = new Color(160, 160, 172);
        // Hyperboloid tower: wide base, pinched waist, flared top
        c.Poly(concrete, V(4, 22), V(7.5f, 13), V(6.5f, 7), V(17.5f, 7), V(16.5f, 13), V(20, 22));
        c.Poly(shade, V(12, 7), V(17.5f, 7), V(16.5f, 13), V(20, 22), V(13, 22));
        c.Rect(6.8f, 7, 10.4f, 1.4f, new Color(120, 120, 130));
        // Steam
        c.Circle(10, 4.5f, 3f, new Color(245, 245, 250, 230));
        c.Circle(14.5f, 3.5f, 2.6f, new Color(235, 235, 245, 230));
        // Radiation dot
        c.Circle(12, 16, 2.2f, new Color(255, 214, 40));
        c.Circle(12, 16, 0.8f, new Color(40, 30, 20));
        c.Outline(1.0f, Outline);
        return c.ToTexture(device);
    }

    private static Texture2D BuildSolarPanel(GraphicsDevice device)
    {
        var c = new Canvas(SmallIconSize);
        c.Rect(11.2f, 14, 1.6f, 8, new Color(140, 140, 150));
        c.Poly(new Color(36, 70, 150), V(3, 15), V(7, 5), V(21, 5), V(21, 15));
        // Cell grid
        for (int i = 1; i < 3; i++)
        {
            float y = 5 + i * 10f / 3f;
            float x0 = 7 - 4 * (y - 5) / 10f;
            c.Line(V(x0, y), V(21, y), 0.7f, new Color(150, 200, 255));
        }
        for (int i = 1; i < 4; i++)
        {
            float xt = 7 + i * 14f / 4f;
            float xb = 3 + i * 18f / 4f;
            c.Line(V(xt, 5), V(xb, 15), 0.7f, new Color(150, 200, 255));
        }
        c.Poly(new Color(255, 255, 255, 90), V(7, 5), V(12, 5), V(9, 10));
        c.Outline(1.0f, Outline);
        return c.ToTexture(device);
    }

    private static Texture2D BuildWindTurbine(GraphicsDevice device)
    {
        var c = new Canvas(SmallIconSize);
        var white = new Color(240, 242, 248);
        c.Poly(new Color(210, 212, 222), V(11.2f, 9), V(12.8f, 9), V(13.6f, 23), V(10.4f, 23));
        var hub = V(12, 8);
        for (int i = 0; i < 3; i++)
        {
            float a = -MathF.PI / 2f + i * MathF.PI * 2f / 3f + 0.35f;
            var dir = new Vector2(MathF.Cos(a), MathF.Sin(a));
            var n = new Vector2(-dir.Y, dir.X);
            c.Poly(white, hub + n * 1.2f, hub + dir * 7.5f + n * 0.3f, hub + dir * 7.8f, hub - n * 0.6f);
        }
        c.Circle(12, 8, 1.6f, new Color(180, 184, 196));
        c.Outline(1.0f, Outline);
        return c.ToTexture(device);
    }

    private static Texture2D BuildFactory(GraphicsDevice device)
    {
        var c = new Canvas(SmallIconSize);
        var brick = new Color(150, 96, 72);
        var dark = new Color(110, 70, 56);
        c.Circle(17, 5, 2.6f, new Color(120, 120, 126, 220));
        c.Circle(20, 3, 2.2f, new Color(150, 150, 156, 200));
        c.Rect(15, 5, 3.4f, 12, dark);
        // Saw-tooth hall
        c.Poly(brick, V(2, 22), V(2, 13), V(6, 10), V(6, 13), V(10, 10), V(10, 13), V(14, 10), V(14, 13), V(21, 13), V(21, 22));
        c.Rect(2, 19, 19, 3, dark);
        c.Rect(4, 15, 2.4f, 2.4f, Window);
        c.Rect(9, 15, 2.4f, 2.4f, Window);
        c.Rect(15, 15, 2.4f, 2.4f, Window);
        c.Outline(1.0f, Outline);
        return c.ToTexture(device);
    }

    private static Texture2D BuildDam(GraphicsDevice device)
    {
        var c = new Canvas(SmallIconSize);
        c.Poly(new Color(60, 130, 220), V(1, 8), V(9, 8), V(9, 20), V(1, 20));
        c.Poly(new Color(196, 196, 204), V(8, 5), V(12, 5), V(17, 21), V(8, 21));
        c.Poly(new Color(150, 150, 160), V(10, 5), V(12, 5), V(17, 21), V(12, 21));
        c.Poly(new Color(120, 200, 255), V(15, 17), V(23, 17), V(23, 21), V(16.5f, 21));
        c.Line(V(17, 18.5f), V(22, 18.5f), 0.7f, Color.White);
        c.Outline(1.0f, Outline);
        return c.ToTexture(device);
    }

    private static Texture2D BuildFusion(GraphicsDevice device)
    {
        var c = new Canvas(SmallIconSize);
        c.Ring(12, 12, 10, 7.6f, new Color(200, 200, 216));
        c.Circle(12, 12, 5.2f, new Color(255, 120, 200));
        c.Circle(12, 12, 3f, new Color(255, 220, 245));
        c.Outline(1.0f, Outline);
        return c.ToTexture(device);
    }

    private static Texture2D BuildTrefoil(GraphicsDevice device)
    {
        var c = new Canvas(SmallIconSize);
        var yellow = new Color(255, 214, 40);
        var black = new Color(30, 26, 20);
        c.Circle(12, 12, 10.5f, yellow);
        for (int i = 0; i < 3; i++)
        {
            float a0 = -MathF.PI / 2f + i * MathF.PI * 2f / 3f - 0.5f;
            float a1 = a0 + 1.0f;
            var pts = new List<Vector2> { V(12, 12) };
            for (int k = 0; k <= 8; k++)
            {
                float a = a0 + (a1 - a0) * k / 8f;
                pts.Add(V(12 + MathF.Cos(a) * 8.6f, 12 + MathF.Sin(a) * 8.6f));
            }
            c.Poly(black, pts.ToArray());
        }
        c.Circle(12, 12, 3.2f, yellow);
        c.Circle(12, 12, 2f, black);
        c.Outline(1.0f, Outline);
        return c.ToTexture(device);
    }

    private static Texture2D BuildFlask(GraphicsDevice device)
    {
        var c = new Canvas(SmallIconSize);
        c.Poly(new Color(220, 236, 240, 230), V(9, 2), V(15, 2), V(15, 9), V(21, 21), V(3, 21), V(9, 9));
        c.Poly(new Color(130, 230, 60), V(6.2f, 14), V(17.8f, 14), V(21, 21), V(3, 21));
        c.Circle(10, 17, 1.2f, new Color(210, 255, 160));
        c.Circle(14, 18.5f, 0.9f, new Color(210, 255, 160));
        c.Rect(8, 1.5f, 8, 2, new Color(160, 160, 170));
        c.Outline(1.0f, Outline);
        return c.ToTexture(device);
    }

    private static Texture2D BuildBiohazard(GraphicsDevice device)
    {
        var c = new Canvas(SmallIconSize);
        var orange = new Color(255, 120, 30);
        c.Circle(12, 12, 11, new Color(40, 30, 26));
        for (int i = 0; i < 3; i++)
        {
            float a = -MathF.PI / 2f + i * MathF.PI * 2f / 3f;
            c.Ring(12 + MathF.Cos(a) * 4.6f, 12 + MathF.Sin(a) * 4.6f, 5.2f, 3.4f, orange);
        }
        c.Ring(12, 12, 3.4f, 2.2f, orange);
        c.Outline(1.0f, Outline);
        return c.ToTexture(device);
    }

    private static Texture2D BuildSilo(GraphicsDevice device)
    {
        var c = new Canvas(SmallIconSize);
        // Hatch in the ground with a missile nose poking out
        c.Poly(new Color(90, 96, 88), V(2, 20), V(5, 15), V(19, 15), V(22, 20));
        c.Poly(new Color(50, 54, 50), V(6, 18.5f), V(8, 16), V(16, 16), V(18, 18.5f));
        c.Poly(new Color(230, 230, 236), V(9.5f, 17), V(9.5f, 8), V(12, 2.5f), V(14.5f, 8), V(14.5f, 17));
        c.Poly(new Color(210, 50, 40), V(9.5f, 8), V(12, 2.5f), V(14.5f, 8));
        c.Rect(9.5f, 12, 5, 1.2f, new Color(210, 50, 40));
        c.Outline(1.0f, Outline);
        return c.ToTexture(device);
    }

    private static Texture2D BuildAnchor(GraphicsDevice device)
    {
        var c = new Canvas(SmallIconSize);
        var steel = new Color(210, 222, 240);
        c.Ring(12, 4.5f, 2.6f, 1.3f, steel);
        c.Rect(11, 6.5f, 2, 13, steel);
        c.Rect(7, 9, 10, 1.8f, steel);
        for (int k = 0; k < 10; k++)
        {
            float a0 = MathF.PI * (0.1f + k * 0.08f), a1 = MathF.PI * (0.1f + (k + 1) * 0.08f);
            c.Line(V(12 + MathF.Cos(a0) * 8, 12 + MathF.Sin(a0) * 8), V(12 + MathF.Cos(a1) * 8, 12 + MathF.Sin(a1) * 8), 2f, steel);
        }
        c.Poly(steel, V(2.5f, 13), V(6, 15.5f), V(3, 17));
        c.Poly(steel, V(21.5f, 13), V(18, 15.5f), V(21, 17));
        c.Outline(1.0f, Outline);
        return c.ToTexture(device);
    }

    private static Texture2D BuildPlane(GraphicsDevice device)
    {
        var c = new Canvas(SmallIconSize);
        var body = new Color(240, 244, 250);
        c.Poly(body, V(11, 2), V(13, 2), V(13.5f, 20), V(10.5f, 20));
        c.Poly(body, V(2, 13), V(11, 8), V(13, 8), V(22, 13), V(22, 14.5f), V(13, 12), V(11, 12), V(2, 14.5f));
        c.Poly(body, V(7, 21), V(11, 18), V(13, 18), V(17, 21), V(17, 22), V(7, 22));
        c.Rect(11.3f, 3, 1.4f, 2, new Color(90, 150, 220));
        c.Outline(1.0f, Outline);
        return c.ToTexture(device);
    }

    private static Texture2D BuildRocket(GraphicsDevice device)
    {
        var c = new Canvas(SmallIconSize);
        var body = new Color(236, 238, 244);
        c.Poly(new Color(255, 150, 40), V(10, 18), V(14, 18), V(12, 23.5f));
        c.Poly(new Color(255, 230, 120), V(11, 18), V(13, 18), V(12, 21.5f));
        c.Poly(body, V(9.5f, 18), V(9.5f, 7), V(12, 1.5f), V(14.5f, 7), V(14.5f, 18));
        c.Poly(new Color(200, 60, 60), V(9.5f, 13), V(6, 19), V(9.5f, 18));
        c.Poly(new Color(200, 60, 60), V(14.5f, 13), V(18, 19), V(14.5f, 18));
        c.Circle(12, 9, 1.6f, new Color(90, 150, 220));
        c.Outline(1.0f, Outline);
        return c.ToTexture(device);
    }

    private static Texture2D BuildSatellite(GraphicsDevice device)
    {
        var c = new Canvas(SmallIconSize);
        var panel = new Color(50, 90, 180);
        c.Rect(1, 9, 7.5f, 6, panel);
        c.Rect(15.5f, 9, 7.5f, 6, panel);
        c.Line(V(4.75f, 9), V(4.75f, 15), 0.6f, new Color(150, 200, 255));
        c.Line(V(19.25f, 9), V(19.25f, 15), 0.6f, new Color(150, 200, 255));
        c.Rect(8, 11.3f, 8, 1.4f, new Color(170, 170, 180));
        c.Rect(9, 8, 6, 8, new Color(230, 200, 90));
        c.Rect(12, 8, 3, 8, new Color(200, 170, 70));
        c.Line(V(12, 8), V(12, 4), 0.8f, new Color(200, 200, 210));
        c.Circle(12, 3.5f, 1.4f, new Color(230, 230, 236));
        c.Outline(1.0f, Outline);
        return c.ToTexture(device);
    }

    private static Texture2D BuildStation(GraphicsDevice device)
    {
        var c = new Canvas(SmallIconSize);
        var panel = new Color(50, 90, 180);
        var hull = new Color(226, 228, 236);
        c.Rect(11, 1, 2, 22, new Color(170, 170, 180));
        c.Rect(1, 2, 8, 5, panel);
        c.Rect(15, 2, 8, 5, panel);
        c.Rect(1, 17, 8, 5, panel);
        c.Rect(15, 17, 8, 5, panel);
        c.Rect(9, 3.8f, 6, 1.4f, new Color(170, 170, 180));
        c.Rect(9, 18.8f, 6, 1.4f, new Color(170, 170, 180));
        c.Rect(7, 9.5f, 10, 5, hull);
        c.Circle(12, 12, 3f, hull);
        c.Circle(12, 12, 1.2f, new Color(255, 220, 120));
        c.Outline(1.0f, Outline);
        return c.ToTexture(device);
    }

    private static Texture2D BuildMoonBase(GraphicsDevice device)
    {
        var c = new Canvas(SmallIconSize);
        c.Circle(12, 12, 11, new Color(190, 190, 196));
        c.Circle(7, 8, 2.4f, new Color(160, 160, 168));
        c.Circle(16, 17, 1.8f, new Color(160, 160, 168));
        c.Circle(17, 7, 1.2f, new Color(160, 160, 168));
        c.Circle(12, 14, 5, new Color(150, 210, 255, 230));
        c.Rect(6, 14, 12, 3, new Color(190, 190, 196));
        c.Rect(7, 13.2f, 10, 1, new Color(240, 240, 250));
        c.Outline(1.0f, Outline);
        return c.ToTexture(device);
    }

    private static Texture2D BuildBolt(GraphicsDevice device)
    {
        var c = new Canvas(SmallIconSize);
        c.Poly(new Color(255, 214, 60), V(14, 1), V(5, 13.5f), V(11, 13.5f), V(9, 23), V(19, 9.5f), V(13, 9.5f), V(16, 1));
        c.Outline(1.0f, Outline);
        return c.ToTexture(device);
    }

    private static Texture2D BuildShield(GraphicsDevice device)
    {
        var c = new Canvas(SmallIconSize);
        c.Poly(new Color(90, 170, 255), V(3, 4), V(12, 1.5f), V(21, 4), V(20, 13), V(12, 22.5f), V(4, 13));
        c.Poly(new Color(60, 120, 210), V(12, 1.5f), V(21, 4), V(20, 13), V(12, 22.5f));
        c.Line(V(8, 12), V(11, 15.5f), 2f, Color.White);
        c.Line(V(11, 15.5f), V(16.5f, 7.5f), 2f, Color.White);
        c.Outline(1.0f, Outline);
        return c.ToTexture(device);
    }

    private static Texture2D BuildCapitol(GraphicsDevice device)
    {
        var c = new Canvas(SmallIconSize);
        var marble = new Color(236, 230, 214);
        var shade = new Color(190, 182, 164);
        c.Circle(12, 9, 5f, marble);
        c.Rect(11.4f, 1.5f, 1.2f, 3.5f, new Color(200, 160, 60));
        c.Poly(marble, V(2, 11), V(12, 7), V(22, 11));
        c.Rect(3, 11, 18, 2, shade);
        for (int i = 0; i < 5; i++) c.Rect(4 + i * 3.6f, 13, 1.8f, 6, marble);
        c.Rect(2, 19, 20, 3, shade);
        c.Outline(1.0f, Outline);
        return c.ToTexture(device);
    }

    private static Texture2D BuildNetwork(GraphicsDevice device)
    {
        var c = new Canvas(SmallIconSize);
        c.Circle(12, 12, 10, new Color(40, 90, 160));
        c.Ring(12, 12, 10, 9, new Color(110, 200, 255));
        var node = new Color(120, 240, 255);
        var pts = new[] { V(6, 8), V(15, 5), V(18, 14), V(9, 17), V(12, 11) };
        c.Line(pts[0], pts[4], 1f, node);
        c.Line(pts[1], pts[4], 1f, node);
        c.Line(pts[2], pts[4], 1f, node);
        c.Line(pts[3], pts[4], 1f, node);
        c.Line(pts[0], pts[1], 1f, node * 0.8f);
        c.Line(pts[2], pts[3], 1f, node * 0.8f);
        foreach (var p in pts) c.Circle(p.X, p.Y, 1.7f, Color.White);
        c.Outline(1.0f, Outline);
        return c.ToTexture(device);
    }
}
