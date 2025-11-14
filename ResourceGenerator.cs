using System;

namespace SimPlanet;

/// <summary>
/// Generates natural resource deposits across the planet
/// </summary>
public class ResourceGenerator
{
    private readonly PlanetMap _map;
    private readonly Random _random;

    public ResourceGenerator(PlanetMap map, int seed)
    {
        _map = map;
        _random = new Random(seed + 7777); // Offset seed for resources
    }

    public void GenerateResources()
    {
        GenerateMetalOres();
        GenerateFossilFuels();
        GenerateIndustrialMinerals();
        GeneratePreciousGems();
    }

    private void GenerateMetalOres()
    {
        // Iron - very common, found in many locations
        GenerateResourceDeposits(ResourceType.Iron, 150, 0.05f, 0.15f,
            (cell) => cell.IsLand && cell.Elevation > -0.2f);

        // Copper - common, often near volcanic activity
        GenerateResourceDeposits(ResourceType.Copper, 100, 0.04f, 0.12f,
            (cell) => cell.IsLand && cell.GetGeology().VolcanicRock > 0.2f);

        // Gold - rare, deep in mountains and volcanic areas
        GenerateResourceDeposits(ResourceType.Gold, 30, 0.02f, 0.06f,
            (cell) => cell.IsLand && (cell.Elevation > 0.5f || cell.GetGeology().VolcanicRock > 0.3f));

        // Silver - rare, mountainous regions
        GenerateResourceDeposits(ResourceType.Silver, 35, 0.02f, 0.07f,
            (cell) => cell.IsLand && cell.Elevation > 0.4f);

        // Aluminum - common, from bauxite in tropical/subtropical
        GenerateResourceDeposits(ResourceType.Aluminum, 120, 0.06f, 0.1f,
            (cell) => cell.IsLand && cell.Temperature > 15 && cell.Rainfall > 0.5f);

        // Titanium - rare, coastal and beach sands
        GenerateResourceDeposits(ResourceType.Titanium, 25, 0.015f, 0.04f,
            (cell) => cell.IsLand && cell.Elevation < 0.15f && IsNearWater(cell));

        // Uranium - very rare, crystalline rock formations
        GenerateResourceDeposits(ResourceType.Uranium, 15, 0.01f, 0.03f,
            (cell) => cell.IsLand && cell.GetGeology().CrystallineRock > 0.5f);

        // Platinum - extremely rare, deep deposits
        GenerateResourceDeposits(ResourceType.Platinum, 10, 0.008f, 0.02f,
            (cell) => cell.IsLand && cell.Elevation > 0.3f);
    }

    private void GenerateFossilFuels()
    {
        // Coal - sedimentary rock, ancient forests
        GenerateResourceDeposits(ResourceType.Coal, 80, 0.08f, 0.2f,
            (cell) => cell.IsLand && cell.GetGeology().SedimentaryRock > 0.4f);

        // Oil - sedimentary basins, often offshore
        GenerateResourceDeposits(ResourceType.Oil, 60, 0.06f, 0.15f,
            (cell) => cell.GetGeology().SedimentaryRock > 0.5f &&
                     (cell.IsWater || cell.Elevation < 0.2f));

        // Natural Gas - often found with oil
        GenerateResourceDeposits(ResourceType.NaturalGas, 70, 0.07f, 0.18f,
            (cell) => cell.GetGeology().SedimentaryRock > 0.5f &&
                     (cell.IsWater || cell.Elevation < 0.2f));
    }

    private void GenerateIndustrialMinerals()
    {
        // Limestone - sedimentary areas
        GenerateResourceDeposits(ResourceType.Limestone, 200, 0.1f, 0.25f,
            (cell) => cell.IsLand && cell.GetGeology().SedimentaryRock > 0.3f);

        // Granite - crystalline rock, mountains
        GenerateResourceDeposits(ResourceType.Granite, 150, 0.08f, 0.2f,
            (cell) => cell.IsLand && cell.GetGeology().CrystallineRock > 0.4f);

        // Salt - evaporite deposits, coastal areas
        GenerateResourceDeposits(ResourceType.Salt, 100, 0.06f, 0.15f,
            (cell) => IsNearWater(cell) && cell.IsLand);

        // Sulfur - volcanic areas
        GenerateResourceDeposits(ResourceType.Sulfur, 40, 0.03f, 0.08f,
            (cell) => cell.GetGeology().VolcanicRock > 0.4f);

        // Phosphate - sedimentary, coastal
        GenerateResourceDeposits(ResourceType.Phosphate, 60, 0.04f, 0.1f,
            (cell) => cell.IsLand && cell.GetGeology().SedimentaryRock > 0.3f && IsNearWater(cell));
    }

    private void GeneratePreciousGems()
    {
        // Diamond - very rare, deep in crystalline rock
        GenerateResourceDeposits(ResourceType.Diamond, 8, 0.005f, 0.015f,
            (cell) => cell.IsLand && cell.GetGeology().CrystallineRock > 0.6f && cell.Elevation > 0.4f);

        // Emerald - rare, mountainous
        GenerateResourceDeposits(ResourceType.Emerald, 10, 0.006f, 0.018f,
            (cell) => cell.IsLand && cell.Elevation > 0.5f);

        // Ruby - rare, metamorphic regions
        GenerateResourceDeposits(ResourceType.Ruby, 10, 0.006f, 0.018f,
            (cell) => cell.IsLand && cell.GetGeology().CrystallineRock > 0.5f);
    }

    private void GenerateResourceDeposits(ResourceType type, int numDeposits,
        float minAmount, float maxAmount, Func<TerrainCell, bool> condition)
    {
        int attempts = 0;
        int maxAttempts = numDeposits * 10;
        int generated = 0;

        while (generated < numDeposits && attempts < maxAttempts)
        {
            attempts++;

            int x = _random.Next(_map.Width);
            int y = _random.Next(_map.Height);
            var cell = _map.Cells[x, y];

            if (condition(cell))
            {
                float amount = (float)(_random.NextDouble() * (maxAmount - minAmount) + minAmount);
                float concentration = (float)(_random.NextDouble() * 0.5 + 0.5); // 0.5 to 1.0
                float depth = (float)_random.NextDouble(); // 0 to 1

                var deposit = new ResourceDeposit(type, amount, concentration, depth);
                cell.AddResource(deposit);

                // Sometimes create clusters (30% chance)
                if (_random.NextDouble() < 0.3)
                {
                    CreateResourceCluster(x, y, type, amount * 0.5f, concentration, depth);
                }

                generated++;
            }
        }
    }

    private void CreateResourceCluster(int centerX, int centerY, ResourceType type,
        float amount, float concentration, float depth)
    {
        // Add 2-4 nearby deposits
        int clusterSize = _random.Next(2, 5);

        for (int i = 0; i < clusterSize; i++)
        {
            int offsetX = _random.Next(-3, 4);
            int offsetY = _random.Next(-3, 4);

            int x = centerX + offsetX;
            int y = centerY + offsetY;

            if (x >= 0 && x < _map.Width && y >= 0 && y < _map.Height)
            {
                var cell = _map.Cells[x, y];
                float clusterAmount = amount * (float)(_random.NextDouble() * 0.5 + 0.5);
                float clusterConcentration = concentration * (float)(_random.NextDouble() * 0.3 + 0.7);

                var deposit = new ResourceDeposit(type, clusterAmount, clusterConcentration, depth);
                cell.AddResource(deposit);
            }
        }
    }

    private bool IsNearWater(TerrainCell cell)
    {
        // Check if cell is near water (simplified check)
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                int x = GetCellX(cell) + dx;
                int y = GetCellY(cell) + dy;

                if (x >= 0 && x < _map.Width && y >= 0 && y < _map.Height)
                {
                    if (_map.Cells[x, y].IsWater)
                        return true;
                }
            }
        }
        return false;
    }

    private int GetCellX(TerrainCell cell)
    {
        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                if (_map.Cells[x, y] == cell)
                    return x;
            }
        }
        return 0;
    }

    private int GetCellY(TerrainCell cell)
    {
        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                if (_map.Cells[x, y] == cell)
                    return y;
            }
        }
        return 0;
    }
}
