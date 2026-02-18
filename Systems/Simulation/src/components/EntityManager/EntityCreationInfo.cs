namespace Simulation.EntityManager;

/// <summary>
/// Contains information about a newly created entity.
/// Returned by RegisterNewEntity() so the caller knows the entity's metadata and properties.
/// </summary>
public class EntityCreationInfo
{
    /// <summary>
    /// The entity that was created, containing ID, name, and description.
    /// </summary>
    public required Entity Entity { get; set; }

    /// <summary>
    /// The set of properties this entity has.
    /// </summary>
    public required HashSet<string> Properties { get; set; }
}

