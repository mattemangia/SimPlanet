namespace SimPlanet;

/// <summary>
/// Types of pathogens that can cause pandemics
/// </summary>
public enum PathogenType
{
    Bacteria,      // Fast spread, medium lethality, easily cured
    Virus,         // Very fast spread, variable lethality, hard to cure
    Fungus,        // Slow spread, low lethality, medium cure difficulty
    Parasite,      // Medium spread, medium lethality, hard to detect
    Prion,         // Very slow spread, extremely lethal, very hard to cure
    Bioweapon      // Custom-designed pathogen with enhanced traits
}

/// <summary>
/// Transmission methods for diseases
/// </summary>
[Flags]
public enum TransmissionMethod
{
    None = 0,
    Air = 1,           // Spreads through air (coughing, breathing)
    Water = 2,         // Spreads through contaminated water
    Blood = 4,         // Spreads through blood contact
    Livestock = 8,     // Spreads through animals/farming
    Insects = 16,      // Spreads through insect vectors (mosquitoes, etc.)
    Rodents = 32,      // Spreads through rats and other rodents
    Birds = 64         // Spreads through migratory birds
}

/// <summary>
/// Symptoms that affect detection and severity
/// </summary>
[Flags]
public enum DiseaseSymptoms
{
    None = 0,
    Coughing = 1,
    Fever = 2,
    Vomiting = 4,
    Pneumonia = 8,
    OrganFailure = 16,
    Hemorrhaging = 32,
    Insanity = 64,
    Paralysis = 128,
    Coma = 256,
    TotalOrganFailure = 512
}

/// <summary>
/// Represents an active disease/pandemic
/// </summary>
public class Disease
{
    public int Id { get; set; }
    public string Name { get; set; } = "Disease X";
    public PathogenType Type { get; set; }

    // Core stats (0-1 range)
    public float Infectivity { get; set; } = 0.5f;      // How easily it spreads
    public float Severity { get; set; } = 0.3f;         // How sick people get
    public float Lethality { get; set; } = 0.2f;        // Death rate

    // Evolution points for upgrading disease
    public int DNAPoints { get; set; } = 0;

    // Transmission
    public TransmissionMethod TransmissionMethods { get; set; } = TransmissionMethod.Air;

    // Symptoms
    public DiseaseSymptoms Symptoms { get; set; } = DiseaseSymptoms.None;

    // Resistances
    public float ColdResistance { get; set; } = 0.0f;   // Arctic/cold climates
    public float HeatResistance { get; set; } = 0.0f;   // Desert/hot climates
    public float DrugResistance { get; set; } = 0.0f;   // Antibiotic/antiviral resistance

    // Abilities
    public bool HardenedResurgence { get; set; } = false;  // Can re-infect cured people
    public bool GeneticReShuffle { get; set; } = false;    // Slows cure research
    public bool TotalOrganShutdown { get; set; } = false;  // Increases lethality

    // Statistics
    public int TotalInfected { get; set; } = 0;
    public int TotalDeaths { get; set; } = 0;
    public int HealthyRemaining { get; set; } = 0;

    // Origin
    public int OriginX { get; set; }
    public int OriginY { get; set; }
    public int? OriginCivId { get; set; }  // Which civ it started in

    // Cure progress (0-100)
    public float GlobalCureProgress { get; set; } = 0.0f;
    public bool CureDeployed { get; set; } = false;

    // Active status
    public bool IsActive { get; set; } = false;
    public int DaysSinceOutbreak { get; set; } = 0;
}

/// <summary>
/// Infection data for a civilization
/// </summary>
public class CivilizationInfection
{
    public int CivilizationId { get; set; }
    public int DiseaseId { get; set; }
    public int InfectedCount { get; set; } = 0;
    public int DeadCount { get; set; } = 0;
    public bool Detected { get; set; } = false;
    public int DaysSinceInfection { get; set; } = 0;

    // Response measures
    public bool BordersClosed { get; set; } = false;
    public bool AirportsClosed { get; set; } = false;
    public bool PortsClosed { get; set; } = false;
    public bool QuarantineActive { get; set; } = false;

    // Research progress (0-100)
    public float LocalCureProgress { get; set; } = 0.0f;
}

/// <summary>
/// Manages disease outbreaks and pandemics
/// </summary>
public class DiseaseManager
{
    private readonly PlanetMap _map;
    private readonly CivilizationManager _civManager;
    private readonly Random _random;
    private List<Disease> _diseases;
    private Dictionary<(int diseaseId, int civId), CivilizationInfection> _infections;
    private int _nextDiseaseId = 1;

    public List<Disease> Diseases => _diseases;
    public Dictionary<(int diseaseId, int civId), CivilizationInfection> Infections => _infections;

    public DiseaseManager(PlanetMap map, CivilizationManager civManager, int seed)
    {
        _map = map;
        _civManager = civManager;
        _random = new Random(seed + 8000);
        _diseases = new List<Disease>();
        _infections = new Dictionary<(int diseaseId, int civId), CivilizationInfection>();
    }

    /// <summary>
    /// Create a new disease outbreak at a specific location
    /// </summary>
    public Disease CreateDisease(string name, PathogenType type, int x, int y)
    {
        var disease = new Disease
        {
            Id = _nextDiseaseId++,
            Name = name,
            Type = type,
            OriginX = x,
            OriginY = y,
            IsActive = true
        };

        // Set base stats based on pathogen type
        switch (type)
        {
            case PathogenType.Bacteria:
                disease.Infectivity = 0.6f;
                disease.Severity = 0.4f;
                disease.Lethality = 0.3f;
                disease.TransmissionMethods = TransmissionMethod.Air | TransmissionMethod.Water;
                break;
            case PathogenType.Virus:
                disease.Infectivity = 0.8f;
                disease.Severity = 0.5f;
                disease.Lethality = 0.4f;
                disease.TransmissionMethods = TransmissionMethod.Air | TransmissionMethod.Blood;
                break;
            case PathogenType.Fungus:
                disease.Infectivity = 0.3f;
                disease.Severity = 0.3f;
                disease.Lethality = 0.2f;
                disease.TransmissionMethods = TransmissionMethod.Air;
                break;
            case PathogenType.Parasite:
                disease.Infectivity = 0.5f;
                disease.Severity = 0.4f;
                disease.Lethality = 0.3f;
                disease.TransmissionMethods = TransmissionMethod.Water | TransmissionMethod.Livestock;
                break;
            case PathogenType.Prion:
                disease.Infectivity = 0.2f;
                disease.Severity = 0.9f;
                disease.Lethality = 0.95f;
                disease.TransmissionMethods = TransmissionMethod.Livestock;
                break;
            case PathogenType.Bioweapon:
                disease.Infectivity = 0.9f;
                disease.Severity = 0.8f;
                disease.Lethality = 0.7f;
                disease.TransmissionMethods = TransmissionMethod.Air | TransmissionMethod.Water | TransmissionMethod.Blood;
                break;
        }

        // Find origin civilization
        var originCiv = _civManager.Civilizations.FirstOrDefault(c =>
            c.Territory.Contains((x, y)));
        if (originCiv != null)
        {
            disease.OriginCivId = originCiv.Id;
            InfectCivilization(disease, originCiv, 1); // Patient zero
        }

        _diseases.Add(disease);
        return disease;
    }

    /// <summary>
    /// Update disease spread and cure research
    /// </summary>
    public void Update(float deltaTime, int currentYear)
    {
        foreach (var disease in _diseases.Where(d => d.IsActive && !d.CureDeployed).ToList())
        {
            disease.DaysSinceOutbreak += (int)(deltaTime * 365); // Convert years to days

            // Spread disease
            SpreadDisease(disease, deltaTime);

            // Update infections
            UpdateInfections(disease, deltaTime);

            // Civilization responses
            UpdateCivilizationResponses(disease, deltaTime);

            // Cure research
            UpdateCureResearch(disease, deltaTime);

            // Update statistics
            UpdateDiseaseStats(disease);

            // Generate DNA points based on infections
            if (disease.DaysSinceOutbreak % 30 == 0) // Every 30 days
            {
                int newInfections = _infections.Values.Count(i => i.DiseaseId == disease.Id && i.DaysSinceInfection < 30);
                disease.DNAPoints += newInfections / 100; // 1 point per 100 new infections
            }

            // Check if disease is eradicated
            if (disease.TotalInfected == 0 && disease.DaysSinceOutbreak > 100)
            {
                disease.IsActive = false;
            }
        }
    }

    private void InfectCivilization(Disease disease, Civilization civ, int initialInfected)
    {
        var key = (disease.Id, civ.Id);
        if (!_infections.ContainsKey(key))
        {
            _infections[key] = new CivilizationInfection
            {
                CivilizationId = civ.Id,
                DiseaseId = disease.Id,
                InfectedCount = initialInfected,
                DaysSinceInfection = 0
            };
        }
    }

    private void SpreadDisease(Disease disease, float deltaTime)
    {
        var infectedCivs = _infections.Values
            .Where(i => i.DiseaseId == disease.Id && i.InfectedCount > 0)
            .ToList();

        foreach (var infection in infectedCivs)
        {
            var sourceCiv = _civManager.Civilizations.FirstOrDefault(c => c.Id == infection.CivilizationId);
            if (sourceCiv == null) continue;

            // Spread to neighboring civilizations
            foreach (var targetCiv in _civManager.Civilizations)
            {
                if (targetCiv.Id == sourceCiv.Id) continue;

                var targetKey = (disease.Id, targetCiv.Id);
                var targetInfection = _infections.GetValueOrDefault(targetKey);

                // Check if already closed borders
                if (targetInfection?.BordersClosed == true) continue;

                float spreadChance = CalculateSpreadChance(disease, sourceCiv, targetCiv, infection);

                if (_random.NextDouble() < spreadChance * deltaTime)
                {
                    int newInfections = Math.Max(1, (int)(infection.InfectedCount * 0.01f));
                    InfectCivilization(disease, targetCiv, newInfections);
                }
            }
        }
    }

    private float CalculateSpreadChance(Disease disease, Civilization source, Civilization target, CivilizationInfection sourceInfection)
    {
        float baseChance = disease.Infectivity * 0.1f;

        // Check for shared borders
        bool sharesBorder = source.Territory.Any(st =>
            target.Territory.Any(tt =>
                Math.Abs(st.x - tt.x) <= 1 && Math.Abs(st.y - tt.y) <= 1));

        if (sharesBorder)
        {
            baseChance *= 2.0f; // Double chance if neighbors
        }

        // Transportation multipliers
        if (source.HasAirTransport && target.HasAirTransport &&
            disease.TransmissionMethods.HasFlag(TransmissionMethod.Air))
        {
            if (!sourceInfection.AirportsClosed)
                baseChance *= 3.0f;
        }

        if (source.HasSeaTransport && target.HasSeaTransport)
        {
            if (!sourceInfection.PortsClosed)
                baseChance *= 2.0f;
        }

        // Population density (cities are hotspots)
        int sourceCities = source.Cities.Count;
        int targetCities = target.Cities.Count;
        baseChance *= (1 + sourceCities * 0.1f) * (1 + targetCities * 0.1f);

        return Math.Min(baseChance, 0.95f); // Cap at 95%
    }

    private void UpdateInfections(Disease disease, float deltaTime)
    {
        var activeInfections = _infections.Values
            .Where(i => i.DiseaseId == disease.Id && i.InfectedCount > 0)
            .ToList();

        foreach (var infection in activeInfections)
        {
            var civ = _civManager.Civilizations.FirstOrDefault(c => c.Id == infection.CivilizationId);
            if (civ == null) continue;

            infection.DaysSinceInfection += (int)(deltaTime * 365);

            // Detection chance increases with time and severity
            if (!infection.Detected)
            {
                float detectionChance = disease.Severity * 0.01f * infection.DaysSinceInfection;
                if (civ.CivType >= CivType.Scientific)
                    detectionChance *= 2.0f;

                if (_random.NextDouble() < detectionChance * deltaTime)
                {
                    infection.Detected = true;
                }
            }

            // Spread within civilization
            int healthyPop = civ.Population - infection.InfectedCount - infection.DeadCount;
            if (healthyPop > 0)
            {
                float infectionRate = disease.Infectivity * 0.1f;

                // Quarantine reduces spread
                if (infection.QuarantineActive)
                    infectionRate *= 0.3f;

                int newInfections = (int)(infection.InfectedCount * infectionRate * deltaTime * 365);
                newInfections = Math.Min(newInfections, healthyPop);
                infection.InfectedCount += newInfections;
            }

            // Deaths from disease
            if (infection.InfectedCount > 0)
            {
                float deathRate = disease.Lethality * disease.Severity * 0.01f;
                int deaths = (int)(infection.InfectedCount * deathRate * deltaTime * 365);

                infection.DeadCount += deaths;
                infection.InfectedCount -= deaths;
                civ.Population = Math.Max(civ.Population - deaths, 100);
            }

            // Recovery (if cure is deployed or natural immunity)
            if (disease.CureDeployed || infection.InfectedCount > civ.Population * 0.7f)
            {
                float recoveryRate = disease.CureDeployed ? 0.5f : 0.05f;
                int recovered = (int)(infection.InfectedCount * recoveryRate * deltaTime * 365);
                infection.InfectedCount = Math.Max(infection.InfectedCount - recovered, 0);
            }
        }
    }

    private void UpdateCivilizationResponses(Disease disease, float deltaTime)
    {
        var activeInfections = _infections.Values
            .Where(i => i.DiseaseId == disease.Id && i.Detected)
            .ToList();

        foreach (var infection in activeInfections)
        {
            var civ = _civManager.Civilizations.FirstOrDefault(c => c.Id == infection.CivilizationId);
            if (civ == null) continue;

            float infectionPercentage = (float)infection.InfectedCount / civ.Population;

            // Close borders/airports/ports based on severity
            if (infectionPercentage > 0.05f && !infection.BordersClosed)
            {
                if (_random.NextDouble() < 0.1f * deltaTime)
                    infection.BordersClosed = true;
            }

            if (infectionPercentage > 0.1f && civ.HasAirTransport && !infection.AirportsClosed)
            {
                if (_random.NextDouble() < 0.2f * deltaTime)
                    infection.AirportsClosed = true;
            }

            if (infectionPercentage > 0.1f && civ.HasSeaTransport && !infection.PortsClosed)
            {
                if (_random.NextDouble() < 0.2f * deltaTime)
                    infection.PortsClosed = true;
            }

            if (infectionPercentage > 0.15f && !infection.QuarantineActive)
            {
                if (_random.NextDouble() < 0.3f * deltaTime)
                    infection.QuarantineActive = true;
            }
        }
    }

    private void UpdateCureResearch(Disease disease, float deltaTime)
    {
        if (disease.CureDeployed) return;

        // Civilizations with detected infections research cure
        var researchingCivs = _infections.Values
            .Where(i => i.DiseaseId == disease.Id && i.Detected)
            .ToList();

        float totalResearchPower = 0f;

        foreach (var infection in researchingCivs)
        {
            var civ = _civManager.Civilizations.FirstOrDefault(c => c.Id == infection.CivilizationId);
            if (civ == null) continue;

            // Research speed based on tech level
            float researchSpeed = civ.CivType switch
            {
                CivType.Tribal => 0.1f,
                CivType.Agricultural => 0.2f,
                CivType.Industrial => 0.5f,
                CivType.Scientific => 2.0f,
                CivType.Spacefaring => 5.0f,
                _ => 0.1f
            };

            // Drug resistance slows research
            researchSpeed *= (1.0f - disease.DrugResistance * 0.5f);

            // Gene reshuffle slows research
            if (disease.GeneticReShuffle)
                researchSpeed *= 0.5f;

            infection.LocalCureProgress += researchSpeed * deltaTime * 10;
            totalResearchPower += researchSpeed;
        }

        // Global cure progress (shared research)
        disease.GlobalCureProgress += totalResearchPower * deltaTime * 10;

        // Deploy cure when research is complete
        if (disease.GlobalCureProgress >= 100f)
        {
            disease.CureDeployed = true;
        }
    }

    private void UpdateDiseaseStats(Disease disease)
    {
        var diseaseInfections = _infections.Values.Where(i => i.DiseaseId == disease.Id).ToList();

        disease.TotalInfected = diseaseInfections.Sum(i => i.InfectedCount);
        disease.TotalDeaths = diseaseInfections.Sum(i => i.DeadCount);

        int totalPopulation = _civManager.Civilizations.Sum(c => c.Population);
        disease.HealthyRemaining = totalPopulation - disease.TotalInfected - disease.TotalDeaths;
    }

    /// <summary>
    /// Evolve disease trait (costs DNA points)
    /// </summary>
    public bool EvolveTrait(Disease disease, string traitName, int cost)
    {
        if (disease.DNAPoints < cost) return false;

        disease.DNAPoints -= cost;

        switch (traitName.ToLower())
        {
            // Transmission
            case "air":
                disease.TransmissionMethods |= TransmissionMethod.Air;
                disease.Infectivity += 0.1f;
                break;
            case "water":
                disease.TransmissionMethods |= TransmissionMethod.Water;
                disease.Infectivity += 0.1f;
                break;
            case "blood":
                disease.TransmissionMethods |= TransmissionMethod.Blood;
                disease.Infectivity += 0.15f;
                break;
            case "livestock":
                disease.TransmissionMethods |= TransmissionMethod.Livestock;
                disease.Infectivity += 0.1f;
                break;
            case "insects":
                disease.TransmissionMethods |= TransmissionMethod.Insects;
                disease.Infectivity += 0.15f;
                break;

            // Symptoms
            case "coughing":
                disease.Symptoms |= DiseaseSymptoms.Coughing;
                disease.Infectivity += 0.05f;
                disease.Severity += 0.05f;
                break;
            case "fever":
                disease.Symptoms |= DiseaseSymptoms.Fever;
                disease.Severity += 0.1f;
                break;
            case "pneumonia":
                disease.Symptoms |= DiseaseSymptoms.Pneumonia;
                disease.Severity += 0.15f;
                disease.Lethality += 0.1f;
                break;
            case "organ_failure":
                disease.Symptoms |= DiseaseSymptoms.OrganFailure;
                disease.Severity += 0.2f;
                disease.Lethality += 0.2f;
                break;
            case "total_organ_failure":
                disease.Symptoms |= DiseaseSymptoms.TotalOrganFailure;
                disease.Severity += 0.3f;
                disease.Lethality += 0.4f;
                break;

            // Resistances
            case "cold_resistance":
                disease.ColdResistance = Math.Min(disease.ColdResistance + 0.33f, 1.0f);
                break;
            case "heat_resistance":
                disease.HeatResistance = Math.Min(disease.HeatResistance + 0.33f, 1.0f);
                break;
            case "drug_resistance":
                disease.DrugResistance = Math.Min(disease.DrugResistance + 0.33f, 1.0f);
                break;

            // Abilities
            case "hardened_resurgence":
                disease.HardenedResurgence = true;
                break;
            case "genetic_reshuffle":
                disease.GeneticReShuffle = true;
                disease.GlobalCureProgress *= 0.5f; // Reset cure progress by 50%
                break;
            case "total_organ_shutdown":
                disease.TotalOrganShutdown = true;
                disease.Lethality = Math.Min(disease.Lethality + 0.3f, 1.0f);
                break;
        }

        // Cap values
        disease.Infectivity = Math.Min(disease.Infectivity, 1.0f);
        disease.Severity = Math.Min(disease.Severity, 1.0f);
        disease.Lethality = Math.Min(disease.Lethality, 1.0f);

        return true;
    }

    /// <summary>
    /// Get infection data for a specific civilization
    /// </summary>
    public CivilizationInfection? GetInfection(int diseaseId, int civId)
    {
        return _infections.GetValueOrDefault((diseaseId, civId));
    }
}
