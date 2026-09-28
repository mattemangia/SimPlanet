using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace SimPlanet;

/// <summary>
/// Nation detail panel: overview (people, government, strategy, energy, networks, space,
/// national projects), succession (line of succession and dynasty tree), politics
/// (parliament, party support, elections) and military (arsenal, armies, wars).
/// Opened from the nation cards of the info panel or the Society menu.
/// Reads the thread-safe <see cref="CivRenderData.Latest"/> snapshot.
/// </summary>
public class NationPanelUI
{
    private readonly GraphicsDevice _graphicsDevice;
    private static readonly RasterizerState ScissorRasterizer = new RasterizerState { ScissorTestEnable = true };
    private static readonly string[] Tabs = { "Overview", "Succession", "Politics", "Military" };

    private int _civId = -1;
    private string _tab = "Overview";
    private int _scroll;
    private int _contentHeight;
    private Rectangle _panelRect, _closeRect, _prevRect, _nextRect, _contentRect;
    private readonly List<(Rectangle Rect, string Tab)> _tabRects = new();
    private MouseState _previousMouse;
    private Texture2D? _disc;
    private string? _hoverTip;

    private const int InfoPanelWidth = 280;

    public bool IsVisible { get; private set; }

    /// <summary>True when the mouse is over the open panel (the map should ignore it).</summary>
    public bool IsMouseOver { get; private set; }

    public NationPanelUI(GraphicsDevice graphicsDevice)
    {
        _graphicsDevice = graphicsDevice;
    }

    public void Open(int civId, string? tab = null)
    {
        _civId = civId;
        if (tab != null) _tab = tab;
        _scroll = 0;
        IsVisible = true;
    }

    /// <summary>Opens the panel on the most populous nation (or toggles it closed).</summary>
    public void Toggle()
    {
        if (IsVisible) { IsVisible = false; return; }
        var civs = CivRenderData.Latest.Civs;
        if (civs.Count == 0) { IsVisible = true; _civId = -1; return; }
        var exists = civs.Any(c => c.Id == _civId);
        Open(exists ? _civId : civs.OrderByDescending(c => c.Population).First().Id);
    }

    public void Close() => IsVisible = false;

    public void Update(MouseState mouse)
    {
        IsMouseOver = IsVisible && _panelRect.Contains(mouse.Position);
        if (!IsVisible)
        {
            _previousMouse = mouse;
            return;
        }

        if (IsMouseOver)
        {
            int wheel = mouse.ScrollWheelValue - _previousMouse.ScrollWheelValue;
            if (wheel != 0) _scroll -= wheel / 3;
        }
        _scroll = Math.Clamp(_scroll, 0, Math.Max(0, _contentHeight - _contentRect.Height + 10));

        bool clicked = mouse.LeftButton == ButtonState.Released && _previousMouse.LeftButton == ButtonState.Pressed;
        if (clicked)
        {
            if (_closeRect.Contains(mouse.Position)) IsVisible = false;
            else if (_prevRect.Contains(mouse.Position)) Cycle(-1);
            else if (_nextRect.Contains(mouse.Position)) Cycle(1);
            foreach (var (rect, tab) in _tabRects)
            {
                if (rect.Contains(mouse.Position))
                {
                    _tab = tab;
                    _scroll = 0;
                }
            }
        }

        _previousMouse = mouse;
    }

    private void Cycle(int dir)
    {
        var civs = CivRenderData.Latest.Civs.OrderByDescending(c => c.Population).ToList();
        if (civs.Count == 0) return;
        int i = civs.FindIndex(c => c.Id == _civId);
        i = i < 0 ? 0 : (i + dir + civs.Count) % civs.Count;
        _civId = civs[i].Id;
        _scroll = 0;
    }

    // ------------------------------------------------------------------
    // Drawing
    // ------------------------------------------------------------------

    public void Draw(SpriteBatch sb, int screenWidth, int screenHeight, int toolbarHeight, int currentYear)
    {
        if (!IsVisible || !UITheme.IsInitialized) return;
        _disc ??= BuildDisc(_graphicsDevice, 32);
        _hoverTip = null;

        int areaX = InfoPanelWidth + 16;
        int areaW = screenWidth - areaX - 16;
        int width = Math.Min(860, areaW);
        int height = Math.Min(640, screenHeight - toolbarHeight - 86);
        int x = areaX + (areaW - width) / 2;
        int y = toolbarHeight + 12;
        _panelRect = new Rectangle(x, y, width, height);

        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp);

        var data = CivRenderData.Latest;
        var found = data.FindCiv(_civId);
        UITheme.DrawPanel(sb, _panelRect, new Color(14, 19, 30, 248), UITheme.BorderBright);

        var mouse = Mouse.GetState();
        DrawHeader(sb, data, found, mouse);

        if (found == null)
        {
            string msg = data.Civs.Count == 0
                ? "No nations yet.\nPlant intelligent life (Tools > Plant) and let history begin."
                : "This nation no longer exists.\nUse the arrows to browse the living nations.";
            float my = _panelRect.Y + 130;
            foreach (var line in msg.Split('\n'))
            {
                var sz = UITheme.Measure(line, UITheme.FontNormal);
                UITheme.DrawText(sb, line, new Vector2(_panelRect.X + (width - sz.X) / 2f, my), UITheme.TextMuted, UITheme.FontNormal);
                my += 22;
            }
            _tabRects.Clear();
            _contentHeight = 0;
            sb.End();
            sb.Begin(samplerState: SamplerState.PointClamp);
            return;
        }

        var civ = found.Value;

        // Tabs
        _tabRects.Clear();
        int tx = _panelRect.X + 14;
        int tabY = _panelRect.Y + 70;
        foreach (var tab in Tabs)
        {
            int tw = (int)UITheme.Measure(tab, UITheme.FontNormal).X + 28;
            var r = new Rectangle(tx, tabY, tw, 28);
            _tabRects.Add((r, tab));
            UITheme.DrawButton(sb, r, tab, r.Contains(mouse.Position), _tab == tab, UITheme.Gold);
            tx += tw + 6;
        }
        sb.Draw(UITheme.Pixel, new Rectangle(_panelRect.X + 1, tabY + 34, _panelRect.Width - 2, 1), UITheme.Border);

        _contentRect = new Rectangle(_panelRect.X + 1, tabY + 36, _panelRect.Width - 2, _panelRect.Bottom - tabY - 38);

        // Content (clipped and scrollable)
        sb.End();
        var previousScissor = _graphicsDevice.ScissorRectangle;
        _graphicsDevice.ScissorRectangle = _contentRect;
        sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, null, ScissorRasterizer);

        int top = _contentRect.Y + 10 - _scroll;
        int bottom = _tab switch
        {
            "Succession" => DrawSuccession(sb, data, civ, top, currentYear, mouse),
            "Politics" => DrawPolitics(sb, data, civ, top, currentYear),
            "Military" => DrawMilitary(sb, data, civ, top),
            _ => DrawOverview(sb, data, civ, top, currentYear)
        };
        _contentHeight = bottom - top + 10;

        sb.End();
        _graphicsDevice.ScissorRectangle = previousScissor;
        sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp);

        // Scrollbar
        if (_contentHeight > _contentRect.Height)
        {
            var track = new Rectangle(_contentRect.Right - 7, _contentRect.Y + 4, 4, _contentRect.Height - 8);
            sb.Draw(UITheme.Pixel, track, new Color(0, 0, 0, 110));
            float ratio = _contentRect.Height / (float)_contentHeight;
            int th = Math.Max(24, (int)(track.Height * ratio));
            float pos = _scroll / (float)Math.Max(1, _contentHeight - _contentRect.Height + 10);
            sb.Draw(UITheme.Pixel, new Rectangle(track.X, track.Y + (int)((track.Height - th) * Math.Clamp(pos, 0f, 1f)), 4, th), UITheme.BorderBright);
        }

        if (_hoverTip != null)
            UITheme.DrawTooltip(sb, _hoverTip, new Point(mouse.X, mouse.Y + 12), screenWidth, screenHeight);

        sb.End();
        sb.Begin(samplerState: SamplerState.PointClamp);
    }

    private void DrawHeader(SpriteBatch sb, CivRenderData data, CivRenderData.CivInfo? found, MouseState mouse)
    {
        var r = _panelRect;
        var header = new Rectangle(r.X + 1, r.Y + 1, r.Width - 2, 62);
        UITheme.FillRounded(sb, header, UITheme.HeaderBg);
        sb.Draw(UITheme.Pixel, new Rectangle(r.X + 1, header.Bottom - 6, r.Width - 2, 6), UITheme.HeaderBg);

        // Close
        _closeRect = new Rectangle(r.Right - 30, r.Y + 8, 22, 22);
        bool hoverClose = _closeRect.Contains(mouse.Position);
        UITheme.FillRounded(sb, _closeRect, hoverClose ? new Color(190, 60, 60) : new Color(60, 70, 92));
        var c = _closeRect.Center.ToVector2();
        UITheme.DrawLine(sb, c + new Vector2(-4, -4), c + new Vector2(4, 4), Color.White, 1.6f);
        UITheme.DrawLine(sb, c + new Vector2(-4, 4), c + new Vector2(4, -4), Color.White, 1.6f);

        // Previous / next nation
        _nextRect = new Rectangle(_closeRect.X - 32, r.Y + 8, 26, 22);
        _prevRect = new Rectangle(_nextRect.X - 30, r.Y + 8, 26, 22);
        UITheme.DrawButton(sb, _prevRect, "<", _prevRect.Contains(mouse.Position), false, null, UITheme.FontNormal, data.Civs.Count > 1);
        UITheme.DrawButton(sb, _nextRect, ">", _nextRect.Contains(mouse.Position), false, null, UITheme.FontNormal, data.Civs.Count > 1);

        if (found == null)
        {
            UITheme.DrawTextShadowed(sb, "NATIONS", new Vector2(r.X + 16, r.Y + 18), UITheme.Gold, UITheme.FontLarge);
            return;
        }

        var civ = found.Value;
        Color civColor = TerrainRenderer.GetCivPaletteColor(civ.Id);
        UITheme.FillRounded(sb, new Rectangle(r.X + 12, r.Y + 12, 8, 40), civColor);
        string name = UITheme.Ellipsize(civ.Name, r.Width - 230, UITheme.FontLarge);
        UITheme.DrawTextShadowed(sb, name, new Vector2(r.X + 30, r.Y + 9), UITheme.Gold, UITheme.FontLarge);

        var parts = new List<string>();
        if (civ.GovType.HasValue) parts.Add(civ.GovType.Value.ToString());
        if (!string.IsNullOrEmpty(civ.Ethnicity)) parts.Add($"{civ.Ethnicity} people");
        parts.Add($"{SocietyStyle.HomelandName(civ.Homeland)} homeland");
        if (civ.Detail != null && civ.Detail.Founded != 0) parts.Add($"founded year {civ.Detail.Founded}");
        if (civ.AtWar) parts.Add("AT WAR");
        string subtitle = UITheme.Ellipsize(string.Join("  -  ", parts), r.Width - 160, UITheme.FontSmall + 1);
        UITheme.DrawText(sb, subtitle, new Vector2(r.X + 31, r.Y + 38), civ.AtWar ? new Color(255, 170, 160) : UITheme.TextDim, UITheme.FontSmall + 1);
    }

    // ------------------------------------------------------------------
    // Small drawing helpers
    // ------------------------------------------------------------------

    private static void Section(SpriteBatch sb, string title, int x, ref int y, int width)
    {
        UITheme.DrawText(sb, title, new Vector2(x, y), UITheme.Accent, 12f);
        var ts = UITheme.Measure(title, 12f);
        sb.Draw(UITheme.Pixel, new Rectangle((int)(x + ts.X + 8), y + 8, Math.Max(0, (int)(width - ts.X - 8)), 1), UITheme.Border);
        y += 22;
    }

    private static void Row(SpriteBatch sb, string label, string value, int x, ref int y, int width, Color? valueColor = null)
    {
        UITheme.DrawText(sb, label, new Vector2(x, y), UITheme.TextDim, 13f);
        value = UITheme.Ellipsize(value, width - UITheme.Measure(label, 13f).X - 12, 13f);
        var vs = UITheme.Measure(value, 13f);
        UITheme.DrawText(sb, value, new Vector2(x + width - vs.X, y), valueColor ?? UITheme.Text, 13f);
        y += 19;
    }

    private static void MeterRow(SpriteBatch sb, string label, float value, int x, ref int y, int width, Color color, string? text = null)
    {
        UITheme.DrawText(sb, label, new Vector2(x, y), UITheme.TextDim, 13f);
        string v = text ?? $"{value:P0}";
        var vs = UITheme.Measure(v, 13f);
        UITheme.DrawText(sb, v, new Vector2(x + width - vs.X, y), color, 13f);
        var bar = new Rectangle(x, y + 18, width, 5);
        sb.Draw(UITheme.Pixel, bar, new Color(0, 0, 0, 140));
        sb.Draw(UITheme.Pixel, new Rectangle(bar.X, bar.Y, (int)(bar.Width * Math.Clamp(value, 0f, 1f)), bar.Height), color);
        y += 28;
    }

    private static void Chip(SpriteBatch sb, string text, Color color, int x, int y, out int width, float size = 11f)
    {
        var ts = UITheme.Measure(text, size);
        width = (int)ts.X + 12;
        var r = new Rectangle(x, y, width, (int)ts.Y + 4);
        UITheme.FillRounded(sb, r, Color.Lerp(new Color(20, 26, 40), color, 0.35f));
        UITheme.OutlineRounded(sb, r, color * 0.8f);
        UITheme.DrawText(sb, text, new Vector2(x + 6, y + 2), Color.Lerp(color, Color.White, 0.45f), size);
    }

    private static void Paragraph(SpriteBatch sb, string text, int x, ref int y, int width, Color color, float size = 13f)
    {
        foreach (var line in UITheme.WrapText(text, width, size))
        {
            UITheme.DrawText(sb, line, new Vector2(x, y), color, size);
            y += (int)(size + 5);
        }
    }

    /// <summary>Horizontal bar split into coloured shares.</summary>
    private static void StackedBar(SpriteBatch sb, Rectangle bar, IEnumerable<(float Share, Color Color)> parts)
    {
        sb.Draw(UITheme.Pixel, bar, new Color(0, 0, 0, 140));
        float x = bar.X;
        foreach (var (share, color) in parts)
        {
            float w = bar.Width * Math.Clamp(share, 0f, 1f);
            if (w <= 0.5f) continue;
            sb.Draw(UITheme.Pixel, new Rectangle((int)x, bar.Y, (int)MathF.Ceiling(w), bar.Height), color);
            x += w;
        }
        UITheme.FillGradient(sb, new Rectangle(bar.X, bar.Y, bar.Width, Math.Max(1, bar.Height / 2)), Color.White * (30 / 255f));
        UITheme.DrawRectOutline(sb, bar, new Color(0, 0, 0, 150));
    }

    private static string FormatNumber(float v)
    {
        if (Math.Abs(v) >= 1_000_000) return $"{v / 1_000_000f:0.#}M";
        if (Math.Abs(v) >= 10_000) return $"{v / 1_000f:0.#}K";
        return v.ToString("N0");
    }

    // ------------------------------------------------------------------
    // Overview
    // ------------------------------------------------------------------

    private int DrawOverview(SpriteBatch sb, CivRenderData data, CivRenderData.CivInfo civ, int top, int currentYear)
    {
        int pad = 18;
        int colGap = 26;
        int colW = (_contentRect.Width - pad * 2 - colGap) / 2;
        int lx = _contentRect.X + pad;
        int rx = lx + colW + colGap;

        // ---- Left column ----
        int y = top;
        Section(sb, "NATION", lx, ref y, colW);
        Row(sb, "Population", civ.Population.ToString("N0"), lx, ref y, colW);
        Row(sb, "Settlements", $"{civ.CityCount}  ({civ.TerritorySize:N0} cells of land)", lx, ref y, colW);
        Row(sb, "Technology", $"level {civ.TechLevel}", lx, ref y, colW);
        string income = civ.GoldIncome >= 0 ? $"+{civ.GoldIncome:N0}" : civ.GoldIncome.ToString("N0");
        Row(sb, "Treasury", $"{civ.Gold:N0} gold ({income}/yr)", lx, ref y, colW, UITheme.Gold);
        Row(sb, "Development", $"x{civ.DevelopmentModifier:0.00} ({SocietyStyle.HomelandName(civ.Homeland).ToLowerInvariant()} homeland)", lx, ref y, colW,
            civ.DevelopmentModifier >= 1f ? UITheme.Good : UITheme.Warn);
        MeterRow(sb, "Stability", civ.Stability, lx, ref y, colW, Color.Lerp(UITheme.Bad, UITheme.Good, civ.Stability));
        MeterRow(sb, "Prosperity", civ.Prosperity, lx, ref y, colW, Color.Lerp(UITheme.Warn, UITheme.Good, civ.Prosperity));

        y += 6;
        Section(sb, "GOVERNMENT", lx, ref y, colW);
        Color govColor = civ.GovType.HasValue ? SocietyStyle.GovernmentColor(civ.GovType.Value) : UITheme.TextDim;
        Row(sb, "Form", civ.GovType?.ToString() ?? "None", lx, ref y, colW, govColor);
        if (!string.IsNullOrEmpty(civ.RulerName))
            Row(sb, "Ruler", $"{civ.RulerTitle} {civ.RulerName}".Trim() + (civ.RulerAge > 0 ? $", {civ.RulerAge}" : ""), lx, ref y, colW);
        if (!string.IsNullOrEmpty(civ.RulingParty))
            Row(sb, "Ruling party", civ.RulingParty, lx, ref y, colW);
        if (civ.Detail != null && civ.Detail.IsElected && civ.NextElectionYear > 0)
            Row(sb, "Next election", $"year {civ.NextElectionYear} (in {Math.Max(0, civ.NextElectionYear - currentYear)})", lx, ref y, colW);
        if (civ.Detail != null && civ.Detail.EstablishedYear != 0)
            Row(sb, "Since", $"year {civ.Detail.EstablishedYear}", lx, ref y, colW);

        y += 6;
        Section(sb, "STRATEGY", lx, ref y, colW);
        Color pc = SocietyStyle.PostureColor(civ.Posture);
        Chip(sb, SocietyStyle.PostureName(civ.Posture).ToUpperInvariant(), pc, lx, y, out int chipW, 12f);
        if (civ.PostureSinceYear > 0)
            UITheme.DrawText(sb, $"since year {civ.PostureSinceYear}", new Vector2(lx + chipW + 8, y + 2), UITheme.TextMuted, 12f);
        y += 26;
        if (!string.IsNullOrEmpty(civ.PostureRationale))
            Paragraph(sb, civ.PostureRationale, lx, ref y, colW, UITheme.Text, 13f);
        if (civ.MainThreatId.HasValue && data.FindCiv(civ.MainThreatId.Value) is { } threat)
            Row(sb, "Main threat", threat.Name, lx, ref y, colW, UITheme.Warn);
        if (civ.ConquestTargetId.HasValue && data.FindCiv(civ.ConquestTargetId.Value) is { } target)
            Row(sb, "Conquest target", target.Name, lx, ref y, colW, UITheme.Bad);
        MeterRow(sb, "Threat level", Math.Min(1f, civ.ThreatLevel), lx, ref y, colW,
            Color.Lerp(UITheme.Good, UITheme.Bad, Math.Min(1f, civ.ThreatLevel)), $"{civ.ThreatLevel:0.00}");
        if (civ.ExistentialThreat > 0.01f)
            MeterRow(sb, "Existential threat", civ.ExistentialThreat, lx, ref y, colW, UITheme.Bad);

        UITheme.DrawText(sb, "Budget", new Vector2(lx, y), UITheme.TextDim, 13f);
        y += 19;
        var budget = new (string Name, float Share, Color Color)[]
        {
            ("Military", civ.MilitaryShare, new Color(230, 96, 80)),
            ("Research", civ.ResearchShare, new Color(100, 170, 255)),
            ("Economy", civ.EconomyShare, new Color(250, 200, 80)),
            ("Space", civ.SpaceShare, new Color(180, 150, 255)),
        };
        StackedBar(sb, new Rectangle(lx, y, colW, 12), budget.Select(b => (b.Share, b.Color)));
        y += 18;
        int bx = lx;
        foreach (var (bn, bs, bc) in budget)
        {
            string label = $"{bn} {bs:P0}";
            int lw = (int)UITheme.Measure(label, 11f).X + 22;
            if (bx + lw > lx + colW) { bx = lx; y += 16; }
            sb.Draw(UITheme.Pixel, new Rectangle(bx, y + 3, 9, 9), bc);
            UITheme.DrawText(sb, label, new Vector2(bx + 13, y), UITheme.TextDim, 11f);
            bx += lw;
        }
        y += 20;
        int leftBottom = y;

        // ---- Right column ----
        y = top;
        Section(sb, "ENERGY", rx, ref y, colW);
        if (civ.EnergyMix != null && civ.EnergyMix.Length > 0)
        {
            StackedBar(sb, new Rectangle(rx, y, colW, 14), civ.EnergyMix.Select(m => (m.Share, SocietyStyle.EnergyColor(m.Source))));
            y += 20;
            int ex = rx;
            foreach (var (src, share) in civ.EnergyMix)
            {
                string label = $"{SocietyStyle.EnergyName(src)} {share:P0}";
                int lw = (int)UITheme.Measure(label, 11f).X + 22;
                if (ex + lw > rx + colW) { ex = rx; y += 16; }
                sb.Draw(UITheme.Pixel, new Rectangle(ex, y + 3, 9, 9), SocietyStyle.EnergyColor(src));
                UITheme.DrawText(sb, label, new Vector2(ex + 13, y), UITheme.TextDim, 11f);
                ex += lw;
            }
            y += 22;
        }
        else
        {
            UITheme.DrawText(sb, "Pre-industrial: wood, water wheels and muscle", new Vector2(rx, y), UITheme.TextMuted, 12f);
            y += 20;
        }
        if (civ.EnergyProduction > 0 || civ.EnergyDemand > 0)
        {
            float balance = civ.EnergyProduction - civ.EnergyDemand;
            Row(sb, "Production / demand", $"{FormatNumber(civ.EnergyProduction)} / {FormatNumber(civ.EnergyDemand)}", rx, ref y, colW,
                balance >= 0 ? UITheme.Good : UITheme.Bad);
        }
        MeterRow(sb, "Electrification", civ.Electrification, rx, ref y, colW, SocietyStyle.PowerLine);
        MeterRow(sb, "Internet", civ.InternetPenetration, rx, ref y, colW, SocietyStyle.DataCable);

        y += 6;
        Section(sb, "SPACE", rx, ref y, colW);
        var stages = Enum.GetValues<SpaceStage>();
        int stageIndex = Array.IndexOf(stages, civ.SpaceStage);
        int pipW = (colW - (stages.Length - 1) * 4) / Math.Max(1, stages.Length - 1);
        for (int i = 1; i < stages.Length; i++)
        {
            var pip = new Rectangle(rx + (i - 1) * (pipW + 4), y, pipW, 8);
            bool reached = i <= stageIndex;
            UITheme.FillRounded(sb, pip, reached ? new Color(180, 150, 255) : new Color(40, 46, 66));
            if (pip.Contains(Mouse.GetState().Position)) _hoverTip = SocietyStyle.SpaceStageName(stages[i]);
        }
        y += 14;
        Row(sb, "Stage", SocietyStyle.SpaceStageName(civ.SpaceStage), rx, ref y, colW, civ.SpaceStage > SpaceStage.None ? new Color(200, 180, 255) : UITheme.TextMuted);
        if (civ.Satellites > 0 || civ.Astronauts > 0)
            Row(sb, "Satellites / astronauts", $"{civ.Satellites} / {civ.Astronauts}", rx, ref y, colW);
        int inOrbit = data.Orbitals.Count(o => o.CivId == civ.Id);
        if (inOrbit > 0)
            Row(sb, "Objects in space", $"{inOrbit} ({data.Orbitals.Where(o => o.CivId == civ.Id).Sum(o => o.Crew)} aboard)", rx, ref y, colW);

        y += 6;
        Section(sb, "NATIONAL PROJECTS", rx, ref y, colW);
        var active = civ.Detail?.ActiveProject;
        if (active != null)
        {
            var ap = active.Value;
            Color kc = SocietyStyle.ProjectColor(ap.Kind);
            Chip(sb, ap.Kind.ToString().ToUpperInvariant(), kc, rx, y, out int kw);
            string pn = UITheme.Ellipsize(ap.Name, colW - kw - 10, 13f);
            UITheme.DrawText(sb, pn, new Vector2(rx + kw + 8, y), UITheme.Text, 13f);
            y += 22;
            UITheme.DrawMeter(sb, new Rectangle(rx, y, colW, 14), ap.Fraction, kc * 0.9f, $"{ap.Fraction:P0}");
            y += 18;
            if (ap.StartedYear != 0)
            {
                UITheme.DrawText(sb, $"Started year {ap.StartedYear}", new Vector2(rx, y), UITheme.TextMuted, 11f);
                y += 16;
            }
        }
        else
        {
            UITheme.DrawText(sb, "No programme under way", new Vector2(rx, y), UITheme.TextMuted, 12f);
            y += 18;
        }
        var done = civ.Detail?.CompletedProjects ?? new List<CivRenderData.ProjectInfo>();
        if (done.Count > 0)
        {
            y += 4;
            UITheme.DrawText(sb, $"Completed ({done.Count})", new Vector2(rx, y), UITheme.TextDim, 12f);
            y += 18;
            int cx = rx;
            foreach (var p in done.OrderByDescending(p => p.CompletedYear))
            {
                string label = p.CompletedYear > 0 ? $"{p.Name} ({p.CompletedYear})" : p.Name;
                int cw = (int)UITheme.Measure(label, 11f).X + 12;
                if (cx + cw > rx + colW) { cx = rx; y += 20; }
                Chip(sb, label, SocietyStyle.ProjectColor(p.Kind), cx, y, out cw);
                cx += cw + 5;
            }
            y += 22;
        }

        return Math.Max(leftBottom, y);
    }

    // ------------------------------------------------------------------
    // Succession
    // ------------------------------------------------------------------

    private static int ReignEnd(CivRenderData.RulerInfo r, List<CivRenderData.RulerInfo> rulers, int currentRulerId, int currentYear)
    {
        if (r.Id == currentRulerId) return currentYear;
        int next = int.MaxValue;
        foreach (var o in rulers)
            if (o.YearTookPower > r.YearTookPower && o.YearTookPower < next) next = o.YearTookPower;
        if (r.DeathYear.HasValue) next = Math.Min(next, r.DeathYear.Value);
        return next == int.MaxValue ? currentYear : next;
    }

    private int DrawSuccession(SpriteBatch sb, CivRenderData data, CivRenderData.CivInfo civ, int top, int currentYear, MouseState mouse)
    {
        var d = civ.Detail;
        int pad = 18;
        int x0 = _contentRect.X + pad;
        int width = _contentRect.Width - pad * 2;
        int y = top;
        if (d == null) return y;

        int currentId = d.CurrentRulerId;
        var byId = new Dictionary<int, CivRenderData.RulerInfo>();
        foreach (var r in d.Rulers) byId[r.Id] = r;
        var reigned = d.Rulers.Where(r => r.YearTookPower > 0 || r.Id == currentId).OrderBy(r => r.YearTookPower).ToList();

        // Current ruler card
        int listW = Math.Min(290, width / 3 + 20);
        Section(sb, d.IsHereditary ? "LINE OF SUCCESSION" : "LEADERS", x0, ref y, listW);
        if (byId.TryGetValue(currentId, out var ruler))
        {
            var card = new Rectangle(x0, y, listW, 50);
            UITheme.FillRounded(sb, card, new Color(40, 34, 18));
            UITheme.OutlineRounded(sb, card, UITheme.Gold * 0.8f);
            var icons = MapIcons.GetShared(_graphicsDevice);
            sb.Draw(icons.Crown, new Rectangle(card.X + 8, card.Y + 12, 24, 24), Color.White);
            UITheme.DrawText(sb, UITheme.Ellipsize($"{ruler.Title} {ruler.Name}".Trim(), listW - 50, 14f), new Vector2(card.X + 40, card.Y + 6), UITheme.Gold, 14f);
            string sub = $"age {ruler.Age}";
            if (ruler.YearTookPower > 0) sub += $" - reigning since {ruler.YearTookPower} ({Math.Max(0, currentYear - ruler.YearTookPower)} years)";
            UITheme.DrawText(sb, UITheme.Ellipsize(sub, listW - 50, 11f), new Vector2(card.X + 40, card.Y + 28), UITheme.TextDim, 11f);
            y += 58;
        }

        if (d.IsHereditary)
        {
            if (d.SuccessionLine.Count == 0)
            {
                UITheme.DrawText(sb, "No heirs: a succession crisis looms", new Vector2(x0, y), UITheme.Bad, 12f);
                y += 20;
            }
            for (int i = 0; i < Math.Min(10, d.SuccessionLine.Count); i++)
            {
                var h = d.SuccessionLine[i];
                bool heir = h.Id == d.HeirApparentId || (d.HeirApparentId < 0 && i == 0);
                var row = new Rectangle(x0, y, listW, 26);
                if (heir)
                {
                    UITheme.FillRounded(sb, row, new Color(52, 44, 20));
                    UITheme.OutlineRounded(sb, row, UITheme.Gold * 0.6f);
                }
                UITheme.DrawText(sb, $"{i + 1}.", new Vector2(row.X + 8, row.Y + 5), UITheme.TextMuted, 12f);
                string relation = h.ParentId == currentId ? "child of the ruler" : h.ParentId.HasValue && byId.ContainsKey(h.ParentId.Value) ? $"child of {byId[h.ParentId.Value].Name}" : "";
                UITheme.DrawText(sb, UITheme.Ellipsize(h.Name, listW - 120, 13f), new Vector2(row.X + 30, row.Y + 4), heir ? UITheme.Gold : UITheme.Text, 13f);
                string age = $"age {h.Age}";
                var agS = UITheme.Measure(age, 11f);
                UITheme.DrawText(sb, age, new Vector2(row.Right - 8 - agS.X, row.Y + 6), UITheme.TextDim, 11f);
                if (row.Contains(mouse.Position))
                    _hoverTip = $"{h.Name}{(heir ? " - heir apparent" : "")}\n{(relation.Length > 0 ? relation : "claimant")}\nWisdom {h.Wisdom:0.0} - charisma {h.Charisma:0.0} - ambition {h.Ambition:0.0}";
                y += 28;
            }
        }
        else
        {
            // Elected / appointed leaders, most recent first
            foreach (var r in reigned.AsEnumerable().Reverse().Take(12))
            {
                int end = ReignEnd(r, reigned, currentId, currentYear);
                bool isCurrent = r.Id == currentId;
                string years = r.YearTookPower > 0 ? $"{r.YearTookPower}-{(isCurrent ? "" : end.ToString())}" : "";
                UITheme.DrawText(sb, UITheme.Ellipsize($"{r.Title} {r.Name}".Trim(), listW - 90, 13f), new Vector2(x0, y), isCurrent ? UITheme.Gold : (r.IsAlive ? UITheme.Text : UITheme.TextMuted), 13f);
                var ys = UITheme.Measure(years, 11f);
                UITheme.DrawText(sb, years, new Vector2(x0 + listW - ys.X, y + 2), UITheme.TextDim, 11f);
                y += 21;
            }
            if (reigned.Count == 0)
            {
                UITheme.DrawText(sb, "No recorded leaders yet", new Vector2(x0, y), UITheme.TextMuted, 12f);
                y += 20;
            }
        }

        // Dynasties
        if (d.Dynasties.Count > 0)
        {
            y += 8;
            Section(sb, "DYNASTIES", x0, ref y, listW);
            foreach (var dyn in d.Dynasties.OrderByDescending(x => x.FoundedYear).Take(6))
            {
                bool reigning = byId.TryGetValue(currentId, out var cr) && cr.DynastyId == dyn.Id;
                string label = UITheme.Ellipsize(dyn.Name, listW - 110, 13f);
                UITheme.DrawText(sb, label, new Vector2(x0, y), dyn.Extinct ? UITheme.TextMuted : reigning ? UITheme.Gold : UITheme.Text, 13f);
                string info = dyn.Extinct ? "extinct" : $"{dyn.Generations} gen. - {dyn.FoundedYear}";
                var isz = UITheme.Measure(info, 11f);
                UITheme.DrawText(sb, info, new Vector2(x0 + listW - isz.X, y + 2), UITheme.TextDim, 11f);
                y += 20;
            }
        }
        int leftBottom = y;

        // ---- Dynasty tree ----
        int treeX = x0 + listW + 24;
        int treeW = _contentRect.Right - pad - treeX;
        y = top;

        // Power not inherited: a reign timeline says more than a family tree
        if (!d.IsHereditary)
            return Math.Max(leftBottom, DrawReignTimeline(sb, reigned, currentId, currentYear, treeX, y, treeW, mouse));

        byId.TryGetValue(currentId, out var head);
        int dynastyId = head.Name != null ? head.DynastyId : (d.Dynasties.Count > 0 ? d.Dynasties[^1].Id : 0);
        string dynName = d.Dynasties.FirstOrDefault(x => x.Id == dynastyId).Name ?? "";
        Section(sb, string.IsNullOrEmpty(dynName) ? "FAMILY TREE" : $"FAMILY TREE - {dynName.ToUpperInvariant()}", treeX, ref y, treeW);

        int heirId = d.HeirApparentId >= 0 ? d.HeirApparentId : (d.SuccessionLine.Count > 0 ? d.SuccessionLine[0].Id : -1);
        var lineIds = new HashSet<int>(d.SuccessionLine.Select(s => s.Id));
        // Rulers, the living and claimants, plus the ancestors that connect them
        var family = d.Rulers.Where(r => dynastyId == 0 || r.DynastyId == dynastyId || lineIds.Contains(r.Id)).ToDictionary(r => r.Id);
        var members = new Dictionary<int, CivRenderData.RulerInfo>();
        foreach (var r in family.Values)
        {
            if (!(r.YearTookPower > 0 || r.IsAlive || r.Id == currentId || lineIds.Contains(r.Id))) continue;
            var cur = r;
            while (members.TryAdd(cur.Id, cur) && cur.ParentId.HasValue && family.TryGetValue(cur.ParentId.Value, out var parent))
                cur = parent;
        }
        bool linked = members.Values.Any(m => m.ParentId.HasValue && members.ContainsKey(m.ParentId.Value));
        if (!linked && members.Count > 1)
            return Math.Max(leftBottom, DrawReignTimeline(sb, reigned, currentId, currentYear, treeX, y, treeW, mouse, false));
        if (members.Count == 0)
        {
            UITheme.DrawText(sb, d.IsHereditary ? "No family recorded yet" : "Power is not inherited under this government", new Vector2(treeX, y), UITheme.TextMuted, 12f);
            return Math.Max(leftBottom, y + 24);
        }

        var children = new Dictionary<int, List<int>>();
        foreach (var m in members.Values)
        {
            if (m.ParentId.HasValue && members.ContainsKey(m.ParentId.Value))
            {
                if (!children.TryGetValue(m.ParentId.Value, out var list)) children[m.ParentId.Value] = list = new List<int>();
                list.Add(m.Id);
            }
        }
        foreach (var list in children.Values) list.Sort((a, b) => members[a].Id.CompareTo(members[b].Id));

        var depth = new Dictionary<int, int>();
        int Depth(int id)
        {
            if (depth.TryGetValue(id, out int dd)) return dd;
            var m = members[id];
            int v = m.ParentId.HasValue && members.ContainsKey(m.ParentId.Value) ? Depth(m.ParentId.Value) + 1 : 0;
            depth[id] = v;
            return v;
        }
        foreach (var id in members.Keys) Depth(id);
        int maxDepth = depth.Values.Max();
        const int maxGenerations = 6;
        int minDepth = Math.Max(0, maxDepth - (maxGenerations - 1));
        var roots = members.Keys.Where(id => depth[id] == minDepth || (depth[id] > minDepth && !(members[id].ParentId.HasValue && members.ContainsKey(members[id].ParentId!.Value))))
                                .Where(id => depth[id] >= minDepth).OrderBy(id => members[id].YearTookPower).ThenBy(id => id).ToList();

        // Tidy layout: leaves take consecutive slots, parents sit over their children
        var slotX = new Dictionary<int, float>();
        float nextSlot = 0;
        float Layout(int id)
        {
            var kids = children.GetValueOrDefault(id);
            if (kids == null || kids.Count == 0) { slotX[id] = nextSlot; nextSlot += 1; return slotX[id]; }
            float sum = 0;
            foreach (var k in kids) sum += Layout(k);
            slotX[id] = sum / kids.Count;
            return slotX[id];
        }
        foreach (var r in roots) { Layout(r); nextSlot += 0.3f; }

        int slots = Math.Max(1, (int)MathF.Ceiling(nextSlot));
        float slotW = Math.Clamp(treeW / (float)slots, 70f, 150f);
        int nodeW = (int)slotW - 8;
        const int nodeH = 40, rowH = 66;
        float totalW = slots * slotW;
        float originX = treeX + Math.Max(0, (treeW - totalW) / 2f);
        int originY = y + 4;

        Rectangle NodeRect(int id) => new Rectangle((int)(originX + slotX[id] * slotW + (slotW - nodeW) / 2f),
            originY + (depth[id] - minDepth) * rowH, nodeW, nodeH);

        // Connectors
        foreach (var (pid, kids) in children)
        {
            if (!slotX.ContainsKey(pid)) continue;
            var pr = NodeRect(pid);
            float midY = pr.Bottom + (rowH - nodeH) / 2f;
            foreach (var k in kids)
            {
                if (!slotX.ContainsKey(k)) continue;
                var kr = NodeRect(k);
                var col = new Color(110, 130, 170);
                UITheme.DrawLine(sb, new Vector2(pr.Center.X, pr.Bottom), new Vector2(pr.Center.X, midY), col, 1.5f);
                UITheme.DrawLine(sb, new Vector2(pr.Center.X, midY), new Vector2(kr.Center.X, midY), col, 1.5f);
                UITheme.DrawLine(sb, new Vector2(kr.Center.X, midY), new Vector2(kr.Center.X, kr.Y), col, 1.5f);
            }
        }

        int treeBottom = originY;
        var crown = MapIcons.GetShared(_graphicsDevice).Crown;
        foreach (var id in slotX.Keys)
        {
            var m = members[id];
            var rect = NodeRect(id);
            treeBottom = Math.Max(treeBottom, rect.Bottom);
            bool isCurrent = id == currentId;
            bool isHeir = id == heirId;
            bool dead = !m.IsAlive;
            bool ruled = m.YearTookPower > 0;

            Color bg = isCurrent ? new Color(64, 52, 20) : dead ? new Color(26, 30, 38) : new Color(30, 42, 62);
            Color border = isCurrent ? UITheme.Gold : isHeir ? new Color(255, 214, 110) : dead ? new Color(60, 66, 78) : UITheme.Border;
            UITheme.FillRounded(sb, rect, bg);
            UITheme.OutlineRounded(sb, rect, border);
            if (isHeir)
            {
                // Double outline for the heir apparent
                UITheme.OutlineRounded(sb, new Rectangle(rect.X - 2, rect.Y - 2, rect.Width + 4, rect.Height + 4), new Color(255, 214, 110) * 0.6f);
            }
            if (ruled) sb.Draw(UITheme.Pixel, new Rectangle(rect.X + 1, rect.Y + 4, 3, rect.Height - 8), dead ? new Color(120, 100, 60) : UITheme.Gold);

            float fs = nodeW < 100 ? 11f : 12f;
            Color nameColor = isCurrent ? UITheme.Gold : dead ? UITheme.TextMuted : UITheme.Text;
            int textLeft = rect.X + 8;
            if (isCurrent)
            {
                sb.Draw(crown, new Rectangle(rect.Right - 18, rect.Y + 3, 14, 14), Color.White);
            }
            UITheme.DrawText(sb, UITheme.Ellipsize(m.Name, nodeW - (isCurrent ? 26 : 12), fs), new Vector2(textLeft, rect.Y + 4), nameColor, fs);
            string line2;
            if (ruled)
            {
                int end = ReignEnd(m, reigned, currentId, currentYear);
                line2 = isCurrent ? $"r. {m.YearTookPower}-" : $"r. {m.YearTookPower}-{end}";
            }
            else if (dead) line2 = m.DeathYear.HasValue ? $"d. {m.DeathYear}" : "deceased";
            else line2 = isHeir ? $"heir, age {m.Age}" : $"age {m.Age}";
            UITheme.DrawText(sb, UITheme.Ellipsize(line2, nodeW - 12, 10.5f), new Vector2(textLeft, rect.Y + 22), dead ? new Color(96, 104, 120) : UITheme.TextDim, 10.5f);

            if (rect.Contains(mouse.Position) && _contentRect.Contains(mouse.Position))
            {
                var tip = new List<string> { $"{m.Title} {m.Name}".Trim() };
                if (ruled) tip.Add($"Reigned {m.YearTookPower}-{(isCurrent ? "present" : ReignEnd(m, reigned, currentId, currentYear).ToString())}");
                tip.Add(dead ? (m.DeathYear.HasValue ? $"Died in year {m.DeathYear} aged {m.Age}" : "Deceased") : $"Alive, age {m.Age}");
                if (isHeir) tip.Add("Heir apparent");
                tip.Add($"Wisdom {m.Wisdom:0.0}  Charisma {m.Charisma:0.0}  Ambition {m.Ambition:0.0}");
                tip.Add($"Brutality {m.Brutality:0.0}  Piety {m.Piety:0.0}");
                _hoverTip = string.Join("\n", tip);
            }
        }

        // Key
        int ky = treeBottom + 14;
        int kx = treeX;
        void Key(Color fill, Color border, string label)
        {
            var r = new Rectangle(kx, ky + 2, 14, 12);
            UITheme.FillRounded(sb, r, fill);
            UITheme.OutlineRounded(sb, r, border);
            UITheme.DrawText(sb, label, new Vector2(kx + 19, ky), UITheme.TextDim, 11f);
            kx += (int)UITheme.Measure(label, 11f).X + 36;
        }
        Key(new Color(64, 52, 20), UITheme.Gold, "Reigning");
        Key(new Color(30, 42, 62), new Color(255, 214, 110), "Heir apparent");
        Key(new Color(30, 42, 62), UITheme.Border, "Living");
        Key(new Color(26, 30, 38), new Color(60, 66, 78), "Deceased");
        if (minDepth > 0)
            UITheme.DrawText(sb, $"Showing the last {maxGenerations} generations", new Vector2(treeX, ky + 18), UITheme.TextMuted, 11f);

        return Math.Max(leftBottom, ky + 40);
    }

    private int DrawReignTimeline(SpriteBatch sb, List<CivRenderData.RulerInfo> reigned, int currentId, int currentYear,
        int x, int y, int width, MouseState mouse, bool header = true)
    {
        if (header) Section(sb, "REIGN TIMELINE", x, ref y, width);
        var rows = reigned.Where(r => r.YearTookPower > 0 || r.Id == currentId).TakeLast(14).ToList();
        if (rows.Count == 0)
        {
            UITheme.DrawText(sb, "No recorded leaders yet", new Vector2(x, y), UITheme.TextMuted, 12f);
            return y + 24;
        }
        int start = rows.Min(r => r.YearTookPower);
        int end = Math.Max(start + 1, Math.Max(currentYear, rows.Max(r => ReignEnd(r, reigned, currentId, currentYear))));
        int labelW = Math.Min(150, width / 3);
        int barX = x + labelW + 8, barW = width - labelW - 8;
        float Px(int year) => barX + (year - start) / (float)(end - start) * barW;

        // Axis
        for (int i = 0; i <= 4; i++)
        {
            int year = start + (end - start) * i / 4;
            float px = Px(year);
            sb.Draw(UITheme.Pixel, new Rectangle((int)px, y, 1, rows.Count * 22 + 4), new Color(255, 255, 255, 14));
            string label = year.ToString();
            var ls = UITheme.Measure(label, 10.5f);
            UITheme.DrawText(sb, label, new Vector2(Math.Clamp(px - ls.X / 2, barX, barX + barW - ls.X), y + rows.Count * 22 + 6), UITheme.TextMuted, 10.5f);
        }

        foreach (var r in rows)
        {
            bool current = r.Id == currentId;
            int rEnd = ReignEnd(r, reigned, currentId, currentYear);
            UITheme.DrawText(sb, UITheme.Ellipsize(r.Name, labelW, 12f), new Vector2(x, y + 2), current ? UITheme.Gold : r.IsAlive ? UITheme.Text : UITheme.TextDim, 12f);
            float bx0 = Px(r.YearTookPower), bx1 = Math.Max(bx0 + 3, Px(rEnd));
            var bar = new Rectangle((int)bx0, y + 4, (int)(bx1 - bx0), 12);
            UITheme.FillRounded(sb, bar, current ? UITheme.Gold : new Color(90, 120, 170));
            var row = new Rectangle(x, y, width, 22);
            if (row.Contains(mouse.Position) && _contentRect.Contains(mouse.Position))
                _hoverTip = $"{r.Title} {r.Name}".Trim() + $"\nIn power {r.YearTookPower}-{(current ? "present" : rEnd.ToString())} ({Math.Max(0, rEnd - r.YearTookPower)} years)" +
                            (r.IsAlive ? $"\nAlive, age {r.Age}" : "\nDeceased");
            y += 22;
        }
        return y + 30;
    }

    // ------------------------------------------------------------------
    // Politics
    // ------------------------------------------------------------------

    private int DrawPolitics(SpriteBatch sb, CivRenderData data, CivRenderData.CivInfo civ, int top, int currentYear)
    {
        var d = civ.Detail;
        int pad = 18;
        int x0 = _contentRect.X + pad;
        int width = _contentRect.Width - pad * 2;
        int y = top;
        if (d == null) return y;

        if (d.Parties.Count == 0)
        {
            Section(sb, "POLITICS", x0, ref y, width);
            string gov = civ.GovType?.ToString() ?? "This nation";
            Paragraph(sb, d.IsElected
                ? $"{gov}: no parties have formed yet. Parties and elections appear as the nation develops."
                : $"{gov}: power is not contested at the ballot box, so there are no parties or elections.",
                x0, ref y, width, UITheme.TextDim);
            y += 8;
            int w3 = Math.Min(360, width);
            MeterRow(sb, "Government stability", d.GovStability, x0, ref y, w3, Color.Lerp(UITheme.Bad, UITheme.Good, d.GovStability));
            MeterRow(sb, "Legitimacy", d.Legitimacy, x0, ref y, w3, Color.Lerp(UITheme.Bad, UITheme.Good, d.Legitimacy));
            MeterRow(sb, "Corruption", d.Corruption, x0, ref y, w3, Color.Lerp(UITheme.Good, UITheme.Bad, d.Corruption));
            return y;
        }

        int colGap = 26;
        int leftW = Math.Min(340, (width - colGap) / 2);
        int rightX = x0 + leftW + colGap;
        int rightW = width - leftW - colGap;

        // Parliament hemicycle (100 seats)
        Section(sb, "PARLIAMENT", x0, ref y, leftW);
        var parties = d.Parties.OrderBy(p => IdeologyOrder(p.Ideology)).ToList();
        int totalSeats = parties.Sum(p => p.SeatsWon);
        bool bySupport = totalSeats <= 0;
        var seatColors = new List<Color>();
        if (bySupport)
        {
            float sum = Math.Max(0.0001f, parties.Sum(p => p.Support));
            foreach (var p in parties)
                for (int i = 0; i < (int)MathF.Round(p.Support / sum * 100f); i++) seatColors.Add(SocietyStyle.IdeologyColor(p.Ideology));
        }
        else
        {
            foreach (var p in parties)
                for (int i = 0; i < p.SeatsWon; i++) seatColors.Add(SocietyStyle.IdeologyColor(p.Ideology));
        }
        int seatCount = seatColors.Count;
        var center = new Vector2(x0 + leftW / 2f, y + 150);
        float outer = Math.Min(leftW / 2f - 8, 140f);
        var seats = HemicycleSeats(Math.Max(1, seatCount), center, outer * 0.42f, outer);
        float seatR = Math.Max(2.5f, outer / 26f);
        for (int i = 0; i < seats.Count && i < seatColors.Count; i++)
            sb.Draw(_disc!, new Rectangle((int)(seats[i].X - seatR), (int)(seats[i].Y - seatR), (int)(seatR * 2), (int)(seatR * 2)), seatColors[i]);
        string seatsLabel = bySupport ? "by support" : $"{totalSeats} seats";
        var sl = UITheme.Measure(seatsLabel, 12f);
        UITheme.DrawText(sb, seatsLabel, new Vector2(center.X - sl.X / 2, center.Y - 22), UITheme.TextDim, 12f);
        y = (int)center.Y + 14;

        if (!string.IsNullOrEmpty(civ.RulingParty))
            Row(sb, "Governing", civ.RulingParty, x0, ref y, leftW, UITheme.Gold);
        if (civ.NextElectionYear > 0)
            Row(sb, "Next election", $"year {civ.NextElectionYear} (in {Math.Max(0, civ.NextElectionYear - currentYear)})", x0, ref y, leftW);
        MeterRow(sb, "Legitimacy", d.Legitimacy, x0, ref y, leftW, Color.Lerp(UITheme.Bad, UITheme.Good, d.Legitimacy));
        int leftBottom = y;

        // Party support bars
        y = top;
        Section(sb, "PARTY SUPPORT", rightX, ref y, rightW);
        float maxSupport = Math.Max(0.01f, d.Parties.Max(p => p.Support));
        foreach (var p in d.Parties.OrderByDescending(p => p.Support))
        {
            Color ic = SocietyStyle.IdeologyColor(p.Ideology);
            bool ruling = p.Name == civ.RulingParty;
            string name = UITheme.Ellipsize(p.Name, rightW - 150, 13f);
            UITheme.DrawText(sb, name, new Vector2(rightX, y), ruling ? UITheme.Gold : UITheme.Text, 13f);
            var nsz = UITheme.Measure(name, 13f);
            Chip(sb, p.Ideology.ToString(), ic, (int)(rightX + nsz.X + 8), y + 1, out _, 10f);
            string val = $"{p.Support:P0}" + (p.SeatsWon > 0 ? $" - {p.SeatsWon} seats" : "");
            var vs = UITheme.Measure(val, 12f);
            UITheme.DrawText(sb, val, new Vector2(rightX + rightW - vs.X, y + 1), UITheme.TextDim, 12f);
            y += 19;
            var bar = new Rectangle(rightX, y, rightW, 9);
            sb.Draw(UITheme.Pixel, bar, new Color(0, 0, 0, 130));
            sb.Draw(UITheme.Pixel, new Rectangle(bar.X, bar.Y, (int)(bar.Width * Math.Clamp(p.Support / maxSupport, 0f, 1f)), bar.Height), ic);
            y += 17;
        }

        y += 6;
        Section(sb, "RECENT ELECTIONS", rightX, ref y, rightW);
        if (d.Elections.Count == 0)
        {
            UITheme.DrawText(sb, "No elections held yet", new Vector2(rightX, y), UITheme.TextMuted, 12f);
            y += 20;
        }
        else
        {
            UITheme.DrawText(sb, "Year", new Vector2(rightX, y), UITheme.TextMuted, 11f);
            UITheme.DrawText(sb, "Winner", new Vector2(rightX + 50, y), UITheme.TextMuted, 11f);
            UITheme.DrawText(sb, "Vote", new Vector2(rightX + rightW - 150, y), UITheme.TextMuted, 11f);
            UITheme.DrawText(sb, "Leader", new Vector2(rightX + rightW - 100, y), UITheme.TextMuted, 11f);
            y += 17;
            foreach (var e in d.Elections.AsEnumerable().Reverse().Take(8))
            {
                var party = d.Parties.FirstOrDefault(p => p.Name == e.WinningParty);
                Color pcol = party.Name != null ? SocietyStyle.IdeologyColor(party.Ideology) : UITheme.TextDim;
                UITheme.DrawText(sb, e.Year.ToString(), new Vector2(rightX, y), UITheme.TextDim, 12f);
                sb.Draw(UITheme.Pixel, new Rectangle(rightX + 50, y + 4, 8, 8), pcol);
                UITheme.DrawText(sb, UITheme.Ellipsize(e.WinningParty, rightW - 220, 12f), new Vector2(rightX + 62, y), UITheme.Text, 12f);
                UITheme.DrawText(sb, $"{e.WinningShare:P0}", new Vector2(rightX + rightW - 150, y), UITheme.Text, 12f);
                UITheme.DrawText(sb, UITheme.Ellipsize(e.LeaderName, 100, 12f), new Vector2(rightX + rightW - 100, y), UITheme.TextDim, 12f);
                y += 19;
            }
        }

        // Ideology key
        y += 8;
        int kx = rightX;
        foreach (var ideology in Enum.GetValues<Ideology>())
        {
            string label = ideology.ToString();
            int lw = (int)UITheme.Measure(label, 11f).X + 24;
            if (kx + lw > rightX + rightW) { kx = rightX; y += 16; }
            sb.Draw(UITheme.Pixel, new Rectangle(kx, y + 3, 9, 9), SocietyStyle.IdeologyColor(ideology));
            UITheme.DrawText(sb, label, new Vector2(kx + 13, y), UITheme.TextDim, 11f);
            kx += lw;
        }
        y += 22;

        return Math.Max(leftBottom, y);
    }

    /// <summary>Left-to-right political order for the hemicycle.</summary>
    private static int IdeologyOrder(Ideology i) => i switch
    {
        Ideology.Socialist => 0,
        Ideology.Green => 1,
        Ideology.Liberal => 2,
        Ideology.Technocratic => 3,
        Ideology.Conservative => 4,
        Ideology.Religious => 5,
        Ideology.Nationalist => 6,
        _ => 7
    };

    /// <summary>Seat positions of a hemicycle, ordered from the left end to the right end.</summary>
    private static List<Vector2> HemicycleSeats(int seats, Vector2 center, float innerR, float outerR)
    {
        int rows = seats <= 30 ? 3 : seats <= 70 ? 4 : 5;
        var radii = new float[rows];
        float totalLen = 0;
        for (int r = 0; r < rows; r++)
        {
            radii[r] = innerR + (outerR - innerR) * (rows == 1 ? 1 : r / (float)(rows - 1));
            totalLen += radii[r];
        }
        var perRow = new int[rows];
        int assigned = 0;
        for (int r = 0; r < rows; r++)
        {
            perRow[r] = (int)MathF.Round(seats * radii[r] / totalLen);
            assigned += perRow[r];
        }
        perRow[rows - 1] += seats - assigned;

        var all = new List<(float Angle, Vector2 Pos)>();
        for (int r = 0; r < rows; r++)
        {
            int n = Math.Max(1, perRow[r]);
            for (int i = 0; i < perRow[r]; i++)
            {
                float a = n == 1 ? MathF.PI / 2 : MathF.PI - i * MathF.PI / (n - 1);
                all.Add((a, center + new Vector2(MathF.Cos(a) * radii[r], -MathF.Sin(a) * radii[r])));
            }
        }
        all.Sort((p, q) => q.Angle.CompareTo(p.Angle));
        return all.Select(p => p.Pos).ToList();
    }

    // ------------------------------------------------------------------
    // Military
    // ------------------------------------------------------------------

    private int DrawMilitary(SpriteBatch sb, CivRenderData data, CivRenderData.CivInfo civ, int top)
    {
        var d = civ.Detail;
        int pad = 18;
        int x0 = _contentRect.X + pad;
        int width = _contentRect.Width - pad * 2;
        int y = top;
        var icons = MapIcons.GetShared(_graphicsDevice);

        // Headline figures
        var stats = new (string Label, string Value, Color Color)[]
        {
            ("Strength", civ.MilitaryStrength.ToString("N0"), UITheme.Text),
            ("Soldiers", FormatNumber(civ.Soldiers), UITheme.Text),
            ("Armies", civ.ArmyCount.ToString(), UITheme.Text),
            ("War dead", FormatNumber(d?.WarCasualties ?? 0), UITheme.Bad),
            ("Cities won / lost", $"{d?.CitiesConquered ?? 0} / {d?.CitiesLost ?? 0}", UITheme.Text),
        };
        int cardW = (width - (stats.Length - 1) * 8) / stats.Length;
        for (int i = 0; i < stats.Length; i++)
        {
            var r = new Rectangle(x0 + i * (cardW + 8), y, cardW, 46);
            UITheme.FillRounded(sb, r, new Color(24, 32, 48));
            UITheme.OutlineRounded(sb, r, UITheme.Border);
            UITheme.DrawText(sb, UITheme.Ellipsize(stats[i].Label, cardW - 12, 11f), new Vector2(r.X + 8, r.Y + 5), UITheme.TextMuted, 11f);
            UITheme.DrawText(sb, stats[i].Value, new Vector2(r.X + 8, r.Y + 21), stats[i].Color, UITheme.FontMedium);
        }
        y += 58;

        // Arsenal
        Section(sb, "STRATEGIC ARSENAL", x0, ref y, width);
        var arsenal = new (Texture2D Icon, string Label, string Value, bool Active)[]
        {
            (icons.Trefoil, "Nuclear warheads", civ.NuclearWarheads.ToString("N0"), civ.HasNuclearWeapons),
            (icons.Silo, "Missile silos", civ.MissileSilos.ToString(), civ.MissileSilos > 0),
            (icons.Flask, "Chemical stockpile", civ.ChemicalStockpile.ToString("N0"), civ.ChemicalStockpile > 0),
            (icons.Biohazard, "Bioweapons", civ.BioweaponProgram ? "Programme" : "None", civ.BioweaponProgram),
            (icons.Shield, "Missile defence", civ.MissileDefense ? "Active" : "None", civ.MissileDefense),
            (icons.Satellite, "Spy satellites", civ.SpySatellites.ToString(), civ.SpySatellites > 0),
        };
        int perRow = width >= 700 ? 3 : 2;
        int aw = (width - (perRow - 1) * 8) / perRow;
        for (int i = 0; i < arsenal.Length; i++)
        {
            var r = new Rectangle(x0 + (i % perRow) * (aw + 8), y + (i / perRow) * 50, aw, 44);
            var a = arsenal[i];
            UITheme.FillRounded(sb, r, a.Active ? new Color(44, 36, 30) : new Color(22, 28, 40));
            UITheme.OutlineRounded(sb, r, a.Active ? new Color(200, 140, 90) : UITheme.Border);
            sb.Draw(a.Icon, new Rectangle(r.X + 8, r.Y + 8, 28, 28), Color.White * (a.Active ? 1f : 0.35f));
            UITheme.DrawText(sb, a.Label, new Vector2(r.X + 44, r.Y + 5), UITheme.TextDim, 11f);
            UITheme.DrawText(sb, a.Value, new Vector2(r.X + 44, r.Y + 20), a.Active ? UITheme.Text : UITheme.TextMuted, 14f);
        }
        y += ((arsenal.Length + perRow - 1) / perRow) * 50 + 2;
        if (d != null && (d.NuclearStrikesLaunched > 0 || d.ChemicalAttacks > 0 || d.BioweaponReleases > 0))
        {
            UITheme.DrawText(sb, $"Used: {d.NuclearStrikesLaunched} nuclear strikes, {d.ChemicalAttacks} chemical attacks, {d.BioweaponReleases} bioweapon releases",
                new Vector2(x0, y), UITheme.Bad, 12f);
            y += 20;
        }

        int colGap = 26;
        int colW = (width - colGap) / 2;
        int rx = x0 + colW + colGap;
        int sectionTop = y + 6;

        // Armies
        y = sectionTop;
        Section(sb, "ARMIES", x0, ref y, colW);
        var armies = data.Armies.Where(a => a.CivId == civ.Id).OrderByDescending(a => a.Soldiers).ToList();
        if (armies.Count == 0)
        {
            UITheme.DrawText(sb, "No armies in the field", new Vector2(x0, y), UITheme.TextMuted, 12f);
            y += 20;
        }
        foreach (var a in armies.Take(10))
        {
            string state = a.IsGarrison ? "Garrison" : a.State;
            string target = a.TargetCivId > 0 && data.FindCiv(a.TargetCivId) is { } t && !a.IsGarrison ? $" - {t.Name}" : "";
            UITheme.DrawText(sb, UITheme.Ellipsize($"{a.Soldiers:N0} soldiers", colW - 150, 13f), new Vector2(x0, y), UITheme.Text, 13f);
            string st = UITheme.Ellipsize(state + target, 150, 11f);
            var ss = UITheme.Measure(st, 11f);
            Color sc = state.StartsWith("Besieg") || state.StartsWith("March") ? UITheme.Warn : state.StartsWith("Retreat") ? UITheme.Bad : UITheme.TextDim;
            UITheme.DrawText(sb, st, new Vector2(x0 + colW - ss.X, y + 2), sc, 11f);
            y += 18;
            var bar = new Rectangle(x0, y, colW, 3);
            sb.Draw(UITheme.Pixel, bar, new Color(0, 0, 0, 120));
            sb.Draw(UITheme.Pixel, new Rectangle(bar.X, bar.Y, (int)(bar.Width * Math.Clamp(a.Morale / 1.2f, 0f, 1f)), 3), Color.Lerp(UITheme.Bad, UITheme.Good, Math.Clamp(a.Morale, 0f, 1f)));
            y += 8;
        }
        if (armies.Count > 10)
        {
            UITheme.DrawText(sb, $"+ {armies.Count - 10} more", new Vector2(x0, y), UITheme.TextMuted, 11f);
            y += 16;
        }
        int leftBottom = y;

        // Wars and relations
        y = sectionTop;
        Section(sb, "WARS & RELATIONS", rx, ref y, colW);
        var relations = (d?.Relations ?? new List<CivRenderData.RelationInfo>())
            .Where(r => data.FindCiv(r.OtherCivId) != null)
            .OrderBy(r => r.Status).ThenBy(r => r.Opinion).ToList();
        if (relations.Count == 0)
        {
            UITheme.DrawText(sb, "No contact with other nations", new Vector2(rx, y), UITheme.TextMuted, 12f);
            y += 20;
        }
        foreach (var rel in relations.Take(12))
        {
            var other = data.FindCiv(rel.OtherCivId)!.Value;
            sb.Draw(UITheme.Pixel, new Rectangle(rx, y + 4, 9, 9), TerrainRenderer.GetCivPaletteColor(other.Id));
            UITheme.DrawText(sb, UITheme.Ellipsize(other.Name, colW - 140, 13f), new Vector2(rx + 14, y), UITheme.Text, 13f);
            Color rc = SocietyStyle.RelationColor(rel.Status);
            string status = rel.Status.ToString().ToUpperInvariant();
            var stw = UITheme.Measure(status, 10f).X + 12;
            Chip(sb, status, rc, (int)(rx + colW - stw - 40), y + 1, out _, 10f);
            string op = rel.Opinion >= 0 ? $"+{rel.Opinion:0}" : $"{rel.Opinion:0}";
            var os = UITheme.Measure(op, 11f);
            UITheme.DrawText(sb, op, new Vector2(rx + colW - os.X, y + 2), rel.Opinion >= 0 ? UITheme.Good : UITheme.Bad, 11f);
            y += 21;
        }

        // Recent battles
        var battles = data.Battles.Where(b => b.AttackerCivId == civ.Id || b.DefenderCivId == civ.Id).OrderByDescending(b => b.Year).Take(6).ToList();
        if (battles.Count > 0)
        {
            y += 8;
            Section(sb, "RECENT BATTLES", rx, ref y, colW);
            foreach (var b in battles)
            {
                bool attacking = b.AttackerCivId == civ.Id;
                var foe = data.FindCiv(attacking ? b.DefenderCivId : b.AttackerCivId);
                string text = $"{b.Year}: {(attacking ? "attacked" : "defended against")} {foe?.Name ?? "a fallen nation"}";
                UITheme.DrawText(sb, UITheme.Ellipsize(text, colW - 70, 12f), new Vector2(rx, y), UITheme.TextDim, 12f);
                string cas = FormatNumber(b.Casualties);
                var cs = UITheme.Measure(cas, 11f);
                UITheme.DrawText(sb, cas, new Vector2(rx + colW - cs.X, y + 1), UITheme.Bad, 11f);
                y += 19;
            }
        }

        return Math.Max(leftBottom, y);
    }

    private static Texture2D BuildDisc(GraphicsDevice device, int size)
    {
        var data = new Color[size * size];
        float r = size / 2f;
        for (int yy = 0; yy < size; yy++)
        {
            for (int xx = 0; xx < size; xx++)
            {
                float dx = xx + 0.5f - r, dy = yy + 0.5f - r;
                data[yy * size + xx] = Color.White * Math.Clamp(r - MathF.Sqrt(dx * dx + dy * dy), 0f, 1f);
            }
        }
        var tex = new Texture2D(device, size, size);
        tex.SetData(data);
        return tex;
    }
}
