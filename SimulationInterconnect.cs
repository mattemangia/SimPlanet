using System;
using System.Threading.Tasks;

namespace SimPlanet
{
    /// <summary>
    /// Bridges the major simulation systems so that changes in one system
    /// immediately feed energy, mass, and feedbacks into the others. This keeps
    /// the planet simulation "alive" by ensuring coupled state between
    /// atmosphere, climate, hydrology, life, and geology.
    /// </summary>
    public class SimulationInterconnect
    {
        private readonly PlanetMap _map;

        public SimulationInterconnect(PlanetMap map)
        {
            _map = map;
        }

        public void Update(float deltaTime, int currentYear)
        {
            SyncGreenhouseAndAlbedo();
            BlendHydrologyAndWeather(deltaTime);
            FeedBiosphereIntoAtmosphere(currentYear, deltaTime);
            FeedGeologyIntoHydrologyAndWeather(deltaTime);
        }

        /// <summary>
        /// Convert atmospheric composition and surface state into greenhouse and
        /// albedo signals used by the climate simulator. This allows atmospheric
        /// changes (gases, humidity) and biosphere growth/ice cover to affect
        /// temperature and precipitation patterns in subsequent steps.
        /// </summary>
        private void SyncGreenhouseAndAlbedo()
        {
            Parallel.For(0, _map.Width, x =>
            {
                for (int y = 0; y < _map.Height; y++)
                {
                    var cell = _map.Cells[x, y];
                    var met = cell.GetMeteorology();

                    // Greenhouse forcing includes long-lived gases and local humidity
                    // so hydrology/atmosphere changes warm the climate.
                    float greenhouse = 0.2f;
                    greenhouse += cell.CO2 * 0.01f;
                    greenhouse += cell.Methane * 0.02f;
                    greenhouse += cell.NitrousOxide * 0.015f;
                    greenhouse += cell.Humidity * 0.25f; // water vapor feedback
                    greenhouse += met.CloudCover * 0.1f;
                    cell.Greenhouse = Math.Clamp(greenhouse, 0f, 3f);

                    // Albedo responds to ice, water, and biomass so climate reacts
                    // to hydrology/biome shifts.
                    float albedo = cell.IsWater ? 0.06f : 0.28f;
                    if (cell.IsIce)
                    {
                        albedo = 0.6f;
                    }
                    else if (cell.Biomass > 0.2f)
                    {
                        // Dense vegetation darkens the surface slightly
                        albedo -= Math.Clamp(cell.Biomass * 0.1f, 0f, 0.12f);
                    }

                    // Wet surfaces darken slightly, creating a hydrology→climate link.
                    albedo -= cell.GetGeology().SoilMoisture * 0.05f;
                    cell.Albedo = Math.Clamp(albedo, 0.04f, 0.85f);
                }
            });
        }

        /// <summary>
        /// Push soil moisture, flooding, and ocean evaporation back into the
        /// meteorology so weather reacts to hydrology (e.g., moist ground fuels
        /// rainfall, tsunamis/floods seed humidity, drought dries the air).
        /// </summary>
        private void BlendHydrologyAndWeather(float deltaTime)
        {
            Parallel.For(0, _map.Width, x =>
            {
                for (int y = 0; y < _map.Height; y++)
                {
                    var cell = _map.Cells[x, y];
                    var geo = cell.GetGeology();
                    var met = cell.GetMeteorology();

                    // Soil moisture and standing water contribute to atmospheric water vapor.
                    float moistureSignal = (geo.SoilMoisture * 0.5f) + (geo.AccumulatedWater * 0.2f) + (geo.FloodLevel * 0.4f);
                    float vaporAddition = moistureSignal * deltaTime;
                    met.Column.WaterVaporColumn = MathF.Min(met.Column.WaterVaporColumn + vaporAddition * 50f, 80f);
                    cell.Humidity = Math.Clamp(cell.Humidity + moistureSignal * 0.1f * deltaTime, 0f, 1f);

                    // Active evaporation from warm oceans feeds clouds; cold drought dries them.
                    if (cell.IsWater && cell.Temperature > 5f)
                    {
                        float evaporation = (cell.Temperature + 10f) * 0.0015f;
                        met.CloudCover = Math.Clamp(met.CloudCover + evaporation * deltaTime, 0f, 1f);
                    }
                    else if (cell.IsDesert)
                    {
                        met.CloudCover = MathF.Max(0f, met.CloudCover - 0.08f * deltaTime);
                    }

                    // Use river flow and floods to nudge local precipitation, creating a hydrology→weather loop.
                    float flowSignal = Math.Clamp(geo.WaterFlow + geo.FloodLevel * 5f, 0f, 5f);
                    met.Precipitation = Math.Clamp(met.Precipitation + flowSignal * 0.02f * deltaTime, 0f, 2f);
                }
            });
        }

        /// <summary>
        /// Feed biosphere productivity back into atmospheric composition so
        /// vegetation and ocean life change oxygen/CO2, and make global metrics
        /// available to UI or stabilizers.
        /// </summary>
        private void FeedBiosphereIntoAtmosphere(int currentYear, float deltaTime)
        {
            double totalCO2 = 0;
            double totalO2 = 0;
            double totalBiomass = 0;
            object totalsLock = new();
            int cells = _map.Width * _map.Height;

            Parallel.For(0, _map.Width, () => (0d, 0d, 0d), (x, state, local) =>
            {
                double localCO2 = local.Item1;
                double localO2 = local.Item2;
                double localBiomass = local.Item3;
                for (int y = 0; y < _map.Height; y++)
                {
                    var cell = _map.Cells[x, y];

                    // Productive biomes reduce CO2 and raise oxygen; dying biomes reverse it.
                    if (cell.Biomass > 0.05f)
                    {
                        float productivity = MathF.Min(cell.Biomass, 1.2f);
                        cell.CO2 = Math.Max(0f, cell.CO2 - productivity * 0.02f * deltaTime);
                        cell.Oxygen = Math.Clamp(cell.Oxygen + productivity * 0.03f * deltaTime, 0f, 100f);
                    }
                    else
                    {
                        cell.CO2 = Math.Min(100f, cell.CO2 + 0.005f * deltaTime);
                    }

                    // Minor seasonal drift to keep cycles alive
                    float seasonalFactor = MathF.Sin((currentYear % 10000) * 0.017f) * 0.0025f;
                    cell.CO2 = Math.Clamp(cell.CO2 + seasonalFactor, 0f, 100f);

                    localCO2 += cell.CO2;
                    localO2 += cell.Oxygen;
                    localBiomass += cell.Biomass;
                }
                return (localCO2, localO2, localBiomass);
            }, totals =>
            {
                lock (totalsLock)
                {
                    totalCO2 += totals.Item1;
                    totalO2 += totals.Item2;
                    totalBiomass += totals.Item3;
                }
            });

            _map.GlobalCO2 = (float)(totalCO2 / cells);
            _map.GlobalOxygen = (float)(totalO2 / cells);

            // Make global temperature slightly sensitive to planetary biomass to keep a living feedback loop.
            float biomassFactor = (float)(totalBiomass / cells);
            _map.GlobalTemperature += Math.Clamp((biomassFactor - 0.3f) * 0.01f, -0.05f, 0.05f) * deltaTime;
        }

        /// <summary>
        /// Let active geology (volcanoes, quakes, tsunamis) disturb atmosphere
        /// and hydrology so tectonics ripple through other systems.
        /// </summary>
        private void FeedGeologyIntoHydrologyAndWeather(float deltaTime)
        {
            Parallel.For(0, _map.Width, x =>
            {
                for (int y = 0; y < _map.Height; y++)
                {
                    var cell = _map.Cells[x, y];
                    var geo = cell.GetGeology();
                    var met = cell.GetMeteorology();

                    // Volcanic activity injects aerosols and CO2, altering clouds and gas balance.
                    if (geo.VolcanicActivity > 0.1f)
                    {
                        float aerosol = Math.Clamp(geo.VolcanicActivity * 0.05f, 0f, 0.3f);
                        met.CloudCover = Math.Clamp(met.CloudCover + aerosol * deltaTime, 0f, 1f);
                        cell.CO2 = Math.Clamp(cell.CO2 + geo.VolcanicActivity * 0.02f * deltaTime, 0f, 100f);
                        // Ash darkens surfaces temporarily.
                        cell.Albedo = Math.Clamp(cell.Albedo - aerosol * 0.1f, 0.02f, 0.9f);
                    }

                    // Seismic shocks and tsunamis stir water columns, changing salinity/density slightly.
                    if (geo.EarthquakeIntensity > 0.05f || geo.TsunamiWaveHeight > 0.05f)
                    {
                        float agitation = Math.Clamp(geo.EarthquakeIntensity + geo.TsunamiWaveHeight, 0f, 2f);
                        geo.Salinity = Math.Clamp(geo.Salinity + (cell.IsWater ? agitation * 0.1f * deltaTime : 0f), 5f, 45f);
                        geo.WaterDensity = Math.Clamp(geo.WaterDensity - agitation * 0.002f * deltaTime, 0.99f, 1.08f);
                        met.PressureTendency -= agitation * 0.05f * deltaTime;
                    }
                }
            });
        }
    }
}
