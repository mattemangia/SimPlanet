namespace SimPlanet;

/// <summary>
/// Meteorological data extension for terrain cells
/// </summary>
public static class MeteorologicalExtensions
{
    private static Dictionary<TerrainCell, MeteorologicalData> _metData = new();

    public static MeteorologicalData GetMeteorology(this TerrainCell cell)
    {
        if (!_metData.ContainsKey(cell))
        {
            _metData[cell] = new MeteorologicalData();
        }
        return _metData[cell];
    }

    public static void ClearMeteorologicalData()
    {
        _metData.Clear();
    }
}

public class MeteorologicalData
{
    public float WindSpeedX { get; set; }
    public float WindSpeedY { get; set; }
    public float AirPressure { get; set; } = 1013.25f; // Standard sea level
    public float CloudCover { get; set; }
    public bool InStorm { get; set; }
    public float Precipitation { get; set; } // Current rainfall/snowfall
    public int Season { get; set; } // 0=Spring, 1=Summer, 2=Fall, 3=Winter
}

/// <summary>
/// Comprehensive weather and meteorology simulation
/// </summary>
public class WeatherSystem
{
    private readonly PlanetMap _map;
    private readonly Random _random;
    private List<Storm> _storms;
    private float _seasonProgress = 0; // 0-4, wraps around
    private const float SeasonLength = 100.0f; // Years per season

    public List<Storm> ActiveStorms => _storms;

    public WeatherSystem(PlanetMap map, int seed)
    {
        _map = map;
        _random = new Random(seed + 3000);
        _storms = new List<Storm>();
    }

    public void Update(float deltaTime, int currentYear)
    {
        UpdateSeasons(deltaTime, currentYear);
        UpdateWindPatterns();
        UpdateAirPressure();
        UpdateStorms(deltaTime, currentYear);
        UpdatePrecipitation();
        UpdateCloudCover();
    }

    private void UpdateSeasons(float deltaTime, int currentYear)
    {
        // Seasonal progression (years-based for simulation speed)
        _seasonProgress += deltaTime * 0.1f;
        if (_seasonProgress >= 4.0f)
            _seasonProgress -= 4.0f;

        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                var cell = _map.Cells[x, y];
                var met = cell.GetMeteorology();

                // Calculate season based on hemisphere
                float latitude = (y - _map.Height / 2.0f) / (_map.Height / 2.0f);
                bool northernHemisphere = latitude > 0;

                // Seasons are opposite in hemispheres
                float localSeason = _seasonProgress;
                if (!northernHemisphere)
                    localSeason = (_seasonProgress + 2.0f) % 4.0f;

                met.Season = (int)localSeason;

                // Apply seasonal temperature variations
                float seasonalModifier = 0;
                switch (met.Season)
                {
                    case 0: // Spring
                        seasonalModifier = 0;
                        break;
                    case 1: // Summer
                        seasonalModifier = Math.Abs(latitude) * 15; // Hotter
                        break;
                    case 2: // Fall
                        seasonalModifier = 0;
                        break;
                    case 3: // Winter
                        seasonalModifier = -Math.Abs(latitude) * 15; // Colder
                        break;
                }

                // Apply seasonal effect gradually
                cell.Temperature += (seasonalModifier - cell.Temperature * 0.1f) * deltaTime * 0.01f;
            }
        }
    }

    private void UpdateWindPatterns()
    {
        // Global wind patterns (trade winds, westerlies, polar easterlies)
        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                var cell = _map.Cells[x, y];
                var met = cell.GetMeteorology();

                float latitude = Math.Abs((y - _map.Height / 2.0f) / (_map.Height / 2.0f));

                // Trade winds (0-30° latitude)
                if (latitude < 0.3f)
                {
                    met.WindSpeedX = 5.0f; // Eastward
                    met.WindSpeedY = 0;
                }
                // Westerlies (30-60° latitude)
                else if (latitude < 0.6f)
                {
                    met.WindSpeedX = -7.0f; // Westward
                    met.WindSpeedY = 0;
                }
                // Polar easterlies (60-90° latitude)
                else
                {
                    met.WindSpeedX = 3.0f; // Eastward
                    met.WindSpeedY = 0;
                }

                // Local wind from pressure differences
                var neighbors = _map.GetNeighbors(x, y).ToList();
                if (neighbors.Count > 0)
                {
                    float avgPressure = neighbors.Average(n => n.cell.GetMeteorology().AirPressure);
                    float pressureDiff = avgPressure - met.AirPressure;

                    // Wind flows from high to low pressure
                    met.WindSpeedX += pressureDiff * 0.1f;
                    met.WindSpeedY += pressureDiff * 0.05f;
                }

                // Terrain affects wind
                if (cell.Elevation > 0.5f) // Mountains slow wind
                {
                    met.WindSpeedX *= 0.5f;
                    met.WindSpeedY *= 0.5f;
                }
            }
        }
    }

    private void UpdateAirPressure()
    {
        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                var cell = _map.Cells[x, y];
                var met = cell.GetMeteorology();

                // Pressure affected by temperature (warm air rises = low pressure)
                float tempEffect = (20 - cell.Temperature) * 0.5f;

                // Elevation affects pressure
                float elevationEffect = -cell.Elevation * 100;

                // Humidity affects pressure
                float humidityEffect = cell.Humidity * 5;

                met.AirPressure = 1013.25f + tempEffect + elevationEffect - humidityEffect;
                met.AirPressure = Math.Clamp(met.AirPressure, 950, 1050);
            }
        }
    }

    private void UpdateStorms(float deltaTime, int currentYear)
    {
        // Generate new storms
        if (_random.NextDouble() < 0.01 * deltaTime)
        {
            GenerateStorm();
        }

        // Update existing storms
        for (int i = _storms.Count - 1; i >= 0; i--)
        {
            var storm = _storms[i];
            storm.Lifetime += deltaTime;

            // Move storm
            storm.CenterX += (int)(storm.VelocityX * deltaTime * 10);
            storm.CenterY += (int)(storm.VelocityY * deltaTime * 10);

            // Wrap around
            storm.CenterX = (storm.CenterX + _map.Width) % _map.Width;
            storm.CenterY = Math.Clamp(storm.CenterY, 0, _map.Height - 1);

            // Decay
            storm.Intensity *= 0.99f;

            // Remove weak or old storms
            if (storm.Intensity < 0.1f || storm.Lifetime > 50)
            {
                _storms.RemoveAt(i);
                continue;
            }

            // Apply storm effects
            ApplyStormEffects(storm);
        }
    }

    private void GenerateStorm()
    {
        int x = _random.Next(_map.Width);
        int y = _random.Next(_map.Height);

        var cell = _map.Cells[x, y];
        var met = cell.GetMeteorology();

        // Storms form in low pressure, high humidity areas
        if (met.AirPressure < 1000 && cell.Humidity > 0.6f && cell.IsWater)
        {
            StormType type = StormType.Thunderstorm;
            float intensity = 0.5f + (float)_random.NextDouble() * 0.5f;

            // Hurricanes in warm oceans
            if (cell.Temperature > 26 && cell.IsWater)
            {
                type = StormType.Hurricane;
                intensity = 0.8f + (float)_random.NextDouble() * 0.2f;
            }
            // Blizzards in cold areas
            else if (cell.Temperature < 0)
            {
                type = StormType.Blizzard;
            }

            var storm = new Storm
            {
                CenterX = x,
                CenterY = y,
                Intensity = intensity,
                Type = type,
                VelocityX = met.WindSpeedX * 0.1f,
                VelocityY = met.WindSpeedY * 0.1f
            };

            _storms.Add(storm);
        }
    }

    private void ApplyStormEffects(Storm storm)
    {
        int radius = storm.Type == StormType.Hurricane ? 15 : 8;

        for (int dx = -radius; dx <= radius; dx++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                int x = (storm.CenterX + dx + _map.Width) % _map.Width;
                int y = storm.CenterY + dy;

                if (y < 0 || y >= _map.Height) continue;

                float dist = MathF.Sqrt(dx * dx + dy * dy);
                if (dist > radius) continue;

                var cell = _map.Cells[x, y];
                var met = cell.GetMeteorology();

                met.InStorm = true;
                float effectStrength = storm.Intensity * (1 - dist / radius);

                // Heavy precipitation
                met.Precipitation = effectStrength;
                cell.Rainfall += effectStrength * 0.1f;

                // Strong winds
                met.WindSpeedX += (x - storm.CenterX) * effectStrength * 0.5f;
                met.WindSpeedY += (y - storm.CenterY) * effectStrength * 0.5f;

                // Lower pressure at center
                met.AirPressure -= effectStrength * 50;

                // Damage to life
                if (storm.Type == StormType.Hurricane && effectStrength > 0.5f)
                {
                    cell.Biomass *= 0.95f; // Storm damage
                }

                // Tornadoes can form in thunderstorms
                if (storm.Type == StormType.Thunderstorm && _random.NextDouble() < 0.001)
                {
                    cell.Biomass *= 0.5f; // Severe localized damage
                }
            }
        }
    }

    private void UpdatePrecipitation()
    {
        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                var cell = _map.Cells[x, y];
                var met = cell.GetMeteorology();

                // Natural precipitation decay
                met.Precipitation *= 0.9f;

                // Rain from clouds and humidity
                if (met.CloudCover > 0.7f && cell.Humidity > 0.6f)
                {
                    met.Precipitation += 0.1f * met.CloudCover;
                    cell.Rainfall += met.Precipitation * 0.01f;
                }

                // Snow instead of rain in freezing temps
                if (cell.Temperature < 0)
                {
                    // Snow accumulation (simplified)
                    cell.Elevation += met.Precipitation * 0.0001f;
                }
            }
        }
    }

    private void UpdateCloudCover()
    {
        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                var cell = _map.Cells[x, y];
                var met = cell.GetMeteorology();

                // Clouds form from humidity
                float targetClouds = cell.Humidity * 0.8f;

                // Low pressure increases clouds
                if (met.AirPressure < 1010)
                {
                    targetClouds += 0.2f;
                }

                // Storms have full cloud cover
                if (met.InStorm)
                {
                    targetClouds = 1.0f;
                }

                met.CloudCover += (targetClouds - met.CloudCover) * 0.1f;
                met.CloudCover = Math.Clamp(met.CloudCover, 0, 1);

                // Reset storm flag
                met.InStorm = false;
            }
        }
    }

    public List<Storm> GetActiveStorms() => _storms;

    public void LoadStorms(List<StormData> stormData)
    {
        _storms.Clear();
        foreach (var data in stormData)
        {
            _storms.Add(new Storm
            {
                CenterX = data.CenterX,
                CenterY = data.CenterY,
                Intensity = data.Intensity,
                VelocityX = data.VelocityX,
                VelocityY = data.VelocityY,
                Type = data.Type
            });
        }
    }
}

public class Storm
{
    public int CenterX { get; set; }
    public int CenterY { get; set; }
    public float Intensity { get; set; }
    public float VelocityX { get; set; }
    public float VelocityY { get; set; }
    public StormType Type { get; set; }
    public float Lifetime { get; set; }
}
