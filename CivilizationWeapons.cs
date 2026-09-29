namespace SimPlanet;

/// <summary>
/// Weapons of mass destruction and their consequences.
///
/// Nations do not use them lightly: first use needs an existential threat and a brutal
/// or authoritarian leadership, and is strongly deterred when the enemy can retaliate.
/// A nuclear attack is almost always answered, and exchanges can escalate. Every use
/// outrages the world. Burning cities fill the stratosphere with soot (nuclear winter),
/// harvests fail, fallout poisons the land, and reactors caught in the fighting melt down.
/// </summary>
public partial class CivilizationManager
{
    private DiseaseManager? _diseaseManager;
    private float _nuclearWinterApplied;                       // Solar reduction currently applied
    private readonly Dictionary<(int attacker, int victim), int> _nuclearAttacks = new(); // Year of last strike

    public void SetDiseaseManager(DiseaseManager diseaseManager)
    {
        _diseaseManager = diseaseManager;
    }

    #region Arsenal

    private void UpdateArsenal(Civilization civ)
    {
        var arsenal = civ.Arsenal;
        var posture = civ.Strategy.Posture;
        bool armed = posture is NationalPosture.Fortify or NationalPosture.Militarize or NationalPosture.Conquer;

        if (civ.HasNuclearWeapons)
        {
            // Democracies keep a small deterrent; others build more when threatened
            int cap = civ.Government?.Type is GovernmentType.Democracy or GovernmentType.Federation or GovernmentType.Republic ? 20 : 60;
            if (armed && civ.NuclearStockpile < cap && civ.Gold > 100 && _random.NextDouble() < 0.25)
            {
                civ.NuclearStockpile++;
                civ.Gold -= 20;
            }
            // Arms reduction in peaceful times
            if (!armed && civ.Strategy.ThreatLevel < 0.3f && civ.NuclearStockpile > 10 && _random.NextDouble() < 0.1)
            {
                civ.NuclearStockpile--;
            }
            arsenal.MissileSilos = Math.Max(arsenal.MissileSilos, Math.Min(civ.NuclearStockpile / 3 + 1, civ.Cities.Count));
        }

        if (civ.TechLevel >= 30 && arsenal.ChemicalStockpile > 0 && !armed && _random.NextDouble() < 0.05)
        {
            arsenal.ChemicalStockpile--; // Stockpiles are destroyed in peacetime
        }

        arsenal.NuclearWarheads = civ.NuclearStockpile;
    }

    #endregion

    #region Doctrine

    private void ConsiderWeaponsOfMassDestruction(int currentYear)
    {
        foreach (var civ in _civilizations.ToList())
        {
            civ.Strategy.ChemicalWeaponsAuthorized = false;
            if (!civ.AtWar || !_civilizations.Contains(civ)) continue;

            var ruler = civ.Government?.CurrentRuler;
            float brutality = ruler?.Brutality ?? 0.5f;
            float threat = civ.Strategy.ExistentialThreat;
            bool accountable = civ.Government?.Type is GovernmentType.Democracy or GovernmentType.Federation or GovernmentType.Republic;

            foreach (var enemy in GetEnemies(civ).ToList())
            {
                if (!_civilizations.Contains(enemy) || !_civilizations.Contains(civ)) break;

                // Second strike: a nuclear attack is almost always answered
                bool wasNuked = _nuclearAttacks.TryGetValue((enemy.Id, civ.Id), out int lastStrike) && currentYear - lastStrike <= 1;
                if (wasNuked && civ.HasNuclearWeapons && civ.NuclearStockpile > 0 && _random.NextDouble() < 0.85)
                {
                    int salvo = Math.Min(civ.NuclearStockpile, 1 + _nuclearAttacks.Count(k => k.Key.victim == civ.Id));
                    for (int i = 0; i < salvo; i++) LaunchNuclearStrike(civ, enemy, currentYear, retaliation: true);
                    continue;
                }

                // First use: only facing annihilation, and only by ruthless or unaccountable leaders
                if (civ.HasNuclearWeapons && civ.NuclearStockpile > 0)
                {
                    float needed = accountable ? 0.95f : 0.75f;
                    bool ruthless = brutality >= 0.6f || civ.Government?.Type == GovernmentType.Dictatorship;
                    if (threat >= needed && ruthless)
                    {
                        float chance = 0.12f * (threat - needed + 0.05f) / (1f - needed + 0.05f) * brutality;
                        if (enemy.HasNuclearWeapons) chance *= 0.3f;              // Mutually assured destruction
                        if (EnemyHasNuclearAllies(enemy, civ)) chance *= 0.5f;
                        if (_random.NextDouble() < chance)
                        {
                            LaunchNuclearStrike(civ, enemy, currentYear, retaliation: false);
                            continue;
                        }
                    }
                }

                // Chemical weapons: desperate, brutal regimes in sieges
                if (civ.TechLevel >= 30 && civ.Arsenal.ChemicalStockpile > 0 && !accountable &&
                    threat >= 0.4f && brutality >= 0.7f && _random.NextDouble() < 0.3)
                {
                    civ.Strategy.ChemicalWeaponsAuthorized = true;
                }

                // Biological weapons: the last, reckless gamble
                if (civ.Arsenal.BioweaponProgram && _diseaseManager != null && !accountable &&
                    threat >= 0.85f && brutality >= 0.8f && _random.NextDouble() < 0.03)
                {
                    ReleaseBioweapon(civ, enemy, currentYear);
                }
            }
        }

        UpdateNuclearWinter();
    }

    private bool EnemyHasNuclearAllies(Civilization enemy, Civilization attacker)
    {
        foreach (var (allyId, relation) in enemy.DiplomaticRelations)
        {
            if (allyId == attacker.Id) continue;
            if (!(relation.HasTreaty(TreatyType.DefensivePact) || relation.HasTreaty(TreatyType.MilitaryAlliance))) continue;
            if (GetCivilizationById(allyId)?.HasNuclearWeapons == true) return true;
        }
        return false;
    }

    /// <summary>
    /// Every other nation condemns the use of weapons of mass destruction; friends of the
    /// victim may join the war against the perpetrator.
    /// </summary>
    private void WorldOutrage(Civilization perpetrator, Civilization victim, float severity, int currentYear)
    {
        foreach (var other in _civilizations.ToList())
        {
            if (other.Id == perpetrator.Id) continue;
            if (!other.DiplomaticRelations.TryGetValue(perpetrator.Id, out var relation)) continue;

            relation.Opinion = Math.Max(-100f, relation.Opinion - 40f * severity);
            relation.TrustLevel = Math.Max(0f, relation.TrustLevel - 0.3f * severity);

            if (other.Id == victim.Id || relation.Status == DiplomaticStatus.War) continue;

            bool friendOfVictim = other.DiplomaticRelations.TryGetValue(victim.Id, out var victimRelation) &&
                                  victimRelation.Opinion > 20;
            bool strongEnough = other.MilitaryStrength > perpetrator.MilitaryStrength * 0.6f;
            bool deterred = perpetrator.HasNuclearWeapons && !other.HasNuclearWeapons;
            if (friendOfVictim && strongEnough && !deterred && _random.NextDouble() < 0.3 * severity)
            {
                DeclareWarBetween(other, perpetrator, currentYear, "in outrage at their atrocities", callAllies: false);
            }
        }
    }

    #endregion

    #region Nuclear strikes

    private void LaunchNuclearStrike(Civilization attacker, Civilization defender, int currentYear, bool retaliation)
    {
        if (attacker.NuclearStockpile <= 0 || defender.Cities.Count == 0) return;

        attacker.NuclearStockpile--;
        attacker.Arsenal.NuclearWarheads = attacker.NuclearStockpile;
        attacker.Arsenal.NuclearStrikesLaunched++;
        _nuclearAttacks[(attacker.Id, defender.Id)] = currentYear;

        // Missile defence may intercept the warhead
        if (defender.Arsenal.MissileDefense && _random.NextDouble() < 0.5)
        {
            AddChronicle(currentYear, HistoryCategory.War,
                $"The {defender.Name} intercept a nuclear missile launched by the {attacker.Name}",
                defender.CenterX, defender.CenterY, defender.Id);
            WorldOutrage(attacker, defender, retaliation ? 0.4f : 1f, currentYear);
            return;
        }

        // Doctrine: strike the largest city, but not one whose fallout would drift onto our own land,
        // and avoid reactors whose meltdown would poison the region for decades
        var target = defender.Cities
            .OrderByDescending(c => c.Population
                                    - (IsNearNuclearPlant(c.X, c.Y, 4) ? c.Population * 0.5f : 0f)
                                    - (attacker.Cities.Any(own => WrappedDistance(own.X, own.Y, c.X, c.Y) < 15) ? c.Population * 0.8f : 0f))
            .First();

        int strikeX = target.X;
        int strikeY = target.Y;

        // Blast, fire, radiation and fallout (terrain, life, tsunamis, EMP)
        if (_disasterManager != null)
        {
            _disasterManager.TriggerNuclearAccident(strikeX, strikeY, currentYear, isWeapon: true);
            _map.SolarEnergy += 0.05f; // Sunlight loss is handled by the nuclear winter model below
        }

        // Casualties in and around the city
        int casualties = 0;
        foreach (var civ in _civilizations)
        {
            foreach (var city in civ.Cities)
            {
                float distance = WrappedDistance(city.X, city.Y, strikeX, strikeY);
                if (distance > 15) continue;
                float lethality = distance <= 2 ? 0.9f : distance <= 6 ? 0.5f : 0.15f;
                int dead = (int)(city.Population * lethality);
                city.Population -= dead;
                city.Happiness = Math.Max(0f, city.Happiness - 0.4f);
                casualties += dead;
                if (distance <= 2) city.Buildings = CityBuilding.None;
            }
            RecalculatePopulation(civ);
        }

        foreach (var army in _armies)
        {
            if (WrappedDistance((int)army.X, (int)army.Y, strikeX, strikeY) <= 6)
            {
                army.Soldiers = (int)(army.Soldiers * 0.1f);
            }
        }

        // A reactor in the blast zone melts down
        var plant = FindNuclearPlantNear(strikeX, strikeY, 6);
        if (plant != null)
        {
            CauseMeltdown(plant.Value.x, plant.Value.y, currentYear, $"the strike on {target.Name}");
        }

        // Burning cities loft soot into the stratosphere
        NuclearWinter = Math.Min(1f, NuclearWinter + 0.06f + Math.Min(0.06f, casualties / 2_000_000f));

        defender.Stability = Math.Max(0f, defender.Stability - 0.3f);
        attacker.Strategy.PostureSinceYear = currentYear;

        AddChronicle(currentYear, HistoryCategory.War,
            retaliation
                ? $"NUCLEAR RETALIATION: the {attacker.Name} strike {target.Name} ({casualties:N0} dead)"
                : $"NUCLEAR ATTACK: the {attacker.Name} destroy {target.Name} ({casualties:N0} dead)",
            strikeX, strikeY, attacker.Id);
        RecordBattle(strikeX, strikeY, currentYear, attacker.Id, defender.Id, casualties);
        WorldOutrage(attacker, defender, retaliation ? 0.4f : 1f, currentYear);
        RebuildOwnerMap();
    }

    /// <summary>
    /// Soot from burning cities blocks sunlight for years: temperatures drop, harvests fail.
    /// </summary>
    private void UpdateNuclearWinter()
    {
        float targetReduction = NuclearWinter * 0.3f;
        float delta = targetReduction - _nuclearWinterApplied;
        if (Math.Abs(delta) > 0.0001f)
        {
            _map.SolarEnergy = Math.Clamp(_map.SolarEnergy - delta, 0.3f, 2f);
            _nuclearWinterApplied = targetReduction;
        }

        // Soot rains out over a few years
        NuclearWinter *= 0.82f;
        if (NuclearWinter < 0.005f) NuclearWinter = 0f;
    }

    /// <summary>
    /// A reactor destroyed by war: fallout spreads over the region.
    /// </summary>
    private void CauseMeltdown(int x, int y, int currentYear, string cause)
    {
        var geo = _map.Cells[x, y].GetGeology();
        if (!geo.HasNuclearPlant) return;
        geo.HasNuclearPlant = false;
        geo.MeltdownRisk = 0f;

        _disasterManager?.TriggerNuclearAccident(x, y, currentYear, isWeapon: false);

        int dead = 0;
        foreach (var civ in _civilizations)
        {
            foreach (var city in civ.Cities)
            {
                float distance = WrappedDistance(city.X, city.Y, x, y);
                if (distance > 10) continue;
                int d = (int)(city.Population * (distance < 4 ? 0.2f : 0.05f));
                city.Population -= d;
                city.Happiness = Math.Max(0f, city.Happiness - 0.2f);
                dead += d;
            }
            RecalculatePopulation(civ);
        }

        int owner = OwnerAt(x, y);
        AddChronicle(currentYear, HistoryCategory.Disaster,
            $"A nuclear reactor melts down after {cause}: fallout spreads ({dead:N0} dead)", x, y, owner);
    }

    #endregion

    /// <summary>
    /// Doomsday: every nuclear power launches its whole arsenal at its rivals.
    /// Used by divine powers and by headless tests.
    /// </summary>
    public void TriggerGlobalNuclearWar(int currentYear)
    {
        lock (_civLock)
        {
            var powers = _civilizations.Where(c => c.HasNuclearWeapons && c.NuclearStockpile > 0).ToList();
            AddChronicle(currentYear, HistoryCategory.War, "GLOBAL NUCLEAR WAR: the great powers launch their arsenals", -1, -1, 0);

            foreach (var attacker in powers)
            {
                foreach (var other in _civilizations.Where(c => c.Id != attacker.Id).ToList())
                {
                    if (!AreAtWar(attacker.Id, other.Id))
                    {
                        DeclareWarBetween(attacker, other, currentYear, "as the missiles fly", callAllies: false, byPlayer: true);
                    }
                }
            }

            bool fired = true;
            while (fired)
            {
                fired = false;
                foreach (var attacker in powers.Where(p => _civilizations.Contains(p) && p.NuclearStockpile > 0))
                {
                    var target = _civilizations
                        .Where(c => c.Id != attacker.Id && c.Cities.Count > 0)
                        .OrderByDescending(c => c.Population)
                        .FirstOrDefault();
                    if (target == null) continue;
                    LaunchNuclearStrike(attacker, target, currentYear, retaliation: true);
                    fired = true;
                }
            }

            foreach (var civ in _civilizations.ToList())
            {
                CheckCivilizationCollapse(civ, currentYear);
            }
        }
    }

    /// <summary>Until this year divine peace forbids new wars.</summary>
    public int DivinePeaceUntilYear { get; private set; } = int.MinValue;

    /// <summary>
    /// Divine intervention: every war ends at once, without reparations. Nations sign
    /// non-aggression pacts, armies go home, grudges soften, and no new war can start
    /// for a generation.
    /// </summary>
    public void TriggerWorldPeace(int currentYear, int years = 30)
    {
        lock (_civLock)
        {
            int warsEnded = 0;
            var visited = new HashSet<DiplomaticRelation>();
            foreach (var civ in _civilizations)
            {
                foreach (var relation in civ.DiplomaticRelations.Values)
                {
                    if (!visited.Add(relation)) continue;

                    if (relation.Status == DiplomaticStatus.War)
                    {
                        warsEnded++;
                    }
                    relation.Status = DiplomaticStatus.Neutral;
                    relation.Opinion = Math.Max(relation.Opinion + 30f, 0f);
                    relation.YearsAtPeace = 0;
                    if (!relation.HasTreaty(TreatyType.NonAggressionPact))
                    {
                        relation.AddTreaty(new Treaty(TreatyType.NonAggressionPact, currentYear, years));
                    }
                }
            }

            foreach (var civ in _civilizations)
            {
                civ.AtWar = false;
                civ.WarTargetId = null;
                civ.WarWeariness = 0f;
                civ.Strategy.ConquestTargetId = null;
                civ.Strategy.ChemicalWeaponsAuthorized = false;
                if (civ.Strategy.Posture is NationalPosture.Conquer or NationalPosture.Militarize)
                {
                    civ.Strategy.Posture = NationalPosture.Develop;
                    civ.Strategy.PostureSinceYear = currentYear;
                }
                foreach (var city in civ.Cities)
                {
                    city.UnderSiege = false;
                    city.SiegeProgress = 0f;
                }
                _lastKnownAtWar[civ.Id] = false;
            }

            foreach (var army in _armies)
            {
                var owner = GetCivilizationById(army.CivilizationId);
                if (army.IsGarrison && owner != null) DisbandGarrison(army, owner);
                else OrderRetreat(army);
            }
            _armies.RemoveAll(a => a.Soldiers < 20);
            _nuclearAttacks.Clear();

            DivinePeaceUntilYear = currentYear + years;
            AddChronicle(currentYear, HistoryCategory.Peace,
                warsEnded > 0
                    ? $"DIVINE PEACE: {warsEnded} war(s) end at once; the nations swear {years} years of peace"
                    : $"DIVINE PEACE: the nations swear {years} years of peace",
                -1, -1, 0);
        }
    }

    #region Chemical and biological weapons

    /// <summary>
    /// Chemical attack during a siege: kills defenders and civilians, speeds up the siege.
    /// </summary>
    private void UseChemicalWeapons(Army army, Civilization attacker, Civilization defender, City city, int currentYear)
    {
        attacker.Arsenal.ChemicalStockpile--;
        attacker.Arsenal.ChemicalAttacks++;
        attacker.Strategy.ChemicalWeaponsAuthorized = false;

        int dead = (int)(city.Population * 0.15f);
        city.Population -= dead;
        city.Happiness = Math.Max(0f, city.Happiness - 0.3f);
        city.SiegeProgress += 0.3f;
        RecalculatePopulation(defender);

        AddChronicle(currentYear, HistoryCategory.War,
            $"The {attacker.Name} use poison gas on {city.Name} ({dead:N0} dead)", city.X, city.Y, attacker.Id);
        WorldOutrage(attacker, defender, 0.6f, currentYear);
    }

    private void ReleaseBioweapon(Civilization attacker, Civilization victim, int currentYear)
    {
        var target = victim.Capital;
        if (target == null || _diseaseManager == null) return;

        attacker.Arsenal.BioweaponReleases++;
        string name = $"{GenerateWord(attacker.Culture, 2, 2)} Strain";
        var disease = _diseaseManager.CreateDisease(name, PathogenType.Bioweapon, target.X, target.Y);
        disease.OriginCivId = victim.Id;

        AddChronicle(currentYear, HistoryCategory.Epidemic,
            $"BIOWEAPON: the {attacker.Name} release the engineered {name} in {target.Name}", target.X, target.Y, attacker.Id);
        WorldOutrage(attacker, victim, 1f, currentYear);
    }

    #endregion
}
