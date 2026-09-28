namespace SimPlanet;

// Data model for national development: homeland and ethnicity, energy, power grids,
// internet, weapons of mass destruction, space programs, national projects,
// political parties and dynastic succession. Simulation logic lives in the
// CivilizationManager partial classes; renderers and UI read these objects.

/// <summary>
/// Broad climate of a people's homeland. Temperate homelands develop faster;
/// the others adapt more slowly (harsher farming, more disease, fewer surpluses).
/// </summary>
public enum HomelandClimate
{
    Temperate,
    Tropical,
    Arid,
    Cold,
    Highland
}

public enum EnergySource
{
    Biomass,   // Wood, dung, charcoal
    Coal,
    Oil,
    Hydro,
    Nuclear,
    Wind,
    Solar,
    Fusion
}

public enum SpaceStage
{
    None,
    Rocketry,          // First rockets
    Satellites,        // Orbital satellites (communications, weather, spy)
    CrewedFlight,      // Astronauts in orbit
    SpaceStation,      // Permanently crewed station
    LunarBase,         // Base on the moon
    OffWorldColony     // Self-sufficient colony: the species can survive a dead planet
}

public enum OrbitalObjectType
{
    Satellite,
    SpaceStation,
    LunarBase,
    Colony
}

/// <summary>
/// Something a civilization has put into space. Crewed objects keep people alive
/// even if every settlement on the surface is destroyed.
/// </summary>
public class OrbitalObject
{
    public int Id { get; set; }
    public int CivilizationId { get; set; }
    public string Name { get; set; } = "";
    public OrbitalObjectType Type { get; set; }
    public int Crew { get; set; }                    // Astronauts aboard
    public float OrbitAngle { get; set; }            // 0-2π, for rendering
    public float OrbitRadius { get; set; } = 1f;     // Relative orbit radius, for rendering
    public int LaunchedYear { get; set; }
    public bool Orphaned { get; set; }               // Its nation no longer exists on the surface
}

public enum ProjectKind
{
    Research,
    Economic,
    Space,
    Military,
    Environmental
}

/// <summary>
/// A national programme that takes years of investment and gives a lasting benefit.
/// </summary>
public class NationalProject
{
    public string Name { get; set; } = "";
    public ProjectKind Kind { get; set; }
    public float Cost { get; set; }                  // Gold-equivalent investment needed
    public float Progress { get; set; }              // Invested so far
    public int StartedYear { get; set; }
    public int CompletedYear { get; set; } = -1;
    public bool Completed => CompletedYear >= 0;
    public float Fraction => Cost <= 0 ? 1f : Math.Clamp(Progress / Cost, 0f, 1f);
}

public enum Ideology
{
    Conservative,
    Liberal,
    Socialist,
    Green,
    Nationalist,
    Technocratic,
    Religious
}

public class PoliticalParty
{
    public string Name { get; set; } = "";
    public Ideology Ideology { get; set; }
    public float Support { get; set; }               // Share of the vote, 0-1
    public int SeatsWon { get; set; }                // Seats at the last election (out of 100)
    public int FoundedYear { get; set; }
}

public class ElectionResult
{
    public int Year { get; set; }
    public string WinningParty { get; set; } = "";
    public float WinningShare { get; set; }
    public string LeaderName { get; set; } = "";
}

/// <summary>
/// Weapons of mass destruction and strategic forces.
/// </summary>
public class Arsenal
{
    public int NuclearWarheads { get; set; }         // Mirrors Civilization.NuclearStockpile
    public int ChemicalStockpile { get; set; }
    public bool BioweaponProgram { get; set; }
    public int MissileSilos { get; set; }
    public int SpySatellites { get; set; }
    public bool MissileDefense { get; set; }
    public int NuclearStrikesLaunched { get; set; }
    public int ChemicalAttacks { get; set; }
    public int BioweaponReleases { get; set; }
}
