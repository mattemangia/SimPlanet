using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace SimPlanet;

/// <summary>
/// 3D rotating planet minimap like in SimEarth with pan and rotation controls
/// </summary>
public class PlanetMinimap3D
{
    private readonly GraphicsDevice _graphicsDevice;
    private readonly PlanetMap _map;
    private Texture2D _minimapTexture;
    private Texture2D _sphereTexture;
    private Color[] _spherePixels;
    private Color[] _terrainColors;
    private WeatherSystem? _weatherSystem;

    private float _rotation = 0;
    private float _tilt = 0; // Vertical tilt angle
    private float _cloudAnimation = 0; // For animated clouds
    private const int MinimapSize = 150;
    private const int SphereRadius = 70;

    public bool ShowClouds { get; set; } = true;
    public bool ShowStorms { get; set; } = true;

    public int PosX { get; set; } = 10;
    public int PosY { get; set; } = 420;
    public bool IsVisible { get; set; } = true;
    public bool AutoRotate { get; set; } = true;

    // Mouse interaction
    private MouseState _previousMouseState;
    private bool _isDragging = false;

    /// <summary>
    /// Check if the mouse is currently over the minimap
    /// </summary>
    public bool IsMouseOver(MouseState mouseState)
    {
        if (!IsVisible) return false;
        Rectangle minimapBounds = new Rectangle(PosX, PosY, MinimapSize, MinimapSize);
        return minimapBounds.Contains(mouseState.Position);
    }

    // Performance optimization
    private bool _isDirty = true;
    public void MarkDirty() => _isDirty = true;

    public PlanetMinimap3D(GraphicsDevice graphicsDevice, PlanetMap map)
    {
        _graphicsDevice = graphicsDevice;
        _map = map;

        _minimapTexture = new Texture2D(_graphicsDevice, MinimapSize, MinimapSize);
        _sphereTexture = new Texture2D(_graphicsDevice, _map.Width, _map.Height);
        _spherePixels = new Color[MinimapSize * MinimapSize];
        _previousMouseState = Mouse.GetState();
    }

    public void SetWeatherSystem(WeatherSystem weatherSystem)
    {
        _weatherSystem = weatherSystem;
    }

    public void Update(float deltaTime)
    {
        // Animate clouds
        _cloudAnimation += deltaTime * 0.05f;
        if (_cloudAnimation > 1.0f) _cloudAnimation -= 1.0f;

        var mouseState = Mouse.GetState();

        // Check if mouse is over minimap
        Rectangle minimapBounds = new Rectangle(PosX, PosY, MinimapSize, MinimapSize);
        bool isMouseOver = minimapBounds.Contains(mouseState.Position);

        // Handle mouse drag for manual rotation
        if (isMouseOver && mouseState.LeftButton == ButtonState.Pressed)
        {
            if (_previousMouseState.LeftButton == ButtonState.Pressed && _isDragging)
            {
                // Drag to rotate
                float dx = mouseState.X - _previousMouseState.X;
                float dy = mouseState.Y - _previousMouseState.Y;

                _rotation -= dx * 0.01f; // Horizontal rotation
                _tilt += dy * 0.01f; // Vertical tilt

                // Clamp tilt to prevent flipping
                _tilt = Math.Clamp(_tilt, -MathF.PI / 3, MathF.PI / 3);

                // Normalize rotation
                if (_rotation > MathF.PI * 2) _rotation -= MathF.PI * 2;
                if (_rotation < 0) _rotation += MathF.PI * 2;

                _isDirty = true;
                AutoRotate = false; // Disable auto-rotate when manually controlled
            }
            _isDragging = true;
        }
        else
        {
            _isDragging = false;
        }

        // Auto-rotate when not manually controlled
        if (AutoRotate && !_isDragging)
        {
            _rotation += deltaTime * 0.5f; // Rotate slowly
            if (_rotation > MathF.PI * 2)
                _rotation -= MathF.PI * 2;
            _isDirty = true; // Need to re-render when rotating
        }

        // Right-click to reset to default view and re-enable auto-rotate
        if (isMouseOver && mouseState.RightButton == ButtonState.Pressed &&
            _previousMouseState.RightButton == ButtonState.Released)
        {
            _rotation = 0;
            _tilt = 0;
            AutoRotate = true;
            _isDirty = true;
        }

        _previousMouseState = mouseState;
    }

    public void UpdateTexture(TerrainRenderer terrainRenderer)
    {
        // Performance optimization: only update when data has changed or rotating
        if (!_isDirty)
            return;

        // Get current terrain colors
        _terrainColors = new Color[_map.Width * _map.Height];

        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                var cell = _map.Cells[x, y];
                _terrainColors[y * _map.Width + x] = GetCellColor(cell);
            }
        }

        _sphereTexture.SetData(_terrainColors);

        // Render 3D sphere
        Render3DSphere();
        _isDirty = false; // Clear dirty flag after update
    }

    private Color GetCellColor(TerrainCell cell)
    {
        // Get base terrain color
        Color terrainColor;

        if (cell.IsIce)
            terrainColor = new Color(240, 250, 255);
        else if (cell.IsWater)
        {
            if (cell.Elevation < -0.5f)
                terrainColor = new Color(0, 50, 120);
            else
                terrainColor = new Color(20, 100, 180);
        }
        else if (cell.Elevation > 0.7f)
            terrainColor = new Color(140, 130, 120); // Mountains
        else if (cell.IsForest)
            terrainColor = new Color(34, 139, 34);
        else if (cell.IsDesert)
            terrainColor = new Color(230, 200, 140);
        else if (cell.Rainfall > 0.4f)
            terrainColor = new Color(100, 160, 80); // Grassland
        else
            terrainColor = new Color(180, 160, 100); // Plains

        // Apply cloud cover overlay if enabled
        if (ShowClouds && _weatherSystem != null)
        {
            var met = cell.GetMeteorology();
            float cloudCover = met.CloudCover;

            if (cloudCover > 0.1f)
            {
                // White clouds with transparency based on cloud density (lighter for minimap)
                byte alpha = (byte)(cloudCover * 120); // Max alpha 120 (reduced from 200)
                Color cloudColor = new Color((byte)255, (byte)255, (byte)255, alpha);

                // Blend clouds with terrain (more transparent for minimap)
                float cloudAlpha = cloudCover * 0.4f; // Max 40% cloud coverage visible (reduced from 80%)
                terrainColor = Color.Lerp(terrainColor, cloudColor, cloudAlpha);
            }
        }

        // Show storms as darker clouds
        if (ShowStorms && _weatherSystem != null)
        {
            var met = cell.GetMeteorology();
            if (met.InStorm || met.Precipitation > 0.5f)
            {
                // Darken for storm clouds
                terrainColor = Color.Lerp(terrainColor, new Color(80, 80, 100), 0.4f);
            }
        }

        return terrainColor;
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

                // Apply horizontal rotation first
                float rotatedX = dx * MathF.Cos(_rotation) - z * MathF.Sin(_rotation);
                float rotatedZ = dx * MathF.Sin(_rotation) + z * MathF.Cos(_rotation);

                // Apply tilt transformation
                float tiltedY = dy * MathF.Cos(_tilt) - rotatedZ * MathF.Sin(_tilt);
                float tiltedZ = dy * MathF.Sin(_tilt) + rotatedZ * MathF.Cos(_tilt);

                // Convert to spherical coordinates for texture mapping
                // Longitude (around equator): atan2 gives angle in horizontal plane
                float longitude = MathF.Atan2(rotatedX, tiltedZ);

                // Latitude (from north pole to south pole): asin for proper latitude
                float latitude = MathF.Asin(Math.Clamp(tiltedY / SphereRadius, -1f, 1f));

                // Map to texture coordinates
                // Longitude: -PI to PI -> 0 to 1 (wraps around)
                float u = (longitude + MathF.PI) / (2 * MathF.PI);

                // Latitude: -PI/2 to PI/2 -> 0 to 1 (north pole to south pole)
                float v = (latitude + MathF.PI / 2) / MathF.PI;

                // Wrap u coordinate for seamless horizontal wrapping
                u = u % 1.0f;
                if (u < 0) u += 1.0f;

                int texX = (int)(u * _map.Width) % _map.Width;
                int texY = (int)(v * _map.Height);
                texY = Math.Clamp(texY, 0, _map.Height - 1);

                // Sample texture
                Color baseColor = _terrainColors != null ? _terrainColors[texY * _map.Width + texX] : Color.Gray;

                // Add cloud layer if enabled
                if (ShowClouds && _weatherSystem != null)
                {
                    var cell = _map.Cells[texX, texY];
                    var met = cell.GetMeteorology();

                    // Cloud cover with animation
                    float cloudCover = met.CloudCover;

                    // Animate clouds (drift with wind)
                    float cloudOffset = _cloudAnimation + (met.WindSpeedX * 0.01f);
                    float animatedU = (u + cloudOffset) % 1.0f;
                    if (animatedU < 0) animatedU += 1.0f; // Handle negative modulo (when wind is negative)
                    int animTexX = (int)(animatedU * _map.Width) % _map.Width;

                    // Use animated position for cloud sampling
                    var animCell = _map.Cells[animTexX, texY];
                    float animCloudCover = animCell.GetMeteorology().CloudCover;

                    if (animCloudCover > 0.3f)
                    {
                        // White semi-transparent clouds (more transparent for minimap)
                        float cloudAlpha = (animCloudCover - 0.3f) * 1.0f; // 0.3-1.0 -> 0-0.7
                        cloudAlpha = Math.Clamp(cloudAlpha, 0, 0.4f); // Max 40% opacity (reduced from 80%)

                        // Blend clouds over terrain
                        Color cloudColor = new Color((byte)255, (byte)255, (byte)255, (byte)(cloudAlpha * 255));
                        baseColor = BlendColors(baseColor, cloudColor);
                    }
                }

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

    private Color BlendColors(Color bottom, Color top)
    {
        float alpha = top.A / 255f;
        return new Color(
            (byte)(bottom.R * (1 - alpha) + top.R * alpha),
            (byte)(bottom.G * (1 - alpha) + top.G * alpha),
            (byte)(bottom.B * (1 - alpha) + top.B * alpha)
        );
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

        // Draw storm systems (cyclone vortices)
        if (ShowStorms && _weatherSystem != null)
        {
            DrawStormVortices(spriteBatch);
        }

        // Draw border
        DrawCircleOutline(spriteBatch, PosX + MinimapSize / 2, PosY + MinimapSize / 2,
                         SphereRadius, Color.White, 2);
    }

    private void DrawStormVortices(SpriteBatch spriteBatch)
    {
        var storms = _weatherSystem!.GetActiveStorms();
        var pixelTexture = new Texture2D(_graphicsDevice, 1, 1);
        pixelTexture.SetData(new[] { Color.White });

        foreach (var storm in storms)
        {
            // Only draw tropical cyclones (hurricanes, typhoons)
            if (storm.Type < StormType.TropicalDepression || storm.Type > StormType.HurricaneCategory5)
                continue;

            // Convert storm position to sphere coordinates
            float u = storm.CenterX / (float)_map.Width;
            float v = storm.CenterY / (float)_map.Height;

            // Convert to longitude/latitude
            float longitude = u * 2 * MathF.PI - MathF.PI;
            float latitude = (v - 0.5f) * MathF.PI;

            // Convert to 3D point on sphere
            float sphereZ = SphereRadius * MathF.Cos(latitude) * MathF.Cos(longitude);
            float sphereX = SphereRadius * MathF.Cos(latitude) * MathF.Sin(longitude);
            float sphereY = SphereRadius * MathF.Sin(latitude);

            // Apply rotation
            float rotatedX = sphereX * MathF.Cos(_rotation) - sphereZ * MathF.Sin(_rotation);
            float rotatedZ = sphereX * MathF.Sin(_rotation) + sphereZ * MathF.Cos(_rotation);

            // Apply tilt
            float tiltedY = sphereY * MathF.Cos(_tilt) - rotatedZ * MathF.Sin(_tilt);
            float tiltedZ = sphereY * MathF.Sin(_tilt) + rotatedZ * MathF.Cos(_tilt);

            // Check if visible (front of sphere)
            if (tiltedZ < 0) continue;

            // Project to 2D screen
            int screenX = PosX + MinimapSize / 2 + (int)rotatedX;
            int screenY = PosY + MinimapSize / 2 - (int)tiltedY;

            // Draw cyclone vortex
            DrawCycloneVortex(spriteBatch, pixelTexture, screenX, screenY, storm);
        }

        pixelTexture.Dispose();
    }

    private void DrawCycloneVortex(SpriteBatch spriteBatch, Texture2D pixelTexture, int centerX, int centerY, Storm storm)
    {
        // Color based on intensity
        Color vortexColor = storm.Type switch
        {
            StormType.TropicalDepression => new Color(200, 200, 255, 180),
            StormType.TropicalStorm => new Color(255, 255, 100, 200),
            StormType.HurricaneCategory1 => new Color(255, 200, 0, 220),
            StormType.HurricaneCategory2 => new Color(255, 150, 0, 220),
            StormType.HurricaneCategory3 => new Color(255, 100, 0, 240),
            StormType.HurricaneCategory4 => new Color(255, 50, 0, 240),
            StormType.HurricaneCategory5 => new Color(255, 0, 0, 255),
            _ => new Color(255, 255, 255, 180)
        };

        // Draw spiral vortex
        int vortexSize = (int)(5 + storm.Intensity * 15); // 5-20 pixels
        float rotationSpeed = _cloudAnimation * 10f * storm.RotationDirection;

        // Draw multiple spiral arms
        for (int arm = 0; arm < 3; arm++)
        {
            float armAngle = (arm * MathF.PI * 2f / 3f) + rotationSpeed;

            for (float r = 0; r < vortexSize; r += 0.5f)
            {
                float angle = armAngle + r * 0.3f * storm.RotationDirection;
                int x = centerX + (int)(MathF.Cos(angle) * r);
                int y = centerY + (int)(MathF.Sin(angle) * r);

                // Fade toward edges
                float alpha = 1.0f - (r / vortexSize);
                Color pixelColor = new Color(
                    vortexColor.R,
                    vortexColor.G,
                    vortexColor.B,
                    (byte)(vortexColor.A * alpha)
                );

                spriteBatch.Draw(pixelTexture,
                    new Rectangle(x, y, 2, 2),
                    pixelColor);
            }
        }

        // Draw eye of storm for major hurricanes
        if (storm.Type >= StormType.HurricaneCategory3)
        {
            DrawCircle(spriteBatch, centerX, centerY, 2, new Color(20, 20, 20, 200));
        }
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
