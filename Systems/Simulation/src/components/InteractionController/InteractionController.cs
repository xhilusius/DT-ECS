namespace Simulation.InteractionController;

using Simulation.EntityManager;
using Simulation.Interfaces;
using Simulation.ServiceManager.CompositeServices;

/// <summary>
/// Entry point for user interaction with the Simulation subsystem.
/// Acts as the "great overseer": owns the top-level TestExecutorService, the
/// CancellationTokenSource, and the PauseHandle that propagate to every inner level.
///
/// Controls:
/// - RunAsync  — starts TestExecutorService, awaits completion
/// - Pause / Continue — suspend/resume between steps at all levels
/// - StopAsync — cancels execution at any depth
///
/// Entity management methods operate on the outer entity store.
/// Does not hold a ServiceManager directly; that is owned by TestExecutorService.
/// </summary>
public class InteractionController : IInteractionController
{
    private readonly EntityManager _entityManager;
    private readonly TestExecutorService _executorService;
    private readonly CancellationTokenSource _cts = new();
    private readonly PauseHandle _pauseHandle = new();

    public InteractionController(EntityManager entityManager, TestExecutorService executorService)
    {
        _entityManager   = entityManager   ?? throw new ArgumentNullException(nameof(entityManager));
        _executorService = executorService ?? throw new ArgumentNullException(nameof(executorService));
    }

    /// <inheritdoc/>
    public async Task RunAsync()
    {
        await _executorService.RunAsync(_cts.Token, _pauseHandle);
    }

    /// <summary>
    /// Gets the EntityManager for accessing entity metadata and state coordination.
    /// </summary>
    public EntityManager GetEntityManager()
    {
        return _entityManager;
    }

    /// <inheritdoc/>
    public void Pause()
    {
        _pauseHandle.Pause();
        Console.WriteLine("Simulation paused.");
    }

    /// <inheritdoc/>
    public void Continue()
    {
        _pauseHandle.Resume();
        Console.WriteLine("Simulation resumed.");
    }

    /// <inheritdoc/>
    public Task StopAsync()
    {
        _cts.Cancel();
        return Task.CompletedTask;
    }

    #region Entity Management

    /// <summary>
    /// Creates a new entity with specified properties and metadata.
    /// Orchestrates through EntityManager for all entity and state management.
    /// EntityManager coordinates with StateManager for property storage and visualization.
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
            // EntityManager coordinates all entity creation and state initialization
            return await _entityManager.RegisterNewEntityWithStateAsync(name, propertyDefaults, description);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating entity: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Adds a property to an existing entity.
    /// Orchestrates through EntityManager for all entity and state management.
    /// EntityManager coordinates with StateManager for property storage.
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
            // EntityManager coordinates all property addition and state management
            await _entityManager.AddPropertyToEntityWithStateAsync(entityId, propertyType, initialValue);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error adding property to entity: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Removes a property from an existing entity.
    /// Orchestrates through EntityManager for all entity management.
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
    /// Removes an entity completely from the simulation.
    /// Coordinates through EntityManager for all entity management and state cleanup.
    /// Frees the entity name for reuse by subsequent entities.
    /// </summary>
    /// <param name="entityId">The entity to remove</param>
    public async Task RemoveEntityAsync(int entityId)
    {
        try
        {
            await _entityManager.RemoveEntityWithStateAsync(entityId);
            Console.WriteLine($"Entity {entityId} has been removed from the simulation");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error removing entity: {ex.Message}");
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

    #endregion
}