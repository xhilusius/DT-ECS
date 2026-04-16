using Simulation;
using Simulation.ServiceManager.CompositeServices;

Console.WriteLine("╔═════════════════════════════════════════════════════════════╗");
Console.WriteLine("║            DIGITAL TWIN SIMULATION - TEST RUN               ║");
Console.WriteLine("╚═════════════════════════════════════════════════════════════╝\n");

bool continueRunning = true;
while (continueRunning)
{
    var testCases = TestExecutorService.LoadAvailableTestCases();
    TestCases.ListAll(testCases);

    Console.Write($"\nSelect test case number (1-{testCases.Count}): ");
    if (!int.TryParse(Console.ReadLine(), out var sel) || sel < 1 || sel > testCases.Count)
    {
        Console.WriteLine("Invalid selection.");
        continue;
    }

    var selected = testCases[sel - 1];

    // Inner setup override
    var setupFolders = GetInnerSetupFolders();
    Console.WriteLine("\n╔═════════════════════════════════════════════════════════════╗");
    Console.WriteLine("║              SELECT CONFIGURATION SETUP                      ║");
    Console.WriteLine("╚═════════════════════════════════════════════════════════════╝\n");
    for (int i = 0; i < setupFolders.Count; i++)
    {
        var isDefault = setupFolders[i] == selected.InnerSetup;
        Console.WriteLine($"  [{i}] {setupFolders[i]}{(isDefault ? " (default from test case)" : "")}");
    }
    Console.Write($"\nSelect configuration setup (0-{setupFolders.Count - 1}, or press Enter for default): ");
    var cfgIn = Console.ReadLine();
    string? configOverride = null;
    if (!string.IsNullOrWhiteSpace(cfgIn) && int.TryParse(cfgIn, out var cfgIdx)
        && cfgIdx >= 0 && cfgIdx < setupFolders.Count
        && setupFolders[cfgIdx] != selected.InnerSetup)
        configOverride = setupFolders[cfgIdx];

    // Visualization
    Console.Write("Use external visualization (Godot)? (y/n): ");
    var useViz = string.Equals(Console.ReadLine(), "y", StringComparison.OrdinalIgnoreCase);
    Simulation.StateManager.VisualizationMapper? visualizationMapper = null;
    if (useViz)
    {
        visualizationMapper = new Simulation.StateManager.VisualizationMapper();
        var (connected, msg) = await visualizationMapper.TestConnectionAsync();
        Console.WriteLine($"   {msg}\n");
        if (connected) Console.WriteLine("✅ Visualization server is ready!\n");
        else Console.WriteLine("⚠️  WARNING: Running without visualization connection.\n");
    }

    // Print options
    bool printOnlyFirstAndLast = false;
    int printEveryNSteps = 1;
    Console.Write("Print only first and last state? (y/n): ");
    if (string.Equals(Console.ReadLine(), "y", StringComparison.OrdinalIgnoreCase))
    {
        printOnlyFirstAndLast = true;
    }
    else
    {
        Console.Write("Print every N steps (Enter for every step): ");
        var nIn = Console.ReadLine();
        if (!string.IsNullOrWhiteSpace(nIn) && int.TryParse(nIn, out var n) && n > 0)
            printEveryNSteps = n;
    }

    Console.Write("Press Enter to START the simulation (or 'n' to cancel): ");
    if (string.Equals(Console.ReadLine(), "n", StringComparison.OrdinalIgnoreCase))
    {
        Console.WriteLine("Simulation cancelled.");
        continue;
    }

    var ic = await SimulationInitializer.CreateAsync(selected.FilePath, visualizationMapper, configOverride, printOnlyFirstAndLast, printEveryNSteps);
    await ic.RunAsync();

    // Post-run
    Console.WriteLine("\n╔═════════════════════════════════════════════════════════════╗");
    Console.WriteLine("║         TEST EXECUTION COMPLETE - WHAT'S NEXT?              ║");
    Console.WriteLine("╚═════════════════════════════════════════════════════════════╝\n");

    if (useViz && visualizationMapper != null)
    {
        Console.Write("Clear visualization? (y/n): ");
        if (string.Equals(Console.ReadLine(), "y", StringComparison.OrdinalIgnoreCase))
        {
            await ic.GetEntityManager().GetStateManager().NotifyVisualizationClearedAsync();
            Console.WriteLine("Visualization cleared.\n");
        }
    }

    Console.Write("Exit application? (y/n, default is exit): ");
    var exitIn = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(exitIn) || !string.Equals(exitIn, "n", StringComparison.OrdinalIgnoreCase))
    {
        continueRunning = false;
        Console.WriteLine("Exiting application.");
    }
    else
    {
        Console.WriteLine("\n🔄 Returning to test selection...\n");
    }
}

static List<string> GetInnerSetupFolders()
{
    var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "TestFiles", "CompositeSetups");
    return Directory.GetDirectories(path)
        .Select(Path.GetFileName).Where(f => f != null).Cast<string>()
        .OrderBy(f => f).ToList();
}

