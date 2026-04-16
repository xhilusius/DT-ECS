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
    public double TimeStepSeconds { get; }

    public bool SilentMode
    {
        get => _stateManager.SilentMode;
        set => _stateManager.SilentMode = value;
    }

    internal InnerService(
        ServiceManager serviceManager,
        EntityManager entityManager,
        StateManager stateManager,
        int simulationSteps,
        double timeStepSeconds)
    {
        _serviceManager = serviceManager;
        _entityManager = entityManager;
        _stateManager = stateManager;
        SimulationSteps = simulationSteps;
        TimeStepSeconds = timeStepSeconds;
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

    public async Task<IReadOnlyList<(int EntityId, string EntityName, object? Value)>> GetPropertyValuesAsync(string propertyType)
    {
        var values = await _stateManager.GetPropertiesByTypeAsync(propertyType);
        if (values == null || values.Count == 0)
            return Array.Empty<(int, string, object?)>();

        var entityIds = _entityManager.GetEntitiesForProperty(propertyType);
        var result    = new List<(int, string, object?)>(entityIds.Count);

        for (int i = 0; i < entityIds.Count && i < values.Count; i++)
        {
            var entity = _entityManager.GetEntity(entityIds[i]);
            result.Add((entityIds[i], entity?.Name ?? $"Entity_{entityIds[i]}", values[i]));
        }

        return result;
    }
}
