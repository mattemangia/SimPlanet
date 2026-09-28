using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Reflection;

namespace SimPlanet
{
    /// <summary>
    /// Splash screen drawn by the main game window during the first seconds.
    ///
    /// This used to be a separate MonoGame <see cref="Game"/> that ran before SimPlanetGame.
    /// Running two Game instances in the same process is not supported by MonoGame DesktopGL:
    /// the first instance's GL context is queued for deletion and is only destroyed after
    /// SDL has been shut down and re-initialised by the second instance, which crashes
    /// (SIGSEGV in X11_GL_DeleteContext / GLXBadContext) on Mesa and other Linux drivers.
    /// Drawing the splash inside the main game avoids the second context entirely.
    /// </summary>
    public class SplashScreen : IDisposable
    {
        private const float DISPLAY_DURATION = 2.6f;
        private const float FADE_IN_DURATION = 0.3f;
        private const float FADE_OUT_DURATION = 0.4f;

        private readonly Texture2D? _splashTexture;
        private readonly Texture2D _pixel;
        private float _elapsed;
        private bool _skipped;

        public bool IsActive { get; private set; } = true;

        public SplashScreen(GraphicsDevice graphicsDevice)
        {
            _pixel = new Texture2D(graphicsDevice, 1, 1);
            _pixel.SetData(new[] { Color.White });

            // Allow disabling the splash (useful for automated runs)
            if (Environment.GetEnvironmentVariable("SIMPLANET_NO_SPLASH") == "1")
            {
                IsActive = false;
                return;
            }

            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                using var stream = assembly.GetManifestResourceStream("SimPlanet.splash.png");
                if (stream != null)
                {
                    _splashTexture = Texture2D.FromStream(graphicsDevice, stream);
                }
                else
                {
                    Console.WriteLine("Failed to load splash.png from embedded resource");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to load splash.png: {ex.Message}");
            }

            if (_splashTexture == null)
                IsActive = false;
        }

        /// <summary>
        /// Advance the splash animation. Any key or mouse click skips it.
        /// </summary>
        public void Update(GameTime gameTime, KeyboardState keyState, MouseState mouseState)
        {
            if (!IsActive) return;

            // Clamp the first (possibly very long) frame so the fade-in is visible
            _elapsed += Math.Min(0.1f, (float)gameTime.ElapsedGameTime.TotalSeconds);

            if (!_skipped && _elapsed > 0.5f &&
                (keyState.GetPressedKeyCount() > 0 || mouseState.LeftButton == ButtonState.Pressed))
            {
                _skipped = true;
                _elapsed = Math.Max(_elapsed, DISPLAY_DURATION);
            }

            if (_elapsed >= DISPLAY_DURATION + FADE_OUT_DURATION)
                IsActive = false;
        }

        private float Alpha
        {
            get
            {
                if (_elapsed < FADE_IN_DURATION) return _elapsed / FADE_IN_DURATION;
                if (_elapsed < DISPLAY_DURATION) return 1f;
                return Math.Max(0f, 1f - (_elapsed - DISPLAY_DURATION) / FADE_OUT_DURATION);
            }
        }

        /// <summary>
        /// Draw the splash covering the whole screen (letterboxed, aspect preserved).
        /// Must be called inside an active SpriteBatch.Begin/End.
        /// </summary>
        public void Draw(SpriteBatch spriteBatch, int screenWidth, int screenHeight)
        {
            if (!IsActive || _splashTexture == null) return;

            spriteBatch.Draw(_pixel, new Rectangle(0, 0, screenWidth, screenHeight), Color.Black);

            float scale = Math.Min((float)screenWidth / _splashTexture.Width, (float)screenHeight / _splashTexture.Height);
            int w = (int)(_splashTexture.Width * scale);
            int h = (int)(_splashTexture.Height * scale);
            var dest = new Rectangle((screenWidth - w) / 2, (screenHeight - h) / 2, w, h);
            spriteBatch.Draw(_splashTexture, dest, Color.White * Alpha);
        }

        public void Dispose()
        {
            _splashTexture?.Dispose();
            _pixel.Dispose();
        }
    }
}
