using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SimPlanet;

/// <summary>
/// Manages forest fires, smoke generation, and firefighting
/// </summary>
public class ForestFireManager
{
    private readonly PlanetMap _map;
    private readonly Random _random;
    private readonly List<ForestFire> _activeFires = new();
    private float _fireCheckTimer = 0;
    private const float FireCheckInterval = 5.0f; // Check for new fires every 5 seconds

    public List<ForestFire> ActiveFires => _activeFires;

    public ForestFireManager(PlanetMap map, int seed)
    {
        _map = map;
        _random = new Random(seed + 9000);
    }

    public void Update(float deltaTime, WeatherSystem weatherSystem, CivilizationManager civManager)
    {
        _fireCheckTimer += deltaTime;

        // Check for natural fire starts (lightning, spontaneous combustion)
        if (_fireCheckTimer >= FireCheckInterval)
        {
            CheckForNaturalFires(weatherSystem);
            _fireCheckTimer = 0;
        }

        // Update existing fires
        for (int i = _activeFires.Count - 1; i >= 0; i--)
        {
            var fire = _activeFires[i];
            UpdateFire(fire, deltaTime, weatherSystem, civManager);

            // Remove extinguished fires
            if (fire.Intensity <= 0 || fire.BurnedArea.Count == 0)
            {
                _activeFires.RemoveAt(i);
            }
        }
    }

    private void CheckForNaturalFires(WeatherSystem weatherSystem)
    {
        // Random chance of fire starting in dry, hot forests
        if (_random.NextDouble() < 0.1) // 10% chance per interval
        {
            int x = _random.Next(_map.Width);
            int y = _random.Next(_map.Height);

            var cell = _map.Cells[x, y];

            // Fire conditions: forest, hot, dry
            if (cell.IsLand && cell.Biomass > 0.5f &&
                cell.Temperature > 25 && cell.Rainfall < 0.3f)
            {
                // Check for lightning storms
                bool hasLightning = weatherSystem.ActiveStorms.Any(s =>
                    s.Type == StormType.Thunderstorm &&
                    Math.Abs(s.CenterX - x) < 10 &&
                    Math.Abs(s.CenterY - y) < 10);

                if (hasLightning || (_random.NextDouble() < 0.05 && cell.Temperature > 35))
                {
                    StartFire(x, y, FireCause.Lightning);
                }
            }
        }
    }

    public void StartFire(int x, int y, FireCause cause)
    {
        // Check if fire already exists nearby
        if (_activeFires.Any(f => f.BurnedArea.Contains((x, y))))
            return;

        var fire = new ForestFire
        {
            OriginX = x,
            OriginY = y,
            Cause = cause,
            Intensity = 1.0f,
            StartYear = 0 // Set by caller if needed
        };

        fire.BurnedArea.Add((x, y));
        _activeFires.Add(fire);
    }

    private void UpdateFire(ForestFire fire, float deltaTime, WeatherSystem weatherSystem, CivilizationManager civManager)
    {
        // Check for rain - extinguishes fire
        var cell = _map.Cells[fire.OriginX, fire.OriginY];
        var meteor = cell.GetMeteorology();

        if (meteor.Precipitation > 0.5f) // Heavy rain
        {
            fire.Intensity -= deltaTime * 0.3f; // Rain puts out fire
            return;
        }
        else if (meteor.Precipitation > 0.2f) // Light rain
        {
            fire.Intensity -= deltaTime * 0.1f; // Slower extinguishing
            return;
        }

        // Check for firefighters
        bool hasFirefighters = false;
        foreach (var civ in civManager.Civilizations)
        {
            if (civ.CivType >= CivType.Industrial) // Industrial+ civilizations have firefighters
            {
                // Check if civilization territory is near fire
                foreach (var (fx, fy) in fire.BurnedArea.ToList())
                {
                    if (Math.Abs(fx - civ.CenterX) < 20 && Math.Abs(fy - civ.CenterY) < 20)
                    {
                        hasFirefighters = true;
                        fire.BeingFought = true;
                        break;
                    }
                }
            }
        }

        if (hasFirefighters)
        {
            // Firefighters reduce fire intensity
            fire.Intensity -= deltaTime * 0.2f;
            if (fire.Intensity < 0) fire.Intensity = 0;
            return;
        }
        else
        {
            fire.BeingFought = false;
        }

        // Spread fire to neighboring cells
        var cellsToCheck = new List<(int x, int y)>(fire.BurnedArea);

        foreach (var (x, y) in cellsToCheck)
        {
            var fireCell = _map.Cells[x, y];

            // Burn current cell
            if (fireCell.Biomass > 0)
            {
                float burnRate = deltaTime * 0.05f * fire.Intensity;
                fireCell.Biomass -= burnRate;

                // Generate smoke (increases atmospheric CO2 and particulates)
                fireCell.CO2 += burnRate * 2.0f;
                fireCell.Temperature += burnRate * 10.0f; // Fire is hot

                // Add smoke to clouds
                var meteorData = fireCell.GetMeteorology();
                meteorData.CloudCover = Math.Min(meteorData.CloudCover + burnRate * 0.5f, 1.0f);

                if (fireCell.Biomass <= 0)
                {
                    fireCell.Biomass = 0;
                    fireCell.LifeType = LifeForm.None; // Burned out
                }
            }

            // Spread to neighbors if still intense
            if (fire.Intensity > 0.3f && _random.NextDouble() < fire.Intensity * 0.3)
            {
                foreach (var (nx, ny, neighbor) in _map.GetNeighbors(x, y))
                {
                    // Fire spreads to flammable neighbors
                    if (neighbor.IsLand && neighbor.Biomass > 0.3f &&
                        !fire.BurnedArea.Contains((nx, ny)))
                    {
                        // Higher chance to spread in dry, hot conditions
                        float spreadChance = 0.1f;
                        if (neighbor.Temperature > 30) spreadChance += 0.2f;
                        if (neighbor.Rainfall < 0.3f) spreadChance += 0.2f;
                        if (neighbor.Biomass > 0.6f) spreadChance += 0.1f; // Dense forests

                        if (_random.NextDouble() < spreadChance)
                        {
                            fire.BurnedArea.Add((nx, ny));
                        }
                    }
                }
            }
        }

        // Fire intensity naturally decreases over time as fuel depletes
        fire.Intensity -= deltaTime * 0.02f;
        if (fire.Intensity < 0) fire.Intensity = 0;
    }

    public void TriggerFire(int x, int y)
    {
        StartFire(x, y, FireCause.Manual);
    }
}

public class ForestFire
{
    public int OriginX { get; set; }
    public int OriginY { get; set; }
    public FireCause Cause { get; set; }
    public float Intensity { get; set; } = 1.0f; // 0-1
    public int StartYear { get; set; }
    public HashSet<(int x, int y)> BurnedArea { get; set; } = new();
    public bool BeingFought { get; set; } = false;
}

public enum FireCause
{
    Lightning,
    Manual,
    Spontaneous
}
