using System.Diagnostics;

namespace SimPlanet;

/// <summary>
/// Lightweight timing probes. Set SIMPLANET_PERF=1 to print, every two seconds, how long each
/// measured section takes per call and how often it runs.
/// </summary>
public static class Perf
{
    public static readonly bool Enabled = Environment.GetEnvironmentVariable("SIMPLANET_PERF") == "1";

    private sealed class Probe
    {
        public long Ticks;
        public int Calls;
        public long Worst;
    }

    private static readonly Dictionary<string, Probe> _probes = new();
    private static readonly Stopwatch _clock = Stopwatch.StartNew();
    private static long _lastReport;

    public static long Start() => Enabled ? Stopwatch.GetTimestamp() : 0;

    public static void Stop(string name, long start)
    {
        if (!Enabled) return;
        long elapsed = Stopwatch.GetTimestamp() - start;
        lock (_probes)
        {
            if (!_probes.TryGetValue(name, out var probe)) _probes[name] = probe = new Probe();
            probe.Ticks += elapsed;
            probe.Calls++;
            if (elapsed > probe.Worst) probe.Worst = elapsed;
        }
    }

    /// <summary>Call once per frame.</summary>
    public static void Report()
    {
        if (!Enabled) return;
        long now = _clock.ElapsedMilliseconds;
        if (now - _lastReport < 2000) return;
        double seconds = (now - _lastReport) / 1000.0;
        _lastReport = now;

        lock (_probes)
        {
            var parts = new List<string>();
            foreach (var (name, probe) in _probes.OrderBy(p => p.Key))
            {
                if (probe.Calls == 0) continue;
                double average = probe.Ticks * 1000.0 / Stopwatch.Frequency / probe.Calls;
                double worst = probe.Worst * 1000.0 / Stopwatch.Frequency;
                parts.Add($"{name} {average:F2}ms (max {worst:F1}, {probe.Calls / seconds:F0}/s)");
                probe.Ticks = 0;
                probe.Calls = 0;
                probe.Worst = 0;
            }
            Console.WriteLine("[perf] " + string.Join(" | ", parts));
        }
    }
}
