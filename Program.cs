using System;
using SimPlanet;

// Check for headless mode
bool headless = false;
foreach (var arg in args)
{
    if (arg == "--no-gui")
    {
        headless = true;
        break;
    }
}

if (headless)
{
    try
    {
        Console.WriteLine("Program started in headless mode.");
        var simulation = new HeadlessSimulation();
        simulation.Run(args);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"CRITICAL ERROR: {ex.Message}");
        Console.WriteLine(ex.StackTrace);
        Environment.Exit(1);
    }
}
else
{
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
}
