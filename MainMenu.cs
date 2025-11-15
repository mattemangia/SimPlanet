using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System.Reflection;

namespace SimPlanet;

public enum GameScreen
{
    MainMenu,
    NewGame,
    LoadGame,
    InGame,
    PauseMenu,
    SaveGame
}

/// <summary>
/// Main menu and game state management with mouse and keyboard support
/// </summary>
public class MainMenu
{
    private readonly GraphicsDevice _graphicsDevice;
    private readonly FontRenderer _font;
    private Texture2D _pixelTexture;
    private Texture2D _splashBackground;

    public GameScreen CurrentScreen { get; set; } = GameScreen.MainMenu;

    private int _selectedMenuItem = 0;
    private int _selectedSaveSlot = 0;
    private List<string> _saveGames = new();
    private MouseState _previousMouseState;

    private string[] _mainMenuItems = { "New Game", "Load Game", "Quit" };
    private string[] _pauseMenuItems = { "Resume", "Save Game", "Main Menu" };

    // Store menu item bounds for mouse clicking
    private List<Rectangle> _menuItemBounds = new();

    public MainMenu(GraphicsDevice graphicsDevice, FontRenderer font)
    {
        _graphicsDevice = graphicsDevice;
        _font = font;

        _pixelTexture = new Texture2D(_graphicsDevice, 1, 1);
        _pixelTexture.SetData(new[] { Color.White });
        _previousMouseState = Mouse.GetState();

        // Load splash background from embedded resource
        LoadSplashBackground();
    }

    private void LoadSplashBackground()
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            using (var stream = assembly.GetManifestResourceStream("SimPlanet.splash.png"))
            {
                if (stream != null)
                {
                    _splashBackground = Texture2D.FromStream(_graphicsDevice, stream);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to load splash background: {ex.Message}");
        }
    }

    public MenuAction HandleInput(KeyboardState keyState, KeyboardState previousKeyState, MouseState mouseState)
    {
        // Handle mouse hover
        for (int i = 0; i < _menuItemBounds.Count; i++)
        {
            if (_menuItemBounds[i].Contains(mouseState.Position))
            {
                _selectedMenuItem = i;
                break;
            }
        }

        // Handle mouse click
        if (mouseState.LeftButton == ButtonState.Released &&
            _previousMouseState.LeftButton == ButtonState.Pressed)
        {
            for (int i = 0; i < _menuItemBounds.Count; i++)
            {
                if (_menuItemBounds[i].Contains(mouseState.Position))
                {
                    _selectedMenuItem = i;
                    _previousMouseState = mouseState;
                    return HandleMenuSelection();
                }
            }
        }

        _previousMouseState = mouseState;

        // Navigate menu with keyboard
        if (keyState.IsKeyDown(Keys.Down) && previousKeyState.IsKeyUp(Keys.Down))
        {
            _selectedMenuItem++;
            if (CurrentScreen == GameScreen.MainMenu && _selectedMenuItem >= _mainMenuItems.Length)
                _selectedMenuItem = 0;
            else if (CurrentScreen == GameScreen.PauseMenu && _selectedMenuItem >= _pauseMenuItems.Length)
                _selectedMenuItem = 0;
            else if (CurrentScreen == GameScreen.LoadGame && _selectedMenuItem >= _saveGames.Count)
                _selectedMenuItem = 0;
        }

        if (keyState.IsKeyDown(Keys.Up) && previousKeyState.IsKeyUp(Keys.Up))
        {
            _selectedMenuItem--;
            if (_selectedMenuItem < 0)
            {
                if (CurrentScreen == GameScreen.MainMenu)
                    _selectedMenuItem = _mainMenuItems.Length - 1;
                else if (CurrentScreen == GameScreen.PauseMenu)
                    _selectedMenuItem = _pauseMenuItems.Length - 1;
                else if (CurrentScreen == GameScreen.LoadGame)
                    _selectedMenuItem = Math.Max(0, _saveGames.Count - 1);
            }
        }

        // Select item
        if (keyState.IsKeyDown(Keys.Enter) && previousKeyState.IsKeyUp(Keys.Enter))
        {
            return HandleMenuSelection();
        }

        // Back/Cancel
        if (keyState.IsKeyDown(Keys.Escape) && previousKeyState.IsKeyUp(Keys.Escape))
        {
            if (CurrentScreen == GameScreen.InGame)
            {
                CurrentScreen = GameScreen.PauseMenu;
                _selectedMenuItem = 0;
                return MenuAction.ShowPauseMenu;
            }
            else if (CurrentScreen == GameScreen.PauseMenu)
            {
                CurrentScreen = GameScreen.InGame;
                return MenuAction.Resume;
            }
            else if (CurrentScreen == GameScreen.NewGame)
            {
                CurrentScreen = GameScreen.MainMenu;
                _selectedMenuItem = 0;
                return MenuAction.CancelNewGame;
            }
            else if (CurrentScreen != GameScreen.MainMenu)
            {
                CurrentScreen = GameScreen.MainMenu;
                _selectedMenuItem = 0;
            }
        }

        return MenuAction.None;
    }

    private MenuAction HandleMenuSelection()
    {
        switch (CurrentScreen)
        {
            case GameScreen.MainMenu:
                switch (_selectedMenuItem)
                {
                    case 0: // New Game
                        CurrentScreen = GameScreen.NewGame;
                        return MenuAction.ShowMapOptions;
                    case 1: // Load Game
                        CurrentScreen = GameScreen.LoadGame;
                        RefreshSaveGames();
                        _selectedMenuItem = 0;
                        return MenuAction.None;
                    case 2: // Quit
                        return MenuAction.Quit;
                }
                break;

            case GameScreen.LoadGame:
                if (_saveGames.Count > 0 && _selectedMenuItem < _saveGames.Count)
                {
                    _selectedSaveSlot = _selectedMenuItem;
                    CurrentScreen = GameScreen.InGame;
                    return MenuAction.LoadGame;
                }
                break;

            case GameScreen.PauseMenu:
                switch (_selectedMenuItem)
                {
                    case 0: // Resume
                        CurrentScreen = GameScreen.InGame;
                        return MenuAction.Resume;
                    case 1: // Save Game
                        CurrentScreen = GameScreen.SaveGame;
                        return MenuAction.SaveGame;
                    case 2: // Main Menu
                        CurrentScreen = GameScreen.MainMenu;
                        _selectedMenuItem = 0;
                        return MenuAction.BackToMainMenu;
                }
                break;
        }

        return MenuAction.None;
    }

    public void Draw(SpriteBatch spriteBatch, int screenWidth, int screenHeight)
    {
        switch (CurrentScreen)
        {
            case GameScreen.MainMenu:
                DrawMainMenu(spriteBatch, screenWidth, screenHeight);
                break;
            case GameScreen.LoadGame:
                DrawLoadGameMenu(spriteBatch, screenWidth, screenHeight);
                break;
            case GameScreen.PauseMenu:
                DrawPauseMenu(spriteBatch, screenWidth, screenHeight);
                break;
        }
    }

    private void DrawMainMenu(SpriteBatch spriteBatch, int screenWidth, int screenHeight)
    {
        _menuItemBounds.Clear();

        // Draw black background first
        spriteBatch.Draw(_pixelTexture, new Rectangle(0, 0, screenWidth, screenHeight), Color.Black);

        // Draw splash background with low alpha for subtle effect
        if (_splashBackground != null)
        {
            // Scale splash to fit screen while maintaining aspect ratio
            float scaleX = (float)screenWidth / _splashBackground.Width;
            float scaleY = (float)screenHeight / _splashBackground.Height;
            float scale = Math.Max(scaleX, scaleY);

            int displayWidth = (int)(_splashBackground.Width * scale);
            int displayHeight = (int)(_splashBackground.Height * scale);
            int x = (screenWidth - displayWidth) / 2;
            int y = (screenHeight - displayHeight) / 2;

            spriteBatch.Draw(_splashBackground,
                new Rectangle(x, y, displayWidth, displayHeight),
                Color.White * 0.15f); // Very subtle transparency
        }

        // Title with glow effect
        DrawCenteredText(spriteBatch, "SIMPLANET", screenHeight / 3 - 10, new Color(255, 200, 50), 1.8f);
        DrawCenteredText(spriteBatch, "Planetary Evolution Simulator", screenHeight / 3 + 35,
            new Color(100, 200, 255), 0.8f);

        // Menu items with fancy boxes
        int startY = screenHeight / 2;
        int buttonWidth = 300;
        int buttonHeight = 50;

        for (int i = 0; i < _mainMenuItems.Length; i++)
        {
            bool isSelected = i == _selectedMenuItem;
            string text = _mainMenuItems[i];

            int x = (screenWidth - buttonWidth) / 2;
            int y = startY + i * 60;

            // Store button bounds
            _menuItemBounds.Add(new Rectangle(x, y, buttonWidth, buttonHeight));

            // Draw button
            Color bgColor = isSelected ? new Color(70, 140, 255, 200) : new Color(30, 60, 100, 180);
            Color borderColor = isSelected ? new Color(120, 200, 255) : new Color(80, 120, 160);

            spriteBatch.Draw(_pixelTexture, _menuItemBounds[i], bgColor);
            DrawBorder(spriteBatch, _menuItemBounds[i].X, _menuItemBounds[i].Y, _menuItemBounds[i].Width, _menuItemBounds[i].Height, borderColor, 2);

            // Draw text
            var textSize = _font.MeasureString(text, 18);
            float textX = x + (buttonWidth - textSize.X) / 2;
            float textY = y + (buttonHeight - textSize.Y) / 2;
            _font.DrawString(spriteBatch, text, new Vector2(textX, textY), Color.White, 18);
        }

        // Instructions
        DrawCenteredText(spriteBatch, "Click or use Arrow Keys + ENTER",
            screenHeight - 50, new Color(150, 150, 150), 0.7f);
    }

    private void DrawBorder(SpriteBatch spriteBatch, int x, int y, int width, int height, Color color, int thickness)
    {
        spriteBatch.Draw(_pixelTexture, new Rectangle(x, y, width, thickness), color);
        spriteBatch.Draw(_pixelTexture, new Rectangle(x, y + height - thickness, width, thickness), color);
        spriteBatch.Draw(_pixelTexture, new Rectangle(x, y, thickness, height), color);
        spriteBatch.Draw(_pixelTexture, new Rectangle(x + width - thickness, y, thickness, height), color);
    }

    private void DrawLoadGameMenu(SpriteBatch spriteBatch, int screenWidth, int screenHeight)
    {
        _menuItemBounds.Clear();

        // Draw black background first
        spriteBatch.Draw(_pixelTexture, new Rectangle(0, 0, screenWidth, screenHeight), Color.Black);

        // Draw splash background with low alpha for subtle effect
        if (_splashBackground != null)
        {
            // Scale splash to fit screen while maintaining aspect ratio
            float scaleX = (float)screenWidth / _splashBackground.Width;
            float scaleY = (float)screenHeight / _splashBackground.Height;
            float scale = Math.Max(scaleX, scaleY);

            int displayWidth = (int)(_splashBackground.Width * scale);
            int displayHeight = (int)(_splashBackground.Height * scale);
            int x = (screenWidth - displayWidth) / 2;
            int y = (screenHeight - displayHeight) / 2;

            spriteBatch.Draw(_splashBackground,
                new Rectangle(x, y, displayWidth, displayHeight),
                Color.White * 0.15f); // Very subtle transparency
        }

        // Title
        DrawCenteredText(spriteBatch, "LOAD GAME", 80, new Color(255, 200, 50), 1.5f);

        if (_saveGames.Count == 0)
        {
            DrawCenteredText(spriteBatch, "No saved games found", screenHeight / 2,
                Color.Gray, 1.0f);
        }
        else
        {
            int startY = 200;
            for (int i = 0; i < _saveGames.Count; i++)
            {
                bool isSelected = i == _selectedMenuItem;
                Color color = isSelected ? Color.Yellow : Color.White;
                string text = isSelected ? "> " + _saveGames[i] + " <" : _saveGames[i];

                var size = _font.MeasureString(text, 16);
                int x = (screenWidth - (int)size.X) / 2;
                int y = startY + i * 35;

                _menuItemBounds.Add(new Rectangle(x - 10, y - 5, (int)size.X + 20, (int)size.Y + 10));

                if (isSelected)
                {
                    spriteBatch.Draw(_pixelTexture, _menuItemBounds[i], new Color(50, 50, 80, 150));
                }

                _font.DrawString(spriteBatch, text, new Vector2(x, y), color, 16);
            }
        }

        DrawCenteredText(spriteBatch, "ESC to go back", screenHeight - 60, Color.Gray, 1.0f);
    }

    private void DrawPauseMenu(SpriteBatch spriteBatch, int screenWidth, int screenHeight)
    {
        _menuItemBounds.Clear();

        // Semi-transparent overlay
        spriteBatch.Draw(_pixelTexture, new Rectangle(0, 0, screenWidth, screenHeight),
            new Color(0, 0, 0, 200));

        // Title
        DrawCenteredText(spriteBatch, "PAUSED", screenHeight / 3, new Color(255, 200, 50), 1.8f);

        // Menu items
        int startY = screenHeight / 2;
        for (int i = 0; i < _pauseMenuItems.Length; i++)
        {
            bool isSelected = i == _selectedMenuItem;
            Color color = isSelected ? Color.Yellow : Color.White;
            string text = isSelected ? "> " + _pauseMenuItems[i] + " <" : _pauseMenuItems[i];

            var size = _font.MeasureString(text, 16);
            int x = (screenWidth - (int)size.X) / 2;
            int y = startY + i * 40;

            _menuItemBounds.Add(new Rectangle(x - 10, y - 5, (int)size.X + 20, (int)size.Y + 10));

            if (isSelected)
            {
                spriteBatch.Draw(_pixelTexture, _menuItemBounds[i], new Color(50, 50, 80, 150));
            }

            _font.DrawString(spriteBatch, text, new Vector2(x, y), color, 16);
        }
    }

    private void DrawCenteredText(SpriteBatch spriteBatch, string text, int y, Color color, float scale)
    {
        var size = _font.MeasureString(text, 16 * scale);
        int x = (_graphicsDevice.Viewport.Width - (int)size.X) / 2;
        _font.DrawString(spriteBatch, text, new Vector2(x, y), color, 16 * scale);
    }

    private void RefreshSaveGames()
    {
        var manager = new SaveLoadManager();
        _saveGames = manager.GetSaveGameList();
    }

    public string GetSelectedSaveName()
    {
        if (_selectedSaveSlot >= 0 && _selectedSaveSlot < _saveGames.Count)
            return _saveGames[_selectedSaveSlot];
        return "";
    }
}

public enum MenuAction
{
    None,
    NewGame,
    LoadGame,
    SaveGame,
    Resume,
    ShowPauseMenu,
    BackToMainMenu,
    ShowMapOptions,
    CancelNewGame,
    Quit
}
