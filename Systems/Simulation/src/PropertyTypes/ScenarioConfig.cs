namespace Simulation.PropertyTypes;

/// <summary>
/// Property value that fully describes one inner simulation scenario.
/// Entities carrying this property are picked up by the WhatIfService and each
/// gets its own isolated inner simulation run.
///
/// Design intent:
/// - Each scenario is a first-class ECS entity in the outer simulation.
/// - The WhatIfService queries all entities that have this property and processes
///   each one as an independent inner simulation, writing a ScenarioResult back.
/// - The inner simulation is fully specified by this value: which CompositeSetup
///   to load and which entities to spawn inside.
/// - Real / pre-existing entities from the outer sim are passed in separately by
///   the WhatIfService (they are not duplicated here); only the additional
///   scenario-specific entities belong in EntitySpawns.
/// </summary>
/// <param name="SetupName">
///   Name of the CompositeSetup to use for the inner simulation
///   (e.g. "SatelliteSetup"). Resolved via CompositeServiceSetupLoader.
/// </param>
/// <param name="BaseEntities">
///   Snapshot of the shared outer world entities (e.g. Earth, existing satellites)
///   that every inner simulation starts from.  Each scenario carries its own copy so
///   inner sims are fully isolated — one scenario's evolution cannot affect another's.
/// </param>
/// <param name="EntitySpawns">
///   Entities to spawn exclusively in this inner simulation.
///   Typically the one or more candidate entities that define this scenario.
/// </param>
public record ScenarioConfig(
    string SetupName,
    IReadOnlyList<BaseEntitySnapshot> BaseEntities,
    IReadOnlyList<ScenarioEntitySpawn> EntitySpawns)
    : IPropertyValue
{
    public string GetPrintable() =>
        $"Setup={SetupName} | Base={BaseEntities.Count} | Candidates={EntitySpawns.Count}";
}

/// <summary>
/// Describes one entity to be spawned inside a scenario's inner simulation.
/// Mirrors the spawn action in a test case but as a typed in-memory value.
/// </summary>
/// <param name="TemplateName">
///   Entity template key from the entity library (e.g. "Satellite", "Earth_ball").
/// </param>
/// <param name="PropertyOverrides">
///   Property values that override the template defaults for this spawn.
/// </param>
public record ScenarioEntitySpawn(
    string TemplateName,
    IReadOnlyDictionary<string, object> PropertyOverrides);
