namespace SimPlanet;

/// <summary>
/// Types of government systems
/// </summary>
public enum GovernmentType
{
    Tribal,          // Chiefdom based on strength
    Monarchy,        // Hereditary rule
    Dynasty,         // Pharaoh/Emperor with divine right
    Theocracy,       // Religious rule
    Republic,        // Elected officials
    Democracy,       // Direct popular vote
    Oligarchy,       // Rule by wealthy elite
    Dictatorship,    // Military strongman
    Federation       // Confederation of states
}

/// <summary>
/// Government system for a civilization
/// </summary>
public class Government
{
    public GovernmentType Type { get; set; }
    public string RulerTitle { get; set; } = "";
    public int EstablishedYear { get; set; }

    // Current ruler (null for democracies/republics)
    public Ruler? CurrentRuler { get; set; }

    // Succession rules
    public bool IsHereditary { get; set; } // Monarchy, Dynasty
    public bool IsElected { get; set; } // Republic, Democracy
    public bool IsReligious { get; set; } // Theocracy

    // Stability
    public float Stability { get; set; } = 1.0f; // 0-1, affects revolution chance
    public float Corruption { get; set; } = 0.0f; // 0-1
    public float Legitimacy { get; set; } = 1.0f; // 0-1

    // Government modifiers
    public float TaxRate { get; set; } = 0.3f;
    public float MilitaryFocus { get; set; } = 0.5f; // vs civilian focus
    public float ExpansionPolicy { get; set; } = 0.5f; // Isolationist vs expansionist

    public Government(GovernmentType type, int year)
    {
        Type = type;
        EstablishedYear = year;
        SetupGovernmentType(type);
    }

    private void SetupGovernmentType(GovernmentType type)
    {
        switch (type)
        {
            case GovernmentType.Tribal:
                RulerTitle = "Chief";
                IsHereditary = false;
                IsElected = false;
                IsReligious = false;
                Stability = 0.6f;
                break;

            case GovernmentType.Monarchy:
                RulerTitle = "King";
                IsHereditary = true;
                IsElected = false;
                IsReligious = false;
                Stability = 0.8f;
                break;

            case GovernmentType.Dynasty:
                RulerTitle = "Pharaoh";
                IsHereditary = true;
                IsElected = false;
                IsReligious = true;
                Stability = 0.9f;
                Legitimacy = 1.0f;
                break;

            case GovernmentType.Theocracy:
                RulerTitle = "High Priest";
                IsHereditary = false;
                IsElected = false;
                IsReligious = true;
                Stability = 0.7f;
                break;

            case GovernmentType.Republic:
                RulerTitle = "Consul";
                IsHereditary = false;
                IsElected = true;
                IsReligious = false;
                Stability = 0.75f;
                break;

            case GovernmentType.Democracy:
                RulerTitle = "President";
                IsHereditary = false;
                IsElected = true;
                IsReligious = false;
                Stability = 0.85f;
                Corruption = 0.2f;
                break;

            case GovernmentType.Oligarchy:
                RulerTitle = "First Citizen";
                IsHereditary = false;
                IsElected = false;
                IsReligious = false;
                Stability = 0.7f;
                Corruption = 0.5f;
                break;

            case GovernmentType.Dictatorship:
                RulerTitle = "Supreme Leader";
                IsHereditary = false;
                IsElected = false;
                IsReligious = false;
                Stability = 0.5f;
                MilitaryFocus = 0.8f;
                break;

            case GovernmentType.Federation:
                RulerTitle = "Chancellor";
                IsHereditary = false;
                IsElected = true;
                IsReligious = false;
                Stability = 0.8f;
                break;
        }
    }

    /// <summary>
    /// Check if government should collapse or change
    /// </summary>
    public bool ShouldCollapse(Random random)
    {
        // Low stability increases revolution chance
        float collapseChance = (1.0f - Stability) * 0.1f;

        // High corruption increases instability
        collapseChance += Corruption * 0.05f;

        // Low legitimacy increases revolution
        collapseChance += (1.0f - Legitimacy) * 0.08f;

        return random.NextDouble() < collapseChance;
    }
}

/// <summary>
/// Represents a ruler of a civilization
/// </summary>
public class Ruler
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Title { get; set; } = "";
    public int Age { get; set; }
    public int YearTookPower { get; set; }
    public int DynastyId { get; set; } // For hereditary rulers

    // Traits
    public float Wisdom { get; set; } // Affects tech advancement
    public float Charisma { get; set; } // Affects stability
    public float Ambition { get; set; } // Affects expansion
    public float Brutality { get; set; } // Affects military/war
    public float Piety { get; set; } // For religious governments

    // Family
    public int? SpouseId { get; set; }
    public List<int> ChildrenIds { get; set; } = new();
    public int? ParentId { get; set; }

    // Status
    public bool IsAlive { get; set; } = true;
    public int? DeathYear { get; set; }

    public Ruler(int id, string name, int age, int yearTookPower)
    {
        Id = id;
        Name = name;
        Age = age;
        YearTookPower = yearTookPower;
    }

    /// <summary>
    /// Age the ruler and check for death
    /// </summary>
    public bool AgeAndCheckDeath(int currentYear, Random random)
    {
        Age++;

        // Death chance increases with age
        float deathChance = Age > 60 ? (Age - 60) * 0.05f : 0.01f;

        if (random.NextDouble() < deathChance)
        {
            IsAlive = false;
            DeathYear = currentYear;
            return true;
        }

        return false;
    }
}

/// <summary>
/// A royal dynasty or family lineage
/// </summary>
public class Dynasty
{
    public int Id { get; set; }
    public string Name { get; set; } = ""; // e.g., "House of Draken"
    public int FoundedYear { get; set; }
    public int FounderId { get; set; } // First ruler
    public List<int> MemberIds { get; set; } = new(); // All family members
    public int CivilizationId { get; set; }

    // Dynasty stats
    public int GenerationCount { get; set; } = 1;
    public bool IsExtinct { get; set; } = false;

    public Dynasty(int id, string name, int foundedYear, int founderId, int civId)
    {
        Id = id;
        Name = name;
        FoundedYear = foundedYear;
        FounderId = founderId;
        CivilizationId = civId;
        MemberIds.Add(founderId);
    }
}
