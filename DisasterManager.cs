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
        // Use a non-deterministic random seed for runtime disasters
        // The passed seed is ignored to ensure disasters are random every run
        _random = new Random();
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

        // Rockfalls on mountain roads (checks all risky roads)
        CheckForRockfalls(deltaTime, currentYear);
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

        // MASSIVE crater formation - asteroids are devastating
        int craterRadius = size * 5;          // Much larger crater
        int blastRadius = size * 10;          // Thermal blast extends further
        int devastationRadius = size * 15;    // Total devastation zone
        float craterDepth = 0.3f * size;      // Deeper crater

        // Phase 1: CRATER - Complete vaporization at ground zero
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

                // Crater bowl shape (parabolic)
                float craterProfile = effect * effect;
                target.Elevation -= craterDepth * craterProfile;

                // Mark as crater
                targetGeo.IsInCrater = true;
                targetGeo.CraterDepth = craterDepth * craterProfile;
                targetGeo.DisasterYear = year;

                // Complete vaporization at center
                if (distance < craterRadius * 0.3f)
                {
                    target.Temperature += 2000 * effect;  // Vaporization temperatures
                    target.Biomass = 0;
                    target.LifeType = LifeForm.None;
                    targetGeo.ImpactScorching = 1.0f;
                    targetGeo.BlastDamage = 1.0f;
                }
                // Inner crater - melted rock
                else
                {
                    target.Temperature += 1000 * effect;
                    target.Biomass = 0;
                    target.LifeType = LifeForm.None;
                    targetGeo.ImpactScorching = effect;
                    targetGeo.BlastDamage = effect;
                    targetGeo.VolcanicRock += 0.3f * effect;  // Melted rock resolidifies
                }

                // Ejecta ring at crater rim
                if (distance > craterRadius * 0.8f && distance <= craterRadius)
                {
                    target.Elevation += 0.15f * size * (1.0f - effect);
                }

                StartRecovery(nx, ny, DisasterType.Asteroid, effect * 500);  // Very slow recovery
            }
        }

        // Phase 2: THERMAL BLAST - Incinerates everything
        for (int dx = -blastRadius; dx <= blastRadius; dx++)
        {
            for (int dy = -blastRadius; dy <= blastRadius; dy++)
            {
                int nx = (x + dx + _map.Width) % _map.Width;
                int ny = y + dy;
                if (ny < 0 || ny >= _map.Height) continue;

                float distance = MathF.Sqrt(dx * dx + dy * dy);
                if (distance <= craterRadius || distance > blastRadius) continue;

                var target = _map.Cells[nx, ny];
                var targetGeo = target.GetGeology();
                float effect = 1.0f - ((distance - craterRadius) / (blastRadius - craterRadius));

                // Thermal blast - burns everything
                target.Temperature += 500 * effect;
                target.Biomass *= (1.0f - 0.95f * effect);
                if (target.Biomass < 0.1f)
                {
                    target.LifeType = LifeForm.None;
                }
                targetGeo.ImpactScorching = Math.Max(targetGeo.ImpactScorching, effect * 0.8f);
                targetGeo.BlastDamage = Math.Max(targetGeo.BlastDamage, effect * 0.6f);
                targetGeo.DisasterYear = year;

                // Destroy infrastructure
                if (effect > 0.5f)
                {
                    targetGeo.HasRoad = false;
                    targetGeo.RoadType = RoadType.None;
                    targetGeo.HasNuclearPlant = false;
                    targetGeo.HasSolarFarm = false;
                    targetGeo.HasWindTurbine = false;
                }

                StartRecovery(nx, ny, DisasterType.Asteroid, effect * 200);
            }
        }

        // Phase 3: SHOCKWAVE - Destroys structures, flattens forests
        for (int dx = -devastationRadius; dx <= devastationRadius; dx++)
        {
            for (int dy = -devastationRadius; dy <= devastationRadius; dy++)
            {
                int nx = (x + dx + _map.Width) % _map.Width;
                int ny = y + dy;
                if (ny < 0 || ny >= _map.Height) continue;

                float distance = MathF.Sqrt(dx * dx + dy * dy);
                if (distance <= blastRadius || distance > devastationRadius) continue;

                var target = _map.Cells[nx, ny];
                var targetGeo = target.GetGeology();
                float effect = 1.0f - ((distance - blastRadius) / (devastationRadius - blastRadius));

                // Shockwave damage
                target.Temperature += 50 * effect;
                target.Biomass *= (1.0f - 0.6f * effect);
                targetGeo.BlastDamage = Math.Max(targetGeo.BlastDamage, effect * 0.3f);

                // CO2 from burning
                target.CO2 += 3.0f * effect;

                StartRecovery(nx, ny, DisasterType.Asteroid, effect * 50);
            }
        }

        // Phase 4: GLOBAL EFFECTS - Impact winter
        float globalEffect = size * size * 0.02f;  // Exponential with size
        _map.SolarEnergy -= globalEffect;          // Dust blocks sunlight
        _map.GlobalCO2 += size * 2.0f;             // Massive CO2 release

        // Trigger secondary effects
        if (size >= 3)
        {
            // Tsunamis if impact is in water or near coast
            if (cell.IsWater || cell.IsCoastal)
            {
                TriggerImpactTsunami(x, y, size, year);
            }

            // Widespread fires
            TriggerImpactFires(x, y, size, year);
        }

        // EXTINCTION-LEVEL EVENT for size 5
        if (size >= 5)
        {
            _map.SolarEnergy -= 0.3f;  // Severe impact winter
            _map.GlobalCO2 += 20.0f;    // Massive greenhouse gas release

            // Global temperature drop followed by warming
            for (int mx = 0; mx < _map.Width; mx++)
            {
                for (int my = 0; my < _map.Height; my++)
                {
                    _map.Cells[mx, my].Temperature -= 5;  // Initial cooling
                }
            }
        }
    }

    private void TriggerImpactTsunami(int x, int y, int size, int year)
    {
        // Use the TsunamiSystem for proper wave propagation
        TsunamiSystem.InitiateTsunamiFromImpact(_map, x, y, size, year);
    }

    private void TriggerImpactFires(int x, int y, int size, int year)
    {
        // Thermal radiation ignites fires across the region
        int fireRadius = size * 20;
        for (int dx = -fireRadius; dx <= fireRadius; dx++)
        {
            for (int dy = -fireRadius; dy <= fireRadius; dy++)
            {
                int nx = (x + dx + _map.Width) % _map.Width;
                int ny = y + dy;
                if (ny < 0 || ny >= _map.Height) continue;

                float distance = MathF.Sqrt(dx * dx + dy * dy);
                if (distance > fireRadius) continue;

                var target = _map.Cells[nx, ny];
                if (!target.IsLand || target.IsIce) continue;

                // Random fires in flammable areas
                if (target.Biomass > 0.3f && _random.NextDouble() < 0.1 * (1.0f - distance / fireRadius))
                {
                    target.Biomass *= 0.3f;
                    target.Temperature += 100;
                    target.CO2 += 2.0f;
                    target.GetGeology().ImpactScorching = Math.Max(target.GetGeology().ImpactScorching, 0.3f);
                }
            }
        }
    }

    /// <summary>
    /// Trigger an Electromagnetic Pulse (EMP) that disables all electronics in a large area.
    /// High-altitude nuclear detonations can affect areas hundreds of kilometers in radius.
    /// </summary>
    private void TriggerEMP(int x, int y, int year)
    {
        // EMP radius is MUCH larger than the blast - can cover entire regions
        // A high-altitude nuke can affect an area 1000+ km in radius
        int empRadius = 80;  // Very large area
        int recoveryYears = 5;  // Electronics take years to replace/repair

        int affectedCells = 0;
        int disabledPlants = 0;
        int disabledSolar = 0;
        int disabledWind = 0;

        for (int dx = -empRadius; dx <= empRadius; dx++)
        {
            for (int dy = -empRadius; dy <= empRadius; dy++)
            {
                int nx = (x + dx + _map.Width) % _map.Width;
                int ny = y + dy;
                if (ny < 0 || ny >= _map.Height) continue;

                float distance = MathF.Sqrt(dx * dx + dy * dy);
                if (distance > empRadius) continue;

                var target = _map.Cells[nx, ny];
                var targetGeo = target.GetGeology();

                // EMP effect strength decreases with distance
                float empStrength = 1.0f - (distance / empRadius);

                // Mark as EMP affected
                targetGeo.IsEMPAffected = true;
                targetGeo.EMPRecoveryYear = year + recoveryYears;

                // Disable power infrastructure
                if (targetGeo.HasNuclearPlant && _random.NextDouble() < empStrength)
                {
                    // Nuclear plants have some hardening but control systems can fail
                    targetGeo.MeltdownRisk += 0.3f * empStrength;  // Increased meltdown risk
                    disabledPlants++;
                }

                if (targetGeo.HasSolarFarm)
                {
                    // Solar inverters are vulnerable to EMP
                    targetGeo.PowerOutput = 0;  // No power output while EMP affected
                    disabledSolar++;
                }

                if (targetGeo.HasWindTurbine)
                {
                    // Wind turbine electronics are vulnerable
                    targetGeo.PowerOutput = 0;
                    disabledWind++;
                }

                // Disable power lines and stations
                if (targetGeo.HasPowerLine || targetGeo.HasPowerStation)
                {
                    targetGeo.IsPowered = false;
                }

                // All powered infrastructure loses power
                targetGeo.IsPowered = false;

                affectedCells++;
            }
        }

        // Log EMP event
        Console.WriteLine($"[EMP] Nuclear EMP at ({x},{y}) affected {affectedCells} cells");
        Console.WriteLine($"[EMP] Disabled: {disabledPlants} nuclear plants, {disabledSolar} solar farms, {disabledWind} wind turbines");

        // Record as disaster event
        RecentDisasters.Add(new DisasterEvent
        {
            Type = DisasterType.EMP,
            X = x,
            Y = y,
            Year = year,
            Magnitude = empRadius
        });
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
                target.Elevation += (float)(_random.NextDouble() - 0.5) * 0.05f * effect;

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

    /// <summary>
    /// Trigger a nuclear explosion or meltdown with realistic effects
    /// </summary>
    /// <param name="x">X coordinate</param>
    /// <param name="y">Y coordinate</param>
    /// <param name="year">Current year</param>
    /// <param name="isWeapon">If true, this is a nuclear weapon (more destructive). If false, it's a reactor meltdown.</param>
    public void TriggerNuclearAccident(int x, int y, int year, bool isWeapon = false)
    {
        int magnitude = isWeapon ? 5 : 1;

        RecentDisasters.Add(new DisasterEvent
        {
            Type = DisasterType.NuclearAccident,
            X = x,
            Y = y,
            Year = year,
            Magnitude = magnitude
        });

        // Nuclear weapons have much larger effects than meltdowns
        int blastRadius = isWeapon ? 15 : 5;          // Thermal/blast radius
        int radiationRadius = isWeapon ? 30 : 15;     // Radiation contamination
        int falloutRadius = isWeapon ? 50 : 25;       // Fallout zone

        // Phase 1: FIREBALL - Instant vaporization
        if (isWeapon)
        {
            int fireballRadius = 3;
            for (int dx = -fireballRadius; dx <= fireballRadius; dx++)
            {
                for (int dy = -fireballRadius; dy <= fireballRadius; dy++)
                {
                    int nx = (x + dx + _map.Width) % _map.Width;
                    int ny = y + dy;
                    if (ny < 0 || ny >= _map.Height) continue;

                    float distance = MathF.Sqrt(dx * dx + dy * dy);
                    if (distance > fireballRadius) continue;

                    var target = _map.Cells[nx, ny];
                    var targetGeo = target.GetGeology();
                    float effect = 1.0f - (distance / fireballRadius);

                    // Complete vaporization
                    target.Temperature += 5000 * effect;  // Nuclear fireball
                    target.Biomass = 0;
                    target.LifeType = LifeForm.None;
                    target.Elevation -= 0.05f * effect;  // Small crater

                    targetGeo.ImpactScorching = 1.0f;
                    targetGeo.BlastDamage = 1.0f;
                    targetGeo.RadioactiveContamination = 1.0f;
                    targetGeo.DisasterYear = year;

                    // Destroy all infrastructure
                    targetGeo.HasRoad = false;
                    targetGeo.RoadType = RoadType.None;
                    targetGeo.HasNuclearPlant = false;
                    targetGeo.HasSolarFarm = false;
                    targetGeo.HasWindTurbine = false;
                }
            }
        }

        // Phase 2: THERMAL BLAST - Burns everything
        for (int dx = -blastRadius; dx <= blastRadius; dx++)
        {
            for (int dy = -blastRadius; dy <= blastRadius; dy++)
            {
                int nx = (x + dx + _map.Width) % _map.Width;
                int ny = y + dy;
                if (ny < 0 || ny >= _map.Height) continue;

                float distance = MathF.Sqrt(dx * dx + dy * dy);
                if (distance > blastRadius) continue;

                var target = _map.Cells[nx, ny];
                var targetGeo = target.GetGeology();
                float effect = 1.0f - (distance / blastRadius);

                // Thermal damage
                float tempIncrease = isWeapon ? 1000 * effect : 200 * effect;
                target.Temperature += tempIncrease;
                target.Biomass *= (1.0f - 0.95f * effect);
                if (target.Biomass < 0.1f)
                {
                    target.LifeType = LifeForm.None;
                }

                targetGeo.ImpactScorching = Math.Max(targetGeo.ImpactScorching, effect * 0.9f);
                targetGeo.BlastDamage = Math.Max(targetGeo.BlastDamage, effect * 0.8f);
                targetGeo.RadioactiveContamination = Math.Max(targetGeo.RadioactiveContamination, effect * 0.5f);
                targetGeo.DisasterYear = year;

                // Destroy infrastructure
                if (effect > 0.3f)
                {
                    targetGeo.HasRoad = false;
                    targetGeo.RoadType = RoadType.None;
                    targetGeo.HasNuclearPlant = false;
                    targetGeo.HasSolarFarm = false;
                    targetGeo.HasWindTurbine = false;
                }

                StartRecovery(nx, ny, DisasterType.NuclearAccident, effect * 300);
            }
        }

        // Phase 3: INTENSE RADIATION ZONE
        for (int dx = -radiationRadius; dx <= radiationRadius; dx++)
        {
            for (int dy = -radiationRadius; dy <= radiationRadius; dy++)
            {
                int nx = (x + dx + _map.Width) % _map.Width;
                int ny = y + dy;
                if (ny < 0 || ny >= _map.Height) continue;

                float distance = MathF.Sqrt(dx * dx + dy * dy);
                if (distance <= blastRadius || distance > radiationRadius) continue;

                var target = _map.Cells[nx, ny];
                var targetGeo = target.GetGeology();
                float effect = 1.0f - ((distance - blastRadius) / (radiationRadius - blastRadius));

                // Radiation kills life
                target.Biomass *= (1.0f - 0.7f * effect);
                targetGeo.RadioactiveContamination = Math.Max(targetGeo.RadioactiveContamination, effect * 0.8f);
                targetGeo.DisasterYear = year;

                // Radiation sickness affects remaining life
                if (target.Biomass > 0.1f && target.LifeType != LifeForm.None)
                {
                    // Mutation/death of complex life
                    if (target.LifeType >= LifeForm.Mammals && _random.NextDouble() < effect * 0.5)
                    {
                        target.LifeType = LifeForm.SimpleAnimals;
                    }
                }

                StartRecovery(nx, ny, DisasterType.NuclearAccident, effect * 500);  // Very slow recovery
            }
        }

        // Phase 4: FALLOUT ZONE (wind-carried radiation)
        // Fallout typically spreads in wind direction, but we'll do circular for simplicity
        for (int dx = -falloutRadius; dx <= falloutRadius; dx++)
        {
            for (int dy = -falloutRadius; dy <= falloutRadius; dy++)
            {
                int nx = (x + dx + _map.Width) % _map.Width;
                int ny = y + dy;
                if (ny < 0 || ny >= _map.Height) continue;

                float distance = MathF.Sqrt(dx * dx + dy * dy);
                if (distance <= radiationRadius || distance > falloutRadius) continue;

                var target = _map.Cells[nx, ny];
                var targetGeo = target.GetGeology();
                float effect = 1.0f - ((distance - radiationRadius) / (falloutRadius - radiationRadius));

                // Light radiation contamination
                targetGeo.RadioactiveContamination = Math.Max(targetGeo.RadioactiveContamination, effect * 0.3f);

                // Some biomass damage
                target.Biomass *= (1.0f - 0.2f * effect);

                StartRecovery(nx, ny, DisasterType.NuclearAccident, effect * 200);
            }
        }

        // Phase 5: GLOBAL EFFECTS (for nuclear weapons)
        if (isWeapon)
        {
            // Nuclear winter effect
            _map.SolarEnergy -= 0.05f;
            _map.GlobalCO2 += 1.0f;  // Fires release CO2

            // EMP (Electromagnetic Pulse) effect - disables all electronics in large radius
            TriggerEMP(x, y, year);
        }

        // Phase 6: TSUNAMI - if near water
        var epicenterCell = _map.Cells[x, y];
        if (epicenterCell.IsWater || epicenterCell.IsCoastal)
        {
            TsunamiSystem.InitiateTsunamiFromNuke(_map, x, y, isWeapon, year);
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

    /// <summary>
    /// Check for rockfalls on mountain roads with risk
    /// </summary>
    private void CheckForRockfalls(float deltaTime, int currentYear)
    {
        // Scan all cells for roads at rockfall risk
        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                var cell = _map.Cells[x, y];
                var geo = cell.GetGeology();

                // Check if this road is at risk
                if (geo.HasRoad && geo.RockfallRisk && !geo.HasTunnel)
                {
                    // Higher chance during heavy rain or earthquakes
                    float baseChance = 0.0001f * deltaTime;

                    // Increase chance if it's raining heavily
                    if (cell.Rainfall > 0.8f)
                        baseChance *= 3.0f;

                    // Random rockfall event
                    if (_random.NextDouble() < baseChance)
                    {
                        TriggerRockfall(x, y, currentYear);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Trigger a rockfall event that damages roads and infrastructure
    /// </summary>
    public void TriggerRockfall(int x, int y, int year)
    {
        var cell = _map.Cells[x, y];
        var geo = cell.GetGeology();

        RecentDisasters.Add(new DisasterEvent
        {
            Type = DisasterType.Rockfall,
            X = x,
            Y = y,
            Year = year,
            Magnitude = 1
        });

        // Damage or destroy the road
        if (_random.NextDouble() < 0.5)
        {
            // 50% chance to completely destroy road
            geo.HasRoad = false;
            geo.RoadType = RoadType.None;
            geo.RockfallRisk = false;
        }
        else
        {
            // Otherwise downgrade road type
            geo.RoadType = geo.RoadType switch
            {
                RoadType.Highway => RoadType.Road,
                RoadType.Road => RoadType.DirtPath,
                RoadType.DirtPath => RoadType.None,
                _ => RoadType.None
            };

            if (geo.RoadType == RoadType.None)
            {
                geo.HasRoad = false;
                geo.RockfallRisk = false;
            }
        }

        // Deposit debris
        geo.SedimentLayer += 0.05f + (float)_random.NextDouble() * 0.1f;

        // Minor damage to nearby cells (debris spread)
        foreach (var (nx, ny, neighbor) in _map.GetNeighbors(x, y))
        {
            neighbor.Biomass *= 0.9f; // 10% vegetation damage
            var neighborGeo = neighbor.GetGeology();
            neighborGeo.SedimentLayer += 0.02f;
        }

        StartRecovery(x, y, DisasterType.Rockfall, 20);
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
    Flood,
    Rockfall,
    EMP  // Electromagnetic Pulse from nuclear weapons
}
