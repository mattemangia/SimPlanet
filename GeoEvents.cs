using System.Collections.Concurrent;

namespace SimPlanet;

public enum GeoEventKind
{
    Earthquake,
    Eruption,
    Impact
}

/// <summary>
/// A geological event as it happened, kept for views that animate it (fault ruptures,
/// seismic waves, eruption columns, impact craters).
/// </summary>
public sealed class GeoEvent
{
    public long Id;
    public GeoEventKind Kind;
    public int X;
    public int Y;
    public float Magnitude;          // Richter magnitude, VEI, or impactor size
    public int Year;
    public long Ticks;               // Environment.TickCount64 when it happened
    public EruptionType Eruption;
    public FaultType Fault;
    public (int x, int y)[] Rupture = Array.Empty<(int x, int y)>(); // Cells that slipped, in rupture order
    public float Radius;             // Affected radius in cells
}

/// <summary>
/// Short rolling history of geological events shared between the simulation thread and the views.
/// </summary>
public static class GeoEvents
{
    private const int MaxEvents = 256;
    private static readonly ConcurrentQueue<GeoEvent> _events = new();
    private static long _nextId;

    public static void Publish(GeoEvent e)
    {
        e.Id = Interlocked.Increment(ref _nextId);
        e.Ticks = Environment.TickCount64;
        _events.Enqueue(e);
        while (_events.Count > MaxEvents)
        {
            _events.TryDequeue(out _);
        }
    }

    public static GeoEvent[] Snapshot() => _events.ToArray();

    public static void Clear()
    {
        while (_events.TryDequeue(out _)) { }
    }
}
