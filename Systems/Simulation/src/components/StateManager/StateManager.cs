namespace Simulation.StateManager;

using System.Numerics;
using DataStorage.Interfaces;
using Simulation.EntityManager;

/// <summary>
/// Manages all state access and reporting for the simulation.
/// Acts as an intermediary between SimEngine and the underlying RepositoryManager.
/// Queries EntityManager to provide entity-aware property mappings.
/// 
/// Responsibilities:
/// - Fetch properties by type for SimEngine
/// - Provide entity-aware property arrays (which indices belong to same entity)
/// - Store properties by type when services complete execution
/// - Report current state in a formatted table
/// - Clear all properties when resetting
/// - Handle all storage operations (SimEngine doesn't access repository directly)
/// - Query EntityManager to map array indices to entities
/// </summary>
public class StateManager
{
    private readonly IRepositoryManager _repositoryManager;
    private readonly EntityManager _entityManager;
    private Dictionary<string, string> _propertyUnits = new();
    private HashSet<string>? _alwaysShowProperties;
    private HashSet<string>? _showOnceProperties;
    private HashSet<string>? _intermediateProperties;
    private bool _isFirstReport = true;

    public StateManager(IRepositoryManager repositoryManager, EntityManager entityManager)
    {
        if (repositoryManager == null)
            throw new ArgumentNullException(nameof(repositoryManager));
        if (entityManager == null)
            throw new ArgumentNullException(nameof(entityManager));

        _repositoryManager = repositoryManager;
        _entityManager = entityManager;
    }

    /// <summary>
    /// Gets the RepositoryManager for direct access to properties.
    /// Primarily used for advanced queries or inspection.
    /// </summary>
    public IRepositoryManager GetRepositoryManager()
    {
        return _repositoryManager;
    }

    /// <summary>
    /// Fetches all property values for a specific property type.
    /// SimEngine uses this to retrieve input data for services.
    /// </summary>
    public async Task<List<object>?> GetPropertiesByTypeAsync(string propertyType)
    {
        if (string.IsNullOrWhiteSpace(propertyType))
            throw new ArgumentException("Property type cannot be null or empty", nameof(propertyType));

        try
        {
            return await _repositoryManager.GetPropertiesByTypeAsync(propertyType);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to get properties of type '{propertyType}'", ex);
        }
    }

    /// <summary>
    /// Fetches property arrays for a specific archetype and returns entity-aware mapping.
    /// Delegates archetype resolution to RepositoryManager, then maps entities using EntityManager.
    /// Critical for SimEngine when executing a service with a specific archetype.
    /// 
    /// Flow:
    /// 1. Query RepositoryManager for archetype definition (e.g., "GravityModel" → [Mass, CurrentSpeed, Radius])
    /// 2. RepositoryManager queries ArchetypeManager internally to resolve the archetype
    /// 3. RepositoryManager returns property arrays for all required properties
    /// 4. Query EntityManager to find which entities have ALL these properties
    /// 5. Return arrays and entity-to-index mappings
    /// </summary>
    /// <param name="archetypeName">The archetype name/ID (e.g., "GravityModel", "PositionModel")</param>
    /// <returns>Bundle containing arrays and entity IDs that have ALL properties for this archetype</returns>
    public async Task<PropertyArrayBundle> GetPropertiesByArchetypeAsync(string archetypeName)
    {
        if (string.IsNullOrWhiteSpace(archetypeName))
            throw new ArgumentException("Archetype name cannot be null or empty", nameof(archetypeName));

        try
        {
            // Step 1: Query RepositoryManager for archetype properties
            // RepositoryManager internally queries ArchetypeManager to resolve the archetype definition
            var arrays = await _repositoryManager.GetPropertiesForArchetypeAsync(archetypeName);

            if (arrays == null || arrays.Count == 0)
                throw new InvalidOperationException($"Archetype '{archetypeName}' not found or has no properties");

            var propertyTypes = arrays.Keys.ToList();

            // Step 2: Query EntityManager to find entities that have ALL required properties
            var validEntityIds = new List<int>();
            var entityToPropertyIndices = new Dictionary<int, Dictionary<string, int>>();
            var allEntityIds = _entityManager.GetAllEntityIds();

            foreach (var entityId in allEntityIds)
            {
                var composition = _entityManager.GetEntityComposition(entityId);
                
                // Check if entity has all required properties
                if (propertyTypes.All(pt => composition.Contains(pt)))
                {
                    validEntityIds.Add(entityId);
                    
                    // Build index mapping for this entity across all properties
                    var indices = new Dictionary<string, int>();
                    foreach (var propertyType in propertyTypes)
                    {
                        int index = _entityManager.GetEntityIndexInProperty(entityId, propertyType);
                        indices[propertyType] = index;
                    }
                    entityToPropertyIndices[entityId] = indices;
                }
            }

            return new PropertyArrayBundle
            {
                Arrays = arrays,
                ValidEntityIds = validEntityIds,
                EntityToPropertyIndices = entityToPropertyIndices
            };
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to get properties for archetype '{archetypeName}'", ex);
        }
    }

    /// <summary>
    /// Fetches multiple property arrays and returns entity-aware mapping.
    /// Internal method for fetching raw property arrays.
    /// Critical for SimEngine when a service requires multiple properties.
    /// 
    /// Returns both the property arrays AND the list of valid entity IDs.
    /// A valid entity ID is one that has ALL the required properties.
    /// </summary>
    /// <param name="propertyTypes">The property types to fetch</param>
    /// <returns>Bundle containing arrays and entity IDs that have ALL properties</returns>
    private async Task<PropertyArrayBundle> GetPropertiesByTypesAsync(IEnumerable<string> propertyTypes)
    {
        if (propertyTypes == null)
            throw new ArgumentNullException(nameof(propertyTypes));

        var propertyTypesList = propertyTypes.ToList();
        if (propertyTypesList.Count == 0)
            throw new ArgumentException("At least one property type must be specified", nameof(propertyTypes));

        try
        {
            // Fetch all property arrays
            var arrays = new Dictionary<string, List<object>>();
            foreach (var propertyType in propertyTypesList)
            {
                var values = await _repositoryManager.GetPropertiesByTypeAsync(propertyType);
                arrays[propertyType] = values ?? new List<object>();
            }

            // Query EntityManager to find entities that have ALL required properties
            var validEntityIds = new List<int>();
            var entityToPropertyIndices = new Dictionary<int, Dictionary<string, int>>();
            var allEntityIds = _entityManager.GetAllEntityIds();

            foreach (var entityId in allEntityIds)
            {
                var composition = _entityManager.GetEntityComposition(entityId);
                
                // Check if entity has all required properties
                if (propertyTypesList.All(pt => composition.Contains(pt)))
                {
                    validEntityIds.Add(entityId);
                    
                    // Build index mapping for this entity across all properties
                    var indices = new Dictionary<string, int>();
                    foreach (var propertyType in propertyTypesList)
                    {
                        int index = _entityManager.GetEntityIndexInProperty(entityId, propertyType);
                        indices[propertyType] = index;
                    }
                    entityToPropertyIndices[entityId] = indices;
                }
            }

            return new PropertyArrayBundle
            {
                Arrays = arrays,
                ValidEntityIds = validEntityIds,
                EntityToPropertyIndices = entityToPropertyIndices
            };
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to get properties for types: {string.Join(", ", propertyTypesList)}", ex);
        }
    }

    /// <summary>
    /// Stores property values for a specific property type.
    /// SimEngine uses this to write output data from services back to storage.
    /// </summary>
    public async Task SetPropertiesByTypeAsync(string propertyType, List<object> propertyValues)
    {
        if (string.IsNullOrWhiteSpace(propertyType))
            throw new ArgumentException("Property type cannot be null or empty", nameof(propertyType));
        if (propertyValues == null)
            throw new ArgumentNullException(nameof(propertyValues));

        try
        {
            await _repositoryManager.SetPropertiesForTypeAsync(propertyType, propertyValues);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to set properties of type '{propertyType}'", ex);
        }
    }

    /// <summary>
    /// Ensures that each entity in the list is registered as owning the given property.
    /// Used when a service produces an output property that may not yet exist on entities.
    /// </summary>
    /// <param name="entityIds">Entities that should own the property</param>
    /// <param name="propertyType">The output property type</param>
    public void EnsureEntitiesHaveProperty(IEnumerable<int> entityIds, string propertyType)
    {
        if (entityIds == null)
            throw new ArgumentNullException(nameof(entityIds));
        if (string.IsNullOrWhiteSpace(propertyType))
            throw new ArgumentException("Property type cannot be null or empty", nameof(propertyType));

        foreach (var entityId in entityIds)
        {
            if (!_entityManager.EntityHasProperty(entityId, propertyType))
            {
                _entityManager.AddPropertyToEntity(entityId, propertyType);
            }
        }
    }

    /// <summary>
    /// Clears all properties from the repository.
    /// Used to reset the state to a clean slate.
    /// </summary>
    public async Task ClearAllPropertiesAsync()
    {
        if (_repositoryManager == null)
            throw new InvalidOperationException("RepositoryManager not available");

        try
        {
            var allProperties = await _repositoryManager.GetAllPropertiesAsync();
            var propertyTypes = allProperties.Keys.ToList();

            foreach (var propertyType in propertyTypes)
            {
                await _repositoryManager.DeletePropertiesByTypeAsync(propertyType);
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to clear all properties", ex);
        }
    }

    /// <summary>
    /// Displays the current state organized by entity.
    /// Shows each entity's properties and their current values.
    /// Called after each simulation step to show results.
    /// Uses EntityManager to determine which array elements belong to each entity.
    /// </summary>
    public async Task ReportStateAsync(string stepDescription = "Simulation Step")
    {
        try
        {
            var allProperties = await _repositoryManager.GetAllPropertiesAsync();
            var allEntityIds = _entityManager.GetAllEntityIds();

            Console.WriteLine();
            Console.WriteLine($"╔═══════════════════════════════════════════════════════════════╗");
            Console.WriteLine($"║ STATE REPORT - {stepDescription,-47}║");
            Console.WriteLine($"╠═══════════════════════════════════════════════════════════════╣");

            if (allEntityIds.Count == 0)
            {
                Console.WriteLine($"║ (No entities created)                                          ║");
            }
            else
            {
                foreach (var entityId in allEntityIds)
                {
                    var entity = _entityManager.GetEntity(entityId);
                    var composition = _entityManager.GetEntityComposition(entityId);
                    var name = entity?.Name ?? "(unnamed)";

                    Console.WriteLine($"║ Entity {entityId,2}: {name,-51}║");
                    Console.WriteLine($"╠═══════════════════════════════════════════════════════════════╣");

                    if (composition.Count == 0)
                    {
                        Console.WriteLine($"║   (no properties)                                              ║");
                    }
                    else
                    {
                        foreach (var propertyType in composition.OrderBy(p => p))
                        {
                            // Skip properties that shouldn't be displayed based on visibility settings
                            if (!ShouldDisplayProperty(propertyType))
                                continue;

                            if (allProperties.ContainsKey(propertyType))
                            {
                                var unit = _propertyUnits.TryGetValue(propertyType, out var unitValue)
                                    ? unitValue
                                    : "";
                                var label = string.IsNullOrWhiteSpace(unit)
                                    ? propertyType
                                    : $"{propertyType} ({unit})";

                                // Get the index of this entity in this specific property's array
                                int entityIndex = _entityManager.GetEntityIndexInProperty(entityId, propertyType);
                                
                                if (entityIndex >= 0 && entityIndex < allProperties[propertyType].Count)
                                {
                                    var value = allProperties[propertyType][entityIndex];
                                    var formattedValue = FormatSingleValue(value);
                                    Console.WriteLine($"║   {label,-26}: {formattedValue,-32}║");
                                }
                            }
                        }
                    }

                    Console.WriteLine($"╠═══════════════════════════════════════════════════════════════╣");
                }
            }

            Console.WriteLine($"╚═══════════════════════════════════════════════════════════════╝");
            Console.WriteLine();

            // Mark that the first report has been displayed
            _isFirstReport = false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error reporting state: {ex.Message}");
        }
    }

    /// <summary>
    /// Formats a single property value into a readable string for display.
    /// </summary>
    private string FormatSingleValue(object value)
    {
        if (value == null)
            return "(null)";

        return value switch
        {
            Vector3 v => $"({v.X:F6}, {v.Y:F6}, {v.Z:F6})",
            float f => f.ToString("F6"),
            int i => i.ToString(),
            _ => value.ToString() ?? "(unknown)"
        };
    }

    /// <summary>
    /// Sets property units used in state reporting.
    /// </summary>
    public void SetPropertyUnits(Dictionary<string, string>? propertyUnits)
    {
        _propertyUnits = propertyUnits == null
            ? new Dictionary<string, string>()
            : new Dictionary<string, string>(propertyUnits, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Sets the properties configuration containing property visibility settings.
    /// Configures which properties to show in state reports based on their category.
    /// </summary>
    public void SetPropertiesConfiguration(ServiceManager.PropertiesConfiguration? config)
    {
        if (config?.PropertyUnits != null)
        {
            _propertyUnits = new Dictionary<string, string>(config.PropertyUnits, StringComparer.OrdinalIgnoreCase);
        }

        if (config?.PropertyVisibility != null)
        {
            _alwaysShowProperties = new HashSet<string>(
                config.PropertyVisibility.AlwaysShow ?? new List<string>(),
                StringComparer.OrdinalIgnoreCase
            );
            _showOnceProperties = new HashSet<string>(
                config.PropertyVisibility.ShowOnce ?? new List<string>(),
                StringComparer.OrdinalIgnoreCase
            );
            _intermediateProperties = new HashSet<string>(
                config.PropertyVisibility.Intermediate ?? new List<string>(),
                StringComparer.OrdinalIgnoreCase
            );
        }
    }

    /// <summary>
    /// Determines if a property should be displayed in the current state report.
    /// Returns false for intermediate properties.
    /// Returns false for showOnce properties after the first report.
    /// </summary>
    private bool ShouldDisplayProperty(string propertyType)
    {
        // Never show intermediate properties
        if (_intermediateProperties?.Contains(propertyType) ?? false)
            return false;

        // Show alwaysShow properties every time
        if (_alwaysShowProperties?.Contains(propertyType) ?? false)
            return true;

        // Show showOnce properties only in first report
        if (_showOnceProperties?.Contains(propertyType) ?? false)
            return _isFirstReport;

        // Default: show all other properties in first report, none in later reports
        return _isFirstReport;
    }
}

