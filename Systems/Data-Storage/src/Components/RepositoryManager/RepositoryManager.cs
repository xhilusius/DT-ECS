namespace DataStorage.RepositoryManager;

using DataStorage.Interfaces;
using DataStorage.StateRepository;
using DataStorage.ArchetypeMapper;
using DataStorage.EntityMapper;

/// <summary>
/// Manages repository operations for external requests using the EPS (Entity-Property-Service) pattern.
/// Works with the StateRepository to manage property lists.
/// Uses ArchetypeMapper to handle intelligent queries based on service/archetype requirements.
/// Uses EntityMapper to track entity metadata and index mappings locally.
/// </summary>
public class RepositoryManager : IRepositoryManager
{
    private readonly StateRepository _stateRepository;
    private readonly ArchetypeMapper _archetypeManager;
    private readonly EntityMapper _entityManager;

    public RepositoryManager()
    {
        _stateRepository = new StateRepository();
        _archetypeManager = new ArchetypeMapper();
        _entityManager = new EntityMapper();
        _entityManager.SetArchetypeMapper(_archetypeManager);
    }

    public RepositoryManager(StateRepository stateRepository, ArchetypeMapper archetypeManager, EntityMapper entityManager)
    {
        _stateRepository = stateRepository ?? throw new ArgumentNullException(nameof(stateRepository));
        _archetypeManager = archetypeManager ?? throw new ArgumentNullException(nameof(archetypeManager));
        _entityManager = entityManager ?? throw new ArgumentNullException(nameof(entityManager));
        _entityManager.SetArchetypeMapper(_archetypeManager);
    }

    /// <summary>
    /// Gets the EntityMapper instance (for queries about entity composition and indices).
    /// </summary>
    public EntityMapper GetEntityMapper()
    {
        return _entityManager;
    }

    /// <summary>
    /// Registers a new entity with the EntityMapper from external subsystem coordination.
    /// Called by StateManager after an entity is created in the Simulation subsystem.
    /// Ensures Data-Storage EntityMapper stays synchronized with entity metadata.
    /// </summary>
    public void SyncEntityRegistration(int entityId, string name, IEnumerable<string> propertyTypes, Dictionary<string, int> propertyIndices, string? description = null)
    {
        _entityManager.RegisterEntityFromSync(entityId, name, propertyTypes, propertyIndices, description);
    }

    /// <summary>
    /// Synchronizes a property addition to an existing entity.
    /// Called by StateManager after a property is added to an entity in the Simulation subsystem.
    /// Ensures Data-Storage EntityMapper stays synchronized with entity composition.
    /// </summary>
    public void SyncPropertyAddition(int entityId, string propertyType, int index)
    {
        _entityManager.AddPropertyToEntityFromSync(entityId, propertyType, index);
    }

    #region Basic Property Type Operations

    public async Task<List<object>?> GetPropertiesByTypeAsync(string propertyType)
    {
        if (string.IsNullOrWhiteSpace(propertyType))
            throw new ArgumentException("Property type cannot be null or empty", nameof(propertyType));

        return await Task.Run(() => _stateRepository.GetPropertiesByType(propertyType));
    }

    public async Task<int> AddPropertyAsync(string propertyType, object propertyValue)
    {
        if (string.IsNullOrWhiteSpace(propertyType))
            throw new ArgumentException("Property type cannot be null or empty", nameof(propertyType));
        if (propertyValue == null)
            throw new ArgumentNullException(nameof(propertyValue));

        return await Task.Run(() => _stateRepository.AddProperty(propertyType, propertyValue));
    }

    public async Task SetPropertiesForTypeAsync(string propertyType, List<object> propertyValues)
    {
        if (string.IsNullOrWhiteSpace(propertyType))
            throw new ArgumentException("Property type cannot be null or empty", nameof(propertyType));
        if (propertyValues == null)
            throw new ArgumentNullException(nameof(propertyValues));

        await Task.Run(() => _stateRepository.SetPropertiesForType(propertyType, propertyValues));
    }

    public async Task<int> DeletePropertiesByTypeAsync(string propertyType)
    {
        if (string.IsNullOrWhiteSpace(propertyType))
            throw new ArgumentException("Property type cannot be null or empty", nameof(propertyType));

        return await Task.Run(() => _stateRepository.DeletePropertiesByType(propertyType));
    }

    public async Task<bool> PropertyTypeExistsAsync(string propertyType)
    {
        if (string.IsNullOrWhiteSpace(propertyType))
            throw new ArgumentException("Property type cannot be null or empty", nameof(propertyType));

        return await Task.Run(() => _stateRepository.PropertyTypeExists(propertyType));
    }

    #endregion

    #region Multiple Property Type Operations

    public async Task<Dictionary<string, List<object>>> GetPropertiesForTypesAsync(IEnumerable<string> propertyTypes)
    {
        if (propertyTypes == null)
            throw new ArgumentNullException(nameof(propertyTypes));

        return await Task.Run(() => _stateRepository.GetPropertiesForTypes(propertyTypes));
    }

    public async Task<List<string>> GetAllPropertyTypesAsync()
    {
        return await Task.Run(() => _stateRepository.GetAllPropertyTypes());
    }

    public async Task<Dictionary<string, List<object>>> GetAllPropertiesAsync()
    {
        return await Task.Run(() => _stateRepository.GetAllProperties());
    }

    #endregion

    #region Query Pattern 1: Property Lists by Service

    public async Task<Dictionary<string, List<object>>> GetPropertiesForServiceAsync(string serviceName)
    {
        if (string.IsNullOrWhiteSpace(serviceName))
            throw new ArgumentException("Service name cannot be null or empty", nameof(serviceName));

        return await Task.Run(() =>
        {
            // Get all archetypes required by this service
            var serviceArchetypes = _archetypeManager.GetServiceArchetypes(serviceName);
            
            if (serviceArchetypes.Count == 0)
                return new Dictionary<string, List<object>>();

            // Collect all unique property types from all service archetypes
            var requiredPropertyTypes = new HashSet<string>();
            foreach (var archetype in serviceArchetypes)
            {
                foreach (var propType in archetype.PropertyTypes)
                {
                    requiredPropertyTypes.Add(propType);
                }
            }

            // Fetch all property lists for these types
            return _stateRepository.GetPropertiesForTypes(requiredPropertyTypes);
        });
    }

    public async Task<Dictionary<string, int>> DeletePropertiesForServiceAsync(string serviceName)
    {
        if (string.IsNullOrWhiteSpace(serviceName))
            throw new ArgumentException("Service name cannot be null or empty", nameof(serviceName));

        return await Task.Run(() =>
        {
            var serviceArchetypes = _archetypeManager.GetServiceArchetypes(serviceName);
            
            if (serviceArchetypes.Count == 0)
                return new Dictionary<string, int>();

            // Collect all unique property types from all service archetypes
            var requiredPropertyTypes = new HashSet<string>();
            foreach (var archetype in serviceArchetypes)
            {
                foreach (var propType in archetype.PropertyTypes)
                {
                    requiredPropertyTypes.Add(propType);
                }
            }

            // Delete all property types for this service
            var result = new Dictionary<string, int>();
            foreach (var propertyType in requiredPropertyTypes)
            {
                int count = _stateRepository.DeletePropertiesByType(propertyType);
                result[propertyType] = count;
            }
            return result;
        });
    }

    #endregion

    #region Query Pattern 2: Property Lists by Archetype

    public async Task<ArchetypeQueryResult> GetPropertiesForArchetypeAsync(string archetypeId)
    {
        if (string.IsNullOrWhiteSpace(archetypeId))
            throw new ArgumentException("Archetype ID cannot be null or empty", nameof(archetypeId));

        return await Task.Run(() =>
        {
            var archetype = _archetypeManager.GetArchetype(archetypeId);
            if (archetype == null)
                return new ArchetypeQueryResult();

            var result = new ArchetypeQueryResult
            {
                Arrays = _stateRepository.GetPropertiesForTypes(archetype.PropertyTypes)
            };

            var indexGroups = _entityManager.GetArchetypeIndexGroups(archetype.Name);
            foreach (var group in indexGroups)
            {
                result.ValidEntityIds.Add(group.EntityId);
                result.EntityToPropertyIndices[group.EntityId] = new Dictionary<string, int>(group.PropertyIndices);
            }

            return result;
        });
    }

    public async Task<ArchetypeQueryResult> GetPropertiesForArchetypeWithOptionalAsync(
        string archetypeId,
        IEnumerable<string>? optionalPropertyTypes)
    {
        if (string.IsNullOrWhiteSpace(archetypeId))
            throw new ArgumentException("Archetype ID cannot be null or empty", nameof(archetypeId));

        return await Task.Run(() =>
        {
            var archetype = _archetypeManager.GetArchetype(archetypeId);
            if (archetype == null)
                return new ArchetypeQueryResult();

            var optionalList = optionalPropertyTypes?.Where(p => !string.IsNullOrWhiteSpace(p))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList() ?? new List<string>();

            var propertyTypes = new HashSet<string>(archetype.PropertyTypes, StringComparer.OrdinalIgnoreCase);
            foreach (var propertyType in optionalList)
            {
                propertyTypes.Add(propertyType);
            }

            var result = new ArchetypeQueryResult
            {
                Arrays = _stateRepository.GetPropertiesForTypes(propertyTypes)
            };

            var indexGroups = _entityManager.GetArchetypeIndexGroupsWithOptional(archetype.Name, optionalList);
            foreach (var group in indexGroups)
            {
                result.ValidEntityIds.Add(group.EntityId);
                result.EntityToPropertyIndices[group.EntityId] = new Dictionary<string, int>(group.PropertyIndices);
            }

            return result;
        });
    }

    public async Task<Dictionary<string, int>> DeletePropertiesForArchetypeAsync(string archetypeId)
    {
        if (string.IsNullOrWhiteSpace(archetypeId))
            throw new ArgumentException("Archetype ID cannot be null or empty", nameof(archetypeId));

        return await Task.Run(() =>
        {
            var archetype = _archetypeManager.GetArchetype(archetypeId);
            if (archetype == null)
                return new Dictionary<string, int>();

            // Delete all property types defined in this archetype
            var result = new Dictionary<string, int>();
            foreach (var propertyType in archetype.PropertyTypes)
            {
                int count = _stateRepository.DeletePropertiesByType(propertyType);
                result[propertyType] = count;
            }
            return result;
        });
    }

    #endregion

    #region Query Pattern 3: Flexible Property Type Requirements

    public async Task<Dictionary<string, List<object>>> GetPropertiesForTypesAsync(params string[] requiredPropertyTypes)
    {
        if (requiredPropertyTypes == null)
            throw new ArgumentNullException(nameof(requiredPropertyTypes));

        return await Task.Run(() => _stateRepository.GetPropertiesForTypes(requiredPropertyTypes));
    }

    public async Task<Dictionary<string, int>> DeletePropertiesForTypesAsync(IEnumerable<string> propertyTypes)
    {
        if (propertyTypes == null)
            throw new ArgumentNullException(nameof(propertyTypes));

        return await Task.Run(() =>
        {
            var result = new Dictionary<string, int>();
            foreach (var propertyType in propertyTypes)
            {
                if (!string.IsNullOrWhiteSpace(propertyType))
                {
                    int count = _stateRepository.DeletePropertiesByType(propertyType);
                    result[propertyType] = count;
                }
            }
            return result;
        });
    }

    #endregion

    #region Archetype Management

    public async Task RegisterArchetypeAsync(string archetypeId, string archetypeName, HashSet<string> propertyTypes, string? description = null)
    {
        if (string.IsNullOrWhiteSpace(archetypeId))
            throw new ArgumentException("Archetype ID cannot be null or empty", nameof(archetypeId));
        if (string.IsNullOrWhiteSpace(archetypeName))
            throw new ArgumentException("Archetype name cannot be null or empty", nameof(archetypeName));
        if (propertyTypes == null || propertyTypes.Count == 0)
            throw new ArgumentException("Archetype must contain at least one property type", nameof(propertyTypes));

        await Task.Run(() =>
        {
            var archetype = new DataStorage.ArchetypeMapper.Archetype
            {
                Id = archetypeId,
                Name = archetypeName,
                PropertyTypes = propertyTypes,
                Description = description
            };
            _archetypeManager.RegisterArchetype(archetype);
            _entityManager.RebuildArchetypeIndexGroups(archetypeName);
        });
    }

    public async Task<object?> GetArchetypeMapperAsync()
    {
        return await Task.FromResult(_archetypeManager);
    }

    /// <summary>
    /// Removes all property values for an entity across all property types.
    /// Removes the entity from the property arrays and updates EntityMapper.
    /// </summary>
    /// <param name="entityId">The entity to remove completely</param>
    public async Task RemoveEntityAsync(int entityId)
    {
        await Task.Run(() =>
        {
            // Get the entity's property composition from EntityMapper
            var properties = _entityManager.GetEntityComposition(entityId);
            
            if (properties == null || properties.Count == 0)
            {
                // Entity doesn't exist or has no properties - just remove from tracking
                _entityManager.RemoveEntity(entityId);
                return;
            }

            // Track which indices were removed for index remapping
            var removedIndices = new Dictionary<string, int>();

            // Remove the entity's values from each property array
            // Process in reverse order to collect removal information
            foreach (var propertyType in properties)
            {
                // Get the index of this entity in this property array
                int index = _entityManager.GetEntityIndexInProperty(entityId, propertyType);
                
                if (index >= 0)
                {
                    // Track the removed index for this property
                    removedIndices[propertyType] = index;
                    
                    // Remove at this index - this shifts subsequent indices down
                    _stateRepository.RemovePropertyAtIndex(propertyType, index);
                }
            }

            // Remove entity from EntityMapper tracking, passing removed indices for remapping
            _entityManager.RemoveEntity(entityId, removedIndices);
        });
    }

    /// <summary>
    /// Removes a property value at a specific index for a property type.
    /// Used when removing an entity's individual properties.
    /// </summary>
    /// <param name="propertyType">The property type to remove from</param>
    /// <param name="entityId">The entity whose property is being removed</param>
    public async Task RemovePropertyAsync(string propertyType, int entityId)
    {
        if (string.IsNullOrWhiteSpace(propertyType))
            throw new ArgumentException("Property type cannot be null or empty", nameof(propertyType));

        await Task.Run(() =>
        {
            // Get the index of this entity in this property array
            int index = _entityManager.GetEntityIndexInProperty(entityId, propertyType);
            
            if (index >= 0)
            {
                // Remove at this index
                _stateRepository.RemovePropertyAtIndex(propertyType, index);
                
                // Update EntityMapper to remove this property from the entity
                // Note: RemovePropertyFromEntity will shift indices in EntityMapper
            }
        });
    }

    /// <summary>
    /// Gets the property indices for a specific entity.
    /// Returns dictionary mapping property types to their indices in property arrays.
    /// </summary>
    public Dictionary<string, int> GetEntityPropertyIndices(int entityId)
    {
        return _entityManager.GetEntityPropertyIndices(entityId);
    }

    /// <summary>
    /// Gets the index of an entity in a specific property's array.
    /// Returns -1 if entity doesn't have this property.
    /// </summary>
    public int GetEntityIndexInProperty(int entityId, string propertyType)
    {
        return _entityManager.GetEntityIndexInProperty(entityId, propertyType);
    }

    #endregion
}


