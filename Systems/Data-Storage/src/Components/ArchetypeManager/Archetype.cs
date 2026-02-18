namespace DataStorage.ArchetypeManager;

/// <summary>
/// Represents an archetype - a specific combination of property types.
/// In EPS (Entity-Property-Service), archetypes define which properties a service needs to operate on entities.
/// </summary>
public class Archetype
{
    /// <summary>
    /// Unique identifier for the archetype
    /// </summary>
    public required string Id { get; set; }

    /// <summary>
    /// Human-readable name for the archetype
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Set of property type names that make up this archetype
    /// </summary>
    public required HashSet<string> PropertyTypes { get; set; }

    /// <summary>
    /// Description of the archetype's purpose
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Checks if this archetype contains all required property types
    /// </summary>
    public bool ContainsAllProperties(IEnumerable<string> requiredProperties)
    {
        return requiredProperties.All(prop => PropertyTypes.Contains(prop));
    }

    /// <summary>
    /// Checks if this archetype matches a set of property types
    /// </summary>
    public bool Matches(IEnumerable<string> propertyTypes)
    {
        var types = new HashSet<string>(propertyTypes);
        return types.SetEquals(PropertyTypes);
    }
}
