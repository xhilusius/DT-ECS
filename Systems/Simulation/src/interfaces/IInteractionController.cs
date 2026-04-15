namespace Simulation.Interfaces;

using Simulation.EntityManager;

/// <summary>
/// Interface for user interaction with the Simulation subsystem.
/// Defines the contract for:
/// 1) Running the top-level test execution (RunAsync)
/// 2) Global pause / continue / stop controls that propagate to all inner levels
/// 3) Ad-hoc entity management on the outer entity store
/// </summary>
public interface IInteractionController
{
    #region Execution Control

    /// <summary>
    /// Starts the top-level test executor and awaits its completion.
    /// Returns when execution finishes normally or is stopped via StopAsync.
    /// </summary>
    Task RunAsync();

    /// <summary>
    /// Pauses execution. The simulation suspends between steps at all levels.
    /// Call Continue to resume.
    /// </summary>
    void Pause();

    /// <summary>
    /// Resumes execution after a Pause call.
    /// </summary>
    void Continue();

    /// <summary>
    /// Stops execution immediately. RunAsync will return after the current step completes.
    /// </summary>
    Task StopAsync();

    #endregion

    #region Entity Management

    /// <summary>
    /// Creates a new entity with the specified properties and metadata.
    /// </summary>
    Task<Entity> CreateEntityAsync(string name, Dictionary<string, object> propertyDefaults, string? description = null);

    /// <summary>
    /// Adds a property to an existing entity.
    /// </summary>
    Task AddPropertyToEntityAsync(int entityId, string propertyType, object initialValue);

    /// <summary>
    /// Removes a property from an existing entity.
    /// </summary>
    void RemovePropertyFromEntity(int entityId, string propertyType);

    /// <summary>
    /// Removes an entity completely from the simulation.
    /// </summary>
    Task RemoveEntityAsync(int entityId);

    /// <summary>
    /// Lists all existing entities and their property compositions.
    /// </summary>
    void ListAllEntities();

    /// <summary>
    /// Displays detailed information about a specific entity.
    /// </summary>
    void InspectEntity(int entityId);

    #endregion
}
