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
        UpdateEvaporation(deltaTime); // NEW: Continuous water evaporation
        UpdateCloudCover();
        UpdateStorms(deltaTime, currentYear);
        UpdatePrecipitation();
    }

    private void UpdateSeasons(float deltaTime, int currentYear)
    {
        // REALISTIC SEASONAL PROGRESSION WITH PLANETARY AXIS TILT
        // Axial tilt: 23.5° (like Earth) - causes seasons
        // Season progress: 0-4 represents one full orbit (year)
        _seasonProgress += deltaTime * 0.1f;
        if (_seasonProgress >= 4.0f)
            _seasonProgress -= 4.0f;

        // Convert season progress to orbital angle (0-2π)
        float orbitalAngle = _seasonProgress * MathF.PI / 2f; // 0-2π over 4 seasons
        const float axialTilt = 23.5f * (MathF.PI / 180f); // 23.5° in radians

        // Solar declination varies with orbital position
        // Declination: angle between equatorial plane and sun's rays
        // Summer solstice: +23.5°, Winter solstice: -23.5°, Equinoxes: 0°
        float solarDeclination = MathF.Sin(orbitalAngle) * axialTilt;

        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                var cell = _map.Cells[x, y];
                var met = cell.GetMeteorology();

                // Calculate latitude (-1 to +1, where +1 is north pole)
                float latitude = (y - _map.Height / 2.0f) / (_map.Height / 2.0f);
                float latitudeRadians = latitude * MathF.PI / 2f;

                // Determine local season based on hemisphere and orbital position
                // Northern hemisphere: 0=Spring, 1=Summer, 2=Fall, 3=Winter
                // Southern hemisphere: opposite (when NH has summer, SH has winter)
                float localSeasonProgress = _seasonProgress;
                if (latitude < 0) // Southern hemisphere
                    localSeasonProgress = (_seasonProgress + 2.0f) % 4.0f;

                met.Season = (int)localSeasonProgress;

                // CALCULATE SEASONAL SOLAR INTENSITY using spherical astronomy
                // Maximum sun elevation at solar noon based on latitude and declination
                float maxSunElevation = MathF.Sin(latitudeRadians) * MathF.Sin(solarDeclination) +
                                       MathF.Cos(latitudeRadians) * MathF.Cos(solarDeclination);

                // Convert to seasonal temperature effect
                // Higher sun = more heating, lower sun = less heating
                float seasonalHeating = maxSunElevation * 25f; // ±25°C seasonal variation

                // Apply stronger seasonal effects at higher latitudes
                float latitudeEffect = Math.Abs(latitude);
                seasonalHeating *= (0.5f + latitudeEffect * 0.5f); // Poles vary more than equator

                // Rainfall modifiers based on season and latitude
                float rainfallModifier = 1.0f;
                switch (met.Season)
                {
                    case 0: // Spring
                        rainfallModifier = 1.2f; // Spring showers
                        break;
                    case 1: // Summer
                        // Monsoons in tropics, dry in subtropics
                        rainfallModifier = Math.Abs(latitude) < 0.3f ? 1.5f : 0.8f;
                        break;
                    case 2: // Fall
                        rainfallModifier = 1.1f; // Moderate precipitation
                        break;
                    case 3: // Winter
                        // Tropical dry season, mid-latitude storms
                        rainfallModifier = Math.Abs(latitude) < 0.3f ? 0.7f : 1.3f;
                        break;
                }

                // Apply seasonal heating gradually (smooth transitions)
                float targetTemp = cell.Temperature + seasonalHeating * deltaTime * 0.02f;
                cell.Temperature += (targetTemp - cell.Temperature) * 0.05f;

                // Apply seasonal rainfall variations
                cell.Humidity = Math.Clamp(cell.Humidity * rainfallModifier, 0, 1);

                // DYNAMIC ICE SHEET FORMATION AND MELTING
                // Ice forms/melts based on sustained temperatures, not instant
                float absLatitude = Math.Abs(latitude);

                // Polar regions (|lat| > 0.7) - Permanent ice caps
                if (absLatitude > 0.7f && cell.Temperature < -5f)
                {
                    cell.IsIce = true; // Permanent polar ice
                }
                // High latitudes (0.5 < |lat| < 0.7) - Seasonal ice sheets
                else if (absLatitude > 0.5f)
                {
                    if (met.Season == 3 && cell.Temperature < -5f) // Winter
                    {
                        cell.IsIce = true; // Winter ice sheets expand
                    }
                    else if (met.Season == 1 && cell.Temperature > 5f) // Summer
                    {
                        if (cell.IsWater || cell.Elevation < 0.3f)
                            cell.IsIce = false; // Summer ice sheets retreat
                    }
                }
                // Mid-latitudes - Sea ice forms in winter
                else if (absLatitude > 0.3f && cell.IsWater)
                {
                    if (cell.Temperature < -2f)
                    {
                        cell.IsIce = true; // Winter sea ice
                    }
                    else if (cell.Temperature > 2f)
                    {
                        cell.IsIce = false; // Sea ice melts in spring
                    }
                }
                // Temperate and tropical - Ice only on high mountains
                else
                {
                    if (cell.Elevation > 0.7f && cell.Temperature < -10f)
                    {
                        cell.IsIce = true; // Mountain glaciers
                    }
                    else if (cell.Temperature > 0f)
                    {
                        if (cell.IsWater || cell.Elevation < 0.7f)
                            cell.IsIce = false; // Only high peaks keep ice
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
        // CONTINUOUS storm generation based on weather conditions (not random!)
        // Check multiple locations per update for storm formation potential
        int checksPerUpdate = (int)(30 * deltaTime); // Check 30 locations per frame

        for (int check = 0; check < checksPerUpdate; check++)
        {
            int x = _random.Next(_map.Width);
            int y = _random.Next(_map.Height);

            CheckAndGenerateStorm(x, y);
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

    private void CheckAndGenerateStorm(int x, int y)
    {
        var cell = _map.Cells[x, y];
        var met = cell.GetMeteorology();

        // Get latitude for storm type determination
        float latitude = (y - _map.Height / 2.0f) / (_map.Height / 2.0f);
        float absLatitude = Math.Abs(latitude);

        // Calculate wind convergence (check if winds are converging)
        float windConvergence = CalculateWindConvergence(x, y);

        // TROPICAL CYCLONE FORMATION (continuous process, not random!)
        // Requires specific conditions to form
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
            // Check if there's already a storm nearby
            bool hasNearbyStorm = _storms.Any(s =>
            {
                int dx = Math.Abs(s.CenterX - x);
                int dy = Math.Abs(s.CenterY - y);
                return dx < 30 && dy < 30;
            });

            if (!hasNearbyStorm && _random.NextDouble() < 0.001) // 0.1% chance when conditions perfect
            {
                // Start as tropical depression
                var storm = new Storm
                {
                    CenterX = x,
                    CenterY = y,
                    Intensity = 0.3f + (float)_random.NextDouble() * 0.2f,
                    Type = StormType.TropicalDepression,
                    VelocityX = met.WindSpeedX * 0.15f + (latitude > 0 ? 0.5f : -0.5f),
                    VelocityY = met.WindSpeedY * 0.15f + (latitude > 0 ? 0.2f : -0.2f),
                    CentralPressure = 1005f - (float)_random.NextDouble() * 10f,
                    MaxWindSpeed = 10f + (float)_random.NextDouble() * 5f,
                    SeaSurfaceTemp = cell.Temperature,
                    OverLand = false,
                    RotationDirection = latitude > 0 ? 1f : -1f
                };
                _storms.Add(storm);
            }
        }
        // THUNDERSTORM FORMATION (continuous process)
        else if (met.AirPressure < 1000 && cell.Humidity > 0.6f && met.CloudCover > 0.6f)
        {
            bool hasNearbyStorm = _storms.Any(s =>
            {
                int dx = Math.Abs(s.CenterX - x);
                int dy = Math.Abs(s.CenterY - y);
                return dx < 15 && dy < 15;
            });

            if (!hasNearbyStorm && _random.NextDouble() < 0.002) // 0.2% chance when conditions right
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
        }
        // BLIZZARD FORMATION (continuous process)
        else if (cell.Temperature < 0 && cell.Humidity > 0.5f && met.CloudCover > 0.7f)
        {
            bool hasNearbyStorm = _storms.Any(s =>
            {
                int dx = Math.Abs(s.CenterX - x);
                int dy = Math.Abs(s.CenterY - y);
                return dx < 15 && dy < 15;
            });

            if (!hasNearbyStorm && _random.NextDouble() < 0.002) // 0.2% chance when conditions right
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
    }

    private void GenerateStorm()
    {
        int x = _random.Next(_map.Width);
        int y = _random.Next(_map.Height);
        CheckAndGenerateStorm(x, y);
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
                    float currentAngle = MathF.Atan2(dy, dx);
                    currentAngle = currentAngle + (storm.RotationDirection * MathF.PI / 2f);
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

    private void UpdateEvaporation(float deltaTime)
    {
        // CONTINUOUS WATER EVAPORATION PROCESS
        // This is critical for the water cycle: evaporation → clouds → rain → runoff → ocean
        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                var cell = _map.Cells[x, y];
                var met = cell.GetMeteorology();

                // Water bodies evaporate continuously
                if (cell.IsWater && !cell.IsIce)
                {
                    // Evaporation rate increases with:
                    // 1. Temperature (warmer water = more evaporation)
                    // 2. Wind speed (wind carries moisture away)
                    // 3. Low humidity (dry air can hold more moisture)

                    float tempFactor = Math.Clamp((cell.Temperature + 10) / 40f, 0, 2); // Peak at 30°C
                    float windFactor = MathF.Sqrt(met.WindSpeedX * met.WindSpeedX + met.WindSpeedY * met.WindSpeedY) * 0.05f;
                    float humidityFactor = (1.0f - cell.Humidity); // More evap when dry

                    // Base evaporation rate (continuous process!)
                    float evaporationRate = 0.02f * tempFactor * (1.0f + windFactor) * (0.5f + humidityFactor) * deltaTime;

                    // Oceans evaporate more than lakes (more surface area)
                    if (cell.Elevation < -0.1f) // Deep ocean
                    {
                        evaporationRate *= 1.5f;
                    }

                    // Add moisture to air (increases humidity)
                    cell.Humidity = Math.Min(1.0f, cell.Humidity + evaporationRate);

                    // Evaporation cools water surface slightly
                    cell.Temperature -= evaporationRate * 0.1f;
                }
                // Land also evaporates water (from soil moisture, vegetation)
                else if (cell.IsLand && !cell.IsIce && cell.Rainfall > 0.1f)
                {
                    // Evapotranspiration from plants and soil
                    float moistureFactor = cell.Rainfall;
                    float tempFactor = Math.Clamp((cell.Temperature + 5) / 35f, 0, 1.5f);
                    float biomassFactor = cell.Biomass * 0.5f; // Plants increase evapotranspiration

                    float evapotranspiration = 0.01f * tempFactor * moistureFactor * (1.0f + biomassFactor) * deltaTime;

                    cell.Humidity = Math.Min(1.0f, cell.Humidity + evapotranspiration);
                }

                // Wind transports moisture horizontally
                if (met.WindSpeedX != 0 || met.WindSpeedY != 0)
                {
                    // Calculate target cell based on wind direction
                    int targetX = x + (int)Math.Sign(met.WindSpeedX);
                    int targetY = y + (int)Math.Sign(met.WindSpeedY);

                    targetX = (targetX + _map.Width) % _map.Width; // Wrap horizontally
                    if (targetY >= 0 && targetY < _map.Height)
                    {
                        var targetCell = _map.Cells[targetX, targetY];
                        float windStrength = MathF.Sqrt(met.WindSpeedX * met.WindSpeedX + met.WindSpeedY * met.WindSpeedY);

                        // Transport moisture downwind
                        float moistureTransport = cell.Humidity * windStrength * 0.005f * deltaTime;
                        cell.Humidity = Math.Max(0, cell.Humidity - moistureTransport);
                        targetCell.Humidity = Math.Min(1.0f, targetCell.Humidity + moistureTransport);
                    }
                }
            }
        }
    }

    private void UpdateCloudCover()
    {
        // CONTINUOUS CLOUD FORMATION from humidity (from evaporation!)
        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                var cell = _map.Cells[x, y];
                var met = cell.GetMeteorology();

                // Clouds form when humidity is high
                float targetClouds = 0;

                // High humidity = cloud formation
                if (cell.Humidity > 0.6f)
                {
                    targetClouds = (cell.Humidity - 0.5f) * 2.0f; // Scale from 0.6-1.0 → 0.2-1.0
                }

                // Low pressure enhances cloud formation (rising air)
                if (met.AirPressure < 1010)
                {
                    float pressureEffect = (1010 - met.AirPressure) / 60f; // 0-1 scale
                    targetClouds += pressureEffect * 0.3f;
                }

                // Mountains force air to rise, creating orographic clouds
                if (cell.Elevation > 0.4f && cell.Humidity > 0.5f)
                {
                    targetClouds += 0.3f;
                }

                // Storms have full cloud cover
                if (met.InStorm)
                {
                    targetClouds = 1.0f;
                }

                // Gradual cloud formation/dissipation
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
