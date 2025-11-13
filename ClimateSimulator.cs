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

                // Base temperature from latitude with stronger polar cooling
                float latitude = Math.Abs((y - _map.Height / 2.0f) / (_map.Height / 2.0f));

                // Realistic temperature gradient: hot equator, freezing poles
                // Equator (lat=0): ~30°C, Poles (lat=1): ~-40°C
                float baseTemp = 30 - (latitude * latitude * 70); // Quadratic for stronger polar effect
                float solarHeating = baseTemp * _map.SolarEnergy;

                // Elevation cooling (6.5°C per km, roughly 0.65°C per 0.1 elevation)
                if (cell.Elevation > 0)
                {
                    solarHeating -= cell.Elevation * 20; // Increased cooling
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
                if (cell.LifeType == LifeForm.PlantLife || cell.IsForest)
                {
                    evaporation += cell.Biomass * 0.3f;
                }

                // Mountains increase rainfall
                float orographicEffect = 0;
                if (cell.Elevation > 0.4f)
                {
                    orographicEffect = (cell.Elevation - 0.4f) * 0.5f;
                }

                // Realistic atmospheric circulation patterns (Hadley, Ferrel, Polar cells)
                float latitude = Math.Abs((y - _map.Height / 2.0f) / (_map.Height / 2.0f));

                float latitudeEffect;
                if (latitude < 0.15f)
                {
                    // ITCZ (Intertropical Convergence Zone) - Heavy rainfall at equator
                    latitudeEffect = 1.5f;
                }
                else if (latitude < 0.4f)
                {
                    // Subtropical high pressure - Deserts (Hadley cell descending air)
                    // Peak aridity around 25-30 degrees (0.25-0.35 latitude)
                    float desertPeak = 0.27f;
                    float desertStrength = 1.0f - Math.Abs(latitude - desertPeak) / 0.15f;
                    desertStrength = Math.Clamp(desertStrength, 0, 1);
                    latitudeEffect = 0.2f + (0.4f * (1.0f - desertStrength)); // Very dry
                }
                else if (latitude < 0.7f)
                {
                    // Mid-latitudes (Ferrel cell) - Moderate rainfall
                    latitudeEffect = 0.8f + (1.0f - ((latitude - 0.4f) / 0.3f)) * 0.4f;
                }
                else
                {
                    // Polar regions - Cold deserts (low moisture capacity)
                    latitudeEffect = 0.3f;
                }

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
