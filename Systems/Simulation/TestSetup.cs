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
    /// Name of the outer composite setup to use (e.g., "SimulationRunSetup", "WhatIfRunSetup").
    /// Determines which outer CompositeService pipeline to instantiate.
    /// </summary>
    public required string OuterSetup { get; init; }

    /// <summary>
    /// Name of the inner composite setup to use (e.g., "DefaultSetup", "NBodySetup").
    /// Passed to the inner CompositeService (e.g., TestSimulationService) to configure the physics simulation.
    /// </summary>
    public required string InnerSetup { get; init; }

    /// <summary>
    /// Step-based actions to execute during the test run.
    /// Step 0 actions are executed before the simulation starts.
    /// </summary>
    public required List<TestStepDefinition> Steps { get; init; }

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

/// <summary>
/// Defines a list of actions to run at a specific simulation step.
/// </summary>
public class TestStepDefinition
{
    /// <summary>
    /// Step index where actions should run. Step 0 runs before the first simulation step.
    /// </summary>
    public required int Step { get; init; }

    /// <summary>
    /// Actions to execute at this step.
    /// </summary>
    public required List<TestActionDefinition> Actions { get; init; }
}

/// <summary>
/// Defines a single action to execute during a test run.
/// Only spawn actions are supported for now.
/// </summary>
public class TestActionDefinition
{
    /// <summary>
    /// Action type, e.g. "spawn".
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// Entity definition used by this action.
    /// </summary>
    public required EntityDefinition Entity { get; init; }

    /// <summary>
    /// Optional property overrides to apply when spawning this entity.
    /// These override the properties defined in the entity definition.
    /// </summary>
    public Dictionary<string, object>? PropertyOverrides { get; init; }
}
