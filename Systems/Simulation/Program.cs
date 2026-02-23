Console.WriteLine("╔═════════════════════════════════════════════════════════════╗");
Console.WriteLine("║            DIGITAL TWIN SIMULATION - TEST RUN               ║");
Console.WriteLine("╚═════════════════════════════════════════════════════════════╝\n");

// Display available test cases
TestCases.ListAll();
Console.Write("\nSelect test case number (0 - " + (TestCases.All.Count - 1) + "): ");
var selectionInput = Console.ReadLine();

if (int.TryParse(selectionInput, out var testIndex))
{
    if (testIndex < 0 || testIndex >= TestCases.All.Count)
    {
        Console.WriteLine("Invalid test case index.");
    }
    else
    {
        var selectedTest = TestCases.All[testIndex];

        // Show test details before running
        Console.WriteLine("\n╔═════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║              READY TO START SIMULATION                     ║");
        Console.WriteLine("╚═════════════════════════════════════════════════════════════╝\n");
        Console.WriteLine($"📋 Test: {selectedTest.Name}");
        Console.WriteLine($"📝 Setup: {selectedTest.Setup.ConfigurationFile}");
        Console.WriteLine($"🎯 Entities: {selectedTest.Setup.Entities.Count}");
        Console.WriteLine($"ℹ️  Description: {selectedTest.Setup.Description}\n");
        
        Console.Write("Use external visualization (Godot)? (y/n): ");
        var visualizationInput = Console.ReadLine();
        var useVisualization = string.Equals(visualizationInput, "y", StringComparison.OrdinalIgnoreCase);

        Simulation.StateManager.VisualizationMapper? visualizationMapper = null;
        if (useVisualization)
        {
            // Test connection to visualization server
            Console.WriteLine("🔍 Testing connection to visualization server...");
            visualizationMapper = new Simulation.StateManager.VisualizationMapper();
            var (isConnected, connectionMessage) = await visualizationMapper.TestConnectionAsync();
            Console.WriteLine($"   {connectionMessage}\n");

            if (isConnected)
            {
                Console.WriteLine("✅ Visualization server is ready!\n");
            }
            else
            {
                Console.WriteLine("⚠️  WARNING: You are running without visualization connection.\n");
                Console.WriteLine("   To integrate with Godot, ensure:");
                Console.WriteLine("   1. Godot is running your visualization project");
                Console.WriteLine("   2. HTTP server is listening on http://127.0.0.1:8080");
                Console.WriteLine("   3. POST /balls/update endpoint is ready to receive updates\n");
            }
        }
        
        Console.Write("Press Enter to START the simulation (or 'n' to cancel): ");
        var confirmInput = Console.ReadLine();

        if (confirmInput?.ToLower() != "n")
        {
            // Components are initialized with test data when test runs
            // Pass the visualization mapper so entities are sent to Godot during creation
            var stateManager = await TestCases.RunByIndexAsync(testIndex, visualizationMapper);

            if (useVisualization)
            {
                Console.Write("Press Enter to CLEAR visualization and exit (or 'n' to skip): ");
                var exitInput = Console.ReadLine();
                if (exitInput?.ToLower() != "n")
                {
                    await stateManager.NotifyVisualizationClearedAsync();
                    Console.WriteLine("Visualization cleared. Exiting.");
                }
            }
        }
        else
        {
            Console.WriteLine("Simulation cancelled.");
        }
    }
}
else
{
    Console.WriteLine("Invalid selection. No test case executed.");
}
