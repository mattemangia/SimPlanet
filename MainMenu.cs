using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

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

        // Background
        spriteBatch.Draw(_pixelTexture, new Rectangle(0, 0, screenWidth, screenHeight),
            new Color(10, 20, 40, 255));

        // Title
        DrawCenteredText(spriteBatch, "=== SIM PLANET ===", screenHeight / 3, Color.Yellow, 1.0f);
        DrawCenteredText(spriteBatch, "Planetary Evolution Simulator", screenHeight / 3 + 30,
            Color.Cyan, 1.0f);

        // Menu items
        int startY = screenHeight / 2;
        for (int i = 0; i < _mainMenuItems.Length; i++)
        {
            bool isSelected = i == _selectedMenuItem;
            Color color = isSelected ? Color.Yellow : Color.White;
            string text = isSelected ? "> " + _mainMenuItems[i] + " <" : _mainMenuItems[i];

            var size = _font.MeasureString(text, 16);
            int x = (screenWidth - (int)size.X) / 2;
            int y = startY + i * 40;

            // Store button bounds for mouse interaction
            _menuItemBounds.Add(new Rectangle(x - 10, y - 5, (int)size.X + 20, (int)size.Y + 10));

            // Draw button background if hovered
            if (isSelected)
            {
                spriteBatch.Draw(_pixelTexture, _menuItemBounds[i], new Color(50, 50, 80, 150));
            }

            _font.DrawString(spriteBatch, text, new Vector2(x, y), color, 16);
        }

        // Instructions
        DrawCenteredText(spriteBatch, "Use MOUSE or UP/DOWN arrows and ENTER to select",
            screenHeight - 60, Color.Gray, 1.0f);
    }

    private void DrawLoadGameMenu(SpriteBatch spriteBatch, int screenWidth, int screenHeight)
    {
        _menuItemBounds.Clear();

        // Background
        spriteBatch.Draw(_pixelTexture, new Rectangle(0, 0, screenWidth, screenHeight),
            new Color(10, 20, 40, 230));

        // Title
        DrawCenteredText(spriteBatch, "=== LOAD GAME ===", 100, Color.Yellow, 1.0f);

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
            new Color(0, 0, 0, 180));

        // Title
        DrawCenteredText(spriteBatch, "=== PAUSED ===", screenHeight / 3, Color.Yellow, 1.0f);

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
