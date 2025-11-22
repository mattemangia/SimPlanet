using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Threading;

namespace SimPlanet;

/// <summary>
/// Simulates life evolution and biomass dynamics
/// </summary>
public class LifeSimulator
{
    private readonly PlanetMap _map;
    private readonly Random _random;
    private readonly ThreadLocal<Random> _threadRandom = new(() => new Random(Interlocked.Increment(ref _seed)));
    private static int _seed;
    private float _autoReseedTimer = 0f;
    private const float AUTO_RESEED_CHECK_INTERVAL = 5.0f; // Check every 5 seconds
    private LifeSupportProfile _lifeProfile;
    private bool _lifeProfileInitialized = false;
    
    // Grace period after manual planting to allow life to establish
    private float _plantingGracePeriodYears = 0f;
    private const float PLANTING_GRACE_DURATION_YEARS = 10f; // 10 years of protection

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
        _seed = Environment.TickCount;
        
        // Initialize life profile with broad default values to prevent instant death on new worlds
        _lifeProfile = new LifeSupportProfile
        {
            AvgOxygen = 20f,
            OxygenStdDev = 5f,
            AvgLandTemp = 15f,
            MinLandTemp = -30f,  // Widened from -20
            MaxLandTemp = 50f,   // Widened from 40
            LandTempStdDev = 15f, // Increased variance tolerance
            AvgLandRain = 0.5f,
            MinLandRain = 0.0f,
            MaxLandRain = 1.0f,
            LandRainStdDev = 0.25f,
            AvgWaterTemp = 10f,
            MinWaterTemp = -5f,
            MaxWaterTemp = 35f,
            WaterTempStdDev = 10f
        };
        
        // Mark as initialized so we don't get invalid windows
        _lifeProfileInitialized = true;
    }

    public void Update(float deltaTime, GeologicalSimulator? geoSim = null, WeatherSystem? weatherSys = null)
    {
        UpdateLifeSupportProfile();
        
        // Countdown grace period in game years
        if (_plantingGracePeriodYears > 0f)
        {
            // deltaTime is already scaled by timeSpeed, and 1 second of sim time is 1 year
            _plantingGracePeriodYears -= deltaTime;
            if (_plantingGracePeriodYears <= 0f)
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

        // Only apply climate stress if NOT in grace period
        if (_plantingGracePeriodYears <= 0f)
        {
            ReactToClimateChanges(deltaTime);
        }

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
        _plantingGracePeriodYears = PLANTING_GRACE_DURATION_YEARS;
        Console.WriteLine("[LifeSimulator] Grace period activated for planted life");
    }

    public void SeedSpecificLife(LifeForm lifeForm)
    {
        UpdateLifeSupportProfile();

        // Seed the specified life form in appropriate locations
        int attempts = lifeForm == LifeForm.Bacteria ? 2000 : 500;
        int successfulSeeds = 0;
        int maxSeeds = lifeForm == LifeForm.Bacteria ? 1000 : 200;

        for (int i = 0; i < attempts && successfulSeeds < maxSeeds; i++)
        {
            int x = _random.Next(_map.Width);
            int y = _random.Next(_map.Height);

            var cell = _map.Cells[x, y];

            // Don't overwrite existing life unless it's very weak
            if (cell.LifeType != LifeForm.None && cell.Biomass > 0.1f)
                continue;

            // Check if location is suitable, but use relaxed check for initial seeding
            bool suitable = CanLifeSurvive(lifeForm, cell);

            // Force planting if we are manually seeding (implied by calling this method directly usually)
            // OR if chance is high enough
            if (suitable || _random.NextDouble() < 0.05) 
            {
                cell.LifeType = lifeForm;
                // Start with high biomass to survive initial fluctuations
                cell.Biomass = lifeForm == LifeForm.Bacteria ? 0.5f : 0.6f; 
                cell.Evolution = 0.0f;
                successfulSeeds++;
            }
        }

        Console.WriteLine($"[LifeSimulator] Seeded {successfulSeeds} {lifeForm} cells across the planet");
    }

    private void SimulateBiomassGrowth(float deltaTime)
    {
        var newBiomass = new float[_map.Width, _map.Height];
        var newLifeType = new LifeForm[_map.Width, _map.Height];
        var newEvolution = new float[_map.Width, _map.Height];

        Parallel.For(0, _map.Width, x =>
        {
            for (int y = 0; y < _map.Height; y++)
            {
                var cell = _map.Cells[x, y];
                newBiomass[x, y] = cell.Biomass;
                newLifeType[x, y] = cell.LifeType;
                newEvolution[x, y] = cell.Evolution;

                if (cell.LifeType == LifeForm.None)
                    continue;

                float growthRate = CalculateGrowthRate(cell, x, y);
                float deathRate = CalculateDeathRate(cell);

                float netGrowth = (growthRate - deathRate) * deltaTime;
                
                // If in grace period, prevent negative growth (death)
                if (_plantingGracePeriodYears > 0f && netGrowth < 0f)
                {
                    netGrowth = 0.01f * deltaTime; // Slight positive growth during grace
                }

                newBiomass[x, y] = Math.Clamp(cell.Biomass + netGrowth, 0, 1);

                // Die off if conditions are too harsh AND biomass hits zero
                // Lower threshold slightly to 0.005 to prevent flickering
                if (newBiomass[x, y] < 0.005f)
                {
                    newLifeType[x, y] = LifeForm.None;
                    newBiomass[x, y] = 0;
                    newEvolution[x, y] = 0;
                }
            }
        });

        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                _map.Cells[x, y].Biomass = newBiomass[x, y];
                _map.Cells[x, y].LifeType = newLifeType[x, y];
                _map.Cells[x, y].Evolution = newEvolution[x, y];
            }
        }
    }

    private float CalculateGrowthRate(TerrainCell cell, int x, int y)
    {
        float growth = 0;
        
        // Boosted growth rates to ensure life establishment
        switch (cell.LifeType)
        {
            case LifeForm.Bacteria:
                // Bacteria is very resilient
                growth = 0.4f; // Increased from 0.15f
                break;

            case LifeForm.Algae:
                if (cell.IsWater && WaterTempBetween(cell, 0.1f, 0.95f))
                {
                    growth = 0.6f * (GetOxygenEfficiency(cell) + 0.5f); // Doubled base growth
                }
                break;

            case LifeForm.PlantLife:
                // Plants need to be robust
                if (cell.IsLand)
                {
                     // CO2 fertilization effect
                    float co2Boost = 1.0f;
                    if (cell.CO2 > 0.5f)
                    {
                        co2Boost = Math.Min(2.5f, 1.0f + (cell.CO2 - 0.5f) * 0.4f);
                    }
                    
                    // Basic suitability check for growth calculation (less strict than survival)
                    float suitability = 1.0f;
                    if (cell.Rainfall < 0.1f) suitability *= 0.5f;
                    if (cell.Temperature < -5f || cell.Temperature > 45f) suitability *= 0.5f;

                    growth = 0.8f * suitability * co2Boost; // Significantly increased from 0.5f
                }
                break;

            // Animal growth rates boosted slightly
            case LifeForm.SimpleAnimals:
                growth = 0.4f * Math.Min(GetNearbyBiomass(x, y), 1.0f);
                break;

            case LifeForm.Fish:
                if (cell.IsWater) growth = 0.45f * Math.Min(GetNearbyBiomass(x, y), 1.0f);
                break;

            case LifeForm.Amphibians:
                growth = 0.4f * Math.Min(GetNearbyBiomass(x, y), 1.0f);
                break;

            case LifeForm.Reptiles:
                growth = 0.35f * Math.Min(GetNearbyBiomass(x, y), 1.0f);
                break;

            case LifeForm.Dinosaurs:
                growth = 0.45f * Math.Min(GetNearbyBiomass(x, y), 1.0f);
                break;

            case LifeForm.MarineDinosaurs:
                growth = 0.4f * Math.Min(GetNearbyBiomass(x, y), 1.0f);
                break;

            case LifeForm.Pterosaurs:
                growth = 0.35f * Math.Min(GetNearbyBiomass(x, y), 1.0f);
                break;

            case LifeForm.Mammals:
                growth = 0.4f * Math.Min(GetNearbyBiomass(x, y), 1.0f);
                break;

            case LifeForm.Birds:
                growth = 0.45f * Math.Min(GetNearbyBiomass(x, y), 1.0f);
                break;

            case LifeForm.ComplexAnimals:
                growth = 0.3f * Math.Min(GetNearbyBiomass(x, y), 1.0f);
                break;

            case LifeForm.Intelligence:
                growth = 0.2f * GetEcosystemDiversity(x, y);
                break;

            case LifeForm.Civilization:
                float resourceBonus = 1.0f + GetNearbyBiomass(x, y) * 0.5f;
                growth = 0.5f * resourceBonus; // Increased from 0.35f
                break;
        }

        return growth;
    }

    private float CalculateDeathRate(TerrainCell cell)
    {
        // ABSOLUTE PROTECTION during grace period
        if (_plantingGracePeriodYears > 0f)
        {
            return 0f;
        }
        
        // Reduced base death rate significantly
        float death = 0.005f; // Reduced from 0.02f/0.03f

        // Bacteria are extremely resilient
        if (cell.LifeType == LifeForm.Bacteria) death = 0.001f;

        var (minComfort, maxComfort) = cell.IsLand
            ? ExpandTemperatureWindow(_lifeProfile.MinLandTemp, _lifeProfile.MaxLandTemp, _lifeProfile.AvgLandTemp, Math.Max(5f, _lifeProfile.LandTempStdDev)) // Min std dev 5f
            : ExpandTemperatureWindow(_lifeProfile.MinWaterTemp, _lifeProfile.MaxWaterTemp, _lifeProfile.AvgWaterTemp, Math.Max(5f, _lifeProfile.WaterTempStdDev));

        // Widen tolerance significantly
        float tolerance = MathF.Max(5f, (maxComfort - minComfort) * 0.3f); // Increased from 0.15f
        float lethalCold = minComfort - tolerance;
        float lethalHeat = maxComfort + tolerance;

        if (cell.LifeType == LifeForm.Bacteria)
        {
            lethalCold -= 30f;
            lethalHeat += 30f;
        }

        // Temperature stress
        if (cell.Temperature < lethalCold || cell.Temperature > lethalHeat)
        {
            // Calculate how far outside range
            float deviation = Math.Min(Math.Abs(cell.Temperature - lethalCold), Math.Abs(cell.Temperature - lethalHeat));
            // Gradual death based on deviation, not instant 0.3f hit
            death += Math.Clamp(deviation * 0.01f, 0.01f, 0.2f); 
        }

        // Oxygen stress
        float oxygenDemand = GetOxygenDemand(cell.LifeType);
        if (oxygenDemand > 0f)
        {
            float oxygenThreshold = Math.Max(1f, _lifeProfile.AvgOxygen * oxygenDemand * 0.5f); // Reduced threshold from 0.7f
            if (cell.Oxygen < oxygenThreshold)
            {
                death += 0.05f; // Reduced from calculated deficit
            }
        }

        // Drought pressure (Land Plants)
        if (cell.IsLand && cell.LifeType == LifeForm.PlantLife)
        {
            // Allow plants to survive in drier conditions
            if (cell.Rainfall < 0.05f) // Significantly reduced from dynamic threshold
            {
                death += 0.05f; // Reduced from 0.1f
            }
        }

        return death;
    }

    private void SimulateEvolution(float deltaTime)
    {
        var newEvolution = new float[_map.Width, _map.Height];
        var newLifeType = new LifeForm[_map.Width, _map.Height];
        var newBiomass = new float[_map.Width, _map.Height];

        Parallel.For(0, _map.Width, x =>
        {
            for (int y = 0; y < _map.Height; y++)
            {
                var cell = _map.Cells[x, y];
                newEvolution[x, y] = cell.Evolution;
                newLifeType[x, y] = cell.LifeType;
                newBiomass[x, y] = cell.Biomass;

                if (cell.LifeType == LifeForm.None || cell.LifeType == LifeForm.Civilization)
                    continue;

                // Evolution progress
                if (cell.Biomass > 0.5f)
                {
                    newEvolution[x, y] += deltaTime * 0.02f; // Faster evolution

                    if (newEvolution[x, y] > 1.0f && _threadRandom.Value.NextDouble() < 0.1)
                    {
                        TryEvolve(cell, out var evolvedLifeType, out var evolvedBiomass);
                        newLifeType[x, y] = evolvedLifeType;
                        newBiomass[x, y] = evolvedBiomass;
                        newEvolution[x, y] = 0;
                    }
                }
            }
        });

        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                _map.Cells[x, y].Evolution = newEvolution[x, y];
                _map.Cells[x, y].LifeType = newLifeType[x, y];
                _map.Cells[x, y].Biomass = newBiomass[x, y];
            }
        }
    }

    private void TryEvolve(TerrainCell cell, out LifeForm evolvedLifeType, out float evolvedBiomass)
    {
        evolvedLifeType = cell.LifeType;
        evolvedBiomass = cell.Biomass;

        switch (cell.LifeType)
        {
            case LifeForm.Bacteria:
                if (cell.IsWater)
                {
                    evolvedLifeType = LifeForm.Algae;
                }
                break;

            case LifeForm.Algae:
                // Easier transition to land
                if (cell.IsLand && cell.Rainfall > 0.15f && cell.Oxygen > 5)
                {
                    evolvedLifeType = LifeForm.PlantLife;
                }
                break;

            case LifeForm.PlantLife:
                if (cell.Oxygen > 12)
                {
                    evolvedLifeType = LifeForm.SimpleAnimals;
                    evolvedBiomass = 0.4f;
                }
                break;

            case LifeForm.SimpleAnimals:
                if (cell.Oxygen > 15)
                {
                    evolvedLifeType = LifeForm.ComplexAnimals;
                    evolvedBiomass = 0.3f;
                }
                break;

            case LifeForm.ComplexAnimals:
                if (cell.Oxygen > 18)
                {
                    evolvedLifeType = LifeForm.Intelligence;
                    evolvedBiomass = 0.2f;
                }
                break;
        }
    }

    private void SimulateLifeSpread(float deltaTime)
    {
        int spreadAttempts = (int)(200 * deltaTime); // Doubled spread rate

        for (int attempt = 0; attempt < spreadAttempts; attempt++)
        {
            int x = _random.Next(_map.Width);
            int y = _random.Next(_map.Height);

            var cell = _map.Cells[x, y];

            if (cell.LifeType == LifeForm.None || cell.Biomass < 0.2f)
                continue;

            float spreadChance = 0.5f; // Higher base chance

            if (_random.NextDouble() > spreadChance)
                continue;

            var neighbors = _map.GetNeighbors(x, y).ToList();
            if (neighbors.Count == 0) continue;

            // Randomize neighbors
            var neighbor = neighbors[_random.Next(neighbors.Count)];
            
            if (neighbor.cell.LifeType != LifeForm.None)
                continue;

            // Use relaxed check for spreading
            if (!CanLifeSurvive(cell.LifeType, neighbor.cell))
                continue;

            neighbor.cell.LifeType = cell.LifeType;
            neighbor.cell.Biomass = 0.2f; // Robust starter biomass
            neighbor.cell.Evolution = cell.Evolution * 0.5f;
        }
    }

    private bool CanLifeSurvive(LifeForm lifeType, TerrainCell cell)
    {
        if (!_lifeProfileInitialized) return false;

        var (minTemp, maxTemp) = cell.IsLand
            ? ExpandTemperatureWindow(_lifeProfile.MinLandTemp, _lifeProfile.MaxLandTemp, _lifeProfile.AvgLandTemp, _lifeProfile.LandTempStdDev)
            : ExpandTemperatureWindow(_lifeProfile.MinWaterTemp, _lifeProfile.MaxWaterTemp, _lifeProfile.AvgWaterTemp, _lifeProfile.WaterTempStdDev);

        // Widen the "survivable" window beyond the comfort zone by 3 standard deviations
        float tempTolerance = Math.Max(10f, (cell.IsLand ? _lifeProfile.LandTempStdDev : _lifeProfile.WaterTempStdDev) * 3);
        minTemp -= tempTolerance;
        maxTemp += tempTolerance;

        // Bacteria are extremophiles
        if (lifeType == LifeForm.Bacteria)
        {
            minTemp -= 40f;
            maxTemp += 40f;
        }

        bool tempOK = cell.Temperature >= minTemp && cell.Temperature <= maxTemp;
        if (!tempOK) return false;

        // Oxygen check for aerobic life
        float oxygenDemand = GetOxygenDemand(lifeType);
        if (oxygenDemand > 0)
        {
            // Life can survive at 30% of the planet's average oxygen level
            float minOxygen = _lifeProfile.AvgOxygen * 0.3f;
            if (cell.Oxygen < minOxygen) return false;
        }

        // Water check for land plants
        if (lifeType == LifeForm.PlantLife)
        {
            // Plants can survive at 20% of the planet's average rainfall
            if (cell.Rainfall < _lifeProfile.AvgLandRain * 0.2f) return false;
        }

        return true;
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

        return lifeTypes.Count / 5.0f;
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
        int landCells = 0;
        float waterTempSum = 0f;
        float waterTempSqSum = 0f;
        float waterTempMin = float.MaxValue;
        float waterTempMax = float.MinValue;
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
            // Ensure min std dev to prevent narrow windows on uniform maps
            landTempStd = Math.Max(5.0f, landTempStd); 

            float avgLandRain = landRainSum / landCells;
            float landRainVariance = MathF.Max(0f, (landRainSqSum / landCells) - avgLandRain * avgLandRain);
            float landRainStd = MathF.Sqrt(landRainVariance);

            _lifeProfile.AvgLandTemp = SmoothValue(_lifeProfile.AvgLandTemp, avgLandTemp, PROFILE_SMOOTHING);
            _lifeProfile.MinLandTemp = UpdateTrackedMin(_lifeProfile.MinLandTemp, landTempMin);
            _lifeProfile.MaxLandTemp = UpdateTrackedMax(_lifeProfile.MaxLandTemp, landTempMax);
            _lifeProfile.LandTempStdDev = SmoothValue(_lifeProfile.LandTempStdDev, landTempStd, PROFILE_SMOOTHING);

            _lifeProfile.AvgLandRain = SmoothValue(_lifeProfile.AvgLandRain, avgLandRain, PROFILE_SMOOTHING);
            _lifeProfile.LandRainStdDev = SmoothValue(_lifeProfile.LandRainStdDev, landRainStd, PROFILE_SMOOTHING);
        }
        else
        {
             // Fallback defaults if no land
            _lifeProfile.AvgLandTemp = _map.GlobalTemperature;
            _lifeProfile.LandTempStdDev = 15f; 
            _lifeProfile.MinLandTemp = _map.GlobalTemperature - 30f;
            _lifeProfile.MaxLandTemp = _map.GlobalTemperature + 30f;
        }

        if (waterCells > 0)
        {
            float avgWaterTemp = waterTempSum / waterCells;
            float waterTempVariance = MathF.Max(0f, (waterTempSqSum / waterCells) - avgWaterTemp * avgWaterTemp);
            float waterTempStd = MathF.Sqrt(waterTempVariance);
            waterTempStd = Math.Max(5.0f, waterTempStd); 

            _lifeProfile.AvgWaterTemp = SmoothValue(_lifeProfile.AvgWaterTemp, avgWaterTemp, PROFILE_SMOOTHING);
            _lifeProfile.MinWaterTemp = UpdateTrackedMin(_lifeProfile.MinWaterTemp, waterTempMin);
            _lifeProfile.MaxWaterTemp = UpdateTrackedMax(_lifeProfile.MaxWaterTemp, waterTempMax);
            _lifeProfile.WaterTempStdDev = SmoothValue(_lifeProfile.WaterTempStdDev, waterTempStd, PROFILE_SMOOTHING);
        }
        
        _lifeProfileInitialized = true;
    }

    private float SmoothValue(float current, float target, float blend)
    {
        if (!_lifeProfileInitialized || float.IsNaN(current)) return target;
        return current + (target - current) * blend;
    }

    private float UpdateTrackedMin(float current, float observed)
    {
        if (!_lifeProfileInitialized || current == float.MaxValue) return observed;
        if (observed < current) return observed;
        return current + (observed - current) * EXTREME_RELAX_RATE;
    }

    private float UpdateTrackedMax(float current, float observed)
    {
        if (!_lifeProfileInitialized || current == float.MinValue) return observed;
        if (observed > current) return observed;
        return current + (observed - current) * EXTREME_RELAX_RATE;
    }

    private bool WaterTempBetween(TerrainCell cell, float normalizedMin, float normalizedMax)
    {
        // Simply use raw checks based on profile
        var (min, max) = GetWaterTemperatureWindow(normalizedMin, normalizedMax);
        return cell.Temperature >= min && cell.Temperature <= max;
    }

    private (float min, float max) GetWaterTemperatureWindow(float normalizedMin, float normalizedMax)
    {
        var (windowMin, windowMax) = ExpandTemperatureWindow(
            _lifeProfile.MinWaterTemp,
            _lifeProfile.MaxWaterTemp,
            _lifeProfile.AvgWaterTemp,
            Math.Max(5f, _lifeProfile.WaterTempStdDev)
        );

        float min = LerpRange(windowMin, windowMax, normalizedMin);
        float max = LerpRange(windowMin, windowMax, normalizedMax);
        return (Math.Min(min, max), Math.Max(min, max));
    }

    private (float min, float max) ExpandTemperatureWindow(float observedMin, float observedMax, float average, float stdDev)
    {
        if (!_lifeProfileInitialized) return (-50f, 100f); // Safe default

        float safeStd = MathF.Max(5f, stdDev); // Minimum 5 degree spread
        float halfSpan = MathF.Max((observedMax - observedMin) * 0.5f, safeStd * 3.0f); // Wider window (3 sigma)
        
        float min = average - halfSpan;
        float max = average + halfSpan;
        
        // Ensure window includes observed extremes
        min = Math.Min(min, observedMin - safeStd);
        max = Math.Max(max, observedMax + safeStd);

        return (min, max);
    }
    
    private float GetOxygenDemand(LifeForm lifeForm)
    {
        // Reduced oxygen demands
        return lifeForm switch
        {
            LifeForm.Algae => 0.1f,
            LifeForm.PlantLife => 0.2f,
            LifeForm.SimpleAnimals => 0.4f,
            LifeForm.Fish => 0.4f,
            LifeForm.Amphibians => 0.5f,
            LifeForm.Reptiles => 0.5f,
            LifeForm.Dinosaurs => 0.6f,
            LifeForm.Mammals => 0.6f,
            _ => 0.5f
        };
    }

    private float GetOxygenEfficiency(TerrainCell cell)
    {
        return Math.Clamp(cell.Oxygen / Math.Max(5f, _lifeProfile.AvgOxygen), 0.5f, 1.5f);
    }

    private float LerpRange(float min, float max, float normalized)
    {
        normalized = Math.Clamp(normalized, 0f, 1f);
        return min + (max - min) * normalized;
    }

    // === PLANETARY EVENT REACTIVITY ===

    private void ReactToVolcanicEruptions(GeologicalSimulator geoSim)
    {
        foreach (var (x, y, year) in geoSim.RecentEruptions)
        {
            var cell = _map.Cells[x, y];
            cell.Biomass = 0;
            cell.LifeType = LifeForm.None;

            foreach (var (nx, ny, neighbor) in _map.GetNeighbors(x, y))
            {
                neighbor.Biomass *= 0.5f;
            }
        }
    }

    private void ReactToEarthquakes(GeologicalSimulator geoSim)
    {
        foreach (var (x, y, magnitude) in geoSim.Earthquakes)
        {
            // Less lethal earthquakes
            float damageRadius = magnitude * 1.5f;
            for (int dx = -(int)damageRadius; dx <= damageRadius; dx++)
            {
                for (int dy = -(int)damageRadius; dy <= damageRadius; dy++)
                {
                    int nx = (x + dx + _map.Width) % _map.Width;
                    int ny = Math.Clamp(y + dy, 0, _map.Height - 1);
                    
                    float dist = MathF.Sqrt(dx * dx + dy * dy);
                    if (dist > damageRadius) continue;

                    var target = _map.Cells[nx, ny];
                    if (target.LifeType == LifeForm.Bacteria) continue;

                    float damage = (1 - dist / damageRadius) * magnitude * 0.1f; // Reduced damage
                    target.Biomass *= (1 - damage);
                }
            }
        }
    }

    private void ReactToStorms(WeatherSystem weatherSys)
    {
        foreach (var storm in weatherSys.ActiveStorms)
        {
            if (storm.Intensity < 0.5f) continue;
            
            // Only very strong storms damage life significantly now
            float radius = 5 + storm.Intensity * 10;
            
            for (int dx = -(int)radius; dx <= radius; dx++)
            {
                for (int dy = -(int)radius; dy <= radius; dy++)
                {
                    int x = (storm.CenterX + dx + _map.Width) % _map.Width;
                    int y = Math.Clamp(storm.CenterY + dy, 0, _map.Height - 1);
                    
                    float dist = MathF.Sqrt(dx * dx + dy * dy);
                    if (dist > radius) continue;

                    var cell = _map.Cells[x, y];
                    if (cell.LifeType == LifeForm.None) continue;
                    
                    
                    float damage = storm.Intensity * (1 - dist/radius) * 0.05f;
                    cell.Biomass = Math.Max(0, cell.Biomass - damage);
                }
            }
        }
    }

    private void ReactToClimateChanges(float deltaTime)
    {
        // CRITICAL FIX: Do not apply climate stress during grace period
        if (_plantingGracePeriodYears > 0f) return;

        var newBiomass = new float[_map.Width, _map.Height];
        var newLifeType = new LifeForm[_map.Width, _map.Height];
        var newEvolution = new float[_map.Width, _map.Height];

        Parallel.For(0, _map.Width, x =>
        {
            for (int y = 0; y < _map.Height; y++)
            {
                var cell = _map.Cells[x, y];
                newBiomass[x, y] = cell.Biomass;
                newLifeType[x, y] = cell.LifeType;
                newEvolution[x, y] = cell.Evolution;

                if (cell.LifeType == LifeForm.None) continue;

                // Protect civilization from simple climate stress logic - they have their own manager
                if (cell.LifeType == LifeForm.Civilization) continue;

                float stressFactor = 0;

                // Use adaptive comfort windows from the life profile
                var (minComfort, maxComfort) = cell.IsLand
                    ? ExpandTemperatureWindow(_lifeProfile.MinLandTemp, _lifeProfile.MaxLandTemp, _lifeProfile.AvgLandTemp, _lifeProfile.LandTempStdDev)
                    : ExpandTemperatureWindow(_lifeProfile.MinWaterTemp, _lifeProfile.MaxWaterTemp, _lifeProfile.AvgWaterTemp, _lifeProfile.WaterTempStdDev);
                
                if (cell.LifeType == LifeForm.Bacteria) { minComfort -= 40f; maxComfort += 40f; }

                if (cell.Temperature > maxComfort)
                {
                    stressFactor += (cell.Temperature - maxComfort) * 0.01f;
                }
                else if (cell.Temperature < minComfort)
                {
                    stressFactor += (minComfort - cell.Temperature) * 0.01f;
                }

                // Oxygen stress based on planetary average
                float oxygenDemand = GetOxygenDemand(cell.LifeType);
                if (oxygenDemand > 0)
                {
                    // Stress occurs below 50% of average oxygen
                    float oxygenThreshold = _lifeProfile.AvgOxygen * 0.5f;
                    if (cell.Oxygen < oxygenThreshold)
                    {
                        stressFactor += (oxygenThreshold - cell.Oxygen) / oxygenThreshold * 0.1f;
                    }
                }

                // Drought stress for plants based on average rainfall
                if (cell.IsLand && cell.LifeType == LifeForm.PlantLife)
                {
                    // Stress occurs below 30% of average rainfall
                    float droughtThreshold = _lifeProfile.AvgLandRain * 0.3f;
                    if (cell.Rainfall < droughtThreshold)
                    {
                        stressFactor += (droughtThreshold - cell.Rainfall) / droughtThreshold * 0.1f;
                    }
                }

                // Apply stress damage slowly
                if (stressFactor > 0)
                {
                    newBiomass[x, y] -= stressFactor * deltaTime * 0.01f;

                    if (newBiomass[x, y] < 0.005f)
                    {
                        newLifeType[x, y] = LifeForm.None;
                        newBiomass[x, y] = 0;
                        newEvolution[x, y] = 0;
                    }
                }
            }
        });

        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                _map.Cells[x, y].Biomass = newBiomass[x, y];
                _map.Cells[x, y].LifeType = newLifeType[x, y];
                _map.Cells[x, y].Evolution = newEvolution[x, y];
            }
        }
    }

    private void CheckAndAutoReseedLife()
    {
        // Count total life on planet
        int lifeCells = 0;
        int totalCells = _map.Width * _map.Height;

        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                if (_map.Cells[x, y].LifeType != LifeForm.None)
                {
                    lifeCells++;
                }
            }
        }

        // If life is extinct or critically low (less than 1% of map), try to reseed
        if (lifeCells < totalCells * 0.01f)
        {
             // Simply reseed without strict checks to kickstart the loop
             SeedSpecificLife(LifeForm.Bacteria);
             
             if (_map.GlobalOxygen > 5f)
                 SeedSpecificLife(LifeForm.Algae);
                 
             if (_map.GlobalOxygen > 10f)
                 SeedSpecificLife(LifeForm.PlantLife);
        }
    }
}