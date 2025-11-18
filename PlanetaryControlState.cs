namespace SimPlanet;

/// <summary>
/// Stores the values coming from the SimEarth-style planetary controls UI so that
/// the underlying simulators can read the user overrides every update tick.
/// </summary>
public class PlanetaryControlState
{
    /// <summary>
    /// Current solar energy multiplier relative to Earth baseline.
    /// </summary>
    public float SolarEnergyMultiplier { get; set; } = 1f;

    /// <summary>
    /// Global temperature offset applied in Celsius across the whole map.
    /// </summary>
    public float TemperatureOffsetCelsius { get; set; } = 0f;

    /// <summary>
    /// Multiplier applied to rainfall calculations. 1 = default rainfall.
    /// </summary>
    public float RainfallMultiplier { get; set; } = 1f;

    /// <summary>
    /// Multiplier applied to wind calculations. 1 = default wind strength.
    /// </summary>
    public float WindStrengthMultiplier { get; set; } = 1f;

    /// <summary>
    /// Multiplier applied to atmospheric pressure. 1 = 1 atm.
    /// </summary>
    public float AtmosphericPressureMultiplier { get; set; } = 1f;

    /// <summary>
    /// Tracked average oxygen percentage for the UI slider.
    /// </summary>
    public float GlobalOxygenPercent { get; set; } = 21f;

    /// <summary>
    /// Tracked average CO2 percentage for the UI slider.
    /// </summary>
    public float GlobalCO2Percent { get; set; } = 2.5f;

    /// <summary>
    /// Target surface albedo set by the planetary controls UI.
    /// </summary>
    public float SurfaceAlbedo { get; set; } = 0.3f;

    /// <summary>
    /// Desired global ice coverage fraction (0-1).
    /// </summary>
    public float TargetIceCoverage { get; set; } = 0.1f;

    /// <summary>
    /// Running ocean level offset slider value.
    /// </summary>
    public float OceanLevelOffset { get; set; } = 0f;

    /// <summary>
    /// Planetary tectonic activity multiplier (drives plate motion speeds).
    /// </summary>
    public float TectonicActivityMultiplier { get; set; } = 1f;

    /// <summary>
    /// Planetary volcanic activity multiplier (affects magma pressure & eruption odds).
    /// </summary>
    public float VolcanicActivityMultiplier { get; set; } = 1f;

    /// <summary>
    /// Global erosion multiplier for sediment transport.
    /// </summary>
    public float ErosionRateMultiplier { get; set; } = 1f;

    /// <summary>
    /// Latest magnetic field slider value.
    /// </summary>
    public float MagneticFieldStrength { get; set; } = 1f;

    /// <summary>
    /// Whether the user is manually overriding the magnetic field simulation.
    /// </summary>
    public bool ManualMagneticField { get; set; } = false;

    /// <summary>
    /// Latest planetary core temperature slider value.
    /// </summary>
    public float CoreTemperatureKelvin { get; set; } = 5000f;

    /// <summary>
    /// Whether the user is manually overriding the core temperature simulation.
    /// </summary>
    public bool ManualCoreTemperature { get; set; } = false;
}
