namespace SimPlanet;

// Data model for city character, transport networks, vehicles and migrations
// (of people and of animals). Simulation logic lives in the CivilizationManager
// partial classes and in WildlifeMigration; renderers read these objects.

/// <summary>
/// What a settlement lives on. Decided each year from its land, resources and buildings.
/// </summary>
public enum CitySpecialization
{
    Farming,      // Grain fields and pastures
    Fishing,      // Fishing village
    Port,         // Harbour town trading by sea
    Mining,       // Mines and quarries in the hills
    Timber,       // Forestry and carpentry
    Trade,        // Market town at a crossroads or river
    Industrial,   // Factories and workshops
    Academic,     // University town
    Holy,         // Temple city, pilgrimage centre
    Fortress,     // Walled border stronghold
    Capital       // Seat of government
}

/// <summary>
/// Architectural tradition of a settlement, from its people's culture and homeland.
/// </summary>
public enum CityStyle
{
    Timber,       // Wooden halls and steep roofs (cold, forested)
    Stone,        // Stone walls and tiled roofs (temperate)
    Adobe,        // Mud brick, flat roofs, domes (arid)
    Stilt,        // Thatch and stilt houses (tropical, wetlands)
    Pagoda,       // Tiered roofs (eastern cultures)
    Terraced,     // Hill terraces (highlands)
    Modern,       // Concrete and glass (industrial era onwards)
    Futuristic    // Arcologies (space age)
}

public enum TransportKind
{
    Road,
    Railway,
    SeaLane,
    AirRoute
}

/// <summary>
/// A link between two settlements. Paths are in map cells and follow the terrain
/// (roads and railways over land, sea lanes over water); air routes are direct.
/// </summary>
public class TransportRoute
{
    public int Id { get; set; }
    public TransportKind Kind { get; set; }
    public int CivilizationId { get; set; }          // Operator
    public int FromCityId { get; set; }
    public int ToCityId { get; set; }
    public List<(int x, int y)> Path { get; set; } = new();
    public float Traffic { get; set; }               // Goods and travellers per year (relative)
    public bool International { get; set; }          // Connects two nations
    public int BuiltYear { get; set; }
}

public enum VehicleKind
{
    Caravan,      // Pack animals and wagons on roads
    Truck,        // Motor vehicles on roads
    Train,
    SailingShip,
    Steamship,
    CargoShip,
    Airliner,
    Warship
}

/// <summary>
/// A moving vehicle for display: it travels along a route and turns back at the end.
/// </summary>
public class Vehicle
{
    public int Id { get; set; }
    public VehicleKind Kind { get; set; }
    public int CivilizationId { get; set; }
    public int RouteId { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public float Heading { get; set; }               // Radians, 0 = east
    internal float Progress { get; set; }            // Index along the route path (fractional)
    internal int Direction { get; set; } = 1;
}

public enum MigrationKind
{
    Urbanization,      // Countryside to city
    Economic,          // Poor nation to rich nation
    WarRefugees,       // Fleeing battles, sieges and occupation
    FamineRefugees,    // Fleeing hunger
    EpidemicRefugees,  // Fleeing plague
    ClimateRefugees,   // Fleeing drought, rising seas or advancing ice
    DisasterRefugees,  // Fleeing eruptions, quakes, floods, fallout
    Persecution,       // Fleeing oppression by a brutal regime
    Settlers           // Founding a new village
}

/// <summary>
/// A flow of people between two places this year (for arrows on the map and statistics).
/// </summary>
public class MigrationFlow
{
    public int FromX { get; set; }
    public int FromY { get; set; }
    public int ToX { get; set; }
    public int ToY { get; set; }
    public int People { get; set; }
    public MigrationKind Kind { get; set; }
    public int FromCivId { get; set; }
    public int ToCivId { get; set; }
    public int Year { get; set; }
}

/// <summary>
/// A migrating group of wild animals: herds on land, flocks in the air, schools in the sea.
/// </summary>
public class AnimalGroup
{
    public int Id { get; set; }
    public LifeForm Species { get; set; }
    public string Name { get; set; } = "";          // e.g. "Mammal herd", "Bird flock"
    public float X { get; set; }
    public float Y { get; set; }
    public float Heading { get; set; }
    public int Size { get; set; }                    // Number of animals
    public bool Flying { get; set; }
    public bool Marine { get; set; }
    public int HomeY { get; set; }                   // Latitude of the summer range
    public int WinterY { get; set; }                 // Latitude of the winter range
    internal float TargetX { get; set; }
    internal float TargetY { get; set; }
}

public enum IntelligenceMission
{
    GatherIntelligence,  // Reveal armies, arsenals and plans
    StealTechnology,     // Copy research
    Sabotage,            // Damage buildings, power plants, stockpiles
    IncitingUnrest,      // Fund dissidents, lower stability
    Assassination,       // Kill the ruler or heir
    CounterIntelligence  // Hunt foreign spies at home
}

/// <summary>
/// One nation's spy network inside another nation.
/// </summary>
public class SpyNetwork
{
    public int OwnerCivId { get; set; }
    public int TargetCivId { get; set; }
    public int Agents { get; set; }
    public float Strength { get; set; }              // 0-1: reach of the network
    public float Exposure { get; set; }              // 0-1: risk of being uncovered
    public IntelligenceMission Mission { get; set; } = IntelligenceMission.GatherIntelligence;
    public int EstablishedYear { get; set; }
    public int Successes { get; set; }
    public int AgentsLost { get; set; }
    public bool Compromised { get; set; }            // Uncovered: the target knows
}
