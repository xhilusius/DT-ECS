namespace Simulation.PropertyTypes;

/// <summary>
/// Discriminant property that marks an entity as a what-if scenario candidate.
///
/// Entities that carry this property are treated by <see cref="Simulation.ServiceManager.CompositeServices.WhatIfService"/>
/// as scenario candidates rather than base-world entities.
/// <see cref="Value"/> holds the human-readable scenario label shown in the summary table.
/// </summary>
public record WhatIfLabel(string Value) : IPropertyValue
{
    public string GetPrintable() => Value;
}
