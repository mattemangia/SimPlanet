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

        if (_seismoTest)
        {
            RunSeismologyTest();
            return;
        }

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

        if (_saveTest) RunSaveTest();

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
    private int _startTech = 0;
    private bool _geoOnly = false;    // --geo-only: only tectonics, volcanism, erosion and earthquakes (fast)
    private bool _seismoTest = false; // --seismo-test: check wave propagation, picks, location and tomography
    private bool _saveTest = false;   // --save-test: after the run, save, reload into a fresh world and compare
    private bool _geoReport = false; // --geo-report: print tectonic / seismic / volcanic statistics     // --start-tech N: seeded civilizations start at this tech level

    private void ParseArguments(string[] args)
    {
        _civOnly = args.Contains("--civ-only");
        _geoReport = args.Contains("--geo-report");
        _geoOnly = args.Contains("--geo-only");
        _seismoTest = args.Contains("--seismo-test");
        _saveTest = args.Contains("--save-test");
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
        _civilizationManager.SetGeologicalSimulator(_geologicalSimulator);
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

            if (_geoOnly)
            {
                _geologicalSimulator.Update(simDeltaTime, _year);
                EarthquakeSystem.Update(_map, simDeltaTime, _year, out _, out _, out _);
            }
            else if (_civOnly)
            {
                _civilizationManager.Update(simDeltaTime, _year);
                _diseaseManager.Update(simDeltaTime, _year);
                _updateManager.Petroleum.Update(simDeltaTime, _year, _civilizationManager.Civilizations);
                _updateManager.Geothermal.Update(simDeltaTime, _year, _civilizationManager.Civilizations);
                if (_year != _lastLifeYear) _updateManager.Seismology.UpdateNetworks(_civilizationManager.Civilizations, _year);
                if (_year != _lastLifeYear)
                {
                    // Vegetation regrows once per year so hunting grounds recover
                    _lastLifeYear = _year;
                    RegrowVegetation();
                }
            }
            else
            {
                long tick = Perf.Start();
                _updateManager.Update(simDeltaTime, _year, _timeSpeed);
                Perf.Stop("sim-tick", tick);
                Perf.Report();
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
                if (_geoReport) LogGeology(); else LogStatus();
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
        var allCities = _civilizationManager.Civilizations.SelectMany(c => c.Cities).ToList();
        var routes = _civilizationManager.Civilizations.SelectMany(c => c.TransportRoutes).ToList();
        Console.WriteLine($"   Cities by role: {string.Join(" ", allCities.GroupBy(c => c.Specialization).OrderByDescending(g => g.Count()).Select(g => $"{g.Key}:{g.Count()}"))}");
        Console.WriteLine($"   Cities by style: {string.Join(" ", allCities.GroupBy(c => c.Style).OrderByDescending(g => g.Count()).Select(g => $"{g.Key}:{g.Count()}"))}");
        Console.WriteLine($"   Routes: {string.Join(" ", routes.GroupBy(r => r.Kind).Select(g => $"{g.Key}:{g.Count()}"))} (international {routes.Count(r => r.International)}) | vehicles: {string.Join(" ", _civilizationManager.GetVehicles().GroupBy(v => v.Kind).Select(g => $"{g.Key}:{g.Count()}"))}");
        Console.WriteLine($"   Migration (last years): {string.Join(" ", _civilizationManager.GetMigrations().GroupBy(m => m.Kind).Select(g => $"{g.Key}:{g.Sum(m => m.People):N0}"))}");
        var spies = _civilizationManager.Civilizations.SelectMany(c => c.SpyNetworks).ToList();
        Console.WriteLine($"   Spy networks: {spies.Count} (agents {spies.Sum(n => n.Agents)}, compromised {spies.Count(n => n.Compromised)}, missions {string.Join(",", spies.GroupBy(n => n.Mission).Select(g => $"{g.Key}:{g.Count()}"))}) | animal groups: {string.Join(" ", _civilizationManager.GetAnimalGroups().GroupBy(g => g.Name).Select(g => $"{g.Key}:{g.Count()}"))}");
        var oil = _updateManager.Petroleum;
        Console.WriteLine($"   Petroleum: reservoirs {oil.Reservoirs.Count} (onshore {oil.Reservoirs.Count(r => !r.Offshore)}, gas fields in foredeeps {oil.Reservoirs.Count(r => r.HasGas && r.InForedeep)}/{oil.Reservoirs.Count(r => r.HasGas)} holding {oil.Reservoirs.Where(r => r.HasGas && r.InForedeep).Sum(r => r.InitialReserves) / Math.Max(0.001f, oil.Reservoirs.Where(r => r.HasGas).Sum(r => r.InitialReserves)):P0} of the gas, surveyed {oil.Reservoirs.Count(r => r.Surveyed)}, discovered {oil.Reservoirs.Count(r => r.Discovered)}) | surveys {oil.Surveys.Count} (active {oil.Surveys.Count(s => !s.Completed)}) | wells: {string.Join(" ", oil.Wells.GroupBy(w => (w.Offshore ? "platform-" : "well-") + w.Status).Select(g => $"{g.Key}:{g.Count()}"))} | seismographs {_updateManager.Seismology.Stations.Count} | geothermal plants {_updateManager.Geothermal.Plants.Count} (producing {_updateManager.Geothermal.Plants.Count(p => p.Producing)})");
        Console.WriteLine($"   People in space: {_civilizationManager.PeopleInSpace} | Nuclear winter: {_civilizationManager.NuclearWinter:F2} | Active diseases: {_diseaseManager.Diseases.Count(d => d.IsActive && !d.CureDeployed)}");
        Console.WriteLine($"Year: {_year} | Speed: {_timeSpeed}x | " +
                          $"Temp: {_map.GlobalTemperature:F1}C | O2: {_map.GlobalOxygen:F1}% | CO2: {_map.GlobalCO2:F2}% | " +
                          $"Life: {lifeCells} ({lifeCells/(float)totalCells*100:F1}%) | " +
                          $"Civs: {civCount} (Pop: {totalPop}) | Stabilizer: {_planetStabilizer.LastAction}");
    }

    private int _lastQuakeTotal;
    private int _lastEruptionTotal;
    private int _lastGeoYear;

    /// <summary>Geological health report: boundaries, relief, volcanoes, faults and event rates.</summary>
    private void LogGeology()
    {
        int conv = 0, div = 0, trans = 0, volcanoes = 0, faults = 0, land = 0, high = 0, cells = 0;
        float minE = float.MaxValue, maxE = float.MinValue, sumStress = 0, maxSed = 0, sumSed = 0;
        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                var c = _map.Cells[x, y];
                var g = c.Geology;
                cells++;
                if (g.BoundaryType == PlateBoundaryType.Convergent) conv++;
                else if (g.BoundaryType == PlateBoundaryType.Divergent) div++;
                else if (g.BoundaryType == PlateBoundaryType.Transform) trans++;
                if (g.IsVolcano) volcanoes++;
                if (g.IsFault) faults++;
                if (c.IsLand) land++;
                if (c.Elevation > 0.7f) high++;
                minE = Math.Min(minE, c.Elevation);
                maxE = Math.Max(maxE, c.Elevation);
                sumStress += g.SeismicStress;
                sumSed += g.SedimentLayer;
                maxSed = Math.Max(maxSed, g.SedimentLayer);
            }
        }
        int years = Math.Max(1, _year - _lastGeoYear);
        int quakes = EarthquakeSystem.TotalEarthquakes - _lastQuakeTotal;
        int eruptions = _geologicalSimulator.TotalEruptions - _lastEruptionTotal;
        _lastQuakeTotal = EarthquakeSystem.TotalEarthquakes;
        _lastEruptionTotal = _geologicalSimulator.TotalEruptions;
        _lastGeoYear = _year;
        Console.WriteLine($"GEO y{_year,5} | land {land * 100f / cells,5:F1}% elev [{minE:F2},{maxE:F2}] mountains {high * 100f / cells:F2}% | " +
                          $"boundary conv {conv} div {div} trans {trans} | volcanoes {volcanoes} faults {faults} | " +
                          $"quakes/yr {quakes / (float)years:F1} eruptions/yr {eruptions / (float)years:F2} | stress avg {sumStress / cells:F2} | sediment avg {sumSed / cells:F2} max {maxSed:F1}");
    }

    /// <summary>
    /// Exercises the seismology chain end to end and compares the compute backends.
    /// </summary>
    private void RunSeismologyTest()
    {
        ComputeBackend.Initialize();
        Console.WriteLine($"Compute backend: {ComputeBackend.Preferred} ({ComputeBackend.Description}); OpenCL available: {ComputeBackend.OpenClAvailable}");

        // Let the geology settle so there are boundaries, basins and volcanoes to image
        for (int i = 0; i < 600; i++) _geologicalSimulator.Update(1f, i / 10);

        var seismo = new SeismologyManager(_map);
        var rnd = new Random(_seed);
        while (seismo.Stations.Count < 40)
        {
            int x = rnd.Next(_map.Width), y = rnd.Next(_map.Height / 8, _map.Height * 7 / 8);
            if (_map.Cells[x, y].IsLand) seismo.AddStation(x, y, 0, 0);
        }

        // Backend agreement and speed on the real grid
        int n = _map.Width * _map.Height;
        var courant = new float[n];
        for (int i = 0; i < n; i++) courant[i] = seismo.VelocityP[i] / SeismologyManager.MaxSpeedKmS * 0.6f;
        var kinds = new List<ComputeKind> { ComputeKind.Scalar, ComputeKind.Simd };
        if (ComputeBackend.OpenClAvailable) kinds.Add(ComputeKind.OpenCL);
        float[]? reference = null;
        foreach (var kind in kinds)
        {
            using var solver = new SeismicWaveSolver(_map.Width, _map.Height, courant, kind);
            solver.Inject(_map.Width / 2, _map.Height / 2, 1f);
            var sw = Stopwatch.StartNew();
            for (int b = 0; b < 50; b++) solver.Advance(4);
            sw.Stop();
            float diff = 0f;
            if (reference == null) reference = (float[])solver.Field.Clone();
            else for (int i = 0; i < n; i++) diff = Math.Max(diff, Math.Abs(solver.Field[i] - reference[i]));
            Console.WriteLine($"  {solver.Kind,-7} 200 steps on {_map.Width}x{_map.Height}: {sw.Elapsed.TotalMilliseconds,8:F1} ms  ({n * 200 / sw.Elapsed.TotalSeconds / 1e6:F0} M cells/s)  max diff vs scalar {diff:E2}");
        }

        // A great earthquake recorded by the whole network
        var faults = new List<(int x, int y)>();
        for (int x = 0; x < _map.Width; x++)
            for (int y = 6; y < _map.Height - 6; y++)
                if (_map.Cells[x, y].Geology.BoundaryType != PlateBoundaryType.None) faults.Add((x, y));

        var first = faults[rnd.Next(faults.Count)];
        seismo.BeginEvent(first.x, first.y, 8.0f, 1);
        seismo.RunToCompletion();
        var rec = seismo.Records.Last();
        Console.WriteLine($"M8.0 at ({first.x},{first.y}): P at {rec.StationsP}/{seismo.Stations.Count} stations, S at {rec.StationsS}; located ({rec.LocatedX},{rec.LocatedY}), error {rec.LocationErrorKm:F0} km; {rec.DurationSeconds:F0} s of record");
        float sumP = 0, sumS = 0; int cnt = 0;
        foreach (var st in seismo.Stations.Where(s => s.PickP > 0 && s.PickS > 0 && s.DistanceKm > 1500).Take(8))
        {
            Console.WriteLine($"   station ({st.X,3},{st.Y,3}) {st.DistanceKm,6:F0} km  P {st.PickP,6:F0} s ({st.DistanceKm / st.PickP:F2} km/s)  S {st.PickS,6:F0} s ({st.DistanceKm / st.PickS:F2} km/s)  S-P {st.PickS - st.PickP:F0} s");
        }
        foreach (var st in seismo.Stations.Where(s => s.PickP > 0 && s.PickS > 0 && s.DistanceKm > 1500))
        {
            sumP += st.DistanceKm / st.PickP; sumS += st.DistanceKm / st.PickS; cnt++;
        }
        if (cnt > 0) Console.WriteLine($"   apparent velocities: P {sumP / cnt:F2} km/s, S {sumS / cnt:F2} km/s, ratio {sumP / sumS:F2}");

        // Many events build up the tomography
        float errSum = 0; int errCount = 0;
        for (int e = 0; e < 40; e++)
        {
            var c = faults[rnd.Next(faults.Count)];
            seismo.BeginEvent(c.x, c.y, 6.3f + (float)rnd.NextDouble() * 1.7f, 2 + e);
            seismo.RunToCompletion();
            var r = seismo.Records.Last();
            if (r.LocationErrorKm >= 0) { errSum += r.LocationErrorKm; errCount++; }
            if (e % 10 == 9)
            {
                int covered = seismo.TomoCoverage.Count(v => v > 0);
                Console.WriteLine($"   after {e + 1} events: {seismo.TomoRays} rays, {covered * 100 / seismo.TomoCoverage.Length}% of blocks crossed, reference {seismo.TomoReference:F2} km/s, correlation with true crust {seismo.TomoCorrelation:F2}");
            }
        }
        Console.WriteLine($"   mean location error {errSum / Math.Max(1, errCount):F0} km over {errCount} located events (cell = {seismo.CellKm:F0} km)");
        seismo.Dispose();
    }

    private string GeoscienceSummary()
    {
        var oil = _updateManager.Petroleum;
        long columns = 0;
        int faults = 0, unconformities = 0;
        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                var geo = _map.Cells[x, y].Geology;
                int k = 0;
                foreach (var bed in geo.SedimentColumn) columns += ((int)bed + 1) * (++k);
                if (geo.IsFault) faults++;
                if (geo.UnconformityBeds >= 0) unconformities++;
            }
        }
        return $"wells {string.Join(",", oil.Wells.GroupBy(w => w.Status).OrderBy(g => g.Key).Select(g => $"{g.Key}:{g.Count()}"))} | " +
               $"surveys {oil.Surveys.Count} (done {oil.Surveys.Count(s => s.Completed)}, with image {oil.Surveys.Count(s => s.Section.Length > 0)}) | " +
               $"fields surveyed {oil.Reservoirs.Count(r => r.Surveyed)} discovered {oil.Reservoirs.Count(r => r.Discovered)} reserves {oil.Reservoirs.Sum(r => oil.RemainingReserves(r)):F1} | " +
               $"geothermal {_updateManager.Geothermal.Plants.Count} | seismographs {_updateManager.Seismology.Stations.Count} | " +
               $"faults {faults} | erosion surfaces {unconformities} | layers checksum {columns}";
    }

    /// <summary>Saves the world, loads it into a freshly generated one and checks nothing was lost.</summary>
    private void RunSaveTest()
    {
        string before = GeoscienceSummary();
        var saves = new SaveLoadManager();
        var state = new GameState { Year = _year, TimeSpeed = 1f };
        saves.SaveGame(_map, state, _civilizationManager, _weatherSystem, _hydrologySimulator, "HeadlessSaveTest",
            data => data.Geoscience = GeoscienceSaveData.Capture(_map, _updateManager));

        _civCount = 0;
        Initialize(); // A brand new world from the same seed
        string fresh = GeoscienceSummary();

        var loaded = saves.LoadGame("HeadlessSaveTest")!;
        saves.ApplySaveData(loaded, _map, state, _civilizationManager, _weatherSystem, _hydrologySimulator);
        _geologicalSimulator.SyncPlatesFromCells();
        loaded.Geoscience?.Restore(_map, _updateManager);
        string after = GeoscienceSummary();

        Console.WriteLine($"SAVE TEST before : {before}");
        Console.WriteLine($"SAVE TEST fresh  : {fresh}");
        Console.WriteLine($"SAVE TEST loaded : {after}");
        Console.WriteLine(before == after ? "SAVE TEST PASSED: the loaded world matches the saved one" : "SAVE TEST FAILED: differences above");
    }
}
