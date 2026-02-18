namespace Simulation;

using System.Collections.Generic;

/// <summary>
/// Defines the complete initialization data for a test case.
/// This includes entities, properties, configuration, and metadata.
/// Components are created from this data when a test runs.
/// </summary>
public class TestSetup
{
    /// <summary>
    /// Name of the configuration file to load (e.g., "DefaultSetup.json")
    /// </summary>
    public required string ConfigurationFile { get; init; }

    /// <summary>
    /// List of entities to create with their initial properties
    /// </summary>
    public required List<EntityDefinition> Entities { get; init; }

    /// <summary>
    /// Human-readable description of what this test setup represents
    /// </summary>
    public required string Description { get; init; }
}

/// <summary>
/// Defines a single entity with its properties and metadata
/// </summary>
public class EntityDefinition
{
    /// <summary>
    /// Human-readable name for the entity
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Initial property values for this entity
    /// Key = property type (e.g., "Mass", "Position")
    /// Value = initial value
    /// </summary>
    public required Dictionary<string, object> Properties { get; init; }

    /// <summary>
    /// Optional description of this entity's purpose
    /// </summary>
    public string? Description { get; init; }
}
