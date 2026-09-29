namespace SimPlanet;

/// <summary>
/// Transport networks: roads that follow the land (with tunnels through mountains),
/// railways that replace them between important cities, sea lanes between harbours
/// (also to trade partners abroad) and air routes between airports. Caravans, trucks,
/// trains, ships and airliners travel along them; traffic brings trade income.
/// </summary>
public partial class CivilizationManager
{
    private int _nextRouteId = 1;
    private int _nextVehicleId = 1;

    private const int MaxRoadRoutes = 40;
    private const int MaxSeaLanes = 12;
    private const int MaxAirRoutes = 10;
    private const int MaxVehiclesPerNation = 40;

    #region Network building (yearly)

    private void UpdateTransport(Civilization civ, int currentYear)
    {
        // Drop routes whose endpoints are gone (captured, abandoned, drowned)
        var cityIds = civ.Cities.Select(c => c.Id).ToHashSet();
        civ.TransportRoutes.RemoveAll(r =>
            (!cityIds.Contains(r.FromCityId) && !r.International) ||
            FindCity(r.FromCityId, out _) == null || FindCity(r.ToCityId, out _) == null);

        if (civ.TechLevel >= 3 && civ.Cities.Count >= 2) BuildRoadLinks(civ, currentYear);
        if (civ.TechLevel >= 25) UpgradeToRailway(civ, currentYear);
        if (civ.HasSeaTransport) BuildSeaLane(civ, currentYear);
        if (civ.HasAirTransport) BuildAirRoute(civ, currentYear);

        // Traffic and trade income
        float income = 0f;
        foreach (var route in civ.TransportRoutes)
        {
            var a = FindCity(route.FromCityId, out var ownerA);
            var b = FindCity(route.ToCityId, out var ownerB);
            if (a == null || b == null) continue;

            float volume = MathF.Sqrt(Math.Max(1, a.Population) * (float)Math.Max(1, b.Population)) / 1000f;
            float modeFactor = route.Kind switch
            {
                TransportKind.Road => 0.6f,
                TransportKind.Railway => 1.5f,
                TransportKind.SeaLane => 1.3f,
                _ => 1.8f
            };
            bool blocked = ownerA != null && ownerB != null && ownerA.Id != ownerB.Id && AreAtWar(ownerA.Id, ownerB.Id);
            route.Traffic = blocked ? 0f : volume * modeFactor;
            income += route.Traffic * (route.International ? 0.25f : 0.12f);
        }
        civ.Gold += income;
        civ.TradeIncome += income;

        // Keep the legacy railway list (used by older views) in sync with railway routes
        civ.Railroads.Clear();
        foreach (var rail in civ.TransportRoutes.Where(r => r.Kind == TransportKind.Railway))
        {
            for (int i = 1; i < rail.Path.Count; i++)
            {
                var (x1, y1) = rail.Path[i - 1];
                var (x2, y2) = rail.Path[i];
                if (Math.Abs(x1 - x2) <= 1) civ.Railroads.Add((x1, y1, x2, y2));
            }
        }

        UpdateVehicleFleet(civ);
    }

    private RoadType CurrentRoadType(Civilization civ) => civ.TechLevel switch
    {
        >= 20 => RoadType.Highway,
        >= 10 => RoadType.Road,
        _ => RoadType.DirtPath
    };

    private bool HasRoute(Civilization civ, int cityA, int cityB, params TransportKind[] kinds) =>
        civ.TransportRoutes.Any(r => kinds.Contains(r.Kind) &&
                                     ((r.FromCityId == cityA && r.ToCityId == cityB) || (r.FromCityId == cityB && r.ToCityId == cityA)));

    private void BuildRoadLinks(Civilization civ, int currentYear)
    {
        int roads = civ.TransportRoutes.Count(r => r.Kind is TransportKind.Road or TransportKind.Railway);
        if (roads >= MaxRoadRoutes) return;

        // Each year link the largest unconnected settlement to its nearest neighbour
        int built = 0;
        foreach (var city in civ.Cities.OrderByDescending(c => c.Population))
        {
            if (built >= 2) break;

            var nearest = civ.Cities
                .Where(o => o.Id != city.Id && Math.Abs(o.X - city.X) <= _map.Width / 2)
                .Select(o => (city: o, d: WrappedDistance(o.X, o.Y, city.X, city.Y)))
                .Where(o => o.d <= 25 && !HasRoute(civ, city.Id, o.city.Id, TransportKind.Road, TransportKind.Railway))
                .OrderBy(o => o.d)
                .FirstOrDefault();
            if (nearest.city == null) continue;

            var path = FindTerrainPath(city.X, city.Y, nearest.city.X, nearest.city.Y, (x, y) => LandTravelCost(civ, x, y), 12000);
            if (path == null) continue;

            AddRoute(civ, TransportKind.Road, city, nearest.city, path, international: false, currentYear);
            var roadType = CurrentRoadType(civ);
            foreach (var (x, y) in path)
            {
                if (OwnerAt(x, y) == civ.Id) PaveCell(civ, x, y, roadType, currentYear);
            }
            built++;
        }

        // A road to the nearest friendly foreign capital (trade)
        if (built == 0 && civ.Capital != null && _random.NextDouble() < 0.2)
        {
            var capital = civ.Capital;
            var partner = civ.DiplomaticRelations
                .Where(r => r.Value.HasTreaty(TreatyType.TradePact))
                .Select(r => GetCivilizationById(r.Key)?.Capital)
                .Where(c => c != null && WrappedDistance(c.X, c.Y, capital.X, capital.Y) < 40 && Math.Abs(c.X - capital.X) <= _map.Width / 2)
                .OrderBy(c => WrappedDistance(c!.X, c.Y, capital.X, capital.Y))
                .FirstOrDefault();
            if (partner != null && !HasRoute(civ, capital.Id, partner.Id, TransportKind.Road, TransportKind.Railway))
            {
                var path = FindTerrainPath(capital.X, capital.Y, partner.X, partner.Y, (x, y) => LandTravelCost(civ, x, y), 16000);
                if (path != null)
                {
                    AddRoute(civ, TransportKind.Road, capital, partner, path, international: true, currentYear);
                    var roadType = CurrentRoadType(civ);
                    foreach (var (x, y) in path)
                    {
                        if (OwnerAt(x, y) == civ.Id) PaveCell(civ, x, y, roadType, currentYear);
                    }
                }
            }
        }

        // Keep road quality up with technology
        if (currentYear % 10 == 0)
        {
            var roadType = CurrentRoadType(civ);
            foreach (var route in civ.TransportRoutes.Where(r => r.Kind is TransportKind.Road or TransportKind.Railway))
            {
                foreach (var (x, y) in route.Path)
                {
                    if (OwnerAt(x, y) == civ.Id) PaveCell(civ, x, y, roadType, currentYear);
                }
            }
        }
    }

    private void UpgradeToRailway(Civilization civ, int currentYear)
    {
        // One road per year becomes a railway, busiest first, between towns or larger
        var candidate = civ.TransportRoutes
            .Where(r => r.Kind == TransportKind.Road)
            .Where(r =>
            {
                var a = FindCity(r.FromCityId, out _);
                var b = FindCity(r.ToCityId, out _);
                return a != null && b != null && a.Type >= CityType.Town && b.Type >= CityType.Town;
            })
            .OrderByDescending(r => r.Traffic)
            .FirstOrDefault();
        if (candidate == null) return;

        candidate.Kind = TransportKind.Railway;
        candidate.BuiltYear = currentYear;
    }

    private void BuildSeaLane(Civilization civ, int currentYear)
    {
        if (civ.TransportRoutes.Count(r => r.Kind == TransportKind.SeaLane) >= MaxSeaLanes) return;
        if (_random.NextDouble() > 0.35) return;

        var ports = civ.Cities.Where(c => c.Coastal).OrderByDescending(c => c.Population).Take(6).ToList();
        if (ports.Count == 0) return;

        // Foreign harbours of trade partners count as destinations too
        var destinations = new List<(City city, bool foreign)>();
        destinations.AddRange(ports.Select(p => (p, false)));
        foreach (var (otherId, relation) in civ.DiplomaticRelations)
        {
            if (!relation.HasTreaty(TreatyType.TradePact) || relation.Status == DiplomaticStatus.War) continue;
            var other = GetCivilizationById(otherId);
            if (other == null) continue;
            destinations.AddRange(other.Cities.Where(c => c.Coastal).OrderByDescending(c => c.Population).Take(2).Select(c => (c, true)));
        }

        foreach (var from in ports)
        {
            foreach (var (to, foreign) in destinations.OrderBy(d => WrappedDistance(d.city.X, d.city.Y, from.X, from.Y)))
            {
                if (to.Id == from.Id) continue;
                float distance = WrappedDistance(to.X, to.Y, from.X, from.Y);
                if (distance < 6 || distance > 70) continue;
                if (Math.Abs(to.X - from.X) > _map.Width / 2) continue;
                if (HasRoute(civ, from.Id, to.Id, TransportKind.SeaLane)) continue;
                // Short hops along the same coast are served by road
                if (!foreign && distance < 15 && HasRoute(civ, from.Id, to.Id, TransportKind.Road, TransportKind.Railway)) continue;

                var start = AdjacentWater(from.X, from.Y);
                var end = AdjacentWater(to.X, to.Y);
                if (start == null || end == null) continue;

                var path = FindTerrainPath(start.Value.x, start.Value.y, end.Value.x, end.Value.y,
                    (x, y) => _map.Cells[x, y].IsWater && !_map.Cells[x, y].IsIce ? 1f : float.PositiveInfinity, 20000);
                if (path == null) continue;

                path.Insert(0, (from.X, from.Y));
                path.Add((to.X, to.Y));
                AddRoute(civ, TransportKind.SeaLane, from, to, path, foreign, currentYear);
                return;
            }
        }
    }

    private void BuildAirRoute(Civilization civ, int currentYear)
    {
        if (civ.TransportRoutes.Count(r => r.Kind == TransportKind.AirRoute) >= MaxAirRoutes) return;
        if (_random.NextDouble() > 0.4) return;

        var airports = civ.Cities.Where(c => c.HasAirport).OrderByDescending(c => c.Population).ToList();
        if (airports.Count == 0) return;

        var destinations = new List<(City city, bool foreign)>(airports.Select(a => (a, false)));
        foreach (var (otherId, relation) in civ.DiplomaticRelations)
        {
            if (relation.Status == DiplomaticStatus.War || relation.Opinion < 0) continue;
            var other = GetCivilizationById(otherId);
            var hub = other?.Cities.Where(c => c.HasAirport).OrderByDescending(c => c.Population).FirstOrDefault();
            if (hub != null) destinations.Add((hub, true));
        }

        foreach (var from in airports.Take(3))
        {
            foreach (var (to, foreign) in destinations.OrderByDescending(d => d.city.Population))
            {
                if (to.Id == from.Id || HasRoute(civ, from.Id, to.Id, TransportKind.AirRoute)) continue;
                if (Math.Abs(to.X - from.X) > _map.Width / 2) continue;
                if (WrappedDistance(to.X, to.Y, from.X, from.Y) < 15) continue;

                AddRoute(civ, TransportKind.AirRoute, from, to, new List<(int x, int y)> { (from.X, from.Y), (to.X, to.Y) }, foreign, currentYear);
                return;
            }
        }
    }

    private void AddRoute(Civilization civ, TransportKind kind, City from, City to, List<(int x, int y)> path, bool international, int currentYear)
    {
        civ.TransportRoutes.Add(new TransportRoute
        {
            Id = _nextRouteId++,
            Kind = kind,
            CivilizationId = civ.Id,
            FromCityId = from.Id,
            ToCityId = to.Id,
            Path = path,
            International = international,
            BuiltYear = currentYear
        });

        if (international && kind != TransportKind.Road && _random.NextDouble() < 0.5)
        {
            var other = FindCity(to.Id, out var owner) != null ? owner : null;
            string what = kind == TransportKind.SeaLane ? "a sea route" : "an air route";
            if (other != null)
            {
                AddChronicle(currentYear, HistoryCategory.Diplomacy,
                    $"The {civ.Name} open {what} from {from.Name} to {to.Name} ({other.Name})", from.X, from.Y, civ.Id);
            }
        }
    }

    private float LandTravelCost(Civilization civ, int x, int y)
    {
        var cell = _map.Cells[x, y];
        if (cell.IsWater) return float.PositiveInfinity;
        if (cell.IsIce) return 4f;

        float cost = 1f;
        if (cell.Elevation > 0.7f) cost = civ.TechLevel >= 10 ? 2f : 5f; // Tunnels once engineering allows
        else if (cell.Elevation > 0.5f) cost = 2f;
        if (cell.IsForest) cost += 0.5f;
        if (cell.GetGeology().HasRoad) cost *= 0.5f;                  // Reuse existing roads

        int owner = OwnerAt(x, y);
        if (owner != 0 && owner != civ.Id) cost *= 2.5f;                 // Prefer own land
        return cost;
    }

    private (int x, int y)? AdjacentWater(int x, int y)
    {
        foreach (var (nx, ny, neighbor) in _map.GetNeighbors(x, y))
        {
            if (neighbor.IsWater && !neighbor.IsIce) return (nx, ny);
        }
        return null;
    }

    /// <summary>
    /// A* over the map with a caller-supplied cost per cell (infinity = impassable).
    /// </summary>
    private List<(int x, int y)>? FindTerrainPath(int sx, int sy, int tx, int ty, Func<int, int, float> cellCost, int maxExpansions)
    {
        int width = _map.Width;
        int height = _map.Height;
        int start = sy * width + sx;
        int target = ty * width + tx;
        if (start == target) return new List<(int, int)> { (sx, sy) };

        var cameFrom = new Dictionary<int, int>();
        var gScore = new Dictionary<int, float> { [start] = 0f };
        var open = new PriorityQueue<int, float>();
        open.Enqueue(start, WrappedDistance(sx, sy, tx, ty));
        int expansions = 0;

        while (open.Count > 0 && expansions++ < maxExpansions)
        {
            int current = open.Dequeue();
            if (current == target)
            {
                var path = new List<(int x, int y)>();
                while (current != start)
                {
                    path.Add((current % width, current / width));
                    current = cameFrom[current];
                }
                path.Add((sx, sy));
                path.Reverse();
                return path;
            }

            int cx = current % width;
            int cy = current / width;
            float g = gScore[current];

            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0) continue;
                    int nx = WrapX(cx + dx);
                    int ny = cy + dy;
                    if (ny < 0 || ny >= height) continue;

                    int next = ny * width + nx;
                    float cost = next == target ? 1f : cellCost(nx, ny);
                    if (float.IsInfinity(cost)) continue;
                    if (dx != 0 && dy != 0) cost *= 1.41f;

                    float tentative = g + cost;
                    if (gScore.TryGetValue(next, out float existing) && existing <= tentative) continue;
                    gScore[next] = tentative;
                    cameFrom[next] = current;
                    open.Enqueue(next, tentative + WrappedDistance(nx, ny, tx, ty) * 0.8f);
                }
            }
        }
        return null;
    }

    #endregion

    #region Vehicles (continuous)

    private void UpdateVehicleFleet(Civilization civ)
    {
        var routeIds = civ.TransportRoutes.Select(r => r.Id).ToHashSet();
        _vehicles.RemoveAll(v => v.CivilizationId == civ.Id && !routeIds.Contains(v.RouteId));

        // Only the busiest routes get visible vehicles, so the map stays readable
        var busiest = civ.TransportRoutes.OrderByDescending(r => r.Traffic).Take(MaxVehiclesPerNation / 2).Select(r => r.Id).ToHashSet();
        foreach (var route in civ.TransportRoutes)
        {
            if (route.Path.Count < 2) continue;
            var kind = VehicleKindFor(civ, route);
            int wanted = route.Traffic <= 0 || !busiest.Contains(route.Id) ? 0 : Math.Clamp((int)(route.Traffic / 8f) + 1, 1, 2);

            var existing = _vehicles.Where(v => v.RouteId == route.Id).ToList();
            foreach (var v in existing) v.Kind = kind; // Fleets modernise with technology

            for (int i = existing.Count; i < wanted; i++)
            {
                float progress = (float)_random.NextDouble() * (route.Path.Count - 1);
                var (x, y) = route.Path[(int)progress];
                _vehicles.Add(new Vehicle
                {
                    Id = _nextVehicleId++,
                    Kind = kind,
                    CivilizationId = civ.Id,
                    RouteId = route.Id,
                    X = x,
                    Y = y,
                    Progress = progress,
                    Direction = _random.NextDouble() < 0.5 ? 1 : -1
                });
            }
            for (int i = existing.Count - 1; i >= wanted; i--)
            {
                _vehicles.Remove(existing[i]);
            }
        }

        // Warships patrol the sea lanes of nations at war
        if (civ.AtWar)
        {
            var lane = civ.TransportRoutes.FirstOrDefault(r => r.Kind == TransportKind.SeaLane && r.Path.Count > 2);
            if (lane != null && !_vehicles.Any(v => v.CivilizationId == civ.Id && v.Kind == VehicleKind.Warship))
            {
                var (x, y) = lane.Path[lane.Path.Count / 2];
                _vehicles.Add(new Vehicle { Id = _nextVehicleId++, Kind = VehicleKind.Warship, CivilizationId = civ.Id, RouteId = lane.Id, X = x, Y = y, Progress = lane.Path.Count / 2 });
            }
        }
        else
        {
            _vehicles.RemoveAll(v => v.CivilizationId == civ.Id && v.Kind == VehicleKind.Warship);
        }
    }

    private static VehicleKind VehicleKindFor(Civilization civ, TransportRoute route) => route.Kind switch
    {
        TransportKind.Road => civ.TechLevel >= 35 ? VehicleKind.Truck : VehicleKind.Caravan,
        TransportKind.Railway => VehicleKind.Train,
        TransportKind.SeaLane => civ.TechLevel >= 60 ? VehicleKind.CargoShip : civ.TechLevel >= 30 ? VehicleKind.Steamship : VehicleKind.SailingShip,
        _ => VehicleKind.Airliner
    };

    private static float VehicleSpeed(VehicleKind kind) => kind switch
    {
        VehicleKind.Caravan => 0.3f,
        VehicleKind.Truck => 1.0f,
        VehicleKind.Train => 1.5f,
        VehicleKind.SailingShip => 0.6f,
        VehicleKind.Steamship => 1.0f,
        VehicleKind.CargoShip => 1.2f,
        VehicleKind.Warship => 1.2f,
        VehicleKind.Airliner => 4f,
        _ => 1f
    };

    /// <summary>
    /// Move vehicles along their routes (cells per second of game time, capped per step so
    /// they glide rather than teleport at high simulation speeds).
    /// </summary>
    private void UpdateVehicles(float deltaTime)
    {
        if (_vehicles.Count == 0) return;

        var routes = new Dictionary<int, TransportRoute>();
        foreach (var civ in _civilizations)
            foreach (var route in civ.TransportRoutes)
                routes[route.Id] = route;

        foreach (var v in _vehicles)
        {
            if (!routes.TryGetValue(v.RouteId, out var route) || route.Path.Count < 2) continue;

            int last = route.Path.Count - 1;
            float step = Math.Min(VehicleSpeed(v.Kind) * deltaTime, 2f);
            if (route.Kind == TransportKind.AirRoute)
            {
                // Direct flight: progress runs 0..1 scaled by distance
                var (ax, ay) = route.Path[0];
                var (bx, by) = route.Path[last];
                float length = Math.Max(1f, WrappedDistance(ax, ay, bx, by));
                step /= length;
                last = 1;
            }

            v.Progress += step * v.Direction;
            if (v.Progress >= last) { v.Progress = last; v.Direction = -1; }
            if (v.Progress <= 0) { v.Progress = 0; v.Direction = 1; }

            int i = Math.Min((int)v.Progress, last - 1);
            float t = v.Progress - i;
            var (x1, y1) = route.Path[i];
            var (x2, y2) = route.Path[i + 1];
            int dx = x2 - x1;
            if (Math.Abs(dx) > _map.Width / 2) dx -= Math.Sign(dx) * _map.Width;
            float x = x1 + dx * t;
            if (x < 0) x += _map.Width;
            if (x >= _map.Width) x -= _map.Width;
            v.X = x;
            v.Y = y1 + (y2 - y1) * t;
            v.Heading = MathF.Atan2((y2 - y1) * v.Direction, dx * v.Direction);
        }
    }

    #endregion
}
