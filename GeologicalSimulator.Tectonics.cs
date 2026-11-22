namespace SimPlanet;

/// <summary>
/// Plate boundary classification, mountain building, subduction, rifting, erosion and
/// sediment transport.
///
/// Boundaries are classified from the relative motion of the two plates alone, so the result
/// does not depend on the simulation speed. Each boundary then drives a belt of cells towards an
/// isostatic limit instead of pushing a single row of cells up or down without bound.
/// </summary>
public partial class GeologicalSimulator
{
    private enum TectonicMode : byte
    {
        None,
        Foredeep,    // Crust flexed down by the weight of a mountain belt: a deep trough of sediment at its foot
        Rift,        // Continental crust pulled apart: subsiding graben
        Ridge,       // Mid-ocean ridge: new crust rises to ridge depth
        Trench,      // Subducting slab bends down
        IslandArc,   // Oceanic plate overriding another oceanic plate
        Orogen,      // Continental margin above a subducting slab (Andes)
        Collision    // Continent against continent (Himalaya)
    }

    private TectonicMode[,] _tectMode = new TectonicMode[0, 0];
    private float[,] _tectWeight = new float[0, 0];
    private float[,] _magmaSupply = new float[0, 0];
    private float[,] _depositAcc = new float[0, 0];
    private float[,] _erodeAcc = new float[0, 0];
    private float _aerosol;

    private const float UpliftSpeed = 0.003f;       // Elevation units per second at full strength
    private const float OrogenLimit = 0.95f;        // Isostatic ceiling for subduction ranges
    private const float CollisionLimit = 1.15f;     // Collision ranges stand higher
    private const float TrenchFloor = -0.95f;
    private const float RidgeCrest = -0.30f;
    private const float RiftFloor = -0.20f;
    private const float AbyssalDepth = -0.62f;
    private const int MaxColumnLayers = 100;

    private static readonly float[] OrogenProfile = { 0.55f, 0.85f, 1.0f, 0.7f, 0.35f };
    private static readonly float[] CollisionProfile = { 1.0f, 1.0f, 0.85f, 0.65f, 0.45f, 0.25f };

    private void AllocateTectonicFields()
    {
        _tectMode = new TectonicMode[_map.Width, _map.Height];
        _tectWeight = new float[_map.Width, _map.Height];
        _magmaSupply = new float[_map.Width, _map.Height];
        _depositAcc = new float[_map.Width, _map.Height];
        _erodeAcc = new float[_map.Width, _map.Height];
    }

    /// <summary>Plate velocity in cells per (arbitrary) time unit, for views.</summary>
    public (float x, float y) GetPlateVelocity(int plateId)
    {
        if (plateId < 0 || plateId >= _plates.Count) return (0, 0);
        return (_plates[plateId].VelocityX, _plates[plateId].VelocityY);
    }

    /// <summary>Reads plate membership back from the cells (after loading a saved game).</summary>
    public void SyncPlatesFromCells()
    {
        foreach (var plate in _plates) plate.Cells.Clear();
        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                int id = Math.Clamp(_map.Cells[x, y].Geology.PlateId, 0, _plates.Count - 1);
                _plateMap[x, y] = id;
                _plates[id].Cells.Add((x, y));
            }
        }
        ClassifyBoundaries();
    }

    private static void AddLayer(GeologicalData geo, SedimentType type)
    {
        var column = geo.SedimentColumn;
        column.Add(type);
        if (geo.UnconformityBeds >= 0) geo.UnconformityBeds++; // The erosion surface is buried one bed deeper
        if (column.Count > MaxColumnLayers)
        {
            column.RemoveRange(0, column.Count - MaxColumnLayers);
        }
        if (geo.UnconformityBeds > column.Count) geo.UnconformityBeds = -1;
    }

    private static bool IsOceanicCrust(GeologicalData geo) => geo.CrustType == CrustType.Oceanic;

    private static float Hash01(int x, int y)
    {
        unchecked
        {
            uint h = (uint)(x * 374761393 + y * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            return ((h ^ (h >> 16)) & 0xFFFFFF) / (float)0x1000000;
        }
    }

    /// <summary>
    /// Works out what every plate boundary is doing and which cells feel it.
    /// </summary>
    private void ClassifyBoundaries()
    {
        int w = _map.Width, h = _map.Height;
        Array.Clear(_tectMode);
        Array.Clear(_tectWeight);
        Array.Clear(_magmaSupply);

        // Pass 1: boundary type from relative plate motion
        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                var geo = _map.Cells[x, y].Geology;
                int p = _plateMap[x, y];
                var plate = _plates[p];
                float sumClose = 0, sumShear = 0;
                int n = 0;

                for (int dy = -1; dy <= 1; dy++)
                {
                    int ny = y + dy;
                    if (ny < 0 || ny >= h) continue;
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        int nx = (x + dx + w) % w;
                        int q = _plateMap[nx, ny];
                        if (q == p) continue;

                        var other = _plates[q];
                        float rx = plate.VelocityX - other.VelocityX;
                        float ry = plate.VelocityY - other.VelocityY;
                        float inv = (dx != 0 && dy != 0) ? 0.70710678f : 1f;
                        float ux = dx * inv, uy = dy * inv;
                        sumClose += rx * ux + ry * uy;            // + = moving towards the neighbour
                        sumShear += MathF.Abs(rx * uy - ry * ux);
                        n++;
                    }
                }

                geo.IsSubducting = false;
                geo.IsForedeep = false;
                geo.SubductionRate = 0;

                if (n == 0)
                {
                    geo.Convergence = 0;
                    geo.BoundaryType = geo.IsManualFault ? PlateBoundaryType.Transform : PlateBoundaryType.None;
                    continue;
                }

                float close = sumClose / n;
                float shear = sumShear / n;
                geo.Convergence = close;

                if (geo.IsManualFault)
                {
                    geo.BoundaryType = PlateBoundaryType.Transform;
                }
                else if (MathF.Abs(close) > 0.04f && MathF.Abs(close) > 0.6f * shear)
                {
                    geo.BoundaryType = close > 0 ? PlateBoundaryType.Convergent : PlateBoundaryType.Divergent;
                }
                else
                {
                    geo.BoundaryType = PlateBoundaryType.Transform;
                }
            }
        }

        // Pass 2: belts of deformation and magma supply around each boundary
        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                var cell = _map.Cells[x, y];
                var geo = cell.Geology;
                if (geo.BoundaryType == PlateBoundaryType.None || geo.IsManualFault) continue;

                int p = _plateMap[x, y];
                float strength = Math.Clamp(MathF.Abs(geo.Convergence) / 0.25f, 0.25f, 1.5f);

                // Boundaries act in segments rather than as one continuous wall
                float noise = _boundaryNoise.OctaveNoise(x * 0.1f, y * 0.1f, 2, 0.5f, 2.0f);
                strength *= 0.45f + 0.55f * Math.Clamp(noise + 0.5f, 0f, 1f);

                EnsureBoundaryFault(x, y, geo);

                if (geo.BoundaryType == PlateBoundaryType.Transform)
                {
                    continue;
                }

                if (geo.BoundaryType == PlateBoundaryType.Divergent)
                {
                    if (cell.IsWater || IsOceanicCrust(geo))
                    {
                        Stamp(x, y, TectonicMode.Ridge, strength);
                        _magmaSupply[x, y] = MathF.Max(_magmaSupply[x, y], 0.4f);
                    }
                    else
                    {
                        Stamp(x, y, TectonicMode.Rift, strength);
                        _magmaSupply[x, y] = MathF.Max(_magmaSupply[x, y], 0.45f);
                    }
                    continue;
                }

                // Convergent: find the crust on the other side
                bool myOceanic = IsOceanicCrust(geo);
                bool otherOceanic = false;
                int q = p;
                for (int dy = -1; dy <= 1 && q == p; dy++)
                {
                    int ny = y + dy;
                    if (ny < 0 || ny >= h) continue;
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int nx = (x + dx + w) % w;
                        if (_plateMap[nx, ny] == p) continue;
                        q = _plateMap[nx, ny];
                        otherOceanic = IsOceanicCrust(_map.Cells[nx, ny].Geology);
                        break;
                    }
                }
                if (q == p) continue;

                if (!myOceanic && !otherOceanic)
                {
                    // Continental collision: both sides crumple into a wide plateau
                    StampBelt(x, y, p, TectonicMode.Collision, strength, CollisionProfile, 0f, 0, 0);
                    continue;
                }

                bool iSubduct = myOceanic &&
                    (!otherOceanic ||
                     _plates[p].Density > _plates[q].Density ||
                     (_plates[p].Density == _plates[q].Density && p > q));

                if (iSubduct)
                {
                    geo.IsSubducting = true;
                    geo.SubductionRate = MathF.Abs(geo.Convergence);
                    Stamp(x, y, TectonicMode.Trench, strength);
                }
                else if (myOceanic)
                {
                    // Oceanic plate riding over another: volcanic island arc
                    StampBelt(x, y, p, TectonicMode.IslandArc, strength, OrogenProfile, 0.7f, 1, 2);
                }
                else
                {
                    // Continental margin above the slab: coastal range with a volcanic arc inland
                    StampBelt(x, y, p, TectonicMode.Orogen, strength, OrogenProfile, 0.6f, 2, 3);
                }
            }
        }

        StampForedeeps();
    }

    /// <summary>
    /// A mountain belt loads the plate it sits on. The crust beside the belt bends down into a
    /// trough (foredeep) that fills with the sediment shed by the rising range: first deep-water
    /// muds and turbidites, then sand and gravel as it fills up. This is where gas collects.
    /// </summary>
    private void StampForedeeps()
    {
        int w = _map.Width, h = _map.Height;
        const int reach = 3;
        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                if (_tectMode[x, y] != TectonicMode.None) continue;
                var cell = _map.Cells[x, y];
                if (cell.Geology.CrustType == CrustType.Oceanic || cell.Elevation < -0.55f) continue;

                float best = 0f;
                for (int dy = -reach; dy <= reach; dy++)
                {
                    int ny = y + dy;
                    if (ny < 0 || ny >= h) continue;
                    for (int dx = -reach; dx <= reach; dx++)
                    {
                        int nx = (x + dx + w) % w;
                        var mode = _tectMode[nx, ny];
                        if (mode != TectonicMode.Orogen && mode != TectonicMode.Collision) continue;
                        float distance = MathF.Sqrt(dx * dx + dy * dy);
                        if (distance > reach + 0.5f) continue;
                        // Deepest at the mountain front, shallowing away from it
                        float strength = _tectWeight[nx, ny] * (1f - (distance - 1f) / (reach + 0.5f));
                        if (strength > best) best = strength;
                    }
                }
                if (best < 0.25f) continue;
                _tectMode[x, y] = TectonicMode.Foredeep;
                _tectWeight[x, y] = MathF.Min(1f, best);
                cell.Geology.IsForedeep = true;
            }
        }
    }

    private void Stamp(int x, int y, TectonicMode mode, float weight)
    {
        if (mode > _tectMode[x, y] || (mode == _tectMode[x, y] && weight > _tectWeight[x, y]))
        {
            _tectMode[x, y] = mode;
            _tectWeight[x, y] = weight;
        }
    }

    /// <summary>
    /// Spreads a boundary's effect over the cells of the same plate behind it.
    /// </summary>
    private void StampBelt(int x, int y, int plateId, TectonicMode mode, float strength, float[] profile,
        float magma, int magmaFrom, int magmaTo)
    {
        int radius = profile.Length - 1;
        for (int dy = -radius; dy <= radius; dy++)
        {
            int ny = y + dy;
            if (ny < 0 || ny >= _map.Height) continue;
            for (int dx = -radius; dx <= radius; dx++)
            {
                int nx = (x + dx + _map.Width) % _map.Width;
                if (_plateMap[nx, ny] != plateId) continue;

                float dist = MathF.Sqrt(dx * dx + dy * dy);
                if (dist > radius) continue;
                int d = (int)MathF.Round(dist);

                Stamp(nx, ny, mode, strength * profile[d]);
                if (magma > 0 && d >= magmaFrom && d <= magmaTo)
                {
                    _magmaSupply[nx, ny] = MathF.Max(_magmaSupply[nx, ny], magma * Math.Clamp(strength, 0.5f, 1f));
                }
            }
        }
    }

    /// <summary>Plate boundaries are fault zones; most of their length is an active fault.</summary>
    private static void EnsureBoundaryFault(int x, int y, GeologicalData geo)
    {
        var type = geo.BoundaryType switch
        {
            PlateBoundaryType.Transform => FaultType.Strike_Slip,
            PlateBoundaryType.Divergent => FaultType.Normal,
            _ => Hash01(y, x) < 0.7f ? FaultType.Thrust : FaultType.Reverse
        };

        if (geo.IsFault)
        {
            if (!geo.IsManualFault) geo.FaultType = type;
            geo.FaultActivity = MathF.Max(geo.FaultActivity, 0.3f);
            return;
        }

        if (Hash01(x, y) < 0.75f)
        {
            geo.IsFault = true;
            geo.FaultType = type;
            geo.FaultActivity = 0.3f + 0.7f * Hash01(x + 91, y + 17);
        }
    }

    /// <summary>
    /// Applies plate boundary forces: uplift, subsidence, crustal thickening and stress loading.
    /// Scientific basis: Wilson cycle (Wilson, 1966); isostasy limits how high a range can stand.
    /// </summary>
    private void UpdatePlateTectonics(int currentYear, float deltaTime)
    {
        if (float.IsNaN(deltaTime) || deltaTime <= 0) return;

        float scale = TectonicScale;
        float step = UpliftSpeed * scale * deltaTime;
        float relax = MathF.Min(1f, 0.05f * deltaTime);
        float volcanicScale = VolcanicScale * deltaTime;

        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                var cell = _map.Cells[x, y];
                var geo = cell.Geology;
                var mode = _tectMode[x, y];
                float weight = _tectWeight[x, y];
                float elevation = cell.Elevation;
                float rate = 0f;         // Fraction of full speed, signed
                float load = 0f;         // Long-term stress loading at this cell

                switch (mode)
                {
                    case TectonicMode.Orogen:
                    {
                        // The range is highest above the slab and tapers inland
                        float crest = OrogenLimit * Math.Clamp(weight, 0f, 1f);
                        if (elevation >= 0 || weight > 0.6f)
                        {
                            rate = weight * Math.Clamp((crest - elevation) / 0.3f, 0f, 1f);
                        }
                        load = 0.6f * weight;
                        break;
                    }
                    case TectonicMode.Collision:
                    {
                        float crest = CollisionLimit * Math.Clamp(weight, 0f, 1f);
                        rate = 1.3f * weight * Math.Clamp((crest - elevation) / 0.3f, 0f, 1f);
                        load = 0.8f * weight;
                        break;
                    }
                    case TectonicMode.IslandArc:
                    {
                        // Only the axis of the arc breaks the surface; the rest is a submarine ridge
                        float crest = weight > 0.8f ? 0.12f : -0.25f;
                        rate = 0.6f * weight * Math.Clamp((crest - elevation) / 0.4f, 0f, 1f);
                        load = 0.5f * weight;
                        break;
                    }
                    case TectonicMode.Trench:
                        rate = -0.8f * weight * Math.Clamp((elevation - TrenchFloor) / 0.4f, 0f, 1f);
                        load = weight;
                        break;
                    case TectonicMode.Ridge:
                        rate = 0.6f * weight * Math.Clamp((RidgeCrest - elevation) / 0.4f, 0f, 1f);
                        load = 0.3f * weight;
                        break;
                    case TectonicMode.Rift:
                        rate = -0.5f * weight * Math.Clamp((elevation - RiftFloor) / 0.4f, 0f, 1f);
                        load = 0.4f * weight;
                        break;
                    case TectonicMode.Foredeep:
                    {
                        // Flexural subsidence keeps making room; the range next door keeps filling it
                        // On land the fill keeps pace and the trough stays a low plain (a Po plain, not a
                        // drowned one); where it is already under the sea it stays a shallow gulf
                        float floor = elevation > 0.02f ? 0.03f : -0.14f;
                        rate = -0.35f * weight * Math.Clamp((elevation - floor) / 0.3f, 0f, 1f);
                        load = 0.25f * weight;
                        geo.SedimentaryRock = MathF.Min(5f, geo.SedimentaryRock + 0.004f * weight * scale * deltaTime);
                        _depositAcc[x, y] += 0.00004f * weight * scale * deltaTime;
                        geo.DepositionRate = MathF.Max(geo.DepositionRate, 0.25f * weight);
                        if (_depositAcc[x, y] > 0.003f)
                        {
                            _depositAcc[x, y] = 0f;
                            // Flysch while the trough is under water, molasse once it has filled
                            int beds = geo.SedimentColumn.Count;
                            AddLayer(geo, elevation < -0.03f
                                ? (beds % 2 == 0 ? SedimentType.Clay : SedimentType.Sand)
                                : (beds % 3 == 0 ? SedimentType.Gravel : SedimentType.Sand));
                        }
                        break;
                    }
                    case TectonicMode.None:
                        if (geo.CrustType == CrustType.Oceanic && elevation < 0f && elevation > AbyssalDepth)
                        {
                            // Sea floor cools, contracts and sinks as it ages away from the ridge
                            rate = -0.08f * Math.Clamp((elevation - AbyssalDepth) / 0.3f, 0f, 1f);
                        }
                        else if (elevation > 0.75f)
                        {
                            // A range no longer held up by convergence sags back (gravitational collapse)
                            rate = -0.05f * (elevation - 0.75f) / 0.4f;
                        }
                        break;
                }

                if (geo.BoundaryType == PlateBoundaryType.Transform)
                {
                    load = MathF.Max(load, geo.IsManualFault ? 1f : 0.2f + 2.5f * MathF.Min(0.3f, MathF.Abs(geo.Convergence) + 0.1f));
                }
                else if (geo.BoundaryType == PlateBoundaryType.None)
                {
                    load *= 0.4f; // Inside the belt but off the plate contact
                }

                if (rate != 0f)
                {
                    cell.Elevation = Math.Clamp(elevation + rate * step, -1.2f, 1.25f);

                    if (rate > 0 && (mode == TectonicMode.Orogen || mode == TectonicMode.Collision))
                    {
                        // Shortening thickens the crust and metamorphoses its roots
                        geo.CrustThickness = MathF.Min(70f, geo.CrustThickness + rate * step * 25f);
                    }
                }

                geo.UpliftRate = rate * UpliftSpeed * scale;

                if (mode == TectonicMode.Ridge && geo.BoundaryType == PlateBoundaryType.Divergent && cell.IsWater)
                {
                    // Sea-floor spreading: the ridge axis is brand new basaltic crust
                    geo.CrustAge = 0;
                    geo.CrustType = CrustType.Oceanic;
                    geo.PrimaryRock = RockType.Basalt;
                    geo.CrustThickness += (7f - geo.CrustThickness) * relax;
                }
                else if (mode == TectonicMode.Rift && cell.Elevation < -0.1f && geo.CrustType == CrustType.Continental)
                {
                    geo.CrustType = CrustType.Transitional; // Stretched, thinned continental crust
                    geo.CrustThickness = MathF.Max(15f, geo.CrustThickness - 5f);
                }

                // Stress follows the loading the boundary applies (relaxes when the boundary moves on)
                float target = load * scale;
                geo.TectonicStress += (target - geo.TectonicStress) * relax;
                if (float.IsNaN(geo.TectonicStress)) geo.TectonicStress = 0;
                geo.TectonicStress = Math.Clamp(geo.TectonicStress, 0f, 3f);

                // New volcanoes where the setting supplies magma
                float supply = _magmaSupply[x, y];
                if (supply > 0 && !geo.IsVolcano &&
                    _simulationRandom.NextDouble() < 0.00004 * supply * volcanicScale && CanPlaceVolcano(x, y))
                {
                    geo.IsVolcano = true;
                    geo.VolcanicActivity = supply;
                    geo.MagmaPressure = 0.2f + (float)_simulationRandom.NextDouble() * 0.3f;
                    geo.LastEruptionYear = currentYear;
                    _volcanoCount++;
                }

                // Faults left behind by a boundary that moved away heal slowly
                if (geo.IsFault && !geo.IsManualFault && geo.BoundaryType == PlateBoundaryType.None)
                {
                    geo.FaultActivity -= 0.002f * scale * deltaTime;
                    if (geo.FaultActivity <= 0f)
                    {
                        geo.IsFault = false;
                        geo.FaultType = FaultType.None;
                        geo.FaultActivity = 0f;
                    }
                }
            }
        }
    }

    /// <summary>Makes a volcano erupt now (teaching tools and divine intervention).</summary>
    public bool TriggerEruption(int x, int y, int year)
    {
        var geo = _map.Cells[x, y].Geology;
        if (!geo.IsVolcano) return false;
        geo.MagmaPressure = MathF.Max(geo.MagmaPressure, 1.6f);
        VolcanicEruption(x, y, year);
        geo.MagmaPressure = 0f;
        geo.LastEruptionYear = year;
        return true;
    }

    /// <summary>Lava and tephra pile up around the vent; tall edifices grow more slowly.</summary>
    private void BuildEdifice(int x, int y, float amount, int radius)
    {
        var cell = _map.Cells[x, y];
        float limiter = cell.Elevation > 0 ? Math.Clamp(1f - cell.Elevation / 1.1f, 0.05f, 1f) : 1f;
        float growth = amount * limiter;
        cell.Elevation += growth;

        for (int dy = -radius; dy <= radius; dy++)
        {
            int ny = y + dy;
            if (ny < 0 || ny >= _map.Height) continue;
            for (int dx = -radius; dx <= radius; dx++)
            {
                if (dx == 0 && dy == 0) continue;
                int nx = (x + dx + _map.Width) % _map.Width;
                var neighbor = _map.Cells[nx, ny];
                if (neighbor.Elevation >= cell.Elevation) continue;
                float dist = MathF.Sqrt(dx * dx + dy * dy);
                neighbor.Elevation += growth * 0.35f / dist;
            }
        }
    }

    private void ClampEruptionHeat(int x, int y, int radius)
    {
        for (int dy = -radius; dy <= radius; dy++)
        {
            int ny = y + dy;
            if (ny < 0 || ny >= _map.Height) continue;
            for (int dx = -radius; dx <= radius; dx++)
            {
                int nx = (x + dx + _map.Width) % _map.Width;
                var cell = _map.Cells[nx, ny];
                if (cell.Temperature > 90f) cell.Temperature = 90f;
            }
        }
    }

    private void AddVolcanicAerosol(float amount)
    {
        amount = Math.Clamp(amount, 0f, 0.08f);
        _aerosol += amount;
        _map.SolarEnergy -= amount;
    }

    /// <summary>Stratospheric aerosols settle out within a few years and sunlight recovers.</summary>
    private void UpdateVolcanicAerosols(float deltaTime)
    {
        if (_aerosol <= 0f) return;
        float cleared = _aerosol * (1f - MathF.Exp(-deltaTime / 30f));
        if (_aerosol - cleared < 0.0005f) cleared = _aerosol;
        _aerosol -= cleared;
        _map.SolarEnergy += cleared;
    }

    /// <summary>
    /// Erosion, transport and deposition. Material removed from high ground travels downhill as
    /// a sediment load and settles where the slope gives out: valleys, deltas and shelves.
    /// </summary>
    private void UpdateErosionAndSedimentation(float deltaTime)
    {
        if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime <= 0) return;

        float k = ErosionScale * deltaTime;
        float fade = MathF.Max(0f, 1f - 0.4f * deltaTime);
        const float compactionRate = 0.02f;
        int w = _map.Width, h = _map.Height;

        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                var cell = _map.Cells[x, y];
                var geo = cell.Geology;

                if (float.IsNaN(geo.SedimentLayer) || float.IsInfinity(geo.SedimentLayer)) geo.SedimentLayer = 0f;
                if (float.IsNaN(cell.Elevation) || float.IsInfinity(cell.Elevation)) cell.Elevation = 0f;
                geo.DepositionRate *= fade;

                // Burial compacts loose sediment into rock
                if (geo.SedimentLayer > 2.0f)
                {
                    float compaction = (geo.SedimentLayer - 2.0f) * MathF.Min(1f, compactionRate * deltaTime);
                    geo.SedimentLayer -= compaction;
                    geo.SedimentaryRock = Math.Clamp(geo.SedimentaryRock + compaction * 0.5f, 0f, 5f);
                }

                if (!cell.IsLand)
                {
                    geo.ErosionRate = 0f;

                    // Trench sediments are dragged down with the slab
                    if (cell.Elevation < -0.5f && (geo.IsSubducting || geo.SedimentLayer > 5.0f))
                    {
                        geo.SedimentLayer *= MathF.Max(0f, 1.0f - 0.02f * deltaTime);
                    }
                    continue;
                }

                // Steepest way down
                float lowest = cell.Elevation;
                int lx = -1, ly = -1;
                for (int dy = -1; dy <= 1; dy++)
                {
                    int ny = y + dy;
                    if (ny < 0 || ny >= h) continue;
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        int nx = (x + dx + w) % w;
                        float e = _map.Cells[nx, ny].Elevation;
                        if (e < lowest)
                        {
                            lowest = e;
                            lx = nx;
                            ly = ny;
                        }
                    }
                }
                float drop = cell.Elevation - lowest;

                float rain = float.IsNaN(cell.Rainfall) ? 0f : Math.Clamp(cell.Rainfall, 0f, 1f);
                float flow = float.IsNaN(geo.WaterFlow) ? 0f : Math.Clamp(geo.WaterFlow, 0f, 3f);
                float power = (0.25f + rain) * (1f + flow);
                if (cell.IsIce) power *= 1.5f;              // Glaciers grind rock
                if (cell.Temperature > 20f) power *= 1.2f;  // Chemical weathering

                // Stream power: steeper, wetter and higher ground wears down faster
                float erosion = k * (0.00001f + 0.012f * drop) * power * cell.Elevation / (0.5f + geo.RockHardness);
                erosion = MathF.Min(erosion, MathF.Min(drop * 0.25f, 0.02f));
                // Sea level is the base level: rivers cannot cut land down below it
                erosion = MathF.Min(erosion, MathF.Max(0f, cell.Elevation - 0.015f));
                if (erosion > 0f)
                {
                    cell.Elevation -= erosion;
                    geo.SedimentLayer += erosion;
                    geo.CumulativeErosion += erosion;

                    // Erosion strips the record from the top: beds are lost and the ground
                    // becomes an erosion surface (a future unconformity once it is buried)
                    _erodeAcc[x, y] += erosion;
                    if (_erodeAcc[x, y] > 0.004f)
                    {
                        _erodeAcc[x, y] = 0f;
                        var beds = geo.SedimentColumn;
                        if (beds.Count > 1) beds.RemoveAt(beds.Count - 1);
                        geo.UnconformityBeds = 0;
                    }
                }
                geo.ErosionRate = erosion / deltaTime * 1000f;

                if (geo.SedimentLayer <= 0.0001f) continue;

                if (lx < 0)
                {
                    // Closed basin: the load stays and fills it
                    float fill = geo.SedimentLayer * MathF.Min(1f, 0.5f * deltaTime);
                    cell.Elevation += fill;
                    geo.SedimentLayer -= fill * 0.5f;
                    RecordDeposit(x, y, geo, fill, deltaTime, cell.Biomass > 0.5f ? SedimentType.Organic : SedimentType.Clay);
                    continue;
                }

                float energy = drop * 10f * (0.3f + rain + flow);
                float moved = geo.SedimentLayer * Math.Clamp(deltaTime * (0.2f + energy), 0f, 0.6f);
                geo.SedimentLayer -= moved;

                var target = _map.Cells[lx, ly];
                var targetGeo = target.Geology;
                if (float.IsNaN(targetGeo.SedimentLayer) || float.IsInfinity(targetGeo.SedimentLayer)) targetGeo.SedimentLayer = 0f;

                // Fast water keeps its load; still water drops it
                float settle = target.IsWater ? 1f : Math.Clamp(1f - energy, 0.1f, 1f);
                float deposit = moved * settle;
                if (target.IsWater)
                {
                    // Shelves and deltas fill up to wave base; the rest is carried further out
                    deposit = MathF.Min(deposit, MathF.Max(0f, -0.02f - target.Elevation));
                }

                target.Elevation = Math.Clamp(target.Elevation + deposit, -1.2f, 1.25f);
                targetGeo.SedimentLayer = Math.Clamp(targetGeo.SedimentLayer + (moved - deposit) + deposit * 0.5f, 0f, 10f);

                if (deposit > 0f)
                {
                    targetGeo.SedimentaryRock = Math.Clamp(targetGeo.SedimentaryRock + deposit * 0.1f, 0f, 5f);

                    SedimentType type;
                    if (geo.VolcanicRock > 0.5f && geo.IsVolcano) type = SedimentType.Volcanic;
                    else if (cell.Biomass > 0.5f && energy < 0.4f) type = SedimentType.Organic;
                    else if (energy > 0.8f) type = SedimentType.Gravel;
                    else if (energy > 0.5f) type = SedimentType.Sand;
                    else if (energy > 0.3f) type = SedimentType.Silt;
                    else type = SedimentType.Clay;

                    RecordDeposit(lx, ly, targetGeo, deposit, deltaTime, type);
                }
            }
        }
    }

    /// <summary>A new bed is added to the column once enough material has settled.</summary>
    private void RecordDeposit(int x, int y, GeologicalData geo, float amount, float deltaTime, SedimentType type)
    {
        geo.DepositionRate = MathF.Min(1f, geo.DepositionRate + amount / deltaTime * 200f);
        geo.CumulativeDeposition += amount;
        _depositAcc[x, y] += amount;
        if (_depositAcc[x, y] > 0.003f)
        {
            _depositAcc[x, y] = 0f;
            AddLayer(geo, type);
        }
    }
}
