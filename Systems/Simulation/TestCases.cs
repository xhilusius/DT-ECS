using System.Numerics;
using System.Drawing;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.IO;
using System.Linq;
using Simulation;
using Simulation.Interfaces;
using Simulation.StateManager;
using Simulation.ServiceManager;

public static class TestCases
{
    public record TestCase(string Name, TestSetup Setup, Func<IInteractionController, StateManager, Task> ExecutionLogic);

    private record TestCaseConfigFile
    {
        [JsonPropertyName("testCases")]
        public required List<TestCaseConfig> TestCases { get; init; }
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
        var configFile = LoadConfigFile();
        var entityLibrary = LoadEntityDefinitions();
        return configFile.TestCases.Select(test => BuildTestCase(test, entityLibrary)).ToList();
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
                var setupConfig = ServiceSetupLoader.LoadConfiguration(setup.ConfigurationFile);
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

    private static TestCaseConfigFile LoadConfigFile()
    {
        string configPath = GetTestCasesPath();

        if (!File.Exists(configPath))
            throw new FileNotFoundException($"Test cases configuration file not found: {configPath}");

        string jsonContent = File.ReadAllText(configPath);
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip
        };

        var configFile = JsonSerializer.Deserialize<TestCaseConfigFile>(jsonContent, options);
        if (configFile == null)
            throw new JsonException("Test cases deserialization resulted in null");

        return configFile;
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
            "Entities.jsonc"
        );

        return Path.GetFullPath(entitiesPath);
    }
}
