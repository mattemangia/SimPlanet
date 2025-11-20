using System;
using System.Threading;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;

namespace SimPlanet;

public class HeadlessSimulation
{
    // Core systems
    private PlanetMap _map;
    private ClimateSimulator _climateSimulator;
    private AtmosphereSimulator _atmosphereSimulator;
    private LifeSimulator _lifeSimulator;
    private AnimalEvolutionSimulator _animalEvolutionSimulator;
    private GeologicalSimulator _geologicalSimulator;
    private HydrologySimulator _hydrologySimulator;
    private WeatherSystem _weatherSystem;
    private CivilizationManager _civilizationManager;
    private BiomeSimulator _biomeSimulator;
    private DisasterManager _disasterManager;
    private ForestFireManager _forestFireManager;
    private MagnetosphereSimulator _magnetosphereSimulator;
    private PlanetStabilizer _planetStabilizer;
    private DiseaseManager _diseaseManager;
    private UpdateManager _updateManager;

    // Map generation settings
    private MapGenerationOptions _mapOptions;

    // Simulation state
    private int _year = 0;
    private float _timeAccumulator = 0;
    private const float SecondsPerGameYear = 10.0f;

    // GameState mimics
    private float _timeSpeed = 1.0f;

    public void Run(string[] args)
    {
        Console.WriteLine("Starting Headless Simulation...");
        Console.Out.Flush();

        Initialize();

        Console.WriteLine("Initialization Complete.");
        Console.WriteLine($"Map Size: {_map.Width}x{_map.Height}");
        Console.WriteLine($"Seed: {_mapOptions.Seed}");
        Console.Out.Flush();

        // Define test phases
        var phases = new[]
        {
            new { DurationYears = 100, Speed = 64.0f }, // Fast forward 100 years
            new { DurationYears = 50, Speed = 32.0f },  // Slow down a bit
            new { DurationYears = 10, Speed = 1.0f }    // Detailed observation
        };

        foreach (var phase in phases)
        {
            Console.WriteLine($"\n--- Starting Phase: Speed {phase.Speed}x for {phase.DurationYears} years ---");
            Console.Out.Flush();
            RunPhase(phase.DurationYears, phase.Speed);
        }

        Console.WriteLine("\nSimulation Complete.");
        Console.Out.Flush();
    }

    private void Initialize()
    {
        // Initialize map generation options
        _mapOptions = new MapGenerationOptions
        {
            Seed = 12345,
            LandRatio = 0.29f,
            MountainLevel = 0.6f,
            WaterLevel = 0.0f,
            Octaves = 6,
            Persistence = 0.5f,
            Lacunarity = 2.0f
        };

        Console.WriteLine("Generating Planet Map...");
        Console.Out.Flush();
        _map = new PlanetMap(240, 120, _mapOptions);

        // Initialize simulators
        Console.WriteLine("Initializing Simulators...");
        Console.Out.Flush();
        _climateSimulator = new ClimateSimulator(_map);
        _atmosphereSimulator = new AtmosphereSimulator(_map);
        _lifeSimulator = new LifeSimulator(_map);
        _animalEvolutionSimulator = new AnimalEvolutionSimulator(_map, _mapOptions.Seed);
        _geologicalSimulator = new GeologicalSimulator(_map, _mapOptions.Seed);
        _hydrologySimulator = new HydrologySimulator(_map, _mapOptions.Seed);
        _weatherSystem = new WeatherSystem(_map, _mapOptions.Seed);
        _civilizationManager = new CivilizationManager(_map, _mapOptions.Seed);
        _civilizationManager.SetWeatherSystem(_weatherSystem);
        _biomeSimulator = new BiomeSimulator(_map, _mapOptions.Seed);
        _disasterManager = new DisasterManager(_map, _geologicalSimulator, _mapOptions.Seed);
        _civilizationManager.SetDisasterManager(_disasterManager);
        _forestFireManager = new ForestFireManager(_map, _mapOptions.Seed);
        _magnetosphereSimulator = new MagnetosphereSimulator(_map, _mapOptions.Seed);
        _planetStabilizer = new PlanetStabilizer(_map, _magnetosphereSimulator);
        _diseaseManager = new DiseaseManager(_map, _civilizationManager, _mapOptions.Seed);

        _updateManager = new UpdateManager(_map, _climateSimulator, _atmosphereSimulator, _lifeSimulator,
            _animalEvolutionSimulator, _geologicalSimulator, _hydrologySimulator, _weatherSystem,
            _civilizationManager, _biomeSimulator, _disasterManager, _forestFireManager,
            _magnetosphereSimulator, _planetStabilizer, _diseaseManager);

        // Generate initial geological features
        EarthquakeSystem.GenerateInitialFaults(_map);

        // Seed initial life
        Console.WriteLine("Seeding Life...");
        Console.Out.Flush();
        _lifeSimulator.SeedInitialLife();

        // Manually seed a civilization to test mechanics
        SeedCivilization();

        _lifeSimulator.ActivatePlantingGracePeriod();
        _planetStabilizer.ActivateEmergencyLifeProtection();

        _year = 0;
    }

    private void SeedCivilization()
    {
        Console.WriteLine("Attempting to seed a test civilization...");
        Console.Out.Flush();
        // Find a suitable land spot
        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                var cell = _map.Cells[x, y];
                if (cell.IsLand && cell.Temperature > 10 && cell.Temperature < 30 && cell.Rainfall > 0.3f)
                {
                    if (_civilizationManager.TryCreateCivilizationAt(x, y, 0))
                    {
                        Console.WriteLine($"Civilization created at {x}, {y}");
                        Console.Out.Flush();
                        return;
                    }
                }
            }
        }
        Console.WriteLine("Could not find suitable location for civilization.");
        Console.Out.Flush();
    }

    private void RunPhase(int durationYears, float speed)
    {
        int startYear = _year;
        int targetYear = startYear + durationYears;
        float fixedDeltaTime = 0.016f; // 60 FPS simulation step

        _timeSpeed = speed;
        int logInterval = 10; // Log every 10 years for high speed
        if (speed <= 1.0f) logInterval = 1;

        int lastLogYear = _year;

        Stopwatch sw = Stopwatch.StartNew();

        while (_year < targetYear)
        {
            // Simulation step
            float simDeltaTime = fixedDeltaTime * _timeSpeed;

            _timeAccumulator += simDeltaTime;

            while (_timeAccumulator >= SecondsPerGameYear)
            {
                _year++;
                _timeAccumulator -= SecondsPerGameYear;
            }

            _updateManager.Update(simDeltaTime, _year, _timeSpeed);

            // Check for NaNs
            if (float.IsNaN(_map.GlobalTemperature))
            {
                Console.WriteLine("ERROR: Global Temperature is NaN!");
                Console.Out.Flush();
                Environment.Exit(1);
            }

            // Logging
            if (_year > lastLogYear && (_year - lastLogYear) >= logInterval)
            {
                UpdateGlobalStats(); // Refresh stats in map
                LogStatus();
                lastLogYear = _year;

                ValidateParameters();
                Console.Out.Flush();
            }
        }

        sw.Stop();
        Console.WriteLine($"Phase finished in {sw.Elapsed.TotalSeconds:F2}s real time.");
        Console.Out.Flush();
    }

    private void ValidateParameters()
    {
        if (_map.GlobalTemperature > 100f || _map.GlobalTemperature < -100f)
        {
            Console.WriteLine($"WARNING: Extreme Global Temperature: {_map.GlobalTemperature:F1}C");
        }

        if (_map.GlobalOxygen < 0f)
        {
             Console.WriteLine($"WARNING: Negative Oxygen: {_map.GlobalOxygen:F1}%");
        }
    }

    private void UpdateGlobalStats()
    {
        float totalTemp = 0;
        float totalO2 = 0;
        float totalCO2 = 0;
        int count = 0;
        int lifeCount = 0;
        int civCount = 0;

        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                var cell = _map.Cells[x, y];
                totalTemp += cell.Temperature;
                totalO2 += cell.Oxygen;
                totalCO2 += cell.CO2;
                count++;

                if (cell.LifeType != LifeForm.None) lifeCount++;
                if (cell.LifeType == LifeForm.Civilization) civCount++;
            }
        }

        _map.GlobalTemperature = totalTemp / count;
        _map.GlobalOxygen = totalO2 / count;
        _map.GlobalCO2 = totalCO2 / count;
    }

    private void LogStatus()
    {
        int civCount = _civilizationManager.Civilizations.Count;
        int totalPop = _civilizationManager.Civilizations.Sum(c => c.Population);

        // Count life cells
        int lifeCells = 0;
        int totalCells = _map.Width * _map.Height;
        for(int x=0; x<_map.Width; x++)
             for(int y=0; y<_map.Height; y++)
                 if(_map.Cells[x,y].LifeType != LifeForm.None) lifeCells++;

        Console.WriteLine($"Year: {_year} | Speed: {_timeSpeed}x | " +
                          $"Temp: {_map.GlobalTemperature:F1}C | O2: {_map.GlobalOxygen:F1}% | CO2: {_map.GlobalCO2:F2}% | " +
                          $"Life: {lifeCells} ({lifeCells/(float)totalCells*100:F1}%) | " +
                          $"Civs: {civCount} (Pop: {totalPop}) | Stabilizer: {_planetStabilizer.LastAction}");
    }
}
