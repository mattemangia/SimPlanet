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
    public const int SettlementSize = 40;
    public const int SmallIconSize = 24;

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
        public readonly int Size;
        private readonly int _hi;
        private readonly Vector4[] _px; // premultiplied RGBA

        public Canvas(int size)
        {
            Size = size;
            _hi = size * SS;
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
            int x0 = (int)MathF.Round(x * SS), y0 = (int)MathF.Round(y * SS);
            int x1 = (int)MathF.Round((x + w) * SS), y1 = (int)MathF.Round((y + h) * SS);
            for (int py = Math.Max(0, y0); py < Math.Min(_hi, y1); py++)
                for (int px = Math.Max(0, x0); px < Math.Min(_hi, x1); px++)
                    Blend(py * _hi + px, c);
        }

        public void Circle(float cx, float cy, float r, Color c)
        {
            float r2 = r * r;
            int x0 = (int)((cx - r) * SS) - 1, x1 = (int)((cx + r) * SS) + 1;
            int y0 = (int)((cy - r) * SS) - 1, y1 = (int)((cy + r) * SS) + 1;
            for (int py = Math.Max(0, y0); py < Math.Min(_hi, y1); py++)
            {
                for (int px = Math.Max(0, x0); px < Math.Min(_hi, x1); px++)
                {
                    float fx = (px + 0.5f) / SS - cx, fy = (py + 0.5f) / SS - cy;
                    if (fx * fx + fy * fy <= r2) Blend(py * _hi + px, c);
                }
            }
        }

        public void Ring(float cx, float cy, float rOuter, float rInner, Color c)
        {
            float o2 = rOuter * rOuter, i2 = rInner * rInner;
            for (int py = 0; py < _hi; py++)
            {
                for (int px = 0; px < _hi; px++)
                {
                    float fx = (px + 0.5f) / SS - cx, fy = (py + 0.5f) / SS - cy;
                    float d = fx * fx + fy * fy;
                    if (d <= o2 && d >= i2) Blend(py * _hi + px, c);
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
            int x0 = Math.Max(0, (int)(minX * SS) - 1), x1 = Math.Min(_hi, (int)(maxX * SS) + 1);
            int y0 = Math.Max(0, (int)(minY * SS) - 1), y1 = Math.Min(_hi, (int)(maxY * SS) + 1);
            for (int py = y0; py < y1; py++)
            {
                float fy = (py + 0.5f) / SS;
                for (int px = x0; px < x1; px++)
                {
                    float fx = (px + 0.5f) / SS;
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

        /// <summary>Adds a dark outline of the given thickness around everything drawn so far.</summary>
        public void Outline(float thickness, Color c)
        {
            int r = Math.Max(1, (int)MathF.Round(thickness * SS));
            var alpha = new float[_hi * _hi];
            for (int i = 0; i < _px.Length; i++) alpha[i] = _px[i].W;

            // Separable max filter (square dilation, then trimmed to a disc)
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

        public Texture2D ToTexture(GraphicsDevice device)
        {
            var data = new Color[Size * Size];
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    Vector4 acc = Vector4.Zero;
                    for (int sy = 0; sy < SS; sy++)
                        for (int sx = 0; sx < SS; sx++)
                            acc += _px[(y * SS + sy) * _hi + x * SS + sx];
                    acc /= SS * SS;
                    // SpriteBatch default blend expects premultiplied alpha
                    data[y * Size + x] = new Color(Math.Clamp(acc.X, 0, 1), Math.Clamp(acc.Y, 0, 1), Math.Clamp(acc.Z, 0, 1), Math.Clamp(acc.W, 0, 1));
                }
            }
            var tex = new Texture2D(device, Size, Size);
            tex.SetData(data);
            return tex;
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
        var b = new Canvas(s);
        var m = new Canvas(s);
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

        b.Outline(1.1f, Outline);
        return (b.ToTexture(device), m.ToTexture(device));
    }

    private static Texture2D BuildCrown(GraphicsDevice device)
    {
        var c = new Canvas(SmallIconSize);
        var gold = new Color(255, 205, 60);
        c.Poly(gold, V(3, 18), V(3, 8), V(8, 13), V(12, 5), V(16, 13), V(21, 8), V(21, 18));
        c.Rect(3, 17, 18, 3, new Color(220, 160, 40));
        c.Circle(12, 5, 1.6f, new Color(255, 240, 180));
        c.Circle(3, 8, 1.4f, new Color(255, 240, 180));
        c.Circle(21, 8, 1.4f, new Color(255, 240, 180));
        c.Circle(12, 15, 1.6f, new Color(220, 40, 60));
        c.Outline(1.1f, Outline);
        return c.ToTexture(device);
    }

    private static Texture2D BuildWallRing(GraphicsDevice device)
    {
        const int s = 64;
        var c = new Canvas(s);
        c.Ring(32, 32, 29, 25, Stone);
        c.Ring(32, 32, 26.5f, 25, StoneDark);
        for (int i = 0; i < 8; i++)
        {
            float a = i * MathF.PI / 4f;
            c.Circle(32 + MathF.Cos(a) * 27, 32 + MathF.Sin(a) * 27, 4f, Stone);
            c.Circle(32 + MathF.Cos(a) * 27 + 0.8f, 32 + MathF.Sin(a) * 27 + 0.8f, 2.4f, StoneDark);
        }
        c.Outline(1.2f, Outline);
        return c.ToTexture(device);
    }

    private static (Texture2D, Texture2D) BuildArmy(GraphicsDevice device)
    {
        int s = SmallIconSize;
        var b = new Canvas(s);
        var m = new Canvas(s);
        // Pole
        b.Rect(5, 2, 1.8f, 20, new Color(92, 66, 40));
        b.Circle(5.9f, 2.2f, 1.4f, new Color(230, 200, 90));
        // Swallow-tail banner (tinted)
        var flag = new[] { V(6.8f, 3), V(21, 3), V(17, 8), V(21, 13), V(6.8f, 13) };
        b.Poly(new Color(128, 128, 128), flag);
        m.Poly(MaskLight, flag);
        m.Poly(MaskShade, V(6.8f, 9.5f), V(19, 9.5f), V(21, 13), V(6.8f, 13));
        // Emblem
        b.Circle(12, 8, 2.2f, new Color(245, 240, 225));
        // Shield at the foot
        b.Poly(new Color(210, 210, 220), V(9, 15), V(17, 15), V(17, 18.5f), V(13, 22), V(9, 18.5f));
        b.Rect(12.4f, 15, 1.2f, 6.5f, new Color(150, 40, 40));
        b.Outline(1.0f, Outline);
        return (b.ToTexture(device), m.ToTexture(device));
    }

    private static Texture2D BuildSwords(GraphicsDevice device)
    {
        var c = new Canvas(SmallIconSize);
        var steel = new Color(225, 230, 240);
        var hilt = new Color(230, 180, 60);
        c.Line(V(4, 4), V(18, 18), 2.4f, steel);
        c.Line(V(20, 4), V(6, 18), 2.4f, steel);
        c.Line(V(14, 19), V(19, 14), 2.0f, hilt);
        c.Line(V(10, 19), V(5, 14), 2.0f, hilt);
        c.Circle(20, 20, 1.8f, hilt);
        c.Circle(4, 20, 1.8f, hilt);
        c.Outline(1.1f, Outline);
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
