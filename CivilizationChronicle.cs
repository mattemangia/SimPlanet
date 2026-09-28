namespace SimPlanet;

/// <summary>
/// The chronicle records the history of the world's peoples: foundings, wars,
/// battles, conquests, famines, rebellions and treaties.
/// </summary>
public partial class CivilizationManager
{
    private readonly List<HistoryEvent> _chronicle = new();
    private const int MaxChronicleEntries = 500;

    /// <summary>
    /// When true, chronicle entries are also printed to the console (used by headless runs).
    /// </summary>
    public static bool LogChronicleToConsole { get; set; } = false;

    public List<HistoryEvent> GetChronicle()
    {
        lock (_civLock)
        {
            return _chronicle.ToList();
        }
    }

    private void AddChronicle(int year, HistoryCategory category, string text, int x, int y, int civId)
    {
        _chronicle.Add(new HistoryEvent
        {
            Year = year,
            Category = category,
            Text = text,
            X = x,
            Y = y,
            CivilizationId = civId
        });

        if (_chronicle.Count > MaxChronicleEntries)
        {
            _chronicle.RemoveRange(0, _chronicle.Count - MaxChronicleEntries);
        }

        if (LogChronicleToConsole)
        {
            Console.WriteLine($"  [Year {year}] {category}: {text}");
        }
    }
}

public enum HistoryCategory
{
    Founding,
    War,
    Battle,
    Conquest,
    Peace,
    Famine,
    Rebellion,
    Diplomacy,
    Disaster,
    Growth
}

public class HistoryEvent
{
    public int Year { get; set; }
    public string Text { get; set; } = "";
    public HistoryCategory Category { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int CivilizationId { get; set; }
}
