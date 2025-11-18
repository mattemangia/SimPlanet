namespace SimPlanet;

/// <summary>
/// Simulates life evolution and biomass dynamics
/// </summary>
public class LifeSimulator
{
    private readonly PlanetMap _map;
    private readonly Random _random;
    private float _autoReseedTimer = 0f;
    private const float AUTO_RESEED_CHECK_INTERVAL = 5.0f; // Check every 5 seconds
    private LifeSupportProfile _lifeProfile;
    private bool _lifeProfileInitialized = false;
    
    // Grace period after manual planting to allow life to establish
    private float _plantingGracePeriod = 0f;
    private const float PLANTING_GRACE_DURATION = 5f; // 5 seconds of protection

    private const float PROFILE_SMOOTHING = 0.05f;
    private const float EXTREME_RELAX_RATE = 0.015f;

    private struct LifeSupportProfile
    {
        public float AvgOxygen;
        public float OxygenStdDev;
        public float AvgLandTemp;
        public float MinLandTemp;
        public float MaxLandTemp;
        public float LandTempStdDev;
        public float AvgLandRain;
        public float MinLandRain;
        public float MaxLandRain;
        public float LandRainStdDev;
        public float AvgWaterTemp;
        public float MinWaterTemp;
        public float MaxWaterTemp;
        public float WaterTempStdDev;
    }

    public LifeSimulator(PlanetMap map)
    {
        _map = map;
        _random = new Random();
        
        // Initialize life profile with reasonable default values based on Earth-like conditions
        // This prevents instant death when life is first seeded
        _lifeProfile = new LifeSupportProfile
        {
            AvgOxygen = 20f,  // Default 20% oxygen
            OxygenStdDev = 5f,
            AvgLandTemp = 15f,  // 15°C average land temp
            MinLandTemp = -20f,  // Reasonable min temp
            MaxLandTemp = 40f,   // Reasonable max temp
            LandTempStdDev = 10f,
            AvgLandRain = 0.5f,  // 50% average rainfall
            MinLandRain = 0.1f,
            MaxLandRain = 0.9f,
            LandRainStdDev = 0.2f,
            AvgWaterTemp = 10f,  // 10°C average water temp
            MinWaterTemp = -2f,  // Near freezing
            MaxWaterTemp = 30f,  // Warm tropical waters
            WaterTempStdDev = 8f
        };
        
        // Mark as initialized so we don't get invalid windows
        _lifeProfileInitialized = true;
    }

    public void Update(float deltaTime, GeologicalSimulator? geoSim = null, WeatherSystem? weatherSys = null)
    {
        UpdateLifeSupportProfile();
        
        // Countdown grace period
        if (_plantingGracePeriod > 0f)
        {
            _plantingGracePeriod -= deltaTime;
            if (_plantingGracePeriod <= 0f)
            {
                Console.WriteLine("[LifeSimulator] Grace period ended");
            }
        }

        // React to planetary events FIRST
        if (geoSim != null)
        {
            ReactToVolcanicEruptions(geoSim);
            ReactToEarthquakes(geoSim);
        }

        if (weatherSys != null)
        {
            ReactToStorms(weatherSys);
        }

        ReactToClimateChanges(deltaTime);

        // Then normal life processes
        SimulateBiomassGrowth(deltaTime);
        SimulateEvolution(deltaTime);
        SimulateLifeSpread(deltaTime);

        // Auto-reseed life if extinct and conditions are good
        _autoReseedTimer += deltaTime;
        if (_autoReseedTimer >= AUTO_RESEED_CHECK_INTERVAL)
        {
            _autoReseedTimer = 0f;
            CheckAndAutoReseedLife();
        }
    }

    public void SeedInitialLife()
    {
        // Seed bacteria in warm, wet areas
        SeedSpecificLife(LifeForm.Bacteria);
    }
    
    public void ActivatePlantingGracePeriod()
    {
        // Called when manual planting happens
        _plantingGracePeriod = PLANTING_GRACE_DURATION;
        Console.WriteLine("[LifeSimulator] Grace period activated for planted life");
    }

    public void SeedSpecificLife(LifeForm lifeForm)
    {
        UpdateLifeSupportProfile();

        // Seed the specified life form in appropriate locations
        // Try many more times to ensure good coverage
        int attempts = lifeForm == LifeForm.Bacteria ? 2000 : 500; // Bacteria should spread widely
        int successfulSeeds = 0;
        int maxSeeds = lifeForm == LifeForm.Bacteria ? 1000 : 200; // Cap total seeds

        for (int i = 0; i < attempts && successfulSeeds < maxSeeds; i++)
        {
            int x = _random.Next(_map.Width);
            int y = _random.Next(_map.Height);

            var cell = _map.Cells[x, y];

            // Don't overwrite existing life
            if (cell.LifeType != LifeForm.None)
                continue;

            // Check if location is suitable for this life form based on the current planet profile
            bool suitable = CanLifeSurvive(lifeForm, cell);

            if (suitable)
            {
                cell.LifeType = lifeForm;
                cell.Biomass = lifeForm == LifeForm.Bacteria ? 0.3f : 0.4f; // Start with higher biomass for faster spread
                cell.Evolution = 0.0f;
                successfulSeeds++;
            }
        }

        Console.WriteLine($"[LifeSimulator] Seeded {successfulSeeds} {lifeForm} cells across the planet");
    }

    private void SimulateBiomassGrowth(float deltaTime)
    {
        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                var cell = _map.Cells[x, y];

                if (cell.LifeType == LifeForm.None)
                    continue;

                float growthRate = CalculateGrowthRate(cell, x, y);
                float deathRate = CalculateDeathRate(cell);

                float netGrowth = (growthRate - deathRate) * deltaTime;
                cell.Biomass = Math.Clamp(cell.Biomass + netGrowth, 0, 1);

                // Note: Gas exchange (O2/CO2) handled by AtmosphereSimulator

                // Die off if conditions are too harsh
                if (cell.Biomass < 0.01f)
                {
                    cell.LifeType = LifeForm.None;
                    cell.Biomass = 0;
                    cell.Evolution = 0;
                }
            }
        }
    }

    private float CalculateGrowthRate(TerrainCell cell, int x, int y)
    {
        float growth = 0;

        switch (cell.LifeType)
        {
            case LifeForm.Bacteria:
                float bacteriaMin = Math.Min(_lifeProfile.MinLandTemp, _lifeProfile.MinWaterTemp) - 30f;
                float bacteriaMax = Math.Max(_lifeProfile.MaxLandTemp, _lifeProfile.MaxWaterTemp) + 40f;
                if (cell.Temperature > bacteriaMin && cell.Temperature < bacteriaMax)
                {
                    growth = 0.15f;
                }
                break;

            case LifeForm.Algae:
                if (cell.IsWater && WaterTempBetween(cell, 0.2f, 0.85f))
                {
                    growth = 0.3f * (GetOxygenEfficiency(cell) + 0.5f);
                }
                break;

            case LifeForm.PlantLife:
                if (cell.IsLand && LandTempBetween(cell, 0.25f, 0.85f) &&
                    MeetsRainfall(cell, 0.35f) && MeetsOxygenRequirement(cell, LifeForm.PlantLife))
                {
                    float rainEfficiency = Math.Clamp(cell.Rainfall / Math.Max(0.05f, _lifeProfile.AvgLandRain), 0f, 1.2f);
                    // CO2 fertilization effect - plants thrive in CO2-rich atmosphere (up to 3%)
                    float co2Boost = 1.0f;
                    if (cell.CO2 > 0.5f)
                    {
                        co2Boost = Math.Min(2.0f, 1.0f + (cell.CO2 - 0.5f) * 0.3f);
                    }
                    growth = 0.5f * rainEfficiency * GetOxygenEfficiency(cell) * co2Boost; // Increased base from 0.4f
                }
                break;

            case LifeForm.SimpleAnimals:
                if (LandTempBetween(cell, 0.2f, 0.85f) && MeetsOxygenRequirement(cell, LifeForm.SimpleAnimals))
                {
                    float foodAvailable = GetNearbyBiomass(x, y);
                    growth = 0.2f * Math.Min(foodAvailable, 1.0f) * GetOxygenEfficiency(cell);
                }
                break;

            case LifeForm.Fish:
                if (cell.IsWater && WaterTempBetween(cell, 0.2f, 0.75f) && MeetsOxygenRequirement(cell, LifeForm.Fish))
                {
                    float foodAvailable = GetNearbyBiomass(x, y);
                    growth = 0.25f * Math.Min(foodAvailable, 1.0f) * GetOxygenEfficiency(cell);
                }
                break;

            case LifeForm.Amphibians:
                if (LandTempBetween(cell, 0.3f, 0.8f) && MeetsRainfall(cell, 0.3f) &&
                    MeetsOxygenRequirement(cell, LifeForm.Amphibians))
                {
                    float foodAvailable = GetNearbyBiomass(x, y);
                    growth = 0.2f * Math.Min(foodAvailable, 1.0f) * GetOxygenEfficiency(cell);
                }
                break;

            case LifeForm.Reptiles:
                if (cell.IsLand && LandTempBetween(cell, 0.35f, 0.95f) &&
                    MeetsOxygenRequirement(cell, LifeForm.Reptiles))
                {
                    float foodAvailable = GetNearbyBiomass(x, y);
                    growth = 0.22f * Math.Min(foodAvailable, 1.0f) * GetOxygenEfficiency(cell);
                }
                break;

            case LifeForm.Dinosaurs:
                if (cell.IsLand && LandTempBetween(cell, 0.45f, 0.95f) &&
                    MeetsOxygenRequirement(cell, LifeForm.Dinosaurs))
                {
                    float foodAvailable = GetNearbyBiomass(x, y);
                    growth = 0.3f * Math.Min(foodAvailable, 1.0f) * GetOxygenEfficiency(cell);
                }
                break;

            case LifeForm.MarineDinosaurs:
                if (cell.IsWater && WaterTempBetween(cell, 0.4f, 0.9f) &&
                    MeetsOxygenRequirement(cell, LifeForm.MarineDinosaurs))
                {
                    float foodAvailable = GetNearbyBiomass(x, y);
                    growth = 0.28f * Math.Min(foodAvailable, 1.0f) * GetOxygenEfficiency(cell);
                }
                break;

            case LifeForm.Pterosaurs:
                if (LandTempBetween(cell, 0.4f, 0.9f) && MeetsOxygenRequirement(cell, LifeForm.Pterosaurs))
                {
                    float foodAvailable = GetNearbyBiomass(x, y);
                    growth = 0.25f * Math.Min(foodAvailable, 1.0f) * GetOxygenEfficiency(cell);
                }
                break;

            case LifeForm.Mammals:
                if (LandTempBetween(cell, 0.2f, 0.9f) && MeetsOxygenRequirement(cell, LifeForm.Mammals))
                {
                    float foodAvailable = GetNearbyBiomass(x, y);
                    growth = 0.27f * Math.Min(foodAvailable, 1.0f) * GetOxygenEfficiency(cell);
                }
                break;

            case LifeForm.Birds:
                if (LandTempBetween(cell, 0.3f, 0.95f) && MeetsOxygenRequirement(cell, LifeForm.Birds))
                {
                    float foodAvailable = GetNearbyBiomass(x, y);
                    growth = 0.26f * Math.Min(foodAvailable, 1.0f) * GetOxygenEfficiency(cell);
                }
                break;

            case LifeForm.ComplexAnimals:
                if (LandTempBetween(cell, 0.2f, 0.85f) && MeetsOxygenRequirement(cell, LifeForm.ComplexAnimals))
                {
                    float foodAvailable = GetNearbyBiomass(x, y);
                    growth = 0.15f * Math.Min(foodAvailable, 1.0f) * GetOxygenEfficiency(cell);
                }
                break;

            case LifeForm.Intelligence:
                if (LandTempBetween(cell, 0.25f, 0.8f) && MeetsOxygenRequirement(cell, LifeForm.Intelligence))
                {
                    float ecosystemHealth = GetEcosystemDiversity(x, y);
                    growth = 0.1f * ecosystemHealth;
                }
                break;

            case LifeForm.Civilization:
                if (LandTempBetween(cell, 0.2f, 0.8f) && MeetsOxygenRequirement(cell, LifeForm.Civilization))
                {
                    // Civilizations should grow more robustly with technology
                    // Base growth enhanced by nearby resources
                    float resourceBonus = 1.0f + GetNearbyBiomass(x, y) * 0.5f;
                    growth = 0.35f * resourceBonus; // Increased from 0.2f
                }
                break;
        }

        return growth;
    }

    private float CalculateDeathRate(TerrainCell cell)
    {
        // During grace period, reduce death rates significantly for planted life
        if (_plantingGracePeriod > 0f)
        {
            // Very low death rate during grace period
            return cell.LifeType == LifeForm.Bacteria ? 0.01f : 0.015f;
        }
        
        // Bacteria are extremely resilient
        // Reduced base death rates for better survival
        float death = cell.LifeType == LifeForm.Bacteria ? 0.02f : 0.03f; // Reduced from 0.05f

        var (minComfort, maxComfort) = cell.IsLand
            ? ExpandTemperatureWindow(_lifeProfile.MinLandTemp, _lifeProfile.MaxLandTemp, _lifeProfile.AvgLandTemp, Math.Max(1f, _lifeProfile.LandTempStdDev))
            : ExpandTemperatureWindow(_lifeProfile.MinWaterTemp, _lifeProfile.MaxWaterTemp, _lifeProfile.AvgWaterTemp, Math.Max(1f, _lifeProfile.WaterTempStdDev));

        float tolerance = MathF.Max(2f, (maxComfort - minComfort) * 0.15f);
        float lethalCold = minComfort - tolerance;
        float lethalHeat = maxComfort + tolerance;

        if (cell.LifeType == LifeForm.Bacteria)
        {
            lethalCold -= 20f;
            lethalHeat += 20f;
        }

        if (cell.Temperature < lethalCold || cell.Temperature > lethalHeat)
        {
            death += cell.LifeType == LifeForm.Bacteria ? 0.1f : 0.3f;
        }

        // Lack of oxygen (for aerobic life) - bacteria don't need oxygen
        float oxygenDemand = GetOxygenDemand(cell.LifeType);
        if (oxygenDemand > 0f)
        {
            float oxygenThreshold = Math.Max(2f, _lifeProfile.AvgOxygen * oxygenDemand * 0.7f); // Reduced from 0.8f
            if (cell.Oxygen < oxygenThreshold)
            {
                float deficit = oxygenThreshold - cell.Oxygen;
                death += MathF.Min(0.2f, 0.03f * deficit); // Reduced from 0.3f and 0.05f
            }
        }

        // Too much CO2 relative to the current planet - adjusted for higher CO2 worlds
        // Allow life to adapt to CO2 levels up to 3x global average or 10%, whichever is higher
        float co2Limit = Math.Max(10f, _map.GlobalCO2 * 3f);
        if (cell.LifeType != LifeForm.Bacteria && cell.CO2 > co2Limit)
        {
            // Reduced death rate from CO2 - life adapts over time
            death += 0.05f;
        }

        // Drought pressure is evaluated against the world's rainfall profile
        if (cell.IsLand && cell.LifeType == LifeForm.PlantLife)
        {
            float droughtThreshold = GetRainfallThreshold(0.15f); // Reduced from 0.2f
            if (cell.Rainfall < droughtThreshold)
            {
                death += 0.1f; // Reduced from 0.2f
            }
        }

        return death;
    }

    private void SimulateEvolution(float deltaTime)
    {
        // NOTE: Main evolution logic now handled by AnimalEvolutionSimulator
        // This only handles basic microbe to plant evolution

        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                var cell = _map.Cells[x, y];

                if (cell.LifeType == LifeForm.None || cell.LifeType == LifeForm.Civilization)
                    continue;

                // Evolution progress (only for basic life forms)
                if (cell.Biomass > 0.5f &&
                    (cell.LifeType == LifeForm.Bacteria ||
                     cell.LifeType == LifeForm.Algae ||
                     cell.LifeType == LifeForm.PlantLife))
                {
                    cell.Evolution += deltaTime * 0.01f;

                    // Check for evolution to next stage
                    if (cell.Evolution > 1.0f && _random.NextDouble() < 0.1)
                    {
                        TryEvolve(cell);
                    }
                }
            }
        }
    }

    private void TryEvolve(TerrainCell cell)
    {
        // Only handle basic evolution: bacteria â†’ algae â†’ plants â†’ simple animals
        // AnimalEvolutionSimulator handles the rest
        switch (cell.LifeType)
        {
            case LifeForm.Bacteria:
                // Evolve to algae in water, stay bacteria on land
                if (cell.IsWater)
                {
                    cell.LifeType = LifeForm.Algae;
                    cell.Evolution = 0;
                }
                break;

            case LifeForm.Algae:
                // Algae can colonize land as plants if conditions are right
                if (cell.IsLand && cell.Rainfall > 0.3f && cell.Oxygen > 10)
                {
                    cell.LifeType = LifeForm.PlantLife;
                    cell.Evolution = 0;
                }
                break;

            case LifeForm.PlantLife:
                // Plants enable simple animals (then AnimalEvolutionSimulator takes over)
                if (cell.Oxygen > 15)
                {
                    cell.LifeType = LifeForm.SimpleAnimals;
                    cell.Biomass = 0.3f;
                    cell.Evolution = 0;
                }
                break;
        }
    }

    private void SimulateLifeSpread(float deltaTime)
    {
        // Life spreads to neighboring cells based on biomass and life type
        // Process multiple random cells per update for better coverage
        int spreadAttempts = (int)(100 * deltaTime); // More attempts = faster spread

        for (int attempt = 0; attempt < spreadAttempts; attempt++)
        {
            int x = _random.Next(_map.Width);
            int y = _random.Next(_map.Height);

            var cell = _map.Cells[x, y];

            // Life must have sufficient biomass to spread
            if (cell.LifeType == LifeForm.None || cell.Biomass < 0.1f) // Reduced from 0.15f
                continue;

            // Different life forms spread at different rates
            float spreadChance = cell.LifeType switch
            {
                LifeForm.Bacteria => 0.8f,      // Very fast reproduction
                LifeForm.Algae => 0.6f,         // Fast in water
                LifeForm.PlantLife => 0.7f,     // Seeds, spores - increased from 0.5f
                LifeForm.SimpleAnimals => 0.3f, // Mobile but slower
                LifeForm.Fish => 0.4f,          // Can swim to new areas
                LifeForm.Amphibians => 0.3f,    // Limited range
                LifeForm.Reptiles => 0.25f,     // Slower reproduction
                LifeForm.Dinosaurs => 0.2f,     // Large animals
                LifeForm.Mammals => 0.3f,       // Better at colonization
                LifeForm.Birds => 0.5f,         // Can fly far
                _ => 0.2f
            };

            // Higher biomass = better chance to spread
            spreadChance *= Math.Min(cell.Biomass * 2, 1.0f);

            if (_random.NextDouble() > spreadChance)
                continue;

            // Try to spread to multiple neighbors (life spreads in all directions)
            var neighbors = _map.GetNeighbors(x, y).ToList();
            if (neighbors.Count == 0)
                continue;

            // Shuffle neighbors for random spread direction
            for (int i = neighbors.Count - 1; i > 0; i--)
            {
                int j = _random.Next(i + 1);
                var temp = neighbors[i];
                neighbors[i] = neighbors[j];
                neighbors[j] = temp;
            }

            // Try to colonize up to 2 neighbors per spread event
            int colonized = 0;
            foreach (var (nx, ny, neighbor) in neighbors)
            {
                if (colonized >= 2) break;

                // Only spread to empty cells
                if (neighbor.LifeType != LifeForm.None)
                    continue;

                // Check if neighbor environment is suitable
                if (!CanLifeSurvive(cell.LifeType, neighbor))
                    continue;

                // Successfully colonize!
                neighbor.LifeType = cell.LifeType;
                neighbor.Biomass = 0.15f + (cell.Biomass * 0.15f); // Increased from 0.1f and 0.1f
                neighbor.Evolution = cell.Evolution * 0.9f; // Increased from 0.8f
                colonized++;
            }
        }
    }

    private bool CanLifeSurvive(LifeForm lifeType, TerrainCell cell)
    {
        return lifeType switch
        {
            LifeForm.Bacteria =>
                cell.Temperature > Math.Min(_lifeProfile.MinLandTemp, _lifeProfile.MinWaterTemp) - 30f &&
                cell.Temperature < Math.Max(_lifeProfile.MaxLandTemp, _lifeProfile.MaxWaterTemp) + 40f,

            LifeForm.Algae => cell.IsWater && WaterTempBetween(cell, 0.2f, 0.85f),

            LifeForm.PlantLife => cell.IsLand && LandTempBetween(cell, 0.25f, 0.85f) &&
                                   MeetsRainfall(cell, 0.35f) && MeetsOxygenRequirement(cell, LifeForm.PlantLife),

            LifeForm.SimpleAnimals => LandTempBetween(cell, 0.2f, 0.85f) &&
                                       MeetsOxygenRequirement(cell, LifeForm.SimpleAnimals),

            LifeForm.Fish => cell.IsWater && WaterTempBetween(cell, 0.2f, 0.75f) &&
                              MeetsOxygenRequirement(cell, LifeForm.Fish),

            LifeForm.Amphibians => LandTempBetween(cell, 0.3f, 0.8f) && MeetsRainfall(cell, 0.3f) &&
                                    MeetsOxygenRequirement(cell, LifeForm.Amphibians),

            LifeForm.Reptiles => cell.IsLand && LandTempBetween(cell, 0.35f, 0.95f) &&
                                  MeetsOxygenRequirement(cell, LifeForm.Reptiles),

            LifeForm.Dinosaurs => cell.IsLand && LandTempBetween(cell, 0.45f, 0.95f) &&
                                   MeetsOxygenRequirement(cell, LifeForm.Dinosaurs),

            LifeForm.MarineDinosaurs => cell.IsWater && WaterTempBetween(cell, 0.4f, 0.9f) &&
                                         MeetsOxygenRequirement(cell, LifeForm.MarineDinosaurs),

            LifeForm.Pterosaurs => LandTempBetween(cell, 0.4f, 0.9f) &&
                                    MeetsOxygenRequirement(cell, LifeForm.Pterosaurs),

            LifeForm.Mammals => LandTempBetween(cell, 0.2f, 0.9f) &&
                                 MeetsOxygenRequirement(cell, LifeForm.Mammals),

            LifeForm.Birds => LandTempBetween(cell, 0.3f, 0.95f) &&
                               MeetsOxygenRequirement(cell, LifeForm.Birds),

            LifeForm.ComplexAnimals => LandTempBetween(cell, 0.2f, 0.85f) &&
                                       MeetsOxygenRequirement(cell, LifeForm.ComplexAnimals),

            LifeForm.Intelligence => LandTempBetween(cell, 0.25f, 0.8f) &&
                                      MeetsOxygenRequirement(cell, LifeForm.Intelligence),

            LifeForm.Civilization => LandTempBetween(cell, 0.2f, 0.8f) &&
                                      MeetsOxygenRequirement(cell, LifeForm.Civilization),

            _ => false
        };
    }

    private float GetNearbyBiomass(int x, int y)
    {
        float total = 0;
        int count = 0;

        foreach (var (nx, ny, neighbor) in _map.GetNeighbors(x, y))
        {
            if (neighbor.LifeType == LifeForm.PlantLife || neighbor.LifeType == LifeForm.Algae)
            {
                total += neighbor.Biomass;
                count++;
            }
        }

        return count > 0 ? total / count : 0;
    }

    private float GetEcosystemDiversity(int x, int y)
    {
        var lifeTypes = new HashSet<LifeForm>();

        foreach (var (nx, ny, neighbor) in _map.GetNeighbors(x, y))
        {
            if (neighbor.LifeType != LifeForm.None)
            {
                lifeTypes.Add(neighbor.LifeType);
            }
        }

        return lifeTypes.Count / 5.0f; // Normalize by max expected diversity
    }

    private void UpdateLifeSupportProfile()
    {
        float totalOxygen = 0f;
        float totalOxygenSq = 0f;
        int totalCells = 0;
        float landTempSum = 0f;
        float landTempSqSum = 0f;
        float landRainSum = 0f;
        float landRainSqSum = 0f;
        float landTempMin = float.MaxValue;
        float landTempMax = float.MinValue;
        float landRainMin = float.MaxValue;
        float landRainMax = float.MinValue;
        float waterTempSum = 0f;
        float waterTempSqSum = 0f;
        float waterTempMin = float.MaxValue;
        float waterTempMax = float.MinValue;
        int landCells = 0;
        int waterCells = 0;

        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                var cell = _map.Cells[x, y];
                totalCells++;
                totalOxygen += cell.Oxygen;
                totalOxygenSq += cell.Oxygen * cell.Oxygen;

                if (cell.IsLand)
                {
                    landCells++;
                    landTempSum += cell.Temperature;
                    landTempSqSum += cell.Temperature * cell.Temperature;
                    landRainSum += cell.Rainfall;
                    landRainSqSum += cell.Rainfall * cell.Rainfall;
                    landTempMin = Math.Min(landTempMin, cell.Temperature);
                    landTempMax = Math.Max(landTempMax, cell.Temperature);
                    landRainMin = Math.Min(landRainMin, cell.Rainfall);
                    landRainMax = Math.Max(landRainMax, cell.Rainfall);
                }
                else
                {
                    waterCells++;
                    waterTempSum += cell.Temperature;
                    waterTempSqSum += cell.Temperature * cell.Temperature;
                    waterTempMin = Math.Min(waterTempMin, cell.Temperature);
                    waterTempMax = Math.Max(waterTempMax, cell.Temperature);
                }
            }
        }

        float newAvgOxygen = totalCells > 0 ? totalOxygen / totalCells : _lifeProfile.AvgOxygen;
        float newOxygenStd = 0f;
        if (totalCells > 0)
        {
            float meanSq = totalOxygenSq / totalCells;
            float variance = MathF.Max(0f, meanSq - newAvgOxygen * newAvgOxygen);
            newOxygenStd = MathF.Sqrt(variance);
        }
        _lifeProfile.AvgOxygen = SmoothValue(_lifeProfile.AvgOxygen, newAvgOxygen, PROFILE_SMOOTHING);
        _lifeProfile.OxygenStdDev = SmoothValue(_lifeProfile.OxygenStdDev, newOxygenStd, PROFILE_SMOOTHING);

        if (landCells > 0)
        {
            float avgLandTemp = landTempSum / landCells;
            float landTempVariance = MathF.Max(0f, (landTempSqSum / landCells) - avgLandTemp * avgLandTemp);
            float landTempStd = MathF.Sqrt(landTempVariance);

            float avgLandRain = landRainSum / landCells;
            float landRainVariance = MathF.Max(0f, (landRainSqSum / landCells) - avgLandRain * avgLandRain);
            float landRainStd = MathF.Sqrt(landRainVariance);

            _lifeProfile.AvgLandTemp = SmoothValue(_lifeProfile.AvgLandTemp, avgLandTemp, PROFILE_SMOOTHING);
            _lifeProfile.MinLandTemp = UpdateTrackedMin(_lifeProfile.MinLandTemp, landTempMin);
            _lifeProfile.MaxLandTemp = UpdateTrackedMax(_lifeProfile.MaxLandTemp, landTempMax);
            _lifeProfile.LandTempStdDev = SmoothValue(_lifeProfile.LandTempStdDev, landTempStd, PROFILE_SMOOTHING);

            _lifeProfile.AvgLandRain = SmoothValue(_lifeProfile.AvgLandRain, avgLandRain, PROFILE_SMOOTHING);
            _lifeProfile.MinLandRain = UpdateTrackedMin(_lifeProfile.MinLandRain, landRainMin);
            _lifeProfile.MaxLandRain = UpdateTrackedMax(_lifeProfile.MaxLandRain, landRainMax);
            _lifeProfile.LandRainStdDev = SmoothValue(_lifeProfile.LandRainStdDev, landRainStd, PROFILE_SMOOTHING);
        }
        else
        {
            _lifeProfile.AvgLandTemp = _map.GlobalTemperature;
            _lifeProfile.MinLandTemp = _map.GlobalTemperature - 20f;
            _lifeProfile.MaxLandTemp = _map.GlobalTemperature + 20f;
            _lifeProfile.AvgLandRain = 0.3f;
            _lifeProfile.MinLandRain = 0f;
            _lifeProfile.MaxLandRain = 1f;
            _lifeProfile.LandTempStdDev = 5f;
            _lifeProfile.LandRainStdDev = 0.2f;
        }

        if (waterCells > 0)
        {
            float avgWaterTemp = waterTempSum / waterCells;
            float waterTempVariance = MathF.Max(0f, (waterTempSqSum / waterCells) - avgWaterTemp * avgWaterTemp);
            float waterTempStd = MathF.Sqrt(waterTempVariance);

            _lifeProfile.AvgWaterTemp = SmoothValue(_lifeProfile.AvgWaterTemp, avgWaterTemp, PROFILE_SMOOTHING);
            _lifeProfile.MinWaterTemp = UpdateTrackedMin(_lifeProfile.MinWaterTemp, waterTempMin);
            _lifeProfile.MaxWaterTemp = UpdateTrackedMax(_lifeProfile.MaxWaterTemp, waterTempMax);
            _lifeProfile.WaterTempStdDev = SmoothValue(_lifeProfile.WaterTempStdDev, waterTempStd, PROFILE_SMOOTHING);
        }
        else
        {
            _lifeProfile.AvgWaterTemp = _lifeProfile.AvgLandTemp;
            _lifeProfile.MinWaterTemp = _lifeProfile.MinLandTemp;
            _lifeProfile.MaxWaterTemp = _lifeProfile.MaxLandTemp;
            _lifeProfile.WaterTempStdDev = Math.Max(1f, _lifeProfile.LandTempStdDev);
        }

        _lifeProfileInitialized = true;
    }

    private float SmoothValue(float current, float target, float blend)
    {
        if (!_lifeProfileInitialized || float.IsNaN(current) || float.IsInfinity(current))
            return target;

        return current + (target - current) * blend;
    }

    private float UpdateTrackedMin(float current, float observed)
    {
        if (!_lifeProfileInitialized || current == float.MaxValue)
            return observed;

        if (observed < current)
            return observed;

        return current + (observed - current) * EXTREME_RELAX_RATE;
    }

    private float UpdateTrackedMax(float current, float observed)
    {
        if (!_lifeProfileInitialized || current == float.MinValue)
            return observed;

        if (observed > current)
            return observed;

        return current + (observed - current) * EXTREME_RELAX_RATE;
    }

    private bool LandTempBetween(TerrainCell cell, float normalizedMin, float normalizedMax)
    {
        var (min, max) = GetLandTemperatureWindow(normalizedMin, normalizedMax);
        return cell.Temperature >= min && cell.Temperature <= max;
    }

    private bool WaterTempBetween(TerrainCell cell, float normalizedMin, float normalizedMax)
    {
        var (min, max) = GetWaterTemperatureWindow(normalizedMin, normalizedMax);
        return cell.Temperature >= min && cell.Temperature <= max;
    }

    private (float min, float max) GetLandTemperatureWindow(float normalizedMin, float normalizedMax)
    {
        var (windowMin, windowMax) = ExpandTemperatureWindow(
            _lifeProfile.MinLandTemp,
            _lifeProfile.MaxLandTemp,
            _lifeProfile.AvgLandTemp,
            Math.Max(1f, _lifeProfile.LandTempStdDev)
        );

        float min = LerpRange(windowMin, windowMax, normalizedMin);
        float max = LerpRange(windowMin, windowMax, normalizedMax);
        if (max < min) (min, max) = (max, min);
        return (min, max);
    }

    private (float min, float max) GetWaterTemperatureWindow(float normalizedMin, float normalizedMax)
    {
        var (windowMin, windowMax) = ExpandTemperatureWindow(
            _lifeProfile.MinWaterTemp,
            _lifeProfile.MaxWaterTemp,
            _lifeProfile.AvgWaterTemp,
            Math.Max(1f, _lifeProfile.WaterTempStdDev)
        );

        float min = LerpRange(windowMin, windowMax, normalizedMin);
        float max = LerpRange(windowMin, windowMax, normalizedMax);
        if (max < min) (min, max) = (max, min);
        return (min, max);
    }

    private (float min, float max) ExpandTemperatureWindow(float observedMin, float observedMax, float average, float stdDev)
    {
        if (!_lifeProfileInitialized || observedMin == float.MaxValue || observedMax == float.MinValue)
        {
            float fallback = Math.Max(5f, Math.Abs(_map.GlobalTemperature));
            return (_map.GlobalTemperature - fallback, _map.GlobalTemperature + fallback);
        }

        float safeStd = MathF.Max(1f, stdDev);
        float halfSpan = MathF.Max((observedMax - observedMin) * 0.5f, safeStd * 2.5f);
        float climateBias = MathF.Abs(average) * 0.1f;

        float min = Math.Min(observedMin, average - halfSpan) - (climateBias + safeStd * 0.75f);
        float max = Math.Max(observedMax, average + halfSpan) + (climateBias + safeStd * 0.75f);

        if (max < min) (min, max) = (max, min);
        return (min, max);
    }

    private (float min, float max) GetRainfallWindow()
    {
        if (!_lifeProfileInitialized ||
            _lifeProfile.MinLandRain == float.MaxValue ||
            _lifeProfile.MaxLandRain == float.MinValue)
        {
            float fallback = Math.Clamp(_lifeProfile.AvgLandRain <= 0f ? 0.3f : _lifeProfile.AvgLandRain, 0.05f, 0.95f);
            return (Math.Max(0f, fallback - 0.25f), Math.Min(1f, fallback + 0.25f));
        }

        float avg = _lifeProfile.AvgLandRain;
        float std = MathF.Max(0.02f, _lifeProfile.LandRainStdDev);

        float min = Math.Max(0f, Math.Min(_lifeProfile.MinLandRain, avg - std * 2.5f) - std);
        float max = Math.Min(1f, Math.Max(_lifeProfile.MaxLandRain, avg + std * 2.5f) + std);

        if (max - min < 0.2f)
        {
            float expand = (0.2f - (max - min)) * 0.5f;
            min = Math.Max(0f, min - expand);
            max = Math.Min(1f, max + expand);
        }

        if (max < min) (min, max) = (max, min);
        return (min, max);
    }

    private bool MeetsRainfall(TerrainCell cell, float normalizedMin)
    {
        if (!cell.IsLand)
            return true;

        float threshold = GetRainfallThreshold(normalizedMin);
        return cell.Rainfall >= threshold;
    }

    private float GetRainfallThreshold(float normalizedMin)
    {
        var (min, max) = GetRainfallWindow();
        return LerpRange(min, max, normalizedMin);
    }

    private bool MeetsOxygenRequirement(TerrainCell cell, LifeForm lifeForm)
    {
        return MeetsOxygenRequirement(cell, lifeForm, 1f);
    }

    private bool MeetsOxygenRequirement(TerrainCell cell, LifeForm lifeForm, float multiplier)
    {
        float demand = GetOxygenDemand(lifeForm);
        if (demand <= 0f)
            return true;

        float baseTarget = _lifeProfile.AvgOxygen * demand * multiplier;
        float tolerance = Math.Max(1f, _lifeProfile.AvgOxygen * 0.15f);
        float target = Math.Max(2f, baseTarget - tolerance);
        return cell.Oxygen >= target;
    }

    private float GetOxygenDemand(LifeForm lifeForm)
    {
        return lifeForm switch
        {
            LifeForm.Algae => 0.2f,
            LifeForm.PlantLife => 0.45f,
            LifeForm.SimpleAnimals => 0.7f,
            LifeForm.Fish => 0.6f,
            LifeForm.Amphibians => 0.7f,
            LifeForm.Reptiles => 0.75f,
            LifeForm.Dinosaurs => 0.85f,
            LifeForm.MarineDinosaurs => 0.8f,
            LifeForm.Pterosaurs => 0.9f,
            LifeForm.Mammals => 0.85f,
            LifeForm.Birds => 0.9f,
            LifeForm.ComplexAnimals => 0.8f,
            LifeForm.Intelligence => 0.9f,
            LifeForm.Civilization => 0.85f,
            _ => 0f
        };
    }

    private float GetOxygenEfficiency(TerrainCell cell)
    {
        return Math.Clamp(cell.Oxygen / Math.Max(0.1f, _lifeProfile.AvgOxygen), 0f, 1.5f);
    }

    private float LerpRange(float min, float max, float normalized)
    {
        normalized = Math.Clamp(normalized, 0f, 1f);
        return min + (max - min) * normalized;
    }

    // === PLANETARY EVENT REACTIVITY ===

    private void ReactToVolcanicEruptions(GeologicalSimulator geoSim)
    {
        // Life is killed or damaged by volcanic eruptions
        foreach (var (x, y, year) in geoSim.RecentEruptions)
        {
            // Direct impact zone
            var cell = _map.Cells[x, y];
            cell.Biomass = 0; // Complete destruction
            cell.LifeType = LifeForm.None;

            // Surrounding area damage
            foreach (var (nx, ny, neighbor) in _map.GetNeighbors(x, y))
            {
                neighbor.Biomass *= 0.3f; // 70% killed
                if (neighbor.Biomass < 0.1f)
                {
                    neighbor.LifeType = LifeForm.None;
                    neighbor.Biomass = 0;
                }
            }
        }
    }

    private void ReactToEarthquakes(GeologicalSimulator geoSim)
    {
        // Earthquakes damage life based on magnitude
        foreach (var (x, y, magnitude) in geoSim.Earthquakes)
        {
            var cell = _map.Cells[x, y];

            // Higher magnitude = more damage
            float damageRadius = magnitude * 3;

            for (int dx = -(int)damageRadius; dx <= damageRadius; dx++)
            {
                for (int dy = -(int)damageRadius; dy <= damageRadius; dy++)
                {
                    int nx = (x + dx + _map.Width) % _map.Width;
                    int ny = y + dy;
                    if (ny < 0 || ny >= _map.Height) continue;

                    float dist = MathF.Sqrt(dx * dx + dy * dy);
                    if (dist > damageRadius) continue;

                    var target = _map.Cells[nx, ny];

                    // Bacteria are extremely resilient and survive earthquakes
                    if (target.LifeType == LifeForm.Bacteria)
                        continue;

                    float damage = (1 - dist / damageRadius) * magnitude * 0.2f;
                    target.Biomass *= (1 - damage);

                    if (target.Biomass < 0.1f)
                    {
                        target.LifeType = LifeForm.None;
                        target.Biomass = 0;
                    }
                }
            }
        }
    }

    private void ReactToStorms(WeatherSystem weatherSys)
    {
        // Storms damage life
        foreach (var storm in weatherSys.ActiveStorms)
        {
            // Check if storm is a hurricane (any category)
            bool isHurricane = storm.Type >= StormType.HurricaneCategory1 &&
                              storm.Type <= StormType.HurricaneCategory5;

            // Determine radius based on storm type
            int radius = storm.Type switch
            {
                StormType.TropicalDepression => 10,
                StormType.TropicalStorm => 12,
                StormType.HurricaneCategory1 => 15,
                StormType.HurricaneCategory2 => 18,
                StormType.HurricaneCategory3 => 20,
                StormType.HurricaneCategory4 => 22,
                StormType.HurricaneCategory5 => 25,
                StormType.Blizzard => 12,
                _ => 8  // Thunderstorm, Tornado
            };

            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dy = -radius; dy <= radius; dy++)
                {
                    int x = (storm.CenterX + dx + _map.Width) % _map.Width;
                    int y = storm.CenterY + dy;
                    if (y < 0 || y >= _map.Height) continue;

                    float dist = MathF.Sqrt(dx * dx + dy * dy);
                    if (dist > radius) continue;

                    var cell = _map.Cells[x, y];
                    float effectStrength = storm.Intensity * (1 - dist / radius);

                    // Storm damage to biomass (already handled in WeatherSystem, but keep for extra damage)
                    if (isHurricane && effectStrength > 0.5f)
                    {
                        cell.Biomass *= (1 - effectStrength * 0.3f);
                    }
                    else if (storm.Type == StormType.Tornado && effectStrength > 0.7f)
                    {
                        cell.Biomass *= 0.2f; // Severe damage
                    }
                    else if (storm.Type == StormType.Blizzard)
                    {
                        // Blizzards less damaging but affect temperature-sensitive life
                        if (cell.LifeType == LifeForm.PlantLife && effectStrength > 0.6f)
                        {
                            cell.Biomass *= 0.7f;
                        }
                    }

                    if (cell.Biomass < 0.1f)
                    {
                        cell.LifeType = LifeForm.None;
                        cell.Biomass = 0;
                    }
                }
            }
        }
    }

    private void ReactToClimateChanges(float deltaTime)
    {
        // Life responds to gradual climate changes
        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                var cell = _map.Cells[x, y];

                if (cell.LifeType == LifeForm.None) continue;

                float stressFactor = 0;

                // Temperature stress
                if (cell.Temperature > 50 || cell.Temperature < -30)
                {
                    stressFactor += 0.5f;
                }
                else if (cell.Temperature > 40 || cell.Temperature < -20)
                {
                    stressFactor += 0.2f;
                }

                // Oxygen stress (for aerobic life)
                if (cell.LifeType != LifeForm.Bacteria)
                {
                    if (cell.Oxygen < 3) // Reduced from 5
                    {
                        stressFactor += 0.5f; // Reduced from 0.8f
                    }
                    else if (cell.Oxygen < 8) // Reduced from 10
                    {
                        stressFactor += 0.2f; // Reduced from 0.3f
                    }
                }

                // CO2 toxicity - adjusted for high CO2 worlds
                // Life adapts to baseline CO2 levels over time
                if (cell.CO2 > Math.Max(20f, _map.GlobalCO2 * 5f))
                {
                    stressFactor += 0.3f; // Reduced from 0.5f
                }
                else if (cell.CO2 > Math.Max(15f, _map.GlobalCO2 * 3f))
                {
                    stressFactor += 0.1f; // Reduced from 0.2f
                }

                // Drought stress (for land life)
                if (cell.IsLand && cell.Rainfall < 0.1f)
                {
                    if (cell.LifeType == LifeForm.PlantLife)
                    {
                        stressFactor += 0.4f;
                    }
                    else if (cell.LifeType == LifeForm.SimpleAnimals ||
                            cell.LifeType == LifeForm.ComplexAnimals)
                    {
                        stressFactor += 0.3f;
                    }
                }

                // Flooding stress (rapid elevation changes)
                var geo = cell.GetGeology();
                if (geo.SedimentLayer > 0.5f && cell.IsLand)
                {
                    stressFactor += 0.2f; // Burial by sediment
                }

                // Apply stress damage
                if (stressFactor > 0)
                {
                    cell.Biomass *= (1 - stressFactor * deltaTime * 0.1f);

                    if (cell.Biomass < 0.05f)
                    {
                        cell.LifeType = LifeForm.None;
                        cell.Biomass = 0;
                        cell.Evolution = 0;
                    }
                }

                // Adaptation over time
                // Life that survives harsh conditions becomes more resilient
                if (stressFactor > 0.2f && cell.Biomass > 0.3f)
                {
                    cell.Evolution += deltaTime * 0.005f; // Stress-driven evolution
                }
            }
        }
    }

    private void CheckAndAutoReseedLife()
    {
        // Count total life on planet
        int lifeCells = 0;
        float avgTemp = 0f;
        float avgOxygen = 0f;
        int sampleCount = 0;

        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                var cell = _map.Cells[x, y];
                if (cell.LifeType != LifeForm.None)
                {
                    lifeCells++;
                }
                avgTemp += cell.Temperature;
                avgOxygen += cell.Oxygen;
                sampleCount++;
            }
        }

        avgTemp /= sampleCount;
        avgOxygen /= sampleCount;

        int totalCells = _map.Width * _map.Height;
        int reseedThreshold = Math.Max(50, totalCells / 40); // ~2.5% of the map

        // If life is extinct or critically low, try to reseed
        if (lifeCells < reseedThreshold)
        {
            // Check if planetary conditions are suitable for life relative to the current climate
            bool temperatureOk = avgTemp > _lifeProfile.MinLandTemp - 5f && avgTemp < _lifeProfile.MaxLandTemp + 5f;
            bool hasWater = true; // Assume there's water somewhere on the planet

            if (temperatureOk)
            {
                Console.WriteLine($"[AUTO-RESEED] Life extinct or critically low ({lifeCells}/{reseedThreshold} cells). Planetary conditions favorable (T={avgTemp:F1}Â°C, O2={avgOxygen:F1}%). Re-seeding bacteria...");

                // Reseed bacteria - they're the most resilient
                SeedSpecificLife(LifeForm.Bacteria);

                // If oxygen is high enough, also seed algae
                if (avgOxygen > 5f)
                {
                    Console.WriteLine($"[AUTO-RESEED] Oxygen levels sufficient. Also seeding algae...");
                    SeedSpecificLife(LifeForm.Algae);
                }
            }
            else
            {
                Console.WriteLine($"[AUTO-RESEED] Life extinct but conditions too harsh (T={avgTemp:F1}Â°C). Waiting for better conditions...");
            }
        }
    }
}