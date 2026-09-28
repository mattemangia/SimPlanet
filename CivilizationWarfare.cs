namespace SimPlanet;

/// <summary>
/// Warfare: wars are declared for land, food and ambition, armies are raised from
/// settlements, march across the map, fight battles, besiege and capture cities.
/// Wars end when a side is exhausted; unhappy provinces may rebel and secede.
/// </summary>
public partial class CivilizationManager
{
    private readonly List<Army> _armies = new();
    private readonly List<BattleEvent> _recentBattles = new();
    private readonly Dictionary<int, bool> _lastKnownAtWar = new();
    private readonly Dictionary<int, int> _casualtiesThisYear = new();
    private readonly Dictionary<int, int> _citiesLostThisYear = new();
    private int _nextArmyId = 1;

    private const float BattleEffectDuration = 30f; // Simulation seconds (3 game years)

    public List<Army> GetArmies()
    {
        lock (_civLock)
        {
            return _armies.ToList();
        }
    }

    public List<BattleEvent> GetRecentBattles()
    {
        lock (_civLock)
        {
            return _recentBattles.ToList();
        }
    }

    private int GetSoldierCount(Civilization civ)
    {
        int total = 0;
        foreach (var army in _armies)
        {
            if (army.CivilizationId == civ.Id) total += army.Soldiers;
        }
        return total;
    }

    private static float GetTechFactor(Civilization civ) => 1f + civ.TechLevel * 0.04f;

    private bool AreAtWar(int civA, int civB)
    {
        var a = GetCivilizationById(civA);
        return a != null && a.DiplomaticRelations.TryGetValue(civB, out var r) && r.Status == DiplomaticStatus.War;
    }

    private IEnumerable<Civilization> GetEnemies(Civilization civ)
    {
        foreach (var (otherId, relation) in civ.DiplomaticRelations)
        {
            if (relation.Status != DiplomaticStatus.War) continue;
            var other = GetCivilizationById(otherId);
            if (other != null) yield return other;
        }
    }

    private void RecordBattle(int x, int y, int year, int attackerId, int defenderId, int casualties)
    {
        _recentBattles.Add(new BattleEvent
        {
            X = x,
            Y = y,
            Year = year,
            AttackerCivId = attackerId,
            DefenderCivId = defenderId,
            Casualties = casualties,
            Age = 0f
        });
        if (_recentBattles.Count > 64) _recentBattles.RemoveAt(0);
    }

    private void AgeBattleEffects(float deltaTime)
    {
        foreach (var battle in _recentBattles)
        {
            battle.Age += deltaTime;
        }
        _recentBattles.RemoveAll(b => b.Age > BattleEffectDuration);
    }

    #region Yearly war decisions

    private void UpdateWarfare(int currentYear)
    {
        SyncExternalWarDeclarations(currentYear);
        ConsiderNewWars(currentYear);
        UpdateWarWeariness();
        ConsiderPeace(currentYear);
        ConsiderNuclearEscalation(currentYear);
        RaiseArmies(currentYear);
        PlanArmyMovements();

        foreach (var civ in _civilizations)
        {
            civ.AtWar = GetEnemies(civ).Any();
            if (!civ.AtWar)
            {
                civ.WarTargetId = null;
            }
            else if (civ.WarTargetId == null || !AreAtWar(civ.Id, civ.WarTargetId.Value))
            {
                civ.WarTargetId = GetEnemies(civ).First().Id;
            }
            _lastKnownAtWar[civ.Id] = civ.AtWar;
        }

        _casualtiesThisYear.Clear();
        _citiesLostThisYear.Clear();
    }

    /// <summary>
    /// The player, divine powers and older UI code toggle AtWar/WarTargetId directly.
    /// Translate those toggles into proper declarations of war or peace.
    /// </summary>
    private void SyncExternalWarDeclarations(int currentYear)
    {
        foreach (var civ in _civilizations.ToList())
        {
            bool wasAtWar = _lastKnownAtWar.GetValueOrDefault(civ.Id);

            if (civ.AtWar && civ.WarTargetId.HasValue && !AreAtWar(civ.Id, civ.WarTargetId.Value))
            {
                var target = GetCivilizationById(civ.WarTargetId.Value);
                if (target != null)
                {
                    DeclareWarBetween(civ, target, currentYear, "by decree");
                }
            }
            else if (wasAtWar && !civ.AtWar)
            {
                foreach (var enemy in GetEnemies(civ).ToList())
                {
                    MakePeace(civ, enemy, currentYear, civ);
                }
            }
        }
    }

    /// <summary>
    /// Pairs of civilizations close enough to fight: shared borders, or cities within
    /// reach across the sea once ships are available.
    /// </summary>
    private IEnumerable<(Civilization a, Civilization b, int border)> GetContactPairs()
    {
        for (int i = 0; i < _civilizations.Count; i++)
        {
            for (int j = i + 1; j < _civilizations.Count; j++)
            {
                var a = _civilizations[i];
                var b = _civilizations[j];
                int border = GetBorderLength(a.Id, b.Id);
                if (border == 0 && (a.HasSeaTransport || b.HasSeaTransport))
                {
                    float reach = 12f + Math.Max(a.TechLevel, b.TechLevel) * 0.15f;
                    bool inReach = a.Cities.Any(ca => b.Cities.Any(cb => WrappedDistance(ca.X, ca.Y, cb.X, cb.Y) < reach));
                    if (inReach) border = 3;
                }
                if (border > 0) yield return (a, b, border);
            }
        }
    }

    private void ConsiderNewWars(int currentYear)
    {
        foreach (var (a, b, border) in GetContactPairs().ToList())
        {
            if (!a.DiplomaticRelations.TryGetValue(b.Id, out var relation)) continue;
            if (relation.Status == DiplomaticStatus.War) continue;
            if (relation.YearsAtPeace < 5) continue; // Fresh peace treaties hold for a while

            // Either side may be the aggressor
            foreach (var (aggressor, victim) in new[] { (a, b), (b, a) })
            {
                float tension = ComputeWarDesire(aggressor, victim, relation, border);
                if (tension <= 0.55f) continue;

                float chance = (tension - 0.55f) * 0.25f;
                if (_random.NextDouble() < chance)
                {
                    string reason = aggressor.Food < aggressor.FoodConsumption * 0.2f ? "hungry for fertile land"
                        : aggressor.MilitaryStrength > victim.MilitaryStrength * 1.5f ? "sensing weakness"
                        : relation.Opinion < -30 ? "after years of border clashes"
                        : "driven by ambition";
                    DeclareWarBetween(aggressor, victim, currentYear, reason);
                    break;
                }
            }
        }
    }

    private float ComputeWarDesire(Civilization aggressor, Civilization victim, DiplomaticRelation relation, int border)
    {
        var ruler = aggressor.Government?.CurrentRuler;
        float tension = aggressor.Aggression * 0.45f
            + (ruler?.Brutality ?? 0.5f) * 0.2f
            + (ruler?.Ambition ?? 0.5f) * 0.15f
            + Math.Min(border, 30) * 0.004f
            - relation.Opinion / 250f
            - relation.TrustLevel * 0.15f
            - aggressor.WarWeariness * 0.8f;

        // Need drives conflict: famine or crowded land
        if (aggressor.Food < aggressor.FoodConsumption * 0.2f) tension += 0.2f;
        if (aggressor.Cities.Any(c => c.Population > c.Capacity)) tension += 0.05f;

        // Opportunity: strike the weak
        float strengthRatio = aggressor.MilitaryStrength / (float)Math.Max(1, victim.MilitaryStrength);
        tension += Math.Clamp((strengthRatio - 1f) * 0.15f, -0.3f, 0.25f);

        // Already fighting elsewhere
        if (aggressor.AtWar) tension -= 0.25f;

        if (relation.HasTreaty(TreatyType.NonAggressionPact)) tension -= 0.35f;
        if (relation.HasTreaty(TreatyType.RoyalMarriage)) tension -= 0.3f;
        if (relation.HasTreaty(TreatyType.DefensivePact) || relation.HasTreaty(TreatyType.MilitaryAlliance)) tension -= 0.5f;
        if (relation.HasTreaty(TreatyType.TradePact)) tension -= 0.1f;

        // Pacifist governments rarely start wars
        tension += aggressor.Government?.Type switch
        {
            GovernmentType.Dictatorship => 0.15f,
            GovernmentType.Tribal => 0.05f,
            GovernmentType.Democracy => -0.2f,
            GovernmentType.Federation => -0.2f,
            GovernmentType.Republic => -0.1f,
            _ => 0f
        };

        // Nuclear deterrence
        if (victim.HasNuclearWeapons) tension -= 0.3f;

        return tension;
    }

    private void DeclareWarBetween(Civilization aggressor, Civilization victim, int currentYear, string reason, bool callAllies = true)
    {
        if (!aggressor.DiplomaticRelations.TryGetValue(victim.Id, out var relation))
        {
            relation = new DiplomaticRelation(aggressor.Id, victim.Id, currentYear);
            aggressor.DiplomaticRelations[victim.Id] = relation;
            victim.DiplomaticRelations[aggressor.Id] = relation;
        }

        if (relation.Status == DiplomaticStatus.War) return;

        // Breaking a pact is remembered by everyone
        bool brokePact = relation.HasTreaty(TreatyType.NonAggressionPact) || relation.HasTreaty(TreatyType.TradePact);
        relation.DeclareWar();
        relation.YearsAtWar = 0;
        relation.YearsAtPeace = 0;
        aggressor.AtWar = true;
        victim.AtWar = true;
        aggressor.WarTargetId ??= victim.Id;
        victim.WarTargetId ??= aggressor.Id;

        if (brokePact)
        {
            foreach (var other in _civilizations)
            {
                if (other.Id == aggressor.Id || other.Id == victim.Id) continue;
                if (other.DiplomaticRelations.TryGetValue(aggressor.Id, out var r))
                {
                    r.Opinion -= 15;
                    r.TrustLevel = Math.Max(0, r.TrustLevel - 0.2f);
                }
            }
        }

        AddChronicle(currentYear, HistoryCategory.War,
            $"The {aggressor.Name} declare war on the {victim.Name}, {reason}", victim.CenterX, victim.CenterY, aggressor.Id);

        // Allies honour their pacts (only for the original declaration, so wars do not cascade endlessly)
        if (!callAllies) return;
        foreach (var (allyId, allyRelation) in victim.DiplomaticRelations.ToList())
        {
            if (allyId == aggressor.Id) continue;
            if (!(allyRelation.HasTreaty(TreatyType.DefensivePact) || allyRelation.HasTreaty(TreatyType.MilitaryAlliance) ||
                  allyRelation.HasTreaty(TreatyType.RoyalMarriage)))
                continue;

            var ally = GetCivilizationById(allyId);
            if (ally == null || AreAtWar(ally.Id, aggressor.Id)) continue;
            if (_random.NextDouble() < 0.65)
            {
                DeclareWarBetween(ally, aggressor, currentYear, $"honouring their alliance with the {victim.Name}", callAllies: false);
            }
        }
    }

    private void UpdateWarWeariness()
    {
        foreach (var civ in _civilizations)
        {
            if (civ.AtWar)
            {
                int casualties = _casualtiesThisYear.GetValueOrDefault(civ.Id);
                int citiesLost = _citiesLostThisYear.GetValueOrDefault(civ.Id);
                float popShare = casualties / (float)Math.Max(1000, civ.Population);
                civ.WarWeariness += 0.015f + popShare * 4f + citiesLost * 0.12f;
                if (civ.Food <= 0) civ.WarWeariness += 0.05f;
                if (civ.Gold < 0) civ.WarWeariness += 0.03f;
                civ.WarWeariness *= 1f - (civ.Government?.CurrentRuler?.Brutality ?? 0.5f) * 0.05f;
            }
            else
            {
                civ.WarWeariness -= 0.04f;
                civ.WarCasualties = 0;
            }
            civ.WarWeariness = Math.Clamp(civ.WarWeariness, 0f, 1f);
        }
    }

    private void ConsiderPeace(int currentYear)
    {
        var visited = new HashSet<DiplomaticRelation>();
        foreach (var civ in _civilizations.ToList())
        {
            foreach (var enemy in GetEnemies(civ).ToList())
            {
                var relation = civ.DiplomaticRelations[enemy.Id];
                if (!visited.Add(relation)) continue;
                if (relation.YearsAtWar < 3) continue;

                float maxWeariness = Math.Max(civ.WarWeariness, enemy.WarWeariness);
                float minWeariness = Math.Min(civ.WarWeariness, enemy.WarWeariness);
                float chance = maxWeariness > 0.7f ? 0.35f
                    : minWeariness > 0.4f ? 0.3f
                    : relation.YearsAtWar > 40 ? 0.1f
                    : 0.01f;

                if (_random.NextDouble() < chance)
                {
                    var loser = civ.WarWeariness >= enemy.WarWeariness ? civ : enemy;
                    MakePeace(civ, enemy, currentYear, loser);
                }
            }
        }
    }

    private void MakePeace(Civilization a, Civilization b, int currentYear, Civilization loser)
    {
        if (!a.DiplomaticRelations.TryGetValue(b.Id, out var relation)) return;

        var winner = loser == a ? b : a;
        relation.Status = DiplomaticStatus.Hostile;
        relation.Opinion = -40f;
        relation.YearsAtPeace = 0;
        relation.AddTreaty(new Treaty(TreatyType.NonAggressionPact, currentYear, 25));

        // War reparations
        float tribute = Math.Max(0, loser.Gold) * 0.3f;
        loser.Gold -= tribute;
        winner.Gold += tribute;

        AddChronicle(currentYear, HistoryCategory.Peace,
            $"Peace between the {a.Name} and the {b.Name}; the {loser.Name} pay {tribute:F0} gold in reparations",
            loser.CenterX, loser.CenterY, winner.Id);

        a.AtWar = GetEnemies(a).Any();
        b.AtWar = GetEnemies(b).Any();

        // Armies go home
        foreach (var army in _armies)
        {
            if ((army.CivilizationId == a.Id && !a.AtWar) || (army.CivilizationId == b.Id && !b.AtWar))
            {
                if (army.IsGarrison)
                {
                    DisbandGarrison(army, army.CivilizationId == a.Id ? a : b);
                }
                else
                {
                    OrderRetreat(army);
                }
            }
            else if ((army.CivilizationId == a.Id && army.TargetCivId == b.Id) || (army.CivilizationId == b.Id && army.TargetCivId == a.Id))
            {
                army.State = ArmyState.Mustering;
                army.TargetCityId = 0;
            }
        }
    }

    private void ConsiderNuclearEscalation(int currentYear)
    {
        foreach (var civ in _civilizations.ToList())
        {
            if (!civ.HasNuclearWeapons || civ.NuclearStockpile <= 0) continue;
            if (_citiesLostThisYear.GetValueOrDefault(civ.Id) == 0) continue;

            float brutality = civ.Government?.CurrentRuler?.Brutality ?? 0.5f;
            foreach (var enemy in GetEnemies(civ).ToList())
            {
                if (_random.NextDouble() < 0.04 * brutality * (1f + civ.WarWeariness))
                {
                    LaunchNuclearStrike(civ, enemy, currentYear);
                    if (enemy.HasNuclearWeapons && enemy.NuclearStockpile > 0 && _random.NextDouble() < 0.7)
                    {
                        LaunchNuclearStrike(enemy, civ, currentYear);
                    }
                    break;
                }
            }
        }
    }

    /// <summary>
    /// Mobilise soldiers from settlements for civilizations at war.
    /// </summary>
    private void RaiseArmies(int currentYear)
    {
        foreach (var civ in _civilizations)
        {
            if (!civ.AtWar || civ.Cities.Count == 0) continue;

            var enemies = GetEnemies(civ).ToList();
            var enemyCities = enemies.SelectMany(e => e.Cities).ToList();
            if (enemyCities.Count == 0) continue;

            float mobilisation = 0.04f + civ.Aggression * 0.04f
                + (civ.Government?.CurrentRuler?.Brutality ?? 0.5f) * 0.02f
                + (civ.Government?.Type == GovernmentType.Dictatorship ? 0.03f : 0f)
                - civ.WarWeariness * 0.04f;
            int desired = (int)(civ.Population * Math.Max(0.01f, mobilisation));
            int current = GetSoldierCount(civ);
            int deficit = desired - current;
            if (deficit < 50 || civ.Gold < -100) continue;

            // Muster at the settlement closest to the enemy
            var muster = civ.Cities
                .Where(c => !c.UnderSiege)
                .OrderBy(c => enemyCities.Min(e => WrappedDistance(c.X, c.Y, e.X, e.Y)))
                .FirstOrDefault();
            if (muster == null) continue;

            int recruited = 0;
            foreach (var city in civ.Cities.OrderBy(c => WrappedDistance(c.X, c.Y, muster.X, muster.Y)))
            {
                float share = city.Has(CityBuilding.Barracks) ? 0.12f : 0.07f;
                int levy = Math.Min(deficit - recruited, (int)(city.Population * share));
                if (levy <= 0) continue;
                city.Population -= levy;
                recruited += levy;
                if (recruited >= deficit) break;
            }
            if (recruited < 30) continue;

            // Arms and armour
            float metalNeeded = recruited * 0.02f;
            float equipment = 0.85f;
            if (civ.Metal >= metalNeeded)
            {
                civ.Metal -= metalNeeded;
                equipment = 1.2f;
            }

            var existing = _armies.FirstOrDefault(a => a.CivilizationId == civ.Id &&
                a.State == ArmyState.Mustering && WrappedDistance((int)a.X, (int)a.Y, muster.X, muster.Y) < 2);
            if (existing != null)
            {
                existing.Equipment = (existing.Equipment * existing.Soldiers + equipment * recruited) / (existing.Soldiers + recruited);
                existing.Soldiers += recruited;
            }
            else
            {
                _armies.Add(new Army
                {
                    Id = _nextArmyId++,
                    CivilizationId = civ.Id,
                    HomeCityId = muster.Id,
                    X = muster.X,
                    Y = muster.Y,
                    Soldiers = recruited,
                    Morale = 1f,
                    Experience = muster.Has(CityBuilding.Barracks) ? 0.3f : 0f,
                    Equipment = equipment,
                    State = ArmyState.Mustering,
                    TargetX = muster.X,
                    TargetY = muster.Y
                });
            }

            RecalculatePopulation(civ);
        }
    }

    /// <summary>
    /// Give orders to armies: relieve besieged cities, attack the weakest enemy cities, go home when peace comes.
    /// </summary>
    private void PlanArmyMovements()
    {
        foreach (var army in _armies.ToList())
        {
            var civ = GetCivilizationById(army.CivilizationId);
            if (civ == null)
            {
                _armies.Remove(army);
                continue;
            }

            if (army.State == ArmyState.Retreating) continue;

            if (!civ.AtWar)
            {
                if (army.IsGarrison)
                {
                    DisbandGarrison(army, civ);
                }
                else
                {
                    OrderRetreat(army);
                }
                continue;
            }

            if (army.IsGarrison)
            {
                // Garrisons stay put while their settlement is still ours
                if (civ.Cities.Any(c => c.Id == army.HomeCityId)) continue;
                army.IsGarrison = false;
            }

            // Keep going if the current objective is still valid
            if ((army.State == ArmyState.Marching || army.State == ArmyState.Besieging) && army.TargetCityId != 0)
            {
                var targetCity = FindCity(army.TargetCityId, out var owner);
                if (targetCity != null && owner != null && AreAtWar(civ.Id, owner.Id)) continue;
                if (targetCity != null && owner?.Id == civ.Id && army.State == ArmyState.Marching) continue; // Relief march
            }

            ChooseArmyObjective(army, civ);
        }
    }

    private void ChooseArmyObjective(Army army, Civilization civ)
    {
        var enemies = GetEnemies(civ).ToList();

        // Defend: relieve our own cities under siege nearby
        var besiegedOwn = civ.Cities
            .Where(c => c.UnderSiege && WrappedDistance(c.X, c.Y, (int)army.X, (int)army.Y) < 30)
            .OrderBy(c => WrappedDistance(c.X, c.Y, (int)army.X, (int)army.Y))
            .FirstOrDefault();

        City? objective = besiegedOwn;
        Civilization? objectiveOwner = besiegedOwn != null ? civ : null;

        if (objective == null)
        {
            // Attack: prefer close, weakly defended enemy cities; capitals are prized
            float bestScore = float.MaxValue;
            foreach (var enemy in enemies)
            {
                foreach (var city in enemy.Cities)
                {
                    float distance = WrappedDistance(city.X, city.Y, (int)army.X, (int)army.Y);
                    float defense = GetCityDefense(enemy, city);
                    float score = distance + defense / Math.Max(1f, army.Soldiers) * 10f - (city.IsCapital ? 5f : 0f);
                    if (score < bestScore)
                    {
                        bestScore = score;
                        objective = city;
                        objectiveOwner = enemy;
                    }
                }
            }
        }

        if (objective == null || objectiveOwner == null)
        {
            army.State = ArmyState.Mustering;
            return;
        }

        var path = FindPath(civ, (int)MathF.Round(army.X), (int)MathF.Round(army.Y), objective.X, objective.Y);
        if (path == null)
        {
            army.State = ArmyState.Mustering;
            return;
        }

        army.Path = path;
        army.PathIndex = 0;
        army.MoveProgress = 0;
        army.TargetCityId = objective.Id;
        army.TargetCivId = objectiveOwner.Id;
        army.TargetX = objective.X;
        army.TargetY = objective.Y;
        army.State = ArmyState.Marching;
    }

    private void DisbandGarrison(Army army, Civilization civ)
    {
        var city = civ.Cities.FirstOrDefault(c => c.Id == army.HomeCityId);
        if (city != null)
        {
            // Soldiers settle down in the town they held
            city.Population += army.Soldiers;
            army.Soldiers = 0;
            RecalculatePopulation(civ);
        }
        else
        {
            army.IsGarrison = false;
            OrderRetreat(army);
        }
    }

    private void OrderRetreat(Army army)
    {
        var civ = GetCivilizationById(army.CivilizationId);
        var home = civ?.Cities.OrderBy(c => WrappedDistance(c.X, c.Y, (int)army.X, (int)army.Y)).FirstOrDefault();
        if (civ == null || home == null)
        {
            army.Soldiers = 0;
            return;
        }

        army.State = ArmyState.Retreating;
        army.HomeCityId = home.Id;
        army.TargetCityId = home.Id;
        army.TargetX = home.X;
        army.TargetY = home.Y;
        army.Path = FindPath(civ, (int)MathF.Round(army.X), (int)MathF.Round(army.Y), home.X, home.Y) ?? new List<(int, int)> { (home.X, home.Y) };
        army.PathIndex = 0;
        army.MoveProgress = 0;
    }

    private City? FindCity(int cityId, out Civilization? owner)
    {
        foreach (var civ in _civilizations)
        {
            foreach (var city in civ.Cities)
            {
                if (city.Id == cityId)
                {
                    owner = civ;
                    return city;
                }
            }
        }
        owner = null;
        return null;
    }

    #endregion

    #region Continuous army simulation

    private float GetArmySpeed(Civilization civ)
    {
        // Cells per game year
        if (civ.TechLevel >= 50) return 40f;   // Mechanised and air-lifted
        if (civ.HasRailTransport) return 22f;
        if (civ.HasLandTransport) return 12f;  // Horses
        return 7f;                             // On foot
    }

    private float GetMoveCost(Civilization civ, int x, int y)
    {
        var cell = _map.Cells[x, y];
        if (cell.IsWater) return civ.HasSeaTransport ? 1.2f : float.PositiveInfinity;
        if (cell.IsIce) return 3f;
        if (cell.Elevation > 0.6f) return 3f;          // Mountains
        if (cell.Elevation > 0.4f) return 1.6f;        // Hills
        if (cell.IsForest) return 1.5f;
        if (civ.Roads.Contains((x, y))) return 0.6f;
        return 1f;
    }

    private void UpdateArmies(float deltaYears, int currentYear)
    {
        if (_armies.Count == 0 || deltaYears <= 0)
        {
            return;
        }

        foreach (var civ in _civilizations)
        {
            foreach (var city in civ.Cities)
            {
                city.UnderSiege = false;
            }
        }

        foreach (var army in _armies.ToList())
        {
            var civ = GetCivilizationById(army.CivilizationId);
            if (civ == null)
            {
                army.Soldiers = 0;
                continue;
            }

            army.BattleCooldown = Math.Max(0, army.BattleCooldown - deltaYears);

            switch (army.State)
            {
                case ArmyState.Marching:
                case ArmyState.Retreating:
                    MoveArmy(army, civ, deltaYears);
                    break;
                case ArmyState.Besieging:
                    UpdateSiege(army, civ, deltaYears, currentYear);
                    break;
                case ArmyState.Mustering:
                    // Recover morale while encamped
                    army.Morale = Math.Min(1f, army.Morale + 0.2f * deltaYears);
                    break;
            }
        }

        ResolveBattles(currentYear);

        _armies.RemoveAll(a => a.Soldiers < 20);
    }

    private void MoveArmy(Army army, Civilization civ, float deltaYears)
    {
        if (army.Path == null || army.PathIndex >= army.Path.Count)
        {
            ArriveAtDestination(army, civ);
            return;
        }

        float budget = GetArmySpeed(civ) * deltaYears;
        while (budget > 0 && army.PathIndex < army.Path.Count)
        {
            var (nx, ny) = army.Path[army.PathIndex];
            float cost = GetMoveCost(civ, nx, ny);
            if (float.IsInfinity(cost))
            {
                // Terrain changed (sea level, lost ships): replan next year
                army.State = ArmyState.Mustering;
                return;
            }

            float needed = cost - army.MoveProgress;
            if (budget >= needed)
            {
                budget -= needed;
                army.MoveProgress = 0;
                army.X = nx;
                army.Y = ny;
                army.PathIndex++;
            }
            else
            {
                army.MoveProgress += budget;
                budget = 0;
            }
        }

        // Smooth position between cells for rendering
        if (army.PathIndex < army.Path.Count && army.MoveProgress > 0)
        {
            var (nx, ny) = army.Path[army.PathIndex];
            float t = Math.Clamp(army.MoveProgress / GetMoveCost(civ, nx, ny), 0f, 1f);
            int cx = (int)MathF.Round(army.X);
            int cy = (int)MathF.Round(army.Y);
            int dx = nx - cx;
            if (Math.Abs(dx) > _map.Width / 2) dx -= Math.Sign(dx) * _map.Width;
            army.X = cx + dx * t;
            army.Y = cy + (ny - cy) * t;
            if (army.X < 0) army.X += _map.Width;
            if (army.X >= _map.Width) army.X -= _map.Width;
        }

        if (army.PathIndex >= army.Path.Count)
        {
            ArriveAtDestination(army, civ);
        }
    }

    private void ArriveAtDestination(Army army, Civilization civ)
    {
        army.X = army.TargetX;
        army.Y = army.TargetY;

        if (army.State == ArmyState.Retreating)
        {
            // Soldiers return home to their families
            var home = civ.Cities.FirstOrDefault(c => c.Id == army.HomeCityId)
                       ?? civ.Cities.OrderBy(c => WrappedDistance(c.X, c.Y, army.TargetX, army.TargetY)).FirstOrDefault();
            if (home != null)
            {
                home.Population += army.Soldiers;
                RecalculatePopulation(civ);
            }
            army.Soldiers = 0;
            return;
        }

        var target = FindCity(army.TargetCityId, out var owner);
        if (target != null && owner != null && AreAtWar(civ.Id, owner.Id))
        {
            army.State = ArmyState.Besieging;
        }
        else
        {
            army.State = ArmyState.Mustering;
        }
    }

    private float GetCityDefense(Civilization owner, City city)
    {
        float militia = city.Population * 0.06f + 50f;
        float defense = militia * GetTechFactor(owner);
        if (city.Has(CityBuilding.Walls)) defense *= 2.2f;
        if (city.OnHighGround) defense *= 1.25f;
        if (city.IsCapital) defense *= 1.2f;
        defense *= 0.8f + city.Happiness * 0.4f;
        return defense;
    }

    private void UpdateSiege(Army army, Civilization civ, float deltaYears, int currentYear)
    {
        var city = FindCity(army.TargetCityId, out var owner);
        if (city == null || owner == null || !AreAtWar(civ.Id, owner.Id))
        {
            army.State = ArmyState.Mustering;
            army.TargetCityId = 0;
            return;
        }

        city.UnderSiege = true;

        float attack = army.Soldiers * GetTechFactor(civ) * army.Morale * army.Equipment * (0.8f + army.Experience * 0.4f);
        float defense = GetCityDefense(owner, city);
        float ratio = attack / Math.Max(1f, defense);

        if (ratio < 0.35f)
        {
            // Hopeless: lift the siege
            city.SiegeProgress = 0;
            army.Morale -= 0.2f;
            OrderRetreat(army);
            return;
        }

        city.SiegeProgress += deltaYears * 0.45f * Math.Min(ratio, 4f);

        // Attrition on both sides
        int attackerLosses = (int)(army.Soldiers * 0.06f * deltaYears * Math.Min(2f, 1f / ratio));
        army.Soldiers -= attackerLosses;
        _casualtiesThisYear[civ.Id] = _casualtiesThisYear.GetValueOrDefault(civ.Id) + attackerLosses;
        civ.WarCasualties += attackerLosses;

        int civilianLosses = (int)(city.Population * 0.04f * deltaYears);
        city.Population -= civilianLosses;
        _casualtiesThisYear[owner.Id] = _casualtiesThisYear.GetValueOrDefault(owner.Id) + civilianLosses;

        if (city.SiegeProgress >= 1f)
        {
            CaptureCity(civ, owner, city, army, currentYear);
        }
    }

    private void CaptureCity(Civilization attacker, Civilization defender, City city, Army army, int currentYear)
    {
        bool wasCapital = city.IsCapital;
        int defenderPopBefore = Math.Max(1, defender.Population);

        defender.Cities.Remove(city);
        attacker.Cities.Add(city);
        city.CivilizationId = attacker.Id;
        city.IsCapital = false;
        city.UnderSiege = false;
        city.SiegeProgress = 0;
        city.Starving = false;
        city.Happiness = 0.3f;

        // Sack and flight
        int refugees = (int)(city.Population * 0.1f);
        int killed = (int)(city.Population * 0.1f);
        city.Population -= refugees + killed;
        var refuge = defender.Cities.OrderBy(c => WrappedDistance(c.X, c.Y, city.X, city.Y)).FirstOrDefault();
        if (refuge != null) refuge.Population += refugees;
        if (_random.NextDouble() < 0.5) city.Buildings &= ~CityBuilding.Walls;

        // Plunder proportional to the city's weight in the realm
        float share = Math.Clamp(city.Population / (float)defenderPopBefore, 0.05f, 0.5f);
        float loot = Math.Max(0, defender.Gold) * share;
        defender.Gold -= loot;
        attacker.Gold += loot;
        float grain = defender.Food * share;
        defender.Food -= grain;
        attacker.Food += grain;

        // The surrounding land changes hands
        int radius = GetSettlementRadius(city) + 1;
        for (int dx = -radius; dx <= radius; dx++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                int x = WrapX(city.X + dx);
                int y = city.Y + dy;
                if (y < 0 || y >= _map.Height) continue;
                if (OwnerAt(x, y) != defender.Id) continue;
                float dCity = WrappedDistance(city.X, city.Y, x, y);
                bool closerToOtherCity = defender.Cities.Any(c => WrappedDistance(c.X, c.Y, x, y) < dCity);
                if (!closerToOtherCity)
                {
                    SetOwner(attacker, x, y);
                }
            }
        }
        SetOwner(attacker, city.X, city.Y);

        attacker.CitiesConquered++;
        defender.CitiesLost++;
        _citiesLostThisYear[defender.Id] = _citiesLostThisYear.GetValueOrDefault(defender.Id) + 1;
        army.Morale = Math.Min(1.2f, army.Morale + 0.15f);
        army.Experience = Math.Min(1f, army.Experience + 0.1f);
        army.State = ArmyState.Mustering;
        army.TargetCityId = 0;
        army.HomeCityId = city.Id;

        // Leave a garrison to hold the conquest; the rest of the army campaigns on
        int garrison = Math.Max(40, (int)(army.Soldiers * 0.3f));
        if (army.Soldiers - garrison >= 60)
        {
            army.Soldiers -= garrison;
            _armies.Add(new Army
            {
                Id = _nextArmyId++,
                CivilizationId = attacker.Id,
                HomeCityId = city.Id,
                X = city.X,
                Y = city.Y,
                Soldiers = garrison,
                Morale = army.Morale,
                Experience = army.Experience,
                Equipment = army.Equipment,
                State = ArmyState.Mustering,
                TargetX = city.X,
                TargetY = city.Y,
                IsGarrison = true
            });
        }
        else
        {
            army.IsGarrison = true;
        }

        AddChronicle(currentYear, HistoryCategory.Conquest,
            $"The {attacker.Name} capture {(wasCapital ? "the capital " : "")}{city.Name} from the {defender.Name}", city.X, city.Y, attacker.Id);

        if (wasCapital && defender.Cities.Count > 0)
        {
            var newCapital = defender.Cities.OrderByDescending(c => c.Population).First();
            newCapital.IsCapital = true;
            defender.CenterX = newCapital.X;
            defender.CenterY = newCapital.Y;
            defender.Stability = Math.Max(0f, defender.Stability - 0.25f);
            if (defender.Government != null) defender.Government.Stability -= 0.2f;
        }

        RecalculatePopulation(attacker);
        RecalculatePopulation(defender);

        if (defender.Cities.Count == 0)
        {
            AnnexCivilization(attacker, defender, currentYear);
        }
    }

    /// <summary>
    /// A people that lost all its settlements is absorbed by the conqueror.
    /// </summary>
    private void AnnexCivilization(Civilization conqueror, Civilization fallen, int currentYear)
    {
        foreach (var cell in fallen.Territory.ToList())
        {
            conqueror.Territory.Add(cell);
            if (_owner != null) _owner[cell.x, cell.y] = conqueror.Id;
        }
        fallen.Territory.Clear();

        conqueror.TechLevel = Math.Max(conqueror.TechLevel, (conqueror.TechLevel + fallen.TechLevel) / 2);
        conqueror.Gold += Math.Max(0, fallen.Gold);

        foreach (var army in _armies.Where(a => a.CivilizationId == fallen.Id))
        {
            army.Soldiers = 0; // Surrender
        }

        AddChronicle(currentYear, HistoryCategory.Conquest,
            $"The {fallen.Name} have been conquered and absorbed by the {conqueror.Name}", fallen.CenterX, fallen.CenterY, conqueror.Id);

        fallen.Population = 0;
        CollapseCivilization(fallen);
    }

    private void ResolveBattles(int currentYear)
    {
        for (int i = 0; i < _armies.Count; i++)
        {
            var a = _armies[i];
            if (a.Soldiers < 20 || a.BattleCooldown > 0) continue;

            for (int j = i + 1; j < _armies.Count; j++)
            {
                var b = _armies[j];
                if (b.Soldiers < 20 || b.BattleCooldown > 0) continue;
                if (a.CivilizationId == b.CivilizationId) continue;

                float dx = Math.Abs(a.X - b.X);
                if (dx > _map.Width / 2f) dx = _map.Width - dx;
                float dy = a.Y - b.Y;
                if (dx * dx + dy * dy > 2.25f) continue;
                if (!AreAtWar(a.CivilizationId, b.CivilizationId)) continue;

                FightBattle(a, b, currentYear);
                if (a.Soldiers < 20) break;
            }
        }
    }

    private void FightBattle(Army a, Army b, int currentYear)
    {
        var civA = GetCivilizationById(a.CivilizationId)!;
        var civB = GetCivilizationById(b.CivilizationId)!;

        float powerA = GetFieldPower(a, civA);
        float powerB = GetFieldPower(b, civB);
        float shareA = powerA / Math.Max(1f, powerA + powerB);

        bool aWins = _random.NextDouble() < Math.Clamp(0.5f + (shareA - 0.5f) * 1.6f, 0.05f, 0.95f);
        var winner = aWins ? a : b;
        var loser = aWins ? b : a;
        float winnerShare = aWins ? shareA : 1f - shareA;

        int loserLosses = (int)(loser.Soldiers * Math.Min(0.85f, 0.25f + 0.5f * winnerShare));
        int winnerLosses = (int)(winner.Soldiers * (0.05f + 0.35f * (1f - winnerShare)));
        loser.Soldiers -= loserLosses;
        winner.Soldiers -= winnerLosses;

        winner.Morale = Math.Min(1.2f, winner.Morale + 0.1f);
        loser.Morale = Math.Max(0.1f, loser.Morale - 0.35f);
        winner.Experience = Math.Min(1f, winner.Experience + 0.1f);
        loser.Experience = Math.Min(1f, loser.Experience + 0.05f);
        winner.BattleCooldown = 0.3f;
        loser.BattleCooldown = 0.6f;

        var winnerCiv = aWins ? civA : civB;
        var loserCiv = aWins ? civB : civA;
        _casualtiesThisYear[winnerCiv.Id] = _casualtiesThisYear.GetValueOrDefault(winnerCiv.Id) + winnerLosses;
        _casualtiesThisYear[loserCiv.Id] = _casualtiesThisYear.GetValueOrDefault(loserCiv.Id) + loserLosses;
        winnerCiv.WarCasualties += winnerLosses;
        loserCiv.WarCasualties += loserLosses;

        if (loser.Soldiers >= 20 && (loser.Morale < 0.4f || loser.Soldiers < winner.Soldiers * 0.3f))
        {
            OrderRetreat(loser);
        }

        int bx = (int)MathF.Round((a.X + b.X) / 2f) % _map.Width;
        int by = Math.Clamp((int)MathF.Round((a.Y + b.Y) / 2f), 0, _map.Height - 1);
        int casualties = loserLosses + winnerLosses;
        RecordBattle(bx, by, currentYear, winnerCiv.Id, loserCiv.Id, casualties);

        if (casualties >= 100)
        {
            AddChronicle(currentYear, HistoryCategory.Battle,
                $"Battle: the {winnerCiv.Name} defeat the {loserCiv.Name} ({casualties:N0} fallen)", bx, by, winnerCiv.Id);
        }
    }

    private float GetFieldPower(Army army, Civilization civ)
    {
        int x = Math.Clamp((int)MathF.Round(army.X), 0, _map.Width - 1);
        int y = Math.Clamp((int)MathF.Round(army.Y), 0, _map.Height - 1);
        var cell = _map.Cells[x, y];

        float terrain = 1f;
        if (OwnerAt(x, y) == civ.Id) terrain *= 1.15f;       // Home ground
        if (cell.Elevation > 0.6f) terrain *= 1.4f;           // Mountains
        else if (cell.Elevation > 0.4f) terrain *= 1.2f;      // Hills
        if (cell.IsForest) terrain *= 1.15f;
        if (army.State == ArmyState.Besieging) terrain *= 0.9f; // Caught in siege lines

        float random = 0.8f + (float)_random.NextDouble() * 0.4f;
        return army.Soldiers * GetTechFactor(civ) * army.Morale * army.Equipment * (0.8f + army.Experience * 0.4f) * terrain * random;
    }

    #endregion

    #region Pathfinding

    /// <summary>
    /// A* over the map grid with terrain costs. Water is only passable with ships.
    /// </summary>
    private List<(int x, int y)>? FindPath(Civilization civ, int sx, int sy, int tx, int ty)
    {
        sx = WrapX(sx);
        tx = WrapX(tx);
        sy = Math.Clamp(sy, 0, _map.Height - 1);
        ty = Math.Clamp(ty, 0, _map.Height - 1);

        if (sx == tx && sy == ty) return new List<(int, int)>();

        int width = _map.Width;
        int height = _map.Height;
        var cameFrom = new Dictionary<int, int>();
        var gScore = new Dictionary<int, float> { [sy * width + sx] = 0f };
        var open = new PriorityQueue<int, float>();
        open.Enqueue(sy * width + sx, WrappedDistance(sx, sy, tx, ty));

        int target = ty * width + tx;
        int expansions = 0;
        const int MaxExpansions = 40000;

        while (open.Count > 0 && expansions++ < MaxExpansions)
        {
            int current = open.Dequeue();
            if (current == target)
            {
                var path = new List<(int x, int y)>();
                while (current != sy * width + sx)
                {
                    path.Add((current % width, current / width));
                    current = cameFrom[current];
                }
                path.Reverse();
                return path;
            }

            int cx = current % width;
            int cy = current / width;
            float g = gScore[current];

            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0) continue;
                    int nx = WrapX(cx + dx);
                    int ny = cy + dy;
                    if (ny < 0 || ny >= height) continue;

                    float cost = GetMoveCost(civ, nx, ny);
                    if (float.IsInfinity(cost)) continue;
                    if (dx != 0 && dy != 0) cost *= 1.41f;

                    int next = ny * width + nx;
                    float tentative = g + cost;
                    if (gScore.TryGetValue(next, out float existing) && existing <= tentative) continue;

                    gScore[next] = tentative;
                    cameFrom[next] = current;
                    open.Enqueue(next, tentative + WrappedDistance(nx, ny, tx, ty) * 0.6f);
                }
            }
        }

        return null;
    }

    #endregion

    #region Rebellions

    /// <summary>
    /// Unhappy, distant provinces of unstable realms may break away.
    /// </summary>
    private void CheckRebellions(int currentYear)
    {
        foreach (var civ in _civilizations.ToList())
        {
            if (civ.Cities.Count < 4) continue;
            if (currentYear - civ.LastRebellionYear < 40) continue;

            float avgHappiness = civ.Cities.Average(c => c.Happiness);
            float govStability = civ.Government?.Stability ?? 0.5f;
            int unrest = (civ.Stability < 0.35f ? 1 : 0) + (govStability < 0.25f ? 1 : 0) + (avgHappiness < 0.4f ? 1 : 0);
            if (unrest < 2) continue;

            float chance = 0.02f + (civ.AtWar ? 0.02f : 0f) + Math.Max(0, 0.4f - avgHappiness) * 0.15f;
            if (_random.NextDouble() > chance) continue;

            var capital = civ.Capital;
            if (capital == null) continue;

            // The most distant, least happy province leads the revolt
            var core = civ.Cities
                .Where(c => !c.IsCapital && !c.UnderSiege)
                .OrderByDescending(c => WrappedDistance(c.X, c.Y, capital.X, capital.Y) * (1.2f - c.Happiness))
                .FirstOrDefault();
            if (core == null || WrappedDistance(core.X, core.Y, capital.X, capital.Y) < 8) continue;

            var rebels = civ.Cities
                .Where(c => !c.IsCapital && WrappedDistance(c.X, c.Y, core.X, core.Y) <= 12)
                .Take(Math.Max(1, civ.Cities.Count / 2))
                .ToList();

            civ.LastRebellionYear = currentYear;
            CreateBreakawayCivilization(civ, rebels, core, currentYear);
        }
    }

    private void CreateBreakawayCivilization(Civilization parent, List<City> rebelCities, City core, int currentYear)
    {
        var rebel = new Civilization
        {
            Id = _nextCivId++,
            Culture = parent.Culture,
            Name = "Free " + core.Name,
            NameRoot = core.Name,
            TribalName = "Free " + core.Name,
            CenterX = core.X,
            CenterY = core.Y,
            TechLevel = parent.TechLevel,
            CivType = parent.CivType,
            Aggression = Math.Clamp(parent.Aggression + (float)(_random.NextDouble() - 0.5) * 0.4f, 0f, 1f),
            EcoFriendliness = Math.Clamp(parent.EcoFriendliness + (float)(_random.NextDouble() - 0.5) * 0.4f, 0f, 1f),
            Founded = currentYear,
            LastRebellionYear = currentYear,
            Prosperity = 0.5f,
            Stability = 0.6f,
            HasLandTransport = parent.HasLandTransport,
            HasSeaTransport = parent.HasSeaTransport,
            HasRailTransport = parent.HasRailTransport,
            HasAirTransport = parent.HasAirTransport,
            Food = parent.Food * 0.3f,
            Wood = parent.Wood * 0.3f,
            Stone = parent.Stone * 0.3f,
            Metal = parent.Metal * 0.3f,
            Gold = Math.Max(0, parent.Gold) * 0.2f
        };
        parent.Food *= 0.7f;
        parent.Wood *= 0.7f;
        parent.Stone *= 0.7f;
        parent.Metal *= 0.7f;

        GovernmentType govType = parent.TechLevel switch
        {
            < 10 => GovernmentType.Tribal,
            < 30 => GovernmentType.Monarchy,
            _ => GovernmentType.Republic
        };
        rebel.Government = new Government(govType, currentYear);
        var ruler = _divinePowers.GenerateRandomRuler(rebel, currentYear);
        ruler.Id = _nextRulerId++;
        rebel.Government.CurrentRuler = ruler;
        rebel.AllRulers.Add(ruler);

        foreach (var city in rebelCities)
        {
            parent.Cities.Remove(city);
            rebel.Cities.Add(city);
            city.CivilizationId = rebel.Id;
            city.OriginalCivilizationId = rebel.Id;
            city.Happiness = 0.7f;
        }
        core.IsCapital = true;

        // Land goes to whichever side's settlement is closer
        foreach (var cell in parent.Territory.ToList())
        {
            float dRebel = rebel.Cities.Min(c => WrappedDistance(c.X, c.Y, cell.x, cell.y));
            float dParent = parent.Cities.Count > 0 ? parent.Cities.Min(c => WrappedDistance(c.X, c.Y, cell.x, cell.y)) : float.MaxValue;
            if (dRebel < dParent)
            {
                parent.Territory.Remove(cell);
                rebel.Territory.Add(cell);
            }
        }

        // Soldiers in the rebel provinces defect
        foreach (var army in _armies.Where(a => a.CivilizationId == parent.Id && rebelCities.Any(c => c.Id == a.HomeCityId)))
        {
            army.CivilizationId = rebel.Id;
            army.State = ArmyState.Mustering;
        }

        foreach (var other in _civilizations)
        {
            var relation = new DiplomaticRelation(rebel.Id, other.Id, currentYear);
            if (other.DiplomaticRelations.TryGetValue(parent.Id, out var parentRelation))
            {
                relation.Opinion = parentRelation.Opinion * 0.5f;
            }
            rebel.DiplomaticRelations[other.Id] = relation;
            other.DiplomaticRelations[rebel.Id] = relation;
        }

        _civilizations.Add(rebel);
        RecalculatePopulation(parent);
        RecalculatePopulation(rebel);
        rebel.LastKnownPopulation = rebel.Population;
        parent.Stability = Math.Min(1f, parent.Stability + 0.15f);
        RebuildOwnerMap();

        AddChronicle(currentYear, HistoryCategory.Rebellion,
            $"{core.Name} rebels against the {parent.Name}: {rebelCities.Count} settlement(s) form the {rebel.Name}", core.X, core.Y, rebel.Id);

        // The crown fights to keep its provinces, unless too weak
        if (parent.MilitaryStrength > rebel.MilitaryStrength && parent.WarWeariness < 0.5f)
        {
            DeclareWarBetween(parent, rebel, currentYear, "to crush the rebellion");
        }
    }

    #endregion
}

public enum ArmyState
{
    Mustering,   // Gathering at a settlement, waiting for orders
    Marching,    // Moving toward an objective
    Besieging,   // Laying siege to an enemy settlement
    Retreating   // Returning home
}

public class Army
{
    public int Id { get; set; }
    public int CivilizationId { get; set; }
    public int HomeCityId { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public int Soldiers { get; set; }
    public float Morale { get; set; } = 1f;       // 0.1-1.2
    public float Experience { get; set; }         // 0-1
    public float Equipment { get; set; } = 1f;    // Weapons quality multiplier
    public ArmyState State { get; set; } = ArmyState.Mustering;
    public int TargetX { get; set; }
    public int TargetY { get; set; }
    public int TargetCityId { get; set; }
    public int TargetCivId { get; set; }
    public float BattleCooldown { get; set; }
    public bool IsGarrison { get; set; }          // Holds a conquered settlement instead of campaigning

    internal List<(int x, int y)>? Path { get; set; }
    internal int PathIndex { get; set; }
    internal float MoveProgress { get; set; }
}

public class BattleEvent
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Year { get; set; }
    public int AttackerCivId { get; set; }
    public int DefenderCivId { get; set; }
    public int Casualties { get; set; }
    public float Age { get; set; }                // Simulation seconds since the battle
}
