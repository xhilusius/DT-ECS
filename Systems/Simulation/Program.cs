Console.WriteLine("╔═════════════════════════════════════════════════════════════╗");
Console.WriteLine("║            DIGITAL TWIN SIMULATION - TEST RUN               ║");
Console.WriteLine("╚═════════════════════════════════════════════════════════════╝\n");

bool continueRunning = true;
while (continueRunning)
{
    // Step 1: Display available test cases
    TestCases.ListAll();
    Console.Write("\nSelect test case number (1 - " + TestCases.All.Count + "): ");
    var selectionInput = Console.ReadLine();

    if (int.TryParse(selectionInput, out var testIndex))
    {
        var zeroBasedIndex = testIndex - 1;
        if (zeroBasedIndex < 0 || zeroBasedIndex >= TestCases.All.Count)
        {
            Console.WriteLine("Invalid test case index.");
        }
        else
        {
            var selectedTest = TestCases.All[zeroBasedIndex];

            // Step 2: Discover and display available configuration files
            var setupsPath = Path.Combine(AppContext.BaseDirectory, "src", "components", "ServiceManager", "ServiceSetups");
            var configFiles = Directory.GetFiles(setupsPath, "*Setup.json")
                .Select(Path.GetFileName)
                .Where(f => f != null)
                .Cast<string>()
                .OrderBy(f => f)
                .ToList();

            Console.WriteLine("\n╔═════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║              SELECT CONFIGURATION FILE                      ║");
            Console.WriteLine("╚═════════════════════════════════════════════════════════════╝\n");
            
            for (int i = 0; i < configFiles.Count; i++)
            {
                var isDefault = configFiles[i] == selectedTest.Setup.ConfigurationFile;
                var marker = isDefault ? " (default from test case)" : "";
                Console.WriteLine($"  [{i}] {configFiles[i]}{marker}");
            }
            
            Console.Write($"\nSelect configuration file (0-{configFiles.Count - 1}, or press Enter for default): ");
            var configInput = Console.ReadLine();
            
            string selectedConfigFile = selectedTest.Setup.ConfigurationFile;
            
            if (!string.IsNullOrWhiteSpace(configInput) && int.TryParse(configInput, out var configIndex))
            {
                if (configIndex >= 0 && configIndex < configFiles.Count)
                {
                    selectedConfigFile = configFiles[configIndex];
                }
                else
                {
                    Console.WriteLine($"Invalid index. Using default: {selectedConfigFile}");
                }
            }
            
            // Determine if we're overriding the default configuration
            string? configOverride = (selectedConfigFile != selectedTest.Setup.ConfigurationFile) ? selectedConfigFile : null;

            // Show test details before running
            Console.WriteLine("\n╔═════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║              READY TO START SIMULATION                      ║");
            Console.WriteLine("╚═════════════════════════════════════════════════════════════╝\n");
            Console.WriteLine($"📋 Test: {selectedTest.Name}");
            Console.WriteLine($"📝 Configuration: {selectedConfigFile}");
            Console.WriteLine($"🎯 Initial entities: {selectedTest.Setup.Steps.FirstOrDefault(step => step.Step == 0)?.Actions.Count ?? 0}");
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
                var stateManager = await TestCases.RunByIndexAsync(zeroBasedIndex, visualizationMapper, configOverride);

                // Ask whether to exit or run another test
                Console.WriteLine("\n╔═════════════════════════════════════════════════════════════╗");
                Console.WriteLine("║         TEST EXECUTION COMPLETE - WHAT'S NEXT?              ║");
                Console.WriteLine("╚═════════════════════════════════════════════════════════════╝\n");
                
                if (useVisualization)
                {
                    Console.Write("Clear visualization? (y/n): ");
                    var clearInput = Console.ReadLine();
                    if (clearInput?.ToLower() == "y")
                    {
                        await stateManager.NotifyVisualizationClearedAsync();
                        Console.WriteLine("Visualization cleared.\n");
                    }
                }
                
                Console.Write("Exit application? (y/n, default is exit): ");
                var exitInput = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(exitInput) || exitInput?.ToLower() != "n")
                {
                    continueRunning = false;
                    Console.WriteLine("Exiting application.");
                }
                else
                {
                    Console.WriteLine("\n🔄 Returning to test selection...\n");
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
}
