using Microsoft.Xna.Framework;

namespace SimPlanet;

/// <summary>
/// Colours and labels shared by the society map views (energy, government, internet,
/// armaments, epidemics...), their legends and the nation detail panel, so the same
/// thing always has the same colour everywhere.
/// </summary>
public static class SocietyStyle
{
    public static readonly EnergySource[] AllEnergySources = Enum.GetValues<EnergySource>();
    public static readonly GovernmentType[] AllGovernmentTypes = Enum.GetValues<GovernmentType>();

    public static Color EnergyColor(EnergySource source) => source switch
    {
        EnergySource.Biomass => new Color(150, 112, 64),
        EnergySource.Coal => new Color(62, 60, 70),
        EnergySource.Oil => new Color(206, 116, 44),
        EnergySource.Hydro => new Color(58, 138, 226),
        EnergySource.Nuclear => new Color(176, 96, 232),
        EnergySource.Wind => new Color(110, 222, 214),
        EnergySource.Solar => new Color(250, 206, 56),
        EnergySource.Fusion => new Color(255, 118, 196),
        _ => Color.Gray
    };

    public static string EnergyName(EnergySource source) => source switch
    {
        EnergySource.Biomass => "Biomass (wood)",
        _ => source.ToString()
    };

    public static bool IsClean(EnergySource source) =>
        source is EnergySource.Hydro or EnergySource.Nuclear or EnergySource.Wind or EnergySource.Solar or EnergySource.Fusion;

    public static Color GovernmentColor(GovernmentType type) => type switch
    {
        GovernmentType.Tribal => new Color(160, 124, 82),
        GovernmentType.Monarchy => new Color(150, 72, 190),
        GovernmentType.Dynasty => new Color(214, 160, 44),
        GovernmentType.Theocracy => new Color(232, 220, 170),
        GovernmentType.Republic => new Color(66, 140, 222),
        GovernmentType.Democracy => new Color(76, 196, 118),
        GovernmentType.Oligarchy => new Color(196, 112, 64),
        GovernmentType.Dictatorship => new Color(206, 52, 52),
        GovernmentType.Federation => new Color(64, 190, 196),
        _ => Color.Gray
    };

    public static Color IdeologyColor(Ideology ideology) => ideology switch
    {
        Ideology.Conservative => new Color(70, 110, 210),
        Ideology.Liberal => new Color(250, 196, 60),
        Ideology.Socialist => new Color(220, 60, 70),
        Ideology.Green => new Color(80, 190, 90),
        Ideology.Nationalist => new Color(130, 80, 50),
        Ideology.Technocratic => new Color(60, 200, 220),
        Ideology.Religious => new Color(170, 110, 220),
        _ => Color.Gray
    };

    private static readonly Color[] DiseasePalette =
    {
        new Color(230, 60, 60),
        new Color(160, 230, 60),
        new Color(236, 130, 30),
        new Color(200, 70, 220),
        new Color(40, 200, 180),
        new Color(240, 220, 60),
    };

    public static Color DiseaseColor(int diseaseId) => DiseasePalette[Math.Abs(diseaseId) % DiseasePalette.Length];

    public static string SpaceStageName(SpaceStage stage) => stage switch
    {
        SpaceStage.None => "Earthbound",
        SpaceStage.Rocketry => "Rocketry",
        SpaceStage.Satellites => "Satellites",
        SpaceStage.CrewedFlight => "Crewed flight",
        SpaceStage.SpaceStation => "Space station",
        SpaceStage.LunarBase => "Lunar base",
        SpaceStage.OffWorldColony => "Off-world colony",
        _ => stage.ToString()
    };

    public static string HomelandName(HomelandClimate climate) => climate switch
    {
        HomelandClimate.Temperate => "Temperate",
        HomelandClimate.Tropical => "Tropical",
        HomelandClimate.Arid => "Arid",
        HomelandClimate.Cold => "Cold",
        HomelandClimate.Highland => "Highland",
        _ => climate.ToString()
    };

    public static Color ProjectColor(ProjectKind kind) => kind switch
    {
        ProjectKind.Research => new Color(110, 180, 255),
        ProjectKind.Economic => new Color(255, 206, 92),
        ProjectKind.Space => new Color(180, 150, 255),
        ProjectKind.Military => new Color(255, 110, 96),
        ProjectKind.Environmental => new Color(110, 220, 130),
        _ => Color.Gray
    };

    public static Color RelationColor(DiplomaticStatus status) => status switch
    {
        DiplomaticStatus.War => new Color(255, 90, 80),
        DiplomaticStatus.Hostile => new Color(255, 160, 80),
        DiplomaticStatus.Neutral => new Color(160, 170, 190),
        DiplomaticStatus.Friendly => new Color(140, 210, 255),
        DiplomaticStatus.Allied => new Color(110, 220, 130),
        _ => Color.Gray
    };

    /// <summary>Military tint: pale khaki (weak) to deep red (strongest nation).</summary>
    public static Color MilitaryColor(float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        var a = new Color(150, 150, 110);
        var b = new Color(214, 150, 60);
        var c = new Color(200, 40, 40);
        return t < 0.5f ? Color.Lerp(a, b, t * 2f) : Color.Lerp(b, c, (t - 0.5f) * 2f);
    }

    /// <summary>Internet penetration: dark navy (offline) to bright cyan (fully online).</summary>
    public static Color InternetColor(float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        var a = new Color(40, 46, 70);
        var b = new Color(50, 110, 190);
        var c = new Color(110, 240, 255);
        return t < 0.5f ? Color.Lerp(a, b, t * 2f) : Color.Lerp(b, c, (t - 0.5f) * 2f);
    }

    /// <summary>Electrification: dark (unlit) to warm yellow (fully electrified).</summary>
    public static Color ElectrificationColor(float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        var a = new Color(56, 50, 58);
        var b = new Color(150, 120, 60);
        var c = new Color(255, 222, 110);
        return t < 0.5f ? Color.Lerp(a, b, t * 2f) : Color.Lerp(b, c, (t - 0.5f) * 2f);
    }

    public static Color PostureColor(NationalPosture posture) => posture switch
    {
        NationalPosture.Develop => new Color(110, 200, 140),
        NationalPosture.Expand => new Color(170, 210, 90),
        NationalPosture.Research => new Color(110, 180, 255),
        NationalPosture.Fortify => new Color(160, 170, 200),
        NationalPosture.Militarize => new Color(240, 150, 70),
        NationalPosture.Conquer => new Color(255, 84, 72),
        NationalPosture.SpaceRace => new Color(186, 150, 255),
        NationalPosture.Recover => new Color(230, 200, 110),
        _ => Color.Gray
    };

    public static string PostureName(NationalPosture posture) => posture switch
    {
        NationalPosture.SpaceRace => "Space race",
        _ => posture.ToString()
    };

    /// <summary>Three-letter tag for compact chips (nation cards).</summary>
    public static string PostureTag(NationalPosture posture) => posture switch
    {
        NationalPosture.Develop => "DEV",
        NationalPosture.Expand => "EXP",
        NationalPosture.Research => "SCI",
        NationalPosture.Fortify => "DEF",
        NationalPosture.Militarize => "ARM",
        NationalPosture.Conquer => "WAR",
        NationalPosture.SpaceRace => "SPC",
        NationalPosture.Recover => "REC",
        _ => "?"
    };

    public static readonly Color PowerLine = new Color(255, 214, 90);
    public static readonly Color DataCable = new Color(90, 230, 255);
    public static readonly Color Railroad = new Color(60, 44, 36);
    public static readonly Color TradeRoute = new Color(255, 206, 92);
    public static readonly Color Radiation = new Color(170, 255, 60);
    public static readonly Color Blackout = new Color(200, 40, 40);

    // ------------------------------------------------------------------
    // Cities, migrations and intelligence
    // ------------------------------------------------------------------

    public static readonly MigrationKind[] AllMigrationKinds = Enum.GetValues<MigrationKind>();
    public static readonly IntelligenceMission[] AllMissions = Enum.GetValues<IntelligenceMission>();

    public static string SpecializationName(CitySpecialization spec) => spec switch
    {
        CitySpecialization.Farming => "Farming",
        CitySpecialization.Fishing => "Fishing",
        CitySpecialization.Port => "Port",
        CitySpecialization.Mining => "Mining",
        CitySpecialization.Timber => "Timber",
        CitySpecialization.Trade => "Trade",
        CitySpecialization.Industrial => "Industry",
        CitySpecialization.Academic => "University",
        CitySpecialization.Holy => "Holy city",
        CitySpecialization.Fortress => "Fortress",
        CitySpecialization.Capital => "Seat of government",
        _ => spec.ToString()
    };

    public static string StyleName(CityStyle style) => style switch
    {
        CityStyle.Timber => "Timber halls",
        CityStyle.Stone => "Stone and red tiles",
        CityStyle.Adobe => "Adobe and domes",
        CityStyle.Stilt => "Stilt houses",
        CityStyle.Pagoda => "Pagoda roofs",
        CityStyle.Terraced => "Hill terraces",
        CityStyle.Modern => "Modern towers",
        CityStyle.Futuristic => "Arcologies",
        _ => style.ToString()
    };

    public static Color MigrationColor(MigrationKind kind) => kind switch
    {
        MigrationKind.Urbanization => new Color(250, 205, 120),
        MigrationKind.Economic => new Color(70, 205, 145),
        MigrationKind.WarRefugees => new Color(245, 70, 60),
        MigrationKind.FamineRefugees => new Color(205, 150, 70),
        MigrationKind.EpidemicRefugees => new Color(200, 235, 60),
        MigrationKind.ClimateRefugees => new Color(80, 190, 245),
        MigrationKind.DisasterRefugees => new Color(255, 135, 40),
        MigrationKind.Persecution => new Color(200, 110, 235),
        MigrationKind.Settlers => new Color(235, 240, 250),
        _ => Color.White
    };

    public static string MigrationName(MigrationKind kind) => kind switch
    {
        MigrationKind.Urbanization => "To the cities",
        MigrationKind.Economic => "Economic migrants",
        MigrationKind.WarRefugees => "War refugees",
        MigrationKind.FamineRefugees => "Famine refugees",
        MigrationKind.EpidemicRefugees => "Fleeing epidemics",
        MigrationKind.ClimateRefugees => "Climate refugees",
        MigrationKind.DisasterRefugees => "Disaster refugees",
        MigrationKind.Persecution => "Fleeing persecution",
        MigrationKind.Settlers => "Settlers",
        _ => kind.ToString()
    };

    /// <summary>Diverging colour for net migration: -1 heavy emigration, +1 heavy immigration.</summary>
    public static Color NetMigrationColor(float t)
    {
        var neutral = new Color(128, 134, 128);
        if (t < 0) return Color.Lerp(neutral, new Color(226, 96, 58), Math.Min(1f, -t));
        return Color.Lerp(neutral, new Color(52, 180, 196), Math.Min(1f, t));
    }

    public static Color MissionColor(IntelligenceMission mission) => mission switch
    {
        IntelligenceMission.GatherIntelligence => new Color(90, 180, 255),
        IntelligenceMission.StealTechnology => new Color(180, 125, 255),
        IntelligenceMission.Sabotage => new Color(255, 140, 50),
        IntelligenceMission.IncitingUnrest => new Color(240, 215, 70),
        IntelligenceMission.Assassination => new Color(240, 60, 72),
        IntelligenceMission.CounterIntelligence => new Color(90, 220, 150),
        _ => Color.White
    };

    public static string MissionName(IntelligenceMission mission) => mission switch
    {
        IntelligenceMission.GatherIntelligence => "Gathering intelligence",
        IntelligenceMission.StealTechnology => "Stealing technology",
        IntelligenceMission.Sabotage => "Sabotage",
        IntelligenceMission.IncitingUnrest => "Inciting unrest",
        IntelligenceMission.Assassination => "Assassination",
        IntelligenceMission.CounterIntelligence => "Counter-intelligence",
        _ => mission.ToString()
    };

    /// <summary>Counter-intelligence strength (0-1) as a territory tint.</summary>
    public static Color CounterIntelColor(float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return t < 0.5f
            ? Color.Lerp(new Color(150, 150, 140), new Color(120, 110, 170), t * 2f)
            : Color.Lerp(new Color(120, 110, 170), new Color(70, 40, 150), (t - 0.5f) * 2f);
    }

    public static string VehicleName(VehicleKind kind) => kind switch
    {
        VehicleKind.SailingShip => "Sailing ship",
        VehicleKind.CargoShip => "Cargo ship",
        _ => kind.ToString()
    };
}
