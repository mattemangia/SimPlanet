namespace SimPlanet;

/// <summary>
/// Simulates climate, temperature, rainfall, and weather patterns
/// </summary>
public class ClimateSimulator
{
    private readonly PlanetMap _map;
    private readonly Random _random;
    private float _previousIceVolume = 0f; // Track ice volume to adjust water levels

    public ClimateSimulator(PlanetMap map)
    {
        _map = map;
        _random = new Random();
        _previousIceVolume = CalculateLandIceVolume(); // Initialize
    }

    public void Update(float deltaTime)
    {
        SimulateTemperature(deltaTime);
        SimulateRainfall(deltaTime);
        SimulateHumidity(deltaTime);
        UpdateIceCycles(deltaTime);
        UpdateWaterLevelFromIce(); // Adjust sea level based on ice sheet changes
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

                // Add longitude variation to prevent perfect horizontal bands
                float longitudeVariation = MathF.Sin(x * 0.3f) * 2.0f; // Small variation across longitude

                // Realistic temperature gradient: hot equator, freezing poles
                // Equator (lat=0): ~30°C, Poles (lat=1): ~-40°C
                float baseTemp = 30 - (latitude * latitude * 70) + longitudeVariation;

                // Calculate surface albedo (reflection coefficient)
                float albedo = CalculateAlbedo(cell);

                // Albedo reduces absorbed solar energy (higher albedo = more reflection = less heating)
                float solarHeating = baseTemp * _map.SolarEnergy * (1.0f - albedo);

                // Elevation cooling (6.5°C per km, roughly 0.65°C per 0.1 elevation)
                if (cell.Elevation > 0)
                {
                    solarHeating -= cell.Elevation * 20; // Increased cooling
                }

                // Greenhouse effect
                solarHeating += cell.Greenhouse * 20;

                // Water moderates temperature (oceanic thermal inertia)
                if (cell.IsWater)
                {
                    solarHeating += 5;
                }

                // Heat diffusion from neighbors (prevents sharp boundaries)
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

                // Blend current temperature with target - higher neighbor influence prevents bands
                float targetTemp = solarHeating * 0.2f + neighborTemp * 0.8f;
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

                // Add longitude variation to break up perfect bands
                float rainfallVariation = MathF.Sin(x * 0.2f + y * 0.1f) * 0.15f;

                float latitudeEffect;
                if (latitude < 0.12f)
                {
                    // ITCZ (Intertropical Convergence Zone) - Heavy rainfall at equator
                    latitudeEffect = 1.3f + rainfallVariation;
                }
                else if (latitude < 0.45f)
                {
                    // Subtropical high pressure - Deserts (Hadley cell descending air)
                    // Peak aridity around 25-30 degrees (0.25-0.35 latitude)
                    float desertPeak = 0.28f;
                    float desertWidth = 0.18f;
                    float distanceFromPeak = Math.Abs(latitude - desertPeak);
                    float desertStrength = Math.Max(0, 1.0f - (distanceFromPeak / desertWidth));

                    // Strong desert belt effect - very dry at peak
                    latitudeEffect = 0.15f + (0.5f * (1.0f - desertStrength)) + rainfallVariation;
                    latitudeEffect = Math.Max(0.1f, latitudeEffect); // Ensure truly arid zones
                }
                else if (latitude < 0.75f)
                {
                    // Mid-latitudes (Ferrel cell) - Moderate to high rainfall
                    float midLatEffect = (latitude - 0.45f) / 0.3f; // 0 to 1
                    latitudeEffect = 1.0f - (midLatEffect * 0.3f) + rainfallVariation; // 1.0 to 0.7
                }
                else
                {
                    // Polar regions - Cold deserts (low moisture capacity due to cold)
                    latitudeEffect = 0.25f + rainfallVariation * 0.5f;
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

    private void UpdateIceCycles(float deltaTime)
    {
        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                var cell = _map.Cells[x, y];

                // Calculate latitude (0 = equator, 1 = poles)
                float latitude = Math.Abs((y - _map.Height / 2.0f) / (_map.Height / 2.0f));

                // Add geographic variation to prevent horizontal ice bands
                float geoVariation = MathF.Sin(x * 0.4f + y * 0.2f) * 0.08f; // Breaks up bands
                float adjustedLatitude = latitude + geoVariation;

                // Gradual transition - not sharp boundary
                bool isPolarRegion = adjustedLatitude > 0.72f;
                float polarStrength = Math.Max(0, (adjustedLatitude - 0.72f) / 0.28f); // 0 to 1 gradient
                bool isMountainPeak = cell.Elevation > 0.7f; // High elevation

                // Ice accumulation threshold varies smoothly by location with geographic noise
                float tempVariation = MathF.Sin(x * 0.25f) * MathF.Cos(y * 0.15f) * 3.0f;
                float iceFormationTemp = -10f + (polarStrength * 5f) + tempVariation;
                float iceMeltingTemp = 5f - (polarStrength * 3f) + (tempVariation * 0.5f);

                // Mountain snow line (permanent ice at high elevations)
                if (isMountainPeak)
                {
                    float snowLine = 0f - (latitude * 15f); // Snow line lower at higher latitudes
                    if (cell.Temperature < snowLine)
                    {
                        // Permanent mountain ice caps
                        cell.Temperature = Math.Min(cell.Temperature, snowLine - 5); // Keep cold
                    }
                }

                // Polar ice sheets (gradual, not banded)
                if (isPolarRegion && polarStrength > 0.15f)
                {
                    // Ice accumulation in polar regions
                    if (cell.Temperature < iceFormationTemp)
                    {
                        // Sea ice forms on ocean
                        if (cell.IsWater)
                        {
                            // Gradual ice-albedo feedback proportional to polar strength
                            // Reduced strength to prevent sharp boundaries
                            cell.Temperature -= deltaTime * 0.6f * polarStrength;

                            // Frozen ocean reduces evaporation
                            cell.Humidity = Math.Max(cell.Humidity - deltaTime * 0.08f * polarStrength, 0.2f);
                        }
                        // Glaciers accumulate on land
                        else if (cell.IsLand)
                        {
                            // Ice sheets slowly increase elevation (glacier growth)
                            cell.Elevation += deltaTime * 0.00006f * polarStrength;
                            cell.Elevation = Math.Min(cell.Elevation, 1.0f);

                            // Gradual ice-albedo feedback
                            cell.Temperature -= deltaTime * 0.5f * polarStrength;
                        }
                    }
                    // Melting
                    else if (cell.Temperature > iceMeltingTemp && cell.IsIce)
                    {
                        // Glacier retreat
                        if (cell.IsLand && cell.Elevation > 0.2f)
                        {
                            cell.Elevation -= deltaTime * 0.00015f; // Faster melting than accumulation
                        }

                        // Melting ice increases humidity
                        cell.Humidity = Math.Min(cell.Humidity + deltaTime * 0.15f, 1.0f);
                    }
                }

                // Non-polar ice (seasonal and mountain ice)
                else
                {
                    // Ice forms at very cold temperatures
                    if (cell.Temperature < iceFormationTemp)
                    {
                        if (cell.IsLand)
                        {
                            // Seasonal snow accumulation
                            cell.Humidity = Math.Min(cell.Humidity + deltaTime * 0.05f, 0.9f);

                            // High altitude glaciers
                            if (isMountainPeak)
                            {
                                cell.Elevation += deltaTime * 0.00005f; // Slower than polar
                                cell.Temperature -= deltaTime * 0.5f; // Albedo effect
                            }
                        }
                    }
                    // Melting
                    else if (cell.Temperature > iceMeltingTemp && cell.IsIce)
                    {
                        // Rapid melting in non-polar regions
                        if (cell.IsLand && isMountainPeak)
                        {
                            cell.Elevation -= deltaTime * 0.0003f; // Fast glacier retreat
                        }

                        // Meltwater increases humidity and can create lakes
                        cell.Humidity = Math.Min(cell.Humidity + deltaTime * 0.2f, 1.0f);
                        cell.Rainfall = Math.Min(cell.Rainfall + deltaTime * 0.1f, 1.0f);
                    }
                }

                // Global ice-albedo feedback (highly reduced to prevent banding)
                // Ice reflects more sunlight, reducing local heating
                if (cell.IsIce)
                {
                    // Very moderate albedo feedback - prevents runaway ice formation and banding
                    // Use geographic variation to avoid uniform cooling
                    float localVariation = MathF.Sin(x * 0.5f + y * 0.3f) * 0.1f;
                    float feedbackStrength = (isPolarRegion ? 0.2f : 0.15f) + localVariation;
                    cell.Temperature -= deltaTime * Math.Max(0.05f, feedbackStrength);

                    // Prevent runaway cooling with latitude-dependent limits
                    float minTemp = isPolarRegion ? -50f : -30f;
                    cell.Temperature = Math.Max(cell.Temperature, minTemp);
                }
            }
        }
    }

    private float CalculateAlbedo(TerrainCell cell)
    {
        // Albedo = fraction of sunlight reflected by surface (0 = all absorbed, 1 = all reflected)
        float albedo = 0.0f;

        // Ice and snow (highest albedo)
        if (cell.IsIce)
        {
            // Fresh snow/ice: 0.8-0.9
            albedo = 0.85f;
        }
        // Water (low albedo)
        else if (cell.IsWater)
        {
            // Ocean water: 0.06-0.10 (dark, absorbs most sunlight)
            if (cell.Elevation < -0.3f)
                albedo = 0.06f; // Deep ocean (very dark)
            else
                albedo = 0.08f; // Shallow water
        }
        // Desert (medium-high albedo)
        else if (cell.IsDesert)
        {
            // Sand: 0.30-0.40 (bright, reflects well)
            albedo = 0.35f;
        }
        // Forest (low-medium albedo)
        else if (cell.IsForest)
        {
            // Dense forest: 0.15-0.20 (dark green, absorbs well)
            albedo = 0.17f;
        }
        // Grassland (medium albedo)
        else if (cell.Rainfall > 0.3f && cell.IsLand)
        {
            // Grass: 0.20-0.25
            albedo = 0.23f;
        }
        // Bare rock/mountains (low-medium albedo)
        else if (cell.Elevation > 0.5f)
        {
            // Rock: 0.10-0.20 (depends on color)
            albedo = 0.15f;
        }
        // Tundra/barren land
        else
        {
            // Mixed vegetation/rock: 0.15-0.25
            albedo = 0.20f;
        }

        // Urban areas (civilizations) have different albedo
        if (cell.LifeType == LifeForm.Civilization)
        {
            // Cities: 0.15-0.25 (concrete, asphalt)
            albedo = 0.20f;
        }

        // Roads modify albedo (asphalt/concrete is dark)
        var geo = cell.GetGeology();
        if (geo.HasRoad)
        {
            // Roads: 0.08-0.12 depending on type (asphalt is very dark, absorbs heat)
            albedo = geo.RoadType switch
            {
                RoadType.Highway => 0.08f,   // Fresh asphalt (darkest)
                RoadType.Road => 0.10f,      // Paved road
                RoadType.DirtPath => 0.18f,  // Dirt path (lighter)
                _ => albedo
            };
        }

        // Solar panels have very low albedo (0.05-0.15, absorb sunlight for energy)
        if (geo.HasSolarFarm)
        {
            albedo = 0.10f; // Dark solar panels
        }

        // Clouds increase effective albedo (in future enhancement)
        // For now, clouds are handled separately in weather system

        return Math.Clamp(albedo, 0.0f, 1.0f);
    }

    /// <summary>
    /// Calculate total ice volume on land (not sea ice, as sea ice doesn't affect sea level)
    /// Ice sheets lock up water on land, removing it from the ocean
    /// </summary>
    private float CalculateLandIceVolume()
    {
        float iceVolume = 0f;

        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                var cell = _map.Cells[x, y];

                // Only count ice on land (glaciers and ice sheets)
                // Sea ice doesn't change sea level (it's already floating)
                if (cell.IsLand && cell.IsIce && cell.Elevation > 0)
                {
                    // Ice volume is proportional to elevation above sea level
                    iceVolume += cell.Elevation;
                }
            }
        }

        return iceVolume;
    }

    /// <summary>
    /// Update global water level based on ice sheet volume changes
    /// When ice sheets grow, water is locked on land -> sea level drops
    /// When ice sheets melt, water returns to ocean -> sea level rises
    /// </summary>
    private void UpdateWaterLevelFromIce()
    {
        float currentIceVolume = CalculateLandIceVolume();
        float iceVolumeChange = currentIceVolume - _previousIceVolume;

        // Convert ice volume change to water level change
        // Scaling factor: ice sheet volume to global sea level
        // Negative because more ice = lower sea level
        float waterLevelChange = -iceVolumeChange * 0.0001f;

        // Apply water level change
        _map.Options.WaterLevel = Math.Clamp(_map.Options.WaterLevel + waterLevelChange, -1.0f, 1.0f);

        // Update tracking
        _previousIceVolume = currentIceVolume;
    }
}
