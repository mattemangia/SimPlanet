namespace SimPlanet;

/// <summary>
/// Manages intelligent civilizations and their development
/// </summary>
public partial class CivilizationManager
{
    private readonly PlanetMap _map;
    private readonly Random _random;
    private List<Civilization> _civilizations;
    private readonly object _civLock = new object();
    private int _nextCivId = 1;
    private DivinePowers _divinePowers;
    private int _nextRulerId = 1;
    private WeatherSystem? _weatherSystem;
    private DisasterManager? _disasterManager;
    private float _pendingGlobalEmissions;

    public List<Civilization> Civilizations => _civilizations;
    public DivinePowers DivinePowers => _divinePowers;

    public void SetWeatherSystem(WeatherSystem weatherSystem)
    {
        _weatherSystem = weatherSystem;
    }

    public void SetDisasterManager(DisasterManager disasterManager)
    {
        _disasterManager = disasterManager;
    }

    public CivilizationManager(PlanetMap map, int seed)
    {
        _map = map;
        // Use non-deterministic random for civilization behavior and events
        _random = new Random();
        _civilizations = new List<Civilization>();
        _divinePowers = new DivinePowers(_random);
    }

    public void Update(float deltaTime, int currentYear)
    {
        lock (_civLock)
        {
            // Continuous systems (every simulation step)
            foreach (var civ in _civilizations.ToList())
            {
                UpdateCivilization(civ, deltaTime, currentYear);
            }

            // Spread global emissions across entire planet (atmospheric mixing happens faster)
            if (_pendingGlobalEmissions > 0)
            {
                for (int x = 0; x < _map.Width; x++)
                {
                    for (int y = 0; y < _map.Height; y++)
                    {
                        _map.Cells[x, y].CO2 += _pendingGlobalEmissions;
                    }
                }
                _pendingGlobalEmissions = 0;
            }

            // Armies march and fight continuously so wars are visible on the map
            UpdateArmies(deltaTime / GameState.SecondsPerGameYear, currentYear);
            AgeBattleEffects(deltaTime);

            // Societal systems run once per simulated year
            if (_lastYearProcessed == int.MinValue || currentYear < _lastYearProcessed)
            {
                _lastYearProcessed = currentYear - 1;
            }

            int yearsToProcess = Math.Min(currentYear - _lastYearProcessed, 3);
            for (int i = 0; i < yearsToProcess; i++)
            {
                RunYearlyTick(currentYear);
            }
            _lastYearProcessed = currentYear;
        }
    }

    /// <summary>
    /// Everything that should happen once per game year: economy, settlements,
    /// technology, government, diplomacy and war decisions.
    /// </summary>
    private void RunYearlyTick(int currentYear)
    {
        CheckForNewCivilizations(currentYear);
        RebuildOwnerMap();

        foreach (var civ in _civilizations.ToList())
        {
            UpdateCivilizationYearly(civ, currentYear);
        }

        UpdateGovernments(currentYear);
        UpdateDiplomacy(currentYear);
        HandleCivilizationInteractions(currentYear);
        UpdateWarfare(currentYear);
        CheckRebellions(currentYear);
        UpdateDisasterResponse(currentYear);
        ApplyEarthquakeDamage(currentYear);
        UpdateSpace(currentYear);
        CheckForEpidemics(currentYear);
        UpdatePolitics(currentYear);
        UpdateHumanFootprint();

        foreach (var civ in _civilizations.ToList())
        {
            CheckCivilizationCollapse(civ, currentYear);
        }
    }

    public bool TryCreateCivilizationAt(int x, int y, int currentYear)
    {
        lock (_civLock)
        {
            if (x < 0 || x >= _map.Width || y < 0 || y >= _map.Height)
                return false;

            var cell = _map.Cells[x, y];

            if (!cell.IsLand)
                return false;
            if (cell.Temperature < -10 || cell.Temperature > 45)
                return false;
            if (cell.Oxygen < 15) // Reduced from 18 - civilizations can adapt to lower oxygen
                return false;

            if (IsCellInCivilization(x, y))
                return false;

            foreach (var civ in _civilizations)
            {
                if (Math.Abs(civ.CenterX - x) < 20 && Math.Abs(civ.CenterY - y) < 20)
                    return false;
            }

            cell.LifeType = LifeForm.Civilization;
            cell.Biomass = Math.Max(cell.Biomass, 0.5f);
            cell.Rainfall = Math.Max(cell.Rainfall, 0.3f);
            cell.Temperature = Math.Clamp(cell.Temperature, 0, 35);

            CreateCivilization(x, y, currentYear);
            RebuildOwnerMap();
            return true;
        }
    }

    private void CheckForNewCivilizations(int currentYear)
    {
        // Scan for intelligence-level life that could form civilizations (evaluated once per year)
        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                var cell = _map.Cells[x, y];

                // Intelligence level life can form civilizations
                if (cell.LifeType == LifeForm.Intelligence &&
                    cell.Biomass > 0.6f &&
                    _random.NextDouble() < 0.01 &&
                    OwnerAt(x, y) == 0 &&
                    !_civilizations.Any(c => WrappedDistance(c.CenterX, c.CenterY, x, y) < 20))
                {
                    CreateCivilization(x, y, currentYear);
                    RebuildOwnerMap();
                }
            }
        }
    }

    private Civilization CreateCivilization(int x, int y, int currentYear = 0)
    {
        var cell = _map.Cells[x, y];
        int culture = _random.Next(CultureSyllables.Length);

        var civ = new Civilization
        {
            Id = _nextCivId++,
            Culture = culture,
            Name = GenerateCivilizationName(culture, out string nameRoot),
            NameRoot = nameRoot,
            CenterX = x,
            CenterY = y,
            Population = 0,
            TechLevel = 0,
            CivType = CivType.Tribal,
            Aggression = (float)_random.NextDouble(),
            EcoFriendliness = (float)_random.NextDouble(),
            Founded = currentYear,
            Prosperity = 0.55f,
            Stability = 0.6f,
            CollapseRisk = 0.0f
        };

        // Initialize government (Tribal starts as Chiefdom)
        civ.Government = new Government(GovernmentType.Tribal, currentYear);

        // Create first ruler
        var ruler = _divinePowers.GenerateRandomRuler(civ, currentYear);
        ruler.Id = _nextRulerId++;
        civ.Government.CurrentRuler = ruler;
        civ.AllRulers.Add(ruler);

        // Initial territory
        civ.Territory.Add((x, y));
        ExpandTerritory(civ, 3); // Start with small territory

        _civilizations.Add(civ);

        // Mark cells as civilization
        foreach (var (tx, ty) in civ.Territory)
        {
            _map.Cells[tx, ty].LifeType = LifeForm.Civilization;
        }

        // Initialize diplomatic relations with existing civilizations
        foreach (var otherCiv in _civilizations.Where(c => c.Id != civ.Id))
        {
            var relation = new DiplomaticRelation(civ.Id, otherCiv.Id, currentYear);
            civ.DiplomaticRelations[otherCiv.Id] = relation;
            otherCiv.DiplomaticRelations[civ.Id] = relation;
        }

        AssignHomeland(civ);

        // Every people starts from a single tribal village that becomes its capital
        var capital = FoundSettlement(civ, x, y, 400 + _random.Next(600), currentYear);
        capital.IsCapital = true;
        civ.Population = civ.Cities.Sum(c => c.Population);
        civ.Food = 60f;

        AddChronicle(currentYear, HistoryCategory.Founding,
            $"The {civ.Name} emerge and settle {capital.Name}", x, y, civ.Id);

        return civ;
    }

    /// <summary>
    /// Continuous per-step effects (pollution, plant risk). Heavy societal logic lives in the yearly tick.
    /// </summary>
    private void UpdateCivilization(Civilization civ, float deltaTime, int currentYear)
    {
        // Update nuclear plant meltdown risk
        UpdateNuclearPlantRisk(civ, deltaTime, currentYear);

        // Environmental impact
        ApplyEnvironmentalImpact(civ, deltaTime);
    }

    private void UpdateCivilizationYearly(Civilization civ, int currentYear)
    {
        // Absorb population changes applied by other systems (disease, divine powers, disasters)
        SyncPopulationFromExternalChanges(civ);

        // Settlements: harvests, growth, famine, buildings, trade
        UpdateSettlements(civ, currentYear);

        // Found new villages when the land is fertile and the granaries are full
        TryFoundNewSettlement(civ, currentYear);

        // Resource extraction from deposits (mines, wells)
        ExtractNaturalResources(civ, GameState.SecondsPerGameYear);

        // Energy, power grid, internet, airports and spaceports
        UpdateInfrastructure(civ, currentYear);

        // National research, economic, space and military programmes
        UpdateNationalProjects(civ, currentYear);

        // Technology advancement driven by population, universities and rulers
        AdvanceTechnology(civ, currentYear);

        // Nuclear powers maintain an arsenal sized by their doctrine
        UpdateArsenal(civ);

        // Military strength = standing armies + militia potential
        civ.MilitaryStrength = (int)(GetSoldierCount(civ) * GetTechFactor(civ)) + (civ.Population / 1000) + civ.TechLevel * 10;

        // Update internal stability metrics so civilizations can recover from setbacks
        UpdateCivilizationStability(civ, 1.0f);
    }

    private void AdvanceTechnology(Civilization civ, int currentYear)
    {
        float science = civ.Cities.Sum(c => c.ScienceProduction);
        float wisdom = civ.Government?.CurrentRuler?.Wisdom ?? 0.5f;
        float chance = (0.04f + science * 0.006f) * (0.75f + wisdom * 0.5f) * (0.6f + civ.Stability * 0.6f) * GetResearchBonus(civ);
        chance /= 1f + civ.TechLevel / 30f + Math.Max(0, civ.TechLevel - 100) / 15f; // Each discovery is harder than the last
        chance = Math.Min(chance, 0.35f);

        if (_random.NextDouble() >= chance)
            return;

        civ.TechLevel++;

        // Advance civilization type
        var previousType = civ.CivType;
        if (civ.TechLevel > 10 && civ.CivType == CivType.Tribal)
        {
            civ.CivType = CivType.Agricultural;
        }
        else if (civ.TechLevel > 30 && civ.CivType == CivType.Agricultural)
        {
            civ.CivType = CivType.Industrial;
        }
        else if (civ.TechLevel > 60 && civ.CivType == CivType.Industrial)
        {
            civ.CivType = CivType.Scientific;
        }
        else if (civ.TechLevel > 100 && civ.CivType == CivType.Scientific)
        {
            civ.CivType = CivType.Spacefaring;
        }

        if (previousType != civ.CivType)
        {
            string era = civ.CivType switch
            {
                CivType.Agricultural => "the Agricultural age",
                CivType.Industrial => "the Industrial revolution",
                CivType.Scientific => "the Scientific age",
                CivType.Spacefaring => "the Space age",
                _ => civ.CivType.ToString()
            };
            AddChronicle(currentYear, HistoryCategory.Growth, $"The {civ.Name} enter {era}", civ.CenterX, civ.CenterY, civ.Id);
        }

        // Unlock transportation based on tech level
        if (civ.TechLevel >= 5 && !civ.HasLandTransport)
        {
            civ.HasLandTransport = true; // Horses/domestication
            BuildRoads(civ, currentYear); // Build basic dirt paths
        }
        if (civ.TechLevel == 10 && civ.Cities.Count > 0)
        {
            BuildRoads(civ, currentYear); // Upgrade to paved roads
        }
        if (civ.TechLevel >= 15 && !civ.HasSeaTransport)
        {
            civ.HasSeaTransport = true; // Ships
        }
        if (civ.TechLevel == 20 && civ.Cities.Count > 0)
        {
            BuildRoads(civ, currentYear); // Upgrade to highways
        }
        if (civ.TechLevel >= 25 && !civ.HasRailTransport)
        {
            civ.HasRailTransport = true; // Trains/railroads
            BuildRailroads(civ); // Build railroads connecting cities
        }
        if (civ.TechLevel >= 50 && !civ.HasAirTransport)
        {
            civ.HasAirTransport = true; // Airplanes
        }
        // Nuclear weapons are no longer automatic: they require a national weapons programme

        // Energy infrastructure at various tech levels
        if (civ.TechLevel == 45)
        {
            BuildWindTurbines(civ, currentYear); // Wind energy
        }
        if (civ.TechLevel == 60)
        {
            BuildNuclearPlants(civ, currentYear); // Nuclear power (before weapons)
        }
        if (civ.TechLevel == 80)
        {
            BuildSolarFarms(civ, currentYear); // Solar energy
        }
    }

    private void UpdateCivilizationStability(Civilization civ, float deltaTime)
    {
        // Prosperity is driven by food security, territory, and infrastructure
        float prosperityTarget = 0.5f;
        float foodRatio = civ.FoodConsumption <= 0.01f
            ? 1.5f
            : civ.FoodProduction / Math.Max(0.1f, civ.FoodConsumption);

        if (foodRatio > 1.2f)
            prosperityTarget += 0.2f;
        else if (foodRatio < 0.8f)
            prosperityTarget -= 0.2f;

        float stockpileRatio = civ.FoodConsumption <= 0.01f
            ? civ.Food / 50f
            : civ.Food / Math.Max(1f, civ.FoodConsumption);
        if (stockpileRatio > 4f)
            prosperityTarget += 0.1f;
        else if (stockpileRatio < 1f)
            prosperityTarget -= 0.1f;

        float territoryDensity = civ.Territory.Count / Math.Max(1f, civ.Population / 1000f);
        if (territoryDensity > 1.5f)
            prosperityTarget += 0.1f;
        else if (territoryDensity < 0.5f)
            prosperityTarget -= 0.1f;

        if (civ.Cities.Count > 0)
            prosperityTarget += 0.05f;

        civ.Prosperity = Math.Clamp(
            LerpTowards(civ.Prosperity, Math.Clamp(prosperityTarget, 0f, 1f), 0.5f * deltaTime),
            0f,
            1f);

        // Stability trends toward prosperity but is penalized by war and disasters
        float stabilityTarget = 0.4f + civ.Prosperity * 0.5f; // Increased base stability
        stabilityTarget += civ.DisasterPreparedness * 0.1f;
        stabilityTarget += civ.AtWar ? -0.1f : 0.1f;

        civ.Stability = Math.Clamp(
            LerpTowards(civ.Stability, Math.Clamp(stabilityTarget, 0f, 1f), 0.35f * deltaTime),
            0f,
            1f);

        // Collapse risk slowly accumulates only if stability remains low
        float riskDelta = 0f;
        if (civ.Stability < 0.4f)
            riskDelta += (0.4f - civ.Stability) * 0.4f * deltaTime; // Reduced penalty
        else
            riskDelta -= (civ.Stability - 0.4f) * 0.5f * deltaTime; // Increased bonus

        if (civ.Food <= civ.FoodConsumption)
            riskDelta += 0.02f * deltaTime; // Reduced penalty
        if (civ.Population < 1000)
            riskDelta += 0.02f * deltaTime; // Reduced penalty
        if (!civ.AtWar && civ.Stability > 0.6f)
            riskDelta -= 0.04f * deltaTime; // Increased bonus

        civ.CollapseRisk = Math.Clamp(civ.CollapseRisk + riskDelta, 0f, 1f);
    }

    private static float LerpTowards(float current, float target, float rate)
    {
        float clampedRate = Math.Clamp(rate, 0f, 1f);
        return current + (target - current) * clampedRate;
    }

    private void ExtractNaturalResources(Civilization civ, float deltaTime)
    {
        // Reset annual production tracking
        civ.AnnualProduction.Clear();
        civ.ProductionBonus = 1.0f;

        // Determine extraction tech level based on civilization type
        ExtractionTech civTech = civ.CivType switch
        {
            CivType.Tribal => ExtractionTech.Primitive,
            CivType.Agricultural => ExtractionTech.Medieval,
            CivType.Industrial => ExtractionTech.Industrial,
            CivType.Scientific => ExtractionTech.Modern,
            CivType.Spacefaring => ExtractionTech.Advanced,
            _ => ExtractionTech.Primitive
        };

        // Scan territory for resources
        foreach (var (x, y) in civ.Territory)
        {
            var cell = _map.Cells[x, y];
            var resources = cell.GetResources();

            foreach (var deposit in resources)
            {
                // Can we extract this resource?
                if (deposit.RequiredTech > civTech) continue;
                if (deposit.Amount <= 0) continue;

                // Discover resource if not yet found
                if (!deposit.Discovered)
                {
                    deposit.Discovered = true;
                    // Create a mine/well if tech level allows
                    if (!civ.ActiveMines.Exists(m => m.x == x && m.y == y && m.type == deposit.Type))
                    {
                        civ.ActiveMines.Add((x, y, deposit.Type));
                    }
                }

                // Calculate extraction rate based on tech and depth
                float baseExtraction = 0.001f; // Base extraction rate
                float techMultiplier = civTech switch
                {
                    ExtractionTech.Primitive => 0.5f,
                    ExtractionTech.Medieval => 1.0f,
                    ExtractionTech.Industrial => 3.0f,
                    ExtractionTech.Modern => 5.0f,
                    ExtractionTech.Advanced => 10.0f,
                    _ => 1.0f
                };

                // Deeper deposits are harder to extract
                float depthPenalty = 1.0f - (deposit.Depth * 0.5f);

                float extractionAmount = baseExtraction * techMultiplier * depthPenalty * deltaTime;

                // Extract resource
                float extracted = cell.ExtractResource(deposit.Type, extractionAmount);

                // Add to stockpile
                if (!civ.ResourceStockpile.ContainsKey(deposit.Type))
                {
                    civ.ResourceStockpile[deposit.Type] = 0;
                }
                civ.ResourceStockpile[deposit.Type] += extracted;

                // Track annual production
                if (!civ.AnnualProduction.ContainsKey(deposit.Type))
                {
                    civ.AnnualProduction[deposit.Type] = 0;
                }
                civ.AnnualProduction[deposit.Type] += extracted / deltaTime;
            }

            // Check for induced seismicity from resource extraction
            CheckInducedSeismicityAtCell(civ, cell, x, y);
        }

        // Calculate production bonus from strategic resources
        civ.ProductionBonus = 1.0f;

        // Iron boosts all production
        if (civ.ResourceStockpile.GetValueOrDefault(ResourceType.Iron, 0) > 1.0f)
        {
            civ.ProductionBonus += 0.2f;
        }

        // Coal/Oil boosts industrial output
        if (civ.CivType >= CivType.Industrial)
        {
            if (civ.ResourceStockpile.GetValueOrDefault(ResourceType.Coal, 0) > 0.5f ||
                civ.ResourceStockpile.GetValueOrDefault(ResourceType.Oil, 0) > 0.5f)
            {
                civ.ProductionBonus += 0.3f;
            }
        }

        // Uranium enables nuclear power
        if (civ.CivType >= CivType.Scientific &&
            civ.ResourceStockpile.GetValueOrDefault(ResourceType.Uranium, 0) > 0.1f)
        {
            civ.ProductionBonus += 0.5f;
        }

        // Apply production bonus to resource generation
        civ.MetalProduction *= civ.ProductionBonus;

        // Cap stockpiles
        foreach (var key in civ.ResourceStockpile.Keys.ToList())
        {
            civ.ResourceStockpile[key] = Math.Min(civ.ResourceStockpile[key], 100f);
        }
    }

    /// <summary>
    /// Check for civilization-induced seismicity from resource extraction
    /// </summary>
    private void CheckInducedSeismicityAtCell(Civilization civ, TerrainCell cell, int x, int y)
    {
        // Check if this cell has active resource extraction
        var activeMine = civ.ActiveMines.FirstOrDefault(m => m.x == x && m.y == y);
        if (activeMine == default) return;

        // Determine what type of extraction is happening
        bool hasOilExtraction = activeMine.type == ResourceType.Oil || activeMine.type == ResourceType.NaturalGas;
        bool hasFracking = civ.CivType >= CivType.Industrial &&
                          (activeMine.type == ResourceType.Oil || activeMine.type == ResourceType.NaturalGas);
        bool hasGeothermal = civ.CivType >= CivType.Scientific &&
                            cell.GetGeology().VolcanicActivity > 0.3f &&
                            cell.GetGeology().MagmaPressure > 0.5f; // Geothermal in volcanic areas

        // Only check if there's a risky activity
        if (hasOilExtraction || hasFracking || hasGeothermal)
        {
            EarthquakeSystem.CheckInducedSeismicity(_map, x, y, hasOilExtraction, hasFracking, hasGeothermal);
        }
    }

    private void ExpandTerritory(Civilization civ, int cells)
    {
        for (int i = 0; i < cells; i++)
        {
            // Find edge cells - land expansion
            var edgeCells = civ.Territory
                .SelectMany(pos => _map.GetNeighbors(pos.x, pos.y))
                .Where(n => !civ.Territory.Contains((n.x, n.y)) &&
                           !IsCellInCivilization(n.x, n.y) &&
                           n.cell.IsLand &&
                           n.cell.Temperature > -10 &&
                           n.cell.Temperature < 40)
                .Select(n => (n.x, n.y))
                .Distinct()
                .ToList();

            // If has sea transport, can also expand to nearby islands
            if (civ.HasSeaTransport)
            {
                var coastalCells = civ.Territory.Where(pos => _map.Cells[pos.x, pos.y].IsLand).ToList();
                foreach (var (cx, cy) in coastalCells)
                {
                    // Check cells within 5 cells distance across water
                    for (int dx = -5; dx <= 5; dx++)
                    {
                        for (int dy = -5; dy <= 5; dy++)
                        {
                            int nx = (cx + dx + _map.Width) % _map.Width;
                            int ny = Math.Clamp(cy + dy, 0, _map.Height - 1);

                            var targetCell = _map.Cells[nx, ny];
                            if (targetCell.IsLand &&
                                !civ.Territory.Contains((nx, ny)) &&
                                !IsCellInCivilization(nx, ny) &&
                                targetCell.Temperature > -10 &&
                                targetCell.Temperature < 40)
                            {
                                edgeCells.Add((nx, ny));
                            }
                        }
                    }
                }

                edgeCells = edgeCells.Distinct().ToList();
            }

            if (edgeCells.Count == 0) break;

            var newCell = edgeCells[_random.Next(edgeCells.Count)];
            civ.Territory.Add(newCell);
            _map.Cells[newCell.Item1, newCell.Item2].LifeType = LifeForm.Civilization;
        }
    }

    private void ApplyEnvironmentalImpact(Civilization civ, float deltaTime)
    {
        // Calculate base pollution per territory cell based on civ type
        float baseEmissions = civ.CivType switch
        {
            CivType.Tribal => 0.01f,
            CivType.Agricultural => 0.05f,
            CivType.Industrial => 2.5f,        // Massive emissions during industrial revolution
            CivType.Scientific => 1.0f,        // Still polluting but more efficient
            CivType.Spacefaring => 0.3f,       // Advanced tech, cleaner energy
            _ => 0
        };

        // Scale by population (more people = more emissions)
        float populationFactor = 1.0f + (civ.Population / 100000f);

        // Eco-friendly civilizations pollute much less
        float ecoMultiplier = 1.0f - (civ.EcoFriendliness * 0.7f);

        // Climate agreements reduce emissions
        float agreementMultiplier = 1.0f - civ.EmissionReduction;

        float actualEmissions = baseEmissions * populationFactor * ecoMultiplier * agreementMultiplier;

        // Global emissions spread across planet
        float globalEmissionsPerCell = (actualEmissions * civ.Territory.Count) / (_map.Width * _map.Height);

        foreach (var (x, y) in civ.Territory)
        {
            var cell = _map.Cells[x, y];

            // Local pollution: heavy in built-up areas, light in the countryside
            cell.CO2 += actualEmissions * deltaTime * (0.1f + cell.HumanFootprint);

            // Deforestation (except eco-friendly civs)
            if (cell.IsForest && civ.EcoFriendliness < 0.5f && _random.NextDouble() < 0.001)
            {
                cell.Biomass *= 0.5f; // Cut down forests
                cell.Rainfall -= 0.1f; // Affects local climate
            }

            // Advanced civs can terraform
            if (civ.CivType == CivType.Scientific || civ.CivType == CivType.Spacefaring)
            {
                if (civ.EcoFriendliness > 0.7f)
                {
                    // Restore ecosystems
                    if (cell.Biomass < 0.5f)
                    {
                        cell.Biomass += 0.01f * deltaTime;
                    }

                    // Carbon capture technology
                    if (cell.CO2 > 1.0f)
                    {
                        cell.CO2 -= 0.2f * deltaTime;
                    }
                }
            }
        }

        // Global emissions are spread across the planet once per step for all civilizations
        _pendingGlobalEmissions += globalEmissionsPerCell * deltaTime * 0.1f;

        // Global warming comes from the greenhouse gases emitted above (the atmosphere and
        // climate simulators turn CO2 and methane into heat); sunlight itself is not changed.
    }

    private void CheckCivilizationCollapse(Civilization civ, int currentYear)
    {
        if (!_civilizations.Contains(civ)) return;

        // Environmental collapse pressure over time instead of instant elimination
        if (civ.Territory.Count > 0)
        {
            float avgCO2 = civ.Territory.Average(pos => _map.Cells[pos.x, pos.y].CO2);
            float avgTemp = civ.Territory.Average(pos => _map.Cells[pos.x, pos.y].Temperature);

            bool harshClimate = avgCO2 > 10 || avgTemp > 45 || avgTemp < -15;
            if (harshClimate)
            {
                // Advanced civilizations can mitigate harsh climate
                float loss = civ.TechLevel >= 40 ? 0.01f : 0.03f;
                foreach (var city in civ.Cities)
                {
                    city.Population = (int)(city.Population * (1f - loss));
                }
                RecalculatePopulation(civ);

                civ.Stability = Math.Max(civ.Stability - 0.05f, 0f);
                civ.CollapseRisk = Math.Clamp(civ.CollapseRisk + 0.05f, 0f, 1f);
            }
            else
            {
                civ.CollapseRisk = Math.Max(civ.CollapseRisk - 0.02f, 0f);
            }
        }

        // Remove settlements that have been abandoned
        foreach (var city in civ.Cities.Where(c => c.Population < 40).ToList())
        {
            AbandonSettlement(civ, city, currentYear);
        }

        if (civ.Cities.Count == 0)
        {
            // Survivors try to rebuild a village somewhere in their remaining land
            if (civ.Population >= 100 && civ.Territory.Count > 0)
            {
                var site = civ.Territory
                    .Where(p => _map.Cells[p.x, p.y].IsLand)
                    .OrderByDescending(p => EvaluateSettlementSite(civ, p.x, p.y))
                    .FirstOrDefault(p => true);
                if (_map.Cells[site.x, site.y].IsLand)
                {
                    var capital = FoundSettlement(civ, site.x, site.y, Math.Min(civ.Population, 800), currentYear);
                    capital.IsCapital = true;
                    RecalculatePopulation(civ);
                    AddChronicle(currentYear, HistoryCategory.Founding,
                        $"Survivors of the {civ.Name} rebuild at {capital.Name}", site.x, site.y, civ.Id);
                    return;
                }
            }

            AddChronicle(currentYear, HistoryCategory.Rebellion,
                $"The {civ.Name} civilization has vanished", civ.CenterX, civ.CenterY, civ.Id);
            CollapseCivilization(civ);
        }
    }

    private void CollapseCivilization(Civilization civ)
    {
        // Revert cells to pre-civilization state
        foreach (var (x, y) in civ.Territory)
        {
            var cell = _map.Cells[x, y];
            if (cell.LifeType == LifeForm.Civilization)
            {
                cell.LifeType = LifeForm.Intelligence;
                cell.Biomass *= 0.5f;
            }
        }

        _armies.RemoveAll(a => a.CivilizationId == civ.Id);
        foreach (var other in _civilizations)
        {
            other.DiplomaticRelations.Remove(civ.Id);
            if (other.WarTargetId == civ.Id)
            {
                other.WarTargetId = null;
            }
            other.AtWar = other.DiplomaticRelations.Values.Any(r => r.Status == DiplomaticStatus.War);
        }

        _civilizations.Remove(civ);
        RebuildOwnerMap();
    }

    /// <summary>
    /// Peaceful interactions between neighbouring civilizations: technology diffusion,
    /// trade and climate cooperation. Wars are handled by the warfare system.
    /// </summary>
    private void HandleCivilizationInteractions(int currentYear)
    {
        foreach (var ((id1, id2), borderLength) in _borders)
        {
            var civ1 = GetCivilizationById(id1);
            var civ2 = GetCivilizationById(id2);
            if (civ1 == null || civ2 == null) continue;
            if (!civ1.DiplomaticRelations.TryGetValue(civ2.Id, out var relation)) continue;
            if (relation.Status == DiplomaticStatus.War || relation.Status == DiplomaticStatus.Hostile) continue;

            // Ideas travel across friendly borders: the less advanced neighbour catches up
            if (relation.Opinion > 10 && Math.Abs(civ1.TechLevel - civ2.TechLevel) > 2 && _random.NextDouble() < 0.06)
            {
                var laggard = civ1.TechLevel < civ2.TechLevel ? civ1 : civ2;
                laggard.TechLevel++;
            }

            // Establish trade routes
            if (relation.Status >= DiplomaticStatus.Friendly || relation.HasTreaty(TreatyType.TradePact))
            {
                if (!civ1.TradeRoutes.Contains((civ2.CenterX, civ2.CenterY)))
                {
                    civ1.TradeRoutes.Add((civ2.CenterX, civ2.CenterY));
                }
                if (!civ2.TradeRoutes.Contains((civ1.CenterX, civ1.CenterY)))
                {
                    civ2.TradeRoutes.Add((civ1.CenterX, civ1.CenterY));
                }
            }

            // Climate agreements for advanced civilizations
            if ((civ1.CivType == CivType.Scientific || civ1.CivType == CivType.Spacefaring) &&
                (civ2.CivType == CivType.Scientific || civ2.CivType == CivType.Spacefaring) &&
                civ1.EcoFriendliness + civ2.EcoFriendliness > 0.9f)
            {
                // Check if global CO2 is high enough to motivate action
                if (_map.GlobalCO2 > 3.0f && !civ1.InClimateAgreement && !civ2.InClimateAgreement)
                {
                    civ1.InClimateAgreement = true;
                    civ2.InClimateAgreement = true;
                    civ1.ClimatePartners.Add(civ2.Id);
                    civ2.ClimatePartners.Add(civ1.Id);

                    // Emission reduction targets (30-60% reduction)
                    float reductionTarget = 0.3f + (float)_random.NextDouble() * 0.3f;
                    civ1.EmissionReduction = Math.Max(civ1.EmissionReduction, reductionTarget);
                    civ2.EmissionReduction = Math.Max(civ2.EmissionReduction, reductionTarget);
                    relation.AddTreaty(new Treaty(TreatyType.ClimateAgreement, currentYear));

                    AddChronicle(currentYear, HistoryCategory.Diplomacy,
                        $"The {civ1.Name} and the {civ2.Name} sign a climate accord", civ1.CenterX, civ1.CenterY, civ1.Id);
                }
            }
        }
    }

    private bool IsCellInCivilization(int x, int y)
    {
        return _civilizations.Any(civ => civ.Territory.Contains((x, y)));
    }

    public List<Civilization> GetAllCivilizations()
    {
        lock (_civLock)
        {
            return _civilizations.ToList();
        }
    }

    /// <summary>
    /// Build roads connecting cities and resources
    /// </summary>
    private void BuildRoads(Civilization civ, int currentYear)
    {
        if (civ.Cities.Count == 0) return;

        // Determine road type based on tech level
        RoadType roadType = civ.TechLevel switch
        {
            >= 20 => RoadType.Highway,   // Modern highways
            >= 10 => RoadType.Road,      // Paved roads
            _ => RoadType.DirtPath       // Basic dirt paths
        };

        // Connect cities to each other
        foreach (var city in civ.Cities)
        {
            // Connect to nearest city
            City? nearestCity = null;
            float minDist = float.MaxValue;

            foreach (var other in civ.Cities)
            {
                if (city.Id == other.Id) continue;
                float dist = MathF.Sqrt((city.X - other.X) * (city.X - other.X) +
                                       (city.Y - other.Y) * (city.Y - other.Y));
                if (dist < minDist && dist < 50) // Only connect if within 50 cells
                {
                    minDist = dist;
                    nearestCity = other;
                }
            }

            if (nearestCity != null)
            {
                BuildRoadPath(civ, city.X, city.Y, nearestCity.X, nearestCity.Y, roadType, currentYear);
            }
        }

        // Connect cities to nearby resources
        foreach (var city in civ.Cities)
        {
            // Find resources within 20 cells
            for (int dx = -20; dx <= 20; dx++)
            {
                for (int dy = -20; dy <= 20; dy++)
                {
                    int rx = (city.X + dx + _map.Width) % _map.Width;
                    int ry = Math.Clamp(city.Y + dy, 0, _map.Height - 1);

                    float dist = MathF.Sqrt(dx * dx + dy * dy);
                    if (dist > 20) continue;

                    // Check if this is a mine/resource extraction site
                    if (civ.ActiveMines.Any(m => m.x == rx && m.y == ry))
                    {
                        BuildRoadPath(civ, city.X, city.Y, rx, ry, roadType, currentYear);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Build a road path between two points using simple line algorithm
    /// </summary>
    private void BuildRoadPath(Civilization civ, int x1, int y1, int x2, int y2, RoadType roadType, int currentYear)
    {
        // Bresenham-like line algorithm to trace road
        int dx = Math.Abs(x2 - x1);
        int dy = Math.Abs(y2 - y1);
        int sx = x1 < x2 ? 1 : -1;
        int sy = y1 < y2 ? 1 : -1;
        int err = dx - dy;

        int x = x1, y = y1;
        int steps = 0;
        int maxSteps = 1000; // Prevent infinite loops

        while (steps < maxSteps)
        {
            // Build road at this cell if it's land and in territory
            if (x >= 0 && x < _map.Width && y >= 0 && y < _map.Height)
            {
                var cell = _map.Cells[x, y];
                if (cell.IsLand && civ.Territory.Contains((x, y)))
                {
                    // Add to civilization's road network
                    civ.Roads.Add((x, y));

                    // Mark cell as having a road
                    var geo = cell.GetGeology();
                    if (!geo.HasRoad || geo.RoadType < roadType) // Upgrade if better road type
                    {
                        geo.HasRoad = true;
                        geo.RoadType = roadType;
                        geo.RoadBuiltYear = currentYear;

                        // Check if tunnel is needed for high mountains (tech level 10+)
                        if (cell.Elevation > 0.7f && civ.TechLevel >= 10)
                        {
                            geo.HasTunnel = true;
                        }
                        // Check for rockfall risk on mountain slopes (elevation 0.5-0.7)
                        else if (cell.Elevation > 0.5f && cell.Elevation <= 0.7f)
                        {
                            // Calculate slope to neighbors
                            float maxSlope = 0f;
                            foreach (var (nx, ny, neighbor) in _map.GetNeighbors(x, y))
                            {
                                float slope = Math.Abs(cell.Elevation - neighbor.Elevation);
                                maxSlope = Math.Max(maxSlope, slope);
                            }

                            // Steep slopes (>0.15 elevation difference) are at risk
                            if (maxSlope > 0.15f)
                            {
                                geo.RockfallRisk = true;
                            }
                        }
                    }
                }
            }

            // Check if we've reached the destination
            if (x == x2 && y == y2) break;

            int e2 = 2 * err;
            if (e2 > -dy)
            {
                err -= dy;
                x += sx;
                // Handle wrapping for x coordinate
                x = (x + _map.Width) % _map.Width;
            }
            if (e2 < dx)
            {
                err += dx;
                y += sy;
            }

            steps++;
        }
    }

    private void BuildRailroads(Civilization civ)
    {
        // Build railroads connecting major cities
        if (civ.Cities.Count < 2) return;

        // Connect nearest cities with railroads
        for (int i = 0; i < civ.Cities.Count; i++)
        {
            var city1 = civ.Cities[i];
            // Find nearest city
            City? nearestCity = null;
            float minDist = float.MaxValue;

            for (int j = 0; j < civ.Cities.Count; j++)
            {
                if (i == j) continue;
                var city2 = civ.Cities[j];
                // Railways run over land and do not cross the map seam
                if (Math.Abs(city1.X - city2.X) > _map.Width / 2) continue;
                if (CountWaterOnLine(city1.X, city1.Y, city2.X, city2.Y) > 1) continue;
                float dist = MathF.Sqrt((city1.X - city2.X) * (city1.X - city2.X) +
                                       (city1.Y - city2.Y) * (city1.Y - city2.Y));
                if (dist < minDist && dist < 30)
                {
                    minDist = dist;
                    nearestCity = city2;
                }
            }

            if (nearestCity != null &&
                !civ.Railroads.Any(r => (r.x1 == city1.X && r.y1 == city1.Y && r.x2 == nearestCity.X && r.y2 == nearestCity.Y) ||
                                       (r.x2 == city1.X && r.y2 == city1.Y && r.x1 == nearestCity.X && r.y1 == nearestCity.Y)))
            {
                civ.Railroads.Add((city1.X, city1.Y, nearestCity.X, nearestCity.Y));
            }
        }
    }

    /// <summary>
    /// Calculate resource proximity score (0-1)
    /// </summary>
    private float CalculateResourceScore(int x, int y)
    {
        float score = 0f;
        int resourcesFound = 0;

        // Scan 10 cell radius for resources
        for (int dx = -10; dx <= 10; dx++)
        {
            for (int dy = -10; dy <= 10; dy++)
            {
                int nx = (x + dx + _map.Width) % _map.Width;
                int ny = Math.Clamp(y + dy, 0, _map.Height - 1);

                float distance = MathF.Sqrt(dx * dx + dy * dy);
                if (distance > 10) continue;

                var cell = _map.Cells[nx, ny];
                var resources = cell.GetResources();

                if (resources.Count > 0)
                {
                    // Closer resources are more valuable
                    float distanceFactor = 1.0f - (distance / 10f);
                    score += distanceFactor * resources.Count * 0.2f;
                    resourcesFound += resources.Count;
                }

                // Forests for wood (basic resource)
                var biome = cell.GetBiomeData().CurrentBiome;
                if (biome == Biome.TemperateForest || biome == Biome.TropicalRainforest)
                {
                    score += 0.05f * (1.0f - distance / 10f);
                }
            }
        }

        return MathF.Min(1.0f, score);
    }

    /// <summary>
    /// Calculate defensive advantage score (0-1)
    /// </summary>
    private float CalculateDefenseScore(int x, int y)
    {
        float score = 0f;
        var cell = _map.Cells[x, y];

        // High ground is defensible
        if (cell.Elevation > 0.3f && cell.Elevation < 0.7f) // Not too high (mountains)
        {
            score += 0.4f;
        }

        // Near mountains for protection
        bool nearMountains = false;
        int waterNeighbors = 0;

        for (int dx = -3; dx <= 3; dx++)
        {
            for (int dy = -3; dy <= 3; dy++)
            {
                if (dx == 0 && dy == 0) continue;

                int nx = (x + dx + _map.Width) % _map.Width;
                int ny = Math.Clamp(y + dy, 0, _map.Height - 1);

                var neighbor = _map.Cells[nx, ny];
                if (neighbor.Elevation > 0.7f) // Mountain
                {
                    nearMountains = true;
                }

                // Count water neighbors (for defensive moat)
                if (dx >= -1 && dx <= 1 && dy >= -1 && dy <= 1 && neighbor.IsWater)
                {
                    waterNeighbors++;
                }
            }
        }

        if (nearMountains) score += 0.3f;

        // Peninsula/island locations are defensible (some water, but not surrounded)
        if (waterNeighbors > 0 && waterNeighbors < 8)
        {
            score += 0.3f;
        }

        return MathF.Min(1.0f, score);
    }

    /// <summary>
    /// Calculate commerce advantage score (0-1)
    /// </summary>
    private float CalculateCommerceScore(int x, int y)
    {
        float score = 0f;
        var cell = _map.Cells[x, y];
        var geo = cell.GetGeology();

        // Coastal cities are excellent for trade
        bool hasWaterNeighbor = false;
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue;

                int nx = (x + dx + _map.Width) % _map.Width;
                int ny = Math.Clamp(y + dy, 0, _map.Height - 1);

                var neighbor = _map.Cells[nx, ny];
                if (neighbor.IsWater)
                {
                    hasWaterNeighbor = true;
                    break;
                }
            }
            if (hasWaterNeighbor) break;
        }

        if (hasWaterNeighbor)
        {
            score += 0.5f; // Major bonus for coastal
        }

        // Near rivers for trade and water
        if (geo.RiverId > 0 || geo.WaterFlow > 0.5f)
        {
            score += 0.4f;
        }

        // Good climate for agriculture (attracts trade)
        if (cell.Temperature > 10 && cell.Temperature < 30 && cell.Rainfall > 0.4f)
        {
            score += 0.2f;
        }

        return MathF.Min(1.0f, score);
    }

    private City CreateCity(Civilization civ, int x, int y)
    {
        var cell = _map.Cells[x, y];
        var geo = cell.GetGeology();

        // Calculate strategic scores for this location
        float resourceScore = CalculateResourceScore(x, y);
        float defenseScore = CalculateDefenseScore(x, y);
        float commerceScore = CalculateCommerceScore(x, y);

        // Check location features
        bool coastal = false;
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                int nx = (x + dx + _map.Width) % _map.Width;
                int ny = Math.Clamp(y + dy, 0, _map.Height - 1);
                if (_map.Cells[nx, ny].IsWater)
                {
                    coastal = true;
                    break;
                }
            }
            if (coastal) break;
        }

        bool nearRiver = geo.RiverId > 0 || geo.WaterFlow > 0.5f;
        bool onHighGround = cell.Elevation > 0.3f && cell.Elevation < 0.7f;

        // Collect nearby resources (within 5 cells)
        var nearbyResources = new List<ResourceType>();
        for (int dx = -5; dx <= 5; dx++)
        {
            for (int dy = -5; dy <= 5; dy++)
            {
                int nx = (x + dx + _map.Width) % _map.Width;
                int ny = Math.Clamp(y + dy, 0, _map.Height - 1);

                var neighborCell = _map.Cells[nx, ny];
                var resources = neighborCell.GetResources();
                foreach (var res in resources)
                {
                    if (!nearbyResources.Contains(res.Type))
                    {
                        nearbyResources.Add(res.Type);
                    }
                }
            }
        }

        var city = new City
        {
            Id = _nextCityId++,
            Name = GenerateSettlementName(civ),
            X = x,
            Y = y,
            Population = 1000 + _random.Next(5000),
            CivilizationId = civ.Id,
            Founded = 0, // Set by caller
            // Strategic placement data
            ResourceScore = resourceScore,
            DefenseScore = defenseScore,
            CommerceScore = commerceScore,
            NearRiver = nearRiver,
            Coastal = coastal,
            OnHighGround = onHighGround,
            NearbyResources = nearbyResources,
            OriginalCivilizationId = civ.Id
        };

        civ.Cities.Add(city);
        return city;
    }

    public void LoadCivilizations(List<CivilizationData> civData, List<OrbitalObjectData>? orbitalData = null, float nuclearWinter = 0f)
    {
        lock (_civLock)
        {
            _orbitalObjects.Clear();
            foreach (var o in orbitalData ?? new List<OrbitalObjectData>())
            {
                _orbitalObjects.Add(new OrbitalObject
                {
                    Id = _nextOrbitalId++,
                    CivilizationId = o.CivilizationId,
                    Name = o.Name,
                    Type = o.Type,
                    Crew = o.Crew,
                    OrbitAngle = o.OrbitAngle,
                    OrbitRadius = o.OrbitRadius,
                    LaunchedYear = o.LaunchedYear,
                    Orphaned = o.Orphaned,
                    TechLevel = o.TechLevel,
                    Culture = o.Culture
                });
            }
            NuclearWinter = nuclearWinter;

            _civilizations.Clear();
            _armies.Clear();
            _recentBattles.Clear();
            _chronicle.Clear();

            foreach (var data in civData)
            {
                var civ = new Civilization
                {
                    Id = data.Id,
                    Name = data.Name,
                    CenterX = data.CenterX,
                    CenterY = data.CenterY,
                    Population = data.Population,
                    TechLevel = data.TechLevel,
                    CivType = data.CivilizationType,
                    Aggression = data.Aggression,
                    EcoFriendliness = data.EcoFriendliness,
                    Prosperity = data.Prosperity,
                    Stability = data.Stability,
                    CollapseRisk = data.CollapseRisk,
                    Culture = data.Culture,
                    NameRoot = data.NameRoot,
                    TribalName = data.TribalName,
                    Food = data.Food,
                    Wood = data.Wood,
                    Stone = data.Stone,
                    Metal = data.Metal,
                    Gold = data.Gold,
                    WarWeariness = data.WarWeariness,
                    HasLandTransport = data.HasLandTransport,
                    HasSeaTransport = data.HasSeaTransport,
                    HasRailTransport = data.HasRailTransport,
                    HasAirTransport = data.HasAirTransport,
                    HasNuclearWeapons = data.HasNuclearWeapons,
                    NuclearStockpile = data.NuclearStockpile,
                    Ethnicity = data.Ethnicity,
                    Homeland = data.Homeland,
                    DevelopmentModifier = data.DevelopmentModifier,
                    SpaceStage = data.SpaceStage,
                    Satellites = data.Satellites
                };
                civ.CompletedProjects.AddRange(data.CompletedProjects.Select(name => new NationalProject { Name = name, CompletedYear = 0 }));
                civ.Arsenal.ChemicalStockpile = data.ChemicalStockpile;
                civ.Arsenal.BioweaponProgram = data.BioweaponProgram;
                civ.Arsenal.MissileDefense = data.MissileDefense;
                civ.Arsenal.NuclearWarheads = data.NuclearStockpile;
                civ.Territory.UnionWith(data.Territory);

                civ.Government = new Government(data.GovernmentType, 0);
                var ruler = _divinePowers.GenerateRandomRuler(civ, 0);
                ruler.Id = _nextRulerId++;
                civ.Government.CurrentRuler = ruler;
                civ.AllRulers.Add(ruler);

                foreach (var cityData in data.Cities)
                {
                    civ.Cities.Add(new City
                    {
                        Id = cityData.Id,
                        Name = cityData.Name,
                        X = cityData.X,
                        Y = cityData.Y,
                        Population = cityData.Population,
                        Type = GetCityType(cityData.Population),
                        CivilizationId = civ.Id,
                        Founded = cityData.Founded,
                        IsCapital = cityData.IsCapital,
                        Buildings = cityData.Buildings,
                        Coastal = cityData.Coastal,
                        NearRiver = cityData.NearRiver,
                        OnHighGround = cityData.OnHighGround,
                        Happiness = cityData.Happiness,
                        OriginalCivilizationId = cityData.OriginalCivilizationId
                    });
                }

                _civilizations.Add(civ);
            }

            _nextCivId = _civilizations.Any() ? _civilizations.Max(c => c.Id) + 1 : 1;
            var allCities = _civilizations.SelectMany(c => c.Cities).ToList();
            _nextCityId = allCities.Any() ? allCities.Max(c => c.Id) + 1 : 1;
            RebuildOwnerMap();

            // Restore shared diplomatic relations (one object per pair)
            for (int i = 0; i < _civilizations.Count; i++)
            {
                for (int j = i + 1; j < _civilizations.Count; j++)
                {
                    var a = _civilizations[i];
                    var b = _civilizations[j];
                    var relation = new DiplomaticRelation(a.Id, b.Id, 0);
                    var saved = civData.First(d => d.Id == a.Id).Relations.FirstOrDefault(r => r.OtherCivilizationId == b.Id);
                    if (saved != null)
                    {
                        relation.Status = saved.Status;
                        relation.Opinion = saved.Opinion;
                        relation.TrustLevel = saved.TrustLevel;
                        relation.YearsAtWar = saved.YearsAtWar;
                        relation.YearsAtPeace = saved.YearsAtPeace;
                    }
                    a.DiplomaticRelations[b.Id] = relation;
                    b.DiplomaticRelations[a.Id] = relation;
                }
            }

            // Saves from before settlements existed: give every people a capital
            foreach (var civ in _civilizations)
            {
                if (civ.Cities.Count == 0 && _map.Cells[civ.CenterX, civ.CenterY].IsLand)
                {
                    var capital = FoundSettlement(civ, civ.CenterX, civ.CenterY, Math.Max(200, civ.Population), 0);
                    capital.IsCapital = true;
                }
                if (string.IsNullOrEmpty(civ.Ethnicity)) AssignHomeland(civ); // Saves from older versions
                civ.AtWar = civ.DiplomaticRelations.Values.Any(r => r.Status == DiplomaticStatus.War);
                _lastKnownAtWar[civ.Id] = civ.AtWar;
                RecalculatePopulation(civ);
            }
        }
    }

    /// <summary>
    /// Update governments, rulers, and succession
    /// </summary>
    private void UpdateGovernments(int currentYear)
    {
        foreach (var civ in _civilizations)
        {
            if (civ.Government == null) continue;

            // Age rulers and check for death
            if (civ.Government.CurrentRuler != null && civ.Government.CurrentRuler.IsAlive)
            {
                if (civ.Government.CurrentRuler.AgeAndCheckDeath(currentYear, _random))
                {
                    // Ruler died - handle succession
                    HandleSuccession(civ, currentYear);
                }
            }

            // Update government stability based on various factors
            UpdateGovernmentStability(civ);

            // Check for government evolution based on tech level
            CheckGovernmentEvolution(civ, currentYear);

            // Check for revolution/collapse
            if (civ.Government.ShouldCollapse(_random))
            {
                HandleRevolution(civ, currentYear);
            }

            var previousName = civ.Name;
            UpdatePolityName(civ);
            if (previousName != civ.Name && civ.Government.Type != GovernmentType.Tribal)
            {
                AddChronicle(currentYear, HistoryCategory.Growth,
                    $"The {previousName} proclaim themselves the {civ.Name}", civ.CenterX, civ.CenterY, civ.Id);
            }
        }
    }

    /// <summary>
    /// Handle succession when a ruler dies
    /// </summary>
    private void HandleSuccession(Civilization civ, int currentYear)
    {
        var deadRuler = civ.Government!.CurrentRuler!;

        if (civ.Government.IsHereditary)
        {
            // Hereditary succession along the line of succession
            var heir = ResolveSuccession(civ, deadRuler, currentYear);

            if (heir != null)
            {
                // Smooth succession
                civ.Government.CurrentRuler = heir;
                civ.Government.Stability = Math.Min(civ.Government.Stability + 0.1f, 1.0f);
            }
            else
            {
                // No heir - succession crisis
                HandleSuccessionCrisis(civ, deadRuler, currentYear);
                civ.Government.Stability -= 0.3f;
                var newRuler = _divinePowers.GenerateRandomRuler(civ, currentYear);
                newRuler.Id = _nextRulerId++;
                civ.Government.CurrentRuler = newRuler;
                civ.AllRulers.Add(newRuler);

                // New dynasty
                if (civ.Government.Type == GovernmentType.Monarchy || civ.Government.Type == GovernmentType.Dynasty)
                {
                    var oldDynasty = civ.Dynasties.FirstOrDefault(d => d.Id == deadRuler.DynastyId);
                    if (oldDynasty != null)
                    {
                        oldDynasty.IsExtinct = true;
                    }

                    var newDynasty = new Dynasty(
                        civ.Dynasties.Count + 1,
                        DivinePowers.GenerateDynastyName(_random),
                        currentYear,
                        newRuler.Id,
                        civ.Id
                    );
                    civ.Dynasties.Add(newDynasty);
                    newRuler.DynastyId = newDynasty.Id;
                }
            }
        }
        else if (civ.Government.IsElected)
        {
            // Election
            var newRuler = _divinePowers.GenerateRandomRuler(civ, currentYear);
            newRuler.Id = _nextRulerId++;
            civ.Government.CurrentRuler = newRuler;
            civ.AllRulers.Add(newRuler);
        }
        else
        {
            // Power struggle
            civ.Government.Stability -= 0.2f;
            var newRuler = _divinePowers.GenerateRandomRuler(civ, currentYear);
            newRuler.Id = _nextRulerId++;
            civ.Government.CurrentRuler = newRuler;
            civ.AllRulers.Add(newRuler);
        }
    }

    /// <summary>
    /// Update government stability based on various factors
    /// </summary>
    private void UpdateGovernmentStability(Civilization civ)
    {
        if (civ.Government == null) return;

        // Ruler charisma affects stability
        if (civ.Government.CurrentRuler != null)
        {
            civ.Government.Stability += (civ.Government.CurrentRuler.Charisma - 0.5f) * 0.01f;
        }

        // Famine reduces stability
        if (civ.Cities.Any(c => c.Starving))
        {
            civ.Government.Stability -= 0.02f;
        }

        // War reduces stability
        if (civ.AtWar)
        {
            civ.Government.Stability -= 0.01f;
        }

        // Peace increases stability
        if (!civ.AtWar)
        {
            civ.Government.Stability += 0.005f;
        }

        // Clamp
        civ.Government.Stability = Math.Clamp(civ.Government.Stability, 0.0f, 1.0f);
    }

    /// <summary>
    /// Check if government should evolve to new type based on tech level
    /// </summary>
    private void CheckGovernmentEvolution(Civilization civ, int currentYear)
    {
        if (civ.Government == null) return;

        // Tribal -> Monarchy at tech 10
        if (civ.Government.Type == GovernmentType.Tribal && civ.TechLevel >= 10 && _random.NextDouble() < 0.05)
        {
            civ.Government = new Government(GovernmentType.Monarchy, currentYear);
            var ruler = _divinePowers.GenerateRandomRuler(civ, currentYear);
            ruler.Id = _nextRulerId++;
            civ.Government.CurrentRuler = ruler;
            civ.AllRulers.Add(ruler);

            var dynasty = new Dynasty(1, DivinePowers.GenerateDynastyName(_random), currentYear, ruler.Id, civ.Id);
            civ.Dynasties.Add(dynasty);
            ruler.DynastyId = dynasty.Id;
        }
        // Monarchy -> Republic at tech 40 (if eco-friendly)
        else if (civ.Government.Type == GovernmentType.Monarchy && civ.TechLevel >= 40 &&
                 civ.EcoFriendliness > 0.6f && _random.NextDouble() < 0.03)
        {
            civ.Government = new Government(GovernmentType.Republic, currentYear);
        }
        // Republic -> Democracy at tech 60
        else if (civ.Government.Type == GovernmentType.Republic && civ.TechLevel >= 60 && _random.NextDouble() < 0.03)
        {
            civ.Government = new Government(GovernmentType.Democracy, currentYear);
        }
        // Aggressive civs can become dictatorships
        else if (civ.Aggression > 0.8f && civ.TechLevel >= 30 && _random.NextDouble() < 0.02)
        {
            civ.Government = new Government(GovernmentType.Dictatorship, currentYear);
            var ruler = _divinePowers.GenerateRandomRuler(civ, currentYear);
            ruler.Id = _nextRulerId++;
            ruler.Brutality = 0.9f;
            civ.Government.CurrentRuler = ruler;
            civ.AllRulers.Add(ruler);
        }
    }

    /// <summary>
    /// Handle revolution/government collapse
    /// </summary>
    private void HandleRevolution(Civilization civ, int currentYear)
    {
        // Population losses from civil strife
        civ.Population = (int)(civ.Population * 0.95f);
        var previousGovernment = civ.Government?.Type;

        // Determine new government type
        GovernmentType newType;
        if (civ.TechLevel < 10)
            newType = GovernmentType.Tribal;
        else if (civ.TechLevel < 30)
            newType = _random.NextDouble() < 0.5 ? GovernmentType.Monarchy : GovernmentType.Theocracy;
        else if (civ.TechLevel < 50)
            newType = _random.NextDouble() < 0.5 ? GovernmentType.Republic : GovernmentType.Dictatorship;
        else
            newType = GovernmentType.Democracy;

        civ.Government = new Government(newType, currentYear);
        AddChronicle(currentYear, HistoryCategory.Rebellion,
            previousGovernment == newType
                ? $"Revolution in the {civ.Name}: a new {newType.ToString().ToLower()} regime seizes power"
                : $"Revolution in the {civ.Name}: {previousGovernment?.ToString().ToLower()} overthrown, {newType.ToString().ToLower()} established",
            civ.CenterX, civ.CenterY, civ.Id);

        // New ruler
        var ruler = _divinePowers.GenerateRandomRuler(civ, currentYear);
        ruler.Id = _nextRulerId++;
        civ.Government.CurrentRuler = ruler;
        civ.AllRulers.Add(ruler);

        if (civ.Government.IsHereditary)
        {
            var dynasty = new Dynasty(
                civ.Dynasties.Count + 1,
                DivinePowers.GenerateDynastyName(_random),
                currentYear,
                ruler.Id,
                civ.Id
            );
            civ.Dynasties.Add(dynasty);
            ruler.DynastyId = dynasty.Id;
        }
    }

    /// <summary>
    /// Update diplomatic relations over time
    /// </summary>
    private void UpdateDiplomacy(int currentYear)
    {
        // Each relation object is shared by both civilizations, so visit it once
        var visited = new HashSet<DiplomaticRelation>();

        foreach (var civ in _civilizations)
        {
            foreach (var relation in civ.DiplomaticRelations.Values)
            {
                if (!visited.Add(relation)) continue;

                var civ1 = GetCivilizationById(relation.CivilizationId1);
                var civ2 = GetCivilizationById(relation.CivilizationId2);
                if (civ1 == null || civ2 == null) continue;

                // Update treaty expirations
                foreach (var treaty in relation.Treaties.Where(t => t.IsActive).ToList())
                {
                    if (treaty.HasExpired(currentYear))
                    {
                        treaty.IsActive = false;
                    }
                }

                if (relation.Status == DiplomaticStatus.War)
                {
                    relation.YearsAtWar++;
                    continue;
                }

                relation.YearsAtPeace++;

                // Opinion slowly drifts back toward neutral; grudges fade, friendships need upkeep
                relation.Opinion *= 0.98f;

                int border = GetBorderLength(civ1.Id, civ2.Id);
                if (border > 0)
                {
                    // Shared borders create friction, especially between aggressive peoples
                    relation.Opinion -= 0.2f + Math.Min(border, 40) * 0.02f * (civ1.Aggression + civ2.Aggression);
                }

                if (relation.HasTreaty(TreatyType.TradePact)) relation.Opinion += 1.5f;
                if (relation.HasTreaty(TreatyType.RoyalMarriage)) relation.Opinion += 1.0f;
                if (civ1.Government?.Type == civ2.Government?.Type) relation.Opinion += 0.5f;
                if (civ1.EcoFriendliness > 0.6f && civ2.EcoFriendliness > 0.6f) relation.Opinion += 0.5f;

                relation.Opinion = Math.Clamp(relation.Opinion, -100f, 100f);

                // Trust increases during peace
                if (relation.Opinion > 20)
                {
                    relation.TrustLevel = Math.Min(relation.TrustLevel + 0.01f, 1.0f);
                }

                // Diplomatic status follows opinion
                relation.Status = relation.Opinion switch
                {
                    > 50 when relation.HasTreaty(TreatyType.MilitaryAlliance) || relation.HasTreaty(TreatyType.DefensivePact) || relation.HasTreaty(TreatyType.RoyalMarriage) => DiplomaticStatus.Allied,
                    > 20 => DiplomaticStatus.Friendly,
                    < -30 => DiplomaticStatus.Hostile,
                    _ => DiplomaticStatus.Neutral
                };

                // Trade pacts between friendly neighbours
                if (relation.Status >= DiplomaticStatus.Friendly && !relation.HasTreaty(TreatyType.TradePact) &&
                    _random.NextDouble() < 0.05)
                {
                    bool renewal = relation.Treaties.Any(t => t.Type == TreatyType.TradePact);
                    relation.AddTreaty(new Treaty(TreatyType.TradePact, currentYear, 50));
                    if (!renewal)
                    {
                        AddChronicle(currentYear, HistoryCategory.Diplomacy,
                            $"The {civ1.Name} and the {civ2.Name} open a trade pact", civ1.CenterX, civ1.CenterY, civ1.Id);
                    }
                }

                // Defensive pacts between long-time friends who fear a common neighbour
                if (relation.Opinion > 45 && !relation.HasTreaty(TreatyType.DefensivePact) && _random.NextDouble() < 0.03)
                {
                    bool renewal = relation.Treaties.Any(t => t.Type == TreatyType.DefensivePact);
                    relation.AddTreaty(new Treaty(TreatyType.DefensivePact, currentYear, 80));
                    if (!renewal)
                    {
                        AddChronicle(currentYear, HistoryCategory.Diplomacy,
                            $"The {civ1.Name} and the {civ2.Name} form a defensive alliance", civ1.CenterX, civ1.CenterY, civ1.Id);
                    }
                }

                // Check for royal marriages (hereditary governments only)
                if (relation.Status >= DiplomaticStatus.Friendly &&
                    civ1.Government?.IsHereditary == true &&
                    civ2.Government?.IsHereditary == true &&
                    !relation.HasTreaty(TreatyType.RoyalMarriage) &&
                    _random.NextDouble() < 0.02)
                {
                    ProposeRoyalMarriage(civ1, civ2, relation, currentYear);
                    AddChronicle(currentYear, HistoryCategory.Diplomacy,
                        $"A royal marriage unites the houses of the {civ1.Name} and the {civ2.Name}", civ1.CenterX, civ1.CenterY, civ1.Id);
                }
            }
        }
    }

    /// <summary>
    /// Propose and create a royal marriage
    /// </summary>
    private void ProposeRoyalMarriage(Civilization civ1, Civilization civ2, DiplomaticRelation relation, int currentYear)
    {
        if (civ1.Government?.CurrentRuler == null || civ2.Government?.CurrentRuler == null)
            return;

        var ruler1 = civ1.Government.CurrentRuler;
        var ruler2 = civ2.Government.CurrentRuler;

        // Create marriage
        var marriage = new RoyalMarriage(ruler1.Id, ruler2.Id, civ1.Id, civ2.Id, currentYear);
        civ1.RoyalMarriages.Add(marriage);
        civ2.RoyalMarriages.Add(marriage);

        // Add marriage treaty
        var treaty = new Treaty(TreatyType.RoyalMarriage, currentYear);
        relation.AddTreaty(treaty);

        // Major opinion boost
        relation.Opinion += 30;
        relation.Status = DiplomaticStatus.Allied;

        // Potential for heir with mixed bloodline
        if (_random.NextDouble() < 0.3)
        {
            var heir = _divinePowers.GenerateRandomRuler(civ1, currentYear);
            heir.Id = _nextRulerId++;
            heir.Age = 0;
            heir.ParentId = ruler1.Id;
            heir.DynastyId = ruler1.DynastyId;
            civ1.AllRulers.Add(heir);
            ruler1.ChildrenIds.Add(heir.Id);
            marriage.ChildrenIds.Add(heir.Id);
        }
    }

    /// <summary>
    /// Update civilization response to disasters
    /// </summary>
    private void UpdateDisasterResponse(int currentYear)
    {
        foreach (var civ in _civilizations)
        {
            // Check territory for disasters
            int disastersInTerritory = 0;
            int totalDamage = 0;
            int cycloneHits = 0;

            foreach (var (x, y) in civ.Territory)
            {
                var cell = _map.Cells[x, y];

                // Check for extreme temperature
                if (cell.Temperature < -20 || cell.Temperature > 50)
                {
                    disastersInTerritory++;
                    totalDamage += 100;
                }

                // Chronic pollution is handled by the harsh-climate collapse pressure

                // Droughts are felt through failed harvests in the settlement economy

                var geo = cell.GetGeology();

                // Earthquakes are handled per event (magnitude and distance) in ApplyEarthquakeDamage

                // Check for Tsunamis
                if (geo.TsunamiWaveHeight > 1.0f)
                {
                    disastersInTerritory++;
                    // Damage based on wave height
                    // 1m = 10 damage, 5m = 50 damage, 10m = 100 damage
                    totalDamage += (int)(geo.TsunamiWaveHeight * 10);
                }
            }

            // Check for cyclones/hurricanes hitting civilization
            if (_weatherSystem != null)
            {
                var storms = _weatherSystem.GetActiveStorms();
                foreach (var storm in storms)
                {
                    // Only tropical cyclones
                    if (storm.Type < StormType.TropicalDepression || storm.Type > StormType.HurricaneCategory5)
                        continue;

                    // Check if storm is hitting civilization territory
                    int stormRadius = storm.Type switch
                    {
                        StormType.TropicalDepression => 3,
                        StormType.TropicalStorm => 5,
                        StormType.HurricaneCategory1 => 8,
                        StormType.HurricaneCategory2 => 10,
                        StormType.HurricaneCategory3 => 12,
                        StormType.HurricaneCategory4 => 15,
                        StormType.HurricaneCategory5 => 20,
                        _ => 3
                    };

                    bool hit = false;
                    int affectedCells = 0;

                    foreach (var (x, y) in civ.Territory)
                    {
                        // Calculate distance from storm center
                        int dx = Math.Abs(x - storm.CenterX);
                        if (dx > _map.Width / 2) dx = _map.Width - dx; // Wrap around

                        int dy = Math.Abs(y - storm.CenterY);
                        float distance = MathF.Sqrt(dx * dx + dy * dy);

                        if (distance < stormRadius)
                        {
                            hit = true;
                            affectedCells++;
                        }
                    }

                    if (hit)
                    {
                        cycloneHits++;
                        disastersInTerritory++;

                        // Damage based on storm category
                        int cycloneDamage = storm.Type switch
                        {
                            StormType.TropicalDepression => 20,
                            StormType.TropicalStorm => 50,
                            StormType.HurricaneCategory1 => 100,
                            StormType.HurricaneCategory2 => 200,
                            StormType.HurricaneCategory3 => 400,
                            StormType.HurricaneCategory4 => 800,
                            StormType.HurricaneCategory5 => 1500,
                            _ => 20
                        };

                        // Scale damage by affected area
                        float areaCovered = affectedCells / (float)civ.Territory.Count;
                        totalDamage += (int)(cycloneDamage * areaCovered);
                    }
                }
            }

            if (disastersInTerritory > 0)
            {
                // Casualties scale with the share of the land that was hit, not the raw cell count
                float affectedShare = Math.Min(1f, disastersInTerritory / (float)Math.Max(1, civ.Territory.Count + cycloneHits));
                float baseCasualtyRate = 0.1f * affectedShare * (1.0f - civ.DisasterPreparedness);

                // Cyclones are more deadly
                if (cycloneHits > 0)
                {
                    baseCasualtyRate += 0.02f * cycloneHits * (1.0f - civ.DisasterPreparedness);
                }

                baseCasualtyRate = Math.Min(baseCasualtyRate, 0.25f);
                int casualties = (int)(civ.Population * baseCasualtyRate);
                if (casualties > 500 && casualties > civ.Population * 0.05f && currentYear - civ.LastDisasterReportYear >= 5)
                {
                    civ.LastDisasterReportYear = currentYear;
                    AddChronicle(currentYear, HistoryCategory.Disaster,
                        $"Disaster strikes the {civ.Name}: {casualties:N0} dead", civ.CenterX, civ.CenterY, civ.Id);
                }

                civ.Population -= casualties;
                civ.PopulationLostToDisasters += casualties;
                civ.DisastersSurvived++;

                // Improve preparedness over time
                civ.DisasterPreparedness = Math.Min(civ.DisasterPreparedness + 0.05f, 0.9f);

                // Disasters reduce stability
                if (civ.Government != null)
                {
                    float stabilityLoss = 0.2f * affectedShare;
                    // Cyclones cause more political instability
                    if (cycloneHits > 0)
                    {
                        stabilityLoss += 0.1f * cycloneHits;
                    }
                    civ.Government.Stability -= stabilityLoss;
                }

                // Resource losses
                float resourceLoss = 0.3f * affectedShare;
                // Cyclones destroy more infrastructure and resources
                if (cycloneHits > 0)
                {
                    resourceLoss += 0.2f * cycloneHits;
                }

                civ.Food *= (1.0f - Math.Min(resourceLoss, 0.9f));
                civ.Wood *= (1.0f - Math.Min(resourceLoss * 0.5f, 0.8f));
                civ.Stone *= (1.0f - Math.Min(resourceLoss * 0.3f, 0.5f));

                // Advanced civilizations can evacuate/adapt better
                if (civ.TechLevel >= 50)
                {
                    // Restore some population through disaster relief
                    civ.Population += casualties / 3;
                }
                // Modern weather forecasting helps
                else if (civ.TechLevel >= 30 && cycloneHits > 0)
                {
                    // Can predict and prepare for cyclones
                    civ.Population += casualties / 5;
                }
            }
        }
    }

    /// <summary>
    /// Build nuclear power plants for energy production
    /// </summary>
    private void BuildNuclearPlants(Civilization civ, int currentYear)
    {
        if (civ.Cities.Count == 0) return;

        // Build 1-3 nuclear plants based on uranium availability
        int plantsToBuil = Math.Min(3, (int)(civ.ResourceStockpile.GetValueOrDefault(ResourceType.Uranium, 0) / 0.5f));
        int plantsBuilt = 0;

        foreach (var city in civ.Cities.OrderByDescending(c => c.Population))
        {
            if (plantsBuilt >= plantsToBuil) break;

            // Find suitable location near city (flat land, near water if possible)
            for (int radius = 2; radius <= 10 && plantsBuilt < plantsToBuil; radius++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    for (int dy = -radius; dy <= radius; dy++)
                    {
                        if (plantsBuilt >= plantsToBuil) break;

                        int nx = (city.X + dx + _map.Width) % _map.Width;
                        int ny = Math.Clamp(city.Y + dy, 0, _map.Height - 1);

                        var cell = _map.Cells[nx, ny];
                        var geo = cell.GetGeology();

                        // Must be in territory, on land, not mountain, not already has plant
                        if (civ.Territory.Contains((nx, ny)) &&
                            cell.IsLand &&
                            cell.Elevation < 0.5f &&
                            !geo.HasNuclearPlant)
                        {
                            geo.HasNuclearPlant = true;
                            geo.EnergyInfraBuiltYear = currentYear;
                            geo.MeltdownRisk = 0.01f; // Initial 1% risk
                            plantsBuilt++;
                            break;
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Build wind turbines for green energy
    /// </summary>
    private void BuildWindTurbines(Civilization civ, int currentYear)
    {
        if (civ.Cities.Count == 0) return;

        // Build turbines on windy high ground
        int turbinesBuilt = 0;
        int targetTurbines = civ.Cities.Count * 5; // 5 turbines per city

        foreach (var (x, y) in civ.Territory.OrderBy(_ => _random.Next()))
        {
            if (turbinesBuilt >= targetTurbines) break;

            var cell = _map.Cells[x, y];
            var geo = cell.GetGeology();

            // Prefer high elevation (windy) locations
            if (cell.IsLand &&
                cell.Elevation > 0.3f &&
                cell.Elevation < 0.7f && // Not too high (mountains)
                !geo.HasWindTurbine &&
                !geo.HasNuclearPlant &&
                !geo.HasSolarFarm)
            {
                geo.HasWindTurbine = true;
                geo.EnergyInfraBuiltYear = currentYear;
                turbinesBuilt++;
            }
        }
    }

    /// <summary>
    /// Build solar farms for green energy
    /// </summary>
    private void BuildSolarFarms(Civilization civ, int currentYear)
    {
        if (civ.Cities.Count == 0) return;

        // Build solar farms in sunny flat areas (deserts ideal)
        int farmsBuilt = 0;
        int targetFarms = civ.Cities.Count * 3; // 3 farms per city

        foreach (var (x, y) in civ.Territory.OrderBy(_ => _random.Next()))
        {
            if (farmsBuilt >= targetFarms) break;

            var cell = _map.Cells[x, y];
            var geo = cell.GetGeology();

            // Prefer flat, sunny locations (deserts are ideal)
            if (cell.IsLand &&
                cell.Elevation < 0.3f && // Flat land
                !geo.HasWindTurbine &&
                !geo.HasNuclearPlant &&
                !geo.HasSolarFarm)
            {
                geo.HasSolarFarm = true;
                geo.EnergyInfraBuiltYear = currentYear;
                farmsBuilt++;
            }
        }
    }

    /// <summary>
    /// Update nuclear plant meltdown risk based on various factors
    /// </summary>
    private void UpdateNuclearPlantRisk(Civilization civ, float deltaTime, int currentYear)
    {
        foreach (var (x, y) in civ.Territory)
        {
            var cell = _map.Cells[x, y];
            var geo = cell.GetGeology();

            if (geo.HasNuclearPlant)
            {
                // Base risk increases over time (aging)
                int plantAge = currentYear - geo.EnergyInfraBuiltYear;
                geo.MeltdownRisk = 0.01f + (plantAge / 1000f) * 0.05f; // +5% per 1000 years

                // Earthquake zones increase risk
                if (geo.TectonicStress > 0.7f)
                {
                    geo.MeltdownRisk += 0.03f;
                }

                // War/low population increases risk (poor maintenance)
                if (civ.Population < 50000 || civ.AtWar)
                {
                    geo.MeltdownRisk += 0.02f;
                }

                // Clamp risk to max 50%
                geo.MeltdownRisk = Math.Min(geo.MeltdownRisk, 0.5f);

                // Check for random meltdown
                if (_random.NextDouble() < geo.MeltdownRisk * 0.0001f * deltaTime)
                {
                    // Trigger meltdown!
                    _disasterManager?.TriggerNuclearAccident(x, y, currentYear);
                    geo.HasNuclearPlant = false; // Plant destroyed
                    geo.MeltdownRisk = 0f;
                }
            }
        }
    }
}

public class Civilization
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int CenterX { get; set; }
    public int CenterY { get; set; }
    public HashSet<(int x, int y)> Territory { get; set; } = new();
    public int Population { get; set; }
    public int TechLevel { get; set; }
    public CivType CivType { get; set; }
    public float Aggression { get; set; } // 0-1
    public float EcoFriendliness { get; set; } // 0-1
    public int Founded { get; set; }

    // Government and Leadership
    public Government? Government { get; set; }
    public List<Ruler> AllRulers { get; set; } = new(); // Historical rulers
    public List<Dynasty> Dynasties { get; set; } = new(); // Royal families
    public List<RoyalMarriage> RoyalMarriages { get; set; } = new(); // Political marriages

    // Diplomacy
    public Dictionary<int, DiplomaticRelation> DiplomaticRelations { get; set; } = new();

    // Transportation
    public bool HasLandTransport { get; set; } = false; // Horses, cars
    public bool HasRailTransport { get; set; } = false; // Trains, railroads
    public bool HasSeaTransport { get; set; } = false; // Ships
    public bool HasAirTransport { get; set; } = false; // Planes
    public List<(int x, int y)> TradeRoutes { get; set; } = new();
    public List<(int x1, int y1, int x2, int y2)> Railroads { get; set; } = new(); // Railroad lines
    public HashSet<(int x, int y)> Roads { get; set; } = new(); // Road cells (local infrastructure)

    // Commerce
    public List<City> Cities { get; set; } = new();
    public float TradeIncome { get; set; } = 0.0f; // Income from trade per year

    // War status
    public bool AtWar { get; set; } = false;
    public int? WarTargetId { get; set; } = null;
    public int MilitaryStrength { get; set; } = 0;

    // Climate agreements
    public bool InClimateAgreement { get; set; } = false;
    public List<int> ClimatePartners { get; set; } = new();
    public float EmissionReduction { get; set; } = 0.0f; // 0-1, how much emissions are reduced

    // Nuclear weapons
    public bool HasNuclearWeapons { get; set; } = false;
    public int NuclearStockpile { get; set; } = 0;
    public List<(int x, int y, int year)> NuclearStrikes { get; set; } = new();

    // Resource extraction
    public Dictionary<ResourceType, float> ResourceStockpile { get; set; } = new();
    public Dictionary<ResourceType, float> AnnualProduction { get; set; } = new();
    public List<(int x, int y, ResourceType type)> ActiveMines { get; set; } = new();
    public float ProductionBonus { get; set; } = 1.0f; // Multiplier from resources

    // Internal health
    public float Prosperity { get; set; } = 0.5f;
    public float Stability { get; set; } = 0.55f;
    public float CollapseRisk { get; set; } = 0.0f;

    // Resources
    public float Food { get; set; } = 100.0f;            // From hunting, farming, fishing
    public float Wood { get; set; } = 50.0f;             // From forests
    public float Stone { get; set; } = 50.0f;            // From quarries
    public float Metal { get; set; } = 0.0f;             // From mines (requires tech)
    public float FoodProduction { get; set; } = 0.0f;    // Per year
    public float WoodProduction { get; set; } = 0.0f;    // Per year
    public float StoneProduction { get; set; } = 0.0f;   // Per year
    public float MetalProduction { get; set; } = 0.0f;   // Per year
    public float FoodConsumption { get; set; } = 0.0f;   // Per year (based on population)

    // Disaster resilience
    // Society
    public int Culture { get; set; } = 0;                 // Naming/culture group
    public string NameRoot { get; set; } = "";            // Stem used to build the polity name
    public string TribalName { get; set; } = "";          // Name used while tribal
    public float Gold { get; set; } = 0.0f;               // Treasury from trade and taxes
    public float GoldIncome { get; set; } = 0.0f;         // Per year
    public float WarWeariness { get; set; } = 0.0f;       // 0-1, desire for peace
    public int WarCasualties { get; set; } = 0;           // Soldiers lost in the current wars
    public int LastKnownPopulation { get; set; } = 0;     // Used to absorb external population changes
    public int SettlementsFounded { get; set; } = 0;
    public int WildlifeHuntedOut { get; set; } = 0;       // Wild regions emptied by hunting
    public int CitiesConquered { get; set; } = 0;
    public int CitiesLost { get; set; } = 0;
    public int LastRebellionYear { get; set; } = int.MinValue / 2;
    public int LastDisasterReportYear { get; set; } = int.MinValue / 2;
    public int LastFamineReportYear { get; set; } = int.MinValue / 2;

    public City? Capital => Cities.FirstOrDefault(c => c.IsCapital) ?? Cities.FirstOrDefault();

    // Strategic AI
    public StrategyState Strategy { get; set; } = new();

    // Homeland and people
    public string Ethnicity { get; set; } = "";
    public HomelandClimate Homeland { get; set; } = HomelandClimate.Temperate;
    public float DevelopmentModifier { get; set; } = 1.0f; // Research/growth multiplier from geography

    // Energy and networks
    public Dictionary<EnergySource, float> EnergyMix { get; set; } = new(); // Share of production, sums to 1
    public float EnergyProduction { get; set; }            // Arbitrary energy units per year
    public float EnergyDemand { get; set; }
    public float Electrification { get; set; }             // Share of settlements on the grid, 0-1
    public float InternetPenetration { get; set; }         // 0-1
    public List<(int x1, int y1, int x2, int y2)> PowerLines { get; set; } = new();
    public List<(int x1, int y1, int x2, int y2)> DataCables { get; set; } = new(); // Backbone and undersea cables

    // Weapons of mass destruction
    public Arsenal Arsenal { get; set; } = new();

    // Space
    public SpaceStage SpaceStage { get; set; } = SpaceStage.None;
    public int Satellites { get; set; }
    public int Astronauts { get; set; }

    // National programmes
    public NationalProject? ActiveProject { get; set; }
    public List<NationalProject> CompletedProjects { get; set; } = new();
    public bool HasProject(string name) => CompletedProjects.Any(p => p.Name == name);

    // Politics
    public List<PoliticalParty> Parties { get; set; } = new();
    public string RulingParty { get; set; } = "";
    public int NextElectionYear { get; set; }
    public List<ElectionResult> Elections { get; set; } = new();
    public Ruler? HeirApparent { get; set; }
    public List<Ruler> SuccessionLine { get; set; } = new();   // Ordered claimants (hereditary governments)

    public float DisasterPreparedness { get; set; } = 0.0f; // 0-1, how prepared for disasters
    public int DisastersSurvived { get; set; } = 0;
    public int PopulationLostToDisasters { get; set; } = 0;
}

/// <summary>
/// Represents a city within a civilization
/// </summary>
public class City
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int X { get; set; }
    public int Y { get; set; }
    public int Population { get; set; }
    public CityType Type { get; set; } = CityType.Village;
    public int CivilizationId { get; set; }
    public int Founded { get; set; }

    // Production specialization
    public float FoodProduction { get; set; }
    public float IndustrialProduction { get; set; }
    public float ScienceProduction { get; set; }
    public float TradeProduction { get; set; }

    // Commerce
    public List<int> TradingWith { get; set; } = new(); // IDs of other cities
    public float TradeVolume { get; set; } = 0.0f;

    // Strategic placement factors (why this location was chosen)
    public float ResourceScore { get; set; } = 0.0f; // Proximity to resources
    public float DefenseScore { get; set; } = 0.0f; // Defensive advantages (high ground, etc.)
    public float CommerceScore { get; set; } = 0.0f; // Near rivers/coast for trade
    public bool NearRiver { get; set; } = false;
    public bool Coastal { get; set; } = false;
    public bool OnHighGround { get; set; } = false;
    public List<ResourceType> NearbyResources { get; set; } = new(); // Resources within 5 cells

    // Settlement life
    public bool IsCapital { get; set; } = false;
    public CityBuilding Buildings { get; set; } = CityBuilding.None;
    public bool Starving { get; set; } = false;
    public float Happiness { get; set; } = 0.6f;          // 0-1
    public int WorkedCells { get; set; } = 0;             // Farmland/hunting grounds worked this year
    public int Capacity { get; set; } = 0;                // Population the local land can feed
    public float GoldProduction { get; set; }

    // Warfare
    public bool UnderSiege { get; set; } = false;
    public float SiegeProgress { get; set; } = 0.0f;      // 0-1, captured at 1
    public int OriginalCivilizationId { get; set; }

    public CityType LargestTypeReached { get; set; } = CityType.Village;

    // Infrastructure
    public bool Electrified { get; set; }                 // Connected to the power grid
    public bool Online { get; set; }                      // Connected to the internet
    public bool HasAirport { get; set; }
    public bool HasSpaceport { get; set; }
    public bool HasPowerPlant { get; set; }
    public EnergySource? PowerPlantType { get; set; }

    public bool Has(CityBuilding building) => (Buildings & building) != 0;
}

[Flags]
public enum CityBuilding
{
    None = 0,
    Granary = 1 << 0,     // Food storage, faster growth, famine buffer
    Walls = 1 << 1,       // Doubles siege defense
    Market = 1 << 2,      // Gold and trade
    Temple = 1 << 3,      // Happiness and stability
    Barracks = 1 << 4,    // Better and larger armies
    Harbor = 1 << 5,      // Fishing and sea trade
    Workshop = 1 << 6,    // Stone and metal output
    University = 1 << 7   // Science
}

public enum CityType
{
    Village,     // < 1000 pop
    Town,        // 1000-5000
    City,        // 5000-50000
    Metropolis   // > 50000
}