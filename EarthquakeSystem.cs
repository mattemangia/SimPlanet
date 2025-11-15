namespace SimPlanet;

/// <summary>
/// Simulates earthquakes, fault lines, and seismic activity
/// </summary>
public static class EarthquakeSystem
{
    private static Random _random = new Random();

    /// <summary>
    /// Update seismic stress and trigger earthquakes
    /// </summary>
    public static void Update(PlanetMap map, float deltaTime, int currentYear, out bool tsunamiTriggered, out (int x, int y) tsunamiEpicenter, out float tsunamiMagnitude)
    {
        tsunamiTriggered = false;
        tsunamiEpicenter = (0, 0);
        tsunamiMagnitude = 0f;

        // First pass: Build up seismic stress
        for (int x = 0; x < map.Width; x++)
        {
            for (int y = 0; y < map.Height; y++)
            {
                var cell = map.Cells[x, y];
                AccumulateSeismicStress(cell, deltaTime);

                // Decay earthquake intensity from previous earthquakes
                if (cell.Geology.EarthquakeIntensity > 0)
                {
                    cell.Geology.EarthquakeIntensity = MathF.Max(0, cell.Geology.EarthquakeIntensity - deltaTime * 10f);
                }
                if (cell.Geology.EarthquakeMagnitude > 0 && cell.Geology.IsEpicenter)
                {
                    // Earthquake epicenter lasts for a year
                    if (currentYear > cell.Geology.LastEarthquakeYear + 1)
                    {
                        cell.Geology.EarthquakeMagnitude = 0;
                        cell.Geology.IsEpicenter = false;
                    }
                }
            }
        }

        // Second pass: Trigger earthquakes where stress exceeds threshold
        for (int x = 0; x < map.Width; x++)
        {
            for (int y = 0; y < map.Height; y++)
            {
                var cell = map.Cells[x, y];

                // Check if stress threshold exceeded
                // Lower thresholds for more frequent small earthquakes
                float threshold = cell.Geology.IsFault ? 0.3f : 0.6f; // Faults trigger much easier

                // Higher stress = higher probability
                double triggerChance = cell.Geology.IsFault ? 0.05 : 0.02; // 5% for faults, 2% otherwise

                // Plate boundaries get extra activity
                if (cell.Geology.BoundaryType != PlateBoundaryType.None)
                {
                    triggerChance *= 3.0; // 3x more earthquakes at plate boundaries
                }

                if (cell.Geology.SeismicStress > threshold && _random.NextDouble() < triggerChance)
                {
                    // Trigger earthquake!
                    float magnitude = CalculateMagnitude(cell.Geology.SeismicStress);
                    TriggerEarthquake(map, x, y, magnitude, currentYear);

                    // Reset stress after release
                    cell.Geology.SeismicStress *= 0.1f; // Keep 10% residual stress

                    // Large ocean earthquakes at subduction zones can trigger tsunamis
                    if (magnitude >= 7.0f && cell.IsWater && cell.Geology.BoundaryType == PlateBoundaryType.Convergent)
                    {
                        tsunamiTriggered = true;
                        tsunamiEpicenter = (x, y);
                        tsunamiMagnitude = magnitude;
                    }

                    // Large earthquakes (M > 6.5) can create new faults (but only once per location every 100 years)
                    if (magnitude >= 6.5f && !cell.Geology.IsFault)
                    {
                        // Only create fault if one doesn't already exist here
                        CreateFaultLine(map, x, y, currentYear);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Accumulate seismic stress based on tectonic activity and faults
    /// </summary>
    private static void AccumulateSeismicStress(TerrainCell cell, float deltaTime)
    {
        float stressRate = 0f;

        // Stress builds up at plate boundaries
        switch (cell.Geology.BoundaryType)
        {
            case PlateBoundaryType.Convergent:
                stressRate = 0.05f * deltaTime; // Highest stress (collision zones)
                break;
            case PlateBoundaryType.Transform:
                stressRate = 0.04f * deltaTime; // High stress (strike-slip)
                break;
            case PlateBoundaryType.Divergent:
                stressRate = 0.02f * deltaTime; // Moderate stress (rifting)
                break;
        }

        // Extra stress on active faults
        if (cell.Geology.IsFault)
        {
            stressRate += 0.03f * cell.Geology.FaultActivity * deltaTime;
        }

        // Induced seismicity from civilization (fracking, geothermal, oil extraction)
        if (cell.Geology.InducedSeismicity)
        {
            stressRate += 0.02f * deltaTime;
        }

        cell.Geology.SeismicStress = MathF.Min(1.5f, cell.Geology.SeismicStress + stressRate);
    }

    /// <summary>
    /// Calculate earthquake magnitude from stress level (Richter scale)
    /// Follows Gutenberg-Richter law: many small earthquakes, few large ones
    /// </summary>
    private static float CalculateMagnitude(float stress)
    {
        // Use exponential distribution for realistic earthquake magnitudes
        // Most earthquakes should be small (M 2-4), very few large (M 7+)

        // Random value determines magnitude category
        double roll = _random.NextDouble();

        float magnitude;
        if (roll < 0.70) // 70% are minor (M 2.0-4.0)
        {
            magnitude = 2.0f + (float)(_random.NextDouble() * 2.0);
        }
        else if (roll < 0.90) // 20% are light-moderate (M 4.0-5.5)
        {
            magnitude = 4.0f + (float)(_random.NextDouble() * 1.5);
        }
        else if (roll < 0.97) // 7% are moderate-strong (M 5.5-6.5)
        {
            magnitude = 5.5f + (float)(_random.NextDouble() * 1.0);
        }
        else if (roll < 0.995) // 2.5% are major (M 6.5-7.5)
        {
            magnitude = 6.5f + (float)(_random.NextDouble() * 1.0);
        }
        else // 0.5% are great earthquakes (M 7.5-9.0)
        {
            magnitude = 7.5f + (float)(_random.NextDouble() * 1.5);
        }

        // Stress level modifies magnitude (higher stress = potential for larger quake)
        magnitude += stress * 0.5f; // Stress can add up to +0.75 to magnitude

        return MathF.Max(2.0f, MathF.Min(9.5f, magnitude));
    }

    /// <summary>
    /// Trigger an earthquake at the epicenter and propagate to surrounding cells
    /// </summary>
    public static void TriggerEarthquake(PlanetMap map, int epicenterX, int epicenterY, float magnitude, int currentYear)
    {
        var epicenter = map.Cells[epicenterX, epicenterY];
        epicenter.Geology.EarthquakeMagnitude = magnitude;
        epicenter.Geology.IsEpicenter = true;
        epicenter.Geology.LastEarthquakeYear = currentYear;
        epicenter.Geology.EarthquakeIntensity = 1.0f;

        // Propagate earthquake intensity to surrounding cells (seismic waves)
        int radius = (int)(magnitude * 5); // Larger earthquakes affect wider area

        for (int dx = -radius; dx <= radius; dx++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                if (dx == 0 && dy == 0) continue;

                int nx = (epicenterX + dx + map.Width) % map.Width; // Wrap horizontally
                int ny = epicenterY + dy;

                if (ny < 0 || ny >= map.Height) continue;

                float distance = MathF.Sqrt(dx * dx + dy * dy);
                if (distance > radius) continue;

                var cell = map.Cells[nx, ny];

                // Intensity decreases with distance (inverse square law approximation)
                float intensity = MathF.Max(0, 1.0f - (distance / radius));
                intensity = MathF.Pow(intensity, 1.5f); // Non-linear falloff

                cell.Geology.EarthquakeIntensity = MathF.Max(cell.Geology.EarthquakeIntensity, intensity);

                // Damage to civilization and biomass based on intensity
                // Only major earthquakes (M 6.0+) cause significant damage
                if (intensity > 0.5f && magnitude >= 6.0f)
                {
                    // Reduce biomass (landslides, destruction) - scaled by magnitude
                    float damagePercent = (magnitude - 5.0f) * 0.05f * intensity; // M6=5%, M7=10%, M8=15%, M9=20%
                    cell.Biomass *= (1.0f - damagePercent);

                    // TODO: Damage cities when civilization system is integrated
                }
            }
        }
    }

    /// <summary>
    /// Create a new fault line from a large earthquake
    /// </summary>
    private static void CreateFaultLine(PlanetMap map, int startX, int startY, int currentYear)
    {
        var startCell = map.Cells[startX, startY];

        // Determine fault type based on boundary type
        FaultType faultType = FaultType.Strike_Slip;
        switch (startCell.Geology.BoundaryType)
        {
            case PlateBoundaryType.Convergent:
                faultType = _random.NextDouble() < 0.5 ? FaultType.Reverse : FaultType.Thrust;
                break;
            case PlateBoundaryType.Divergent:
                faultType = FaultType.Normal;
                break;
            case PlateBoundaryType.Transform:
                faultType = FaultType.Strike_Slip;
                break;
            default:
                faultType = (FaultType)_random.Next(1, 6); // Random fault type
                break;
        }

        // Create fault line extending from epicenter
        int faultLength = 3 + _random.Next(7); // 3-10 cells (smaller, more realistic)
        float directionX = (float)(_random.NextDouble() * 2 - 1);
        float directionY = (float)(_random.NextDouble() * 2 - 1);
        float magnitude = MathF.Sqrt(directionX * directionX + directionY * directionY);
        directionX /= magnitude;
        directionY /= magnitude;

        for (int i = 0; i < faultLength; i++)
        {
            int x = (int)(startX + directionX * i);
            int y = (int)(startY + directionY * i);

            x = (x + map.Width) % map.Width; // Wrap horizontally
            if (y < 0 || y >= map.Height) break;

            var cell = map.Cells[x, y];
            cell.Geology.IsFault = true;
            cell.Geology.FaultType = faultType;
            cell.Geology.FaultActivity = 0.5f + (float)_random.NextDouble() * 0.5f; // 0.5-1.0 activity
        }
    }

    /// <summary>
    /// Generate initial faults during world generation at plate boundaries
    /// </summary>
    public static void GenerateInitialFaults(PlanetMap map)
    {
        for (int x = 0; x < map.Width; x++)
        {
            for (int y = 0; y < map.Height; y++)
            {
                var cell = map.Cells[x, y];

                // Create faults at plate boundaries (especially transform and convergent)
                if (cell.Geology.BoundaryType != PlateBoundaryType.None)
                {
                    float faultProbability = 0f;

                    switch (cell.Geology.BoundaryType)
                    {
                        case PlateBoundaryType.Transform:
                            faultProbability = 0.8f; // Very high (San Andreas type)
                            break;
                        case PlateBoundaryType.Convergent:
                            faultProbability = 0.6f; // High (subduction zones)
                            break;
                        case PlateBoundaryType.Divergent:
                            faultProbability = 0.3f; // Moderate (rifts)
                            break;
                    }

                    if (_random.NextDouble() < faultProbability)
                    {
                        cell.Geology.IsFault = true;

                        // Assign fault type based on boundary
                        cell.Geology.FaultType = cell.Geology.BoundaryType switch
                        {
                            PlateBoundaryType.Transform => FaultType.Strike_Slip,
                            PlateBoundaryType.Convergent => _random.NextDouble() < 0.7 ? FaultType.Thrust : FaultType.Reverse,
                            PlateBoundaryType.Divergent => FaultType.Normal,
                            _ => FaultType.Oblique
                        };

                        cell.Geology.FaultActivity = 0.3f + (float)_random.NextDouble() * 0.7f;
                    }
                }

                // Small chance of intraplate faults (within plates, away from boundaries)
                else if (_random.NextDouble() < 0.01)
                {
                    cell.Geology.IsFault = true;
                    cell.Geology.FaultType = (FaultType)_random.Next(1, 6);
                    cell.Geology.FaultActivity = 0.1f + (float)_random.NextDouble() * 0.3f; // Lower activity
                }
            }
        }
    }

    /// <summary>
    /// Check for civilization-induced seismicity (fracking, geothermal, oil extraction)
    /// </summary>
    public static void CheckInducedSeismicity(PlanetMap map, int x, int y, bool hasOilExtraction, bool hasFracking, bool hasGeothermal)
    {
        var cell = map.Cells[x, y];

        // Induce seismicity if extracting resources or using geothermal
        if (hasOilExtraction || hasFracking || hasGeothermal)
        {
            cell.Geology.InducedSeismicity = true;

            // Small chance of immediate induced earthquake
            if (_random.NextDouble() < 0.001) // 0.1% chance
            {
                float magnitude = 2.0f + (float)(_random.NextDouble() * 3.0); // M 2.0-5.0 (smaller than natural)
                TriggerEarthquake(map, x, y, magnitude, 0);
            }
        }
        else
        {
            // Gradually reduce induced seismicity flag when activities stop
            if (cell.Geology.InducedSeismicity && _random.NextDouble() < 0.1)
            {
                cell.Geology.InducedSeismicity = false;
            }
        }
    }
}
