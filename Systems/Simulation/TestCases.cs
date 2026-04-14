using System.Numerics;
using System.Drawing;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.IO;
using System.Linq;
using Simulation;
using Simulation.Interfaces;
using Simulation.PropertyTypes;
using Simulation.StateManager;
using Simulation.ServiceManager;
using Simulation.ServiceManager.CompositeServices;

public static class TestCases
{
    public record TestCase(string Name, TestSetup Setup, Func<IInteractionController, StateManager, Task> ExecutionLogic);

    private record TestCaseIndexFile
    {
        [JsonPropertyName("testCases")]
        public required List<string> TestCases { get; init; }
    }

    private record TestCaseConfig
    {
        [JsonPropertyName("name")]
        public required string Name { get; init; }

        [JsonPropertyName("configurationFile")]
        public required string ConfigurationFile { get; init; }

        [JsonPropertyName("description")]
        public required string Description { get; init; }

        [JsonPropertyName("headerLines")]
        public required List<string> HeaderLines { get; init; }

        [JsonPropertyName("steps")]
        public required List<StepConfig> Steps { get; init; }
    }

    private record StepConfig
    {
        [JsonPropertyName("step")]
        public required int Step { get; init; }

        [JsonPropertyName("actions")]
        public required List<ActionConfig> Actions { get; init; }
    }

    private record ActionConfig
    {
        [JsonPropertyName("type")]
        public required string Type { get; init; }

        [JsonPropertyName("entity")]
        public required string Entity { get; init; }

        [JsonPropertyName("properties")]
        public Dictionary<string, JsonElement>? Properties { get; init; }
    }

    private record EntityLibraryFile
    {
        [JsonPropertyName("entities")]
        public required Dictionary<string, EntityConfig> Entities { get; init; }
    }

    private record EntityConfig
    {
        [JsonPropertyName("name")]
        public required string Name { get; init; }

        [JsonPropertyName("description")]
        public string? Description { get; init; }

        [JsonPropertyName("properties")]
        public required Dictionary<string, JsonElement> Properties { get; init; }
    }

    private static readonly Lazy<IReadOnlyList<TestCase>> _all = new(() => LoadAll());

    public static IReadOnlyList<TestCase> All => _all.Value;

    public static void ListAll()
    {
        Console.WriteLine("Available test cases:");
        for (int i = 0; i < All.Count; i++)
        {
            Console.WriteLine($"  [{i + 1}] {All[i].Name}");
        }
    }

    public static async Task<StateManager> RunByIndexAsync(int index, Simulation.StateManager.VisualizationMapper? visualizationMapper = null, string? configurationFileOverride = null)
    {
        if (index < 0 || index >= All.Count)
            throw new ArgumentOutOfRangeException(nameof(index), "Invalid test case index.");

        var testCase = All[index];

        // Initialize components from test setup data
        // Pass visualization mapper so entities are sent to external tools during creation
        var interactionController = await SimulationInitializer.InitializeFromTestSetupAsync(testCase.Setup, visualizationMapper, configurationFileOverride);
        var stateManager = interactionController.GetEntityManager().GetStateManager();

        // Run the test execution logic
        await testCase.ExecutionLogic(interactionController, stateManager);

        return stateManager;
    }

    private static IReadOnlyList<TestCase> LoadAll()
    {
        var indexPath = GetTestCasesPath();
        var index = LoadIndexFile(indexPath);
        var entityLibrary = LoadEntityDefinitions();
        var testFilesFolder = Path.GetDirectoryName(indexPath)!;
        var jsonCases = index.TestCases
            .Select(relativePath => LoadSingleTestCaseFile(
                Path.GetFullPath(Path.Combine(testFilesFolder, relativePath)),
                entityLibrary))
            .ToList();

        return jsonCases.Append(BuildTC18(entityLibrary)).ToList();
    }

    private static TestCase LoadSingleTestCaseFile(string filePath, IReadOnlyDictionary<string, EntityDefinition> entityLibrary)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Test case file not found: {filePath}");

        string jsonContent = File.ReadAllText(filePath);
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip
        };

        var config = JsonSerializer.Deserialize<TestCaseConfig>(jsonContent, options);
        if (config == null)
            throw new JsonException($"Test case deserialization resulted in null for: {filePath}");

        return BuildTestCase(config, entityLibrary);
    }

    private static TestCase BuildTestCase(TestCaseConfig config, IReadOnlyDictionary<string, EntityDefinition> entityLibrary)
    {
        var setup = new TestSetup
        {
            ConfigurationFile = config.ConfigurationFile,
            Description = config.Description,
            Steps = config.Steps.Select(step => BuildStepDefinition(step, entityLibrary)).ToList()
        };

        return new TestCase(config.Name, setup, CreateExecutionLogic(setup, config.HeaderLines));
    }

    private static TestStepDefinition BuildStepDefinition(StepConfig stepConfig, IReadOnlyDictionary<string, EntityDefinition> entityLibrary)
    {
        return new TestStepDefinition
        {
            Step = stepConfig.Step,
            Actions = stepConfig.Actions.Select(action => BuildActionDefinition(action, entityLibrary)).ToList()
        };
    }

    private static TestActionDefinition BuildActionDefinition(ActionConfig actionConfig, IReadOnlyDictionary<string, EntityDefinition> entityLibrary)
    {
        if (!entityLibrary.TryGetValue(actionConfig.Entity, out var entityDefinition))
            throw new JsonException($"Unknown entity reference: {actionConfig.Entity}");

        var propertyOverrides = actionConfig.Properties != null ? BuildProperties(actionConfig.Properties) : null;

        return new TestActionDefinition
        {
            Type = actionConfig.Type,
            Entity = entityDefinition,
            PropertyOverrides = propertyOverrides
        };
    }

    private static Dictionary<string, object> BuildProperties(Dictionary<string, JsonElement> properties)
    {
        var result = new Dictionary<string, object>();
        foreach (var (propertyName, value) in properties)
        {
            var parsed = ParsePropertyValue(propertyName, value);
            if (parsed != null)
            {
                result[propertyName] = parsed;
            }
        }

        return result;
    }

    private static IReadOnlyDictionary<string, EntityDefinition> LoadEntityDefinitions()
    {
        string entitiesPath = GetEntitiesPath();

        if (!File.Exists(entitiesPath))
            throw new FileNotFoundException($"Entity library file not found: {entitiesPath}");

        string jsonContent = File.ReadAllText(entitiesPath);
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip
        };

        var libraryFile = JsonSerializer.Deserialize<EntityLibraryFile>(jsonContent, options);
        if (libraryFile == null)
            throw new JsonException("Entity library deserialization resulted in null");

        var result = new Dictionary<string, EntityDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, entityConfig) in libraryFile.Entities)
        {
            result[key] = new EntityDefinition
            {
                Name = entityConfig.Name,
                Description = entityConfig.Description,
                Properties = BuildProperties(entityConfig.Properties)
            };
        }

        return result;
    }

    private static object? ParsePropertyValue(string propertyName, JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Number:
                return (float)value.GetDouble();
            case JsonValueKind.String:
                return ParseStringValue(propertyName, value.GetString());
            case JsonValueKind.True:
            case JsonValueKind.False:
                return value.GetBoolean();
            case JsonValueKind.Array:
                return ParseVector3Array(value);
            case JsonValueKind.Object:
                return ParseVector3Object(value);
            case JsonValueKind.Null:
                return null;
            default:
                throw new JsonException($"Unsupported property value kind for {propertyName}: {value.ValueKind}");
        }
    }

    private static object ParseStringValue(string propertyName, string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
            throw new JsonException($"Property {propertyName} cannot be empty");

        if (propertyName.Equals("Color", StringComparison.OrdinalIgnoreCase))
            return ParseColor(rawValue);

        return rawValue;
    }

    private static double[] ParseVector3Array(JsonElement value)
    {
        var elements = value.EnumerateArray().ToList();
        if (elements.Count != 3)
            throw new JsonException("Vector3 arrays must have exactly 3 elements");

        return new double[]
        {
            elements[0].GetDouble(),
            elements[1].GetDouble(),
            elements[2].GetDouble()
        };
    }

    private static double[] ParseVector3Object(JsonElement value)
    {
        double x = GetRequiredNumber(value, "x");
        double y = GetRequiredNumber(value, "y");
        double z = GetRequiredNumber(value, "z");
        return new double[] { x, y, z };
    }

    private static double GetRequiredNumber(JsonElement value, string property)
    {
        if (!value.TryGetProperty(property, out var element) && !value.TryGetProperty(property.ToUpperInvariant(), out element))
            throw new JsonException($"Vector3 object missing '{property}' field");

        return element.GetDouble();
    }

    private static Color ParseColor(string rawValue)
    {
        if (rawValue.StartsWith("#", StringComparison.Ordinal))
            return ColorTranslator.FromHtml(rawValue);

        var color = Color.FromName(rawValue);
        if (!color.IsKnownColor && !color.IsNamedColor && !color.IsSystemColor)
            throw new JsonException($"Unknown color name: {rawValue}");

        return color;
    }

    private static Func<IInteractionController, StateManager, Task> CreateExecutionLogic(TestSetup setup, IReadOnlyList<string> headerLines)
    {
        return async (controller, stateManager) =>
        {
            PrintBanner(headerLines);

            try
            {
                var setupConfig = CompositeServiceSetupLoader.LoadConfiguration(setup.ConfigurationFile);
                int simulationSteps = setupConfig.SimulationSteps;

                var stepsByIndex = setup.Steps.ToDictionary(step => step.Step, step => step.Actions);

                if (stepsByIndex.TryGetValue(0, out var initialActions))
                {
                    await ExecuteStepActionsAsync(controller, initialActions);
                    await stateManager.ReportStateAsync("Initial State");
                }

                for (int step = 1; step <= simulationSteps; step++)
                {
                    if (stepsByIndex.TryGetValue(step, out var stepActions))
                    {
                        await ExecuteStepActionsAsync(controller, stepActions);
                    }

                    await controller.OneStepAsync();
                }

                await controller.StopAsync();

                Console.WriteLine("✓ Test case completed successfully!\n");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Test case failed: {ex.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
            }
        };
    }

    private static async Task ExecuteStepActionsAsync(IInteractionController controller, IReadOnlyList<TestActionDefinition> actions)
    {
        foreach (var action in actions)
        {
            if (string.Equals(action.Type, "spawn", StringComparison.OrdinalIgnoreCase))
            {
                var entityDefinition = action.Entity;
                var properties = new Dictionary<string, object>(entityDefinition.Properties);

                // Apply property overrides from the action
                if (action.PropertyOverrides != null)
                {
                    foreach (var (key, value) in action.PropertyOverrides)
                    {
                        properties[key] = value;
                    }
                }

                await controller.CreateEntityAsync(entityDefinition.Name, properties, entityDefinition.Description);
            }
            else if (string.Equals(action.Type, "remove", StringComparison.OrdinalIgnoreCase))
            {
                // Remove action expects the entity ID to be specified via property override
                if (action.PropertyOverrides != null && action.PropertyOverrides.TryGetValue("EntityId", out var entityIdObj))
                {
                    if (int.TryParse(entityIdObj.ToString(), out int entityId))
                    {
                        await controller.RemoveEntityAsync(entityId);
                    }
                    else
                    {
                        Console.WriteLine($"Warning: Invalid EntityId in remove action - skipping.");
                    }
                }
                else
                {
                    Console.WriteLine($"Warning: Remove action missing EntityId property - skipping.");
                }
            }
            else
            {
                Console.WriteLine($"Warning: Unsupported action type '{action.Type}' - skipping.");
            }
        }
    }

    private static void PrintBanner(IReadOnlyList<string> lines)
    {
        if (lines.Count == 0)
            return;

        int width = lines.Max(line => line.Length);
        string top = $"╔{new string('═', width + 2)}╗";
        string bottom = $"╚{new string('═', width + 2)}╝";

        Console.WriteLine(top);
        foreach (var line in lines)
        {
            Console.WriteLine($"║ {line.PadRight(width)} ║");
        }
        Console.WriteLine(bottom + "\n");
    }

    private static TestCaseIndexFile LoadIndexFile(string indexPath)
    {
        if (!File.Exists(indexPath))
            throw new FileNotFoundException($"Test cases index file not found: {indexPath}");

        string jsonContent = File.ReadAllText(indexPath);
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip
        };

        var indexFile = JsonSerializer.Deserialize<TestCaseIndexFile>(jsonContent, options);
        if (indexFile == null)
            throw new JsonException("Test cases index deserialization resulted in null");

        return indexFile;
    }

    private static string GetTestCasesPath()
    {
        var assemblyPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
        var assemblyFolder = Path.GetDirectoryName(assemblyPath);

        if (string.IsNullOrEmpty(assemblyFolder))
            throw new InvalidOperationException("Could not determine assembly folder");

        string configPath = Path.Combine(
            assemblyFolder,
            "..", "..", "..",
            "TestFiles",
            "TestCases.jsonc"
        );

        return Path.GetFullPath(configPath);
    }

    private static string GetEntitiesPath()
    {
        var assemblyPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
        var assemblyFolder = Path.GetDirectoryName(assemblyPath);

        if (string.IsNullOrEmpty(assemblyFolder))
            throw new InvalidOperationException("Could not determine assembly folder");

        string entitiesPath = Path.Combine(
            assemblyFolder,
            "..", "..", "..",
            "TestFiles",
            "EntityTemplates.jsonc"
        );

        return Path.GetFullPath(entitiesPath);
    }

    // -------------------------------------------------------------------------
    // TC18 — What-If: Collision analysis, 100-satellite constellation
    // -------------------------------------------------------------------------
    // Driven programmatically rather than from JSON because ScenarioConfig contains
    // typed C# objects (BaseEntitySnapshot lists) that cannot be expressed in the
    // simple JSON test-case format.
    // -------------------------------------------------------------------------

    private static TestCase BuildTC18(IReadOnlyDictionary<string, EntityDefinition> entityLibrary)
    {
        const double R = 6_771_000; // LEO at 400 km altitude (m)
        const double V = 7_672;     // Circular orbital speed (m/s)
        const double DetRadius = 10_000; // 10 km detection zone — matches TC17

        var setup = new TestSetup
        {
            ConfigurationFile = "SatelliteSetup",
            Description = "What-If collision analysis: 100-satellite LEO constellation vs N candidate insertion orbits",
            Steps = new List<TestStepDefinition>()
        };

        return new TestCase(
            "TC18: What-If — 100-Sat Constellation, N Candidate Orbits",
            setup,
            async (controller, stateManager) =>
            {
                PrintBanner(new[]
                {
                    "TEST CASE 18: What-If — Satellite Constellation Collision Analysis",
                    "Constellation: Earth + 100 LEO satellites (400 km, evenly spaced, CCW)",
                    "Candidate:     1 CW satellite inserted at 5 different start positions",
                    "Detection:     Radius=10,000 m each (20 km combined zone)",
                    "Inner sim:     SatelliteSetup — 1,500 steps at 1 s/step (silent)",
                    "Outer sim:     WhatIfService driven directly — no outer loop",
                });

                try
                {
                    // ----------------------------------------------------------
                    // Build base-entity snapshots from the entity library.
                    // The outer store is NOT used — snapshots are built directly
                    // from known spawn data so no outer store reading is needed.
                    // ----------------------------------------------------------

                    var earthDef = entityLibrary["Earth_ball"];
                    var satDef   = entityLibrary["Satellite"];

                    var baseEntities = new List<BaseEntitySnapshot>();

                    // Earth
                    baseEntities.Add(new BaseEntitySnapshot(
                        earthDef.Name,
                        earthDef.Description,
                        new Dictionary<string, object>(earthDef.Properties)));

                    // 100 satellites evenly spread around the orbit, travelling CCW
                    for (int i = 0; i < 100; i++)
                    {
                        double angle = i * 2 * Math.PI / 100;
                        double px = R * Math.Cos(angle);
                        double py = R * Math.Sin(angle);
                        double vx = -V * Math.Sin(angle);
                        double vy =  V * Math.Cos(angle);

                        var props = new Dictionary<string, object>(satDef.Properties)
                        {
                            ["Position"]         = new double[] { px, py, 0 },
                            ["CurrentSpeed"]     = new double[] { vx, vy, 0 },
                            ["Radius"]           = (float)DetRadius,
                            ["CollisionDetected"] = false,
                            ["Color"]            = Color.Cyan,
                        };
                        baseEntities.Add(new BaseEntitySnapshot($"Sat_{i}", null, props));
                    }

                    // ----------------------------------------------------------
                    // Define N candidate scenarios — each a CW satellite starting
                    // at a different orbital angle, varying speed in two cases.
                    // ----------------------------------------------------------

                    var scenarios = new[]
                    {
                        (Label: "CW at   0° (head-on with Sat_0)",
                         Pos: new double[] { R, 0, 0 },
                         Spd: new double[] { 0, -V, 0 }),

                        (Label: "CW at  90° (head-on with Sat_25)",
                         Pos: new double[] { 0, R, 0 },
                         Spd: new double[] { V, 0, 0 }),

                        (Label: "CW at 180° (mirrors TC17)",
                         Pos: new double[] { -R, 0, 0 },
                         Spd: new double[] { 0, V, 0 }),

                        (Label: "CW at 270° (head-on with Sat_75)",
                         Pos: new double[] { 0, -R, 0 },
                         Spd: new double[] { -V, 0, 0 }),

                        (Label: "CW at   0°, 5% faster (v=8056 m/s)",
                         Pos: new double[] { R, 0, 0 },
                         Spd: new double[] { 0, -V * 1.05, 0 }),
                    };

                    // ----------------------------------------------------------
                    // Build entity templates dict for WhatIfService.
                    // Converts EntityDefinition → Dictionary<string, object>.
                    // ----------------------------------------------------------

                    var templates = entityLibrary.ToDictionary(
                        kvp => kvp.Key,
                        kvp => new Dictionary<string, object>(kvp.Value.Properties));

                    // ----------------------------------------------------------
                    // Create and populate WhatIfService directly.
                    // The outer ServiceManager is not involved in what-if dispatch
                    // yet — WhatIfService is driven from the test lambda.
                    // ----------------------------------------------------------

                    var factory = controller.GetInnerServiceFactory();
                    var whatIfService = new WhatIfService(factory, templates);
                    await whatIfService.InitializeAsync("SatelliteSetup");

                    for (int i = 0; i < scenarios.Length; i++)
                    {
                        var s = scenarios[i];
                        var overrides = new Dictionary<string, object>
                        {
                            ["Position"]          = s.Pos,
                            ["CurrentSpeed"]      = s.Spd,
                            ["Radius"]            = (float)DetRadius,
                            ["CollisionDetected"] = false,
                            ["Color"]             = Color.Red,
                        };
                        var spawn  = new ScenarioEntitySpawn("Satellite", overrides);
                        var config = new ScenarioConfig("SatelliteSetup", baseEntities, new[] { spawn });
                        whatIfService.Scenarios[i + 1] = config;
                    }

                    Console.WriteLine($"Running {scenarios.Length} what-if scenarios (inner sims are silent)...\n");
                    await whatIfService.ExecuteAsync();

                    // ----------------------------------------------------------
                    // Print results
                    // ----------------------------------------------------------

                    Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
                    Console.WriteLine("║ WHAT-IF RESULTS                                              ║");
                    Console.WriteLine("╠══════════════════════════════════════════════════════════════╣");
                    for (int i = 0; i < scenarios.Length; i++)
                    {
                        var label  = scenarios[i].Label;
                        var result = whatIfService.Results.TryGetValue(i + 1, out var r) ? r : null;
                        var status = result?.GetPrintable() ?? "(no result)";
                        Console.WriteLine($"║ [{i + 1}] {label}");
                        Console.WriteLine($"║     → {status}");
                    }
                    Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");

                    Console.WriteLine("\n✓ Test case completed successfully!\n");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"✗ Test case failed: {ex.Message}");
                    Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                }
            });
    }
}
