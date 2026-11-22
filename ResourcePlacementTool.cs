using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;

namespace SimPlanet;

/// <summary>
/// Tool for manually placing resources on the map
/// </summary>
public class ResourcePlacementTool
{
    private readonly PlanetMap _map;
    private readonly GraphicsDevice _graphicsDevice;
    private readonly FontRenderer _font;
    private Texture2D _pixelTexture;
    private MouseState _previousMouseState;

    public bool IsActive { get; set; } = false;
    public ResourceType CurrentResourceType { get; set; } = ResourceType.Iron;
    public float DepositAmount { get; set; } = 10.0f; // Amount of resource to place

    public ResourcePlacementTool(PlanetMap map, GraphicsDevice graphicsDevice, FontRenderer font)
    {
        _map = map;
        _graphicsDevice = graphicsDevice;
        _font = font;

        _pixelTexture = new Texture2D(_graphicsDevice, 1, 1);
        _pixelTexture.SetData(new[] { Color.White });
    }

    public void Update(MouseState mouseState, int cellSize, float cameraX, float cameraY, float zoomLevel,
                      int mapRenderOffsetX, int mapRenderOffsetY)
    {
        if (!IsActive)
        {
            _previousMouseState = mouseState;
            return;
        }

        bool clicked = mouseState.LeftButton == ButtonState.Pressed &&
                      _previousMouseState.LeftButton == ButtonState.Released;

        // Adjust deposit amount with mouse wheel
        int scrollDelta = mouseState.ScrollWheelValue - _previousMouseState.ScrollWheelValue;
        if (scrollDelta > 0)
            DepositAmount = Math.Min(DepositAmount + 5.0f, 100.0f);
        else if (scrollDelta < 0)
            DepositAmount = Math.Max(DepositAmount - 5.0f, 5.0f);

        if (clicked)
        {
            // Convert screen coordinates to map coordinates
            float mapRelativeX = (mouseState.X - mapRenderOffsetX) + cameraX;
            float mapRelativeY = (mouseState.Y - mapRenderOffsetY) + cameraY;
            int tileX = (int)(mapRelativeX / (cellSize * zoomLevel));
            int tileY = (int)(mapRelativeY / (cellSize * zoomLevel));

            if (tileX >= 0 && tileX < _map.Width && tileY >= 0 && tileY < _map.Height)
            {
                PlaceResource(tileX, tileY);
            }
        }

        _previousMouseState = mouseState;
    }

    private void PlaceResource(int x, int y)
    {
        var cell = _map.Cells[x, y];
        var resources = cell.GetResources();

        // Check if resource already exists at this location
        var existing = resources.FirstOrDefault(r => r.Type == CurrentResourceType);
        if (existing != null)
        {
            // Add to existing deposit
            existing.Amount += DepositAmount;
            existing.Discovered = true; // Auto-discover when manually placed
        }
        else
        {
            // Create new resource deposit
            float depth = 0.3f + (float)new Random().NextDouble() * 0.4f; // Random depth 0.3-0.7
            float concentration = 0.7f + (float)new Random().NextDouble() * 0.3f; // High quality 0.7-1.0

            var newDeposit = new ResourceDeposit(CurrentResourceType, DepositAmount, concentration, depth)
            {
                RequiredTech = GetRequiredTechForResource(CurrentResourceType),
                Discovered = true // Auto-discover when manually placed
            };

            resources.Add(newDeposit);
        }
    }

    private ExtractionTech GetRequiredTechForResource(ResourceType type)
    {
        return type switch
        {
            ResourceType.Iron => ExtractionTech.Primitive,
            ResourceType.Copper => ExtractionTech.Primitive,
            ResourceType.Coal => ExtractionTech.Medieval,
            ResourceType.Gold => ExtractionTech.Medieval,
            ResourceType.Silver => ExtractionTech.Medieval,
            ResourceType.Oil => ExtractionTech.Industrial,
            ResourceType.NaturalGas => ExtractionTech.Industrial,
            ResourceType.Uranium => ExtractionTech.Modern,
            ResourceType.Platinum => ExtractionTech.Modern,
            ResourceType.Diamond => ExtractionTech.Industrial,
            _ => ExtractionTech.Primitive
        };
    }

    private static readonly ResourceType[] ListedResources =
    {
        ResourceType.Iron, ResourceType.Copper, ResourceType.Coal,
        ResourceType.Gold, ResourceType.Silver, ResourceType.Oil,
        ResourceType.NaturalGas, ResourceType.Uranium,
        ResourceType.Platinum, ResourceType.Diamond
    };

    public void Draw(SpriteBatch spriteBatch, int screenWidth, int screenHeight)
    {
        if (!IsActive) return;

        const int panelWidth = 220, rowHeight = 20;
        int panelHeight = UIWidgets.HeaderHeight + 10 + 22 + UIWidgets.RowHeight + 31 + 8 + 22 + ListedResources.Length * rowHeight + 8 + 34 + 8;
        var panel = new Rectangle(screenWidth - panelWidth - 10, screenHeight - panelHeight - 80, panelWidth, panelHeight);
        int x = panel.X + 12;
        int width = panelWidth - 24;

        int y = UIWidgets.DrawWindow(spriteBatch, panel, "RESOURCE TOOL", UITheme.Warn, closeButton: false, note: "M: close");

        // Current deposit
        y = UIWidgets.DrawSection(spriteBatch, x, y, width, "DEPOSIT");
        y = UIWidgets.DrawKeyValue(spriteBatch, x, y, width, "Type", CurrentResourceType.ToString(),
            UIWidgets.Readable(ResourceExtensions.GetResourceColor(CurrentResourceType)));
        y = UIWidgets.DrawMeterRow(spriteBatch, x, y, width, "Amount (wheel)", $"{DepositAmount:F0}", DepositAmount / 100f, UITheme.Warn) + 8;

        // All resources, the selected one highlighted
        y = UIWidgets.DrawSection(spriteBatch, x, y, width, "RESOURCES (R: NEXT)");
        foreach (var type in ListedResources)
        {
            bool selected = type == CurrentResourceType;
            if (selected)
            {
                var row = new Rectangle(x - 4, y - 1, width + 8, rowHeight);
                UITheme.FillRounded(spriteBatch, row, Color.Lerp(UITheme.ButtonBg, UITheme.Warn, 0.25f));
                UITheme.OutlineRounded(spriteBatch, row, UITheme.Warn * 0.8f);
            }
            UIWidgets.DrawSwatch(spriteBatch, new Rectangle(x + 2, y + 4, 12, 10), ResourceExtensions.GetResourceColor(type));
            UITheme.DrawText(spriteBatch, type.ToString(), new Vector2(x + 22, y + 2), selected ? Color.White : UITheme.TextDim, UITheme.FontSmall);
            y += rowHeight;
        }

        UIWidgets.DrawParagraph(spriteBatch, x, y + 8, width, "Click the map to place a deposit. It is discovered at once.", UITheme.TextMuted);
    }

    public void CycleResourceType()
    {
        CurrentResourceType = CurrentResourceType switch
        {
            ResourceType.Iron => ResourceType.Copper,
            ResourceType.Copper => ResourceType.Coal,
            ResourceType.Coal => ResourceType.Gold,
            ResourceType.Gold => ResourceType.Silver,
            ResourceType.Silver => ResourceType.Oil,
            ResourceType.Oil => ResourceType.NaturalGas,
            ResourceType.NaturalGas => ResourceType.Uranium,
            ResourceType.Uranium => ResourceType.Platinum,
            ResourceType.Platinum => ResourceType.Diamond,
            ResourceType.Diamond => ResourceType.Iron,
            _ => ResourceType.Iron
        };
    }
}
