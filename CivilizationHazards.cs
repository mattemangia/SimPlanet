namespace SimPlanet;

/// <summary>
/// The planet strikes back: volcanic eruptions, asteroid impacts, storms, floods, acid rain,
/// rockfalls, civil nuclear accidents and EMPs hurt the settlements they hit; rising seas
/// swallow coastal towns and advancing ice buries northern ones; famine and war drive
/// refugees across borders.
/// </summary>
public partial class CivilizationManager
{
    private GeologicalSimulator? _geologicalSimulator;
    private readonly HashSet<(DisasterType type, int x, int y, int year)> _handledDisasters = new();
    private readonly HashSet<(int x, int y, int year)> _handledEruptions = new();

    public void SetGeologicalSimulator(GeologicalSimulator geologicalSimulator)
    {
        _geologicalSimulator = geologicalSimulator;
    }

    /// <summary>
    /// Mark a disaster this manager triggered itself (it already applied the casualties).
    /// </summary>
    private void MarkDisasterHandled(DisasterType type, int x, int y, int year)
    {
        _handledDisasters.Add((type, x, y, year));
    }

    private void ApplyNaturalHazards(int currentYear)
    {
        ApplyEruptions(currentYear);
        ApplyDisasterEvents(currentYear);
        ApplySeaAndIce(currentYear);
        UpdateMigration(currentYear);

        _handledDisasters.RemoveWhere(d => d.year < currentYear - 3);
        _handledEruptions.RemoveWhere(e => e.year < currentYear - 3);
    }

    #region Eruptions and disasters

    private void ApplyEruptions(int currentYear)
    {
        if (_geologicalSimulator == null) return;

        List<(int x, int y, int year)> eruptions;
        try
        {
            eruptions = _geologicalSimulator.RecentEruptions.ToList();
        }
        catch (Exception) // Modified concurrently by another simulator; retry next year
        {
            return;
        }

        foreach (var eruption in eruptions)
        {
            if (eruption.year < currentYear - 1 || !_handledEruptions.Add(eruption)) continue;

            var (dead, worst, owner) = StrikeSettlements(eruption.x, eruption.y, 4f, 0.45f, 0.3f);
            if (dead >= 200 && worst != null)
            {
                AddChronicle(currentYear, HistoryCategory.Disaster,
                    $"A volcano erupts near {worst.Name}: lava and ash kill {dead:N0}", eruption.x, eruption.y, owner?.Id ?? 0);
            }
        }
    }

    private void ApplyDisasterEvents(int currentYear)
    {
        if (_disasterManager == null) return;

        List<DisasterEvent> events;
        try
        {
            events = _disasterManager.RecentDisasters.ToList();
        }
        catch (Exception) // Modified concurrently by another simulator; retry next year
        {
            return;
        }

        foreach (var ev in events)
        {
            if (ev.Year < currentYear - 1) continue;
            if (!_handledDisasters.Add((ev.Type, ev.X, ev.Y, ev.Year))) continue;

            // Radius (cells), lethality at the centre and happiness loss
            (float radius, float lethality, float unhappiness, string what) = ev.Type switch
            {
                DisasterType.Asteroid => (4f + ev.Magnitude * 4f, Math.Min(0.95f, 0.3f + ev.Magnitude * 0.15f), 0.4f, "An asteroid strikes"),
                DisasterType.NuclearAccident => (6f, 0.2f, 0.3f, "A nuclear reactor melts down"),
                DisasterType.Tornado => (2f, 0.04f, 0.05f, "A tornado tears through"),
                DisasterType.HeavyRain => (3f, 0.01f, 0.03f, "Torrential rain floods"),
                DisasterType.Flood => (4f, 0.05f, 0.1f, "A great flood hits"),
                DisasterType.AcidRain => (5f, 0.005f, 0.08f, "Acid rain falls on"),
                DisasterType.Rockfall => (1.5f, 0.05f, 0.03f, "A landslide buries part of"),
                DisasterType.EMP => (ev.Magnitude, 0f, 0.1f, "An electromagnetic pulse blacks out"),
                _ => (0f, 0f, 0f, "")
            };
            if (radius <= 0) continue;

            var (dead, worst, owner) = StrikeSettlements(ev.X, ev.Y, radius, lethality, unhappiness);

            if (ev.Type == DisasterType.EMP)
            {
                foreach (var civ in _civilizations)
                {
                    foreach (var city in civ.Cities.Where(c => WrappedDistance(c.X, c.Y, ev.X, ev.Y) <= radius))
                    {
                        city.Electrified = false;
                        city.Online = false;
                    }
                }
            }

            bool notable = ev.Type == DisasterType.Asteroid || dead >= 500;
            if (notable && worst != null)
            {
                AddChronicle(currentYear, HistoryCategory.Disaster,
                    dead > 0 ? $"{what} {worst.Name} ({dead:N0} dead)" : $"{what} {worst.Name}",
                    ev.X, ev.Y, owner?.Id ?? 0);
            }
        }
    }

    /// <summary>
    /// Kill and demoralise the people in settlements near a disaster; returns total deaths,
    /// the worst-hit settlement and its owner.
    /// </summary>
    private (int dead, City? worst, Civilization? owner) StrikeSettlements(int x, int y, float radius, float lethality, float unhappiness)
    {
        int total = 0;
        City? worst = null;
        Civilization? worstOwner = null;
        int worstDead = -1;

        foreach (var civ in _civilizations)
        {
            bool hit = false;
            foreach (var city in civ.Cities)
            {
                float distance = WrappedDistance(city.X, city.Y, x, y);
                if (distance > radius) continue;

                float intensity = 1f - distance / (radius + 1f);
                int dead = (int)(city.Population * lethality * intensity * (1f - civ.DisasterPreparedness * 0.5f));
                city.Population -= dead;
                city.Happiness = Math.Max(0f, city.Happiness - unhappiness * intensity);
                total += dead;
                hit = true;
                if (lethality > 0.02f) _citiesHitThisYear.Add(city.Id);

                if (dead > worstDead || worst == null)
                {
                    worstDead = dead;
                    worst = city;
                    worstOwner = civ;
                }
            }

            if (hit)
            {
                RecalculatePopulation(civ);
                civ.DisastersSurvived++;
                civ.DisasterPreparedness = Math.Min(0.9f, civ.DisasterPreparedness + 0.01f);
            }
        }

        return (total, worst, worstOwner);
    }

    #endregion

    #region Rising seas and advancing ice

    private void ApplySeaAndIce(int currentYear)
    {
        foreach (var civ in _civilizations.ToList())
        {
            foreach (var city in civ.Cities.ToList())
            {
                var cell = _map.Cells[city.X, city.Y];
                string? fate = null;
                if (cell.IsWater) fate = $"{city.Name} is swallowed by the sea";
                else if (cell.IsIce && cell.Temperature < -15f) fate = $"Advancing ice buries {city.Name}";
                if (fate == null) continue;

                // Most people get out and move to the nearest safe settlement
                int survivors = (int)(city.Population * 0.8f);
                civ.Cities.Remove(city);
                var refuge = civ.Cities
                    .Where(c => !_map.Cells[c.X, c.Y].IsWater && !_map.Cells[c.X, c.Y].IsIce)
                    .OrderBy(c => WrappedDistance(c.X, c.Y, city.X, city.Y))
                    .FirstOrDefault();
                if (refuge != null)
                {
                    refuge.Population += survivors;
                    RecordMigration(city.X, city.Y, refuge.X, refuge.Y, survivors, MigrationKind.ClimateRefugees, civ.Id, civ.Id, currentYear);
                }

                if (city.IsCapital && civ.Cities.Count > 0)
                {
                    var newCapital = civ.Cities.OrderByDescending(c => c.Population).First();
                    newCapital.IsCapital = true;
                    civ.CenterX = newCapital.X;
                    civ.CenterY = newCapital.Y;
                }

                if (cell.IsWater) SetOwner(null, city.X, city.Y);
                RecalculatePopulation(civ);

                if (city.Type >= CityType.Town || city.IsCapital)
                {
                    AddChronicle(currentYear, HistoryCategory.Disaster,
                        refuge != null ? $"{fate}; its people flee to {refuge.Name}" : fate,
                        city.X, city.Y, civ.Id);
                }
            }
        }
    }

    #endregion
}
