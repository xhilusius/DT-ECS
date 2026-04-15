namespace Simulation.ServiceManager.CompositeServices;

using System.Drawing;
using System.Text.Json;
using System.Text.Json.Serialization;
using Simulation.EntityManager;
using Simulation.Interfaces;
using Simulation.ServiceManager.ExternalServices;

/// <summary>
/// Top-level composite service that orchestrates an end-to-end test case execution.
///
/// Owned directly by <see cref="Simulation.InteractionController.InteractionController"/>
/// (not scheduled inside a <see cref="ServiceManager"/>).
///
/// Responsibilities:
/// - Parses the test case file (.jsonc) and the shared entity library.
/// - Constructs and directly drives: SensingService → TestSimulationService → ActuatingService.
/// - Propagates CancellationToken and PauseHandle into every layer.
///
/// The outer <see cref="EntityManager"/> is stored for future sensing/actuating use;
/// for the current test cases, all entity lifecycle is handled inside
/// <see cref="TestSimulationService"/> via the inner store.
/// </summary>
public class TestExecutorService
{
    // -------------------------------------------------------------------------
    // Construction-time dependencies
    // -------------------------------------------------------------------------

    private readonly EntityManager _entityManager;
    private readonly IInnerServiceFactory _innerFactory;

    // -------------------------------------------------------------------------
    // Populated by InitializeAsync
    // -------------------------------------------------------------------------

    private string _tcDisplayName = string.Empty;
    private IReadOnlyList<string> _headerLines = Array.Empty<string>();
    private TestSimulationService? _testSimulationService;

    private readonly SensingService  _sensingService  = new();
    private readonly ActuatingService _actuatingService = new();

    // -------------------------------------------------------------------------
    // Constructor
    // -------------------------------------------------------------------------

    public TestExecutorService(EntityManager entityManager, IInnerServiceFactory innerFactory)
    {
        _entityManager = entityManager ?? throw new ArgumentNullException(nameof(entityManager));
        _innerFactory  = innerFactory  ?? throw new ArgumentNullException(nameof(innerFactory));
    }

    // -------------------------------------------------------------------------
    // Initialization
    // -------------------------------------------------------------------------

    /// <summary>
    /// Parses the test case file at <paramref name="tcFilePath"/> and wires the inner
    /// <see cref="TestSimulationService"/> with the resolved step actions.
    /// </summary>
    /// <param name="tcFilePath">Absolute path to the .jsonc test case file.</param>
    /// <param name="innerSetupOverride">
    ///   Optional override for the inner physics setup name declared in the TC file.
    /// </param>
    public Task InitializeAsync(string tcFilePath, string? innerSetupOverride = null)
    {
        if (!File.Exists(tcFilePath))
            throw new FileNotFoundException($"Test case file not found: {tcFilePath}");

        var entityLibrary = LoadEntityDefinitions();
        var config        = ParseTCFile(tcFilePath);

        _tcDisplayName = config.Name;
        _headerLines   = config.HeaderLines;

        var innerSetup = innerSetupOverride ?? config.InnerSetup;

        var step0Actions = ParseActions(
            config.Steps.FirstOrDefault(s => s.Step == 0)?.Actions ?? new List<ActionConfig>(),
            entityLibrary);

        var midSimActions = config.Steps
            .Where(s => s.Step > 0)
            .ToDictionary(
                s => s.Step,
                s => ParseActions(s.Actions, entityLibrary));

        _testSimulationService = new TestSimulationService(
            innerSetup, step0Actions, midSimActions, _innerFactory);

        return Task.CompletedTask;
    }

    // -------------------------------------------------------------------------
    // Execution
    // -------------------------------------------------------------------------

    /// <summary>
    /// Runs the outer pipeline once: Sensing → TestSimulationService → Actuating.
    /// Checks <paramref name="ct"/> and <paramref name="pauseHandle"/> between each stage.
    /// </summary>
    public async Task RunAsync(CancellationToken ct, PauseHandle pauseHandle)
    {
        if (_testSimulationService == null)
            throw new InvalidOperationException("Call InitializeAsync before RunAsync.");

        PrintBanner(_headerLines);

        try
        {
            await _sensingService.ExecuteAsync();

            await pauseHandle.WaitIfPausedAsync(ct);
            ct.ThrowIfCancellationRequested();

            await _testSimulationService.ExecuteAsync(ct, pauseHandle);

            await pauseHandle.WaitIfPausedAsync(ct);
            ct.ThrowIfCancellationRequested();

            await _actuatingService.ExecuteAsync();

            Console.WriteLine("✓ Test case completed successfully!\n");
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("Simulation stopped by user.\n");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Test case failed: {ex.Message}");
            Console.WriteLine($"Stack Trace: {ex.StackTrace}");
        }
    }

    // =========================================================================
    // Static helpers — test case discovery
    // =========================================================================

    public record TestCaseSummary(string Name, string InnerSetup, string FilePath);

    /// <summary>
    /// Loads the list of available test cases from the TC index without full parsing.
    /// </summary>
    public static IReadOnlyList<TestCaseSummary> LoadAvailableTestCases()
    {
        var indexPath = GetTestCasesIndexPath();
        var options   = JsonOptions();

        var indexFile = JsonSerializer.Deserialize<TestCaseIndexFile>(
            File.ReadAllText(indexPath), options)
            ?? throw new JsonException("Test cases index deserialization resulted in null");

        var folder = Path.GetDirectoryName(indexPath)!;

        return indexFile.TestCases
            .Select(relativePath =>
            {
                var fullPath = Path.GetFullPath(Path.Combine(folder, relativePath));
                var config   = ParseTCFile(fullPath);
                return new TestCaseSummary(config.Name, config.InnerSetup, fullPath);
            })
            .ToList();
    }

    // =========================================================================
    // File loading — private
    // =========================================================================

    private static TestCaseConfig ParseTCFile(string filePath)
    {
        var config = JsonSerializer.Deserialize<TestCaseConfig>(
            File.ReadAllText(filePath), JsonOptions())
            ?? throw new JsonException($"Test case deserialization resulted in null: {filePath}");
        return config;
    }

    private static IReadOnlyDictionary<string, EntityDefinition> LoadEntityDefinitions()
    {
        var path = GetEntityLibraryPath();
        if (!File.Exists(path))
            throw new FileNotFoundException($"Entity library not found: {path}");

        var libraryFile = JsonSerializer.Deserialize<EntityLibraryFile>(
            File.ReadAllText(path), JsonOptions())
            ?? throw new JsonException("Entity library deserialization resulted in null");

        var result = new Dictionary<string, EntityDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, entityConfig) in libraryFile.Entities)
        {
            result[key] = new EntityDefinition
            {
                Name        = entityConfig.Name,
                Description = entityConfig.Description,
                Properties  = BuildProperties(entityConfig.Properties),
            };
        }
        return result;
    }

    private static List<TestActionDefinition> ParseActions(
        IEnumerable<ActionConfig> actions,
        IReadOnlyDictionary<string, EntityDefinition> entityLibrary)
    {
        return actions.Select(a => BuildActionDefinition(a, entityLibrary)).ToList();
    }

    private static TestActionDefinition BuildActionDefinition(
        ActionConfig actionConfig,
        IReadOnlyDictionary<string, EntityDefinition> entityLibrary)
    {
        if (!entityLibrary.TryGetValue(actionConfig.Entity, out var entityDef))
            throw new JsonException($"Unknown entity reference: '{actionConfig.Entity}'");

        return new TestActionDefinition
        {
            Type            = actionConfig.Type,
            Entity          = entityDef,
            PropertyOverrides = actionConfig.Properties != null
                ? BuildProperties(actionConfig.Properties)
                : null,
        };
    }

    private static Dictionary<string, object> BuildProperties(
        Dictionary<string, JsonElement> properties)
    {
        var result = new Dictionary<string, object>();
        foreach (var (name, value) in properties)
        {
            var parsed = ParsePropertyValue(name, value);
            if (parsed != null) result[name] = parsed;
        }
        return result;
    }

    private static object? ParsePropertyValue(string propertyName, JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.Number => (float)value.GetDouble(),
            JsonValueKind.String => ParseStringValue(propertyName, value.GetString()),
            JsonValueKind.True   => true,
            JsonValueKind.False  => false,
            JsonValueKind.Array  => ParseVector3Array(value),
            JsonValueKind.Object => ParseVector3Object(value),
            JsonValueKind.Null   => null,
            _ => throw new JsonException(
                $"Unsupported property value kind for '{propertyName}': {value.ValueKind}"),
        };
    }

    private static object ParseStringValue(string propertyName, string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
            throw new JsonException($"Property '{propertyName}' cannot be empty");

        if (propertyName.Equals("Color", StringComparison.OrdinalIgnoreCase))
            return ColorTranslator.FromHtml(rawValue.StartsWith("#") ? rawValue
                : $"#{Color.FromName(rawValue).R:X2}{Color.FromName(rawValue).G:X2}{Color.FromName(rawValue).B:X2}");

        return rawValue;
    }

    private static double[] ParseVector3Array(JsonElement value)
    {
        var elements = value.EnumerateArray().ToList();
        if (elements.Count != 3)
            throw new JsonException("Vector3 arrays must have exactly 3 elements");
        return new[] { elements[0].GetDouble(), elements[1].GetDouble(), elements[2].GetDouble() };
    }

    private static double[] ParseVector3Object(JsonElement value)
    {
        double Get(string prop)
        {
            if (!value.TryGetProperty(prop, out var el) &&
                !value.TryGetProperty(prop.ToUpperInvariant(), out el))
                throw new JsonException($"Vector3 object missing '{prop}' field");
            return el.GetDouble();
        }
        return new[] { Get("x"), Get("y"), Get("z") };
    }

    // =========================================================================
    // Paths
    // =========================================================================

    private static string GetTestCasesIndexPath()
    {
        var assemblyFolder = Path.GetDirectoryName(
            System.Reflection.Assembly.GetExecutingAssembly().Location)!;
        return Path.GetFullPath(Path.Combine(
            assemblyFolder, "..", "..", "..", "TestFiles", "TestCases.jsonc"));
    }

    private static string GetEntityLibraryPath()
    {
        var assemblyFolder = Path.GetDirectoryName(
            System.Reflection.Assembly.GetExecutingAssembly().Location)!;
        return Path.GetFullPath(Path.Combine(
            assemblyFolder, "..", "..", "..", "TestFiles", "EntityTemplates.jsonc"));
    }

    private static JsonSerializerOptions JsonOptions() => new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas         = true,
        ReadCommentHandling         = JsonCommentHandling.Skip,
    };

    // =========================================================================
    // Banner
    // =========================================================================

    private static void PrintBanner(IReadOnlyList<string> lines)
    {
        if (lines.Count == 0) return;
        int width = lines.Max(l => l.Length);
        Console.WriteLine($"╔{new string('═', width + 2)}╗");
        foreach (var line in lines)
            Console.WriteLine($"║ {line.PadRight(width)} ║");
        Console.WriteLine($"╚{new string('═', width + 2)}╝\n");
    }

    // =========================================================================
    // Private JSON record types
    // =========================================================================

    private record TestCaseIndexFile
    {
        [JsonPropertyName("testCases")]
        public required List<string> TestCases { get; init; }
    }

    private record TestCaseConfig
    {
        [JsonPropertyName("name")]
        public required string Name { get; init; }

        [JsonPropertyName("outerSetup")]
        public required string OuterSetup { get; init; }

        [JsonPropertyName("innerSetup")]
        public required string InnerSetup { get; init; }

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
}
