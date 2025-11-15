using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.IO;
using System.Reflection;

namespace SimPlanet
{
    /// <summary>
    /// Cross-platform splash screen using MonoGame
    /// Displays the game logo before the main game starts
    /// </summary>
    public class SplashScreen : Game
    {
        private GraphicsDeviceManager graphics;
        private SpriteBatch spriteBatch;
        private Texture2D splashTexture;
        private DateTime startTime;
        private const float DISPLAY_DURATION = 3.0f; // 3 seconds
        private const float FADE_IN_DURATION = 0.3f; // 300ms fade in
        private const float FADE_OUT_DURATION = 0.3f; // 300ms fade out
        private float elapsedTime = 0f;
        private float alpha = 0f;

        public SplashScreen()
        {
            graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            IsMouseVisible = false;

            // Borderless window
            Window.IsBorderless = true;

            // Set resolution
            graphics.PreferredBackBufferWidth = 1920;
            graphics.PreferredBackBufferHeight = 1080;
            graphics.IsFullScreen = false;
        }

        protected override void Initialize()
        {
            startTime = DateTime.Now;
            base.Initialize();
        }

        protected override void LoadContent()
        {
            spriteBatch = new SpriteBatch(GraphicsDevice);

            // Load splash.png from embedded resource
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                using (var stream = assembly.GetManifestResourceStream("SimPlanet.splash.png"))
                {
                    if (stream != null)
                    {
                        splashTexture = Texture2D.FromStream(GraphicsDevice, stream);

                        // Adjust window size to match image
                        graphics.PreferredBackBufferWidth = splashTexture.Width;
                        graphics.PreferredBackBufferHeight = splashTexture.Height;
                        graphics.ApplyChanges();

                        // Center window on screen
                        var screen = GraphicsDevice.Adapter.CurrentDisplayMode;
                        Window.Position = new Point(
                            (screen.Width - graphics.PreferredBackBufferWidth) / 2,
                            (screen.Height - graphics.PreferredBackBufferHeight) / 2
                        );
                    }
                    else
                    {
                        Console.WriteLine("Failed to load splash.png from embedded resource");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to load splash.png: {ex.Message}");
            }
        }

        protected override void Update(GameTime gameTime)
        {
            elapsedTime += (float)gameTime.ElapsedGameTime.TotalSeconds;

            // Calculate alpha based on time
            if (elapsedTime < FADE_IN_DURATION)
            {
                // Fade in
                alpha = elapsedTime / FADE_IN_DURATION;
            }
            else if (elapsedTime < DISPLAY_DURATION)
            {
                // Fully visible
                alpha = 1.0f;
            }
            else if (elapsedTime < DISPLAY_DURATION + FADE_OUT_DURATION)
            {
                // Fade out
                float fadeOutProgress = (elapsedTime - DISPLAY_DURATION) / FADE_OUT_DURATION;
                alpha = 1.0f - fadeOutProgress;
            }
            else
            {
                // Done - exit splash screen
                Exit();
            }

            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.Black);

            if (splashTexture != null)
            {
                spriteBatch.Begin();
                spriteBatch.Draw(
                    splashTexture,
                    new Rectangle(0, 0, graphics.PreferredBackBufferWidth, graphics.PreferredBackBufferHeight),
                    Color.White * alpha
                );
                spriteBatch.End();
            }

            base.Draw(gameTime);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                splashTexture?.Dispose();
                spriteBatch?.Dispose();
                graphics?.Dispose();
            }
            base.Dispose(disposing);
        }

        /// <summary>
        /// Show the splash screen (cross-platform compatible)
        /// </summary>
        public static void ShowSplash()
        {
            using (var splash = new SplashScreen())
            {
                splash.Run();
            }
        }
    }
}
