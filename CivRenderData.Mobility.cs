namespace SimPlanet;

/// <summary>
/// Snapshot of transport networks, vehicles, migrations, wildlife herds and spy networks.
/// </summary>
public sealed partial class CivRenderData
{
    public readonly struct RouteInfo
    {
        public int Id { get; init; }
        public TransportKind Kind { get; init; }
        public int CivId { get; init; }
        public (int X, int Y)[] Path { get; init; }
        public float Traffic { get; init; }
        public bool International { get; init; }
        public int FromCityId { get; init; }
        public int ToCityId { get; init; }
    }

    public readonly struct VehicleInfo
    {
        public int Id { get; init; }
        public VehicleKind Kind { get; init; }
        public int CivId { get; init; }
        public int RouteId { get; init; }
        public float X { get; init; }
        public float Y { get; init; }
        public float Heading { get; init; }
    }

    public readonly struct MigrationInfo
    {
        public int FromX { get; init; }
        public int FromY { get; init; }
        public int ToX { get; init; }
        public int ToY { get; init; }
        public int People { get; init; }
        public MigrationKind Kind { get; init; }
        public int FromCivId { get; init; }
        public int ToCivId { get; init; }
        public int Year { get; init; }
    }

    public readonly struct AnimalInfo
    {
        public int Id { get; init; }
        public LifeForm Species { get; init; }
        public string Name { get; init; }
        public float X { get; init; }
        public float Y { get; init; }
        public float Heading { get; init; }
        public int Size { get; init; }
        public bool Flying { get; init; }
        public bool Marine { get; init; }
    }

    public readonly struct SpyInfo
    {
        public int OwnerCivId { get; init; }
        public int TargetCivId { get; init; }
        public int Agents { get; init; }
        public float Strength { get; init; }
        public float Exposure { get; init; }
        public IntelligenceMission Mission { get; init; }
        public int EstablishedYear { get; init; }
        public int Successes { get; init; }
        public int AgentsLost { get; init; }
        public bool Compromised { get; init; }
    }

    public List<RouteInfo> Routes { get; } = new();
    public List<VehicleInfo> Vehicles { get; } = new();
    public List<MigrationInfo> Migrations { get; } = new();
    public List<AnimalInfo> Animals { get; } = new();
    public List<SpyInfo> SpyNetworks { get; } = new();

    /// <summary>Most recent year seen in the migration flows and chronicle (0 when unknown).</summary>
    public int LatestYear { get; private set; }

    private const int MaxRoutes = 4000;
    private const int MaxVehicles = 3000;
    private const int MaxMigrations = 2000;
    private const int MaxAnimals = 1500;

    private static void CaptureMobility(CivRenderData data, CivilizationManager manager)
    {
        foreach (var civ in manager.GetAllCivilizations())
        {
            foreach (var route in civ.TransportRoutes)
            {
                if (data.Routes.Count >= MaxRoutes) break;
                if (route.Path == null || route.Path.Count < 2) continue;
                var path = new (int X, int Y)[route.Path.Count];
                for (int i = 0; i < path.Length; i++) path[i] = (route.Path[i].x, route.Path[i].y);
                data.Routes.Add(new RouteInfo
                {
                    Id = route.Id,
                    Kind = route.Kind,
                    CivId = route.CivilizationId != 0 ? route.CivilizationId : civ.Id,
                    Path = path,
                    Traffic = route.Traffic,
                    International = route.International,
                    FromCityId = route.FromCityId,
                    ToCityId = route.ToCityId
                });
            }

            foreach (var n in civ.SpyNetworks)
            {
                data.SpyNetworks.Add(new SpyInfo
                {
                    OwnerCivId = n.OwnerCivId != 0 ? n.OwnerCivId : civ.Id,
                    TargetCivId = n.TargetCivId,
                    Agents = n.Agents,
                    Strength = n.Strength,
                    Exposure = n.Exposure,
                    Mission = n.Mission,
                    EstablishedYear = n.EstablishedYear,
                    Successes = n.Successes,
                    AgentsLost = n.AgentsLost,
                    Compromised = n.Compromised
                });
            }
        }

        foreach (var v in manager.GetVehicles())
        {
            if (data.Vehicles.Count >= MaxVehicles) break;
            data.Vehicles.Add(new VehicleInfo
            {
                Id = v.Id, Kind = v.Kind, CivId = v.CivilizationId, RouteId = v.RouteId,
                X = v.X, Y = v.Y, Heading = v.Heading
            });
        }

        int latest = 0;
        var flows = manager.GetMigrations();
        // Keep the largest flows when there are too many to draw
        if (flows.Count > MaxMigrations) flows = flows.OrderByDescending(f => f.People).Take(MaxMigrations).ToList();
        foreach (var f in flows)
        {
            latest = Math.Max(latest, f.Year);
            data.Migrations.Add(new MigrationInfo
            {
                FromX = f.FromX, FromY = f.FromY, ToX = f.ToX, ToY = f.ToY, People = f.People, Kind = f.Kind,
                FromCivId = f.FromCivId, ToCivId = f.ToCivId, Year = f.Year
            });
        }

        foreach (var a in manager.GetAnimalGroups())
        {
            if (data.Animals.Count >= MaxAnimals) break;
            data.Animals.Add(new AnimalInfo
            {
                Id = a.Id, Species = a.Species, Name = a.Name ?? "", X = a.X, Y = a.Y, Heading = a.Heading,
                Size = a.Size, Flying = a.Flying, Marine = a.Marine
            });
        }

        if (data.Chronicle.Count > 0) latest = Math.Max(latest, data.Chronicle[^1].Year);
        data.LatestYear = latest;
    }
}
