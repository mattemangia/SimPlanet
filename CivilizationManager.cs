namespace SimPlanet;

/// <summary>
/// Manages intelligent civilizations and their development
/// </summary>
public class CivilizationManager
{
    private readonly PlanetMap _map;
    private readonly Random _random;
    private List<Civilization> _civilizations;
    private int _nextCivId = 1;
    private DivinePowers _divinePowers;
    private int _nextRulerId = 1;
    private WeatherSystem? _weatherSystem;
    private DisasterManager? _disasterManager;

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

    private static readonly string[] CivNames = new[]
    {
        "Terrans", "Aquans", "Volcanids", "Glacians", "Foresters",
        "Deserters", "Mountaineers", "Islanders", "Nomads", "Builders"
    };

    public CivilizationManager(PlanetMap map, int seed)
    {
        _map = map;
        _random = new Random(seed + 4000);
        _civilizations = new List<Civilization>();
        _divinePowers = new DivinePowers(_random);
    }

    public void Update(float deltaTime, int currentYear)
    {
        // Check for new civilization emergence
        CheckForNewCivilizations(currentYear);

        // Update existing civilizations
        foreach (var civ in _civilizations.ToList())
        {
            UpdateCivilization(civ, deltaTime, currentYear);
        }

        // Update governments and rulers
        UpdateGovernments(currentYear);

        // Handle interactions between civilizations
        HandleCivilizationInteractions(currentYear);

        // Update diplomatic relations
        UpdateDiplomacy(currentYear);

        // Check disaster impacts
        UpdateDisasterResponse(currentYear);
    }

    private void CheckForNewCivilizations(int currentYear)
    {
        // Scan for intelligence-level life that could form civilizations
        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                var cell = _map.Cells[x, y];

                // Intelligence level life can form civilizations
                if (cell.LifeType == LifeForm.Intelligence &&
                    cell.Biomass > 0.6f &&
                    !IsCellInCivilization(x, y) &&
                    _random.NextDouble() < 0.001)
                {
                    CreateCivilization(x, y);
                }
            }
        }
    }

    private void CreateCivilization(int x, int y, int currentYear = 0)
    {
        var cell = _map.Cells[x, y];

        var civ = new Civilization
        {
            Id = _nextCivId++,
            Name = CivNames[_random.Next(CivNames.Length)] + " " + _nextCivId,
            CenterX = x,
            CenterY = y,
            Population = 1000 + _random.Next(5000),
            TechLevel = 0,
            CivType = CivType.Tribal,
            Aggression = (float)_random.NextDouble(),
            EcoFriendliness = (float)_random.NextDouble(),
            Founded = currentYear
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
    }

    private void UpdateCivilization(Civilization civ, float deltaTime, int currentYear)
    {
        // Resource production and consumption
        UpdateResources(civ, deltaTime);

        // Population growth (affected by food availability)
        float foodModifier = civ.Food > 0 ? 1.0f : 0.5f; // Starving civilizations grow slower
        float growthRate = 1.0f + civ.EcoFriendliness * 0.5f;
        civ.Population += (int)(civ.Population * 0.01f * growthRate * deltaTime * foodModifier);

        // Create cities as population grows
        int expectedCities = Math.Max(1, civ.Population / 10000); // 1 city per 10,000 people
        if (civ.Cities.Count < expectedCities && civ.TechLevel >= 3)
        {
            // Find strategically optimal location for a new city
            if (civ.Territory.Count > 0)
            {
                var (x, y, score) = FindBestCityLocation(civ);
                CreateCity(civ, x, y);
            }
        }

        // Technology advancement
        if (_random.NextDouble() < 0.01 * deltaTime)
        {
            civ.TechLevel++;

            // Advance civilization type
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
            if (civ.TechLevel >= 70 && !civ.HasNuclearWeapons)
            {
                civ.HasNuclearWeapons = true; // Nuclear weapons
                civ.NuclearStockpile = 5; // Initial stockpile
            }

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

        // Build nuclear stockpile for advanced civilizations
        if (civ.HasNuclearWeapons && civ.NuclearStockpile < 50)
        {
            if (_random.NextDouble() < 0.01 * deltaTime)
            {
                civ.NuclearStockpile++;
            }
        }

        // Update nuclear plant meltdown risk
        UpdateNuclearPlantRisk(civ, deltaTime, currentYear);

        // Update military strength based on population and tech
        civ.MilitaryStrength = (civ.Population / 1000) + (civ.TechLevel * 10);

        // Territorial expansion (faster with transportation)
        int expansionRate = civ.HasLandTransport ? 2 : 1;
        if (civ.HasAirTransport) expansionRate = 5;

        if (civ.Population > civ.Territory.Count * 1000 && _random.NextDouble() < 0.05)
        {
            ExpandTerritory(civ, expansionRate);
        }

        // Environmental impact
        ApplyEnvironmentalImpact(civ, deltaTime);

        // Check for collapse conditions
        CheckCivilizationCollapse(civ);
    }

    private void UpdateResources(Civilization civ, float deltaTime)
    {
        // Calculate resource production based on territory
        civ.FoodProduction = 0;
        civ.WoodProduction = 0;
        civ.StoneProduction = 0;
        civ.MetalProduction = 0;

        foreach (var (x, y) in civ.Territory)
        {
            var cell = _map.Cells[x, y];
            var biome = cell.GetBiomeData().CurrentBiome;

            // Food production
            // Hunting in forests and grasslands
            if (biome == Biome.TemperateForest || biome == Biome.TropicalRainforest ||
                biome == Biome.BorealForest)
            {
                civ.FoodProduction += 2.0f * cell.Biomass; // Hunting in forests
            }
            if (biome == Biome.Grassland || biome == Biome.Savanna)
            {
                civ.FoodProduction += 3.0f * cell.Biomass; // Hunting/grazing in grasslands
            }

            // Farming (requires Agricultural+)
            if (civ.CivType >= CivType.Agricultural)
            {
                if (cell.Rainfall > 0.4f && cell.Temperature > 5 && cell.Temperature < 35)
                {
                    civ.FoodProduction += 5.0f; // Agriculture
                }
            }

            // Fishing (coastal cells)
            if (cell.IsLand)
            {
                bool hasWaterNeighbor = _map.GetNeighbors(x, y).Any(n => n.cell.IsWater);
                if (hasWaterNeighbor)
                {
                    civ.FoodProduction += 1.5f; // Fishing
                }
            }

            // Wood production from forests
            if (biome == Biome.TemperateForest || biome == Biome.TropicalRainforest ||
                biome == Biome.BorealForest)
            {
                civ.WoodProduction += 1.0f;
                // Deforestation reduces biomass slightly
                cell.Biomass = Math.Max(cell.Biomass - 0.001f, 0.1f);
            }

            // Stone production from mountains
            if (biome == Biome.Mountain || cell.Elevation > 0.6f)
            {
                civ.StoneProduction += 0.5f;
            }

            // Metal production (requires Industrial+)
            if (civ.CivType >= CivType.Industrial)
            {
                var geo = cell.GetGeology();
                // Mining based on rock composition
                float miningPotential = geo.CrystallineRock + geo.VolcanicRock;
                civ.MetalProduction += miningPotential * 0.3f;
            }
        }

        // Food consumption based on population
        civ.FoodConsumption = civ.Population / 100.0f; // Each 100 people need 1 food per year

        // Apply production and consumption
        civ.Food += (civ.FoodProduction - civ.FoodConsumption) * deltaTime;
        civ.Wood += civ.WoodProduction * deltaTime;
        civ.Stone += civ.StoneProduction * deltaTime;
        civ.Metal += civ.MetalProduction * deltaTime;

        // Resource caps
        civ.Food = Math.Max(civ.Food, 0); // Can't go negative (starvation)
        civ.Wood = Math.Max(civ.Wood, 0);
        civ.Stone = Math.Max(civ.Stone, 0);
        civ.Metal = Math.Max(civ.Metal, 0);

        // Cap storage based on civ type
        float storageMultiplier = civ.CivType switch
        {
            CivType.Tribal => 1.0f,
            CivType.Agricultural => 3.0f,
            CivType.Industrial => 10.0f,
            CivType.Scientific => 20.0f,
            CivType.Spacefaring => 50.0f,
            _ => 1.0f
        };

        civ.Food = Math.Min(civ.Food, 1000 * storageMultiplier);
        civ.Wood = Math.Min(civ.Wood, 500 * storageMultiplier);
        civ.Stone = Math.Min(civ.Stone, 500 * storageMultiplier);
        civ.Metal = Math.Min(civ.Metal, 300 * storageMultiplier);

        // Starvation effects
        if (civ.Food <= 0)
        {
            // Population loss from starvation
            int popLoss = (int)(civ.Population * 0.05f * deltaTime);
            civ.Population = Math.Max(civ.Population - popLoss, 100);
        }

        // Natural resource extraction
        ExtractNaturalResources(civ, deltaTime);
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

            // Local pollution in civilization territory
            cell.CO2 += actualEmissions * deltaTime;

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

        // Spread global emissions across entire planet (atmospheric mixing happens faster)
        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                _map.Cells[x, y].CO2 += globalEmissionsPerCell * deltaTime * 0.1f;
            }
        }

        // Industrial civilizations affect solar energy (global warming)
        if (civ.CivType == CivType.Industrial || civ.CivType == CivType.Scientific)
        {
            // Increase greenhouse effect globally
            _map.SolarEnergy += 0.0001f * actualEmissions * deltaTime;
            _map.SolarEnergy = Math.Clamp(_map.SolarEnergy, 0.8f, 1.5f);
        }
    }

    private void CheckCivilizationCollapse(Civilization civ)
    {
        // Environmental collapse
        float avgCO2 = civ.Territory.Average(pos => _map.Cells[pos.x, pos.y].CO2);
        float avgTemp = civ.Territory.Average(pos => _map.Cells[pos.x, pos.y].Temperature);

        if (avgCO2 > 10 || avgTemp > 45 || avgTemp < -15)
        {
            // Civilization decline
            civ.Population = (int)(civ.Population * 0.9f);

            if (civ.Population < 100)
            {
                CollapseCivilization(civ);
            }
        }

        // Check if all territory lost
        if (civ.Territory.Count == 0)
        {
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

        _civilizations.Remove(civ);
    }

    private void HandleCivilizationInteractions(int currentYear)
    {
        // Check for border conflicts or cooperation
        for (int i = 0; i < _civilizations.Count; i++)
        {
            for (int j = i + 1; j < _civilizations.Count; j++)
            {
                var civ1 = _civilizations[i];
                var civ2 = _civilizations[j];

                // Check if civilizations are neighbors
                bool areNeighbors = civ1.Territory.Any(pos1 =>
                    civ2.Territory.Any(pos2 =>
                        Math.Abs(pos1.x - pos2.x) <= 1 &&
                        Math.Abs(pos1.y - pos2.y) <= 1));

                if (areNeighbors)
                {
                    // War or cooperation based on aggression
                    if (civ1.Aggression > 0.7f || civ2.Aggression > 0.7f)
                    {
                        // Declare war if not already at war
                        if (!civ1.AtWar && _random.NextDouble() < 0.1)
                        {
                            civ1.AtWar = true;
                            civ1.WarTargetId = civ2.Id;
                        }
                        if (!civ2.AtWar && _random.NextDouble() < 0.1)
                        {
                            civ2.AtWar = true;
                            civ2.WarTargetId = civ1.Id;
                        }

                        // Nuclear warfare - escalates if both have nukes and war is desperate
                        bool nuclearWarfare = false;
                        if (civ1.HasNuclearWeapons && civ2.HasNuclearWeapons &&
                            (civ1.Population < 50000 || civ2.Population < 50000))
                        {
                            // Desperate situation - risk of nuclear exchange
                            if (_random.NextDouble() < 0.05)
                            {
                                nuclearWarfare = true;
                                LaunchNuclearStrike(civ1, civ2, 0);
                                // Retaliation
                                if (civ2.NuclearStockpile > 0 && _random.NextDouble() < 0.8)
                                {
                                    LaunchNuclearStrike(civ2, civ1, 0);
                                }
                            }
                        }

                        if (!nuclearWarfare)
                        {
                            // Conventional warfare - stronger civ takes territory
                            if (civ1.MilitaryStrength > civ2.MilitaryStrength * 1.5f)
                            {
                                ConquerTerritory(civ1, civ2);
                                // Population losses from war
                                civ1.Population = (int)(civ1.Population * 0.95f);
                                civ2.Population = (int)(civ2.Population * 0.85f);

                                // Nuclear strike as last resort
                                if (civ2.HasNuclearWeapons && civ2.Population < 20000 &&
                                    civ2.NuclearStockpile > 0 && _random.NextDouble() < 0.1)
                                {
                                    LaunchNuclearStrike(civ2, civ1, 0);
                                }
                            }
                            else if (civ2.MilitaryStrength > civ1.MilitaryStrength * 1.5f)
                            {
                                ConquerTerritory(civ2, civ1);
                                civ2.Population = (int)(civ2.Population * 0.95f);
                                civ1.Population = (int)(civ1.Population * 0.85f);

                                // Nuclear strike as last resort
                                if (civ1.HasNuclearWeapons && civ1.Population < 20000 &&
                                    civ1.NuclearStockpile > 0 && _random.NextDouble() < 0.1)
                                {
                                    LaunchNuclearStrike(civ1, civ2, 0);
                                }
                            }
                            else
                            {
                                // Stalemate - both lose population
                                civ1.Population = (int)(civ1.Population * 0.98f);
                                civ2.Population = (int)(civ2.Population * 0.98f);
                            }
                        }
                    }
                    else if (civ1.EcoFriendliness > 0.6f && civ2.EcoFriendliness > 0.6f)
                    {
                        // Peaceful cooperation - tech sharing and trade
                        int avgTech = (civ1.TechLevel + civ2.TechLevel) / 2;
                        civ1.TechLevel = (civ1.TechLevel + avgTech) / 2;
                        civ2.TechLevel = (civ2.TechLevel + avgTech) / 2;

                        // Establish trade routes
                        if (!civ1.TradeRoutes.Contains((civ2.CenterX, civ2.CenterY)))
                        {
                            civ1.TradeRoutes.Add((civ2.CenterX, civ2.CenterY));
                        }
                        if (!civ2.TradeRoutes.Contains((civ1.CenterX, civ1.CenterY)))
                        {
                            civ2.TradeRoutes.Add((civ1.CenterX, civ1.CenterY));
                        }

                        // Economic benefits
                        civ1.Population += 100;
                        civ2.Population += 100;

                        // Climate agreements for advanced civilizations
                        if ((civ1.CivType == CivType.Scientific || civ1.CivType == CivType.Spacefaring) &&
                            (civ2.CivType == CivType.Scientific || civ2.CivType == CivType.Spacefaring))
                        {
                            // Check if global CO2 is high enough to motivate action
                            if (_map.GlobalCO2 > 3.0f && !civ1.InClimateAgreement && !civ2.InClimateAgreement)
                            {
                                // Form climate agreement
                                civ1.InClimateAgreement = true;
                                civ2.InClimateAgreement = true;
                                civ1.ClimatePartners.Add(civ2.Id);
                                civ2.ClimatePartners.Add(civ1.Id);

                                // Emission reduction targets (30-60% reduction)
                                float reductionTarget = 0.3f + (float)_random.NextDouble() * 0.3f;
                                civ1.EmissionReduction = Math.Max(civ1.EmissionReduction, reductionTarget);
                                civ2.EmissionReduction = Math.Max(civ2.EmissionReduction, reductionTarget);
                            }
                        }
                    }
                }
            }
        }
    }

    private void ConquerTerritory(Civilization attacker, Civilization defender)
    {
        // Take some border cells
        var borderCells = defender.Territory
            .Where(pos => attacker.Territory.Any(aPos =>
                Math.Abs(pos.x - aPos.x) <= 1 &&
                Math.Abs(pos.y - aPos.y) <= 1))
            .Take(3)
            .ToList();

        foreach (var cell in borderCells)
        {
            defender.Territory.Remove(cell);
            attacker.Territory.Add(cell);
        }

        defender.Population -= 1000;
    }

    private bool IsCellInCivilization(int x, int y)
    {
        return _civilizations.Any(civ => civ.Territory.Contains((x, y)));
    }

    private void LaunchNuclearStrike(Civilization attacker, Civilization defender, int currentYear)
    {
        if (attacker.NuclearStockpile <= 0) return;

        // Select target in defender's territory
        var target = defender.Territory.ElementAt(_random.Next(defender.Territory.Count));
        attacker.NuclearStockpile--;
        attacker.NuclearStrikes.Add((target.x, target.y, currentYear));

        int strikeX = target.x;
        int strikeY = target.y;

        // Nuclear blast radius (affects 5x5 area)
        for (int dx = -5; dx <= 5; dx++)
        {
            for (int dy = -5; dy <= 5; dy++)
            {
                int nx = (strikeX + dx + _map.Width) % _map.Width;
                int ny = Math.Clamp(strikeY + dy, 0, _map.Height - 1);

                float distance = MathF.Sqrt(dx * dx + dy * dy);
                if (distance > 5) continue;

                var cell = _map.Cells[nx, ny];
                float impactStrength = 1.0f - (distance / 5.0f);

                // Massive destruction
                cell.Biomass *= 0.1f * (1.0f - impactStrength); // 90% of life destroyed at center
                cell.Temperature += 200 * impactStrength; // Extreme heat
                cell.CO2 += 10.0f * impactStrength; // Massive CO2 release

                // Crater formation at ground zero
                if (distance < 2)
                {
                    cell.Elevation -= 0.1f * impactStrength;
                }

                // Radiation contamination
                var geo = cell.GetGeology();
                geo.TectonicStress += 0.5f * impactStrength; // Seismic activity

                // Remove from territories
                foreach (var civ in _civilizations)
                {
                    civ.Territory.Remove((nx, ny));
                }

                // Convert to wasteland
                if (distance < 3)
                {
                    cell.LifeType = LifeForm.None;
                }
            }
        }

        // Global climate impact
        _map.SolarEnergy += 0.02f; // Nuclear winter temporary effect
        _map.GlobalCO2 += 0.5f;

        // Massive population loss
        defender.Population = (int)(defender.Population * 0.3f); // 70% casualties
        attacker.Population = (int)(attacker.Population * 0.95f); // Some losses from retaliation
    }

    public List<Civilization> GetAllCivilizations() => _civilizations;

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
                float dist = MathF.Sqrt((city1.X - city2.X) * (city1.X - city2.X) +
                                       (city1.Y - city2.Y) * (city1.Y - city2.Y));
                if (dist < minDist)
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
    /// Find the best location for a new city based on strategic factors
    /// </summary>
    private (int x, int y, float score) FindBestCityLocation(Civilization civ)
    {
        var candidates = new List<(int x, int y, float score)>();

        // Evaluate each territory cell for city placement
        foreach (var (x, y) in civ.Territory)
        {
            var cell = _map.Cells[x, y];

            // Cities must be on land
            if (!cell.IsLand) continue;

            // Don't place cities too close to existing cities
            bool tooClose = civ.Cities.Any(c =>
            {
                int dx = Math.Abs(c.X - x);
                int dy = Math.Abs(c.Y - y);
                return Math.Sqrt(dx * dx + dy * dy) < 10; // Minimum 10 cells apart
            });
            if (tooClose) continue;

            // Calculate strategic scores
            float resourceScore = CalculateResourceScore(x, y);
            float defenseScore = CalculateDefenseScore(x, y);
            float commerceScore = CalculateCommerceScore(x, y);

            // Combined score with weights
            float totalScore = (resourceScore * 0.4f) + (defenseScore * 0.3f) + (commerceScore * 0.3f);

            candidates.Add((x, y, totalScore));
        }

        // Return best location
        if (candidates.Count == 0)
        {
            // Fallback to random if no good candidates
            var location = civ.Territory.ElementAt(_random.Next(civ.Territory.Count));
            return (location.x, location.y, 0f);
        }

        // Pick from top 5 candidates to add variety
        var topCandidates = candidates.OrderByDescending(c => c.score).Take(5).ToList();
        return topCandidates[_random.Next(topCandidates.Count)];
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

    private void CreateCity(Civilization civ, int x, int y)
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
            Id = civ.Cities.Count + 1,
            Name = GenerateCityName(civ),
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
            NearbyResources = nearbyResources
        };

        civ.Cities.Add(city);
    }

    private static readonly string[] CityPrefixes = new[]
    {
        "New", "Old", "North", "South", "East", "West", "Upper", "Lower",
        "Great", "Little", "Fort", "Port", "San", "Saint"
    };

    private static readonly string[] CitySuffixes = new[]
    {
        "ville", "town", "city", "burg", "port", "haven", "field", "ford",
        "dale", "shire", "land", "stead", "ton", "ham", "chester"
    };

    private static readonly string[] CityNames = new[]
    {
        "Ashford", "Brightwater", "Clearspring", "Deepwood", "Eastmarch",
        "Fairhaven", "Goldfield", "Highmont", "Ironforge", "Jadehaven",
        "Kingsport", "Lakeview", "Meadowbrook", "Northwind", "Oakdale",
        "Pinecrest", "Queenstown", "Riverdale", "Stonebridge", "Thornbury",
        "Underhill", "Valleyview", "Westport", "Yewdale", "Zenith"
    };

    private string GenerateCityName(Civilization civ)
    {
        // Use a combination of civ name and random elements
        if (_random.NextDouble() < 0.5 && civ.Cities.Count == 0)
        {
            return civ.Name + " Capital";
        }

        return CityNames[_random.Next(CityNames.Length)] + " " + (civ.Cities.Count + 1);
    }

    public void LoadCivilizations(List<CivilizationData> civData)
    {
        _civilizations.Clear();
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
                EcoFriendliness = data.EcoFriendliness
            };
            civ.Territory.UnionWith(data.Territory);
            _civilizations.Add(civ);
        }

        _nextCivId = _civilizations.Any() ? _civilizations.Max(c => c.Id) + 1 : 1;
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
            // Hereditary succession
            var heir = FindHeir(civ, deadRuler);

            if (heir != null)
            {
                // Smooth succession
                civ.Government.CurrentRuler = heir;
                civ.Government.Stability = Math.Min(civ.Government.Stability + 0.1f, 1.0f);
            }
            else
            {
                // No heir - succession crisis
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
    /// Find the heir to a deceased ruler
    /// </summary>
    private Ruler? FindHeir(Civilization civ, Ruler deadRuler)
    {
        // Look for children
        if (deadRuler.ChildrenIds.Count > 0)
        {
            var heirId = deadRuler.ChildrenIds.First();
            var heir = civ.AllRulers.FirstOrDefault(r => r.Id == heirId && r.IsAlive);
            if (heir != null) return heir;

            // Create new heir if not yet generated
            var newHeir = _divinePowers.GenerateRandomRuler(civ, 0);
            newHeir.Id = _nextRulerId++;
            newHeir.Age = 20 + _random.Next(20);
            newHeir.ParentId = deadRuler.Id;
            newHeir.DynastyId = deadRuler.DynastyId;
            civ.AllRulers.Add(newHeir);
            return newHeir;
        }

        return null;
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

        // Food shortage reduces stability
        if (civ.Food < 10)
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
        // Population losses from civil war
        civ.Population = (int)(civ.Population * 0.85f);

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
        foreach (var civ in _civilizations)
        {
            foreach (var relation in civ.DiplomaticRelations.Values)
            {
                // Update treaty expirations
                foreach (var treaty in relation.Treaties.Where(t => t.IsActive).ToList())
                {
                    if (treaty.HasExpired(currentYear))
                    {
                        treaty.IsActive = false;
                    }
                }

                // Peace increases opinion slowly
                if (relation.Status != DiplomaticStatus.War)
                {
                    relation.YearsAtPeace++;
                    relation.Opinion += 0.5f;
                }
                else
                {
                    relation.YearsAtWar++;
                }

                // Trust increases during peace
                if (relation.Status == DiplomaticStatus.Friendly || relation.Status == DiplomaticStatus.Allied)
                {
                    relation.TrustLevel = Math.Min(relation.TrustLevel + 0.01f, 1.0f);
                }

                // Check for treaty proposals between friendly civilizations
                if (relation.Status == DiplomaticStatus.Friendly && !relation.HasTreaty(TreatyType.TradePact) &&
                    _random.NextDouble() < 0.05)
                {
                    // Propose trade pact
                    var treaty = new Treaty(TreatyType.TradePact, currentYear);
                    relation.AddTreaty(treaty);
                }

                // Check for royal marriages (hereditary governments only)
                var civ1 = _civilizations.FirstOrDefault(c => c.Id == relation.CivilizationId1);
                var civ2 = _civilizations.FirstOrDefault(c => c.Id == relation.CivilizationId2);

                if (civ1 != null && civ2 != null &&
                    relation.Status == DiplomaticStatus.Friendly &&
                    civ1.Government?.IsHereditary == true &&
                    civ2.Government?.IsHereditary == true &&
                    !relation.HasTreaty(TreatyType.RoyalMarriage) &&
                    _random.NextDouble() < 0.02)
                {
                    // Royal marriage
                    ProposeRoyalMarriage(civ1, civ2, relation, currentYear);
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

                // Check for extreme CO2
                if (cell.CO2 > 5.0f)
                {
                    disastersInTerritory++;
                    totalDamage += 50;
                }

                // Check for drought (low rainfall in agricultural areas)
                if (cell.Rainfall < 0.2f && civ.CivType >= CivType.Agricultural)
                {
                    disastersInTerritory++;
                    totalDamage += 30;
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
                // Calculate casualties based on preparedness
                float baseCasualtyRate = 0.01f * disastersInTerritory * (1.0f - civ.DisasterPreparedness);

                // Cyclones are more deadly
                if (cycloneHits > 0)
                {
                    baseCasualtyRate += 0.05f * cycloneHits * (1.0f - civ.DisasterPreparedness);
                }

                int casualties = (int)(civ.Population * baseCasualtyRate);

                civ.Population -= casualties;
                civ.PopulationLostToDisasters += casualties;
                civ.DisastersSurvived++;

                // Improve preparedness over time
                civ.DisasterPreparedness = Math.Min(civ.DisasterPreparedness + 0.05f, 0.9f);

                // Disasters reduce stability
                if (civ.Government != null)
                {
                    float stabilityLoss = 0.05f * disastersInTerritory;
                    // Cyclones cause more political instability
                    if (cycloneHits > 0)
                    {
                        stabilityLoss += 0.1f * cycloneHits;
                    }
                    civ.Government.Stability -= stabilityLoss;
                }

                // Resource losses
                float resourceLoss = 0.1f * disastersInTerritory;
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
}

public enum CityType
{
    Village,     // < 1000 pop
    Town,        // 1000-5000
    City,        // 5000-50000
    Metropolis   // > 50000
}
