namespace SimPlanet;

/// <summary>
/// Represents a tectonic plate with movement and boundaries
/// </summary>
public class TectonicPlate
{
    public int Id { get; set; }
    public float VelocityX { get; set; }  // Plate movement in X direction
    public float VelocityY { get; set; }  // Plate movement in Y direction
    public float Density { get; set; }     // Oceanic plates are denser
    public bool IsOceanic { get; set; }
    public HashSet<(int x, int y)> Cells { get; set; }

    public TectonicPlate(int id)
    {
        Id = id;
        Cells = new HashSet<(int x, int y)>();
    }
}

public enum PlateBoundaryType
{
    None,
    Divergent,    // Plates moving apart (mid-ocean ridges)
    Convergent,   // Plates colliding (mountains, trenches)
    Transform     // Plates sliding past (earthquakes)
}

public enum FaultType
{
    None,
    Strike_Slip,   // Horizontal movement (Transform boundaries - San Andreas)
    Normal,        // Extensional, plates pulling apart (Divergent - East African Rift)
    Reverse,       // Compressional, plates pushing together (Convergent - Himalayas)
    Thrust,        // Low-angle reverse fault (major mountain building)
    Oblique        // Combined strike-slip and dip-slip
}

public enum SedimentType
{
    Sand,
    Silt,
    Clay,
    Gravel,
    Organic,
    Volcanic,
    Limestone
}

public enum EruptionType
{
    Effusive,        // Lava flows (Hawaiian style)
    Strombolian,     // Mild explosive with lava fountains
    Vulcanian,       // Moderate explosive with ash
    Plinian,         // Massive explosive eruption (Vesuvius, Krakatoa)
    Phreatomagmatic  // Explosive interaction with water
}

public enum CrustType
{
    Oceanic,      // Basaltic, dense, thin (~7 km)
    Continental,  // Granitic, light, thick (~35 km)
    Transitional  // Mixed/intermediate
}

public enum RockType
{
    Basalt,        // Oceanic crust, volcanic
    Granite,       // Continental crust, igneous
    Gabbro,        // Deep oceanic crust
    Limestone,     // Carbonate platform, sedimentary
    Sandstone,     // Sedimentary
    Shale,         // Fine sedimentary
    Metamorphic    // Transformed rock
}

public enum RoadType
{
    None,          // No road
    DirtPath,      // Basic path
    Road,          // Paved road
    Highway        // Major highway
}

/// <summary>
/// Extended terrain cell with geological properties (now uses embedded data for performance)
/// </summary>
public static class TerrainCellExtensions
{
    // Extension methods now simply access embedded property (maintains backward compatibility)
    public static GeologicalData GetGeology(this TerrainCell cell)
    {
        return cell.Geology;
    }

    // No longer needed as data is embedded in TerrainCell, but kept for API compatibility
    public static void ClearGeologicalData()
    {
        // No-op: data is now managed per-cell, cleared when cells are recreated
    }
}

public class GeologicalData
{
    // Tectonics
    public int PlateId { get; set; }
    public PlateBoundaryType BoundaryType { get; set; }
    public float TectonicStress { get; set; }  // Builds up at boundaries
    public float SubductionRate { get; set; }  // Rate of subduction at convergent boundaries

    // Crust properties
    public CrustType CrustType { get; set; } = CrustType.Continental;
    public float CrustThickness { get; set; } = 35.0f;  // km (35 continental, 7 oceanic)
    public RockType PrimaryRock { get; set; } = RockType.Granite;

    // Volcanism
    public bool IsVolcano { get; set; }
    public float VolcanicActivity { get; set; }
    public float MagmaPressure { get; set; }
    public int LastEruptionYear { get; set; }
    public EruptionType LastEruptionType { get; set; } = EruptionType.Effusive;
    public int EruptionIntensity { get; set; } = 1; // VEI 0-8 scale
    public bool IsHotSpot { get; set; }  // Hot spot volcano (away from plate boundaries)

    // Seismic activity (earthquakes and faults)
    public bool IsFault { get; set; }  // Is this cell on a fault line?
    public FaultType FaultType { get; set; } = FaultType.None;
    public float FaultActivity { get; set; }  // 0-1, how active the fault is
    public float EarthquakeMagnitude { get; set; }  // Current earthquake magnitude (0 if none)
    public int LastEarthquakeYear { get; set; }
    public float SeismicStress { get; set; }  // Accumulated stress (releases as earthquakes)
    public bool IsEpicenter { get; set; }  // Is this the earthquake epicenter?
    public float EarthquakeIntensity { get; set; }  // Intensity at this location (0-1)
    public bool InducedSeismicity { get; set; }  // Human-induced earthquake (fracking, etc.)

    // Tsunami
    public float TsunamiWaveHeight { get; set; }  // Current tsunami wave height (0 if none)
    public float TsunamiVelocity { get; set; }  // Wave propagation velocity
    public (float x, float y) TsunamiDirection { get; set; }  // Wave direction
    public int TsunamiSourceYear { get; set; }  // Year tsunami was generated

    // Erosion and Sedimentation
    public float SedimentLayer { get; set; }
    public float ErosionRate { get; set; }
    public float RockHardness { get; set; } = 0.5f;
    public List<SedimentType> SedimentColumn { get; set; } = new();  // Sediment column from bottom to top

    // Marine sedimentation
    public bool IsCarbonatePlatform { get; set; } = false;  // Shallow sea with carbonate deposition
    public float CarbonateLayer { get; set; } = 0.0f;       // Thickness of carbonate sediments

    // Hydrology
    public float WaterFlow { get; set; }
    public int RiverId { get; set; }
    public bool IsRiverSource { get; set; }
    public float SoilMoisture { get; set; }
    public (int x, int y) FlowDirection { get; set; }
    public float FloodLevel { get; set; }  // Current flood water level
    public float TideLevel { get; set; }  // Tidal variation

    // Infrastructure (built by civilizations)
    public bool HasRoad { get; set; } = false;
    public RoadType RoadType { get; set; } = RoadType.None;
    public int RoadBuiltYear { get; set; } = 0;

    // Age and composition
    public int CrustAge { get; set; }  // Millions of years
    public float CrystallineRock { get; set; } = 0.5f;  // Granite, gabbro
    public float SedimentaryRock { get; set; } = 0.3f;  // Sandstone, limestone, shale
    public float VolcanicRock { get; set; } = 0.2f;     // Basalt

    // Detailed rock composition
    public float Basalt { get; set; } = 0.2f;      // Oceanic crust
    public float Granite { get; set; } = 0.5f;     // Continental crust
    public float Limestone { get; set; } = 0.1f;   // Carbonate
    public float Sandstone { get; set; } = 0.1f;   // Clastic sediment
    public float Shale { get; set; } = 0.1f;       // Fine sediment
}
