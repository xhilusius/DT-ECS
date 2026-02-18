namespace Simulation.EntityManager;

/// <summary>
/// Manages the mapping between entities and their properties in the StateRepository.
/// 
/// Responsibilities:
/// - Track which array indices correspond to which entities (per property type)
/// - Maintain entity composition (which properties each entity has)
/// - Manage entity creation and removal
/// - Update mappings when entity properties are added/removed
/// - Provide entity metadata queries
/// 
/// NOTE: EntityManager does NOT own StateManager.
/// EntityManager tracks metadata and indices. StateManager handles storage.
/// 
/// How it works:
/// StateRepository stores properties as: {"PropertyType": [value1, value2, value3, ...]}
/// EntityManager maintains per-property lists tracking which entities own each index:
///   _propertyToEntityList["Mass"] = [entityId1, entityId2, entityId3]
///   This means: Mass array has entity1_mass at [0], entity2_mass at [1], entity3_mass at [2]
/// 
/// When an entity's property is removed or added, the list automatically adjusts and indices shift.
/// 
/// Users interact through: InteractionController → EntityManager (metadata) + StateManager (storage)
/// Both must be kept in sync when properties are added/removed.
/// </summary>
public class EntityManager
{
    /// <summary>
    /// Maps each property type to a list of entity IDs that own that property, in order.
    /// Key: PropertyType, Value: List of EntityIds (in array order)
    /// 
    /// Example:
    ///   _propertyToEntityList["Mass"] = [0, 1, 3]
    ///   Means: entity 0 is at index 0, entity 1 is at index 1, entity 3 is at index 2
    /// </summary>
    private readonly Dictionary<string, List<int>> _propertyToEntityList;
    
    /// <summary>
    /// Maps EntityId to the set of properties that entity has.
    /// Key: EntityId, Value: set of property type names
    /// </summary>
    private readonly Dictionary<int, HashSet<string>> _entityComposition;
    
    /// <summary>
    /// Maps EntityId to the Entity object containing metadata (name, description).
    /// Key: EntityId, Value: Entity with ID, Name, Description
    /// </summary>
    private readonly Dictionary<int, Entity> _entityMetadata;
    
    /// <summary>
    /// Counter for assigning unique entity IDs.
    /// Starts at 0, increments for each new entity.
    /// </summary>
    private int _nextEntityId;

    public EntityManager()
    {
        _propertyToEntityList = new Dictionary<string, List<int>>();
        _entityComposition = new Dictionary<int, HashSet<string>>();
        _entityMetadata = new Dictionary<int, Entity>();
        _nextEntityId = 0;
    }

    /// <summary>
    /// Registers a new entity with the specified properties and metadata.
    /// Tracks the entity ID, name, description, and assigns it to each property's list.
    /// The caller must handle creating property entries in StateManager/Repository.
    /// </summary>
    /// <param name="name">Human-readable name for the entity</param>
    /// <param name="propertyTypes">Collection of property type names for this entity</param>
    /// <param name="description">Optional description of the entity's purpose</param>
    /// <returns>EntityCreationInfo containing the Entity object and properties</returns>
    public EntityCreationInfo RegisterNewEntity(string name, IEnumerable<string> propertyTypes, string? description = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Entity name cannot be null or empty", nameof(name));

        if (propertyTypes == null)
            throw new ArgumentNullException(nameof(propertyTypes));

        var properties = propertyTypes.ToList();
        if (properties.Count == 0)
            throw new ArgumentException("Entity must have at least one property", nameof(propertyTypes));

        // Assign new entity ID
        int entityId = _nextEntityId++;

        // Create and store entity metadata
        var entity = new Entity(entityId, name, description)
        {
            Name = name
        };
        _entityMetadata[entityId] = entity;

        // Track entity composition
        _entityComposition[entityId] = new HashSet<string>(properties);

        // Add entity to each property's list (at the end)
        foreach (var propertyType in properties)
        {
            if (!_propertyToEntityList.ContainsKey(propertyType))
            {
                _propertyToEntityList[propertyType] = new List<int>();
            }

            _propertyToEntityList[propertyType].Add(entityId);
        }

        return new EntityCreationInfo
        {
            Entity = entity,
            Properties = new HashSet<string>(properties)
        };
    }

    /// <summary>
    /// Adds a property to an existing entity.
    /// Appends the entity ID to the end of that property's entity list.
    /// Must be called after the property value is added to StateRepository.
    /// </summary>
    /// <param name="entityId">The entity to modify</param>
    /// <param name="propertyType">The property type to add</param>
    public void AddPropertyToEntity(int entityId, string propertyType)
    {
        if (!_entityComposition.ContainsKey(entityId))
            throw new ArgumentException($"Entity {entityId} does not exist", nameof(entityId));

        if (string.IsNullOrWhiteSpace(propertyType))
            throw new ArgumentException("Property type cannot be null or empty", nameof(propertyType));

        // Check if entity already has this property
        if (_entityComposition[entityId].Contains(propertyType))
            throw new InvalidOperationException($"Entity {entityId} already has property {propertyType}");

        // Add to composition tracking
        _entityComposition[entityId].Add(propertyType);

        // Add entity to the property's list (at the end)
        if (!_propertyToEntityList.ContainsKey(propertyType))
        {
            _propertyToEntityList[propertyType] = new List<int>();
        }

        _propertyToEntityList[propertyType].Add(entityId);
    }

    /// <summary>
    /// Removes a property from an entity.
    /// Removes the entity ID from that property's list, causing subsequent entities' indices to shift down.
    /// Must be called AFTER the property value is removed from StateRepository.
    /// </summary>
    /// <param name="entityId">The entity to modify</param>
    /// <param name="propertyType">The property type to remove</param>
    public void RemovePropertyFromEntity(int entityId, string propertyType)
    {
        if (!_entityComposition.ContainsKey(entityId))
            throw new ArgumentException($"Entity {entityId} does not exist", nameof(entityId));

        if (string.IsNullOrWhiteSpace(propertyType))
            throw new ArgumentException("Property type cannot be null or empty", nameof(propertyType));

        if (!_entityComposition[entityId].Contains(propertyType))
            throw new InvalidOperationException($"Entity {entityId} does not have property {propertyType}");

        try
        {
            // Remove from composition
            _entityComposition[entityId].Remove(propertyType);

            // Remove from property's entity list
            if (_propertyToEntityList.ContainsKey(propertyType))
            {
                _propertyToEntityList[propertyType].Remove(entityId);

                // If no entities have this property, remove the property entry
                if (_propertyToEntityList[propertyType].Count == 0)
                {
                    _propertyToEntityList.Remove(propertyType);
                }
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to remove property {propertyType} from entity {entityId}", ex);
        }
    }

    /// <summary>
    /// Gets the array index of an entity within a specific property's array.
    /// Different properties may have different indices for the same entity (if not all entities have all properties).
    /// </summary>
    /// <param name="entityId">The entity to look up</param>
    /// <param name="propertyType">The property type to find index in</param>
    /// <returns>The index in the property array, or -1 if entity doesn't have this property</returns>
    public int GetEntityIndexInProperty(int entityId, string propertyType)
    {
        if (!_propertyToEntityList.ContainsKey(propertyType))
            return -1;

        return _propertyToEntityList[propertyType].IndexOf(entityId);
    }

    /// <summary>
    /// Gets the entity ID at a specific index in a property's array.
    /// </summary>
    /// <param name="propertyType">The property type</param>
    /// <param name="index">The index in the property array</param>
    /// <returns>The entity ID at that index, or -1 if index is out of bounds</returns>
    public int GetEntityIdAtPropertyIndex(string propertyType, int index)
    {
        if (!_propertyToEntityList.ContainsKey(propertyType) || index < 0 || index >= _propertyToEntityList[propertyType].Count)
            return -1;

        return _propertyToEntityList[propertyType][index];
    }

    /// <summary>
    /// Gets all entity IDs in a property's array (in order).
    /// </summary>
    /// <param name="propertyType">The property type</param>
    /// <returns>List of entity IDs that have this property</returns>
    public List<int> GetEntitiesForProperty(string propertyType)
    {
        if (!_propertyToEntityList.ContainsKey(propertyType))
            return new List<int>();

        return new List<int>(_propertyToEntityList[propertyType]);
    }

    /// <summary>
    /// Gets the set of properties that an entity has.
    /// </summary>
    /// <param name="entityId">The entity to look up</param>
    /// <returns>Set of property type names owned by this entity</returns>
    public HashSet<string> GetEntityComposition(int entityId)
    {
        if (!_entityComposition.ContainsKey(entityId))
            throw new ArgumentException($"Entity {entityId} does not exist", nameof(entityId));

        return new HashSet<string>(_entityComposition[entityId]);
    }

    /// <summary>
    /// Checks whether an entity already has a given property.
    /// </summary>
    /// <param name="entityId">The entity to check</param>
    /// <param name="propertyType">The property type to check</param>
    /// <returns>True if the entity has the property, false otherwise</returns>
    public bool EntityHasProperty(int entityId, string propertyType)
    {
        if (!_entityComposition.ContainsKey(entityId))
            throw new ArgumentException($"Entity {entityId} does not exist", nameof(entityId));

        if (string.IsNullOrWhiteSpace(propertyType))
            throw new ArgumentException("Property type cannot be null or empty", nameof(propertyType));

        return _entityComposition[entityId].Contains(propertyType);
    }

    /// <summary>
    /// Gets all entity IDs currently managed.
    /// </summary>
    /// <returns>List of all entity IDs</returns>
    public List<int> GetAllEntityIds()
    {
        return _entityComposition.Keys.ToList();
    }

    /// <summary>
    /// Checks if an entity exists.
    /// </summary>
    /// <param name="entityId">The entity to check</param>
    /// <returns>True if the entity exists, false otherwise</returns>
    public bool EntityExists(int entityId)
    {
        return _entityComposition.ContainsKey(entityId);
    }

    /// <summary>
    /// Gets the total number of entities currently managed.
    /// </summary>
    public int EntityCount => _entityComposition.Count;

    /// <summary>
    /// Gets the Entity metadata (ID, name, description) for a specific entity.
    /// </summary>
    /// <param name="entityId">The entity to look up</param>
    /// <returns>Entity object with metadata, or null if entity doesn't exist</returns>
    public Entity? GetEntity(int entityId)
    {
        return _entityMetadata.TryGetValue(entityId, out var entity) ? entity : null;
    }

    /// <summary>
    /// Gets all Entity objects (with metadata) for all managed entities.
    /// </summary>
    /// <returns>List of all Entity objects</returns>
    public List<Entity> GetAllEntities()
    {
        return new List<Entity>(_entityMetadata.Values);
    }
}