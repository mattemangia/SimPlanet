namespace SimPlanet;

/// <summary>
/// What a nation is trying to achieve this year.
/// </summary>
public enum NationalPosture
{
    Develop,     // Grow the economy and cities
    Expand,      // Settle free land
    Research,    // Invest in knowledge
    Fortify,     // Build walls and defensive armies against a threat
    Militarize,  // Arm for a future war
    Conquer,     // Wage war on a chosen rival
    SpaceRace,   // Reach orbit and beyond
    Recover      // Survive famine, plague, disaster or defeat
}

/// <summary>
/// A nation's strategic assessment: threats, opportunities, chosen posture and budget.
/// </summary>
public class StrategyState
{
    public NationalPosture Posture { get; set; } = NationalPosture.Develop;
    public float ThreatLevel { get; set; }           // 0 = safe, 1+ = in danger
    public int? MainThreatId { get; set; }
    public int? ConquestTargetId { get; set; }
    public float ExistentialThreat { get; set; }     // 0-1: the nation may be destroyed
    public float MilitaryShare { get; set; } = 0.2f; // Budget shares (sum to 1)
    public float ResearchShare { get; set; } = 0.25f;
    public float EconomyShare { get; set; } = 0.45f;
    public float SpaceShare { get; set; }
    public string Rationale { get; set; } = "";
    public int CitiesAtWarStart { get; set; }
    public bool ChemicalWeaponsAuthorized { get; set; }  // Doctrine allows chemical attacks this year
    public int PostureSinceYear { get; set; }
}

/// <summary>
/// Strategic AI. Each year every nation weighs threats, opportunities, needs and the
/// personality of its rulers and parties, and picks a posture. The posture drives war
/// decisions, army sizes, building and project choices and the weapons doctrine, so
/// nations behave deliberately instead of rolling dice.
/// </summary>
public partial class CivilizationManager
{
    private void UpdateStrategies(int currentYear)
    {
        foreach (var civ in _civilizations)
        {
            UpdateStrategy(civ, currentYear);
        }
    }

    private void UpdateStrategy(Civilization civ, int currentYear)
    {
        var s = civ.Strategy;
        var ruler = civ.Government?.CurrentRuler;
        float aggression = civ.Aggression;
        float ambition = ruler?.Ambition ?? 0.5f;
        float brutality = ruler?.Brutality ?? 0.5f;
        float wisdom = ruler?.Wisdom ?? 0.5f;
        var ideology = GetRulingIdeology(civ);

        // ---- Threat assessment ----
        float threat = 0f;
        int? mainThreat = null;
        float bestOpportunity = 0f;
        int? target = null;
        float myStrength = Math.Max(1f, civ.MilitaryStrength);

        foreach (var (a, b, border) in GetContactPairs())
        {
            if (a.Id != civ.Id && b.Id != civ.Id) continue;
            var other = a.Id == civ.Id ? b : a;
            if (!civ.DiplomaticRelations.TryGetValue(other.Id, out var relation)) continue;

            float ratio = Math.Min(3f, other.MilitaryStrength / myStrength);
            float hostility = relation.Status switch
            {
                DiplomaticStatus.War => 1.5f,
                DiplomaticStatus.Hostile => 0.8f,
                DiplomaticStatus.Neutral => 0.35f,
                DiplomaticStatus.Friendly => 0.1f,
                _ => 0.02f
            };
            float otherAggression = other.Aggression * 0.5f + (other.Government?.CurrentRuler?.Ambition ?? 0.5f) * 0.5f;
            float t = ratio * hostility * (0.5f + otherAggression) * (other.HasNuclearWeapons ? 1.3f : 1f)
                      * (1f - Math.Clamp(relation.YearsAtPeace / 200f, 0f, 0.5f)); // Long peace builds confidence
            if (t > threat)
            {
                threat = t;
                mainThreat = other.Id;
            }

            // Opportunity: a weaker, non-allied neighbour with land worth taking
            if (relation.Status >= DiplomaticStatus.Friendly) continue;
            if (relation.HasTreaty(TreatyType.DefensivePact) || relation.HasTreaty(TreatyType.RoyalMarriage)) continue;
            if (other.HasNuclearWeapons && !civ.HasNuclearWeapons) continue; // Never provoke a nuclear power without a deterrent

            float weakness = Math.Clamp(1.5f - ratio, 0f, 1.5f);
            float prize = Math.Min(1f, other.Cities.Count / 10f) + (civ.Food < civ.FoodConsumption * 0.3f ? 0.5f : 0f);
            float allies = CountAllies(other) * 0.25f;
            float opportunity = weakness * (0.5f + prize) - allies - (relation.Opinion > 0 ? relation.Opinion / 100f : 0f);
            if (other.HasNuclearWeapons) opportunity *= 0.2f; // Mutually assured destruction
            if (opportunity > bestOpportunity)
            {
                bestOpportunity = opportunity;
                target = other.Id;
            }
        }

        s.ThreatLevel = threat;
        s.MainThreatId = mainThreat;
        s.ConquestTargetId = target;
        s.ExistentialThreat = ComputeExistentialThreat(civ);

        // ---- Posture utilities ----
        bool starving = civ.Cities.Any(c => c.Starving);
        float peaceful = ideology switch
        {
            Ideology.Green => 0.35f,
            Ideology.Liberal => 0.2f,
            Ideology.Socialist => 0.15f,
            Ideology.Nationalist => -0.25f,
            _ => 0f
        } + (civ.Government?.Type is GovernmentType.Democracy or GovernmentType.Federation or GovernmentType.Republic ? 0.2f : 0f)
          - (civ.Government?.Type == GovernmentType.Dictatorship ? 0.2f : 0f);

        var utilities = new Dictionary<NationalPosture, float>
        {
            [NationalPosture.Develop] = 0.45f + (1f - civ.Prosperity) * 0.3f + (ideology is Ideology.Liberal or Ideology.Conservative or Ideology.Socialist ? 0.1f : 0f),
            [NationalPosture.Expand] = HasFreeLand(civ) ? 0.35f + ambition * 0.2f : 0f,
            [NationalPosture.Research] = 0.2f + wisdom * 0.3f + (ideology == Ideology.Technocratic ? 0.2f : 0f) + civ.DevelopmentModifier * 0.05f,
            [NationalPosture.Fortify] = Math.Min(1.2f, threat * 0.6f),
            [NationalPosture.Militarize] = Math.Min(1f, threat * 0.35f + bestOpportunity * 0.25f) * (aggression + ambition) * 0.8f - peaceful * 0.5f,
            [NationalPosture.Conquer] = target == null ? 0f
                : bestOpportunity * 1.8f * (aggression * 0.6f + ambition * 0.4f + brutality * 0.2f) * (1f - civ.WarWeariness) - peaceful - civ.WarWeariness * 0.5f,
            [NationalPosture.SpaceRace] = civ.TechLevel >= 50 ? 0.35f + wisdom * 0.2f + (ideology == Ideology.Technocratic ? 0.2f : 0f) + RivalSpaceLead(civ) * 0.2f : 0f,
            [NationalPosture.Recover] = (starving ? 0.6f : 0f) + (civ.Stability < 0.35f ? 0.4f : 0f) + civ.WarWeariness * 0.6f + (civ.Gold < 0 ? 0.3f : 0f)
        };

        // Already at war: the war is the priority
        if (civ.AtWar)
        {
            utilities[NationalPosture.Conquer] = Math.Max(utilities[NationalPosture.Conquer], 0.6f * (1f - civ.WarWeariness));
            utilities[NationalPosture.Fortify] = Math.Max(utilities[NationalPosture.Fortify], 0.5f + s.ExistentialThreat);
        }

        // Some inertia so plans are not abandoned every year
        if (utilities.ContainsKey(s.Posture)) utilities[s.Posture] += 0.08f;

        var best = utilities.OrderByDescending(u => u.Value).First().Key;
        if (best != s.Posture)
        {
            s.PostureSinceYear = currentYear;
        }
        s.Posture = best;

        // ---- Budget ----
        (s.MilitaryShare, s.ResearchShare, s.EconomyShare, s.SpaceShare) = best switch
        {
            NationalPosture.Conquer => (0.55f, 0.1f, 0.35f, 0f),
            NationalPosture.Militarize => (0.4f, 0.15f, 0.45f, 0f),
            NationalPosture.Fortify => (0.4f, 0.15f, 0.45f, 0f),
            NationalPosture.Research => (0.1f, 0.5f, 0.35f, 0.05f),
            NationalPosture.SpaceRace => (0.1f, 0.3f, 0.25f, 0.35f),
            NationalPosture.Recover => (0.1f, 0.1f, 0.8f, 0f),
            NationalPosture.Expand => (0.15f, 0.15f, 0.7f, 0f),
            _ => (0.12f, 0.25f, 0.6f, 0.03f)
        };

        var targetCiv = target.HasValue ? GetCivilizationById(target.Value) : null;
        var threatCiv = mainThreat.HasValue ? GetCivilizationById(mainThreat.Value) : null;
        s.Rationale = best switch
        {
            NationalPosture.Conquer when civ.AtWar => "Winning the war",
            NationalPosture.Conquer => $"Preparing to conquer the {targetCiv?.Name ?? "neighbours"}",
            NationalPosture.Fortify => $"Defending against the {threatCiv?.Name ?? "enemy"}",
            NationalPosture.Militarize => "Building up the armed forces",
            NationalPosture.Research => "Investing in knowledge",
            NationalPosture.SpaceRace => "Reaching for the stars",
            NationalPosture.Recover => starving ? "Fighting famine" : "Rebuilding after hardship",
            NationalPosture.Expand => "Settling new lands",
            _ => "Growing the economy"
        };
    }

    private int CountAllies(Civilization civ) =>
        civ.DiplomaticRelations.Values.Count(r => r.Status == DiplomaticStatus.Allied ||
                                                  r.HasTreaty(TreatyType.DefensivePact) ||
                                                  r.HasTreaty(TreatyType.MilitaryAlliance));

    private bool HasFreeLand(Civilization civ)
    {
        int maxSettlements = Math.Min(36, 3 + civ.TechLevel / 3 + (civ.Government?.Type == GovernmentType.Tribal ? 0 : 3));
        return civ.Cities.Count < maxSettlements;
    }

    private float RivalSpaceLead(Civilization civ)
    {
        int best = 0;
        foreach (var other in _civilizations)
        {
            if (other.Id != civ.Id) best = Math.Max(best, (int)other.SpaceStage);
        }
        return Math.Max(0, best - (int)civ.SpaceStage) / 3f;
    }

    /// <summary>
    /// How close the nation is to being destroyed in its current wars (0-1).
    /// </summary>
    private float ComputeExistentialThreat(Civilization civ)
    {
        if (!civ.AtWar) return 0f;

        float lostShare = civ.Strategy.CitiesAtWarStart > 0
            ? Math.Clamp(1f - civ.Cities.Count / (float)civ.Strategy.CitiesAtWarStart, 0f, 1f)
            : 0f;
        var capital = civ.Capital;
        bool capitalBesieged = capital != null && capital.UnderSiege;
        bool capitalLost = civ.Cities.All(c => c.OriginalCivilizationId != civ.Id) && civ.CitiesLost > 0;

        float enemyStrength = GetEnemies(civ).Sum(e => (float)e.MilitaryStrength);
        float outmatched = Math.Clamp(enemyStrength / Math.Max(1f, civ.MilitaryStrength) - 1.5f, 0f, 2f) / 2f;

        return Math.Clamp(lostShare * 1.2f + (capitalBesieged ? 0.35f : 0f) + (capitalLost ? 0.3f : 0f) + outmatched * 0.3f, 0f, 1f);
    }

    /// <summary>
    /// How attractive a city is as a military objective: close, weakly defended and
    /// valuable targets are preferred; cities next to nuclear power plants are avoided
    /// because fighting there risks a meltdown whose fallout would poison the winner too.
    /// </summary>
    private float ScoreMilitaryObjective(Army army, Civilization attacker, Civilization owner, City city)
    {
        float distance = WrappedDistance(city.X, city.Y, (int)army.X, (int)army.Y);
        float defense = GetCityDefense(owner, city);
        float score = distance + defense / Math.Max(1f, army.Soldiers) * 10f;

        if (city.IsCapital) score -= 5f;
        score -= Math.Min(10f, city.Population / 5000f);   // Rich prizes
        if (city.OriginalCivilizationId == attacker.Id) score -= 8f; // Lost homeland

        if (IsNearNuclearPlant(city.X, city.Y, 3))
        {
            // Only desperate nations fight near reactors
            score += attacker.Strategy.ExistentialThreat > 0.7f ? 5f : 40f;
        }

        return score;
    }

    private bool IsNearNuclearPlant(int x, int y, int radius)
    {
        for (int dx = -radius; dx <= radius; dx++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                int nx = WrapX(x + dx);
                int ny = y + dy;
                if (ny < 0 || ny >= _map.Height) continue;
                if (_map.Cells[nx, ny].GetGeology().HasNuclearPlant) return true;
            }
        }
        return false;
    }

    private (int x, int y)? FindNuclearPlantNear(int x, int y, int radius)
    {
        for (int dx = -radius; dx <= radius; dx++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                int nx = WrapX(x + dx);
                int ny = y + dy;
                if (ny < 0 || ny >= _map.Height) continue;
                if (_map.Cells[nx, ny].GetGeology().HasNuclearPlant) return (nx, ny);
            }
        }
        return null;
    }

    private Ideology? GetRulingIdeology(Civilization civ)
    {
        if (string.IsNullOrEmpty(civ.RulingParty)) return null;
        return civ.Parties.FirstOrDefault(p => p.Name == civ.RulingParty)?.Ideology;
    }
}
