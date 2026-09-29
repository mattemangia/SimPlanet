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
    private Texture2D? _splashBackground;

    public GameScreen CurrentScreen { get; set; } = GameScreen.MainMenu;

    private int _selectedMenuItem = 0;
    private int _selectedSaveSlot = 0;
    private List<string> _saveGames = new();
    private MouseState _previousMouseState;

    private string[] _mainMenuItems = { "New Game", "Load Game", "About", "Quit" };
    private string[] _pauseMenuItems = { "Resume", "Save Game", "About", "Main Menu" };

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
                    RemoveCornerMarks(_splashBackground);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to load splash background: {ex.Message}");
        }
    }

    /// <summary>
    /// The splash art carries small tool logos in its top corners; cover them with a
    /// patch of nearby starry sky so the art works as a clean menu backdrop.
    /// </summary>
    internal static void RemoveCornerMarks(Texture2D tex)
    {
        if (tex.Width != 1536 || tex.Height != 1024) return; // only for the known artwork
        var data = new Color[tex.Width * tex.Height];
        tex.GetData(data);
        void CopyPatch(int dstX, int dstY, int w, int h, int srcX, int srcY)
        {
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    data[(dstY + y) * tex.Width + dstX + x] = data[(srcY + y) * tex.Width + srcX + x];
        }
        CopyPatch(80, 60, 110, 145, 1380, 560);   // mark in the top-left corner
        CopyPatch(1180, 70, 170, 185, 20, 760);   // mark in the top-right corner
        tex.SetData(data);
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
                    case 2: // About
                        return MenuAction.ShowAbout;
                    case 3: // Quit
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
                    case 2: // About
                        return MenuAction.ShowAbout;
                    case 3: // Main Menu
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
        spriteBatch.End();
        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp);

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
            case GameScreen.NewGame:
                // The world generator dialog is drawn on top of this backdrop
                DrawBackdrop(spriteBatch, screenWidth, screenHeight, false);
                break;
        }

        spriteBatch.End();
        spriteBatch.Begin(samplerState: SamplerState.PointClamp);
    }

    /// <summary>Starry backdrop with the planet artwork. Returns the rectangle used by the art.</summary>
    private Rectangle DrawBackdrop(SpriteBatch spriteBatch, int screenWidth, int screenHeight, bool heroLayout)
    {
        spriteBatch.Draw(_pixelTexture, new Rectangle(0, 0, screenWidth, screenHeight), new Color(4, 6, 14));

        Rectangle artRect = Rectangle.Empty;
        if (_splashBackground != null)
        {
            // Hero layout: artwork fills the left part of the screen, menu card on the right
            Rectangle area = heroLayout
                ? new Rectangle(0, 0, (int)(screenWidth * 0.64f), screenHeight)
                : new Rectangle(0, 0, screenWidth, screenHeight);
            float scale = heroLayout
                ? Math.Min((float)area.Width / _splashBackground.Width, (float)area.Height / _splashBackground.Height) * 1.02f
                : Math.Max((float)area.Width / _splashBackground.Width, (float)area.Height / _splashBackground.Height);
            int w = (int)(_splashBackground.Width * scale);
            int h = (int)(_splashBackground.Height * scale);
            artRect = new Rectangle(area.X + (area.Width - w) / 2, area.Y + (area.Height - h) / 2, w, h);
            spriteBatch.Draw(_splashBackground, artRect, Color.White * (heroLayout ? 1f : 0.28f));
            if (heroLayout)
            {
                // Blend the artwork's right edge into the backdrop
                UITheme.FillGradientHorizontal(spriteBatch, new Rectangle(artRect.Right - 160, 0, 161, screenHeight), new Color(4, 6, 14), fadeToRight: false);
                spriteBatch.Draw(_pixelTexture, new Rectangle(artRect.Right, 0, screenWidth - artRect.Right, screenHeight), new Color(4, 6, 14));
            }
        }

        // Vignette: darken the top edge
        UITheme.FillGradient(spriteBatch, new Rectangle(0, 0, screenWidth, screenHeight / 5), Color.Black * 0.6f);
        return artRect;
    }

    private void DrawMenuButtons(SpriteBatch spriteBatch, string[] items, int x, int y, int width, int height, int spacing)
    {
        var mouse = Mouse.GetState();
        for (int i = 0; i < items.Length; i++)
        {
            var rect = new Rectangle(x, y + i * (height + spacing), width, height);
            _menuItemBounds.Add(rect);
            bool selected = i == _selectedMenuItem;
            bool hovered = rect.Contains(mouse.Position);
            UITheme.DrawButton(spriteBatch, rect, items[i], hovered || selected, selected, UITheme.Accent, UITheme.FontMedium + 2);
            if (selected)
            {
                // Small accent marker on the left of the selected entry
                UITheme.FillRounded(spriteBatch, new Rectangle(rect.X + 10, rect.Y + height / 2 - 3, 6, 6), UITheme.Gold);
            }
        }
    }

    private void DrawMainMenu(SpriteBatch spriteBatch, int screenWidth, int screenHeight)
    {
        _menuItemBounds.Clear();

        bool hero = screenWidth >= 1100 && screenWidth > screenHeight;
        DrawBackdrop(spriteBatch, screenWidth, screenHeight, hero);

        int buttonWidth = 300;
        int buttonHeight = 48;
        int spacing = 12;
        int cardWidth = buttonWidth + 60;
        int cardHeight = 126 + _mainMenuItems.Length * (buttonHeight + spacing) + 40;
        int cardX = hero ? (int)(screenWidth * 0.64f) + ((int)(screenWidth * 0.36f) - cardWidth) / 2 : (screenWidth - cardWidth) / 2;
        int cardY = (screenHeight - cardHeight) / 2;
        var card = new Rectangle(cardX, cardY, cardWidth, cardHeight);

        UITheme.DrawPanel(spriteBatch, card, new Color(12, 18, 30, 225), UITheme.Border);

        // Title
        UITheme.DrawTextCentered(spriteBatch, hero ? "Planetary Evolution" : "SIMPLANET", new Rectangle(card.X, card.Y + 22, card.Width, 44), UITheme.Gold, UITheme.FontTitle);
        UITheme.DrawTextCentered(spriteBatch, "From molten rock to civilization", new Rectangle(card.X, card.Y + 70, card.Width, 20), UITheme.Accent, UITheme.FontNormal);
        spriteBatch.Draw(UITheme.Pixel, new Rectangle(card.X + 40, card.Y + 104, card.Width - 80, 1), UITheme.Border);

        DrawMenuButtons(spriteBatch, _mainMenuItems, card.X + 30, card.Y + 124, buttonWidth, buttonHeight, spacing);

        UITheme.DrawTextCentered(spriteBatch, "Mouse or Arrow keys + ENTER", new Rectangle(card.X, card.Bottom - 34, card.Width, 20), UITheme.TextMuted, UITheme.FontSmall);
    }

    private void DrawLoadGameMenu(SpriteBatch spriteBatch, int screenWidth, int screenHeight)
    {
        _menuItemBounds.Clear();

        DrawBackdrop(spriteBatch, screenWidth, screenHeight, false);

        int cardWidth = Math.Min(560, screenWidth - 40);
        int rowHeight = 40;
        int maxRows = Math.Max(1, (screenHeight - 260) / (rowHeight + 6));
        int rows = Math.Max(1, Math.Min(_saveGames.Count, maxRows));
        int cardHeight = 110 + rows * (rowHeight + 6) + 50;
        var card = new Rectangle((screenWidth - cardWidth) / 2, (screenHeight - cardHeight) / 2, cardWidth, cardHeight);
        int contentY = UITheme.DrawTitledPanel(spriteBatch, card, "LOAD GAME", UITheme.Gold, 44);

        if (_saveGames.Count == 0)
        {
            UITheme.DrawTextCentered(spriteBatch, "No saved games found", new Rectangle(card.X, contentY + 10, card.Width, 30), UITheme.TextDim, UITheme.FontMedium);
            UITheme.DrawTextCentered(spriteBatch, "Press F5 in game to quick-save", new Rectangle(card.X, contentY + 40, card.Width, 20), UITheme.TextMuted, UITheme.FontSmall);
        }
        else
        {
            // Keep the selected entry visible when the list is longer than the card
            int first = Math.Clamp(_selectedMenuItem - rows + 1, 0, Math.Max(0, _saveGames.Count - rows));
            var mouse = Mouse.GetState();
            float textH = UITheme.Measure("Ag", UITheme.FontMedium).Y;
            for (int i = 0; i < _saveGames.Count; i++)
            {
                if (i < first || i >= first + rows)
                {
                    _menuItemBounds.Add(Rectangle.Empty);
                    continue;
                }
                var rect = new Rectangle(card.X + 20, contentY + 6 + (i - first) * (rowHeight + 6), card.Width - 40, rowHeight);
                _menuItemBounds.Add(rect);
                bool selected = i == _selectedMenuItem;
                UITheme.DrawButton(spriteBatch, rect, "", selected || rect.Contains(mouse.Position), selected, UITheme.Accent);
                UITheme.DrawTextShadowed(spriteBatch, UITheme.Ellipsize(_saveGames[i], rect.Width - 30, UITheme.FontMedium),
                    new Vector2(rect.X + 16, rect.Y + (rowHeight - textH) / 2f), selected ? UITheme.Gold : UITheme.Text, UITheme.FontMedium);
            }
        }

        UITheme.DrawTextCentered(spriteBatch, "ENTER to load  -  ESC to go back", new Rectangle(card.X, card.Bottom - 34, card.Width, 20), UITheme.TextMuted, UITheme.FontSmall);
    }

    private void DrawPauseMenu(SpriteBatch spriteBatch, int screenWidth, int screenHeight)
    {
        _menuItemBounds.Clear();

        // Dim the running world behind the menu instead of hiding it
        spriteBatch.Draw(_pixelTexture, new Rectangle(0, 0, screenWidth, screenHeight), new Color(4, 6, 12) * 0.72f);

        int buttonWidth = 280;
        int buttonHeight = 46;
        int spacing = 10;
        int cardWidth = buttonWidth + 60;
        int cardHeight = 90 + _pauseMenuItems.Length * (buttonHeight + spacing) + 20;
        var card = new Rectangle((screenWidth - cardWidth) / 2, (screenHeight - cardHeight) / 2, cardWidth, cardHeight);
        UITheme.DrawPanel(spriteBatch, card, new Color(12, 18, 30, 240), UITheme.BorderBright);
        UITheme.DrawTextCentered(spriteBatch, "PAUSED", new Rectangle(card.X, card.Y + 18, card.Width, 40), UITheme.Gold, UITheme.FontTitle);
        spriteBatch.Draw(UITheme.Pixel, new Rectangle(card.X + 30, card.Y + 70, card.Width - 60, 1), UITheme.Border);

        DrawMenuButtons(spriteBatch, _pauseMenuItems, card.X + 30, card.Y + 86, buttonWidth, buttonHeight, spacing);
    }

    private void DrawBorder(SpriteBatch spriteBatch, int x, int y, int width, int height, Color color, int thickness)
    {
        spriteBatch.Draw(_pixelTexture, new Rectangle(x, y, width, thickness), color);
        spriteBatch.Draw(_pixelTexture, new Rectangle(x, y + height - thickness, width, thickness), color);
        spriteBatch.Draw(_pixelTexture, new Rectangle(x, y, thickness, height), color);
        spriteBatch.Draw(_pixelTexture, new Rectangle(x + width - thickness, y, thickness, height), color);
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
    ShowAbout,
    Quit
}
