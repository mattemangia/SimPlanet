namespace SimPlanet;

/// <summary>
/// Immutable per-frame snapshot of the civilization data needed for rendering.
///
/// The snapshot is taken on the render thread while the map data lock is held, so the
/// renderer and UI can draw cities, armies, networks and national data without touching
/// the live collections that the simulation thread keeps modifying.
/// </summary>
public sealed partial class CivRenderData
{
    public readonly struct CityInfo
    {
        public int X { get; init; }
        public int Y { get; init; }
        public string Name { get; init; }
        public int Population { get; init; }
        public int Type { get; init; }           // 0 Village, 1 Town, 2 City, 3 Metropolis
        public int CivId { get; init; }
        public bool IsCapital { get; init; }
        public bool HasWalls { get; init; }
        public bool UnderSiege { get; init; }
        public float SiegeProgress { get; init; }
        public bool Starving { get; init; }
        public string Buildings { get; init; }
        public bool HasHarbor { get; init; }
        public bool Electrified { get; init; }
        public bool Online { get; init; }
        public bool HasAirport { get; init; }
        public bool HasSpaceport { get; init; }
        public bool HasPowerPlant { get; init; }
        public EnergySource? PowerPlantType { get; init; }
    }

    public readonly struct ArmyInfo
    {
        public int Id { get; init; }
        public int CivId { get; init; }
        public float X { get; init; }
        public float Y { get; init; }
        public int Soldiers { get; init; }
        public string State { get; init; }
        public int TargetX { get; init; }
        public int TargetY { get; init; }
        public int TargetCivId { get; init; }
        public bool IsGarrison { get; init; }
        public float Morale { get; init; }
    }

    public readonly struct BattleInfo
    {
        public int X { get; init; }
        public int Y { get; init; }
        public int Year { get; init; }
        public int AttackerCivId { get; init; }
        public int DefenderCivId { get; init; }
        public int Casualties { get; init; }
        public float Age { get; init; }
    }

    public readonly struct HistoryInfo
    {
        public int Year { get; init; }
        public string Text { get; init; }
        public string Category { get; init; }
        public int X { get; init; }
        public int Y { get; init; }
        public int CivId { get; init; }
    }

    public readonly struct CivInfo
    {
        public int Id { get; init; }
        public string Name { get; init; }
        public int Population { get; init; }
        public int TechLevel { get; init; }
        public string CivType { get; init; }
        public bool AtWar { get; init; }
        public int? WarTargetId { get; init; }
        public int CityCount { get; init; }
        public int TerritorySize { get; init; }
        public float Gold { get; init; }
        public float WarWeariness { get; init; }
        public float Stability { get; init; }
        public float Prosperity { get; init; }
        public string Government { get; init; }
        public int MilitaryStrength { get; init; }

        // People and development
        public int CenterX { get; init; }
        public int CenterY { get; init; }
        public int CapitalX { get; init; }
        public int CapitalY { get; init; }
        public string Ethnicity { get; init; }
        public HomelandClimate Homeland { get; init; }
        public float DevelopmentModifier { get; init; }
        public float GoldIncome { get; init; }

        // Government
        public GovernmentType? GovType { get; init; }
        public string RulerName { get; init; }
        public string RulerTitle { get; init; }
        public int RulerAge { get; init; }
        public string RulingParty { get; init; }
        public int NextElectionYear { get; init; }

        // Energy and networks
        public (EnergySource Source, float Share)[] EnergyMix { get; init; }
        public EnergySource? DominantEnergy { get; init; }
        public float EnergyProduction { get; init; }
        public float EnergyDemand { get; init; }
        public float Electrification { get; init; }
        public float InternetPenetration { get; init; }

        // Strategic forces
        public bool HasNuclearWeapons { get; init; }
        public int NuclearWarheads { get; init; }
        public int ChemicalStockpile { get; init; }
        public bool BioweaponProgram { get; init; }
        public int MissileSilos { get; init; }
        public int SpySatellites { get; init; }
        public bool MissileDefense { get; init; }
        public int Soldiers { get; init; }
        public int ArmyCount { get; init; }

        // Space and projects
        public SpaceStage SpaceStage { get; init; }
        public int Satellites { get; init; }
        public int Astronauts { get; init; }
        public string ActiveProjectName { get; init; }
        public float ActiveProjectFraction { get; init; }

        // Strategy (what the nation is trying to do, and why)
        public NationalPosture Posture { get; init; }
        public string PostureRationale { get; init; }
        public float ThreatLevel { get; init; }
        public int? MainThreatId { get; init; }
        public int? ConquestTargetId { get; init; }
        public float ExistentialThreat { get; init; }
        public float MilitaryShare { get; init; }
        public float ResearchShare { get; init; }
        public float EconomyShare { get; init; }
        public float SpaceShare { get; init; }
        public int PostureSinceYear { get; init; }

        /// <summary>Heavier per-nation data used by the nation detail panel.</summary>
        public NationDetail Detail { get; init; }
    }

    public readonly struct RulerInfo
    {
        public int Id { get; init; }
        public string Name { get; init; }
        public string Title { get; init; }
        public int Age { get; init; }
        public int? ParentId { get; init; }
        public int[] ChildrenIds { get; init; }
        public int DynastyId { get; init; }
        public bool IsAlive { get; init; }
        public int YearTookPower { get; init; }
        public int? DeathYear { get; init; }
        public float Wisdom { get; init; }
        public float Charisma { get; init; }
        public float Ambition { get; init; }
        public float Brutality { get; init; }
        public float Piety { get; init; }
    }

    public readonly struct PartyInfo
    {
        public string Name { get; init; }
        public Ideology Ideology { get; init; }
        public float Support { get; init; }
        public int SeatsWon { get; init; }
        public int FoundedYear { get; init; }
    }

    public readonly struct ProjectInfo
    {
        public string Name { get; init; }
        public ProjectKind Kind { get; init; }
        public float Fraction { get; init; }
        public int StartedYear { get; init; }
        public int CompletedYear { get; init; }
    }

    public readonly struct ElectionInfo
    {
        public int Year { get; init; }
        public string WinningParty { get; init; }
        public float WinningShare { get; init; }
        public string LeaderName { get; init; }
    }

    public readonly struct RelationInfo
    {
        public int OtherCivId { get; init; }
        public DiplomaticStatus Status { get; init; }
        public float Opinion { get; init; }
    }

    public readonly struct DynastyInfo
    {
        public int Id { get; init; }
        public string Name { get; init; }
        public int FoundedYear { get; init; }
        public int FounderId { get; init; }
        public bool Extinct { get; init; }
        public int Generations { get; init; }
    }

    /// <summary>Per-nation data only needed by the nation detail panel.</summary>
    public sealed class NationDetail
    {
        public int CurrentRulerId { get; init; } = -1;
        public int HeirApparentId { get; init; } = -1;
        public int EstablishedYear { get; init; }
        public bool IsHereditary { get; init; }
        public bool IsElected { get; init; }
        public float GovStability { get; init; }
        public float Legitimacy { get; init; }
        public float Corruption { get; init; }
        public List<RulerInfo> Rulers { get; } = new();
        public List<RulerInfo> SuccessionLine { get; } = new();
        public List<DynastyInfo> Dynasties { get; } = new();
        public List<PartyInfo> Parties { get; } = new();
        public List<ElectionInfo> Elections { get; } = new();
        public List<ProjectInfo> CompletedProjects { get; } = new();
        public ProjectInfo? ActiveProject { get; init; }
        public List<RelationInfo> Relations { get; } = new();
        public int NuclearStrikesLaunched { get; init; }
        public int ChemicalAttacks { get; init; }
        public int BioweaponReleases { get; init; }
        public int WarCasualties { get; init; }
        public int CitiesConquered { get; init; }
        public int CitiesLost { get; init; }
        public int Founded { get; init; }
        public float FoodProduction { get; init; }
        public float FoodConsumption { get; init; }
        public float TradeIncome { get; init; }
        public int RoadCells { get; init; }
    }

    /// <summary>A straight network link between two cells (power line, cable, railroad, trade route).</summary>
    public readonly struct LinkInfo
    {
        public int CivId { get; init; }
        public int X1 { get; init; }
        public int Y1 { get; init; }
        public int X2 { get; init; }
        public int Y2 { get; init; }
    }

    public readonly struct OrbitalInfo
    {
        public int Id { get; init; }
        public int CivId { get; init; }
        public string Name { get; init; }
        public OrbitalObjectType Type { get; init; }
        public int Crew { get; init; }
        public float OrbitAngle { get; init; }
        public float OrbitRadius { get; init; }
        public int LaunchedYear { get; init; }
        public bool Orphaned { get; init; }
    }

    public readonly struct DiseaseInfo
    {
        public int Id { get; init; }
        public string Name { get; init; }
        public string Type { get; init; }
        public int TotalInfected { get; init; }
        public int TotalDeaths { get; init; }
        public float Lethality { get; init; }
        public float CureProgress { get; init; }
    }

    public readonly struct InfectionInfo
    {
        public int DiseaseId { get; init; }
        public int CivId { get; init; }
        public int Infected { get; init; }
        public int Dead { get; init; }
        public float Share { get; init; }          // Infected / population, 0-1
        public bool Quarantine { get; init; }
        public bool BordersClosed { get; init; }
    }

    public static readonly CivRenderData Empty = new CivRenderData();

    /// <summary>Most recent snapshot taken by the renderer (for UI panels).</summary>
    public static CivRenderData Latest { get; private set; } = Empty;

    public List<CivInfo> Civs { get; } = new();
    public List<CityInfo> Cities { get; } = new();
    public List<ArmyInfo> Armies { get; } = new();
    public List<BattleInfo> Battles { get; } = new();
    public List<HistoryInfo> Chronicle { get; } = new();
    public bool HasChronicleSupport { get; private set; }

    public List<LinkInfo> PowerLines { get; } = new();
    public List<LinkInfo> DataCables { get; } = new();
    public List<LinkInfo> Railroads { get; } = new();
    public List<LinkInfo> TradeRoutes { get; } = new();
    /// <summary>Cells chosen to show each nation's missile silos (deterministic, inside its territory).</summary>
    public List<(int CivId, int X, int Y)> SiloSites { get; } = new();
    public List<OrbitalInfo> Orbitals { get; } = new();
    public int PeopleInSpace { get; private set; }
    public float NuclearWinter { get; private set; }
    public List<DiseaseInfo> Diseases { get; } = new();
    public List<InfectionInfo> Infections { get; } = new();

    public CivInfo? FindCiv(int id)
    {
        foreach (var c in Civs) if (c.Id == id) return c;
        return null;
    }

    // Battles fade out in real time, whatever the simulation speed
    private static readonly Dictionary<(int x, int y, int year, int casualties), long> _battleFirstSeen = new();
    private static readonly System.Diagnostics.Stopwatch _clock = System.Diagnostics.Stopwatch.StartNew();

    /// <summary>Optional development hook; has no implementation in normal builds.</summary>
    static partial void DevInject(CivilizationManager manager);

    /// <summary>
    /// Builds a snapshot. Must be called while the simulation is not mutating civilization
    /// data (i.e. while holding the map data lock). Never throws.
    /// </summary>
    public static CivRenderData Capture(CivilizationManager? manager, int maxChronicle = 200, DiseaseManager? diseases = null)
    {
        var data = new CivRenderData { HasChronicleSupport = manager != null };
        if (manager == null) return data;

        try
        {
            DevInject(manager);

            var soldiers = new Dictionary<int, (int soldiers, int armies)>();
            var armyList = manager.GetArmies();
            foreach (var army in armyList)
            {
                var s0 = soldiers.GetValueOrDefault(army.CivilizationId);
                soldiers[army.CivilizationId] = (s0.soldiers + army.Soldiers, s0.armies + 1);
            }

            foreach (var civ in manager.GetAllCivilizations())
            {
                var mix = civ.EnergyMix.Where(kv => kv.Value > 0.001f)
                    .OrderByDescending(kv => kv.Value)
                    .Select(kv => (kv.Key, kv.Value)).ToArray();
                var capital = civ.Capital;
                var ruler = civ.Government?.CurrentRuler;
                var armies = soldiers.GetValueOrDefault(civ.Id);

                data.Civs.Add(new CivInfo
                {
                    Id = civ.Id,
                    Name = civ.Name,
                    Population = civ.Population,
                    TechLevel = civ.TechLevel,
                    CivType = civ.CivType.ToString(),
                    AtWar = civ.AtWar,
                    WarTargetId = civ.WarTargetId,
                    CityCount = civ.Cities.Count,
                    TerritorySize = civ.Territory.Count,
                    Gold = civ.Gold,
                    WarWeariness = civ.WarWeariness,
                    Stability = civ.Stability,
                    Prosperity = civ.Prosperity,
                    Government = civ.Government?.Type.ToString() ?? "",
                    MilitaryStrength = civ.MilitaryStrength,

                    CenterX = civ.CenterX,
                    CenterY = civ.CenterY,
                    CapitalX = capital?.X ?? civ.CenterX,
                    CapitalY = capital?.Y ?? civ.CenterY,
                    Ethnicity = civ.Ethnicity ?? "",
                    Homeland = civ.Homeland,
                    DevelopmentModifier = civ.DevelopmentModifier,
                    GoldIncome = civ.GoldIncome,

                    GovType = civ.Government?.Type,
                    RulerName = ruler?.Name ?? "",
                    RulerTitle = !string.IsNullOrEmpty(ruler?.Title) ? ruler!.Title : civ.Government?.RulerTitle ?? "",
                    RulerAge = ruler?.Age ?? 0,
                    RulingParty = civ.RulingParty ?? "",
                    NextElectionYear = civ.NextElectionYear,

                    EnergyMix = mix,
                    DominantEnergy = mix.Length > 0 ? mix[0].Key : null,
                    EnergyProduction = civ.EnergyProduction,
                    EnergyDemand = civ.EnergyDemand,
                    Electrification = civ.Electrification,
                    InternetPenetration = civ.InternetPenetration,

                    HasNuclearWeapons = civ.HasNuclearWeapons || civ.Arsenal.NuclearWarheads > 0,
                    NuclearWarheads = Math.Max(civ.Arsenal.NuclearWarheads, civ.NuclearStockpile),
                    ChemicalStockpile = civ.Arsenal.ChemicalStockpile,
                    BioweaponProgram = civ.Arsenal.BioweaponProgram,
                    MissileSilos = civ.Arsenal.MissileSilos,
                    SpySatellites = civ.Arsenal.SpySatellites,
                    MissileDefense = civ.Arsenal.MissileDefense,
                    Soldiers = armies.soldiers,
                    ArmyCount = armies.armies,

                    SpaceStage = civ.SpaceStage,
                    Satellites = civ.Satellites,
                    Astronauts = civ.Astronauts,
                    ActiveProjectName = civ.ActiveProject?.Name ?? "",
                    ActiveProjectFraction = civ.ActiveProject?.Fraction ?? 0f,

                    Posture = civ.Strategy.Posture,
                    PostureRationale = civ.Strategy.Rationale ?? "",
                    ThreatLevel = civ.Strategy.ThreatLevel,
                    MainThreatId = civ.Strategy.MainThreatId,
                    ConquestTargetId = civ.Strategy.ConquestTargetId,
                    ExistentialThreat = civ.Strategy.ExistentialThreat,
                    MilitaryShare = civ.Strategy.MilitaryShare,
                    ResearchShare = civ.Strategy.ResearchShare,
                    EconomyShare = civ.Strategy.EconomyShare,
                    SpaceShare = civ.Strategy.SpaceShare,
                    PostureSinceYear = civ.Strategy.PostureSinceYear,

                    Detail = CaptureDetail(civ)
                });

                foreach (var l in civ.PowerLines)
                    data.PowerLines.Add(new LinkInfo { CivId = civ.Id, X1 = l.x1, Y1 = l.y1, X2 = l.x2, Y2 = l.y2 });
                foreach (var l in civ.DataCables)
                    data.DataCables.Add(new LinkInfo { CivId = civ.Id, X1 = l.x1, Y1 = l.y1, X2 = l.x2, Y2 = l.y2 });
                foreach (var l in civ.Railroads)
                    data.Railroads.Add(new LinkInfo { CivId = civ.Id, X1 = l.x1, Y1 = l.y1, X2 = l.x2, Y2 = l.y2 });
                // Trade routes are stored as the partner's centre: draw them from our capital
                foreach (var (tx, ty) in civ.TradeRoutes)
                    data.TradeRoutes.Add(new LinkInfo { CivId = civ.Id, X1 = capital?.X ?? civ.CenterX, Y1 = capital?.Y ?? civ.CenterY, X2 = tx, Y2 = ty });

                AddSiloSites(data, civ);

                foreach (var city in civ.Cities)
                {
                    data.Cities.Add(new CityInfo
                    {
                        X = city.X,
                        Y = city.Y,
                        Name = city.Name,
                        Population = city.Population,
                        Type = (int)city.Type,
                        CivId = civ.Id,
                        IsCapital = city.IsCapital,
                        HasWalls = city.Has(CityBuilding.Walls),
                        UnderSiege = city.UnderSiege,
                        SiegeProgress = city.SiegeProgress,
                        Starving = city.Starving,
                        Buildings = city.Buildings == CityBuilding.None ? "" : city.Buildings.ToString(),
                        HasHarbor = city.Has(CityBuilding.Harbor),
                        Electrified = city.Electrified,
                        Online = city.Online,
                        HasAirport = city.HasAirport,
                        HasSpaceport = city.HasSpaceport,
                        HasPowerPlant = city.HasPowerPlant,
                        PowerPlantType = city.PowerPlantType
                    });
                }
            }

            foreach (var army in armyList)
            {
                data.Armies.Add(new ArmyInfo
                {
                    Id = army.Id,
                    CivId = army.CivilizationId,
                    X = army.X,
                    Y = army.Y,
                    Soldiers = army.Soldiers,
                    State = army.State.ToString(),
                    TargetX = army.TargetX,
                    TargetY = army.TargetY,
                    TargetCivId = army.TargetCivId,
                    IsGarrison = army.IsGarrison,
                    Morale = army.Morale
                });
            }

            long now = _clock.ElapsedMilliseconds;
            var seen = new HashSet<(int, int, int, int)>();
            foreach (var battle in manager.GetRecentBattles())
            {
                var key = (battle.X, battle.Y, battle.Year, battle.Casualties);
                seen.Add(key);
                if (!_battleFirstSeen.TryGetValue(key, out long firstSeen))
                {
                    firstSeen = now;
                    _battleFirstSeen[key] = now;
                }

                data.Battles.Add(new BattleInfo
                {
                    X = battle.X,
                    Y = battle.Y,
                    Year = battle.Year,
                    AttackerCivId = battle.AttackerCivId,
                    DefenderCivId = battle.DefenderCivId,
                    Casualties = battle.Casualties,
                    Age = (now - firstSeen) / 1000f
                });
            }
            foreach (var stale in _battleFirstSeen.Keys.Where(k => !seen.Contains(k)).ToList())
            {
                _battleFirstSeen.Remove(stale);
            }

            var chronicle = manager.GetChronicle();
            int start = Math.Max(0, chronicle.Count - maxChronicle);
            for (int i = start; i < chronicle.Count; i++)
            {
                var ev = chronicle[i];
                data.Chronicle.Add(new HistoryInfo
                {
                    Year = ev.Year,
                    Text = ev.Text,
                    Category = ev.Category.ToString(),
                    X = ev.X,
                    Y = ev.Y,
                    CivId = ev.CivilizationId
                });
            }

            foreach (var o in manager.GetOrbitalObjects())
            {
                data.Orbitals.Add(new OrbitalInfo
                {
                    Id = o.Id,
                    CivId = o.CivilizationId,
                    Name = o.Name,
                    Type = o.Type,
                    Crew = o.Crew,
                    OrbitAngle = o.OrbitAngle,
                    OrbitRadius = o.OrbitRadius,
                    LaunchedYear = o.LaunchedYear,
                    Orphaned = o.Orphaned
                });
            }
            data.PeopleInSpace = data.Orbitals.Sum(o => o.Crew);
            data.NuclearWinter = Math.Clamp(manager.NuclearWinter, 0f, 1f);

            if (diseases != null) CaptureDiseases(data, diseases);
        }
        catch (Exception)
        {
            // Collections can still change underneath us in rare cases (e.g. during a load);
            // a partial snapshot is fine, the next one will be complete.
        }

        Latest = data;
        return data;
    }

    private static RulerInfo ToInfo(Ruler r) => new RulerInfo
    {
        Id = r.Id,
        Name = r.Name,
        Title = r.Title,
        Age = r.Age,
        ParentId = r.ParentId,
        ChildrenIds = r.ChildrenIds.ToArray(),
        DynastyId = r.DynastyId,
        IsAlive = r.IsAlive,
        YearTookPower = r.YearTookPower,
        DeathYear = r.DeathYear,
        Wisdom = r.Wisdom,
        Charisma = r.Charisma,
        Ambition = r.Ambition,
        Brutality = r.Brutality,
        Piety = r.Piety
    };

    private static NationDetail CaptureDetail(Civilization civ)
    {
        var gov = civ.Government;
        var active = civ.ActiveProject;
        var detail = new NationDetail
        {
            CurrentRulerId = gov?.CurrentRuler?.Id ?? -1,
            HeirApparentId = civ.HeirApparent?.Id ?? -1,
            EstablishedYear = gov?.EstablishedYear ?? 0,
            IsHereditary = gov?.IsHereditary ?? false,
            IsElected = gov?.IsElected ?? false,
            GovStability = gov?.Stability ?? 0f,
            Legitimacy = gov?.Legitimacy ?? 0f,
            Corruption = gov?.Corruption ?? 0f,
            ActiveProject = active == null ? null : new ProjectInfo
            {
                Name = active.Name,
                Kind = active.Kind,
                Fraction = active.Fraction,
                StartedYear = active.StartedYear,
                CompletedYear = active.CompletedYear
            },
            NuclearStrikesLaunched = civ.Arsenal.NuclearStrikesLaunched,
            ChemicalAttacks = civ.Arsenal.ChemicalAttacks,
            BioweaponReleases = civ.Arsenal.BioweaponReleases,
            WarCasualties = civ.WarCasualties,
            CitiesConquered = civ.CitiesConquered,
            CitiesLost = civ.CitiesLost,
            Founded = civ.Founded,
            FoodProduction = civ.FoodProduction,
            FoodConsumption = civ.FoodConsumption,
            TradeIncome = civ.TradeIncome,
            RoadCells = civ.Roads.Count
        };

        // Keep the most recent rulers (long dynasties can accumulate hundreds)
        var rulers = civ.AllRulers;
        var ids = new HashSet<int>();
        int start = Math.Max(0, rulers.Count - 160);
        for (int i = start; i < rulers.Count; i++)
        {
            if (ids.Add(rulers[i].Id)) detail.Rulers.Add(ToInfo(rulers[i]));
        }
        var current = gov?.CurrentRuler;
        if (current != null && ids.Add(current.Id)) detail.Rulers.Add(ToInfo(current));
        foreach (var r in civ.SuccessionLine)
        {
            detail.SuccessionLine.Add(ToInfo(r));
            if (ids.Add(r.Id)) detail.Rulers.Add(ToInfo(r));
        }
        if (civ.HeirApparent != null && ids.Add(civ.HeirApparent.Id)) detail.Rulers.Add(ToInfo(civ.HeirApparent));

        foreach (var d in civ.Dynasties)
        {
            detail.Dynasties.Add(new DynastyInfo
            {
                Id = d.Id, Name = d.Name, FoundedYear = d.FoundedYear, FounderId = d.FounderId,
                Extinct = d.IsExtinct, Generations = d.GenerationCount
            });
        }

        foreach (var p in civ.Parties)
        {
            detail.Parties.Add(new PartyInfo
            {
                Name = p.Name, Ideology = p.Ideology, Support = p.Support, SeatsWon = p.SeatsWon, FoundedYear = p.FoundedYear
            });
        }

        int e0 = Math.Max(0, civ.Elections.Count - 12);
        for (int i = e0; i < civ.Elections.Count; i++)
        {
            var e = civ.Elections[i];
            detail.Elections.Add(new ElectionInfo { Year = e.Year, WinningParty = e.WinningParty, WinningShare = e.WinningShare, LeaderName = e.LeaderName });
        }

        foreach (var p in civ.CompletedProjects)
        {
            detail.CompletedProjects.Add(new ProjectInfo
            {
                Name = p.Name, Kind = p.Kind, Fraction = 1f, StartedYear = p.StartedYear, CompletedYear = p.CompletedYear
            });
        }

        foreach (var (otherId, rel) in civ.DiplomaticRelations)
            detail.Relations.Add(new RelationInfo { OtherCivId = otherId, Status = rel.Status, Opinion = rel.Opinion });

        return detail;
    }

    private static int CellHash(int x, int y)
    {
        int h = x * 374761393 + y * 668265263 + 9001;
        h = (h ^ (h >> 13)) * 1274126177;
        return h ^ (h >> 16);
    }

    private static void AddSiloSites(CivRenderData data, Civilization civ)
    {
        int silos = Math.Min(civ.Arsenal.MissileSilos, 12);
        if (silos <= 0 || civ.Territory.Count == 0) return;
        // Deterministic spread: the territory cells with the lowest hash, kept a few cells apart
        var candidates = civ.Territory.OrderBy(c => CellHash(c.x, c.y)).Take(400);
        int placed = 0;
        var chosen = new List<(int x, int y)>();
        foreach (var c in candidates)
        {
            bool farEnough = true;
            foreach (var o in chosen)
            {
                if (Math.Abs(o.x - c.x) + Math.Abs(o.y - c.y) < 4) { farEnough = false; break; }
            }
            if (!farEnough) continue;
            chosen.Add(c);
            data.SiloSites.Add((civ.Id, c.x, c.y));
            if (++placed >= silos) break;
        }
    }

    private static void CaptureDiseases(CivRenderData data, DiseaseManager diseases)
    {
        var population = new Dictionary<int, int>();
        foreach (var c in data.Civs) population[c.Id] = c.Population;

        foreach (var d in diseases.Diseases)
        {
            if (!d.IsActive) continue;
            data.Diseases.Add(new DiseaseInfo
            {
                Id = d.Id,
                Name = d.Name,
                Type = d.Type.ToString(),
                TotalInfected = d.TotalInfected,
                TotalDeaths = d.TotalDeaths,
                Lethality = d.Lethality,
                CureProgress = d.GlobalCureProgress
            });
        }

        foreach (var inf in diseases.Infections.Values)
        {
            if (inf.InfectedCount <= 0) continue;
            int pop = population.GetValueOrDefault(inf.CivilizationId);
            data.Infections.Add(new InfectionInfo
            {
                DiseaseId = inf.DiseaseId,
                CivId = inf.CivilizationId,
                Infected = inf.InfectedCount,
                Dead = inf.DeadCount,
                Share = pop > 0 ? Math.Clamp(inf.InfectedCount / (float)pop, 0f, 1f) : 1f,
                Quarantine = inf.QuarantineActive,
                BordersClosed = inf.BordersClosed
            });
        }
    }
}
