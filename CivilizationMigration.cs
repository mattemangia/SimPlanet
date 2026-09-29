namespace SimPlanet;

/// <summary>
/// Human migration. People leave places where they are in danger or poor — war, famine,
/// plague, harsh climate, disasters, persecution — and move to safe, prosperous places,
/// preferably abroad in nations at peace with their own, or else within their country.
/// Industrialisation draws villagers to the cities, and poor nations lose people to rich
/// neighbours. Every flow is recorded for the map and the chronicle.
/// </summary>
public partial class CivilizationManager
{
    private readonly HashSet<int> _citiesHitThisYear = new();
    private readonly Dictionary<(int civ, MigrationKind kind), int> _lastMigrationReport = new();

    private void UpdateMigration(int currentYear)
    {
        _migrations.RemoveAll(m => m.Year < currentYear - 4);

        foreach (var civ in _civilizations.ToList())
        {
            if (civ.Cities.Count == 0) continue;

            float infectedShare = GetInfectedShare(civ);
            var ruler = civ.Government?.CurrentRuler;
            bool oppressive = (ruler?.Brutality ?? 0f) > 0.75f &&
                              civ.Government?.Type is GovernmentType.Dictatorship or GovernmentType.Theocracy;

            foreach (var city in civ.Cities.ToList())
            {
                var (kind, rate) = GetDisplacement(civ, city, infectedShare, oppressive);
                if (rate <= 0f) continue;

                int people = (int)(city.Population * rate);
                if (people < 20) continue;

                bool mayStayHome = kind is MigrationKind.WarRefugees or MigrationKind.ClimateRefugees or MigrationKind.DisasterRefugees;
                var (destination, host) = FindRefuge(civ, city, allowOwnCountry: mayStayHome);
                if (destination == null || host == null) continue;

                MovePeople(civ, city, host, destination, people, kind, currentYear);
            }
        }

        EconomicMigration(currentYear);
        Urbanization(currentYear);
        _citiesHitThisYear.Clear();

        if (_migrations.Count > 400) _migrations.RemoveRange(0, _migrations.Count - 400);
    }

    /// <summary>
    /// Why people are leaving a settlement this year, and what share of them.
    /// </summary>
    private (MigrationKind kind, float rate) GetDisplacement(Civilization civ, City city, float infectedShare, bool oppressive)
    {
        var cell = _map.Cells[city.X, city.Y];

        if (city.UnderSiege && city.SiegeProgress > 0.2f) return (MigrationKind.WarRefugees, 0.05f);
        if (civ.AtWar && _armies.Any(a => a.CivilizationId != civ.Id && AreAtWar(a.CivilizationId, civ.Id) &&
                                          WrappedDistance((int)a.X, (int)a.Y, city.X, city.Y) < 5))
            return (MigrationKind.WarRefugees, 0.03f);
        if (_citiesHitThisYear.Contains(city.Id) || cell.GetGeology().RadioactiveContamination > 0.3f)
            return (MigrationKind.DisasterRefugees, 0.04f);
        if (city.Starving) return (MigrationKind.FamineRefugees, 0.04f);
        if (infectedShare > 0.1f) return (MigrationKind.EpidemicRefugees, Math.Min(0.02f, infectedShare * 0.05f));
        if (cell.Temperature > 38f || cell.Temperature < -12f || (cell.Rainfall < 0.12f && city.Population > city.Capacity * 0.9f))
            return (MigrationKind.ClimateRefugees, 0.02f);
        if (oppressive && city.OriginalCivilizationId != civ.Id && city.Happiness < 0.5f)
            return (MigrationKind.Persecution, 0.02f);
        return (MigrationKind.Economic, 0f);
    }

    private float GetInfectedShare(Civilization civ)
    {
        if (_diseaseManager == null || civ.Population <= 0) return 0f;
        int infected = 0;
        try
        {
            foreach (var infection in _diseaseManager.Infections.Values.ToList())
            {
                if (infection.CivilizationId == civ.Id) infected += infection.InfectedCount;
            }
        }
        catch (Exception)
        {
            return 0f; // Being updated by the disease system
        }
        return infected / (float)civ.Population;
    }

    /// <summary>
    /// Nearest safe settlement with room: abroad in a nation at peace with the refugees' own,
    /// or (for war, climate and disaster refugees) within their own country.
    /// </summary>
    private (City? city, Civilization? owner) FindRefuge(Civilization from, City origin, bool allowOwnCountry)
    {
        City? best = null;
        Civilization? bestOwner = null;
        float bestScore = float.MaxValue;

        foreach (var other in _civilizations)
        {
            bool own = other.Id == from.Id;
            if (own && !allowOwnCountry) continue;
            if (!own && (AreAtWar(other.Id, from.Id) || other.Food < other.FoodConsumption * 0.3f)) continue;

            foreach (var candidate in other.Cities)
            {
                if (candidate.Id == origin.Id || candidate.Starving || candidate.UnderSiege) continue;
                if (candidate.Population > candidate.Capacity) continue;
                if (_citiesHitThisYear.Contains(candidate.Id)) continue;

                float distance = WrappedDistance(candidate.X, candidate.Y, origin.X, origin.Y);
                if (distance > 35) continue;

                // Abroad is preferred when it is safer; friends welcome refugees
                float score = distance + (own ? 8f : 0f);
                if (!own && other.DiplomaticRelations.TryGetValue(from.Id, out var relation))
                {
                    score -= relation.Opinion / 20f;
                }
                if (score < bestScore)
                {
                    bestScore = score;
                    best = candidate;
                    bestOwner = other;
                }
            }
        }
        return (best, bestOwner);
    }

    private void MovePeople(Civilization from, City origin, Civilization to, City destination, int people, MigrationKind kind, int currentYear)
    {
        origin.Population -= people;
        destination.Population += people;
        RecalculatePopulation(from);
        if (to != from)
        {
            RecalculatePopulation(to);
            // Friendly hosts grow closer; others resent the influx
            if (to.DiplomaticRelations.TryGetValue(from.Id, out var relation))
            {
                relation.Opinion += relation.Opinion > 20 ? 0.5f : -1f;
            }
            // Newcomers in a foreign land are not yet at home
            destination.Happiness = Math.Max(0f, destination.Happiness - Math.Min(0.05f, people / (float)Math.Max(1, destination.Population)));
        }

        RecordMigration(origin.X, origin.Y, destination.X, destination.Y, people, kind, from.Id, to.Id, currentYear);

        // Large exoduses enter the chronicle (at most once a decade per nation and cause)
        if (people >= 1500 && to != from &&
            currentYear - _lastMigrationReport.GetValueOrDefault((from.Id, kind), int.MinValue / 2) >= 10)
        {
            _lastMigrationReport[(from.Id, kind)] = currentYear;
            string cause = kind switch
            {
                MigrationKind.WarRefugees => "fleeing the war",
                MigrationKind.FamineRefugees => "fleeing famine",
                MigrationKind.EpidemicRefugees => "fleeing the plague",
                MigrationKind.ClimateRefugees => "fleeing a hostile climate",
                MigrationKind.DisasterRefugees => "fleeing disaster",
                MigrationKind.Persecution => "fleeing persecution",
                MigrationKind.Economic => "seeking a better life",
                _ => "on the move"
            };
            AddChronicle(currentYear, HistoryCategory.Migration,
                $"{people:N0} people leave {origin.Name} ({from.Name}), {cause}, for {destination.Name} ({to.Name})",
                origin.X, origin.Y, from.Id);
        }
    }

    private void RecordMigration(int fromX, int fromY, int toX, int toY, int people, MigrationKind kind, int fromCiv, int toCiv, int year)
    {
        _migrations.Add(new MigrationFlow
        {
            FromX = fromX,
            FromY = fromY,
            ToX = toX,
            ToY = toY,
            People = people,
            Kind = kind,
            FromCivId = fromCiv,
            ToCivId = toCiv,
            Year = year
        });
    }

    /// <summary>
    /// People move from poor nations to richer neighbours at peace with them.
    /// </summary>
    private void EconomicMigration(int currentYear)
    {
        foreach (var (a, b, _) in GetContactPairs().ToList())
        {
            if (AreAtWar(a.Id, b.Id) || a.Population <= 0 || b.Population <= 0) continue;

            float wealthA = a.Prosperity + a.Gold / Math.Max(1f, a.Population / 100f) * 0.01f;
            float wealthB = b.Prosperity + b.Gold / Math.Max(1f, b.Population / 100f) * 0.01f;
            if (Math.Abs(wealthA - wealthB) < 0.2f) continue;

            var (poor, rich) = wealthA < wealthB ? (a, b) : (b, a);
            if (rich.DiplomaticRelations.TryGetValue(poor.Id, out var relation) && relation.Opinion < -30) continue; // Closed borders

            var origin = poor.Cities.OrderByDescending(c => c.Population).First();
            var destination = rich.Cities
                .Where(c => c.Population < c.Capacity && !c.Starving)
                .OrderBy(c => WrappedDistance(c.X, c.Y, origin.X, origin.Y))
                .FirstOrDefault();
            if (destination == null) continue;

            int people = (int)(origin.Population * 0.006f * Math.Min(2f, Math.Abs(wealthA - wealthB) * 3f));
            if (people < 30) continue;
            MovePeople(poor, origin, rich, destination, people, MigrationKind.Economic, currentYear);
        }
    }

    /// <summary>
    /// With industry and railways, villagers leave for the big cities.
    /// </summary>
    private void Urbanization(int currentYear)
    {
        foreach (var civ in _civilizations)
        {
            if (civ.CivType < CivType.Industrial || civ.Cities.Count < 3) continue;

            var magnets = civ.Cities
                .Where(c => c.Type >= CityType.City && c.Population < c.Capacity * 1.1f)
                .OrderByDescending(c => c.Population)
                .Take(3)
                .ToList();
            if (magnets.Count == 0) continue;

            foreach (var village in civ.Cities.Where(c => c.Type == CityType.Village && !c.IsCapital).ToList())
            {
                int people = (int)(village.Population * 0.015f);
                if (people < 10) continue;
                var destination = magnets.OrderBy(m => WrappedDistance(m.X, m.Y, village.X, village.Y)).First();
                village.Population -= people;
                destination.Population += people;
                RecordMigration(village.X, village.Y, destination.X, destination.Y, people, MigrationKind.Urbanization, civ.Id, civ.Id, currentYear);
            }
            RecalculatePopulation(civ);
        }
    }
}
