namespace Simulation.TransformExecutor;

using System.Numerics;
using Simulation.Interfaces;
using Simulation.StateManager;

/// <summary>
/// Executes batches of TransformServices against the ECS property store.
/// 
/// Responsibilities:
/// - Maintain registry of transform services
/// - Execute service batches provided by ServiceManager
/// - For each service: request StateManager for inputs, call service, request StateManager to store outputs
/// - Services in a batch execute to completion (sequentially or in parallel)
/// 
/// Data flow:
/// TransformExecutor requests StateManager → StateManager queries RepositoryManager → data returned
/// TransformExecutor passes bundle to service (pure computation)
/// TransformExecutor requests StateManager to store outputs → StateManager delegates to RepositoryManager
/// 
/// Does not manage service ordering - ServiceManager determines execution order based on dependencies.
/// Does not manage simulation state - ServiceManager controls Running/Paused/Stopped.
/// Does not access repository directly - delegates all storage operations to StateManager.
/// </summary>
public class TransformExecutor
{
    private readonly List<ITransformService> _registeredServices;
    private readonly StateManager _stateManager;

    public TransformExecutor(StateManager stateManager)
    {
        if (stateManager == null)
            throw new ArgumentNullException(nameof(stateManager));

        _stateManager = stateManager;
        _registeredServices = new List<ITransformService>();
    }

    /// <summary>
    /// Registers a simulation service to be available for execution.
    /// This is called during initialization, before simulation starts.
    /// </summary>
    public void RegisterService(ITransformService service)
    {
        if (service == null)
            throw new ArgumentNullException(nameof(service));

        _registeredServices.Add(service);
    }

    /// <summary>
    /// Executes a batch of services.
    /// For each service in the batch:
    /// 1. Request StateManager to fetch required input properties
    /// 2. Call the service with input data
    /// 3. Request StateManager to write output properties back
    /// 
    /// Services in a batch can be treated as independent (each has its inputs available),
    /// so they could execute in parallel if desired.
    /// 
    /// After the batch completes, the state has been updated with all service outputs.
    /// </summary>
    public async Task ExecuteServiceBatchAsync(List<ServiceDescriptor> batch, bool parallel = false)
    {
        if (batch == null)
            throw new ArgumentNullException(nameof(batch));

        try
        {
            if (parallel)
            {
                // Parallel execution: fetch inputs, execute all services concurrently, then write all outputs
                var serviceExecutionTasks = batch.Select(async serviceDescriptor =>
                {
                    var service = serviceDescriptor.Service;
                    if (service == null)
                        throw new InvalidOperationException($"Service {serviceDescriptor.ServiceName} has null Service reference");

                    // Step 1: Fetch inputs for this service
                    var inputBundle = await _stateManager.GetPropertiesByArchetypeWithOptionalAsync(
                        serviceDescriptor.ServiceName,
                        serviceDescriptor.OptionalInputProperties
                    );

                    // Step 2: Execute the service (this runs in parallel with other services)
                    var outputData = await service.ExecuteAsync(inputBundle);

                    // Return both the descriptor and output data for processing after all complete
                    return (serviceDescriptor, outputData, inputBundle);
                }).ToList();

                // Wait for all services to complete their execution
                var results = await Task.WhenAll(serviceExecutionTasks);

                // Step 3: Write all outputs back to state (done sequentially to avoid race conditions)
                foreach (var (serviceDescriptor, outputData, inputBundle) in results)
                {
                    foreach (var outputProperty in serviceDescriptor.OutputProperties)
                    {
                        if (outputData.ContainsKey(outputProperty))
                        {
                            await _stateManager.SetPropertiesByTypeAsync(outputProperty, outputData[outputProperty]);
                            _stateManager.EnsureEntitiesHaveProperty(inputBundle.ValidEntityIds, outputProperty);
                        }
                    }
                }
            }
            else
            {
                // Sequential execution: traditional one-by-one approach
                foreach (var serviceDescriptor in batch)
                {
                    var service = serviceDescriptor.Service;
                    if (service == null)
                        throw new InvalidOperationException($"Service {serviceDescriptor.ServiceName} has null Service reference");

                    // Step 1: Request StateManager to fetch all input properties for this service's archetype
                    var inputBundle = await _stateManager.GetPropertiesByArchetypeWithOptionalAsync(
                        serviceDescriptor.ServiceName,
                        serviceDescriptor.OptionalInputProperties
                    );

                    // Step 2: Execute the service with the input bundle
                    var outputData = await service.ExecuteAsync(inputBundle);

                    // Step 3: Request StateManager to write output properties back
                    foreach (var outputProperty in serviceDescriptor.OutputProperties)
                    {
                        if (outputData.ContainsKey(outputProperty))
                        {
                            await _stateManager.SetPropertiesByTypeAsync(outputProperty, outputData[outputProperty]);
                            _stateManager.EnsureEntitiesHaveProperty(inputBundle.ValidEntityIds, outputProperty);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR in ExecuteServiceBatchAsync: {ex.Message}");
            Console.WriteLine($"Inner Exception: {ex.InnerException?.Message}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"Stack Trace: {ex.InnerException.StackTrace}");
            }
            throw new InvalidOperationException("Service batch execution failed", ex);
        }
    }

    /// <summary>
    /// Clears all simulation state (empties the property repository).
    /// Used when stopping to reset to a clean state.
    /// </summary>
    public async Task ClearAllStateAsync()
    {
        await _stateManager.ClearAllPropertiesAsync();
    }

    /// <summary>
    /// Gets the list of registered services.
    /// Primarily for inspection/debugging.
    /// </summary>
    public IReadOnlyList<ITransformService> GetRegisteredServices()
    {
        return _registeredServices.AsReadOnly();
    }

    /// <summary>
    /// Gets a reference to the StateManager (for inspection/debugging only).
    /// </summary>
    public StateManager GetStateManager()
    {
        return _stateManager;
    }
}

