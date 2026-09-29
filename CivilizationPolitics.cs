namespace SimPlanet;

/// <summary>
/// Internal politics: political parties and elections in republics and democracies,
/// and dynastic succession in monarchies (births, lines of succession, regencies,
/// succession crises and wars of succession).
/// </summary>
public partial class CivilizationManager
{
    private static readonly Dictionary<Ideology, string[]> PartyNames = new()
    {
        [Ideology.Conservative] = new[] { "Conservative Party", "Party of Order", "Traditionalist League" },
        [Ideology.Liberal] = new[] { "Liberal Union", "Free Citizens", "Progressive Party" },
        [Ideology.Socialist] = new[] { "Workers' Party", "Social Democrats", "People's Front" },
        [Ideology.Green] = new[] { "Green Alliance", "Earth Party", "Ecologists" },
        [Ideology.Nationalist] = new[] { "National Front", "Patriotic Union", "Homeland Party" },
        [Ideology.Technocratic] = new[] { "Technocratic Movement", "Science Party", "Future Coalition" },
        [Ideology.Religious] = new[] { "Faith Party", "Covenant Party", "Pious Union" }
    };

    private void UpdatePolitics(int currentYear)
    {
        foreach (var civ in _civilizations.ToList())
        {
            var government = civ.Government;
            if (government == null) continue;

            if (government.IsElected || government.Type == GovernmentType.Oligarchy)
            {
                UpdateParties(civ, currentYear);
            }
            else if (civ.Parties.Count > 0)
            {
                civ.Parties.Clear();
                civ.RulingParty = "";
            }

            if (government.IsHereditary)
            {
                UpdateRoyalFamily(civ, currentYear);
            }
            else
            {
                civ.SuccessionLine.Clear();
                civ.HeirApparent = null;
            }
        }
    }

    #region Parties and elections

    private void UpdateParties(Civilization civ, int currentYear)
    {
        if (civ.Parties.Count == 0)
        {
            FoundParties(civ, currentYear);
            civ.NextElectionYear = currentYear + 1;
        }

        ApplyIdeology(civ);

        if (currentYear < civ.NextElectionYear) return;

        int term = civ.Government!.Type switch
        {
            GovernmentType.Democracy => 4,
            GovernmentType.Federation => 4,
            GovernmentType.Oligarchy => 8,
            _ => 5
        };
        civ.NextElectionYear = currentYear + term;
        HoldElection(civ, currentYear);
    }

    private void FoundParties(Civilization civ, int currentYear)
    {
        var ideologies = new List<Ideology> { Ideology.Conservative, Ideology.Liberal };
        if (civ.CivType >= CivType.Industrial) ideologies.Add(Ideology.Socialist);
        if (civ.EcoFriendliness > 0.5f || civ.TechLevel >= 60) ideologies.Add(Ideology.Green);
        if (civ.Aggression > 0.5f || civ.Strategy.ThreatLevel > 0.8f) ideologies.Add(Ideology.Nationalist);
        if (civ.TechLevel >= 50) ideologies.Add(Ideology.Technocratic);
        if (civ.Cities.Count(c => c.Has(CityBuilding.Temple)) > civ.Cities.Count / 2) ideologies.Add(Ideology.Religious);

        foreach (var ideology in ideologies)
        {
            var names = PartyNames[ideology];
            string name = ideology == Ideology.Religious ? "Faith Party" : names[_random.Next(names.Length)];
            civ.Parties.Add(new PoliticalParty
            {
                Name = $"{name} of {civ.NameRoot}",
                Ideology = ideology,
                Support = 0.5f + (float)_random.NextDouble(),
                FoundedYear = currentYear
            });
        }
        NormaliseSupport(civ);
    }

    private void HoldElection(Civilization civ, int currentYear)
    {
        // Parties founded later as the country changes
        if (!civ.Parties.Any(p => p.Ideology == Ideology.Green) && (_map.GlobalCO2 > 2f || NuclearWinter > 0.1f || civ.TechLevel >= 60))
        {
            civ.Parties.Add(new PoliticalParty { Name = $"Green Alliance of {civ.NameRoot}", Ideology = Ideology.Green, Support = 0.1f, FoundedYear = currentYear });
        }
        if (!civ.Parties.Any(p => p.Ideology == Ideology.Technocratic) && civ.TechLevel >= 50)
        {
            civ.Parties.Add(new PoliticalParty { Name = $"Technocratic Movement of {civ.NameRoot}", Ideology = Ideology.Technocratic, Support = 0.08f, FoundedYear = currentYear });
        }

        bool famine = civ.Cities.Any(c => c.Starving);
        float happiness = civ.Cities.Count > 0 ? civ.Cities.Average(c => c.Happiness) : 0.5f;
        float pollution = civ.Cities.Count > 0 ? civ.Cities.Average(c => _map.Cells[c.X, c.Y].CO2) : 0f;

        foreach (var party in civ.Parties)
        {
            float swing = party.Ideology switch
            {
                Ideology.Conservative => civ.Stability * 0.06f + (civ.AtWar ? 0.02f : 0f),
                Ideology.Liberal => civ.Prosperity * 0.08f + Math.Min(civ.TradeRoutes.Count, 5) * 0.01f,
                Ideology.Socialist => (1f - happiness) * 0.15f + (famine ? 0.05f : 0f),
                Ideology.Green => Math.Clamp((pollution - 1f) * 0.05f, 0f, 0.15f) + NuclearWinter * 0.3f + (_map.GlobalCO2 > 3f ? 0.05f : 0f),
                Ideology.Nationalist => civ.Strategy.ThreatLevel * 0.08f + (civ.CitiesLost > 0 && civ.AtWar ? 0.08f : 0f),
                Ideology.Technocratic => civ.TechLevel / 400f + civ.InternetPenetration * 0.08f,
                Ideology.Religious => civ.Cities.Count(c => c.Has(CityBuilding.Temple)) / (float)Math.Max(1, civ.Cities.Count) * 0.06f,
                _ => 0f
            };

            // Voters punish the governing party for hard times
            if (party.Name == civ.RulingParty)
            {
                swing -= 0.04f + civ.WarWeariness * 0.15f + (famine ? 0.1f : 0f) + (1f - civ.Prosperity) * 0.08f;
                if (civ.Prosperity > 0.7f && !civ.AtWar) swing += 0.06f;
            }

            party.Support = Math.Max(0.02f, party.Support + swing + (float)(_random.NextDouble() - 0.5) * 0.08f);
        }
        NormaliseSupport(civ);

        foreach (var party in civ.Parties)
        {
            party.SeatsWon = (int)Math.Round(party.Support * 100);
        }

        var winner = civ.Parties.OrderByDescending(p => p.Support).First();
        bool changeOfGovernment = winner.Name != civ.RulingParty;
        civ.RulingParty = winner.Name;

        // The winning party's leader takes office
        var leader = _divinePowers.GenerateRandomRuler(civ, currentYear);
        leader.Id = _nextRulerId++;
        (leader.Ambition, leader.Brutality) = winner.Ideology switch
        {
            Ideology.Nationalist => (0.6f + (float)_random.NextDouble() * 0.4f, 0.5f + (float)_random.NextDouble() * 0.4f),
            Ideology.Green or Ideology.Liberal or Ideology.Socialist => ((float)_random.NextDouble() * 0.6f, (float)_random.NextDouble() * 0.3f),
            _ => (leader.Ambition, leader.Brutality * 0.7f)
        };
        if (winner.Ideology == Ideology.Technocratic) leader.Wisdom = 0.6f + (float)_random.NextDouble() * 0.4f;
        civ.Government!.CurrentRuler = leader;
        civ.AllRulers.Add(leader);

        civ.Elections.Add(new ElectionResult
        {
            Year = currentYear,
            WinningParty = winner.Name,
            WinningShare = winner.Support,
            LeaderName = leader.Name
        });
        if (civ.Elections.Count > 30) civ.Elections.RemoveAt(0);

        if (changeOfGovernment || civ.Elections.Count <= 1)
        {
            AddChronicle(currentYear, HistoryCategory.Politics,
                $"Elections in the {civ.Name}: the {winner.Name} win with {winner.Support:P0}; {leader.Name} leads the government",
                civ.CenterX, civ.CenterY, civ.Id);
        }
    }

    private static void NormaliseSupport(Civilization civ)
    {
        float total = civ.Parties.Sum(p => p.Support);
        if (total <= 0) return;
        foreach (var party in civ.Parties) party.Support /= total;
    }

    /// <summary>
    /// The governing party slowly steers the nation toward its ideals.
    /// </summary>
    private void ApplyIdeology(Civilization civ)
    {
        var ideology = GetRulingIdeology(civ);
        if (ideology == null) return;

        float LerpToward(float value, float target) => value + (target - value) * 0.05f;

        switch (ideology.Value)
        {
            case Ideology.Green:
                civ.EcoFriendliness = LerpToward(civ.EcoFriendliness, 0.9f);
                civ.EmissionReduction = Math.Max(civ.EmissionReduction, 0.3f);
                civ.Aggression = LerpToward(civ.Aggression, 0.2f);
                break;
            case Ideology.Nationalist:
                civ.Aggression = LerpToward(civ.Aggression, 0.75f);
                break;
            case Ideology.Liberal:
            case Ideology.Socialist:
                civ.Aggression = LerpToward(civ.Aggression, 0.3f);
                break;
            case Ideology.Conservative:
                civ.Aggression = LerpToward(civ.Aggression, 0.45f);
                break;
        }
    }

    #endregion

    #region Dynasties and succession

    private void UpdateRoyalFamily(Civilization civ, int currentYear)
    {
        var ruler = civ.Government?.CurrentRuler;
        if (ruler == null) return;

        // The royal family ages (the monarch ages in UpdateGovernments)
        foreach (var member in civ.AllRulers.Where(r => r.IsAlive && r != ruler && r.DynastyId == ruler.DynastyId).ToList())
        {
            member.Age++;
            float death = member.Age < 5 ? 0.02f : member.Age > 60 ? (member.Age - 60) * 0.05f : 0.008f;
            if (_random.NextDouble() < death)
            {
                member.IsAlive = false;
                member.DeathYear = currentYear;
            }
        }

        // Births
        int children = ruler.ChildrenIds.Count;
        if (ruler.Age >= 18 && ruler.Age <= 50 && children < 6 && _random.NextDouble() < 0.15)
        {
            var child = _divinePowers.GenerateRandomRuler(civ, currentYear);
            child.Id = _nextRulerId++;
            // Royal children often take the name of an ancestor
            var ancestors = civ.AllRulers.Where(r => r.DynastyId == ruler.DynastyId && r.YearTookPower > 0).ToList();
            child.Name = ancestors.Count > 0 && _random.NextDouble() < 0.5
                ? BaseName(ancestors[_random.Next(ancestors.Count)].Name)
                : GeneratePersonalName(civ.Culture, _random);
            child.Age = 0;
            child.ParentId = ruler.Id;
            child.DynastyId = ruler.DynastyId;
            child.YearTookPower = 0;
            ruler.ChildrenIds.Add(child.Id);
            civ.AllRulers.Add(child);
            civ.Dynasties.FirstOrDefault(d => d.Id == ruler.DynastyId)?.MemberIds.Add(child.Id);
        }

        civ.SuccessionLine = BuildSuccessionLine(civ, ruler);
        civ.HeirApparent = civ.SuccessionLine.FirstOrDefault();
    }

    /// <summary>
    /// Primogeniture: the monarch's descendants (eldest line first), then siblings and their
    /// descendants, then uncles and aunts and theirs.
    /// </summary>
    private List<Ruler> BuildSuccessionLine(Civilization civ, Ruler monarch)
    {
        var byId = civ.AllRulers.GroupBy(r => r.Id).ToDictionary(g => g.Key, g => g.First());
        var line = new List<Ruler>();
        var visited = new HashSet<int> { monarch.Id };

        void AddDescendants(Ruler person)
        {
            var kids = person.ChildrenIds
                .Select(id => byId.GetValueOrDefault(id))
                .Where(k => k != null)
                .OrderByDescending(k => k!.Age)
                .ToList();
            foreach (var kid in kids)
            {
                if (!visited.Add(kid!.Id)) continue;
                if (kid.IsAlive) line.Add(kid);
                AddDescendants(kid);
            }
        }

        AddDescendants(monarch);

        var ancestor = monarch.ParentId.HasValue ? byId.GetValueOrDefault(monarch.ParentId.Value) : null;
        for (int generation = 0; generation < 2 && ancestor != null; generation++)
        {
            visited.Add(ancestor.Id);
            AddDescendants(ancestor);
            ancestor = ancestor.ParentId.HasValue ? byId.GetValueOrDefault(ancestor.ParentId.Value) : null;
        }

        return line.Take(12).ToList();
    }

    /// <summary>
    /// Called when a hereditary ruler dies. Returns the successor, or null when the line is extinct.
    /// </summary>
    private Ruler? ResolveSuccession(Civilization civ, Ruler deadRuler, int currentYear)
    {
        var line = BuildSuccessionLine(civ, deadRuler);
        var heir = line.FirstOrDefault(r => r.IsAlive);
        if (heir == null) return null;

        heir.YearTookPower = currentYear;
        heir.Title = civ.Government?.RulerTitle ?? heir.Title;

        // Regnal number: the second, third... monarch of that name
        int sameName = civ.AllRulers.Count(r => r != heir && r.YearTookPower > 0 && BaseName(r.Name) == BaseName(heir.Name));
        if (sameName > 0) heir.Name = $"{BaseName(heir.Name)} {ToRoman(sameName + 1)}";

        if (heir.Age < 16)
        {
            civ.Government!.Stability -= 0.1f;
            AddChronicle(currentYear, HistoryCategory.Politics,
                $"{heir.Name}, aged {heir.Age}, inherits the throne of the {civ.Name}; a regent rules in the child's name",
                civ.CenterX, civ.CenterY, civ.Id);
        }
        else if (civ.Cities.Count >= 3)
        {
            AddChronicle(currentYear, HistoryCategory.Politics,
                $"{deadRuler.Name} dies; {heir.Name} succeeds to the throne of the {civ.Name}",
                civ.CenterX, civ.CenterY, civ.Id);
        }

        return heir;
    }

    /// <summary>
    /// The dynasty has died out. Foreign rulers related by marriage may claim the throne by force.
    /// </summary>
    private void HandleSuccessionCrisis(Civilization civ, Ruler deadRuler, int currentYear)
    {
        var claimant = civ.RoyalMarriages
            .Select(m => m.CivilizationId1 == civ.Id ? m.CivilizationId2 : m.CivilizationId1)
            .Select(id => GetCivilizationById(id))
            .Where(c => c != null && c.Government?.IsHereditary == true && !AreAtWar(c.Id, civ.Id))
            .OrderByDescending(c => c!.MilitaryStrength)
            .FirstOrDefault();

        if (claimant != null && claimant.MilitaryStrength > civ.MilitaryStrength * 0.8f &&
            (claimant.Government?.CurrentRuler?.Ambition ?? 0f) > 0.5f && _random.NextDouble() < 0.6)
        {
            AddChronicle(currentYear, HistoryCategory.War,
                $"The house of {deadRuler.Name} is extinct: the {claimant.Name} claim the throne of the {civ.Name}",
                civ.CenterX, civ.CenterY, claimant.Id);
            DeclareWarBetween(claimant, civ, currentYear, "in a war of succession", callAllies: false);
        }
        else
        {
            AddChronicle(currentYear, HistoryCategory.Politics,
                $"Succession crisis in the {civ.Name}: the royal line of {deadRuler.Name} has died out",
                civ.CenterX, civ.CenterY, civ.Id);
        }
    }

    private static string BaseName(string name) => name.Split(' ')[0];

    private static string ToRoman(int number)
    {
        var numerals = new[] { (10, "X"), (9, "IX"), (5, "V"), (4, "IV"), (1, "I") };
        var result = "";
        foreach (var (value, symbol) in numerals)
        {
            while (number >= value)
            {
                result += symbol;
                number -= value;
            }
        }
        return result;
    }

    #endregion
}
