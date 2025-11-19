using System;
using System.Threading.Tasks;

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
        private readonly PlanetMap _map;

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
            DiseaseManager diseaseManager)
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
        }

        public void Update(float simDeltaTime, int newYear, float timeSpeed)
        {
            // Stage 1: Core physics and geology simulators. These have few dependencies on each other.
            var stage1Tasks = new Task[]
            {
                Task.Run(() => _climateSimulator.Update(simDeltaTime)),
                Task.Run(() => _atmosphereSimulator.Update(simDeltaTime)),
                Task.Run(() => _geologicalSimulator.Update(simDeltaTime, newYear)),
                Task.Run(() => _hydrologySimulator.Update(simDeltaTime)),
                Task.Run(() => _magnetosphereSimulator.Update(simDeltaTime, newYear)),
                Task.Run(() => _animalEvolutionSimulator.Update(simDeltaTime, newYear)),
                Task.Run(() => _biomeSimulator.Update(simDeltaTime))
            };
            Task.WhenAll(stage1Tasks).Wait();

            // Stage 2: Systems that depend on the core physics.
            var stage2Tasks = new Task[]
            {
                Task.Run(() => _weatherSystem.Update(simDeltaTime, newYear)),
                Task.Run(() => _civilizationManager.Update(simDeltaTime, newYear)),
                Task.Run(() => _disasterManager.Update(simDeltaTime, newYear))
            };
            Task.WhenAll(stage2Tasks).Wait();

            // Stage 3: Systems that depend on weather and civilization.
            var stage3Tasks = new Task[]
            {
                Task.Run(() => _lifeSimulator.Update(simDeltaTime, _geologicalSimulator, _weatherSystem)),
                Task.Run(() => _diseaseManager.Update(simDeltaTime, newYear)),
                Task.Run(() => _forestFireManager.Update(simDeltaTime, _weatherSystem, _civilizationManager))
            };
            Task.WhenAll(stage3Tasks).Wait();

            // Stage 4: Finalizers and special systems.
            _planetStabilizer.Update(simDeltaTime, timeSpeed);

            EarthquakeSystem.Update(_map, simDeltaTime, newYear, out bool tsunamiTriggered, out var tsunamiEpicenter, out float tsunamiMagnitude);
            if (tsunamiTriggered)
            {
                TsunamiSystem.InitiateTsunami(_map, tsunamiEpicenter.x, tsunamiEpicenter.y, tsunamiMagnitude, newYear);
            }

            TsunamiSystem.Update(_map, simDeltaTime, newYear);
            TsunamiSystem.DrainFloodWaters(_map, simDeltaTime);
        }

        public async Task FastForward(int years, int startYear, Action<float, int> onProgress)
        {
            await Task.Run(() =>
            {
                float simDeltaTime = 1.0f;
                for (int i = 0; i < years; i++)
                {
                    int currentYear = startYear + i;
                    Update(simDeltaTime, currentYear, 32.0f);
                    onProgress?.Invoke((float)(i + 1) / years, currentYear + 1);
                }
            });
        }
    }
}
