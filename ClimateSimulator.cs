namespace SimPlanet;

/// <summary>
/// Simulates climate, temperature, rainfall, and weather patterns
/// </summary>
public class ClimateSimulator
{
    private readonly PlanetMap _map;
    private readonly Random _random;

    public ClimateSimulator(PlanetMap map)
    {
        _map = map;
        _random = new Random();
    }

    public void Update(float deltaTime)
    {
        SimulateTemperature(deltaTime);
        SimulateRainfall(deltaTime);
        SimulateHumidity(deltaTime);
    }

    private void SimulateTemperature(float deltaTime)
    {
        var newTemperatures = new float[_map.Width, _map.Height];

        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                var cell = _map.Cells[x, y];

                // Base temperature from latitude
                float latitude = Math.Abs((y - _map.Height / 2.0f) / (_map.Height / 2.0f));
                float solarHeating = (30 - latitude * 40) * _map.SolarEnergy;

                // Elevation cooling
                if (cell.Elevation > 0)
                {
                    solarHeating -= cell.Elevation * 15;
                }

                // Greenhouse effect
                solarHeating += cell.Greenhouse * 20;

                // Water moderates temperature
                if (cell.IsWater)
                {
                    solarHeating += 5;
                }

                // Heat diffusion from neighbors
                float neighborTemp = 0;
                int neighborCount = 0;
                foreach (var (nx, ny, neighbor) in _map.GetNeighbors(x, y))
                {
                    neighborTemp += neighbor.Temperature;
                    neighborCount++;
                }
                if (neighborCount > 0)
                {
                    neighborTemp /= neighborCount;
                }

                // Blend current temperature with target
                float targetTemp = solarHeating * 0.3f + neighborTemp * 0.7f;
                newTemperatures[x, y] = cell.Temperature + (targetTemp - cell.Temperature) * deltaTime * 0.1f;
            }
        }

        // Apply new temperatures
        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                _map.Cells[x, y].Temperature = newTemperatures[x, y];
            }
        }
    }

    private void SimulateRainfall(float deltaTime)
    {
        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                var cell = _map.Cells[x, y];

                // Evaporation from water
                float evaporation = 0;
                if (cell.IsWater && cell.Temperature > 0)
                {
                    evaporation = 0.8f * (cell.Temperature / 30.0f);
                }

                // Evapotranspiration from plants
                if (cell.LifeType == LifeForm.PlantLife || cell.LifeType == LifeForm.Forest)
                {
                    evaporation += cell.Biomass * 0.3f;
                }

                // Mountains increase rainfall
                float orographicEffect = 0;
                if (cell.Elevation > 0.4f)
                {
                    orographicEffect = (cell.Elevation - 0.4f) * 0.5f;
                }

                // Equatorial regions get more rain
                float latitude = Math.Abs((y - _map.Height / 2.0f) / (_map.Height / 2.0f));
                float latitudeEffect = 1.0f - latitude * 0.6f;

                float targetRainfall = (evaporation + orographicEffect) * latitudeEffect;
                targetRainfall = Math.Clamp(targetRainfall, 0, 1);

                // Smooth transition
                cell.Rainfall += (targetRainfall - cell.Rainfall) * deltaTime * 0.05f;
            }
        }
    }

    private void SimulateHumidity(float deltaTime)
    {
        var newHumidity = new float[_map.Width, _map.Height];

        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                var cell = _map.Cells[x, y];

                // Water bodies are always humid
                if (cell.IsWater)
                {
                    newHumidity[x, y] = 0.9f;
                    continue;
                }

                // Humidity from rainfall
                float humidityFromRain = cell.Rainfall * 0.8f;

                // Humidity diffusion from neighbors
                float neighborHumidity = 0;
                int waterNeighbors = 0;
                foreach (var (nx, ny, neighbor) in _map.GetNeighbors(x, y))
                {
                    neighborHumidity += neighbor.Humidity;
                    if (neighbor.IsWater) waterNeighbors++;
                }
                neighborHumidity /= 8;

                // More water neighbors = more humid
                float waterEffect = waterNeighbors / 8.0f;

                float targetHumidity = Math.Max(humidityFromRain, neighborHumidity * 0.5f + waterEffect * 0.3f);
                newHumidity[x, y] = cell.Humidity + (targetHumidity - cell.Humidity) * deltaTime * 0.2f;
            }
        }

        // Apply new humidity
        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                _map.Cells[x, y].Humidity = newHumidity[x, y];
            }
        }
    }
}
