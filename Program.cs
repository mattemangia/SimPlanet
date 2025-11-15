using System;
using SimPlanet;

// Show splash screen before starting the game (cross-platform MonoGame splash)
try
{
    SplashScreen.ShowSplash();
}
catch (Exception ex)
{
    // If splash screen fails, just continue to game
    Console.WriteLine($"Splash screen error: {ex.Message}");
}

// Start the main game
using var game = new SimPlanetGame();
game.Run();
