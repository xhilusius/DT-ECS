namespace DataStorage.EntityMapper;

using DataStorage.ArchetypeMapper;

/// <summary>
/// Data-Storage subsystem's local copy of entity metadata and index mappings.
/// 
/// Purpose:
/// Each subsystem maintains its own EntityMapper to reduce cross-system queries.
/// This allows fast local lookups without querying external systems.
/// 
/// Responsibilities:
/// - Track entity composition (which properties each entity has)
/// - Track entity property indices (which index in each property array belongs to each entity)
/// - Provide fast query methods for entity-aware data lookups
/// - Synchronize with other subsystem EntityMappers when entities are created/modified
/// 
/// Synchronization Flow:
/// 1. Simulation EntityManager creates/modifies entity
/// 2. Simulation system notifies Data-Storage system of the change
/// 3. Data-Storage EntityMapper updates its local state via UpdateFromSync methods
/// 4. Other subsystems receive similar notifications and update their own EntityMappers
/// 
/// Data Structures:
/// - _propertyToEntityList: For each property type, which entities have it (in order)
/// - _entityComposition: For each entity, which properties it has
/// - _entityPropertyIndices: For each entity, which index in each property array belongs to it
/// - _entityMetadata: Entity name, description, and other metadata
/// </summary>
public class EntityMapper
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
    /// Maps each entity to its property indices in the repository.
    /// Key: EntityId, Value: {PropertyType → Index in property array}
    /// This mapping is synchronized from the Simulation EntityManager when entities are created.
    /// </summary>
    private readonly Dictionary<int, Dictionary<string, int>> _entityPropertyIndices;
    
    /// <summary>
    /// Maps EntityId to the Entity object containing metadata (name, description).
    /// Key: EntityId, Value: Entity with ID, Name, Description
    /// </summary>
    private readonly Dictionary<int, Entity> _entityMetadata;

    /// <summary>
    /// For each archetype, maps entityId to its property indices within that archetype.
    /// Key: ArchetypeName, Value: {EntityId -> {PropertyType -> Index}}
    /// </summary>
    private readonly Dictionary<string, Dictionary<int, Dictionary<string, int>>> _archetypeEntityIndices;

    /// <summary>
    /// ArchetypeMapper reference for accessing archetype definitions.
    /// Set by RepositoryManager so the registry lives in the ArchetypeMapper.
    /// </summary>
    private ArchetypeMapper? _archetypeManager;
    
    /// <summary>
    /// Counter for assigning unique entity IDs (tracks the next available ID).
    /// Synchronized with other EntityMappers to ensure consistency.
    /// </summary>
    private int _nextEntityId;

    /// <summary>
    /// Monotonic version for entity composition and index changes.
    /// Incremented whenever entities or their properties change.
    /// </summary>
    private int _compositionVersion;

    private readonly Dictionary<string, Dictionary<int, Dictionary<string, int>>> _optionalMappingCache;
    private int _optionalMappingCacheVersion;

    public EntityMapper()
    {
        _propertyToEntityList = new Dictionary<string, List<int>>();
        _entityComposition = new Dictionary<int, HashSet<string>>();
        _entityPropertyIndices = new Dictionary<int, Dictionary<string, int>>();
        _entityMetadata = new Dictionary<int, Entity>();
        _archetypeEntityIndices = new Dictionary<string, Dictionary<int, Dictionary<string, int>>>();
        _archetypeManager = null;
        _nextEntityId = 0;
        _compositionVersion = 0;
        _optionalMappingCache = new Dictionary<string, Dictionary<int, Dictionary<string, int>>>(StringComparer.OrdinalIgnoreCase);
        _optionalMappingCacheVersion = -1;
    }

    /// <summary>
    /// Represents the index group for a single entity within an archetype.
    /// </summary>
    public sealed class ArchetypeEntityIndexGroup
    {
        public ArchetypeEntityIndexGroup(int entityId, Dictionary<string, int> propertyIndices)
        {
            EntityId = entityId;
            PropertyIndices = propertyIndices ?? throw new ArgumentNullException(nameof(propertyIndices));
        }

        public int EntityId { get; }

        public Dictionary<string, int> PropertyIndices { get; }
    }

    #region Query Methods (Read-Only)

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
    /// Gets the property indices mapping for a specific entity.
    /// Returns which index in each property array belongs to this entity.
    /// </summary>
    /// <param name="entityId">The entity to look up</param>
    /// <returns>Dictionary of propertyType → index in repository, or empty dict if entity not found</returns>
    public Dictionary<string, int> GetEntityPropertyIndices(int entityId)
    {
        if (_entityPropertyIndices.TryGetValue(entityId, out var indices))
        {
            return new Dictionary<string, int>(indices);
        }
        return new Dictionary<string, int>();
    }

    /// <summary>
    /// Gets the array index of an entity within a specific property's array.
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
    /// Checks whether an entity already has a given property.
    /// </summary>
    /// <param name="entityId">The entity to check</param>
    /// <param name="propertyType">The property type to check</param>
    /// <returns>True if the entity has the property, false otherwise</returns>
    public bool EntityHasProperty(int entityId, string propertyType)
    {
        if (!_entityComposition.ContainsKey(entityId))
            return false;

        return _entityComposition[entityId].Contains(propertyType);
    }

    /// <summary>
    /// Gets the total number of entities currently managed.
    /// </summary>
    public int EntityCount => _entityComposition.Count;

    #endregion

    #region Archetype Index Grouping

    /// <summary>
    /// Sets the ArchetypeMapper reference used for archetype definitions.
    /// RepositoryManager should call this after construction.
    /// </summary>
    public void SetArchetypeMapper(ArchetypeMapper archetypeManager)
    {
        if (archetypeManager == null)
            throw new ArgumentNullException(nameof(archetypeManager));

        _archetypeManager = archetypeManager;
        RebuildAllArchetypeIndexGroups();
    }

    /// <summary>
    /// Rebuilds index groups for all registered archetypes.
    /// </summary>
    public void RebuildAllArchetypeIndexGroups()
    {
        _archetypeEntityIndices.Clear();

        if (_archetypeManager == null)
            return;

        foreach (var archetype in _archetypeManager.GetAllArchetypes())
        {
            RebuildArchetypeIndexGroups(archetype);
        }
    }

    /// <summary>
    /// Rebuilds index groups for a specific archetype name.
    /// </summary>
    public void RebuildArchetypeIndexGroups(string archetypeName)
    {
        if (string.IsNullOrWhiteSpace(archetypeName))
            throw new ArgumentException("Archetype name cannot be null or empty", nameof(archetypeName));

        if (_archetypeManager == null)
            return;

        var archetype = _archetypeManager
            .GetAllArchetypes()
            .FirstOrDefault(a => string.Equals(a.Name, archetypeName, StringComparison.Ordinal));

        if (archetype == null)
            return;

        RebuildArchetypeIndexGroups(archetype);
    }

    /// <summary>
    /// Returns the index grouping list for an archetype.
    /// Each item shows which indices across the archetype's properties belong to the same entity.
    /// </summary>
    public List<ArchetypeEntityIndexGroup> GetArchetypeIndexGroups(string archetypeName)
    {
        if (string.IsNullOrWhiteSpace(archetypeName))
            throw new ArgumentException("Archetype name cannot be null or empty", nameof(archetypeName));

        if (!_archetypeEntityIndices.ContainsKey(archetypeName))
        {
            RebuildArchetypeIndexGroups(archetypeName);
        }

        if (!_archetypeEntityIndices.TryGetValue(archetypeName, out var entityMap))
            return new List<ArchetypeEntityIndexGroup>();

        return entityMap
            .OrderBy(kvp => kvp.Key)
            .Select(kvp => new ArchetypeEntityIndexGroup(kvp.Key, new Dictionary<string, int>(kvp.Value)))
            .ToList();
    }

    /// <summary>
    /// Returns the index grouping list for an archetype plus optional properties.
    /// Optional properties are included when present on an entity and do not affect membership.
    /// </summary>
    public List<ArchetypeEntityIndexGroup> GetArchetypeIndexGroupsWithOptional(
        string archetypeName,
        IEnumerable<string>? optionalPropertyTypes)
    {
        if (string.IsNullOrWhiteSpace(archetypeName))
            throw new ArgumentException("Archetype name cannot be null or empty", nameof(archetypeName));

        var optionalList = optionalPropertyTypes?.Where(p => !string.IsNullOrWhiteSpace(p))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? new List<string>();

        if (!_archetypeEntityIndices.ContainsKey(archetypeName))
        {
            RebuildArchetypeIndexGroups(archetypeName);
        }

        if (!_archetypeEntityIndices.TryGetValue(archetypeName, out var requiredMap))
            return new List<ArchetypeEntityIndexGroup>();

        if (optionalList.Count == 0)
        {
            return requiredMap
                .OrderBy(kvp => kvp.Key)
                .Select(kvp => new ArchetypeEntityIndexGroup(kvp.Key, new Dictionary<string, int>(kvp.Value)))
                .ToList();
        }

        if (_compositionVersion != _optionalMappingCacheVersion)
        {
            _optionalMappingCache.Clear();
            _optionalMappingCacheVersion = _compositionVersion;
        }

        var cacheKey = BuildOptionalCacheKey(archetypeName, optionalList);
        if (!_optionalMappingCache.TryGetValue(cacheKey, out var cachedMapping))
        {
            cachedMapping = CloneMapping(requiredMap);

            foreach (var entityId in requiredMap.Keys)
            {
                if (!_entityPropertyIndices.TryGetValue(entityId, out var allIndices))
                    continue;

                foreach (var propertyType in optionalList)
                {
                    if (allIndices.TryGetValue(propertyType, out var index))
                    {
                        if (!cachedMapping.ContainsKey(entityId))
                        {
                            cachedMapping[entityId] = new Dictionary<string, int>();
                        }

                        cachedMapping[entityId][propertyType] = index;
                    }
                }
            }

            _optionalMappingCache[cacheKey] = CloneMapping(cachedMapping);
        }

        return cachedMapping
            .OrderBy(kvp => kvp.Key)
            .Select(kvp => new ArchetypeEntityIndexGroup(kvp.Key, new Dictionary<string, int>(kvp.Value)))
            .ToList();
    }

    private void RebuildArchetypeIndexGroups(Archetype archetype)
    {
        var requiredProps = archetype.PropertyTypes;

        var entityMap = new Dictionary<int, Dictionary<string, int>>();

        foreach (var entityId in _entityComposition.Keys)
        {
            if (!EntityHasAllProperties(entityId, requiredProps))
                continue;

            var indices = new Dictionary<string, int>();
            foreach (var propertyType in requiredProps)
            {
                indices[propertyType] = _entityPropertyIndices[entityId][propertyType];
            }

            entityMap[entityId] = indices;
        }

        _archetypeEntityIndices[archetype.Name] = entityMap;
    }

    private void UpdateArchetypeMembershipForEntity(int entityId)
    {
        if (_archetypeManager == null)
            return;

        foreach (var archetype in _archetypeManager.GetAllArchetypes())
        {
            var archetypeName = archetype.Name;
            var requiredProps = archetype.PropertyTypes;

            if (!_archetypeEntityIndices.ContainsKey(archetypeName))
            {
                _archetypeEntityIndices[archetypeName] = new Dictionary<int, Dictionary<string, int>>();
            }

            if (EntityHasAllProperties(entityId, requiredProps))
            {
                var indices = new Dictionary<string, int>();
                foreach (var propertyType in requiredProps)
                {
                    indices[propertyType] = _entityPropertyIndices[entityId][propertyType];
                }

                _archetypeEntityIndices[archetypeName][entityId] = indices;
            }
            else
            {
                _archetypeEntityIndices[archetypeName].Remove(entityId);
            }
        }
    }

    private static string BuildOptionalCacheKey(string archetypeName, List<string> optionalList)
    {
        var ordered = optionalList
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase);

        return $"{archetypeName}|{string.Join(",", ordered)}";
    }

    private static Dictionary<int, Dictionary<string, int>> CloneMapping(
        Dictionary<int, Dictionary<string, int>> source)
    {
        var clone = new Dictionary<int, Dictionary<string, int>>();
        foreach (var kvp in source)
        {
            clone[kvp.Key] = new Dictionary<string, int>(kvp.Value);
        }

        return clone;
    }

    private bool EntityHasAllProperties(int entityId, HashSet<string> requiredProps)
    {
        if (!_entityComposition.ContainsKey(entityId))
            return false;

        if (!_entityPropertyIndices.ContainsKey(entityId))
            return false;

        foreach (var propertyType in requiredProps)
        {
            if (!_entityComposition[entityId].Contains(propertyType))
                return false;
            if (!_entityPropertyIndices[entityId].ContainsKey(propertyType))
                return false;
        }

        return true;
    }

    #endregion

    #region Synchronization Methods (Called by other systems)

    /// <summary>
    /// Registers a new entity from a synchronization update.
    /// Called when another subsystem (e.g., Simulation) creates an entity.
    /// This updates the local copy of entity metadata.
    /// </summary>
    /// <param name="entityId">The entity ID</param>
    /// <param name="name">Human-readable name for the entity</param>
    /// <param name="propertyTypes">Collection of property type names for this entity</param>
    /// <param name="propertyIndices">Dictionary of propertyType → index in repository</param>
    /// <param name="description">Optional description of the entity's purpose</param>
    public void RegisterEntityFromSync(int entityId, string name, IEnumerable<string> propertyTypes, Dictionary<string, int> propertyIndices, string? description = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Entity name cannot be null or empty", nameof(name));
        if (propertyTypes == null)
            throw new ArgumentNullException(nameof(propertyTypes));
        if (propertyIndices == null)
            throw new ArgumentNullException(nameof(propertyIndices));

        try
        {
            var properties = propertyTypes.ToList();
            if (properties.Count == 0)
                throw new ArgumentException("Entity must have at least one property", nameof(propertyTypes));

            // Update next entity ID to be at least entityId + 1
            if (entityId >= _nextEntityId)
            {
                _nextEntityId = entityId + 1;
            }

            // Create and store entity metadata
            var entity = new Entity(entityId, name, description);
            _entityMetadata[entityId] = entity;

            // Track entity composition
            _entityComposition[entityId] = new HashSet<string>(properties);

            // Store property indices for this entity
            _entityPropertyIndices[entityId] = new Dictionary<string, int>(propertyIndices);

            // Add entity to each property's list (at the position indicated by its index)
            foreach (var propertyType in properties)
            {
                if (!_propertyToEntityList.ContainsKey(propertyType))
                {
                    _propertyToEntityList[propertyType] = new List<int>();
                }

                int index = propertyIndices[propertyType];
                
                // Ensure the list is large enough
                while (_propertyToEntityList[propertyType].Count <= index)
                {
                    _propertyToEntityList[propertyType].Add(-1); // Placeholder
                }

                _propertyToEntityList[propertyType][index] = entityId;
            }

            UpdateArchetypeMembershipForEntity(entityId);
            _compositionVersion++;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to register entity {entityId} from synchronization", ex);
        }
    }

    /// <summary>
    /// Adds a property to an existing entity from a synchronization update.
    /// Called when another subsystem adds a property to an existing entity.
    /// </summary>
    /// <param name="entityId">The entity to modify</param>
    /// <param name="propertyType">The property type to add</param>
    /// <param name="index">The index where this property was placed in the repository</param>
    public void AddPropertyToEntityFromSync(int entityId, string propertyType, int index)
    {
        if (!_entityComposition.ContainsKey(entityId))
            throw new ArgumentException($"Entity {entityId} does not exist", nameof(entityId));

        if (string.IsNullOrWhiteSpace(propertyType))
            throw new ArgumentException("Property type cannot be null or empty", nameof(propertyType));

        try
        {
            // Add to composition tracking
            _entityComposition[entityId].Add(propertyType);

            // Store the property index for this entity
            if (!_entityPropertyIndices.ContainsKey(entityId))
            {
                _entityPropertyIndices[entityId] = new Dictionary<string, int>();
            }
            _entityPropertyIndices[entityId][propertyType] = index;

            // Add entity to the property's list at the specified index
            if (!_propertyToEntityList.ContainsKey(propertyType))
            {
                _propertyToEntityList[propertyType] = new List<int>();
            }

            // Ensure the list is large enough
            while (_propertyToEntityList[propertyType].Count <= index)
            {
                _propertyToEntityList[propertyType].Add(-1); // Placeholder
            }

            _propertyToEntityList[propertyType][index] = entityId;

            UpdateArchetypeMembershipForEntity(entityId);
            _compositionVersion++;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to add property {propertyType} to entity {entityId} from synchronization", ex);
        }
    }

    /// <summary>
    /// Updates the property index mapping when a property's index changes.
    /// Used if synchronization updates need to modify index information.
    /// </summary>
    /// <param name="entityId">The entity to update</param>
    /// <param name="propertyType">The property type</param>
    /// <param name="newIndex">The new index in the property array</param>
    public void UpdatePropertyIndex(int entityId, string propertyType, int newIndex)
    {
        if (!_entityComposition.ContainsKey(entityId))
            throw new ArgumentException($"Entity {entityId} does not exist", nameof(entityId));

        if (!_entityComposition[entityId].Contains(propertyType))
            throw new ArgumentException($"Entity {entityId} does not have property {propertyType}", nameof(propertyType));

        try
        {
            // Update the index mapping
            _entityPropertyIndices[entityId][propertyType] = newIndex;

            // Update the property list if needed
            if (_propertyToEntityList.ContainsKey(propertyType))
            {
                // Find and remove the old entry
                var oldIndex = _propertyToEntityList[propertyType].IndexOf(entityId);
                if (oldIndex >= 0)
                {
                    _propertyToEntityList[propertyType][oldIndex] = -1; // Mark as empty
                }

                // Ensure the list is large enough
                while (_propertyToEntityList[propertyType].Count <= newIndex)
                {
                    _propertyToEntityList[propertyType].Add(-1); // Placeholder
                }

                _propertyToEntityList[propertyType][newIndex] = entityId;
            }

            UpdateArchetypeMembershipForEntity(entityId);
            _compositionVersion++;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to update property index for entity {entityId}", ex);
        }
    }

    /// <summary>
    /// Removes an entity from the EntityMapper tracking.
    /// Cleans up all mappings for the removed entity.
    /// </summary>
    /// <param name="entityId">The entity to remove</param>
    /// <summary>
    /// Removes an entity and updates indices for all remaining entities.
    /// This is called after StateRepository has already removed the values and shifted indices.
    /// </summary>
    public void RemoveEntity(int entityId, Dictionary<string, int>? removedIndices = null)
    {
        try
        {
            // Get the entity's properties before removing them
            var properties = _entityComposition.TryGetValue(entityId, out var props) ? props : new HashSet<string>();

            // Update indices for all remaining entities BEFORE removing the entity
            // When an entity is removed from an array at index i, all entities with index > i need to decrement by 1
            if (removedIndices != null && removedIndices.Count > 0)
            {
                foreach (var entityEntry in _entityPropertyIndices.Where(kvp => kvp.Key != entityId).ToList())
                {
                    var otherEntityId = entityEntry.Key;
                    var otherIndices = entityEntry.Value;

                    foreach (var propertyType in removedIndices.Keys)
                    {
                        int removedIndex = removedIndices[propertyType];

                        // If this entity has this property and its index is > removed index, decrement it
                        if (otherIndices.TryGetValue(propertyType, out int currentIndex))
                        {
                            // If current index > removed index, it shifted down by 1
                            if (currentIndex > removedIndex)
                            {
                                otherIndices[propertyType] = currentIndex - 1;
                            }
                        }
                    }
                }
            }

            // Remove from metadata
            _entityMetadata.Remove(entityId);

            // Remove from composition tracking
            if (_entityComposition.TryGetValue(entityId, out var compositionProperties))
            {
                // Remove entity from each property's list
                foreach (var propertyType in compositionProperties)
                {
                    if (_propertyToEntityList.TryGetValue(propertyType, out var entityList))
                    {
                        entityList.Remove(entityId);
                        
                        // If no entities have this property, remove the property entry
                        if (entityList.Count == 0)
                        {
                            _propertyToEntityList.Remove(propertyType);
                        }
                    }
                }

                _entityComposition.Remove(entityId);
            }

            // Remove property indices mapping
            _entityPropertyIndices.Remove(entityId);

            // Remove from archetype indices
            foreach (var archetypeIndices in _archetypeEntityIndices.Values)
            {
                archetypeIndices.Remove(entityId);
            }

            _compositionVersion++;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to remove entity {entityId}", ex);
        }
    }

    #endregion
}
