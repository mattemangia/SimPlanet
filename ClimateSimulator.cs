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
                var met = cell.GetMeteorology();

                // Base temperature from latitude with stronger polar cooling
                float latitude = Math.Abs((y - _map.Height / 2.0f) / (_map.Height / 2.0f));
                float signedLatitude = (y - _map.Height / 2.0f) / (_map.Height / 2.0f);

                // *** ENHANCED: STRONGER LONGITUDINAL VARIATIONS ***
                // Ocean currents create warm/cold zones (Gulf Stream, Kuroshio, Peru Current)
                float oceanCurrentEffect = 0;
                if (cell.IsWater)
                {
                    // Warm currents on west coasts in mid-latitudes, cold currents on east coasts
                    float currentPattern = MathF.Sin(x * 0.15f + signedLatitude * 3f) * 8.0f; // Up to Â±8Â°C
                    oceanCurrentEffect = currentPattern;
                }

                // Land heating variations (continentality - land heats/cools faster than ocean)
                float continentalityEffect = 0;
                if (cell.IsLand)
                {
                    // Check distance to ocean (simplified as neighbor count)
                    int oceanNeighbors = 0;
                    foreach (var (nx, ny, neighbor) in _map.GetNeighbors(x, y))
                    {
                        if (neighbor.IsWater) oceanNeighbors++;
                    }
                    float continentality = 1.0f - (oceanNeighbors / 8.0f); // 0=coastal, 1=interior

                    // Continental interiors have more extreme temperatures
                    continentalityEffect = continentality * latitude * 10f; // Interior cools more at high lat
                }

                // Local topographic effects create temperature pockets
                float topographicVariation = MathF.Sin(x * 0.4f) * MathF.Cos(y * 0.35f) * 3.0f;

                // Realistic temperature gradient: hot equator, freezing poles
                // Equator (lat=0): ~30Â°C, Poles (lat=1): ~-40Â°C
                float baseTemp = 30 - (latitude * latitude * 70) + oceanCurrentEffect +
                                continentalityEffect + topographicVariation;

                // Calculate surface albedo (reflection coefficient)
                float albedo = CalculateAlbedo(cell);

                // Validate albedo
                if (float.IsNaN(albedo) || float.IsInfinity(albedo))
                    albedo = 0.3f;

                // Validate map properties
                float solarEnergy = _map.SolarEnergy;
                if (float.IsNaN(solarEnergy) || float.IsInfinity(solarEnergy) || solarEnergy < 0)
                    solarEnergy = 1.0f;

                // Albedo reduces absorbed solar energy (higher albedo = more reflection = less heating)
                float solarHeating = baseTemp * solarEnergy * (1.0f - albedo);

                // Validate solarHeating
                if (float.IsNaN(solarHeating) || float.IsInfinity(solarHeating))
                    solarHeating = 15.0f;

                // Elevation cooling (6.5Â°C per km, roughly 0.65Â°C per 0.1 elevation)
                float elevation = cell.Elevation;
                if (float.IsNaN(elevation) || float.IsInfinity(elevation))
                    elevation = 0;

                if (elevation > 0)
                {
                    solarHeating -= elevation * 20; // Increased cooling
                }

                // Greenhouse effect
                float greenhouse = cell.Greenhouse;
                if (float.IsNaN(greenhouse) || float.IsInfinity(greenhouse))
                    greenhouse = 0.3f;

                solarHeating += greenhouse * 20;

                // Water moderates temperature (oceanic thermal inertia)
                if (cell.IsWater)
                {
                    solarHeating += 5;
                }

                // Validate solarHeating after all modifications
                if (float.IsNaN(solarHeating) || float.IsInfinity(solarHeating))
                    solarHeating = 15.0f;

                // *** WIND-DRIVEN HEAT TRANSPORT ***
                // Advection: winds carry warm/cold air

                // Validate current cell temperature (prevent NaN propagation)
                if (float.IsNaN(cell.Temperature) || float.IsInfinity(cell.Temperature))
                    cell.Temperature = 15.0f; // Reset to reasonable default

                float windTemp = cell.Temperature; // Default to current temp

                // Validate wind speeds before use
                float windSpeedX = met.WindSpeedX;
                float windSpeedY = met.WindSpeedY;
                if (float.IsNaN(windSpeedX) || float.IsInfinity(windSpeedX)) windSpeedX = 0;
                if (float.IsNaN(windSpeedY) || float.IsInfinity(windSpeedY)) windSpeedY = 0;

                if (Math.Abs(windSpeedX) > 0.5f || Math.Abs(windSpeedY) > 0.5f)
                {
                    // Calculate upwind cell (where wind is coming from)
                    int upwindX = x - (int)Math.Sign(windSpeedX);
                    int upwindY = y - (int)Math.Sign(windSpeedY);

                    upwindX = (upwindX + _map.Width) % _map.Width;
                    upwindY = Math.Clamp(upwindY, 0, _map.Height - 1);

                    var upwindCell = _map.Cells[upwindX, upwindY];

                    // Validate upwind temperature
                    float upwindTemp = upwindCell.Temperature;
                    if (float.IsNaN(upwindTemp) || float.IsInfinity(upwindTemp))
                        upwindTemp = cell.Temperature; // Fallback to current temp

                    float windSpeed = MathF.Sqrt(windSpeedX * windSpeedX + windSpeedY * windSpeedY);

                    // Validate windSpeed
                    if (float.IsNaN(windSpeed) || float.IsInfinity(windSpeed))
                        windSpeed = 0;

                    // Strong winds transport more heat
                    float advectionStrength = Math.Clamp(windSpeed / 10f, 0, 0.6f); // Max 60% influence
                    windTemp = cell.Temperature * (1f - advectionStrength) + upwindTemp * advectionStrength;

                    // Validate result
                    if (float.IsNaN(windTemp) || float.IsInfinity(windTemp))
                        windTemp = cell.Temperature;
                }

                // *** REDUCED DIFFUSION (was 80%, now 30%) ***
                // Heat diffusion from neighbors (prevents sharp boundaries but doesn't create bands)
                float neighborTemp = 0;
                int neighborCount = 0;
                foreach (var (nx, ny, neighbor) in _map.GetNeighbors(x, y))
                {
                    float nTemp = neighbor.Temperature;
                    // Validate neighbor temperature
                    if (float.IsNaN(nTemp) || float.IsInfinity(nTemp))
                        nTemp = cell.Temperature; // Use current cell as fallback

                    neighborTemp += nTemp;
                    neighborCount++;
                }
                if (neighborCount > 0)
                {
                    neighborTemp /= neighborCount;
                }
                else
                {
                    neighborTemp = cell.Temperature;
                }

                // Validate all intermediate values
                if (float.IsNaN(solarHeating) || float.IsInfinity(solarHeating))
                    solarHeating = 15.0f; // Default moderate temperature
                if (float.IsNaN(windTemp) || float.IsInfinity(windTemp))
                    windTemp = cell.Temperature;
                if (float.IsNaN(neighborTemp) || float.IsInfinity(neighborTemp))
                    neighborTemp = cell.Temperature;

                // *** KEY FIX: Reduced neighbor influence from 80% to 30% ***
                // This prevents band formation while still smoothing sharp edges
                float targetTemp = solarHeating * 0.5f + windTemp * 0.2f + neighborTemp * 0.3f;

                // Validate targetTemp
                if (float.IsNaN(targetTemp) || float.IsInfinity(targetTemp))
                    targetTemp = cell.Temperature;

                float newTemp = cell.Temperature + (targetTemp - cell.Temperature) * deltaTime * 0.1f;

                // Final validation and clamping to physically reasonable range
                if (float.IsNaN(newTemp) || float.IsInfinity(newTemp))
                    newTemp = 15.0f; // Default to moderate temperature

                // Clamp to physically possible range (-100°C to 100°C)
                newTemp = Math.Clamp(newTemp, -100f, 100f);

                newTemperatures[x, y] = newTemp;
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

                // SMOOTH latitude-based rainfall with continuous transitions
                float latitudeEffect;
                
                // ITCZ effect (peak at equator, fade by 15°)
                float itczEffect = Math.Max(0, 1.0f - (latitude / 0.15f));
                
                // Subtropical desert effect (peak around 25-30°)
                float desertPeak = 0.28f;
                float desertWidth = 0.2f;
                float distanceFromPeak = Math.Abs(latitude - desertPeak);
                float desertEffect = Math.Max(0, 1.0f - (distanceFromPeak / desertWidth));
                
                // Mid-latitude effect (peak around 45-55°)
                float midLatPeak = 0.5f;
                float midLatWidth = 0.25f;
                float distanceFromMidLat = Math.Abs(latitude - midLatPeak);
                float midLatEffect = Math.Max(0, 1.0f - (distanceFromMidLat / midLatWidth));
                
                // Polar desert effect (increase dryness toward poles)
                float polarEffect = Math.Max(0, (latitude - 0.65f) / 0.35f);
                
                // Blend all effects smoothly
                latitudeEffect = 1.3f * itczEffect +                    // Wet equator
                                0.15f * desertEffect +                  // Dry subtropics
                                0.9f * midLatEffect +                   // Wet mid-latitudes
                                0.3f * (1.0f - desertEffect - midLatEffect - itczEffect) + // Transition zones
                                -0.5f * polarEffect;                    // Dry poles
                                
                latitudeEffect = Math.Max(0.1f, latitudeEffect + rainfallVariation);

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

                // Validate rainfall
                float rainfall = cell.Rainfall;
                if (float.IsNaN(rainfall) || float.IsInfinity(rainfall))
                    rainfall = 0;

                // Humidity from rainfall
                float humidityFromRain = rainfall * 0.8f;

                // Humidity diffusion from neighbors
                float neighborHumidity = 0;
                int waterNeighbors = 0;
                foreach (var (nx, ny, neighbor) in _map.GetNeighbors(x, y))
                {
                    float nHumidity = neighbor.Humidity;
                    if (float.IsNaN(nHumidity) || float.IsInfinity(nHumidity))
                        nHumidity = 0.5f;

                    neighborHumidity += nHumidity;
                    if (neighbor.IsWater) waterNeighbors++;
                }
                neighborHumidity /= 8;

                // Validate neighborHumidity
                if (float.IsNaN(neighborHumidity) || float.IsInfinity(neighborHumidity))
                    neighborHumidity = 0.5f;

                // More water neighbors = more humid
                float waterEffect = waterNeighbors / 8.0f;

                float targetHumidity = Math.Max(humidityFromRain, neighborHumidity * 0.5f + waterEffect * 0.3f);

                // Validate targetHumidity
                if (float.IsNaN(targetHumidity) || float.IsInfinity(targetHumidity))
                    targetHumidity = 0.5f;

                // Validate current humidity
                float currentHumidity = cell.Humidity;
                if (float.IsNaN(currentHumidity) || float.IsInfinity(currentHumidity))
                    currentHumidity = 0.5f;

                float newHum = currentHumidity + (targetHumidity - currentHumidity) * deltaTime * 0.2f;

                // Final validation and clamping
                if (float.IsNaN(newHum) || float.IsInfinity(newHum))
                    newHum = 0.5f;

                newHum = Math.Clamp(newHum, 0f, 1f);
                newHumidity[x, y] = newHum;
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