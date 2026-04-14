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
public record ScenarioResult(int ScenarioEntityId, bool Completed, int StepsExecuted, string Summary)
    : IPropertyValue
{
    public string GetPrintable() =>
        $"Completed={Completed} | Steps={StepsExecuted} | {Summary}";
}
