using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace SimPlanet;

/// <summary>
/// History of the world: a scrollable chronicle panel (key O) and short-lived
/// notification toasts for new events (wars, battles, foundings, famines...).
/// Reads the thread-safe <see cref="CivRenderData.Latest"/> snapshot.
/// </summary>
public class ChronicleUI
{
    private readonly GraphicsDevice _graphicsDevice;

    private sealed class Toast
    {
        public CivRenderData.HistoryInfo Event;
        public float Age;
    }

    private readonly List<Toast> _toasts = new();
    private readonly HashSet<string> _seen = new();
    private bool _primed;
    private int _scrollOffset;
    private int _contentHeight;
    private string _filter = "All";
    private readonly List<(Rectangle Rect, string Filter)> _filterChips = new();
    private Rectangle _panelRect;
    private Rectangle _closeRect;
    private MouseState _previousMouse;

    private const float ToastLifetime = 7f;
    private const int MaxToasts = 4;
    private const int InfoPanelWidth = 280;

    private static readonly string[] Filters = { "All", "War", "Diplomacy", "Growth", "Hardship" };

    public bool IsVisible { get; set; }
    public bool ShowToasts { get; set; } = true;

    /// <summary>True when the mouse is over the open chronicle panel.</summary>
    public bool IsMouseOver { get; private set; }

    public ChronicleUI(GraphicsDevice graphicsDevice)
    {
        _graphicsDevice = graphicsDevice;
    }

    public static Color GetCategoryColor(string category) => category switch
    {
        "Founding" => new Color(120, 200, 255),
        "War" => new Color(255, 90, 80),
        "Battle" => new Color(255, 140, 60),
        "Conquest" => new Color(225, 95, 210),
        "Peace" => new Color(120, 220, 140),
        "Famine" => new Color(215, 175, 90),
        "Rebellion" => new Color(255, 120, 150),
        "Diplomacy" => new Color(150, 200, 255),
        "Disaster" => new Color(255, 205, 70),
        "Growth" => new Color(150, 230, 120),
        _ => new Color(190, 200, 215)
    };

    private static bool MatchesFilter(string category, string filter) => filter switch
    {
        "War" => category is "War" or "Battle" or "Conquest" or "Rebellion",
        "Diplomacy" => category is "Diplomacy" or "Peace",
        "Growth" => category is "Founding" or "Growth",
        "Hardship" => category is "Famine" or "Disaster" or "Rebellion",
        _ => true
    };

    private static string Key(CivRenderData.HistoryInfo e) => $"{e.Year}|{e.Category}|{e.Text}";

    public void Update(float deltaTime, MouseState mouse)
    {
        var data = CivRenderData.Latest;

        // Detect new events for toasts (the first snapshot only primes the "seen" set)
        foreach (var ev in data.Chronicle)
        {
            if (_seen.Add(Key(ev)) && _primed && ShowToasts && !IsVisible)
            {
                _toasts.Add(new Toast { Event = ev });
            }
        }
        if (data.Chronicle.Count > 0) _primed = true;
        if (_seen.Count > 5000) _seen.Clear(); // keep memory bounded on very long games

        for (int i = _toasts.Count - 1; i >= 0; i--)
        {
            _toasts[i].Age += deltaTime;
            if (_toasts[i].Age > ToastLifetime) _toasts.RemoveAt(i);
        }
        while (_toasts.Count > MaxToasts) _toasts.RemoveAt(0);

        IsMouseOver = IsVisible && _panelRect.Contains(mouse.Position);

        if (IsVisible)
        {
            if (IsMouseOver)
            {
                int wheel = mouse.ScrollWheelValue - _previousMouse.ScrollWheelValue;
                if (wheel != 0) _scrollOffset -= wheel / 3;
            }
            _scrollOffset = Math.Clamp(_scrollOffset, 0, Math.Max(0, _contentHeight - (_panelRect.Height - 90)));

            bool clicked = mouse.LeftButton == ButtonState.Released && _previousMouse.LeftButton == ButtonState.Pressed;
            if (clicked)
            {
                if (_closeRect.Contains(mouse.Position)) IsVisible = false;
                foreach (var (rect, filter) in _filterChips)
                {
                    if (rect.Contains(mouse.Position))
                    {
                        _filter = filter;
                        _scrollOffset = 0;
                    }
                }
            }
        }

        _previousMouse = mouse;
    }

    public void Toggle()
    {
        IsVisible = !IsVisible;
        if (IsVisible) _toasts.Clear();
    }

    public void Draw(SpriteBatch spriteBatch, int screenWidth, int screenHeight, int toolbarHeight)
    {
        if (!UITheme.IsInitialized) return;

        spriteBatch.End();
        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp);

        if (!IsVisible) DrawToasts(spriteBatch, screenWidth, toolbarHeight);
        if (IsVisible) DrawPanel(spriteBatch, screenWidth, screenHeight, toolbarHeight);

        spriteBatch.End();
        spriteBatch.Begin(samplerState: SamplerState.PointClamp);
    }

    private void DrawToasts(SpriteBatch spriteBatch, int screenWidth, int toolbarHeight)
    {
        if (_toasts.Count == 0) return;
        int width = 470;
        int mapCenter = InfoPanelWidth + (screenWidth - InfoPanelWidth) / 2;
        int x = mapCenter - width / 2;
        int y = toolbarHeight + 12;

        foreach (var toast in _toasts)
        {
            float appear = Math.Clamp(toast.Age / 0.25f, 0f, 1f);
            float fade = Math.Clamp((ToastLifetime - toast.Age) / 0.6f, 0f, 1f);
            float alpha = Math.Min(appear, fade);
            int slide = (int)((1f - appear) * -12);
            var ev = toast.Event;
            Color cat = GetCategoryColor(ev.Category);

            var lines = UITheme.WrapText(ev.Text, width - 110, UITheme.FontNormal);
            if (lines.Count > 2) lines = lines.GetRange(0, 2);
            int h = 16 + lines.Count * 18;
            var rect = new Rectangle(x, y + slide, width, h);

            UITheme.FillRounded(spriteBatch, new Rectangle(rect.X + 2, rect.Y + 4, rect.Width, rect.Height), Color.Black * (0.35f * alpha));
            UITheme.FillRounded(spriteBatch, rect, new Color(14, 20, 32) * (0.94f * alpha));
            UITheme.OutlineRounded(spriteBatch, rect, cat * (0.7f * alpha));
            UITheme.FillRounded(spriteBatch, new Rectangle(rect.X, rect.Y, 5, rect.Height), cat * alpha);

            // Category tag
            string tag = ev.Category.ToUpperInvariant();
            UITheme.DrawText(spriteBatch, tag, new Vector2(rect.X + 14, rect.Y + 8), cat * alpha, 11f);
            UITheme.DrawText(spriteBatch, $"Year {ev.Year}", new Vector2(rect.X + 14, rect.Y + 22), UITheme.TextMuted * alpha, 11f);

            float ty = rect.Y + 8;
            foreach (var line in lines)
            {
                UITheme.DrawTextShadowed(spriteBatch, line, new Vector2(rect.X + 100, ty), UITheme.Text * alpha, UITheme.FontNormal);
                ty += 18;
            }

            y += h + 6;
        }
    }

    private void DrawPanel(SpriteBatch spriteBatch, int screenWidth, int screenHeight, int toolbarHeight)
    {
        int width = Math.Min(430, screenWidth - InfoPanelWidth - 40);
        int x = screenWidth - width - 12;
        int y = toolbarHeight + 10;
        int height = screenHeight - y - 72;
        _panelRect = new Rectangle(x, y, width, height);

        int contentTop = UITheme.DrawTitledPanel(spriteBatch, _panelRect, "WORLD CHRONICLE", UITheme.Gold);

        // Close button
        _closeRect = new Rectangle(_panelRect.Right - 28, _panelRect.Y + 7, 20, 20);
        bool hoverClose = _closeRect.Contains(Mouse.GetState().Position);
        UITheme.FillRounded(spriteBatch, _closeRect, hoverClose ? new Color(190, 60, 60) : new Color(60, 70, 92));
        var c = _closeRect.Center.ToVector2();
        UITheme.DrawLine(spriteBatch, c + new Vector2(-4, -4), c + new Vector2(4, 4), Color.White, 1.6f);
        UITheme.DrawLine(spriteBatch, c + new Vector2(-4, 4), c + new Vector2(4, -4), Color.White, 1.6f);

        // Filter chips
        _filterChips.Clear();
        int fx = x + 12;
        var mouse = Mouse.GetState();
        foreach (var f in Filters)
        {
            int w = (int)UITheme.Measure(f, UITheme.FontSmall).X + 18;
            var chip = new Rectangle(fx, contentTop - 2, w, 22);
            _filterChips.Add((chip, f));
            UITheme.DrawButton(spriteBatch, chip, f, chip.Contains(mouse.Position), _filter == f, UITheme.Gold, UITheme.FontSmall);
            fx += w + 6;
        }

        var data = CivRenderData.Latest;
        int listTop = contentTop + 30;
        int listBottom = _panelRect.Bottom - 10;
        var events = new List<CivRenderData.HistoryInfo>();
        for (int i = data.Chronicle.Count - 1; i >= 0; i--)
            if (MatchesFilter(data.Chronicle[i].Category, _filter)) events.Add(data.Chronicle[i]);

        if (events.Count == 0)
        {
            string msg = data.Civs.Count == 0
                ? "No civilizations yet.\nHistory begins when intelligent life\nfounds its first settlement."
                : "No recorded events yet.\nWars, foundings and famines will\nappear here as history unfolds.";
            float my = listTop + 20;
            foreach (var line in msg.Split('\n'))
            {
                var sz = UITheme.Measure(line, UITheme.FontNormal);
                UITheme.DrawText(spriteBatch, line, new Vector2(x + (width - sz.X) / 2f, my), UITheme.TextMuted, UITheme.FontNormal);
                my += 20;
            }
            _contentHeight = 0;
            return;
        }

        // Clip the list
        spriteBatch.End();
        var previousScissor = _graphicsDevice.ScissorRectangle;
        _graphicsDevice.ScissorRectangle = new Rectangle(x + 1, listTop, width - 2, listBottom - listTop);
        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, null, ScissorRasterizer);

        var civColors = new Dictionary<int, Color>();
        foreach (var civ in data.Civs) civColors[civ.Id] = TerrainRenderer.GetCivPaletteColor(civ.Id);

        int ty = listTop - _scrollOffset;
        int textX = x + 78;
        int textWidth = width - 78 - 16;
        int lastYear = int.MinValue;
        foreach (var ev in events)
        {
            var lines = UITheme.WrapText(ev.Text, textWidth, 13f);
            int h = Math.Max(1, lines.Count) * 17 + 8;
            if (ty + h >= listTop && ty <= listBottom)
            {
                Color cat = GetCategoryColor(ev.Category);
                if (ev.Year != lastYear)
                {
                    UITheme.DrawText(spriteBatch, ev.Year.ToString(), new Vector2(x + 14, ty + 1), UITheme.Gold, 13f);
                }
                // Timeline rail with a category dot
                spriteBatch.Draw(UITheme.Pixel, new Rectangle(x + 64, ty, 1, h), UITheme.Border);
                UITheme.DrawGlow(spriteBatch, new Vector2(x + 64.5f, ty + 9), 6, cat * 0.6f);
                UITheme.FillRounded(spriteBatch, new Rectangle(x + 61, ty + 6, 7, 7), cat);

                float ly = ty;
                for (int i = 0; i < lines.Count; i++)
                {
                    Color col = i == 0 ? UITheme.Text : UITheme.TextDim;
                    UITheme.DrawText(spriteBatch, lines[i], new Vector2(textX, ly), col, 13f);
                    ly += 17;
                }
                if (civColors.TryGetValue(ev.CivId, out var civColor))
                {
                    spriteBatch.Draw(UITheme.Pixel, new Rectangle(textX - 8, ty + 3, 3, h - 10), civColor);
                }
            }
            lastYear = ev.Year;
            ty += h;
        }
        _contentHeight = ty + _scrollOffset - listTop;

        spriteBatch.End();
        _graphicsDevice.ScissorRectangle = previousScissor;
        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp);

        // Scroll hint
        if (_contentHeight > listBottom - listTop)
        {
            float ratio = (float)(listBottom - listTop) / _contentHeight;
            int trackH = listBottom - listTop;
            int thumbH = Math.Max(24, (int)(trackH * ratio));
            float pos = _contentHeight - trackH > 0 ? (float)_scrollOffset / (_contentHeight - trackH) : 0f;
            int thumbY = listTop + (int)((trackH - thumbH) * Math.Clamp(pos, 0f, 1f));
            spriteBatch.Draw(UITheme.Pixel, new Rectangle(_panelRect.Right - 7, listTop, 3, trackH), new Color(0, 0, 0, 90));
            spriteBatch.Draw(UITheme.Pixel, new Rectangle(_panelRect.Right - 7, thumbY, 3, thumbH), UITheme.BorderBright);
        }
    }

    private static readonly RasterizerState ScissorRasterizer = new RasterizerState { ScissorTestEnable = true };
}
