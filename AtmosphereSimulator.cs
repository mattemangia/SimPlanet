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
        UpdateGreenhouseEffect();
        UpdateGlobalAtmosphere();
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
                if (cell.LifeType == LifeForm.Algae)
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
                if (cell.LifeType == LifeForm.Algae)
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

    private void UpdateGreenhouseEffect()
    {
        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                var cell = _map.Cells[x, y];

                // Greenhouse effect from CO2
                cell.Greenhouse = cell.CO2 * 0.02f;

                // Water vapor also contributes
                if (cell.Humidity > 0.5f)
                {
                    cell.Greenhouse += (cell.Humidity - 0.5f) * 0.1f;
                }

                cell.Greenhouse = Math.Clamp(cell.Greenhouse, 0, 2);
            }
        }
    }

    private void UpdateGlobalAtmosphere()
    {
        float totalO2 = 0;
        float totalCO2 = 0;
        int count = 0;

        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                totalO2 += _map.Cells[x, y].Oxygen;
                totalCO2 += _map.Cells[x, y].CO2;
                count++;
            }
        }

        _map.GlobalOxygen = totalO2 / count;
        _map.GlobalCO2 = totalCO2 / count;
    }

    private void MixAtmosphere(Func<TerrainCell, float> getValue, Action<TerrainCell, float> setValue, float deltaTime)
    {
        var newValues = new float[_map.Width, _map.Height];

        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                var cell = _map.Cells[x, y];
                float currentValue = getValue(cell);
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

                // Mix with neighbors
                float mixRate = 0.05f * deltaTime;
                newValues[x, y] = currentValue + (neighborAvg - currentValue) * mixRate;
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
