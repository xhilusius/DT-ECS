namespace Simulation.PropertyTypes;

/// <summary>
/// Immutable snapshot of a single entity's state captured from the outer simulation.
///
/// Registered in the outer ECS by <see cref="WhatIfCaseLoaderService"/> so every inner
/// simulation starts from an identical copy of the shared outer world.  Each inner sim
/// receives its own copy of the list, so the sims are fully isolated.
/// </summary>
public record BaseEntitySnapshot(
    string Name,
    string? Description,
    IReadOnlyDictionary<string, object> Properties);
