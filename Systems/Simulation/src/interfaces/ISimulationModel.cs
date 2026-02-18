namespace Simulation.Interfaces;

using System.Numerics;
using Simulation.StateManager;

/// <summary>
/// Interface for simulation models that calculate effects on property arrays.
/// 
/// Execution model:
/// - SimEngine fetches required input properties from StateManager via GetPropertiesByTypesAsync()
/// - StateManager returns PropertyArrayBundle with:
///   - Arrays: The property arrays
///   - ValidEntityIds: Entity IDs that have ALL required properties
/// - SimEngine passes PropertyArrayBundle to the model
/// - Model iterates over ValidEntityIds to access consistent data from all arrays
/// - Model performs pure computation on the data
/// - Model returns computed output data
/// - SimEngine writes output data back to StateManager
/// 
/// Models are pure computational units - they don't access the repository.
/// SimEngine handles all state management (reading/writing).
/// ServiceDescriptor declares which properties each model needs as input/output.
/// 
/// Examples:
/// - GravityModel:     inputs=[Mass, CurrentSpeed] → valid entities=[0,1] → outputs=[Displacement]
/// - PositionModel:    inputs=[Position, Displacement] → valid entities=[0,1] → outputs=[Position]
/// </summary>
public interface ISimulationModel
{
    /// <summary>
    /// Executes the simulation model on input data.
    /// The model performs pure computation - no state access.
    /// Input data is already fetched by SimEngine from the repository.
    /// </summary>
    /// <param name="inputBundle">PropertyArrayBundle containing:
    ///   - Arrays: Dictionary of property name -> array of values
    ///   - ValidEntityIds: List of entity IDs that have ALL required input properties
    /// </param>
    /// <returns>Dictionary of property name -> array of computed values for all entities in ValidEntityIds</returns>
    Task<Dictionary<string, List<object>>> ExecuteAsync(PropertyArrayBundle inputBundle);
}

