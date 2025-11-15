using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System.Threading;
using System.Runtime.InteropServices;

namespace SimPlanet;

/// <summary>
/// SDL2 P/Invoke declarations for window icon
/// </summary>
internal static class SDL
{
    [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr SDL_CreateRGBSurfaceFrom(
        IntPtr pixels, int width, int height, int depth, int pitch,
        uint Rmask, uint Gmask, uint Bmask, uint Amask);

    [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
    public static extern void SDL_SetWindowIcon(IntPtr window, IntPtr icon);

    [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
    public static extern void SDL_FreeSurface(IntPtr surface);
}

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
    private DivinePowersUI _divinePowersUI;
    private DisasterControlUI _disasterControlUI;
    private ManualPlantingTool _plantingTool;
    private DiseaseControlUI _diseaseControlUI;
    private ToolbarUI _toolbar;
    private PlanetaryControlsUI _planetaryControlsUI;
    private FontRenderer _font;
    private LoadingScreen _loadingScreen;

    // Game state
    private GameState _gameState;
    private RenderMode _currentRenderMode = RenderMode.Terrain;

    // Map rendering offsets (for coordinate conversion)
    private int _mapRenderOffsetX = 0;
    private int _mapRenderOffsetY = 0;

    // Input
    private KeyboardState _previousKeyState;
    private MouseState _previousMouseState;

    // Performance optimization: throttle expensive operations
    private float _globalStatsTimer = 0;
    private float _visualUpdateTimer = 0;
    private const float GlobalStatsInterval = 1.0f; // Update global stats every 1 second
    private const float VisualUpdateInterval = 0.1f; // Update visuals 10 times per second

    // Multithreading: Simulation runs on background thread, UI on main thread
    private Thread _simulationThread;
    private readonly object _simulationLock = new object();
    private bool _simulationRunning = false;
    private bool _simulationThreadActive = false;
    private DateTime _lastSimulationUpdate = DateTime.Now;

    // Map generation settings
    private MapGenerationOptions _mapOptions;

    // World generation thread
    private Thread _generationThread;
    private bool _isGenerating = false;
    private PlanetMap _newMap;

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

    private void SimulationThreadLoop()
    {
        while (_simulationThreadActive)
        {
            try
            {
                // Calculate delta time for simulation
                DateTime now = DateTime.Now;
                float deltaTime = (float)(now - _lastSimulationUpdate).TotalSeconds;
                _lastSimulationUpdate = now;

                // Clamp deltaTime to prevent huge jumps
                deltaTime = Math.Clamp(deltaTime, 0, 0.1f);

                // Check if simulation should run
                bool shouldRun = false;
                GameState gameStateCopy;

                lock (_simulationLock)
                {
                    shouldRun = _simulationRunning && !_gameState.IsPaused;
                    gameStateCopy = new GameState
                    {
                        Year = _gameState.Year,
                        TimeSpeed = _gameState.TimeSpeed,
                        IsPaused = _gameState.IsPaused,
                        TimeAccumulator = _gameState.TimeAccumulator
                    };
                }

                if (shouldRun)
                {
                    // Apply time speed
                    float simDeltaTime = deltaTime * gameStateCopy.TimeSpeed;

                    // Update time accumulator and year
                    float newAccumulator = gameStateCopy.TimeAccumulator + simDeltaTime;
                    int newYear = gameStateCopy.Year;

                    if (newAccumulator >= 10.0f)
                    {
                        newYear++;
                        newAccumulator -= 10.0f;
                    }

                    // Run all simulators (thread-safe, they only modify map data)
                    _climateSimulator.Update(simDeltaTime);
                    _atmosphereSimulator.Update(simDeltaTime);
                    _weatherSystem.Update(simDeltaTime, newYear);
                    _lifeSimulator.Update(simDeltaTime, _geologicalSimulator, _weatherSystem);
                    _animalEvolutionSimulator.Update(simDeltaTime, newYear);
                    _geologicalSimulator.Update(simDeltaTime, newYear);
                    _hydrologySimulator.Update(simDeltaTime);
                    _civilizationManager.Update(simDeltaTime, newYear);
                    _diseaseManager.Update(simDeltaTime, newYear);
                    _biomeSimulator.Update(simDeltaTime);
                    _disasterManager.Update(simDeltaTime, newYear);
                    _forestFireManager.Update(simDeltaTime, _weatherSystem, _civilizationManager);
                    _magnetosphereSimulator.Update(simDeltaTime, newYear);
                    _planetStabilizer.Update(simDeltaTime);

                    // Earthquake system (triggers tsunamis)
                    EarthquakeSystem.Update(_map, simDeltaTime, newYear, out bool tsunamiTriggered, out (int x, int y) tsunamiEpicenter, out float tsunamiMagnitude);
                    if (tsunamiTriggered)
                    {
                        TsunamiSystem.InitiateTsunami(_map, tsunamiEpicenter.x, tsunamiEpicenter.y, tsunamiMagnitude, newYear);
                    }

                    // Tsunami wave propagation and flooding
                    TsunamiSystem.Update(_map, simDeltaTime, newYear);
                    TsunamiSystem.DrainFloodWaters(_map, simDeltaTime);

                    // Update game state (thread-safe)
                    lock (_simulationLock)
                    {
                        _gameState.Year = newYear;
                        _gameState.TimeAccumulator = newAccumulator;

                        // Mark renderer dirty so UI knows to update
                        _terrainRenderer.MarkDirty();
                    }
                }

                // Sleep to prevent CPU spinning (target ~60 updates/sec)
                Thread.Sleep(16);
            }
            catch (Exception ex)
            {
                // Log error but keep thread alive
                Console.WriteLine($"Simulation thread error: {ex.Message}");
                Thread.Sleep(100);
            }
        }
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
        _map = new PlanetMap(240, 120, _mapOptions);

        // Initialize simulators
        _climateSimulator = new ClimateSimulator(_map);
        _atmosphereSimulator = new AtmosphereSimulator(_map);
        _lifeSimulator = new LifeSimulator(_map);
        _animalEvolutionSimulator = new AnimalEvolutionSimulator(_map, _mapOptions.Seed);
        _geologicalSimulator = new GeologicalSimulator(_map, _mapOptions.Seed);
        _hydrologySimulator = new HydrologySimulator(_map, _mapOptions.Seed);
        _weatherSystem = new WeatherSystem(_map, _mapOptions.Seed);
        _civilizationManager = new CivilizationManager(_map, _mapOptions.Seed);
        _civilizationManager.SetWeatherSystem(_weatherSystem); // Connect weather system for cyclone response
        _biomeSimulator = new BiomeSimulator(_map, _mapOptions.Seed);
        _disasterManager = new DisasterManager(_map, _geologicalSimulator, _mapOptions.Seed);
        _civilizationManager.SetDisasterManager(_disasterManager); // Connect disaster manager for nuclear accidents
        _forestFireManager = new ForestFireManager(_map, _mapOptions.Seed);
        _magnetosphereSimulator = new MagnetosphereSimulator(_map, _mapOptions.Seed);
        _planetStabilizer = new PlanetStabilizer(_map, _magnetosphereSimulator);
        _diseaseManager = new DiseaseManager(_map, _civilizationManager, _mapOptions.Seed);

        // Generate initial geological features
        EarthquakeSystem.GenerateInitialFaults(_map); // Create fault lines at plate boundaries

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

        // Start background simulation thread
        _simulationThreadActive = true;
        _simulationThread = new Thread(SimulationThreadLoop);
        _simulationThread.IsBackground = true;
        _simulationThread.Name = "Simulation Thread";
        _simulationThread.Start();

        // Call base.Initialize() LAST so LoadContent() can use the initialized data
        base.Initialize();
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);

        // Set custom window icon (procedurally generated planet)
        SetCustomIcon();

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
        _minimap3D.SetWeatherSystem(_weatherSystem); // Connect weather system for clouds and storms
        _eventsUI = new GeologicalEventsUI(_spriteBatch, _font, GraphicsDevice);
        _eventsUI.InitializeOverlayTexture(_map);
        _eventsUI.SetSimulators(_geologicalSimulator, _hydrologySimulator);
        _interactiveControls = new InteractiveControls(GraphicsDevice, _font, _map);
        _sedimentViewer = new SedimentColumnViewer(GraphicsDevice, _font, _map);
        _sedimentViewer.SetCivilizationManager(_civilizationManager);
        _playerCivControl = new PlayerCivilizationControl(GraphicsDevice, _font, _civilizationManager);
        _divinePowersUI = new DivinePowersUI(GraphicsDevice, _font, _civilizationManager);
        _disasterControlUI = new DisasterControlUI(GraphicsDevice, _font, _disasterManager, _map);
        _plantingTool = new ManualPlantingTool(_map, GraphicsDevice, _font);
        _diseaseControlUI = new DiseaseControlUI(GraphicsDevice, _font, _diseaseManager, _map, _civilizationManager);

        // Create toolbar
        _toolbar = new ToolbarUI(this, GraphicsDevice, _font);

        // Create planetary controls UI
        _planetaryControlsUI = new PlanetaryControlsUI(GraphicsDevice, _font, _map, _magnetosphereSimulator, _planetStabilizer);
        _planetaryControlsUI.SetGeologicalSimulator(_geologicalSimulator);

        // Create main menu
        _mainMenu = new MainMenu(GraphicsDevice, _font);

        // Create loading screen
        _loadingScreen = new LoadingScreen(_spriteBatch, _font, GraphicsDevice);
    }

    protected override void Update(GameTime gameTime)
    {
        var keyState = Keyboard.GetState();
        var mouseState = Mouse.GetState();

        // Check if world generation is in progress
        if (_isGenerating)
        {
            // Update loading screen with generation progress
            _loadingScreen.IsVisible = true;
            _loadingScreen.Progress = PlanetMap.GenerationProgress;
            _loadingScreen.CurrentTask = PlanetMap.GenerationTask;

            // Check if generation is complete
            if (_generationThread != null && !_generationThread.IsAlive && _newMap != null)
            {
                // Generation finished - finalize the new world
                FinalizeNewWorld(_newMap);
                _isGenerating = false;
                _loadingScreen.IsVisible = false;
                _generationThread = null;
                _newMap = null;
            }

            base.Update(gameTime);
            return;
        }

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

        // Simulation now runs on background thread - UI thread just handles input/rendering
        // Enable simulation when in-game
        lock (_simulationLock)
        {
            _simulationRunning = (_mainMenu.CurrentScreen == GameScreen.InGame);
        }

        // Update global stats periodically (UI thread)
        if (_mainMenu.CurrentScreen == GameScreen.InGame)
        {
            float realDeltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
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
            _toolbar.Update(mouseState);
            _minimap3D.Update(realDeltaTime);
            _eventsUI.Update(_gameState.Year);
            _interactiveControls.Update(realDeltaTime);
            _sedimentViewer.Update(Mouse.GetState(), _terrainRenderer.CellSize,
                _terrainRenderer.CameraX, _terrainRenderer.CameraY, _terrainRenderer.ZoomLevel,
                _mapRenderOffsetX, _mapRenderOffsetY);
            _playerCivControl.Update(Mouse.GetState());
            _divinePowersUI.Update(Mouse.GetState(), realDeltaTime);
            _disasterControlUI.Update(Mouse.GetState(), _gameState.Year, _terrainRenderer.CellSize,
                _terrainRenderer.CameraX, _terrainRenderer.CameraY, _terrainRenderer.ZoomLevel,
                _mapRenderOffsetX, _mapRenderOffsetY);
            _diseaseControlUI.Update(Mouse.GetState(), _previousMouseState, keyState);
            _plantingTool.Update(Mouse.GetState(), _terrainRenderer.CellSize,
                _terrainRenderer.CameraX, _terrainRenderer.CameraY, _terrainRenderer.ZoomLevel,
                _civilizationManager, _gameState.Year, _mapRenderOffsetX, _mapRenderOffsetY);
            _planetaryControlsUI.Update(Mouse.GetState());

            // Update day/night cycle (24 hours = 1 day)
            _terrainRenderer.DayNightTime += realDeltaTime * 2.4f; // Complete cycle in 10 seconds at 1x speed
            if (_terrainRenderer.DayNightTime >= 24.0f)
            {
                _terrainRenderer.DayNightTime -= 24.0f;
            }

            // Auto-enable/disable day/night based on time speed
            // Only show day/night cycle when simulation is slow enough to appreciate it
            if (_gameState.TimeSpeed <= 0.5f)
            {
                _terrainRenderer.ShowDayNight = true;
            }
            else if (_gameState.TimeSpeed > 1.0f)
            {
                // Disable day/night at faster speeds (user manually toggling with 'C' overrides this)
                _terrainRenderer.ShowDayNight = false;
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

        // Mouse controls for pan and zoom (MUST be processed every frame, not just on keyboard changes)
        var mouseState = Mouse.GetState();

        // Check if mouse is over the minimap (don't pan if it is)
        bool isOverMinimap = _minimap3D != null && _minimap3D.IsMouseOver(mouseState);

        // Left mouse button for panning (more intuitive than middle button)
        // Don't pan if mouse is over minimap
        if (mouseState.LeftButton == ButtonState.Pressed && !isOverMinimap)
        {
            if (_previousMouseState.LeftButton == ButtonState.Pressed)
            {
                float dx = mouseState.X - _previousMouseState.X;
                float dy = mouseState.Y - _previousMouseState.Y;
                _terrainRenderer.CameraX -= dx;
                _terrainRenderer.CameraY -= dy;
            }
        }

        // Middle mouse button for panning (alternative)
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

        // Only process key presses (not holds) - but mouse input is always processed above
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

        // Geological hazards view modes (E, Q, U)
        if (keyState.IsKeyDown(Keys.E) && _previousKeyState.IsKeyUp(Keys.E))
            _currentRenderMode = RenderMode.Earthquakes;
        if (keyState.IsKeyDown(Keys.Q) && _previousKeyState.IsKeyUp(Keys.Q))
            _currentRenderMode = RenderMode.Faults;
        if (keyState.IsKeyDown(Keys.U) && _previousKeyState.IsKeyUp(Keys.U))
            _currentRenderMode = RenderMode.Tsunamis;

        // Biome view mode (F10)
        if (keyState.IsKeyDown(Keys.F10) && _previousKeyState.IsKeyUp(Keys.F10))
            _currentRenderMode = RenderMode.Biomes;

        // Albedo view mode (A key) - surface reflectivity and ice-albedo feedback
        if (keyState.IsKeyDown(Keys.A) && _previousKeyState.IsKeyUp(Keys.A))
            _currentRenderMode = RenderMode.Albedo;

        // Radiation view mode (F12) - cosmic rays and solar radiation levels
        if (keyState.IsKeyDown(Keys.F12) && _previousKeyState.IsKeyUp(Keys.F12))
            _currentRenderMode = RenderMode.Radiation;

        // Resources view mode (J key)
        if (keyState.IsKeyDown(Keys.J) && _previousKeyState.IsKeyUp(Keys.J))
            _currentRenderMode = RenderMode.Resources;

        // Infrastructure view mode (O key) - civilization infrastructure
        if (keyState.IsKeyDown(Keys.O) && _previousKeyState.IsKeyUp(Keys.O))
            _currentRenderMode = RenderMode.Infrastructure;

        // Apply render mode to terrain renderer (triggers texture update when mode changes)
        _terrainRenderer.Mode = _currentRenderMode;

        // Toggle day/night cycle (C key)
        if (keyState.IsKeyDown(Keys.C) && _previousKeyState.IsKeyUp(Keys.C))
        {
            _terrainRenderer.ShowDayNight = !_terrainRenderer.ShowDayNight;
        }

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

        // Open divine powers (I key - "Intervene")
        if (keyState.IsKeyDown(Keys.I) && _previousKeyState.IsKeyUp(Keys.I))
        {
            _divinePowersUI.IsOpen = !_divinePowersUI.IsOpen;
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

        // Toggle planet controls (X key)
        if (keyState.IsKeyDown(Keys.X) && _previousKeyState.IsKeyUp(Keys.X))
        {
            _planetaryControlsUI.IsVisible = !_planetaryControlsUI.IsVisible;
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
        if (keyState.IsKeyDown(Keys.S) && _previousKeyState.IsKeyUp(Keys.S))
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
        _mapOptionsUI.IsVisible = false;
        // Don't randomize seed - use the seed configured in the editor
        StartWorldGeneration();
        // Screen will switch to InGame after generation completes in FinalizeNewWorld
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
            _minimap3D.SetWeatherSystem(_weatherSystem); // Connect weather system for clouds and storms
            _eventsUI.InitializeOverlayTexture(_map);
            _eventsUI.SetSimulators(_geologicalSimulator, _hydrologySimulator);

            _mainMenu.CurrentScreen = GameScreen.InGame;
        }
    }

    public void QuickSave()
    {
        SaveGame("QuickSave");
    }

    public void QuickLoad()
    {
        LoadGame("QuickSave");
    }

    public void RegeneratePlanet()
    {
        // Use a new random seed
        _mapOptions.Seed = new Random().Next();

        // Start background generation
        StartWorldGeneration();
    }

    private void StartWorldGeneration()
    {
        _isGenerating = true;
        _loadingScreen.IsVisible = true;

        // Clear data from old map
        TerrainCellExtensions.ClearGeologicalData();
        MeteorologicalExtensions.ClearMeteorologicalData();
        BiomeExtensions.ClearBiomeData();
        ResourceExtensions.ClearResourceData();

        // Create the map on a background thread
        _generationThread = new Thread(() =>
        {
            try
            {
                _newMap = new PlanetMap(_mapOptions.MapWidth, _mapOptions.MapHeight, _mapOptions);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"World generation error: {ex.Message}");
                _isGenerating = false;
            }
        });
        _generationThread.IsBackground = true;
        _generationThread.Name = "World Generation";
        _generationThread.Start();
    }

    private void FinalizeNewWorld(PlanetMap newMap)
    {
        // Replace the old map
        _map = newMap;

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
        _eventsUI.InitializeOverlayTexture(_map);
        _eventsUI.SetSimulators(_geologicalSimulator, _hydrologySimulator);

        // Update sediment viewer with new map reference
        _sedimentViewer = new SedimentColumnViewer(GraphicsDevice, _font, _map);
        _sedimentViewer.SetCivilizationManager(_civilizationManager);

        // Update other interactive tools with new map
        _disasterControlUI = new DisasterControlUI(GraphicsDevice, _font, _disasterManager, _map);
        _plantingTool = new ManualPlantingTool(_map, GraphicsDevice, _font);
        _planetaryControlsUI = new PlanetaryControlsUI(GraphicsDevice, _font, _map, _magnetosphereSimulator, _planetStabilizer);
        _planetaryControlsUI.SetGeologicalSimulator(_geologicalSimulator);

        // Reset game state
        _gameState.Year = 0;
        _gameState.TimeAccumulator = 0;

        // Switch to in-game screen
        _mainMenu.CurrentScreen = GameScreen.InGame;
    }

    // OPTIMIZED: Calculate all global stats in one pass instead of multiple full-map scans
    // Reduces 3 separate 20,000+ cell scans to just 1 scan
    private void UpdateGlobalStats()
    {
        float totalTemp = 0;
        float totalO2 = 0;
        float totalCO2 = 0;
        int count = 0;

        for (int x = 0; x < _map.Width; x++)
        {
            for (int y = 0; y < _map.Height; y++)
            {
                var cell = _map.Cells[x, y];
                totalTemp += cell.Temperature;
                totalO2 += cell.Oxygen;
                totalCO2 += cell.CO2;
                count++;
            }
        }

        _map.GlobalTemperature = totalTemp / count;
        _map.GlobalOxygen = totalO2 / count;
        _map.GlobalCO2 = totalCO2 / count;
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

        // Split screen layout: Toolbar at top (36px), Info panel on left (280px), map on right
        int toolbarHeight = _toolbar.ToolbarHeight;
        int infoPanelWidth = 280;
        int mapAreaX = infoPanelWidth;
        int mapAreaWidth = GraphicsDevice.Viewport.Width - infoPanelWidth;
        int mapAreaHeight = GraphicsDevice.Viewport.Height - toolbarHeight;

        // Draw terrain (centered in right area, below toolbar)
        int mapPixelWidth = _map.Width * _terrainRenderer.CellSize;
        int mapPixelHeight = _map.Height * _terrainRenderer.CellSize;
        int offsetX = mapAreaX + (mapAreaWidth - mapPixelWidth) / 2;
        int offsetY = toolbarHeight + (mapAreaHeight - mapPixelHeight) / 2;

        // Store offsets for coordinate conversion in Update
        _mapRenderOffsetX = offsetX;
        _mapRenderOffsetY = offsetY;

        _terrainRenderer.Draw(_spriteBatch, offsetX, offsetY);

        // Draw cyclone vortices on weather view modes
        if (_currentRenderMode == RenderMode.Clouds || _currentRenderMode == RenderMode.Storms ||
            _currentRenderMode == RenderMode.Wind || _currentRenderMode == RenderMode.Pressure)
        {
            DrawCycloneVortices2D(_spriteBatch, offsetX, offsetY);
        }

        // Draw geological overlays (volcanoes, rivers, plates)
        // TerrainRenderer applies camera offset internally (camX = offsetX - CameraX)
        // DrawOverlay uses offset directly, so we need to apply camera offset before passing
        int overlayOffsetX = offsetX - (int)_terrainRenderer.CameraX;
        int overlayOffsetY = offsetY - (int)_terrainRenderer.CameraY;
        _eventsUI.DrawOverlay(_map, overlayOffsetX, overlayOffsetY, _terrainRenderer.CellSize, _terrainRenderer.ZoomLevel);

        // Draw view mode legend
        _terrainRenderer.DrawLegend(_spriteBatch, _font, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);

        // Draw UI with current zoom and overlay states (below toolbar)
        _ui.Draw(_gameState, _currentRenderMode, _terrainRenderer.ZoomLevel,
            _eventsUI.ShowVolcanoes, _eventsUI.ShowRivers, _eventsUI.ShowPlates, toolbarHeight);

        // Draw map options menu (if visible)
        _mapOptionsUI.Draw(_mapOptions);

        // Draw geological events log (below toolbar)
        _eventsUI.DrawEventLog(GraphicsDevice.Viewport.Width, toolbarHeight);

        // Note: Removed old overlay legend - overlay status now shown in info panel
        // and symbols are visible directly on map when enabled

        // Update and draw 3D minimap (positioned at bottom of screen to avoid covering info panel text)
        _minimap3D.PosX = 10;
        _minimap3D.PosY = GraphicsDevice.Viewport.Height - 160; // 150px minimap + 10px margin
        _minimap3D.UpdateTexture(_terrainRenderer);
        _minimap3D.Draw(_spriteBatch);

        // Draw interactive controls
        _interactiveControls.Draw(_spriteBatch);

        // Draw sediment column viewer
        _sedimentViewer.Draw(_spriteBatch, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);

        // Draw player civilization control
        _playerCivControl.Draw(_spriteBatch, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);

        // Draw divine powers UI
        _divinePowersUI.Draw(_spriteBatch, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);

        // Draw disaster control UI
        _disasterControlUI.Draw(_spriteBatch, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);

        // Draw disease control UI
        _diseaseControlUI.Draw(_spriteBatch, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);

        // Draw planetary controls UI
        _planetaryControlsUI.Draw(_spriteBatch);

        // Draw manual planting tool
        _plantingTool.Draw(_spriteBatch, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);

        // Draw pause menu overlay if paused
        if (_mainMenu.CurrentScreen == GameScreen.PauseMenu)
        {
            _mainMenu.Draw(_spriteBatch, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);
        }

        // Draw loading screen overlay if generating world
        if (_loadingScreen.IsVisible)
        {
            _loadingScreen.Draw();
        }

        // Draw toolbar LAST (shown in-game only) so tooltips appear on top
        if (_mainMenu.CurrentScreen == GameScreen.InGame)
        {
            _toolbar.Draw(_spriteBatch, GraphicsDevice.Viewport.Width);
        }

        _spriteBatch.End();

        base.Draw(gameTime);
    }

    private void DrawCycloneVortices2D(SpriteBatch spriteBatch, int offsetX, int offsetY)
    {
        var storms = _weatherSystem.GetActiveStorms();
        var pixelTexture = new Texture2D(GraphicsDevice, 1, 1);
        pixelTexture.SetData(new[] { Color.White });

        foreach (var storm in storms)
        {
            // Only draw tropical cyclones (hurricanes, typhoons)
            if (storm.Type < StormType.TropicalDepression || storm.Type > StormType.HurricaneCategory5)
                continue;

            // Convert storm position to screen coordinates
            float stormX = storm.CenterX * _terrainRenderer.CellSize;
            float stormY = storm.CenterY * _terrainRenderer.CellSize;

            // Apply camera offset
            int screenX = offsetX + (int)(stormX - _terrainRenderer.CameraX);
            int screenY = offsetY + (int)(stormY - _terrainRenderer.CameraY);

            // Apply zoom
            screenX = offsetX + (int)((stormX - _terrainRenderer.CameraX) * _terrainRenderer.ZoomLevel);
            screenY = offsetY + (int)((stormY - _terrainRenderer.CameraY) * _terrainRenderer.ZoomLevel);

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

            // Draw spiral vortex (scaled with zoom)
            int vortexSize = (int)((15 + storm.Intensity * 30) * _terrainRenderer.ZoomLevel);
            float rotationSpeed = (float)_gameState.Year * 0.5f * storm.RotationDirection;

            // Draw multiple spiral arms
            for (int arm = 0; arm < 3; arm++)
            {
                float armAngle = (arm * MathF.PI * 2f / 3f) + rotationSpeed;

                for (float r = 0; r < vortexSize; r += 1.0f)
                {
                    float angle = armAngle + r * 0.2f * storm.RotationDirection;
                    int x = screenX + (int)(MathF.Cos(angle) * r);
                    int y = screenY + (int)(MathF.Sin(angle) * r);

                    // Fade toward edges
                    float alpha = 1.0f - (r / vortexSize);
                    Color pixelColor = new Color(
                        vortexColor.R,
                        vortexColor.G,
                        vortexColor.B,
                        (byte)(vortexColor.A * alpha)
                    );

                    int pixelSize = Math.Max(1, (int)(2 * _terrainRenderer.ZoomLevel));
                    spriteBatch.Draw(pixelTexture,
                        new Rectangle(x, y, pixelSize, pixelSize),
                        pixelColor);
                }
            }

            // Draw eye of storm for major hurricanes
            if (storm.Type >= StormType.HurricaneCategory3)
            {
                int eyeRadius = (int)(4 * _terrainRenderer.ZoomLevel);
                for (int dy = -eyeRadius; dy <= eyeRadius; dy++)
                {
                    for (int dx = -eyeRadius; dx <= eyeRadius; dx++)
                    {
                        if (dx * dx + dy * dy <= eyeRadius * eyeRadius)
                        {
                            spriteBatch.Draw(pixelTexture,
                                new Rectangle(screenX + dx, screenY + dy, 1, 1),
                                new Color(20, 20, 20, 200));
                        }
                    }
                }
            }
        }

        pixelTexture.Dispose();
    }

    private void SetCustomIcon()
    {
        try
        {
            // Create a procedurally generated planet icon (32x32)
            const int iconSize = 32;
            var iconData = new byte[iconSize * iconSize * 4]; // RGBA format

            int centerX = iconSize / 2;
            int centerY = iconSize / 2;
            float radius = iconSize / 2.0f - 1;

            // Create a simple planet with ocean, land, and polar cap
            for (int y = 0; y < iconSize; y++)
            {
                for (int x = 0; x < iconSize; x++)
                {
                    int index = (y * iconSize + x) * 4;
                    float dx = x - centerX;
                    float dy = y - centerY;
                    float distance = MathF.Sqrt(dx * dx + dy * dy);

                    if (distance <= radius)
                    {
                        // Inside planet sphere
                        // Add simple shading based on distance from edge
                        float edgeFactor = 1.0f - (distance / radius);
                        float shade = 0.6f + edgeFactor * 0.4f;

                        byte r, g, b;
                        // Polar cap (top)
                        if (y < iconSize * 0.2f)
                        {
                            r = (byte)(240 * shade);
                            g = (byte)(245 * shade);
                            b = (byte)(250 * shade);
                        }
                        // Land masses (simple noise pattern)
                        else if ((x + y * 3) % 7 < 3 && y < iconSize * 0.7f)
                        {
                            r = (byte)(60 * shade);
                            g = (byte)(160 * shade);
                            b = (byte)(80 * shade);
                        }
                        // Ocean
                        else
                        {
                            r = (byte)(30 * shade);
                            g = (byte)(90 * shade);
                            b = (byte)(180 * shade);
                        }

                        iconData[index + 0] = r;     // Red
                        iconData[index + 1] = g;     // Green
                        iconData[index + 2] = b;     // Blue
                        iconData[index + 3] = 255;   // Alpha (opaque)
                    }
                    else
                    {
                        // Outside planet - transparent
                        iconData[index + 0] = 0;
                        iconData[index + 1] = 0;
                        iconData[index + 2] = 0;
                        iconData[index + 3] = 0;
                    }
                }
            }

            // Use SDL2 to set the window icon
            unsafe
            {
                fixed (byte* pixels = iconData)
                {
                    IntPtr surface = SDL.SDL_CreateRGBSurfaceFrom(
                        (IntPtr)pixels,
                        iconSize,
                        iconSize,
                        32,
                        iconSize * 4,
                        0x000000FF,
                        0x0000FF00,
                        0x00FF0000,
                        0xFF000000
                    );

                    if (surface != IntPtr.Zero)
                    {
                        SDL.SDL_SetWindowIcon(Window.Handle, surface);
                        SDL.SDL_FreeSurface(surface);
                    }
                }
            }
        }
        catch
        {
            // Silently fail if icon setting is not supported on this platform
            // The game will still work fine without a custom icon
        }
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
        // Stop simulation thread
        _simulationThreadActive = false;
        if (_simulationThread != null && _simulationThread.IsAlive)
        {
            _simulationThread.Join(1000); // Wait up to 1 second for thread to finish
        }

        // Force cleanup before exiting
        CleanupResources();
        base.OnExiting(sender, args);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Stop simulation thread
            _simulationThreadActive = false;
            if (_simulationThread != null && _simulationThread.IsAlive)
            {
                _simulationThread.Join(1000);
            }

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
        _loadingScreen?.Dispose();
        _spriteBatch?.Dispose();
        _graphics?.Dispose();
    }

    // Public methods for toolbar
    public void SetRenderMode(RenderMode mode)
    {
        _currentRenderMode = mode;
        _terrainRenderer.Mode = mode;
    }

    public void TogglePause()
    {
        _gameState.IsPaused = !_gameState.IsPaused;
    }

    public void IncreaseTimeSpeed()
    {
        _gameState.TimeSpeed = Math.Min(_gameState.TimeSpeed * 2.0f, 32.0f);
    }

    public void DecreaseTimeSpeed()
    {
        _gameState.TimeSpeed = Math.Max(_gameState.TimeSpeed / 2.0f, 0.25f);
    }

    public void ToggleHelp()
    {
        _ui.ShowHelp = !_ui.ShowHelp;
    }

    public void ToggleMapOptions()
    {
        _mapOptionsUI.IsVisible = !_mapOptionsUI.IsVisible;
        if (_mapOptionsUI.IsVisible)
        {
            _mapOptionsUI.NeedsPreviewUpdate = true;
        }
    }

    public void ToggleMinimap()
    {
        _minimap3D.IsVisible = !_minimap3D.IsVisible;
    }

    public void ToggleDayNight()
    {
        _terrainRenderer.ShowDayNight = !_terrainRenderer.ShowDayNight;
    }

    public void ToggleVolcanoes()
    {
        _eventsUI.ShowVolcanoes = !_eventsUI.ShowVolcanoes;
    }

    public void ToggleRivers()
    {
        _eventsUI.ShowRivers = !_eventsUI.ShowRivers;
    }

    public void TogglePlates()
    {
        _eventsUI.ShowPlates = !_eventsUI.ShowPlates;
    }

    public void SeedLife()
    {
        _lifeSimulator.SeedInitialLife();
    }

    public void ToggleCivilization()
    {
        _playerCivControl.OpenCivilizationSelector();
    }

    public void ToggleDivinePowers()
    {
        _divinePowersUI.IsOpen = !_divinePowersUI.IsOpen;
    }

    public void ToggleDisasters()
    {
        _disasterControlUI.IsVisible = !_disasterControlUI.IsVisible;
    }

    public void ToggleDiseases()
    {
        _diseaseControlUI.IsVisible = !_diseaseControlUI.IsVisible;
    }

    public void TogglePlantTool()
    {
        _plantingTool.IsActive = !_plantingTool.IsActive;
    }

    public void ToggleStabilizer()
    {
        _planetStabilizer.IsActive = !_planetStabilizer.IsActive;
    }

    public void TogglePlanetControls()
    {
        _planetaryControlsUI.IsVisible = !_planetaryControlsUI.IsVisible;
    }

    public new void Exit()
    {
        // Stop simulation thread
        _simulationThreadActive = false;
        if (_simulationThread != null && _simulationThread.IsAlive)
        {
            _simulationThread.Join(1000);
        }

        // Ensure proper cleanup and force exit
        CleanupResources();
        base.Exit();

        // Force process exit if base.Exit() doesn't work
        Environment.Exit(0);
    }
}
