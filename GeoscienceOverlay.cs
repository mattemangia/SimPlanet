using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace SimPlanet;

/// <summary>
/// Map overlay for the applied geosciences: oil and gas fields, seismic survey lines, wells
/// and platforms, seismograph stations, seismic wave fronts and the tomographic image.
/// </summary>
public sealed class GeoscienceOverlay
{
    private readonly PlanetMap _map;
    private PetroleumSystem? _oil;
    private SeismologyManager? _seismo;
    private GeothermalSystem? _geothermal;
    private float _time;

    public bool ShowPetroleum { get; set; } = true;
    public bool ShowSeismographs { get; set; } = true;
    public bool ShowTomography { get; set; }

    public GeoscienceOverlay(PlanetMap map)
    {
        _map = map;
    }

    public void SetSystems(PetroleumSystem oil, SeismologyManager seismo, GeothermalSystem geothermal)
    {
        _geothermal = geothermal;
        _oil = oil;
        _seismo = seismo;
    }

    /// <summary>The well or platform under the cursor, if any.</summary>
    public OilWell? WellAt(Point mouse, float originX, float originY, float cellPixels)
    {
        if (_oil == null || !ShowPetroleum) return null;
        float reach = MathF.Max(9f, cellPixels * 0.9f);
        OilWell? best = null;
        float bestDistance = reach;
        foreach (var well in _oil.Wells)
        {
            var p = new Vector2(originX + (well.X + 0.5f) * cellPixels, originY + (well.Y + 0.5f) * cellPixels);
            float d = Vector2.Distance(p, mouse.ToVector2());
            if (d < bestDistance)
            {
                bestDistance = d;
                best = well;
            }
        }
        return best;
    }

    /// <summary>The geothermal plant under the cursor, if any.</summary>
    public GeothermalPlant? PlantAt(Point mouse, float originX, float originY, float cellPixels)
    {
        if (_geothermal == null || !ShowPetroleum) return null;
        float reach = MathF.Max(9f, cellPixels * 0.9f);
        foreach (var plant in _geothermal.Plants)
        {
            var p = new Vector2(originX + (plant.X + 0.5f) * cellPixels, originY + (plant.Y + 0.5f) * cellPixels);
            if (Vector2.Distance(p, mouse.ToVector2()) < reach) return plant;
        }
        return null;
    }

    public void Draw(SpriteBatch sb, float originX, float originY, float cellPixels, Rectangle mapArea, RenderMode mode, float deltaTime, Point mouse)
    {
        _time += deltaTime;
        Vector2 At(float x, float y) => new(originX + (x + 0.5f) * cellPixels, originY + (y + 0.5f) * cellPixels);
        bool Visible(Vector2 p) => p.X > mapArea.X - 20 && p.X < mapArea.Right + 20 && p.Y > mapArea.Y - 20 && p.Y < mapArea.Bottom + 20;

        if (ShowTomography && _seismo != null)
        {
            float block = SeismologyManager.TomoBlock * cellPixels;
            for (int by = 0; by < _seismo.TomoHeight; by++)
            {
                for (int bx = 0; bx < _seismo.TomoWidth; bx++)
                {
                    int b = by * _seismo.TomoWidth + bx;
                    var rect = new Rectangle((int)(originX + bx * block), (int)(originY + by * block), (int)block + 1, (int)block + 1);
                    if (!rect.Intersects(mapArea)) continue;
                    if (_seismo.TomoCoverage[b] <= 0f)
                    {
                        UITheme.FillRect(sb, rect, new Color(10, 12, 18) * 0.55f); // Unresolved: no rays
                        continue;
                    }
                    float anomaly = Math.Clamp((_seismo.TomoVelocity[b] - _seismo.TomoReference) / 1.2f, -1f, 1f);
                    Color color = anomaly >= 0
                        ? Color.Lerp(new Color(232, 232, 226), new Color(40, 80, 200), anomaly)
                        : Color.Lerp(new Color(232, 232, 226), new Color(214, 50, 40), -anomaly);
                    UITheme.FillRect(sb, rect, color * 0.7f);
                }
            }
            var note = new Rectangle(mapArea.X + 12, mapArea.Bottom - 104, 330, 26);
            UITheme.FillRounded(sb, note, new Color(8, 12, 20, 210));
            UITheme.DrawText(sb, $"SEISMIC TOMOGRAPHY   {_seismo.TomoRays} rays   red slow / blue fast   r={_seismo.TomoCorrelation:F2}",
                new Vector2(note.X + 8, note.Y + 6), UITheme.Accent, UITheme.FontSmall);
        }

        if (ShowPetroleum && _oil != null)
        {
            // Fields the nations know about (all of them in the Resources view)
            foreach (var reservoir in _oil.Reservoirs)
            {
                bool known = reservoir.Discovered || reservoir.Surveyed;
                if (!known && mode != RenderMode.Resources) continue;
                var centre = At(reservoir.CenterX, reservoir.CenterY);
                if (!Visible(centre)) continue;
                Color color = reservoir.HasGas && !reservoir.HasOil ? new Color(250, 226, 120) : new Color(30, 26, 22);
                Color rim = reservoir.Discovered ? UITheme.Gold : known ? new Color(120, 240, 255) : Color.White * 0.5f;
                foreach (var (x, y) in reservoir.Cells)
                {
                    var rect = new Rectangle((int)(originX + x * cellPixels), (int)(originY + y * cellPixels), (int)MathF.Ceiling(cellPixels), (int)MathF.Ceiling(cellPixels));
                    UITheme.FillRect(sb, rect, color * (known ? 0.55f : 0.3f));
                    // Outline only on the outer edge of the field
                    if (_oil.GetReservoirAt((x + 1) % _map.Width, y) != reservoir) UITheme.FillRect(sb, new Rectangle(rect.Right - 1, rect.Y, 1, rect.Height), rim);
                    if (_oil.GetReservoirAt((x - 1 + _map.Width) % _map.Width, y) != reservoir) UITheme.FillRect(sb, new Rectangle(rect.X, rect.Y, 1, rect.Height), rim);
                    if (_oil.GetReservoirAt(x, y + 1) != reservoir) UITheme.FillRect(sb, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1), rim);
                    if (_oil.GetReservoirAt(x, y - 1) != reservoir) UITheme.FillRect(sb, new Rectangle(rect.X, rect.Y, rect.Width, 1), rim);
                }
            }

            // Seismic lines: shot part solid, the rest faint, with the crew and its pulse
            foreach (var survey in _oil.Surveys)
            {
                var a = At(survey.X1, survey.Y1);
                var b = At(survey.X2, survey.Y2);
                if (!Visible(a) && !Visible(b)) continue;
                Color cyan = new Color(120, 240, 255);
                if (survey.Completed)
                {
                    UITheme.DrawLine(sb, a, b, cyan * 0.45f, 1.5f);
                    continue;
                }
                var crew = Vector2.Lerp(a, b, Math.Clamp(survey.Progress, 0f, 1f));
                UITheme.DrawLine(sb, a, b, cyan * 0.3f, 1.5f);
                UITheme.DrawLine(sb, a, crew, cyan, 2.5f);
                float ping = (_time * 1.5f) % 1f;
                UITheme.DrawGlow(sb, crew, 6f + ping * 14f, cyan * (1f - ping));
                UITheme.FillRect(sb, new Rectangle((int)crew.X - 3, (int)crew.Y - 3, 6, 6), survey.Marine ? Color.White : UITheme.Gold);
                UITheme.DrawRectOutline(sb, new Rectangle((int)crew.X - 3, (int)crew.Y - 3, 6, 6), Color.Black);
            }

            OilWell? hovered = WellAt(mouse, originX, originY, cellPixels);
            foreach (var well in _oil.Wells)
            {
                var p = At(well.X, well.Y);
                if (!Visible(p)) continue;
                float s = Math.Clamp(cellPixels * 0.9f, 7f, 16f);
                Color body = well.Status switch
                {
                    WellStatus.Producing => new Color(26, 26, 30),
                    WellStatus.Drilling => UITheme.Gold,
                    _ => new Color(130, 130, 134)
                };
                UITheme.DrawGlow(sb, p + new Vector2(1, 2), s * 1.1f, Color.Black * 0.45f);

                if (well.Offshore)
                {
                    // Platform: deck on legs
                    UITheme.DrawLine(sb, p + new Vector2(-s * 0.4f, 0), p + new Vector2(-s * 0.55f, s * 0.6f), Color.White, 1.5f);
                    UITheme.DrawLine(sb, p + new Vector2(s * 0.4f, 0), p + new Vector2(s * 0.55f, s * 0.6f), Color.White, 1.5f);
                    UITheme.FillRect(sb, new Rectangle((int)(p.X - s * 0.6f), (int)(p.Y - s * 0.15f), (int)(s * 1.2f), (int)MathF.Max(3, s * 0.3f)), UITheme.Gold);
                    UITheme.DrawRectOutline(sb, new Rectangle((int)(p.X - s * 0.6f), (int)(p.Y - s * 0.15f), (int)(s * 1.2f), (int)MathF.Max(3, s * 0.3f)), Color.Black);
                    p.Y -= s * 0.15f;
                }

                // Derrick
                var apex = p + new Vector2(0, -s);
                UITheme.DrawLine(sb, p + new Vector2(-s * 0.45f, 0), apex, Color.Black, 3.5f);
                UITheme.DrawLine(sb, p + new Vector2(s * 0.45f, 0), apex, Color.Black, 3.5f);
                UITheme.DrawLine(sb, p + new Vector2(-s * 0.45f, 0), apex, Color.Lerp(body, Color.White, 0.75f), 1.6f);
                UITheme.DrawLine(sb, p + new Vector2(s * 0.45f, 0), apex, Color.Lerp(body, Color.White, 0.75f), 1.6f);
                UITheme.DrawLine(sb, p + new Vector2(-s * 0.25f, -s * 0.45f), p + new Vector2(s * 0.25f, -s * 0.45f), Color.Lerp(body, Color.White, 0.75f), 1.4f);
                if (well.Status == WellStatus.Producing)
                {
                    UITheme.DrawGlow(sb, apex, 5f + 2f * MathF.Sin(_time * 9f + well.Id), new Color(255, 160, 50));
                }
                else if (well.Status == WellStatus.Drilling)
                {
                    UITheme.FillRect(sb, new Rectangle((int)(p.X - s * 0.5f), (int)(p.Y + 3), (int)(s * well.Progress), 2), UITheme.Good);
                }

                if (well == hovered)
                {
                    UITheme.DrawRectOutline(sb, new Rectangle((int)(p.X - s), (int)(p.Y - s * 1.3f), (int)(s * 2), (int)(s * 1.9f)), UITheme.Gold, 1);
                }
            }

            if (hovered != null)
            {
                string kind = hovered.Offshore ? "Platform" : "Oil well";
                string state = hovered.Status switch
                {
                    WellStatus.Drilling => $"drilling, {hovered.Progress:P0}",
                    WellStatus.Producing => "producing",
                    WellStatus.Dry => "dry hole",
                    _ => "depleted"
                };
                UITheme.DrawTooltip(sb, $"{kind} - {state} - target {hovered.TargetDepthKm:F1} km\nClick: open it in Subsurface 3D", mouse, mapArea.Right, mapArea.Bottom);
            }
        }

        if (ShowPetroleum && _geothermal != null)
        {
            var hoveredPlant = PlantAt(mouse, originX, originY, cellPixels);
            foreach (var plant in _geothermal.Plants)
            {
                var p = At(plant.X, plant.Y);
                if (!Visible(p)) continue;
                float s = Math.Clamp(cellPixels * 0.8f, 7f, 14f);
                UITheme.DrawGlow(sb, p, s * 1.5f, new Color(255, 120, 40) * 0.45f); // Hot ground
                var hall = new Rectangle((int)(p.X - s * 0.6f), (int)(p.Y - s * 0.2f), (int)(s * 1.2f), (int)(s * 0.6f));
                UITheme.FillRect(sb, hall, plant.Producing ? new Color(232, 236, 244) : new Color(150, 150, 156));
                UITheme.FillRect(sb, new Rectangle(hall.X, hall.Y, hall.Width, 2), new Color(230, 90, 50));
                UITheme.DrawRectOutline(sb, hall, Color.Black);
                if (plant.Producing)
                {
                    // Steam from the cooling towers
                    for (int k = 0; k < 3; k++)
                    {
                        float t = (_time * 0.5f + k / 3f + plant.Id * 0.37f) % 1f;
                        UITheme.DrawGlow(sb, p + new Vector2(-s * 0.3f + k * s * 0.3f + t * 3f, -s * 0.3f - t * s * 1.3f), 3f + t * 5f, Color.White * (0.55f * (1f - t)));
                    }
                }
                else
                {
                    UITheme.FillRect(sb, new Rectangle(hall.X, hall.Bottom + 2, (int)(hall.Width * plant.Progress), 2), UITheme.Good);
                }
                if (plant == hoveredPlant) UITheme.DrawRectOutline(sb, new Rectangle(hall.X - 4, hall.Y - 8, hall.Width + 8, hall.Height + 12), UITheme.Gold);
            }
            if (hoveredPlant != null)
            {
                string state = hoveredPlant.Producing ? "producing" : $"drilling, {hoveredPlant.Progress:P0}";
                UITheme.DrawTooltip(sb, $"Geothermal plant - {state}\n{hoveredPlant.ReservoirTempC:F0} C at {hoveredPlant.DepthKm:F1} km, heat flow {hoveredPlant.HeatFlow:F0} mW/m2\nClick: open it in Subsurface 3D", mouse, mapArea.Right, mapArea.Bottom);
            }
        }

        if (ShowSeismographs && _seismo != null)
        {
            foreach (var station in _seismo.Stations)
            {
                var p = At(station.X, station.Y) + new Vector2(cellPixels * 0.45f, -cellPixels * 0.45f);
                if (!Visible(p)) continue;
                Color lamp = !_seismo.IsPropagating ? new Color(110, 220, 130)
                    : station.PickS >= 0 ? new Color(255, 110, 90)
                    : station.PickP >= 0 ? new Color(120, 210, 255) : new Color(110, 220, 130);
                UITheme.FillRect(sb, new Rectangle((int)p.X - 4, (int)p.Y - 4, 8, 8), Color.Black);
                UITheme.FillRect(sb, new Rectangle((int)p.X - 3, (int)p.Y - 3, 6, 6), new Color(240, 240, 246));
                // A tiny trace across the box
                UITheme.DrawLine(sb, p + new Vector2(-3, 0), p + new Vector2(-1, -2), lamp, 1f);
                UITheme.DrawLine(sb, p + new Vector2(-1, -2), p + new Vector2(1, 2), lamp, 1f);
                UITheme.DrawLine(sb, p + new Vector2(1, 2), p + new Vector2(3, 0), lamp, 1f);
            }

            if (_seismo.IsPropagating && _seismo.Stations.Count > 0)
            {
                // P and S fronts spreading from the epicentre at their own speeds
                var centre = At(_seismo.EventX, _seismo.EventY);
                var mapClip = Rectangle.Intersect(mapArea, new Rectangle((int)originX, (int)originY, (int)(_map.Width * cellPixels), (int)(_map.Height * cellPixels)));
                DrawCircle(sb, centre, _seismo.ElapsedSeconds * 6.7f / _seismo.CellKm * cellPixels, new Color(120, 210, 255) * 0.8f, mapClip);
                DrawCircle(sb, centre, _seismo.ElapsedSeconds * 3.9f / _seismo.CellKm * cellPixels, new Color(255, 110, 90) * 0.8f, mapClip);
                UITheme.DrawGlow(sb, centre, 10f, UITheme.Gold);
            }
            else if (_seismo.Records.Count > 0 && _seismo.Stations.Count >= 3)
            {
                // Where the network placed the last earthquake
                var last = _seismo.Records[^1];
                if (last.LocatedX >= 0)
                {
                    var p = At(last.LocatedX, last.LocatedY);
                    if (Visible(p))
                    {
                        UITheme.DrawLine(sb, p + new Vector2(-6, -6), p + new Vector2(6, 6), UITheme.Gold, 2f);
                        UITheme.DrawLine(sb, p + new Vector2(-6, 6), p + new Vector2(6, -6), UITheme.Gold, 2f);
                        UITheme.DrawTextOutlined(sb, $"M{last.Magnitude:F1} located", p + new Vector2(9, -7), UITheme.Gold, UITheme.FontSmall);
                    }
                }
            }
        }
    }

    private static void DrawCircle(SpriteBatch sb, Vector2 centre, float radius, Color color, Rectangle clip)
    {
        if (radius < 2f || radius > 6000f) return;
        const int segments = 72;
        var previous = centre + new Vector2(radius, 0);
        for (int s = 1; s <= segments; s++)
        {
            float a = s * MathHelper.TwoPi / segments;
            var point = centre + new Vector2(MathF.Cos(a), MathF.Sin(a)) * radius;
            if (clip.Contains(previous.ToPoint()) && clip.Contains(point.ToPoint())) UITheme.DrawLine(sb, previous, point, color, 1.5f);
            previous = point;
        }
    }
}
