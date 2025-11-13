namespace SimPlanet;

/// <summary>
/// Manages intelligent civilizations and their development
/// </summary>
public class CivilizationManager
{
    private readonly PlanetMap _map;
    private readonly Random _random;
    private List<Civilization> _civilizations;
    private int _nextCivId = 1;

    private static readonly string[] CivNames = new[]
    {
        "Terrans", "Aquans", "Volcanids", "Glacians", "Foresters",
        "Deserters", "Mountaineers", "Islanders", "Nomads", "Builders"
    };

    public CivilizationManager(PlanetMap map, int seed)
    {
        _map = map;
        _random = new Random(seed + 4000);
        _civilizations = new List<Civilization>();
    }

    public void Update(float deltaTime, int currentYear)
    {
        // Check for new civilization emergence
        CheckForNewCivilizations(currentYear);

        // Update existing civilizations
        foreach (var civ in _civilizations.ToList())
        {
            UpdateCivilization(civ, deltaTime, currentYear);
        }

        // Handle interactions between civilizations
        HandleCivilizationInteractions();
    }

    private void CheckForNewCivilizations(int currentYear)
    {
        // Scan for intelligence-level life that could form civilizations
        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                var cell = _map.Cells[x, y];

                // Intelligence level life can form civilizations
                if (cell.LifeType == LifeForm.Intelligence &&
                    cell.Biomass > 0.6f &&
                    !IsCellInCivilization(x, y) &&
                    _random.NextDouble() < 0.001)
                {
                    CreateCivilization(x, y);
                }
            }
        }
    }

    private void CreateCivilization(int x, int y)
    {
        var cell = _map.Cells[x, y];

        var civ = new Civilization
        {
            Id = _nextCivId++,
            Name = CivNames[_random.Next(CivNames.Length)] + " " + _nextCivId,
            CenterX = x,
            CenterY = y,
            Population = 1000 + _random.Next(5000),
            TechLevel = 0,
            CivType = CivType.Tribal,
            Aggression = (float)_random.NextDouble(),
            EcoFriendliness = (float)_random.NextDouble(),
            Founded = 0
        };

        // Initial territory
        civ.Territory.Add((x, y));
        ExpandTerritory(civ, 3); // Start with small territory

        _civilizations.Add(civ);

        // Mark cells as civilization
        foreach (var (tx, ty) in civ.Territory)
        {
            _map.Cells[tx, ty].LifeType = LifeForm.Civilization;
        }
    }

    private void UpdateCivilization(Civilization civ, float deltaTime, int currentYear)
    {
        // Population growth
        float growthRate = 1.0f + civ.EcoFriendliness * 0.5f;
        civ.Population += (int)(civ.Population * 0.01f * growthRate * deltaTime);

        // Technology advancement
        if (_random.NextDouble() < 0.01 * deltaTime)
        {
            civ.TechLevel++;

            // Advance civilization type
            if (civ.TechLevel > 10 && civ.CivType == CivType.Tribal)
            {
                civ.CivType = CivType.Agricultural;
            }
            else if (civ.TechLevel > 30 && civ.CivType == CivType.Agricultural)
            {
                civ.CivType = CivType.Industrial;
            }
            else if (civ.TechLevel > 60 && civ.CivType == CivType.Industrial)
            {
                civ.CivType = CivType.Scientific;
            }
            else if (civ.TechLevel > 100 && civ.CivType == CivType.Scientific)
            {
                civ.CivType = CivType.Spacefaring;
            }
        }

        // Territorial expansion
        if (civ.Population > civ.Territory.Count * 1000 && _random.NextDouble() < 0.05)
        {
            ExpandTerritory(civ, 1);
        }

        // Environmental impact
        ApplyEnvironmentalImpact(civ, deltaTime);

        // Check for collapse conditions
        CheckCivilizationCollapse(civ);
    }

    private void ExpandTerritory(Civilization civ, int cells)
    {
        for (int i = 0; i < cells; i++)
        {
            // Find edge cells
            var edgeCells = civ.Territory
                .SelectMany(pos => _map.GetNeighbors(pos.x, pos.y))
                .Where(n => !civ.Territory.Contains((n.x, n.y)) &&
                           !IsCellInCivilization(n.x, n.y) &&
                           n.cell.IsLand &&
                           n.cell.Temperature > -10 &&
                           n.cell.Temperature < 40)
                .Select(n => (n.x, n.y))
                .Distinct()
                .ToList();

            if (edgeCells.Count == 0) break;

            var newCell = edgeCells[_random.Next(edgeCells.Count)];
            civ.Territory.Add(newCell);
            _map.Cells[newCell.Item1, newCell.Item2].LifeType = LifeForm.Civilization;
        }
    }

    private void ApplyEnvironmentalImpact(Civilization civ, float deltaTime)
    {
        foreach (var (x, y) in civ.Territory)
        {
            var cell = _map.Cells[x, y];

            // Pollution and CO2 emissions
            float pollution = civ.CivType switch
            {
                CivType.Tribal => 0.01f,
                CivType.Agricultural => 0.05f,
                CivType.Industrial => 0.5f,
                CivType.Scientific => 0.2f,
                CivType.Spacefaring => 0.1f,
                _ => 0
            };

            // Eco-friendly civilizations pollute less
            pollution *= (1.0f - civ.EcoFriendliness * 0.5f);

            cell.CO2 += pollution * deltaTime;

            // Deforestation (except eco-friendly civs)
            if (cell.IsForest && civ.EcoFriendliness < 0.5f && _random.NextDouble() < 0.001)
            {
                cell.Biomass *= 0.5f; // Cut down forests
                cell.Rainfall -= 0.1f; // Affects local climate
            }

            // Advanced civs can terraform
            if (civ.CivType == CivType.Scientific || civ.CivType == CivType.Spacefaring)
            {
                if (civ.EcoFriendliness > 0.7f)
                {
                    // Restore ecosystems
                    if (cell.Biomass < 0.5f)
                    {
                        cell.Biomass += 0.01f * deltaTime;
                    }

                    // Carbon capture
                    if (cell.CO2 > 1.0f)
                    {
                        cell.CO2 -= 0.1f * deltaTime;
                    }
                }
            }
        }
    }

    private void CheckCivilizationCollapse(Civilization civ)
    {
        // Environmental collapse
        float avgCO2 = civ.Territory.Average(pos => _map.Cells[pos.x, pos.y].CO2);
        float avgTemp = civ.Territory.Average(pos => _map.Cells[pos.x, pos.y].Temperature);

        if (avgCO2 > 10 || avgTemp > 45 || avgTemp < -15)
        {
            // Civilization decline
            civ.Population = (int)(civ.Population * 0.9f);

            if (civ.Population < 100)
            {
                CollapseCivilization(civ);
            }
        }

        // Check if all territory lost
        if (civ.Territory.Count == 0)
        {
            CollapseCivilization(civ);
        }
    }

    private void CollapseCivilization(Civilization civ)
    {
        // Revert cells to pre-civilization state
        foreach (var (x, y) in civ.Territory)
        {
            var cell = _map.Cells[x, y];
            if (cell.LifeType == LifeForm.Civilization)
            {
                cell.LifeType = LifeForm.Intelligence;
                cell.Biomass *= 0.5f;
            }
        }

        _civilizations.Remove(civ);
    }

    private void HandleCivilizationInteractions()
    {
        // Check for border conflicts or cooperation
        for (int i = 0; i < _civilizations.Count; i++)
        {
            for (int j = i + 1; j < _civilizations.Count; j++)
            {
                var civ1 = _civilizations[i];
                var civ2 = _civilizations[j];

                // Check if civilizations are neighbors
                bool areNeighbors = civ1.Territory.Any(pos1 =>
                    civ2.Territory.Any(pos2 =>
                        Math.Abs(pos1.x - pos2.x) <= 1 &&
                        Math.Abs(pos1.y - pos2.y) <= 1));

                if (areNeighbors)
                {
                    // War or cooperation based on aggression
                    if (civ1.Aggression > 0.7f || civ2.Aggression > 0.7f)
                    {
                        // Conflict - stronger civ takes territory
                        if (civ1.TechLevel > civ2.TechLevel + 10)
                        {
                            ConquerTerritory(civ1, civ2);
                        }
                        else if (civ2.TechLevel > civ1.TechLevel + 10)
                        {
                            ConquerTerritory(civ2, civ1);
                        }
                    }
                    else if (civ1.EcoFriendliness > 0.6f && civ2.EcoFriendliness > 0.6f)
                    {
                        // Peaceful cooperation - tech sharing
                        int avgTech = (civ1.TechLevel + civ2.TechLevel) / 2;
                        civ1.TechLevel = (civ1.TechLevel + avgTech) / 2;
                        civ2.TechLevel = (civ2.TechLevel + avgTech) / 2;
                    }
                }
            }
        }
    }

    private void ConquerTerritory(Civilization attacker, Civilization defender)
    {
        // Take some border cells
        var borderCells = defender.Territory
            .Where(pos => attacker.Territory.Any(aPos =>
                Math.Abs(pos.x - aPos.x) <= 1 &&
                Math.Abs(pos.y - aPos.y) <= 1))
            .Take(3)
            .ToList();

        foreach (var cell in borderCells)
        {
            defender.Territory.Remove(cell);
            attacker.Territory.Add(cell);
        }

        defender.Population -= 1000;
    }

    private bool IsCellInCivilization(int x, int y)
    {
        return _civilizations.Any(civ => civ.Territory.Contains((x, y)));
    }

    public List<Civilization> GetAllCivilizations() => _civilizations;

    public void LoadCivilizations(List<CivilizationData> civData)
    {
        _civilizations.Clear();
        foreach (var data in civData)
        {
            var civ = new Civilization
            {
                Id = data.Id,
                Name = data.Name,
                CenterX = data.CenterX,
                CenterY = data.CenterY,
                Population = data.Population,
                TechLevel = data.TechLevel,
                CivType = data.CivilizationType,
                Aggression = data.Aggression,
                EcoFriendliness = data.EcoFriendliness
            };
            civ.Territory.AddRange(data.Territory);
            _civilizations.Add(civ);
        }

        _nextCivId = _civilizations.Any() ? _civilizations.Max(c => c.Id) + 1 : 1;
    }
}

public class Civilization
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int CenterX { get; set; }
    public int CenterY { get; set; }
    public HashSet<(int x, int y)> Territory { get; set; } = new();
    public int Population { get; set; }
    public int TechLevel { get; set; }
    public CivType CivType { get; set; }
    public float Aggression { get; set; } // 0-1
    public float EcoFriendliness { get; set; } // 0-1
    public int Founded { get; set; }
}
