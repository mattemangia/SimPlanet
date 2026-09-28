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
public class MapIcons : IDisposable
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
    }

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
}
