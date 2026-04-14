namespace Simulation.ServiceManager;

using System.Text.Json;
using DataStorage.RepositoryManager;
using Simulation.EntityManager;
using Simulation.Interfaces;
using Simulation.StateManager;
using Simulation.TransformExecutor;

/// <summary>
/// Manages the ordered execution of services respecting their property dependencies.
/// 
/// Responsibilities:
/// - Register services with their input/output property declarations
/// - Determine execution order based on property dependencies
/// - Provide executable batches to TransformExecutor (services that can run in parallel)
/// - Wait for each batch to complete before providing the next
/// - Manage simulation state (Running, Paused, Stopped)
/// - Control the simulation loop
/// 
/// Execution model:
/// 1. ServiceManager computes which services can execute (dependencies met)
/// 2. ServiceManager provides batch to TransformExecutor
/// 3. TransformExecutor executes batch, services read/write properties
/// 4. TransformExecutor returns results
/// 5. ServiceManager updates available properties, computes next batch
/// 6. Repeat until all services executed
/// 
/// Does not own TransformExecutor - receives it as a reference.
/// </summary>
public class ServiceManager : IInnerServiceFactory
{
    private readonly TransformExecutor _transformExecutor;
    private readonly Dictionary<string, ServiceDescriptor> _services;
    private readonly HashSet<string> _completedServices;
    private readonly HashSet<string> _availableProperties;
    private SimulationState _currentState;
    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _simulationLoopTask;
    private readonly object _stateLock = new object();
    private int _stepCounter = 0;
    private bool _parallelExecution = false;
    private int _stepDelayMs = 0;
    private int _simulationSteps = 10;
    
    /// <summary>
    /// Maps each service name to its batch index.
    /// Determines the execution phase order.
    /// </summary>
    private Dictionary<string, int> _serviceToBatchIndex = new();

    /// <summary>
    /// Represents the current state of the simulation.
    /// </summary>
    public enum SimulationState
    {
        Stopped,      // Not running, ready to be started
        Running,      // Actively executing simulation steps
        Paused,       // Temporarily halted, can be resumed
        Stopping      // In process of stopping
    }

    /// <summary>
    /// Creates a ServiceManager that loads configuration from a JSON file.
    /// Call InitializeAsync after construction to load services from configuration.
    /// </summary>
    /// <param name="transformExecutor">The TransformExecutor instance to use for service execution</param>
    public ServiceManager(TransformExecutor transformExecutor)
    {
        if (transformExecutor == null)
            throw new ArgumentNullException(nameof(transformExecutor));

        _transformExecutor = transformExecutor;
        _services = new Dictionary<string, ServiceDescriptor>();
        _completedServices = new HashSet<string>();
        _availableProperties = new HashSet<string>();
        _currentState = SimulationState.Stopped;
    }

    /// <summary>
    /// Asynchronously initializes services by loading configuration from a setup folder.
    /// Must be called after construction to set up services.
    /// </summary>
    /// <param name="setupName">Name of the setup folder in TestFiles/CompositeSetups (e.g., "DefaultSetup", "OrbitalSetup")</param>
    public async Task InitializeAsync(string setupName)
    {
        if (string.IsNullOrWhiteSpace(setupName))
            throw new ArgumentException("Setup name cannot be null or empty", nameof(setupName));

        await InitializeServicesFromConfigurationAsync(setupName);
    }

    /// <inheritdoc/>
    public async Task<IInnerService> CreateInnerServiceAsync(string setupName)
    {
        if (string.IsNullOrWhiteSpace(setupName))
            throw new ArgumentException("Setup name cannot be null or empty", nameof(setupName));

        var repositoryManager = new RepositoryManager();
        var entityManager = new EntityManager();
        var stateManager = new StateManager(repositoryManager, entityManager);
        entityManager.SetStateManager(stateManager);
        stateManager.SilentMode = true;

        var transformExecutor = new TransformExecutor(stateManager);
        var innerServiceManager = new ServiceManager(transformExecutor);
        await innerServiceManager.InitializeAsync(setupName);

        var controller = new global::Simulation.InteractionController.InteractionController(
            innerServiceManager, entityManager);

        var setup = CompositeServiceSetupLoader.LoadConfiguration(setupName);
        return new InnerService(controller, setup.SimulationSteps);
    }

    /// <summary>
    /// Registers a service with its input/output property declarations.
    /// Must be called before starting the simulation.
    /// </summary>
    public void RegisterService(ServiceDescriptor descriptor)
    {
        if (descriptor == null)
            throw new ArgumentNullException(nameof(descriptor));

        lock (_stateLock)
        {
            if (_currentState != SimulationState.Stopped)
                throw new InvalidOperationException("Cannot register services while simulation is running");

            _services[descriptor.ServiceName] = descriptor;
        }
    }

    /// <summary>
    /// Starts the simulation loop.
    /// Executes all registered services in batches respecting dependencies.
    /// </summary>
    public async Task StartAsync()
    {
        lock (_stateLock)
        {
            if (_currentState != SimulationState.Stopped)
                throw new InvalidOperationException("Simulation is already running or paused");

            _currentState = SimulationState.Running;
            _stepCounter = 0; // Reset step counter when starting
        }

        try
        {
            _cancellationTokenSource = new CancellationTokenSource();
            _simulationLoopTask = RunSimulationLoopAsync(_cancellationTokenSource.Token);
            await _simulationLoopTask;
        }
        catch (OperationCanceledException)
        {
            // Expected when stopping
        }
        finally
        {
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;

            lock (_stateLock)
            {
                _currentState = SimulationState.Stopped;
            }
        }
    }

    /// <summary>
    /// Pauses the simulation loop.
    /// The loop stops processing but state is preserved in the repository.
    /// </summary>
    public void Pause()
    {
        lock (_stateLock)
        {
            if (_currentState == SimulationState.Running)
            {
                _currentState = SimulationState.Paused;
            }
        }
    }

    /// <summary>
    /// Resumes the simulation loop from a paused state.
    /// </summary>
    public void Continue()
    {
        lock (_stateLock)
        {
            if (_currentState == SimulationState.Paused)
            {
                _currentState = SimulationState.Running;
            }
        }
    }

    /// <summary>
    /// Stops the simulation loop completely.
    /// Waits for the current step to complete.
    /// </summary>
    public async Task StopAsync()
    {
        lock (_stateLock)
        {
            if (_currentState == SimulationState.Stopped)
                return;

            _currentState = SimulationState.Stopping;
        }

        _cancellationTokenSource?.Cancel();

        if (_simulationLoopTask != null)
        {
            try
            {
                await _simulationLoopTask;
            }
            catch (OperationCanceledException)
            {
                // Expected
            }
        }
    }
    /// <summary>
    /// Executes exactly one simulation step (all services in dependency order) and automatically pauses.
    /// Used for step-by-step debugging or manual control.
    /// Respects step delay configuration for visualization pacing.
    /// </summary>
    public async Task ExecuteOneStepAsync()
    {
        lock (_stateLock)
        {
            if (_currentState != SimulationState.Paused && _currentState != SimulationState.Stopped)
                throw new InvalidOperationException("Can only execute one step when paused or stopped");
            
            // Reset counter if starting from stopped state
            if (_currentState == SimulationState.Stopped)
                _stepCounter = 0;
        }

        // Record step start time if step delay is configured
        var stepStartTime = _stepDelayMs > 0 ? (DateTime?)DateTime.UtcNow : null;

        // Execute one simulation step
        await ExecuteSimulationStepAsync();

        // Apply step delay for visualization pacing if configured
        if (_stepDelayMs > 0 && stepStartTime.HasValue)
        {
            var elapsedMs = (DateTime.UtcNow - stepStartTime.Value).TotalMilliseconds;
            var remainingDelayMs = _stepDelayMs - (int)elapsedMs;

            if (remainingDelayMs > 0)
            {
                await Task.Delay(remainingDelayMs);
            }
        }

        // Ensure we're in paused state
        lock (_stateLock)
        {
            _currentState = SimulationState.Paused;
        }
    }

    /// <summary>
    /// Clears all simulation state (resets repository and service completion tracking).
    /// Used when stopping to ensure a fresh start.
    /// </summary>
    public async Task ClearAllStateAsync()
    {
        await _transformExecutor.ClearAllStateAsync();

        lock (_stateLock)
        {
            _completedServices.Clear();
            _availableProperties.Clear();
        }
    }

    /// <summary>
    /// Gets the current simulation state.
    /// </summary>
    public SimulationState GetState()
    {
        lock (_stateLock)
        {
            return _currentState;
        }
    }

    /// <summary>
    /// Runs the simulation loop, repeatedly executing all services in dependency order.
    /// Respects pause/stop requests and step delay configuration for visualization pacing.
    /// </summary>
    private async Task RunSimulationLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var state = GetState();

            // Check if paused
            if (state == SimulationState.Paused)
            {
                await Task.Delay(100, cancellationToken);
                continue;
            }

            // Check if stopping
            if (state == SimulationState.Stopping)
            {
                break;
            }

            // Record step start time if step delay is configured
            var stepStartTime = _stepDelayMs > 0 ? (DateTime?)DateTime.UtcNow : null;

            // Execute one simulation step
            await ExecuteSimulationStepAsync();

            // Apply step delay for visualization pacing if configured
            if (_stepDelayMs > 0 && stepStartTime.HasValue)
            {
                var elapsedMs = (DateTime.UtcNow - stepStartTime.Value).TotalMilliseconds;
                var remainingDelayMs = _stepDelayMs - (int)elapsedMs;

                if (remainingDelayMs > 0)
                {
                    await Task.Delay(remainingDelayMs, cancellationToken);
                }
            }
            else
            {
                // No step delay: small delay for frame rate control (~60 FPS)
                await Task.Delay(16, cancellationToken);
            }
        }
    }

    /// <summary>
    /// Executes one complete simulation step.
    /// Executes all services in batches respecting their dependencies and batch order.
    /// Services within a batch can run in parallel, but batches execute sequentially.
    /// </summary>
    private async Task ExecuteSimulationStepAsync()
    {
        lock (_stateLock)
        {
            _completedServices.Clear();
            _availableProperties.Clear();
        }

        var existingPropertyTypes = await _transformExecutor.GetStateManager()
            .GetRepositoryManager()
            .GetAllPropertyTypesAsync();

        lock (_stateLock)
        {
            foreach (var propertyType in existingPropertyTypes)
            {
                _availableProperties.Add(propertyType);
            }
        }

        // Determine the number of batches
        int maxBatchIndex = _serviceToBatchIndex.Values.Max();

        // Execute each batch sequentially
        for (int currentBatch = 0; currentBatch <= maxBatchIndex; currentBatch++)
        {
            // Get all services in this batch
            var servicesInBatch = _services.Values
                .Where(s => _serviceToBatchIndex[s.ServiceName] == currentBatch)
                .ToList();

            // Execute all services in the batch that have their dependencies met
            while (true)
            {
                var executableServices = servicesInBatch
                    .Where(s => 
                        !_completedServices.Contains(s.ServiceName) &&
                        s.InputProperties.All(prop => _availableProperties.Contains(prop))
                    )
                    .ToList();

                if (executableServices.Count == 0)
                {
                    // All services in this batch have executed
                    break;
                }

                // Execute the batch of services (in parallel or sequential based on configuration)
                await _transformExecutor.ExecuteServiceBatchAsync(executableServices, _parallelExecution);

                // Mark as completed and update available properties
                lock (_stateLock)
                {
                    foreach (var serviceDescriptor in executableServices)
                    {
                        _completedServices.Add(serviceDescriptor.ServiceName);

                        foreach (var outputProp in serviceDescriptor.OutputProperties)
                        {
                            _availableProperties.Add(outputProp);
                        }
                    }
                }
            }
        }

        // Increment and report step completion
        int currentStep;
        lock (_stateLock)
        {
            _stepCounter++;
            currentStep = _stepCounter;
        }
        await _transformExecutor.GetStateManager().ReportStateAsync($"Simulation step {currentStep} complete");
    }

    /// <summary>
    /// Initializes services from a setup configuration folder.
    /// Loads the configuration, instantiates models, registers archetypes, and registers them with their batch assignments.
    /// </summary>
    private async Task InitializeServicesFromConfigurationAsync(string setupName)
    {
        try
        {
            // Load the setup configuration from TestFiles/CompositeSetups/{SetupName}/Setup.json
            var config = CompositeServiceSetupLoader.LoadConfiguration(setupName);

            // Load properties configuration (units and visibility settings) from TestFiles/CompositeSetups/{SetupName}/PropertiesConfig.json
            var propertiesConfig = CompositeServiceSetupLoader.LoadPropertiesConfiguration(setupName);
            var entityPropertiesConfig = CompositeServiceSetupLoader.LoadEntityPropertiesConfiguration();

            // Store the parallel execution setting, step delay, and step count
            _parallelExecution = config.Parallel;
            _stepDelayMs = config.StepDelayMs;
            _simulationSteps = config.SimulationSteps;

            // Create a mapping of model names to their configurations for easy lookup
            var modelConfigMap = config.Services.ToDictionary(m => m.Name, m => m);

            var timeStepSeconds = CompositeServiceSetupLoader.GetTimeStepSeconds(config.TimeStep);
            _transformExecutor.GetStateManager().SetPropertiesConfiguration(entityPropertiesConfig, propertiesConfig);

            var repositoryManager = _transformExecutor.GetStateManager().GetRepositoryManager();

            // Process each batch and register services
            _serviceToBatchIndex = new Dictionary<string, int>();

            for (int batchIndex = 0; batchIndex < config.ExecutionBatches.Count; batchIndex++)
            {
                var batch = config.ExecutionBatches[batchIndex];

                foreach (var modelName in batch)
                {
                    if (!modelConfigMap.TryGetValue(modelName, out var modelConfig))
                        throw new InvalidOperationException($"Model {modelName} referenced in execution batch but not defined");

                    var serviceType = modelConfig.Type?.Trim().ToLowerInvariant() ?? "transform";

                    if (serviceType == "transform")
                    {
                        // Register archetype for this model through RepositoryManager
                        await repositoryManager.RegisterArchetypeAsync(
                            modelName,
                            modelName,
                            new HashSet<string>(modelConfig.InputProperties),
                            $"Archetype for {modelName} requiring properties: {string.Join(", ", modelConfig.InputProperties)}"
                        );

                        // Create the model instance
                        var modelInstance = TransformServiceFactory.CreateModel(modelName, timeStepSeconds);

                        // Register the model with SimEngine
                        _transformExecutor.RegisterService(modelInstance);

                        // Create and register the service descriptor
                        var descriptor = new ServiceDescriptor(
                            modelName,
                            modelInstance,
                            modelConfig.InputProperties,
                            modelConfig.OutputProperties,
                            modelConfig.OptionalInputProperties
                        );

                        RegisterService(descriptor);

                        // Track the batch assignment for this service
                        _serviceToBatchIndex[modelName] = batchIndex;
                    }
                    else if (serviceType == "composite")
                    {
                        // Composite service support: inner ServiceManager stack owned by an ICompositeService.
                        // Registration will be implemented when ICompositeService scheduling is introduced.
                        throw new NotSupportedException(
                            $"Composite service '{modelName}' (setupName: '{modelConfig.SetupName}') cannot be registered yet. " +
                            $"ICompositeService scheduling support is pending.");
                    }
                    else if (serviceType == "external")
                    {
                        // External service support: IExternalService crosses the system boundary.
                        // Registration will be implemented when IExternalService scheduling is introduced.
                        throw new NotSupportedException(
                            $"External service '{modelName}' cannot be registered yet. " +
                            $"IExternalService scheduling support is pending.");
                    }
                    else
                    {
                        throw new InvalidOperationException(
                            $"Unknown service type '{modelConfig.Type}' for service '{modelName}'. " +
                            $"Supported types: transform, composite, external.");
                    }
                }
            }
        }
        catch (FileNotFoundException ex)
        {
            throw new InvalidOperationException($"Configuration file not found: {ex.Message}", ex);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Error parsing configuration file: {ex.Message}", ex);
        }
    }
}




