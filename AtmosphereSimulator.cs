namespace SimPlanet;

/// <summary>
/// Simulates atmospheric composition and greenhouse effects
/// </summary>
public class AtmosphereSimulator
{
    private readonly PlanetMap _map;

    public AtmosphereSimulator(PlanetMap map)
    {
        _map = map;
    }

    public void Update(float deltaTime)
    {
        SimulateOxygenCycle(deltaTime);
        SimulateCarbonCycle(deltaTime);
        SimulateMethaneCycle(deltaTime);
        SimulateNitrousOxideCycle(deltaTime);
        UpdateGreenhouseEffect();
        // Global atmosphere stats now calculated in SimPlanetGame.UpdateGlobalStats() for performance
    }

    private void SimulateOxygenCycle(float deltaTime)
    {
        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                var cell = _map.Cells[x, y];

                float oxygenChange = 0;

                // Photosynthesis produces oxygen
                if (cell.LifeType == LifeForm.Bacteria)
                {
                    // Cyanobacteria - early photosynthetic oxygen producers
                    oxygenChange += cell.Biomass * 0.3f * deltaTime;
                }
                else if (cell.LifeType == LifeForm.Algae)
                {
                    oxygenChange += cell.Biomass * 0.5f * deltaTime;
                }
                else if (cell.LifeType == LifeForm.PlantLife)
                {
                    oxygenChange += cell.Biomass * 1.0f * deltaTime;
                }

                // Animals consume oxygen
                if (cell.LifeType == LifeForm.SimpleAnimals ||
                    cell.LifeType == LifeForm.ComplexAnimals ||
                    cell.LifeType == LifeForm.Intelligence ||
                    cell.LifeType == LifeForm.Civilization)
                {
                    oxygenChange -= cell.Biomass * 0.3f * deltaTime;
                }

                // Fire/volcanoes consume oxygen (simplified)
                if (cell.Temperature > 100)
                {
                    oxygenChange -= 0.1f * deltaTime;
                }

                cell.Oxygen = Math.Clamp(cell.Oxygen + oxygenChange, 0, 100);
            }
        }

        // Atmospheric mixing
        MixAtmosphere(c => c.Oxygen, (c, v) => c.Oxygen = v, deltaTime);
    }

    private void SimulateCarbonCycle(float deltaTime)
    {
        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                var cell = _map.Cells[x, y];

                float co2Change = 0;

                // Photosynthesis consumes CO2
                if (cell.LifeType == LifeForm.Bacteria)
                {
                    // Cyanobacteria - early photosynthetic CO2 consumers
                    co2Change -= cell.Biomass * 0.2f * deltaTime;
                }
                else if (cell.LifeType == LifeForm.Algae)
                {
                    co2Change -= cell.Biomass * 0.3f * deltaTime;
                }
                else if (cell.LifeType == LifeForm.PlantLife)
                {
                    co2Change -= cell.Biomass * 0.6f * deltaTime;
                }

                // Respiration produces CO2
                if (cell.Biomass > 0)
                {
                    co2Change += cell.Biomass * 0.1f * deltaTime;
                }

                // Animals produce CO2
                if (cell.LifeType == LifeForm.SimpleAnimals ||
                    cell.LifeType == LifeForm.ComplexAnimals)
                {
                    co2Change += cell.Biomass * 0.2f * deltaTime;
                }

                // Civilization produces lots of CO2
                if (cell.LifeType == LifeForm.Civilization)
                {
                    co2Change += cell.Biomass * 2.0f * deltaTime;
                }

                // Volcanic activity (simplified - hot spots)
                if (cell.Temperature > 200)
                {
                    co2Change += 0.5f * deltaTime;
                }

                // Ocean absorption
                if (cell.IsWater && cell.Temperature < 20)
                {
                    co2Change -= 0.1f * deltaTime;
                }

                cell.CO2 = Math.Clamp(cell.CO2 + co2Change, 0, 100);
            }
        }

        // Atmospheric mixing
        MixAtmosphere(c => c.CO2, (c, v) => c.CO2 = v, deltaTime);
    }

    private void SimulateMethaneCycle(float deltaTime)
    {
        // Methane (CH4) is a potent greenhouse gas (28x CO2 over 100 years)
        // Sources: wetlands, agriculture, decomposition, volcanic activity, ocean
        // Sinks: atmospheric oxidation, soil absorption

        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                var cell = _map.Cells[x, y];
                float ch4Change = 0;

                // Wetlands produce methane (anaerobic decomposition)
                if (cell.IsLand && cell.Humidity > 0.7f && cell.Rainfall > 0.6f && cell.Temperature > 5)
                {
                    ch4Change += 0.15f * cell.Biomass * deltaTime;
                }

                // Ocean sediments release methane (especially warm shallow seas)
                if (cell.IsWater && cell.Elevation > -0.3f && cell.Temperature > 15)
                {
                    ch4Change += 0.08f * deltaTime;
                }

                // Decomposing organic matter
                if (cell.Biomass > 0.5f && cell.Humidity > 0.5f)
                {
                    ch4Change += cell.Biomass * 0.05f * deltaTime;
                }

                // Civilization activities (agriculture, livestock, fossil fuels)
                if (cell.LifeType == LifeForm.Civilization)
                {
                    ch4Change += cell.Biomass * 0.8f * deltaTime;
                }

                // Volcanic emissions
                if (cell.Temperature > 200)
                {
                    ch4Change += 0.2f * deltaTime;
                }

                // Atmospheric oxidation (methane breaks down in ~10 years)
                ch4Change -= cell.Methane * 0.01f * deltaTime;

                // Soil bacteria consume methane
                if (cell.IsLand && cell.Biomass > 0.2f)
                {
                    ch4Change -= cell.Methane * 0.005f * deltaTime;
                }

                cell.Methane = Math.Clamp(cell.Methane + ch4Change, 0, 100);
            }
        }

        // Atmospheric mixing
        MixAtmosphere(c => c.Methane, (c, v) => c.Methane = v, deltaTime);
    }

    private void SimulateNitrousOxideCycle(float deltaTime)
    {
        // Nitrous oxide (N2O) is a very potent greenhouse gas (265x CO2 over 100 years)
        // Sources: microbial processes in soil and water, fertilizers, combustion
        // Sinks: stratospheric photolysis (very long lifetime ~120 years)

        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                var cell = _map.Cells[x, y];
                float n2oChange = 0;

                // Soil microbial processes (nitrification and denitrification)
                if (cell.IsLand && cell.Biomass > 0.3f && cell.Humidity > 0.4f)
                {
                    n2oChange += 0.03f * cell.Biomass * deltaTime;
                }

                // Agricultural fertilizer use
                if (cell.LifeType == LifeForm.Civilization && cell.Biomass > 0.5f)
                {
                    n2oChange += 0.25f * cell.Biomass * deltaTime;
                }

                // Ocean production (particularly oxygen-minimum zones)
                if (cell.IsWater && cell.Oxygen < 30)
                {
                    n2oChange += 0.05f * deltaTime;
                }

                // Combustion processes
                if (cell.Temperature > 150)
                {
                    n2oChange += 0.05f * deltaTime;
                }

                // Very slow atmospheric breakdown (120-year lifetime)
                n2oChange -= cell.NitrousOxide * 0.0001f * deltaTime;

                cell.NitrousOxide = Math.Clamp(cell.NitrousOxide + n2oChange, 0, 100);
            }
        }

        // Atmospheric mixing
        MixAtmosphere(c => c.NitrousOxide, (c, v) => c.NitrousOxide = v, deltaTime);
    }

    private void UpdateGreenhouseEffect()
    {
        // Enhanced greenhouse effect with multiple gases
        // Relative forcing: CO2 (1x), CH4 (28x), N2O (265x), H2O (varies)
        // Source: IPCC AR6 Report

        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                var cell = _map.Cells[x, y];

                // CO2 greenhouse contribution (baseline)
                float co2Effect = cell.CO2 * 0.02f;

                // Methane contribution (28x more potent than CO2)
                float ch4Effect = cell.Methane * 0.56f;

                // Nitrous oxide contribution (265x more potent than CO2)
                float n2oEffect = cell.NitrousOxide * 5.3f;

                // Water vapor feedback (most important greenhouse gas)
                // Water vapor amplifies warming from other gases
                float waterVaporEffect = 0;
                if (cell.Humidity > 0.5f)
                {
                    waterVaporEffect = (cell.Humidity - 0.5f) * 0.2f;

                    // Positive feedback: warmer air holds more water vapor
                    if (cell.Temperature > 15)
                    {
                        waterVaporEffect *= (1.0f + (cell.Temperature - 15) * 0.01f);
                    }
                }

                // Total greenhouse effect with gas interactions
                cell.Greenhouse = co2Effect + ch4Effect + n2oEffect + waterVaporEffect;

                // Clouds can have both warming (trap heat) and cooling (reflect sunlight) effects
                // Net effect depends on altitude and type - simplified here
                var met = cell.GetMeteorology();
                if (met.CloudCover > 0.5f)
                {
                    // High clouds trap more heat
                    cell.Greenhouse += (met.CloudCover - 0.5f) * 0.15f;
                }

                cell.Greenhouse = Math.Clamp(cell.Greenhouse, 0, 5);
            }
        }
    }

    // REMOVED: UpdateGlobalAtmosphere() - now handled by SimPlanetGame.UpdateGlobalStats()
    // This eliminates a redundant 28,800-cell scan every frame (240x120 map)
    // Global stats are now calculated once per second in a single combined pass

    private void MixAtmosphere(Func<TerrainCell, float> getValue, Action<TerrainCell, float> setValue, float deltaTime)
    {
        var newValues = new float[_map.Width, _map.Height];

        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                var cell = _map.Cells[x, y];
                float currentValue = getValue(cell);

                // Diffusion mixing with neighbors
                float neighborAvg = 0;
                int count = 0;

                foreach (var (nx, ny, neighbor) in _map.GetNeighbors(x, y))
                {
                    neighborAvg += getValue(neighbor);
                    count++;
                }

                if (count > 0)
                {
                    neighborAvg /= count;
                }

                // Base diffusion mixing (faster rate for better atmospheric circulation)
                float diffusionRate = 0.15f * deltaTime;
                float diffusedValue = currentValue + (neighborAvg - currentValue) * diffusionRate;

                // Wind-driven advection (gases carried by wind)
                var met = cell.GetMeteorology();
                float windTransport = 0;

                // Calculate wind direction and fetch upwind gas concentration
                if (Math.Abs(met.WindSpeedX) > 0.1f || Math.Abs(met.WindSpeedY) > 0.1f)
                {
                    // Determine upwind direction
                    int windDx = met.WindSpeedX > 0 ? -1 : (met.WindSpeedX < 0 ? 1 : 0);
                    int windDy = met.WindSpeedY > 0 ? -1 : (met.WindSpeedY < 0 ? 1 : 0);

                    if (windDx != 0 || windDy != 0)
                    {
                        int upwindX = (x + windDx + _map.Width) % _map.Width;
                        int upwindY = Math.Clamp(y + windDy, 0, _map.Height - 1);

                        float upwindValue = getValue(_map.Cells[upwindX, upwindY]);
                        float windSpeed = MathF.Sqrt(met.WindSpeedX * met.WindSpeedX + met.WindSpeedY * met.WindSpeedY);

                        // Transport rate proportional to wind speed
                        float transportRate = Math.Clamp(windSpeed * 0.01f * deltaTime, 0, 0.2f);
                        windTransport = (upwindValue - currentValue) * transportRate;
                    }
                }

                newValues[x, y] = Math.Clamp(diffusedValue + windTransport, 0, 100);
            }
        }

        // Apply new values
        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                setValue(_map.Cells[x, y], newValues[x, y]);
            }
        }
    }
}
