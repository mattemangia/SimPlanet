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

    public void Draw(SpriteBatch spriteBatch, int screenWidth, int screenHeight)
    {
        if (!IsActive) return;

        int panelX = screenWidth - 220;
        int panelY = screenHeight - 350;
        int panelWidth = 210;
        int panelHeight = 340;

        // Background
        spriteBatch.Draw(_pixelTexture,
            new Rectangle(panelX, panelY, panelWidth, panelHeight),
            new Color(40, 30, 20, 230));

        // Border
        DrawBorder(spriteBatch, panelX, panelY, panelWidth, panelHeight, Color.Orange, 2);

        // Title
        _font.DrawString(spriteBatch, "RESOURCE TOOL",
            new Vector2(panelX + 45, panelY + 5), Color.Orange);

        int textY = panelY + 30;
        int lineHeight = 20;

        // Current resource
        _font.DrawString(spriteBatch, $"Type: {CurrentResourceType}",
            new Vector2(panelX + 10, textY), Color.White);
        textY += lineHeight;

        _font.DrawString(spriteBatch, "(R to cycle)",
            new Vector2(panelX + 10, textY), Color.Gray);
        textY += lineHeight + 5;

        // Deposit amount
        _font.DrawString(spriteBatch, $"Amount: {DepositAmount:F0}",
            new Vector2(panelX + 10, textY), Color.White);
        textY += lineHeight;

        _font.DrawString(spriteBatch, "(Scroll wheel)",
            new Vector2(panelX + 10, textY), Color.Gray);
        textY += lineHeight + 10;

        // Instructions
        _font.DrawString(spriteBatch, "RESOURCES:",
            new Vector2(panelX + 10, textY), Color.Yellow);
        textY += lineHeight;

        var resourceTypes = new[]
        {
            ResourceType.Iron, ResourceType.Copper, ResourceType.Coal,
            ResourceType.Gold, ResourceType.Silver, ResourceType.Oil,
            ResourceType.NaturalGas, ResourceType.Uranium,
            ResourceType.Platinum, ResourceType.Diamond
        };

        foreach (var type in resourceTypes)
        {
            Color color = type == CurrentResourceType ? Color.Orange : Color.Gray;
            _font.DrawString(spriteBatch, $"- {type}",
                new Vector2(panelX + 15, textY), color);
            textY += 16;
        }

        textY += 5;
        _font.DrawString(spriteBatch, "Click to place",
            new Vector2(panelX + 10, textY), Color.Yellow);
        textY += lineHeight;
        _font.DrawString(spriteBatch, "M: Toggle Tool",
            new Vector2(panelX + 10, textY), Color.Yellow);
    }

    private void DrawBorder(SpriteBatch spriteBatch, int x, int y, int width, int height, Color color, int thickness)
    {
        spriteBatch.Draw(_pixelTexture, new Rectangle(x, y, width, thickness), color);
        spriteBatch.Draw(_pixelTexture, new Rectangle(x, y + height - thickness, width, thickness), color);
        spriteBatch.Draw(_pixelTexture, new Rectangle(x, y, thickness, height), color);
        spriteBatch.Draw(_pixelTexture, new Rectangle(x + width - thickness, y, thickness, height), color);
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
