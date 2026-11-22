using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace SimPlanet;

/// <summary>
/// Territory overlay: every nation's land is framed by a crisp outline in its colour with a
/// soft glow fading inwards, instead of relying on a flat tint of the terrain.
///
/// The overlay is a separate high resolution texture (with mipmaps) stretched over the map.
/// It is rebuilt on a worker thread only when the ownership map changes, so shifting borders
/// during a war never stall the frame.
/// </summary>
public partial class TerrainRenderer
{
    private enum BorderStyle { None, Terrain, Political, Society }

    private sealed class BorderJob
    {
        public int Width, Height, TexW, TexH;
        public int[] Owner = Array.Empty<int>();
        public float[] Field = Array.Empty<float>();
        public BorderStyle Style;
        public long Signature;
        public Color[][] Levels = Array.Empty<Color[]>();
    }

    private Texture2D? _borderTexture;
    private long _borderSignature = -1;          // signature of the texture on screen
    private long _borderRequested = -1;          // signature of the build in flight
    private Task<BorderJob>? _borderTask;
    private DateTime _lastBorderBuild = DateTime.MinValue;
    private BorderStyle _borderStyleOnScreen = BorderStyle.None;
    private const double BorderRebuildIntervalMs = 350;

    private static BorderStyle BorderStyleFor(RenderMode mode) =>
        mode == RenderMode.Terrain ? BorderStyle.Terrain
        : mode == RenderMode.Civilizations ? BorderStyle.Political
        : IsSocietyMode(mode) ? BorderStyle.Society
        : BorderStyle.None;

    /// <summary>
    /// Starts a rebuild of the overlay when territories (or the view) changed. Called from the
    /// texture refresh, i.e. on the draw thread while the simulation lock is held.
    /// </summary>
    private void UpdateBorderOverlay()
    {
        CollectBorderResult();

        var style = BorderStyleFor(Mode);
        if (style == BorderStyle.None || !_hasTerritory) return;

        int w = _map.Width, h = _map.Height;
        long sig = 1469598103934665603L;
        unchecked
        {
            for (int i = 0; i < _civOwnerMap.Length; i++)
                sig = (sig ^ _civOwnerMap[i]) * 1099511628211L;
            sig = (sig ^ (int)style) * 1099511628211L;
        }
        if (sig == _borderSignature || sig == _borderRequested) return;
        if (_borderTask != null) return; // one build at a time; the next refresh picks up the change
        bool styleChanged = style != _borderStyleOnScreen;
        if (!styleChanged && (DateTime.Now - _lastBorderBuild).TotalMilliseconds < BorderRebuildIntervalMs) return;

        // Snapshot what the worker needs. The land/water field is sampled here (not taken from
        // the terrain texture pass) so the overlay never depends on a stale frame.
        var job = new BorderJob
        {
            Width = w, Height = h, Style = style, Signature = sig,
            Owner = (int[])_civOwnerMap.Clone(),
            Field = new float[w * h],
            TexW = OverlaySize(w), TexH = OverlaySize(h)
        };
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                var cell = _map.Cells[x, y];
                var geo = cell.GetGeology();
                bool dryBasin = cell.IsWater && (geo.IsDryBasin || (!geo.IsConnectedToOcean && geo.AccumulatedWater < 0.1f));
                bool water = cell.IsWater && !dryBasin;
                job.Field[y * w + x] = water ? Math.Min(cell.Elevation, -0.002f) : Math.Max(cell.Elevation, 0.002f);
            }
        }

        _borderRequested = sig;
        _lastBorderBuild = DateTime.Now;
        _borderTask = Task.Run(() => BuildBorderOverlay(job));
    }

    /// <summary>Largest power of two that keeps about eight texels per cell within the Reach limit.</summary>
    private static int OverlaySize(int cells)
    {
        int size = 256;
        while (size < 2048 && size < cells * 7) size *= 2;
        return size;
    }

    private void CollectBorderResult()
    {
        if (_borderTask == null || !_borderTask.IsCompleted) return;
        var task = _borderTask;
        _borderTask = null;
        _borderRequested = -1;
        if (task.IsFaulted || task.IsCanceled)
        {
            Console.WriteLine($"Territory overlay build failed: {task.Exception?.GetBaseException().Message}");
            return;
        }

        var job = task.Result;
        try
        {
            if (_borderTexture == null || _borderTexture.Width != job.TexW || _borderTexture.Height != job.TexH)
            {
                _borderTexture?.Dispose();
                _borderTexture = new Texture2D(_graphicsDevice, job.TexW, job.TexH, true, SurfaceFormat.Color);
            }
            int lw = job.TexW, lh = job.TexH;
            for (int level = 0; level < job.Levels.Length && level < _borderTexture.LevelCount; level++)
            {
                _borderTexture.SetData(level, null, job.Levels[level], 0, lw * lh);
                lw = Math.Max(1, lw / 2);
                lh = Math.Max(1, lh / 2);
            }
            _borderSignature = job.Signature;
            _borderStyleOnScreen = job.Style;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Territory overlay upload failed: {ex.Message}");
            _borderTexture?.Dispose();
            _borderTexture = null;
        }
    }

    /// <summary>Draws the territory overlay over the terrain (under roads, routes and settlements).</summary>
    private void DrawTerritoryOverlay(SpriteBatch sb, int offsetX, int offsetY)
    {
        CollectBorderResult();
        if (_borderTexture == null || !_hasTerritory) return;
        // A view change shows the right style as soon as its build lands; never the wrong one
        if (BorderStyleFor(Mode) != _borderStyleOnScreen) return;

        int zoomedWidth = (int)(_map.Width * CellSize * ZoomLevel);
        int zoomedHeight = (int)(_map.Height * CellSize * ZoomLevel);
        var mapRect = new Rectangle(offsetX - (int)CameraX, offsetY - (int)CameraY, zoomedWidth, zoomedHeight);
        sb.Draw(_borderTexture, mapRect, Color.White);
    }

    private void DisposeBorderOverlay()
    {
        _borderTexture?.Dispose();
        _borderTexture = null;
    }

    // ------------------------------------------------------------------
    // Worker: rasterizes the overlay
    // ------------------------------------------------------------------

    private static BorderJob BuildBorderOverlay(BorderJob job)
    {
        int w = job.Width, h = job.Height, tw = job.TexW, th = job.TexH;
        var owner = job.Owner;
        var field = job.Field;
        const int R = 2; // glow reach in cells

        // Cells with a frontier (foreign land or sea) within reach: only these need distances
        var near = new bool[w * h];
        Parallel.For(0, h, y =>
        {
            for (int x = 0; x < w; x++)
            {
                int o = owner[y * w + x];
                if (o <= 0 || field[y * w + x] < 0) continue;
                bool found = false;
                for (int dy = -R; dy <= R && !found; dy++)
                {
                    int ny = y + dy;
                    if (ny < 0 || ny >= h) continue;
                    for (int dx = -R; dx <= R; dx++)
                    {
                        int nx = (x + dx + w) % w;
                        int ni = ny * w + nx;
                        if (field[ni] < 0 || owner[ni] != o) { found = true; break; }
                    }
                }
                near[y * w + x] = found;
            }
        });

        var colors = new Dictionary<int, Vector3>();
        foreach (int o in owner)
        {
            if (o > 0 && !colors.ContainsKey(o))
            {
                var c = GetCivColor(o);
                colors[o] = new Vector3(c.R / 255f, c.G / 255f, c.B / 255f);
            }
        }

        var style = job.Style;
        var px = new Color[tw * th];
        float sx = w / (float)tw, sy = h / (float)th;

        Parallel.For(0, th, py =>
        {
            float cyf = (py + 0.5f) * sy;          // position in cell units (cell y spans [y, y+1])
            float fy = cyf - 0.5f;
            int y0 = (int)MathF.Floor(fy);
            float ty = fy - y0;
            int y1 = Math.Min(h - 1, y0 + 1);
            y0 = Math.Max(0, y0);

            for (int tx0 = 0; tx0 < tw; tx0++)
            {
                float cxf = (tx0 + 0.5f) * sx;
                float fx = cxf - 0.5f;
                int x0 = (int)MathF.Floor(fx);
                float tx = fx - x0;
                int x1 = x0 + 1;
                if (x0 < 0) x0 += w;
                if (x1 >= w) x1 -= w;

                int i00 = y0 * w + x0, i10 = y0 * w + x1, i01 = y1 * w + x0, i11 = y1 * w + x1;
                float w00 = (1 - tx) * (1 - ty), w10 = tx * (1 - ty), w01 = (1 - tx) * ty, w11 = tx * ty;
                float f = field[i00] * w00 + field[i10] * w10 + field[i01] * w01 + field[i11] * w11;
                if (f <= 0) continue; // sea

                // The texel belongs to the nearest land corner (matches the smooth coastline)
                int best = -1; float bw = -1;
                if (field[i00] >= 0 && w00 > bw) { bw = w00; best = i00; }
                if (field[i10] >= 0 && w10 > bw) { bw = w10; best = i10; }
                if (field[i01] >= 0 && w01 > bw) { bw = w01; best = i01; }
                if (field[i11] >= 0 && w11 > bw) { bw = w11; best = i11; }
                if (best < 0) continue;
                int o = owner[best];
                if (o <= 0) continue;

                float dBorder = 9f, dSea = 9f;
                if (near[best])
                {
                    int bx = best % w, by = best / w;
                    // Position relative to the owner cell, robust to the horizontal wrap
                    float lx = cxf - bx;
                    if (lx > w / 2f) lx -= w; else if (lx < -w / 2f) lx += w;
                    float ly = cyf - by;
                    for (int dy = -R; dy <= R; dy++)
                    {
                        int ny = by + dy;
                        if (ny < 0 || ny >= h) continue;
                        float ey = MathF.Max(0f, MathF.Abs(ly - (dy + 0.5f)) - 0.5f);
                        for (int dx = -R; dx <= R; dx++)
                        {
                            if (dx == 0 && dy == 0) continue;
                            int ni = ny * w + (bx + dx + w) % w;
                            bool sea = field[ni] < 0;
                            if (!sea && owner[ni] == o) continue;
                            float ex = MathF.Max(0f, MathF.Abs(lx - (dx + 0.5f)) - 0.5f);
                            float d = MathF.Sqrt(ex * ex + ey * ey);
                            if (sea) { if (d < dSea) dSea = d; }
                            else if (d < dBorder) dBorder = d;
                        }
                    }
                }

                var cc = colors[o];
                float r = 0, g = 0, b = 0, a = 0;
                void Over(float cr, float cg, float cb, float al)
                {
                    if (al <= 0.003f) return;
                    al = MathF.Min(1f, al);
                    r = cr * al + r * (1 - al);
                    g = cg * al + g * (1 - al);
                    b = cb * al + b * (1 - al);
                    a = al + a * (1 - al);
                }

                switch (style)
                {
                    case BorderStyle.Terrain:
                    {
                        // Glow of the nation's colour hugging the frontier, a bright line on it
                        // and a dark seam between neighbours
                        Over(cc.X, cc.Y, cc.Z, 0.20f * MathF.Exp(-dSea / 0.30f));
                        Over(cc.X, cc.Y, cc.Z, 0.58f * MathF.Exp(-dBorder / 0.45f));
                        float line = 1f - Smooth(0.15f, 0.24f, dBorder);
                        Over(MathF.Min(1f, cc.X * 1.08f + 0.06f), MathF.Min(1f, cc.Y * 1.08f + 0.06f), MathF.Min(1f, cc.Z * 1.08f + 0.06f), 0.95f * line);
                        Over(0.03f, 0.04f, 0.07f, 0.75f * (1f - Smooth(0.015f, 0.05f, dBorder)));
                        break;
                    }
                    case BorderStyle.Political:
                    {
                        // Flat colour fill underneath: deepen the interior, light up the rim,
                        // then a dark frontier line so neighbouring colours never bleed together
                        float d = MathF.Min(dBorder, dSea);
                        Over(0.04f, 0.06f, 0.12f, 0.30f * Smooth(0.1f, 1.6f, d));
                        Over(1f, 1f, 1f, 0.26f * MathF.Exp(-MathF.Abs(dBorder - 0.3f) / 0.2f));
                        Over(1f, 1f, 1f, 0.14f * MathF.Exp(-dSea / 0.3f));
                        float line = 1f - Smooth(0.10f, 0.17f, dBorder);
                        Over(cc.X * 0.28f, cc.Y * 0.28f, cc.Z * 0.28f + 0.03f, 0.92f * line);
                        break;
                    }
                    default:
                    {
                        // Society views colour nations by a statistic: neutral embossed frontier
                        Over(1f, 1f, 1f, 0.20f * MathF.Exp(-MathF.Abs(dBorder - 0.28f) / 0.2f));
                        Over(1f, 1f, 1f, 0.10f * MathF.Exp(-dSea / 0.3f));
                        float line = 1f - Smooth(0.09f, 0.16f, dBorder);
                        Over(0.03f, 0.05f, 0.09f, 0.85f * line);
                        break;
                    }
                }

                if (a <= 0.003f) continue;
                // Fade out right at the shore so the overlay never spills onto the surf
                float shore = MathF.Min(1f, f / 0.006f);
                r *= shore; g *= shore; b *= shore; a *= shore;
                px[py * tw + tx0] = new Color(r, g, b, a); // premultiplied
            }
        });

        // Mipmap chain (box filter), so thin frontier lines stay smooth when zoomed out
        var levels = new List<Color[]> { px };
        int lw = tw, lh = th;
        var cur = px;
        while (lw > 1 || lh > 1)
        {
            int nw = Math.Max(1, lw / 2), nh = Math.Max(1, lh / 2);
            var next = new Color[nw * nh];
            int cw = lw, chh = lh;
            var src = cur;
            Parallel.For(0, nh, y =>
            {
                int ya = Math.Min(chh - 1, y * 2), yb = Math.Min(chh - 1, y * 2 + 1);
                for (int x = 0; x < nw; x++)
                {
                    int xa = Math.Min(cw - 1, x * 2), xb = Math.Min(cw - 1, x * 2 + 1);
                    var c0 = src[ya * cw + xa]; var c1 = src[ya * cw + xb];
                    var c2 = src[yb * cw + xa]; var c3 = src[yb * cw + xb];
                    next[y * nw + x] = new Color(
                        (byte)((c0.R + c1.R + c2.R + c3.R + 2) / 4), (byte)((c0.G + c1.G + c2.G + c3.G + 2) / 4),
                        (byte)((c0.B + c1.B + c2.B + c3.B + 2) / 4), (byte)((c0.A + c1.A + c2.A + c3.A + 2) / 4));
                }
            });
            levels.Add(next);
            cur = next;
            lw = nw;
            lh = nh;
        }
        job.Levels = levels.ToArray();
        return job;
    }

    private static float Smooth(float edge0, float edge1, float x)
    {
        float t = Math.Clamp((x - edge0) / (edge1 - edge0), 0f, 1f);
        return t * t * (3f - 2f * t);
    }
}
