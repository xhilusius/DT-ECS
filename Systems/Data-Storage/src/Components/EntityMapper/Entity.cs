namespace DataStorage.EntityMapper;

/// <summary>
/// Represents an entity with metadata (ID, name, description).
/// Shared across all subsystems via synchronization.
/// </summary>
public class Entity
{
    public int Id { get; }
    public string Name { get; set; }
    public string? Description { get; set; }

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

    public override string ToString()
    {
        return $"Entity {Id}: {Name}";
    }
}
