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
    private const float AdjustmentInterval = 2.0f; // Adjust every 2 seconds

    // Stabilization targets (Earth-like conditions)
    private const float TargetGlobalTemp = 15.0f; // 15°C average
    private const float TargetOxygen = 21.0f; // 21% oxygen
    private const float TargetCO2 = 0.04f; // 0.04% CO2 (400 ppm)
    private const float TargetLandRatio = 0.29f; // 29% land
    private const float TargetMagneticField = 1.0f; // Earth-like
    private const float TargetCoreTemp = 5000f; // Active core

    public bool IsActive { get; set; } = false;

    // Statistics
    public int AdjustmentsMade { get; private set; } = 0;
    public string LastAction { get; private set; } = "Inactive";

    public PlanetStabilizer(PlanetMap map, MagnetosphereSimulator magnetosphere)
    {
        _map = map;
        _magnetosphere = magnetosphere;
    }

    public void Update(float deltaTime)
    {
        if (!IsActive) return;

        _adjustmentTimer += deltaTime;

        if (_adjustmentTimer >= AdjustmentInterval)
        {
            PerformStabilization();
            _adjustmentTimer = 0f;
        }
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

        // Priority 5: Prevent runaway ice ages or greenhouse effects
        PreventExtremeFeedbacks();
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

        // Too cold - increase greenhouse gases slightly
        if (tempDeviation < -5f)
        {
            // Add CO2 to warm planet (but not too much)
            if (avgCO2 < 0.1f)
            {
                AdjustCO2Globally(0.01f);
                LastAction = $"Adding CO2 to warm planet (avg: {avgTemp:F1}°C)";
                AdjustmentsMade++;
            }
        }
        // Too hot - reduce greenhouse gases
        else if (tempDeviation > 5f)
        {
            // Remove excess CO2
            if (avgCO2 > TargetCO2)
            {
                AdjustCO2Globally(-0.02f);
                LastAction = $"Removing CO2 to cool planet (avg: {avgTemp:F1}°C)";
                AdjustmentsMade++;
            }
        }

        // Adjust solar energy if extreme
        if (avgTemp < -10f && _map.SolarEnergy < 1.2f)
        {
            _map.SolarEnergy += 0.01f;
            LastAction = "Increasing solar energy";
            AdjustmentsMade++;
        }
        else if (avgTemp > 40f && _map.SolarEnergy > 0.8f)
        {
            _map.SolarEnergy -= 0.01f;
            LastAction = "Decreasing solar energy";
            AdjustmentsMade++;
        }
    }

    private void StabilizeAtmosphere(float avgOxygen, float avgCO2)
    {
        // Maintain oxygen at breathable levels
        if (avgOxygen < 18f)
        {
            // Boost photosynthesis by enhancing plant growth
            BoostPlantGrowth();
            LastAction = $"Boosting O2 production (current: {avgOxygen:F1}%)";
            AdjustmentsMade++;
        }
        else if (avgOxygen > 25f)
        {
            // Too much oxygen is dangerous (fire risk)
            ReduceOxygenGlobally(0.5f);
            LastAction = $"Reducing excess O2 (current: {avgOxygen:F1}%)";
            AdjustmentsMade++;
        }

        // Keep CO2 in safe range
        if (avgCO2 > 1.0f)
        {
            // Dangerous CO2 levels - remove excess
            AdjustCO2Globally(-0.1f);
            LastAction = $"Removing excess CO2 (current: {avgCO2:F2}%)";
            AdjustmentsMade++;
        }
        else if (avgCO2 < 0.01f)
        {
            // Too little CO2 - plants need it
            AdjustCO2Globally(0.005f);
            LastAction = $"Adding CO2 for plants (current: {avgCO2:F3}%)";
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
        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                var cell = _map.Cells[x, y];
                cell.CO2 = Math.Max(0, cell.CO2 + amount);

                // Update greenhouse effect
                cell.Greenhouse = cell.CO2 * 10f + (cell.Temperature - 15f) * 0.01f;
            }
        }
    }

    private void ReduceOxygenGlobally(float amount)
    {
        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                _map.Cells[x, y].Oxygen = Math.Max(0, _map.Cells[x, y].Oxygen - amount);
            }
        }
    }

    private void BoostPlantGrowth()
    {
        // Enhance plant life to produce more oxygen
        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                var cell = _map.Cells[x, y];
                if (cell.LifeType == LifeForm.PlantLife ||
                    cell.LifeType == LifeForm.Algae)
                {
                    cell.Biomass = Math.Min(cell.Biomass + 0.05f, 1.0f);
                    cell.Oxygen += 0.1f;
                }
            }
        }
    }

    private void RaiseLandMasses()
    {
        // Raise the lowest land areas
        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                var cell = _map.Cells[x, y];
                if (cell.IsWater && cell.Elevation > -0.3f)
                {
                    cell.Elevation += 0.05f;
                }
            }
        }
    }

    private void AddWaterToLowlands()
    {
        // Lower some land areas to create seas
        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                var cell = _map.Cells[x, y];
                if (cell.IsLand && cell.Elevation < 0.2f)
                {
                    cell.Elevation -= 0.05f;
                }
            }
        }
    }

    private void EnsureRainfallDistribution()
    {
        // Boost rainfall in dry areas with plant life
        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                var cell = _map.Cells[x, y];
                if (cell.IsLand && cell.Rainfall < 0.2f && cell.LifeType != LifeForm.None)
                {
                    cell.Rainfall += 0.05f;
                    cell.Humidity += 0.05f;
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
