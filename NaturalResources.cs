using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace SimPlanet;

/// <summary>
/// Types of natural resources that can be extracted
/// </summary>
public enum ResourceType
{
    None,

    // Metals
    Iron,           // Common, used for tools and construction
    Copper,         // Common, used for electronics and construction
    Gold,           // Rare, used for electronics and currency
    Silver,         // Rare, used for electronics and currency
    Aluminum,       // Common, lightweight metal
    Titanium,       // Rare, advanced materials

    // Energy
    Coal,           // Fossil fuel, found in sedimentary layers
    Oil,            // Liquid fossil fuel, underground reservoirs
    NaturalGas,     // Gaseous fossil fuel, often with oil
    Uranium,        // Radioactive, nuclear energy

    // Industrial Minerals
    Limestone,      // Construction, cement
    Granite,        // Construction, monuments
    Salt,           // Food, chemical industry
    Sulfur,         // Chemical industry
    Phosphate,      // Fertilizers

    // Precious/Rare
    Diamond,        // Industrial and luxury
    Emerald,        // Luxury
    Ruby,           // Luxury
    Platinum        // Industrial and luxury
}

/// <summary>
/// Tech level required to extract a resource
/// </summary>
public enum ExtractionTech
{
    Primitive,      // Stone age mining (surface ores)
    Medieval,       // Deep mining, basic smelting
    Industrial,     // Oil drilling, advanced mining
    Modern,         // Advanced extraction, offshore drilling
    Advanced        // Deep sea, asteroid mining
}

/// <summary>
/// Resource deposit information
/// </summary>
public class ResourceDeposit
{
    public ResourceType Type { get; set; }
    public float Amount { get; set; }           // Total amount (0-1)
    public float Concentration { get; set; }    // Quality (0-1)
    public float Depth { get; set; }            // How deep (0=surface, 1=very deep)
    public bool Discovered { get; set; }        // Has a civilization found it?
    public ExtractionTech RequiredTech { get; set; }

    public ResourceDeposit(ResourceType type, float amount, float concentration, float depth)
    {
        Type = type;
        Amount = amount;
        Concentration = concentration;
        Depth = depth;
        Discovered = false;
        RequiredTech = GetRequiredTech(type);
    }

    private static ExtractionTech GetRequiredTech(ResourceType type)
    {
        return type switch
        {
            ResourceType.Iron => ExtractionTech.Primitive,
            ResourceType.Copper => ExtractionTech.Primitive,
            ResourceType.Gold => ExtractionTech.Medieval,
            ResourceType.Silver => ExtractionTech.Medieval,
            ResourceType.Coal => ExtractionTech.Medieval,
            ResourceType.Aluminum => ExtractionTech.Industrial,
            ResourceType.Oil => ExtractionTech.Industrial,
            ResourceType.NaturalGas => ExtractionTech.Industrial,
            ResourceType.Titanium => ExtractionTech.Modern,
            ResourceType.Uranium => ExtractionTech.Modern,
            ResourceType.Platinum => ExtractionTech.Advanced,
            ResourceType.Diamond => ExtractionTech.Medieval,
            _ => ExtractionTech.Primitive
        };
    }
}

/// <summary>
/// Extension methods for resource data on terrain cells (now uses embedded data for performance)
/// </summary>
public static class ResourceExtensions
{
    // Extension methods now simply access embedded property (maintains backward compatibility)
    public static List<ResourceDeposit> GetResources(this TerrainCell cell)
    {
        return cell.Resources;
    }

    public static void AddResource(this TerrainCell cell, ResourceDeposit deposit)
    {
        cell.Resources.Add(deposit);
    }

    public static bool HasResource(this TerrainCell cell, ResourceType type)
    {
        return cell.Resources.Exists(r => r.Type == type && r.Amount > 0.01f);
    }

    public static ResourceDeposit? GetResourceDeposit(this TerrainCell cell, ResourceType type)
    {
        return cell.Resources.Find(r => r.Type == type);
    }

    public static float ExtractResource(this TerrainCell cell, ResourceType type, float amount)
    {
        var deposit = cell.GetResourceDeposit(type);
        if (deposit == null || deposit.Amount <= 0) return 0;

        float extracted = Math.Min(amount, deposit.Amount);
        deposit.Amount -= extracted;

        return extracted * deposit.Concentration; // Quality affects yield
    }

    // No longer needed as data is embedded in TerrainCell, but kept for API compatibility
    public static void ClearResourceData()
    {
        // No-op: data is now managed per-cell, cleared when cells are recreated
    }

    public static Color GetResourceColor(ResourceType type)
    {
        return type switch
        {
            ResourceType.Iron => new Color(180, 100, 80),
            ResourceType.Copper => new Color(200, 120, 80),
            ResourceType.Gold => new Color(255, 215, 0),
            ResourceType.Silver => new Color(192, 192, 192),
            ResourceType.Aluminum => new Color(200, 200, 220),
            ResourceType.Titanium => new Color(150, 150, 180),
            ResourceType.Coal => new Color(40, 40, 40),
            ResourceType.Oil => new Color(20, 20, 20),
            ResourceType.NaturalGas => new Color(180, 220, 255),
            ResourceType.Uranium => new Color(100, 255, 100),
            ResourceType.Limestone => new Color(240, 240, 220),
            ResourceType.Granite => new Color(150, 150, 150),
            ResourceType.Salt => new Color(255, 255, 255),
            ResourceType.Sulfur => new Color(255, 255, 0),
            ResourceType.Phosphate => new Color(200, 180, 160),
            ResourceType.Diamond => new Color(200, 255, 255),
            ResourceType.Emerald => new Color(80, 200, 120),
            ResourceType.Ruby => new Color(200, 40, 70),
            ResourceType.Platinum => new Color(220, 220, 240),
            _ => Color.Gray
        };
    }
}
