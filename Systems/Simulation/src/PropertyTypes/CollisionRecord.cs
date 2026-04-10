namespace Simulation.PropertyTypes;

/// <summary>
/// Property value produced by <c>CollisionDetectionModel</c>.
/// Stores the first collision event detected for an entity: the other entity's ID
/// and both entity positions at the moment detection fired.
/// Once set on an entity, this record is cumulative — it persists for the rest of the simulation.
/// </summary>
/// <param name="CollidedWithEntityId">ID of the other entity involved in the collision.</param>
/// <param name="EntityPosition">Position of this entity at the time of collision detection.</param>
/// <param name="OtherEntityPosition">Position of the other entity at the time of collision detection.</param>
public record CollisionRecord(int CollidedWithEntityId, double[] EntityPosition, double[] OtherEntityPosition)
    : IPropertyValue
{
    public string GetPrintable() =>
        $"Entity {CollidedWithEntityId} | pos=({EntityPosition[0]:F0}, {EntityPosition[1]:F0}, {EntityPosition[2]:F0}) | other=({OtherEntityPosition[0]:F0}, {OtherEntityPosition[1]:F0}, {OtherEntityPosition[2]:F0})";
}
