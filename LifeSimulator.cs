namespace SimPlanet;

/// <summary>
/// Simulates life evolution and biomass dynamics
/// </summary>
public class LifeSimulator
{
    private readonly PlanetMap _map;
    private readonly Random _random;

    public LifeSimulator(PlanetMap map)
    {
        _map = map;
        _random = new Random();
    }

    public void Update(float deltaTime)
    {
        SimulateBiomassGrowth(deltaTime);
        SimulateEvolution(deltaTime);
        SimulateLifeSpread(deltaTime);
    }

    public void SeedInitialLife()
    {
        // Seed bacteria in warm, wet areas
        for (int i = 0; i < 100; i++)
        {
            int x = _random.Next(_map.Width);
            int y = _random.Next(_map.Height);

            var cell = _map.Cells[x, y];
            if (cell.Temperature > 0 && cell.Temperature < 50 && cell.Humidity > 0.3f)
            {
                cell.LifeType = LifeForm.Bacteria;
                cell.Biomass = 0.1f;
                cell.Evolution = 0.0f;
            }
        }
    }

    private void SimulateBiomassGrowth(float deltaTime)
    {
        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                var cell = _map.Cells[x, y];

                if (cell.LifeType == LifeForm.None)
                    continue;

                float growthRate = CalculateGrowthRate(cell);
                float deathRate = CalculateDeathRate(cell);

                float netGrowth = (growthRate - deathRate) * deltaTime;
                cell.Biomass = Math.Clamp(cell.Biomass + netGrowth, 0, 1);

                // Die off if conditions are too harsh
                if (cell.Biomass < 0.01f)
                {
                    cell.LifeType = LifeForm.None;
                    cell.Biomass = 0;
                    cell.Evolution = 0;
                }
            }
        }
    }

    private float CalculateGrowthRate(TerrainCell cell)
    {
        float growth = 0;

        switch (cell.LifeType)
        {
            case LifeForm.Bacteria:
                // Can survive almost anywhere
                if (cell.Temperature > -20 && cell.Temperature < 80)
                {
                    growth = 0.1f;
                }
                break;

            case LifeForm.Algae:
                // Needs water and sunlight
                if (cell.IsWater && cell.Temperature > 0 && cell.Temperature < 40)
                {
                    growth = 0.3f * (cell.Oxygen / 100.0f + 0.5f);
                }
                break;

            case LifeForm.PlantLife:
                // Needs land, water, and moderate temperature
                if (cell.IsLand && cell.Temperature > 0 && cell.Temperature < 45 &&
                    cell.Rainfall > 0.2f && cell.Oxygen > 10)
                {
                    growth = 0.4f * cell.Rainfall * (cell.Oxygen / 21.0f);
                }
                break;

            case LifeForm.SimpleAnimals:
                // Needs oxygen and food (other biomass)
                if (cell.Temperature > -10 && cell.Temperature < 50 && cell.Oxygen > 15)
                {
                    float foodAvailable = GetNearbyBiomass(x, y);
                    growth = 0.2f * Math.Min(foodAvailable, 1.0f) * (cell.Oxygen / 21.0f);
                }
                break;

            case LifeForm.ComplexAnimals:
                // Needs good oxygen and abundant food
                if (cell.Temperature > -5 && cell.Temperature < 40 && cell.Oxygen > 18)
                {
                    float foodAvailable = GetNearbyBiomass(x, y);
                    growth = 0.15f * Math.Min(foodAvailable, 1.0f) * (cell.Oxygen / 21.0f);
                }
                break;

            case LifeForm.Intelligence:
                // Tool users, needs diverse ecosystem
                if (cell.Temperature > -10 && cell.Temperature < 35 && cell.Oxygen > 19)
                {
                    float ecosystemHealth = GetEcosystemDiversity(x, y);
                    growth = 0.1f * ecosystemHealth;
                }
                break;

            case LifeForm.Civilization:
                // Advanced civilization
                if (cell.Temperature > -20 && cell.Temperature < 40 && cell.Oxygen > 18)
                {
                    growth = 0.2f; // Civilizations can adapt
                }
                break;
        }

        return growth;
    }

    private float CalculateDeathRate(TerrainCell cell)
    {
        float death = 0.05f; // Base death rate

        // Temperature extremes
        if (cell.Temperature < -30 || cell.Temperature > 60)
        {
            death += 0.3f;
        }

        // Lack of oxygen (for aerobic life)
        if (cell.LifeType != LifeForm.Bacteria && cell.Oxygen < 10)
        {
            death += 0.2f;
        }

        // Too much CO2
        if (cell.CO2 > 10)
        {
            death += 0.1f;
        }

        // Drought
        if (cell.IsLand && cell.Rainfall < 0.1f && cell.LifeType == LifeForm.PlantLife)
        {
            death += 0.2f;
        }

        return death;
    }

    private void SimulateEvolution(float deltaTime)
    {
        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                var cell = _map.Cells[x, y];

                if (cell.LifeType == LifeForm.None || cell.LifeType == LifeForm.Civilization)
                    continue;

                // Evolution progress
                if (cell.Biomass > 0.5f)
                {
                    cell.Evolution += deltaTime * 0.01f;

                    // Check for evolution to next stage
                    if (cell.Evolution > 1.0f && _random.NextDouble() < 0.1)
                    {
                        TryEvolve(cell);
                    }
                }
            }
        }
    }

    private void TryEvolve(TerrainCell cell)
    {
        switch (cell.LifeType)
        {
            case LifeForm.Bacteria:
                // Evolve to algae in water, stay bacteria on land
                if (cell.IsWater)
                {
                    cell.LifeType = LifeForm.Algae;
                    cell.Evolution = 0;
                }
                break;

            case LifeForm.Algae:
                // Algae can colonize land as plants if conditions are right
                if (cell.IsLand && cell.Rainfall > 0.3f && cell.Oxygen > 10)
                {
                    cell.LifeType = LifeForm.PlantLife;
                    cell.Evolution = 0;
                }
                break;

            case LifeForm.PlantLife:
                // Plants enable animals
                if (cell.Oxygen > 15)
                {
                    cell.LifeType = LifeForm.SimpleAnimals;
                    cell.Biomass = 0.3f;
                    cell.Evolution = 0;
                }
                break;

            case LifeForm.SimpleAnimals:
                if (cell.Oxygen > 18)
                {
                    cell.LifeType = LifeForm.ComplexAnimals;
                    cell.Evolution = 0;
                }
                break;

            case LifeForm.ComplexAnimals:
                // Rare evolution to intelligence
                if (cell.Oxygen > 20 && _random.NextDouble() < 0.05)
                {
                    cell.LifeType = LifeForm.Intelligence;
                    cell.Evolution = 0;
                }
                break;

            case LifeForm.Intelligence:
                // Even rarer evolution to civilization
                if (_random.NextDouble() < 0.02)
                {
                    cell.LifeType = LifeForm.Civilization;
                    cell.Evolution = 0;
                }
                break;
        }
    }

    private void SimulateLifeSpread(float deltaTime)
    {
        // Randomly spread life to neighboring cells
        if (_random.NextDouble() < deltaTime * 0.1)
        {
            int x = _random.Next(_map.Width);
            int y = _random.Next(_map.Height);

            var cell = _map.Cells[x, y];
            if (cell.LifeType != LifeForm.None && cell.Biomass > 0.3f)
            {
                // Try to spread to a neighbor
                var neighbors = _map.GetNeighbors(x, y).ToList();
                if (neighbors.Count > 0)
                {
                    var (nx, ny, neighbor) = neighbors[_random.Next(neighbors.Count)];

                    if (neighbor.LifeType == LifeForm.None)
                    {
                        // Check if neighbor is suitable
                        if (CanLifeSurvive(cell.LifeType, neighbor))
                        {
                            neighbor.LifeType = cell.LifeType;
                            neighbor.Biomass = 0.1f;
                            neighbor.Evolution = cell.Evolution * 0.5f;
                        }
                    }
                }
            }
        }
    }

    private bool CanLifeSurvive(LifeForm lifeType, TerrainCell cell)
    {
        switch (lifeType)
        {
            case LifeForm.Bacteria:
                return cell.Temperature > -20 && cell.Temperature < 80;

            case LifeForm.Algae:
                return cell.IsWater && cell.Temperature > 0 && cell.Temperature < 40;

            case LifeForm.PlantLife:
                return cell.IsLand && cell.Temperature > 0 && cell.Temperature < 45 &&
                       cell.Rainfall > 0.2f && cell.Oxygen > 10;

            case LifeForm.SimpleAnimals:
            case LifeForm.ComplexAnimals:
                return cell.Temperature > -10 && cell.Temperature < 50 && cell.Oxygen > 15;

            case LifeForm.Intelligence:
            case LifeForm.Civilization:
                return cell.Temperature > -20 && cell.Temperature < 40 && cell.Oxygen > 18;

            default:
                return false;
        }
    }

    private float GetNearbyBiomass(int x, int y)
    {
        float total = 0;
        int count = 0;

        foreach (var (nx, ny, neighbor) in _map.GetNeighbors(x, y))
        {
            if (neighbor.LifeType == LifeForm.PlantLife || neighbor.LifeType == LifeForm.Algae)
            {
                total += neighbor.Biomass;
                count++;
            }
        }

        return count > 0 ? total / count : 0;
    }

    private float GetEcosystemDiversity(int x, int y)
    {
        var lifeTypes = new HashSet<LifeForm>();

        foreach (var (nx, ny, neighbor) in _map.GetNeighbors(x, y))
        {
            if (neighbor.LifeType != LifeForm.None)
            {
                lifeTypes.Add(neighbor.LifeType);
            }
        }

        return lifeTypes.Count / 5.0f; // Normalize by max expected diversity
    }

    private int x, y; // Helper fields for neighbor calculations
}
