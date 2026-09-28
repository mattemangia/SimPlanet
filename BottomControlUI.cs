using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;

namespace SimPlanet;

/// <summary>
/// Bottom control bar for time and system controls
/// </summary>
public class BottomControlUI
{
    private enum Glyph { Slower, Pause, Play, Faster, FastForward, Save, Load, Map, Help, Regenerate }

    private class ControlButton
    {
        public Rectangle Bounds { get; set; }
        public required string Tooltip { get; set; }
        public required Glyph Glyph { get; set; }
        public required Action OnClick { get; set; }
        public bool IsHovered { get; set; }
        public Color Accent { get; set; }
        public bool GapBefore { get; set; }
    }

    private readonly SimPlanetGame _game;
    private readonly GraphicsDevice _graphicsDevice;
    private readonly FontRenderer _font;
    private Texture2D _pixelTexture;
    private readonly Dictionary<Glyph, Texture2D> _glyphs = new();

    private List<ControlButton> _buttons = new();
    private MouseState _previousMouseState;
    private Rectangle _panelRect;

    // Dimensions
    private const int PanelHeight = 46;
    private const int ButtonSize = 34;
    private const int Spacing = 5;
    private const int GroupGap = 14;
    private const int SpeedReadoutWidth = 92;
    private const int InfoPanelWidth = 280;

    public BottomControlUI(SimPlanetGame game, GraphicsDevice graphicsDevice, FontRenderer font)
    {
        _game = game;
        _graphicsDevice = graphicsDevice;
        _font = font;

        _pixelTexture = new Texture2D(_graphicsDevice, 1, 1);
        _pixelTexture.SetData(new[] { Color.White });

        BuildGlyphs();
        InitializeButtons();
    }

    /// <summary>True when the mouse is over the bar (so the map should not react).</summary>
    public bool IsMouseOver { get; private set; }

    private void InitializeButtons()
    {
        AddButton(Glyph.Slower, "Slower  (-)", () => _game.DecreaseTimeSpeed(), UITheme.Accent);
        AddButton(Glyph.Pause, "Pause / Resume  (Space)", () => _game.TogglePause(), UITheme.Gold);
        AddButton(Glyph.Faster, "Faster  (+)", () => _game.IncreaseTimeSpeed(), UITheme.Accent);
        AddButton(Glyph.FastForward, "Fast forward 10,000 years  (F)", () => _game.ToggleFastForward(), UITheme.Warn);

        AddButton(Glyph.Save, "Quick save  (F5)", () => _game.QuickSave(), UITheme.Good, gapBefore: true);
        AddButton(Glyph.Load, "Quick load  (F9)", () => _game.QuickLoad(), new Color(80, 200, 200));
        AddButton(Glyph.Map, "Map options / world generator  (M)", () => _game.ToggleMapOptions(), new Color(180, 130, 255));
        AddButton(Glyph.Help, "Help and controls  (H)", () => _game.ToggleHelp(), new Color(120, 200, 255));
        AddButton(Glyph.Regenerate, "Regenerate planet  (R)\nCreates a brand new world!", () => _game.RegeneratePlanet(), UITheme.Bad, gapBefore: true);
    }

    private void AddButton(Glyph glyph, string tooltip, Action onClick, Color accent, bool gapBefore = false)
    {
        _buttons.Add(new ControlButton
        {
            Glyph = glyph,
            Tooltip = tooltip,
            OnClick = onClick,
            Accent = accent,
            GapBefore = gapBefore
        });
    }

    private void Layout()
    {
        int screenWidth = _graphicsDevice.Viewport.Width;
        int screenHeight = _graphicsDevice.Viewport.Height;

        int contentWidth = SpeedReadoutWidth + Spacing;
        foreach (var b in _buttons) contentWidth += ButtonSize + Spacing + (b.GapBefore ? GroupGap : 0);
        int totalWidth = contentWidth + Spacing;

        // Centre in the map area (right of the info panel)
        int mapAreaX = InfoPanelWidth;
        int panelX = mapAreaX + (screenWidth - mapAreaX - totalWidth) / 2;
        panelX = Math.Max(mapAreaX + 10, panelX);
        int panelY = screenHeight - PanelHeight - 10;
        _panelRect = new Rectangle(panelX, panelY, totalWidth, PanelHeight);

        int x = panelX + Spacing + SpeedReadoutWidth + Spacing;
        int y = panelY + (PanelHeight - ButtonSize) / 2;
        foreach (var button in _buttons)
        {
            if (button.GapBefore) x += GroupGap;
            button.Bounds = new Rectangle(x, y, ButtonSize, ButtonSize);
            x += ButtonSize + Spacing;
        }
    }

    public void Update(MouseState mouseState)
    {
        Layout();
        IsMouseOver = _panelRect.Contains(mouseState.Position);

        foreach (var button in _buttons)
        {
            button.IsHovered = button.Bounds.Contains(mouseState.Position);
        }

        if (mouseState.LeftButton == ButtonState.Pressed &&
            _previousMouseState.LeftButton == ButtonState.Released)
        {
            foreach (var button in _buttons)
            {
                if (button.IsHovered)
                {
                    button.OnClick?.Invoke();
                    break;
                }
            }
        }

        _previousMouseState = mouseState;
    }

    public void Draw(SpriteBatch spriteBatch)
    {
        Layout();
        var state = _game.CurrentGameState;
        bool paused = state?.IsPaused ?? false;

        spriteBatch.End();
        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp);

        UITheme.DrawPanel(spriteBatch, _panelRect, new Color(14, 20, 32, 236), UITheme.Border);

        // Speed / pause readout
        var readout = new Rectangle(_panelRect.X + Spacing, _panelRect.Y + (PanelHeight - ButtonSize) / 2, SpeedReadoutWidth, ButtonSize);
        UITheme.FillRounded(spriteBatch, readout, new Color(8, 12, 20, 230));
        UITheme.OutlineRounded(spriteBatch, readout, paused ? UITheme.Gold * 0.8f : UITheme.AccentDim);
        string speedText = paused ? "PAUSED" : FormatSpeed(state?.TimeSpeed ?? 1f);
        Color speedColor = paused ? UITheme.Gold : UITheme.Good;
        var st = UITheme.Measure(speedText, UITheme.FontMedium);
        UITheme.DrawTextShadowed(spriteBatch, speedText,
            new Vector2(readout.X + (readout.Width - st.X) / 2f, readout.Y + 2), speedColor, UITheme.FontMedium);
        string sub = paused ? "space to resume" : "sim speed";
        var ss = UITheme.Measure(sub, 10f);
        UITheme.DrawText(spriteBatch, sub, new Vector2(readout.X + (readout.Width - ss.X) / 2f, readout.Bottom - ss.Y - 2), UITheme.TextMuted, 10f);

        foreach (var button in _buttons)
        {
            bool active = button.Glyph == Glyph.Pause && paused;
            UITheme.DrawButton(spriteBatch, button.Bounds, "", button.IsHovered, active, button.Accent);

            // Coloured accent line at the bottom
            spriteBatch.Draw(_pixelTexture, new Rectangle(button.Bounds.X + 6, button.Bounds.Bottom - 3, button.Bounds.Width - 12, 2),
                button.Accent * (button.IsHovered ? 1f : 0.7f));

            Glyph glyph = button.Glyph == Glyph.Pause && paused ? Glyph.Play : button.Glyph;
            if (_glyphs.TryGetValue(glyph, out var tex))
            {
                int gs = 22;
                var r = new Rectangle(button.Bounds.X + (ButtonSize - gs) / 2, button.Bounds.Y + (ButtonSize - gs) / 2 - 1, gs, gs);
                spriteBatch.Draw(tex, r, button.IsHovered ? Color.White : new Color(225, 232, 245));
            }
        }

        // Separators between groups
        foreach (var button in _buttons)
        {
            if (!button.GapBefore) continue;
            int sx = button.Bounds.X - GroupGap / 2 - Spacing / 2;
            spriteBatch.Draw(_pixelTexture, new Rectangle(sx, _panelRect.Y + 10, 1, PanelHeight - 20), UITheme.Border);
        }

        var hoveredButton = _buttons.Find(b => b.IsHovered);
        if (hoveredButton != null)
        {
            UITheme.DrawTooltip(spriteBatch, hoveredButton.Tooltip, new Point(hoveredButton.Bounds.Center.X, hoveredButton.Bounds.Top - 4),
                _graphicsDevice.Viewport.Width, _graphicsDevice.Viewport.Height, preferAbove: true);
        }

        spriteBatch.End();
        spriteBatch.Begin(samplerState: SamplerState.PointClamp);
    }

    private static string FormatSpeed(float speed)
    {
        if (speed < 1f) return $"{speed:0.##}x";
        return $"{speed:0}x";
    }

    // ------------------------------------------------------------------
    // Vector glyphs rendered once into small anti-aliased textures
    // ------------------------------------------------------------------
    private void BuildGlyphs()
    {
        const int s = 22;
        var white = new Color(255, 255, 255);
        Vector2 V(float x, float y) => new Vector2(x, y);

        MapIcons.Canvas C() => new MapIcons.Canvas(s);

        var c = C();
        c.Poly(white, V(11, 5), V(3, 11), V(11, 17));
        c.Poly(white, V(19, 5), V(11, 11), V(19, 17));
        _glyphs[Glyph.Slower] = c.ToTexture(_graphicsDevice);

        c = C();
        c.Rect(6, 5, 3.6f, 12, white);
        c.Rect(12.4f, 5, 3.6f, 12, white);
        _glyphs[Glyph.Pause] = c.ToTexture(_graphicsDevice);

        c = C();
        c.Poly(white, V(7, 4), V(18, 11), V(7, 18));
        _glyphs[Glyph.Play] = c.ToTexture(_graphicsDevice);

        c = C();
        c.Poly(white, V(3, 5), V(11, 11), V(3, 17));
        c.Poly(white, V(11, 5), V(19, 11), V(11, 17));
        _glyphs[Glyph.Faster] = c.ToTexture(_graphicsDevice);

        c = C();
        c.Poly(white, V(1, 6), V(7.5f, 11), V(1, 16));
        c.Poly(white, V(7.5f, 6), V(14, 11), V(7.5f, 16));
        c.Poly(white, V(14, 6), V(20.5f, 11), V(14, 16));
        c.Rect(19.5f, 6, 1.8f, 10, white);
        _glyphs[Glyph.FastForward] = c.ToTexture(_graphicsDevice);

        c = C();
        c.Poly(white, V(3, 3), V(16, 3), V(19, 6), V(19, 19), V(3, 19));
        c.Rect(6, 3, 9, 5, new Color(40, 50, 70));
        c.Rect(12, 4, 2, 3, white);
        c.Rect(6, 11, 10, 6, new Color(40, 50, 70));
        _glyphs[Glyph.Save] = c.ToTexture(_graphicsDevice);

        c = C();
        c.Poly(white, V(2, 5), V(8, 5), V(10, 7), V(19, 7), V(19, 18), V(2, 18));
        c.Poly(new Color(170, 185, 210), V(4, 10), V(21, 10), V(19, 18), V(2, 18));
        _glyphs[Glyph.Load] = c.ToTexture(_graphicsDevice);

        c = C();
        c.Circle(11, 11, 8.5f, white);
        c.Circle(11, 11, 7f, new Color(60, 110, 170));
        c.Poly(new Color(120, 200, 120), V(6, 7), V(10, 5), V(12, 9), V(9, 13), V(6, 11));
        c.Poly(new Color(120, 200, 120), V(13, 12), V(17, 11), V(16, 16), V(13, 16));
        _glyphs[Glyph.Map] = c.ToTexture(_graphicsDevice);

        c = C();
        c.Circle(11, 11, 9f, white);
        c.Circle(11, 11, 7.4f, new Color(40, 50, 70));
        c.Ring(11, 9, 3.6f, 2f, white);
        c.Rect(7, 8.6f, 2, 1.6f, new Color(40, 50, 70));
        c.Rect(10.1f, 11.5f, 1.8f, 2.4f, white);
        c.Circle(11, 15.8f, 1.1f, white);
        _glyphs[Glyph.Help] = c.ToTexture(_graphicsDevice);

        c = C();
        c.Ring(11, 11, 8.2f, 5.8f, white);
        c.ErasePoly(V(11, 11), V(22, 0), V(22, 11));
        c.Poly(white, V(14, 1.5f), V(20.5f, 6), V(13.5f, 9));
        _glyphs[Glyph.Regenerate] = c.ToTexture(_graphicsDevice);
    }
}
