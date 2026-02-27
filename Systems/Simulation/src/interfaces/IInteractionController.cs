namespace Simulation.Interfaces;

using Simulation.EntityManager;

/// <summary>
/// Interface for user interaction with the Simulation subsystem.
/// Defines the contract for:
/// 1) Controlling simulation execution (Start, Stop, Pause, Continue, OneStep)
/// 2) Managing entities (Create, Modify, List, Inspect)
/// </summary>
public interface IInteractionController
{
    #region Simulation Control

    /// <summary>
    /// Starts the simulation.
    /// Executes all registered services continuously until stopped or paused.
    /// </summary>
    Task StartAsync();

    /// <summary>
    /// Pauses the simulation.
    /// Services stop executing, state is preserved.
    /// Can be resumed with Continue().
    /// </summary>
    void Pause();

    /// <summary>
    /// Resumes the simulation from a paused state.
    /// Execution continues exactly where it was paused.
    /// </summary>
    void Continue();

    /// <summary>
    /// Stops the simulation completely.
    /// Halts execution and clears all state for a fresh start.
    /// </summary>
    Task StopAsync();

    /// <summary>
    /// Gets the current state of the simulation (Running, Paused, Stopped).
    /// </summary>
    string GetCurrentState();

    /// <summary>
    /// Executes exactly one simulation step and automatically pauses.
    /// Useful for step-by-step debugging or manual control.
    /// Can be called when paused or at the beginning (before Start).
    /// </summary>
    Task OneStepAsync();

    #endregion

    #region Entity Management

    /// <summary>
    /// Creates a new entity with the specified properties and metadata.
    /// </summary>
    /// <param name="name">Human-readable name for the entity</param>
    /// <param name="propertyDefaults">Dictionary of property type → initial value</param>
    /// <param name="description">Optional description of the entity's purpose</param>
    /// <returns>The Entity object created with ID, name, and description</returns>
    Task<Entity> CreateEntityAsync(string name, Dictionary<string, object> propertyDefaults, string? description = null);

    /// <summary>
    /// Adds a property to an existing entity.
    /// </summary>
    /// <param name="entityId">The entity to modify</param>
    /// <param name="propertyType">The property type to add</param>
    /// <param name="initialValue">Initial value for the property</param>
    Task AddPropertyToEntityAsync(int entityId, string propertyType, object initialValue);

    /// <summary>
    /// Removes a property from an existing entity.
    /// </summary>
    /// <param name="entityId">The entity to modify</param>
    /// <param name="propertyType">The property type to remove</param>
    void RemovePropertyFromEntity(int entityId, string propertyType);

    /// <summary>
    /// Removes an entity completely from the simulation.
    /// Frees the entity name for reuse and notifies visualization system.
    /// </summary>
    /// <param name="entityId">The entity to remove</param>
    Task RemoveEntityAsync(int entityId);

    /// <summary>
    /// Lists all existing entities and their property compositions.
    /// </summary>
    void ListAllEntities();

    /// <summary>
    /// Displays detailed information about a specific entity.
    /// </summary>
    /// <param name="entityId">The entity to inspect</param>
    void InspectEntity(int entityId);

    #endregion
}
