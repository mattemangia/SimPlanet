namespace SimPlanet;

/// <summary>
/// Represents a single cell in the planet simulation
/// </summary>
public class TerrainCell
{
    // Terrain properties
    public float Elevation { get; set; }        // -1.0 (deep ocean) to 1.0 (high mountain)
    public float Temperature { get; set; }      // In Celsius
    public float Rainfall { get; set; }         // 0.0 to 1.0
    public float Humidity { get; set; }         // 0.0 to 1.0

    // Atmospheric
    public float Oxygen { get; set; }           // Percentage
    public float CO2 { get; set; }              // Percentage
    public float Greenhouse { get; set; }       // Greenhouse effect strength

    // Life
    public LifeForm LifeType { get; set; }
    public float Biomass { get; set; }          // Amount of life
    public float Evolution { get; set; }        // Evolution level (0-1)

    // Ice state (controlled by seasonal dynamics)
    public bool IsIce { get; set; }

    // Derived properties
    public bool IsWater => Elevation < 0;
    public bool IsLand => Elevation >= 0;
    public bool IsDesert => Rainfall < 0.2f && IsLand;
    public bool IsForest => Rainfall > 0.5f && Temperature > 5 && IsLand && Biomass > 0.3f;

    public TerrainType GetTerrainType()
    {
        if (IsIce) return TerrainType.Ice;
        if (IsWater)
        {
            if (Elevation < -0.5f) return TerrainType.DeepOcean;
            return TerrainType.ShallowWater;
        }

        if (Elevation > 0.7f) return TerrainType.Mountain;
        if (IsForest) return TerrainType.Forest;
        if (IsDesert) return TerrainType.Desert;
        if (Rainfall > 0.4f) return TerrainType.Grassland;
        return TerrainType.Plains;
    }
}

public enum TerrainType
{
    DeepOcean,
    ShallowWater,
    Beach,
    Plains,
    Grassland,
    Forest,
    Desert,
    Mountain,
    Ice,
    Tundra
}

public enum LifeForm
{
    None,
    Bacteria,
    Algae,
    PlantLife,

    // Early animals
    SimpleAnimals,      // Invertebrates, worms, early marine life

    // Vertebrate evolution
    Fish,              // First vertebrates (~500 Mya)
    Amphibians,        // Land colonization (~370 Mya)
    Reptiles,          // First true land animals (~320 Mya)

    // Age of Reptiles (Mesozoic Era: 252-66 Mya)
    Dinosaurs,         // Dominant land animals (230-66 Mya)
    MarineDinosaurs,   // Plesiosaurs, Mosasaurs
    Pterosaurs,        // Flying reptiles

    // Mammals and Birds (Cenozoic Era: 66 Mya - present)
    Mammals,           // Dominant after K-T extinction
    Birds,             // Evolved from dinosaurs

    // Advanced life
    ComplexAnimals,    // Advanced ecosystems
    Intelligence,      // Tool users
    Civilization       // Advanced civilization
}
