namespace Simulation;

using DataStorage.RepositoryManager;
using Simulation.EntityManager;
using Simulation.ServiceManager;
using Simulation.SimulationEngine;
using Simulation.StateManager;
using Simulation.InteractionController;

/// <summary>
/// Responsible for creating and initializing all simulation components from test setup data.
/// This ensures components are created with their data upfront when a test runs,
/// rather than initialized eagerly at application startup.
/// </summary>
public static class SimulationInitializer
{
    /// <summary>
    /// Creates and initializes all simulation components from a test setup definition.
    /// Components receive their data at initialization time.
    /// Connects visualization mapper so entities are sent to external tools (Godot, etc) immediately.
    /// </summary>
    /// <param name="testSetup">The test setup definition containing configuration and entity data</param>
    /// <param name="visualizationMapper">Optional visualization mapper for sending updates to external tools</param>
    /// <returns>InteractionController ready to run the simulation</returns>
    public static async Task<global::Simulation.InteractionController.InteractionController> InitializeFromTestSetupAsync(
        TestSetup testSetup,
        Simulation.StateManager.VisualizationMapper? visualizationMapper = null)
    {
        Console.WriteLine("╔═════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║              INITIALIZING SIMULATION COMPONENTS             ║");
        Console.WriteLine("╚═════════════════════════════════════════════════════════════╝\n");

        Console.WriteLine($"📄 Test: {testSetup.Description}");
        Console.WriteLine($"⚙️  Configuration: {testSetup.ConfigurationFile}");
        Console.WriteLine($"🎯 Entities to create: {testSetup.Entities.Count}\n");

        // ===== STEP 1: Initialize the dependency hierarchy =====
        Console.WriteLine("[1/6] Initializing Data Storage Layer...");
        var repositoryManager = new global::DataStorage.RepositoryManager.RepositoryManager();

        Console.WriteLine("[2/6] Initializing Entity Manager...");
        var entityManager = new global::Simulation.EntityManager.EntityManager();

        Console.WriteLine("[3/6] Initializing State Manager...");
        var stateManager = new global::Simulation.StateManager.StateManager(repositoryManager, entityManager);
        
        // Set StateManager reference in EntityManager (circular dependency resolution)
        entityManager.SetStateManager(stateManager);
        
        // Connect visualization mapper if provided (for Godot, Unity, etc)
        if (visualizationMapper != null)
        {
            stateManager.SetVisualizationMapper(visualizationMapper);
            Console.WriteLine("   📡 Visualization mapper connected - entities will be sent to external tool");
        }

        Console.WriteLine("[4/6] Initializing Simulation Engine...");
        var simEngine = new SimEngine(stateManager);

        Console.WriteLine("[5/6] Initializing Service Manager...");
        var serviceManager = new global::Simulation.ServiceManager.ServiceManager(simEngine);
        await serviceManager.InitializeAsync(testSetup.ConfigurationFile);

        Console.WriteLine("[6/6] Initializing Interaction Controller...");
        var interactionController = new global::Simulation.InteractionController.InteractionController(serviceManager, entityManager);

        Console.WriteLine("✓ Component hierarchy initialized.\n");

        // ===== STEP 2: Create entities from test setup =====
        Console.WriteLine("╔═════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║                  CREATING TEST ENTITIES                     ║");
        Console.WriteLine("╚═════════════════════════════════════════════════════════════╝\n");

        foreach (var entityDef in testSetup.Entities)
        {
            Console.WriteLine($"Creating entity: {entityDef.Name}");
            await interactionController.CreateEntityAsync(
                entityDef.Name,
                entityDef.Properties,
                entityDef.Description
            );
        }

        Console.WriteLine($"\n✓ Created {testSetup.Entities.Count} entities successfully.\n");

        // Display initial state
        await stateManager.ReportStateAsync("Initial State");

        return interactionController;
    }
}
