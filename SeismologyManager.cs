namespace SimPlanet;

/// <summary>A seismograph station run by a civilization.</summary>
public sealed class SeismicStation
{
    public int X;
    public int Y;
    public int CivId;
    public int YearBuilt;

    // Recording of the event in progress (or the last one)
    public float[] TraceP = Array.Empty<float>();
    public float[] TraceS = Array.Empty<float>();
    public int Samples;
    public float PickP = -1f;     // Seconds after origin time; -1 = not detected
    public float PickS = -1f;
    public float DistanceKm;      // True epicentral distance of the event being recorded
}

/// <summary>What the network made of one earthquake.</summary>
public sealed class SeismicRecord
{
    public int X, Y, Year;
    public float Magnitude;
    public int StationsP, StationsS;
    public int LocatedX = -1, LocatedY = -1;
    public float LocationErrorKm = -1f;
    public float DurationSeconds;
}

/// <summary>
/// Seismology: propagates P and S waves from earthquakes through a crust whose wave speed
/// follows the simulated geology, lets civilizations build seismograph networks that record the
/// arrivals, locates the events from S-P times and inverts the P travel times into a
/// tomographic image of the crust.
/// </summary>
public sealed class SeismologyManager : IDisposable
{
    private readonly PlanetMap _map;
    private readonly Random _random = new(4242);

    public List<SeismicStation> Stations { get; } = new();
    public List<SeismicRecord> Records { get; } = new();

    public float CellKm { get; }
    public const float MaxSpeedKmS = 8.5f;
    private const float CourantMax = 0.6f;
    private const int StepsPerSample = 2;
    public const float NoiseFloor = 0.004f;

    /// <summary>Seconds of wave travel per recorded sample.</summary>
    public float SampleSeconds { get; }

    // True crustal P velocity (km/s) per cell, rebuilt for every event
    public float[] VelocityP { get; private set; }
    public float[] VelocityS { get; private set; }

    // Wave field of the event in progress
    private SeismicWaveSolver? _solverP, _solverS;
    public bool IsPropagating { get; private set; }
    public float[]? FieldP => _solverP?.Field;
    public float[]? FieldS => _solverS?.Field;
    public int EventX { get; private set; }
    public int EventY { get; private set; }
    public float EventMagnitude { get; private set; }
    public int EventYear { get; private set; }
    public float EventAmplitude { get; private set; }
    public float ElapsedSeconds { get; private set; }
    public long EventSerial { get; private set; }
    private int _samples, _maxSamples;
    private float _stepDebt;
    private float _stepsPerRealSecond;
    private long _lastGeoEventId;
    private GeoEvent? _pending;

    // Tomography (coarse grid)
    public const int TomoBlock = 4;
    public int TomoWidth { get; }
    public int TomoHeight { get; }
    public float[] TomoVelocity { get; }   // Recovered P velocity (km/s)
    public float[] TomoCoverage { get; }   // Ray length through each block (cells)
    public float TomoReference { get; private set; } = 6.6f;
    public int TomoRays => _observations.Count;
    public float TomoCorrelation { get; private set; }
    private readonly List<(int sx, int sy, int rx, int ry, float time)> _observations = new();
    private const int MaxObservations = 600;

    public ComputeKind Kind => _solverP?.Kind ?? ComputeBackend.Preferred;

    public SeismologyManager(PlanetMap map)
    {
        _map = map;
        ComputeBackend.Initialize();
        CellKm = 40000f / map.Width;
        SampleSeconds = StepsPerSample * CourantMax * CellKm / MaxSpeedKmS;
        VelocityP = new float[map.Width * map.Height];
        VelocityS = new float[map.Width * map.Height];
        TomoWidth = (map.Width + TomoBlock - 1) / TomoBlock;
        TomoHeight = (map.Height + TomoBlock - 1) / TomoBlock;
        TomoVelocity = new float[TomoWidth * TomoHeight];
        TomoCoverage = new float[TomoWidth * TomoHeight];
        BuildVelocityModel();
    }

    // ---------------------------------------------------------------------------------------
    // Earth model
    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// Crustal wave speeds from the geology: fast in old cold cratons and oceanic lithosphere,
    /// slow in sedimentary basins, rifts, ridges and around magma.
    /// </summary>
    public void BuildVelocityModel()
    {
        int w = _map.Width, h = _map.Height;
        var raw = new float[w * h];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                var geo = _map.Cells[x, y].Geology;
                float v = geo.CrustType switch
                {
                    CrustType.Oceanic => 7.2f,       // Thin crust: the path is mostly upper mantle
                    CrustType.Transitional => 6.6f,
                    _ => 6.2f
                };
                if (geo.CrustType == CrustType.Continental && geo.CrustAge > 2000) v += 0.35f; // Craton
                v -= 0.9f * Math.Clamp(geo.SedimentLayer / 4f + geo.SedimentaryRock / 6f, 0f, 1f); // Basins
                v -= 0.3f * Math.Clamp((geo.CrustThickness - 40f) / 30f, 0f, 1f);                 // Thick orogenic roots
                if (geo.IsVolcano) v -= 0.5f + 0.9f * Math.Clamp(geo.VolcanicActivity, 0f, 1f);   // Magma and heat
                if (geo.BoundaryType == PlateBoundaryType.Divergent) v -= 0.7f;                   // Hot upwelling
                if (geo.IsSubducting) v += 0.5f;                                                  // Cold slab
                raw[y * w + x] = v;
            }
        }

        // Heat and composition vary smoothly: blur so anomalies spread over neighbouring cells
        for (int pass = 0; pass < 2; pass++)
        {
            for (int y = 0; y < h; y++)
            {
                int ya = Math.Max(0, y - 1), yb = Math.Min(h - 1, y + 1);
                for (int x = 0; x < w; x++)
                {
                    int xl = (x + w - 1) % w, xr = (x + 1) % w;
                    VelocityP[y * w + x] = (raw[y * w + x] * 4f + raw[y * w + xl] + raw[y * w + xr] + raw[ya * w + x] + raw[yb * w + x]) / 8f;
                }
            }
            Array.Copy(VelocityP, raw, raw.Length);
        }

        for (int i = 0; i < VelocityP.Length; i++)
        {
            VelocityP[i] = Math.Clamp(VelocityP[i], 4.2f, 8.2f);
            VelocityS[i] = VelocityP[i] / 1.732f;
        }
    }

    // ---------------------------------------------------------------------------------------
    // Networks
    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// Once a year: scientific nations install seismographs in their cities.
    /// </summary>
    public void UpdateNetworks(IReadOnlyList<Civilization> civilizations, int year)
    {
        // Stations of nations that no longer exist, or whose city fell silent, shut down
        Stations.RemoveAll(s =>
        {
            var owner = civilizations.FirstOrDefault(c => c.Id == s.CivId);
            return owner == null || !owner.Cities.Any(c => c.X == s.X && c.Y == s.Y);
        });

        foreach (var civ in civilizations)
        {
            if (civ.TechLevel < StationTechLevel) continue;

            // One instrument per city, starting from the largest; a couple of new ones each year
            int built = 0;
            foreach (var city in civ.Cities.OrderByDescending(c => c.Population))
            {
                if (built >= 2 || Stations.Count >= MaxStations) break;
                if (Stations.Any(s => Math.Abs(s.X - city.X) <= 2 && Math.Abs(s.Y - city.Y) <= 2)) continue;
                Stations.Add(new SeismicStation { X = city.X, Y = city.Y, CivId = civ.Id, YearBuilt = year });
                built++;
            }
        }
    }

    public const int StationTechLevel = 40;
    public const int MaxStations = 96;

    public void AddStation(int x, int y, int civId, int year)
    {
        if (y < 1 || y >= _map.Height - 1) return;
        Stations.Add(new SeismicStation { X = x, Y = y, CivId = civId, YearBuilt = year });
    }

    // ---------------------------------------------------------------------------------------
    // Wave propagation
    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// Called every frame with real elapsed time. Picks up new earthquakes and impacts and
    /// advances the wave field of the event in progress.
    /// </summary>
    public void Advance(float realSeconds)
    {
        foreach (var e in GeoEvents.Snapshot())
        {
            if (e.Id <= _lastGeoEventId) continue;
            _lastGeoEventId = e.Id;
            if (e.Kind == GeoEventKind.Eruption) continue;

            float magnitude = e.Kind == GeoEventKind.Impact ? 6.5f + 0.5f * e.Magnitude : e.Magnitude;
            if (magnitude < 5.0f) continue;
            if (_pending == null || magnitude >= PendingMagnitude(_pending)) _pending = e;
        }

        if (!IsPropagating && _pending != null)
        {
            var e = _pending;
            _pending = null;
            BeginEvent(e.X, e.Y, PendingMagnitude(e), e.Year);
        }

        if (!IsPropagating) return;

        _stepDebt += Math.Min(realSeconds, 0.1f) * _stepsPerRealSecond;
        int batches = (int)(_stepDebt / StepsPerSample);
        if (batches <= 0) return;
        _stepDebt -= batches * StepsPerSample;
        for (int b = 0; b < batches && IsPropagating; b++) StepSample();
    }

    private static float PendingMagnitude(GeoEvent e) =>
        e.Kind == GeoEventKind.Impact ? 6.5f + 0.5f * e.Magnitude : e.Magnitude;

    /// <summary>Runs the event in progress to its end at once (tests and fast-forward).</summary>
    public void RunToCompletion()
    {
        while (IsPropagating) StepSample();
    }

    public void BeginEvent(int x, int y, float magnitude, int year)
    {
        BuildVelocityModel();

        int w = _map.Width, h = _map.Height;
        var courantP = new float[w * h];
        var courantS = new float[w * h];
        for (int i = 0; i < courantP.Length; i++)
        {
            courantP[i] = VelocityP[i] / MaxSpeedKmS * CourantMax;
            courantS[i] = VelocityS[i] / MaxSpeedKmS * CourantMax;
        }

        _solverP?.Dispose();
        _solverS?.Dispose();
        _solverP = new SeismicWaveSolver(w, h, courantP, ComputeBackend.Preferred);
        _solverS = new SeismicWaveSolver(w, h, courantS, ComputeBackend.Preferred);

        EventX = x;
        EventY = Math.Clamp(y, 2, h - 3);
        EventMagnitude = magnitude;
        EventYear = year;
        EventAmplitude = MathF.Pow(10f, 0.6f * (magnitude - 5f));
        ElapsedSeconds = 0f;
        EventSerial++;
        _samples = 0;
        _stepDebt = 0f;

        // Long enough for the S wave of a great earthquake to cross half the planet; small
        // shocks fade into the noise sooner
        float reachCells = Math.Clamp(w * 0.5f * (0.35f + 0.65f * (magnitude - 5f) / 3f), w * 0.12f, w * 0.5f);
        float sCellsPerStep = 3.6f / MaxSpeedKmS * CourantMax;
        _maxSamples = (int)(reachCells / sCellsPerStep / StepsPerSample) + 8;
        _stepsPerRealSecond = _maxSamples * StepsPerSample / 14f; // The whole event plays in about 14 s

        foreach (var station in Stations)
        {
            if (station.TraceP.Length < _maxSamples)
            {
                station.TraceP = new float[_maxSamples];
                station.TraceS = new float[_maxSamples];
            }
            else
            {
                Array.Clear(station.TraceP);
                Array.Clear(station.TraceS);
            }
            station.Samples = 0;
            station.PickP = station.PickS = -1f;
            station.DistanceKm = DistanceCells(x, EventY, station.X, station.Y) * CellKm;
        }

        IsPropagating = true;
    }

    private void StepSample()
    {
        if (_solverP == null || _solverS == null) return;

        // Source time function: a short Ricker pulse; S carries more energy than P
        const int sourceSamples = 8;
        if (_samples < sourceSamples)
        {
            float t = (_samples - sourceSamples * 0.5f) / (sourceSamples * 0.22f);
            float ricker = (1f - 2f * t * t) * MathF.Exp(-t * t);
            _solverP.Inject(EventX, EventY, EventAmplitude * ricker);
            _solverS.Inject(EventX, EventY, EventAmplitude * 1.6f * ricker);
        }

        _solverP.Advance(StepsPerSample);
        _solverS.Advance(StepsPerSample);
        _samples++;
        ElapsedSeconds = _samples * SampleSeconds;

        var fieldP = _solverP.Field;
        var fieldS = _solverS.Field;
        int w = _map.Width;
        foreach (var station in Stations)
        {
            if (station.TraceP.Length < _samples) continue;
            int i = station.Y * w + station.X;
            float p = fieldP[i], s = fieldS[i];
            station.TraceP[_samples - 1] = p;
            station.TraceS[_samples - 1] = s;
            station.Samples = _samples;

            // An arrival is picked when the ground motion first rises clearly above the noise
            if (station.PickP < 0 && MathF.Abs(p) > NoiseFloor * 3f) station.PickP = ElapsedSeconds;
            if (station.PickS < 0 && MathF.Abs(s) > NoiseFloor * 3f) station.PickS = ElapsedSeconds;
        }

        if (_samples >= _maxSamples) FinishEvent();
    }

    private void FinishEvent()
    {
        IsPropagating = false;

        var record = new SeismicRecord
        {
            X = EventX, Y = EventY, Year = EventYear, Magnitude = EventMagnitude,
            DurationSeconds = ElapsedSeconds,
            StationsP = Stations.Count(s => s.PickP >= 0),
            StationsS = Stations.Count(s => s.PickS >= 0)
        };

        if (Stations.Count == 0) return;

        Locate(record);

        foreach (var station in Stations)
        {
            if (station.PickP < 0) continue;
            if (DistanceCells(EventX, EventY, station.X, station.Y) < 4f) continue; // Too close to say anything about the path
            _observations.Add((EventX, EventY, station.X, station.Y, station.PickP));
        }
        if (_observations.Count > MaxObservations)
        {
            _observations.RemoveRange(0, _observations.Count - MaxObservations);
        }

        Records.Add(record);
        if (Records.Count > 40) Records.RemoveAt(0);

        if (_observations.Count >= 8) RunTomography();
    }

    /// <summary>Shortest distance in cells on the cylinder (the map wraps in x).</summary>
    public float DistanceCells(int x1, int y1, int x2, int y2)
    {
        float dx = MathF.Abs(x1 - x2);
        dx = MathF.Min(dx, _map.Width - dx);
        float dy = y1 - y2;
        return MathF.Sqrt(dx * dx + dy * dy);
    }

    /// <summary>
    /// Epicentre from S-P times: each station's delay gives a distance, and the epicentre is
    /// where those circles meet best.
    /// </summary>
    private void Locate(SeismicRecord record)
    {
        var ranged = Stations.Where(s => s.PickP >= 0 && s.PickS > s.PickP).ToList();
        if (ranged.Count < 3) return;

        const float vp = 6.6f, vs = vp / 1.732f;
        float kmPerSecond = vp * vs / (vp - vs);
        float best = float.MaxValue;
        for (int y = 2; y < _map.Height - 2; y += 2)
        {
            for (int x = 0; x < _map.Width; x += 2)
            {
                float error = 0f;
                foreach (var s in ranged)
                {
                    float d = DistanceCells(x, y, s.X, s.Y) * CellKm;
                    float expected = (s.PickS - s.PickP) * kmPerSecond;
                    float diff = d - expected;
                    error += diff * diff;
                }
                if (error < best)
                {
                    best = error;
                    record.LocatedX = x;
                    record.LocatedY = y;
                }
            }
        }
        record.LocationErrorKm = DistanceCells(record.LocatedX, record.LocatedY, record.X, record.Y) * CellKm;
    }

    // ---------------------------------------------------------------------------------------
    // Travel-time tomography
    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// Inverts the recorded P travel times for crustal velocity (straight rays, SIRT).
    /// Only blocks crossed by rays are resolved, so the image sharpens as networks and
    /// earthquakes accumulate.
    /// </summary>
    public void RunTomography()
    {
        int blocks = TomoWidth * TomoHeight;
        Array.Clear(TomoCoverage);
        if (_observations.Count == 0)
        {
            Array.Clear(TomoVelocity);
            return;
        }

        // Ray geometry: the length of each ray inside each block (km)
        var rayBlocks = new List<int[]>(_observations.Count);
        var rayLengths = new List<float[]>(_observations.Count);
        var rayTimes = new List<float>(_observations.Count);
        var scratch = new Dictionary<int, float>();
        float totalKm = 0f, totalTime = 0f;

        foreach (var (sx, sy, rx, ry, time) in _observations)
        {
            float dx = rx - sx;
            if (dx > _map.Width / 2f) dx -= _map.Width;
            if (dx < -_map.Width / 2f) dx += _map.Width;
            float dy = ry - sy;
            float lengthCells = MathF.Sqrt(dx * dx + dy * dy);
            int samples = Math.Max(2, (int)(lengthCells * 2f));
            float segmentKm = lengthCells * CellKm / samples;

            scratch.Clear();
            for (int k = 0; k < samples; k++)
            {
                float t = (k + 0.5f) / samples;
                int cx = (int)MathF.Floor(sx + dx * t);
                cx = ((cx % _map.Width) + _map.Width) % _map.Width;
                int cy = Math.Clamp((int)MathF.Floor(sy + dy * t), 0, _map.Height - 1);
                int block = (cy / TomoBlock) * TomoWidth + cx / TomoBlock;
                scratch[block] = scratch.GetValueOrDefault(block) + segmentKm;
            }

            rayBlocks.Add(scratch.Keys.ToArray());
            rayLengths.Add(scratch.Values.ToArray());
            rayTimes.Add(time);
            totalKm += lengthCells * CellKm;
            totalTime += time;
        }

        TomoReference = Math.Clamp(totalKm / MathF.Max(1f, totalTime), 4.5f, 8.2f);
        var slowness = new float[blocks];
        Array.Fill(slowness, 1f / TomoReference);
        var correction = new float[blocks];
        var weight = new float[blocks];

        for (int r = 0; r < rayBlocks.Count; r++)
        {
            var ids = rayBlocks[r];
            var len = rayLengths[r];
            for (int k = 0; k < ids.Length; k++) TomoCoverage[ids[k]] += len[k] / CellKm;
        }

        for (int iteration = 0; iteration < 40; iteration++)
        {
            Array.Clear(correction);
            Array.Clear(weight);
            for (int r = 0; r < rayBlocks.Count; r++)
            {
                var ids = rayBlocks[r];
                var len = rayLengths[r];
                float predicted = 0f, norm = 0f;
                for (int k = 0; k < ids.Length; k++)
                {
                    predicted += len[k] * slowness[ids[k]];
                    norm += len[k] * len[k];
                }
                float residual = (rayTimes[r] - predicted) / MathF.Max(norm, 1e-6f);
                for (int k = 0; k < ids.Length; k++)
                {
                    correction[ids[k]] += residual * len[k] * len[k];
                    weight[ids[k]] += len[k];
                }
            }
            for (int b = 0; b < blocks; b++)
            {
                if (weight[b] <= 0f) continue;
                slowness[b] += 0.6f * correction[b] / weight[b];
                slowness[b] = Math.Clamp(slowness[b], 1f / 9f, 1f / 3.5f);
            }
        }

        for (int b = 0; b < blocks; b++) TomoVelocity[b] = TomoCoverage[b] > 0f ? 1f / slowness[b] : 0f;

        TomoCorrelation = CorrelateWithTruth();
    }

    /// <summary>How well the image matches the real crust, over the blocks that rays resolve.</summary>
    private float CorrelateWithTruth()
    {
        int w = _map.Width, h = _map.Height;
        double sa = 0, sb = 0, saa = 0, sbb = 0, sab = 0;
        int n = 0;
        for (int by = 0; by < TomoHeight; by++)
        {
            for (int bx = 0; bx < TomoWidth; bx++)
            {
                int b = by * TomoWidth + bx;
                if (TomoCoverage[b] < TomoBlock * 3f) continue;
                float truth = 0f;
                int count = 0;
                for (int y = by * TomoBlock; y < Math.Min(h, (by + 1) * TomoBlock); y++)
                {
                    for (int x = bx * TomoBlock; x < Math.Min(w, (bx + 1) * TomoBlock); x++)
                    {
                        truth += VelocityP[y * w + x];
                        count++;
                    }
                }
                truth /= Math.Max(1, count);
                float image = TomoVelocity[b];
                sa += truth; sb += image; saa += truth * truth; sbb += image * image; sab += truth * image;
                n++;
            }
        }
        if (n < 4) return 0f;
        double cov = sab / n - sa / n * (sb / n);
        double va = saa / n - sa / n * (sa / n), vb = sbb / n - sb / n * (sb / n);
        return va <= 1e-9 || vb <= 1e-9 ? 0f : (float)(cov / Math.Sqrt(va * vb));
    }

    public void Dispose()
    {
        _solverP?.Dispose();
        _solverS?.Dispose();
        _solverP = _solverS = null;
    }
}
