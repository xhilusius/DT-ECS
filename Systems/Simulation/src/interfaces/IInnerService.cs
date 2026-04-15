namespace Simulation.Interfaces;

using Simulation.EntityManager;

/// <summary>
/// Represents a single isolated inner service session.
/// Provides the minimal surface a composite service needs to populate
/// an inner world and advance it, without any knowledge of how the
/// underlying stack (RepositoryManager, EntityManager, ServiceManager, …) is assembled.
/// </summary>
public interface IInnerService
{
    /// <summary>
    /// Number of simulation steps configured for this inner session.
    /// Determined by the setup loaded at creation time.
    /// </summary>
    int SimulationSteps { get; }

    /// <summary>
    /// Spawns an entity in the inner store and returns the created entity with its assigned ID.
    /// </summary>
    Task<Entity> CreateEntityAsync(string name, Dictionary<string, object> properties, string? description = null);

    /// <summary>
    /// Removes an entity from the inner store.
    /// </summary>
    Task RemoveEntityAsync(int entityId);

    /// <summary>
    /// Advances the inner simulation by one step.
    /// </summary>
    Task OneStepAsync();

    /// <summary>
    /// Stops and cleans up the inner simulation.
    /// </summary>
    Task StopAsync();
}
