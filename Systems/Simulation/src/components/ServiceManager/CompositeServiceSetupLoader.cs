namespace Simulation.ServiceManager;

using System.Text.Json;
using System.Text.Json.Serialization;

// NOTE: This file defines the configuration classes and loader for composite service setups.
// It functions as the central place to define how simulation services are configured, their input/output properties,
// and the execution order of services in a composite service run.
// It includes:
// - ServiceConfig: Defines the type and input/output properties for a single service entry.
// - TimeStepConfig: Defines the time step value and unit for the simulation.
// - PropertyVisibility: Defines which properties to show in state reports (always, once, intermediate).
// - PropertiesConfiguration: Contains property units and visibility settings, loaded from a JSON file.
// - CompositeServiceSetup: The complete setup configuration, loaded from a Setup.json file.

/// <summary>
/// Represents a single service entry in a composite service setup.
/// The <c>type</c> field controls which service interface is instantiated:
/// - "transform"  (default): pure ITransformService — property-array-in / property-array-out
/// - "composite":             ICompositeService — owns an inner ServiceManager stack, identified by setupName
/// - "external":              IExternalService  — crosses a system boundary (sensing / actuating)
/// InputProperties and OutputProperties declare what this entry reads/writes from the outer property store,
/// allowing the outer ServiceManager to schedule it correctly within execution batches.
/// </summary>
public class ServiceConfig
{
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    /// <summary>
    /// Service type. Defaults to "transform" if omitted, preserving backward compatibility
    /// with existing Setup.json files that do not specify a type.
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = "transform";

    /// <summary>
    /// Name of the inner setup folder in TestFiles/CompositeSetups/.
    /// Required when type is "composite"; ignored for other types.
    /// </summary>
    [JsonPropertyName("setupName")]
    public string? SetupName { get; set; }

    [JsonPropertyName("inputProperties")]
    public List<string>? InputProperties { get; set; }

    [JsonPropertyName("optionalInputProperties")]
    public List<string>? OptionalInputProperties { get; set; }

    [JsonPropertyName("outputProperties")]
    public List<string>? OutputProperties { get; set; }
}

/// <summary>
/// Time step configuration for the simulation setup.
/// </summary>
public class TimeStepConfig
{
    [JsonPropertyName("value")]
    public required double Value { get; set; }

    [JsonPropertyName("unit")]
    public required string Unit { get; set; }
}

/// <summary>
/// Property visibility configuration defining which properties to display in state reports.
/// Supports three categories:
/// - alwaysShow: Properties displayed in every state report (dynamic properties like Position, CurrentSpeed, forces)
/// - showOnce: Properties displayed only in the first state report (static properties like Mass, Radius)
/// - intermediate: Properties never displayed (internal computation properties like Displacement)
/// </summary>
public class PropertyVisibility
{
    [JsonPropertyName("alwaysShow")]
    public List<string> AlwaysShow { get; set; } = new();

    [JsonPropertyName("showOnce")]
    public List<string> ShowOnce { get; set; } = new();

    [JsonPropertyName("intermediate")]
    public List<string> Intermediate { get; set; } = new();
}

/// <summary>
/// Properties configuration containing units and visibility settings for all property types.
/// Loaded from PropertiesConfig.json to centralize property metadata.
/// </summary>
public class PropertiesConfiguration
{
    [JsonPropertyName("propertyUnits")]
    public Dictionary<string, string>? PropertyUnits { get; set; }

    [JsonPropertyName("propertyVisibility")]
    public PropertyVisibility? PropertyVisibility { get; set; }
}

/// <summary>
/// Represents the complete setup configuration for a simulation.
/// Loaded from JSON files in the ServiceSetups folder.
/// Execution is organized into batches where all models in a batch can execute in parallel.
/// </summary>
public class CompositeServiceSetup
{
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("simulationModels")]
    public required List<ServiceConfig> Services { get; set; }

    /// <summary>
    /// Time step applied to all models in this setup.
    /// Supports seconds or milliseconds (e.g., 1 ms or 0.001 s).
    /// </summary>
    [JsonPropertyName("timeStep")]
    public required TimeStepConfig TimeStep { get; set; }

    /// <summary>
    /// Execution batches: each inner list is a batch where all models can run in parallel.
    /// Models execute in batch order (batch 0, then batch 1, etc.).
    /// All models in a batch must complete before the next batch begins.
    /// 
    /// Example:
    /// [
    ///   ["GravityModel"],           // Batch 0: runs first
    ///   ["PositionModel"]           // Batch 1: runs after batch 0 completes
    /// ]
    /// 
    /// Or with parallel execution:
    /// [
    ///   ["ModelA", "ModelB"],       // Batch 0: ModelA and ModelB run in parallel
    ///   ["ModelC"]                  // Batch 1: ModelC runs after batch 0 completes
    /// ]
    /// </summary>
    [JsonPropertyName("executionBatches")]
    public required List<List<string>> ExecutionBatches { get; set; }

    /// <summary>
    /// Controls whether services within a batch execute in parallel.
    /// When true: services in the same batch run concurrently using Task.WhenAll
    /// When false: services in the same batch run sequentially
    /// Default: false (for safety and easier debugging)
    /// 
    /// Services can only be parallelized if they have no cross-dependencies.
    /// The executor will enforce this constraint.
    /// </summary>
    [JsonPropertyName("parallel")]
    public bool Parallel { get; set; } = false;

    /// <summary>
    /// Step delay in milliseconds to pace simulation execution for visualization.
    /// When > 0: Each simulation step waits this long before executing the next step.
    /// When 0: Steps execute as fast as possible (no artificial delay).
    /// Default: 0 (maximum speed)
    /// 
    /// USAGE: Set to 1000 to make each step take ~1 second, pacing visualization
    /// to run in real-time even when simulation runs much faster than 1s/step.
    /// 
    /// TIMING: The delay is applied AFTER the step completes.
    /// If the step takes longer than the delay, the next step starts immediately.
    /// </summary>
    [JsonPropertyName("stepDelayMs")]
    public int StepDelayMs { get; set; } = 0;

    /// <summary>
    /// Number of simulation steps to execute in test cases.
    /// This controls how many iterations the simulation runs before stopping.
    /// Default: 10
    /// 
    /// USAGE: Test cases can read this value to determine how many steps to execute,
    /// allowing configuration-driven test duration without hardcoding step counts.
    /// </summary>
    [JsonPropertyName("simulationSteps")]
    public int SimulationSteps { get; set; } = 10;
}

/// <summary>
/// Loads and parses Service Setup configuration files.
/// Supports reading JSON configuration files from the TestFiles/CompositeSetups folder organized by setup name.
/// Each setup has its own folder (e.g., TestFiles/CompositeSetups/DefaultSetup/, TestFiles/CompositeSetups/OrbitalSetup/) containing:
/// - Setup.json (the setup configuration)
/// - PropertiesConfig.json (property units and display settings)
/// </summary>
public class CompositeServiceSetupLoader
{
    private const string EntityPropertiesConfigFileName = "PropertiesConfig.json";

    /// <summary>
    /// Loads a service setup configuration from a Setup.json file in a named setup folder.
    /// The file is expected to be in TestFiles/CompositeSetups/{SetupName}/Setup.json folder.
    /// </summary>
    /// <param name="setupName">Name of the setup folder (e.g., "DefaultSetup", "OrbitalSetup")</param>
    /// <returns>Parsed CompositeServiceSetup</returns>
    /// <exception cref="FileNotFoundException">If the configuration file is not found</exception>
    /// <exception cref="JsonException">If the JSON is invalid</exception>
    public static CompositeServiceSetup LoadConfiguration(string setupName)
    {
        if (string.IsNullOrWhiteSpace(setupName))
            throw new ArgumentException("Setup name cannot be null or empty", nameof(setupName));

        // Determine the path to the configuration file
        // The file is expected to be in TestFiles/{SetupName}/Setup.json folder relative to this assembly
        var assemblyPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
        var assemblyFolder = System.IO.Path.GetDirectoryName(assemblyPath);
        
        if (string.IsNullOrEmpty(assemblyFolder))
            throw new InvalidOperationException("Could not determine assembly folder");

        // Navigate from the assembly location up to the project root and down to TestFiles/CompositeSetups/{SetupName}/
        // Assembly is typically at: bin/Debug/net9.0/Simulation.dll
        // We need to get to: TestFiles/CompositeSetups/{SetupName}/Setup.json
        string configPath = System.IO.Path.Combine(
            assemblyFolder, 
            "..", "..", "..", 
            "TestFiles", "CompositeSetups", setupName,
            "Setup.json"
        );

        // Normalize the path
        configPath = System.IO.Path.GetFullPath(configPath);

        if (!System.IO.File.Exists(configPath))
            throw new FileNotFoundException($"Setup configuration file not found: {configPath}");

        try
        {
            string jsonContent = System.IO.File.ReadAllText(configPath);
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                AllowTrailingCommas = true,
                ReadCommentHandling = JsonCommentHandling.Skip
            };

            var configuration = JsonSerializer.Deserialize<CompositeServiceSetup>(jsonContent, options);
            
            if (configuration == null)
                throw new JsonException("Configuration deserialization resulted in null");

            ValidateConfiguration(configuration);
            return configuration;
        }
        catch (JsonException ex)
        {
            throw new JsonException($"Error parsing setup configuration for {setupName}: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Validates the loaded configuration for consistency.
    /// </summary>
    private static void ValidateConfiguration(CompositeServiceSetup config)
    {
        if (string.IsNullOrWhiteSpace(config.Name))
            throw new InvalidOperationException("Configuration must have a name");

        if (config.Services == null || config.Services.Count == 0)
            throw new InvalidOperationException("Configuration must define at least one simulation model");

        if (config.TimeStep == null)
            throw new InvalidOperationException("Configuration must define a timeStep");

        if (config.TimeStep.Value <= 0)
            throw new InvalidOperationException("timeStep value must be greater than zero");

        if (string.IsNullOrWhiteSpace(config.TimeStep.Unit))
            throw new InvalidOperationException("timeStep unit cannot be null or empty");

        // Validate time step unit
        _ = GetTimeStepSeconds(config.TimeStep);

        if (config.ExecutionBatches == null || config.ExecutionBatches.Count == 0)
            throw new InvalidOperationException("Configuration must define at least one execution batch");

        // Verify all execution batch entries refer to defined models
        var modelNames = new HashSet<string>(config.Services.Select(m => m.Name));
        var allBatchModels = new HashSet<string>();

        for (int batchIndex = 0; batchIndex < config.ExecutionBatches.Count; batchIndex++)
        {
            var batch = config.ExecutionBatches[batchIndex];

            if (batch == null || batch.Count == 0)
                throw new InvalidOperationException($"Execution batch {batchIndex} cannot be empty");

            foreach (var modelName in batch)
            {
                if (!modelNames.Contains(modelName))
                    throw new InvalidOperationException($"Execution batch {batchIndex} references undefined model: {modelName}");

                if (allBatchModels.Contains(modelName))
                    throw new InvalidOperationException($"Model {modelName} appears in multiple execution batches");

                allBatchModels.Add(modelName);
            }
        }

        // Verify all models are in execution batches
        if (allBatchModels.Count != modelNames.Count)
        {
            var missingModels = modelNames.Except(allBatchModels);
            throw new InvalidOperationException($"Models not included in execution batches: {string.Join(", ", missingModels)}");
        }

        // Validate each service entry
        foreach (var service in config.Services)
        {
            if (string.IsNullOrWhiteSpace(service.Name))
                throw new InvalidOperationException("Service entry must have a name");

            var type = service.Type?.Trim().ToLowerInvariant() ?? "transform";

            if (type == "transform")
            {
                if (service.InputProperties == null || service.InputProperties.Count == 0)
                    throw new InvalidOperationException($"Transform service '{service.Name}' must have at least one input property");

                if (service.OutputProperties == null || service.OutputProperties.Count == 0)
                    throw new InvalidOperationException($"Transform service '{service.Name}' must have at least one output property");
            }
            // "external" has no additional structural requirements at the config level
        }
    }

    /// <summary>
    /// Converts a time step configuration to seconds.
    /// </summary>
    public static float GetTimeStepSeconds(TimeStepConfig timeStep)
    {
        if (timeStep == null)
            throw new ArgumentNullException(nameof(timeStep));

        var unit = timeStep.Unit.Trim().ToLowerInvariant();
        return unit switch
        {
            "s" or "sec" or "secs" or "second" or "seconds" => (float)timeStep.Value,
            "ms" or "msec" or "msecs" or "millisecond" or "milliseconds" => (float)(timeStep.Value / 1000.0),
            _ => throw new InvalidOperationException($"Unsupported timeStep unit: {timeStep.Unit}")
        };
    }

    /// <summary>
    /// Loads the properties configuration from a JSON file in a setup folder.
    /// Contains property units and visibility settings specific to each setup.
    /// The file is expected to be in TestFiles/CompositeSetups/{SetupName}/PropertiesConfig.json
    /// </summary>
    /// <param name="setupName">Name of the setup folder (e.g., "DefaultSetup", "OrbitalSetup")</param>
    /// <returns>Parsed PropertiesConfiguration</returns>
    /// <exception cref="FileNotFoundException">If the configuration file is not found</exception>
    /// <exception cref="JsonException">If the JSON is invalid</exception>
    public static PropertiesConfiguration LoadPropertiesConfiguration(string setupName = "DefaultSetup")
    {
        if (string.IsNullOrWhiteSpace(setupName))
            throw new ArgumentException("Setup name cannot be null or empty", nameof(setupName));

        // Determine the path to the configuration file
        var assemblyPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
        var assemblyFolder = System.IO.Path.GetDirectoryName(assemblyPath);
        
        if (string.IsNullOrEmpty(assemblyFolder))
            throw new InvalidOperationException("Could not determine assembly folder");

        // Navigate to TestFiles/CompositeSetups/{SetupName}/PropertiesConfig.json
        string configPath = System.IO.Path.Combine(
            assemblyFolder, 
            "..", "..", "..", 
            "TestFiles", "CompositeSetups", setupName,
            "PropertiesConfig.json"
        );

        // Normalize the path
        configPath = System.IO.Path.GetFullPath(configPath);

        if (!System.IO.File.Exists(configPath))
            throw new FileNotFoundException($"Properties configuration file not found: {configPath}");

        try
        {
            string jsonContent = System.IO.File.ReadAllText(configPath);
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                AllowTrailingCommas = true,
                ReadCommentHandling = JsonCommentHandling.Skip
            };

            var configuration = JsonSerializer.Deserialize<PropertiesConfiguration>(jsonContent, options);
            
            if (configuration == null)
                throw new JsonException("Properties configuration deserialization resulted in null");

            return configuration;
        }
        catch (JsonException ex)
        {
            throw new JsonException($"Error parsing properties configuration for {setupName}: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Loads the canonical entity properties configuration from TestFiles/PropertiesConfig.json.
    /// This configuration defines the units used for entity definitions and simulation calculations.
    /// If the file is missing or invalid, a default SI configuration is returned.
    /// </summary>
    public static PropertiesConfiguration LoadEntityPropertiesConfiguration()
    {
        var defaultConfiguration = CreateDefaultEntityPropertiesConfiguration();

        var assemblyPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
        var assemblyFolder = System.IO.Path.GetDirectoryName(assemblyPath);

        if (string.IsNullOrEmpty(assemblyFolder))
            return defaultConfiguration;

        string configPath = System.IO.Path.Combine(
            assemblyFolder,
            "..", "..", "..",
            "TestFiles",
            EntityPropertiesConfigFileName
        );

        configPath = System.IO.Path.GetFullPath(configPath);

        if (!System.IO.File.Exists(configPath))
            return defaultConfiguration;

        try
        {
            string jsonContent = System.IO.File.ReadAllText(configPath);
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                AllowTrailingCommas = true,
                ReadCommentHandling = JsonCommentHandling.Skip
            };

            var configuration = JsonSerializer.Deserialize<PropertiesConfiguration>(jsonContent, options);

            if (configuration?.PropertyUnits == null || configuration.PropertyUnits.Count == 0)
                return defaultConfiguration;

            return configuration;
        }
        catch
        {
            return defaultConfiguration;
        }
    }

    /// <summary>
    /// Returns the default canonical unit map for entity properties.
    /// </summary>
    public static PropertiesConfiguration CreateDefaultEntityPropertiesConfiguration()
    {
        return new PropertiesConfiguration
        {
            PropertyUnits = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Mass"] = "kg",
                ["Position"] = "m",
                ["Displacement"] = "m",
                ["CurrentSpeed"] = "m/s",
                ["Radius"] = "m",
                ["GravityForce"] = "N",
                ["DragForce"] = "N",
                ["MagnetismForce"] = "N",
                ["WindForce"] = "N"
            }
        };
    }
}

