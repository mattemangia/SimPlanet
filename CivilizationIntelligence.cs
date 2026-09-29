namespace SimPlanet;

/// <summary>
/// Intelligence services. Nations fund spy networks in the countries they fear, covet or
/// envy. Networks grow with money and time and carry out missions chosen by the nation's
/// strategy: gathering intelligence (an edge in battle), stealing technology, sabotage,
/// stirring unrest and — rarely — assassination. Counter-intelligence hunts them down;
/// an exposed network is a diplomatic scandal that can end in war.
/// </summary>
public partial class CivilizationManager
{
    private void UpdateIntelligence(int currentYear)
    {
        foreach (var civ in _civilizations.ToList())
        {
            if (!_civilizations.Contains(civ)) continue;

            // Networks in vanished nations dissolve
            civ.SpyNetworks.RemoveAll(n => GetCivilizationById(n.TargetCivId) == null || n.Agents <= 0);

            if (civ.TechLevel < 8)
            {
                civ.IntelligenceBudget = 0f;
                continue;
            }

            // Budget follows threats and ambitions
            var posture = civ.Strategy.Posture;
            float share = posture switch
            {
                NationalPosture.Conquer or NationalPosture.Militarize => 0.08f,
                NationalPosture.Fortify => 0.06f,
                NationalPosture.Research => 0.04f,
                _ => 0.02f
            };
            if (civ.Government?.Type is GovernmentType.Dictatorship) share += 0.03f;
            civ.IntelligenceBudget = Math.Max(0f, civ.GoldIncome) * share + civ.TechLevel * 0.05f;
            civ.Gold -= civ.IntelligenceBudget * 0.5f;

            // Counter-intelligence: money, technology, loyalty
            float ci = 0.1f + Math.Min(0.4f, civ.IntelligenceBudget / 200f) + civ.TechLevel / 400f + civ.Stability * 0.2f;
            if (civ.Government?.Type is GovernmentType.Dictatorship or GovernmentType.Theocracy) ci += 0.1f;
            civ.CounterIntelligence = Math.Clamp(ci, 0f, 0.95f);

            OpenNetworks(civ, currentYear);

            foreach (var network in civ.SpyNetworks.ToList())
            {
                var target = GetCivilizationById(network.TargetCivId);
                if (target == null) continue;
                RunNetwork(civ, target, network, currentYear);
                if (!_civilizations.Contains(civ)) break;
            }
        }
    }

    private void OpenNetworks(Civilization civ, int currentYear)
    {
        int maxNetworks = 1 + civ.TechLevel / 25;
        if (civ.SpyNetworks.Count >= maxNetworks || civ.IntelligenceBudget < 2f) return;

        // Priorities: current enemies, the main threat, the conquest target, then tech leaders
        var targets = new List<Civilization>();
        targets.AddRange(GetEnemies(civ));
        foreach (var id in new[] { civ.Strategy.MainThreatId, civ.Strategy.ConquestTargetId })
        {
            if (id.HasValue && GetCivilizationById(id.Value) is { } t) targets.Add(t);
        }
        targets.AddRange(_civilizations.Where(o => o.Id != civ.Id && o.TechLevel > civ.TechLevel + 5)
                                       .OrderByDescending(o => o.TechLevel));

        var target = targets.FirstOrDefault(t => t.Id != civ.Id && !civ.SpyNetworks.Any(n => n.TargetCivId == t.Id));
        if (target == null) return;

        civ.SpyNetworks.Add(new SpyNetwork
        {
            OwnerCivId = civ.Id,
            TargetCivId = target.Id,
            Agents = 3 + _random.Next(4),
            Strength = 0.05f,
            Exposure = 0.05f,
            EstablishedYear = currentYear
        });
    }

    private void RunNetwork(Civilization owner, Civilization target, SpyNetwork network, int currentYear)
    {
        // Growth with funding and time
        float funding = owner.IntelligenceBudget / Math.Max(1, owner.SpyNetworks.Count);
        network.Agents = Math.Min(60, network.Agents + (funding > 5f ? 1 : 0) + (_random.NextDouble() < 0.3 ? 1 : 0));
        network.Strength = Math.Clamp(network.Strength + 0.02f + funding / 500f - target.CounterIntelligence * 0.02f, 0f, 1f);

        network.Mission = ChooseMission(owner, target, network);

        // Counter-intelligence sweep
        network.Exposure = Math.Clamp(network.Exposure + target.CounterIntelligence * 0.015f + network.Agents / 4000f, 0f, 1f);
        if (_random.NextDouble() < network.Exposure * target.CounterIntelligence * 0.2f)
        {
            ExposeNetwork(owner, target, network, currentYear);
            return;
        }

        // Missions succeed more often with a strong network
        float success = network.Strength * (1f - target.CounterIntelligence * 0.5f);
        if (_random.NextDouble() > success * 0.3f) return;

        switch (network.Mission)
        {
            case IntelligenceMission.StealTechnology when target.TechLevel > owner.TechLevel:
                owner.TechLevel++;
                network.Successes++;
                network.Exposure += 0.1f;
                if (network.Successes == 1 || _random.NextDouble() < 0.1)
                {
                    AddChronicle(currentYear, HistoryCategory.Espionage,
                        $"Spies of the {owner.Name} steal technology from the {target.Name}", target.CenterX, target.CenterY, owner.Id);
                }
                break;

            case IntelligenceMission.Sabotage:
                var victim = target.Cities.Where(c => c.Buildings != CityBuilding.None).OrderByDescending(c => c.Population).FirstOrDefault();
                if (victim != null)
                {
                    var lost = new[] { CityBuilding.Workshop, CityBuilding.Barracks, CityBuilding.Granary, CityBuilding.Market }
                        .FirstOrDefault(b => victim.Has(b));
                    if (lost != CityBuilding.None) victim.Buildings &= ~lost;
                    victim.Electrified = false;
                    target.Metal *= 0.8f;
                    network.Successes++;
                    network.Exposure += 0.15f;
                    AddChronicle(currentYear, HistoryCategory.Espionage,
                        $"Saboteurs strike {victim.Name} ({target.Name}){(lost != CityBuilding.None ? $": the {lost.ToString().ToLower()} is destroyed" : "")}",
                        victim.X, victim.Y, owner.Id);
                }
                break;

            case IntelligenceMission.IncitingUnrest:
                target.Stability = Math.Max(0f, target.Stability - 0.05f * network.Strength);
                if (target.Government != null) target.Government.Stability -= 0.04f * network.Strength;
                foreach (var city in target.Cities.Where(c => c.OriginalCivilizationId != target.Id))
                {
                    city.Happiness = Math.Max(0f, city.Happiness - 0.05f);
                }
                network.Successes++;
                network.Exposure += 0.08f;
                break;

            case IntelligenceMission.Assassination:
                var ruler = target.Government?.CurrentRuler;
                if (ruler != null && ruler.IsAlive && _random.NextDouble() < 0.3)
                {
                    ruler.IsAlive = false;
                    ruler.DeathYear = currentYear;
                    network.Successes++;
                    network.Exposure += 0.5f;
                    AddChronicle(currentYear, HistoryCategory.Espionage,
                        $"{ruler.Name}, ruler of the {target.Name}, is assassinated", target.CenterX, target.CenterY, target.Id);
                    HandleSuccession(target, currentYear);
                }
                break;

            default:
                // Intelligence gathering: steady advantage in battle (see GetIntelligenceEdge)
                network.Successes++;
                break;
        }
    }

    private IntelligenceMission ChooseMission(Civilization owner, Civilization target, SpyNetwork network)
    {
        bool atWar = AreAtWar(owner.Id, target.Id);
        float brutality = owner.Government?.CurrentRuler?.Brutality ?? 0.5f;
        bool accountable = owner.Government?.Type is GovernmentType.Democracy or GovernmentType.Federation or GovernmentType.Republic;

        if (atWar && network.Strength > 0.6f && brutality > 0.8f && !accountable) return IntelligenceMission.Assassination;
        if (atWar && network.Strength > 0.3f) return IntelligenceMission.Sabotage;
        if (target.TechLevel > owner.TechLevel + 3) return IntelligenceMission.StealTechnology;
        if (owner.Strategy.ConquestTargetId == target.Id && network.Strength > 0.4f) return IntelligenceMission.IncitingUnrest;
        return IntelligenceMission.GatherIntelligence;
    }

    private void ExposeNetwork(Civilization owner, Civilization target, SpyNetwork network, int currentYear)
    {
        int caught = Math.Max(1, (int)(network.Agents * (0.5f + (float)_random.NextDouble() * 0.5f)));
        network.Agents -= caught;
        network.AgentsLost += caught;
        network.Compromised = true;
        network.Strength *= 0.4f;
        network.Exposure = 0.1f;

        if (target.DiplomaticRelations.TryGetValue(owner.Id, out var relation))
        {
            relation.Opinion = Math.Max(-100f, relation.Opinion - 15f);
            relation.TrustLevel = Math.Max(0f, relation.TrustLevel - 0.2f);
        }

        if (caught >= 10)
        {
            AddChronicle(currentYear, HistoryCategory.Espionage,
                $"The {target.Name} uncover a spy ring of the {owner.Name}: {caught} agents arrested", target.CenterX, target.CenterY, target.Id);
        }

        // A scandal can be the last straw for an aggressive, stronger target
        if (!AreAtWar(owner.Id, target.Id) && target.Aggression > 0.7f &&
            target.MilitaryStrength > owner.MilitaryStrength && _random.NextDouble() < 0.15)
        {
            DeclareWarBetween(target, owner, currentYear, "after uncovering their spies");
        }

        if (network.Agents <= 0)
        {
            owner.SpyNetworks.Remove(network);
        }
    }

    /// <summary>
    /// Battlefield advantage from knowing the enemy's plans (1.0 = none, up to 1.2).
    /// </summary>
    private float GetIntelligenceEdge(Civilization civ, Civilization enemy)
    {
        var network = civ.SpyNetworks.FirstOrDefault(n => n.TargetCivId == enemy.Id && !n.Compromised);
        return network == null ? 1f : 1f + network.Strength * 0.2f;
    }
}
