using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace SimPlanet;

/// <summary>
/// Things that move across the map: transport routes and their vehicles, migrations of
/// people, herds/flocks/schools of wild animals and the spy networks between nations.
///
/// Positions come from the <see cref="CivRenderData"/> snapshot and are smoothed on the
/// render side, so vehicles and animals glide even when the simulation updates them in steps.
/// Drawing is capped (routes, vehicles, flows) to keep the frame cost bounded.
/// </summary>
public partial class TerrainRenderer
{
    /// <summary>Show herds, flocks and schools of wild animals (terrain view when zoomed, life and migration views).</summary>
    public bool ShowWildlife { get; set; } = true;

    private const float TerrainTransportZoom = 8f;   // Pixels per cell above which the terrain view shows transport
    private const float TerrainWildlifeZoom = 6f;
    private const int MaxVehiclesDrawn = 700;
    private const int MaxRouteSegments = 16000;
    private const int MaxFlowsDrawn = 400;
    private const int MaxAnimalsDrawn = 400;

    private sealed class RouteGeom
    {
        public int Id;
        public TransportKind Kind;
        public int CivId;
        public float Traffic;
        public bool International;
        public bool BesideRoad;          // Railway sharing its corridor with a road: drawn offset to one side
        public (int, int) CityPair;
        public Vector2[] Points = Array.Empty<Vector2>();   // Cell coordinates, unwrapped (x may leave 0..width)
        public float MinX, MaxX, MinY, MaxY;
        public (int, int, int, int, int) Key;
    }

    private CivRenderData? _geomFor;
    private readonly Dictionary<int, RouteGeom> _routeGeom = new();
    private float _maxTraffic = 1f;

    private readonly Dictionary<int, (Vector2 Pos, float Heading)> _vehicleDisplay = new();
    private readonly Dictionary<int, (Vector2 Pos, float Heading)> _animalDisplay = new();
    private readonly Stopwatch _smoothClock = Stopwatch.StartNew();
    private double _lastSmoothTime;

    /// <summary>Hover targets registered while drawing (flows, networks, herds, vehicles).</summary>
    private readonly List<(Vector2 Pos, float Radius, string Text)> _hoverTargets = new();

    // ------------------------------------------------------------------
    // Entry points called from DrawCityMarkers
    // ------------------------------------------------------------------

    private bool ShowsTransport(float cellPx) =>
        Mode == RenderMode.Infrastructure || (Mode == RenderMode.Terrain && cellPx >= TerrainTransportZoom);

    private bool ShowsWildlife(float cellPx) => ShowWildlife &&
        (Mode is RenderMode.Migrations or RenderMode.Life || (Mode == RenderMode.Terrain && cellPx >= TerrainWildlifeZoom));

    /// <summary>True when this frame has moving things to draw even without any settlement.</summary>
    private bool HasMobilityContent(CivRenderData data)
    {
        float cellPx = CellSize * ZoomLevel;
        return (data.Animals.Count > 0 && ShowsWildlife(cellPx)) || (data.Routes.Count > 0 && ShowsTransport(cellPx));
    }

    /// <summary>Routes, ships, land vehicles, animals, migration flows and spy links (under the settlements).</summary>
    private void DrawMobilityUnderlay(SpriteBatch sb, CivRenderData data, int offsetX, int offsetY, Rectangle clip, float time)
    {
        _hoverTargets.Clear();
        float cellPx = CellSize * ZoomLevel;
        UpdateSmoothing(data);

        if (ShowsTransport(cellPx))
        {
            EnsureRouteGeometry(data);
            DrawRoutes(sb, data, offsetX, offsetY, clip, cellPx, time);
            DrawVehicles(sb, data, offsetX, offsetY, clip, cellPx, time, airborne: false);
        }

        if (ShowsWildlife(cellPx)) DrawAnimals(sb, data, offsetX, offsetY, clip, cellPx, time);

        if (Mode == RenderMode.Migrations) DrawMigrationFlows(sb, data, offsetX, offsetY, clip, time);
        if (Mode == RenderMode.SpyNetworks) DrawSpyNetworks(sb, data, offsetX, offsetY, clip, time);
    }

    /// <summary>Aircraft fly over the settlements.</summary>
    private void DrawMobilityOverlay(SpriteBatch sb, CivRenderData data, int offsetX, int offsetY, Rectangle clip, float time)
    {
        float cellPx = CellSize * ZoomLevel;
        if (ShowsTransport(cellPx)) DrawVehicles(sb, data, offsetX, offsetY, clip, cellPx, time, airborne: true);
    }

    /// <summary>Tooltip for the nearest hover target under the mouse (true when one was shown).</summary>
    private bool TryMobilityTooltip(Point mouse)
    {
        float best = float.MaxValue;
        string? text = null;
        var m = mouse.ToVector2();
        foreach (var (pos, radius, t) in _hoverTargets)
        {
            float d = Vector2.Distance(pos, m);
            if (d <= radius && d < best)
            {
                best = d;
                text = t;
            }
        }
        if (text == null) return false;
        QueueTooltip(text, new Point(mouse.X, mouse.Y + 14));
        return true;
    }

    // ------------------------------------------------------------------
    // Smoothing of positions between snapshots
    // ------------------------------------------------------------------

    private void UpdateSmoothing(CivRenderData data)
    {
        double now = _smoothClock.Elapsed.TotalSeconds;
        float dt = (float)Math.Clamp(now - _lastSmoothTime, 0, 0.25);
        _lastSmoothTime = now;
        float k = 1f - MathF.Exp(-dt * 6f);
        int w = _map.Width;

        void Step(Dictionary<int, (Vector2 Pos, float Heading)> dict, int id, float x, float y, float heading)
        {
            var target = new Vector2(x, y);
            if (!dict.TryGetValue(id, out var cur))
            {
                dict[id] = (target, heading);
                return;
            }
            // Take the short way around the wrapping map; snap on big jumps (route change, load)
            float dx = target.X - cur.Pos.X;
            if (dx > w / 2f) cur.Pos.X += w;
            else if (dx < -w / 2f) cur.Pos.X -= w;
            if (Vector2.DistanceSquared(cur.Pos, target) > 36f)
            {
                dict[id] = (target, heading);
                return;
            }
            float dh = MathF.IEEERemainder(heading - cur.Heading, MathF.PI * 2f);
            dict[id] = (Vector2.Lerp(cur.Pos, target, k), cur.Heading + dh * k);
        }

        foreach (var v in data.Vehicles) Step(_vehicleDisplay, v.Id, v.X, v.Y, v.Heading);
        foreach (var a in data.Animals) Step(_animalDisplay, a.Id, a.X, a.Y, a.Heading);

        Prune(_vehicleDisplay, data.Vehicles.Count, data.Vehicles.Select(v => v.Id));
        Prune(_animalDisplay, data.Animals.Count, data.Animals.Select(a => a.Id));
    }

    private static void Prune(Dictionary<int, (Vector2, float)> dict, int live, IEnumerable<int> ids)
    {
        if (dict.Count <= live * 2 + 64) return;
        var keep = new HashSet<int>(ids);
        foreach (var id in dict.Keys.Where(id => !keep.Contains(id)).ToList()) dict.Remove(id);
    }

    // ------------------------------------------------------------------
    // Route geometry (cached per route while its path is unchanged)
    // ------------------------------------------------------------------

    private void EnsureRouteGeometry(CivRenderData data)
    {
        if (ReferenceEquals(_geomFor, data)) return;
        _geomFor = data;
        int w = _map.Width;
        var live = new HashSet<int>();
        _maxTraffic = 1e-3f;

        foreach (var r in data.Routes)
        {
            live.Add(r.Id);
            _maxTraffic = Math.Max(_maxTraffic, r.Traffic);
            var path = r.Path;
            var key = ((int)r.Kind, path.Length, path[0].X * 1000 + path[0].Y, path[^1].X * 1000 + path[^1].Y, path[path.Length / 2].X * 1000 + path[path.Length / 2].Y);
            if (_routeGeom.TryGetValue(r.Id, out var cached) && cached.Key == key)
            {
                cached.Traffic = r.Traffic;
                cached.CivId = r.CivId;
                cached.International = r.International;
                continue;
            }

            var pts = new List<Vector2>(path.Length);
            float prevX = path[0].X;
            pts.Add(new Vector2(path[0].X, path[0].Y));
            for (int i = 1; i < path.Length; i++)
            {
                float x = path[i].X;
                while (x - prevX > w / 2f) x -= w;
                while (x - prevX < -w / 2f) x += w;
                pts.Add(new Vector2(x, path[i].Y));
                prevX = x;
            }

            Vector2[] geom = r.Kind switch
            {
                TransportKind.AirRoute => new[] { pts[0], pts[^1] },
                TransportKind.SeaLane => Chaikin(Simplify(pts), 2),
                _ => Chaikin(Simplify(pts), 1)
            };

            var g = new RouteGeom
            {
                Id = r.Id, Kind = r.Kind, CivId = r.CivId, Traffic = r.Traffic, International = r.International,
                Points = geom, Key = key, CityPair = (Math.Min(r.FromCityId, r.ToCityId), Math.Max(r.FromCityId, r.ToCityId)),
                MinX = geom.Min(p => p.X), MaxX = geom.Max(p => p.X), MinY = geom.Min(p => p.Y), MaxY = geom.Max(p => p.Y)
            };
            _routeGeom[r.Id] = g;
        }

        if (_routeGeom.Count > live.Count)
            foreach (var id in _routeGeom.Keys.Where(id => !live.Contains(id)).ToList()) _routeGeom.Remove(id);

        // Railways built alongside a road between the same two settlements
        var roads = new HashSet<(int, int)>();
        foreach (var g in _routeGeom.Values)
            if (g.Kind == TransportKind.Road && g.CityPair != (0, 0)) roads.Add(g.CityPair);
        foreach (var g in _routeGeom.Values)
            if (g.Kind == TransportKind.Railway) g.BesideRoad = roads.Contains(g.CityPair);
    }

    /// <summary>Screen distance between a road and the railway running beside it.</summary>
    private static float RailOffset(float cellPx) => Math.Clamp(cellPx * 0.32f, 2.2f, 5f);

    /// <summary>Shifts a polyline sideways (to the left of its direction of travel) by <paramref name="d"/> pixels.</summary>
    private static void OffsetPolyline(Vector2[] pts, float d)
    {
        if (pts.Length < 2) return;
        var normals = new Vector2[pts.Length];
        for (int i = 0; i < pts.Length - 1; i++)
        {
            var dir = pts[i + 1] - pts[i];
            if (dir.LengthSquared() < 1e-6f) continue;
            dir.Normalize();
            var n = new Vector2(-dir.Y, dir.X);
            normals[i] += n;
            normals[i + 1] += n;
        }
        for (int i = 0; i < pts.Length; i++)
        {
            var n = normals[i];
            if (n.LengthSquared() < 1e-6f) continue;
            n.Normalize();
            pts[i] += n * d;
        }
    }

    /// <summary>Left normal of the route segment nearest to a position (cell coordinates).</summary>
    private Vector2 RouteNormalAt(RouteGeom g, Vector2 pos)
    {
        int w = _map.Width;
        float best = float.MaxValue;
        Vector2 normal = Vector2.Zero;
        foreach (float shift in new float[] { 0, w, -w })
        {
            var p = pos + new Vector2(shift, 0);
            for (int i = 0; i < g.Points.Length - 1; i++)
            {
                var a = g.Points[i];
                var ab = g.Points[i + 1] - a;
                float len2 = ab.LengthSquared();
                if (len2 < 1e-6f) continue;
                float t = Math.Clamp(Vector2.Dot(p - a, ab) / len2, 0f, 1f);
                float d = Vector2.DistanceSquared(a + ab * t, p);
                if (d < best)
                {
                    best = d;
                    var dir = ab / MathF.Sqrt(len2);
                    normal = new Vector2(-dir.Y, dir.X);
                }
            }
        }
        return normal;
    }

    /// <summary>Drops points that continue in the same direction (cell paths are mostly straight runs).</summary>
    private static List<Vector2> Simplify(List<Vector2> pts)
    {
        if (pts.Count < 3) return pts;
        var result = new List<Vector2>(pts.Count) { pts[0] };
        for (int i = 1; i < pts.Count - 1; i++)
        {
            var d1 = pts[i] - result[^1];
            var d2 = pts[i + 1] - pts[i];
            if (MathF.Abs(d1.X * d2.Y - d1.Y * d2.X) > 1e-3f || Vector2.Dot(d1, d2) < 0) result.Add(pts[i]);
        }
        result.Add(pts[^1]);
        return result;
    }

    /// <summary>Corner cutting: turns a cell path into a smooth curve.</summary>
    private static Vector2[] Chaikin(List<Vector2> pts, int iterations)
    {
        var cur = pts;
        for (int it = 0; it < iterations && cur.Count >= 3; it++)
        {
            var next = new List<Vector2>(cur.Count * 2) { cur[0] };
            for (int i = 0; i < cur.Count - 1; i++)
            {
                next.Add(Vector2.Lerp(cur[i], cur[i + 1], 0.25f));
                next.Add(Vector2.Lerp(cur[i], cur[i + 1], 0.75f));
            }
            next.Add(cur[^1]);
            cur = next;
        }
        return cur.ToArray();
    }

    /// <summary>
    /// Horizontal shifts at which a route is drawn: as is, plus a copy at the other edge
    /// when it crosses the map seam (like <see cref="ForEachLinkSegment"/>). Off-screen copies are skipped.
    /// </summary>
    private IEnumerable<float> RouteShifts(RouteGeom g, int offsetX, int offsetY, Rectangle clip)
    {
        int w = _map.Width;
        foreach (float shift in new float[] { 0, g.MinX < 0 ? w : float.NaN, g.MaxX >= w ? -w : float.NaN })
        {
            if (float.IsNaN(shift)) continue;
            var a = CellToScreen(g.MinX + shift, g.MinY, offsetX, offsetY);
            var b = CellToScreen(g.MaxX + shift, g.MaxY, offsetX, offsetY);
            var box = new Rectangle((int)a.X - 12, (int)a.Y - 12, (int)(b.X - a.X) + 24, (int)(b.Y - a.Y) + 24);
            if (box.Intersects(clip)) yield return shift;
        }
    }

    private static Color RoadColor(int techLevel) => techLevel >= 20 ? new Color(255, 196, 70)
        : techLevel >= 10 ? new Color(226, 226, 232) : new Color(176, 146, 100);

    private static readonly Color SeaLaneColor = new Color(196, 228, 250);
    private static readonly Color AirRouteColor = new Color(236, 242, 255);

    private void DrawRoutes(SpriteBatch sb, CivRenderData data, int offsetX, int offsetY, Rectangle clip, float cellPx, float time)
    {
        bool full = Mode == RenderMode.Infrastructure;
        var tech = new Dictionary<int, int>();
        foreach (var c in data.Civs) tech[c.Id] = c.TechLevel;
        int budget = MaxRouteSegments;

        // Draw order: sea lanes, roads, railways, then air routes on top
        foreach (var kind in new[] { TransportKind.SeaLane, TransportKind.Road, TransportKind.Railway, TransportKind.AirRoute })
        {
            // The terrain view already shows road cells in its texture
            if (!full && kind == TransportKind.Road) continue;
            foreach (var g in _routeGeom.Values)
            {
                if (g.Kind != kind) continue;
                if (budget <= 0) return;
                float traffic = MathF.Sqrt(Math.Clamp(g.Traffic / _maxTraffic, 0f, 1f));
                foreach (float shift in RouteShifts(g, offsetX, offsetY, clip))
                {
                    var pts = new Vector2[g.Points.Length];
                    for (int i = 0; i < pts.Length; i++) pts[i] = CellToScreen(g.Points[i].X + shift, g.Points[i].Y, offsetX, offsetY);
                    budget -= pts.Length;
                    switch (kind)
                    {
                        case TransportKind.Road:
                        {
                            int t = tech.GetValueOrDefault(g.CivId);
                            float width = (t >= 20 ? 2.2f : t >= 10 ? 1.6f : 1.2f) * (0.85f + 0.5f * traffic);
                            for (int i = 1; i < pts.Length; i++) UITheme.DrawLine(sb, pts[i - 1], pts[i], Color.Black * 0.5f, width + 1.6f);
                            var col = RoadColor(t);
                            for (int i = 1; i < pts.Length; i++) UITheme.DrawLine(sb, pts[i - 1], pts[i], col, width);
                            break;
                        }
                        case TransportKind.Railway:
                            if (g.BesideRoad) OffsetPolyline(pts, RailOffset(cellPx));
                            DrawTrack(sb, pts, cellPx, full ? 1f : 0.85f);
                            break;
                        case TransportKind.SeaLane:
                            DrawDottedPath(sb, pts, SeaLaneColor * ((full ? 0.75f : 0.5f) + 0.25f * traffic), g.International ? 2.2f : 1.7f, 7f, time * 5f + g.Id);
                            break;
                        case TransportKind.AirRoute:
                        {
                            var c = AirArcControl(pts[0], pts[^1]);
                            DrawArc(sb, pts[0], c, pts[^1], AirRouteColor * ((full ? 0.14f : 0.1f) + 0.12f * traffic), 1f, 20);
                            break;
                        }
                    }
                }
            }
        }
    }

    /// <summary>Air routes bulge towards the nearer pole, like great circles on a flat map.</summary>
    private static Vector2 AirArcControl(Vector2 a, Vector2 b)
    {
        Vector2 d = b - a;
        Vector2 n = new Vector2(-d.Y, d.X);
        if (n.Y > 0) n = -n;
        return (a + b) / 2f + n * 0.16f;
    }

    private static void DrawTrack(SpriteBatch sb, Vector2[] pts, float cellPx, float alpha)
    {
        var bed = new Color(34, 28, 26) * alpha;
        var rail = new Color(236, 228, 210) * alpha;
        for (int i = 1; i < pts.Length; i++) UITheme.DrawLine(sb, pts[i - 1], pts[i], bed, 4f);
        if (cellPx >= 9f)
        {
            // Close up: sleepers across a pair of rails
            var tie = new Color(150, 120, 90) * alpha;
            float carry = 0;
            for (int i = 1; i < pts.Length; i++)
            {
                var a = pts[i - 1];
                var d = pts[i] - a;
                float len = d.Length();
                if (len < 0.01f) continue;
                var dir = d / len;
                var n = new Vector2(-dir.Y, dir.X);
                for (float t = carry; t < len; t += 5f)
                {
                    var p = a + dir * t;
                    UITheme.DrawLine(sb, p - n * 2.6f, p + n * 2.6f, tie, 1.2f);
                }
                carry = (carry - len) % 5f;
                if (carry < 0) carry += 5f;
                UITheme.DrawLine(sb, a + n * 1.1f, pts[i] + n * 1.1f, rail, 0.8f);
                UITheme.DrawLine(sb, a - n * 1.1f, pts[i] - n * 1.1f, rail, 0.8f);
            }
        }
        else
        {
            // Classic map railway: dark bed with light dashes
            float carry = 0;
            for (int i = 1; i < pts.Length; i++)
            {
                var a = pts[i - 1];
                var d = pts[i] - a;
                float len = d.Length();
                if (len < 0.01f) continue;
                var dir = d / len;
                for (float t = carry; t < len; t += 10f)
                    UITheme.DrawLine(sb, a + dir * t, a + dir * Math.Min(len, t + 5f), rail, 1.6f);
                carry = (carry - len) % 10f;
                if (carry < 0) carry += 10f;
            }
        }
    }

    private void DrawDottedPath(SpriteBatch sb, Vector2[] pts, Color color, float dot, float spacing, float phase)
    {
        float carry = phase % spacing;
        for (int i = 1; i < pts.Length; i++)
        {
            var a = pts[i - 1];
            var d = pts[i] - a;
            float len = d.Length();
            if (len < 0.01f) continue;
            var dir = d / len;
            float t = carry;
            for (; t < len; t += spacing)
            {
                var p = a + dir * t;
                sb.Draw(_pixelTexture, new Rectangle((int)(p.X - dot / 2), (int)(p.Y - dot / 2), (int)MathF.Ceiling(dot), (int)MathF.Ceiling(dot)), color);
            }
            carry = t - len;
        }
    }

    // ------------------------------------------------------------------
    // Vehicles
    // ------------------------------------------------------------------

    /// <summary>Colour with zero alpha: adds light when drawn with premultiplied blending.</summary>
    private static Color Additive(Color c) => new Color(c.R, c.G, c.B, (byte)0);

    private static bool IsShip(VehicleKind k) => k is VehicleKind.SailingShip or VehicleKind.Steamship or VehicleKind.CargoShip or VehicleKind.Warship;

    private void DrawVehicles(SpriteBatch sb, CivRenderData data, int offsetX, int offsetY, Rectangle clip, float cellPx, float time, bool airborne)
    {
        if (_icons == null || data.Vehicles.Count == 0) return;
        int drawn = 0;
        bool full = Mode == RenderMode.Infrastructure;
        var names = new Dictionary<int, string>();
        foreach (var c in data.Civs) names[c.Id] = c.Name;
        // Busy air corridors would be a swarm of planes: show at most two per route
        var perRoute = airborne ? new Dictionary<int, int>() : null;

        foreach (var v in data.Vehicles)
        {
            bool air = v.Kind == VehicleKind.Airliner;
            if (air != airborne) continue;
            if (!full && v.Kind is VehicleKind.Caravan or VehicleKind.Truck && cellPx < TerrainTransportZoom + 2) continue;
            if (!_vehicleDisplay.TryGetValue(v.Id, out var disp)) disp = (new Vector2(v.X, v.Y), v.Heading);

            Vector2 pos;
            float heading = disp.Heading;
            if (air && _routeGeom.TryGetValue(v.RouteId, out var g) && g.Kind == TransportKind.AirRoute)
            {
                // Fly along the drawn arc: project onto the chord, then onto the curve
                var a0 = g.Points[0];
                var b0 = g.Points[1];
                var p0 = disp.Pos;
                if (p0.X - a0.X > _map.Width / 2f) p0.X -= _map.Width;
                else if (p0.X - a0.X < -_map.Width / 2f) p0.X += _map.Width;
                var ab = b0 - a0;
                float t = ab.LengthSquared() > 1e-4f ? Math.Clamp(Vector2.Dot(p0 - a0, ab) / ab.LengthSquared(), 0f, 1f) : 0f;
                var A = CellToScreen(a0.X, a0.Y, offsetX, offsetY);
                var B = CellToScreen(b0.X, b0.Y, offsetX, offsetY);
                var C = AirArcControl(A, B);
                pos = Bezier(A, C, B, t);
                var tangent = 2 * (1 - t) * (C - A) + 2 * t * (B - C);
                // Keep the travel direction given by the simulation
                if (Vector2.Dot(tangent, new Vector2(MathF.Cos(v.Heading), MathF.Sin(v.Heading))) < 0) tangent = -tangent;
                if (tangent.LengthSquared() > 1e-4f) heading = MathF.Atan2(tangent.Y, tangent.X);
                // The route may be drawn on the other copy of the map
                if (!clip.Contains(pos.ToPoint()))
                {
                    float mapPx = _map.Width * CellSize * ZoomLevel;
                    if (clip.Contains((pos + new Vector2(mapPx, 0)).ToPoint())) pos.X += mapPx;
                    else if (clip.Contains((pos - new Vector2(mapPx, 0)).ToPoint())) pos.X -= mapPx;
                }
            }
            else
            {
                float x = ((disp.Pos.X % _map.Width) + _map.Width) % _map.Width;
                pos = CellToScreen(x, disp.Pos.Y, offsetX, offsetY);
                // Trains keep to their track when it runs beside a road
                if (v.Kind == VehicleKind.Train && _routeGeom.TryGetValue(v.RouteId, out var rail) && rail.BesideRoad)
                    pos += RouteNormalAt(rail, new Vector2(x, disp.Pos.Y)) * RailOffset(cellPx);
            }
            if (!clip.Contains(pos.ToPoint())) continue;
            if (perRoute != null)
            {
                int onRoute = perRoute.GetValueOrDefault(v.RouteId);
                if (onRoute >= (full ? 2 : 1)) continue;
                perRoute[v.RouteId] = onRoute + 1;
            }
            if (++drawn > MaxVehiclesDrawn) break;

            float size = v.Kind switch
            {
                VehicleKind.Airliner => Math.Clamp(cellPx * 2.1f, 10f, 22f),
                VehicleKind.CargoShip or VehicleKind.Warship or VehicleKind.Steamship => Math.Clamp(cellPx * 2.5f, 11f, 24f),
                VehicleKind.SailingShip => Math.Clamp(cellPx * 2.3f, 10f, 22f),
                VehicleKind.Train => Math.Clamp(cellPx * 2.2f, 10f, 20f),
                _ => Math.Clamp(cellPx * 1.8f, 8f, 16f)
            };
            var dir = new Vector2(MathF.Cos(heading), MathF.Sin(heading));
            var nrm = new Vector2(-dir.Y, dir.X);
            Color civColor = GetCivColor(v.CivId);

            if (IsShip(v.Kind))
            {
                // Wake: a white V behind the stern
                var stern = pos - dir * size * 0.4f;
                float wl = size * (1.1f + 0.1f * MathF.Sin(time * 3f + v.Id));
                UITheme.DrawLine(sb, stern, stern - dir * wl + nrm * size * 0.45f, Color.White * 0.35f, 1.2f);
                UITheme.DrawLine(sb, stern, stern - dir * wl - nrm * size * 0.45f, Color.White * 0.35f, 1.2f);
                UITheme.DrawGlow(sb, stern - dir * size * 0.2f, size * 0.35f, new Color(220, 240, 255, 0) * 0.35f);
            }
            else if (air)
            {
                // Contrail fading behind the tail
                var tail = pos - dir * size * 0.42f;
                for (int k = 0; k < 4; k++)
                {
                    float a0 = size * (0.1f + k * 0.6f), a1 = size * (0.1f + (k + 1) * 0.6f);
                    UITheme.DrawLine(sb, tail - dir * a0, tail - dir * a1, Color.White * (0.38f * (1f - k / 4f)), 1.6f - k * 0.25f);
                }
                UITheme.DrawGlow(sb, pos + new Vector2(size * 0.25f, size * 0.35f), size * 0.35f, Color.Black * 0.25f); // shadow
            }
            else if (v.Kind == VehicleKind.Caravan)
            {
                UITheme.DrawGlow(sb, pos - dir * size * 0.5f, size * 0.35f, new Color(190, 160, 110) * 0.25f);
            }
            else if (v.Kind == VehicleKind.Train)
            {
                float puff = (time * 1.4f + v.Id * 0.37f) % 1f;
                var chimney = pos + dir * size * 0.28f;
                UITheme.DrawGlow(sb, chimney - dir * puff * size * 0.8f + nrm * puff * 2f, size * (0.15f + puff * 0.25f), new Color(230, 230, 230) * (0.45f * (1 - puff)));
            }

            var sprite = _icons.VehicleSprite(v.Kind);
            if (sprite != null)
            {
                float scale = size / MapIcons.SmallIconSize;
                var origin = new Vector2(MapIcons.SmallIconSize / 2f);
                sb.Draw(sprite.Value.Base, pos, null, Color.White, heading, origin, scale, SpriteEffects.None, 0f);
                sb.Draw(sprite.Value.Mask, pos, null, civColor, heading, origin, scale, SpriteEffects.None, 0f);
            }

            if (full || cellPx >= TerrainTransportZoom)
                _hoverTargets.Add((pos, Math.Max(6f, size * 0.45f), $"{SocietyStyle.VehicleName(v.Kind)}\nOperated by {names.GetValueOrDefault(v.CivId, "unknown")}"));
        }
    }

    // ------------------------------------------------------------------
    // Wildlife: herds, flocks and schools
    // ------------------------------------------------------------------

    private static Color AnimalColor(LifeForm species, bool flying, bool marine)
    {
        if (marine) return species == LifeForm.MarineDinosaurs ? new Color(120, 150, 150) : new Color(200, 222, 236);
        if (flying) return species == LifeForm.Pterosaurs ? new Color(110, 70, 50) : new Color(28, 28, 34);
        return species switch
        {
            LifeForm.Dinosaurs => new Color(92, 118, 66),
            LifeForm.Reptiles => new Color(104, 132, 72),
            LifeForm.Amphibians => new Color(80, 146, 86),
            LifeForm.Mammals => new Color(112, 78, 52),
            _ => new Color(132, 104, 76)
        };
    }

    private static float Hash01(int a, int b)
    {
        int h = a * 374761393 + b * 668265263 + 1442695;
        h = (h ^ (h >> 13)) * 1274126177;
        h ^= h >> 16;
        return (h & 0xFFFF) / 65535f;
    }

    private void DrawAnimals(SpriteBatch sb, CivRenderData data, int offsetX, int offsetY, Rectangle clip, float cellPx, float time)
    {
        _discTexture ??= BuildDisc(_graphicsDevice, 64);
        int drawn = 0;
        foreach (var a in data.Animals)
        {
            if (!_animalDisplay.TryGetValue(a.Id, out var disp)) disp = (new Vector2(a.X, a.Y), a.Heading);
            float x = ((disp.Pos.X % _map.Width) + _map.Width) % _map.Width;
            var pos = CellToScreen(x, disp.Pos.Y, offsetX, offsetY);
            if (!clip.Contains(pos.ToPoint())) continue;
            if (++drawn > MaxAnimalsDrawn) break;

            int n = Math.Clamp(2 + (int)(MathF.Log2(Math.Max(2, a.Size)) * 0.6f), 3, 10);
            float spread = Math.Clamp(cellPx * 1.9f, 9f, 30f);
            float unit = Math.Clamp(cellPx * 0.3f, 1.8f, 4.5f);
            var dir = new Vector2(MathF.Cos(disp.Heading), MathF.Sin(disp.Heading));
            var nrm = new Vector2(-dir.Y, dir.X);
            var col = AnimalColor(a.Species, a.Flying, a.Marine);

            if (a.Flying)
            {
                // V formation with flapping wings
                for (int k = 0; k < n; k++)
                {
                    int row = (k + 1) / 2;
                    float side = k == 0 ? 0 : (k % 2 == 0 ? 1 : -1);
                    var p = pos - dir * row * spread * 0.3f + nrm * side * row * spread * 0.15f
                            + nrm * MathF.Sin(time * 1.5f + k) * 0.6f;
                    // Gull silhouette seen from above the map: two wings rising and falling
                    float flap = MathF.Sin(time * 9f + k * 1.3f + a.Id);
                    float rise = unit * (0.35f + 0.45f * flap);
                    var tipL = p + new Vector2(-unit * 1.4f, -rise);
                    var tipR = p + new Vector2(unit * 1.4f, -rise);
                    UITheme.DrawLine(sb, p, tipL, col, Math.Max(1f, unit * 0.4f));
                    UITheme.DrawLine(sb, p, tipR, col, Math.Max(1f, unit * 0.4f));
                }
            }
            else if (a.Marine)
            {
                UITheme.DrawGlow(sb, pos, spread * 0.8f, new Color(160, 210, 240, 0) * 0.25f);
                for (int k = 0; k < n; k++)
                {
                    float along = (Hash01(a.Id, k) - 0.5f) * spread * 1.2f;
                    float across = (Hash01(a.Id, k + 31) - 0.5f) * spread * 0.9f;
                    float swim = MathF.Sin(time * 2.2f + k * 0.9f) * 1.2f;
                    var p = pos + dir * (along + swim) + nrm * across;
                    float wig = MathF.Sin(time * 12f + k) * 0.35f;
                    var tail = p - dir * unit;
                    DrawEllipse(sb, p, disp.Heading, unit * 2.4f, Math.Max(1.4f, unit * 0.9f), col);
                    var tailDir = -dir * unit * 0.8f;
                    UITheme.DrawLine(sb, tail, tail + tailDir + nrm * unit * (0.5f + wig), col, 1f);
                    UITheme.DrawLine(sb, tail, tail + tailDir - nrm * unit * (0.5f - wig), col, 1f);
                }
            }
            else
            {
                // Herd: grazing bodies with a trail of dust
                UITheme.DrawGlow(sb, pos - dir * spread * 0.6f, spread * 0.5f, new Color(170, 140, 100) * 0.14f);
                for (int k = 0; k < n; k++)
                {
                    float along = (Hash01(a.Id, k) - 0.5f) * spread * 1.3f;
                    float across = (Hash01(a.Id, k + 17) - 0.5f) * spread * 0.9f;
                    float bob = MathF.Sin(time * 4f + k * 1.7f) * 0.5f;
                    var p = pos + dir * along + nrm * across + new Vector2(0, bob);
                    // Body along the direction of travel, with a head in front
                    DrawEllipse(sb, p + new Vector2(1, 1), disp.Heading, unit * 2.4f, unit * 1.4f, Color.Black * 0.35f);
                    DrawEllipse(sb, p, disp.Heading, unit * 2.4f, unit * 1.4f, col);
                    DrawEllipse(sb, p + dir * unit * 1.35f, disp.Heading, unit * 1.1f, unit * 0.9f, col);
                }
            }

            string kind = a.Flying ? "flock" : a.Marine ? "school" : "herd";
            string name = string.IsNullOrEmpty(a.Name) ? $"{a.Species} {kind}" : a.Name;
            string heading = CompassName(disp.Heading);
            _hoverTargets.Add((pos, spread, $"{name}\n{a.Size:N0} {SpeciesName(a.Species)}\nMigrating {heading}"));
        }
    }

    /// <summary>Filled ellipse rotated to an angle (length along the angle, width across).</summary>
    private void DrawEllipse(SpriteBatch sb, Vector2 center, float angle, float length, float width, Color color)
    {
        if (_discTexture == null) return;
        float s = _discTexture.Width;
        sb.Draw(_discTexture, center, null, color, angle, new Vector2(s / 2f), new Vector2(Math.Max(1f, length) / s, Math.Max(1f, width) / s), SpriteEffects.None, 0f);
    }

    private static string SpeciesName(LifeForm species) => species switch
    {
        LifeForm.MarineDinosaurs => "marine reptiles",
        LifeForm.SimpleAnimals => "animals",
        LifeForm.ComplexAnimals => "animals",
        _ => species.ToString().ToLowerInvariant()
    };

    private static string CompassName(float heading)
    {
        // Screen y points south
        string[] names = { "east", "south-east", "south", "south-west", "west", "north-west", "north", "north-east" };
        int i = (int)MathF.Round(heading / (MathF.PI / 4f));
        return names[((i % 8) + 8) % 8];
    }

    // ------------------------------------------------------------------
    // Migrations view
    // ------------------------------------------------------------------

    /// <summary>Flows from the last ten years (by the newest year seen).</summary>
    private static IEnumerable<CivRenderData.MigrationInfo> RecentFlows(CivRenderData data)
    {
        int from = data.LatestYear - 9;
        return data.Migrations.Where(f => f.Year >= from);
    }

    /// <summary>Net migration per nation over the last decade (immigrants minus emigrants).</summary>
    private static Dictionary<int, (int In, int Out)> NetMigration(CivRenderData data)
    {
        var result = new Dictionary<int, (int In, int Out)>();
        foreach (var f in RecentFlows(data))
        {
            if (f.FromCivId == f.ToCivId) continue;
            if (f.ToCivId > 0) { var v = result.GetValueOrDefault(f.ToCivId); result[f.ToCivId] = (v.In + f.People, v.Out); }
            if (f.FromCivId > 0) { var v = result.GetValueOrDefault(f.FromCivId); result[f.FromCivId] = (v.In, v.Out + f.People); }
        }
        return result;
    }

    private Color GetMigrationTint(CivRenderData data, Dictionary<int, (int In, int Out)> net, int maxAbs, int civId)
    {
        var (i, o) = net.GetValueOrDefault(civId);
        float t = maxAbs > 0 ? (i - o) / (float)maxAbs : 0f;
        return SocietyStyle.NetMigrationColor(MathF.Sign(t) * MathF.Sqrt(MathF.Abs(t)));
    }

    private void DrawMigrationFlows(SpriteBatch sb, CivRenderData data, int offsetX, int offsetY, Rectangle clip, float time)
    {
        if (data.Migrations.Count == 0) return;
        int latest = data.LatestYear;
        var flows = data.Migrations.Where(f => f.Year >= latest - 9).OrderBy(f => f.People).ToList();
        if (flows.Count > MaxFlowsDrawn) flows = flows.Skip(flows.Count - MaxFlowsDrawn).ToList();
        if (flows.Count == 0) return;
        float maxPeople = Math.Max(1, flows.Max(f => f.People));
        float cs = CellSize * ZoomLevel;

        foreach (var f in flows)
        {
            float share = MathF.Sqrt(f.People / maxPeople);
            float thickness = 1.2f + 3.8f * share;
            int age = Math.Max(0, latest - f.Year);
            float alpha = Math.Clamp(1f - age / 12f, 0.3f, 1f);
            var col = SocietyStyle.MigrationColor(f.Kind);
            float bulge = ((int)f.Kind % 2 == 0 ? 0.18f : -0.18f);
            int seed = f.FromX * 31 + f.FromY * 17 + f.ToX * 7 + f.ToY + (int)f.Kind * 101;
            string text = FlowText(data, f);

            ForEachLinkSegment(f.FromX, f.FromY, f.ToX, f.ToY, offsetX, offsetY, clip, (a, b) =>
            {
                float len = Vector2.Distance(a, b);
                if (len < Math.Max(4f, cs * 0.8f))
                {
                    // Local movement (e.g. to the city next door): a pulsing ring
                    float pulse = (time * 0.8f + seed * 0.13f) % 1f;
                    UITheme.DrawGlow(sb, b, 3 + thickness * 2 + pulse * 6, Additive(col) * (0.5f * alpha * (1 - pulse)));
                    _hoverTargets.Add((b, 8f, text));
                    return;
                }
                var d = b - a;
                var n = new Vector2(-d.Y, d.X);
                var c = (a + b) / 2f + n * bulge;
                DrawArc(sb, a, c, b, Color.Black * (0.3f * alpha), thickness + 1.6f, 16);
                DrawArc(sb, a, c, b, col * (0.6f * alpha), thickness, 16);

                // People moving along the arrow
                int dots = 1 + (int)(share * 4);
                for (int j = 0; j < dots; j++)
                {
                    float t = (time * (0.18f + 0.1f * share) + j / (float)dots + (seed % 97) / 97f) % 1f;
                    var p = Bezier(a, c, b, t);
                    UITheme.DrawGlow(sb, p, thickness * 1.5f + 2f, Additive(col) * (0.85f * alpha));
                    UITheme.DrawGlow(sb, p, thickness * 0.6f + 1f, new Color(255, 255, 255, 0) * (0.6f * alpha));
                }

                // Arrowhead
                var tip = b;
                var from = Bezier(a, c, b, 0.9f);
                var dir = tip - from;
                if (dir.LengthSquared() > 0.01f)
                {
                    dir.Normalize();
                    var nn = new Vector2(-dir.Y, dir.X);
                    float hs = 4f + thickness * 1.3f;
                    UITheme.DrawLine(sb, tip, tip - dir * hs + nn * hs * 0.6f, col * alpha, Math.Max(1.5f, thickness * 0.7f));
                    UITheme.DrawLine(sb, tip, tip - dir * hs - nn * hs * 0.6f, col * alpha, Math.Max(1.5f, thickness * 0.7f));
                }

                for (float t = 0.2f; t < 0.9f; t += 0.2f)
                    _hoverTargets.Add((Bezier(a, c, b, t), 6f + thickness, text));
            });
        }
    }

    private static string FlowText(CivRenderData data, CivRenderData.MigrationInfo f)
    {
        string from = data.FindCiv(f.FromCivId)?.Name ?? "unclaimed lands";
        string to = data.FindCiv(f.ToCivId)?.Name ?? "unclaimed lands";
        string route = f.FromCivId == f.ToCivId ? $"within {from}" : $"from {from} to {to}";
        return $"{SocietyStyle.MigrationName(f.Kind)}\n{f.People:N0} people {route}\nYear {f.Year}";
    }

    // ------------------------------------------------------------------
    // Spy networks view
    // ------------------------------------------------------------------

    private void DrawSpyNetworks(SpriteBatch sb, CivRenderData data, int offsetX, int offsetY, Rectangle clip, float time)
    {
        foreach (var net in data.SpyNetworks)
        {
            var owner = data.FindCiv(net.OwnerCivId);
            var target = data.FindCiv(net.TargetCivId);
            if (owner == null || target == null) continue;
            var col = SocietyStyle.MissionColor(net.Mission);
            float alpha = net.Compromised ? 0.6f : 0.95f;
            float thickness = 1.3f + 2.2f * Math.Clamp(net.Strength, 0f, 1f);
            string text = SpyText(data, net, owner.Value, target.Value);
            int seed = net.OwnerCivId * 131 + net.TargetCivId * 17;

            ForEachLinkSegment(owner.Value.CapitalX, owner.Value.CapitalY, target.Value.CapitalX, target.Value.CapitalY, offsetX, offsetY, clip, (a, b) =>
            {
                var d = b - a;
                if (d.LengthSquared() < 4f) return;
                // Not flipped: A->B and B->A bulge on opposite sides
                var c = (a + b) / 2f + new Vector2(-d.Y, d.X) * 0.2f;
                DrawArc(sb, a, c, b, col * (0.3f * alpha), Math.Max(1f, thickness * 0.5f), 20);
                const int steps = 34;
                float phase = (time * 0.6f + seed * 0.01f) % 1f;
                for (int i = 0; i <= steps; i++)
                {
                    float t = (i + phase) / steps;
                    if (t > 1f) continue;
                    var p = Bezier(a, c, b, t);
                    float r = thickness * 0.9f + 1f;
                    sb.Draw(_pixelTexture, new Rectangle((int)(p.X - r - 0.5f), (int)(p.Y - r - 0.5f), (int)(r * 2 + 1), (int)(r * 2 + 1)), Color.Black * (0.45f * alpha));
                    sb.Draw(_pixelTexture, new Rectangle((int)(p.X - r / 2 - 0.5f), (int)(p.Y - r / 2 - 0.5f), (int)Math.Max(1, r + 1), (int)Math.Max(1, r + 1)),
                        (net.Compromised && i % 2 == 0 ? new Color(255, 70, 70) : col) * alpha);
                }

                // Agents travelling to the target
                float tt = (time * 0.12f + (seed % 53) / 53f) % 1f;
                UITheme.DrawGlow(sb, Bezier(a, c, b, tt), 4f + thickness, Additive(col) * 0.9f);

                // Target marker
                UITheme.DrawGlow(sb, b, 7f + thickness * 1.5f, Additive(col) * 0.35f);

                if (net.Compromised)
                {
                    var mid = Bezier(a, c, b, 0.5f);
                    UITheme.DrawGlow(sb, mid, 8f, Color.Black * 0.6f);
                    UITheme.DrawLine(sb, mid + new Vector2(-4, -4), mid + new Vector2(4, 4), new Color(255, 80, 80), 2f);
                    UITheme.DrawLine(sb, mid + new Vector2(-4, 4), mid + new Vector2(4, -4), new Color(255, 80, 80), 2f);
                }

                for (float t = 0.15f; t < 0.95f; t += 0.14f)
                    _hoverTargets.Add((Bezier(a, c, b, t), 5f + thickness, text));
            });
        }
    }

    private static string SpyText(CivRenderData data, CivRenderData.SpyInfo n, CivRenderData.CivInfo owner, CivRenderData.CivInfo target)
    {
        var lines = new List<string>
        {
            $"{owner.Name} spies on {target.Name}",
            SocietyStyle.MissionName(n.Mission),
            $"{n.Agents} agents - strength {n.Strength:P0} - exposure {n.Exposure:P0}",
            $"Successes {n.Successes} - agents lost {n.AgentsLost}"
        };
        if (n.EstablishedYear != 0) lines.Add($"Since year {n.EstablishedYear}");
        if (n.Compromised) lines.Add("COMPROMISED: uncovered by the target");
        return string.Join("\n", lines);
    }

    // ------------------------------------------------------------------
    // Settlement specialization badges
    // ------------------------------------------------------------------

    /// <summary>
    /// Small badge at the foot of a settlement telling what it lives on (harbour crane, mine
    /// headframe, market stall...). Villages show it only when zoomed in; capitals already wear a crown.
    /// Industrial towns smoke; ports have a boat bobbing next to them.
    /// </summary>
    private void DrawSpecializationBadge(SpriteBatch sb, CivRenderData.CityInfo city, Rectangle iconRect, int size, float cellPx, float time)
    {
        if (_icons == null || city.Specialization == CitySpecialization.Capital) return;
        float minCellPx = city.Type == 0 ? 8f : 4.5f;
        if (cellPx < minCellPx) return;
        var badge = _icons.SpecializationBadge(city.Specialization);
        if (badge == null) return;
        _discTexture ??= BuildDisc(_graphicsDevice, 64);

        int bs = Math.Max(10, (int)(size * 0.46f));
        var center = new Vector2(iconRect.Right - bs * 0.2f, iconRect.Bottom - bs * 0.35f);

        if (city.Specialization == CitySpecialization.Industrial)
        {
            // Smoke puffs drifting up from the chimney
            for (int k = 0; k < 3; k++)
            {
                float t = (time * 0.35f + k / 3f + city.X * 0.07f) % 1f;
                var p = center + new Vector2(bs * 0.12f + t * bs * 0.6f, -bs * 0.45f - t * bs * 1.3f);
                UITheme.DrawGlow(sb, p, bs * (0.18f + t * 0.35f), new Color(150, 150, 156) * (0.55f * (1f - t)));
            }
        }

        // Light medallion with a ring in the nation's colour
        var disc = new Rectangle((int)(center.X - bs * 0.6f), (int)(center.Y - bs * 0.6f), (int)(bs * 1.2f), (int)(bs * 1.2f));
        var ring = new Rectangle(disc.X - 2, disc.Y - 2, disc.Width + 4, disc.Height + 4);
        sb.Draw(_discTexture, new Rectangle(ring.X - 1, ring.Y - 1, ring.Width + 2, ring.Height + 2), Color.Black * 0.6f);
        sb.Draw(_discTexture, ring, GetCivColor(city.CivId));
        sb.Draw(_discTexture, disc, new Color(238, 232, 214));
        float bob = city.Specialization is CitySpecialization.Port or CitySpecialization.Fishing ? MathF.Sin(time * 2.2f + city.Y) * 0.8f : 0f;
        int inner = (int)(bs * 0.92f);
        sb.Draw(badge, new Rectangle((int)(center.X - inner / 2f), (int)(center.Y - inner / 2f + bob), inner, inner), Color.White);
    }
}
