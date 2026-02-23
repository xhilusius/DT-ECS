using System.Numerics;
using System.Drawing;
using Simulation;
using Simulation.Interfaces;
using Simulation.StateManager;
using Simulation.ServiceManager;

public static class TestCases
{
    public record TestCase(string Name, TestSetup Setup, Func<IInteractionController, StateManager, Task> ExecutionLogic);

    public static IReadOnlyList<TestCase> All { get; } = new List<TestCase>
    {
        new(
            "Single entity: gravity and positioning",
            // Setup data
            new TestSetup
            {
                ConfigurationFile = "DefaultSetup.json",
                Description = "Single entity falling under gravity with terminal velocity",
                Entities = new List<EntityDefinition>
                {
                    new()
                    {
                        Name = "Ball",
                        Properties = new Dictionary<string, object>
                        {
                            { "Mass", 1.0f },
                            { "Position", new Vector3(0, 50, 0) },
                            { "Displacement", new Vector3(0, 0, 0) },
                            { "CurrentSpeed", new Vector3(0, 0, 0) },
                            { "Radius", 0.2f },                            
                            { "GravityForce", new Vector3(0, 0, 0) },
                            { "Color", Color.Red }
                        },
                        Description = "Non-metallic, no wind (missing MagnetismForce and WindForce properties) - gravity only"
                    }
                }
            },
            // Execution logic
            async (controller, stateManager) =>
            {
                Console.WriteLine("╔═════════════════════════════════════════════════════════════╗");
                Console.WriteLine("║       TEST CASE 1: Single Entity - Gravity & Positioning    ║");
                Console.WriteLine("╚═════════════════════════════════════════════════════════════╝\n");

                try
                {
                    // Load configuration to get simulation step count
                    var config = ServiceSetupLoader.LoadConfiguration("DefaultSetup.json");
                    int simulationSteps = config.SimulationSteps;

                    // Run simulation for configured number of steps to see gravity and terminal velocity effects
                    for (int step = 1; step <= simulationSteps; step++)
                    {
                        await controller.OneStepAsync();
                    }

                    // Stop simulation
                    await controller.StopAsync();

                    Console.WriteLine("✓ Test case completed successfully!\n");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"✗ Test case failed: {ex.Message}");
                    Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                }
            }
        ),
        new(
            "Three entities: mass and radius variations",
            // Setup data
            new TestSetup
            {
                ConfigurationFile = "DefaultSetup.json",
                Description = "Three entities with different mass/radius combinations showing terminal velocity differences",
                Entities = new List<EntityDefinition>
                {
                    new()
                    {
                        Name = "Entity 1 (small radius)",
                        Properties = new Dictionary<string, object>
                        {
                            { "Mass", 0.5f },
                            { "Position", new Vector3(-2, 50, 0) },
                            { "Displacement", new Vector3(0, 0, 0) },
                            { "CurrentSpeed", new Vector3(0, 0, 0) },
                            { "Radius", 0.4f },
                            { "GravityForce", new Vector3(0, 0, 0) },
                            { "DragForce", new Vector3(0, 0, 0) },
                            { "Color", Color.Blue }
                        },
                        Description = "Mass=0.5kg, Radius=0.4m (low mass, moderate drag)"
                    },
                    new()
                    {
                        Name = "Entity 2 (large radius)",
                        Properties = new Dictionary<string, object>
                        {
                            { "Mass", 0.5f },
                            { "Position", new Vector3(0, 50, 0) },
                            { "Displacement", new Vector3(0, 0, 0) },
                            { "CurrentSpeed", new Vector3(0, 0, 0) },
                            { "Radius", 0.6f },
                            { "GravityForce", new Vector3(0, 0, 0) },
                            { "DragForce", new Vector3(0, 0, 0) },
                            { "Color", Color.Lime }
                        },
                        Description = "Mass=0.5kg, Radius=0.6m (same mass as E1, more drag = SLOWEST)"
                    },
                    new()
                    {
                        Name = "Entity 3 (heavy)",
                        Properties = new Dictionary<string, object>
                        {
                            { "Mass", 2.0f },
                            { "Position", new Vector3(2, 50, 0) },
                            { "Displacement", new Vector3(0, 0, 0) },
                            { "CurrentSpeed", new Vector3(0, 0, 0) },
                            { "Radius", 0.6f },
                            { "GravityForce", new Vector3(0, 0, 0) },
                            { "DragForce", new Vector3(0, 0, 0) },
                            { "Color", Color.Yellow }
                        },
                        Description = "Mass=2.0kg, Radius=0.6m (heavier, same drag as E2 = FASTEST)"
                    }
                }
            },
            // Execution logic
            async (controller, stateManager) =>
            {
                Console.WriteLine("╔═════════════════════════════════════════════════════════════╗");
                Console.WriteLine("║  TEST CASE 2: Three Entities - Mass & Radius Variations     ║");
                Console.WriteLine("║  E1 & E2: same mass, different radius                       ║");
                Console.WriteLine("║  E2 & E3: same radius, different mass                       ║");
                Console.WriteLine("╚═════════════════════════════════════════════════════════════╝\n");

                try
                {
                    // Load configuration to get simulation step count
                    var config = ServiceSetupLoader.LoadConfiguration("DefaultSetup.json");
                    int simulationSteps = config.SimulationSteps;

                    // Run simulation for configured number of steps to observe clear differences
                    for (int step = 1; step <= simulationSteps; step++)
                    {
                        await controller.OneStepAsync();
                    }

                    // Stop simulation
                    await controller.StopAsync();

                    Console.WriteLine("✓ Test case completed successfully!\n");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"✗ Test case failed: {ex.Message}");
                    Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                }
            }
        ),
        new(
            "Two entities: one complete, one incomplete",
            // Setup data
            new TestSetup
            {
                ConfigurationFile = "DefaultSetup.json",
                Description = "Two entities: one with all properties (simulates), one missing Mass (no behavior)",
                Entities = new List<EntityDefinition>
                {
                    new()
                    {
                        Name = "Complete entity",
                        Properties = new Dictionary<string, object>
                        {
                            { "Mass", 1.0f },
                            { "Position", new Vector3(-1, 50, 0) },
                            { "Displacement", new Vector3(0, 0, 0) },
                            { "CurrentSpeed", new Vector3(0, 0, 0) },
                            { "Radius", 0.2f },
                            { "GravityForce", new Vector3(0, 0, 0) },
                            { "DragForce", new Vector3(0, 0, 0) },
                            { "Color", Color.Cyan }
                        },
                        Description = "Has all properties: Mass, Position, CurrentSpeed, Radius, Displacement"
                    },
                    new()
                    {
                        Name = "Incomplete entity",
                        Properties = new Dictionary<string, object>
                        {
                            { "Position", new Vector3(1, 50, 0) },
                            { "Displacement", new Vector3(0, 0, 0) },
                            { "CurrentSpeed", new Vector3(0, 0, 0) },
                            { "Radius", 0.2f },
                            { "Color", Color.Magenta }
                        },
                        Description = "Missing Mass property - will have no behavior"
                    }
                }
            },
            // Execution logic
            async (controller, stateManager) =>
            {
                Console.WriteLine("╔═════════════════════════════════════════════════════════════╗");
                Console.WriteLine("║  TEST CASE 3: Two Entities - Complete vs Incomplete         ║");
                Console.WriteLine("║  E1: has all required properties (will simulate)            ║");
                Console.WriteLine("║  E2: missing Mass (will NOT simulate)                       ║");
                Console.WriteLine("╚═════════════════════════════════════════════════════════════╝\n");

                try
                {
                    // Load configuration to get simulation step count
                    var config = ServiceSetupLoader.LoadConfiguration("DefaultSetup.json");
                    int simulationSteps = config.SimulationSteps;

                    // Run simulation for configured number of steps
                    for (int step = 1; step <= simulationSteps; step++)
                    {
                        await controller.OneStepAsync();
                    }

                    // Stop simulation
                    await controller.StopAsync();

                    Console.WriteLine("✓ Test case completed successfully!\n");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"✗ Test case failed: {ex.Message}");
                    Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                }
            }
        ),
        new(
            "Magnetism: metallic vs non-metallic entities",
            // Setup data
            new TestSetup
            {
                ConfigurationFile = "DefaultSetup.json",
                Description = "Two entities: one metallic (affected by magnetic field), one non-metallic (gravity only)",
                Entities = new List<EntityDefinition>
                {
                    new()
                    {
                        Name = "Metal ball",
                        Properties = new Dictionary<string, object>
                        {
                            { "Mass", 1.0f },
                            { "Position", new Vector3(-1, 50, 0) },
                            { "Displacement", new Vector3(0, 0, 0) },
                            { "CurrentSpeed", new Vector3(0, 0, 0) },
                            { "Radius", 0.2f },                            
                            { "GravityForce", new Vector3(0, 0, 0) },
                            { "DragForce", new Vector3(0, 0, 0) },                           
                            { "MagnetismForce", new Vector3(0, 0, 0) },
                            { "Color", Color.Silver }
                        },
                        Description = "Metallic object (has MagnetismForce property) - affected by both gravity and magnetic field (upward)"
                    },
                    new()
                    {
                        Name = "Plastic ball",
                        Properties = new Dictionary<string, object>
                        {
                            { "Mass", 1.0f },
                            { "Position", new Vector3(1, 50, 0) },
                            { "Displacement", new Vector3(0, 0, 0) },
                            { "CurrentSpeed", new Vector3(0, 0, 0) },
                            { "Radius", 0.2f },
                            { "GravityForce", new Vector3(0, 0, 0) },
                            { "DragForce", new Vector3(0, 0, 0) },
                            { "Color", Color.Orange }
                        },
                        Description = "Non-metallic object (no MagnetismForce property) - affected by gravity and drag only"
                    }
                }
            },
            // Execution logic
            async (controller, stateManager) =>
            {
                Console.WriteLine("╔═════════════════════════════════════════════════════════════╗");
                Console.WriteLine("║  TEST CASE 4: Magnetism - Metallic vs Non-Metallic          ║");
                Console.WriteLine("║  Metal ball: gravity + magnetic force (upward)              ║");
                Console.WriteLine("║  Plastic ball: gravity only                                 ║");
                Console.WriteLine("╚═════════════════════════════════════════════════════════════╝\n");

                try
                {
                    // Load configuration to get simulation step count
                    var config = ServiceSetupLoader.LoadConfiguration("DefaultSetup.json");
                    int simulationSteps = config.SimulationSteps;

                    // Run simulation for configured number of steps to see magnetic effect
                    for (int step = 1; step <= simulationSteps; step++)
                    {
                        await controller.OneStepAsync();
                    }

                    // Stop simulation
                    await controller.StopAsync();

                    Console.WriteLine("✓ Test case completed successfully!\n");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"✗ Test case failed: {ex.Message}");
                    Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                }
            }
        ),
        new(
            "Wind effect: large radius vs small radius",
            // Setup data
            new TestSetup
            {
                ConfigurationFile = "DefaultSetup.json",
                Description = "Two entities with same mass: one with large radius (affected by wind), one with small radius (minimal wind effect)",
                Entities = new List<EntityDefinition>
                {
                    new()
                    {
                        Name = "Large ball (wind-affected)",
                        Properties = new Dictionary<string, object>
                        {
                            { "Mass", 1.0f },
                            { "Position", new Vector3(-1.5f, 50, 0) },
                            { "Displacement", new Vector3(0, 0, 0) },
                            { "CurrentSpeed", new Vector3(0, 0, 0) },
                            { "Radius", 1.0f },
                            { "GravityForce", new Vector3(0, 0, 0) },
                            { "DragForce", new Vector3(0, 0, 0) },
                            { "WindForce", new Vector3(0, 0, 0) },
                            { "Color", Color.DeepSkyBlue }
                        },
                        Description = "Mass=1.0kg, Radius=1.0m - large cross-section, significantly affected by wind"
                    },
                    new()
                    {
                        Name = "Small ball (minimal wind)",
                        Properties = new Dictionary<string, object>
                        {
                            { "Mass", 1.0f },
                            { "Position", new Vector3(1.5f, 50, 0) },
                            { "Displacement", new Vector3(0, 0, 0) },
                            { "CurrentSpeed", new Vector3(0, 0, 0) },
                            { "Radius", 0.5f },
                            { "GravityForce", new Vector3(0, 0, 0) },
                            { "DragForce", new Vector3(0, 0, 0) },
                            { "WindForce", new Vector3(0, 0, 0) },
                            { "Color", Color.Turquoise }
                        },
                        Description = "Mass=1.0kg, Radius=0.5m - small cross-section, minimal wind effect"
                    }
                }
            },
            // Execution logic
            async (controller, stateManager) =>
            {
                Console.WriteLine("╔═════════════════════════════════════════════════════════════╗");
                Console.WriteLine("║  TEST CASE 5: Wind Effect - Large vs Small Radius           ║");
                Console.WriteLine("║  Large ball: gravity + wind (large cross-section)           ║");
                Console.WriteLine("║  Small ball: gravity + minimal wind (small cross-section)   ║");
                Console.WriteLine("╚═════════════════════════════════════════════════════════════╝\n");

                try
                {
                    // Load configuration to get simulation step count
                    var config = ServiceSetupLoader.LoadConfiguration("DefaultSetup.json");
                    int simulationSteps = config.SimulationSteps;

                    // Run simulation for configured number of steps to observe wind effect differences
                    for (int step = 1; step <= simulationSteps; step++)
                    {
                        await controller.OneStepAsync();
                    }

                    // Stop simulation
                    await controller.StopAsync();

                    Console.WriteLine("✓ Test case completed successfully!\n");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"✗ Test case failed: {ex.Message}");
                    Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                }
            }
        ),
        new(
            "Combined forces: gravity only, gravity+magnetism, gravity+wind, gravity+magnetism+wind",
            // Setup data
            new TestSetup
            {
                ConfigurationFile = "DefaultSetup.json",
                Description = "Four entities with identical size/mass but different force combinations: gravity-only, gravity+magnetism, gravity+wind, gravity+magnetism+wind",
                Entities = new List<EntityDefinition>
                {
                    new()
                    {
                        Name = "Gravity only",
                        Properties = new Dictionary<string, object>
                        {
                            { "Mass", 1.0f },
                            { "Position", new Vector3(-3, 50, 0) },
                            { "Displacement", new Vector3(0, 0, 0) },
                            { "CurrentSpeed", new Vector3(0, 0, 0) },
                            { "Radius", 0.3f },
                            { "GravityForce", new Vector3(0, 0, 0) },
                            { "DragForce", new Vector3(0, 0, 0) },
                            { "Color", Color.Red }
                        },
                        Description = "Non-metallic, no wind (missing MagnetismForce and WindForce properties) - gravity only"
                    },
                    new()
                    {
                        Name = "Gravity + Magnetism",
                        Properties = new Dictionary<string, object>
                        {
                            { "Mass", 1.0f },
                            { "Position", new Vector3(-1, 50, 0) },
                            { "Displacement", new Vector3(0, 0, 0) },
                            { "CurrentSpeed", new Vector3(0, 0, 0) },
                            { "Radius", 0.3f },
                            { "GravityForce", new Vector3(0, 0, 0) },
                            { "DragForce", new Vector3(0, 0, 0) },
                            { "MagnetismForce", new Vector3(0, 0, 0) },
                            { "Color", Color.Gold }
                        },
                        Description = "Metallic (has MagnetismForce property), no wind effect - gravity and magnetic field (upward force counteracts gravity)"
                    },
                    new()
                    {
                        Name = "Gravity + Wind",
                        Properties = new Dictionary<string, object>
                        {
                            { "Mass", 1.0f },
                            { "Position", new Vector3(1, 50, 0) },
                            { "Displacement", new Vector3(0, 0, 0) },
                            { "CurrentSpeed", new Vector3(0, 0, 0) },
                            { "Radius", 0.3f },
                            { "GravityForce", new Vector3(0, 0, 0) },
                            { "DragForce", new Vector3(0, 0, 0) },
                            { "WindForce", new Vector3(0, 0, 0) },
                            { "Color", Color.Cyan }
                        },
                        Description = "Non-metallic (no MagnetismForce property), affected by wind - gravity and wind force (horizontal push)"
                    },
                    new()
                    {
                        Name = "Gravity + Wind + Magnetism",
                        Properties = new Dictionary<string, object>
                        {
                            { "Mass", 1.0f },
                            { "Position", new Vector3(3, 50, 0) },
                            { "Displacement", new Vector3(0, 0, 0) },
                            { "CurrentSpeed", new Vector3(0, 0, 0) },
                            { "Radius", 0.3f },
                            { "GravityForce", new Vector3(0, 0, 0) },
                            { "DragForce", new Vector3(0, 0, 0) },
                            { "MagnetismForce", new Vector3(0, 0, 0) },
                            { "WindForce", new Vector3(0, 0, 0) },
                            { "Color", Color.Lime }
                        },
                        Description = "Metallic (has MagnetismForce and WindForce properties) - gravity+magnetism+wind (all forces active)"
                    }
                }
            },
            // Execution logic
            async (controller, stateManager) =>
            {
                Console.WriteLine("╔═════════════════════════════════════════════════════════════╗");
                Console.WriteLine("║  TEST CASE 6: Combined Forces - Force Composition           ║");
                Console.WriteLine("║  Entity 1: gravity only (downward)                          ║");
                Console.WriteLine("║  Entity 2: gravity + magnetism (upward magnetic force)      ║");
                Console.WriteLine("║  Entity 3: gravity + wind (horizontal wind push)            ║");
                Console.WriteLine("║  Entity 4: gravity + magnetism + wind (all forces)          ║");
                Console.WriteLine("╚═════════════════════════════════════════════════════════════╝\n");

                try
                {
                    // Load configuration to get simulation step count
                    var config = ServiceSetupLoader.LoadConfiguration("DefaultSetup.json");
                    int simulationSteps = config.SimulationSteps;

                    // Run simulation for configured number of steps to observe different force interactions
                    for (int step = 1; step <= simulationSteps; step++)
                    {
                        await controller.OneStepAsync();
                    }

                    // Stop simulation
                    await controller.StopAsync();

                    Console.WriteLine("✓ Test case completed successfully!\n");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"✗ Test case failed: {ex.Message}");
                    Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                }
            }
        )
    };

    public static void ListAll()
    {
        Console.WriteLine("Available test cases:");
        for (int i = 0; i < All.Count; i++)
        {
            Console.WriteLine($"  [{i}] {All[i].Name}");
        }
    }

    public static async Task<StateManager> RunByIndexAsync(int index, Simulation.StateManager.VisualizationMapper? visualizationMapper = null)
    {
        if (index < 0 || index >= All.Count)
            throw new ArgumentOutOfRangeException(nameof(index), "Invalid test case index.");

        var testCase = All[index];
        
        // Initialize components from test setup data
        // Pass visualization mapper so entities are sent to external tools during creation
        var interactionController = await SimulationInitializer.InitializeFromTestSetupAsync(testCase.Setup, visualizationMapper);
        var stateManager = interactionController.GetEntityManager().GetStateManager();
        
        // Run the test execution logic
        await testCase.ExecutionLogic(interactionController, stateManager);

        return stateManager;
    }
}
