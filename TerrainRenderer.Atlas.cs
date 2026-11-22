using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace SimPlanet;

/// <summary>
/// Atlas touches on top of the civilization layer: nation names written across their
/// territory in spaced capitals (terrain and political views) and shockwaves on battlefields.
/// </summary>
public partial class TerrainRenderer
{
    private readonly record struct NationLabel(float X, float Y, int Cells, float SpanX);

    private readonly Dictionary<int, NationLabel> _nationLabels = new();
    private readonly Dictionary<int, (double Cos, double Sin, double Y, int N, int MinY, int MaxY)> _labelSums = new();
    private readonly Dictionary<(string, int), (string[] Chars, float[] Advance, float Width)> _spacedText = new();
    private readonly List<Rectangle> _labelRects = new();
    private readonly List<Rectangle> _labelBlockers = new();
    private static readonly (int X, int Y)[] NameOffsets =
    {
        (0, 0), (0, -1), (0, 1), (-1, 0), (1, 0), (0, -2), (0, 2), (-1, -1), (1, -1), (-1, 1), (1, 1),
        (0, -3), (0, 3), (-1, -2), (1, -2), (-1, 2), (1, 2), (-2, 0), (2, 0), (0, -4), (0, 4), (-2, -3), (2, -3), (-2, 3), (2, 3)
    };
    private readonly List<(int Id, NationLabel Label)> _labelOrder = new();

    /// <summary>
    /// Finds where each nation's name goes: the centre of mass of its land (on a circle, so
    /// territories across the map seam work). Called from the texture refresh.
    /// </summary>
    private void UpdateNationLabelAnchors()
    {
        _nationLabels.Clear();
        if (!_hasTerritory) return;
        int w = _map.Width, h = _map.Height;
        _labelSums.Clear();
        double k = Math.PI * 2 / w;
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int o = _civOwnerMap[y * w + x];
                if (o <= 0) continue;
                _labelSums.TryGetValue(o, out var s);
                if (s.N == 0) { s.MinY = y; s.MaxY = y; }
                _labelSums[o] = (s.Cos + Math.Cos(x * k), s.Sin + Math.Sin(x * k), s.Y + y, s.N + 1, Math.Min(s.MinY, y), Math.Max(s.MaxY, y));
            }
        }
        foreach (var (id, s) in _labelSums)
        {
            double angle = Math.Atan2(s.Sin, s.Cos);
            if (angle < 0) angle += Math.PI * 2;
            float cx = (float)(angle / k);
            float cy = (float)(s.Y / s.N);
            // A nation spread over several islands can have its centre of mass in the sea:
            // move the anchor to its nearest own land
            int icx = ((int)MathF.Round(cx) % w + w) % w, icy = Math.Clamp((int)MathF.Round(cy), 0, h - 1);
            if (_civOwnerMap[icy * w + icx] != id)
            {
                float bestD = float.MaxValue;
                for (int y = s.MinY; y <= s.MaxY; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        if (_civOwnerMap[y * w + x] != id) continue;
                        float dx = MathF.Abs(x - cx);
                        if (dx > w / 2f) dx = w - dx;
                        float d = dx * dx + (y - cy) * (y - cy);
                        if (d < bestD) { bestD = d; icx = x; icy = y; }
                    }
                }
                cx = icx;
                cy = icy;
            }
            // Rough horizontal extent: area over height
            float span = s.N / (float)Math.Max(1, s.MaxY - s.MinY + 1);
            _nationLabels[id] = new NationLabel(cx, cy, s.N, span);
        }
    }

    private (string[] Chars, float[] Advance, float Width) SpacedText(string text, int fontSize, float tracking)
    {
        if (_spacedText.TryGetValue((text, fontSize), out var cached)) return cached;
        if (_spacedText.Count > 400) _spacedText.Clear();
        var chars = new string[text.Length];
        var adv = new float[text.Length];
        float width = 0;
        for (int i = 0; i < text.Length; i++)
        {
            chars[i] = text[i].ToString();
            float cw = text[i] == ' ' ? fontSize * 0.42f : UITheme.Measure(chars[i], fontSize).X;
            adv[i] = cw + tracking;
            width += adv[i];
        }
        width -= tracking;
        var result = (chars, adv, Math.Max(0, width));
        _spacedText[(text, fontSize)] = result;
        return result;
    }

    /// <summary>Nation names across their territory, largest nations first; names that would collide are skipped.</summary>
    private void DrawNationNames(SpriteBatch sb, CivRenderData data, int offsetX, int offsetY, Rectangle clip, float cellPx)
    {
        if (!ShowSettlementLabels || UITheme.Font == null) return;
        if (Mode != RenderMode.Terrain && Mode != RenderMode.Civilizations) return;
        // An overview feature: fades out once the player is close enough to read the cities
        float fade = 1f - Math.Clamp((cellPx - 13f) / 5f, 0f, 1f);
        if (fade <= 0.02f || _nationLabels.Count == 0) return;

        _labelOrder.Clear();
        foreach (var kv in _nationLabels) _labelOrder.Add((kv.Key, kv.Value));
        _labelOrder.Sort((a, b) => b.Label.Cells.CompareTo(a.Label.Cells));
        _labelRects.Clear();
        float mapPx = _map.Width * cellPx;

        // Settlement icons and their name plates come first: nation names only use free room
        _labelBlockers.Clear();
        float iconScale = Math.Clamp(cellPx / 5.5f, 0.7f, 1.7f);
        foreach (var city in data.Cities)
        {
            var p = CellToScreen(city.X, city.Y, offsetX, offsetY);
            if (p.X < clip.X - 80 || p.X > clip.Right + 80 || p.Y < clip.Y - 40 || p.Y > clip.Bottom + 40) continue;
            int type = Math.Clamp(city.Type, 0, 3);
            int size = (int)(SettlementPixelSize[type] * iconScale);
            _labelBlockers.Add(new Rectangle((int)(p.X - size / 2f), (int)(p.Y - size * 0.62f), size, size));
            float minCellPx = city.IsCapital ? 3.5f : type switch { 3 => 3.5f, 2 => 5f, 1 => 8f, _ => 11f };
            if (cellPx < minCellPx || string.IsNullOrEmpty(city.Name)) continue;
            int lw = city.Name.Length * 7 + 8;
            _labelBlockers.Add(new Rectangle((int)(p.X - lw / 2f), (int)(p.Y + size * 0.42f), lw, 17));
        }

        foreach (var (id, label) in _labelOrder)
        {
            var civ = data.FindCiv(id);
            if (civ == null || string.IsNullOrEmpty(civ.Value.Name)) continue;
            float extentPx = MathF.Sqrt(label.Cells) * cellPx;
            if (extentPx < 34f) continue; // too small to carry a name at this zoom

            int fontSize = (int)Math.Clamp(9f + extentPx / 16f, 11f, 22f);
            float tracking = fontSize * 0.34f;
            var (chars, adv, width) = SpacedText(civ.Value.Name.ToUpperInvariant(), fontSize, tracking);
            float maxWidth = Math.Max(label.SpanX * cellPx * 1.7f, extentPx * 1.6f);
            while (width > maxWidth && fontSize > 11)
            {
                fontSize--;
                tracking = fontSize * 0.34f;
                (chars, adv, width) = SpacedText(civ.Value.Name.ToUpperInvariant(), fontSize, tracking);
            }
            if (width > maxWidth * 1.35f) continue;

            var anchor = CellToScreen(label.X, label.Y, offsetX, offsetY);
            // The map wraps horizontally: use the copy that is on screen
            if (anchor.X < clip.X - width && anchor.X + mapPx < clip.Right + width) anchor.X += mapPx;
            else if (anchor.X > clip.Right + width && anchor.X - mapPx > clip.X - width) anchor.X -= mapPx;

            // Try the centre first, then nearby spots, and give up rather than write over a town
            Vector2 pos = default;
            Rectangle rect = default;
            bool placed = false;
            float stepY = fontSize * 1.25f, stepX = Math.Max(12f, width * 0.22f);
            float reach = Math.Max(2, MathF.Sqrt(label.Cells) * cellPx * 0.5f / stepY);
            for (int k = 0; k < NameOffsets.Length && !placed; k++)
            {
                var (ox, oy) = NameOffsets[k];
                if (Math.Abs(oy) > reach) continue;
                var cand = anchor + new Vector2(ox * stepX, oy * stepY);
                var cr = new Rectangle((int)(cand.X - width / 2f) - 4, (int)(cand.Y - fontSize * 0.7f) - 1, (int)width + 8, (int)(fontSize * 1.35f) + 2);
                if (cr.X < clip.X + 4 || cr.Right > clip.Right - 4 || cr.Y < clip.Y + 4 || cr.Bottom > clip.Bottom - 4) continue;
                bool blocked = false;
                foreach (var r in _labelRects) if (r.Intersects(cr)) { blocked = true; break; }
                if (!blocked) foreach (var r in _labelBlockers) if (r.Intersects(cr)) { blocked = true; break; }
                if (blocked) continue;
                pos = cand;
                rect = cr;
                placed = true;
            }
            if (!placed) continue;
            _labelRects.Add(rect);

            var cc = GetCivColor(id);
            var ink = Color.Lerp(cc, Color.White, Mode == RenderMode.Civilizations ? 0.88f : 0.62f) * (0.94f * fade);
            float x = pos.X - width / 2f;
            float y = pos.Y - fontSize * 0.7f;
            // Soft dark halo behind the whole word, then outlined letters
            UITheme.DrawGlow(sb, new Vector2(pos.X, pos.Y), Math.Max(width * 0.36f, fontSize * 1.4f), Color.Black * (0.16f * fade));
            for (int i = 0; i < chars.Length; i++)
            {
                if (chars[i] != " ") UITheme.DrawTextOutlined(sb, chars[i], new Vector2(x, y), ink, fontSize);
                x += adv[i];
            }
        }
    }

    /// <summary>Shockwave rings and flying sparks on fresh battlefields (on top of the crossed swords).</summary>
    private void DrawBattleEffects(SpriteBatch sb, CivRenderData data, int offsetX, int offsetY, Rectangle clip, float cellPx, float time)
    {
        if (data.Battles.Count == 0) return;
        float iconScale = Math.Clamp(cellPx / 5.5f, 0.7f, 1.7f);
        foreach (var battle in data.Battles)
        {
            if (battle.Age > 3.5f) continue;
            var pos = CellToScreen(battle.X, battle.Y, offsetX, offsetY);
            if (!clip.Contains(pos.ToPoint())) continue;
            float t = Math.Clamp(battle.Age / 3.5f, 0f, 1f);
            float fade = 1f - t;

            // Two expanding rings, the second trailing the first
            float r1 = (9f + 30f * MathF.Sqrt(t)) * iconScale;
            MapGfx.Ring(sb, pos, r1, new Color(255, 196, 120, 0) * (0.75f * fade * fade));
            float t2 = Math.Clamp(t * 1.6f - 0.25f, 0f, 1f);
            if (t2 > 0f)
                MapGfx.Ring(sb, pos, (6f + 20f * MathF.Sqrt(t2)) * iconScale, new Color(255, 110, 60, 0) * (0.6f * (1f - t2)));

            // Sparks thrown outwards
            int seed = battle.X * 73 + battle.Y * 151 + battle.Year * 7;
            for (int k = 0; k < 7; k++)
            {
                float ang = Hash01(seed, k) * MathF.PI * 2f;
                float speed = 0.6f + 0.6f * Hash01(seed, k + 40);
                var dir = new Vector2(MathF.Cos(ang), MathF.Sin(ang));
                float dist = (5f + 22f * t * speed) * iconScale;
                var p = pos + dir * dist + new Vector2(0, 10f * t * t * iconScale);
                MapGfx.Line(sb, p - dir * (3.5f * iconScale * fade), p, new Color(255, 226, 150, 0) * fade, 1.4f);
            }
        }
    }
}
