namespace SimPlanet;

/// <summary>
/// Immutable per-frame snapshot of the civilization data needed for rendering.
///
/// The snapshot is taken on the render thread while the map data lock is held, so the
/// renderer and UI can draw cities, armies and battles without touching the live
/// collections that the simulation thread keeps modifying.
/// </summary>
public sealed class CivRenderData
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

    // Battles fade out in real time, whatever the simulation speed
    private static readonly Dictionary<(int x, int y, int year, int casualties), long> _battleFirstSeen = new();
    private static readonly System.Diagnostics.Stopwatch _clock = System.Diagnostics.Stopwatch.StartNew();

    /// <summary>
    /// Builds a snapshot. Must be called while the simulation is not mutating civilization
    /// data (i.e. while holding the map data lock). Never throws.
    /// </summary>
    public static CivRenderData Capture(CivilizationManager? manager, int maxChronicle = 200)
    {
        var data = new CivRenderData { HasChronicleSupport = manager != null };
        if (manager == null) return data;

        try
        {
            foreach (var civ in manager.GetAllCivilizations())
            {
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
                    MilitaryStrength = civ.MilitaryStrength
                });

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
                        Buildings = city.Buildings == CityBuilding.None ? "" : city.Buildings.ToString()
                    });
                }
            }

            foreach (var army in manager.GetArmies())
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
                    TargetY = army.TargetY
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
        }
        catch (Exception)
        {
            // Collections can still change underneath us in rare cases (e.g. during a load);
            // a partial snapshot is fine, the next one will be complete.
        }

        Latest = data;
        return data;
    }
}
