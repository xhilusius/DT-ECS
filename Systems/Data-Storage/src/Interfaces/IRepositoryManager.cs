namespace DataStorage.Interfaces;

using DataStorage.RepositoryManager;

/// <summary>
/// Interface for managing repository operations using the EPS (Entity-Property-Service) pattern.
/// The repository stores properties organized by property type as arrays.
/// The manager handles intelligent queries using the ArchetypeMapper to fulfill service requirements.
/// </summary>
public interface IRepositoryManager
{
    #region Basic Property Type Operations

    /// <summary>
    /// Gets all properties of a specific type as a list.
    /// Returns null if the property type doesn't exist.
    /// </summary>
    Task<List<object>?> GetPropertiesByTypeAsync(string propertyType);

    /// <summary>
    /// Adds a single property value to the list for a property type.
    /// Returns the index where the property was added.
    /// </summary>
    Task<int> AddPropertyAsync(string propertyType, object propertyValue);

    /// <summary>
    /// Replaces the entire list of properties for a property type.
    /// </summary>
    Task SetPropertiesForTypeAsync(string propertyType, List<object> propertyValues);

    /// <summary>
    /// Deletes all properties of a specific property type.
    /// Returns the count of deleted properties.
    /// </summary>
    Task<int> DeletePropertiesByTypeAsync(string propertyType);

    /// <summary>
    /// Checks if a property type exists.
    /// </summary>
    Task<bool> PropertyTypeExistsAsync(string propertyType);

    #endregion

    #region Multiple Property Type Operations

    /// <summary>
    /// Gets properties for multiple property types.
    /// Returns a dictionary mapping property type to its list of values.
    /// </summary>
    Task<Dictionary<string, List<object>>> GetPropertiesForTypesAsync(IEnumerable<string> propertyTypes);

    /// <summary>
    /// Gets all property types currently in the repository.
    /// </summary>
    Task<List<string>> GetAllPropertyTypesAsync();

    /// <summary>
    /// Gets all properties organized by property type.
    /// </summary>
    Task<Dictionary<string, List<object>>> GetAllPropertiesAsync();

    #endregion

    #region Query Pattern 1: Property Lists by Service

    /// <summary>
    /// Query Pattern 1: Gets all property lists required by a service.
    /// The service's archetypes define which property types are needed.
    /// Returns a dictionary mapping property type to its list of values.
    /// </summary>
    Task<Dictionary<string, List<object>>> GetPropertiesForServiceAsync(string serviceName);

    /// <summary>
    /// Deletes all properties of types required by a service.
    /// Returns a dictionary mapping property type to count of deleted properties.
    /// </summary>
    Task<Dictionary<string, int>> DeletePropertiesForServiceAsync(string serviceName);

    #endregion

    #region Query Pattern 2: Property Lists by Archetype

    /// <summary>
    /// Query Pattern 2: Gets all property lists defined in a specific archetype.
    /// Returns property arrays and entity-to-index mappings for the archetype.
    /// </summary>
    Task<ArchetypeQueryResult> GetPropertiesForArchetypeAsync(string archetypeId);

    /// <summary>
    /// Query Pattern 2: Gets all property lists defined in a specific archetype plus optional properties.
    /// Returns property arrays and entity-to-index mappings for the archetype with optional indices when present.
    /// </summary>
    Task<ArchetypeQueryResult> GetPropertiesForArchetypeWithOptionalAsync(
        string archetypeId,
        IEnumerable<string>? optionalPropertyTypes);

    /// <summary>
    /// Deletes all properties of types defined in an archetype.
    /// Returns a dictionary mapping property type to count of deleted properties.
    /// </summary>
    Task<Dictionary<string, int>> DeletePropertiesForArchetypeAsync(string archetypeId);

    #endregion

    #region Query Pattern 3: Property Lists by Flexible Requirements

    /// <summary>
    /// Query Pattern 3: Gets property lists for a flexible set of property types.
    /// Returns a dictionary mapping property type to its list of values.
    /// </summary>
    Task<Dictionary<string, List<object>>> GetPropertiesForTypesAsync(params string[] requiredPropertyTypes);

    /// <summary>
    /// Deletes all properties of specified types.
    /// Returns a dictionary mapping property type to count of deleted properties.
    /// </summary>
    Task<Dictionary<string, int>> DeletePropertiesForTypesAsync(IEnumerable<string> propertyTypes);

    #endregion

    #region Archetype Management

    /// <summary>
    /// Registers an archetype with the archetype mapper.
    /// Archetypes define property combinations required by services.
    /// </summary>
    Task RegisterArchetypeAsync(string archetypeId, string archetypeName, HashSet<string> propertyTypes, string? description = null);

    /// <summary>
    /// Gets access to the archetype mapper for registering service requirements.
    /// </summary>
    Task<object?> GetArchetypeMapperAsync();

    #endregion

    #region Cross-Subsystem Synchronization

    /// <summary>
    /// Synchronizes a new entity registration from the Simulation subsystem.
    /// Updates the Data-Storage EntityMapper with entity metadata and property indices.
    /// </summary>
    void SyncEntityRegistration(int entityId, string name, IEnumerable<string> propertyTypes, Dictionary<string, int> propertyIndices, string? description = null);

    /// <summary>
    /// Synchronizes a property addition to an entity from the Simulation subsystem.
    /// Updates the Data-Storage EntityMapper with the new property and its index.
    /// </summary>
    void SyncPropertyAddition(int entityId, string propertyType, int index);

    #endregion
}


