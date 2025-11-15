namespace SimPlanet;

/// <summary>
/// Divine powers that the player (god) can use to interfere with civilizations
/// </summary>
public class DivinePowers
{
    private readonly Random _random;

    public DivinePowers(Random random)
    {
        _random = random;
    }

    /// <summary>
    /// Overthrow a government and install a new one
    /// </summary>
    public bool OverthrownGovernment(Civilization civ, GovernmentType newType, int currentYear)
    {
        // Create revolution/coup
        civ.Government = new Government(newType, currentYear);

        // Generate new ruler if needed
        if (civ.Government.IsHereditary || !civ.Government.IsElected)
        {
            var ruler = GenerateRandomRuler(civ, currentYear);
            civ.Government.CurrentRuler = ruler;
            civ.AllRulers.Add(ruler);

            // Start new dynasty if monarchy
            if (newType == GovernmentType.Monarchy || newType == GovernmentType.Dynasty)
            {
                var dynasty = new Dynasty(
                    civ.Dynasties.Count + 1,
                    GenerateDynastyName(_random),
                    currentYear,
                    ruler.Id,
                    civ.Id
                );
                civ.Dynasties.Add(dynasty);
                ruler.DynastyId = dynasty.Id;
            }
        }

        // Revolution causes instability
        civ.Government.Stability = 0.3f;
        civ.Population = (int)(civ.Population * 0.9f); // 10% casualties

        return true;
    }

    /// <summary>
    /// Send spies to sabotage another civilization
    /// </summary>
    public SpyMissionResult SendSpies(Civilization source, Civilization target, SpyMission mission)
    {
        float successChance = 0.5f;

        // Advanced civs have better intelligence
        if (source.TechLevel > target.TechLevel)
        {
            successChance += 0.2f;
        }

        bool success = _random.NextDouble() < successChance;

        var result = new SpyMissionResult
        {
            Success = success,
            Mission = mission
        };

        if (success)
        {
            switch (mission)
            {
                case SpyMission.StealTechnology:
                    // Steal tech
                    if (target.TechLevel > source.TechLevel)
                    {
                        source.TechLevel += (target.TechLevel - source.TechLevel) / 2;
                        result.Message = $"Spies stole technology from {target.Name}!";
                    }
                    break;

                case SpyMission.Sabotage:
                    // Destroy resources
                    target.Food *= 0.5f;
                    target.Metal *= 0.5f;
                    result.Message = $"Spies sabotaged {target.Name}'s resources!";
                    break;

                case SpyMission.AssassinateRuler:
                    // Kill the ruler
                    if (target.Government?.CurrentRuler != null)
                    {
                        target.Government.CurrentRuler.IsAlive = false;
                        target.Government.Stability -= 0.4f;
                        result.Message = $"Assassinated {target.Government.CurrentRuler.Name}!";
                    }
                    break;

                case SpyMission.InciteRevolution:
                    // Reduce stability
                    if (target.Government != null)
                    {
                        target.Government.Stability -= 0.3f;
                        target.Government.Legitimacy -= 0.2f;
                        result.Message = $"Incited unrest in {target.Name}!";
                    }
                    break;

                case SpyMission.StealResources:
                    // Transfer resources
                    float stolenGold = target.Metal * 0.3f;
                    target.Metal -= stolenGold;
                    source.Metal += stolenGold;
                    result.Message = $"Stole resources from {target.Name}!";
                    break;
            }
        }
        else
        {
            result.Message = "Spy mission failed!";

            // Failed missions hurt relations
            if (_random.NextDouble() < 0.3f)
            {
                result.Message += " Spies were caught!";
                // Could trigger war
            }
        }

        return result;
    }

    /// <summary>
    /// Force a betrayal - make one civ break treaties with another
    /// </summary>
    public void ForceBetray(Civilization betrayer, Civilization victim, DiplomaticRelation relation)
    {
        // Break all treaties
        foreach (var treaty in relation.Treaties.Where(t => t.IsActive).ToList())
        {
            relation.BreakTreaty(treaty.Type);
        }

        // Declare war
        relation.DeclareWar();
        betrayer.AtWar = true;
        betrayer.WarTargetId = victim.Id;

        // Reduce betrayer's stability (treachery angers population)
        if (betrayer.Government != null)
        {
            betrayer.Government.Stability -= 0.2f;
        }
    }

    /// <summary>
    /// Force a civilization to change government type without election
    /// </summary>
    public void ForceGovernmentChange(Civilization civ, GovernmentType newType, int currentYear)
    {
        OverthrownGovernment(civ, newType, currentYear);
    }

    /// <summary>
    /// Manipulate relations between two civs
    /// </summary>
    public void ManipulateRelations(DiplomaticRelation relation, float opinionChange)
    {
        relation.Opinion += opinionChange;
        relation.Opinion = Math.Clamp(relation.Opinion, -100, 100);

        // Update status based on opinion
        if (relation.Opinion > 70)
            relation.Status = DiplomaticStatus.Allied;
        else if (relation.Opinion > 30)
            relation.Status = DiplomaticStatus.Friendly;
        else if (relation.Opinion > -30)
            relation.Status = DiplomaticStatus.Neutral;
        else if (relation.Opinion > -70)
            relation.Status = DiplomaticStatus.Hostile;
        else
            relation.Status = DiplomaticStatus.War;
    }

    /// <summary>
    /// Bless a civilization (divine favor)
    /// </summary>
    public void BlessCivilization(Civilization civ)
    {
        // Boost population
        civ.Population = (int)(civ.Population * 1.2f);

        // Boost resources
        civ.Food *= 1.5f;

        // Boost stability
        if (civ.Government != null)
        {
            civ.Government.Stability = Math.Min(civ.Government.Stability + 0.3f, 1.0f);
            civ.Government.Legitimacy = 1.0f;
        }
    }

    /// <summary>
    /// Curse a civilization (divine wrath)
    /// </summary>
    public void CurseCivilization(Civilization civ)
    {
        // Reduce population
        civ.Population = (int)(civ.Population * 0.7f);

        // Destroy resources
        civ.Food *= 0.5f;
        civ.Metal *= 0.5f;

        // Reduce stability
        if (civ.Government != null)
        {
            civ.Government.Stability = Math.Max(civ.Government.Stability - 0.5f, 0.0f);
        }
    }

    /// <summary>
    /// Advance a civilization (increase tech level and progress)
    /// </summary>
    public void AdvanceCivilization(Civilization civ)
    {
        // Boost technology
        civ.TechLevel += 10;

        // Boost population
        civ.Population = (int)(civ.Population * 1.1f);

        // Boost resources
        civ.Food *= 1.3f;
        civ.Metal *= 1.3f;

        // Boost stability (progress brings optimism)
        if (civ.Government != null)
        {
            civ.Government.Stability = Math.Min(civ.Government.Stability + 0.2f, 1.0f);
        }
    }

    /// <summary>
    /// Generate a random ruler
    /// </summary>
    public Ruler GenerateRandomRuler(Civilization civ, int currentYear)
    {
        int rulerId = civ.AllRulers.Count + 1;
        string name = GenerateRulerName(_random);
        int age = 20 + _random.Next(40);

        var ruler = new Ruler(rulerId, name, age, currentYear)
        {
            Title = civ.Government?.RulerTitle ?? "Leader",
            Wisdom = (float)_random.NextDouble(),
            Charisma = (float)_random.NextDouble(),
            Ambition = (float)_random.NextDouble(),
            Brutality = (float)_random.NextDouble(),
            Piety = (float)_random.NextDouble()
        };

        return ruler;
    }

    private static readonly string[] RulerFirstNames = new[]
    {
        "Alexander", "Caesar", "Cleopatra", "Darius", "Elizabeth", "Frederick",
        "Genghis", "Hammurabi", "Isabella", "Julius", "Khufu", "Leonidas",
        "Mansa", "Napoleon", "Octavian", "Pericles", "Qin", "Ramses",
        "Saladin", "Theodora", "Ulysses", "Victoria", "William", "Xerxes",
        "Yongle", "Zenobia", "Akbar", "Boudicca", "Charlemagne", "Dido"
    };

    private static readonly string[] RulerSuffixes = new[]
    {
        "the Great", "the Wise", "the Brave", "the Just", "the Terrible",
        "the Bold", "the Fair", "the Strong", "the Pious", "the Ambitious",
        "the Conqueror", "the Builder", "the Reformer", "I", "II", "III"
    };

    public static string GenerateRulerName(Random random)
    {
        string firstName = RulerFirstNames[random.Next(RulerFirstNames.Length)];
        if (random.NextDouble() < 0.4)
        {
            string suffix = RulerSuffixes[random.Next(RulerSuffixes.Length)];
            return $"{firstName} {suffix}";
        }
        return firstName;
    }

    private static readonly string[] DynastyPrefixes = new[]
    {
        "House of", "Dynasty of", "Line of", "Clan", "Family"
    };

    private static readonly string[] DynastyNames = new[]
    {
        "Draken", "Phoenix", "Lion", "Eagle", "Dragon", "Wolf", "Bear",
        "Falcon", "Tiger", "Serpent", "Griffin", "Raven", "Hawk", "Stag",
        "Oak", "Iron", "Gold", "Silver", "Jade", "Ruby", "Emerald"
    };

    public static string GenerateDynastyName(Random random)
    {
        string prefix = DynastyPrefixes[random.Next(DynastyPrefixes.Length)];
        string name = DynastyNames[random.Next(DynastyNames.Length)];
        return $"{prefix} {name}";
    }
}

public enum SpyMission
{
    StealTechnology,
    Sabotage,
    AssassinateRuler,
    InciteRevolution,
    StealResources
}

public class SpyMissionResult
{
    public bool Success { get; set; }
    public SpyMission Mission { get; set; }
    public string Message { get; set; } = "";
}
