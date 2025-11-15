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

    public void Update(float deltaTime, GeologicalSimulator? geoSim = null, WeatherSystem? weatherSys = null)
    {
        // React to planetary events FIRST
        if (geoSim != null)
        {
            ReactToVolcanicEruptions(geoSim);
            ReactToEarthquakes(geoSim);
        }

        if (weatherSys != null)
        {
            ReactToStorms(weatherSys);
        }

        ReactToClimateChanges(deltaTime);

        // Then normal life processes
        SimulateBiomassGrowth(deltaTime);
        SimulateEvolution(deltaTime);
        SimulateLifeSpread(deltaTime);
    }

    public void SeedInitialLife()
    {
        // Seed bacteria in warm, wet areas
        SeedSpecificLife(LifeForm.Bacteria);
    }

    public void SeedSpecificLife(LifeForm lifeForm)
    {
        // Seed the specified life form in appropriate locations
        for (int i = 0; i < 100; i++)
        {
            int x = _random.Next(_map.Width);
            int y = _random.Next(_map.Height);

            var cell = _map.Cells[x, y];

            // Check if location is suitable for this life form
            bool suitable = lifeForm switch
            {
                LifeForm.Bacteria => cell.Temperature > -20 && cell.Temperature < 80,
                LifeForm.Algae => cell.IsWater && cell.Temperature > 0 && cell.Temperature < 40,
                LifeForm.PlantLife => cell.IsLand && cell.Temperature > 0 && cell.Temperature < 45 && cell.Rainfall > 0.2f,
                LifeForm.SimpleAnimals => cell.IsLand && cell.Oxygen > 15 && cell.Temperature > -10 && cell.Temperature < 40,
                LifeForm.ComplexAnimals => cell.IsLand && cell.Oxygen > 18 && cell.Temperature > -10 && cell.Temperature < 35,
                LifeForm.Dinosaurs => cell.IsLand && cell.Oxygen > 18 && cell.Temperature > 15 && cell.Temperature < 35,
                LifeForm.Mammals => cell.IsLand && cell.Oxygen > 18 && cell.Temperature > -20 && cell.Temperature < 40,
                LifeForm.Intelligence => cell.IsLand && cell.Oxygen > 20 && cell.Temperature > -10 && cell.Temperature < 30,
                _ => cell.Temperature > 0 && cell.Humidity > 0.3f
            };

            if (suitable)
            {
                cell.LifeType = lifeForm;
                cell.Biomass = lifeForm == LifeForm.Bacteria ? 0.1f : 0.3f;
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

                // Note: Gas exchange (O2/CO2) handled by AtmosphereSimulator

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

            case LifeForm.Fish:
                // Aquatic vertebrates need water and oxygen
                if (cell.IsWater && cell.Temperature > 0 && cell.Temperature < 35 && cell.Oxygen > 12)
                {
                    float foodAvailable = GetNearbyBiomass(x, y);
                    growth = 0.25f * Math.Min(foodAvailable, 1.0f) * (cell.Oxygen / 21.0f);
                }
                break;

            case LifeForm.Amphibians:
                // Need both water and land nearby
                if (cell.Temperature > 5 && cell.Temperature < 40 && cell.Oxygen > 15)
                {
                    float foodAvailable = GetNearbyBiomass(x, y);
                    growth = 0.2f * Math.Min(foodAvailable, 1.0f) * (cell.Oxygen / 21.0f);
                }
                break;

            case LifeForm.Reptiles:
                // Cold-blooded, need warmth
                if (cell.IsLand && cell.Temperature > 10 && cell.Temperature < 45 && cell.Oxygen > 16)
                {
                    float foodAvailable = GetNearbyBiomass(x, y);
                    growth = 0.22f * Math.Min(foodAvailable, 1.0f) * (cell.Oxygen / 21.0f);
                }
                break;

            case LifeForm.Dinosaurs:
                // Large land reptiles, dominant predators/herbivores
                if (cell.IsLand && cell.Temperature > 15 && cell.Temperature < 40 && cell.Oxygen > 18)
                {
                    float foodAvailable = GetNearbyBiomass(x, y);
                    growth = 0.3f * Math.Min(foodAvailable, 1.0f) * (cell.Oxygen / 21.0f);
                }
                break;

            case LifeForm.MarineDinosaurs:
                // Marine reptiles
                if (cell.IsWater && cell.Temperature > 10 && cell.Temperature < 35 && cell.Oxygen > 17)
                {
                    float foodAvailable = GetNearbyBiomass(x, y);
                    growth = 0.28f * Math.Min(foodAvailable, 1.0f) * (cell.Oxygen / 21.0f);
                }
                break;

            case LifeForm.Pterosaurs:
                // Flying reptiles
                if (cell.Temperature > 15 && cell.Temperature < 38 && cell.Oxygen > 19)
                {
                    float foodAvailable = GetNearbyBiomass(x, y);
                    growth = 0.25f * Math.Min(foodAvailable, 1.0f) * (cell.Oxygen / 21.0f);
                }
                break;

            case LifeForm.Mammals:
                // Warm-blooded, more adaptable
                if (cell.Temperature > -20 && cell.Temperature < 45 && cell.Oxygen > 18)
                {
                    float foodAvailable = GetNearbyBiomass(x, y);
                    growth = 0.27f * Math.Min(foodAvailable, 1.0f) * (cell.Oxygen / 21.0f);
                }
                break;

            case LifeForm.Birds:
                // Warm-blooded, efficient
                if (cell.Temperature > -10 && cell.Temperature < 45 && cell.Oxygen > 19)
                {
                    float foodAvailable = GetNearbyBiomass(x, y);
                    growth = 0.26f * Math.Min(foodAvailable, 1.0f) * (cell.Oxygen / 21.0f);
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
        // NOTE: Main evolution logic now handled by AnimalEvolutionSimulator
        // This only handles basic microbe to plant evolution

        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                var cell = _map.Cells[x, y];

                if (cell.LifeType == LifeForm.None || cell.LifeType == LifeForm.Civilization)
                    continue;

                // Evolution progress (only for basic life forms)
                if (cell.Biomass > 0.5f &&
                    (cell.LifeType == LifeForm.Bacteria ||
                     cell.LifeType == LifeForm.Algae ||
                     cell.LifeType == LifeForm.PlantLife))
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
        // Only handle basic evolution: bacteria → algae → plants → simple animals
        // AnimalEvolutionSimulator handles the rest
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
                // Plants enable simple animals (then AnimalEvolutionSimulator takes over)
                if (cell.Oxygen > 15)
                {
                    cell.LifeType = LifeForm.SimpleAnimals;
                    cell.Biomass = 0.3f;
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
                return cell.Temperature > -10 && cell.Temperature < 50 && cell.Oxygen > 15;

            case LifeForm.Fish:
                return cell.IsWater && cell.Temperature > 0 && cell.Temperature < 35 && cell.Oxygen > 12;

            case LifeForm.Amphibians:
                return cell.Temperature > 5 && cell.Temperature < 40 && cell.Oxygen > 15;

            case LifeForm.Reptiles:
                return cell.IsLand && cell.Temperature > 10 && cell.Temperature < 45 && cell.Oxygen > 16;

            case LifeForm.Dinosaurs:
                return cell.IsLand && cell.Temperature > 15 && cell.Temperature < 40 && cell.Oxygen > 18;

            case LifeForm.MarineDinosaurs:
                return cell.IsWater && cell.Temperature > 10 && cell.Temperature < 35 && cell.Oxygen > 17;

            case LifeForm.Pterosaurs:
                return cell.Temperature > 15 && cell.Temperature < 38 && cell.Oxygen > 19;

            case LifeForm.Mammals:
                return cell.Temperature > -20 && cell.Temperature < 45 && cell.Oxygen > 18;

            case LifeForm.Birds:
                return cell.Temperature > -10 && cell.Temperature < 45 && cell.Oxygen > 19;

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

    // === PLANETARY EVENT REACTIVITY ===

    private void ReactToVolcanicEruptions(GeologicalSimulator geoSim)
    {
        // Life is killed or damaged by volcanic eruptions
        foreach (var (x, y, year) in geoSim.RecentEruptions)
        {
            // Direct impact zone
            var cell = _map.Cells[x, y];
            cell.Biomass = 0; // Complete destruction
            cell.LifeType = LifeForm.None;

            // Surrounding area damage
            foreach (var (nx, ny, neighbor) in _map.GetNeighbors(x, y))
            {
                neighbor.Biomass *= 0.3f; // 70% killed
                if (neighbor.Biomass < 0.1f)
                {
                    neighbor.LifeType = LifeForm.None;
                    neighbor.Biomass = 0;
                }
            }
        }
    }

    private void ReactToEarthquakes(GeologicalSimulator geoSim)
    {
        // Earthquakes damage life based on magnitude
        foreach (var (x, y, magnitude) in geoSim.Earthquakes)
        {
            var cell = _map.Cells[x, y];

            // Higher magnitude = more damage
            float damageRadius = magnitude * 3;

            for (int dx = -(int)damageRadius; dx <= damageRadius; dx++)
            {
                for (int dy = -(int)damageRadius; dy <= damageRadius; dy++)
                {
                    int nx = (x + dx + _map.Width) % _map.Width;
                    int ny = y + dy;
                    if (ny < 0 || ny >= _map.Height) continue;

                    float dist = MathF.Sqrt(dx * dx + dy * dy);
                    if (dist > damageRadius) continue;

                    var target = _map.Cells[nx, ny];

                    // Bacteria are extremely resilient and survive earthquakes
                    if (target.LifeType == LifeForm.Bacteria)
                        continue;

                    float damage = (1 - dist / damageRadius) * magnitude * 0.2f;
                    target.Biomass *= (1 - damage);

                    if (target.Biomass < 0.1f)
                    {
                        target.LifeType = LifeForm.None;
                        target.Biomass = 0;
                    }
                }
            }
        }
    }

    private void ReactToStorms(WeatherSystem weatherSys)
    {
        // Storms damage life
        foreach (var storm in weatherSys.ActiveStorms)
        {
            // Check if storm is a hurricane (any category)
            bool isHurricane = storm.Type >= StormType.HurricaneCategory1 &&
                              storm.Type <= StormType.HurricaneCategory5;

            // Determine radius based on storm type
            int radius = storm.Type switch
            {
                StormType.TropicalDepression => 10,
                StormType.TropicalStorm => 12,
                StormType.HurricaneCategory1 => 15,
                StormType.HurricaneCategory2 => 18,
                StormType.HurricaneCategory3 => 20,
                StormType.HurricaneCategory4 => 22,
                StormType.HurricaneCategory5 => 25,
                StormType.Blizzard => 12,
                _ => 8  // Thunderstorm, Tornado
            };

            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dy = -radius; dy <= radius; dy++)
                {
                    int x = (storm.CenterX + dx + _map.Width) % _map.Width;
                    int y = storm.CenterY + dy;
                    if (y < 0 || y >= _map.Height) continue;

                    float dist = MathF.Sqrt(dx * dx + dy * dy);
                    if (dist > radius) continue;

                    var cell = _map.Cells[x, y];
                    float effectStrength = storm.Intensity * (1 - dist / radius);

                    // Storm damage to biomass (already handled in WeatherSystem, but keep for extra damage)
                    if (isHurricane && effectStrength > 0.5f)
                    {
                        cell.Biomass *= (1 - effectStrength * 0.3f);
                    }
                    else if (storm.Type == StormType.Tornado && effectStrength > 0.7f)
                    {
                        cell.Biomass *= 0.2f; // Severe damage
                    }
                    else if (storm.Type == StormType.Blizzard)
                    {
                        // Blizzards less damaging but affect temperature-sensitive life
                        if (cell.LifeType == LifeForm.PlantLife && effectStrength > 0.6f)
                        {
                            cell.Biomass *= 0.7f;
                        }
                    }

                    if (cell.Biomass < 0.1f)
                    {
                        cell.LifeType = LifeForm.None;
                        cell.Biomass = 0;
                    }
                }
            }
        }
    }

    private void ReactToClimateChanges(float deltaTime)
    {
        // Life responds to gradual climate changes
        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                var cell = _map.Cells[x, y];

                if (cell.LifeType == LifeForm.None) continue;

                float stressFactor = 0;

                // Temperature stress
                if (cell.Temperature > 50 || cell.Temperature < -30)
                {
                    stressFactor += 0.5f;
                }
                else if (cell.Temperature > 40 || cell.Temperature < -20)
                {
                    stressFactor += 0.2f;
                }

                // Oxygen stress (for aerobic life)
                if (cell.LifeType != LifeForm.Bacteria)
                {
                    if (cell.Oxygen < 5)
                    {
                        stressFactor += 0.8f; // Severe
                    }
                    else if (cell.Oxygen < 10)
                    {
                        stressFactor += 0.3f;
                    }
                }

                // CO2 toxicity
                if (cell.CO2 > 15)
                {
                    stressFactor += 0.5f;
                }
                else if (cell.CO2 > 10)
                {
                    stressFactor += 0.2f;
                }

                // Drought stress (for land life)
                if (cell.IsLand && cell.Rainfall < 0.1f)
                {
                    if (cell.LifeType == LifeForm.PlantLife)
                    {
                        stressFactor += 0.4f;
                    }
                    else if (cell.LifeType == LifeForm.SimpleAnimals ||
                            cell.LifeType == LifeForm.ComplexAnimals)
                    {
                        stressFactor += 0.3f;
                    }
                }

                // Flooding stress (rapid elevation changes)
                var geo = cell.GetGeology();
                if (geo.SedimentLayer > 0.5f && cell.IsLand)
                {
                    stressFactor += 0.2f; // Burial by sediment
                }

                // Apply stress damage
                if (stressFactor > 0)
                {
                    cell.Biomass *= (1 - stressFactor * deltaTime * 0.1f);

                    if (cell.Biomass < 0.05f)
                    {
                        cell.LifeType = LifeForm.None;
                        cell.Biomass = 0;
                        cell.Evolution = 0;
                    }
                }

                // Adaptation over time
                // Life that survives harsh conditions becomes more resilient
                if (stressFactor > 0.2f && cell.Biomass > 0.3f)
                {
                    cell.Evolution += deltaTime * 0.005f; // Stress-driven evolution
                }
            }
        }
    }

    private int x, y; // Helper fields for neighbor calculations
}
