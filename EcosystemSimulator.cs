using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SimPlanet;

/// <summary>
/// Simulates detailed ecosystem interactions including food webs, resource competition,
/// and symbiotic relationships.
/// </summary>
public class EcosystemSimulator
{
    private readonly PlanetMap _map;
    private readonly Random _random;
    private readonly AnimalEvolutionSimulator _animalSim;
    private readonly CivilizationManager _civManager;

    // Interaction weights
    private const float HERBIVORE_EAT_RATE = 0.1f;
    private const float CARNIVORE_EAT_RATE = 0.05f;
    private const float PLANT_REGROWTH_RATE = 0.05f;
    private const float DECOMPOSITION_RATE = 0.02f;

    public EcosystemSimulator(PlanetMap map, AnimalEvolutionSimulator animalSim, CivilizationManager civManager, int seed)
    {
        _map = map;
        _animalSim = animalSim;
        _civManager = civManager;
        _random = new Random(seed + 9000);
    }

    public void Update(float deltaTime)
    {
        // Use parallel processing for cell-based updates
        Parallel.For(0, _map.Width, x =>
        {
            for (int y = 0; y < _map.Height; y++)
            {
                UpdateCellEcosystem(x, y, deltaTime);
            }
        });

        // Global ecosystem effects
        UpdateGlobalEffects(deltaTime);
    }

    private void UpdateCellEcosystem(int x, int y, float deltaTime)
    {
        var cell = _map.Cells[x, y];

        // Skip empty cells
        if (cell.LifeType == LifeForm.None)
        {
            // Possible spontaneous growth if neighbors are healthy
            TrySpontaneousGrowth(x, y, deltaTime);
            return;
        }

        // 1. Food Web Dynamics
        if (IsHerbivore(cell.LifeType))
        {
            EatPlants(x, y, cell, deltaTime);
        }
        else if (IsCarnivore(cell.LifeType))
        {
            EatPrey(x, y, cell, deltaTime);
        }
        else if (cell.LifeType == LifeForm.PlantLife || cell.LifeType == LifeForm.Algae)
        {
            // Plants grow based on resources
            GrowPlants(x, y, cell, deltaTime);
        }

        // 2. Civilization Interactions
        // Ensure civilization doesn't get eaten or starved by simple mechanics
        if (cell.LifeType == LifeForm.Civilization)
        {
            // Civilization manages its own food, but we can add ecosystem benefits
            // e.g. high biomass neighbors improve happiness/food
        }

        // 3. Decomposition
        // Dead biomass returns nutrients (abstracted as better soil/growth potential)
        // We don't have a specific "dead biomass" field, but we can say if biomass is high
        // and life dies, it might leave nutrients. For now, simplified:
        // If biomass is very high, it might decay if not supported.
    }

    private void EatPlants(int x, int y, TerrainCell predator, float deltaTime)
    {
        // Look for plants in current or neighbor cells
        float foodFound = 0;

        // Check current cell first (if mixed life was possible, but it's 1 life per cell)
        // Since 1 life per cell, we look at neighbors
        foreach (var (nx, ny, neighbor) in _map.GetNeighbors(x, y))
        {
            if (neighbor.LifeType == LifeForm.PlantLife || neighbor.LifeType == LifeForm.Algae)
            {
                // Eat some biomass
                float eatAmount = Math.Min(neighbor.Biomass, HERBIVORE_EAT_RATE * deltaTime);
                neighbor.Biomass -= eatAmount;
                foodFound += eatAmount;

                // If plant dies
                if (neighbor.Biomass <= 0.05f)
                {
                    neighbor.LifeType = LifeForm.None;
                    neighbor.Biomass = 0;
                }

                if (foodFound > 0.1f) break; // Full
            }
        }

        // Effect on predator
        if (foodFound > 0.01f)
        {
            predator.Biomass = Math.Min(predator.Biomass + foodFound * 0.8f, 1.0f); // 80% efficiency
        }
        else
        {
            // Starvation
            predator.Biomass -= 0.02f * deltaTime;
        }
    }

    private void EatPrey(int x, int y, TerrainCell predator, float deltaTime)
    {
        float foodFound = 0;
        foreach (var (nx, ny, neighbor) in _map.GetNeighbors(x, y))
        {
            // Don't eat Civilization!
            if (neighbor.LifeType == LifeForm.Civilization) continue;

            if (IsPrey(neighbor.LifeType))
            {
                // Hunt
                // Success depends on evolution difference?
                float huntChance = 0.3f;
                if (_random.NextDouble() < huntChance * deltaTime)
                {
                    float eatAmount = Math.Min(neighbor.Biomass, CARNIVORE_EAT_RATE * deltaTime);
                    neighbor.Biomass -= eatAmount;
                    foodFound += eatAmount;

                    if (neighbor.Biomass <= 0.05f)
                    {
                        neighbor.LifeType = LifeForm.None;
                        neighbor.Biomass = 0;
                    }
                }

                if (foodFound > 0.1f) break;
            }
        }

        if (foodFound > 0.01f)
        {
            predator.Biomass = Math.Min(predator.Biomass + foodFound * 0.8f, 1.0f);
        }
        else
        {
             predator.Biomass -= 0.02f * deltaTime;
        }
    }

    private void GrowPlants(int x, int y, TerrainCell plant, float deltaTime)
    {
        // Photosynthesis / Nutrients
        // Controlled by Temperature, Rainfall, CO2
        if (plant.CO2 > 0.5f && plant.Rainfall > 0.1f)
        {
            plant.Biomass = Math.Min(plant.Biomass + PLANT_REGROWTH_RATE * deltaTime, 1.0f);

            // Plants consume CO2 and produce Oxygen
            plant.CO2 = Math.Max(0, plant.CO2 - 0.01f * deltaTime);
            plant.Oxygen = Math.Min(100, plant.Oxygen + 0.01f * deltaTime);
        }
    }

    private void TrySpontaneousGrowth(int x, int y, float deltaTime)
    {
        // If neighbors are plants, they might spread
        // This is also handled in LifeSimulator.SimulateLifeSpread
        // We can add symbiotic spreading here (e.g. animals spreading seeds)

        // Check for animal neighbors that might spread seeds
        bool seedSpreaderNearby = false;
        foreach (var (nx, ny, neighbor) in _map.GetNeighbors(x, y))
        {
            if (IsSeedSpreader(neighbor.LifeType))
            {
                seedSpreaderNearby = true;
                break;
            }
        }

        if (seedSpreaderNearby && _random.NextDouble() < 0.05 * deltaTime)
        {
            var cell = _map.Cells[x, y];
            if (cell.IsLand && cell.Rainfall > 0.2f && cell.Temperature > 5)
            {
                cell.LifeType = LifeForm.PlantLife;
                cell.Biomass = 0.1f;
            }
        }
    }

    private void UpdateGlobalEffects(float deltaTime)
    {
        // e.g. Global oxygen levels affecting global size of animals
    }

    private bool IsHerbivore(LifeForm life)
    {
        return life == LifeForm.SimpleAnimals ||
               life == LifeForm.Amphibians ||
               life == LifeForm.Mammals ||
               life == LifeForm.ComplexAnimals;
    }

    private bool IsCarnivore(LifeForm life)
    {
        return life == LifeForm.Dinosaurs ||
               life == LifeForm.MarineDinosaurs ||
               life == LifeForm.Pterosaurs ||
               life == LifeForm.Reptiles || // Some reptiles
               life == LifeForm.Fish; // Some fish
    }

    private bool IsPrey(LifeForm life)
    {
        return IsHerbivore(life) || life == LifeForm.Fish || life == LifeForm.SimpleAnimals;
    }

    private bool IsSeedSpreader(LifeForm life)
    {
        return life == LifeForm.Birds || life == LifeForm.Mammals;
    }
}
