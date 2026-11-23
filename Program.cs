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
    // The splash screen is drawn by SimPlanetGame itself (see SplashScreen.cs).
    // Running a separate MonoGame Game instance for it crashes on Linux/Mesa.

    // Sandbox start: --civs N --start-tech N puts developed nations on every new world
    for (int i = 0; i < args.Length - 1; i++)
    {
        if (args[i] == "--civs" && int.TryParse(args[i + 1], out int civs)) SimPlanetGame.StartupCivilizations = civs;
        if (args[i] == "--start-tech" && int.TryParse(args[i + 1], out int tech)) SimPlanetGame.StartupTechLevel = tech;
    }

    // Start the main game
    using var game = new SimPlanetGame();
    game.Run();
}
