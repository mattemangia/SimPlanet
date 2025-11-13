using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace SimPlanet;

/// <summary>
/// Main game class - SimEarth-like planetary simulation
/// </summary>
public class SimPlanetGame : Game
{
    private GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch;

    // Core systems
    private PlanetMap _map;
    private ClimateSimulator _climateSimulator;
    private AtmosphereSimulator _atmosphereSimulator;
    private LifeSimulator _lifeSimulator;
    private GeologicalSimulator _geologicalSimulator;
    private HydrologySimulator _hydrologySimulator;

    // Rendering
    private TerrainRenderer _terrainRenderer;
    private GameUI _ui;
    private MapOptionsUI _mapOptionsUI;
    private PlanetMinimap3D _minimap3D;
    private GeologicalEventsUI _eventsUI;
    private SimpleFont _font;

    // Game state
    private GameState _gameState;
    private RenderMode _currentRenderMode = RenderMode.Terrain;

    // Input
    private KeyboardState _previousKeyState;

    // Map generation settings
    private MapGenerationOptions _mapOptions;

    public SimPlanetGame()
    {
        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        IsMouseVisible = true;

        // Set resolution
        _graphics.PreferredBackBufferWidth = 1280;
        _graphics.PreferredBackBufferHeight = 720;
        _graphics.IsFullScreen = false;
        _graphics.ApplyChanges();

        Window.Title = "SimPlanet - Planetary Evolution Simulator";
    }

    protected override void Initialize()
    {
        base.Initialize();

        // Initialize map generation options with defaults
        _mapOptions = new MapGenerationOptions
        {
            Seed = 12345,
            LandRatio = 0.3f,
            MountainLevel = 0.5f,
            WaterLevel = 0.0f,
            Octaves = 6,
            Persistence = 0.5f,
            Lacunarity = 2.0f
        };

        // Create planet map (200x100 for performance)
        _map = new PlanetMap(200, 100, _mapOptions);

        // Initialize simulators
        _climateSimulator = new ClimateSimulator(_map);
        _atmosphereSimulator = new AtmosphereSimulator(_map);
        _lifeSimulator = new LifeSimulator(_map);
        _geologicalSimulator = new GeologicalSimulator(_map, _mapOptions.Seed);
        _hydrologySimulator = new HydrologySimulator(_map, _mapOptions.Seed);

        // Seed initial life
        _lifeSimulator.SeedInitialLife();

        // Initialize game state
        _gameState = new GameState
        {
            Year = 0,
            TimeSpeed = 1.0f,
            IsPaused = false,
            TimeAccumulator = 0
        };

        _previousKeyState = Keyboard.GetState();
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);

        // Create font
        _font = new SimpleFont(GraphicsDevice);

        // Create renderer
        _terrainRenderer = new TerrainRenderer(_map, GraphicsDevice);
        _terrainRenderer.CellSize = 4;

        // Create UI
        _ui = new GameUI(_spriteBatch, _font, _map, GraphicsDevice);
        _mapOptionsUI = new MapOptionsUI(_spriteBatch, _font, GraphicsDevice);
        _minimap3D = new PlanetMinimap3D(GraphicsDevice, _map);
        _eventsUI = new GeologicalEventsUI(_spriteBatch, _font, GraphicsDevice);
        _eventsUI.SetSimulators(_geologicalSimulator, _hydrologySimulator);
    }

    protected override void Update(GameTime gameTime)
    {
        var keyState = Keyboard.GetState();

        // Handle input
        HandleInput(keyState);

        _previousKeyState = keyState;

        // Update simulation if not paused
        if (!_gameState.IsPaused)
        {
            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds * _gameState.TimeSpeed;

            // Accumulate time for year tracking
            _gameState.TimeAccumulator += deltaTime;

            // Each "year" is 10 seconds of real time at 1x speed
            if (_gameState.TimeAccumulator >= 10.0f)
            {
                _gameState.Year++;
                _gameState.TimeAccumulator -= 10.0f;
            }

            // Update simulators
            _climateSimulator.Update(deltaTime);
            _atmosphereSimulator.Update(deltaTime);
            _lifeSimulator.Update(deltaTime);
            _geologicalSimulator.Update(deltaTime, _gameState.Year);
            _hydrologySimulator.Update(deltaTime);

            // Update global temperature average
            UpdateGlobalStats();

            // Update UI systems
            _minimap3D.Update(deltaTime);
            _eventsUI.Update(_gameState.Year);
        }

        base.Update(gameTime);
    }

    private void HandleInput(KeyboardState keyState)
    {
        // Quit
        if (keyState.IsKeyDown(Keys.Escape))
            Exit();

        // Only process key presses (not holds)
        if (keyState == _previousKeyState)
            return;

        // Pause/Resume
        if (keyState.IsKeyDown(Keys.Space) && _previousKeyState.IsKeyUp(Keys.Space))
        {
            _gameState.IsPaused = !_gameState.IsPaused;
        }

        // Time speed controls
        if (keyState.IsKeyDown(Keys.OemPlus) || keyState.IsKeyDown(Keys.Add))
        {
            _gameState.TimeSpeed = Math.Min(_gameState.TimeSpeed * 2.0f, 32.0f);
        }
        if (keyState.IsKeyDown(Keys.OemMinus) || keyState.IsKeyDown(Keys.Subtract))
        {
            _gameState.TimeSpeed = Math.Max(_gameState.TimeSpeed / 2.0f, 0.25f);
        }

        // View mode controls
        if (keyState.IsKeyDown(Keys.D1)) _currentRenderMode = RenderMode.Terrain;
        if (keyState.IsKeyDown(Keys.D2)) _currentRenderMode = RenderMode.Temperature;
        if (keyState.IsKeyDown(Keys.D3)) _currentRenderMode = RenderMode.Rainfall;
        if (keyState.IsKeyDown(Keys.D4)) _currentRenderMode = RenderMode.Life;
        if (keyState.IsKeyDown(Keys.D5)) _currentRenderMode = RenderMode.Oxygen;
        if (keyState.IsKeyDown(Keys.D6)) _currentRenderMode = RenderMode.CO2;
        if (keyState.IsKeyDown(Keys.D7)) _currentRenderMode = RenderMode.Elevation;
        if (keyState.IsKeyDown(Keys.D8)) _currentRenderMode = RenderMode.Geological;
        if (keyState.IsKeyDown(Keys.D9)) _currentRenderMode = RenderMode.TectonicPlates;
        if (keyState.IsKeyDown(Keys.D0)) _currentRenderMode = RenderMode.Volcanoes;

        // Seed life
        if (keyState.IsKeyDown(Keys.L) && _previousKeyState.IsKeyUp(Keys.L))
        {
            _lifeSimulator.SeedInitialLife();
        }

        // Regenerate planet
        if (keyState.IsKeyDown(Keys.R) && _previousKeyState.IsKeyUp(Keys.R))
        {
            RegeneratePlanet();
        }

        // Toggle help
        if (keyState.IsKeyDown(Keys.H) && _previousKeyState.IsKeyUp(Keys.H))
        {
            _ui.ShowHelp = !_ui.ShowHelp;
        }

        // Toggle map options menu
        if (keyState.IsKeyDown(Keys.M) && _previousKeyState.IsKeyUp(Keys.M))
        {
            _mapOptionsUI.IsVisible = !_mapOptionsUI.IsVisible;
        }

        // Map option controls (only when menu is visible)
        if (_mapOptionsUI.IsVisible)
        {
            // Land Ratio: Q/W
            if (keyState.IsKeyDown(Keys.Q) && _previousKeyState.IsKeyUp(Keys.Q))
            {
                _mapOptions.LandRatio = Math.Max(0.1f, _mapOptions.LandRatio - 0.05f);
            }
            if (keyState.IsKeyDown(Keys.W) && _previousKeyState.IsKeyUp(Keys.W))
            {
                _mapOptions.LandRatio = Math.Min(0.9f, _mapOptions.LandRatio + 0.05f);
            }

            // Mountain Level: A/S
            if (keyState.IsKeyDown(Keys.A) && _previousKeyState.IsKeyUp(Keys.A))
            {
                _mapOptions.MountainLevel = Math.Max(0.0f, _mapOptions.MountainLevel - 0.1f);
            }
            if (keyState.IsKeyDown(Keys.S) && _previousKeyState.IsKeyUp(Keys.S))
            {
                _mapOptions.MountainLevel = Math.Min(1.0f, _mapOptions.MountainLevel + 0.1f);
            }

            // Water Level: Z/X
            if (keyState.IsKeyDown(Keys.Z) && _previousKeyState.IsKeyUp(Keys.Z))
            {
                _mapOptions.WaterLevel = Math.Max(-1.0f, _mapOptions.WaterLevel - 0.1f);
            }
            if (keyState.IsKeyDown(Keys.X) && _previousKeyState.IsKeyUp(Keys.X))
            {
                _mapOptions.WaterLevel = Math.Min(1.0f, _mapOptions.WaterLevel + 0.1f);
            }
        }

        // Toggle minimap
        if (keyState.IsKeyDown(Keys.P) && _previousKeyState.IsKeyUp(Keys.P))
        {
            _minimap3D.IsVisible = !_minimap3D.IsVisible;
        }

        // Toggle event overlays
        if (keyState.IsKeyDown(Keys.V) && _previousKeyState.IsKeyUp(Keys.V))
        {
            _eventsUI.ShowVolcanoes = !_eventsUI.ShowVolcanoes;
        }
        if (keyState.IsKeyDown(Keys.B) && _previousKeyState.IsKeyUp(Keys.B))
        {
            _eventsUI.ShowRivers = !_eventsUI.ShowRivers;
        }
        if (keyState.IsKeyDown(Keys.N) && _previousKeyState.IsKeyUp(Keys.N))
        {
            _eventsUI.ShowPlates = !_eventsUI.ShowPlates;
        }
    }

    private void RegeneratePlanet()
    {
        // Use a new random seed
        _mapOptions.Seed = new Random().Next();

        // Recreate the map
        _map = new PlanetMap(_map.Width, _map.Height, _mapOptions);

        // Clear geological data from old map
        TerrainCellExtensions.ClearGeologicalData();

        // Recreate simulators
        _climateSimulator = new ClimateSimulator(_map);
        _atmosphereSimulator = new AtmosphereSimulator(_map);
        _lifeSimulator = new LifeSimulator(_map);
        _geologicalSimulator = new GeologicalSimulator(_map, _mapOptions.Seed);
        _hydrologySimulator = new HydrologySimulator(_map, _mapOptions.Seed);

        // Seed initial life
        _lifeSimulator.SeedInitialLife();

        // Update renderer
        _terrainRenderer.Dispose();
        _terrainRenderer = new TerrainRenderer(_map, GraphicsDevice);
        _terrainRenderer.CellSize = 4;

        // Update UI
        _ui = new GameUI(_spriteBatch, _font, _map, GraphicsDevice);
        _minimap3D.Dispose();
        _minimap3D = new PlanetMinimap3D(GraphicsDevice, _map);
        _eventsUI.SetSimulators(_geologicalSimulator, _hydrologySimulator);

        // Reset game state
        _gameState.Year = 0;
        _gameState.TimeAccumulator = 0;
    }

    private void UpdateGlobalStats()
    {
        float totalTemp = 0;
        int count = 0;

        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                totalTemp += _map.Cells[x, y].Temperature;
                count++;
            }
        }

        _map.GlobalTemperature = totalTemp / count;
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Color.Black);

        _spriteBatch.Begin(samplerState: SamplerState.PointClamp);

        // Update terrain texture if render mode changed
        _terrainRenderer.Mode = _currentRenderMode;
        _terrainRenderer.UpdateTerrainTexture();

        // Draw terrain (centered)
        int mapPixelWidth = _map.Width * _terrainRenderer.CellSize;
        int mapPixelHeight = _map.Height * _terrainRenderer.CellSize;
        int offsetX = (GraphicsDevice.Viewport.Width - mapPixelWidth) / 2;
        int offsetY = (GraphicsDevice.Viewport.Height - mapPixelHeight) / 2;

        _terrainRenderer.Draw(_spriteBatch, offsetX, offsetY);

        // Draw geological overlays (volcanoes, rivers, plates)
        _eventsUI.DrawOverlay(_map, offsetX, offsetY, _terrainRenderer.CellSize);

        // Draw UI
        _ui.Draw(_gameState, _currentRenderMode);

        // Draw map options menu (if visible)
        _mapOptionsUI.Draw(_mapOptions);

        // Draw geological events log
        _eventsUI.DrawEventLog(GraphicsDevice.Viewport.Width);

        // Draw legend
        _eventsUI.DrawLegend(GraphicsDevice.Viewport.Height);

        // Update and draw 3D minimap
        _minimap3D.UpdateTexture(_terrainRenderer);
        _minimap3D.Draw(_spriteBatch);

        _spriteBatch.End();

        base.Draw(gameTime);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _terrainRenderer?.Dispose();
            _font?.Dispose();
            _minimap3D?.Dispose();
        }

        base.Dispose(disposing);
    }
}
