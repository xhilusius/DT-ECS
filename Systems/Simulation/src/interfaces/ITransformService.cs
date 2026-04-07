namespace Simulation.Interfaces;

using Simulation.StateManager;

/// <summary>
/// Pure computational service that transforms input property arrays into output property arrays.
/// 
/// Execution model:
/// - TransformExecutor fetches required input properties from StateManager (by archetype)
/// - TransformExecutor passes the PropertyArrayBundle to the service
/// - Service performs pure computation — no repository or state access
/// - Service returns computed output arrays
/// - TransformExecutor writes outputs back to StateManager
/// 
/// All entities in ValidEntityIds have every required input property.
/// Services operate on aligned arrays — same index = same entity across all arrays.
/// 
/// Covers all service shapes that fit the property-in / property-out contract:
/// - Per-entity transforms  (GravityModel, PositionModel)
/// - Cross-entity reduces   (NBodyGravityModel reads all positions per entity)
/// - Aggregate reducers     (DecisionService folds all EnergyDriftRate values)
/// 
/// Services that do not fit this contract (external I/O, inner orchestration)
/// implement IExternalService or ICompositeService instead.
/// </summary>
public interface ITransformService
{
    /// <summary>
    /// Executes pure computation on the provided property arrays.
    /// No state access — all inputs are pre-fetched by TransformExecutor, all outputs are returned for writing.
    /// </summary>
    /// <param name="inputBundle">PropertyArrayBundle containing:
    ///   - Arrays: Dictionary of property name to array of values (aligned by entity index)
    ///   - ValidEntityIds: Entity IDs that have ALL required input properties
    /// </param>
    /// <returns>Dictionary of property name to computed output values for entities in ValidEntityIds</returns>
    Task<Dictionary<string, List<object>>> ExecuteAsync(PropertyArrayBundle inputBundle);
}
