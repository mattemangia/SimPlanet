namespace SimPlanet;

public sealed class StationSaveData
{
    public int X { get; set; }
    public int Y { get; set; }
    public int CivId { get; set; }
    public int YearBuilt { get; set; }
}

/// <summary>
/// Everything the geoscience systems add to a saved game: the layers under every cell,
/// faults, oil and gas fields and their wells and surveys, geothermal plants, seismographs.
/// </summary>
public sealed class GeoscienceSaveData
{
    public PetroleumSaveData Petroleum { get; set; } = new();
    public List<GeothermalPlant> GeothermalPlants { get; set; } = new();
    public List<StationSaveData> Stations { get; set; } = new();

    // Per cell, row by row
    public string[] Columns { get; set; } = Array.Empty<string>();      // One digit per bed, oldest first
    public int[] UnconformityBeds { get; set; } = Array.Empty<int>();
    public float[] CumulativeErosion { get; set; } = Array.Empty<float>();
    public float[] CumulativeDeposition { get; set; } = Array.Empty<float>();
    public float[] SedimentaryRock { get; set; } = Array.Empty<float>();
    public float[] CrustThickness { get; set; } = Array.Empty<float>();
    public byte[] CrustType { get; set; } = Array.Empty<byte>();
    public byte[] Fault { get; set; } = Array.Empty<byte>();            // 0 = none, otherwise fault type; +100 = drawn by the player
    public float[] FaultActivity { get; set; } = Array.Empty<float>();
    public byte[] HotSpot { get; set; } = Array.Empty<byte>();

    public static GeoscienceSaveData Capture(PlanetMap map, UpdateManager systems)
    {
        int n = map.Width * map.Height;
        var data = new GeoscienceSaveData
        {
            Petroleum = systems.Petroleum.Capture(),
            GeothermalPlants = systems.Geothermal.Plants.ToList(),
            Stations = systems.Seismology.Stations.Select(s => new StationSaveData { X = s.X, Y = s.Y, CivId = s.CivId, YearBuilt = s.YearBuilt }).ToList(),
            Columns = new string[n], UnconformityBeds = new int[n], CumulativeErosion = new float[n], CumulativeDeposition = new float[n],
            SedimentaryRock = new float[n], CrustThickness = new float[n], CrustType = new byte[n], Fault = new byte[n],
            FaultActivity = new float[n], HotSpot = new byte[n]
        };

        var text = new System.Text.StringBuilder();
        for (int y = 0; y < map.Height; y++)
        {
            for (int x = 0; x < map.Width; x++)
            {
                int i = y * map.Width + x;
                var geo = map.Cells[x, y].Geology;
                text.Clear();
                foreach (var bed in geo.SedimentColumn) text.Append((char)('0' + (int)bed));
                data.Columns[i] = text.ToString();
                data.UnconformityBeds[i] = geo.UnconformityBeds;
                data.CumulativeErosion[i] = geo.CumulativeErosion;
                data.CumulativeDeposition[i] = geo.CumulativeDeposition;
                data.SedimentaryRock[i] = geo.SedimentaryRock;
                data.CrustThickness[i] = geo.CrustThickness;
                data.CrustType[i] = (byte)geo.CrustType;
                data.Fault[i] = !geo.IsFault ? (byte)0 : (byte)((int)geo.FaultType + (geo.IsManualFault ? 100 : 0));
                data.FaultActivity[i] = geo.FaultActivity;
                data.HotSpot[i] = (byte)(geo.IsHotSpot ? 1 : 0);
            }
        }
        return data;
    }

    /// <summary>
    /// Applies saved geoscience state. Call after the cells and plates have been restored and
    /// the fields rebuilt. Older saves have none of this and keep what the seed generated.
    /// </summary>
    public void Restore(PlanetMap map, UpdateManager systems)
    {
        int n = map.Width * map.Height;
        if (Columns.Length == n)
        {
            for (int y = 0; y < map.Height; y++)
            {
                for (int x = 0; x < map.Width; x++)
                {
                    int i = y * map.Width + x;
                    var geo = map.Cells[x, y].Geology;
                    geo.SedimentColumn.Clear();
                    foreach (char c in Columns[i] ?? "")
                    {
                        int bed = c - '0';
                        if (bed >= 0 && bed <= (int)SedimentType.Limestone) geo.SedimentColumn.Add((SedimentType)bed);
                    }
                    if (geo.SedimentColumn.Count == 0) geo.SedimentColumn.Add(SedimentType.Gravel);
                    geo.UnconformityBeds = UnconformityBeds[i];
                    geo.CumulativeErosion = CumulativeErosion[i];
                    geo.CumulativeDeposition = CumulativeDeposition[i];
                    geo.SedimentaryRock = SedimentaryRock[i];
                    geo.CrustThickness = CrustThickness[i];
                    geo.CrustType = (CrustType)CrustType[i];
                    int fault = Fault[i];
                    geo.IsManualFault = fault >= 100;
                    if (fault >= 100) fault -= 100;
                    geo.IsFault = Fault[i] != 0;
                    geo.FaultType = (FaultType)fault;
                    geo.FaultActivity = FaultActivity[i];
                    geo.IsHotSpot = HotSpot[i] != 0;
                }
            }
        }

        systems.Petroleum.Restore(Petroleum);
        systems.Geothermal.Restore(GeothermalPlants);
        systems.Seismology.Stations.Clear();
        foreach (var station in Stations) systems.Seismology.AddStation(station.X, station.Y, station.CivId, station.YearBuilt);
    }
}
