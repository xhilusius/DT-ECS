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

        [JsonPropertyName("entities")]
        public required List<EntityConfig> Entities { get; init; }
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
            Console.WriteLine($"  [{i}] {All[i].Name}");
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
        return configFile.TestCases.Select(BuildTestCase).ToList();
    }

    private static TestCase BuildTestCase(TestCaseConfig config)
    {
        var setup = new TestSetup
        {
            ConfigurationFile = config.ConfigurationFile,
            Description = config.Description,
            Entities = config.Entities.Select(BuildEntityDefinition).ToList()
        };

        return new TestCase(config.Name, setup, CreateExecutionLogic(config));
    }

    private static EntityDefinition BuildEntityDefinition(EntityConfig config)
    {
        return new EntityDefinition
        {
            Name = config.Name,
            Description = config.Description,
            Properties = BuildProperties(config.Properties)
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

    private static Func<IInteractionController, StateManager, Task> CreateExecutionLogic(TestCaseConfig config)
    {
        return async (controller, stateManager) =>
        {
            PrintBanner(config.HeaderLines);

            try
            {
                var setupConfig = ServiceSetupLoader.LoadConfiguration(config.ConfigurationFile);
                int simulationSteps = setupConfig.SimulationSteps;

                for (int step = 1; step <= simulationSteps; step++)
                {
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
            "TestCases.json"
        );

        return Path.GetFullPath(configPath);
    }
}
