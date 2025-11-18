using System;

namespace SimPlanet;

/// <summary>
/// Automatically stabilizes planetary conditions to maintain habitability and life support
/// Monitors and adjusts temperature, atmosphere, water, and magnetic field
/// </summary>
public class PlanetStabilizer
{
    private readonly PlanetMap _map;
    private readonly MagnetosphereSimulator _magnetosphere;
    private float _adjustmentTimer = 0f;
    private const float AdjustmentInterval = 2.0f; // Baseline adjustment cadence
    private float _responseMultiplier = 1f;

    // Stabilization targets (Earth-like conditions)
    private const float TargetGlobalTemp = 15.0f; // 15Â°C average
    private const float TargetOxygen = 21.0f; // 21% oxygen (modern Earth)
    private const float MinCO2 = 0.01f; // Minimum CO2 for photosynthesis
    private const float MaxCO2 = 5.0f; // Maximum safe CO2 (allows early planet high CO2)
    private const float TargetLandRatio = 0.29f; // 29% land
    private const float TargetMagneticField = 1.0f; // Earth-like
    private const float TargetCoreTemp = 5000f; // Active core

    public bool IsActive { get; set; } = true; // Auto-enabled to prevent runaway climate issues

    // Statistics
    public int AdjustmentsMade { get; private set; } = 0;
    public string LastAction { get; private set; } = "Inactive";

    public PlanetStabilizer(PlanetMap map, MagnetosphereSimulator magnetosphere)
    {
        _map = map;
        _magnetosphere = magnetosphere;
    }

    public void Update(float deltaTime, float timeSpeed)
    {
        if (!IsActive) return;

        _responseMultiplier = CalculateResponseMultiplier(timeSpeed);
        _adjustmentTimer += deltaTime;

        float effectiveInterval = AdjustmentInterval / MathF.Max(1f, _responseMultiplier);
        effectiveInterval = Math.Max(0.05f, effectiveInterval); // allow very fast reaction at high speeds

        if (_adjustmentTimer >= effectiveInterval)
        {
            int adjustmentsNeeded = (int)(_adjustmentTimer / effectiveInterval);
            _adjustmentTimer -= adjustmentsNeeded * effectiveInterval;

            for (int i = 0; i < adjustmentsNeeded; i++)
            {
                PerformStabilization();
            }
        }
    }

    private float CalculateResponseMultiplier(float timeSpeed)
    {
        float clampedSpeed = Math.Clamp(timeSpeed, 0.25f, 128f);
        float multiplier = MathF.Pow(clampedSpeed, 0.95f);
        return Math.Clamp(multiplier, 0.5f, 25f);
    }

    private void PerformStabilization()
    {
        // Calculate current global averages
        float avgTemp = CalculateAverageTemperature();
        float avgOxygen = CalculateAverageOxygen();
        float avgCO2 = CalculateAverageCO2();
        float landRatio = CalculateLandRatio();

        // Priority 1: Stabilize magnetosphere (protects against radiation)
        StabilizeMagnetosphere();

        // Priority 2: Stabilize temperature (critical for life)
        StabilizeTemperature(avgTemp, avgCO2);

        // Priority 3: Stabilize atmosphere composition
        StabilizeAtmosphere(avgOxygen, avgCO2);

        // Priority 4: Stabilize water levels
        StabilizeWaterCycle();

        // Priority 5: Maintain moisture corridors before deserts can form
        ReinforceMoistureCorridors();

        // Priority 6: Reverse fast-forming deserts during high-speed play
        CombatRapidDesertification();

        // Priority 7: Prevent runaway ice ages or greenhouse effects
        PreventExtremeFeedbacks();

        // Priority 8: ACTIVELY PROTECT LIFE from disasters and harsh conditions
        ProtectAndNurtureLife();
    }

    private void StabilizeMagnetosphere()
    {
        // Ensure magnetic field is active to protect from radiation
        if (_magnetosphere.CoreTemperature < TargetCoreTemp)
        {
            _magnetosphere.CoreTemperature += 100f; // Gradually warm core
            LastAction = "Warming planetary core";
            AdjustmentsMade++;
        }

        if (!_magnetosphere.HasDynamo && _magnetosphere.CoreTemperature >= 3000f)
        {
            _magnetosphere.HasDynamo = true;
            _magnetosphere.MagneticFieldStrength = TargetMagneticField;
            LastAction = "Reactivated magnetic dynamo";
            AdjustmentsMade++;
        }

        // Restore magnetic field strength if weakened
        if (_magnetosphere.MagneticFieldStrength < 0.8f)
        {
            _magnetosphere.MagneticFieldStrength = Math.Min(
                _magnetosphere.MagneticFieldStrength + 0.1f,
                TargetMagneticField
            );
            LastAction = "Restoring magnetic field";
            AdjustmentsMade++;
        }
    }

    private void StabilizeTemperature(float avgTemp, float avgCO2)
    {
        float tempDeviation = avgTemp - TargetGlobalTemp;

        // CRITICAL FIX: More aggressive temperature control - act sooner and stronger
        // Too cold - increase greenhouse gases slightly
        if (tempDeviation < -5f)
        {
            // Add CO2 to warm planet (but not too much)
            if (avgCO2 < MaxCO2)
            {
                AdjustCO2Globally(0.01f);
                LastAction = $"Adding CO2 to warm planet (avg: {avgTemp:F1}Â°C)";
                AdjustmentsMade++;
            }
        }
        // Too hot - reduce greenhouse gases (FIXED: lower threshold from 10f to 5f, more aggressive reduction)
        else if (tempDeviation > 5f)
        {
            // Calculate avg methane and N2O to control them too
            float avgMethane = CalculateAverageMethane();
            float avgN2O = CalculateAverageN2O();

            // CRITICAL FIX: Control methane and N2O, which were causing runaway heating!
            // Prioritize removing the most potent gases first
            if (avgN2O > 0.5f)
            {
                ReduceN2OGlobally(0.2f); // Aggressive N2O reduction
                LastAction = $"Removing N2O to cool planet (avg: {avgTemp:F1}Â°C, N2O: {avgN2O:F2})";
                AdjustmentsMade++;
            }
            else if (avgMethane > 1.0f)
            {
                ReduceMethaneGlobally(0.3f); // Aggressive methane reduction
                LastAction = $"Removing methane to cool planet (avg: {avgTemp:F1}Â°C, CH4: {avgMethane:F2})";
                AdjustmentsMade++;
            }
            else if (avgCO2 > 0.5f)
            {
                AdjustCO2Globally(-0.1f); // Increased from -0.05f
                LastAction = $"Removing CO2 to cool planet (avg: {avgTemp:F1}Â°C)";
                AdjustmentsMade++;
            }
        }

        // Adjust solar energy if extreme (FIXED: lower hot threshold from 40f to 25f)
        if (avgTemp < -10f && _map.SolarEnergy < 1.2f)
        {
            _map.SolarEnergy += 0.01f;
            LastAction = "Increasing solar energy";
            AdjustmentsMade++;
        }
        else if (avgTemp > 25f && _map.SolarEnergy > 0.8f)
        {
            _map.SolarEnergy -= 0.02f; // Increased from -0.01f
            LastAction = "Decreasing solar energy";
            AdjustmentsMade++;
        }

        // EMERGENCY: Direct temperature reduction if critically high anywhere
        ClampExtremeTemperatures();
    }

    private void StabilizeAtmosphere(float avgOxygen, float avgCO2)
    {
        // Maintain oxygen at breathable levels (but don't interfere with early evolution)
        if (avgOxygen < 15f)
        {
            // Boost photosynthesis by enhancing plant growth
            BoostPlantGrowth();
            LastAction = $"Boosting O2 production (current: {avgOxygen:F1}%)";
            AdjustmentsMade++;
        }
        else if (avgOxygen > 30f)
        {
            // Too much oxygen is dangerous (fire risk, toxicity)
            ReduceOxygenGlobally(0.5f);
            LastAction = $"Reducing excess O2 (current: {avgOxygen:F1}%)";
            AdjustmentsMade++;
        }

        // Keep CO2 in safe range for life
        if (avgCO2 > MaxCO2)
        {
            // Dangerously high CO2 - remove excess
            AdjustCO2Globally(-0.1f);
            LastAction = $"Removing dangerous CO2 (current: {avgCO2:F2}%)";
            AdjustmentsMade++;
        }
        else if (avgCO2 < MinCO2)
        {
            // Too little CO2 - plants need it for photosynthesis
            AdjustCO2Globally(0.01f);
            LastAction = $"Adding CO2 for photosynthesis (current: {avgCO2:F3}%)";
            AdjustmentsMade++;
        }
    }

    private void StabilizeWaterCycle()
    {
        float landRatio = CalculateLandRatio();

        // Adjust water levels if too extreme
        if (landRatio < 0.1f)
        {
            // Almost all water - raise some land
            RaiseLandMasses();
            LastAction = "Raising land masses (too much ocean)";
            AdjustmentsMade++;
        }
        else if (landRatio > 0.9f)
        {
            // Almost all land - add water
            AddWaterToLowlands();
            LastAction = "Adding water to lowlands (too much land)";
            AdjustmentsMade++;
        }

        // Ensure adequate rainfall for life
        EnsureRainfallDistribution();
    }

    private void PreventExtremeFeedbacks()
    {
        // Check for runaway ice-albedo feedback (snowball Earth)
        int iceCells = 0;
        int totalCells = 0;

        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                var cell = _map.Cells[x, y];
                totalCells++;
                if (cell.IsIce) iceCells++;
            }
        }

        float iceRatio = (float)iceCells / totalCells;

        // Snowball Earth scenario - break the feedback
        if (iceRatio > 0.7f)
        {
            // Warm the planet to break ice-albedo feedback
            _map.SolarEnergy = Math.Min(_map.SolarEnergy + 0.05f, 1.3f);
            AdjustCO2Globally(0.05f);
            LastAction = "Breaking snowball Earth feedback";
            AdjustmentsMade++;
        }
        // Runaway greenhouse - cool it down
        else if (iceRatio < 0.05f && CalculateAverageTemperature() > 35f)
        {
            AdjustCO2Globally(-0.05f);
            LastAction = "Preventing runaway greenhouse";
            AdjustmentsMade++;
        }
    }

    // Helper methods
    private float CalculateAverageTemperature()
    {
        float total = 0;
        int count = 0;
        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                total += _map.Cells[x, y].Temperature;
                count++;
            }
        }
        return total / count;
    }

    private float CalculateAverageOxygen()
    {
        float total = 0;
        int count = 0;
        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                total += _map.Cells[x, y].Oxygen;
                count++;
            }
        }
        return total / count;
    }

    private float CalculateAverageCO2()
    {
        float total = 0;
        int count = 0;
        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                total += _map.Cells[x, y].CO2;
                count++;
            }
        }
        return total / count;
    }

    private float CalculateAverageMethane()
    {
        float total = 0;
        int count = 0;
        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                total += _map.Cells[x, y].Methane;
                count++;
            }
        }
        return total / count;
    }

    private float CalculateAverageN2O()
    {
        float total = 0;
        int count = 0;
        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                total += _map.Cells[x, y].NitrousOxide;
                count++;
            }
        }
        return total / count;
    }

    private float CalculateLandRatio()
    {
        int landCells = 0;
        int totalCells = 0;
        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                totalCells++;
                if (_map.Cells[x, y].IsLand) landCells++;
            }
        }
        return (float)landCells / totalCells;
    }

    private void AdjustCO2Globally(float amount)
    {
        float scaledAmount = amount * _responseMultiplier;
        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                var cell = _map.Cells[x, y];
                cell.CO2 = Math.Max(0, cell.CO2 + scaledAmount);

                // Update greenhouse effect
                cell.Greenhouse = cell.CO2 * 10f + (cell.Temperature - 15f) * 0.01f;
            }
        }
    }

    private void ReduceOxygenGlobally(float amount)
    {
        float scaledAmount = amount * _responseMultiplier;
        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                _map.Cells[x, y].Oxygen = Math.Max(0, _map.Cells[x, y].Oxygen - scaledAmount);
            }
        }
    }

    private void ReduceMethaneGlobally(float amount)
    {
        float scaledAmount = amount * _responseMultiplier;
        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                var cell = _map.Cells[x, y];
                cell.Methane = Math.Max(0, cell.Methane - scaledAmount);

                // Update greenhouse effect after reducing methane
                UpdateCellGreenhouse(cell);
            }
        }
    }

    private void ReduceN2OGlobally(float amount)
    {
        float scaledAmount = amount * _responseMultiplier;
        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                var cell = _map.Cells[x, y];
                cell.NitrousOxide = Math.Max(0, cell.NitrousOxide - scaledAmount);

                // Update greenhouse effect after reducing N2O
                UpdateCellGreenhouse(cell);
            }
        }
    }

    private void UpdateCellGreenhouse(TerrainCell cell)
    {
        // Recalculate greenhouse effect using the same formula as AtmosphereSimulator
        float co2Effect = cell.CO2 * 0.02f;
        float ch4Effect = cell.Methane * 0.006f;
        float n2oEffect = cell.NitrousOxide * 0.01f;

        float waterVaporEffect = 0;
        if (cell.Humidity > 0.5f)
        {
            waterVaporEffect = (cell.Humidity - 0.5f) * 0.2f;
            if (cell.Temperature > 15)
            {
                float tempFactor = Math.Min((cell.Temperature - 15) * 0.005f, 0.5f);
                waterVaporEffect *= (1.0f + tempFactor);
            }
            waterVaporEffect = Math.Min(waterVaporEffect, 0.3f);
        }

        cell.Greenhouse = co2Effect + ch4Effect + n2oEffect + waterVaporEffect;
        cell.Greenhouse = Math.Clamp(cell.Greenhouse, 0, 5);
    }

    private void BoostPlantGrowth()
    {
        // Enhance plant life to produce more oxygen
        float biomassBoost = 0.05f * _responseMultiplier;
        float oxygenBoost = 0.1f * _responseMultiplier;
        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                var cell = _map.Cells[x, y];
                if (cell.LifeType == LifeForm.PlantLife ||
                    cell.LifeType == LifeForm.Algae)
                {
                    cell.Biomass = Math.Min(cell.Biomass + biomassBoost, 1.0f);
                    cell.Oxygen = Math.Min(cell.Oxygen + oxygenBoost, 35f);
                }
            }
        }
    }

    private void RaiseLandMasses()
    {
        float elevationChange = 0.05f * _responseMultiplier;
        // Raise the lowest land areas
        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                var cell = _map.Cells[x, y];
                if (cell.IsWater && cell.Elevation > -0.3f)
                {
                    cell.Elevation += elevationChange;
                }
            }
        }
    }

    private void AddWaterToLowlands()
    {
        float elevationChange = 0.05f * _responseMultiplier;
        // Lower some land areas to create seas
        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                var cell = _map.Cells[x, y];
                if (cell.IsLand && cell.Elevation < 0.2f)
                {
                    cell.Elevation -= elevationChange;
                }
            }
        }
    }

    private void EnsureRainfallDistribution()
    {
        float rainfallBoost = 0.05f * _responseMultiplier;
        float humidityBoost = 0.05f * _responseMultiplier;
        // Boost rainfall in dry areas with plant life
        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                var cell = _map.Cells[x, y];
                if (cell.IsLand && cell.Rainfall < 0.2f && cell.LifeType != LifeForm.None)
                {
                    cell.Rainfall = Math.Clamp(cell.Rainfall + rainfallBoost, 0, 1f);
                    cell.Humidity = Math.Clamp(cell.Humidity + humidityBoost, 0, 1f);
                }
            }
        }
    }

    private void ReinforceMoistureCorridors()
    {
        int landCells = 0;
        int dryCells = 0;
        int threatenedLifeCells = 0;

        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                var cell = _map.Cells[x, y];
                if (!cell.IsLand) continue;

                landCells++;
                bool moisturePoor = cell.Rainfall < 0.25f || cell.Humidity < 0.35f;
                if (!moisturePoor) continue;

                dryCells++;
                if (cell.LifeType != LifeForm.None)
                {
                    threatenedLifeCells++;
                }
            }
        }

        if (landCells == 0) return;

        float drynessRatio = (float)dryCells / landCells;
        if (drynessRatio < 0.08f && threatenedLifeCells < 200) return;

        float severity = Math.Clamp(drynessRatio * 1.5f, 0f, 1f);
        float rainBoost = (0.02f + 0.1f * severity) * _responseMultiplier;
        float humidityBoost = (0.03f + 0.15f * severity) * _responseMultiplier;
        float cooling = (0.1f + 0.5f * severity) * _responseMultiplier;

        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                var cell = _map.Cells[x, y];
                if (!cell.IsLand) continue;

                bool needsCorridor = cell.Rainfall < 0.4f || cell.Humidity < 0.5f || cell.LifeType != LifeForm.None;
                if (!needsCorridor) continue;

                cell.Rainfall = Math.Clamp(cell.Rainfall + rainBoost, 0f, 1f);
                cell.Humidity = Math.Clamp(cell.Humidity + humidityBoost, 0f, 1f);
                cell.Temperature = MathF.Max(cell.Temperature - cooling, 10f);

                if (cell.LifeType == LifeForm.None && cell.Rainfall > 0.35f && cell.Humidity > 0.45f)
                {
                    cell.LifeType = LifeForm.PlantLife;
                    cell.Biomass = Math.Max(cell.Biomass, 0.15f);
                }
            }
        }

        LastAction = $"Stabilizing moisture corridors (dry land: {(drynessRatio * 100f):F0}%)";
        AdjustmentsMade++;
    }

    private void CombatRapidDesertification()
    {
        int totalLand = 0;
        int desertifyingCells = 0;

        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                var cell = _map.Cells[x, y];
                if (!cell.IsLand) continue;

                totalLand++;
                if (cell.Rainfall < 0.15f && cell.Humidity < 0.25f && cell.Temperature > 25f)
                {
                    desertifyingCells++;
                }
            }
        }

        if (desertifyingCells == 0 || totalLand == 0)
        {
            return;
        }

        float severity = Math.Clamp((float)desertifyingCells / totalLand * 3f, 0f, 1f);
        float rainfallBoost = (0.08f + 0.25f * severity) * _responseMultiplier;
        float humidityBoost = (0.1f + 0.3f * severity) * _responseMultiplier;
        float cooling = (0.4f + 0.6f * severity) * _responseMultiplier;
        float biomassBoost = (0.04f + 0.12f * severity) * _responseMultiplier;

        int assistedCells = 0;

        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                var cell = _map.Cells[x, y];
                if (!cell.IsLand) continue;

                bool isDesertifying = cell.Rainfall < 0.15f && cell.Humidity < 0.25f && cell.Temperature > 25f;
                if (!isDesertifying) continue;

                assistedCells++;
                cell.Rainfall = Math.Clamp(cell.Rainfall + rainfallBoost, 0f, 1f);
                cell.Humidity = Math.Clamp(cell.Humidity + humidityBoost, 0f, 1f);
                cell.Temperature = MathF.Max(cell.Temperature - cooling, 12f);

                if (cell.LifeType == LifeForm.None)
                {
                    cell.LifeType = LifeForm.PlantLife;
                }

                cell.Biomass = Math.Min(cell.Biomass + biomassBoost, 0.7f);
            }
        }

        LastAction = $"Emergency desert recovery: {assistedCells} cells (severity {(severity * 100f):F0}%)";
        AdjustmentsMade++;
    }

    private void ClampExtremeTemperatures()
    {
        // CRITICAL FIX: Directly clamp temperatures that are dangerously high
        // This prevents runaway heating at poles and other regions
        int clampedCells = 0;

        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                var cell = _map.Cells[x, y];

                // Calculate expected max temperature based on latitude
                float latitude = Math.Abs((y - _map.Height / 2.0f) / (_map.Height / 2.0f));

                // Poles (lat > 0.7) should never exceed 10Â°C under normal conditions
                // Mid-latitudes should never exceed 40Â°C
                // Equator can be hot but shouldn't exceed 50Â°C
                float maxAllowedTemp;
                
                // Smooth temperature limit curve based on latitude
                if (latitude < 0.5f)
                {
                    // Tropical to mid-latitude blend
                    maxAllowedTemp = 45f - (latitude * 30f); // 45°C at equator to 30°C at 50° latitude
                }
                else
                {
                    // Mid-latitude to polar blend
                    float polarBlend = (latitude - 0.5f) / 0.5f;
                    maxAllowedTemp = 30f - (polarBlend * 20f); // 30°C at 50° latitude to 10°C at poles
                }

                // Emergency clamp if temperature exceeds safe limits
                if (cell.Temperature > maxAllowedTemp)
                {
                    // Aggressively reduce temperature
                    cell.Temperature = maxAllowedTemp;
                    clampedCells++;

                    // Also reduce greenhouse gases at this location
                    cell.Methane = Math.Max(0, cell.Methane * 0.5f);
                    cell.NitrousOxide = Math.Max(0, cell.NitrousOxide * 0.5f);
                    cell.CO2 = Math.Max(0, cell.CO2 * 0.9f);

                    UpdateCellGreenhouse(cell);
                }
            }
        }

        if (clampedCells > 0)
        {
            LastAction = $"EMERGENCY: Clamped {clampedCells} cells with extreme temperatures";
            AdjustmentsMade++;
        }
    }

    private void ProtectAndNurtureLife()
    {
        // Count life cells and find areas needing help
        int lifeCells = 0;
        int criticalCells = 0; // Life with very low biomass

        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                var cell = _map.Cells[x, y];

                if (cell.LifeType != LifeForm.None)
                {
                    lifeCells++;

                    // Boost struggling life (low biomass but good conditions)
                    if (cell.Biomass < 0.2f && cell.Biomass > 0.01f)
                    {
                        criticalCells++;

                        // Check if conditions are actually good for this life
                        bool goodConditions = cell.LifeType switch
                        {
                            LifeForm.Bacteria => cell.Temperature > -20 && cell.Temperature < 80,
                            LifeForm.Algae => cell.IsWater && cell.Temperature > 0 && cell.Oxygen > 5,
                            LifeForm.PlantLife => cell.IsLand && cell.Temperature > 0 && cell.Rainfall > 0.2f && cell.Oxygen > 10,
                            _ => cell.Temperature > -10 && cell.Temperature < 40 && cell.Oxygen > 15
                        };

                        if (goodConditions)
                        {
                            // Give life a boost to help it recover
                            float biomassBoost = 0.1f * _responseMultiplier;
                            cell.Biomass = Math.Min(cell.Biomass + biomassBoost, 0.5f);
                        }
                    }

                    // Prevent catastrophic die-off from temperature extremes
                    if (cell.LifeType != LifeForm.Bacteria && cell.LifeType != LifeForm.None)
                    {
                        float tempDelta = 2f * _responseMultiplier;
                        // Moderate extreme temperatures where life exists
                        if (cell.Temperature > 50f)
                        {
                            cell.Temperature = Math.Max(cell.Temperature - tempDelta, 45f);
                        }
                        else if (cell.Temperature < -25f)
                        {
                            cell.Temperature = Math.Min(cell.Temperature + tempDelta, -20f);
                        }
                    }

                    // Ensure life has minimum oxygen (except bacteria/algae)
                    if (cell.LifeType != LifeForm.Bacteria && cell.LifeType != LifeForm.Algae)
                    {
                        if (cell.Oxygen < 12f)
                        {
                            float oxygenBoost = 1f * _responseMultiplier;
                            cell.Oxygen = Math.Min(cell.Oxygen + oxygenBoost, 15f);
                        }
                    }

                    // Reduce toxic CO2 where life exists
                    if (cell.CO2 > 10f && cell.LifeType != LifeForm.Bacteria)
                    {
                        float co2Reduction = 0.5f * _responseMultiplier;
                        cell.CO2 = Math.Max(cell.CO2 - co2Reduction, 8f);
                    }
                }
            }
        }

        if (criticalCells > 100)
        {
            LastAction = $"Nurturing {criticalCells} struggling life populations";
            AdjustmentsMade++;
        }

        // If life is critically low globally, take emergency action
        if (lifeCells < 500) // Less than 500 cells with life (about 17% of default map)
        {
            LastAction = $"EMERGENCY: Life critically low ({lifeCells} cells) - boosting survival";
            AdjustmentsMade++;

            // Emergency boost to all surviving life
            for (int x = 0; x < _map.Width; x++)
            {
                for (int y = 0; y < _map.Height; y++)
                {
                    var cell = _map.Cells[x, y];
                    if (cell.LifeType != LifeForm.None && cell.Biomass > 0)
                    {
                        float biomassBoost = 0.15f * _responseMultiplier;
                        cell.Biomass = Math.Min(cell.Biomass + biomassBoost, 0.8f); // Major boost
                    }
                }
            }
        }
    }

    public void Reset()
    {
        AdjustmentsMade = 0;
        LastAction = "Reset";
    }
}