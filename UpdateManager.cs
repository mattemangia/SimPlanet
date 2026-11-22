using System;
using System.Threading.Tasks;
using System.Threading;

namespace SimPlanet
{
    public class UpdateManager
    {
        private readonly ClimateSimulator _climateSimulator;
        private readonly AtmosphereSimulator _atmosphereSimulator;
        private readonly LifeSimulator _lifeSimulator;
        private readonly AnimalEvolutionSimulator _animalEvolutionSimulator;
        private readonly GeologicalSimulator _geologicalSimulator;
        private readonly HydrologySimulator _hydrologySimulator;
        private readonly WeatherSystem _weatherSystem;
        private readonly CivilizationManager _civilizationManager;
        private readonly BiomeSimulator _biomeSimulator;
        private readonly DisasterManager _disasterManager;
        private readonly ForestFireManager _forestFireManager;
        private readonly MagnetosphereSimulator _magnetosphereSimulator;
        private readonly PlanetStabilizer _planetStabilizer;
        private readonly DiseaseManager _diseaseManager;
        private readonly EcosystemSimulator _ecosystemSimulator;
        private readonly SimulationInterconnect _interconnect;
        private readonly PlanetMap _map;
        private int _lastNetworkYear = int.MinValue;

        /// <summary>Oil and gas fields, seismic surveys, wells and platforms.</summary>
        public PetroleumSystem Petroleum { get; }

        /// <summary>Geothermal prospects and power plants.</summary>
        public GeothermalSystem Geothermal { get; }

        /// <summary>Seismograph networks, wave propagation and tomography.</summary>
        public SeismologyManager Seismology { get; }

        public UpdateManager(
            PlanetMap map,
            ClimateSimulator climateSimulator,
            AtmosphereSimulator atmosphereSimulator,
            LifeSimulator lifeSimulator,
            AnimalEvolutionSimulator animalEvolutionSimulator,
            GeologicalSimulator geologicalSimulator,
            HydrologySimulator hydrologySimulator,
            WeatherSystem weatherSystem,
            CivilizationManager civilizationManager,
            BiomeSimulator biomeSimulator,
            DisasterManager disasterManager,
            ForestFireManager forestFireManager,
            MagnetosphereSimulator magnetosphereSimulator,
            PlanetStabilizer planetStabilizer,
            DiseaseManager diseaseManager,
            EcosystemSimulator ecosystemSimulator)
        {
            _map = map;
            _climateSimulator = climateSimulator;
            _atmosphereSimulator = atmosphereSimulator;
            _lifeSimulator = lifeSimulator;
            _animalEvolutionSimulator = animalEvolutionSimulator;
            _geologicalSimulator = geologicalSimulator;
            _hydrologySimulator = hydrologySimulator;
            _weatherSystem = weatherSystem;
            _civilizationManager = civilizationManager;
            _biomeSimulator = biomeSimulator;
            _disasterManager = disasterManager;
            _forestFireManager = forestFireManager;
            _magnetosphereSimulator = magnetosphereSimulator;
            _planetStabilizer = planetStabilizer;
            _diseaseManager = diseaseManager;
            _ecosystemSimulator = ecosystemSimulator;
            _interconnect = new SimulationInterconnect(map);
            _planetStabilizer.IsClaimed = _civilizationManager.IsClaimed;
            Petroleum = new PetroleumSystem(map, map.Width * 31 + map.Height);
            Petroleum.Chronicle = (year, text, x, y, civId) =>
                _civilizationManager.RecordHistory(year, HistoryCategory.Science, text, x, y, civId);
            Geothermal = new GeothermalSystem(map, map.Width * 17 + map.Height);
            Geothermal.Chronicle = Petroleum.Chronicle;
            Seismology = new SeismologyManager(map);
        }

        // Slow planetary processes do not need every tick: each runs on its own beat with the
        // time that has passed since its last turn, and the beats are offset so the heavy ones
        // do not land on the same tick.
        private readonly float[] _pending = new float[8];
        private long _tick;
        private static readonly bool EveryTick = Environment.GetEnvironmentVariable("SIMPLANET_EVERY_TICK") == "1";
        private const float LargestStep = 3.3f; // The simulators are already run with steps this large at 32x

        private bool Due(int slot, int period, int phase, float simDeltaTime, out float deltaTime)
        {
            float pending = _pending[slot] + simDeltaTime;
            bool run = EveryTick || (_tick + phase) % period == 0 || pending + simDeltaTime > LargestStep;
            deltaTime = pending;
            _pending[slot] = run ? 0f : pending;
            return run;
        }

        public void Update(float simDeltaTime, int newYear, float timeSpeed)
        {
            _tick++;
            // Stage 1: Core physics and geology simulators. These have few dependencies on each other.
            var stage1Tasks = new Task[]
            {
                Task.Run(() => { if (!Due(0, 2, 0, simDeltaTime, out float dt)) return; long t = Perf.Start(); _climateSimulator.Update(dt); Perf.Stop("sim:climateSimulator", t); }),
                Task.Run(() => { if (!Due(1, 3, 1, simDeltaTime, out float dt)) return; long t = Perf.Start(); _atmosphereSimulator.Update(dt); Perf.Stop("sim:atmosphereSimulator", t); }),
                Task.Run(() => { if (!Due(6, 2, 1, simDeltaTime, out float dt)) return; long t = Perf.Start(); _geologicalSimulator.Update(dt, newYear); Perf.Stop("sim:geologicalSimulator", t); }),
                Task.Run(() => { if (!Due(2, 3, 0, simDeltaTime, out float dt)) return; long t = Perf.Start(); _hydrologySimulator.Update(dt); Perf.Stop("sim:hydrologySimulator", t); }),
                Task.Run(() => { if (!Due(3, 4, 2, simDeltaTime, out float dt)) return; long t = Perf.Start(); _magnetosphereSimulator.Update(dt, newYear); Perf.Stop("sim:magnetosphereSimulator", t); }),
                Task.Run(() => { long t = Perf.Start(); _animalEvolutionSimulator.Update(simDeltaTime, newYear); Perf.Stop("sim:animalEvolutionSimulator", t); }),
                Task.Run(() => { if (!Due(4, 4, 3, simDeltaTime, out float dt)) return; long t = Perf.Start(); _biomeSimulator.Update(dt); Perf.Stop("sim:biomeSimulator", t); })
            };
            Task.WhenAll(stage1Tasks).Wait();

            // Stage 2: Systems that depend on the core physics.
            var stage2Tasks = new Task[]
            {
                Task.Run(() => { if (!Due(5, 3, 2, simDeltaTime, out float dt)) return; long t = Perf.Start(); _weatherSystem.Update(dt, newYear); Perf.Stop("sim:weatherSystem", t); }),
                Task.Run(() => { long t = Perf.Start(); _civilizationManager.Update(simDeltaTime, newYear); Perf.Stop("sim:civilizationManager", t); }),
                Task.Run(() => { long t = Perf.Start(); _disasterManager.Update(simDeltaTime, newYear); Perf.Stop("sim:disasterManager", t); })
            };
            Task.WhenAll(stage2Tasks).Wait();

            // Oil exploration and seismograph networks follow the civilizations
            long geoStart = Perf.Start();
            Petroleum.Update(simDeltaTime, newYear, _civilizationManager.Civilizations);
            Geothermal.Update(simDeltaTime, newYear, _civilizationManager.Civilizations);
            if (newYear != _lastNetworkYear)
            {
                _lastNetworkYear = newYear;
                Seismology.UpdateNetworks(_civilizationManager.Civilizations, newYear);
            }

            Perf.Stop("sim:geoscience", geoStart);

            // Stage 3: Systems that depend on weather and civilization.
            var stage3Tasks = new Task[]
            {
                Task.Run(() => { long t = Perf.Start(); _lifeSimulator.Update(simDeltaTime, _geologicalSimulator, _weatherSystem); Perf.Stop("sim:lifeSimulator", t); }),
                Task.Run(() => { long t = Perf.Start(); _diseaseManager.Update(simDeltaTime, newYear); Perf.Stop("sim:diseaseManager", t); }),
                Task.Run(() => { long t = Perf.Start(); _forestFireManager.Update(simDeltaTime, _weatherSystem, _civilizationManager); Perf.Stop("sim:forestFireManager", t); })
            };
            Task.WhenAll(stage3Tasks).Wait();

            // Stage 3.5: Ecosystem interactions (must run after LifeSimulator to avoid race conditions)
            // EcosystemSimulator modifies biomass and neighbors, so it should run sequentially
            { long t = Perf.Start(); _ecosystemSimulator.Update(simDeltaTime); Perf.Stop("sim:ecosystem", t); }

            // Stage 4: Finalizers and special systems.
            { long t = Perf.Start(); _planetStabilizer.Update(simDeltaTime, timeSpeed); Perf.Stop("sim:stabilizer", t); }

            long quakeStart = Perf.Start();
            EarthquakeSystem.Update(_map, simDeltaTime, newYear, out bool tsunamiTriggered, out var tsunamiEpicenter, out float tsunamiMagnitude);
            Perf.Stop("sim:earthquakes", quakeStart);
            if (tsunamiTriggered)
            {
                TsunamiSystem.InitiateTsunami(_map, tsunamiEpicenter.x, tsunamiEpicenter.y, tsunamiMagnitude, newYear);
            }

            { long t = Perf.Start(); TsunamiSystem.Update(_map, simDeltaTime, newYear); Perf.Stop("sim:tsunami", t); }
            { long t = Perf.Start(); TsunamiSystem.DrainFloodWaters(_map, simDeltaTime); Perf.Stop("sim:flooddrain", t); }

            // Bind systems together so gas cycles, hydrology, geology, and biosphere feed each other.
            { long t = Perf.Start(); _interconnect.Update(simDeltaTime, newYear); Perf.Stop("sim:interconnect", t); }
        }

        public async Task FastForward(int years, int startYear, Action<float, int> onProgress, CancellationToken cancellationToken)
        {
            await Task.Run(() =>
            {
                float simDeltaTime = 1.0f;
                for (int i = 0; i < years; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    int currentYear = startYear + i;
                    Update(simDeltaTime, currentYear, 32.0f);
                    onProgress?.Invoke((float)(i + 1) / years, currentYear + 1);
                }
            }, cancellationToken);
        }
    }
}
