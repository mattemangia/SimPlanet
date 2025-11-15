namespace SimPlanet;

/// <summary>
/// Types of diplomatic relations
/// </summary>
public enum DiplomaticStatus
{
    War,           // Open warfare
    Hostile,       // Tense relations, border skirmishes
    Neutral,       // No formal relations
    Friendly,      // Good relations, some trade
    Allied         // Military alliance
}

/// <summary>
/// Types of diplomatic agreements
/// </summary>
public enum TreatyType
{
    TradePact,          // Trade agreement, boosts economy
    DefensivePact,      // Mutual defense
    MilitaryAlliance,   // Offensive alliance
    NonAggressionPact,  // Promise not to attack
    RoyalMarriage,      // Marriage alliance
    Vassalage,          // One civ is vassal to another
    TributePact,        // Weaker pays tribute to stronger
    CulturalExchange,   // Tech/culture sharing
    ClimateAgreement    // Emission reduction
}

/// <summary>
/// Diplomatic relation between two civilizations
/// </summary>
public class DiplomaticRelation
{
    public int CivilizationId1 { get; set; }
    public int CivilizationId2 { get; set; }

    public DiplomaticStatus Status { get; set; } = DiplomaticStatus.Neutral;
    public float Opinion { get; set; } = 0.0f; // -100 to +100

    // Active treaties
    public List<Treaty> Treaties { get; set; } = new();

    // History
    public int YearEstablished { get; set; }
    public int TotalWars { get; set; } = 0;
    public int YearsAtPeace { get; set; } = 0;
    public int YearsAtWar { get; set; } = 0;

    // Modifiers
    public float TrustLevel { get; set; } = 0.5f; // 0-1
    public List<string> OpinionModifiers { get; set; } = new();

    public DiplomaticRelation(int civ1, int civ2, int year)
    {
        CivilizationId1 = civ1;
        CivilizationId2 = civ2;
        YearEstablished = year;
    }

    /// <summary>
    /// Check if specific treaty is active
    /// </summary>
    public bool HasTreaty(TreatyType type)
    {
        return Treaties.Any(t => t.Type == type && t.IsActive);
    }

    /// <summary>
    /// Add a new treaty
    /// </summary>
    public void AddTreaty(Treaty treaty)
    {
        Treaties.Add(treaty);

        // Treaties improve relations
        Opinion += 10;
        TrustLevel += 0.1f;
        TrustLevel = Math.Min(TrustLevel, 1.0f);
    }

    /// <summary>
    /// Break a treaty (major diplomatic incident)
    /// </summary>
    public void BreakTreaty(TreatyType type)
    {
        var treaty = Treaties.FirstOrDefault(t => t.Type == type && t.IsActive);
        if (treaty != null)
        {
            treaty.IsActive = false;
            treaty.Broken = true;

            // Major trust loss
            Opinion -= 50;
            TrustLevel -= 0.5f;
            TrustLevel = Math.Max(TrustLevel, 0.0f);

            OpinionModifiers.Add($"Broke {type} treaty");
        }
    }

    /// <summary>
    /// Declare war
    /// </summary>
    public void DeclareWar()
    {
        Status = DiplomaticStatus.War;
        Opinion = -100;
        TrustLevel = 0.0f;
        TotalWars++;

        // Break all treaties except vassalage
        foreach (var treaty in Treaties.Where(t => t.Type != TreatyType.Vassalage))
        {
            treaty.IsActive = false;
        }
    }

    /// <summary>
    /// Make peace
    /// </summary>
    public void MakePeace()
    {
        if (Status == DiplomaticStatus.War)
        {
            Status = DiplomaticStatus.Hostile;
            Opinion = -50;
        }
    }
}

/// <summary>
/// A diplomatic treaty between civilizations
/// </summary>
public class Treaty
{
    public TreatyType Type { get; set; }
    public int SignedYear { get; set; }
    public int Duration { get; set; } = -1; // -1 = permanent
    public bool IsActive { get; set; } = true;
    public bool Broken { get; set; } = false;

    // Treaty-specific data
    public Dictionary<string, object> Terms { get; set; } = new();

    public Treaty(TreatyType type, int year, int duration = -1)
    {
        Type = type;
        SignedYear = year;
        Duration = duration;
    }

    /// <summary>
    /// Check if treaty has expired
    /// </summary>
    public bool HasExpired(int currentYear)
    {
        if (Duration < 0) return false; // Permanent
        return (currentYear - SignedYear) >= Duration;
    }
}

/// <summary>
/// A royal marriage between two civilizations
/// </summary>
public class RoyalMarriage
{
    public int RulerId1 { get; set; }
    public int RulerId2 { get; set; }
    public int CivilizationId1 { get; set; }
    public int CivilizationId2 { get; set; }
    public int MarriageYear { get; set; }

    // Children from this marriage
    public List<int> ChildrenIds { get; set; } = new();

    // Status
    public bool IsActive { get; set; } = true;

    public RoyalMarriage(int ruler1, int ruler2, int civ1, int civ2, int year)
    {
        RulerId1 = ruler1;
        RulerId2 = ruler2;
        CivilizationId1 = civ1;
        CivilizationId2 = civ2;
        MarriageYear = year;
    }
}
