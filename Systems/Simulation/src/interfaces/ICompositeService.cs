namespace Simulation.Interfaces;

/// <summary>
/// Service that internally orchestrates a structured set of other services.
/// Represents a complete self-contained execution pipeline running within an outer service context.
/// 
/// From the outer ServiceManager's perspective this is a black box — it is scheduled
/// and executed like any other service. Internally it owns its own ServiceManager
/// and isolated property store, making it fully responsible for inner orchestration.
/// 
/// Lifecycle:
/// - InitializeAsync:    loads inner service configuration, creates inner stack (once at startup)
/// - ExecuteAsync:       runs the inner service pipeline (every outer step)
/// - ReinitializeAsync:  replaces inner configuration without recreating this instance,
///                       allowing inner logic to be updated while the outer loop continues
/// 
/// The recursive consequence: the entire DT outer loop is itself a CompositeService at the root.
/// Each CompositeService may contain ITransformService, IExternalService, or further ICompositeService instances.
/// 
/// Examples:
/// - DTSimulationService: runs N inner simulation scenarios with variant initial conditions,
///                        writes one result entity per scenario to the outer property store
/// </summary>
public interface ICompositeService
{
    /// <summary>
    /// Loads and initializes the inner service configuration by name.
    /// Creates the inner service stack (ServiceManager, TransformExecutor, store).
    /// Must be called once before ExecuteAsync.
    /// </summary>
    Task InitializeAsync(string setupName);

    /// <summary>
    /// Executes the inner service pipeline.
    /// Reads initial state from the outer store, runs inner scenarios, writes results back.
    /// Responds to <paramref name="ct"/> cancellation and <paramref name="pauseHandle"/> pause signals
    /// propagated from the outer execution context.
    /// </summary>
    Task ExecuteAsync(CancellationToken ct, PauseHandle pauseHandle);

    /// <summary>
    /// Replaces the inner service configuration without recreating this instance.
    /// Allows updating inner physics models or analysis logic while the outer loop runs.
    /// </summary>
    Task ReinitializeAsync(string setupName);
}
