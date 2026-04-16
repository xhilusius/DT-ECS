namespace Simulation.PropertyTypes;

/// <summary>
/// Property value written back to a scenario entity by the WhatIfService after
/// the inner simulation for that scenario has completed.
///
/// The WhatIfService produces one ScenarioResult per scenario entity and stores
/// it under the "ScenarioResult" property type in the outer state store.
/// </summary>
/// <param name="ScenarioEntityId">
///   ID of the scenario entity in the outer simulation this result belongs to.
/// </param>
/// <param name="Completed">
///   True if the inner simulation ran to completion without error.
/// </param>
/// <param name="StepsExecuted">
///   Number of inner simulation steps that were executed.
/// </param>
/// <param name="Summary">
///   Human-readable summary string produced by the inner simulation
///   (e.g. collision details, or "No collision detected").
/// </param>
public record ScenarioResult(
    int ScenarioEntityId,
    bool Completed,
    int StepsExecuted,
    string Summary,
    string Label = "",
    bool CollisionDetected = false,
    int CollisionAtStep = 0,
    int CollidedWithEntityId = -1,
    string CollidedWithEntityName = "",
    double[]? CollisionPosition = null)
    : IPropertyValue
{
    /// <summary>
    /// Placeholder instance used when a scenario entity is first created.
    /// <see cref="Simulation.ServiceManager.CompositeServices.WhatIfService"/> overwrites
    /// this with the real result after the inner simulation completes.
    /// </summary>
    public static ScenarioResult Default => new(0, false, 0, string.Empty);

    public string GetPrintable() =>
        CollisionDetected
            ? $"[{Label}] CRASH at step {CollisionAtStep} with '{CollidedWithEntityName}'"
            : $"[{Label}] NO CRASH — {StepsExecuted} steps completed";
}
