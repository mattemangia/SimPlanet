namespace SimPlanet;

/// <summary>
/// Settlement economy: villages that grow into towns, cities and metropolises.
/// Each settlement works the land around it (hunting, farming, fishing, forestry,
/// quarrying, mining), feeds the civilization's granaries, grows or starves,
/// constructs buildings, trades and sends settlers to found new villages.
/// The land responds: hunting thins wildlife, farming clears forests and tires the soil.
/// </summary>
public partial class CivilizationManager
{
    private int[,]? _owner;                                   // Civilization id owning each cell (0 = none)
    private float[,]? _soilFatigue;                           // 0-1, soil exhaustion from farming
    private readonly Dictionary<(int, int), int> _borders = new(); // (civA, civB) -> shared border length
    private int _nextCityId = 1;
    private int _lastYearProcessed = int.MinValue;

    // Culture groups give each people its own naming style
    private static readonly string[][] CultureSyllables =
    {
        new[] { "ka", "ra", "tan", "mor", "eth", "ul", "zar", "ok", "ven", "dra" },
        new[] { "al", "ber", "ia", "lo", "mar", "sen", "ti", "va", "gor", "ne" },
        new[] { "xi", "lan", "shu", "ming", "tai", "hou", "jin", "pao", "wen", "yu" },
        new[] { "ath", "el", "is", "or", "phi", "the", "ni", "kos", "ly", "da" },
        new[] { "qua", "tl", "hu", "ix", "ca", "zo", "pec", "mi", "te", "co" },
        new[] { "bjor", "ha", "sk", "ul", "ing", "fro", "gar", "vik", "dal", "ed" },
        new[] { "an", "ku", "ma", "si", "wa", "ndi", "zu", "lo", "ba", "te" },
        new[] { "sa", "ha", "mir", "ra", "ka", "bad", "zan", "qa", "ir", "un" }
    };

    private static readonly string[] PolityTitles =
    {
        "Tribes", "Clans", "Confederacy", "Kingdom", "Realm", "League", "Union", "Dominion"
    };

    #region Ownership & helpers

    /// <summary>
    /// Rebuild the cell -> civilization lookup and the shared border table.
    /// </summary>
    private void RebuildOwnerMap()
    {
        if (_owner == null || _owner.GetLength(0) != _map.Width || _owner.GetLength(1) != _map.Height)
        {
            _owner = new int[_map.Width, _map.Height];
        }
        else
        {
            Array.Clear(_owner);
        }

        foreach (var civ in _civilizations)
        {
            foreach (var (x, y) in civ.Territory)
            {
                _owner[x, y] = civ.Id;
            }
        }

        _borders.Clear();
        for (int x = 0; x < _map.Width; x++)
        {
            int nx = (x + 1) % _map.Width;
            for (int y = 0; y < _map.Height; y++)
            {
                int a = _owner[x, y];
                if (a == 0) continue;

                int right = _owner[nx, y];
                if (right != 0 && right != a) AddBorder(a, right);

                if (y + 1 < _map.Height)
                {
                    int down = _owner[x, y + 1];
                    if (down != 0 && down != a) AddBorder(a, down);
                }
            }
        }
    }

    private void AddBorder(int a, int b)
    {
        var key = a < b ? (a, b) : (b, a);
        _borders[key] = _borders.GetValueOrDefault(key) + 1;
    }

    private int GetBorderLength(int a, int b)
    {
        var key = a < b ? (a, b) : (b, a);
        return _borders.GetValueOrDefault(key);
    }

    private int OwnerAt(int x, int y)
    {
        if (_owner == null) return IsCellInCivilization(x, y) ? -1 : 0;
        return _owner[x, y];
    }

    private void SetOwner(Civilization? civ, int x, int y)
    {
        int previous = OwnerAt(x, y);
        if (previous > 0 && (civ == null || previous != civ.Id))
        {
            GetCivilizationById(previous)?.Territory.Remove((x, y));
        }

        if (civ != null)
        {
            civ.Territory.Add((x, y));
            _map.Cells[x, y].LifeType = LifeForm.Civilization;
        }

        if (_owner != null)
        {
            _owner[x, y] = civ?.Id ?? 0;
        }
    }

    private Civilization? GetCivilizationById(int id)
    {
        for (int i = 0; i < _civilizations.Count; i++)
        {
            if (_civilizations[i].Id == id) return _civilizations[i];
        }
        return null;
    }

    /// <summary>
    /// Distance on a map that wraps horizontally.
    /// </summary>
    private float WrappedDistance(int x1, int y1, int x2, int y2)
    {
        int dx = Math.Abs(x1 - x2);
        if (dx > _map.Width / 2) dx = _map.Width - dx;
        int dy = y1 - y2;
        return MathF.Sqrt(dx * dx + dy * dy);
    }

    private int WrapX(int x) => ((x % _map.Width) + _map.Width) % _map.Width;

    private static int GetSettlementRadius(City city) => city.Type switch
    {
        CityType.Village => 2,
        CityType.Town => 3,
        CityType.City => 4,
        _ => 5
    };

    private void RecalculatePopulation(Civilization civ)
    {
        civ.Population = civ.Cities.Sum(c => c.Population);
        civ.LastKnownPopulation = civ.Population;
    }

    /// <summary>
    /// Other systems (disease, divine powers, disasters) change civ.Population directly.
    /// Spread those changes over the settlements so they stay consistent.
    /// </summary>
    private void SyncPopulationFromExternalChanges(Civilization civ)
    {
        if (civ.Cities.Count == 0)
        {
            civ.LastKnownPopulation = civ.Population;
            return;
        }

        int citySum = civ.Cities.Sum(c => c.Population);
        if (civ.LastKnownPopulation > 0 && civ.Population != civ.LastKnownPopulation && citySum > 0)
        {
            float ratio = Math.Max(0f, civ.Population / (float)civ.LastKnownPopulation);
            foreach (var city in civ.Cities)
            {
                city.Population = Math.Max(0, (int)(city.Population * ratio));
            }
        }

        RecalculatePopulation(civ);
    }

    #endregion

    #region Names

    private string GenerateWord(int culture, int minSyllables, int maxSyllables)
    {
        var syllables = CultureSyllables[culture % CultureSyllables.Length];
        int count = _random.Next(minSyllables, maxSyllables + 1);
        var word = string.Concat(Enumerable.Range(0, count).Select(_ => syllables[_random.Next(syllables.Length)]));
        return char.ToUpper(word[0]) + word.Substring(1);
    }

    private string GenerateCivilizationName(int culture, out string root)
    {
        root = GenerateWord(culture, 2, 3);
        return _random.NextDouble() < 0.5 ? root + "ans" : root + " " + PolityTitles[_random.Next(3)];
    }

    /// <summary>
    /// A people's formal name follows its form of government.
    /// </summary>
    private static void UpdatePolityName(Civilization civ)
    {
        if (string.IsNullOrEmpty(civ.NameRoot) || civ.Government == null) return;
        if (string.IsNullOrEmpty(civ.TribalName)) civ.TribalName = civ.Name;

        civ.Name = civ.Government.Type switch
        {
            GovernmentType.Tribal => civ.TribalName,
            GovernmentType.Monarchy => $"Kingdom of {civ.NameRoot}",
            GovernmentType.Dynasty => $"{civ.NameRoot} Empire",
            GovernmentType.Theocracy => $"Holy Realm of {civ.NameRoot}",
            GovernmentType.Republic => $"{civ.NameRoot} Republic",
            GovernmentType.Democracy => $"{civ.NameRoot} Commonwealth",
            GovernmentType.Oligarchy => $"{civ.NameRoot} League",
            GovernmentType.Dictatorship => $"{civ.NameRoot} Regime",
            GovernmentType.Federation => $"{civ.NameRoot} Federation",
            _ => civ.TribalName
        };
    }

    private string GenerateSettlementName(Civilization civ)
    {
        for (int attempt = 0; attempt < 10; attempt++)
        {
            string name = GenerateWord(civ.Culture, 2, 3);
            if (!_civilizations.Any(c => c.Cities.Any(city => city.Name == name)))
                return name;
        }
        return GenerateWord(civ.Culture, 3, 4);
    }

    #endregion

    #region Land yields

    /// <summary>
    /// Food, wood, stone and metal a worked cell yields per year for a civilization.
    /// </summary>
    private (float food, float wood, float stone, float metal, bool farmed) GetCellYield(Civilization civ, City? city, int x, int y)
    {
        var cell = _map.Cells[x, y];
        if (!cell.IsLand)
        {
            return (0, 0, 0, 0, false);
        }

        var biome = cell.GetBiomeData().CurrentBiome;
        var geo = cell.GetGeology();

        // Hunting & gathering depends on how much wildlife and vegetation the land holds
        float gatherBase = biome switch
        {
            Biome.TropicalRainforest or Biome.TemperateForest or Biome.BorealForest => 0.9f,
            Biome.Grassland or Biome.Savanna => 1.1f,
            Biome.Shrubland or Biome.Wetland => 0.7f,
            Biome.Tundra or Biome.AlpineTundra => 0.35f,
            Biome.Desert => 0.1f,
            _ => 0.2f
        };
        // Game animals roam the wild land around the fields
        float wildlife = 0f;
        float marineLife = 0f;
        bool coastal = cell.IsCoastal;
        foreach (var (_, _, neighbor) in _map.GetNeighbors(x, y))
        {
            if (neighbor.IsWater)
            {
                coastal = true;
                marineLife += neighbor.Biomass;
            }
            else if (IsGameAnimal(neighbor.LifeType))
            {
                wildlife += neighbor.Biomass;
            }
        }
        float gathering = gatherBase * (Math.Clamp(cell.Biomass, 0f, 1.2f) * 0.7f + Math.Min(wildlife, 1.5f) * 0.4f);

        // Farming needs knowledge, water and a suitable temperature
        float farming = 0f;
        float farmMultiplier = GetFarmingMultiplier(civ);
        if (farmMultiplier > 0 && cell.Temperature > 2 && cell.Temperature < 36 && biome != Biome.Glacier && biome != Biome.Mountain)
        {
            bool irrigated = civ.TechLevel >= 6 && (geo.RiverId > 0 || geo.WaterFlow > 0.3f);
            float water = Math.Clamp(cell.Rainfall / 0.5f, 0f, 1.3f);
            if (irrigated) water = Math.Max(water, 1.0f);

            if (water > 0.25f)
            {
                float warmth = Math.Clamp(1f - Math.Abs(cell.Temperature - 20f) / 22f, 0.15f, 1f);
                float fatigue = _soilFatigue?[x, y] ?? 0f;
                farming = farmMultiplier * water * warmth * (1f - fatigue * 0.6f);
                if (cell.Elevation > 0.55f) farming *= 0.5f; // Terraces are hard work
            }
        }

        // Fishing on coasts
        float fishing = 0f;
        if (coastal)
        {
            // Fish stocks depend on the life in nearby waters
            fishing = 0.3f + Math.Min(marineLife, 2f) * 0.3f;
            if (city != null && city.Has(CityBuilding.Harbor)) fishing += 1.0f;
            if (civ.CivType >= CivType.Industrial) fishing *= 1.8f;
        }

        bool farmed = farming > gathering;
        float food = Math.Max(farming, gathering) + fishing;

        float wood = biome is Biome.TropicalRainforest or Biome.TemperateForest or Biome.BorealForest
            ? 0.5f * Math.Clamp(cell.Biomass, 0f, 1f)
            : 0.05f;

        float stone = biome == Biome.Mountain || cell.Elevation > 0.5f ? 0.5f : (cell.Elevation > 0.3f ? 0.15f : 0.03f);

        float metal = 0f;
        if (civ.TechLevel >= 8) // Bronze working
        {
            metal = Math.Min(0.4f, (geo.CrystallineRock + geo.VolcanicRock) * 0.05f);
            foreach (var deposit in cell.GetResources())
            {
                if (deposit.Type is ResourceType.Iron or ResourceType.Copper && deposit.Amount > 0)
                {
                    metal += 0.6f;
                }
            }
        }

        return (food, wood, stone, metal, farmed);
    }

    private static float GetFarmingMultiplier(Civilization civ)
    {
        if (civ.TechLevel < 3) return 0f; // Pure hunter-gatherers
        return civ.CivType switch
        {
            CivType.Tribal => 1.3f,
            CivType.Agricultural => 2.2f,
            CivType.Industrial => 4.0f,
            CivType.Scientific => 6.5f,
            CivType.Spacefaring => 9.0f,
            _ => 1f
        };
    }

    /// <summary>
    /// How attractive a location is for a new settlement.
    /// </summary>
    private float EvaluateSettlementSite(Civilization civ, int x, int y)
    {
        var cell = _map.Cells[x, y];
        if (!cell.IsLand) return float.MinValue;

        float score = 0f;
        for (int dx = -2; dx <= 2; dx++)
        {
            for (int dy = -2; dy <= 2; dy++)
            {
                int nx = WrapX(x + dx);
                int ny = y + dy;
                if (ny < 0 || ny >= _map.Height) continue;
                int owner = OwnerAt(nx, ny);
                if (owner != 0 && owner != civ.Id) continue;
                var yield = GetCellYield(civ, null, nx, ny);
                score += yield.food + yield.wood * 0.3f + yield.stone * 0.2f + yield.metal * 0.5f;
            }
        }

        var geo = cell.GetGeology();
        if (geo.RiverId > 0 || geo.WaterFlow > 0.3f) score += 3f;
        if (_map.GetNeighbors(x, y).Any(n => n.cell.IsWater)) score += 2f;
        if (cell.Elevation > 0.25f && cell.Elevation < 0.6f) score += 1f; // Defensible hills

        return score;
    }

    #endregion

    #region Settlement founding

    /// <summary>
    /// Create a settlement at a location and claim the land around it.
    /// </summary>
    private City FoundSettlement(Civilization civ, int x, int y, int population, int currentYear)
    {
        var city = CreateCity(civ, x, y);
        city.Population = Math.Max(population, 50);
        city.Founded = currentYear;
        city.Type = GetCityType(city.Population);
        city.Happiness = 0.65f;
        civ.SettlementsFounded++;

        SetOwner(civ, x, y);
        ClaimLandAround(civ, city, 9);
        return city;
    }

    private void TryFoundNewSettlement(Civilization civ, int currentYear)
    {
        if (civ.Cities.Count == 0) return;

        // Early peoples can only hold together a handful of villages
        int maxSettlements = 3 + civ.TechLevel / 2 + (civ.Government?.Type == GovernmentType.Tribal ? 0 : 3);
        if (civ.Cities.Count >= maxSettlements) return;
        if (civ.Food < civ.FoodConsumption * 0.3f) return;

        // Find a crowded settlement willing to send settlers
        var sources = civ.Cities
            .Where(c => c.Population >= 300 && c.Population > c.Capacity * 0.75f && !c.UnderSiege)
            .ToList();
        if (sources.Count == 0) return;
        if (_random.NextDouble() > 0.6) return;

        var source = sources[_random.Next(sources.Count)];
        int reach = 8 + (civ.HasLandTransport ? 4 : 0) + Math.Min(civ.TechLevel / 5, 10);
        float minTemp = civ.TechLevel >= 40 ? -25 : -8;
        float maxTemp = civ.TechLevel >= 40 ? 45 : 38;

        (int x, int y) best = (-1, -1);
        float bestScore = 4f;

        for (int attempt = 0; attempt < 60; attempt++)
        {
            double angle = _random.NextDouble() * Math.PI * 2;
            int dist = 4 + _random.Next(Math.Max(1, reach - 3));
            int x = WrapX(source.X + (int)Math.Round(Math.Cos(angle) * dist));
            int y = source.Y + (int)Math.Round(Math.Sin(angle) * dist);
            if (y < 1 || y >= _map.Height - 1) continue;

            var cell = _map.Cells[x, y];
            if (!cell.IsLand || cell.IsIce) continue;
            if (cell.Temperature < minTemp || cell.Temperature > maxTemp) continue;

            int owner = OwnerAt(x, y);
            if (owner != 0 && owner != civ.Id) continue;
            if (_civilizations.Any(c => c.Cities.Any(city => WrappedDistance(city.X, city.Y, x, y) < 4.5f))) continue;
            if (!civ.HasSeaTransport && CountWaterOnLine(source.X, source.Y, x, y) > 1) continue;

            float score = EvaluateSettlementSite(civ, x, y) - dist * 0.15f;
            if (score > bestScore)
            {
                bestScore = score;
                best = (x, y);
            }
        }

        if (best.x < 0) return;

        int settlers = Math.Min((int)(source.Population * 0.3f), 150 + _random.Next(250));
        source.Population -= settlers;
        var village = FoundSettlement(civ, best.x, best.y, settlers, currentYear);
        RecalculatePopulation(civ);

        if (civ.Cities.Count <= 12 || _random.NextDouble() < 0.3)
        {
            AddChronicle(currentYear, HistoryCategory.Founding,
                $"Settlers from {source.Name} found the village of {village.Name} ({civ.Name})", best.x, best.y, civ.Id);
        }
    }

    private int CountWaterOnLine(int x1, int y1, int x2, int y2)
    {
        int dx = x2 - x1;
        if (Math.Abs(dx) > _map.Width / 2) dx -= Math.Sign(dx) * _map.Width;
        int dy = y2 - y1;
        int steps = Math.Max(Math.Abs(dx), Math.Abs(dy));
        int water = 0;
        for (int i = 1; i < steps; i++)
        {
            int x = WrapX(x1 + (int)Math.Round(dx * i / (float)steps));
            int y = Math.Clamp(y1 + (int)Math.Round(dy * i / (float)steps), 0, _map.Height - 1);
            if (_map.Cells[x, y].IsWater) water++;
        }
        return water;
    }

    private void AbandonSettlement(Civilization civ, City city, int currentYear)
    {
        civ.Cities.Remove(city);
        _armies.RemoveAll(a => a.HomeCityId == city.Id && a.State == ArmyState.Mustering);

        if (city.IsCapital && civ.Cities.Count > 0)
        {
            var newCapital = civ.Cities.OrderByDescending(c => c.Population).First();
            newCapital.IsCapital = true;
            civ.CenterX = newCapital.X;
            civ.CenterY = newCapital.Y;
        }

        if (city.Type >= CityType.Town)
        {
            AddChronicle(currentYear, HistoryCategory.Famine, $"{city.Name} has been abandoned", city.X, city.Y, civ.Id);
        }

        RecalculatePopulation(civ);
    }

    /// <summary>
    /// Settlements extend their influence over nearby unclaimed land as they grow.
    /// </summary>
    private void ClaimLandAround(Civilization civ, City city, int maxCells)
    {
        int radius = GetSettlementRadius(city);
        var candidates = new List<(int x, int y, float d)>();

        for (int dx = -radius; dx <= radius; dx++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                int x = WrapX(city.X + dx);
                int y = city.Y + dy;
                if (y < 0 || y >= _map.Height) continue;
                float d = MathF.Sqrt(dx * dx + dy * dy);
                if (d > radius + 0.5f) continue;
                if (!_map.Cells[x, y].IsLand) continue;
                if (OwnerAt(x, y) != 0) continue;
                candidates.Add((x, y, d));
            }
        }

        foreach (var (x, y, _) in candidates.OrderBy(c => c.d).Take(maxCells))
        {
            SetOwner(civ, x, y);
        }
    }

    private static CityType GetCityType(int population) => population switch
    {
        < 1000 => CityType.Village,
        < 5000 => CityType.Town,
        < 50000 => CityType.City,
        _ => CityType.Metropolis
    };

    #endregion

    #region Yearly settlement economy

    private void UpdateSettlements(Civilization civ, int currentYear)
    {
        if (_soilFatigue == null || _soilFatigue.GetLength(0) != _map.Width || _soilFatigue.GetLength(1) != _map.Height)
        {
            _soilFatigue = new float[_map.Width, _map.Height];
        }

        float totalFood = 0, totalWood = 0, totalStone = 0, totalMetal = 0, totalGold = 0;
        float workshopBonus = civ.ProductionBonus;
        int soldiers = GetSoldierCount(civ);
        var workedThisYear = new HashSet<(int, int)>();

        foreach (var city in civ.Cities)
        {
            city.Type = GetCityType(city.Population);
            int radius = GetSettlementRadius(city);

            // Gather the land this settlement can work
            var yields = new List<(int x, int y, float food, float wood, float stone, float metal, bool farmed)>();
            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dy = -radius; dy <= radius; dy++)
                {
                    if (dx * dx + dy * dy > (radius + 0.5f) * (radius + 0.5f)) continue;
                    int x = WrapX(city.X + dx);
                    int y = city.Y + dy;
                    if (y < 0 || y >= _map.Height) continue;
                    if (OwnerAt(x, y) != civ.Id) continue;
                    if (workedThisYear.Contains((x, y))) continue; // Shared fields are worked once

                    var yld = GetCellYield(civ, city, x, y);
                    if (yld.food + yld.wood + yld.stone + yld.metal <= 0.01f) continue;
                    yields.Add((x, y, yld.food, yld.wood, yld.stone, yld.metal, yld.farmed));
                }
            }

            // Labour limits how much land can be worked: best fields first
            int workers = 2 + city.Population / 120;
            float food = 0, wood = 0, stone = 0, metal = 0;
            int worked = 0;
            foreach (var y in yields.OrderByDescending(v => v.food))
            {
                if (worked >= workers) break;
                worked++;
                workedThisYear.Add((y.x, y.y));
                food += y.food;
                wood += y.wood;
                stone += y.stone;
                metal += y.metal;
                ApplyLandUse(civ, y.x, y.y, y.farmed);
            }
            city.WorkedCells = worked;

            if (city.Has(CityBuilding.Granary)) food *= 1.15f;
            if (city.Has(CityBuilding.Workshop))
            {
                stone *= 1.5f;
                metal *= 1.5f;
            }

            // The city centre itself always provides a little
            food += 1.0f;

            city.FoodProduction = food;
            city.IndustrialProduction = (stone + metal) * workshopBonus;

            // Carrying capacity from local harvests (trade imports are added once the realm's surplus is known)
            city.Capacity = (int)(food * 95f);

            // Gold: taxes, markets and trade
            float gold = city.Population / 1000f * 0.6f;
            if (city.Has(CityBuilding.Market)) gold *= 2f;
            if (city.Has(CityBuilding.Harbor)) gold += 1f;
            if (city.Coastal || city.NearRiver) gold *= 1.2f;
            city.GoldProduction = gold;
            city.TradeProduction = gold;

            // Knowledge
            city.ScienceProduction = city.Population / 4000f
                + (city.Has(CityBuilding.University) ? 3f : 0f)
                + (city.Has(CityBuilding.Temple) && civ.TechLevel < 25 ? 0.3f : 0f)
                + (city.Has(CityBuilding.Market) ? 0.3f : 0f)
                + (city.IsCapital ? 0.5f : 0f);

            totalFood += food;
            totalWood += wood;
            totalStone += stone;
            totalMetal += metal * workshopBonus;
            totalGold += gold;

            // Slowly extend influence over surrounding land
            ClaimLandAround(civ, city, 2);
        }

        // Trade routes with friendly neighbours bring gold
        float tradeGold = civ.TradeRoutes.Count * (1.5f + civ.Cities.Count(c => c.Has(CityBuilding.Market)) * 0.5f);
        totalGold += tradeGold;
        civ.TradeIncome = tradeGold;

        // Fallow land recovers
        RecoverSoil(civ, workedThisYear);

        // Civilization-wide granaries
        civ.FoodProduction = totalFood;
        civ.WoodProduction = totalWood;
        civ.StoneProduction = totalStone;
        civ.MetalProduction = totalMetal;
        civ.FoodConsumption = (civ.Population + soldiers * 1.5f) / 100f;

        civ.Food += totalFood - civ.FoodConsumption;
        civ.Wood += totalWood;
        civ.Stone += totalStone;
        civ.Metal += totalMetal;

        // Armies cost money
        float upkeep = soldiers * 0.004f;
        civ.GoldIncome = totalGold - upkeep;
        civ.Gold += civ.GoldIncome;

        int granaries = civ.Cities.Count(c => c.Has(CityBuilding.Granary));
        float foodCap = 50f + civ.FoodConsumption * (1.5f + granaries * 0.3f);
        float materialCap = 300f + 150f * civ.Cities.Count;
        civ.Food = Math.Min(civ.Food, foodCap);
        civ.Wood = Math.Clamp(civ.Wood, 0, materialCap);
        civ.Stone = Math.Clamp(civ.Stone, 0, materialCap);
        civ.Metal = Math.Clamp(civ.Metal, 0, materialCap);
        civ.Gold = Math.Clamp(civ.Gold, -200f, 5000f + 500f * civ.Cities.Count);

        // Markets, harbours and trade routes let settlements import the realm's surplus harvest
        float surplus = totalFood - civ.FoodConsumption;
        if (surplus > 0 && civ.Cities.Count > 0)
        {
            float TradeWeight(City c) => c.Population * (1f
                + (c.Has(CityBuilding.Market) ? 1f : 0f)
                + (c.Has(CityBuilding.Harbor) ? 0.5f : 0f)
                + (c.IsCapital ? 0.5f : 0f));
            float totalWeight = civ.Cities.Sum(TradeWeight);
            if (totalWeight > 0)
            {
                foreach (var city in civ.Cities)
                {
                    city.Capacity += (int)(surplus * 90f * TradeWeight(city) / totalWeight);
                }
            }
        }

        // Growth, famine and happiness
        bool famine = civ.Food < 0;
        float deficitRatio = famine ? Math.Min(1f, -civ.Food / Math.Max(1f, civ.FoodConsumption)) : 0f;
        if (famine) civ.Food = 0;

        bool wasStarving = civ.Cities.Any(c => c.Starving);
        float warPenalty = civ.AtWar ? 0.1f : 0f;
        var migrants = 0;

        foreach (var city in civ.Cities)
        {
            float cap = Math.Max(150f, city.Capacity);

            if (famine)
            {
                int dead = (int)(city.Population * deficitRatio * 0.25f);
                city.Population -= dead;
                city.Starving = true;
            }
            else
            {
                city.Starving = false;
                float rate = 0.03f * (city.Has(CityBuilding.Granary) ? 1.2f : 1f) * (0.6f + city.Happiness * 0.6f);
                if (city.Population < cap)
                {
                    city.Population += (int)Math.Ceiling(city.Population * rate * (1f - city.Population / cap));
                }
                else
                {
                    // Overcrowded: people leave for other settlements
                    int leaving = (int)((city.Population - cap) * 0.15f);
                    city.Population -= leaving;
                    migrants += leaving;
                }
            }

            // Happiness
            float target = 0.55f
                + (city.Has(CityBuilding.Temple) ? 0.15f : 0f)
                + (city.Has(CityBuilding.Market) ? 0.05f : 0f)
                + (city.IsCapital ? 0.1f : 0f)
                + (civ.Government?.CurrentRuler?.Charisma ?? 0.5f) * 0.1f
                - (city.Starving ? 0.35f : 0f)
                - (city.UnderSiege ? 0.25f : 0f)
                - civ.WarWeariness * 0.25f
                - warPenalty
                - (civ.Gold < 0 ? 0.1f : 0f)
                - (city.OriginalCivilizationId != civ.Id ? 0.1f : 0f) // Conquered peoples resent their rulers
                - Math.Clamp((_map.Cells[city.X, city.Y].CO2 - 2f) * 0.03f, 0f, 0.2f); // Smog
            city.Happiness = Math.Clamp(city.Happiness + (target - city.Happiness) * 0.3f, 0f, 1f);

            // Promotions
            var newType = GetCityType(city.Population);
            if (newType > city.LargestTypeReached && newType >= CityType.City &&
                (newType == CityType.Metropolis || civ.Cities.Count(c => c.LargestTypeReached >= CityType.City) < 4))
            {
                AddChronicle(currentYear, HistoryCategory.Growth,
                    $"{city.Name} has grown into a {(newType == CityType.Metropolis ? "metropolis" : "city")} ({civ.Name})", city.X, city.Y, civ.Id);
            }
            city.Type = newType;
            if (newType > city.LargestTypeReached) city.LargestTypeReached = newType;
        }

        // Migrants settle in settlements with room to spare
        if (migrants > 0)
        {
            var destination = civ.Cities.Where(c => c.Population < c.Capacity).OrderBy(c => c.Population / (float)Math.Max(1, c.Capacity)).FirstOrDefault();
            if (destination != null) destination.Population += migrants;
        }

        if (famine && !wasStarving && civ.Population > 500 && currentYear - civ.LastFamineReportYear >= 15)
        {
            civ.LastFamineReportYear = currentYear;
            AddChronicle(currentYear, HistoryCategory.Famine, $"Famine strikes the {civ.Name}", civ.CenterX, civ.CenterY, civ.Id);
        }

        // Settlements' mood feeds civilization stability
        if (civ.Cities.Count > 0)
        {
            float avgHappiness = civ.Cities.Average(c => c.Happiness);
            civ.Stability = Math.Clamp(civ.Stability + (avgHappiness - 0.5f) * 0.05f, 0f, 1f);
        }

        ConstructBuildings(civ, currentYear);
        RecalculatePopulation(civ);

        var capital = civ.Capital;
        if (capital != null)
        {
            capital.IsCapital = true;
            civ.CenterX = capital.X;
            civ.CenterY = capital.Y;
        }
    }

    /// <summary>
    /// Working the land changes it: forests are cleared for fields, wildlife is hunted,
    /// soils tire and farming releases methane.
    /// </summary>
    private void ApplyLandUse(Civilization civ, int x, int y, bool farmed)
    {
        var cell = _map.Cells[x, y];
        float care = civ.EcoFriendliness;

        if (farmed)
        {
            // Clearing forest for fields
            if (cell.Biomass > 0.45f)
            {
                cell.Biomass = Math.Max(0.4f, cell.Biomass - 0.04f * (1f - care * 0.7f));
            }

            float fatigueRate = 0.012f * (1f - care * 0.5f) * (civ.TechLevel >= 30 ? 0.5f : 1f);
            _soilFatigue![x, y] = Math.Min(1f, _soilFatigue[x, y] + fatigueRate);

            if (civ.CivType >= CivType.Agricultural)
            {
                cell.Methane += 0.0005f; // Livestock and paddies
            }
        }
        else
        {
            // Hunting and gathering thins vegetation and game
            cell.Biomass = Math.Max(0.1f, cell.Biomass - 0.008f * (1f - care * 0.5f));
        }

        // Hunting pressure on wild animals and fishing pressure on nearby waters
        float huntPressure = (farmed ? 0.004f : 0.012f) * (1f - care * 0.6f);
        float fishPressure = 0.002f * (civ.CivType >= CivType.Industrial ? 3f : 1f) * (1f - care * 0.6f);
        foreach (var (nx, ny, neighbor) in _map.GetNeighbors(x, y))
        {
            if (neighbor.IsWater)
            {
                if (neighbor.Biomass > 0.05f)
                {
                    neighbor.Biomass = Math.Max(0.05f, neighbor.Biomass - fishPressure);
                }
            }
            else if (IsGameAnimal(neighbor.LifeType) && OwnerAt(nx, ny) == 0)
            {
                neighbor.Biomass -= huntPressure;
                if (neighbor.Biomass < 0.05f)
                {
                    // Hunted out: the species disappears from this land
                    RecordLocalExtinction(civ, neighbor.LifeType, nx, ny);
                    neighbor.LifeType = LifeForm.PlantLife;
                    neighbor.Biomass = 0.2f;
                }
            }
        }
    }

    private static bool IsGameAnimal(LifeForm life) => life is LifeForm.Mammals or LifeForm.Birds or LifeForm.Reptiles
        or LifeForm.Amphibians or LifeForm.Dinosaurs or LifeForm.ComplexAnimals or LifeForm.SimpleAnimals;

    private void RecordLocalExtinction(Civilization civ, LifeForm species, int x, int y)
    {
        civ.WildlifeHuntedOut++;
        if (civ.WildlifeHuntedOut == 10 || civ.WildlifeHuntedOut == 100 || civ.WildlifeHuntedOut == 500)
        {
            string what = species switch
            {
                LifeForm.Dinosaurs => "the great saurians",
                LifeForm.Mammals => "the herds",
                LifeForm.Birds => "the flocks",
                _ => "the wild game"
            };
            AddChronicle(_lastYearProcessed + 1, HistoryCategory.Disaster,
                $"The {civ.Name} have hunted {what} from {civ.WildlifeHuntedOut} regions", x, y, civ.Id);
        }
    }

    private void RecoverSoil(Civilization civ, HashSet<(int, int)> worked)
    {
        foreach (var (x, y) in civ.Territory)
        {
            if (!worked.Contains((x, y)) && _soilFatigue![x, y] > 0)
            {
                _soilFatigue[x, y] = Math.Max(0f, _soilFatigue[x, y] - 0.03f);
            }
        }
    }

    #endregion

    #region Earthquakes

    private readonly HashSet<(int x, int y, int year, float magnitude)> _processedEarthquakes = new();

    /// <summary>
    /// Strong earthquakes (M6+) damage the settlements close to the epicentre.
    /// </summary>
    private void ApplyEarthquakeDamage(int currentYear)
    {
        _processedEarthquakes.RemoveWhere(q => q.year < currentYear - 2);

        foreach (var quake in EarthquakeSystem.RecentSystemEarthquakes.ToArray())
        {
            if (quake.Year < currentYear - 1 || quake.Magnitude < 6f) continue;
            if (!_processedEarthquakes.Add((quake.X, quake.Y, quake.Year, quake.Magnitude))) continue;

            float reach = quake.Magnitude * 2.5f;
            foreach (var civ in _civilizations)
            {
                int dead = 0;
                City? worstHit = null;
                foreach (var city in civ.Cities)
                {
                    float distance = WrappedDistance(city.X, city.Y, quake.X, quake.Y);
                    if (distance > reach) continue;

                    float shaking = 1f - distance / reach;
                    float rate = (quake.Magnitude - 5.5f) * 0.04f * shaking * (1f - civ.DisasterPreparedness);
                    int cityDead = (int)(city.Population * Math.Clamp(rate, 0f, 0.35f));
                    city.Population -= cityDead;
                    city.Happiness = Math.Max(0f, city.Happiness - 0.1f * shaking);
                    dead += cityDead;
                    if (worstHit == null || cityDead > 0 && shaking > 0.5f) worstHit = city;
                }

                if (dead == 0) continue;

                RecalculatePopulation(civ);
                civ.PopulationLostToDisasters += dead;
                civ.DisastersSurvived++;
                civ.DisasterPreparedness = Math.Min(civ.DisasterPreparedness + 0.03f, 0.9f);
                if (civ.Government != null)
                {
                    civ.Government.Stability -= Math.Min(0.1f, dead / (float)Math.Max(1, civ.Population) * 2f);
                }

                if (dead >= 200 && worstHit != null)
                {
                    AddChronicle(currentYear, HistoryCategory.Disaster,
                        $"A magnitude {quake.Magnitude:F1} earthquake strikes near {worstHit.Name} ({civ.Name}): {dead:N0} dead",
                        quake.X, quake.Y, civ.Id);
                }
            }
        }
    }

    #endregion

    #region Buildings

    private static readonly (CityBuilding building, int tech, float wood, float stone, float metal, float gold)[] BuildingCosts =
    {
        (CityBuilding.Granary, 3, 30, 10, 0, 0),
        (CityBuilding.Temple, 5, 10, 40, 0, 10),
        (CityBuilding.Walls, 6, 10, 60, 0, 0),
        (CityBuilding.Barracks, 8, 30, 20, 15, 10),
        (CityBuilding.Market, 10, 30, 30, 0, 20),
        (CityBuilding.Harbor, 15, 60, 20, 0, 20),
        (CityBuilding.Workshop, 20, 20, 40, 30, 30),
        (CityBuilding.University, 30, 20, 80, 10, 80)
    };

    private void ConstructBuildings(Civilization civ, int currentYear)
    {
        bool threatened = civ.AtWar || _borders.Keys.Any(k =>
            (k.Item1 == civ.Id || k.Item2 == civ.Id) &&
            civ.DiplomaticRelations.TryGetValue(k.Item1 == civ.Id ? k.Item2 : k.Item1, out var r) &&
            r.Status <= DiplomaticStatus.Hostile);

        foreach (var city in civ.Cities.OrderByDescending(c => c.Population))
        {
            var choice = ChooseBuilding(civ, city, threatened);
            if (choice == null) continue;

            var cost = choice.Value;
            if (civ.Wood < cost.wood || civ.Stone < cost.stone || civ.Metal < cost.metal || civ.Gold < cost.gold)
                continue;

            civ.Wood -= cost.wood;
            civ.Stone -= cost.stone;
            civ.Metal -= cost.metal;
            civ.Gold -= cost.gold;
            city.Buildings |= cost.building;

            if (cost.building is CityBuilding.University or CityBuilding.Walls && city.Type >= CityType.Town && _random.NextDouble() < 0.3)
            {
                string what = cost.building == CityBuilding.Walls ? "raises stone walls" : "founds a university";
                AddChronicle(currentYear, HistoryCategory.Growth, $"{city.Name} {what} ({civ.Name})", city.X, city.Y, civ.Id);
            }
        }
    }

    private (CityBuilding building, int tech, float wood, float stone, float metal, float gold)? ChooseBuilding(Civilization civ, City city, bool threatened)
    {
        var available = BuildingCosts.Where(b => civ.TechLevel >= b.tech && !city.Has(b.building)).ToList();
        if (available.Count == 0) return null;

        // Villages only build the basics
        if (city.Type == CityType.Village)
        {
            available = available.Where(b => b.building is CityBuilding.Granary or CityBuilding.Temple or CityBuilding.Walls).ToList();
        }
        if (!city.Coastal)
        {
            available = available.Where(b => b.building != CityBuilding.Harbor).ToList();
        }
        if (available.Count == 0) return null;

        // Needs drive priorities
        CityBuilding[] priority;
        if (threatened || civ.Aggression > 0.75f)
            priority = new[] { CityBuilding.Walls, CityBuilding.Barracks, CityBuilding.Granary, CityBuilding.Workshop, CityBuilding.Market, CityBuilding.Temple, CityBuilding.Harbor, CityBuilding.University };
        else if (city.Starving || city.Population > city.Capacity * 0.8f)
            priority = new[] { CityBuilding.Granary, CityBuilding.Harbor, CityBuilding.Market, CityBuilding.Temple, CityBuilding.Workshop, CityBuilding.University, CityBuilding.Walls, CityBuilding.Barracks };
        else if (city.Happiness < 0.45f)
            priority = new[] { CityBuilding.Temple, CityBuilding.Market, CityBuilding.Granary, CityBuilding.University, CityBuilding.Harbor, CityBuilding.Workshop, CityBuilding.Walls, CityBuilding.Barracks };
        else
            priority = new[] { CityBuilding.Granary, CityBuilding.Market, CityBuilding.Temple, CityBuilding.Harbor, CityBuilding.University, CityBuilding.Workshop, CityBuilding.Walls, CityBuilding.Barracks };

        foreach (var building in priority)
        {
            var match = available.FirstOrDefault(b => b.building == building);
            if (match.building != CityBuilding.None) return match;
        }
        return null;
    }

    #endregion
}
