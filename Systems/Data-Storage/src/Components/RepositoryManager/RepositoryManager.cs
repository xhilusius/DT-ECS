namespace DataStorage.RepositoryManager;

using DataStorage.Interfaces;
using DataStorage.StateRepository;
using DataStorage.ArchetypeManager;

/// <summary>
/// Manages repository operations for external requests using the EPS (Entity-Property-Service) pattern.
/// Works with the StateRepository to manage property lists.
/// Uses ArchetypeManager to handle intelligent queries based on service/archetype requirements.
/// </summary>
public class RepositoryManager : IRepositoryManager
{
    private readonly StateRepository _stateRepository;
    private readonly ArchetypeManager _archetypeManager;

    public RepositoryManager()
    {
        _stateRepository = new StateRepository();
        _archetypeManager = new ArchetypeManager();
    }

    public RepositoryManager(StateRepository stateRepository, ArchetypeManager archetypeManager)
    {
        _stateRepository = stateRepository ?? throw new ArgumentNullException(nameof(stateRepository));
        _archetypeManager = archetypeManager ?? throw new ArgumentNullException(nameof(archetypeManager));
    }

    #region Basic Property Type Operations

    public async Task<List<object>?> GetPropertiesByTypeAsync(string propertyType)
    {
        if (string.IsNullOrWhiteSpace(propertyType))
            throw new ArgumentException("Property type cannot be null or empty", nameof(propertyType));

        return await Task.Run(() => _stateRepository.GetPropertiesByType(propertyType));
    }

    public async Task AddPropertyAsync(string propertyType, object propertyValue)
    {
        if (string.IsNullOrWhiteSpace(propertyType))
            throw new ArgumentException("Property type cannot be null or empty", nameof(propertyType));
        if (propertyValue == null)
            throw new ArgumentNullException(nameof(propertyValue));

        await Task.Run(() => _stateRepository.AddProperty(propertyType, propertyValue));
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

    public async Task<Dictionary<string, List<object>>> GetPropertiesForArchetypeAsync(string archetypeId)
    {
        if (string.IsNullOrWhiteSpace(archetypeId))
            throw new ArgumentException("Archetype ID cannot be null or empty", nameof(archetypeId));

        return await Task.Run(() =>
        {
            var archetype = _archetypeManager.GetArchetype(archetypeId);
            if (archetype == null)
                return new Dictionary<string, List<object>>();

            // Fetch properties for all property types in this archetype
            return _stateRepository.GetPropertiesForTypes(archetype.PropertyTypes);
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
            var archetype = new DataStorage.ArchetypeManager.Archetype
            {
                Id = archetypeId,
                Name = archetypeName,
                PropertyTypes = propertyTypes,
                Description = description
            };
            _archetypeManager.RegisterArchetype(archetype);
        });
    }

    public async Task<object?> GetArchetypeManagerAsync()
    {
        return await Task.FromResult(_archetypeManager);
    }

    #endregion
}


