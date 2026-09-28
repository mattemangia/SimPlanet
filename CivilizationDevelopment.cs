namespace SimPlanet;

/// <summary>
/// National development: homeland and ethnicity, energy and power grids, the internet,
/// airports and spaceports, national research/economic/space/military programmes,
/// the space programme with its satellites, stations and colonies (whose crews survive
/// the fall of their nation), and natural epidemics.
/// </summary>
public partial class CivilizationManager
{
    private int _nextOrbitalId = 1;

    #region Homeland and ethnicity

    /// <summary>
    /// Temperate lands with rivers give their peoples surpluses, fewer diseases and time to
    /// think; deserts, jungles, highlands and the cold slow development down.
    /// </summary>
    private void AssignHomeland(Civilization civ)
    {
        var cell = _map.Cells[civ.CenterX, civ.CenterY];
        var geo = cell.GetGeology();

        civ.Homeland = cell.Elevation > 0.65f ? HomelandClimate.Highland
            : cell.Temperature < 5f ? HomelandClimate.Cold
            : cell.Rainfall < 0.25f ? HomelandClimate.Arid
            : cell.Temperature > 23f && cell.Rainfall > 0.5f ? HomelandClimate.Tropical
            : HomelandClimate.Temperate;

        float modifier = civ.Homeland switch
        {
            HomelandClimate.Temperate => 1.25f,
            HomelandClimate.Highland => 0.95f,
            HomelandClimate.Tropical => 0.9f,
            HomelandClimate.Arid => 0.85f,
            HomelandClimate.Cold => 0.8f,
            _ => 1f
        };
        if (geo.RiverId > 0 || geo.WaterFlow > 0.3f) modifier += 0.1f; // River valley civilizations
        if (_map.GetNeighbors(civ.CenterX, civ.CenterY).Any(n => n.cell.IsWater)) modifier += 0.05f;
        civ.DevelopmentModifier = modifier;

        if (string.IsNullOrEmpty(civ.Ethnicity))
        {
            civ.Ethnicity = GenerateWord(civ.Culture, 2, 2) + (civ.Homeland switch
            {
                HomelandClimate.Cold => " (northern)",
                HomelandClimate.Arid => " (desert)",
                HomelandClimate.Tropical => " (tropical)",
                HomelandClimate.Highland => " (highland)",
                _ => ""
            });
        }
    }

    #endregion

    #region Energy, power grid, internet and transport hubs

    private static readonly EnergySource[] AllSources = Enum.GetValues<EnergySource>();

    private void UpdateInfrastructure(Civilization civ, int currentYear)
    {
        int tech = civ.TechLevel;
        var ideology = GetRulingIdeology(civ);

        // ---- Energy mix ----
        var weights = new Dictionary<EnergySource, float>();
        weights[EnergySource.Biomass] = tech < 40 ? 1f : 0.2f;
        if (tech >= 25) weights[EnergySource.Coal] = civ.ResourceStockpile.GetValueOrDefault(ResourceType.Coal) > 0.1f || civ.HasProject("Industrialization") ? 1.2f : 0.5f;
        if (tech >= 35) weights[EnergySource.Oil] = civ.ResourceStockpile.GetValueOrDefault(ResourceType.Oil) > 0.1f ? 1.2f : 0.4f;
        if (tech >= 30 && civ.Cities.Any(c => c.NearRiver)) weights[EnergySource.Hydro] = 0.6f;
        if (tech >= 60 && civ.Territory.Any(p => _map.Cells[p.x, p.y].GetGeology().HasNuclearPlant)) weights[EnergySource.Nuclear] = 0.9f;
        if (tech >= 45) weights[EnergySource.Wind] = 0.3f + civ.EcoFriendliness * 0.5f;
        if (tech >= 80) weights[EnergySource.Solar] = 0.5f + civ.EcoFriendliness * 0.8f;
        if (civ.HasProject("Fusion Power")) weights[EnergySource.Fusion] = 3f;

        bool green = ideology == Ideology.Green || civ.HasProject("Renewable Transition") || civ.InClimateAgreement;
        if (green)
        {
            foreach (var fossil in new[] { EnergySource.Coal, EnergySource.Oil })
                if (weights.ContainsKey(fossil)) weights[fossil] *= 0.3f;
            foreach (var clean in new[] { EnergySource.Wind, EnergySource.Solar, EnergySource.Hydro })
                if (weights.ContainsKey(clean)) weights[clean] *= 2f;
        }

        float total = weights.Values.Sum();
        civ.EnergyMix = weights.ToDictionary(w => w.Key, w => w.Value / total);

        float perCapita = civ.CivType switch
        {
            CivType.Tribal => 0.1f,
            CivType.Agricultural => 0.3f,
            CivType.Industrial => 2f,
            CivType.Scientific => 4f,
            CivType.Spacefaring => 6f,
            _ => 0.1f
        };
        civ.EnergyDemand = civ.Population / 1000f * perCapita;

        // Fossil fuels run short without deposits; war disrupts supply
        float supply = 1f;
        if (civ.EnergyMix.GetValueOrDefault(EnergySource.Oil) > 0.3f && civ.ResourceStockpile.GetValueOrDefault(ResourceType.Oil) <= 0.05f) supply -= 0.2f;
        if (civ.AtWar) supply -= 0.1f;
        if (NuclearWinter > 0.3f) supply -= 0.2f * (civ.EnergyMix.GetValueOrDefault(EnergySource.Solar) + civ.EnergyMix.GetValueOrDefault(EnergySource.Wind));
        civ.EnergyProduction = civ.EnergyDemand * Math.Clamp(supply, 0.3f, 1.1f);

        // ---- Power grid (from electrification, tech 28) ----
        civ.PowerLines.Clear();
        if (tech >= 28)
        {
            var capital = civ.Capital;
            var electrified = new List<City>();
            if (capital != null)
            {
                capital.Electrified = true;
                electrified.Add(capital);
            }

            float reach = 10f + (tech - 28) * 0.4f + (civ.HasProject("Electrification") ? 8f : 0f);
            foreach (var city in civ.Cities.Where(c => c != capital).OrderBy(c => capital == null ? 0 : WrappedDistance(c.X, c.Y, capital.X, capital.Y)))
            {
                var nearest = electrified.OrderBy(e => WrappedDistance(e.X, e.Y, city.X, city.Y)).FirstOrDefault();
                if (nearest != null && WrappedDistance(nearest.X, nearest.Y, city.X, city.Y) <= reach && city.Type >= CityType.Town || nearest != null && tech >= 55)
                {
                    city.Electrified = true;
                    electrified.Add(city);
                    civ.PowerLines.Add((nearest.X, nearest.Y, city.X, city.Y));
                }
                else
                {
                    city.Electrified = false;
                }
            }

            // Blackouts when production falls short
            if (civ.EnergyProduction < civ.EnergyDemand * 0.8f)
            {
                foreach (var city in civ.Cities.Where(c => !c.IsCapital && _random.NextDouble() < 0.3))
                {
                    city.Electrified = false;
                    city.Happiness = Math.Max(0f, city.Happiness - 0.05f);
                }
            }

            // Power plants in the largest cities, by dominant source
            var dominant = civ.EnergyMix.OrderByDescending(e => e.Value).First().Key;
            foreach (var city in civ.Cities)
            {
                city.HasPowerPlant = city.Type >= CityType.City && city.Electrified;
                city.PowerPlantType = city.HasPowerPlant
                    ? (IsNearNuclearPlant(city.X, city.Y, 3) ? EnergySource.Nuclear : dominant)
                    : null;
            }
        }
        else
        {
            foreach (var city in civ.Cities) city.Electrified = false;
        }
        civ.Electrification = civ.Cities.Count == 0 ? 0f : civ.Cities.Count(c => c.Electrified) / (float)civ.Cities.Count;

        // ---- Internet (tech 65) ----
        civ.DataCables.Clear();
        if (tech >= 65)
        {
            float target = Math.Clamp((tech - 65) / 40f, 0f, 1f) * civ.Electrification * (0.6f + civ.Prosperity * 0.4f);
            if (civ.HasProject("Computer Revolution")) target = Math.Min(1f, target + 0.2f);
            if (civ.HasProject("Global Trade Network")) target = Math.Min(1f, target + 0.1f);
            civ.InternetPenetration += (target - civ.InternetPenetration) * 0.15f;

            foreach (var city in civ.Cities)
            {
                city.Online = city.Electrified && (city.IsCapital || city.Type >= CityType.City || (city.Id % 100) / 100f < civ.InternetPenetration);
            }
            foreach (var (x1, y1, x2, y2) in civ.PowerLines)
            {
                var a = civ.Cities.FirstOrDefault(c => c.X == x1 && c.Y == y1);
                var b = civ.Cities.FirstOrDefault(c => c.X == x2 && c.Y == y2);
                if (a?.Online == true && b?.Online == true) civ.DataCables.Add((x1, y1, x2, y2));
            }

            // Undersea cables to coastal trade partners
            var hub = civ.Cities.Where(c => c.Coastal && c.Online).OrderByDescending(c => c.Population).FirstOrDefault();
            if (hub != null)
            {
                foreach (var (otherId, relation) in civ.DiplomaticRelations)
                {
                    if (otherId < civ.Id || !relation.HasTreaty(TreatyType.TradePact)) continue;
                    var other = GetCivilizationById(otherId);
                    var otherHub = other?.Cities.Where(c => c.Coastal && c.Online).OrderByDescending(c => c.Population).FirstOrDefault();
                    if (otherHub != null) civ.DataCables.Add((hub.X, hub.Y, otherHub.X, otherHub.Y));
                }
            }
        }
        else
        {
            civ.InternetPenetration = 0f;
            foreach (var city in civ.Cities) city.Online = false;
        }

        // ---- Airports and spaceports ----
        foreach (var city in civ.Cities)
        {
            city.HasAirport = civ.HasAirTransport && (city.Type >= CityType.City || city.IsCapital);
        }
        if (civ.SpaceStage >= SpaceStage.Rocketry && !civ.Cities.Any(c => c.HasSpaceport))
        {
            // Launch from the city closest to the equator
            var port = civ.Cities.OrderBy(c => Math.Abs(c.Y - _map.Height / 2)).ThenByDescending(c => c.Population).FirstOrDefault();
            if (port != null) port.HasSpaceport = true;
        }
    }

    /// <summary>
    /// Industrial footprint per cell follows the energy mix: fossil fuels pollute, renewables and fusion don't.
    /// </summary>
    private float GetEmissionIntensity(Civilization civ)
    {
        if (civ.EnergyMix.Count == 0)
        {
            return civ.CivType switch
            {
                CivType.Tribal => 0.01f,
                CivType.Agricultural => 0.05f,
                _ => 0.4f
            };
        }

        float dirty = civ.EnergyMix.GetValueOrDefault(EnergySource.Coal) * 1.0f
                    + civ.EnergyMix.GetValueOrDefault(EnergySource.Oil) * 0.8f
                    + civ.EnergyMix.GetValueOrDefault(EnergySource.Biomass) * 0.15f;
        float intensity = civ.CivType switch
        {
            CivType.Tribal => 0.05f,
            CivType.Agricultural => 0.2f,
            CivType.Industrial => 0.9f,
            CivType.Scientific => 1f,
            CivType.Spacefaring => 1f,
            _ => 0.1f
        };
        return Math.Clamp(dirty * intensity, 0f, 1f);
    }

    #endregion

    #region National projects

    private record ProjectTemplate(string Name, ProjectKind Kind, int Tech, float Cost, string? Requires = null);

    private static readonly ProjectTemplate[] ProjectCatalogue =
    {
        // Research
        new("Great Library", ProjectKind.Research, 12, 150),
        new("Printing Press", ProjectKind.Research, 20, 250, "Great Library"),
        new("Scientific Academy", ProjectKind.Research, 35, 500, "Printing Press"),
        new("Electrification", ProjectKind.Research, 28, 600),
        new("Computer Revolution", ProjectKind.Research, 60, 1500, "Electrification"),
        new("Genome Project", ProjectKind.Research, 70, 2500, "Scientific Academy"),
        new("Artificial Intelligence", ProjectKind.Research, 95, 5000, "Computer Revolution"),
        new("Fusion Power", ProjectKind.Research, 100, 8000, "Scientific Academy"),
        // Economy
        new("Irrigation Works", ProjectKind.Economic, 8, 120),
        new("Road Network", ProjectKind.Economic, 15, 250),
        new("Central Bank", ProjectKind.Economic, 25, 500),
        new("Industrialization", ProjectKind.Economic, 32, 900),
        new("Green Revolution", ProjectKind.Economic, 55, 1500, "Industrialization"),
        new("Global Trade Network", ProjectKind.Economic, 65, 2000, "Central Bank"),
        new("Renewable Transition", ProjectKind.Environmental, 80, 3000),
        new("National Parks", ProjectKind.Environmental, 40, 400),
        new("Carbon Capture", ProjectKind.Environmental, 90, 4000, "Renewable Transition"),
        // Space
        new("Rocketry Program", ProjectKind.Space, 50, 1200),
        new("Satellite Program", ProjectKind.Space, 55, 1500, "Rocketry Program"),
        new("Crewed Spaceflight", ProjectKind.Space, 62, 2500, "Satellite Program"),
        new("Orbital Station", ProjectKind.Space, 70, 4000, "Crewed Spaceflight"),
        new("Lunar Base", ProjectKind.Space, 85, 7000, "Orbital Station"),
        new("Off-World Colony", ProjectKind.Space, 110, 15000, "Lunar Base"),
        // Military
        new("Standing Army", ProjectKind.Military, 10, 200),
        new("Chemical Weapons", ProjectKind.Military, 35, 600),
        new("Nuclear Weapons Program", ProjectKind.Military, 70, 4000, "Scientific Academy"),
        new("Bioweapons Program", ProjectKind.Military, 70, 3000, "Genome Project"),
        new("Missile Defense", ProjectKind.Military, 90, 6000, "Nuclear Weapons Program")
    };

    private void UpdateNationalProjects(Civilization civ, int currentYear)
    {
        if (civ.ActiveProject == null)
        {
            civ.ActiveProject = ChooseProject(civ, currentYear);
            if (civ.ActiveProject == null) return;
        }

        var project = civ.ActiveProject;
        var s = civ.Strategy;
        float share = project.Kind switch
        {
            ProjectKind.Research => s.ResearchShare,
            ProjectKind.Space => Math.Max(s.SpaceShare, 0.1f),
            ProjectKind.Military => s.MilitaryShare,
            _ => s.EconomyShare * 0.5f
        };

        // Invest part of the treasury and of this year's income
        float investment = Math.Max(0f, civ.Gold) * 0.15f * share + Math.Max(0f, civ.GoldIncome) * share;
        investment = Math.Min(investment, Math.Max(0f, civ.Gold));
        civ.Gold -= investment;
        project.Progress += investment * (0.8f + civ.DevelopmentModifier * 0.2f);

        if (project.Progress >= project.Cost)
        {
            project.CompletedYear = currentYear;
            civ.CompletedProjects.Add(project);
            civ.ActiveProject = null;
            CompleteProject(civ, project, currentYear);
        }
    }

    private NationalProject? ChooseProject(Civilization civ, int currentYear)
    {
        var posture = civ.Strategy.Posture;
        var ideology = GetRulingIdeology(civ);
        bool accountable = civ.Government?.Type is GovernmentType.Democracy or GovernmentType.Federation or GovernmentType.Republic;
        var rival = civ.Strategy.MainThreatId.HasValue ? GetCivilizationById(civ.Strategy.MainThreatId.Value) : null;

        ProjectTemplate? best = null;
        float bestScore = 0f;
        foreach (var t in ProjectCatalogue)
        {
            if (civ.TechLevel < t.Tech || civ.HasProject(t.Name)) continue;
            if (t.Requires != null && !civ.HasProject(t.Requires)) continue;

            float score = t.Kind switch
            {
                ProjectKind.Research => posture == NationalPosture.Research ? 1.2f : 0.7f,
                ProjectKind.Economic => posture is NationalPosture.Develop or NationalPosture.Recover or NationalPosture.Expand ? 1.1f : 0.6f,
                ProjectKind.Space => posture == NationalPosture.SpaceRace ? 1.4f : 0.4f,
                ProjectKind.Environmental => 0.3f + civ.EcoFriendliness * 0.6f + (ideology == Ideology.Green ? 0.6f : 0f),
                ProjectKind.Military => posture is NationalPosture.Militarize or NationalPosture.Fortify or NationalPosture.Conquer ? 1f : 0.15f,
                _ => 0.5f
            };

            // Weapons of mass destruction are pursued only under pressure, and never lightly
            switch (t.Name)
            {
                case "Nuclear Weapons Program":
                    bool rivalNuclear = rival?.HasNuclearWeapons == true;
                    score = (rivalNuclear ? 1.3f : 0f) + (civ.Strategy.ThreatLevel > 1f ? 0.5f : 0f)
                            + (civ.Aggression > 0.7f ? 0.3f : 0f) - (ideology == Ideology.Green ? 0.8f : 0f);
                    break;
                case "Chemical Weapons":
                case "Bioweapons Program":
                    score = accountable ? 0f : ((civ.Government?.CurrentRuler?.Brutality ?? 0f) > 0.7f && civ.Strategy.ThreatLevel > 0.8f ? 0.8f : 0f);
                    break;
                case "Missile Defense":
                    score = rival?.HasNuclearWeapons == true ? 1.2f : 0f;
                    break;
                case "Fusion Power":
                case "Renewable Transition":
                    if (NuclearWinter > 0 || _map.GlobalCO2 > 3f) score += 0.4f;
                    break;
            }

            if (ideology == Ideology.Technocratic && t.Kind is ProjectKind.Research or ProjectKind.Space) score += 0.3f;
            if (ideology == Ideology.Nationalist && t.Kind == ProjectKind.Military) score += 0.3f;
            score /= 1f + t.Cost / 5000f; // Prefer affordable programmes

            if (score > bestScore)
            {
                bestScore = score;
                best = t;
            }
        }

        if (best == null || bestScore < 0.1f) return null;
        return new NationalProject { Name = best.Name, Kind = best.Kind, Cost = best.Cost, StartedYear = currentYear };
    }

    private void CompleteProject(Civilization civ, NationalProject project, int currentYear)
    {
        var category = project.Kind switch
        {
            ProjectKind.Space => HistoryCategory.Space,
            ProjectKind.Research => HistoryCategory.Science,
            ProjectKind.Military => HistoryCategory.War,
            _ => HistoryCategory.Growth
        };
        string text = $"The {civ.Name} complete the {project.Name}";

        switch (project.Name)
        {
            case "Irrigation Works":
            case "Green Revolution":
            case "Road Network":
            case "Central Bank":
            case "Industrialization":
            case "Great Library":
            case "Printing Press":
            case "Scientific Academy":
            case "Artificial Intelligence":
            case "Genome Project":
                break; // Passive bonuses (see GetProjectBonuses)
            case "Standing Army":
                civ.Stability = Math.Min(1f, civ.Stability + 0.05f);
                break;
            case "National Parks":
                civ.EcoFriendliness = Math.Min(1f, civ.EcoFriendliness + 0.15f);
                break;
            case "Renewable Transition":
                civ.EmissionReduction = Math.Max(civ.EmissionReduction, 0.6f);
                break;
            case "Carbon Capture":
                civ.EmissionReduction = Math.Max(civ.EmissionReduction, 0.85f);
                break;
            case "Nuclear Weapons Program":
                civ.HasNuclearWeapons = true;
                civ.NuclearStockpile = Math.Max(civ.NuclearStockpile, 3);
                text = $"The {civ.Name} test their first nuclear weapon";
                foreach (var other in _civilizations.Where(o => o.Id != civ.Id))
                {
                    if (other.DiplomaticRelations.TryGetValue(civ.Id, out var r)) r.Opinion -= 10;
                }
                break;
            case "Chemical Weapons":
                civ.Arsenal.ChemicalStockpile = 5;
                text = $"The {civ.Name} stockpile chemical weapons";
                break;
            case "Bioweapons Program":
                civ.Arsenal.BioweaponProgram = true;
                text = $"The {civ.Name} secretly engineer biological weapons";
                break;
            case "Missile Defense":
                civ.Arsenal.MissileDefense = true;
                break;
            case "Rocketry Program":
                civ.SpaceStage = SpaceStage.Rocketry;
                text = $"The {civ.Name} launch their first rocket";
                break;
            case "Satellite Program":
                civ.SpaceStage = SpaceStage.Satellites;
                LaunchSatellite(civ, currentYear, "Sat-1");
                text = $"The {civ.Name} put the first satellite in orbit";
                break;
            case "Crewed Spaceflight":
                civ.SpaceStage = SpaceStage.CrewedFlight;
                text = $"An astronaut of the {civ.Name} orbits the planet";
                break;
            case "Orbital Station":
                civ.SpaceStage = SpaceStage.SpaceStation;
                AddOrbitalObject(civ, OrbitalObjectType.SpaceStation, $"{GenerateWord(civ.Culture, 2, 2)} Station", 6, currentYear);
                text = $"The {civ.Name} open a permanently crewed space station";
                break;
            case "Lunar Base":
                civ.SpaceStage = SpaceStage.LunarBase;
                AddOrbitalObject(civ, OrbitalObjectType.LunarBase, $"{GenerateWord(civ.Culture, 2, 2)} Lunar Base", 20, currentYear);
                text = $"The {civ.Name} found a base on the moon";
                break;
            case "Off-World Colony":
                civ.SpaceStage = SpaceStage.OffWorldColony;
                AddOrbitalObject(civ, OrbitalObjectType.Colony, $"New {GenerateWord(civ.Culture, 2, 2)}", 500, currentYear);
                text = $"The {civ.Name} found a self-sufficient colony beyond the planet: the species can no longer die out";
                break;
            case "Fusion Power":
                text = $"The {civ.Name} achieve controlled fusion: clean, limitless energy";
                break;
        }

        AddChronicle(currentYear, category, text, civ.CenterX, civ.CenterY, civ.Id);
    }

    /// <summary>
    /// Research multiplier from completed programmes, the internet and geography.
    /// </summary>
    private float GetResearchBonus(Civilization civ)
    {
        float bonus = civ.DevelopmentModifier;
        if (civ.HasProject("Great Library")) bonus += 0.1f;
        if (civ.HasProject("Printing Press")) bonus += 0.15f;
        if (civ.HasProject("Scientific Academy")) bonus += 0.2f;
        if (civ.HasProject("Computer Revolution")) bonus += 0.2f;
        if (civ.HasProject("Artificial Intelligence")) bonus += 0.4f;
        bonus += civ.InternetPenetration * 0.3f;
        bonus *= 0.7f + civ.Strategy.ResearchShare;
        return bonus;
    }

    private float GetFoodBonus(Civilization civ)
    {
        float bonus = 1f;
        if (civ.HasProject("Irrigation Works")) bonus += 0.1f;
        if (civ.HasProject("Green Revolution")) bonus += 0.35f;
        return bonus;
    }

    private float GetGoldBonus(Civilization civ)
    {
        float bonus = 1f;
        if (civ.HasProject("Road Network")) bonus += 0.1f;
        if (civ.HasProject("Central Bank")) bonus += 0.2f;
        if (civ.HasProject("Industrialization")) bonus += 0.2f;
        if (civ.HasProject("Global Trade Network")) bonus += 0.25f;
        bonus += civ.InternetPenetration * 0.2f;
        return bonus;
    }

    #endregion

    #region Space

    private void LaunchSatellite(Civilization civ, int currentYear, string name)
    {
        civ.Satellites++;
        AddOrbitalObject(civ, OrbitalObjectType.Satellite, $"{civ.NameRoot}-{name}", 0, currentYear);
    }

    private void AddOrbitalObject(Civilization civ, OrbitalObjectType type, string name, int crew, int currentYear)
    {
        _orbitalObjects.Add(new OrbitalObject
        {
            Id = _nextOrbitalId++,
            CivilizationId = civ.Id,
            Name = name,
            Type = type,
            Crew = crew,
            OrbitAngle = (float)(_random.NextDouble() * Math.PI * 2),
            OrbitRadius = type switch
            {
                OrbitalObjectType.Satellite => 1.05f + (float)_random.NextDouble() * 0.15f,
                OrbitalObjectType.SpaceStation => 1.1f,
                OrbitalObjectType.LunarBase => 1.6f,
                _ => 2.2f
            },
            LaunchedYear = currentYear,
            TechLevel = civ.TechLevel,
            Culture = civ.Culture
        });
        civ.Astronauts = _orbitalObjects.Where(o => o.CivilizationId == civ.Id).Sum(o => o.Crew);
    }

    /// <summary>
    /// Space programmes keep launching satellites and rotating crews. When a nation falls, its
    /// crews in orbit survive; once the surface is habitable again they return to found a new
    /// nation that remembers everything its ancestors knew.
    /// </summary>
    private void UpdateSpace(int currentYear)
    {
        foreach (var civ in _civilizations)
        {
            if (civ.SpaceStage >= SpaceStage.Satellites && civ.Satellites < 3 + civ.TechLevel / 10 &&
                civ.Strategy.SpaceShare > 0.05f && civ.Gold > 200 && _random.NextDouble() < 0.2)
            {
                civ.Gold -= 100;
                LaunchSatellite(civ, currentYear, $"Sat-{civ.Satellites + 1}");
            }
            if (civ.Satellites > 0)
            {
                // Weather satellites help prepare for disasters
                civ.DisasterPreparedness = Math.Min(0.9f, civ.DisasterPreparedness + 0.01f);
                civ.Arsenal.SpySatellites = civ.HasNuclearWeapons ? Math.Min(civ.Satellites, 5) : 0;
            }
            civ.Astronauts = _orbitalObjects.Where(o => o.CivilizationId == civ.Id).Sum(o => o.Crew);
        }

        foreach (var obj in _orbitalObjects)
        {
            obj.OrbitAngle = (obj.OrbitAngle + 0.4f / obj.OrbitRadius) % (MathF.PI * 2);
            bool nationAlive = _civilizations.Any(c => c.Id == obj.CivilizationId);
            if (!nationAlive && !obj.Orphaned)
            {
                obj.Orphaned = true;
                if (obj.Crew > 0)
                {
                    AddChronicle(currentYear, HistoryCategory.Space,
                        $"Their nation is gone: {obj.Crew} people aboard {obj.Name} are now alone in space", -1, -1, obj.CivilizationId);
                }
            }
        }

        // Uncrewed satellites of vanished nations slowly fall back to the planet
        _orbitalObjects.RemoveAll(o => o.Orphaned && o.Crew == 0 && _random.NextDouble() < 0.05);

        // Stranded crews return when the skies clear
        foreach (var obj in _orbitalObjects.Where(o => o.Orphaned && o.Crew > 0).ToList())
        {
            if (NuclearWinter > 0.15f) continue;

            var site = FindReturnSite();
            if (site == null) continue;
            if (_random.NextDouble() > 0.3) continue;

            int returning = obj.Type == OrbitalObjectType.Colony ? Math.Min(obj.Crew, 200) : obj.Crew;
            obj.Crew -= returning;

            var civ = CreateCivilization(site.Value.x, site.Value.y, currentYear);
            civ.Culture = obj.Culture;
            civ.TechLevel = Math.Max(obj.TechLevel - 10, 30);
            civ.CivType = civ.TechLevel > 60 ? CivType.Scientific : CivType.Industrial;
            civ.HasLandTransport = civ.HasSeaTransport = civ.HasRailTransport = true;
            civ.HasAirTransport = civ.TechLevel >= 50;
            civ.EcoFriendliness = Math.Max(civ.EcoFriendliness, 0.7f); // They saw what happened
            civ.Aggression = Math.Min(civ.Aggression, 0.3f);
            var capital = civ.Capital;
            if (capital != null) capital.Population = Math.Max(returning * 20, 200); // Survivors and those they find
            RecalculatePopulation(civ);
            RebuildOwnerMap();

            AddChronicle(currentYear, HistoryCategory.Space,
                $"The crew of {obj.Name} returns to the surface and founds the {civ.Name}", site.Value.x, site.Value.y, civ.Id);

            if (obj.Crew <= 0) _orbitalObjects.Remove(obj);
        }
    }

    private (int x, int y)? FindReturnSite()
    {
        (int x, int y)? best = null;
        float bestScore = float.MinValue;
        for (int attempt = 0; attempt < 200; attempt++)
        {
            int x = _random.Next(_map.Width);
            int y = _random.Next(_map.Height / 8, _map.Height * 7 / 8);
            var cell = _map.Cells[x, y];
            if (!cell.IsLand || cell.Temperature < 0 || cell.Temperature > 35 || OwnerAt(x, y) != 0) continue;
            if (cell.GetGeology().RadioactiveContamination > 0.2f) continue;
            float score = EvaluateSettlementSite(_civilizations.FirstOrDefault() ?? new Civilization(), x, y);
            if (score > bestScore)
            {
                bestScore = score;
                best = (x, y);
            }
        }
        return best;
    }

    #endregion

    #region Epidemics

    private static readonly string[] PlagueNames = { "Plague", "Fever", "Pox", "Flu", "Sickness", "Blight", "Cough" };

    /// <summary>
    /// Crowded cities, livestock, trade and hot, wet climates breed epidemics; medicine,
    /// sanitation (the Genome Project) and cold climates keep them rare.
    /// </summary>
    private void CheckForEpidemics(int currentYear)
    {
        if (_diseaseManager == null) return;
        if (_diseaseManager.Diseases.Count(d => d.IsActive && !d.CureDeployed) >= 2) return;

        foreach (var civ in _civilizations)
        {
            int crowded = civ.Cities.Count(c => c.Type >= CityType.City);
            if (crowded == 0 && civ.Cities.Count < 6) continue;

            float chance = 0.0015f * (crowded + civ.Cities.Count * 0.1f);
            if (civ.CivType >= CivType.Agricultural) chance *= 1.5f;           // Livestock
            chance *= 1f + Math.Min(civ.TradeRoutes.Count, 5) * 0.1f;          // Travellers
            if (civ.Homeland == HomelandClimate.Tropical) chance *= 1.5f;
            if (civ.Homeland == HomelandClimate.Cold) chance *= 0.6f;
            if (civ.TechLevel >= 45) chance *= 0.5f;                           // Sanitation, vaccines
            if (civ.HasProject("Genome Project")) chance *= 0.4f;
            if (civ.Cities.Any(c => c.Starving)) chance *= 1.5f;

            if (_random.NextDouble() >= chance) continue;

            var origin = civ.Cities.OrderByDescending(c => c.Population).First();
            var type = _random.NextDouble() switch
            {
                < 0.45 => PathogenType.Virus,
                < 0.8 => PathogenType.Bacteria,
                < 0.95 => PathogenType.Parasite,
                _ => PathogenType.Fungus
            };
            string name = $"{origin.Name} {PlagueNames[_random.Next(PlagueNames.Length)]}";
            var disease = _diseaseManager.CreateDisease(name, type, origin.X, origin.Y);
            disease.OriginCivId = civ.Id;

            // Natural diseases vary; modern medicine softens them
            disease.Lethality *= 0.4f + (float)_random.NextDouble() * 0.6f;
            if (civ.TechLevel >= 60) disease.Lethality *= 0.5f;

            AddChronicle(currentYear, HistoryCategory.Epidemic,
                $"An epidemic breaks out in {origin.Name}: the {name} ({type.ToString().ToLower()})", origin.X, origin.Y, civ.Id);
            return; // At most one new outbreak per year
        }
    }

    #endregion
}
