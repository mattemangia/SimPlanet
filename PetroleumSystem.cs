namespace SimPlanet;

public enum TrapType
{
    Anticline,      // Folded strata arch over the reservoir
    FaultTrap,      // A fault seals the reservoir against impermeable rock
    SaltDome,       // Rising salt bends and pierces the strata
    Stratigraphic   // A porous lens pinches out inside tight rock
}

/// <summary>An oil and/or gas accumulation spanning several cells.</summary>
public sealed class Reservoir
{
    public int Id;
    public List<(int x, int y)> Cells = new();
    public int CenterX, CenterY;
    public bool Offshore;
    public bool HasOil, HasGas;
    public float DepthKm;           // Depth of the reservoir top below the surface / sea floor
    public float ThicknessKm;
    public TrapType Trap;
    public float InitialReserves;
    public bool Surveyed;           // A reflection survey has imaged it
    public bool Discovered;         // A well has found hydrocarbons
    public int DiscoveredBy = -1;
    public bool Depleted;
}

public enum WellStatus
{
    Drilling,
    Producing,
    Dry,
    Depleted
}

/// <summary>A well on land, or a platform at sea.</summary>
public sealed class OilWell
{
    public int Id;
    public int X, Y;
    public int CivId;
    public int ReservoirId = -1;
    public bool Offshore;
    public float TargetDepthKm;
    public float Progress;          // Drilling progress 0-1
    public WellStatus Status;
    public int YearSpudded;
    public float Production;        // Last year's output
    public float Cumulative;
}

/// <summary>A reflection seismic line shot across a prospect.</summary>
public sealed class SeismicSurvey
{
    public int Id;
    public int CivId;
    public int ReservoirId;
    public float X1, Y1, X2, Y2;    // Line ends in cell coordinates (X may run past the map edge: it wraps)
    public bool Marine;
    public float Progress;          // 0-1 along the line
    public int YearStarted;
    public bool Completed;

    // Stacked section: Traces x Samples amplitudes, trace after trace
    public int Traces, Samples;
    public float[] Section = Array.Empty<float>();
    public float TwoWayTimeSeconds; // Time at the bottom of the section
}

/// <summary>
/// Hydrocarbon exploration and production. Reservoirs sit in sedimentary basins; industrial
/// nations shoot reflection seismic lines across prospects, drill wells on what the sections
/// show (platforms at sea), and pump the fields until they run dry.
/// </summary>
public sealed class PetroleumSystem
{
    private readonly PlanetMap _map;
    private readonly Random _random;
    private int[,] _reservoirAt;
    private int _nextWellId = 1, _nextSurveyId = 1;
    private int _lastYear = int.MinValue;

    public List<Reservoir> Reservoirs { get; } = new();
    public List<OilWell> Wells { get; } = new();
    public List<SeismicSurvey> Surveys { get; } = new();

    /// <summary>Hook for the world chronicle (year, text, x, y, civ id).</summary>
    public Action<int, string, int, int, int>? Chronicle;

    public const int SectionTraces = 120;
    public const int SectionSamples = 256;

    public PetroleumSystem(PlanetMap map, int seed)
    {
        _map = map;
        _random = new Random(seed + 777);
        _reservoirAt = new int[map.Width, map.Height];
        BuildReservoirs();
    }

    public Reservoir? GetReservoirAt(int x, int y)
    {
        if (x < 0 || y < 0 || x >= _map.Width || y >= _map.Height) return null;
        int id = _reservoirAt[x, y];
        return id > 0 ? Reservoirs[id - 1] : null;
    }

    public OilWell? GetWellAt(int x, int y, int radius = 0)
    {
        OilWell? best = null;
        int bestDist = int.MaxValue;
        foreach (var well in Wells)
        {
            int dx = Math.Abs(well.X - x);
            dx = Math.Min(dx, _map.Width - dx);
            int d = Math.Max(dx, Math.Abs(well.Y - y));
            if (d <= radius && d < bestDist)
            {
                best = well;
                bestDist = d;
            }
        }
        return best;
    }

    private static bool IsHydrocarbon(ResourceDeposit d) =>
        (d.Type == ResourceType.Oil || d.Type == ResourceType.NaturalGas) && d.Amount > 0.0005f;

    /// <summary>
    /// Groups oil and gas deposits into fields. Safe to call again after loading a game.
    /// </summary>
    public void BuildReservoirs()
    {
        int w = _map.Width, h = _map.Height;
        Reservoirs.Clear();
        _reservoirAt = new int[w, h];
        var stack = new Stack<(int x, int y)>();

        for (int sy = 0; sy < h; sy++)
        {
            for (int sx = 0; sx < w; sx++)
            {
                if (_reservoirAt[sx, sy] != 0 || !_map.Cells[sx, sy].Resources.Exists(IsHydrocarbon)) continue;

                var reservoir = new Reservoir { Id = Reservoirs.Count };
                Reservoirs.Add(reservoir);
                _reservoirAt[sx, sy] = reservoir.Id + 1;
                stack.Push((sx, sy));

                while (stack.Count > 0)
                {
                    var (cx, cy) = stack.Pop();
                    reservoir.Cells.Add((cx, cy));
                    for (int dy = -2; dy <= 2; dy++)
                    {
                        int ny = cy + dy;
                        if (ny < 0 || ny >= h) continue;
                        for (int dx = -2; dx <= 2; dx++)
                        {
                            int nx = (cx + dx + w) % w;
                            if (_reservoirAt[nx, ny] != 0 || !_map.Cells[nx, ny].Resources.Exists(IsHydrocarbon)) continue;
                            _reservoirAt[nx, ny] = reservoir.Id + 1;
                            stack.Push((nx, ny));
                        }
                    }
                }
            }
        }

        foreach (var reservoir in Reservoirs)
        {
            var rnd = new Random(reservoir.Cells[0].x * 7919 + reservoir.Cells[0].y * 104729);

            // A field is wider than a single cell: the accumulation thins out towards its edges
            if (reservoir.Cells.Count < 5)
            {
                var (cx, cy) = reservoir.Cells[0];
                var seed = _map.Cells[cx, cy].Resources.First(IsHydrocarbon);
                int radius = 1 + rnd.Next(2);
                for (int dy = -radius; dy <= radius; dy++)
                {
                    int ny = cy + dy;
                    if (ny < 1 || ny >= h - 1) continue;
                    for (int dx = -radius; dx <= radius; dx++)
                    {
                        int nx = (cx + dx + w) % w;
                        if (_reservoirAt[nx, ny] != 0) continue;
                        float dist = MathF.Sqrt(dx * dx + dy * dy);
                        if (dist > radius + 0.3f || rnd.NextDouble() < 0.2) continue;
                        var cell = _map.Cells[nx, ny];
                        if (cell.Elevation > 0.35f) continue; // Basins, not mountains
                        cell.AddResource(new ResourceDeposit(seed.Type, seed.Amount * (0.6f - 0.15f * dist), seed.Concentration, seed.Depth));
                        _reservoirAt[nx, ny] = reservoir.Id + 1;
                        reservoir.Cells.Add((nx, ny));
                    }
                }
            }

            // Centre (taking the wrap in x into account), contents and geometry
            float ax = 0, ay = 0, depth = 0;
            int refX = reservoir.Cells[0].x;
            bool nearFault = false;
            int water = 0;
            foreach (var (x, y) in reservoir.Cells)
            {
                int rx = x - refX;
                if (rx > w / 2) rx -= w;
                if (rx < -w / 2) rx += w;
                ax += rx;
                ay += y;
                var cell = _map.Cells[x, y];
                if (cell.IsWater) water++;
                if (cell.Geology.IsFault) nearFault = true;
                foreach (var d in cell.Resources)
                {
                    if (!IsHydrocarbon(d)) continue;
                    reservoir.InitialReserves += d.Amount;
                    depth = MathF.Max(depth, d.Depth);
                    if (d.Type == ResourceType.Oil) reservoir.HasOil = true; else reservoir.HasGas = true;
                }
            }
            reservoir.CenterX = (((int)MathF.Round(ax / reservoir.Cells.Count) + refX) % w + w) % w;
            reservoir.CenterY = (int)MathF.Round(ay / reservoir.Cells.Count);
            reservoir.Offshore = water * 2 > reservoir.Cells.Count;
            reservoir.DepthKm = 1.2f + depth * 3.3f;
            reservoir.ThicknessKm = 0.15f + 0.25f * (float)rnd.NextDouble();
            reservoir.Trap = nearFault ? TrapType.FaultTrap : (TrapType)(new[] { 0, 0, 2, 3 }[rnd.Next(4)]);
        }
    }

    public float RemainingReserves(Reservoir reservoir)
    {
        float total = 0f;
        foreach (var (x, y) in reservoir.Cells)
        {
            foreach (var d in _map.Cells[x, y].Resources)
            {
                if (IsHydrocarbon(d)) total += d.Amount;
            }
        }
        return total;
    }

    // ---------------------------------------------------------------------------------------
    // Simulation
    // ---------------------------------------------------------------------------------------

    public void Update(float deltaTime, int year, IReadOnlyList<Civilization> civilizations)
    {
        // Crews and rigs work continuously
        foreach (var survey in Surveys)
        {
            if (survey.Completed) continue;
            survey.Progress += deltaTime / 25f; // About two and a half years per line
            if (survey.Progress >= 1f) CompleteSurvey(survey, year);
        }
        foreach (var well in Wells)
        {
            if (well.Status != WellStatus.Drilling) continue;
            well.Progress += deltaTime / (8f + 4f * well.TargetDepthKm);
            if (well.Progress >= 1f) CompleteWell(well, year, civilizations);
        }

        if (year == _lastYear) return;
        _lastYear = year;

        // Wells and surveys of vanished nations are abandoned; old dry holes are forgotten
        Wells.RemoveAll(wl => civilizations.All(c => c.Id != wl.CivId) ||
                              ((wl.Status == WellStatus.Dry || wl.Status == WellStatus.Depleted) && year - wl.YearSpudded > 60));
        Surveys.RemoveAll(s => civilizations.All(c => c.Id != s.CivId) || (s.Completed && year - s.YearStarted > 80));

        foreach (var civ in civilizations)
        {
            if (civ.CivType < CivType.Industrial) continue;
            Produce(civ);
            Explore(civ, year);
        }
    }

    private void Produce(Civilization civ)
    {
        float tech = civ.CivType switch
        {
            CivType.Industrial => 1f,
            CivType.Scientific => 1.8f,
            _ => 3f
        };

        foreach (var well in Wells)
        {
            if (well.CivId != civ.Id || well.Status != WellStatus.Producing || well.ReservoirId < 0) continue;
            var reservoir = Reservoirs[well.ReservoirId];
            float wanted = 0.004f * tech;
            float got = 0f;

            // Draw down the nearest cells of the field first (pressure drops around the well)
            foreach (var (x, y) in reservoir.Cells.OrderBy(c => Math.Abs(c.x - well.X) + Math.Abs(c.y - well.Y)))
            {
                if (wanted <= 0.0001f) break;
                var cell = _map.Cells[x, y];
                foreach (var deposit in cell.Resources)
                {
                    if (!IsHydrocarbon(deposit)) continue;
                    float before = deposit.Amount;
                    float yield = cell.ExtractResource(deposit.Type, wanted);
                    float taken = before - deposit.Amount;
                    wanted -= taken;
                    got += yield;
                    civ.ResourceStockpile[deposit.Type] = MathF.Min(100f, civ.ResourceStockpile.GetValueOrDefault(deposit.Type) + yield * 30f);
                    civ.AnnualProduction[deposit.Type] = civ.AnnualProduction.GetValueOrDefault(deposit.Type) + yield * 30f;
                }
            }

            well.Production = got;
            well.Cumulative += got;
            _map.Cells[well.X, well.Y].Geology.InducedSeismicity = got > 0f;

            if (got <= 0.00001f || RemainingReserves(reservoir) < reservoir.InitialReserves * 0.03f)
            {
                well.Status = WellStatus.Depleted;
                well.Production = 0f;
                _map.Cells[well.X, well.Y].Geology.InducedSeismicity = false;
                if (!reservoir.Depleted)
                {
                    reservoir.Depleted = true;
                    Chronicle?.Invoke(_lastYear, $"{(well.Offshore ? "An offshore field" : "An oil field")} of {civ.Name} has run dry", well.X, well.Y, civ.Id);
                }
            }
        }
    }

    private void Explore(Civilization civ, int year)
    {
        bool canDrillOffshore = civ.CivType >= CivType.Scientific;
        int activeSurveys = Surveys.Count(s => s.CivId == civ.Id && !s.Completed);
        int drilling = Wells.Count(wl => wl.CivId == civ.Id && wl.Status == WellStatus.Drilling);
        int ownWells = Wells.Count(wl => wl.CivId == civ.Id && wl.Status != WellStatus.Dry && wl.Status != WellStatus.Depleted);
        int maxWells = 3 + civ.TechLevel / 12;

        foreach (var reservoir in Reservoirs)
        {
            if (reservoir.Offshore && !canDrillOffshore) continue;
            if (!InReach(civ, reservoir)) continue;
            if (RemainingReserves(reservoir) < reservoir.InitialReserves * 0.1f) continue;

            if (!reservoir.Surveyed)
            {
                // Geophysics first: a seismic line across the prospect
                if (activeSurveys < (civ.CivType >= CivType.Scientific ? 2 : 1) &&
                    !Surveys.Any(s => s.ReservoirId == reservoir.Id && !s.Completed) &&
                    _random.NextDouble() < 0.35)
                {
                    StartSurvey(civ, reservoir, year);
                    activeSurveys++;
                }
                continue;
            }

            int wellsHere = Wells.Count(wl => wl.ReservoirId == reservoir.Id && wl.Status != WellStatus.Dry && wl.Status != WellStatus.Depleted);
            int wanted = 1 + reservoir.Cells.Count / 6;
            if (wellsHere < wanted && drilling < 2 && ownWells < maxWells && _random.NextDouble() < 0.5)
            {
                SpudWell(civ, reservoir, year);
                drilling++;
                ownWells++;
            }
        }

        // Early oilmen drill on a hunch, and mostly miss
        if (civ.CivType == CivType.Industrial && drilling == 0 && ownWells < 2 && civ.Territory.Count > 0 && _random.NextDouble() < 0.03)
        {
            var (x, y) = civ.Territory.ElementAt(_random.Next(civ.Territory.Count));
            var cell = _map.Cells[x, y];
            if (cell.IsLand && cell.Geology.SedimentaryRock > 0.35f && GetWellAt(x, y, 1) == null)
            {
                var reservoir = GetReservoirAt(x, y);
                Wells.Add(new OilWell
                {
                    Id = _nextWellId++, X = x, Y = y, CivId = civ.Id, ReservoirId = reservoir?.Id ?? -1,
                    TargetDepthKm = reservoir?.DepthKm ?? 1.5f + 2f * (float)_random.NextDouble(),
                    Status = WellStatus.Drilling, YearSpudded = year
                });
            }
        }
    }

    private bool InReach(Civilization civ, Reservoir reservoir)
    {
        int reach = reservoir.Offshore ? 7 : 0;
        foreach (var (x, y) in reservoir.Cells)
        {
            if (civ.Territory.Contains((x, y))) return true;
        }
        if (reach == 0) return false;

        for (int dy = -reach; dy <= reach; dy++)
        {
            int ny = reservoir.CenterY + dy;
            if (ny < 0 || ny >= _map.Height) continue;
            for (int dx = -reach; dx <= reach; dx++)
            {
                int nx = (reservoir.CenterX + dx + _map.Width) % _map.Width;
                if (civ.Territory.Contains((nx, ny))) return true;
            }
        }
        return false;
    }

    private void StartSurvey(Civilization civ, Reservoir reservoir, int year)
    {
        // The line crosses the prospect at a random azimuth, overshooting both flanks
        double angle = _random.NextDouble() * Math.PI;
        float half = 5f + MathF.Sqrt(reservoir.Cells.Count);
        float dx = (float)Math.Cos(angle) * half, dy = (float)Math.Sin(angle) * half * 0.6f;
        var survey = new SeismicSurvey
        {
            Id = _nextSurveyId++, CivId = civ.Id, ReservoirId = reservoir.Id,
            X1 = reservoir.CenterX - dx, Y1 = Math.Clamp(reservoir.CenterY - dy, 2, _map.Height - 3),
            X2 = reservoir.CenterX + dx, Y2 = Math.Clamp(reservoir.CenterY + dy, 2, _map.Height - 3),
            Marine = reservoir.Offshore, YearStarted = year
        };
        Surveys.Add(survey);
        Chronicle?.Invoke(year, $"{civ.Name} begins a {(survey.Marine ? "marine" : "land")} seismic survey in search of oil", reservoir.CenterX, reservoir.CenterY, civ.Id);
    }

    private void CompleteSurvey(SeismicSurvey survey, int year)
    {
        survey.Progress = 1f;
        survey.Completed = true;
        BuildSection(survey);
        Reservoirs[survey.ReservoirId].Surveyed = true;
    }

    private void SpudWell(Civilization civ, Reservoir reservoir, int year)
    {
        // Drill the crest: the richest cell that has no well yet
        (int x, int y) best = (reservoir.CenterX, reservoir.CenterY);
        float bestAmount = -1f;
        foreach (var (x, y) in reservoir.Cells)
        {
            if (GetWellAt(x, y, 1) != null) continue;
            float amount = 0f;
            foreach (var d in _map.Cells[x, y].Resources) if (IsHydrocarbon(d)) amount += d.Amount;
            if (amount > bestAmount)
            {
                bestAmount = amount;
                best = (x, y);
            }
        }
        if (bestAmount < 0f) return;

        bool offshore = _map.Cells[best.x, best.y].IsWater;
        Wells.Add(new OilWell
        {
            Id = _nextWellId++, X = best.x, Y = best.y, CivId = civ.Id, ReservoirId = reservoir.Id,
            Offshore = offshore, TargetDepthKm = reservoir.DepthKm, Status = WellStatus.Drilling, YearSpudded = year
        });
    }

    private void CompleteWell(OilWell well, int year, IReadOnlyList<Civilization> civilizations)
    {
        well.Progress = 1f;
        var civ = civilizations.FirstOrDefault(c => c.Id == well.CivId);
        string who = civ?.Name ?? "A nation";

        if (well.ReservoirId < 0)
        {
            well.Status = WellStatus.Dry;
            return;
        }

        var reservoir = Reservoirs[well.ReservoirId];
        well.Status = WellStatus.Producing;
        if (!reservoir.Discovered)
        {
            reservoir.Discovered = true;
            reservoir.DiscoveredBy = well.CivId;
            string what = reservoir.HasOil && reservoir.HasGas ? "oil and gas" : reservoir.HasOil ? "oil" : "natural gas";
            Chronicle?.Invoke(year, $"{who} strikes {what} {(well.Offshore ? "offshore " : "")}at {reservoir.DepthKm:F1} km depth", well.X, well.Y, well.CivId);
        }
        foreach (var (x, y) in reservoir.Cells)
        {
            foreach (var d in _map.Cells[x, y].Resources) if (IsHydrocarbon(d)) d.Discovered = true;
        }
    }

    // ---------------------------------------------------------------------------------------
    // Synthetic reflection seismics
    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// Builds the stacked section of a survey from the simulated subsurface. Each trace is the
    /// acoustic impedance of the column under that point (see <see cref="Stratigraphy"/>),
    /// turned into reflection coefficients in two-way time and convolved with the source
    /// wavelet. The same column is what the 3D sections draw and what a well logs.
    /// </summary>
    public void BuildSection(SeismicSurvey survey)
    {
        int traces = SectionTraces, samples = SectionSamples;
        const float totalTime = 4.0f; // Seconds two-way time
        float dt = totalTime / samples;
        survey.Traces = traces;
        survey.Samples = samples;
        survey.TwoWayTimeSeconds = totalTime;
        survey.Section = new float[traces * samples];

        // 14 Hz Ricker wavelet
        var wavelet = new float[41];
        for (int k = 0; k < wavelet.Length; k++)
        {
            float t = (k - wavelet.Length / 2) * dt;
            float a = MathF.PI * 14f * t;
            wavelet[k] = (1f - 2f * a * a) * MathF.Exp(-a * a);
        }

        var reflectivity = new float[samples];
        var trace = new float[samples];
        var noise = new Random(survey.Id * 31 + 5);
        var column = new StrataColumn();
        const float dz = 0.005f; // 5 m depth steps

        for (int tr = 0; tr < traces; tr++)
        {
            float f = tr / (float)(traces - 1);
            float fx = survey.X1 + (survey.X2 - survey.X1) * f;
            float fy = survey.Y1 + (survey.Y2 - survey.Y1) * f;
            Stratigraphy.Build(_map, this, fx, fy, column);
            Array.Clear(reflectivity);

            // Water (or the weathered layer on land), then down through the column
            float time = 2f * column.WaterKm / 1.5f;
            float impedance = column.WaterKm > 0 ? 1.5f * 1.03f : 1.8f * 1.9f;
            float maxDepth = column.SedimentKm + 0.3f;
            for (float depth = 0f; depth < maxDepth && time < totalTime; depth += dz)
            {
                var rock = column.Sample(depth);
                float z = rock.Velocity * rock.Density;
                if (MathF.Abs(z - impedance) > 0.0005f)
                {
                    AddReflector(reflectivity, time, dt, (z - impedance) / (z + impedance));
                    impedance = z;
                }
                time += 2f * dz / rock.Velocity;
            }

            ComputeBackend.Convolve(reflectivity, wavelet, trace);

            // Field noise grows with time as the signal weakens with depth
            for (int s = 0; s < samples; s++)
            {
                float n = ((float)noise.NextDouble() - 0.5f) * (0.012f + 0.03f * s / samples);
                survey.Section[tr * samples + s] = trace[s] + n;
            }
        }
    }

    private static void AddReflector(float[] reflectivity, float time, float dt, float coefficient)
    {
        float position = time / dt;
        int i = (int)MathF.Floor(position);
        float frac = position - i;
        if (i >= 0 && i < reflectivity.Length) reflectivity[i] += coefficient * (1f - frac);
        if (i + 1 >= 0 && i + 1 < reflectivity.Length) reflectivity[i + 1] += coefficient * frac;
    }
}
