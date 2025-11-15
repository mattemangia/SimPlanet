using System.Text.Json;
using System.Text.Json.Serialization;

namespace SimPlanet;

/// <summary>
/// Manages save/load operations
/// </summary>
public class SaveLoadManager
{
    private const string SaveDirectory = "Saves";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        IncludeFields = true,
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals
    };

    public SaveLoadManager()
    {
        // Create saves directory if it doesn't exist
        if (!Directory.Exists(SaveDirectory))
        {
            Directory.CreateDirectory(SaveDirectory);
        }
    }

    public void SaveGame(PlanetMap map, GameState gameState,
                        CivilizationManager civManager, WeatherSystem weatherSystem,
                        HydrologySimulator hydroSim, string saveName)
    {
        var saveData = new SaveGameData
        {
            SaveName = saveName,
            SaveDate = DateTime.Now,
            GameYear = gameState.Year,
            TimeSpeed = gameState.TimeSpeed,
            MapWidth = map.Width,
            MapHeight = map.Height,
            MapOptions = map.Options,
            GlobalTemperature = map.GlobalTemperature,
            GlobalOxygen = map.GlobalOxygen,
            GlobalCO2 = map.GlobalCO2,
            SolarEnergy = map.SolarEnergy
        };

        // Save all cell data
        var cellList = new List<CellData>();
        for (int x = 0; x < map.Width; x++)
        {
            for (int y = 0; y < map.Height; y++)
            {
                var cell = map.Cells[x, y];
                var geo = cell.GetGeology();

                cellList.Add(new CellData
                {
                    X = x,
                    Y = y,
                    Elevation = cell.Elevation,
                    Temperature = cell.Temperature,
                    Rainfall = cell.Rainfall,
                    Humidity = cell.Humidity,
                    Oxygen = cell.Oxygen,
                    CO2 = cell.CO2,
                    Greenhouse = cell.Greenhouse,
                    LifeType = cell.LifeType,
                    Biomass = cell.Biomass,
                    Evolution = cell.Evolution,
                    PlateId = geo.PlateId,
                    IsVolcano = geo.IsVolcano,
                    VolcanicActivity = geo.VolcanicActivity,
                    MagmaPressure = geo.MagmaPressure,
                    ErosionRate = geo.ErosionRate,
                    SedimentLayer = geo.SedimentLayer,
                    WindSpeedX = cell.GetMeteorology().WindSpeedX,
                    WindSpeedY = cell.GetMeteorology().WindSpeedY,
                    AirPressure = cell.GetMeteorology().AirPressure,
                    InStorm = cell.GetMeteorology().InStorm
                });
            }
        }
        saveData.Cells = cellList.ToArray();

        // Save civilizations
        saveData.Civilizations = civManager.GetAllCivilizations()
            .Select(civ => new CivilizationData
            {
                Id = civ.Id,
                Name = civ.Name,
                CenterX = civ.CenterX,
                CenterY = civ.CenterY,
                Territory = civ.Territory.ToList(),
                Population = civ.Population,
                TechLevel = civ.TechLevel,
                CivilizationType = civ.CivType,
                Aggression = civ.Aggression,
                EcoFriendliness = civ.EcoFriendliness
            }).ToList();

        // Save weather
        saveData.ActiveStorms = weatherSystem.GetActiveStorms()
            .Select(s => new StormData
            {
                CenterX = s.CenterX,
                CenterY = s.CenterY,
                Intensity = s.Intensity,
                VelocityX = s.VelocityX,
                VelocityY = s.VelocityY,
                Type = s.Type
            }).ToList();

        // Save rivers
        saveData.Rivers = hydroSim.Rivers
            .Select(r => new RiverData
            {
                Id = r.Id,
                SourceX = r.SourceX,
                SourceY = r.SourceY,
                MouthX = r.MouthX,
                MouthY = r.MouthY,
                Path = r.Path.ToList(),
                WaterVolume = r.WaterVolume
            }).ToList();

        // Serialize to JSON
        string fileName = Path.Combine(SaveDirectory, $"{saveName}.json");
        string json = JsonSerializer.Serialize(saveData, JsonOptions);
        File.WriteAllText(fileName, json);
    }

    public SaveGameData? LoadGame(string saveName)
    {
        string fileName = Path.Combine(SaveDirectory, $"{saveName}.json");

        if (!File.Exists(fileName))
            return null;

        string json = File.ReadAllText(fileName);
        return JsonSerializer.Deserialize<SaveGameData>(json, JsonOptions);
    }

    public List<string> GetSaveGameList()
    {
        if (!Directory.Exists(SaveDirectory))
            return new List<string>();

        return Directory.GetFiles(SaveDirectory, "*.json")
            .Select(Path.GetFileNameWithoutExtension)
            .Where(name => name != null)
            .Cast<string>()
            .OrderByDescending(name =>
            {
                string path = Path.Combine(SaveDirectory, $"{name}.json");
                return File.GetLastWriteTime(path);
            })
            .ToList();
    }

    public void DeleteSave(string saveName)
    {
        string fileName = Path.Combine(SaveDirectory, $"{saveName}.json");
        if (File.Exists(fileName))
        {
            File.Delete(fileName);
        }
    }

    public void ApplySaveData(SaveGameData saveData, PlanetMap map, GameState gameState,
                             CivilizationManager civManager, WeatherSystem weatherSystem,
                             HydrologySimulator hydroSim)
    {
        // Restore game state
        gameState.Year = saveData.GameYear;
        gameState.TimeSpeed = saveData.TimeSpeed;

        // Restore global values
        map.GlobalTemperature = saveData.GlobalTemperature;
        map.GlobalOxygen = saveData.GlobalOxygen;
        map.GlobalCO2 = saveData.GlobalCO2;
        map.SolarEnergy = saveData.SolarEnergy;

        // Restore cell data
        foreach (var cellData in saveData.Cells)
        {
            if (cellData.X >= 0 && cellData.X < map.Width &&
                cellData.Y >= 0 && cellData.Y < map.Height)
            {
                var cell = map.Cells[cellData.X, cellData.Y];
                cell.Elevation = cellData.Elevation;
                cell.Temperature = cellData.Temperature;
                cell.Rainfall = cellData.Rainfall;
                cell.Humidity = cellData.Humidity;
                cell.Oxygen = cellData.Oxygen;
                cell.CO2 = cellData.CO2;
                cell.Greenhouse = cellData.Greenhouse;
                cell.LifeType = cellData.LifeType;
                cell.Biomass = cellData.Biomass;
                cell.Evolution = cellData.Evolution;

                var geo = cell.GetGeology();
                geo.PlateId = cellData.PlateId;
                geo.IsVolcano = cellData.IsVolcano;
                geo.VolcanicActivity = cellData.VolcanicActivity;
                geo.MagmaPressure = cellData.MagmaPressure;
                geo.ErosionRate = cellData.ErosionRate;
                geo.SedimentLayer = cellData.SedimentLayer;

                var met = cell.GetMeteorology();
                met.WindSpeedX = cellData.WindSpeedX;
                met.WindSpeedY = cellData.WindSpeedY;
                met.AirPressure = cellData.AirPressure;
                met.InStorm = cellData.InStorm;
            }
        }

        // Restore civilizations
        civManager.LoadCivilizations(saveData.Civilizations);

        // Restore weather
        weatherSystem.LoadStorms(saveData.ActiveStorms);

        // Restore rivers - convert RiverData to River
        var rivers = saveData.Rivers.Select(rd => new River
        {
            Id = rd.Id,
            SourceX = rd.SourceX,
            SourceY = rd.SourceY,
            MouthX = rd.MouthX,
            MouthY = rd.MouthY,
            Path = rd.Path,
            WaterVolume = rd.WaterVolume
        }).ToList();
        hydroSim.LoadRivers(rivers);
    }
}