namespace SimPlanet;

/// <summary>
/// Meteorological data extension for terrain cells (now uses embedded data for performance)
/// </summary>
public static class MeteorologicalExtensions
{
    // Extension methods now simply access embedded property (maintains backward compatibility)
    public static MeteorologicalData GetMeteorology(this TerrainCell cell)
    {
        return cell.Meteorology;
    }

    // No longer needed as data is embedded in TerrainCell, but kept for API compatibility
    public static void ClearMeteorologicalData()
    {
        // No-op: data is now managed per-cell, cleared when cells are recreated
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
                float tempModifier = 0;
                float rainfallModifier = 1.0f;

                switch (met.Season)
                {
                    case 0: // Spring
                        tempModifier = 0;
                        rainfallModifier = 1.2f; // Spring rains
                        break;
                    case 1: // Summer
                        tempModifier = Math.Abs(latitude) * 15; // Hotter
                        // Monsoons in tropics (latitude < 0.3), dry in mid-latitudes
                        rainfallModifier = Math.Abs(latitude) < 0.3f ? 1.5f : 0.8f;
                        break;
                    case 2: // Fall
                        tempModifier = 0;
                        rainfallModifier = 1.1f; // Moderate rains
                        break;
                    case 3: // Winter
                        tempModifier = -Math.Abs(latitude) * 15; // Colder
                        // Dry season in tropics, wet in mid-latitudes (winter storms)
                        rainfallModifier = Math.Abs(latitude) < 0.3f ? 0.7f : 1.3f;
                        break;
                }

                // Apply temperature effect gradually
                cell.Temperature += (tempModifier - cell.Temperature * 0.1f) * deltaTime * 0.01f;

                // Apply seasonal rainfall variations
                float baseRainfall = cell.Rainfall; // Preserve base climate rainfall
                cell.Humidity = Math.Clamp(cell.Humidity * rainfallModifier, 0, 1);

                // Seasonal ice formation/melting
                // Ice can form on both water (sea ice) and land (ice sheets, glaciers)
                if (cell.Temperature < -10)
                {
                    // Permanent ice caps and glaciers (very cold)
                    cell.IsIce = true;
                }
                else if (cell.Temperature < 0 && cell.Temperature >= -10)
                {
                    // Seasonal ice formation
                    // Sea ice forms easily, land ice (glaciers) only in very cold sustained conditions
                    if (cell.IsWater && cell.Temperature < -2)
                    {
                        cell.IsIce = true;
                    }
                }
                else if (cell.Temperature >= 0)
                {
                    // Ice melts at or above freezing
                    // Sea ice melts quickly, land ice (glaciers) persist a bit longer
                    if (cell.IsWater)
                    {
                        // Sea ice melts at 0°C
                        cell.IsIce = false;
                    }
                    else if (cell.Temperature > 2)
                    {
                        // Land ice melts above 2°C (gives glaciers some persistence)
                        cell.IsIce = false;
                    }
                }
            }
        }
    }

    private void UpdateWindPatterns()
    {
        // Global wind patterns with Coriolis effect
        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                var cell = _map.Cells[x, y];
                var met = cell.GetMeteorology();

                // Latitude: -1 (south pole) to +1 (north pole), 0 at equator
                float signedLatitude = (y - _map.Height / 2.0f) / (_map.Height / 2.0f);
                float absLatitude = Math.Abs(signedLatitude);

                // Base wind patterns by latitude zone
                float baseWindX = 0;
                float baseWindY = 0;

                // Trade winds (0-30° latitude) - easterlies
                if (absLatitude < 0.3f)
                {
                    baseWindX = 5.0f; // Eastward
                    // Converge toward equator (ITCZ - Intertropical Convergence Zone)
                    baseWindY = -Math.Sign(signedLatitude) * 2.0f;
                }
                // Westerlies (30-60° latitude)
                else if (absLatitude < 0.6f)
                {
                    baseWindX = -7.0f; // Westward
                    // Diverge from mid-latitudes
                    baseWindY = Math.Sign(signedLatitude) * 1.5f;
                }
                // Polar easterlies (60-90° latitude)
                else
                {
                    baseWindX = 3.0f; // Eastward
                    // Converge at poles
                    baseWindY = -Math.Sign(signedLatitude) * 1.0f;
                }

                // Apply Coriolis effect (deflects winds based on latitude and hemisphere)
                // Coriolis parameter: f = 2 * Ω * sin(latitude)
                // Simplified: deflection proportional to latitude and wind speed
                float coriolisStrength = signedLatitude * 0.3f; // Stronger at poles, zero at equator

                // Coriolis deflection: perpendicular to wind direction
                // Northern hemisphere: deflect right, Southern: deflect left
                float coriolisDeflectionX = -baseWindY * coriolisStrength;
                float coriolisDeflectionY = baseWindX * coriolisStrength;

                // Apply base wind + Coriolis deflection
                float windX = baseWindX + coriolisDeflectionX;
                float windY = baseWindY + coriolisDeflectionY;

                // Local wind from pressure differences (geostrophic wind)
                var neighbors = _map.GetNeighbors(x, y).ToList();
                if (neighbors.Count > 0)
                {
                    float avgPressure = neighbors.Average(n => n.cell.GetMeteorology().AirPressure);
                    float pressureDiff = avgPressure - met.AirPressure;

                    // Wind flows from high to low pressure, also affected by Coriolis
                    float pressureWindX = pressureDiff * 0.1f;
                    float pressureWindY = pressureDiff * 0.05f;

                    // Apply Coriolis to pressure gradient wind
                    windX += pressureWindX - pressureWindY * coriolisStrength;
                    windY += pressureWindY + pressureWindX * coriolisStrength;
                }

                // Terrain affects wind (mountains slow and redirect wind)
                if (cell.Elevation > 0.5f)
                {
                    windX *= 0.5f;
                    windY *= 0.5f;
                }

                // Set final wind speeds
                met.WindSpeedX = windX;
                met.WindSpeedY = windY;
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
        // Generate new tropical cyclones (less frequently, more realistic)
        if (_random.NextDouble() < 0.005 * deltaTime)
        {
            GenerateStorm();
        }

        // Update existing storms
        for (int i = _storms.Count - 1; i >= 0; i--)
        {
            var storm = _storms[i];
            storm.Lifetime += deltaTime;

            // Get latitude for Coriolis effect
            float latitude = (storm.CenterY - _map.Height / 2.0f) / (_map.Height / 2.0f);
            float absLatitude = Math.Abs(latitude);

            // Apply Coriolis force to storm movement (curves trajectory)
            // Storms curve right in NH, left in SH
            float coriolisDeflection = latitude * 0.3f;  // Stronger at higher latitudes

            // Base movement from steering winds
            float steeringWindX = storm.VelocityX;
            float steeringWindY = storm.VelocityY;

            // Apply Coriolis deflection perpendicular to motion
            storm.VelocityX = steeringWindX - steeringWindY * coriolisDeflection;
            storm.VelocityY = steeringWindY + steeringWindX * coriolisDeflection;

            // Move storm with realistic speed (tropical cyclones move ~10-30 km/h)
            storm.CenterX += (int)(storm.VelocityX * deltaTime * 8);
            storm.CenterY += (int)(storm.VelocityY * deltaTime * 8);

            // Wrap around horizontally
            storm.CenterX = (storm.CenterX + _map.Width) % _map.Width;
            storm.CenterY = Math.Clamp(storm.CenterY, 0, _map.Height - 1);

            // Check if over land or water
            var centerCell = _map.Cells[storm.CenterX, storm.CenterY];
            storm.OverLand = centerCell.IsLand;
            storm.SeaSurfaceTemp = centerCell.Temperature;

            // Update storm intensity based on conditions
            UpdateStormIntensity(storm, deltaTime);

            // Update storm category based on wind speed
            UpdateStormCategory(storm);

            // Remove dissipated storms
            if (storm.Intensity < 0.05f || storm.Lifetime > 100 ||
                storm.MaxWindSpeed < 5f || Math.Abs(latitude) < 0.05f)  // Dissipate near equator
            {
                _storms.RemoveAt(i);
                continue;
            }

            // Apply storm effects
            ApplyStormEffects(storm);
        }
    }

    private void UpdateStormIntensity(Storm storm, float deltaTime)
    {
        // Tropical cyclones intensify over warm water (>26°C), weaken over land or cool water
        bool isTropical = storm.Type >= StormType.TropicalDepression &&
                         storm.Type <= StormType.HurricaneCategory5;

        if (isTropical)
        {
            if (storm.OverLand)
            {
                // Rapid weakening over land (friction, no moisture)
                storm.Intensity *= 0.92f;  // Lose ~8% per timestep
                storm.MaxWindSpeed *= 0.93f;
                storm.CentralPressure += 2.0f * deltaTime;  // Pressure rises
            }
            else if (storm.SeaSurfaceTemp > 26)
            {
                // Intensification over warm water
                float warmthBonus = (storm.SeaSurfaceTemp - 26) * 0.01f;
                storm.Intensity = Math.Min(1.0f, storm.Intensity + warmthBonus * deltaTime);
                storm.MaxWindSpeed += warmthBonus * 5.0f * deltaTime;
                storm.CentralPressure = Math.Max(900, storm.CentralPressure - warmthBonus * 10f * deltaTime);
            }
            else
            {
                // Slow weakening over cool water
                storm.Intensity *= 0.98f;
                storm.MaxWindSpeed *= 0.985f;
                storm.CentralPressure += 0.5f * deltaTime;
            }
        }
        else
        {
            // Regular storms (thunderstorms, blizzards) decay normally
            storm.Intensity *= 0.99f;
        }

        // Natural dissipation over time
        storm.Intensity *= (1.0f - 0.002f * deltaTime);
    }

    private void UpdateStormCategory(Storm storm)
    {
        // Update tropical cyclone category based on wind speed (Saffir-Simpson scale)
        // Wind speeds in m/s (multiply by ~2.237 to get mph)
        if (storm.Type == StormType.Thunderstorm || storm.Type == StormType.Blizzard ||
            storm.Type == StormType.Tornado)
        {
            return; // Don't categorize non-tropical storms
        }

        float windMph = storm.MaxWindSpeed * 2.237f;

        if (windMph < 39)
            storm.Type = StormType.TropicalDepression;
        else if (windMph < 74)
            storm.Type = StormType.TropicalStorm;
        else if (windMph < 96)
            storm.Type = StormType.HurricaneCategory1;
        else if (windMph < 111)
            storm.Type = StormType.HurricaneCategory2;
        else if (windMph < 130)
            storm.Type = StormType.HurricaneCategory3;
        else if (windMph < 157)
            storm.Type = StormType.HurricaneCategory4;
        else
            storm.Type = StormType.HurricaneCategory5;
    }

    private void GenerateStorm()
    {
        int x = _random.Next(_map.Width);
        int y = _random.Next(_map.Height);

        var cell = _map.Cells[x, y];
        var met = cell.GetMeteorology();

        // Get latitude for storm type determination
        float latitude = (y - _map.Height / 2.0f) / (_map.Height / 2.0f);
        float absLatitude = Math.Abs(latitude);

        // Calculate wind convergence (check if winds are converging)
        float windConvergence = CalculateWindConvergence(x, y);

        // Tropical cyclones require:
        // 1. Warm ocean (>26°C)
        // 2. High cloud cover and humidity
        // 3. Low pressure
        // 4. Away from equator (need Coriolis: 5-30° latitude)
        // 5. Wind convergence
        bool canFormTropical = cell.IsWater &&
                              cell.Temperature > 26 &&
                              met.CloudCover > 0.7f &&
                              cell.Humidity > 0.7f &&
                              met.AirPressure < 1005 &&
                              absLatitude > 0.08f &&  // ~5° from equator
                              absLatitude < 0.5f &&   // ~30° latitude
                              windConvergence > 0.02f;

        if (canFormTropical)
        {
            // Start as tropical depression
            var storm = new Storm
            {
                CenterX = x,
                CenterY = y,
                Intensity = 0.3f + (float)_random.NextDouble() * 0.2f,
                Type = StormType.TropicalDepression,
                VelocityX = met.WindSpeedX * 0.15f + (latitude > 0 ? 0.5f : -0.5f),  // Westward drift in tropics
                VelocityY = met.WindSpeedY * 0.15f + (latitude > 0 ? 0.2f : -0.2f),  // Poleward drift
                CentralPressure = 1005f - (float)_random.NextDouble() * 10f,
                MaxWindSpeed = 10f + (float)_random.NextDouble() * 5f,  // Start weak
                SeaSurfaceTemp = cell.Temperature,
                OverLand = false,
                RotationDirection = latitude > 0 ? 1f : -1f  // Counterclockwise NH, clockwise SH
            };

            _storms.Add(storm);
        }
        // Regular thunderstorms
        else if (met.AirPressure < 1000 && cell.Humidity > 0.6f && met.CloudCover > 0.6f)
        {
            var storm = new Storm
            {
                CenterX = x,
                CenterY = y,
                Intensity = 0.4f + (float)_random.NextDouble() * 0.3f,
                Type = StormType.Thunderstorm,
                VelocityX = met.WindSpeedX * 0.12f,
                VelocityY = met.WindSpeedY * 0.12f,
                CentralPressure = 995f,
                MaxWindSpeed = 15f,
                SeaSurfaceTemp = cell.Temperature,
                OverLand = cell.IsLand
            };

            _storms.Add(storm);
        }
        // Blizzards in cold areas
        else if (cell.Temperature < 0 && cell.Humidity > 0.5f && met.CloudCover > 0.7f)
        {
            var storm = new Storm
            {
                CenterX = x,
                CenterY = y,
                Intensity = 0.5f + (float)_random.NextDouble() * 0.3f,
                Type = StormType.Blizzard,
                VelocityX = met.WindSpeedX * 0.15f,
                VelocityY = met.WindSpeedY * 0.15f,
                CentralPressure = 980f,
                MaxWindSpeed = 20f,
                SeaSurfaceTemp = cell.Temperature,
                OverLand = cell.IsLand
            };

            _storms.Add(storm);
        }
    }

    private float CalculateWindConvergence(int x, int y)
    {
        // Check if winds are converging toward this location
        // Positive convergence = winds flowing inward (favorable for storm formation)
        var met = _map.Cells[x, y].GetMeteorology();
        float centerWindX = met.WindSpeedX;
        float centerWindY = met.WindSpeedY;

        float convergence = 0;
        int count = 0;

        // Check neighboring cells
        foreach (var (nx, ny, neighbor) in _map.GetNeighbors(x, y))
        {
            var neighborMet = neighbor.GetMeteorology();

            // Calculate if wind is blowing toward center
            float dx = x - nx;
            float dy = y - ny;
            float dist = MathF.Sqrt(dx * dx + dy * dy);

            if (dist > 0)
            {
                // Normalize direction vector
                dx /= dist;
                dy /= dist;

                // Dot product of wind with direction toward center
                float windTowardCenter = (neighborMet.WindSpeedX * dx + neighborMet.WindSpeedY * dy);

                convergence += windTowardCenter;
                count++;
            }
        }

        return count > 0 ? convergence / count : 0;
    }

    private void ApplyStormEffects(Storm storm)
    {
        // Determine radius based on storm type
        int radius = storm.Type switch
        {
            StormType.TropicalDepression => 10,
            StormType.TropicalStorm => 12,
            StormType.HurricaneCategory1 => 15,
            StormType.HurricaneCategory2 => 18,
            StormType.HurricaneCategory3 => 20,
            StormType.HurricaneCategory4 => 22,
            StormType.HurricaneCategory5 => 25,
            StormType.Blizzard => 12,
            _ => 8  // Thunderstorm
        };

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

                // Apply CYCLONIC ROTATION (spiral winds around the eye)
                // Winds rotate counterclockwise in NH, clockwise in SH
                if (dist > 2)  // Eye wall at center (calm eye)
                {
                    float angle = MathF.Atan2(dy, dx);
                    float tangentialSpeed = storm.MaxWindSpeed * effectStrength;

                    // Rotate 90 degrees for tangential flow, direction based on hemisphere
                    float rotatedAngle = angle + (storm.RotationDirection * MathF.PI / 2f);

                    // Add cyclonic circulation to existing winds
                    met.WindSpeedX += MathF.Cos(rotatedAngle) * tangentialSpeed * 0.3f;
                    met.WindSpeedY += MathF.Sin(rotatedAngle) * tangentialSpeed * 0.3f;

                    // Add inward spiraling component (convergence toward center)
                    met.WindSpeedX += -dx / dist * effectStrength * storm.MaxWindSpeed * 0.1f;
                    met.WindSpeedY += -dy / dist * effectStrength * storm.MaxWindSpeed * 0.1f;
                }

                // Heavy precipitation (strongest in eye wall)
                float rainIntensity = dist < radius * 0.3f ? effectStrength * 1.5f : effectStrength;
                met.Precipitation = Math.Max(met.Precipitation, rainIntensity);
                cell.Rainfall += rainIntensity * 0.08f;

                // Lower pressure (lowest at center)
                float pressureDrop = effectStrength * (1013.25f - storm.CentralPressure);
                met.AirPressure = Math.Min(met.AirPressure, storm.CentralPressure + pressureDrop);

                // Increase cloud cover
                met.CloudCover = Math.Max(met.CloudCover, effectStrength * 0.9f);

                // Temperature effects
                // Tropical cyclones cool sea surface temperature by mixing deep cold water
                if (cell.IsWater && storm.Type >= StormType.TropicalStorm)
                {
                    float cooling = effectStrength * 2.0f; // Up to 2°C cooling
                    cell.Temperature -= cooling * 0.05f; // Gradual cooling
                }
                // Evaporative cooling from heavy rain
                if (rainIntensity > 0.5f)
                {
                    cell.Temperature -= rainIntensity * 0.3f;
                }

                // Ocean current disruption from cyclone winds
                if (cell.IsWater && storm.Type >= StormType.TropicalStorm)
                {
                    // Cyclones create strong vertical mixing and surface currents
                    // Add turbulent flow component
                    float currentStrength = effectStrength * storm.MaxWindSpeed * 0.02f;

                    // Create circular current pattern
                    float currentAngle = angle + (storm.RotationDirection * MathF.PI / 2f);
                    met.WindSpeedX += MathF.Cos(currentAngle) * currentStrength;
                    met.WindSpeedY += MathF.Sin(currentAngle) * currentStrength;

                    // Upwelling in the cyclone's wake (brings cold water to surface)
                    if (dist < radius * 0.5f)
                    {
                        cell.Temperature -= effectStrength * 0.5f;
                    }
                }

                // Damage to life from high winds
                bool isMajorHurricane = storm.Type >= StormType.HurricaneCategory3;
                if (isMajorHurricane && effectStrength > 0.5f)
                {
                    cell.Biomass *= 0.93f; // Severe storm damage
                }
                else if ((storm.Type >= StormType.TropicalStorm) && effectStrength > 0.6f)
                {
                    cell.Biomass *= 0.97f; // Moderate storm damage
                }

                // Storm surge damage on coastlines (hurricanes only)
                if (storm.Type >= StormType.HurricaneCategory1 && cell.IsLand && effectStrength > 0.7f)
                {
                    var neighbors = _map.GetNeighbors(x, y);
                    bool nearWater = neighbors.Any(n => n.cell.IsWater);

                    if (nearWater)
                    {
                        cell.Biomass *= 0.85f; // Coastal flooding damage
                    }
                }

                // Tornadoes can spawn in strong thunderstorms
                if (storm.Type == StormType.Thunderstorm && effectStrength > 0.6f &&
                    _random.NextDouble() < 0.0005)
                {
                    cell.Biomass *= 0.4f; // Severe localized tornado damage
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
    public float Intensity { get; set; }  // 0-1 scale
    public float VelocityX { get; set; }
    public float VelocityY { get; set; }
    public StormType Type { get; set; }
    public float Lifetime { get; set; }
    public float CentralPressure { get; set; } = 1013.25f;  // Millibars (lower = stronger)
    public float MaxWindSpeed { get; set; } = 0f;  // m/s
    public float SeaSurfaceTemp { get; set; } = 0f;  // Track temp for growth/decay
    public bool OverLand { get; set; } = false;  // Weakens over land
    public float RotationDirection { get; set; } = 0f;  // 1 for counterclockwise (NH), -1 for clockwise (SH)
}
