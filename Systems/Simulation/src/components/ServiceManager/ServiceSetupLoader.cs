namespace Simulation.ServiceManager;

using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Represents a simulation model configuration entry.
/// Defines input/output properties for a single simulation model.
/// </summary>
public class SimulationModelConfig
{
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("inputProperties")]
    public required List<string> InputProperties { get; set; }

    [JsonPropertyName("outputProperties")]
    public required List<string> OutputProperties { get; set; }
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
public class ServiceSetupConfiguration
{
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("simulationModels")]
    public required List<SimulationModelConfig> SimulationModels { get; set; }

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
}

/// <summary>
/// Loads and parses Service Setup configuration files.
/// Supports reading JSON configuration files from the ServiceSetups folder.
/// </summary>
public class ServiceSetupLoader
{
    private const string ServiceSetupsFolder = "ServiceSetups";

    /// <summary>
    /// Loads a service setup configuration from a JSON file.
    /// </summary>
    /// <param name="configurationFileName">Name of the configuration file (e.g., "DefaultSetup.json")</param>
    /// <returns>Parsed ServiceSetupConfiguration</returns>
    /// <exception cref="FileNotFoundException">If the configuration file is not found</exception>
    /// <exception cref="JsonException">If the JSON is invalid</exception>
    public static ServiceSetupConfiguration LoadConfiguration(string configurationFileName)
    {
        if (string.IsNullOrWhiteSpace(configurationFileName))
            throw new ArgumentException("Configuration file name cannot be null or empty", nameof(configurationFileName));

        // Determine the path to the configuration file
        // The file is expected to be in the ServiceSetups folder relative to this assembly
        var assemblyPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
        var assemblyFolder = System.IO.Path.GetDirectoryName(assemblyPath);
        
        if (string.IsNullOrEmpty(assemblyFolder))
            throw new InvalidOperationException("Could not determine assembly folder");

        // Navigate from the assembly location up to the project root and down to ServiceSetups
        // Assembly is typically at: bin/Debug/net9.0/Simulation.dll
        // We need to get to: src/components/ServiceManager/ServiceSetups/
        string configPath = System.IO.Path.Combine(
            assemblyFolder, 
            "..", "..", "..", 
            "src", "components", "ServiceManager", ServiceSetupsFolder, 
            configurationFileName
        );

        // Normalize the path
        configPath = System.IO.Path.GetFullPath(configPath);

        if (!System.IO.File.Exists(configPath))
            throw new FileNotFoundException($"Configuration file not found: {configPath}");

        try
        {
            string jsonContent = System.IO.File.ReadAllText(configPath);
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                AllowTrailingCommas = true,
                ReadCommentHandling = JsonCommentHandling.Skip
            };

            var configuration = JsonSerializer.Deserialize<ServiceSetupConfiguration>(jsonContent, options);
            
            if (configuration == null)
                throw new JsonException("Configuration deserialization resulted in null");

            ValidateConfiguration(configuration);
            return configuration;
        }
        catch (JsonException ex)
        {
            throw new JsonException($"Error parsing configuration file {configurationFileName}: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Validates the loaded configuration for consistency.
    /// </summary>
    private static void ValidateConfiguration(ServiceSetupConfiguration config)
    {
        if (string.IsNullOrWhiteSpace(config.Name))
            throw new InvalidOperationException("Configuration must have a name");

        if (config.SimulationModels == null || config.SimulationModels.Count == 0)
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
        var modelNames = new HashSet<string>(config.SimulationModels.Select(m => m.Name));
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

        // Validate each model
        foreach (var model in config.SimulationModels)
        {
            if (string.IsNullOrWhiteSpace(model.Name))
                throw new InvalidOperationException("Model must have a name");

            if (model.InputProperties == null || model.InputProperties.Count == 0)
                throw new InvalidOperationException($"Model {model.Name} must have at least one input property");

            if (model.OutputProperties == null || model.OutputProperties.Count == 0)
                throw new InvalidOperationException($"Model {model.Name} must have at least one output property");
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
    /// Loads the properties configuration from a JSON file.
    /// Contains property units and visibility settings.
    /// </summary>
    /// <param name="configurationFileName">Name of the properties configuration file (e.g., "PropertiesConfig.json")</param>
    /// <returns>Parsed PropertiesConfiguration</returns>
    /// <exception cref="FileNotFoundException">If the configuration file is not found</exception>
    /// <exception cref="JsonException">If the JSON is invalid</exception>
    public static PropertiesConfiguration LoadPropertiesConfiguration(string configurationFileName = "PropertiesConfig.json")
    {
        if (string.IsNullOrWhiteSpace(configurationFileName))
            throw new ArgumentException("Configuration file name cannot be null or empty", nameof(configurationFileName));

        // Determine the path to the configuration file
        var assemblyPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
        var assemblyFolder = System.IO.Path.GetDirectoryName(assemblyPath);
        
        if (string.IsNullOrEmpty(assemblyFolder))
            throw new InvalidOperationException("Could not determine assembly folder");

        string configPath = System.IO.Path.Combine(
            assemblyFolder, 
            "..", "..", "..", 
            "src", "components", "ServiceManager", ServiceSetupsFolder, 
            configurationFileName
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
            throw new JsonException($"Error parsing properties configuration file {configurationFileName}: {ex.Message}", ex);
        }
    }
}

