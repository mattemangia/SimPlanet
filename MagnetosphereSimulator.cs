using System;
using System.Collections.Generic;

namespace SimPlanet;

/// <summary>
/// Simulates planetary magnetosphere, cosmic rays, and radiation protection
/// </summary>
public class MagnetosphereSimulator
{
    private readonly PlanetMap _map;
    private readonly Random _random;

    // Planetary magnetic field
    public float MagneticFieldStrength { get; set; } = 1.0f; // 1.0 = Earth-like
    public float CoreTemperature { get; set; } = 5000f; // Kelvin
    public bool HasDynamo { get; set; } = true; // Active core dynamo

    // Solar activity
    public float SolarWindStrength { get; set; } = 1.0f; // 1.0 = normal
    public float CosmicRayIntensity { get; set; } = 1.0f; // Background cosmic rays

    // Radiation tracking
    public float GlobalRadiation { get; set; } = 0.0f; // Average surface radiation

    public MagnetosphereSimulator(PlanetMap map, int seed)
    {
        _map = map;
        _random = new Random(seed + 11000);

        // Initialize based on planet properties
        InitializeMagnetosphere();
    }

    private void InitializeMagnetosphere()
    {
        // Magnetic field depends on core temperature and rotation
        // Earth-like dynamo requires liquid iron core
        if (CoreTemperature > 3000f && CoreTemperature < 6000f)
        {
            HasDynamo = true;
            MagneticFieldStrength = 0.8f + (float)_random.NextDouble() * 0.4f; // 0.8-1.2
        }
        else
        {
            HasDynamo = false;
            MagneticFieldStrength = 0.0f;
        }
    }

    public void Update(float deltaTime, int gameYear)
    {
        // Update core temperature (very slowly cools over time)
        CoreTemperature -= deltaTime * 0.00001f;

        // Magnetic field weakens if core cools too much
        if (CoreTemperature < 3000f)
        {
            HasDynamo = false;
            MagneticFieldStrength = Math.Max(MagneticFieldStrength - deltaTime * 0.001f, 0.0f);
        }

        // Random magnetic reversals (like Earth's historical reversals)
        if (HasDynamo && _random.NextDouble() < 0.0001 * deltaTime)
        {
            // Magnetic field weakens during reversal
            MagneticFieldStrength *= 0.5f;
        }
        else if (MagneticFieldStrength < 1.0f && HasDynamo)
        {
            // Recover magnetic field strength
            MagneticFieldStrength = Math.Min(MagneticFieldStrength + deltaTime * 0.01f, 1.0f);
        }

        // Vary solar activity (solar cycles)
        SolarWindStrength = 0.8f + 0.4f * (float)Math.Sin(gameYear * 0.1);
        CosmicRayIntensity = 0.9f + 0.2f * (float)_random.NextDouble();

        // Calculate radiation levels for each cell
        CalculateRadiation(deltaTime);

        // Simulate auroras at poles
        SimulateAuroras();
    }

    private void CalculateRadiation(float deltaTime)
    {
        float totalRadiation = 0;
        int count = 0;

        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                var cell = _map.Cells[x, y];

                // Calculate latitude (0 = equator, 1 = poles)
                float latitude = Math.Abs((y - _map.Height / 2.0f) / (_map.Height / 2.0f));

                // Base cosmic ray exposure
                float cosmicRays = CosmicRayIntensity;

                // Magnetosphere deflects cosmic rays (stronger at equator)
                float magneticProtection = MagneticFieldStrength * (1.0f - latitude * 0.3f);
                cosmicRays *= (1.0f - magneticProtection * 0.7f);

                // Polar regions get more cosmic rays (field lines converge) - smooth transition
                // Gradually increase from 60° latitude
                if (latitude > 0.6f)
                {
                    float polarEnhancement = (latitude - 0.6f) / 0.4f; // 0 to 1 from 60° to poles
                    cosmicRays *= (1.0f + polarEnhancement * latitude);
                }

                // Atmosphere shields radiation (more atmosphere = less radiation)
                float atmosphericShielding = Math.Min(cell.Oxygen / 21.0f, 1.0f) * 0.6f;
                atmosphericShielding += Math.Min(cell.CO2 / 0.04f, 1.0f) * 0.2f; // CO2 also shields
                cosmicRays *= (1.0f - atmosphericShielding);

                // Elevation increases radiation (less atmosphere above)
                if (cell.Elevation > 0.5f)
                {
                    cosmicRays *= (1.0f + (cell.Elevation - 0.5f) * 0.8f);
                }

                // Solar wind adds radiation (varies with solar activity)
                float solarRadiation = SolarWindStrength * 0.3f;
                if (!HasDynamo)
                {
                    // No magnetosphere = direct solar wind impact (like Mars)
                    solarRadiation *= 3.0f;
                }
                else
                {
                    // Magnetosphere deflects solar wind
                    solarRadiation *= (1.0f - MagneticFieldStrength * 0.8f);
                }

                // Natural radioactivity from uranium deposits
                float naturalRadiation = 0.0f;
                var geo = cell.GetGeology();
                var uraniumDeposit = cell.GetResourceDeposit(ResourceType.Uranium);
                if (uraniumDeposit != null && uraniumDeposit.Amount > 0.1f)
                {
                    // Uranium deposits emit radiation (0.5-2.0 based on concentration)
                    naturalRadiation = uraniumDeposit.Amount * 2.0f;
                }

                // Nuclear power plants emit radiation
                if (geo.HasNuclearPlant)
                {
                    // Operating nuclear plants emit low-level radiation (0.5)
                    // Higher if plant is old or at high meltdown risk
                    naturalRadiation += 0.5f + geo.MeltdownRisk * 2.0f;
                }

                // Total radiation for this cell
                float totalCellRadiation = cosmicRays + solarRadiation + naturalRadiation;

                // Store radiation data
                var magneticData = cell.GetMagneticData();
                magneticData.RadiationLevel = totalCellRadiation;
                magneticData.MagneticFieldStrength = MagneticFieldStrength * (1.0f - latitude * 0.2f);

                totalRadiation += totalCellRadiation;
                count++;

                // Radiation damages life
                if (totalCellRadiation > 2.0f && cell.LifeType != LifeForm.None)
                {
                    // High radiation kills complex life
                    if (cell.LifeType != LifeForm.Bacteria && _random.NextDouble() < totalCellRadiation * 0.01f)
                    {
                        cell.Biomass -= deltaTime * totalCellRadiation * 0.05f;
                        if (cell.Biomass < 0)
                        {
                            cell.Biomass = 0;
                            cell.LifeType = LifeForm.None;
                        }
                    }
                }
            }
        }

        GlobalRadiation = totalRadiation / count;
    }

    private void SimulateAuroras()
    {
        // Auroras occur in polar regions when solar wind is strong and magnetosphere is active
        if (!HasDynamo || SolarWindStrength < 0.8f) return;

        float auroraIntensity = (SolarWindStrength - 0.8f) * MagneticFieldStrength;

        for (int x = 0; x < _map.Width; x++)
        {
            // Northern aurora (top 10% of map)
            for (int y = 0; y < _map.Height / 10; y++)
            {
                var cell = _map.Cells[x, y];
                var magneticData = cell.GetMagneticData();
                magneticData.AuroraIntensity = auroraIntensity * (1.0f - (y / (_map.Height / 10.0f)));
            }

            // Southern aurora (bottom 10% of map)
            for (int y = _map.Height * 9 / 10; y < _map.Height; y++)
            {
                var cell = _map.Cells[x, y];
                var magneticData = cell.GetMagneticData();
                int distanceFromBottom = _map.Height - y;
                magneticData.AuroraIntensity = auroraIntensity * (1.0f - (distanceFromBottom / (_map.Height / 10.0f)));
            }
        }
    }

    public void TriggerSolarStorm()
    {
        // Massive increase in solar wind
        SolarWindStrength = 3.0f + (float)_random.NextDouble() * 2.0f;
    }

    public void TriggerMagneticReversal()
    {
        // Force a magnetic field reversal
        MagneticFieldStrength *= 0.3f;
    }
}

/// <summary>
/// Extension methods for magnetic/radiation data (now uses embedded data for performance)
/// </summary>
public static class MagneticExtensions
{
    // Extension methods now simply access embedded property (maintains backward compatibility)
    public static MagneticData GetMagneticData(this TerrainCell cell)
    {
        return cell.Magnetic;
    }

    // No longer needed as data is embedded in TerrainCell, but kept for API compatibility
    public static void ClearMagneticData()
    {
        // No-op: data is now managed per-cell, cleared when cells are recreated
    }
}

public class MagneticData
{
    public float MagneticFieldStrength { get; set; } = 1.0f; // Local field strength
    public float RadiationLevel { get; set; } = 0.0f; // Surface radiation (0-5+)
    public float AuroraIntensity { get; set; } = 0.0f; // Aurora brightness (0-1)
}