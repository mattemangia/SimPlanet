using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace SimPlanet;

/// <summary>
/// 3D rotating planet minimap like in SimEarth
/// </summary>
public class PlanetMinimap3D
{
    private readonly GraphicsDevice _graphicsDevice;
    private readonly PlanetMap _map;
    private Texture2D _minimapTexture;
    private Texture2D _sphereTexture;
    private Color[] _spherePixels;

    private float _rotation = 0;
    private const int MinimapSize = 150;
    private const int SphereRadius = 70;

    public int PosX { get; set; } = 10;
    public int PosY { get; set; } = 420;
    public bool IsVisible { get; set; } = true;
    public bool AutoRotate { get; set; } = true;

    public PlanetMinimap3D(GraphicsDevice graphicsDevice, PlanetMap map)
    {
        _graphicsDevice = graphicsDevice;
        _map = map;

        _minimapTexture = new Texture2D(_graphicsDevice, MinimapSize, MinimapSize);
        _sphereTexture = new Texture2D(_graphicsDevice, _map.Width, _map.Height);
        _spherePixels = new Color[MinimapSize * MinimapSize];
    }

    public void Update(float deltaTime)
    {
        if (AutoRotate)
        {
            _rotation += deltaTime * 0.5f; // Rotate slowly
            if (_rotation > MathF.PI * 2)
                _rotation -= MathF.PI * 2;
        }
    }

    public void UpdateTexture(TerrainRenderer terrainRenderer)
    {
        // Get current terrain colors
        var terrainColors = new Color[_map.Width * _map.Height];

        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                var cell = _map.Cells[x, y];
                terrainColors[y * _map.Width + x] = GetCellColor(cell);
            }
        }

        _sphereTexture.SetData(terrainColors);

        // Render 3D sphere
        Render3DSphere();
    }

    private Color GetCellColor(TerrainCell cell)
    {
        // Simple color based on terrain type
        if (cell.IsIce) return new Color(240, 250, 255);
        if (cell.IsWater)
        {
            if (cell.Elevation < -0.5f)
                return new Color(0, 50, 120);
            return new Color(20, 100, 180);
        }

        if (cell.Elevation > 0.7f) return new Color(140, 130, 120); // Mountains
        if (cell.IsForest) return new Color(34, 139, 34);
        if (cell.IsDesert) return new Color(230, 200, 140);
        if (cell.Rainfall > 0.4f) return new Color(100, 160, 80); // Grassland

        return new Color(180, 160, 100); // Plains
    }

    private void Render3DSphere()
    {
        int centerX = MinimapSize / 2;
        int centerY = MinimapSize / 2;

        for (int py = 0; py < MinimapSize; py++)
        {
            for (int px = 0; px < MinimapSize; px++)
            {
                int index = py * MinimapSize + px;

                // Calculate 3D sphere coordinates
                float dx = px - centerX;
                float dy = py - centerY;
                float distFromCenter = MathF.Sqrt(dx * dx + dy * dy);

                if (distFromCenter > SphereRadius)
                {
                    _spherePixels[index] = Color.Transparent;
                    continue;
                }

                // Calculate 3D point on sphere surface
                float z = MathF.Sqrt(SphereRadius * SphereRadius - dx * dx - dy * dy);

                // Convert to spherical coordinates
                float theta = MathF.Atan2(dy, dx); // Latitude
                float phi = MathF.Acos(z / SphereRadius); // Longitude with rotation

                // Apply rotation
                phi += _rotation;

                // Map to texture coordinates
                float u = (phi / MathF.PI); // 0 to 1
                float v = (theta + MathF.PI) / (2 * MathF.PI); // 0 to 1

                // Wrap around
                u = u % 1.0f;
                if (u < 0) u += 1.0f;

                int texX = (int)(u * _map.Width) % _map.Width;
                int texY = (int)(v * _map.Height);
                texY = Math.Clamp(texY, 0, _map.Height - 1);

                // Sample texture
                Color baseColor = _sphereTexture.GetData<Color>()[texY * _map.Width + texX];

                // Apply shading based on angle to light
                float lightX = 1.0f;
                float lightY = 0.0f;
                float lightZ = 1.0f;
                float lightLen = MathF.Sqrt(lightX * lightX + lightY * lightY + lightZ * lightZ);
                lightX /= lightLen;
                lightY /= lightLen;
                lightZ /= lightLen;

                // Surface normal
                float nx = dx / SphereRadius;
                float ny = dy / SphereRadius;
                float nz = z / SphereRadius;

                // Dot product for lighting
                float lighting = Math.Max(0, nx * lightX + ny * lightY + nz * lightZ);
                lighting = 0.3f + lighting * 0.7f; // Ambient + diffuse

                _spherePixels[index] = new Color(
                    (byte)(baseColor.R * lighting),
                    (byte)(baseColor.G * lighting),
                    (byte)(baseColor.B * lighting)
                );
            }
        }

        _minimapTexture.SetData(_spherePixels);
    }

    public void Draw(SpriteBatch spriteBatch)
    {
        if (!IsVisible) return;

        // Draw background circle
        DrawCircle(spriteBatch, PosX + MinimapSize / 2, PosY + MinimapSize / 2,
                   SphereRadius + 5, new Color(20, 20, 40, 200));

        // Draw the 3D sphere
        spriteBatch.Draw(_minimapTexture,
                        new Rectangle(PosX, PosY, MinimapSize, MinimapSize),
                        Color.White);

        // Draw border
        DrawCircleOutline(spriteBatch, PosX + MinimapSize / 2, PosY + MinimapSize / 2,
                         SphereRadius, Color.White, 2);
    }

    private void DrawCircle(SpriteBatch spriteBatch, int centerX, int centerY, int radius, Color color)
    {
        var pixelTexture = new Texture2D(_graphicsDevice, 1, 1);
        pixelTexture.SetData(new[] { Color.White });

        for (int y = -radius; y <= radius; y++)
        {
            for (int x = -radius; x <= radius; x++)
            {
                if (x * x + y * y <= radius * radius)
                {
                    spriteBatch.Draw(pixelTexture,
                                   new Rectangle(centerX + x, centerY + y, 1, 1),
                                   color);
                }
            }
        }
    }

    private void DrawCircleOutline(SpriteBatch spriteBatch, int centerX, int centerY,
                                   int radius, Color color, int thickness)
    {
        var pixelTexture = new Texture2D(_graphicsDevice, 1, 1);
        pixelTexture.SetData(new[] { Color.White });

        for (int angle = 0; angle < 360; angle += 1)
        {
            float rad = angle * MathF.PI / 180f;
            for (int t = 0; t < thickness; t++)
            {
                int x = centerX + (int)((radius + t) * MathF.Cos(rad));
                int y = centerY + (int)((radius + t) * MathF.Sin(rad));
                spriteBatch.Draw(pixelTexture, new Rectangle(x, y, 1, 1), color);
            }
        }
    }

    public void Dispose()
    {
        _minimapTexture?.Dispose();
        _sphereTexture?.Dispose();
    }
}
