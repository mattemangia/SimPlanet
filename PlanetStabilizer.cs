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
    private const float AdjustmentInterval = 1.0f; // Faster baseline interval (was 2.0f)
    private float _responseMultiplier = 1f;
    private float _currentTimeSpeed = 1f;

    // Stabilization targets are derived from the planet that was generated instead of
    // forcing Earth-specific numbers. Each world establishes its own baseline and we
    // nudge conditions back toward that moving average instead of hard-coded values.
    private readonly float _baselineGlobalTemp;
    private readonly float _baselineOxygen;
    private readonly float _baselineCO2;
    private readonly float _baselineLandRatio;
    private readonly float _baselineMagneticField;
    private readonly float _baselineCoreTemp;

    private float _targetGlobalTemp;
    private float _targetOxygen;
    private float _minCO2;
    private float _maxCO2;
    private float _targetLandRatio;
    private float _targetMagneticField;
    private float _targetCoreTemp;

    public bool IsActive { get; set; } = true; // Auto-enabled to prevent runaway climate issues
    
    // Emergency mode for when life is newly planted or critically endangered
    private bool _emergencyLifeProtection = false;
    private float _emergencyModeTimer = 0f;
    private const float EMERGENCY_MODE_DURATION = 60f; // Increased to 60 seconds of aggressive protection

    // Statistics
    public int AdjustmentsMade { get; private set; } = 0;
    public string LastAction { get; private set; } = "Inactive";

    public PlanetStabilizer(PlanetMap map, MagnetosphereSimulator magnetosphere)
    {
        _map = map;
        _magnetosphere = magnetosphere;

        // Store baselines for reference
        _baselineGlobalTemp = map.GlobalTemperature;
        _baselineOxygen = map.GlobalOxygen;
        _baselineCO2 = map.GlobalCO2;
        _baselineLandRatio = CalculateLandRatio();
        _baselineMagneticField = magnetosphere.MagneticFieldStrength;
        _baselineCoreTemp = magnetosphere.CoreTemperature;

        // Set LIFE-FRIENDLY targets regardless of initial conditions
        // These are optimal for supporting diverse life forms
        _targetGlobalTemp = 20f; // Optimal temperature for life (warmer than 15f)
        _targetOxygen = 21f; // Earth-like oxygen for complex life
        _minCO2 = 0.03f; // Minimum for photosynthesis (300 ppm)
        _maxCO2 = 0.15f; // Maximum safe level (1500 ppm) - relaxed
        _targetLandRatio = 0.3f; // 30% land is ideal
        _targetMagneticField = Math.Max(0.5f, _baselineMagneticField); // Strong field for radiation protection
        _targetCoreTemp = Math.Max(4000f, _baselineCoreTemp); // Hot core for magnetic field
    }
    
    /// <summary>
    /// Activates emergency life protection mode when life is planted or endangered
    /// </summary>
    public void ActivateEmergencyLifeProtection()
    {
        _emergencyLifeProtection = true;
        _emergencyModeTimer = EMERGENCY_MODE_DURATION;
        LastAction = "EMERGENCY LIFE PROTECTION ACTIVATED";
        Console.WriteLine("[PlanetStabilizer] Emergency life protection mode activated!");
    }

    public void Update(float deltaTime, float timeSpeed)
    {
        if (!IsActive) return;

        // Update emergency mode timer
        if (_emergencyLifeProtection)
        {
            _emergencyModeTimer -= deltaTime;
            if (_emergencyModeTimer <= 0f)
            {
                _emergencyLifeProtection = false;
                Console.WriteLine("[PlanetStabilizer] Emergency life protection mode deactivated");
            }
        }

        _currentTimeSpeed = timeSpeed;
        _responseMultiplier = CalculateResponseMultiplier(timeSpeed);
        
        // In emergency mode, stabilize much more aggressively
        if (_emergencyLifeProtection)
        {
            _responseMultiplier *= 5f; // Quintuple the response rate
        }
        
        _adjustmentTimer += deltaTime;

        float effectiveInterval = AdjustmentInterval / MathF.Max(1f, _responseMultiplier);
        effectiveInterval = Math.Max(0.02f, effectiveInterval); // allow extremely fast reaction
        
        // In emergency mode, update continuously
        if (_emergencyLifeProtection)
        {
            effectiveInterval = 0.01f; 
        }

        if (_adjustmentTimer >= effectiveInterval)
        {
            // Use a while loop to catch up on missed adjustments at high speed
            while (_adjustmentTimer >= effectiveInterval)
            {
                PerformStabilization();
                _adjustmentTimer -= effectiveInterval;
            }
        }
    }

    private float CalculateResponseMultiplier(float timeSpeed)
    {
        // More aggressive scaling with time speed to keep up
        float clampedSpeed = Math.Clamp(timeSpeed, 0.25f, 128f);
        float multiplier = MathF.Pow(clampedSpeed, 1.1f); 
        return Math.Clamp(multiplier, 1.0f, 50f);
    }

    private void PerformStabilization()
    {
        // Calculate current global averages
        float avgTemp = CalculateAverageTemperature();
        float avgOxygen = CalculateAverageOxygen();
        float avgCO2 = CalculateAverageCO2();
        float avgLandRainfall = CalculateAverageLandRainfall();

        // Priority 1: ACTIVELY PROTECT LIFE from disasters and harsh conditions
        // Moved to Priority 1 to ensure life survives before global adjustments happen
        ProtectAndNurtureLife();

        // Priority 2: Stabilize magnetosphere (protects against radiation)
        StabilizeMagnetosphere();

        // Priority 3: Stabilize temperature (critical for life)
        StabilizeTemperature(avgTemp, avgCO2);

        // Priority 4: Stabilize atmosphere composition
        StabilizeAtmosphere(avgOxygen, avgCO2);

        // Priority 5: Stabilize water levels
        StabilizeWaterCycle(avgLandRainfall);
        ManageRainfallMultiplier(avgLandRainfall);

        // Priority 6: Maintain moisture corridors before deserts can form
        ReinforceMoistureCorridors(avgLandRainfall);

        // Priority 7: Reverse fast-forming deserts during high-speed play
        CombatRapidDesertification(avgLandRainfall);

        // Priority 8: Prevent runaway ice ages or greenhouse effects
        PreventExtremeFeedbacks();
    }

    private static bool IsCivilizationCell(TerrainCell cell)
    {
        return cell.LifeType == LifeForm.Civilization;
    }

    private void StabilizeMagnetosphere()
    {
        // Ensure magnetic field is active to protect from radiation
        if (_magnetosphere.CoreTemperature < _targetCoreTemp)
        {
            _magnetosphere.CoreTemperature += 150f * _responseMultiplier; // Faster warming
            LastAction = "Warming planetary core";
            AdjustmentsMade++;
        }

        if (!_magnetosphere.HasDynamo && _magnetosphere.CoreTemperature >= Math.Min(3000f, _targetCoreTemp))
        {
            _magnetosphere.HasDynamo = true;
            _magnetosphere.MagneticFieldStrength = _targetMagneticField;
            LastAction = "Reactivated magnetic dynamo";
            AdjustmentsMade++;
        }

        // Restore magnetic field strength if weakened
        if (_magnetosphere.MagneticFieldStrength < _targetMagneticField)
        {
            _magnetosphere.MagneticFieldStrength = Math.Min(
                _magnetosphere.MagneticFieldStrength + 0.05f * _responseMultiplier,
                _targetMagneticField + 0.5f // Buffer
            );
            LastAction = "Restoring magnetic field";
            AdjustmentsMade++;
        }
    }

    private void StabilizeTemperature(float avgTemp, float avgCO2)
    {
        float tempDeviation = avgTemp - _targetGlobalTemp;

        // AGGRESSIVE temperature control
        // If life is present, we cannot allow global temp to drift too far
        
        // Too cold - warm up
        if (tempDeviation < -1f) 
        {
            // Add CO2 to warm planet
            if (avgCO2 < _maxCO2 * 1.5f) // Allow overshoot to correct temp
            {
                AdjustCO2Globally(0.05f); 
                LastAction = $"Adding CO2 to warm planet (avg: {avgTemp:F1}Â°C)";
                AdjustmentsMade++;
            }
            
            // Increase solar energy directly
            if (_map.SolarEnergy < 1.5f)
            {
                _map.SolarEnergy += 0.01f * _responseMultiplier;
                LastAction = $"Increasing solar energy (avg: {avgTemp:F1}Â°C)";
            }
        }
        // Too hot - cool down
        else if (tempDeviation > 1f) 
        {
            // Calculate avg methane and N2O to control them too
            float avgMethane = CalculateAverageMethane();
            float avgN2O = CalculateAverageN2O();

            // Prioritize removing the most potent gases first
            if (avgN2O > 0.1f)
            {
                ReduceN2OGlobally(0.5f); 
                LastAction = $"Removing N2O to cool planet (avg: {avgTemp:F1}Â°C)";
                AdjustmentsMade++;
            }
            else if (avgMethane > 0.2f)
            {
                ReduceMethaneGlobally(0.5f); 
                LastAction = $"Removing methane to cool planet (avg: {avgTemp:F1}Â°C)";
                AdjustmentsMade++;
            }
            else if (avgCO2 > 0.1f)
            {
                AdjustCO2Globally(-0.2f);
                LastAction = $"Removing CO2 to cool planet (avg: {avgTemp:F1}Â°C)";
                AdjustmentsMade++;
            }
            
            // Decrease solar energy
            if (_map.SolarEnergy > 0.6f)
            {
                _map.SolarEnergy -= 0.01f * _responseMultiplier;
                LastAction = $"Decreasing solar energy (avg: {avgTemp:F1}Â°C)";
            }
        }

        // EMERGENCY: Direct temperature reduction if critically high anywhere
        ClampExtremeTemperatures();
    }

    private void StabilizeAtmosphere(float avgOxygen, float avgCO2)
    {
        float lowOxygenThreshold = 15f;
        float highOxygenThreshold = 35f;

        // Maintain oxygen at breathable levels
        if (avgOxygen < lowOxygenThreshold)
        {
            // Boost photosynthesis massively
            BoostPlantGrowth();
            // Direct injection if critically low
            if (avgOxygen < 10f)
            {
                InjectOxygenGlobally(0.5f);
            }
            LastAction = $"Boosting O2 production (current: {avgOxygen:F1}%)";
            AdjustmentsMade++;
        }
        else if (avgOxygen > highOxygenThreshold)
        {
            ReduceOxygenGlobally(0.5f);
            LastAction = $"Reducing excess O2 (current: {avgOxygen:F1}%)";
            AdjustmentsMade++;
        }

        // Keep CO2 in safe range for life
        if (avgCO2 < _minCO2)
        {
            AdjustCO2Globally(0.02f);
            LastAction = $"Adding CO2 for photosynthesis (current: {avgCO2:F3}%)";
            AdjustmentsMade++;
        }
    }

    private void StabilizeWaterCycle(float avgLandRainfall)
    {
        float landRatio = CalculateLandRatio();
        float lowLandThreshold = _targetLandRatio * 0.8f;
        float highLandThreshold = _targetLandRatio * 1.2f;

        // Adjust water levels if too extreme
        if (landRatio < lowLandThreshold)
        {
            RaiseLandMasses();
            LastAction = "Raising land masses (too much ocean)";
            AdjustmentsMade++;
        }
        else if (landRatio > highLandThreshold)
        {
            AddWaterToLowlands();
            LastAction = "Adding water to lowlands (too much land)";
            AdjustmentsMade++;
        }

        // Ensure adequate rainfall for life
        EnsureRainfallDistribution(avgLandRainfall);
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
        if (iceRatio > 0.6f)
        {
            // Warm the planet to break ice-albedo feedback
            _map.SolarEnergy = Math.Min(_map.SolarEnergy + 0.1f, 2.0f); // Aggressive boost
            AdjustCO2Globally(0.1f);
            LastAction = "Breaking snowball Earth feedback";
            AdjustmentsMade++;
        }
        // Runaway greenhouse - cool it down
        else if (iceRatio < 0.01f && CalculateAverageTemperature() > 35f)
        {
            AdjustCO2Globally(-0.1f);
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

    private float CalculateAverageLandRainfall()
    {
        float total = 0;
        int count = 0;
        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                var cell = _map.Cells[x, y];
                if (!cell.IsLand) continue;

                total += cell.Rainfall;
                count++;
            }
        }

        if (count == 0) return 0f;
        return total / count;
    }

    private float CalculateAverageOceanRainfall()
    {
        float total = 0f;
        int count = 0;
        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                var cell = _map.Cells[x, y];
                if (!cell.IsWater || cell.IsIce) continue;

                total += cell.Rainfall;
                count++;
            }
        }

        if (count == 0) return 0f;
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
                // Direct greenhouse update
                UpdateCellGreenhouse(cell);
            }
        }
    }
    
    private void InjectOxygenGlobally(float amount)
    {
        float scaledAmount = amount * _responseMultiplier;
        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                _map.Cells[x, y].Oxygen += scaledAmount;
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
                UpdateCellGreenhouse(cell);
            }
        }
    }

    private void UpdateCellGreenhouse(TerrainCell cell)
    {
        // Simplified recalculate greenhouse effect
        float co2Effect = cell.CO2 * 0.02f;
        float ch4Effect = cell.Methane * 0.006f;
        float n2oEffect = cell.NitrousOxide * 0.01f;

        float waterVaporEffect = 0;
        if (cell.Humidity > 0.5f)
        {
            waterVaporEffect = (cell.Humidity - 0.5f) * 0.2f;
        }

        cell.Greenhouse = co2Effect + ch4Effect + n2oEffect + waterVaporEffect;
        cell.Greenhouse = Math.Clamp(cell.Greenhouse, 0, 10);
    }

    private void BoostPlantGrowth()
    {
        // Enhance plant life to produce more oxygen
        float biomassBoost = 0.1f * _responseMultiplier;
        float oxygenBoost = 0.2f * _responseMultiplier;
        
        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                var cell = _map.Cells[x, y];
                if (cell.LifeType == LifeForm.PlantLife || cell.LifeType == LifeForm.Algae)
                {
                    cell.Biomass = Math.Min(cell.Biomass + biomassBoost, 1.0f);
                    cell.Oxygen = Math.Min(cell.Oxygen + oxygenBoost, 35f);
                }
            }
        }
    }

    private void RaiseLandMasses()
    {
        float elevationChange = 0.02f * _responseMultiplier;
        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                var cell = _map.Cells[x, y];
                if (cell.IsWater && cell.Elevation > -0.2f)
                {
                    cell.Elevation += elevationChange;
                }
            }
        }
    }

    private void AddWaterToLowlands()
    {
        float elevationChange = 0.02f * _responseMultiplier;
        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                var cell = _map.Cells[x, y];
                if (cell.IsLand && cell.Elevation < 0.2f && !IsCivilizationCell(cell))
                {
                    cell.Elevation -= elevationChange;
                }
            }
        }
    }

    private void EnsureRainfallDistribution(float avgLandRainfall)
    {
        float rainfallBoost = 0.1f * _responseMultiplier;
        bool severeDrought = avgLandRainfall < 0.18f;
        bool catastrophicDrought = avgLandRainfall < 0.12f;
        float barrenBoost = catastrophicDrought ? rainfallBoost : rainfallBoost * 0.4f;
        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                var cell = _map.Cells[x, y];
                bool supportsLife = cell.LifeType != LifeForm.None;

                if (cell.IsLand && cell.Rainfall < 0.1f && supportsLife)
                {
                    cell.Rainfall = Math.Clamp(cell.Rainfall + rainfallBoost, 0, 1f);
                    cell.Humidity = Math.Clamp(cell.Humidity + rainfallBoost, 0, 1f);
                }
                else if (catastrophicDrought && cell.IsLand && cell.Rainfall < 0.08f)
                {
                    float boost = barrenBoost * (1f - (cell.Rainfall / 0.08f));
                    cell.Rainfall = Math.Clamp(cell.Rainfall + boost, 0, 1f);
                    cell.Humidity = Math.Clamp(cell.Humidity + boost * 0.8f, 0, 1f);
                }
                else if (severeDrought && cell.IsLand && cell.Rainfall < 0.15f && supportsLife)
                {
                    cell.Rainfall = Math.Clamp(cell.Rainfall + rainfallBoost * 0.5f, 0, 1f);
                    cell.Humidity = Math.Clamp(cell.Humidity + rainfallBoost * 0.4f, 0, 1f);
                }
            }
        }
    }

    private void ManageRainfallMultiplier(float avgLandRainfall)
    {
        var controls = _map.PlanetaryControls;
        if (controls == null) return;

        float targetComfortRainfall = 0.35f; // Was 0.32f - higher target
        float floodThreshold = 0.65f; // Was 0.48f - allow more rain
        float oceanRainfall = CalculateAverageOceanRainfall();
        float landOceanGap = Math.Max(0f, oceanRainfall - avgLandRainfall);
        float droughtSeverity = Math.Clamp((targetComfortRainfall - avgLandRainfall) / 0.22f, 0f, 1f);
        float oceanBias = Math.Clamp(landOceanGap / 0.25f, 0f, 1f);
        float timeAcceleration = Math.Clamp((_currentTimeSpeed - 1f) / 16f, 0f, 1f);

        float responseScale = MathF.Sqrt(MathF.Max(1f, _responseMultiplier));

        float fastTimeBias = Math.Clamp((_currentTimeSpeed - 4f) / 28f, 0f, 1f);
        if (fastTimeBias > 0f && avgLandRainfall < floodThreshold)
        {
            float safetyFloor = Math.Clamp(0.9f + fastTimeBias * 0.9f, 0.25f, 3f);
            if (controls.RainfallMultiplier < safetyFloor)
            {
                controls.RainfallMultiplier = safetyFloor;
                LastAction = $"Holding rainfall multiplier at {controls.RainfallMultiplier:F2} for fast-time stability";
                AdjustmentsMade++;
            }
        }

        if (avgLandRainfall < targetComfortRainfall)
        {
            float urgency = droughtSeverity * 0.65f + oceanBias * 0.35f;
            float adjustment = (0.06f + 0.25f * urgency) * (1f + timeAcceleration); // Increased rates
            adjustment *= responseScale;
            controls.RainfallMultiplier = Math.Clamp(controls.RainfallMultiplier + adjustment, 0.25f, 3f);
            LastAction = $"Boosting rainfall multiplier to {controls.RainfallMultiplier:F2}";
            AdjustmentsMade++;
        }
        else if (avgLandRainfall > floodThreshold)
        {
            float floodSeverity = Math.Clamp((avgLandRainfall - floodThreshold) / 0.2f, 0f, 1f);
            float adjustment = (0.02f + 0.12f * floodSeverity) * responseScale;
            controls.RainfallMultiplier = Math.Clamp(controls.RainfallMultiplier - adjustment, 0.1f, 3f);
            LastAction = $"Trimming rainfall multiplier to {controls.RainfallMultiplier:F2}";
            AdjustmentsMade++;
        }
        else
        {
            float drift = (controls.RainfallMultiplier - 1f) * 0.04f * responseScale;
            if (Math.Abs(drift) > 0.005f)
            {
                controls.RainfallMultiplier = Math.Clamp(controls.RainfallMultiplier - drift, 0.1f, 3f);
            }
        }
    }

    private void ReinforceMoistureCorridors(float avgLandRainfall)
    {
        bool urgent = _emergencyLifeProtection || avgLandRainfall < 0.2f;
        if (!urgent && _adjustmentTimer < 1.0f) return;

        float droughtMultiplier = avgLandRainfall < 0.2f ? 1.5f : 1f;
        float rainBoost = 0.05f * _responseMultiplier * droughtMultiplier;
        float humidityBoost = 0.05f * _responseMultiplier * droughtMultiplier;

        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                var cell = _map.Cells[x, y];
                if (!cell.IsLand) continue;

                bool needsCorridor = cell.Rainfall < 0.2f && cell.LifeType != LifeForm.None;
                if (!needsCorridor) continue;

                cell.Rainfall = Math.Clamp(cell.Rainfall + rainBoost, 0f, 1f);
                cell.Humidity = Math.Clamp(cell.Humidity + humidityBoost, 0f, 1f);
                
                // Cool down dry hot spots
                if (cell.Temperature > 30f)
                {
                    cell.Temperature -= 1f * _responseMultiplier * droughtMultiplier;
                }
            }
        }
    }

    private void CombatRapidDesertification(float avgLandRainfall)
    {
        float severityScale = avgLandRainfall < 0.18f ? 1.5f : 1f;
        float rainfallBoost = 0.1f * _responseMultiplier * severityScale;
        float cooling = 2f * _responseMultiplier * severityScale;
        bool rescueBarren = avgLandRainfall < 0.15f;

        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                var cell = _map.Cells[x, y];
                if (!cell.IsLand) continue;

                bool isDesertifying = cell.Rainfall < 0.15f && cell.Temperature > 30f;
                if (!isDesertifying) continue;

                // If life exists, fight the desert
                if (cell.LifeType != LifeForm.None)
                {
                    cell.Rainfall = Math.Clamp(cell.Rainfall + rainfallBoost, 0.2f, 1f);
                    cell.Temperature -= cooling;
                }
                else if (rescueBarren && cell.Rainfall < 0.05f)
                {
                    cell.Rainfall = Math.Clamp(cell.Rainfall + rainfallBoost * 0.5f, 0.1f, 1f);
                    cell.Temperature -= cooling * 0.5f;
                }
            }
        }
    }

    private void ClampExtremeTemperatures()
    {
        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                var cell = _map.Cells[x, y];

                float maxAllowedTemp = 60f;
                float minAllowedTemp = -70f;

                if (cell.LifeType != LifeForm.None)
                {
                    maxAllowedTemp = 45f;
                    minAllowedTemp = -30f;
                    
                    if (cell.LifeType == LifeForm.Bacteria)
                    {
                        maxAllowedTemp = 80f;
                        minAllowedTemp = -50f;
                    }
                }

                if (cell.Temperature > maxAllowedTemp)
                {
                    cell.Temperature = maxAllowedTemp - 1f;
                    // Reduce greenhouse locally
                    cell.Methane *= 0.9f;
                    cell.CO2 *= 0.9f;
                }
                else if (cell.Temperature < minAllowedTemp)
                {
                    cell.Temperature = minAllowedTemp + 1f;
                }
            }
        }
    }

    private void ProtectAndNurtureLife()
    {
        int nurturedCells = 0;

        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                var cell = _map.Cells[x, y];

                if (cell.LifeType == LifeForm.None) continue;

                bool modified = false;
                
                // 1. CRITICAL BIOMASS SUPPORT
                // Prevent "one frame death" by ensuring biomass stays above 0.1
                // If emergency mode is on, boost it higher
                float minBiomass = _emergencyLifeProtection ? 0.5f : 0.2f;
                if (cell.Biomass < minBiomass)
                {
                    // Hard reset of biomass to ensure survival
                    cell.Biomass = minBiomass + 0.05f; 
                    modified = true;
                }

                // 2. FORCE TEMPERATURE TO OPTIMAL
                // Don't just nudge, clamp it to the sweet spot
                float idealTemp = 20f;
                float tempRange = 15f; // +/- 15 degrees is safe
                
                if (cell.LifeType == LifeForm.Bacteria)
                {
                    tempRange = 40f; // Bacteria tough
                }
                else if (cell.LifeType == LifeForm.PlantLife)
                {
                    idealTemp = 22f;
                    tempRange = 12f;
                }
                else if (cell.LifeType == LifeForm.Algae)
                {
                    idealTemp = 15f;
                    tempRange = 15f;
                }
                
                if (Math.Abs(cell.Temperature - idealTemp) > tempRange)
                {
                    // Force temperature into safe range immediately
                    if (cell.Temperature > idealTemp) cell.Temperature = idealTemp + tempRange - 1f;
                    else cell.Temperature = idealTemp - tempRange + 1f;
                    modified = true;
                }

                // 3. FORCE WATER/RAINFALL (Land Plants/Animals)
                if (cell.IsLand && cell.LifeType != LifeForm.Bacteria)
                {
                    if (cell.Rainfall < 0.2f)
                    {
                        cell.Rainfall = 0.3f; // Instant hydration
                        cell.Humidity = Math.Max(cell.Humidity, 0.4f);
                        modified = true;
                    }
                }

                // 4. FORCE OXYGEN (Aerobic Life)
                if (cell.LifeType >= LifeForm.SimpleAnimals && cell.LifeType != LifeForm.Civilization)
                {
                    if (cell.Oxygen < 15f)
                    {
                        cell.Oxygen = 18f; // Instant breathability
                        modified = true;
                    }
                }

                // 5. REDUCE TOXINS
                if (cell.LifeType != LifeForm.Bacteria && cell.CO2 > 10f)
                {
                    cell.CO2 = 5f; // Remove excess CO2 immediately
                    modified = true;
                }

                if (modified) nurturedCells++;
            }
        }

        if (nurturedCells > 0)
        {
            AdjustmentsMade++;
            if (nurturedCells > 100)
            {
                LastAction = $"Life Support: Stabilized {nurturedCells} cells";
            }
        }
        
        // Global Emergency Reseed Trigger
        // If life is totally gone but we are in emergency mode, put it back!
        if (_emergencyLifeProtection)
        {
            int lifeCount = 0;
            for (int x = 0; x < _map.Width; x++)
            {
                 for (int y = 0; y < _map.Height; y++)
                 {
                     if (_map.Cells[x,y].LifeType != LifeForm.None) lifeCount++;
                 }
            }
            
            if (lifeCount == 0)
            {
                LastAction = "EMERGENCY RESEEDING";
                // Pick random spots
                Random rng = new Random();
                for(int i=0; i<50; i++)
                {
                    int rx = rng.Next(_map.Width);
                    int ry = rng.Next(_map.Height);
                    var c = _map.Cells[rx,ry];
                    if (c.IsLand) 
                    {
                        c.LifeType = LifeForm.Bacteria;
                        c.Biomass = 0.5f;
                        c.Temperature = 20f;
                    }
                    else
                    {
                        c.LifeType = LifeForm.Algae;
                        c.Biomass = 0.5f;
                        c.Temperature = 15f;
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