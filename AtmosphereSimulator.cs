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
        UpdateAtmosphericLayers(deltaTime);
        UpdateSpectralRadiativeTransfer();
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

    private void UpdateAtmosphericLayers(float deltaTime)
    {
        // Update temperature profile of atmospheric layers based on surface conditions
        // Implements realistic lapse rates and stratospheric warming from ozone

        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                var cell = _map.Cells[x, y];
                var met = cell.GetMeteorology();
                var column = met.Column;

                // Surface temperature (Celsius to Kelvin)
                column.SurfaceTemp = cell.Temperature + 273.15f;

                // Tropospheric lapse rate: -6.5 K/km (environmental lapse rate)
                // Layer 1: 2-8 km above surface (average 5 km)
                column.LowerTropTemp = column.SurfaceTemp - (6.5f * 5f);

                // Layer 2: 8-12 km above surface (average 10 km)
                column.UpperTropTemp = column.SurfaceTemp - (6.5f * 10f);

                // Stratosphere: Temperature inversion due to ozone absorption
                // Warms with altitude from ~220K to ~270K
                float ozoneHeating = column.OzoneColumn / 300f * 20f; // Ozone heating effect
                column.StratosphereTemp = 220f + ozoneHeating;

                // Water vapor column from humidity (kg/m²)
                // Clausius-Clapeyron: exponential increase with temperature
                float saturationVaporPressure = 6.112f * MathF.Exp(17.67f * cell.Temperature / (cell.Temperature + 243.5f));
                column.WaterVaporColumn = cell.Humidity * saturationVaporPressure * 2.5f;
                column.WaterVaporColumn = Math.Clamp(column.WaterVaporColumn, 0, 70); // 0-70 kg/m²

                // Ozone column varies with latitude (higher at poles, lower at equator)
                float latitude = Math.Abs((y - _map.Height / 2.0f) / (_map.Height / 2.0f));
                column.OzoneColumn = 250f + (latitude * 150f); // 250-400 DU
                column.OzoneColumn = Math.Clamp(column.OzoneColumn, 150, 500);
            }
        }
    }

    private void UpdateSpectralRadiativeTransfer()
    {
        // Multi-band radiative transfer through atmospheric layers
        // Implements two-stream approximation for upward and downward fluxes
        // Source: Pierrehumbert "Principles of Planetary Climate" (2010)

        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                var cell = _map.Cells[x, y];
                var met = cell.GetMeteorology();
                var column = met.Column;

                // Calculate latitude for solar geometry
                float latitude = (y - _map.Height / 2.0f) / (_map.Height / 2.0f);
                float latitudeRad = latitude * (MathF.PI / 2f);

                // ===== SHORTWAVE (SOLAR) RADIATION =====
                // Incoming solar radiation at top of atmosphere
                float solarConstant = 1361f * _map.SolarEnergy; // W/m² (solar constant)
                float cosSolarZenith = Math.Max(0, MathF.Cos(latitudeRad)); // Simplified: assumes noon
                float solarTOA = solarConstant * cosSolarZenith;

                // Ozone absorption in stratosphere (UV and visible)
                // Ozone absorbs ~3% of solar radiation
                float ozoneAbsorption = column.OzoneColumn / 300f * 0.03f;
                float solarAfterOzone = solarTOA * (1f - ozoneAbsorption);

                // Rayleigh scattering in atmosphere (~10% of solar)
                float rayleighScatter = 0.10f * met.AirPressure / 1013.25f;

                // Cloud reflection and absorption
                float cloudReflection = met.CloudCover * 0.5f; // Thick clouds reflect 50%
                float cloudAbsorption = met.CloudCover * 0.1f; // Clouds absorb 10%

                // Water vapor absorption (near-IR bands)
                // Absorbs ~10-20% depending on column amount
                float waterVaporAbsorption = Math.Min(0.2f, column.WaterVaporColumn / 50f * 0.15f);

                // Total atmospheric attenuation
                float atmosphericTransmission = (1f - rayleighScatter) * (1f - cloudReflection) *
                                               (1f - cloudAbsorption) * (1f - waterVaporAbsorption);

                // Shortwave reaching surface
                column.ShortwaveDownSurface = solarAfterOzone * atmosphericTransmission;

                // Surface reflection (albedo)
                column.ShortwaveUpSurface = column.ShortwaveDownSurface * cell.Albedo;

                // ===== LONGWAVE (THERMAL INFRARED) RADIATION =====
                // Stefan-Boltzmann constant
                const float sigma = 5.67e-8f; // W m⁻² K⁻⁴

                // Surface emission (blackbody, emissivity ≈ 0.95)
                float surfaceEmissivity = 0.95f;
                column.LongwaveUpSurface = surfaceEmissivity * sigma *
                                          MathF.Pow(column.SurfaceTemp, 4);

                // ===== ATMOSPHERIC ABSORPTION BY LAYER =====

                // Calculate absorption coefficients for each gas
                // These are simplified spectral absorption in the thermal IR window

                // CO2 absorption (15 μm band) - strongest greenhouse gas band
                float co2Absorption = CalculateCO2Absorption(cell.CO2);

                // H2O absorption (multiple bands: 6.3 μm, rotation band)
                float h2oAbsorption = CalculateWaterVaporAbsorption(column.WaterVaporColumn);

                // CH4 absorption (7.6 μm band)
                float ch4Absorption = CalculateMethaneAbsorption(cell.Methane);

                // N2O absorption (overlaps with CO2 at 7.8 μm)
                float n2oAbsorption = CalculateN2OAbsorption(cell.NitrousOxide);

                // Cloud absorption/emission (clouds are nearly blackbodies in IR)
                float cloudEmission = met.CloudCover;

                // Total atmospheric absorptivity/emissivity
                float totalAbsorption = Math.Min(0.95f, co2Absorption + h2oAbsorption +
                                                       ch4Absorption + n2oAbsorption + cloudEmission);

                // Effective atmospheric temperature (weighted average of layers)
                float effectiveAtmosphereTemp = (column.LowerTropTemp * 0.6f +
                                                column.UpperTropTemp * 0.3f +
                                                column.StratosphereTemp * 0.1f);

                // Atmospheric back-radiation to surface
                column.LongwaveDownSurface = totalAbsorption * sigma *
                                            MathF.Pow(effectiveAtmosphereTemp, 4);

                // Outgoing longwave radiation at top of atmosphere
                // Some surface radiation escapes through atmospheric window
                float atmosphericWindow = (1f - totalAbsorption);
                float atmosphericEmission = totalAbsorption * sigma *
                                           MathF.Pow(effectiveAtmosphereTemp, 4);

                column.LongwaveUpTOA = (column.LongwaveUpSurface * atmosphericWindow) +
                                      (atmosphericEmission * 0.5f); // Half emitted upward

                // ===== NET RADIATION AND TEMPERATURE EFFECT =====
                // Net radiation budget affects surface temperature
                float netShortwave = column.ShortwaveDownSurface - column.ShortwaveUpSurface;
                float netLongwave = column.LongwaveDownSurface - column.LongwaveUpSurface;
                float netRadiation = netShortwave + netLongwave;

                // Store net radiation effect (used in climate simulator)
                // This replaces the simple greenhouse multiplier
                cell.Greenhouse = netLongwave / 100f; // Normalized greenhouse forcing
                cell.Greenhouse = Math.Clamp(cell.Greenhouse, 0, 5);
            }
        }
    }

    private float CalculateCO2Absorption(float co2Concentration)
    {
        // CO2 absorption in 15 μm band (most important greenhouse band)
        // Logarithmic dependence: doubling CO2 adds constant forcing
        // Source: IPCC radiative forcing formula

        float co2Reference = 280f; // Pre-industrial CO2 (ppm equivalent)
        float co2Current = Math.Max(co2Concentration, 1f);

        // Logarithmic absorption: Δα ≈ 5.35 × ln(C/C₀)
        float absorption = 0.12f + 0.05f * MathF.Log(co2Current / co2Reference);
        return Math.Clamp(absorption, 0.05f, 0.35f);
    }

    private float CalculateWaterVaporAbsorption(float waterColumn)
    {
        // Water vapor absorption (continuum + rotation band)
        // Strongest greenhouse gas, but condensable (feedback)
        // Absorption increases with column amount (kg/m²)

        // Square root dependence (approximate)
        float absorption = 0.25f * MathF.Sqrt(waterColumn / 25f);
        return Math.Clamp(absorption, 0.1f, 0.6f);
    }

    private float CalculateMethaneAbsorption(float ch4Concentration)
    {
        // Methane absorption in 7.6 μm band
        // 28× more potent than CO2 per molecule

        float ch4Reference = 0.7f; // Pre-industrial CH4 (ppm equivalent)
        float ch4Current = Math.Max(ch4Concentration, 0.1f);

        // Square root dependence (band saturation)
        float absorption = 0.015f * MathF.Sqrt(ch4Current / ch4Reference);
        return Math.Clamp(absorption, 0f, 0.08f);
    }

    private float CalculateN2OAbsorption(float n2oConcentration)
    {
        // N2O absorption overlapping with CO2 bands
        // 265× more potent than CO2 per molecule

        float n2oReference = 0.27f; // Pre-industrial N2O (ppm equivalent)
        float n2oCurrent = Math.Max(n2oConcentration, 0.05f);

        // Square root dependence
        float absorption = 0.01f * MathF.Sqrt(n2oCurrent / n2oReference);
        return Math.Clamp(absorption, 0f, 0.05f);
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
