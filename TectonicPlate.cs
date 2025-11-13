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

/// <summary>
/// Extended terrain cell with geological properties
/// </summary>
public static class TerrainCellExtensions
{
    // Additional properties stored in dictionaries for existing cells
    private static Dictionary<TerrainCell, GeologicalData> _geologicalData = new();

    public static GeologicalData GetGeology(this TerrainCell cell)
    {
        if (!_geologicalData.ContainsKey(cell))
        {
            _geologicalData[cell] = new GeologicalData();
        }
        return _geologicalData[cell];
    }

    public static void ClearGeologicalData()
    {
        _geologicalData.Clear();
    }
}

public class GeologicalData
{
    // Tectonics
    public int PlateId { get; set; }
    public PlateBoundaryType BoundaryType { get; set; }
    public float TectonicStress { get; set; }  // Builds up at boundaries
    public float SubductionRate { get; set; }  // Rate of subduction at convergent boundaries

    // Volcanism
    public bool IsVolcano { get; set; }
    public float VolcanicActivity { get; set; }
    public float MagmaPressure { get; set; }
    public int LastEruptionYear { get; set; }
    public EruptionType LastEruptionType { get; set; } = EruptionType.Effusive;
    public int EruptionIntensity { get; set; } = 1; // VEI 0-8 scale

    // Erosion and Sedimentation
    public float SedimentLayer { get; set; }
    public float ErosionRate { get; set; }
    public float RockHardness { get; set; } = 0.5f;
    public List<SedimentType> SedimentColumn { get; set; } = new();  // Sediment column from bottom to top

    // Hydrology
    public float WaterFlow { get; set; }
    public int RiverId { get; set; }
    public bool IsRiverSource { get; set; }
    public float SoilMoisture { get; set; }
    public (int x, int y) FlowDirection { get; set; }
    public float FloodLevel { get; set; }  // Current flood water level
    public float TideLevel { get; set; }  // Tidal variation

    // Age and composition
    public int CrustAge { get; set; }  // Millions of years
    public float CrystallineRock { get; set; } = 0.5f;
    public float SedimentaryRock { get; set; } = 0.3f;
    public float VolcanicRock { get; set; } = 0.2f;
}
