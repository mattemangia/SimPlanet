namespace SimPlanet;

public enum RockKind : byte
{
    Clay, Silt, Sand, Gravel, Organic, Volcanic, Limestone, Salt, Granite, Basalt
}

public enum PoreFluid : byte
{
    Brine, Oil, Gas
}

/// <summary>Rock and its physical properties at one depth.</summary>
public struct RockSample
{
    public RockKind Kind;
    public PoreFluid Fluid;
    public float Velocity;      // P velocity, km/s
    public float Density;       // g/cm3
    public float Gamma;         // Natural gamma ray, API units
    public float Resistivity;   // ohm.m
}

/// <summary>
/// The layers under one point of the map, from the surface (or sea floor) down to basement.
///
/// This is the single description of the subsurface: the 3D sections, the synthetic
/// reflection seismics and the well logs are all computed from it, so a bed seen in a section
/// is the same bed a well logs and a seismic line images.
/// </summary>
public sealed class StrataColumn
{
    public const int Beds = 12;

    public float WaterKm;                       // Sea above the column
    public float SedimentKm;                    // Thickness of the sedimentary pile
    public readonly float[] BedTop = new float[Beds + 1];   // Depth of each bed top; last entry = basement
    public readonly RockKind[] BedKind = new RockKind[Beds];
    public RockKind Basement;
    public float CrustKm;                       // Moho depth below the surface

    public bool HasReservoir;
    public PoreFluid ReservoirFluid;
    public float ReservoirTop, FluidContact, ReservoirBottom;

    public bool HasSalt;
    public float SaltTop;

    public int BedCount;                        // Beds actually present (the rest have no thickness)
    public float UnconformityKm = -1f;          // Depth of the latest erosion surface (-1 = none, 0 = the ground)

    /// <summary>Rock at a depth (km below the surface or sea floor).</summary>
    public RockSample Sample(float depth)
    {
        RockKind kind;
        if (depth >= SedimentKm) kind = Basement;
        else if (HasSalt && depth >= SaltTop) kind = RockKind.Salt;
        else
        {
            int bed = Beds - 1;
            for (int b = 0; b < Beds; b++)
            {
                if (depth < BedTop[b + 1])
                {
                    bed = b;
                    break;
                }
            }
            kind = BedKind[bed];
        }

        var sample = Properties(kind);
        sample.Kind = kind;

        if (kind < RockKind.Salt)
        {
            // Burial squeezes the pores shut: faster, denser, more resistive with depth
            sample.Velocity *= 1f + 0.12f * depth;
            sample.Density = MathF.Min(2.72f, sample.Density + 0.05f * depth);
            sample.Resistivity *= 1f + 0.6f * depth;
        }

        if (HasReservoir && depth >= ReservoirTop && depth < ReservoirBottom && kind != RockKind.Salt && depth < SedimentKm)
        {
            // The reservoir is clean porous sandstone whatever the bed around it
            var sand = Properties(RockKind.Sand);
            sample.Kind = RockKind.Sand;
            sample.Velocity = sand.Velocity * (1f + 0.12f * depth);
            sample.Density = MathF.Min(2.72f, sand.Density + 0.05f * depth);
            sample.Gamma = 30f;
            sample.Resistivity = 1.5f;      // Salt water in the pores conducts well

            if (depth < FluidContact)
            {
                sample.Fluid = ReservoirFluid;
                if (ReservoirFluid == PoreFluid.Gas)
                {
                    sample.Velocity *= 0.74f;   // Gas makes the rock acoustically soft: bright spot
                    sample.Density *= 0.88f;
                    sample.Resistivity = 400f;
                }
                else
                {
                    sample.Velocity *= 0.90f;
                    sample.Density *= 0.95f;
                    sample.Resistivity = 120f;
                }
            }
        }
        return sample;
    }

    public static RockSample Properties(RockKind kind) => kind switch
    {
        RockKind.Clay => new RockSample { Velocity = 2.1f, Density = 2.15f, Gamma = 115f, Resistivity = 4f },
        RockKind.Silt => new RockSample { Velocity = 2.4f, Density = 2.25f, Gamma = 85f, Resistivity = 7f },
        RockKind.Sand => new RockSample { Velocity = 2.8f, Density = 2.30f, Gamma = 40f, Resistivity = 3f },
        RockKind.Gravel => new RockSample { Velocity = 3.2f, Density = 2.40f, Gamma = 50f, Resistivity = 12f },
        RockKind.Organic => new RockSample { Velocity = 1.9f, Density = 1.90f, Gamma = 150f, Resistivity = 25f },
        RockKind.Volcanic => new RockSample { Velocity = 3.8f, Density = 2.60f, Gamma = 60f, Resistivity = 80f },
        RockKind.Limestone => new RockSample { Velocity = 4.4f, Density = 2.65f, Gamma = 20f, Resistivity = 90f },
        RockKind.Salt => new RockSample { Velocity = 4.5f, Density = 2.15f, Gamma = 8f, Resistivity = 2000f },
        RockKind.Basalt => new RockSample { Velocity = 6.2f, Density = 2.90f, Gamma = 25f, Resistivity = 600f },
        _ => new RockSample { Velocity = 5.9f, Density = 2.70f, Gamma = 160f, Resistivity = 1500f }
    };

    public static RockKind FromSediment(SedimentType type) => type switch
    {
        SedimentType.Clay => RockKind.Clay,
        SedimentType.Silt => RockKind.Silt,
        SedimentType.Sand => RockKind.Sand,
        SedimentType.Gravel => RockKind.Gravel,
        SedimentType.Organic => RockKind.Organic,
        SedimentType.Volcanic => RockKind.Volcanic,
        _ => RockKind.Limestone
    };
}

public static class Stratigraphy
{
    /// <summary>How far a trap lifts the beds at a point, in km (0 away from any field).</summary>
    public static float TrapRelief(PlanetMap map, Reservoir reservoir, float x, float y, out float offset)
    {
        float halfWidth = MathF.Max(1.5f, MathF.Sqrt(reservoir.Cells.Count) * 0.75f);
        float dx = x - reservoir.CenterX;
        if (dx > map.Width / 2f) dx -= map.Width;
        if (dx < -map.Width / 2f) dx += map.Width;
        float dy = y - reservoir.CenterY;
        offset = MathF.Sqrt(dx * dx + dy * dy) / halfWidth;

        return reservoir.Trap switch
        {
            TrapType.Anticline => 0.35f * MathF.Exp(-offset * offset),
            TrapType.SaltDome => 0.55f * MathF.Exp(-offset * offset * 2.5f),
            TrapType.FaultTrap => dx > 0 ? 0.25f * Math.Clamp(1.6f - offset, 0f, 1f) : 0f,   // Upthrown block
            _ => 0.06f * MathF.Exp(-offset * offset)
        };
    }

    /// <summary>
    /// Fills in the column under a point. Position is in cell coordinates and may be fractional
    /// (seismic traces fall between cell centres); the beds come from the nearest cell.
    /// </summary>
    public static void Build(PlanetMap map, PetroleumSystem? oil, float x, float y, StrataColumn column)
    {
        int cx = (((int)MathF.Round(x)) % map.Width + map.Width) % map.Width;
        int cy = Math.Clamp((int)MathF.Round(y), 0, map.Height - 1);
        var cell = map.Cells[cx, cy];
        var geo = cell.Geology;

        column.WaterKm = cell.IsWater ? MathF.Min(6f, -cell.Elevation * 4.4f) : 0f;
        column.CrustKm = geo.CrustThickness;
        column.Basement = geo.CrustType == CrustType.Oceanic ? RockKind.Basalt : RockKind.Granite;

        // The nearest field within reach shapes the beds (the trap extends past the oil itself)
        Reservoir? reservoir = oil?.GetReservoirAt(cx, cy);
        if (reservoir == null && oil != null)
        {
            for (int dy = -3; dy <= 3 && reservoir == null; dy++)
            {
                for (int dx = -3; dx <= 3 && reservoir == null; dx++)
                {
                    int ny = cy + dy;
                    if (ny < 0 || ny >= map.Height) continue;
                    reservoir = oil.GetReservoirAt((cx + dx + map.Width) % map.Width, ny);
                }
            }
        }

        // Thickness and water depth vary smoothly between cell centres, so beds and reflectors
        // are continuous along a section or a seismic line
        float sedimentKm = 0f, waterKm = 0f;
        int x0 = (int)MathF.Floor(x), y0 = (int)MathF.Floor(y);
        float tx = x - x0, ty = y - y0;
        for (int k = 0; k < 4; k++)
        {
            int sx = ((x0 + (k & 1)) % map.Width + map.Width) % map.Width;
            int sy = Math.Clamp(y0 + (k >> 1), 0, map.Height - 1);
            float weight = ((k & 1) == 0 ? 1f - tx : tx) * ((k >> 1) == 0 ? 1f - ty : ty);
            if (weight <= 0f) continue;
            var c = map.Cells[sx, sy];
            var g = c.Geology;
            float thickness = 0.6f + 0.5f * Math.Clamp(g.SedimentaryRock, 0f, 5f) + 0.2f * Math.Clamp(g.SedimentLayer, 0f, 10f);
            if (g.CrustType == CrustType.Oceanic) thickness = MathF.Min(thickness, 1.2f);
            if (g.IsForedeep) thickness += 2.6f; // The foreland trough holds kilometres of fill
            sedimentKm += weight * thickness;
            waterKm += weight * (c.IsWater ? MathF.Min(6f, -c.Elevation * 4.4f) : 0f);
        }
        column.WaterKm = waterKm;

        float relief = 0f, offset = 9f;
        if (reservoir != null)
        {
            relief = TrapRelief(map, reservoir, x, y, out offset);
            if (offset < 1.6f) sedimentKm = MathF.Max(sedimentKm, reservoir.DepthKm + reservoir.ThicknessKm + 0.5f);
        }
        column.SedimentKm = sedimentKm;

        // Beds: runs of the same sediment in the cell's column, youngest on top, each as thick
        // as its share of the record. A place that kept little (eroded uplands) has a thin pile.
        var beds = geo.SedimentColumn;
        int count = 0;
        try { count = beds.Count; } catch { }
        float preserved = Math.Clamp(count / 10f, 0.12f, 1f);
        if (reservoir == null || offset >= 1.6f) sedimentKm *= preserved;

        Span<int> runLength = stackalloc int[StrataColumn.Beds];
        int runs = 0, layers = 0;
        SedimentType current = SedimentType.Clay;
        for (int i = count - 1; i >= 0; i--)
        {
            SedimentType type;
            try { type = beds[i]; } catch { break; }
            if (runs == 0 || (type != current && runs < StrataColumn.Beds))
            {
                current = type;
                column.BedKind[runs] = StrataColumn.FromSediment(type);
                runLength[runs++] = 0;
            }
            runLength[runs - 1]++; // Beyond the last slot everything older joins the bottom bed
            layers++;
        }
        if (runs == 0)
        {
            column.BedKind[0] = RockKind.Gravel;
            runLength[0] = 1;
            runs = 1;
            layers = 1;
        }
        column.BedCount = runs;

        float reservoirDepth = reservoir?.DepthKm ?? sedimentKm;
        int above = 0;
        int unconformityLayers = geo.UnconformityBeds;
        column.UnconformityKm = unconformityLayers == 0 ? 0f : -1f;
        for (int b = 0; b <= StrataColumn.Beds; b++)
        {
            float flat = sedimentKm * Math.Min(above, layers) / layers;
            float drape = relief * Math.Clamp(flat / MathF.Max(0.5f, reservoirDepth), 0f, 1f);
            column.BedTop[b] = MathF.Max(flat * 0.2f, flat - drape);
            if (unconformityLayers > 0 && above <= unconformityLayers && b < runs && above + runLength[b] >= unconformityLayers)
            {
                // The erosion surface lies inside or at the base of this bed
                float at = sedimentKm * unconformityLayers / layers;
                column.UnconformityKm = MathF.Max(at * 0.2f, at - relief * Math.Clamp(at / MathF.Max(0.5f, reservoirDepth), 0f, 1f));
                unconformityLayers = -1;
            }
            if (b < runs) above += runLength[b];
            if (b >= runs && b < StrataColumn.Beds) column.BedKind[b] = column.BedKind[runs - 1];
        }
        column.BedTop[StrataColumn.Beds] = sedimentKm - relief * 0.3f;   // Basement is gently involved too
        for (int b = 0; b < StrataColumn.Beds; b++) column.BedTop[b] = MathF.Min(column.BedTop[b], column.BedTop[StrataColumn.Beds]);
        column.SedimentKm = column.BedTop[StrataColumn.Beds];

        column.HasReservoir = false;
        column.HasSalt = false;
        if (reservoir != null && offset < 1f)
        {
            // The top follows the structure; the fluid contact is level (fluids settle by density)
            column.HasReservoir = true;
            column.ReservoirFluid = reservoir.HasGas ? PoreFluid.Gas : PoreFluid.Oil;
            float crestTop = reservoir.DepthKm - TrapRelief(map, reservoir, reservoir.CenterX, reservoir.CenterY, out _);
            column.ReservoirTop = reservoir.DepthKm - relief;
            column.FluidContact = crestTop + reservoir.ThicknessKm * 0.6f + 0.05f;
            column.ReservoirBottom = column.ReservoirTop + reservoir.ThicknessKm;
            if (reservoir.Trap == TrapType.Stratigraphic)
            {
                // A lens: it thins out towards its edges instead of being sealed by structure
                column.ReservoirBottom = column.ReservoirTop + reservoir.ThicknessKm * (1f - offset * offset);
                column.FluidContact = column.ReservoirBottom;
            }
            if (column.FluidContact <= column.ReservoirTop) column.FluidContact = column.ReservoirTop;
        }
        if (reservoir != null && reservoir.Trap == TrapType.SaltDome && offset < 0.45f)
        {
            column.HasSalt = true;
            column.SaltTop = reservoir.DepthKm - relief + reservoir.ThicknessKm + 0.1f;
        }
    }
}
