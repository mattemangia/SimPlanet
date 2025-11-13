using System.Text.Json.Serialization;

namespace SimPlanet;

/// <summary>
/// Serializable save game data
/// </summary>
public class SaveGameData
{
    public string SaveName { get; set; } = "AutoSave";
    public DateTime SaveDate { get; set; }
    public int GameYear { get; set; }
    public float TimeSpeed { get; set; }

    // Map configuration
    public int MapWidth { get; set; }
    public int MapHeight { get; set; }
    public MapGenerationOptions MapOptions { get; set; } = new();

    // Global stats
    public float GlobalTemperature { get; set; }
    public float GlobalOxygen { get; set; }
    public float GlobalCO2 { get; set; }
    public float SolarEnergy { get; set; }

    // Terrain data
    public CellData[] Cells { get; set; } = Array.Empty<CellData>();

    // Civilization data
    public List<CivilizationData> Civilizations { get; set; } = new();

    // Weather patterns
    public List<StormData> ActiveStorms { get; set; } = new();

    // Rivers
    public List<RiverData> Rivers { get; set; } = new();
}

public class CellData
{
    public int X { get; set; }
    public int Y { get; set; }
    public float Elevation { get; set; }
    public float Temperature { get; set; }
    public float Rainfall { get; set; }
    public float Humidity { get; set; }
    public float Oxygen { get; set; }
    public float CO2 { get; set; }
    public float Greenhouse { get; set; }
    public LifeForm LifeType { get; set; }
    public float Biomass { get; set; }
    public float Evolution { get; set; }

    // Geological
    public int PlateId { get; set; }
    public bool IsVolcano { get; set; }
    public float VolcanicActivity { get; set; }
    public float MagmaPressure { get; set; }
    public float ErosionRate { get; set; }
    public float SedimentLayer { get; set; }

    // Meteorological
    public float WindSpeedX { get; set; }
    public float WindSpeedY { get; set; }
    public float AirPressure { get; set; }
    public bool InStorm { get; set; }
}

public class CivilizationData
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int CenterX { get; set; }
    public int CenterY { get; set; }
    public List<(int x, int y)> Territory { get; set; } = new();
    public int Population { get; set; }
    public int TechLevel { get; set; }
    public CivType CivilizationType { get; set; }
    public float Aggression { get; set; }
    public float EcoFriendliness { get; set; }
}

public class StormData
{
    public int CenterX { get; set; }
    public int CenterY { get; set; }
    public float Intensity { get; set; }
    public float VelocityX { get; set; }
    public float VelocityY { get; set; }
    public StormType Type { get; set; }
}

public class RiverData
{
    public int Id { get; set; }
    public int SourceX { get; set; }
    public int SourceY { get; set; }
    public List<(int x, int y)> Path { get; set; } = new();
}

public enum CivType
{
    Tribal,
    Agricultural,
    Industrial,
    Scientific,
    Spacefaring
}

public enum StormType
{
    Thunderstorm,
    Hurricane,
    Blizzard,
    Tornado
}
