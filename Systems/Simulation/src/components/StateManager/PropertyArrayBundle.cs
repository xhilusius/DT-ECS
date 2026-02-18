namespace Simulation.StateManager;

/// <summary>
/// Bundles related property arrays with entity mapping information.
/// Returned by StateManager.GetPropertiesByTypesAsync when a service requires multiple properties.
/// 
/// Contains:
/// - Arrays: The actual property arrays indexed by property type
/// - ValidEntityIds: List of entity IDs that have ALL the requested properties
/// - EntityToPropertyIndices: Maps each entity to its index in each property array
/// </summary>
public class PropertyArrayBundle
{
    /// <summary>
    /// Property arrays indexed by property type.
    /// Key = property type name (e.g., "Mass", "Position", "CurrentSpeed")
    /// Value = list of values for that property
    /// </summary>
    public Dictionary<string, List<object>> Arrays { get; set; } = new();

    /// <summary>
    /// Valid entity IDs that have ALL the requested properties.
    /// 
    /// For a service requiring {Mass, CurrentSpeed}:
    /// - ValidEntityIds might be [0, 1, 3]
    /// - Entity 2 is NOT included because it might be missing CurrentSpeed
    /// </summary>
    public List<int> ValidEntityIds { get; set; } = new();

    /// <summary>
    /// Maps each valid entity to its index in each property array.
    /// Key = EntityId, Value = Dictionary of property type -> array index
    /// 
    /// Example for Mass and CurrentSpeed:
    ///   EntityToPropertyIndices[0]["Mass"] = 0 (entity 0 is at index 0 in Mass array)
    ///   EntityToPropertyIndices[0]["CurrentSpeed"] = 0
    ///   EntityToPropertyIndices[1]["Mass"] = 1
    ///   EntityToPropertyIndices[1]["CurrentSpeed"] = 1
    /// 
    /// This allows SimulationModels to access the correct values:
    ///   - mass = Arrays["Mass"][EntityToPropertyIndices[entityId]["Mass"]]
    ///   - speed = Arrays["CurrentSpeed"][EntityToPropertyIndices[entityId]["CurrentSpeed"]]
    /// </summary>
    public Dictionary<int, Dictionary<string, int>> EntityToPropertyIndices { get; set; } = new();
}
