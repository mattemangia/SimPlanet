namespace SimPlanet;

/// <summary>
/// Every settlement has its own character: what it lives on (specialization) and how it
/// is built (style, from its people's culture, homeland and era). Specializations shape
/// what the city produces.
/// </summary>
public partial class CivilizationManager
{
    /// <summary>
    /// Decide what each settlement of a nation lives on and how it is built. Special roles
    /// (university towns, industrial centres) are limited to a share of the nation's cities.
    /// </summary>
    private void AssignCityCharacters(Civilization civ)
    {
        int academicSlots = Math.Max(1, civ.Cities.Count / 6);
        int industrialSlots = Math.Max(1, civ.Cities.Count / 4);
        int fortressSlots = Math.Max(1, civ.Cities.Count / 6);

        foreach (var city in civ.Cities.OrderByDescending(c => c.Population))
        {
            city.Style = ChooseCityStyle(civ, city);
            var spec = ChooseSpecialization(civ, city, academicSlots > 0, industrialSlots > 0, fortressSlots > 0);
            if (spec == CitySpecialization.Academic) academicSlots--;
            if (spec == CitySpecialization.Fortress) fortressSlots--;
            if (spec == CitySpecialization.Industrial) industrialSlots--;
            city.Specialization = spec;
        }
    }

    private CityStyle ChooseCityStyle(Civilization civ, City city)
    {
        // Only the great cities are rebuilt in modern materials; old towns keep their tradition
        if (civ.CivType == CivType.Spacefaring && (city.Type == CityType.Metropolis || city.IsCapital)) return CityStyle.Futuristic;
        if (civ.CivType >= CivType.Industrial && (city.Type == CityType.Metropolis || (city.Type == CityType.City && city.Id % 2 == 0))) return CityStyle.Modern;

        var cell = _map.Cells[city.X, city.Y];
        if (cell.Elevation > 0.55f) return CityStyle.Terraced;
        if (cell.Temperature < 6f || cell.GetBiomeData().CurrentBiome is Biome.BorealForest or Biome.Tundra) return CityStyle.Timber;
        if (cell.Rainfall < 0.25f || cell.GetBiomeData().CurrentBiome == Biome.Desert) return CityStyle.Adobe;
        if (cell.Temperature > 23f && cell.Rainfall > 0.5f) return CityStyle.Stilt;

        // Culture group 2 builds in the tiered-roof tradition
        if (civ.Culture % CultureSyllables.Length == 2) return CityStyle.Pagoda;
        return CityStyle.Stone;
    }

    private CitySpecialization ChooseSpecialization(Civilization civ, City city, bool academicAllowed, bool industrialAllowed, bool fortressAllowed)
    {
        if (city.IsCapital) return CitySpecialization.Capital;
        if (academicAllowed && city.Has(CityBuilding.University) && city.Type >= CityType.Town) return CitySpecialization.Academic;
        if (fortressAllowed && city.Has(CityBuilding.Walls) && city.Has(CityBuilding.Barracks) && IsBorderCity(civ, city)) return CitySpecialization.Fortress;
        if (industrialAllowed && city.Has(CityBuilding.Workshop) && civ.CivType >= CivType.Industrial) return CitySpecialization.Industrial;
        if (city.Has(CityBuilding.Harbor) || (city.Coastal && city.Type >= CityType.Town && civ.HasSeaTransport)) return CitySpecialization.Port;
        if (city.Has(CityBuilding.Temple) && city.Happiness > 0.7f && city.Type >= CityType.Town && city.Id % 4 == 0) return CitySpecialization.Holy;

        // Otherwise the land decides
        float farm = 0, fish = 0, mine = 0, wood = 0;
        for (int dx = -2; dx <= 2; dx++)
        {
            for (int dy = -2; dy <= 2; dy++)
            {
                int x = WrapX(city.X + dx);
                int y = city.Y + dy;
                if (y < 0 || y >= _map.Height) continue;
                var cell = _map.Cells[x, y];
                if (cell.IsWater) { fish += 1f; continue; }
                var biome = cell.GetBiomeData().CurrentBiome;
                if (biome is Biome.Grassland or Biome.Savanna or Biome.Shrubland) farm += 1f;
                if (biome is Biome.TemperateForest or Biome.BorealForest or Biome.TropicalRainforest) wood += 1f;
                if (cell.Elevation > 0.45f) mine += 1f;
                foreach (var deposit in cell.GetResources())
                {
                    if (deposit.Amount > 0 && deposit.Type is ResourceType.Iron or ResourceType.Copper or ResourceType.Gold
                        or ResourceType.Silver or ResourceType.Coal) mine += 3f;
                }
            }
        }

        if ((city.NearRiver || city.Has(CityBuilding.Market)) && city.Type >= CityType.Town && farm < 12) return CitySpecialization.Trade;
        if (mine >= farm && mine >= wood && mine >= 5) return CitySpecialization.Mining;
        if (fish >= 8 && fish >= farm) return CitySpecialization.Fishing;
        if (wood > farm) return CitySpecialization.Timber;
        return CitySpecialization.Farming;
    }

    private bool IsBorderCity(Civilization civ, City city)
    {
        for (int dx = -4; dx <= 4; dx += 2)
        {
            for (int dy = -4; dy <= 4; dy += 2)
            {
                int y = city.Y + dy;
                if (y < 0 || y >= _map.Height) continue;
                int owner = OwnerAt(WrapX(city.X + dx), y);
                if (owner != 0 && owner != civ.Id) return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Production multipliers of a specialization: (food, wood, stone+metal, gold, science).
    /// </summary>
    private static (float food, float wood, float industry, float gold, float science) GetSpecializationBonus(CitySpecialization spec) => spec switch
    {
        CitySpecialization.Farming => (1.15f, 1f, 1f, 1f, 1f),
        CitySpecialization.Fishing => (1.1f, 1f, 1f, 1.05f, 1f),
        CitySpecialization.Port => (1.05f, 1f, 1f, 1.35f, 1.05f),
        CitySpecialization.Mining => (0.95f, 1f, 1.5f, 1.1f, 1f),
        CitySpecialization.Timber => (1f, 1.6f, 1.1f, 1f, 1f),
        CitySpecialization.Trade => (1f, 1f, 1f, 1.4f, 1.1f),
        CitySpecialization.Industrial => (0.95f, 1.1f, 1.6f, 1.2f, 1.05f),
        CitySpecialization.Academic => (1f, 1f, 1f, 1.05f, 1.6f),
        CitySpecialization.Holy => (1f, 1f, 1f, 1.1f, 1.05f),
        CitySpecialization.Fortress => (0.95f, 1f, 1.1f, 0.9f, 1f),
        CitySpecialization.Capital => (1f, 1f, 1.1f, 1.25f, 1.2f),
        _ => (1f, 1f, 1f, 1f, 1f)
    };
}
