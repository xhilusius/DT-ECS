namespace Simulation.ServiceManager;

using Simulation.EntityManager;
using Simulation.Interfaces;
using Simulation.StateManager;

/// <summary>
/// Concrete implementation of <see cref="IInnerService"/>.
/// Wraps a fully wired simulation stack (RepositoryManager, EntityManager,
/// StateManager, TransformExecutor, ServiceManager) behind the minimal surface
/// composite services need.
///
/// Created exclusively by <see cref="ServiceManager"/> via <see cref="IInnerServiceFactory"/>.
/// </summary>
internal sealed class InnerService : IInnerService
{
    private readonly ServiceManager _serviceManager;
    private readonly EntityManager _entityManager;
    private readonly StateManager _stateManager;

    public int SimulationSteps { get; }

    public bool SilentMode
    {
        get => _stateManager.SilentMode;
        set => _stateManager.SilentMode = value;
    }

    internal InnerService(
        ServiceManager serviceManager,
        EntityManager entityManager,
        StateManager stateManager,
        int simulationSteps)
    {
        _serviceManager = serviceManager;
        _entityManager = entityManager;
        _stateManager = stateManager;
        SimulationSteps = simulationSteps;
    }

    public Task<Entity> CreateEntityAsync(string name, Dictionary<string, object> properties, string? description = null)
        => _entityManager.RegisterNewEntityWithStateAsync(name, properties, description);

    public Task RemoveEntityAsync(int entityId)
        => _entityManager.RemoveEntityWithStateAsync(entityId);

    public Task OneStepAsync()
        => _serviceManager.ExecuteOneStepAsync();

    public Task StopAsync()
        => _serviceManager.StopAsync();

    public Task ReportStateAsync(string description)
        => _stateManager.ReportStateAsync(description);

    public Task UpdateVisualizationAsync()
        => _stateManager.NotifyStateUpdatedAsync();
}
