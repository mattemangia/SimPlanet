using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace SimPlanet;

/// <summary>
/// Society views of the map: power grid, energy, armaments, governments, internet,
/// infrastructure and epidemics, plus space activity and the nuclear winter haze.
///
/// Territories are coloured per nation in the terrain texture (see <see cref="GetSocietyColor"/>);
/// networks, glows, badges and legends are drawn as vector overlays from the
/// <see cref="CivRenderData"/> snapshot.
/// </summary>
public partial class TerrainRenderer
{
    private DiseaseManager? _diseaseManager;
    private readonly byte[] _cellRoadType;                               // RoadType per cell (land only)
    private readonly Dictionary<int, Color> _nationTint = new();          // Society view colour per nation
    private float _nationTintStrength = 0.75f;
    private readonly List<(int X, int Y, EnergySource Type)> _plantSites = new();
    private readonly Dictionary<int, Vector2> _nationAnchor = new();          // Badge anchor (cell coordinates)
    private readonly List<(string Text, Vector2 Pos, Color Color, float Size)> _pendingTexts = new();
    private Texture2D? _hazeTexture;
    private Texture2D? _discTexture;

    public void SetDiseaseManager(DiseaseManager manager)
    {
        _diseaseManager = manager;
    }

    private (string Text, Point Anchor)? _deferredTooltip;

    private void QueueTooltip(string text, Point anchor) => _deferredTooltip = (text, anchor);

    /// <summary>
    /// Draws the settlement / nation tooltip of this frame. Called after the map overlays
    /// (volcanoes, rivers...) so they don't cover it.
    /// </summary>
    public void DrawDeferredTooltip(SpriteBatch spriteBatch)
    {
        if (_deferredTooltip == null) return;
        var vp = _graphicsDevice.Viewport;
        UITheme.DrawTooltip(spriteBatch, _deferredTooltip.Value.Text, _deferredTooltip.Value.Anchor, vp.Width, vp.Height);
        _deferredTooltip = null;
    }

    public static bool IsSocietyMode(RenderMode mode) => mode is RenderMode.Electricity or RenderMode.Infrastructure
        or RenderMode.Energy or RenderMode.Armaments or RenderMode.Governments or RenderMode.Internet or RenderMode.Epidemics
        or RenderMode.Migrations or RenderMode.SpyNetworks;

    private static readonly Color SocietyUnclaimed = new Color(96, 100, 92);
    private static readonly Color HealthyTint = new Color(96, 132, 112);
    private static readonly Color NoDataTint = new Color(150, 156, 172);

    // ------------------------------------------------------------------
    // Territory colours (terrain texture)
    // ------------------------------------------------------------------

    /// <summary>Computes the colour of each nation for the current society view.</summary>
    private void PrepareSocietyView()
    {
        _nationTint.Clear();
        _plantSites.Clear();
        _nationAnchor.Clear();
        if (!IsSocietyMode(Mode)) return;

        // Anchor for each nation's badge: under the middle of its territory
        var sums = new Dictionary<int, (double sx, int n, int maxY)>();
        int w = _map.Width;
        for (int i = 0; i < _civOwnerMap.Length; i++)
        {
            int owner = _civOwnerMap[i];
            if (owner <= 0) continue;
            var s = sums.GetValueOrDefault(owner);
            int x = i % w, y = i / w;
            sums[owner] = (s.sx + x, s.n + 1, Math.Max(s.maxY, y));
        }
        foreach (var (id, s) in sums)
        {
            float cx = (float)(s.sx / s.n);
            // Territories wrapping around the map seam would average to the middle: fall back to the centre
            var civ = CivData.FindCiv(id);
            if (civ != null && Math.Abs(cx - civ.Value.CenterX) > w / 4f) cx = civ.Value.CenterX;
            _nationAnchor[id] = new Vector2(cx, s.maxY + 1);
        }

        var data = CivData;
        int maxStrength = 1;
        foreach (var c in data.Civs) maxStrength = Math.Max(maxStrength, c.MilitaryStrength);

        _nationTintStrength = Mode switch
        {
            RenderMode.Infrastructure => 0.22f,
            RenderMode.Migrations or RenderMode.SpyNetworks => 0.6f,
            _ => 0.74f
        };
        var net = Mode == RenderMode.Migrations ? NetMigration(data) : null;
        int maxNet = net == null || net.Count == 0 ? 0 : net.Values.Max(v => Math.Abs(v.In - v.Out));

        foreach (var civ in data.Civs)
        {
            Color tint = Mode switch
            {
                RenderMode.Electricity => SocietyStyle.ElectrificationColor(civ.Electrification),
                RenderMode.Energy => civ.DominantEnergy.HasValue ? SocietyStyle.EnergyColor(civ.DominantEnergy.Value) : NoDataTint,
                RenderMode.Armaments => SocietyStyle.MilitaryColor(MathF.Sqrt(civ.MilitaryStrength / (float)maxStrength)),
                RenderMode.Governments => civ.GovType.HasValue ? SocietyStyle.GovernmentColor(civ.GovType.Value) : NoDataTint,
                RenderMode.Internet => SocietyStyle.InternetColor(civ.InternetPenetration),
                RenderMode.Epidemics => GetEpidemicTint(data, civ.Id),
                RenderMode.Migrations => GetMigrationTint(data, net!, maxNet, civ.Id),
                RenderMode.SpyNetworks => SocietyStyle.CounterIntelColor(civ.CounterIntelligence),
                _ => GetCivColor(civ.Id)
            };
            _nationTint[civ.Id] = tint;
        }
    }

    private static (CivRenderData.InfectionInfo? Worst, float Total) GetWorstInfection(CivRenderData data, int civId)
    {
        CivRenderData.InfectionInfo? worst = null;
        float total = 0f;
        foreach (var inf in data.Infections)
        {
            if (inf.CivId != civId) continue;
            total += inf.Share;
            if (worst == null || inf.Share > worst.Value.Share) worst = inf;
        }
        return (worst, Math.Min(1f, total));
    }

    private static Color GetEpidemicTint(CivRenderData data, int civId)
    {
        var (worst, total) = GetWorstInfection(data, civId);
        if (worst == null) return HealthyTint;
        float t = MathF.Pow(Math.Clamp(total / 0.3f, 0f, 1f), 0.6f);
        return Color.Lerp(HealthyTint, SocietyStyle.DiseaseColor(worst.Value.DiseaseId), 0.25f + 0.75f * t);
    }

    private Color GetSocietyColor(TerrainCell cell, int x, int y)
    {
        var geo = cell.GetGeology();
        bool dryBasin = cell.IsWater && (geo.IsDryBasin || (!geo.IsConnectedToOcean && geo.AccumulatedWater < 0.1f));
        if (cell.IsWater && !dryBasin)
        {
            float depth = Math.Clamp(-cell.Elevation * 1.5f, 0f, 1f);
            return Color.Lerp(new Color(34, 52, 80), new Color(14, 22, 40), depth);
        }

        float e = Math.Clamp(cell.Elevation, 0f, 1f);
        Color c = cell.IsIce ? new Color(178, 186, 198) : Color.Lerp(new Color(86, 92, 80), new Color(140, 134, 120), Math.Min(1f, e * 1.4f));

        int owner = _civOwnerMap[y * _map.Width + x];
        if (owner > 0 && _nationTint.TryGetValue(owner, out var tint))
        {
            c = Color.Lerp(c, tint, _nationTintStrength);
        }

        switch (Mode)
        {
            case RenderMode.Armaments:
                if (geo.RadioactiveContamination > 0.01f)
                    c = Color.Lerp(c, SocietyStyle.Radiation, 0.35f + 0.55f * Math.Min(1f, geo.RadioactiveContamination));
                break;
            case RenderMode.Electricity:
                if (geo.IsEMPAffected) c = Color.Lerp(c, new Color(220, 40, 40), 0.7f);
                goto case RenderMode.Energy;
            case RenderMode.Energy:
                if (geo.HasNuclearPlant) _plantSites.Add((x, y, EnergySource.Nuclear));
                else if (geo.HasSolarFarm) _plantSites.Add((x, y, EnergySource.Solar));
                else if (geo.HasWindTurbine) _plantSites.Add((x, y, EnergySource.Wind));
                break;
        }
        return c;
    }

    // ------------------------------------------------------------------
    // Geometry helpers
    // ------------------------------------------------------------------

    /// <summary>Nation owning the land cell under a screen position (0 for none or water).</summary>
    public int GetOwnerAtScreen(Point screen, int offsetX, int offsetY)
    {
        float cs = CellSize * ZoomLevel;
        if (cs <= 0) return 0;
        int cx = (int)MathF.Floor((screen.X - offsetX + CameraX) / cs);
        int cy = (int)MathF.Floor((screen.Y - offsetY + CameraY) / cs);
        if (cx < 0 || cy < 0 || cx >= _map.Width || cy >= _map.Height) return 0;
        int idx = cy * _map.Width + cx;
        if (_cellField[idx] < 0) return 0;
        return _civOwnerMap[idx];
    }

    private bool IsWaterCell(float x, float y)
    {
        int cx = (((int)MathF.Round(x)) % _map.Width + _map.Width) % _map.Width;
        int cy = Math.Clamp((int)MathF.Round(y), 0, _map.Height - 1);
        return _cellField[cy * _map.Width + cx] < 0;
    }

    /// <summary>
    /// Calls <paramref name="draw"/> with the screen end points of a link, taking the shortest way
    /// around the horizontally wrapping map (a link crossing the seam is drawn at both edges).
    /// </summary>
    private void ForEachLinkSegment(int x1, int y1, int x2, int y2, int offsetX, int offsetY, Rectangle clip, Action<Vector2, Vector2> draw)
    {
        int w = _map.Width;
        float dx = x2 - x1;
        if (dx > w / 2f) dx -= w;
        else if (dx < -w / 2f) dx += w;

        void Emit(Vector2 a, Vector2 b)
        {
            var box = new Rectangle((int)Math.Min(a.X, b.X) - 20, (int)Math.Min(a.Y, b.Y) - 20,
                (int)Math.Abs(a.X - b.X) + 40, (int)Math.Abs(a.Y - b.Y) + 40);
            if (box.Intersects(clip)) draw(a, b);
        }

        Emit(CellToScreen(x1, y1, offsetX, offsetY), CellToScreen(x1 + dx, y2, offsetX, offsetY));
        if (x1 + dx < 0 || x1 + dx >= w)
            Emit(CellToScreen(x2 - dx, y1, offsetX, offsetY), CellToScreen(x2, y2, offsetX, offsetY));
    }

    private static Vector2 Bezier(Vector2 a, Vector2 c, Vector2 b, float t)
    {
        float u = 1 - t;
        return u * u * a + 2 * u * t * c + t * t * b;
    }

    private static Vector2 ArcControl(Vector2 a, Vector2 b, float bulge)
    {
        Vector2 d = b - a;
        Vector2 n = new Vector2(-d.Y, d.X);
        if (n.Y < 0) n = -n; // always bulge towards the bottom of the screen
        return (a + b) / 2f + n * bulge;
    }

    private static void DrawArc(SpriteBatch sb, Vector2 a, Vector2 c, Vector2 b, Color color, float thickness, int segments = 18)
    {
        Vector2 prev = a;
        for (int i = 1; i <= segments; i++)
        {
            var p = Bezier(a, c, b, i / (float)segments);
            UITheme.DrawLine(sb, prev, p, color, thickness);
            prev = p;
        }
    }

    private static void DrawDashedArc(SpriteBatch sb, Vector2 a, Vector2 c, Vector2 b, Color color, float thickness, float time)
    {
        const int segments = 30;
        int phase = (int)(time * 4f) % 2;
        Vector2 prev = a;
        for (int i = 1; i <= segments; i++)
        {
            var p = Bezier(a, c, b, i / (float)segments);
            if ((i + phase) % 2 == 0)
            {
                UITheme.DrawLine(sb, prev, p, Color.Black * 0.45f, thickness + 1.5f);
                UITheme.DrawLine(sb, prev, p, color, thickness);
            }
            prev = p;
        }
    }

    private static int SettlementSizePx(int type, float iconScale) => (int)(SettlementPixelSize[Math.Clamp(type, 0, 3)] * iconScale);

    private void DrawIcon(SpriteBatch sb, Texture2D? tex, Vector2 center, float size, float alpha = 1f)
    {
        if (tex == null) return;
        int s = Math.Max(6, (int)size);
        sb.Draw(tex, new Rectangle((int)(center.X - s / 2f), (int)(center.Y - s / 2f), s, s), Color.White * alpha);
    }

    // ------------------------------------------------------------------
    // Overlays under the settlements: networks and glows
    // ------------------------------------------------------------------

    private void DrawSocietyUnderlay(SpriteBatch sb, CivRenderData data, int offsetX, int offsetY, Rectangle clip, float iconScale, float time)
    {
        switch (Mode)
        {
            case RenderMode.Electricity:
                DrawPowerLines(sb, data, offsetX, offsetY, clip, time);
                DrawCityGlows(sb, data, offsetX, offsetY, clip, iconScale, time);
                break;
            case RenderMode.Internet:
                DrawDataCables(sb, data, offsetX, offsetY, clip, time);
                DrawCityGlows(sb, data, offsetX, offsetY, clip, iconScale, time);
                break;
            case RenderMode.Infrastructure:
                DrawTradeRoutes(sb, data, offsetX, offsetY, clip, time);
                // Railway routes follow the terrain and are drawn with the other transport routes
                if (!data.Routes.Any(r => r.Kind == TransportKind.Railway))
                    DrawRailroads(sb, data, offsetX, offsetY, clip);
                break;
            case RenderMode.Epidemics:
                DrawCityGlows(sb, data, offsetX, offsetY, clip, iconScale, time);
                break;
        }
    }

    private void DrawPowerLines(SpriteBatch sb, CivRenderData data, int offsetX, int offsetY, Rectangle clip, float time)
    {
        var col = SocietyStyle.PowerLine;
        foreach (var l in data.PowerLines)
        {
            ForEachLinkSegment(l.X1, l.Y1, l.X2, l.Y2, offsetX, offsetY, clip, (a, b) =>
            {
                UITheme.DrawLine(sb, a, b, Color.Black * 0.5f, 3.5f);
                UITheme.DrawLine(sb, a, b, col * 0.25f, 6f);
                UITheme.DrawLine(sb, a, b, col, 1.6f);
                float len = Vector2.Distance(a, b);
                if (len < 4) return;
                var dir = (b - a) / len;
                // Pylons
                for (float t = 8; t < len - 4; t += 16)
                    sb.Draw(_pixelTexture, new Rectangle((int)(a.X + dir.X * t) - 1, (int)(a.Y + dir.Y * t) - 1, 3, 3), new Color(60, 50, 30));
                // Current flowing along the line
                float phase = (time * 40f + (l.X1 * 7 + l.Y1 * 13)) % Math.Max(1f, len);
                UITheme.DrawGlow(sb, a + dir * phase, 5f, new Color(255, 250, 200, 0) * 0.9f);
            });
        }
    }

    private void DrawDataCables(SpriteBatch sb, CivRenderData data, int offsetX, int offsetY, Rectangle clip, float time)
    {
        var land = SocietyStyle.DataCable;
        var sea = new Color(70, 160, 255);
        foreach (var l in data.DataCables)
        {
            // Undersea if any sample along the link falls in the sea
            float dxw = l.X2 - l.X1;
            if (dxw > _map.Width / 2f) dxw -= _map.Width; else if (dxw < -_map.Width / 2f) dxw += _map.Width;
            bool undersea = false;
            for (int k = 1; k < 4 && !undersea; k++)
                undersea = IsWaterCell(l.X1 + dxw * k / 4f, l.Y1 + (l.Y2 - l.Y1) * k / 4f);

            ForEachLinkSegment(l.X1, l.Y1, l.X2, l.Y2, offsetX, offsetY, clip, (a, b) =>
            {
                float phase = ((time * 0.35f + (l.X1 * 0.13f + l.Y2 * 0.07f)) % 1f);
                if (undersea)
                {
                    var c = ArcControl(a, b, 0.22f);
                    DrawArc(sb, a, c, b, sea * 0.22f, 5f);
                    DrawArc(sb, a, c, b, sea, 1.4f);
                    UITheme.DrawGlow(sb, Bezier(a, c, b, phase), 4.5f, new Color(200, 240, 255, 0));
                }
                else
                {
                    UITheme.DrawLine(sb, a, b, Color.Black * 0.45f, 3f);
                    UITheme.DrawLine(sb, a, b, land * 0.25f, 5f);
                    UITheme.DrawLine(sb, a, b, land, 1.4f);
                    UITheme.DrawGlow(sb, Vector2.Lerp(a, b, phase), 4.5f, new Color(220, 255, 255, 0));
                }
            });
        }
    }

    private void DrawRailroads(SpriteBatch sb, CivRenderData data, int offsetX, int offsetY, Rectangle clip)
    {
        foreach (var l in data.Railroads)
        {
            ForEachLinkSegment(l.X1, l.Y1, l.X2, l.Y2, offsetX, offsetY, clip, (a, b) =>
            {
                float len = Vector2.Distance(a, b);
                if (len < 2) return;
                var dir = (b - a) / len;
                UITheme.DrawLine(sb, a, b, new Color(30, 24, 22), 4f);
                // Classic map railway: dark track with light dashes
                for (float t = 0; t < len; t += 10)
                {
                    float e = Math.Min(len, t + 5);
                    UITheme.DrawLine(sb, a + dir * t, a + dir * e, new Color(236, 228, 210), 1.6f);
                }
            });
        }
    }

    private void DrawTradeRoutes(SpriteBatch sb, CivRenderData data, int offsetX, int offsetY, Rectangle clip, float time)
    {
        // Routes are stored per nation as the partner's centre; draw each pair once, capital to capital
        var byCentre = new Dictionary<(int, int), CivRenderData.CivInfo>();
        foreach (var c in data.Civs) byCentre[(c.CenterX, c.CenterY)] = c;
        var drawn = new HashSet<(int, int)>();
        foreach (var route in data.TradeRoutes)
        {
            var from = data.FindCiv(route.CivId);
            if (from == null || !byCentre.TryGetValue((route.X2, route.Y2), out var to)) continue;
            var key = (Math.Min(from.Value.Id, to.Id), Math.Max(from.Value.Id, to.Id));
            if (!drawn.Add(key)) continue;

            ForEachLinkSegment(from.Value.CapitalX, from.Value.CapitalY, to.CapitalX, to.CapitalY, offsetX, offsetY, clip, (a, b) =>
            {
                var c = ArcControl(a, b, -0.18f);
                DrawDashedArc(sb, a, c, b, SocietyStyle.TradeRoute, 1.8f, time);
                float phase = (time * 0.12f + key.Item1 * 0.31f) % 1f;
                UITheme.DrawGlow(sb, Bezier(a, c, b, phase), 5f, new Color(255, 230, 150, 0));
            });
        }
    }

    private void DrawCityGlows(SpriteBatch sb, CivRenderData data, int offsetX, int offsetY, Rectangle clip, float iconScale, float time)
    {
        var electrification = new Dictionary<int, float>();
        foreach (var c in data.Civs) electrification[c.Id] = c.Electrification;
        var infection = new Dictionary<int, (CivRenderData.InfectionInfo? Worst, float Total)>();
        if (Mode == RenderMode.Epidemics)
            foreach (var c in data.Civs) infection[c.Id] = GetWorstInfection(data, c.Id);

        foreach (var city in data.Cities)
        {
            var pos = CellToScreen(city.X, city.Y, offsetX, offsetY);
            if (!clip.Contains(pos.ToPoint())) continue;
            float size = SettlementSizePx(city.Type, iconScale);
            var center = pos + new Vector2(0, -size * 0.1f);
            float pulse = 0.5f + 0.5f * MathF.Sin(time * 3f + city.X * 0.7f + city.Y);

            switch (Mode)
            {
                case RenderMode.Electricity:
                    if (city.Electrified)
                        UITheme.DrawGlow(sb, center, size * (0.9f + city.Type * 0.15f), new Color(255, 214, 110, 0) * 0.75f);
                    else if (electrification.GetValueOrDefault(city.CivId) > 0.001f)
                        UITheme.DrawGlow(sb, center, size * 0.85f, SocietyStyle.Blackout * (0.35f + 0.25f * pulse));
                    break;
                case RenderMode.Internet:
                    if (city.Online)
                    {
                        UITheme.DrawGlow(sb, center, size * (0.9f + 0.25f * pulse), new Color(90, 230, 255, 0) * 0.8f);
                        UITheme.DrawGlow(sb, center, size * 0.4f, new Color(220, 255, 255, 0) * 0.6f);
                    }
                    break;
                case RenderMode.Epidemics:
                    var (worst, total) = infection.GetValueOrDefault(city.CivId);
                    if (worst != null && total > 0.001f)
                    {
                        float strength = Math.Clamp(total / 0.3f, 0.15f, 1f);
                        UITheme.DrawGlow(sb, center, size * (0.8f + strength * (0.6f + 0.4f * pulse)),
                            SocietyStyle.DiseaseColor(worst.Value.DiseaseId) * (0.35f + 0.4f * strength));
                    }
                    break;
            }
        }
    }

    // ------------------------------------------------------------------
    // Markers above the settlements: plants, transport hubs, weapons
    // ------------------------------------------------------------------

    private void DrawSocietyMarkers(SpriteBatch sb, CivRenderData data, List<CivRenderData.CityInfo> ordered, bool[] fullIcon,
        int offsetX, int offsetY, Rectangle clip, float iconScale, float time)
    {
        _pendingTexts.Clear();
        if (_icons == null) return;
        float small = 15f * iconScale;

        switch (Mode)
        {
            case RenderMode.Electricity:
            case RenderMode.Energy:
            {
                // Plants built on the land (geology), then the settlements' own power stations
                int drawnSites = 0;
                foreach (var (x, y, type) in _plantSites)
                {
                    var p = CellToScreen(x, y, offsetX, offsetY);
                    if (!clip.Contains(p.ToPoint())) continue;
                    DrawIcon(sb, _icons.PowerPlantIcon(type), p, small * 0.9f);
                    if (++drawnSites > 400) break;
                }
                var electrification = new Dictionary<int, float>();
                foreach (var c in data.Civs) electrification[c.Id] = c.Electrification;
                for (int i = 0; i < ordered.Count; i++)
                {
                    var city = ordered[i];
                    var pos = CellToScreen(city.X, city.Y, offsetX, offsetY);
                    if (!clip.Contains(pos.ToPoint())) continue;
                    float size = SettlementSizePx(city.Type, iconScale);
                    if (city.HasPowerPlant)
                    {
                        var type = city.PowerPlantType ?? EnergySource.Coal;
                        var at = pos + new Vector2(size * 0.55f, size * 0.05f);
                        UITheme.DrawGlow(sb, at, small * 0.8f, Color.Black * 0.35f);
                        DrawIcon(sb, _icons.PowerPlantIcon(type), at, small);
                    }
                    else if (Mode == RenderMode.Electricity && !city.Electrified && fullIcon[i] &&
                             electrification.GetValueOrDefault(city.CivId) > 0.001f)
                    {
                        // Blackout: grey bolt with a red slash
                        var at = pos + new Vector2(size * 0.5f, -size * 0.45f);
                        DrawIcon(sb, _icons.Bolt, at, small * 0.8f, 0.55f);
                        UITheme.DrawLine(sb, at + new Vector2(-small * 0.35f, small * 0.35f), at + new Vector2(small * 0.35f, -small * 0.35f), SocietyStyle.Blackout, 2f);
                    }
                }
                break;
            }

            case RenderMode.Infrastructure:
            {
                for (int i = 0; i < ordered.Count; i++)
                {
                    var city = ordered[i];
                    if (!fullIcon[i]) continue;
                    var pos = CellToScreen(city.X, city.Y, offsetX, offsetY);
                    if (!clip.Contains(pos.ToPoint())) continue;
                    var hubs = new List<Texture2D>();
                    if (city.HasHarbor) hubs.Add(_icons.Anchor);
                    if (city.HasAirport) hubs.Add(_icons.Plane);
                    if (city.HasSpaceport) hubs.Add(_icons.Rocket);
                    if (hubs.Count == 0) continue;
                    float size = SettlementSizePx(city.Type, iconScale);
                    float s = small * 0.85f;
                    float x0 = pos.X + size * 0.5f + s * 0.3f;
                    float y0 = pos.Y - size * 0.55f;
                    for (int k = 0; k < hubs.Count; k++)
                    {
                        var at = new Vector2(x0 + k * (s + 1), y0);
                        UITheme.DrawGlow(sb, at, s * 0.75f, new Color(10, 14, 24) * 0.7f);
                        DrawIcon(sb, hubs[k], at, s);
                    }
                }
                break;
            }

            case RenderMode.Armaments:
            {
                foreach (var (civId, x, y) in data.SiloSites)
                {
                    var p = CellToScreen(x, y, offsetX, offsetY);
                    if (!clip.Contains(p.ToPoint())) continue;
                    UITheme.DrawGlow(sb, p + new Vector2(0, small * 0.3f), small * 0.6f, GetCivColor(civId) * 0.6f);
                    DrawIcon(sb, _icons.Silo, p, small * 0.95f);
                }

                foreach (var civ in data.Civs)
                {
                    var icons = new List<(Texture2D Tex, string? Count)>();
                    if (civ.HasNuclearWeapons) icons.Add((_icons.Trefoil, civ.NuclearWarheads > 0 ? civ.NuclearWarheads.ToString("N0") : null));
                    if (civ.ChemicalStockpile > 0) icons.Add((_icons.Flask, null));
                    if (civ.BioweaponProgram) icons.Add((_icons.Biohazard, null));
                    if (civ.MissileDefense) icons.Add((_icons.Shield, null));
                    if (icons.Count == 0) continue;

                    var capital = FindCapital(data, civ);
                    var pos = CellToScreen(capital.X, capital.Y, offsetX, offsetY);
                    if (!clip.Contains(pos.ToPoint())) continue;
                    float size = SettlementSizePx(capital.Type, iconScale);
                    float s = small * 1.05f;
                    float x = pos.X + size * 0.55f + s * 0.4f;
                    float y = pos.Y - size * 0.62f;
                    for (int k = 0; k < icons.Count; k++)
                    {
                        var at = new Vector2(x, y + k * (s + 2));
                        float pulse = k == 0 && civ.HasNuclearWeapons ? 0.5f + 0.5f * MathF.Sin(time * 2.5f) : 0f;
                        if (pulse > 0) UITheme.DrawGlow(sb, at, s * (0.9f + 0.3f * pulse), new Color(255, 214, 40, 0) * 0.45f);
                        DrawIcon(sb, icons[k].Tex, at, s);
                        if (icons[k].Count != null)
                            _pendingTexts.Add(("x" + icons[k].Count, new Vector2(at.X + s * 0.6f, at.Y - 7), new Color(255, 226, 120), 12f));
                    }
                }
                break;
            }

            case RenderMode.Epidemics:
            {
                foreach (var civ in data.Civs)
                {
                    var (worst, total) = GetWorstInfection(data, civ.Id);
                    if (worst == null || total < 0.001f) continue;
                    var capital = FindCapital(data, civ);
                    var pos = CellToScreen(capital.X, capital.Y, offsetX, offsetY);
                    if (!clip.Contains(pos.ToPoint())) continue;
                    float size = SettlementSizePx(capital.Type, iconScale);
                    var at = pos + new Vector2(size * 0.6f + small * 0.3f, -size * 0.55f);
                    DrawIcon(sb, _icons.Biohazard, at, small);
                    _pendingTexts.Add(($"{total:P0}", new Vector2(at.X + small * 0.6f, at.Y - 7), SocietyStyle.DiseaseColor(worst.Value.DiseaseId), 12f));
                }
                break;
            }
        }
    }

    private static CivRenderData.CityInfo FindCapital(CivRenderData data, CivRenderData.CivInfo civ)
    {
        CivRenderData.CityInfo? any = null;
        foreach (var c in data.Cities)
        {
            if (c.CivId != civ.Id) continue;
            if (c.IsCapital) return c;
            any ??= c;
        }
        return any ?? new CivRenderData.CityInfo { X = civ.CapitalX, Y = civ.CapitalY, Type = 1, CivId = civ.Id, Name = civ.Name };
    }

    // ------------------------------------------------------------------
    // Nation badges (drawn with point sampling for crisp text)
    // ------------------------------------------------------------------

    private string? GetNationBadgeValue(CivRenderData data, CivRenderData.CivInfo civ, out Color color)
    {
        color = _nationTint.TryGetValue(civ.Id, out var t) ? t : UITheme.TextDim;
        switch (Mode)
        {
            case RenderMode.Electricity:
                if (civ.Electrification <= 0.001f) { color = UITheme.TextMuted; return "No grid"; }
                return $"{civ.Electrification:P0} electrified" + (civ.EnergyDemand > civ.EnergyProduction * 1.02f ? " - deficit" : "");
            case RenderMode.Energy:
                if (civ.EnergyMix == null || civ.EnergyMix.Length == 0) { color = UITheme.TextMuted; return "Pre-industrial"; }
                return $"{SocietyStyle.EnergyName(civ.EnergyMix[0].Source)} {civ.EnergyMix[0].Share:P0}";
            case RenderMode.Armaments:
                color = civ.HasNuclearWeapons ? new Color(255, 214, 40) : new Color(230, 170, 120);
                return $"Strength {civ.MilitaryStrength:N0}" + (civ.HasNuclearWeapons ? " - nuclear" : "");
            case RenderMode.Governments:
                return civ.GovType?.ToString() ?? "No government";
            case RenderMode.Internet:
                if (civ.InternetPenetration <= 0.001f) { color = UITheme.TextMuted; return "Offline"; }
                return $"{civ.InternetPenetration:P0} online";
            case RenderMode.Migrations:
            {
                var (i, o) = NetMigration(data).GetValueOrDefault(civ.Id);
                if (i == 0 && o == 0) return null;
                int n = i - o;
                color = SocietyStyle.NetMigrationColor(n >= 0 ? 1f : -1f);
                return n >= 0 ? $"Net +{FormatPeople(n)}" : $"Net -{FormatPeople(-n)}";
            }
            case RenderMode.SpyNetworks:
            {
                int abroad = data.SpyNetworks.Count(nw => nw.OwnerCivId == civ.Id);
                color = SocietyStyle.CounterIntelColor(Math.Max(0.5f, civ.CounterIntelligence));
                return $"CI {civ.CounterIntelligence:P0} - {abroad} abroad";
            }
            case RenderMode.Epidemics:
            {
                var (worst, total) = GetWorstInfection(data, civ.Id);
                if (worst == null) { color = new Color(140, 210, 160); return "Healthy"; }
                string name = data.Diseases.FirstOrDefault(d => d.Id == worst.Value.DiseaseId).Name ?? "Disease";
                color = SocietyStyle.DiseaseColor(worst.Value.DiseaseId);
                return $"{name} {total:P1}";
            }
            default:
                return null;
        }
    }

    private void DrawNationBadges(SpriteBatch sb, CivRenderData data, int offsetX, int offsetY, Rectangle clip)
    {
        foreach (var (text, pos, color, size) in _pendingTexts)
            UITheme.DrawTextOutlined(sb, text, pos, color, size);
        _pendingTexts.Clear();

        if (Mode == RenderMode.Infrastructure) return;
        float cellPx = CellSize * ZoomLevel;
        if (cellPx < 3f) return;

        var placed = new List<Rectangle>();
        var civs = new List<CivRenderData.CivInfo>(data.Civs);
        civs.Sort((a, b) => b.TerritorySize.CompareTo(a.TerritorySize));
        foreach (var civ in civs)
        {
            if (civ.TerritorySize < 6) continue;
            string? value = GetNationBadgeValue(data, civ, out var valueColor);
            if (value == null) continue;
            var anchor = _nationAnchor.TryGetValue(civ.Id, out var a) ? a : new Vector2(civ.CenterX, civ.CenterY + 3);
            var p = CellToScreen(anchor.X, anchor.Y, offsetX, offsetY);
            p.Y -= cellPx * 0.5f;

            string name = UITheme.Ellipsize(civ.Name.ToUpperInvariant(), 180, 11f);
            var ns = UITheme.Measure(name, 11f);
            var vs = UITheme.Measure(value, 12f);
            int w = (int)Math.Max(ns.X, vs.X + 12) + 14;
            int h = 34;
            var rect = new Rectangle((int)p.X - w / 2, (int)p.Y + 4, w, h);
            if (!clip.Contains(rect)) continue;
            bool overlaps = false;
            foreach (var r in placed) if (r.Intersects(rect)) { overlaps = true; break; }
            if (overlaps) continue;
            placed.Add(rect);

            UITheme.FillRounded(sb, rect, new Color(10, 14, 24) * 0.78f);
            UITheme.OutlineRounded(sb, rect, GetCivColor(civ.Id) * 0.8f);
            UITheme.DrawText(sb, name, new Vector2(rect.X + (w - ns.X) / 2f, rect.Y + 3), UITheme.TextDim, 11f);
            float vx = rect.X + (w - vs.X - 12) / 2f;
            UITheme.FillRounded(sb, new Rectangle((int)vx, rect.Y + 21, 8, 8), valueColor);
            UITheme.DrawText(sb, value, new Vector2(vx + 12, rect.Y + 17), UITheme.Text, 12f);
        }
    }

    // ------------------------------------------------------------------
    // Tooltips
    // ------------------------------------------------------------------

    private void AddSocietyCityLines(List<string> lines, CivRenderData.CityInfo city)
    {
        switch (Mode)
        {
            case RenderMode.Electricity:
            case RenderMode.Energy:
                lines.Add(city.Electrified ? "On the power grid" : "Not electrified");
                if (city.HasPowerPlant) lines.Add($"Power plant: {SocietyStyle.EnergyName(city.PowerPlantType ?? EnergySource.Coal)}");
                break;
            case RenderMode.Internet:
                lines.Add(city.Online ? "Online" : "Offline");
                break;
            case RenderMode.Infrastructure:
                var hubs = new List<string>();
                if (city.HasHarbor) hubs.Add("harbour");
                if (city.HasAirport) hubs.Add("airport");
                if (city.HasSpaceport) hubs.Add("spaceport");
                lines.Add(hubs.Count > 0 ? "Hubs: " + string.Join(", ", hubs) : "No transport hubs");
                if (city.Id != 0)
                {
                    var links = CivData.Routes.Where(r => r.FromCityId == city.Id || r.ToCityId == city.Id)
                        .GroupBy(r => r.Kind).Select(g => $"{g.Count()} {RouteKindName(g.Key, g.Count())}").ToList();
                    if (links.Count > 0) lines.Add("Links: " + string.Join(", ", links));
                }
                break;
        }
    }

    private void DrawNationTooltip(SpriteBatch sb, int civId, CivRenderData data, Point mouse, Viewport viewport)
    {
        var found = data.FindCiv(civId);
        if (found == null) return;
        var civ = found.Value;
        var lines = new List<string> { civ.Name };
        string gov = civ.GovType?.ToString() ?? "";
        if (!string.IsNullOrEmpty(civ.RulerName)) gov += $" - {civ.RulerTitle} {civ.RulerName}".TrimEnd();
        if (!string.IsNullOrEmpty(gov)) lines.Add(gov);
        var cities = data.Cities.Where(c => c.CivId == civId).ToList();

        switch (Mode)
        {
            case RenderMode.Electricity:
                lines.Add($"Electrification {civ.Electrification:P0}");
                lines.Add(EnergyBalanceText(civ));
                int plants = cities.Count(c => c.HasPowerPlant);
                int dark = civ.Electrification > 0.001f ? cities.Count(c => !c.Electrified) : 0;
                lines.Add($"Power plants {plants} - lines {data.PowerLines.Count(l => l.CivId == civId)}");
                if (dark > 0) lines.Add($"Blackout in {dark} of {cities.Count} settlements");
                break;

            case RenderMode.Energy:
                if (civ.EnergyMix == null || civ.EnergyMix.Length == 0)
                {
                    lines.Add("Pre-industrial: wood and muscle power");
                }
                else
                {
                    foreach (var (src, share) in civ.EnergyMix.Take(4))
                        lines.Add($"{SocietyStyle.EnergyName(src),-14} {share,5:P0}");
                    float clean = civ.EnergyMix.Where(m => SocietyStyle.IsClean(m.Source)).Sum(m => m.Share);
                    lines.Add($"Low-carbon share {clean:P0}");
                }
                lines.Add(EnergyBalanceText(civ));
                break;

            case RenderMode.Armaments:
                lines.Add($"Military strength {civ.MilitaryStrength:N0}");
                lines.Add($"{civ.Soldiers:N0} soldiers in {civ.ArmyCount} {(civ.ArmyCount == 1 ? "army" : "armies")}");
                if (civ.HasNuclearWeapons) lines.Add($"Nuclear warheads {civ.NuclearWarheads:N0}");
                if (civ.MissileSilos > 0) lines.Add($"Missile silos {civ.MissileSilos}");
                if (civ.ChemicalStockpile > 0) lines.Add($"Chemical stockpile {civ.ChemicalStockpile:N0}");
                if (civ.BioweaponProgram) lines.Add("Bioweapons programme");
                if (civ.MissileDefense) lines.Add("Missile defence shield");
                if (civ.SpySatellites > 0) lines.Add($"Spy satellites {civ.SpySatellites}");
                var enemies = civ.Detail?.Relations.Where(r => r.Status == DiplomaticStatus.War)
                    .Select(r => data.FindCiv(r.OtherCivId)?.Name).Where(n => n != null).ToList();
                if (enemies != null && enemies.Count > 0) lines.Add("At war with " + string.Join(", ", enemies));
                else if (civ.AtWar) lines.Add("At war");
                break;

            case RenderMode.Governments:
                if (civ.RulerAge > 0) lines.Add($"Ruler's age {civ.RulerAge}");
                if (!string.IsNullOrEmpty(civ.RulingParty)) lines.Add($"Ruling party: {civ.RulingParty}");
                if (civ.Detail != null && civ.Detail.IsElected && civ.NextElectionYear > 0) lines.Add($"Next election in year {civ.NextElectionYear}");
                lines.Add($"Stability {civ.Stability:P0}");
                if (civ.Detail?.HeirApparentId >= 0)
                {
                    var heir = civ.Detail.Rulers.FirstOrDefault(r => r.Id == civ.Detail.HeirApparentId);
                    if (heir.Name != null) lines.Add($"Heir apparent: {heir.Name}");
                }
                break;

            case RenderMode.Internet:
                lines.Add($"Internet {civ.InternetPenetration:P0} of the population");
                lines.Add($"Online settlements {cities.Count(c => c.Online)} of {cities.Count}");
                lines.Add($"Data cables {data.DataCables.Count(l => l.CivId == civId)}");
                break;

            case RenderMode.Infrastructure:
            {
                var routes = data.Routes.Where(r => r.CivId == civId).ToList();
                lines.Add($"Roads {routes.Count(r => r.Kind == TransportKind.Road)} - railways {routes.Count(r => r.Kind == TransportKind.Railway)}");
                lines.Add($"Sea lanes {routes.Count(r => r.Kind == TransportKind.SeaLane)} - air routes {routes.Count(r => r.Kind == TransportKind.AirRoute)}");
                int foreign = routes.Count(r => r.International);
                if (foreign > 0) lines.Add($"International links {foreign}");
                int vehicles = data.Vehicles.Count(v => v.CivId == civId);
                if (vehicles > 0) lines.Add($"Vehicles on the move {vehicles}");
                lines.Add($"Road cells {civ.Detail?.RoadCells ?? 0:N0} - trade partners {data.TradeRoutes.Count(l => l.CivId == civId)}");
                lines.Add($"Harbours {cities.Count(c => c.HasHarbor)} - airports {cities.Count(c => c.HasAirport)} - spaceports {cities.Count(c => c.HasSpaceport)}");
                break;
            }

            case RenderMode.Migrations:
            {
                var recent = RecentFlows(data).ToList();
                var arrivals = recent.Where(f => f.ToCivId == civId && f.FromCivId != civId).ToList();
                var departures = recent.Where(f => f.FromCivId == civId && f.ToCivId != civId).ToList();
                var internalFlows = recent.Where(f => f.FromCivId == civId && f.ToCivId == civId).ToList();
                lines.Add($"Last 10 years: {FormatPeople(arrivals.Sum(f => f.People))} arrived, {FormatPeople(departures.Sum(f => f.People))} left");
                foreach (var g in departures.GroupBy(f => f.Kind).OrderByDescending(g => g.Sum(f => f.People)).Take(3))
                    lines.Add($"  out: {SocietyStyle.MigrationName(g.Key)} {FormatPeople(g.Sum(f => f.People))}");
                foreach (var g in arrivals.GroupBy(f => f.Kind).OrderByDescending(g => g.Sum(f => f.People)).Take(3))
                    lines.Add($"  in: {SocietyStyle.MigrationName(g.Key)} {FormatPeople(g.Sum(f => f.People))}");
                if (internalFlows.Count > 0)
                    lines.Add($"Moving within: {FormatPeople(internalFlows.Sum(f => f.People))}");
                break;
            }

            case RenderMode.SpyNetworks:
            {
                lines.Add($"Counter-intelligence {civ.CounterIntelligence:P0} - budget {civ.IntelligenceBudget:N0} gold/yr");
                var abroad = data.SpyNetworks.Where(n => n.OwnerCivId == civId).ToList();
                var athome = data.SpyNetworks.Where(n => n.TargetCivId == civId).ToList();
                if (abroad.Count == 0) lines.Add("No networks abroad");
                foreach (var n in abroad.OrderByDescending(n => n.Strength).Take(4))
                    lines.Add($"  in {data.FindCiv(n.TargetCivId)?.Name ?? "?"}: {SocietyStyle.MissionName(n.Mission)}{(n.Compromised ? " (compromised)" : "")}");
                if (athome.Count > 0)
                    lines.Add($"Foreign networks here: {athome.Count} ({athome.Count(n => n.Compromised)} uncovered)");
                break;
            }

            case RenderMode.Epidemics:
            {
                bool any = false;
                foreach (var inf in data.Infections.Where(i => i.CivId == civId).OrderByDescending(i => i.Share))
                {
                    string name = data.Diseases.FirstOrDefault(d => d.Id == inf.DiseaseId).Name ?? "Disease";
                    lines.Add($"{name}: {inf.Infected:N0} infected ({inf.Share:P1}), {inf.Dead:N0} dead");
                    var measures = new List<string>();
                    if (inf.Quarantine) measures.Add("quarantine");
                    if (inf.BordersClosed) measures.Add("borders closed");
                    if (measures.Count > 0) lines.Add("  " + string.Join(", ", measures));
                    any = true;
                }
                if (!any) lines.Add("No infections");
                break;
            }
        }
        lines.Add("Click a nation card in the info panel for details");
        QueueTooltip(string.Join("\n", lines), new Point(mouse.X, mouse.Y + 14));
    }

    private static string FormatPeople(int n) => n >= 1_000_000 ? $"{n / 1_000_000f:0.#}M" : n >= 10_000 ? $"{n / 1000f:0.#}K" : n.ToString("N0");

    private static string RouteKindName(TransportKind kind, int count) => kind switch
    {
        TransportKind.Road => count == 1 ? "road" : "roads",
        TransportKind.Railway => count == 1 ? "railway" : "railways",
        TransportKind.SeaLane => count == 1 ? "sea lane" : "sea lanes",
        _ => count == 1 ? "air route" : "air routes"
    };

    private static string EnergyBalanceText(CivRenderData.CivInfo civ)
    {
        if (civ.EnergyProduction <= 0 && civ.EnergyDemand <= 0) return "No energy statistics yet";
        float balance = civ.EnergyProduction - civ.EnergyDemand;
        string sign = balance >= 0 ? "surplus" : "deficit";
        return $"Energy {civ.EnergyProduction:N0} / demand {civ.EnergyDemand:N0} ({sign} {Math.Abs(balance):N0})";
    }

    // ------------------------------------------------------------------
    // Legend
    // ------------------------------------------------------------------

    private enum LegendKind { Swatch, Icon, Line, Dashed, Rail, Glow, Arc, Dotted, Arrow, Cross, Herd }

    private readonly record struct LegendRow(LegendKind Kind, Color Color, Texture2D? Icon, string Label);

    private void DrawSocietyLegend(SpriteBatch sb, int screenWidth, int screenHeight)
    {
        var rows = new List<LegendRow>();
        Func<float, Color>? gradient = null;
        string[]? gradientLabels = null;
        string? note = null;
        string title = "LEGEND";
        var icons = _icons;

        switch (Mode)
        {
            case RenderMode.Electricity:
                title = "POWER GRID";
                gradient = SocietyStyle.ElectrificationColor;
                gradientLabels = new[] { "Unlit", "Partly", "Electrified" };
                rows.Add(new(LegendKind.Line, SocietyStyle.PowerLine, null, "Power line"));
                rows.Add(new(LegendKind.Glow, new Color(255, 214, 110), null, "Lit settlement"));
                rows.Add(new(LegendKind.Glow, SocietyStyle.Blackout, null, "Blackout"));
                rows.Add(new(LegendKind.Swatch, new Color(220, 40, 40), null, "EMP damage"));
                if (icons != null)
                {
                    rows.Add(new(LegendKind.Icon, Color.White, icons.CoolingTower, "Nuclear plant"));
                    rows.Add(new(LegendKind.Icon, Color.White, icons.SolarPanel, "Solar farm"));
                    rows.Add(new(LegendKind.Icon, Color.White, icons.WindTurbine, "Wind farm"));
                    rows.Add(new(LegendKind.Icon, Color.White, icons.Dam, "Hydro dam"));
                    rows.Add(new(LegendKind.Icon, Color.White, icons.Factory, "Fossil plant"));
                    rows.Add(new(LegendKind.Icon, Color.White, icons.FusionCore, "Fusion plant"));
                }
                break;

            case RenderMode.Energy:
                title = "ENERGY SOURCES";
                foreach (var src in SocietyStyle.AllEnergySources)
                    rows.Add(new(LegendKind.Swatch, SocietyStyle.EnergyColor(src), null, SocietyStyle.EnergyName(src)));
                rows.Add(new(LegendKind.Swatch, NoDataTint, null, "Pre-industrial"));
                note = "Dominant source per nation";
                break;

            case RenderMode.Armaments:
                title = "ARMAMENTS";
                gradient = t => SocietyStyle.MilitaryColor(t);
                gradientLabels = new[] { "Weak", "Military strength", "Strongest" };
                if (icons != null)
                {
                    rows.Add(new(LegendKind.Icon, Color.White, icons.Trefoil, "Nuclear arsenal"));
                    rows.Add(new(LegendKind.Icon, Color.White, icons.Silo, "Missile silo"));
                    rows.Add(new(LegendKind.Icon, Color.White, icons.Flask, "Chemical weapons"));
                    rows.Add(new(LegendKind.Icon, Color.White, icons.Biohazard, "Bioweapons"));
                    rows.Add(new(LegendKind.Icon, Color.White, icons.Shield, "Missile defence"));
                    rows.Add(new(LegendKind.Icon, Color.White, icons.CrossedSwords, "Battle"));
                }
                rows.Add(new(LegendKind.Swatch, SocietyStyle.Radiation, null, "Radioactive fallout"));
                break;

            case RenderMode.Governments:
                title = "GOVERNMENTS";
                foreach (var g in SocietyStyle.AllGovernmentTypes)
                    rows.Add(new(LegendKind.Swatch, SocietyStyle.GovernmentColor(g), null, g.ToString()));
                note = "Hover a nation for its ruler and party";
                break;

            case RenderMode.Internet:
                title = "INTERNET";
                gradient = SocietyStyle.InternetColor;
                gradientLabels = new[] { "Offline", "50%", "Online" };
                rows.Add(new(LegendKind.Line, SocietyStyle.DataCable, null, "Land backbone"));
                rows.Add(new(LegendKind.Arc, new Color(70, 160, 255), null, "Undersea cable"));
                rows.Add(new(LegendKind.Glow, new Color(90, 230, 255), null, "Online settlement"));
                break;

            case RenderMode.Infrastructure:
                title = "INFRASTRUCTURE";
                rows.Add(new(LegendKind.Line, new Color(255, 196, 70), null, "Highway"));
                rows.Add(new(LegendKind.Line, new Color(226, 226, 232), null, "Paved road"));
                rows.Add(new(LegendKind.Line, new Color(176, 146, 100), null, "Dirt path"));
                rows.Add(new(LegendKind.Rail, Color.White, null, "Railway"));
                rows.Add(new(LegendKind.Dotted, SeaLaneColor, null, "Sea lane"));
                rows.Add(new(LegendKind.Arc, AirRouteColor * 0.6f, null, "Air route"));
                rows.Add(new(LegendKind.Dashed, SocietyStyle.TradeRoute, null, "Trade partners"));
                if (icons != null)
                {
                    rows.Add(new(LegendKind.Icon, Color.White, icons.Anchor, "Harbour"));
                    rows.Add(new(LegendKind.Icon, Color.White, icons.Plane, "Airport"));
                    rows.Add(new(LegendKind.Icon, Color.White, icons.Rocket, "Spaceport"));
                    foreach (var (kind, label) in new[] { (VehicleKind.Caravan, "Caravan / truck"), (VehicleKind.Train, "Train"),
                                 (VehicleKind.CargoShip, "Ship (wake)"), (VehicleKind.Airliner, "Airliner") })
                    {
                        var sprite = icons.VehicleSprite(kind);
                        if (sprite != null) rows.Add(new(LegendKind.Icon, Color.White, sprite.Value.Base, label));
                    }
                }
                note = "Road colour shows the era: dirt, paved, highway";
                break;

            case RenderMode.Migrations:
            {
                var recent = RecentFlows(CivData).ToList();
                int y0 = recent.Count > 0 ? recent.Min(f => f.Year) : CivData.LatestYear;
                title = recent.Count > 0 ? $"MIGRATIONS {y0}-{CivData.LatestYear}" : "MIGRATIONS";
                gradient = t => SocietyStyle.NetMigrationColor(t * 2f - 1f);
                gradientLabels = new[] { "Emigration", "Net migration", "Immigration" };
                var totals = recent.GroupBy(f => f.Kind).ToDictionary(g => g.Key, g => g.Sum(f => f.People));
                foreach (var kind in SocietyStyle.AllMigrationKinds)
                {
                    int people = totals.GetValueOrDefault(kind);
                    rows.Add(new(LegendKind.Arrow, SocietyStyle.MigrationColor(kind), null,
                        people > 0 ? $"{SocietyStyle.MigrationName(kind)}  {FormatPeople(people)}" : SocietyStyle.MigrationName(kind)));
                }
                if (ShowWildlife) rows.Add(new(LegendKind.Herd, new Color(112, 78, 52), null, "Wild herds and flocks"));
                note = recent.Count == 0 ? "No migrations recorded yet" : "Thicker arrows carry more people";
                break;
            }

            case RenderMode.SpyNetworks:
                title = "SPY NETWORKS";
                gradient = SocietyStyle.CounterIntelColor;
                gradientLabels = new[] { "Open", "Counter-intelligence", "Watchful" };
                foreach (var mission in SocietyStyle.AllMissions)
                    rows.Add(new(LegendKind.Dotted, SocietyStyle.MissionColor(mission), null, SocietyStyle.MissionName(mission)));
                rows.Add(new(LegendKind.Cross, new Color(255, 80, 80), null, "Compromised"));
                note = CivData.SpyNetworks.Count == 0 ? "No spy networks yet" : $"{CivData.SpyNetworks.Count} networks - line width: strength";
                break;

            case RenderMode.Epidemics:
                title = "EPIDEMICS";
                var sick = CivData.Diseases.Count == 1 ? SocietyStyle.DiseaseColor(CivData.Diseases[0].Id) : new Color(230, 60, 60);
                gradient = t => Color.Lerp(HealthyTint, sick, 0.25f + 0.75f * MathF.Pow(t, 0.6f));
                gradientLabels = new[] { "Healthy", "15%", "30%+ infected" };
                foreach (var d in CivData.Diseases)
                    rows.Add(new(LegendKind.Swatch, SocietyStyle.DiseaseColor(d.Id), null, $"{d.Name} ({d.TotalInfected:N0})"));
                if (CivData.Diseases.Count == 0) note = "No active epidemics";
                break;
        }

        const int rowH = 20;
        // The migration summary reads better as one tall column with the totals
        bool singleColumn = Mode == RenderMode.Migrations;
        bool twoColumns = rows.Count > 8 && !singleColumn;
        int legendWidth = twoColumns ? 340 : singleColumn ? 270 : 250;
        int perColumn = twoColumns ? (rows.Count + 1) / 2 : rows.Count;
        int gradientH = gradient != null ? 42 : 0;
        int noteH = note != null ? 20 : 0;
        int legendHeight = 40 + gradientH + perColumn * rowH + noteH + 6;
        int legendX = screenWidth - legendWidth - 12;
        int legendY = AvoidBottomBar(legendX, screenHeight - legendHeight - 12, legendWidth, legendHeight);
        var rect = new Rectangle(legendX, legendY, legendWidth, legendHeight);

        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp);
        UITheme.DrawTitledPanel(sb, rect, title, UITheme.Accent, 30);

        int y = legendY + 40;
        if (gradient != null)
        {
            int gx = legendX + 12, gw = legendWidth - 24;
            for (int i = 0; i < gw; i++)
                sb.Draw(_pixelTexture, new Rectangle(gx + i, y, 1, 12), gradient(i / (float)(gw - 1)));
            UITheme.DrawRectOutline(sb, new Rectangle(gx, y, gw, 12), new Color(0, 0, 0, 170));
            if (gradientLabels != null)
            {
                UITheme.DrawText(sb, gradientLabels[0], new Vector2(gx, y + 15), UITheme.TextDim, 11f);
                var ms = UITheme.Measure(gradientLabels[1], 11f);
                UITheme.DrawText(sb, gradientLabels[1], new Vector2(gx + (gw - ms.X) / 2, y + 15), UITheme.TextDim, 11f);
                var es = UITheme.Measure(gradientLabels[2], 11f);
                UITheme.DrawText(sb, gradientLabels[2], new Vector2(gx + gw - es.X, y + 15), UITheme.TextDim, 11f);
            }
            y += gradientH;
        }

        int columnWidth = twoColumns ? (legendWidth - 24) / 2 : legendWidth - 24;
        for (int i = 0; i < rows.Count; i++)
        {
            int col = twoColumns && i >= perColumn ? 1 : 0;
            int row = col == 1 ? i - perColumn : i;
            int x = legendX + 12 + col * columnWidth;
            int ry = y + row * rowH;
            DrawLegendSample(sb, rows[i], new Rectangle(x, ry + 2, 18, 16));
            string label = UITheme.Ellipsize(rows[i].Label, columnWidth - 28, 12f);
            UITheme.DrawText(sb, label, new Vector2(x + 24, ry + 2), UITheme.Text, 12f);
        }

        if (note != null)
            UITheme.DrawText(sb, note, new Vector2(legendX + 12, legendY + legendHeight - 24), UITheme.TextMuted, 11f);

        sb.End();
        sb.Begin(samplerState: SamplerState.PointClamp);
    }

    /// <summary>Lifts a bottom-anchored legend above the time control bar when they would overlap (small windows).</summary>
    private static int AvoidBottomBar(int x, int y, int width, int height)
    {
        var bar = BottomControlUI.LastBounds;
        if (!bar.IsEmpty && new Rectangle(x, y, width, height).Intersects(bar))
            y = bar.Y - height - 8;
        return y;
    }

    private void DrawLegendSample(SpriteBatch sb, LegendRow row, Rectangle r)
    {
        var mid = new Vector2(r.X, r.Center.Y);
        var end = new Vector2(r.Right, r.Center.Y);
        switch (row.Kind)
        {
            case LegendKind.Swatch:
                var sw = new Rectangle(r.X + 2, r.Y + 1, 14, 14);
                sb.Draw(_pixelTexture, sw, row.Color);
                UITheme.DrawRectOutline(sb, sw, new Color(0, 0, 0, 160));
                break;
            case LegendKind.Icon:
                if (row.Icon != null) sb.Draw(row.Icon, new Rectangle(r.X + 1, r.Y, 16, 16), Color.White);
                break;
            case LegendKind.Line:
                UITheme.DrawLine(sb, mid, end, Color.Black * 0.5f, 4f);
                UITheme.DrawLine(sb, mid, end, row.Color, 2f);
                break;
            case LegendKind.Dashed:
                for (int x = 0; x < r.Width; x += 6)
                    UITheme.DrawLine(sb, mid + new Vector2(x, 0), mid + new Vector2(Math.Min(r.Width, x + 3), 0), row.Color, 2f);
                break;
            case LegendKind.Rail:
                UITheme.DrawLine(sb, mid, end, new Color(30, 24, 22), 4f);
                for (int x = 0; x < r.Width; x += 8)
                    UITheme.DrawLine(sb, mid + new Vector2(x, 0), mid + new Vector2(Math.Min(r.Width, x + 4), 0), new Color(236, 228, 210), 1.6f);
                break;
            case LegendKind.Glow:
                UITheme.DrawGlow(sb, r.Center.ToVector2(), 9f, row.Color);
                break;
            case LegendKind.Dotted:
                for (int x = 1; x < r.Width; x += 5)
                    sb.Draw(_pixelTexture, new Rectangle(r.X + x, r.Center.Y - 1, 2, 2), row.Color);
                break;
            case LegendKind.Arrow:
                UITheme.DrawLine(sb, mid, end, row.Color * 0.8f, 3f);
                UITheme.DrawLine(sb, end, end + new Vector2(-6, -4), row.Color, 2f);
                UITheme.DrawLine(sb, end, end + new Vector2(-6, 4), row.Color, 2f);
                break;
            case LegendKind.Cross:
                UITheme.DrawLine(sb, r.Center.ToVector2() + new Vector2(-5, -5), r.Center.ToVector2() + new Vector2(5, 5), row.Color, 2f);
                UITheme.DrawLine(sb, r.Center.ToVector2() + new Vector2(-5, 5), r.Center.ToVector2() + new Vector2(5, -5), row.Color, 2f);
                break;
            case LegendKind.Herd:
                _discTexture ??= BuildDisc(_graphicsDevice, 64);
                for (int k = 0; k < 4; k++)
                    sb.Draw(_discTexture, new Rectangle(r.X + 1 + k * 4, r.Y + 4 + (k % 2) * 5, 6, 4), row.Color);
                break;
            case LegendKind.Arc:
                var a = new Vector2(r.X, r.Center.Y + 3);
                var b = new Vector2(r.Right, r.Center.Y + 3);
                DrawArc(sb, a, new Vector2(r.Center.X, r.Y - 4), b, row.Color, 2f, 10);
                break;
        }
    }

    // ------------------------------------------------------------------
    // Space: orbit widget and nuclear winter haze
    // ------------------------------------------------------------------

    private void DrawNuclearWinterHaze(SpriteBatch sb, Rectangle mapRect)
    {
        float winter = CivData.NuclearWinter;
        if (winter <= 0.005f) return;
        _hazeTexture ??= BuildHazeTexture(_graphicsDevice, 128);

        // Flat ash-grey veil plus two layers of drifting soot
        sb.Draw(_pixelTexture, mapRect, new Color(84, 76, 66) * (0.1f + 0.36f * winter));
        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearWrap);
        float t = (float)(DateTime.Now.TimeOfDay.TotalSeconds % 10000);
        float scale = 3.2f;
        var src1 = new Rectangle((int)(t * 6f), (int)(t * 1.5f), (int)(mapRect.Width / scale), (int)(mapRect.Height / scale));
        var src2 = new Rectangle((int)(-t * 4f), (int)(t * 2.5f), (int)(mapRect.Width / (scale * 1.7f)), (int)(mapRect.Height / (scale * 1.7f)));
        sb.Draw(_hazeTexture, mapRect, src1, new Color(168, 152, 128) * (0.08f + 0.28f * winter));
        sb.Draw(_hazeTexture, mapRect, src2, new Color(40, 36, 32) * (0.08f + 0.32f * winter));
        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp);
    }

    /// <summary>Tileable smooth noise used for the soot clouds (alpha only, premultiplied white).</summary>
    private static Texture2D BuildHazeTexture(GraphicsDevice device, int size)
    {
        var data = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = x / (float)size * MathF.PI * 2f, v = y / (float)size * MathF.PI * 2f;
                float n = 0.5f
                    + 0.22f * MathF.Sin(u * 2 + MathF.Cos(v * 1) * 1.3f)
                    + 0.16f * MathF.Sin(v * 3 + MathF.Sin(u * 2) * 1.7f)
                    + 0.10f * MathF.Sin((u + v) * 5 + 1.3f)
                    + 0.06f * MathF.Cos((u * 7 - v * 4) + 0.7f);
                float a = Math.Clamp((n - 0.35f) / 0.6f, 0f, 1f);
                a = a * a * (3 - 2 * a);
                data[y * size + x] = Color.White * a;
            }
        }
        var tex = new Texture2D(device, size, size);
        tex.SetData(data);
        return tex;
    }

    private static Texture2D BuildDisc(GraphicsDevice device, int size)
    {
        var data = new Color[size * size];
        float r = size / 2f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x + 0.5f - r, dy = y + 0.5f - r;
                float d = MathF.Sqrt(dx * dx + dy * dy);
                data[y * size + x] = Color.White * Math.Clamp(r - d, 0f, 1f);
            }
        }
        var tex = new Texture2D(device, size, size);
        tex.SetData(data);
        return tex;
    }

    /// <summary>Screen rectangle of the orbit widget (empty when hidden).</summary>
    public Rectangle OrbitWidgetBounds { get; private set; }

    /// <summary>
    /// Space activity readout at the top right of the map: a small planet with its satellites,
    /// stations and bases on their orbits, people in space and the nuclear winter level.
    /// </summary>
    public void DrawSpaceOverlay(SpriteBatch sb, int offsetX, int offsetY)
    {
        var data = CivData;
        OrbitWidgetBounds = Rectangle.Empty;
        if (data.Orbitals.Count == 0 && data.NuclearWinter <= 0.005f) return;
        if (UITheme.Font == null || _icons == null) return;
        _discTexture ??= BuildDisc(_graphicsDevice, 64);

        var viewport = _graphicsDevice.Viewport;
        bool hasOrbit = data.Orbitals.Count > 0;
        int w = 300;
        int h = hasOrbit ? 140 : 58;
        var rect = new Rectangle(viewport.Width - w - 12, offsetY + 10, w, h);
        OrbitWidgetBounds = rect;
        float time = (float)(DateTime.Now.TimeOfDay.TotalSeconds % 3600);

        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp);
        UITheme.DrawPanel(sb, rect, new Color(10, 14, 24, 232));

        int textX = rect.X + 12;
        if (hasOrbit)
        {
            // --- Planet and orbits ---
            var center = new Vector2(rect.X + 78, rect.Y + 76);
            const float tilt = 0.36f;
            var orbits = new List<(CivRenderData.OrbitalInfo O, Vector2 Pos, bool Front, float Size, Texture2D Tex)>();
            foreach (var o in data.Orbitals)
            {
                float baseR = o.Type switch
                {
                    OrbitalObjectType.Satellite => 36f,
                    OrbitalObjectType.SpaceStation => 47f,
                    OrbitalObjectType.LunarBase => 64f,
                    _ => 68f
                };
                float radius = baseR + Math.Clamp(o.OrbitRadius - 1f, -0.5f, 1.5f) * 5f;
                float speed = o.Type == OrbitalObjectType.LunarBase || o.Type == OrbitalObjectType.Colony ? 0.05f : 18f / MathF.Pow(radius, 1.2f);
                float angle = o.OrbitAngle + time * speed;
                var pos = center + new Vector2(MathF.Cos(angle) * radius, MathF.Sin(angle) * radius * tilt);
                bool front = MathF.Sin(angle) >= 0;
                var (size, tex) = o.Type switch
                {
                    OrbitalObjectType.Satellite => (11f, _icons.Satellite),
                    OrbitalObjectType.SpaceStation => (16f, _icons.Station),
                    OrbitalObjectType.LunarBase => (18f, _icons.MoonBase),
                    _ => (15f, _icons.Rocket)
                };
                orbits.Add((o, pos, front, size, tex));
            }

            // Orbit rings (behind the planet)
            foreach (var r in new[] { 36f, 47f, 64f })
            {
                if (!data.Orbitals.Any(o => r == 36f ? o.Type == OrbitalObjectType.Satellite :
                        r == 47f ? o.Type == OrbitalObjectType.SpaceStation : o.Type is OrbitalObjectType.LunarBase or OrbitalObjectType.Colony)) continue;
                Vector2 prev = center + new Vector2(r, 0);
                for (int i = 1; i <= 48; i++)
                {
                    float a = i / 48f * MathF.PI * 2f;
                    var p = center + new Vector2(MathF.Cos(a) * r, MathF.Sin(a) * r * tilt);
                    UITheme.DrawLine(sb, prev, p, new Color(120, 160, 220) * (MathF.Sin(a) >= 0 ? 0.35f : 0.18f), 1f);
                    prev = p;
                }
            }

            void DrawObject((CivRenderData.OrbitalInfo O, Vector2 Pos, bool Front, float Size, Texture2D Tex) item)
            {
                float alpha = item.O.Orphaned ? 0.45f : (item.Front ? 1f : 0.55f);
                Color civColor = GetCivColor(item.O.CivId);
                UITheme.DrawGlow(sb, item.Pos, item.Size * 0.8f, civColor * (0.5f * alpha));
                sb.Draw(item.Tex, new Rectangle((int)(item.Pos.X - item.Size / 2), (int)(item.Pos.Y - item.Size / 2), (int)item.Size, (int)item.Size), Color.White * alpha);
            }

            foreach (var item in orbits.Where(i => !i.Front)) DrawObject(item);

            // Planet: ocean disc, a hint of land, atmosphere rim and terminator
            float pr = 22f;
            UITheme.DrawGlow(sb, center, pr * 1.5f, new Color(80, 150, 255, 0) * 0.5f);
            var discRect = new Rectangle((int)(center.X - pr), (int)(center.Y - pr), (int)(pr * 2), (int)(pr * 2));
            sb.Draw(_discTexture, discRect, new Color(40, 96, 176));
            UITheme.DrawGlow(sb, center + new Vector2(-5, -4), pr * 0.55f, new Color(80, 160, 90) * 0.9f);
            UITheme.DrawGlow(sb, center + new Vector2(7, 5), pr * 0.4f, new Color(90, 150, 80) * 0.8f);
            if (data.NuclearWinter > 0.005f)
                sb.Draw(_discTexture, discRect, new Color(110, 96, 80) * (0.3f + 0.6f * data.NuclearWinter));
            UITheme.DrawGlow(sb, center + new Vector2(pr * 0.55f, pr * 0.2f), pr * 1.1f, Color.Black * 0.55f);

            foreach (var item in orbits.Where(i => i.Front)) DrawObject(item);

            // --- Counters ---
            textX = rect.X + 168;
            int ty = rect.Y + 10;
            UITheme.DrawText(sb, "IN SPACE", new Vector2(textX, ty), UITheme.Accent, 11f);
            ty += 18;
            void Row(string label, int value, Color color)
            {
                UITheme.DrawText(sb, label, new Vector2(textX, ty), UITheme.TextDim, 12f);
                string v = value.ToString("N0");
                var vs = UITheme.Measure(v, 12f);
                UITheme.DrawText(sb, v, new Vector2(rect.Right - 12 - vs.X, ty), color, 12f);
                ty += 16;
            }
            Row("Satellites", data.Orbitals.Count(o => o.Type == OrbitalObjectType.Satellite), UITheme.Text);
            Row("Stations", data.Orbitals.Count(o => o.Type == OrbitalObjectType.SpaceStation), UITheme.Text);
            Row("Lunar bases", data.Orbitals.Count(o => o.Type == OrbitalObjectType.LunarBase), UITheme.Text);
            Row("Colonies", data.Orbitals.Count(o => o.Type == OrbitalObjectType.Colony), UITheme.Text);
            Row("People", data.PeopleInSpace, UITheme.Gold);
        }

        // --- Nuclear winter meter ---
        if (data.NuclearWinter > 0.005f)
        {
            int my = rect.Bottom - 24;
            int mx = hasOrbit ? rect.X + 12 : rect.X + 12;
            int mw = hasOrbit ? 110 : w - 24;
            if (!hasOrbit)
            {
                UITheme.DrawText(sb, "NUCLEAR WINTER", new Vector2(mx, rect.Y + 8), new Color(230, 170, 120), 11f);
                string pct = $"{data.NuclearWinter:P0}";
                var ps = UITheme.Measure(pct, 12f);
                UITheme.DrawText(sb, pct, new Vector2(rect.Right - 12 - ps.X, rect.Y + 7), UITheme.Bad, 12f);
                UITheme.DrawMeter(sb, new Rectangle(mx, my, mw, 10), data.NuclearWinter, new Color(150, 110, 80));
            }
            else
            {
                UITheme.DrawText(sb, $"Nuclear winter {data.NuclearWinter:P0}", new Vector2(mx, rect.Y + 8), new Color(230, 170, 120), 11f);
            }
        }

        // Hover: list the objects
        var mouse = Mouse.GetState();
        sb.End();
        sb.Begin(samplerState: SamplerState.PointClamp);
        if (hasOrbit && rect.Contains(mouse.Position))
        {
            var lines = new List<string> { $"{data.Orbitals.Count} objects in space, {data.PeopleInSpace:N0} people aboard" };
            foreach (var o in data.Orbitals.OrderByDescending(o => o.Crew).ThenBy(o => o.Type).Take(10))
            {
                string owner = data.FindCiv(o.CivId)?.Name ?? "lost nation";
                string crew = o.Crew > 0 ? $", crew {o.Crew}" : "";
                lines.Add($"{o.Name} ({o.Type}) - {owner}{crew}{(o.Orphaned ? " - orphaned" : "")}");
            }
            if (data.Orbitals.Count > 10) lines.Add($"... and {data.Orbitals.Count - 10} more");
            UITheme.DrawTooltip(sb, string.Join("\n", lines), new Point(mouse.X, mouse.Y + 14), viewport.Width, viewport.Height);
        }
    }
}
