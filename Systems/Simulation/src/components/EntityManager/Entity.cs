namespace Simulation.EntityManager;

using System.Diagnostics.CodeAnalysis;

/// <summary>
/// Represents an entity in the simulation system.
/// Each entity has a unique ID, name, and description for identification and tracking.
/// </summary>
public class Entity
{
    /// <summary>
    /// The unique identifier for this entity.
    /// Auto-assigned when the entity is created.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Human-readable name for this entity.
    /// Used for displaying and identifying the entity in logs and UI.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Detailed description of this entity's purpose or role in the simulation.
    /// Optional field that can be left empty.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Constructor for creating an entity with all metadata.
    /// </summary>
    /// <param name="id">Unique entity identifier</param>
    /// <param name="name">Human-readable name</param>
    /// <param name="description">Brief description of the entity</param>
    [SetsRequiredMembers]
    public Entity(int id, string name, string? description = null)
    {
        if (id < 0)
            throw new ArgumentException("Entity ID cannot be negative", nameof(id));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Entity name cannot be null or empty", nameof(name));

        Id = id;
        Name = name;
        Description = description;
    }

    /// <summary>
    /// Returns a string representation of the entity.
    /// </summary>
    public override string ToString()
    {
        if (string.IsNullOrWhiteSpace(Description))
            return $"Entity {Id}: {Name}";
        return $"Entity {Id}: {Name} - {Description}";
    }
}
