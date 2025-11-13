namespace SimPlanet;

/// <summary>
/// Simulates accurate animal evolution timeline with dinosaurs and mammals
/// Manages geological eras and mass extinction events
/// </summary>
public class AnimalEvolutionSimulator
{
    private readonly PlanetMap _map;
    private readonly Random _random;

    // Geological timeline (game years map to millions of years ago)
    // Assuming 1 game year = ~1 million years for geological accuracy
    private const int CAMBRIAN_EXPLOSION = 541;      // First complex animals
    private const int FISH_APPEAR = 500;             // First fish
    private const int LAND_PLANTS = 470;             // Plants colonize land
    private const int AMPHIBIANS_APPEAR = 370;       // First amphibians
    private const int REPTILES_APPEAR = 320;         // First reptiles
    private const int PERMIAN_EXTINCTION = 252;      // "Great Dying" - 96% species extinct
    private const int DINOSAURS_APPEAR = 230;        // First dinosaurs (Triassic)
    private const int DINOSAURS_DOMINATE_START = 200;// Jurassic - dinosaur dominance
    private const int KT_EXTINCTION = 66;            // K-T extinction - dinosaurs extinct
    private const int MAMMALS_DOMINATE = 60;         // Mammals become dominant
    private const int INTELLIGENCE_EARLIEST = 10;    // Earliest possible intelligence
    private const int CIVILIZATION_EARLIEST = 1;     // Earliest possible civilization

    // Extinction event tracking
    private bool _ktExtinctionOccurred = false;
    private bool _permianExtinctionOccurred = false;
    private int _lastExtinctionCheckYear = -1000;

    public bool DinosaursDominant { get; private set; } = false;
    public bool MammalsDominant { get; private set; } = false;
    public GeologicalEra CurrentEra { get; private set; } = GeologicalEra.Precambrian;

    public AnimalEvolutionSimulator(PlanetMap map, int seed)
    {
        _map = map;
        _random = new Random(seed + 7000);
    }

    public void Update(float deltaTime, int gameYear)
    {
        // Update geological era
        UpdateGeologicalEra(gameYear);

        // Check for mass extinction events
        CheckForExtinctions(gameYear);

        // Evolve animals based on current era
        EvolveAnimals(deltaTime, gameYear);

        // Ecosystem interactions
        SimulateEcosystems(deltaTime);
    }

    private void UpdateGeologicalEra(int year)
    {
        // Determine current geological era based on game year
        // (counting backwards from present, like real geological time)
        if (year < PERMIAN_EXTINCTION)
            CurrentEra = GeologicalEra.Precambrian;
        else if (year < KT_EXTINCTION)
            CurrentEra = GeologicalEra.Mesozoic;  // Age of Dinosaurs
        else
            CurrentEra = GeologicalEra.Cenozoic;  // Age of Mammals

        DinosaursDominant = year >= DINOSAURS_DOMINATE_START && year < KT_EXTINCTION;
        MammalsDominant = year >= MAMMALS_DOMINATE;
    }

    private void CheckForExtinctions(int gameYear)
    {
        // Avoid checking same year multiple times
        if (gameYear == _lastExtinctionCheckYear) return;
        _lastExtinctionCheckYear = gameYear;

        // Permian Extinction (252 Mya) - "The Great Dying"
        if (gameYear == PERMIAN_EXTINCTION && !_permianExtinctionOccurred)
        {
            TriggerPermianExtinction();
            _permianExtinctionOccurred = true;
        }

        // K-T Extinction (66 Mya) - Kills dinosaurs
        if (gameYear == KT_EXTINCTION && !_ktExtinctionOccurred)
        {
            TriggerKTExtinction();
            _ktExtinctionOccurred = true;
        }
    }

    private void TriggerPermianExtinction()
    {
        // "The Great Dying" - 96% of marine species, 70% of terrestrial species extinct
        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                var cell = _map.Cells[x, y];

                if (cell.LifeType != LifeForm.None &&
                    cell.LifeType != LifeForm.Bacteria &&
                    cell.LifeType != LifeForm.PlantLife)
                {
                    // 96% kill rate for marine life
                    if (cell.IsWater && _random.NextDouble() < 0.96)
                    {
                        cell.LifeType = LifeForm.None;
                        cell.Biomass = 0;
                    }
                    // 70% kill rate for land life
                    else if (cell.IsLand && _random.NextDouble() < 0.70)
                    {
                        cell.LifeType = LifeForm.None;
                        cell.Biomass = 0;
                    }
                }

                // Severe global warming and ocean acidification
                cell.Temperature += 8;  // +8°C global warming
                cell.CO2 += 5;          // Massive CO2 spike
            }
        }
    }

    private void TriggerKTExtinction()
    {
        // K-T Extinction - Asteroid impact kills dinosaurs and 75% of species
        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                var cell = _map.Cells[x, y];

                // Kill all dinosaurs
                if (cell.LifeType == LifeForm.Dinosaurs ||
                    cell.LifeType == LifeForm.MarineDinosaurs ||
                    cell.LifeType == LifeForm.Pterosaurs)
                {
                    cell.LifeType = LifeForm.None;
                    cell.Biomass = 0;
                }
                // 75% of other species die
                else if (cell.LifeType != LifeForm.None &&
                         cell.LifeType != LifeForm.Bacteria &&
                         cell.LifeType != LifeForm.PlantLife)
                {
                    if (_random.NextDouble() < 0.75)
                    {
                        cell.LifeType = LifeForm.None;
                        cell.Biomass = 0;
                    }
                }

                // Impact winter - global cooling
                cell.Temperature -= 15;  // Severe cooling

                // Reduce biomass (plants die from lack of sunlight)
                if (cell.LifeType == LifeForm.PlantLife)
                {
                    cell.Biomass *= 0.3f;  // 70% die-off
                }
            }
        }

        // After extinction, seed initial mammals in suitable areas
        SeedMammals();
    }

    private void SeedMammals()
    {
        // Seed small mammals in temperate forest areas
        int mammalsSeed = 0;
        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                var cell = _map.Cells[x, y];

                // Mammals thrive in temperate forests with good oxygen
                if (cell.IsLand &&
                    cell.Temperature > 0 && cell.Temperature < 30 &&
                    cell.Rainfall > 0.4f &&
                    cell.Oxygen > 18 &&
                    cell.LifeType == LifeForm.None &&
                    _random.NextDouble() < 0.05)
                {
                    cell.LifeType = LifeForm.Mammals;
                    cell.Biomass = 0.2f;
                    cell.Evolution = 0.3f;
                    mammalsSeed++;
                }
            }
        }
    }

    private void EvolveAnimals(float deltaTime, int gameYear)
    {
        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                var cell = _map.Cells[x, y];

                if (cell.LifeType == LifeForm.None) continue;

                // Evolution progress
                if (cell.Biomass > 0.5f)
                {
                    cell.Evolution += deltaTime * 0.01f;

                    // Attempt evolution if conditions are met
                    if (cell.Evolution > 1.0f && _random.NextDouble() < 0.05)
                    {
                        TryEvolveAnimal(cell, gameYear, x, y);
                    }
                }
            }
        }
    }

    private void TryEvolveAnimal(TerrainCell cell, int gameYear, int x, int y)
    {
        cell.Evolution = 0; // Reset after evolution attempt

        switch (cell.LifeType)
        {
            case LifeForm.SimpleAnimals:
                // Evolve to fish in water if time is right
                if (cell.IsWater && gameYear >= FISH_APPEAR)
                {
                    cell.LifeType = LifeForm.Fish;
                }
                break;

            case LifeForm.Fish:
                // Fish can evolve to amphibians on land edges
                if (gameYear >= AMPHIBIANS_APPEAR)
                {
                    bool nearLand = _map.GetNeighbors(x, y).Any(n => n.cell.IsLand);
                    if (nearLand && cell.Oxygen > 15)
                    {
                        cell.LifeType = LifeForm.Amphibians;
                    }
                }
                break;

            case LifeForm.Amphibians:
                // Amphibians evolve to reptiles
                if (gameYear >= REPTILES_APPEAR && cell.IsLand && cell.Oxygen > 18)
                {
                    cell.LifeType = LifeForm.Reptiles;
                }
                break;

            case LifeForm.Reptiles:
                // Reptiles evolve to dinosaurs in Mesozoic
                if (gameYear >= DINOSAURS_APPEAR && gameYear < KT_EXTINCTION)
                {
                    if (cell.IsLand)
                    {
                        cell.LifeType = LifeForm.Dinosaurs;
                        cell.Biomass = 0.6f; // Dinosaurs are large
                    }
                    else if (cell.IsWater)
                    {
                        cell.LifeType = LifeForm.MarineDinosaurs;
                        cell.Biomass = 0.5f;
                    }
                }
                // After K-T extinction, reptiles can't become dinosaurs anymore
                else if (gameYear >= KT_EXTINCTION)
                {
                    // Small reptiles survive
                    cell.Biomass = 0.2f;
                }
                break;

            case LifeForm.Dinosaurs:
                // Dinosaurs can evolve to birds
                if (_random.NextDouble() < 0.01)
                {
                    cell.LifeType = LifeForm.Birds;
                    cell.Biomass = 0.3f;
                }
                break;

            case LifeForm.Mammals:
                // Mammals evolve to complex animals in Cenozoic
                if (gameYear >= MAMMALS_DOMINATE && cell.Oxygen > 20)
                {
                    cell.LifeType = LifeForm.ComplexAnimals;
                }
                break;

            case LifeForm.ComplexAnimals:
                // Complex animals can evolve intelligence
                // But NOT while dinosaurs are dominant!
                if (gameYear >= INTELLIGENCE_EARLIEST &&
                    !DinosaursDominant &&
                    cell.Oxygen > 20 &&
                    _random.NextDouble() < 0.02)
                {
                    float ecosystemHealth = GetEcosystemDiversity(x, y);
                    if (ecosystemHealth > 0.5f)
                    {
                        cell.LifeType = LifeForm.Intelligence;
                        cell.Biomass = 0.3f;
                    }
                }
                break;

            case LifeForm.Intelligence:
                // Intelligence can develop civilization
                // Definitely NOT during dinosaur age!
                if (gameYear >= CIVILIZATION_EARLIEST &&
                    !DinosaursDominant &&
                    _random.NextDouble() < 0.01)
                {
                    cell.LifeType = LifeForm.Civilization;
                }
                break;
        }
    }

    private void SimulateEcosystems(float deltaTime)
    {
        // Predator-prey dynamics
        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                var cell = _map.Cells[x, y];

                if (!IsAnimal(cell.LifeType)) continue;

                // Carnivores need prey
                if (IsCarnivore(cell.LifeType))
                {
                    float preyAvailable = GetNearbyPrey(x, y);
                    if (preyAvailable < 0.1f)
                    {
                        // Starvation
                        cell.Biomass -= deltaTime * 0.05f;
                        if (cell.Biomass < 0.1f)
                        {
                            cell.LifeType = LifeForm.None;
                            cell.Biomass = 0;
                        }
                    }
                    else
                    {
                        // Hunting success
                        cell.Biomass = Math.Min(cell.Biomass + deltaTime * 0.02f, 0.8f);
                    }
                }
                // Herbivores need plants
                else if (IsHerbivore(cell.LifeType))
                {
                    float plantFood = GetNearbyPlants(x, y);
                    if (plantFood < 0.2f)
                    {
                        // No food
                        cell.Biomass -= deltaTime * 0.03f;
                        if (cell.Biomass < 0.1f)
                        {
                            cell.LifeType = LifeForm.None;
                            cell.Biomass = 0;
                        }
                    }
                    else
                    {
                        // Feeding
                        cell.Biomass = Math.Min(cell.Biomass + deltaTime * 0.03f, 0.9f);
                    }
                }
            }
        }
    }

    private bool IsAnimal(LifeForm life)
    {
        return life == LifeForm.SimpleAnimals ||
               life == LifeForm.Fish ||
               life == LifeForm.Amphibians ||
               life == LifeForm.Reptiles ||
               life == LifeForm.Dinosaurs ||
               life == LifeForm.MarineDinosaurs ||
               life == LifeForm.Pterosaurs ||
               life == LifeForm.Mammals ||
               life == LifeForm.Birds ||
               life == LifeForm.ComplexAnimals;
    }

    private bool IsCarnivore(LifeForm life)
    {
        // Large predators
        return life == LifeForm.Dinosaurs ||
               life == LifeForm.MarineDinosaurs ||
               life == LifeForm.Pterosaurs;
    }

    private bool IsHerbivore(LifeForm life)
    {
        return life == LifeForm.Mammals ||
               life == LifeForm.Amphibians ||
               life == LifeForm.ComplexAnimals;
    }

    private float GetNearbyPrey(int x, int y)
    {
        float total = 0;
        int count = 0;

        foreach (var (nx, ny, neighbor) in _map.GetNeighbors(x, y))
        {
            if (IsHerbivore(neighbor.LifeType) || neighbor.LifeType == LifeForm.SimpleAnimals)
            {
                total += neighbor.Biomass;
                count++;
            }
        }

        return count > 0 ? total / count : 0;
    }

    private float GetNearbyPlants(int x, int y)
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

        return lifeTypes.Count / 8.0f; // Normalize by expected max diversity
    }

    public string GetCurrentEraName()
    {
        return CurrentEra switch
        {
            GeologicalEra.Precambrian => "Precambrian (Early Life)",
            GeologicalEra.Paleozoic => "Paleozoic (Fish & Amphibians)",
            GeologicalEra.Mesozoic => "Mesozoic (Age of Dinosaurs)",
            GeologicalEra.Cenozoic => "Cenozoic (Age of Mammals)",
            _ => "Unknown Era"
        };
    }
}

public enum GeologicalEra
{
    Precambrian,  // Before 541 Mya - early life
    Paleozoic,    // 541-252 Mya - fish, amphibians, reptiles
    Mesozoic,     // 252-66 Mya - AGE OF DINOSAURS
    Cenozoic      // 66 Mya - present - Age of Mammals
}
