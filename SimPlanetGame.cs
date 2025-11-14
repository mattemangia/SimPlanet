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
    private AnimalEvolutionSimulator _animalEvolutionSimulator;
    private GeologicalSimulator _geologicalSimulator;
    private HydrologySimulator _hydrologySimulator;
    private WeatherSystem _weatherSystem;
    private CivilizationManager _civilizationManager;
    private BiomeSimulator _biomeSimulator;
    private DisasterManager _disasterManager;
    private ForestFireManager _forestFireManager;
    private MagnetosphereSimulator _magnetosphereSimulator;
    private PlanetStabilizer _planetStabilizer;
    private DiseaseManager _diseaseManager;

    // Menu and save/load
    private MainMenu _mainMenu;
    private SaveLoadManager _saveLoadManager;

    // Rendering
    private TerrainRenderer _terrainRenderer;
    private GameUI _ui;
    private MapOptionsUI _mapOptionsUI;
    private PlanetMinimap3D _minimap3D;
    private GeologicalEventsUI _eventsUI;
    private InteractiveControls _interactiveControls;
    private SedimentColumnViewer _sedimentViewer;
    private PlayerCivilizationControl _playerCivControl;
    private DisasterControlUI _disasterControlUI;
    private ManualPlantingTool _plantingTool;
    private DiseaseControlUI _diseaseControlUI;
    private FontRenderer _font;

    // Game state
    private GameState _gameState;
    private RenderMode _currentRenderMode = RenderMode.Terrain;

    // Input
    private KeyboardState _previousKeyState;
    private MouseState _previousMouseState;

    // Performance optimization: throttle expensive operations
    private float _globalStatsTimer = 0;
    private float _visualUpdateTimer = 0;
    private float _simulationUpdateTimer = 0;
    private const float GlobalStatsInterval = 1.0f; // Update global stats every 1 second
    private const float VisualUpdateInterval = 0.1f; // Update visuals 10 times per second
    private const float SimulationUpdateInterval = 0.033f; // Update simulation ~30 times per second

    // Map generation settings
    private MapGenerationOptions _mapOptions;

    public SimPlanetGame()
    {
        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        IsMouseVisible = true;

        // Use Reach profile for maximum cross-platform compatibility
        // (Mac M1/Intel, Linux, Windows)
        _graphics.GraphicsProfile = GraphicsProfile.Reach;

        // Set default resolution (larger window)
        _graphics.PreferredBackBufferWidth = 1600;
        _graphics.PreferredBackBufferHeight = 900;
        _graphics.IsFullScreen = false;

        // Enable window resizing
        Window.AllowUserResizing = true;
        Window.ClientSizeChanged += OnClientSizeChanged;

        _graphics.ApplyChanges();

        Window.Title = "SimPlanet - Planetary Evolution Simulator";
    }

    protected override void Initialize()
    {
        // Initialize map generation options with Earth-like defaults
        _mapOptions = new MapGenerationOptions
        {
            Seed = 12345,
            LandRatio = 0.29f,      // Earth-like 29% land
            MountainLevel = 0.6f,   // Moderate mountains
            WaterLevel = 0.0f,      // Balanced sea level
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
        _animalEvolutionSimulator = new AnimalEvolutionSimulator(_map, _mapOptions.Seed);
        _geologicalSimulator = new GeologicalSimulator(_map, _mapOptions.Seed);
        _hydrologySimulator = new HydrologySimulator(_map, _mapOptions.Seed);
        _weatherSystem = new WeatherSystem(_map, _mapOptions.Seed);
        _civilizationManager = new CivilizationManager(_map, _mapOptions.Seed);
        _biomeSimulator = new BiomeSimulator(_map, _mapOptions.Seed);
        _disasterManager = new DisasterManager(_map, _geologicalSimulator, _mapOptions.Seed);
        _forestFireManager = new ForestFireManager(_map, _mapOptions.Seed);
        _magnetosphereSimulator = new MagnetosphereSimulator(_map, _mapOptions.Seed);
        _planetStabilizer = new PlanetStabilizer(_map, _magnetosphereSimulator);
        _diseaseManager = new DiseaseManager(_map, _civilizationManager, _mapOptions.Seed);

        // Seed initial life
        _lifeSimulator.SeedInitialLife();

        // Initialize save/load manager
        _saveLoadManager = new SaveLoadManager();

        // Initialize game state
        _gameState = new GameState
        {
            Year = 0,
            TimeSpeed = 1.0f,
            IsPaused = false,
            TimeAccumulator = 0
        };

        _previousKeyState = Keyboard.GetState();
        _previousMouseState = Mouse.GetState();

        // Call base.Initialize() LAST so LoadContent() can use the initialized data
        base.Initialize();
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);

        // Create font
        _font = new FontRenderer(GraphicsDevice, 16);

        // Create renderer
        _terrainRenderer = new TerrainRenderer(_map, GraphicsDevice);
        _terrainRenderer.CellSize = 4;

        // Create UI
        _ui = new GameUI(_spriteBatch, _font, _map, GraphicsDevice);
        _ui.SetManagers(_civilizationManager, _weatherSystem);
        _ui.SetAnimalEvolutionSimulator(_animalEvolutionSimulator);
        _ui.SetPlanetStabilizer(_planetStabilizer);
        _mapOptionsUI = new MapOptionsUI(_spriteBatch, _font, GraphicsDevice);
        _minimap3D = new PlanetMinimap3D(GraphicsDevice, _map);
        _eventsUI = new GeologicalEventsUI(_spriteBatch, _font, GraphicsDevice);
        _eventsUI.SetSimulators(_geologicalSimulator, _hydrologySimulator);
        _interactiveControls = new InteractiveControls(GraphicsDevice, _font, _map);
        _sedimentViewer = new SedimentColumnViewer(GraphicsDevice, _font, _map);
        _playerCivControl = new PlayerCivilizationControl(GraphicsDevice, _font, _civilizationManager);
        _disasterControlUI = new DisasterControlUI(GraphicsDevice, _font, _disasterManager, _map);
        _plantingTool = new ManualPlantingTool(_map, GraphicsDevice, _font);
        _diseaseControlUI = new DiseaseControlUI(GraphicsDevice, _font, _diseaseManager, _map, _civilizationManager);

        // Create main menu
        _mainMenu = new MainMenu(GraphicsDevice, _font);
    }

    protected override void Update(GameTime gameTime)
    {
        var keyState = Keyboard.GetState();
        var mouseState = Mouse.GetState();

        // Handle menu navigation
        if (_mainMenu.CurrentScreen != GameScreen.InGame)
        {
            // Show map options UI when on NewGame screen
            if (_mainMenu.CurrentScreen == GameScreen.NewGame)
            {
                _mapOptionsUI.IsVisible = true;

                // Update map options UI (handles mouse interactions)
                if (_mapOptionsUI.Update(mouseState, _mapOptions))
                {
                    _mainMenu.CurrentScreen = GameScreen.MainMenu;
                }

                // Check if Generate button was clicked
                if (_mapOptionsUI.GenerateRequested)
                {
                    StartNewGame();
                }

                // Update preview
                _mapOptionsUI.UpdatePreview(_mapOptions);
            }
            else
            {
                _mapOptionsUI.IsVisible = false;
            }

            var menuAction = _mainMenu.HandleInput(keyState, _previousKeyState, mouseState);
            HandleMenuAction(menuAction);

            _previousKeyState = keyState;
            _previousMouseState = mouseState;
            base.Update(gameTime);
            return;
        }

        _mapOptionsUI.IsVisible = false;

        // Handle in-game input
        HandleInput(keyState);

        _previousKeyState = keyState;

        // Update simulation if not paused
        if (!_gameState.IsPaused && _mainMenu.CurrentScreen == GameScreen.InGame)
        {
            float realDeltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
            float deltaTime = realDeltaTime * _gameState.TimeSpeed;

            // Accumulate time for year tracking
            _gameState.TimeAccumulator += deltaTime;

            // Each "year" is 10 seconds of real time at 1x speed
            if (_gameState.TimeAccumulator >= 10.0f)
            {
                _gameState.Year++;
                _gameState.TimeAccumulator -= 10.0f;
            }

            // Performance: Throttle simulation updates to ~30 FPS instead of 60 FPS
            _simulationUpdateTimer += realDeltaTime;
            if (_simulationUpdateTimer >= SimulationUpdateInterval)
            {
                // Accumulate time since last update for more accurate simulation
                float simDeltaTime = deltaTime * (_simulationUpdateTimer / realDeltaTime);

                // Update simulators
                _climateSimulator.Update(simDeltaTime);
                _atmosphereSimulator.Update(simDeltaTime);
                _weatherSystem.Update(simDeltaTime, _gameState.Year);
                _lifeSimulator.Update(simDeltaTime, _geologicalSimulator, _weatherSystem);
                _animalEvolutionSimulator.Update(simDeltaTime, _gameState.Year);
                _geologicalSimulator.Update(simDeltaTime, _gameState.Year);
                _hydrologySimulator.Update(simDeltaTime);
                _civilizationManager.Update(simDeltaTime, _gameState.Year);
                _diseaseManager.Update(simDeltaTime, _gameState.Year);
                _biomeSimulator.Update(simDeltaTime);
                _disasterManager.Update(simDeltaTime, _gameState.Year);
                _forestFireManager.Update(simDeltaTime, _weatherSystem, _civilizationManager);
                _magnetosphereSimulator.Update(simDeltaTime, _gameState.Year);
                _planetStabilizer.Update(simDeltaTime);

                _simulationUpdateTimer = 0;
            }

            // Performance optimization: Update global stats only once per second
            _globalStatsTimer += realDeltaTime;
            if (_globalStatsTimer >= GlobalStatsInterval)
            {
                UpdateGlobalStats();
                _globalStatsTimer = 0;
            }

            // Performance optimization: Mark terrain for visual update periodically
            _visualUpdateTimer += realDeltaTime;
            if (_visualUpdateTimer >= VisualUpdateInterval)
            {
                _terrainRenderer.MarkDirty();
                _minimap3D.MarkDirty();
                _visualUpdateTimer = 0;
            }

            // Update UI systems
            _minimap3D.Update(deltaTime);
            _eventsUI.Update(_gameState.Year);
            _interactiveControls.Update(deltaTime);
            _sedimentViewer.Update(Mouse.GetState(), _terrainRenderer.CellSize,
                _terrainRenderer.CameraX, _terrainRenderer.CameraY, _terrainRenderer.ZoomLevel);
            _playerCivControl.Update(Mouse.GetState());
            _disasterControlUI.Update(Mouse.GetState(), _gameState.Year, _terrainRenderer.CellSize,
                _terrainRenderer.CameraX, _terrainRenderer.CameraY, _terrainRenderer.ZoomLevel);
            _diseaseControlUI.Update(Mouse.GetState(), _previousMouseState, keyState);
            _plantingTool.Update(Mouse.GetState(), _terrainRenderer.CellSize,
                _terrainRenderer.CameraX, _terrainRenderer.CameraY, _terrainRenderer.ZoomLevel,
                _civilizationManager, _gameState.Year);

            // Update day/night cycle (24 hours = 1 day)
            _terrainRenderer.DayNightTime += deltaTime * 2.4f; // Complete cycle in 10 seconds at 1x speed
            if (_terrainRenderer.DayNightTime >= 24.0f)
            {
                _terrainRenderer.DayNightTime -= 24.0f;
            }

            // Auto-enable day/night when time is very slow
            if (_gameState.TimeSpeed <= 0.5f)
            {
                _terrainRenderer.ShowDayNight = true;
            }
        }

        base.Update(gameTime);
    }

    private void HandleInput(KeyboardState keyState)
    {
        // ESC opens pause menu (not quit)
        if (keyState.IsKeyDown(Keys.Escape) && _previousKeyState.IsKeyUp(Keys.Escape))
        {
            _mainMenu.CurrentScreen = GameScreen.PauseMenu;
            return;
        }

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

        // Meteorology view modes (F1-F4)
        if (keyState.IsKeyDown(Keys.F1) && _previousKeyState.IsKeyUp(Keys.F1))
            _currentRenderMode = RenderMode.Clouds;
        if (keyState.IsKeyDown(Keys.F2) && _previousKeyState.IsKeyUp(Keys.F2))
            _currentRenderMode = RenderMode.Wind;
        if (keyState.IsKeyDown(Keys.F3) && _previousKeyState.IsKeyUp(Keys.F3))
            _currentRenderMode = RenderMode.Pressure;
        if (keyState.IsKeyDown(Keys.F4) && _previousKeyState.IsKeyUp(Keys.F4))
            _currentRenderMode = RenderMode.Storms;

        // Biome view mode (F10)
        if (keyState.IsKeyDown(Keys.F10) && _previousKeyState.IsKeyUp(Keys.F10))
            _currentRenderMode = RenderMode.Biomes;

        // Resources view mode (F11)
        if (keyState.IsKeyDown(Keys.F11) && _previousKeyState.IsKeyUp(Keys.F11))
            _currentRenderMode = RenderMode.Resources;

        // Toggle day/night cycle (C key)
        if (keyState.IsKeyDown(Keys.C) && _previousKeyState.IsKeyUp(Keys.C))
        {
            _terrainRenderer.ShowDayNight = !_terrainRenderer.ShowDayNight;
        }

        // Mouse controls for pan and zoom
        var mouseState = Mouse.GetState();

        // Middle mouse button for panning
        if (mouseState.MiddleButton == ButtonState.Pressed)
        {
            if (_previousMouseState.MiddleButton == ButtonState.Pressed)
            {
                float dx = mouseState.X - _previousMouseState.X;
                float dy = mouseState.Y - _previousMouseState.Y;
                _terrainRenderer.CameraX -= dx;
                _terrainRenderer.CameraY -= dy;
            }
        }

        // Mouse wheel for zoom
        int scrollDelta = mouseState.ScrollWheelValue - _previousMouseState.ScrollWheelValue;
        if (scrollDelta != 0)
        {
            float zoomChange = scrollDelta > 0 ? 1.1f : 0.9f;
            _terrainRenderer.ZoomLevel = Math.Clamp(_terrainRenderer.ZoomLevel * zoomChange, 0.5f, 4.0f);
        }

        _previousMouseState = mouseState;

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

        // Toggle map options menu (in-game)
        if (keyState.IsKeyDown(Keys.M) && _previousKeyState.IsKeyUp(Keys.M))
        {
            _mapOptionsUI.IsVisible = !_mapOptionsUI.IsVisible;
            if (_mapOptionsUI.IsVisible)
            {
                _mapOptionsUI.NeedsPreviewUpdate = true;
            }
        }

        // Map option controls (only when menu is visible in-game)
        if (_mapOptionsUI.IsVisible)
        {
            _mapOptionsUI.Update(Mouse.GetState(), _mapOptions);
            _mapOptionsUI.UpdatePreview(_mapOptions);
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

        // Control civilization (G key)
        if (keyState.IsKeyDown(Keys.G) && _previousKeyState.IsKeyUp(Keys.G))
        {
            _playerCivControl.OpenCivilizationSelector();
        }

        // Toggle disaster control (D key)
        if (keyState.IsKeyDown(Keys.D) && _previousKeyState.IsKeyUp(Keys.D))
        {
            _disasterControlUI.IsVisible = !_disasterControlUI.IsVisible;
        }

        // Toggle disease control (K key for disease/sickness)
        if (keyState.IsKeyDown(Keys.K) && _previousKeyState.IsKeyUp(Keys.K))
        {
            _diseaseControlUI.IsVisible = !_diseaseControlUI.IsVisible;
        }

        // Toggle manual planting tool (T key)
        if (keyState.IsKeyDown(Keys.T) && _previousKeyState.IsKeyUp(Keys.T))
        {
            _plantingTool.IsActive = !_plantingTool.IsActive;
        }

        // Toggle planet stabilizer (Y key)
        if (keyState.IsKeyDown(Keys.Y) && _previousKeyState.IsKeyUp(Keys.Y))
        {
            _planetStabilizer.IsActive = !_planetStabilizer.IsActive;
        }

        // Quick save (F5)
        if (keyState.IsKeyDown(Keys.F5) && _previousKeyState.IsKeyUp(Keys.F5))
        {
            QuickSave();
        }

        // Quick load (F9)
        if (keyState.IsKeyDown(Keys.F9) && _previousKeyState.IsKeyUp(Keys.F9))
        {
            QuickLoad();
        }
    }

    private void HandleMenuAction(MenuAction action)
    {
        switch (action)
        {
            case MenuAction.NewGame:
            case MenuAction.ShowMapOptions:
                _mapOptionsUI.IsVisible = true;
                _mapOptionsUI.NeedsPreviewUpdate = true;
                break;
            case MenuAction.CancelNewGame:
                _mapOptionsUI.IsVisible = false;
                break;
            case MenuAction.LoadGame:
                LoadGame(_mainMenu.GetSelectedSaveName());
                break;
            case MenuAction.SaveGame:
                SaveGame("AutoSave_" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss"));
                _mainMenu.CurrentScreen = GameScreen.InGame;
                break;
            case MenuAction.Quit:
                Exit();
                break;
        }
    }

    private void HandleMapOptionsInput(KeyboardState keyState)
    {
        // Randomize seed
        if (keyState.IsKeyDown(Keys.R) && _previousKeyState.IsKeyUp(Keys.R))
        {
            _mapOptions.Seed = new Random().Next();
            _mapOptionsUI.NeedsPreviewUpdate = true;
        }

        // Map size
        if (keyState.IsKeyDown(Keys.D1) && _previousKeyState.IsKeyUp(Keys.D1))
        {
            _mapOptions.MapWidth = 150;
            _mapOptions.MapHeight = 75;
            _mapOptionsUI.NeedsPreviewUpdate = true;
        }
        if (keyState.IsKeyDown(Keys.D2) && _previousKeyState.IsKeyUp(Keys.D2))
        {
            _mapOptions.MapWidth = 250;
            _mapOptions.MapHeight = 125;
            _mapOptionsUI.NeedsPreviewUpdate = true;
        }

        // Land ratio
        if (keyState.IsKeyDown(Keys.Q))
        {
            _mapOptions.LandRatio = Math.Max(0.05f, _mapOptions.LandRatio - 0.01f);
            _mapOptionsUI.NeedsPreviewUpdate = true;
        }
        if (keyState.IsKeyDown(Keys.W))
        {
            _mapOptions.LandRatio = Math.Min(1.0f, _mapOptions.LandRatio + 0.01f);
            _mapOptionsUI.NeedsPreviewUpdate = true;
        }

        // Mountain level
        if (keyState.IsKeyDown(Keys.A))
        {
            _mapOptions.MountainLevel = Math.Max(0.1f, _mapOptions.MountainLevel - 0.01f);
            _mapOptionsUI.NeedsPreviewUpdate = true;
        }
        if (keyState.IsKeyDown(Keys.S))
        {
            _mapOptions.MountainLevel = Math.Min(1.0f, _mapOptions.MountainLevel + 0.01f);
            _mapOptionsUI.NeedsPreviewUpdate = true;
        }

        // Water level
        if (keyState.IsKeyDown(Keys.Z))
        {
            _mapOptions.WaterLevel = Math.Max(-1.0f, _mapOptions.WaterLevel - 0.01f);
            _mapOptionsUI.NeedsPreviewUpdate = true;
        }
        if (keyState.IsKeyDown(Keys.X))
        {
            _mapOptions.WaterLevel = Math.Min(1.0f, _mapOptions.WaterLevel + 0.01f);
            _mapOptionsUI.NeedsPreviewUpdate = true;
        }

        // Persistence
        if (keyState.IsKeyDown(Keys.E))
        {
            _mapOptions.Persistence = Math.Max(0.1f, _mapOptions.Persistence - 0.01f);
            _mapOptionsUI.NeedsPreviewUpdate = true;
        }
        if (keyState.IsKeyDown(Keys.D))
        {
            _mapOptions.Persistence = Math.Min(1.0f, _mapOptions.Persistence + 0.01f);
            _mapOptionsUI.NeedsPreviewUpdate = true;
        }

        // Lacunarity
        if (keyState.IsKeyDown(Keys.C))
        {
            _mapOptions.Lacunarity = Math.Max(1.0f, _mapOptions.Lacunarity - 0.05f);
            _mapOptionsUI.NeedsPreviewUpdate = true;
        }
        if (keyState.IsKeyDown(Keys.V))
        {
            _mapOptions.Lacunarity = Math.Min(4.0f, _mapOptions.Lacunarity + 0.05f);
            _mapOptionsUI.NeedsPreviewUpdate = true;
        }

        // Presets
        if (keyState.IsKeyDown(Keys.F6) && _previousKeyState.IsKeyUp(Keys.F6))
        {
            MapOptionsUI.ApplyEarthPreset(_mapOptions);
            _mapOptionsUI.NeedsPreviewUpdate = true;
        }
        if (keyState.IsKeyDown(Keys.F7) && _previousKeyState.IsKeyUp(Keys.F7))
        {
            MapOptionsUI.ApplyMarsPreset(_mapOptions);
            _mapOptionsUI.NeedsPreviewUpdate = true;
        }
        if (keyState.IsKeyDown(Keys.F8) && _previousKeyState.IsKeyUp(Keys.F8))
        {
            MapOptionsUI.ApplyWaterWorldPreset(_mapOptions);
            _mapOptionsUI.NeedsPreviewUpdate = true;
        }
        if (keyState.IsKeyDown(Keys.F9) && _previousKeyState.IsKeyUp(Keys.F9))
        {
            MapOptionsUI.ApplyDesertWorldPreset(_mapOptions);
            _mapOptionsUI.NeedsPreviewUpdate = true;
        }

        // Generate and start game
        if (keyState.IsKeyDown(Keys.Enter) && _previousKeyState.IsKeyUp(Keys.Enter))
        {
            StartNewGame();
        }

        // Update preview
        _mapOptionsUI.UpdatePreview(_mapOptions);
    }

    private void StartNewGame()
    {
        RegeneratePlanet();
        _mapOptionsUI.IsVisible = false;
        _mainMenu.CurrentScreen = GameScreen.InGame;
    }

    private void SaveGame(string saveName)
    {
        _saveLoadManager.SaveGame(_map, _gameState, _civilizationManager,
                                  _weatherSystem, _hydrologySimulator, saveName);
    }

    private void LoadGame(string saveName)
    {
        var saveData = _saveLoadManager.LoadGame(saveName);
        if (saveData != null)
        {
            // Recreate map with saved dimensions
            _map = new PlanetMap(saveData.MapWidth, saveData.MapHeight, saveData.MapOptions);

            // Recreate simulators
            _climateSimulator = new ClimateSimulator(_map);
            _atmosphereSimulator = new AtmosphereSimulator(_map);
            _lifeSimulator = new LifeSimulator(_map);
            _animalEvolutionSimulator = new AnimalEvolutionSimulator(_map, saveData.MapOptions.Seed);
            _geologicalSimulator = new GeologicalSimulator(_map, saveData.MapOptions.Seed);
            _hydrologySimulator = new HydrologySimulator(_map, saveData.MapOptions.Seed);
            _weatherSystem = new WeatherSystem(_map, saveData.MapOptions.Seed);
            _civilizationManager = new CivilizationManager(_map, saveData.MapOptions.Seed);
            _diseaseManager = new DiseaseManager(_map, _civilizationManager, saveData.MapOptions.Seed);
            _biomeSimulator = new BiomeSimulator(_map, saveData.MapOptions.Seed);
            _disasterManager = new DisasterManager(_map, _geologicalSimulator, saveData.MapOptions.Seed);
            _forestFireManager = new ForestFireManager(_map, saveData.MapOptions.Seed);
            _magnetosphereSimulator = new MagnetosphereSimulator(_map, saveData.MapOptions.Seed);

            // Apply save data
            _saveLoadManager.ApplySaveData(saveData, _map, _gameState,
                                          _civilizationManager, _weatherSystem,
                                          _hydrologySimulator);

            // Update renderer
            _terrainRenderer.Dispose();
            _terrainRenderer = new TerrainRenderer(_map, GraphicsDevice);
            _terrainRenderer.CellSize = 4;

            // Update UI
            _ui = new GameUI(_spriteBatch, _font, _map, GraphicsDevice);
            _ui.SetManagers(_civilizationManager, _weatherSystem);
            _ui.SetPlanetStabilizer(_planetStabilizer);
            _minimap3D.Dispose();
            _minimap3D = new PlanetMinimap3D(GraphicsDevice, _map);
            _eventsUI.SetSimulators(_geologicalSimulator, _hydrologySimulator);

            _mainMenu.CurrentScreen = GameScreen.InGame;
        }
    }

    private void QuickSave()
    {
        SaveGame("QuickSave");
    }

    private void QuickLoad()
    {
        LoadGame("QuickSave");
    }

    private void RegeneratePlanet()
    {
        // Use a new random seed
        _mapOptions.Seed = new Random().Next();

        // Recreate the map
        _map = new PlanetMap(_map.Width, _map.Height, _mapOptions);

        // Clear data from old map
        TerrainCellExtensions.ClearGeologicalData();
        MeteorologicalExtensions.ClearMeteorologicalData();
        BiomeExtensions.ClearBiomeData();
        ResourceExtensions.ClearResourceData();

        // Recreate simulators
        _climateSimulator = new ClimateSimulator(_map);
        _atmosphereSimulator = new AtmosphereSimulator(_map);
        _lifeSimulator = new LifeSimulator(_map);
        _animalEvolutionSimulator = new AnimalEvolutionSimulator(_map, _mapOptions.Seed);
        _geologicalSimulator = new GeologicalSimulator(_map, _mapOptions.Seed);
        _hydrologySimulator = new HydrologySimulator(_map, _mapOptions.Seed);
        _weatherSystem = new WeatherSystem(_map, _mapOptions.Seed);
        _civilizationManager = new CivilizationManager(_map, _mapOptions.Seed);
        _diseaseManager = new DiseaseManager(_map, _civilizationManager, _mapOptions.Seed);
        _biomeSimulator = new BiomeSimulator(_map, _mapOptions.Seed);
        _disasterManager = new DisasterManager(_map, _geologicalSimulator, _mapOptions.Seed);
        _forestFireManager = new ForestFireManager(_map, _mapOptions.Seed);
        _magnetosphereSimulator = new MagnetosphereSimulator(_map, _mapOptions.Seed);
        _planetStabilizer = new PlanetStabilizer(_map, _magnetosphereSimulator);

        // Seed initial life
        _lifeSimulator.SeedInitialLife();

        // Update renderer
        _terrainRenderer.Dispose();
        _terrainRenderer = new TerrainRenderer(_map, GraphicsDevice);
        _terrainRenderer.CellSize = 4;

        // Update UI
        _ui = new GameUI(_spriteBatch, _font, _map, GraphicsDevice);
        _ui.SetManagers(_civilizationManager, _weatherSystem);
        _ui.SetPlanetStabilizer(_planetStabilizer);
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

        // Draw menu screens
        if (_mainMenu.CurrentScreen != GameScreen.InGame)
        {
            _mainMenu.Draw(_spriteBatch, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);

            // Draw map options UI if on NewGame screen
            if (_mainMenu.CurrentScreen == GameScreen.NewGame)
            {
                _mapOptionsUI.Draw(_mapOptions);
            }

            _spriteBatch.End();
            base.Draw(gameTime);
            return;
        }

        // In-game rendering
        // Set render mode (texture will auto-update when mode changes via dirty flag)
        _terrainRenderer.Mode = _currentRenderMode;

        // Update terrain texture only when dirty (performance optimization)
        _terrainRenderer.UpdateTerrainTexture();

        // Split screen layout: Info panel on left (400px), map on right
        int infoPanelWidth = 400;
        int mapAreaX = infoPanelWidth;
        int mapAreaWidth = GraphicsDevice.Viewport.Width - infoPanelWidth;
        int mapAreaHeight = GraphicsDevice.Viewport.Height;

        // Draw terrain (centered in right area)
        int mapPixelWidth = _map.Width * _terrainRenderer.CellSize;
        int mapPixelHeight = _map.Height * _terrainRenderer.CellSize;
        int offsetX = mapAreaX + (mapAreaWidth - mapPixelWidth) / 2;
        int offsetY = (mapAreaHeight - mapPixelHeight) / 2;

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

        // Draw interactive controls
        _interactiveControls.Draw(_spriteBatch);

        // Draw sediment column viewer
        _sedimentViewer.Draw(_spriteBatch, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);

        // Draw player civilization control
        _playerCivControl.Draw(_spriteBatch, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);

        // Draw disaster control UI
        _disasterControlUI.Draw(_spriteBatch, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);

        // Draw disease control UI
        _diseaseControlUI.Draw(_spriteBatch, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);

        // Draw manual planting tool
        _plantingTool.Draw(_spriteBatch, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);

        // Draw pause menu overlay if paused
        if (_mainMenu.CurrentScreen == GameScreen.PauseMenu)
        {
            _mainMenu.Draw(_spriteBatch, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);
        }

        _spriteBatch.End();

        base.Draw(gameTime);
    }

    private void OnClientSizeChanged(object sender, EventArgs e)
    {
        // Update graphics when window is resized
        if (_graphics != null && Window != null)
        {
            _graphics.PreferredBackBufferWidth = Window.ClientBounds.Width;
            _graphics.PreferredBackBufferHeight = Window.ClientBounds.Height;
            _graphics.ApplyChanges();
        }
    }

    protected override void OnExiting(object sender, EventArgs args)
    {
        // Force cleanup before exiting
        CleanupResources();
        base.OnExiting(sender, args);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            CleanupResources();
        }

        base.Dispose(disposing);
    }

    private void CleanupResources()
    {
        // Dispose all IDisposable resources
        _terrainRenderer?.Dispose();
        _font?.Dispose();
        _minimap3D?.Dispose();
        _spriteBatch?.Dispose();
        _graphics?.Dispose();
    }

    public new void Exit()
    {
        // Ensure proper cleanup and force exit
        CleanupResources();
        base.Exit();

        // Force process exit if base.Exit() doesn't work
        Environment.Exit(0);
    }
}
