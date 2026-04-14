namespace Simulation.PropertyTypes;

/// <summary>
/// Immutable snapshot of a single entity's state captured from the outer simulation.
///
/// Passed inside a <see cref="ScenarioConfig"/> so every inner simulation starts from an
/// identical copy of the shared outer world.  Each inner sim receives its own copy of the
/// list, so the sims are fully isolated — one scenario's entity evolution cannot affect
/// another's even when they run in parallel.
/// </summary>
public record BaseEntitySnapshot(
    string Name,
    string? Description,
    IReadOnlyDictionary<string, object> Properties);
