using System;
using System.Collections.Generic;
using System.Linq;

namespace SimPlanet;

/// <summary>
/// Manages natural and man-made disasters
/// </summary>
public class DisasterManager
{
    private readonly PlanetMap _map;
    private readonly Random _random;
    private readonly GeologicalSimulator _geoSimulator;

    // Disaster settings
    public bool RandomDisastersEnabled { get; set; } = true;
    public bool AsteroidsEnabled { get; set; } = true;
    public bool EarthquakesEnabled { get; set; } = true;
    public bool NuclearAccidentsEnabled { get; set; } = true;
    public bool AcidRainEnabled { get; set; } = true;
    public bool TornadoesEnabled { get; set; } = true;
    public bool HeavyRainsEnabled { get; set; } = true;

    // Recent disasters for UI
    public List<DisasterEvent> RecentDisasters { get; } = new();

    // Recovery tracking
    private Dictionary<(int x, int y), DisasterRecovery> _recoveryData = new();

    public DisasterManager(PlanetMap map, GeologicalSimulator geoSimulator, int seed)
    {
        _map = map;
        _geoSimulator = geoSimulator;
        _random = new Random(seed + 7000);
    }

    public void Update(float deltaTime, int currentYear)
    {
        if (RandomDisastersEnabled)
        {
            CheckForRandomDisasters(deltaTime, currentYear);
        }

        UpdateRecovery(deltaTime);
    }

    private void CheckForRandomDisasters(float deltaTime, int currentYear)
    {
        // Asteroid impact (rare)
        if (AsteroidsEnabled && _random.NextDouble() < 0.0001 * deltaTime)
        {
            int x = _random.Next(_map.Width);
            int y = _random.Next(_map.Height);
            int size = _random.Next(1, 6); // 1-5 size
            TriggerAsteroid(x, y, size, currentYear);
        }

        // Earthquakes at plate boundaries
        if (EarthquakesEnabled && _random.NextDouble() < 0.01 * deltaTime)
        {
            TriggerRandomEarthquake(currentYear);
        }

        // Nuclear accidents (very rare, requires civilizations)
        if (NuclearAccidentsEnabled && _random.NextDouble() < 0.0005 * deltaTime)
        {
            TriggerRandomNuclearAccident(currentYear);
        }

        // Acid rain in polluted areas
        if (AcidRainEnabled && _random.NextDouble() < 0.005 * deltaTime)
        {
            TriggerRandomAcidRain(currentYear);
        }

        // Tornadoes
        if (TornadoesEnabled && _random.NextDouble() < 0.002 * deltaTime)
        {
            TriggerRandomTornado(currentYear);
        }

        // Heavy rains / floods
        if (HeavyRainsEnabled && _random.NextDouble() < 0.01 * deltaTime)
        {
            TriggerRandomHeavyRain(currentYear);
        }
    }

    public void TriggerAsteroid(int x, int y, int size, int year)
    {
        var cell = _map.Cells[x, y];
        var geo = cell.GetGeology();

        // Record disaster
        RecentDisasters.Add(new DisasterEvent
        {
            Type = DisasterType.Asteroid,
            X = x,
            Y = y,
            Year = year,
            Magnitude = size
        });

        // Crater formation
        int craterRadius = size * 3;
        float craterDepth = 0.2f * size;

        for (int dx = -craterRadius; dx <= craterRadius; dx++)
        {
            for (int dy = -craterRadius; dy <= craterRadius; dy++)
            {
                int nx = (x + dx + _map.Width) % _map.Width;
                int ny = y + dy;
                if (ny < 0 || ny >= _map.Height) continue;

                float distance = MathF.Sqrt(dx * dx + dy * dy);
                if (distance > craterRadius) continue;

                var target = _map.Cells[nx, ny];
                var targetGeo = target.GetGeology();
                float effect = 1.0f - (distance / craterRadius);

                // Crater depression
                target.Elevation -= craterDepth * effect;

                // Ejecta ring at crater edge
                if (distance > craterRadius * 0.7f && distance <= craterRadius)
                {
                    target.Elevation += 0.1f * size * effect;
                }

                // Heat and destruction
                if (distance < craterRadius * 0.5f)
                {
                    target.Temperature += 500 * effect;
                    target.Biomass = 0;
                }
                else
                {
                    target.Temperature += 100 * effect;
                    target.Biomass *= (1.0f - 0.8f * effect);
                }

                // Shockwave damage
                target.CO2 += 5.0f * effect;

                // Mark for recovery
                StartRecovery(nx, ny, DisasterType.Asteroid, effect * 100);
            }
        }

        // Global effects for large impacts
        if (size >= 4)
        {
            _map.SolarEnergy -= 0.1f * size; // Impact winter
        }
    }

    public void TriggerEarthquake(int x, int y, float magnitude, int year)
    {
        var cell = _map.Cells[x, y];
        var geo = cell.GetGeology();

        RecentDisasters.Add(new DisasterEvent
        {
            Type = DisasterType.Earthquake,
            X = x,
            Y = y,
            Year = year,
            Magnitude = magnitude
        });

        int radius = (int)(magnitude * 5);

        for (int dx = -radius; dx <= radius; dx++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                int nx = (x + dx + _map.Width) % _map.Width;
                int ny = y + dy;
                if (ny < 0 || ny >= _map.Height) continue;

                float distance = MathF.Sqrt(dx * dx + dy * dy);
                if (distance > radius) continue;

                var target = _map.Cells[nx, ny];
                var targetGeo = target.GetGeology();
                float effect = (1.0f - distance / radius) * magnitude;

                // Ground deformation
                target.Elevation += (_random.NextDouble() - 0.5) * 0.05f * effect;

                // Building damage (affects civilizations)
                target.Biomass *= (1.0f - 0.3f * effect);

                // Trigger landslides on slopes
                if (target.Elevation > 0.5f && _random.NextDouble() < 0.2 * effect)
                {
                    targetGeo.SedimentLayer += 0.1f;
                    target.Elevation -= 0.02f;
                }

                StartRecovery(nx, ny, DisasterType.Earthquake, effect * 50);
            }
        }

        // Can trigger volcanic eruptions
        if (geo.IsVolcano && magnitude > 5.0f)
        {
            geo.MagmaPressure += magnitude * 0.5f;
        }
    }

    private void TriggerRandomEarthquake(int year)
    {
        // Find high-stress tectonic areas
        var stressedCells = new List<(int x, int y, float stress)>();

        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                var geo = _map.Cells[x, y].GetGeology();
                if (geo.TectonicStress > 0.5f)
                {
                    stressedCells.Add((x, y, geo.TectonicStress));
                }
            }
        }

        if (stressedCells.Any())
        {
            var chosen = stressedCells[_random.Next(stressedCells.Count)];
            float magnitude = 4.0f + (float)_random.NextDouble() * 5.0f; // 4.0-9.0
            TriggerEarthquake(chosen.x, chosen.y, magnitude, year);

            // Release tectonic stress
            _map.Cells[chosen.x, chosen.y].GetGeology().TectonicStress *= 0.3f;
        }
    }

    public void TriggerNuclearAccident(int x, int y, int year)
    {
        RecentDisasters.Add(new DisasterEvent
        {
            Type = DisasterType.NuclearAccident,
            X = x,
            Y = y,
            Year = year,
            Magnitude = 1
        });

        int radius = 10;

        for (int dx = -radius; dx <= radius; dx++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                int nx = (x + dx + _map.Width) % _map.Width;
                int ny = y + dy;
                if (ny < 0 || ny >= _map.Height) continue;

                float distance = MathF.Sqrt(dx * dx + dy * dy);
                if (distance > radius) continue;

                var target = _map.Cells[nx, ny];
                float effect = 1.0f - (distance / radius);

                // Radioactive contamination
                target.Biomass *= (1.0f - 0.9f * effect);
                target.Temperature += 50 * effect;

                // Long-lasting contamination
                StartRecovery(nx, ny, DisasterType.NuclearAccident, effect * 200); // Very slow recovery
            }
        }
    }

    private void TriggerRandomNuclearAccident(int year)
    {
        // Find industrial/scientific civilizations
        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                var cell = _map.Cells[x, y];
                if (cell.LifeType == LifeForm.Civilization && _random.NextDouble() < 0.001)
                {
                    TriggerNuclearAccident(x, y, year);
                    return;
                }
            }
        }
    }

    public void TriggerAcidRain(int x, int y, int year)
    {
        RecentDisasters.Add(new DisasterEvent
        {
            Type = DisasterType.AcidRain,
            X = x,
            Y = y,
            Year = year,
            Magnitude = 1
        });

        int radius = 8;

        for (int dx = -radius; dx <= radius; dx++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                int nx = (x + dx + _map.Width) % _map.Width;
                int ny = y + dy;
                if (ny < 0 || ny >= _map.Height) continue;

                float distance = MathF.Sqrt(dx * dx + dy * dy);
                if (distance > radius) continue;

                var target = _map.Cells[nx, ny];
                float effect = 1.0f - (distance / radius);

                // Damages plants and water quality
                target.Biomass *= (1.0f - 0.4f * effect);
                target.GetGeology().SedimentaryRock *= (1.0f - 0.1f * effect); // Erodes rock

                StartRecovery(nx, ny, DisasterType.AcidRain, effect * 30);
            }
        }
    }

    private void TriggerRandomAcidRain(int year)
    {
        // Find polluted areas
        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                var cell = _map.Cells[x, y];
                if (cell.CO2 > 3.0f && _random.NextDouble() < 0.01)
                {
                    TriggerAcidRain(x, y, year);
                    return;
                }
            }
        }
    }

    public void TriggerTornado(int x, int y, int year)
    {
        RecentDisasters.Add(new DisasterEvent
        {
            Type = DisasterType.Tornado,
            X = x,
            Y = y,
            Year = year,
            Magnitude = 1
        });

        // Tornado path (moves in random direction)
        int pathLength = 20;
        int dx = _random.Next(-1, 2);
        int dy = _random.Next(-1, 2);

        for (int i = 0; i < pathLength; i++)
        {
            int nx = (x + dx * i + _map.Width) % _map.Width;
            int ny = y + dy * i;
            if (ny < 0 || ny >= _map.Height) break;

            var cell = _map.Cells[nx, ny];

            // Tornado damage in 2-cell radius
            for (int r = -2; r <= 2; r++)
            {
                for (int c = -2; c <= 2; c++)
                {
                    int tx = (nx + r + _map.Width) % _map.Width;
                    int ty = ny + c;
                    if (ty < 0 || ty >= _map.Height) continue;

                    var target = _map.Cells[tx, ty];
                    target.Biomass *= 0.6f; // Destroys vegetation

                    StartRecovery(tx, ty, DisasterType.Tornado, 20);
                }
            }
        }
    }

    private void TriggerRandomTornado(int year)
    {
        // Tornadoes more common in grasslands with warm, moist conditions
        var candidates = new List<(int x, int y)>();

        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                var cell = _map.Cells[x, y];
                var biome = cell.GetBiomeData().CurrentBiome;

                if ((biome == Biome.Grassland || biome == Biome.Shrubland) &&
                    cell.Temperature > 15 && cell.Humidity > 0.6f)
                {
                    candidates.Add((x, y));
                }
            }
        }

        if (candidates.Any())
        {
            var chosen = candidates[_random.Next(candidates.Count)];
            TriggerTornado(chosen.x, chosen.y, year);
        }
    }

    public void TriggerHeavyRain(int x, int y, int year)
    {
        RecentDisasters.Add(new DisasterEvent
        {
            Type = DisasterType.HeavyRain,
            X = x,
            Y = y,
            Year = year,
            Magnitude = 1
        });

        int radius = 15;

        for (int dx = -radius; dx <= radius; dx++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                int nx = (x + dx + _map.Width) % _map.Width;
                int ny = y + dy;
                if (ny < 0 || ny >= _map.Height) continue;

                float distance = MathF.Sqrt(dx * dx + dy * dy);
                if (distance > radius) continue;

                var target = _map.Cells[nx, ny];
                var targetGeo = target.GetGeology();
                float effect = 1.0f - (distance / radius);

                // Intense rainfall
                target.Rainfall = Math.Min(target.Rainfall + 0.5f * effect, 1.0f);

                // Flooding
                if (target.IsLand)
                {
                    targetGeo.FloodLevel += 0.3f * effect;
                }

                // Erosion and sediment transport
                targetGeo.SedimentLayer += 0.05f * effect;

                StartRecovery(nx, ny, DisasterType.HeavyRain, effect * 15);
            }
        }
    }

    private void TriggerRandomHeavyRain(int year)
    {
        int x = _random.Next(_map.Width);
        int y = _random.Next(_map.Height);
        TriggerHeavyRain(x, y, year);
    }

    private void StartRecovery(int x, int y, DisasterType type, float severity)
    {
        var key = (x, y);
        if (!_recoveryData.ContainsKey(key))
        {
            _recoveryData[key] = new DisasterRecovery();
        }

        var recovery = _recoveryData[key];
        recovery.DisasterType = type;
        recovery.RecoveryProgress = 0;
        recovery.TotalRecoveryTime = severity;
    }

    private void UpdateRecovery(float deltaTime)
    {
        var keysToRemove = new List<(int x, int y)>();

        foreach (var kvp in _recoveryData)
        {
            var (x, y) = kvp.Key;
            var recovery = kvp.Value;

            recovery.RecoveryProgress += deltaTime;

            // Gradual recovery
            float recoveryRatio = recovery.RecoveryProgress / recovery.TotalRecoveryTime;
            if (recoveryRatio < 1.0f)
            {
                var cell = _map.Cells[x, y];
                var biome = cell.GetBiomeData();

                // Biomass regrowth
                if (cell.Biomass < 0.5f && cell.IsLand)
                {
                    cell.Biomass += 0.001f * deltaTime * recoveryRatio;
                }

                // Soil recovery
                if (recovery.DisasterType == DisasterType.NuclearAccident)
                {
                    // Very slow recovery from radiation
                    cell.Biomass = Math.Min(cell.Biomass, 0.1f);
                }
            }
            else
            {
                keysToRemove.Add(kvp.Key);
            }
        }

        foreach (var key in keysToRemove)
        {
            _recoveryData.Remove(key);
        }

        // Cleanup old disaster events
        if (RecentDisasters.Count > 50)
        {
            RecentDisasters.RemoveRange(0, RecentDisasters.Count - 50);
        }
    }

    public List<DisasterEvent> GetAllDisasters()
    {
        return RecentDisasters;
    }
}

public class DisasterEvent
{
    public DisasterType Type { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int Year { get; set; }
    public float Magnitude { get; set; }
}

public class DisasterRecovery
{
    public DisasterType DisasterType { get; set; }
    public float RecoveryProgress { get; set; }
    public float TotalRecoveryTime { get; set; }
}

public enum DisasterType
{
    Asteroid,
    Earthquake,
    VolcanicEruption,
    NuclearAccident,
    AcidRain,
    Tornado,
    HeavyRain,
    Flood
}
