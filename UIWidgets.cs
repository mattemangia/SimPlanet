using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace SimPlanet;

/// <summary>
/// Reusable controls built on <see cref="UITheme"/>: windows with a close button, section
/// headers, label/value rows, sliders, switches, coloured action buttons, list rows, tool
/// status bars and toasts. Every widget takes the rectangle it is drawn in, so a panel can use
/// the same rectangle for hit-testing in Update and for drawing.
/// </summary>
public static class UIWidgets
{
    public const int HeaderHeight = 36;
    public const int Padding = 14;
    public const int RowHeight = 21;
    public const int ButtonHeight = 30;

    // ---- Mouse helpers -----------------------------------------------------------------

    public static bool Hovered(Rectangle rect) => rect.Contains(Mouse.GetState().Position);

    public static bool Pressed(Rectangle rect)
    {
        var mouse = Mouse.GetState();
        return mouse.LeftButton == ButtonState.Pressed && rect.Contains(mouse.Position);
    }

    // ---- Windows -----------------------------------------------------------------------

    /// <summary>Dims everything behind a modal window.</summary>
    public static void DrawBackdrop(SpriteBatch sb, int screenWidth, int screenHeight, float strength = 0.62f)
    {
        UITheme.FillRect(sb, new Rectangle(0, 0, screenWidth, screenHeight), new Color(4, 6, 12) * strength);
    }

    /// <summary>Where the close button of a window with the given header sits.</summary>
    public static Rectangle CloseButtonRect(Rectangle panel, int headerHeight = HeaderHeight)
    {
        int size = Math.Min(22, headerHeight - 10);
        return new Rectangle(panel.Right - size - 8, panel.Y + (headerHeight - size) / 2 + 1, size, size);
    }

    /// <summary>Round close button with a cross; turns red under the mouse.</summary>
    public static void DrawCloseButton(SpriteBatch sb, Rectangle rect)
    {
        bool hovered = Hovered(rect);
        bool pressed = hovered && Pressed(rect);
        UITheme.FillRounded(sb, rect, pressed ? new Color(150, 44, 44) : hovered ? new Color(196, 64, 60) : new Color(52, 64, 88));
        UITheme.OutlineRounded(sb, rect, hovered ? new Color(255, 150, 140) : UITheme.Border);
        var c = new Vector2(rect.X + rect.Width / 2f, rect.Y + rect.Height / 2f);
        float r = rect.Width * 0.2f;
        Color cross = hovered ? Color.White : UITheme.Text;
        UITheme.DrawLine(sb, c + new Vector2(-r, -r), c + new Vector2(r, r), cross, 1.6f);
        UITheme.DrawLine(sb, c + new Vector2(-r, r), c + new Vector2(r, -r), cross, 1.6f);
    }

    /// <summary>
    /// Titled window with an optional close button and an optional note on the right of the
    /// title bar. Returns the Y coordinate where the content starts.
    /// </summary>
    public static int DrawWindow(SpriteBatch sb, Rectangle panel, string title, Color? accent = null,
        bool closeButton = true, string? note = null, int headerHeight = HeaderHeight)
    {
        int reserved = closeButton ? 44 : 14;
        float noteWidth = string.IsNullOrEmpty(note) ? 0 : UITheme.Measure(note, UITheme.FontSmall).X + 16;
        string fitted = UITheme.Ellipsize(title, panel.Width - 24 - reserved - noteWidth, UITheme.FontMedium);
        UITheme.DrawTitledPanel(sb, panel, fitted, accent ?? UITheme.Gold, headerHeight);

        if (!string.IsNullOrEmpty(note))
        {
            var size = UITheme.Measure(note, UITheme.FontSmall);
            UITheme.DrawText(sb, note, new Vector2(panel.Right - reserved - size.X, panel.Y + (headerHeight - size.Y) / 2f + 1),
                UITheme.TextMuted, UITheme.FontSmall);
        }
        if (closeButton) DrawCloseButton(sb, CloseButtonRect(panel, headerHeight));
        return panel.Y + headerHeight + 10;
    }

    /// <summary>Inset card used to group content inside a window.</summary>
    public static void DrawCard(SpriteBatch sb, Rectangle rect)
    {
        UITheme.FillRounded(sb, rect, new Color(10, 14, 23, 200));
        UITheme.OutlineRounded(sb, rect, UITheme.Border * 0.75f);
    }

    // ---- Text rows ---------------------------------------------------------------------

    /// <summary>Small accent caption followed by a divider line. Returns the next Y.</summary>
    public static int DrawSection(SpriteBatch sb, int x, int y, int width, string title, Color? color = null)
    {
        UITheme.DrawText(sb, title, new Vector2(x, y), color ?? UITheme.Accent, UITheme.FontSmall);
        var size = UITheme.Measure(title, UITheme.FontSmall);
        int lineX = x + (int)size.X + 10;
        if (lineX < x + width)
            UITheme.FillRect(sb, new Rectangle(lineX, y + 8, x + width - lineX, 1), UITheme.Border);
        return y + 22;
    }

    public static void DrawDivider(SpriteBatch sb, int x, int y, int width)
    {
        UITheme.FillRect(sb, new Rectangle(x, y, width, 1), UITheme.Border);
    }

    /// <summary>Dim label on the left, value right-aligned at the column edge. Returns the next Y.</summary>
    public static int DrawKeyValue(SpriteBatch sb, int x, int y, int width, string label, string value,
        Color? valueColor = null, float fontSize = UITheme.FontNormal)
    {
        UITheme.DrawText(sb, label, new Vector2(x, y), UITheme.TextDim, fontSize);
        float labelWidth = UITheme.Measure(label, fontSize).X;
        value = UITheme.Ellipsize(value, Math.Max(20, width - labelWidth - 12), fontSize);
        var size = UITheme.Measure(value, fontSize);
        UITheme.DrawText(sb, value, new Vector2(x + width - size.X, y), valueColor ?? UITheme.Text, fontSize);
        return y + RowHeight;
    }

    /// <summary>Label, value and a meter bar underneath. Returns the next Y.</summary>
    public static int DrawMeterRow(SpriteBatch sb, int x, int y, int width, string label, string value, float value01, Color fill)
    {
        DrawKeyValue(sb, x, y, width, label, value, UITheme.Text, UITheme.FontSmall);
        UITheme.DrawMeter(sb, new Rectangle(x, y + 17, width, 6), value01, fill);
        return y + 32;
    }

    /// <summary>Word-wrapped paragraph. Returns the next Y.</summary>
    public static int DrawParagraph(SpriteBatch sb, int x, int y, int width, string text, Color? color = null,
        float fontSize = UITheme.FontSmall)
    {
        int lineHeight = (int)fontSize + 5;
        foreach (var line in UITheme.WrapText(text, width, fontSize))
        {
            UITheme.DrawText(sb, line, new Vector2(x, y), color ?? UITheme.TextDim, fontSize);
            y += lineHeight;
        }
        return y;
    }

    /// <summary>Small rounded label such as a status badge.</summary>
    public static Rectangle DrawPill(SpriteBatch sb, int x, int y, string text, Color color)
    {
        var size = UITheme.Measure(text, 11f);
        var rect = new Rectangle(x, y, (int)size.X + 16, 18);
        UITheme.FillRounded(sb, rect, Color.Lerp(new Color(16, 22, 34), color, 0.32f));
        UITheme.OutlineRounded(sb, rect, color * 0.8f);
        UITheme.DrawTextCentered(sb, text, rect, Color.Lerp(color, Color.White, 0.45f), 11f);
        return rect;
    }

    /// <summary>Colour chip with a thin light outline (legend entries, list markers).</summary>
    public static void DrawSwatch(SpriteBatch sb, Rectangle rect, Color color)
    {
        UITheme.FillRect(sb, rect, color);
        UITheme.DrawRectOutline(sb, rect, Color.White * 0.3f);
    }

    /// <summary>Legend entry: swatch and label. Returns the next Y.</summary>
    public static int DrawLegendRow(SpriteBatch sb, int x, int y, int width, Color color, string text)
    {
        DrawSwatch(sb, new Rectangle(x, y + 2, 14, 12), color);
        UITheme.DrawText(sb, UITheme.Ellipsize(text, width - 22, UITheme.FontSmall), new Vector2(x + 22, y + 1), UITheme.Text, UITheme.FontSmall);
        return y + 20;
    }

    // ---- Controls ----------------------------------------------------------------------

    /// <summary>
    /// Slider with a thin track, a coloured fill and a round knob. <paramref name="hit"/> is the
    /// rectangle used for hit-testing: the knob travels across its full width, so the value under
    /// the mouse is (mouse.X - hit.X) / hit.Width.
    /// </summary>
    public static void DrawSlider(SpriteBatch sb, Rectangle hit, float value01, bool dragging, Color? fill = null)
    {
        value01 = Math.Clamp(value01, 0f, 1f);
        bool hovered = dragging || Hovered(hit);
        Color color = fill ?? UITheme.Accent;
        int cy = hit.Y + hit.Height / 2;
        var track = new Rectangle(hit.X, cy - 3, hit.Width, 6);
        UITheme.FillRect(sb, track, new Color(0, 0, 0, 150));
        UITheme.DrawRectOutline(sb, track, hovered ? UITheme.BorderBright * 0.8f : UITheme.Border);
        int knobX = hit.X + (int)MathF.Round(hit.Width * value01);
        if (knobX > hit.X + 1)
            UITheme.FillRect(sb, new Rectangle(hit.X + 1, cy - 2, knobX - hit.X - 1, 4), dragging ? Color.Lerp(color, Color.White, 0.25f) : color);

        // Knob: soft halo when active, dark rim, light body
        if (hovered) UITheme.DrawGlow(sb, new Vector2(knobX, cy), 15, color * (dragging ? 0.55f : 0.35f));
        var knob = new Rectangle(knobX - 7, cy - 7, 14, 14);
        UITheme.FillRounded(sb, knob, dragging ? UITheme.Gold : hovered ? Color.White : UITheme.Text);
        UITheme.OutlineRounded(sb, knob, new Color(8, 11, 18));
        UITheme.FillRect(sb, new Rectangle(knobX - 1, cy - 3, 2, 6), new Color(8, 11, 18) * 0.55f);
    }

    /// <summary>On/off switch: a pill track with a sliding knob.</summary>
    public static void DrawSwitch(SpriteBatch sb, Rectangle rect, bool on, bool hovered = false)
    {
        Color track = on ? new Color(44, 150, 84) : new Color(58, 64, 80);
        if (hovered) track = Color.Lerp(track, Color.White, 0.14f);
        UITheme.FillRounded(sb, rect, track);
        UITheme.OutlineRounded(sb, rect, on ? new Color(120, 230, 150) * 0.7f : UITheme.Border);
        int knobSize = rect.Height - 4;
        int knobX = on ? rect.Right - knobSize - 2 : rect.X + 2;
        UITheme.FillRounded(sb, new Rectangle(knobX, rect.Y + 2, knobSize, knobSize), on ? Color.White : new Color(170, 178, 194));
    }

    /// <summary>Full-width row with a label on the left and a switch on the right.</summary>
    public static void DrawToggleRow(SpriteBatch sb, Rectangle rect, string label, bool on, bool emphasised = false)
    {
        bool hovered = Hovered(rect);
        bool pressed = hovered && Pressed(rect);
        if (hovered || emphasised)
            UITheme.FillRounded(sb, rect, pressed ? UITheme.ButtonActive * 0.55f : hovered ? UITheme.ButtonHover * 0.85f : UITheme.ButtonBg * 0.8f);
        if (emphasised) UITheme.OutlineRounded(sb, rect, hovered ? UITheme.BorderBright : UITheme.Border);
        const int switchWidth = 36, switchHeight = 18;
        var sw = new Rectangle(rect.Right - switchWidth - 8, rect.Y + (rect.Height - switchHeight) / 2, switchWidth, switchHeight);
        string fitted = UITheme.Ellipsize(label, rect.Width - switchWidth - 28, UITheme.FontNormal);
        var size = UITheme.Measure(fitted, UITheme.FontNormal);
        UITheme.DrawText(sb, fitted, new Vector2(rect.X + 10, rect.Y + (rect.Height - size.Y) / 2f), on ? UITheme.Text : UITheme.TextDim, UITheme.FontNormal);
        DrawSwitch(sb, sw, on, hovered);
    }

    /// <summary>
    /// Button with a colour key on its left edge. Shows hover and pressed states;
    /// <paramref name="selected"/> keeps it lit (the active mode or the chosen entry).
    /// </summary>
    public static void DrawActionButton(SpriteBatch sb, Rectangle rect, string text, Color accent,
        bool selected = false, bool enabled = true, bool centered = true, float fontSize = UITheme.FontNormal)
    {
        bool hovered = enabled && Hovered(rect);
        bool pressed = hovered && Pressed(rect);
        Color key = Readable(accent);
        UITheme.DrawButton(sb, rect, "", hovered, selected || pressed, key, fontSize, enabled);
        // Colour key
        UITheme.FillRect(sb, new Rectangle(rect.X + 3, rect.Y + 5, 3, rect.Height - 10), key * (!enabled ? 0.3f : hovered || selected ? 1f : 0.75f));

        string fitted = UITheme.Ellipsize(text, rect.Width - 24, fontSize);
        var size = UITheme.Measure(fitted, fontSize);
        float tx = centered ? rect.X + (rect.Width - size.X) / 2f : rect.X + 14;
        float ty = rect.Y + (rect.Height - size.Y) / 2f + (pressed ? 1 : 0);
        UITheme.DrawTextShadowed(sb, fitted, new Vector2(tx, ty), !enabled ? UITheme.TextMuted : selected ? Color.White : UITheme.Text, fontSize);
    }

    /// <summary>Plain themed button with hover and pressed states.</summary>
    public static void DrawButton(SpriteBatch sb, Rectangle rect, string text, bool selected = false, bool enabled = true,
        Color? accent = null, float fontSize = UITheme.FontNormal)
    {
        bool hovered = enabled && Hovered(rect);
        bool pressed = hovered && Pressed(rect);
        UITheme.DrawButton(sb, rect, UITheme.Ellipsize(text, rect.Width - 12, fontSize), hovered, selected || pressed, accent, fontSize, enabled);
    }

    /// <summary>
    /// Selectable list row: optional colour marker, title and a dim subtitle on a second line
    /// (when the row is tall enough) or right-aligned on the same line.
    /// </summary>
    public static void DrawListRow(SpriteBatch sb, Rectangle rect, string title, string? subtitle, bool selected,
        Color? marker = null, bool enabled = true)
    {
        bool hovered = enabled && Hovered(rect);
        bool pressed = hovered && Pressed(rect);
        Color bg = selected ? Color.Lerp(UITheme.ButtonBg, UITheme.Accent, pressed ? 0.42f : 0.3f)
                 : pressed ? UITheme.ButtonActive * 0.7f
                 : hovered ? UITheme.ButtonHover
                 : UITheme.ButtonBg * 0.75f;
        UITheme.FillRounded(sb, rect, bg);
        UITheme.OutlineRounded(sb, rect, selected ? UITheme.Accent : hovered ? UITheme.BorderBright * 0.8f : UITheme.Border * 0.8f);
        if (selected) UITheme.FillRect(sb, new Rectangle(rect.X + 1, rect.Y + 5, 3, rect.Height - 10), UITheme.Gold);

        int textX = rect.X + 10;
        if (marker.HasValue)
        {
            UITheme.FillRounded(sb, new Rectangle(rect.X + 8, rect.Y + (rect.Height - 12) / 2, 12, 12), marker.Value);
            UITheme.OutlineRounded(sb, new Rectangle(rect.X + 8, rect.Y + (rect.Height - 12) / 2, 12, 12), Color.White * 0.3f);
            textX += 20;
        }
        int textWidth = rect.Right - 10 - textX;
        Color titleColor = !enabled ? UITheme.TextMuted : selected ? Color.White : UITheme.Text;
        bool twoLines = !string.IsNullOrEmpty(subtitle) && rect.Height >= 34;
        if (twoLines)
        {
            UITheme.DrawText(sb, UITheme.Ellipsize(title, textWidth, UITheme.FontNormal), new Vector2(textX, rect.Y + 3), titleColor, UITheme.FontNormal);
            UITheme.DrawText(sb, UITheme.Ellipsize(subtitle!, textWidth, 11f), new Vector2(textX, rect.Y + 20), UITheme.TextDim, 11f);
            return;
        }

        float subWidth = 0;
        if (!string.IsNullOrEmpty(subtitle))
        {
            var ss = UITheme.Measure(subtitle, UITheme.FontSmall);
            if (ss.X < textWidth * 0.55f)
            {
                subWidth = ss.X + 10;
                UITheme.DrawText(sb, subtitle, new Vector2(rect.Right - 10 - ss.X, rect.Y + (rect.Height - ss.Y) / 2f), UITheme.TextDim, UITheme.FontSmall);
            }
        }
        string fitted = UITheme.Ellipsize(title, textWidth - subWidth, UITheme.FontNormal);
        var ts = UITheme.Measure(fitted, UITheme.FontNormal);
        UITheme.DrawText(sb, fitted, new Vector2(textX, rect.Y + (rect.Height - ts.Y) / 2f), titleColor, UITheme.FontNormal);
    }

    /// <summary>Thin vertical scroll bar. Draws nothing when everything fits.</summary>
    public static void DrawScrollBar(SpriteBatch sb, Rectangle track, int offset, int maxOffset, int viewSize, int contentSize)
    {
        if (maxOffset <= 0 || contentSize <= 0) return;
        UITheme.FillRect(sb, track, Color.White * 0.06f);
        int thumb = Math.Max(24, track.Height * viewSize / contentSize);
        int thumbY = track.Y + (track.Height - thumb) * Math.Clamp(offset, 0, maxOffset) / maxOffset;
        UITheme.FillRounded(sb, new Rectangle(track.X, thumbY, track.Width, thumb), UITheme.BorderBright);
    }

    // ---- Tool status bar and toasts -----------------------------------------------------

    /// <summary>
    /// One-line status bar for a map tool: coloured dot and tool name, label/value pairs
    /// separated by thin dividers, then a muted hint. Centred on <paramref name="centerX"/>
    /// and kept inside the screen. Returns the rectangle it occupies.
    /// </summary>
    public static Rectangle DrawToolBar(SpriteBatch sb, int centerX, int y, int screenWidth, string title, Color accent,
        (string Label, string Value, Color? Color)[] items, string? hint = null)
    {
        const int height = 36, pad = 14, gap = 12;
        float font = UITheme.FontNormal;
        float width = pad + 16 + UITheme.Measure(title, font).X;
        foreach (var item in items)
            width += gap * 2 + 1 + UITheme.Measure(item.Label + " ", UITheme.FontSmall).X + UITheme.Measure(item.Value, font).X;
        if (!string.IsNullOrEmpty(hint)) width += gap * 2 + 1 + UITheme.Measure(hint, UITheme.FontSmall).X;
        width += pad;

        int w = (int)MathF.Ceiling(width);
        int x = Math.Clamp(centerX - w / 2, 4, Math.Max(4, screenWidth - w - 4));
        var rect = new Rectangle(x, y, w, height);
        UITheme.DrawPanel(sb, rect, new Color(12, 17, 28, 246), Color.Lerp(UITheme.Border, accent, 0.45f));
        UITheme.FillRect(sb, new Rectangle(rect.X + 1, rect.Y + 6, 3, height - 12), accent);

        float cx = x + pad;
        float textY = y + (height - UITheme.Measure("Ag", font).Y) / 2f;
        float smallY = y + (height - UITheme.Measure("Ag", UITheme.FontSmall).Y) / 2f + 1;
        UITheme.FillRounded(sb, new Rectangle((int)cx, y + height / 2 - 4, 8, 8), accent);
        cx += 16;
        UITheme.DrawTextShadowed(sb, title, new Vector2(cx, textY), UITheme.Gold, font);
        cx += UITheme.Measure(title, font).X;

        foreach (var item in items)
        {
            cx += gap;
            UITheme.FillRect(sb, new Rectangle((int)cx, y + 9, 1, height - 18), UITheme.Border);
            cx += gap + 1;
            UITheme.DrawText(sb, item.Label, new Vector2(cx, smallY), UITheme.TextDim, UITheme.FontSmall);
            cx += UITheme.Measure(item.Label + " ", UITheme.FontSmall).X;
            UITheme.DrawText(sb, item.Value, new Vector2(cx, textY), item.Color ?? UITheme.Text, font);
            cx += UITheme.Measure(item.Value, font).X;
        }

        if (!string.IsNullOrEmpty(hint))
        {
            cx += gap;
            UITheme.FillRect(sb, new Rectangle((int)cx, y + 9, 1, height - 18), UITheme.Border);
            cx += gap + 1;
            UITheme.DrawText(sb, hint, new Vector2(cx, smallY), UITheme.TextMuted, UITheme.FontSmall);
        }
        return rect;
    }

    /// <summary>Short message in a rounded box, centred horizontally at <paramref name="y"/>.</summary>
    public static void DrawToast(SpriteBatch sb, string message, int screenWidth, int y, Color? accent = null)
    {
        string fitted = UITheme.Ellipsize(message, screenWidth - 80, UITheme.FontNormal);
        var size = UITheme.Measure(fitted, UITheme.FontNormal);
        var rect = new Rectangle((int)((screenWidth - size.X) / 2f) - 16, y, (int)size.X + 32, 34);
        UITheme.DrawPanel(sb, rect, new Color(12, 17, 28, 246), accent ?? UITheme.Gold);
        UITheme.DrawTextCentered(sb, fitted, rect, UITheme.Text, UITheme.FontNormal);
    }

    /// <summary>
    /// Brightens colours that are too dark to read as an accent on the navy panels
    /// (dark red, purple, brown, grey...), leaving bright ones untouched.
    /// </summary>
    public static Color Readable(Color color)
    {
        float luminance = (0.299f * color.R + 0.587f * color.G + 0.114f * color.B) / 255f;
        if (luminance >= 0.45f) return color;
        return Color.Lerp(color, Color.White, Math.Clamp((0.45f - luminance) * 1.3f, 0f, 0.6f));
    }
}
