using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace SimPlanet;

/// <summary>
/// Shared look and feel for all UI panels: palette, panels with rounded corners and
/// soft shadows, buttons, tooltips and text helpers. Call <see cref="Initialize"/> once
/// from LoadContent before any panel is drawn.
/// </summary>
public static class UITheme
{
    // ---- Palette -------------------------------------------------------
    public static readonly Color Backdrop = new Color(8, 11, 18);
    public static readonly Color PanelBg = new Color(16, 22, 34, 242);
    public static readonly Color PanelBgLight = new Color(24, 32, 48, 245);
    public static readonly Color HeaderBg = new Color(28, 40, 62, 250);
    public static readonly Color Border = new Color(56, 78, 116);
    public static readonly Color BorderBright = new Color(96, 136, 196);
    public static readonly Color Accent = new Color(96, 176, 255);
    public static readonly Color AccentDim = new Color(52, 100, 160);
    public static readonly Color Gold = new Color(255, 206, 92);
    public static readonly Color Text = new Color(226, 234, 246);
    public static readonly Color TextDim = new Color(150, 166, 192);
    public static readonly Color TextMuted = new Color(104, 118, 142);
    public static readonly Color Good = new Color(110, 220, 130);
    public static readonly Color Warn = new Color(255, 184, 76);
    public static readonly Color Bad = new Color(255, 104, 96);
    public static readonly Color ButtonBg = new Color(34, 46, 68);
    public static readonly Color ButtonHover = new Color(52, 72, 106);
    public static readonly Color ButtonActive = new Color(46, 96, 160);
    public static readonly Color Shadow = new Color(0, 0, 0, 110);

    // ---- Font sizes ----------------------------------------------------
    public const float FontSmall = 12f;
    public const float FontNormal = 14f;
    public const float FontMedium = 16f;
    public const float FontLarge = 20f;
    public const float FontTitle = 28f;

    public const int CornerRadius = 6;

    public static Texture2D Pixel { get; private set; } = null!;
    public static FontRenderer? Font { get; private set; }

    private static Texture2D _corner = null!;       // filled quarter circle (top-left)
    private static Texture2D _cornerRing = null!;   // 1px quarter ring (top-left)
    private static Texture2D _softCircle = null!;   // radial falloff, used for glows and shadows
    private static Texture2D _vGradient = null!;    // vertical white->transparent gradient
    private static bool _initialized;

    public static bool IsInitialized => _initialized;

    public static void Initialize(GraphicsDevice device, FontRenderer font)
    {
        Font = font;
        if (_initialized) return;

        Pixel = new Texture2D(device, 1, 1);
        Pixel.SetData(new[] { Color.White });

        int r = CornerRadius;
        _corner = new Texture2D(device, r, r);
        _cornerRing = new Texture2D(device, r, r);
        var fill = new Color[r * r];
        var ring = new Color[r * r];
        for (int y = 0; y < r; y++)
        {
            for (int x = 0; x < r; x++)
            {
                // Distance from the circle centre located at (r, r), sampled with 4x4 supersampling
                float cover = 0f, ringCover = 0f;
                for (int sy = 0; sy < 4; sy++)
                {
                    for (int sx = 0; sx < 4; sx++)
                    {
                        float px = x + (sx + 0.5f) / 4f;
                        float py = y + (sy + 0.5f) / 4f;
                        float d = MathF.Sqrt((r - px) * (r - px) + (r - py) * (r - py));
                        if (d <= r) cover += 1f / 16f;
                        if (d <= r && d >= r - 1.2f) ringCover += 1f / 16f;
                    }
                }
                fill[y * r + x] = Color.White * cover;
                ring[y * r + x] = Color.White * ringCover;
            }
        }
        _corner.SetData(fill);
        _cornerRing.SetData(ring);

        const int cs = 64;
        _softCircle = new Texture2D(device, cs, cs);
        var soft = new Color[cs * cs];
        for (int y = 0; y < cs; y++)
        {
            for (int x = 0; x < cs; x++)
            {
                float dx = (x + 0.5f) / cs * 2f - 1f;
                float dy = (y + 0.5f) / cs * 2f - 1f;
                float d = MathF.Sqrt(dx * dx + dy * dy);
                float a = Math.Clamp(1f - d, 0f, 1f);
                a = a * a * (3f - 2f * a);
                soft[y * cs + x] = Color.White * a;
            }
        }
        _softCircle.SetData(soft);

        _vGradient = new Texture2D(device, 1, 64);
        var grad = new Color[64];
        for (int i = 0; i < 64; i++) grad[i] = Color.White * (1f - i / 63f);
        _vGradient.SetData(grad);

        _initialized = true;
    }

    // ---- Primitive helpers --------------------------------------------

    public static void FillRect(SpriteBatch sb, Rectangle rect, Color color)
    {
        sb.Draw(Pixel, rect, color);
    }

    public static void DrawRectOutline(SpriteBatch sb, Rectangle rect, Color color, int thickness = 1)
    {
        sb.Draw(Pixel, new Rectangle(rect.X, rect.Y, rect.Width, thickness), color);
        sb.Draw(Pixel, new Rectangle(rect.X, rect.Bottom - thickness, rect.Width, thickness), color);
        sb.Draw(Pixel, new Rectangle(rect.X, rect.Y, thickness, rect.Height), color);
        sb.Draw(Pixel, new Rectangle(rect.Right - thickness, rect.Y, thickness, rect.Height), color);
    }

    /// <summary>Vertical gradient overlay: <paramref name="top"/> fading to transparent.</summary>
    public static void FillGradient(SpriteBatch sb, Rectangle rect, Color top)
    {
        sb.Draw(_vGradient, rect, top);
    }

    /// <summary>
    /// Horizontal gradient. With <paramref name="fadeToRight"/> the colour is solid on the left and
    /// transparent on the right; otherwise transparent on the left and solid on the right.
    /// </summary>
    public static void FillGradientHorizontal(SpriteBatch sb, Rectangle rect, Color color, bool fadeToRight = true)
    {
        // Rotate the vertical gradient texture by -90 degrees (top -> left) or +90 (top -> right)
        float rotation = fadeToRight ? -MathF.PI / 2f : MathF.PI / 2f;
        var origin = Vector2.Zero;
        var pos = fadeToRight ? new Vector2(rect.X, rect.Bottom) : new Vector2(rect.Right, rect.Y);
        var scale = new Vector2(rect.Height / 1f, rect.Width / 64f);
        sb.Draw(_vGradient, pos, null, color, rotation, origin, scale, SpriteEffects.None, 0f);
    }

    /// <summary>Soft round glow / blob centred on a point.</summary>
    public static void DrawGlow(SpriteBatch sb, Vector2 center, float radius, Color color)
    {
        int r = Math.Max(1, (int)radius);
        sb.Draw(_softCircle, new Rectangle((int)center.X - r, (int)center.Y - r, r * 2, r * 2), color);
    }

    public static void DrawLine(SpriteBatch sb, Vector2 a, Vector2 b, Color color, float thickness = 1f)
    {
        Vector2 d = b - a;
        float len = d.Length();
        if (len < 0.01f) return;
        float angle = MathF.Atan2(d.Y, d.X);
        sb.Draw(Pixel, a, null, color, angle, new Vector2(0, 0.5f), new Vector2(len, thickness), SpriteEffects.None, 0f);
    }

    /// <summary>Rectangle with anti-aliased rounded corners.</summary>
    public static void FillRounded(SpriteBatch sb, Rectangle rect, Color color)
    {
        int r = CornerRadius;
        if (rect.Width < r * 2 || rect.Height < r * 2)
        {
            sb.Draw(Pixel, rect, color);
            return;
        }

        // Centre cross
        sb.Draw(Pixel, new Rectangle(rect.X + r, rect.Y, rect.Width - 2 * r, rect.Height), color);
        sb.Draw(Pixel, new Rectangle(rect.X, rect.Y + r, r, rect.Height - 2 * r), color);
        sb.Draw(Pixel, new Rectangle(rect.Right - r, rect.Y + r, r, rect.Height - 2 * r), color);

        // Corners
        sb.Draw(_corner, new Rectangle(rect.X, rect.Y, r, r), null, color, 0f, Vector2.Zero, SpriteEffects.None, 0f);
        sb.Draw(_corner, new Rectangle(rect.Right - r, rect.Y, r, r), null, color, 0f, Vector2.Zero, SpriteEffects.FlipHorizontally, 0f);
        sb.Draw(_corner, new Rectangle(rect.X, rect.Bottom - r, r, r), null, color, 0f, Vector2.Zero, SpriteEffects.FlipVertically, 0f);
        sb.Draw(_corner, new Rectangle(rect.Right - r, rect.Bottom - r, r, r), null, color, 0f, Vector2.Zero,
            SpriteEffects.FlipHorizontally | SpriteEffects.FlipVertically, 0f);
    }

    /// <summary>1px anti-aliased rounded outline.</summary>
    public static void OutlineRounded(SpriteBatch sb, Rectangle rect, Color color)
    {
        int r = CornerRadius;
        if (rect.Width < r * 2 || rect.Height < r * 2)
        {
            DrawRectOutline(sb, rect, color);
            return;
        }

        sb.Draw(Pixel, new Rectangle(rect.X + r, rect.Y, rect.Width - 2 * r, 1), color);
        sb.Draw(Pixel, new Rectangle(rect.X + r, rect.Bottom - 1, rect.Width - 2 * r, 1), color);
        sb.Draw(Pixel, new Rectangle(rect.X, rect.Y + r, 1, rect.Height - 2 * r), color);
        sb.Draw(Pixel, new Rectangle(rect.Right - 1, rect.Y + r, 1, rect.Height - 2 * r), color);

        sb.Draw(_cornerRing, new Rectangle(rect.X, rect.Y, r, r), null, color, 0f, Vector2.Zero, SpriteEffects.None, 0f);
        sb.Draw(_cornerRing, new Rectangle(rect.Right - r, rect.Y, r, r), null, color, 0f, Vector2.Zero, SpriteEffects.FlipHorizontally, 0f);
        sb.Draw(_cornerRing, new Rectangle(rect.X, rect.Bottom - r, r, r), null, color, 0f, Vector2.Zero, SpriteEffects.FlipVertically, 0f);
        sb.Draw(_cornerRing, new Rectangle(rect.Right - r, rect.Bottom - r, r, r), null, color, 0f, Vector2.Zero,
            SpriteEffects.FlipHorizontally | SpriteEffects.FlipVertically, 0f);
    }

    /// <summary>Soft drop shadow under a panel.</summary>
    public static void DrawShadow(SpriteBatch sb, Rectangle rect, int spread = 6)
    {
        for (int i = spread; i >= 1; i -= 2)
        {
            var r = new Rectangle(rect.X - i + 3, rect.Y - i + 5, rect.Width + i * 2, rect.Height + i * 2);
            FillRounded(sb, r, new Color(0, 0, 0, 22));
        }
    }

    /// <summary>
    /// Standard floating panel: shadow, rounded background, subtle top highlight and border.
    /// </summary>
    public static void DrawPanel(SpriteBatch sb, Rectangle rect, Color? background = null, Color? border = null, bool shadow = true)
    {
        if (shadow) DrawShadow(sb, rect);
        FillRounded(sb, rect, background ?? PanelBg);
        // Faint sheen at the top for depth
        FillGradient(sb, new Rectangle(rect.X + 2, rect.Y + 1, rect.Width - 4, Math.Min(28, rect.Height / 3)), Color.White * ((10) / 255f));
        OutlineRounded(sb, rect, border ?? Border);
    }

    /// <summary>
    /// Panel with a title bar. Returns the Y coordinate where content should start.
    /// </summary>
    public static int DrawTitledPanel(SpriteBatch sb, Rectangle rect, string title, Color? accent = null, int headerHeight = 34)
    {
        DrawPanel(sb, rect);
        var headerRect = new Rectangle(rect.X + 1, rect.Y + 1, rect.Width - 2, headerHeight - 1);
        FillRounded(sb, headerRect, HeaderBg);
        sb.Draw(Pixel, new Rectangle(rect.X + 1, rect.Y + headerHeight - CornerRadius, rect.Width - 2, CornerRadius), HeaderBg);
        sb.Draw(Pixel, new Rectangle(rect.X + 1, rect.Y + headerHeight, rect.Width - 2, 1), accent ?? Border);
        if (Font != null)
        {
            var size = Font.MeasureString(title, FontMedium);
            DrawTextShadowed(sb, title, new Vector2(rect.X + 12, rect.Y + (headerHeight - size.Y) / 2f), accent ?? Gold, FontMedium);
        }
        return rect.Y + headerHeight + 8;
    }

    // ---- Buttons -------------------------------------------------------

    /// <summary>
    /// Standard button. <paramref name="accent"/> tints the bottom bar / active fill.
    /// </summary>
    public static void DrawButton(SpriteBatch sb, Rectangle rect, string text, bool hovered, bool active = false,
        Color? accent = null, float fontSize = FontNormal, bool enabled = true)
    {
        Color acc = accent ?? Accent;
        Color bg = !enabled ? new Color(28, 34, 46) :
                   active ? Color.Lerp(ButtonBg, acc, 0.45f) :
                   hovered ? ButtonHover : ButtonBg;
        FillRounded(sb, rect, bg);
        FillGradient(sb, new Rectangle(rect.X + 1, rect.Y + 1, rect.Width - 2, rect.Height / 2), Color.White * ((hovered ? 26 : 14) / 255f));
        OutlineRounded(sb, rect, active ? acc : hovered ? BorderBright : Border);

        if (Font != null && !string.IsNullOrEmpty(text))
        {
            var size = Font.MeasureString(text, fontSize);
            var pos = new Vector2(rect.X + (rect.Width - size.X) / 2f, rect.Y + (rect.Height - size.Y) / 2f);
            DrawTextShadowed(sb, text, pos, enabled ? Text : TextMuted, fontSize);
        }
    }

    // ---- Text ----------------------------------------------------------

    public static Vector2 Measure(string text, float fontSize = FontNormal)
    {
        return Font?.MeasureString(text, fontSize) ?? Vector2.Zero;
    }

    public static void DrawText(SpriteBatch sb, string text, Vector2 pos, Color color, float fontSize = FontNormal)
    {
        Font?.DrawString(sb, text, Snap(pos), color, fontSize);
    }

    public static void DrawTextShadowed(SpriteBatch sb, string text, Vector2 pos, Color color, float fontSize = FontNormal)
    {
        if (Font == null) return;
        pos = Snap(pos);
        Font.DrawString(sb, text, pos + new Vector2(1, 1), new Color(0, 0, 0, (int)(color.A * 0.7f)), fontSize);
        Font.DrawString(sb, text, pos, color, fontSize);
    }

    /// <summary>Text with a dark outline, readable on top of any map colour.</summary>
    public static void DrawTextOutlined(SpriteBatch sb, string text, Vector2 pos, Color color, float fontSize = FontNormal)
    {
        if (Font == null) return;
        pos = Snap(pos);
        var outline = new Color(0, 0, 0, (int)(color.A * 0.85f));
        Font.DrawString(sb, text, pos + new Vector2(-1, 0), outline, fontSize);
        Font.DrawString(sb, text, pos + new Vector2(1, 0), outline, fontSize);
        Font.DrawString(sb, text, pos + new Vector2(0, -1), outline, fontSize);
        Font.DrawString(sb, text, pos + new Vector2(0, 1), outline, fontSize);
        Font.DrawString(sb, text, pos + new Vector2(1, 1), outline, fontSize);
        Font.DrawString(sb, text, pos, color, fontSize);
    }

    public static void DrawTextCentered(SpriteBatch sb, string text, Rectangle rect, Color color, float fontSize = FontNormal)
    {
        var size = Measure(text, fontSize);
        DrawTextShadowed(sb, text, new Vector2(rect.X + (rect.Width - size.X) / 2f, rect.Y + (rect.Height - size.Y) / 2f), color, fontSize);
    }

    /// <summary>Truncates text with an ellipsis so it fits in <paramref name="maxWidth"/>.</summary>
    public static string Ellipsize(string text, float maxWidth, float fontSize = FontNormal)
    {
        if (Font == null || string.IsNullOrEmpty(text)) return text;
        if (Font.MeasureString(text, fontSize).X <= maxWidth) return text;
        const string dots = "...";
        int lo = 0, hi = text.Length;
        while (lo < hi)
        {
            int mid = (lo + hi + 1) / 2;
            if (Font.MeasureString(text.Substring(0, mid) + dots, fontSize).X <= maxWidth) lo = mid;
            else hi = mid - 1;
        }
        return text.Substring(0, lo).TrimEnd() + dots;
    }

    /// <summary>Greedy word wrap.</summary>
    public static List<string> WrapText(string text, float maxWidth, float fontSize = FontNormal)
    {
        var lines = new List<string>();
        if (Font == null || string.IsNullOrEmpty(text)) { lines.Add(text ?? ""); return lines; }
        foreach (var paragraph in text.Split('\n'))
        {
            var words = paragraph.Split(' ');
            string line = "";
            foreach (var w in words)
            {
                string candidate = line.Length == 0 ? w : line + " " + w;
                if (Font.MeasureString(candidate, fontSize).X > maxWidth && line.Length > 0)
                {
                    lines.Add(line);
                    line = w;
                }
                else
                {
                    line = candidate;
                }
            }
            lines.Add(line);
        }
        return lines;
    }

    /// <summary>
    /// Tooltip box placed near <paramref name="anchor"/>, kept inside the screen.
    /// Supports multi-line text separated by '\n'.
    /// </summary>
    public static void DrawTooltip(SpriteBatch sb, string text, Point anchor, int screenWidth, int screenHeight, bool preferAbove = false)
    {
        if (Font == null || string.IsNullOrEmpty(text)) return;
        var lines = text.Split('\n');
        float w = 0, lineH = Font.MeasureString("Ag", FontNormal).Y + 2;
        foreach (var l in lines) w = Math.Max(w, Font.MeasureString(l, FontNormal).X);
        int pad = 8;
        int boxW = (int)w + pad * 2;
        int boxH = (int)(lineH * lines.Length) + pad * 2 - 2;

        int x = anchor.X - boxW / 2;
        int y = preferAbove ? anchor.Y - boxH - 8 : anchor.Y + 8;
        if (y + boxH > screenHeight - 4) y = anchor.Y - boxH - 8;
        if (y < 4) y = anchor.Y + 8;
        x = Math.Clamp(x, 4, Math.Max(4, screenWidth - boxW - 4));

        var rect = new Rectangle(x, y, boxW, boxH);
        DrawPanel(sb, rect, new Color(12, 16, 26, 248), Gold * 0.8f);
        float ty = y + pad;
        for (int i = 0; i < lines.Length; i++)
        {
            DrawTextShadowed(sb, lines[i], new Vector2(x + pad, ty), i == 0 ? Text : TextDim, FontNormal);
            ty += lineH;
        }
    }

    /// <summary>Horizontal meter bar (0..1) with optional label drawn over it.</summary>
    public static void DrawMeter(SpriteBatch sb, Rectangle rect, float value, Color fill, string? label = null)
    {
        value = Math.Clamp(value, 0f, 1f);
        sb.Draw(Pixel, rect, new Color(0, 0, 0, 120));
        if (value > 0)
            sb.Draw(Pixel, new Rectangle(rect.X, rect.Y, Math.Max(1, (int)(rect.Width * value)), rect.Height), fill);
        FillGradient(sb, new Rectangle(rect.X, rect.Y, rect.Width, Math.Max(1, rect.Height / 2)), Color.White * ((30) / 255f));
        DrawRectOutline(sb, rect, new Color(0, 0, 0, 140));
        if (label != null)
            DrawTextCentered(sb, label, rect, Text, FontSmall);
    }

    private static Vector2 Snap(Vector2 v) => new Vector2(MathF.Round(v.X), MathF.Round(v.Y));
}
