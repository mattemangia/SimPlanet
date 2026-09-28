using System.Collections;
using System.Reflection;

namespace SimPlanet;

/// <summary>
/// Immutable per-frame snapshot of the civilization data needed for rendering.
///
/// The snapshot is taken on the render thread while the map data lock is held, so the
/// renderer and UI can draw cities, armies and battles without touching the live
/// collections that the simulation thread keeps modifying.
///
/// Some of the data (armies, battles, chronicle, capital/buildings/siege flags, gold)
/// belongs to the extended civilization simulation. Those members are read through a
/// small cached-reflection layer so the renderer works whether or not they exist yet:
/// when a member is missing a sensible fallback is used (e.g. the first city of a
/// civilization is its capital, no armies are drawn).
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

    public List<CivInfo> Civs { get; } = new();
    public List<CityInfo> Cities { get; } = new();
    public List<ArmyInfo> Armies { get; } = new();
    public List<BattleInfo> Battles { get; } = new();
    public List<HistoryInfo> Chronicle { get; } = new();
    public bool HasChronicleSupport { get; private set; }

    /// <summary>
    /// Builds a snapshot. Must be called while the simulation is not mutating civilization
    /// data (i.e. while holding the map data lock). Never throws.
    /// </summary>
    public static CivRenderData Capture(CivilizationManager? manager, int maxChronicle = 200)
    {
        var data = new CivRenderData();
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
                    Gold = Get<float>(civ, "Gold", 0f),
                    WarWeariness = Get<float>(civ, "WarWeariness", 0f),
                    Stability = civ.Stability,
                    Prosperity = civ.Prosperity,
                    Government = civ.Government?.Type.ToString() ?? "",
                    MilitaryStrength = civ.MilitaryStrength
                });

                // Young civilizations have no cities yet: show their home settlement
                if (civ.Cities.Count == 0)
                {
                    data.Cities.Add(new CityInfo
                    {
                        X = civ.CenterX,
                        Y = civ.CenterY,
                        Name = civ.Name,
                        Population = civ.Population,
                        Type = 0,
                        CivId = civ.Id,
                        IsCapital = true,
                        Buildings = ""
                    });
                }

                bool anyExplicitCapital = false;
                foreach (var city in civ.Cities)
                    if (Get<bool>(city, "IsCapital", false)) { anyExplicitCapital = true; break; }

                for (int i = 0; i < civ.Cities.Count; i++)
                {
                    var city = civ.Cities[i];
                    string buildings = GetRaw(city, "Buildings")?.ToString() ?? "";
                    data.Cities.Add(new CityInfo
                    {
                        X = city.X,
                        Y = city.Y,
                        Name = city.Name,
                        Population = city.Population,
                        Type = (int)city.Type,
                        CivId = civ.Id,
                        IsCapital = anyExplicitCapital ? Get<bool>(city, "IsCapital", false) : i == 0,
                        HasWalls = buildings.Contains("Walls"),
                        UnderSiege = Get<bool>(city, "UnderSiege", false),
                        SiegeProgress = Get<float>(city, "SiegeProgress", 0f),
                        Starving = Get<bool>(city, "Starving", false),
                        Buildings = buildings == "0" || buildings == "None" ? "" : buildings
                    });
                }
            }

            foreach (var army in InvokeList(manager, "GetArmies"))
            {
                data.Armies.Add(new ArmyInfo
                {
                    Id = Get<int>(army, "Id", 0),
                    CivId = Get<int>(army, "CivilizationId", 0),
                    X = Get<float>(army, "X", 0f),
                    Y = Get<float>(army, "Y", 0f),
                    Soldiers = Get<int>(army, "Soldiers", 0),
                    State = GetRaw(army, "State")?.ToString() ?? "",
                    TargetX = Get<int>(army, "TargetX", 0),
                    TargetY = Get<int>(army, "TargetY", 0)
                });
            }

            foreach (var battle in InvokeList(manager, "GetRecentBattles"))
            {
                data.Battles.Add(new BattleInfo
                {
                    X = Get<int>(battle, "X", 0),
                    Y = Get<int>(battle, "Y", 0),
                    Year = Get<int>(battle, "Year", 0),
                    AttackerCivId = Get<int>(battle, "AttackerCivId", 0),
                    DefenderCivId = Get<int>(battle, "DefenderCivId", 0),
                    Casualties = Get<int>(battle, "Casualties", 0),
                    Age = Get<float>(battle, "Age", 0f)
                });
            }

            var chronicle = InvokeList(manager, "GetChronicle", out bool hasChronicle);
            data.HasChronicleSupport = hasChronicle;
            int start = Math.Max(0, chronicle.Count - maxChronicle);
            for (int i = start; i < chronicle.Count; i++)
            {
                var ev = chronicle[i];
                data.Chronicle.Add(new HistoryInfo
                {
                    Year = Get<int>(ev, "Year", 0),
                    Text = GetRaw(ev, "Text")?.ToString() ?? "",
                    Category = GetRaw(ev, "Category")?.ToString() ?? "",
                    X = Get<int>(ev, "X", -1),
                    Y = Get<int>(ev, "Y", -1),
                    CivId = Get<int>(ev, "CivilizationId", 0)
                });
            }
        }
        catch (Exception)
        {
            // Collections can still change underneath us in rare cases (e.g. during a load);
            // a partial snapshot is fine, the next one will be complete.
        }

        AugmentSnapshot(data, manager);
        return data;
    }

    /// <summary>Optional hook for development builds (e.g. to inject sample data).</summary>
    static partial void AugmentSnapshot(CivRenderData data, CivilizationManager manager);

    // ---- Cached reflection helpers ------------------------------------

    private static readonly Dictionary<(Type, string), MemberInfo?> _members = new();
    private static readonly object _cacheLock = new();

    private static MemberInfo? FindMember(Type type, string name)
    {
        lock (_cacheLock)
        {
            if (_members.TryGetValue((type, name), out var cached)) return cached;
            MemberInfo? member = (MemberInfo?)type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance)
                                 ?? type.GetField(name, BindingFlags.Public | BindingFlags.Instance);
            _members[(type, name)] = member;
            return member;
        }
    }

    private static object? GetRaw(object obj, string name)
    {
        var member = FindMember(obj.GetType(), name);
        return member switch
        {
            PropertyInfo p => p.GetValue(obj),
            FieldInfo f => f.GetValue(obj),
            _ => null
        };
    }

    private static T Get<T>(object obj, string name, T fallback)
    {
        try
        {
            var raw = GetRaw(obj, name);
            if (raw == null) return fallback;
            if (raw is T t) return t;
            return (T)Convert.ChangeType(raw, typeof(T));
        }
        catch
        {
            return fallback;
        }
    }

    private static List<object> InvokeList(object target, string method) => InvokeList(target, method, out _);

    private static List<object> InvokeList(object target, string method, out bool exists)
    {
        var result = new List<object>();
        exists = false;
        MethodInfo? mi;
        lock (_cacheLock)
        {
            var key = (target.GetType(), "()" + method);
            if (!_members.TryGetValue(key, out var cached))
            {
                cached = target.GetType().GetMethod(method, BindingFlags.Public | BindingFlags.Instance, Type.EmptyTypes);
                _members[key] = cached;
            }
            mi = cached as MethodInfo;
        }
        if (mi == null) return result;
        exists = true;
        if (mi.Invoke(target, null) is IEnumerable list)
        {
            foreach (var item in list)
                if (item != null) result.Add(item);
        }
        return result;
    }
}
