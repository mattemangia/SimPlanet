using System;
using System.Threading;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;

namespace SimPlanet;

public class HeadlessSimulation
{
    // Core systems
    private PlanetMap _map = null!;
    private ClimateSimulator _climateSimulator = null!;
    private AtmosphereSimulator _atmosphereSimulator = null!;
    private LifeSimulator _lifeSimulator = null!;
    private AnimalEvolutionSimulator _animalEvolutionSimulator = null!;
    private GeologicalSimulator _geologicalSimulator = null!;
    private HydrologySimulator _hydrologySimulator = null!;
    private WeatherSystem _weatherSystem = null!;
    private CivilizationManager _civilizationManager = null!;
    private BiomeSimulator _biomeSimulator = null!;
    private DisasterManager _disasterManager = null!;
    private ForestFireManager _forestFireManager = null!;
    private MagnetosphereSimulator _magnetosphereSimulator = null!;
    private PlanetStabilizer _planetStabilizer = null!;
    private DiseaseManager _diseaseManager = null!;
    private EcosystemSimulator _ecosystemSimulator = null!;
    private UpdateManager _updateManager = null!;

    // Map generation settings
    private MapGenerationOptions _mapOptions = null!;

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

        // Initialize non-nullable fields to null! before Initialize is called
        // They will be properly set in Initialize()
        _map = null!;
        _climateSimulator = null!;
        _atmosphereSimulator = null!;
        _lifeSimulator = null!;
        _animalEvolutionSimulator = null!;
        _geologicalSimulator = null!;
        _hydrologySimulator = null!;
        _weatherSystem = null!;
        _civilizationManager = null!;
        _biomeSimulator = null!;
        _disasterManager = null!;
        _forestFireManager = null!;
        _magnetosphereSimulator = null!;
        _planetStabilizer = null!;
        _diseaseManager = null!;
        _ecosystemSimulator = null!;
        _updateManager = null!;
        _mapOptions = null!;

        ParseArguments(args);
        CivilizationManager.LogChronicleToConsole = true;
        Initialize();

        Console.WriteLine("Initialization Complete.");
        Console.WriteLine($"Map Size: {_map.Width}x{_map.Height}");
        Console.WriteLine($"Seed: {_mapOptions.Seed}");
        Console.Out.Flush();

        // Define test phases
        var phases = _years > 0
            ? new[] { new { DurationYears = _years, Speed = 64.0f } }
            : new[]
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

    // Command line options: --years N, --civs N, --size WxH, --seed N
    private int _years = 0;
    private int _civCount = 1;
    private int _mapWidth = 512;
    private int _mapHeight = 256;
    private int _seed = 12345;
    private bool _civOnly = false; // --civ-only: skip planetary physics to quickly test societies
    private int _doomsdayYear = -1; // --doomsday N: global nuclear war in year N
    private int _peaceYear = -1;    // --peace N: divine world peace in year N
    private int _startTech = 0;     // --start-tech N: seeded civilizations start at this tech level

    private void ParseArguments(string[] args)
    {
        _civOnly = args.Contains("--civ-only");
        for (int i = 0; i < args.Length - 1; i++)
        {
            switch (args[i])
            {
                case "--years": int.TryParse(args[i + 1], out _years); break;
                case "--civs": int.TryParse(args[i + 1], out _civCount); break;
                case "--seed": int.TryParse(args[i + 1], out _seed); break;
                case "--doomsday": int.TryParse(args[i + 1], out _doomsdayYear); break;
                case "--peace": int.TryParse(args[i + 1], out _peaceYear); break;
                case "--start-tech": int.TryParse(args[i + 1], out _startTech); break;
                case "--size":
                    var parts = args[i + 1].Split('x');
                    if (parts.Length == 2 && int.TryParse(parts[0], out int w) && int.TryParse(parts[1], out int h))
                    {
                        _mapWidth = w;
                        _mapHeight = h;
                    }
                    break;
            }
        }
    }

    private void Initialize()
    {
        // Initialize map generation options
        _mapOptions = new MapGenerationOptions
        {
            Seed = _seed,
            MapWidth = _mapWidth,
            MapHeight = _mapHeight,
            LandRatio = 0.29f,
            MountainLevel = 0.6f,
            WaterLevel = 0.0f,
            Octaves = 6,
            Persistence = 0.5f,
            Lacunarity = 2.0f
        };

        Console.WriteLine($"Generating Planet Map ({_mapOptions.MapWidth}x{_mapOptions.MapHeight})...");
        Console.Out.Flush();
        _map = new PlanetMap(_mapOptions.MapWidth, _mapOptions.MapHeight, _mapOptions);

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
        _civilizationManager.SetDiseaseManager(_diseaseManager);
        _ecosystemSimulator = new EcosystemSimulator(_map, _animalEvolutionSimulator, _civilizationManager, _mapOptions.Seed);

        _updateManager = new UpdateManager(_map, _climateSimulator, _atmosphereSimulator, _lifeSimulator,
            _animalEvolutionSimulator, _geologicalSimulator, _hydrologySimulator, _weatherSystem,
            _civilizationManager, _biomeSimulator, _disasterManager, _forestFireManager,
            _magnetosphereSimulator, _planetStabilizer, _diseaseManager, _ecosystemSimulator);

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
        Console.WriteLine($"Attempting to seed {_civCount} test civilization(s)...");
        Console.Out.Flush();

        // Find suitable land spots spread across the map
        var random = new Random(_seed);
        int created = 0;
        for (int attempt = 0; attempt < 20000 && created < _civCount; attempt++)
        {
            int x = random.Next(_map.Width);
            int y = random.Next(_map.Height / 8, _map.Height * 7 / 8);
            var cell = _map.Cells[x, y];
            if (cell.IsLand && cell.Temperature > 5 && cell.Temperature < 32 && cell.Rainfall > 0.25f)
            {
                if (_civilizationManager.TryCreateCivilizationAt(x, y, 0))
                {
                    Console.WriteLine($"Civilization created at {x}, {y}");
                    created++;
                }
            }
        }

        if (_startTech > 0)
        {
            foreach (var civ in _civilizationManager.Civilizations)
            {
                civ.TechLevel = _startTech;
                civ.CivType = _startTech > 100 ? CivType.Spacefaring : _startTech > 60 ? CivType.Scientific
                    : _startTech > 30 ? CivType.Industrial : _startTech > 10 ? CivType.Agricultural : CivType.Tribal;
                civ.HasLandTransport = _startTech >= 5;
                civ.HasSeaTransport = _startTech >= 15;
                civ.HasRailTransport = _startTech >= 25;
                civ.HasAirTransport = _startTech >= 50;
            }
        }

        if (created == 0)
        {
            Console.WriteLine("Could not find suitable location for civilization.");
        }
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

            if (_year == _peaceYear)
            {
                _peaceYear = -1;
                _civilizationManager.TriggerWorldPeace(_year);
            }

            if (_year == _doomsdayYear)
            {
                _doomsdayYear = -1;
                _civilizationManager.TriggerGlobalNuclearWar(_year);
            }

            if (_civOnly)
            {
                _civilizationManager.Update(simDeltaTime, _year);
                _diseaseManager.Update(simDeltaTime, _year);
                if (_year != _lastLifeYear)
                {
                    // Vegetation regrows once per year so hunting grounds recover
                    _lastLifeYear = _year;
                    RegrowVegetation();
                }
            }
            else
            {
                _updateManager.Update(simDeltaTime, _year, _timeSpeed);
            }

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

    private int _lastLifeYear = -1;

    private void RegrowVegetation()
    {
        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                var cell = _map.Cells[x, y];
                if (cell.IsLand && cell.Rainfall > 0.2f && cell.Temperature > 0)
                {
                    cell.Biomass = Math.Min(1f, cell.Biomass + 0.02f * cell.Rainfall);
                }
                if (cell.Temperature > 45f)
                {
                    cell.Temperature = 45f; // Stand-in for climate relaxation after blasts and fires
                }
                if (cell.CO2 > 0.5f)
                {
                    cell.CO2 *= 0.5f; // Stand-in for atmospheric mixing
                }
            }
        }
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

        foreach (var civ in _civilizationManager.Civilizations)
        {
            int soldiers = _civilizationManager.GetArmies().Where(a => a.CivilizationId == civ.Id).Sum(a => a.Soldiers);
            var types = string.Join(",", civ.Cities.GroupBy(c => c.Type).OrderBy(g => g.Key).Select(g => $"{g.Count()}{g.Key.ToString()[0]}"));
            Console.WriteLine($"   {civ.Name,-24} pop {civ.Population,8:N0} | settlements {civ.Cities.Count,2} ({types}) | tech {civ.TechLevel,3} {civ.CivType,-12} | " +
                              $"food {civ.Food,6:F0} (+{civ.FoodProduction:F0}/-{civ.FoodConsumption:F0}) gold {civ.Gold,6:F0} | land {civ.Territory.Count,4} | " +
                              $"{(civ.AtWar ? "WAR" : "peace")} soldiers {soldiers} weary {civ.WarWeariness:F2} stab {civ.Stability:F2}");
            var capital = civ.Capital;
            if (capital != null)
            {
                Console.WriteLine($"      capital {capital.Name}: pop {capital.Population} cap {capital.Capacity} happy {capital.Happiness:F2} buildings [{capital.Buildings}]");
            }
            var mix = civ.EnergyMix.Count == 0 ? "-" : string.Join(" ", civ.EnergyMix.Where(e => e.Value > 0.05f).OrderByDescending(e => e.Value).Select(e => $"{e.Key}{e.Value:P0}"));
            Console.WriteLine($"      {civ.Homeland} x{civ.DevelopmentModifier:F2} | {civ.Government?.Type} {civ.RulingParty} | posture {civ.Strategy.Posture} ({civ.Strategy.Rationale}) threat {civ.Strategy.ThreatLevel:F2}");
            Console.WriteLine($"      project {civ.ActiveProject?.Name ?? "-"} {civ.ActiveProject?.Fraction ?? 0:P0} | done {civ.CompletedProjects.Count} | space {civ.SpaceStage} sats {civ.Satellites} astronauts {civ.Astronauts} | nukes {civ.NuclearStockpile} | grid {civ.Electrification:P0} net {civ.InternetPenetration:P0} | energy {mix}");
            if (civ.SuccessionLine.Count > 0)
            {
                Console.WriteLine($"      heir {civ.HeirApparent?.Name} ({civ.HeirApparent?.Age}) line {civ.SuccessionLine.Count}");
            }
        }

        float ch4 = 0, n2o = 0, gh = 0, landTemp = 0; int landCells = 0, cells = 0;
        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                var c = _map.Cells[x, y];
                ch4 += c.Methane; n2o += c.NitrousOxide; gh += c.Greenhouse; cells++;
                if (c.IsLand) { landTemp += c.Temperature; landCells++; }
            }
        }
        Console.WriteLine($"   Climate: CH4 {ch4 / cells:F3} N2O {n2o / cells:F3} greenhouse {gh / cells:F3} solar {_map.SolarEnergy:F3} land temp {landTemp / Math.Max(1, landCells):F1}C");
        Console.WriteLine($"   People in space: {_civilizationManager.PeopleInSpace} | Nuclear winter: {_civilizationManager.NuclearWinter:F2} | Active diseases: {_diseaseManager.Diseases.Count(d => d.IsActive && !d.CureDeployed)}");
        Console.WriteLine($"Year: {_year} | Speed: {_timeSpeed}x | " +
                          $"Temp: {_map.GlobalTemperature:F1}C | O2: {_map.GlobalOxygen:F1}% | CO2: {_map.GlobalCO2:F2}% | " +
                          $"Life: {lifeCells} ({lifeCells/(float)totalCells*100:F1}%) | " +
                          $"Civs: {civCount} (Pop: {totalPop}) | Stabilizer: {_planetStabilizer.LastAction}");
    }
}
