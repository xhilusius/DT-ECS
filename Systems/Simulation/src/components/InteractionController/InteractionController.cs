namespace Simulation.InteractionController;

using Simulation.EntityManager;
using Simulation.Interfaces;
using Simulation.ServiceManager;
using Simulation.StateManager;

/// <summary>
/// Entry point for user interaction with the Simulation subsystem.
/// Provides a user-friendly interface for controlling simulation execution and entity management.
/// Delegates simulation management to ServiceManager.
/// Delegates entity metadata management to EntityManager.
/// Delegates state storage to StateManager.
/// 
/// Two main areas of responsibility:
/// 
/// 1) SIMULATION CONTROL (5 run modes):
///    - Start: Execute simulation continuously until stopped
///    - Stop: Halt execution and reset all state
///    - Pause: Temporarily halt while preserving state
///    - Continue: Resume from paused state
///    - OneStep: Execute a single step and auto-pause
/// 
/// 2) ENTITY MANAGEMENT:
///    - Create new entities with specified properties
///    - Add properties to existing entities
///    - Remove properties from entities
///    - Query entity composition and status
/// 
/// Does not own ServiceManager, EntityManager, or StateManager - receives them as references.
/// </summary>
public class InteractionController : IInteractionController
{
    private readonly ServiceManager _serviceManager;
    private readonly EntityManager _entityManager;
    private readonly StateManager _stateManager;

    public InteractionController(ServiceManager serviceManager, EntityManager entityManager, StateManager stateManager)
    {
        if (serviceManager == null)
            throw new ArgumentNullException(nameof(serviceManager));
        if (entityManager == null)
            throw new ArgumentNullException(nameof(entityManager));
        if (stateManager == null)
            throw new ArgumentNullException(nameof(stateManager));

        _serviceManager = serviceManager;
        _entityManager = entityManager;
        _stateManager = stateManager;
    }

    /// <summary>
    /// 1) START: Starts the simulation.
    /// Executes the simulation loop continuously until Pause() or Stop() is called.
    /// State is preserved and updated in the repository with each step.
    /// </summary>
    public async Task StartAsync()
    {
        var state = _serviceManager.GetState();

        if (state != ServiceManager.SimulationState.Stopped)
        {
            throw new InvalidOperationException("Simulation is already running or paused. Stop it first.");
        }

        Console.WriteLine("Starting simulation...");
        await _serviceManager.StartAsync();
    }

    /// <summary>
    /// 3) PAUSE: Pauses the simulation without losing state.
    /// The simulation loop stops executing after completing the current step.
    /// All state is preserved in the StateRepository.
    /// Can be resumed later with Continue().
    /// </summary>
    public void Pause()
    {
        var state = _serviceManager.GetState();

        if (state == ServiceManager.SimulationState.Running)
        {
            _serviceManager.Pause();
            Console.WriteLine("Simulation paused. State is preserved.");
        }
        else if (state == ServiceManager.SimulationState.Paused)
        {
            Console.WriteLine("Simulation is already paused.");
        }
        else
        {
            throw new InvalidOperationException("Cannot pause a stopped simulation.");
        }
    }

    /// <summary>
    /// 4) CONTINUE: Resumes the simulation from a paused state.
    /// Execution continues exactly where it was paused.
    /// State is restored from the StateRepository.
    /// </summary>
    public void Continue()
    {
        var state = _serviceManager.GetState();

        if (state == ServiceManager.SimulationState.Paused)
        {
            _serviceManager.Continue();
            Console.WriteLine("Simulation resumed from pause.");
        }
        else if (state == ServiceManager.SimulationState.Running)
        {
            Console.WriteLine("Simulation is already running.");
        }
        else
        {
            throw new InvalidOperationException("Cannot continue a stopped simulation. Start it instead.");
        }
    }

    /// <summary>
    /// 2) STOP: Stops the simulation completely and resets to clean state.
    /// Halts all execution immediately.
    /// Deletes all properties from the StateRepository for a fresh start.
    /// State is NOT preserved after stop.
    /// </summary>
    public async Task StopAsync()
    {
        var state = _serviceManager.GetState();

        if (state == ServiceManager.SimulationState.Stopped)
        {
            Console.WriteLine("Simulation is already stopped.");
            return;
        }

        await _serviceManager.StopAsync();

        // Clear all state for a clean reset
        await _serviceManager.ClearAllStateAsync();
    }

    /// <summary>
    /// Gets the current state of the simulation.
    /// </summary>
    public string GetCurrentState()
    {
        return _serviceManager.GetState().ToString();
    }

    /// <summary>
    /// 5) ONE-STEP: Executes exactly one simulation step and automatically pauses.
    /// Useful for step-by-step debugging or manual control of the simulation.
    /// Can be called when paused or at the beginning (before Start).
    /// State is preserved after each step.
    /// Executes all registered SimulationServices once.
    /// </summary>
    public async Task OneStepAsync()
    {
        var state = _serviceManager.GetState();

        if (state != ServiceManager.SimulationState.Paused && state != ServiceManager.SimulationState.Stopped)
        {
            throw new InvalidOperationException("OneStep can only be used when paused or stopped. Pause the simulation first.");
        }

        await _serviceManager.ExecuteOneStepAsync();
    }

    #region Entity Management

    /// <summary>
    /// Creates a new entity with specified properties and metadata.
    /// User-facing method for entity creation through the interaction controller.
    /// Orchestrates EntityManager (metadata) and StateManager (storage).
    /// MVP: Also notifies visualization system of entity creation.
    /// </summary>
    /// <param name="name">Human-readable name for the entity</param>
    /// <param name="propertyDefaults">Dictionary of property type → initial value</param>
    /// <param name="description">Optional description of the entity's purpose</param>
    /// <returns>The Entity object created with ID, name, and description</returns>
    public async Task<Entity> CreateEntityAsync(string name, Dictionary<string, object> propertyDefaults, string? description = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Entity name cannot be null or empty", nameof(name));

        if (propertyDefaults == null)
            throw new ArgumentNullException(nameof(propertyDefaults));

        try
        {
            // Step 1: Register the entity in EntityManager (metadata + composition)
            var creationInfo = _entityManager.RegisterNewEntity(name, propertyDefaults.Keys, description);

            // Step 2: Add entries to StateManager's properties
            foreach (var propertyType in propertyDefaults.Keys)
            {
                var initialValue = propertyDefaults[propertyType];

                // Fetch current property array and add the entity's value at the end
                var currentValues = await _stateManager.GetPropertiesByTypeAsync(propertyType) ?? new List<object>();
                currentValues.Add(initialValue);

                // Store updated property array
                await _stateManager.SetPropertiesByTypeAsync(propertyType, currentValues);
            }

            // Step 3: MVP DEPENDENCY - Notify visualization system of new entity
            // This allows external visualization tool to register the entity
            await _stateManager.NotifyEntitiesCreatedAsync(new[] { creationInfo.Entity.Id });

            return creationInfo.Entity;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating entity: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Adds a property to an existing entity.
    /// Orchestrates EntityManager (metadata) and StateManager (storage).
    /// </summary>
    /// <param name="entityId">The entity to modify</param>
    /// <param name="propertyType">The property type to add</param>
    /// <param name="initialValue">Initial value for the property</param>
    public async Task AddPropertyToEntityAsync(int entityId, string propertyType, object initialValue)
    {
        if (string.IsNullOrWhiteSpace(propertyType))
            throw new ArgumentException("Property type cannot be null or empty", nameof(propertyType));

        try
        {
            // Step 1: Get current property array length (this will be the index for this entity)
            var currentValues = await _stateManager.GetPropertiesByTypeAsync(propertyType) ?? new List<object>();
            int propertyIndex = currentValues.Count;

            // Step 2: Add value to StateManager's property array
            currentValues.Add(initialValue);
            await _stateManager.SetPropertiesByTypeAsync(propertyType, currentValues);

            // Step 3: Update EntityManager to track this entity at the new index
            _entityManager.AddPropertyToEntity(entityId, propertyType);

            Console.WriteLine($"Property '{propertyType}' added to Entity {entityId}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error adding property to entity: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Removes a property from an existing entity.
    /// </summary>
    /// <param name="entityId">The entity to modify</param>
    /// <param name="propertyType">The property type to remove</param>
    public void RemovePropertyFromEntity(int entityId, string propertyType)
    {
        try
        {
            _entityManager.RemovePropertyFromEntity(entityId, propertyType);
            Console.WriteLine($"Property '{propertyType}' removed from Entity {entityId}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error removing property from entity: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Gets the list of all existing entities and their compositions.
    /// </summary>
    public void ListAllEntities()
    {
        var entities = _entityManager.GetAllEntities();

        if (entities.Count == 0)
        {
            Console.WriteLine("No entities exist.");
            return;
        }

        Console.WriteLine($"\n╔═══════════════════════════════════════════════╗");
        Console.WriteLine($"║ ENTITIES ({entities.Count} total)");
        Console.WriteLine($"╠═══════════════════════════════════════════════╣");

        foreach (var entity in entities.OrderBy(e => e.Id))
        {
            var composition = _entityManager.GetEntityComposition(entity.Id);
            var properties = string.Join(", ", composition.OrderBy(p => p));
            var description = string.IsNullOrWhiteSpace(entity.Description) ? "(no description)" : entity.Description;
            Console.WriteLine($"║ Entity {entity.Id,3}: {entity.Name} - {description}");
            Console.WriteLine($"║   Properties: [{properties}]");
        }

        Console.WriteLine($"╚═══════════════════════════════════════════════╝\n");
    }

    /// <summary>
    /// Gets detailed information about a specific entity.
    /// </summary>
    /// <param name="entityId">The entity to inspect</param>
    public void InspectEntity(int entityId)
    {
        try
        {
            var entity = _entityManager.GetEntity(entityId);
            if (entity == null)
            {
                Console.WriteLine($"Entity {entityId} does not exist.");
                return;
            }

            var composition = _entityManager.GetEntityComposition(entityId);
            var description = string.IsNullOrWhiteSpace(entity.Description) ? "(no description)" : entity.Description;

            Console.WriteLine($"\n╔═══════════════════════════════════════════════╗");
            Console.WriteLine($"║ ENTITY {entity.Id} DETAILS");
            Console.WriteLine($"╠═══════════════════════════════════════════════╣");
            Console.WriteLine($"║ Name: {entity.Name}");
            Console.WriteLine($"║ Description: {description}");
            Console.WriteLine($"║ Properties ({composition.Count}):");

            foreach (var property in composition.OrderBy(p => p))
            {
                Console.WriteLine($"║   - {property}");
            }

            Console.WriteLine($"╚═══════════════════════════════════════════════╝\n");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error inspecting entity: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Gets the StateManager instance for direct state queries.
    /// Used by test execution logic to report state.
    /// </summary>
    public StateManager GetStateManager()
    {
        return _stateManager;
    }

    #endregion
}