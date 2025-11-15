namespace SimPlanet;

/// <summary>
/// Simulates tsunami wave propagation and coastal flooding
/// </summary>
public static class TsunamiSystem
{
    private static Random _random = new Random();

    /// <summary>
    /// Initialize a tsunami from an earthquake epicenter
    /// </summary>
    public static void InitiateTsunami(PlanetMap map, int epicenterX, int epicenterY, float magnitude, int currentYear)
    {
        var epicenter = map.Cells[epicenterX, epicenterY];

        // Calculate initial wave height based on earthquake magnitude
        // M7.0 = ~1m, M8.0 = ~5m, M9.0+ = ~20m+
        float waveHeight = MathF.Pow(10, magnitude - 7.0f);
        waveHeight = MathF.Min(30.0f, waveHeight); // Cap at 30m

        epicenter.Geology.TsunamiWaveHeight = waveHeight;
        epicenter.Geology.TsunamiSourceYear = currentYear;

        // Initial wave velocity (tsunamis travel at ~700-800 km/h in deep ocean)
        // We'll use simplified values for simulation
        epicenter.Geology.TsunamiVelocity = 0.8f + (float)_random.NextDouble() * 0.2f; // 0.8-1.0

        // Tsunami propagates in all directions initially
        epicenter.Geology.TsunamiDirection = (0, 0); // Omnidirectional from source
    }

    /// <summary>
    /// Update tsunami wave propagation
    /// </summary>
    public static void Update(PlanetMap map, float deltaTime, int currentYear)
    {
        // Create a list of cells with tsunami waves to propagate
        var waveCells = new List<(int x, int y, float height, float velocity)>();

        // First pass: Identify active tsunami cells
        for (int x = 0; x < map.Width; x++)
        {
            for (int y = 0; y < map.Height; y++)
            {
                var cell = map.Cells[x, y];

                if (cell.Geology.TsunamiWaveHeight > 0.1f)
                {
                    waveCells.Add((x, y, cell.Geology.TsunamiWaveHeight, cell.Geology.TsunamiVelocity));
                }
            }
        }

        // Second pass: Propagate waves
        foreach (var (x, y, height, velocity) in waveCells)
        {
            PropagateWave(map, x, y, height, velocity, deltaTime);
        }

        // Third pass: Apply coastal damage and decay waves
        for (int x = 0; x < map.Width; x++)
        {
            for (int y = 0; y < map.Height; y++)
            {
                var cell = map.Cells[x, y];

                if (cell.Geology.TsunamiWaveHeight > 0.1f)
                {
                    // Apply damage to coastal areas
                    if (cell.IsLand)
                    {
                        ApplyCoastalDamage(cell, map, x, y);

                        // Waves decay rapidly on land
                        cell.Geology.TsunamiWaveHeight *= 0.5f; // 50% decay per update on land
                    }
                    else
                    {
                        // Waves decay slowly in water
                        cell.Geology.TsunamiWaveHeight *= 0.98f; // 2% decay per update in water
                    }

                    // Clear very small waves
                    if (cell.Geology.TsunamiWaveHeight < 0.1f)
                    {
                        cell.Geology.TsunamiWaveHeight = 0;
                        cell.Geology.TsunamiVelocity = 0;
                        cell.Geology.TsunamiDirection = (0, 0);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Propagate tsunami wave to neighboring cells
    /// </summary>
    private static void PropagateWave(PlanetMap map, int x, int y, float height, float velocity, float deltaTime)
    {
        var sourceCell = map.Cells[x, y];

        // Tsunami propagates to all 8 neighbors
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue;

                int nx = (x + dx + map.Width) % map.Width; // Wrap horizontally
                int ny = y + dy;

                if (ny < 0 || ny >= map.Height) continue;

                var neighbor = map.Cells[nx, ny];

                // Calculate wave transfer based on velocity and time
                float transferAmount = height * velocity * deltaTime * 0.5f;

                // Diagonal neighbors get slightly less energy
                if (dx != 0 && dy != 0)
                {
                    transferAmount *= 0.7f;
                }

                // Transfer wave energy to neighbor (but don't exceed source height)
                float newHeight = neighbor.Geology.TsunamiWaveHeight + transferAmount;

                // Wave height amplifies in shallow water (coastal areas)
                if (neighbor.IsWater && neighbor.Elevation > -0.2f) // Shallow water
                {
                    newHeight *= 1.3f; // 30% amplification
                }

                // Massive amplification when hitting coast
                if (neighbor.IsLand && sourceCell.IsWater)
                {
                    newHeight *= 2.0f; // Wave doubles when hitting shore
                }

                neighbor.Geology.TsunamiWaveHeight = MathF.Max(neighbor.Geology.TsunamiWaveHeight, newHeight);
                neighbor.Geology.TsunamiVelocity = velocity;
                neighbor.Geology.TsunamiDirection = (dx, dy);
            }
        }
    }

    /// <summary>
    /// Apply damage from tsunami to coastal areas
    /// </summary>
    private static void ApplyCoastalDamage(TerrainCell cell, PlanetMap map, int x, int y)
    {
        float waveHeight = cell.Geology.TsunamiWaveHeight;

        // Damage scales with wave height
        // 1m wave = minor damage, 5m wave = moderate, 10m+ = catastrophic
        float damageFactor = MathF.Min(1.0f, waveHeight / 10.0f);

        // Destroy biomass (vegetation washed away)
        cell.Biomass *= (1.0f - damageFactor * 0.7f);

        // Flooding: Add temporary water to low-lying areas
        if (cell.Elevation < 0.2f) // Low-lying coastal land
        {
            cell.Geology.FloodLevel = MathF.Max(cell.Geology.FloodLevel, waveHeight * 0.5f);
        }

        // Increase sediment from erosion
        cell.Geology.SedimentLayer += waveHeight * 0.1f;

        // TODO: Damage cities and infrastructure when integrated
        // High wave height would destroy buildings, kill population
    }

    /// <summary>
    /// Drain flood waters over time
    /// </summary>
    public static void DrainFloodWaters(PlanetMap map, float deltaTime)
    {
        for (int x = 0; x < map.Width; x++)
        {
            for (int y = 0; y < map.Height; y++)
            {
                var cell = map.Cells[x, y];

                if (cell.Geology.FloodLevel > 0)
                {
                    // Flood water drains over time (days to weeks)
                    cell.Geology.FloodLevel = MathF.Max(0, cell.Geology.FloodLevel - deltaTime * 0.1f);

                    // Water flows to lower neighbors
                    if (cell.Geology.FloodLevel > 0.5f)
                    {
                        // Find lowest neighbor
                        int lowestX = x, lowestY = y;
                        float lowestElevation = cell.Elevation;

                        for (int dx = -1; dx <= 1; dx++)
                        {
                            for (int dy = -1; dy <= 1; dy++)
                            {
                                if (dx == 0 && dy == 0) continue;

                                int nx = (x + dx + map.Width) % map.Width;
                                int ny = y + dy;

                                if (ny < 0 || ny >= map.Height) continue;

                                var neighbor = map.Cells[nx, ny];
                                if (neighbor.Elevation < lowestElevation)
                                {
                                    lowestElevation = neighbor.Elevation;
                                    lowestX = nx;
                                    lowestY = ny;
                                }
                            }
                        }

                        // Transfer water to lowest neighbor
                        if (lowestX != x || lowestY != y)
                        {
                            float transferAmount = cell.Geology.FloodLevel * 0.3f * deltaTime;
                            cell.Geology.FloodLevel -= transferAmount;
                            map.Cells[lowestX, lowestY].Geology.FloodLevel += transferAmount;
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Check if a cell is in a tsunami risk zone (coastal area near subduction zones)
    /// </summary>
    public static bool IsInTsunamiRiskZone(TerrainCell cell, PlanetMap map, int x, int y)
    {
        // Coastal cells near convergent boundaries are at risk
        if (!cell.IsLand) return false;

        // Check if near ocean
        bool nearOcean = false;
        for (int dx = -2; dx <= 2; dx++)
        {
            for (int dy = -2; dy <= 2; dy++)
            {
                int nx = (x + dx + map.Width) % map.Width;
                int ny = y + dy;

                if (ny < 0 || ny >= map.Height) continue;

                if (map.Cells[nx, ny].IsWater)
                {
                    nearOcean = true;
                    // Check if that ocean cell is near a subduction zone
                    if (map.Cells[nx, ny].Geology.BoundaryType == PlateBoundaryType.Convergent)
                    {
                        return true; // High risk
                    }
                }
            }
        }

        return nearOcean; // Moderate risk if just near ocean
    }
}
