using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace SimPlanet;

/// <summary>
/// Smooth vector primitives for the map overlays (routes, flows, markers).
///
/// SpriteBatch has no anti-aliasing of its own: a stretched 1x1 pixel gives jagged lines.
/// Lines here are drawn with tiny textures that carry a one-texel transparent edge, picked
/// by thickness so the soft edge is always about a pixel wide; dots and rings use a
/// pre-rendered anti-aliased disc. Must be drawn with linear filtering.
/// </summary>
public static class MapGfx
{
    private static readonly int[] Cores = { 1, 2, 3, 4, 6, 9 };
    private static Texture2D[]? _lines;
    private static Texture2D? _disc;
    private static Texture2D? _ring;
    private const int DiscSize = 64;

    /// <summary>Creates the shared textures (cheap; call before drawing).</summary>
    public static void Ensure(GraphicsDevice device)
    {
        if (_lines != null && _disc != null && !_disc.IsDisposed) return;

        _lines = new Texture2D[Cores.Length];
        for (int i = 0; i < Cores.Length; i++)
        {
            int h = Cores[i] + 2;
            var data = new Color[2 * h];
            for (int y = 1; y <= Cores[i]; y++)
            {
                data[y * 2] = Color.White;
                data[y * 2 + 1] = Color.White;
            }
            _lines[i] = new Texture2D(device, 2, h);
            _lines[i].SetData(data);
        }

        _disc = BuildRadial(device, DiscSize, 0f);
        _ring = BuildRadial(device, DiscSize, 0.78f);
    }

    private static Texture2D BuildRadial(GraphicsDevice device, int size, float innerRatio)
    {
        var data = new Color[size * size];
        float r = size / 2f - 1f;
        float inner = r * innerRatio;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x + 0.5f - size / 2f, dy = y + 0.5f - size / 2f;
                float d = MathF.Sqrt(dx * dx + dy * dy);
                float a = Math.Clamp(r - d, 0f, 1f);
                if (innerRatio > 0f) a *= Math.Clamp(d - inner, 0f, 1f);
                data[y * size + x] = Color.White * a;
            }
        }
        var tex = new Texture2D(device, size, size, true, SurfaceFormat.Color);
        tex.SetData(0, null, data, 0, data.Length);
        // Mip chain so small dots stay round
        int n = size;
        int level = 1;
        while (n > 1)
        {
            int h = n / 2;
            var next = new Color[h * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < h; x++)
                {
                    var c0 = data[(y * 2) * n + x * 2]; var c1 = data[(y * 2) * n + x * 2 + 1];
                    var c2 = data[(y * 2 + 1) * n + x * 2]; var c3 = data[(y * 2 + 1) * n + x * 2 + 1];
                    byte v = (byte)((c0.A + c1.A + c2.A + c3.A + 2) / 4);
                    next[y * h + x] = new Color(v, v, v, v);
                }
            tex.SetData(level++, null, next, 0, next.Length);
            data = next;
            n = h;
        }
        return tex;
    }

    /// <summary>Anti-aliased line segment. Colours are premultiplied (use <c>color * alpha</c>).</summary>
    public static void Line(SpriteBatch sb, Vector2 a, Vector2 b, Color color, float thickness = 1f)
    {
        if (_lines == null) { UITheme.DrawLine(sb, a, b, color, thickness); return; }
        Vector2 d = b - a;
        float len = d.Length();
        if (len < 0.01f) return;
        // Hairlines keep their width and fade instead, so they never break up
        if (thickness < 1f)
        {
            color *= Math.Max(0.15f, thickness);
            thickness = 1f;
        }
        int i = 0;
        while (i < Cores.Length - 1 && Cores[i + 1] <= thickness + 0.01f) i++;
        var tex = _lines[i];
        float angle = MathF.Atan2(d.Y, d.X);
        // Slightly longer than the segment so the joints of a polyline don't show gaps
        float cap = Math.Min(thickness * 0.35f, 1.5f);
        var start = a - d / len * cap;
        sb.Draw(tex, start, null, color, angle, new Vector2(0, tex.Height / 2f),
            new Vector2((len + cap * 2f) / tex.Width, thickness / Cores[i]), SpriteEffects.None, 0f);
    }

    /// <summary>Polyline drawn with <see cref="Line"/>.</summary>
    public static void Polyline(SpriteBatch sb, Vector2[] pts, Color color, float thickness)
    {
        for (int i = 1; i < pts.Length; i++) Line(sb, pts[i - 1], pts[i], color, thickness);
    }

    /// <summary>Filled anti-aliased disc.</summary>
    public static void Dot(SpriteBatch sb, Vector2 center, float radius, Color color)
    {
        if (_disc == null) return;
        float s = radius * 2f / (DiscSize - 2);
        sb.Draw(_disc, center, null, color, 0f, new Vector2(DiscSize / 2f), s, SpriteEffects.None, 0f);
    }

    /// <summary>Anti-aliased ring (its stroke is about a fifth of the radius).</summary>
    public static void Ring(SpriteBatch sb, Vector2 center, float radius, Color color)
    {
        if (_ring == null) return;
        float s = radius * 2f / (DiscSize - 2);
        sb.Draw(_ring, center, null, color, 0f, new Vector2(DiscSize / 2f), s, SpriteEffects.None, 0f);
    }

    /// <summary>Filled ellipse rotated to an angle.</summary>
    public static void Ellipse(SpriteBatch sb, Vector2 center, float angle, float length, float width, Color color)
    {
        if (_disc == null) return;
        sb.Draw(_disc, center, null, color, angle, new Vector2(DiscSize / 2f),
            new Vector2(Math.Max(1f, length) / (DiscSize - 2), Math.Max(1f, width) / (DiscSize - 2)), SpriteEffects.None, 0f);
    }

    /// <summary>Filled triangle-ish arrow head pointing along <paramref name="dir"/> (unit vector), tip at <paramref name="tip"/>.</summary>
    public static void ArrowHead(SpriteBatch sb, Vector2 tip, Vector2 dir, float size, Color color)
    {
        var n = new Vector2(-dir.Y, dir.X);
        // Fan of shortening strokes fills the head without a triangle primitive
        int steps = Math.Max(2, (int)(size * 0.9f));
        for (int i = 0; i <= steps; i++)
        {
            float t = i / (float)steps;
            var p = tip - dir * (size * t);
            float half = size * 0.55f * t;
            if (half < 0.35f) continue;
            Line(sb, p - n * half, p + n * half, color, size / steps + 0.9f);
        }
    }
}
