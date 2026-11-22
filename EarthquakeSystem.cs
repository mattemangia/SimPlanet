using System.Collections.Concurrent;

namespace SimPlanet;

/// <summary>
/// Simulates earthquakes, fault lines, and seismic activity
/// </summary>
public static class EarthquakeSystem
{
    private static Random _random = new Random();

    // Queue for UI to read recent earthquakes
    public static ConcurrentQueue<EarthquakeEvent> RecentSystemEarthquakes = new();

    /// <summary>Running count of earthquakes since start (statistics and tests).</summary>
    public static int TotalEarthquakes;

    /// <summary>
    /// Loads faults and plate boundaries with elastic strain and releases it as ruptures.
    /// Rates are per unit of simulated time, so the seismicity does not depend on frame rate.
    /// </summary>
    public static void Update(PlanetMap map, float deltaTime, int currentYear, out bool tsunamiTriggered, out (int x, int y) tsunamiEpicenter, out float tsunamiMagnitude)
    {
        tsunamiTriggered = false;
        tsunamiEpicenter = (0, 0);
        tsunamiMagnitude = 0f;
        if (float.IsNaN(deltaTime) || deltaTime <= 0) return;

        float shakeDecay = deltaTime * 0.5f;

        for (int x = 0; x < map.Width; x++)
        {
            for (int y = 0; y < map.Height; y++)
            {
                var cell = map.Cells[x, y];
                var geo = cell.Geology;
                AccumulateSeismicStress(cell, deltaTime);

                if (geo.EarthquakeIntensity > 0)
                {
                    geo.EarthquakeIntensity = MathF.Max(0, geo.EarthquakeIntensity - shakeDecay);
                }
                if (geo.IsEpicenter && currentYear > geo.LastEarthquakeYear + 1)
                {
                    geo.EarthquakeMagnitude = 0;
                    geo.IsEpicenter = false;
                }

                // Elastic rebound: the further past its strength a fault is loaded, the sooner it fails
                bool weak = geo.IsFault || geo.BoundaryType != PlateBoundaryType.None;
                float threshold = weak ? 0.6f : 1.0f;
                if (geo.SeismicStress <= threshold) continue;

                float hazard = 0.05f * (geo.SeismicStress - threshold) / threshold;
                if (_random.NextDouble() >= hazard * deltaTime) continue;

                float magnitude = Rupture(map, x, y, currentYear, 0f);

                // Great subduction earthquakes under or beside the sea raise tsunamis
                bool nearWater = cell.IsWater || IsAdjacentToWater(map, x, y);
                if (magnitude >= 7.0f && nearWater && geo.BoundaryType == PlateBoundaryType.Convergent)
                {
                    tsunamiTriggered = true;
                    tsunamiEpicenter = (x, y);
                    tsunamiMagnitude = magnitude;
                }

                // A large intraplate shock breaks fresh rock and leaves a new fault
                if (magnitude >= 6.5f && !geo.IsFault)
                {
                    CreateFaultLine(map, x, y, currentYear);
                }
            }
        }
    }

    /// <summary>
    /// Strain builds where plates grind (tectonic stress) and on active faults.
    /// </summary>
    private static void AccumulateSeismicStress(TerrainCell cell, float deltaTime)
    {
        var geo = cell.Geology;

        float rate = 0.00002f;                               // Far-field intraplate loading
        rate += 0.0012f * MathF.Min(geo.TectonicStress, 2f); // Plate boundary loading
        if (geo.IsFault) rate += 0.0005f * geo.FaultActivity;
        if (geo.InducedSeismicity) rate += 0.0008f;          // Fluid injection and extraction

        geo.SeismicStress = MathF.Min(1.5f, geo.SeismicStress + rate * deltaTime);
    }

    private static readonly List<(int x, int y)> _ruptureCells = new();
    private static readonly Queue<(int x, int y)> _ruptureFrontier = new();
    private static readonly HashSet<(int x, int y)> _ruptureSeen = new();

    /// <summary>
    /// Breaks a fault starting at the hypocentre. The rupture runs along connected stressed
    /// fault cells; its length and stress drop set the magnitude, so long faults give great
    /// earthquakes and most events stay small (Gutenberg-Richter).
    /// Pass a positive magnitude to force an event of that size.
    /// </summary>
    public static float Rupture(PlanetMap map, int startX, int startY, int currentYear, float forcedMagnitude)
    {
        var start = map.Cells[startX, startY].Geology;
        int wanted = forcedMagnitude > 0
            ? Math.Clamp((int)MathF.Pow(10f, (forcedMagnitude - 4.5f) / 2f), 1, 80)
            : 80;

        _ruptureCells.Clear();
        _ruptureFrontier.Clear();
        _ruptureSeen.Clear();
        _ruptureFrontier.Enqueue((startX, startY));
        _ruptureSeen.Add((startX, startY));
        float stressSum = 0f;

        while (_ruptureFrontier.Count > 0 && _ruptureCells.Count < wanted)
        {
            var (cx, cy) = _ruptureFrontier.Dequeue();
            var geo = map.Cells[cx, cy].Geology;
            _ruptureCells.Add((cx, cy));
            stressSum += geo.SeismicStress;

            for (int dy = -1; dy <= 1; dy++)
            {
                int ny = cy + dy;
                if (ny < 0 || ny >= map.Height) continue;
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    int nx = (cx + dx + map.Width) % map.Width;
                    if (_ruptureSeen.Contains((nx, ny))) continue;

                    var next = map.Cells[nx, ny].Geology;
                    if (!next.IsFault && next.BoundaryType == PlateBoundaryType.None) continue;

                    // The front only keeps going through fault that is itself close to failure
                    float pass = forcedMagnitude > 0 ? 0.9f : Math.Clamp((next.SeismicStress - 0.3f) * 1.4f, 0f, 0.88f);
                    if (_random.NextDouble() >= pass) continue;

                    _ruptureSeen.Add((nx, ny));
                    _ruptureFrontier.Enqueue((nx, ny));
                }
            }
        }

        int length = _ruptureCells.Count;
        float meanStress = stressSum / length;
        float magnitude = forcedMagnitude > 0
            ? forcedMagnitude
            : 2.6f + 1.4f * meanStress + 2.0f * MathF.Log10(length) + (float)(_random.NextDouble() - 0.5);
        magnitude = Math.Clamp(magnitude, 2.0f, 9.5f);

        FaultType faultType = start.FaultType != FaultType.None ? start.FaultType : start.BoundaryType switch
        {
            PlateBoundaryType.Convergent => FaultType.Thrust,
            PlateBoundaryType.Divergent => FaultType.Normal,
            PlateBoundaryType.Transform => FaultType.Strike_Slip,
            _ => FaultType.Oblique
        };

        // Coseismic slip: thrusts lift the ground, normal faults drop it, strike-slip moves sideways
        float slip = Math.Clamp(0.004f * MathF.Pow(10f, (magnitude - 6f) / 2f), 0f, 0.03f);
        float vertical = faultType switch
        {
            FaultType.Thrust or FaultType.Reverse => slip,
            FaultType.Normal => -slip,
            FaultType.Oblique => slip * 0.4f,
            _ => 0f
        };

        foreach (var (rx, ry) in _ruptureCells)
        {
            var cell = map.Cells[rx, ry];
            var geo = cell.Geology;
            geo.SeismicStress *= 0.1f;                                 // Stress drop
            geo.FaultActivity = MathF.Min(1f, geo.FaultActivity + 0.1f);
            geo.LastEarthquakeYear = currentYear;
            if (vertical != 0f && !geo.IsSubducting)
            {
                cell.Elevation = Math.Clamp(cell.Elevation + vertical, -1.2f, 1.25f);
            }
        }

        // Stress shed by the rupture loads the fault beyond its tips: aftershocks and sequences
        foreach (var (rx, ry) in _ruptureCells)
        {
            for (int dy = -2; dy <= 2; dy++)
            {
                int ny = ry + dy;
                if (ny < 0 || ny >= map.Height) continue;
                for (int dx = -2; dx <= 2; dx++)
                {
                    int nx = (rx + dx + map.Width) % map.Width;
                    if (_ruptureSeen.Contains((nx, ny))) continue;
                    var near = map.Cells[nx, ny].Geology;
                    if (near.IsFault || near.BoundaryType != PlateBoundaryType.None)
                    {
                        near.SeismicStress = MathF.Min(1.5f, near.SeismicStress + 0.01f * magnitude / 8f);
                    }
                }
            }
        }

        TriggerEarthquake(map, startX, startY, magnitude, currentYear);

        GeoEvents.Publish(new GeoEvent
        {
            Kind = GeoEventKind.Earthquake,
            X = startX,
            Y = startY,
            Magnitude = magnitude,
            Year = currentYear,
            Fault = faultType,
            Rupture = _ruptureCells.ToArray(),
            Radius = magnitude * 5f
        });

        return magnitude;
    }

    /// <summary>
    /// Trigger an earthquake at the epicenter and propagate to surrounding cells
    /// </summary>
    public static void TriggerEarthquake(PlanetMap map, int epicenterX, int epicenterY, float magnitude, int currentYear)
    {
        TotalEarthquakes++;

        // Add to recent queue for UI
        RecentSystemEarthquakes.Enqueue(new EarthquakeEvent
        {
            X = epicenterX,
            Y = epicenterY,
            Magnitude = magnitude,
            Year = currentYear
        });

        // Limit queue size
        while (RecentSystemEarthquakes.Count > 50)
        {
            RecentSystemEarthquakes.TryDequeue(out _);
        }

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

                    // Faults start at different points of their seismic cycle
                    cell.Geology.SeismicStress = (float)_random.NextDouble() * 0.65f;
                }

                // Small chance of intraplate faults (within plates, away from boundaries)
                else
                {
                    bool isContinentalInterior = cell.IsLand && cell.Geology.CrustType != CrustType.Oceanic;

                    if (isContinentalInterior)
                    {
                        // Only draw major continental faults: favor high terrain/stress belts.
                        float baseChance = 0.005f; // Much rarer by default
                        float reliefBoost = MathF.Max(0f, cell.Elevation - 0.35f) * 0.05f;
                        float stressBoost = MathF.Max(0f, cell.Geology.TectonicStress - 0.6f) * 0.03f;

                        if (_random.NextDouble() < baseChance + reliefBoost + stressBoost)
                        {
                            cell.Geology.IsFault = true;
                            cell.Geology.FaultType = (FaultType)_random.Next(1, 6);
                            cell.Geology.FaultActivity = 0.6f + (float)_random.NextDouble() * 0.4f; // Major, visible faults only
                        }
                    }
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

    /// <summary>
    /// Check if the given coordinate borders ocean water. Used to allow coastal subduction
    /// earthquakes to trigger tsunamis even if the epicenter is on land.
    /// </summary>
    private static bool IsAdjacentToWater(PlanetMap map, int x, int y)
    {
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue;

                int nx = (x + dx + map.Width) % map.Width;
                int ny = y + dy;
                if (ny < 0 || ny >= map.Height) continue;

                if (map.Cells[nx, ny].IsWater)
                {
                    return true;
                }
            }
        }

        return false;
    }
}
