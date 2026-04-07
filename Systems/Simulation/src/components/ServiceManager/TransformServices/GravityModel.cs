namespace Simulation.ServiceManager.TransformServices;

using System.Numerics;
using Simulation.Interfaces;
using Simulation.StateManager;
using DataStorage.Interfaces;

/// <summary>
/// Gravity simulation model that calculates gravitational forces on objects.
/// Part of a parallel force composition system where multiple models calculate forces independently.
/// 
/// Input properties:  ["Mass", "GravityForce"]
/// Output properties: ["GravityForce"]
/// 
/// Physics:
/// - Gravitational force: F_gravity = mass * g (downward, negative Y)
/// - Where: g = 9.81 m/s² (Earth's gravity)
/// 
/// Note: Air resistance (drag) is handled separately by DragModel to enable
/// independent calculations and true parallel execution.
/// 
/// The gravitational force is output independently and summed by PhysicsIntegrator
/// with forces from other models (e.g., DragModel, MagnetismModel).
/// This enables true parallel execution of force models on multi-core systems.
/// Uses double precision for GravityForce to handle Earth-scale simulations.
/// </summary>
public class GravityModel : ITransformService
{
    private const double GravitationalAcceleration = 9.81; // m/s² (Earth's gravity)

    public GravityModel(float timeStepSeconds)
    {
        // Time step not needed for gravity calculation (force only)
    }

    /// <summary>
    /// Executes gravity force calculation on entities that have Mass.
    /// Calculates gravitational force for each entity.
    /// </summary>
    public async Task<Dictionary<string, List<object>>> ExecuteAsync(PropertyArrayBundle inputBundle)
    {
        if (inputBundle == null)
            throw new ArgumentNullException(nameof(inputBundle));

        return await Task.Run(() =>
        {
            // Extract input arrays
            var massValues = inputBundle.Arrays.ContainsKey("Mass") ? inputBundle.Arrays["Mass"] : new List<object>();
            var gravityForceValues = inputBundle.Arrays.ContainsKey("GravityForce") ? inputBundle.Arrays["GravityForce"] : new List<object>();

            // Preserve existing force array to maintain entity indices
            var outputForces = new List<object>(gravityForceValues);

            if (inputBundle.ValidEntityIds.Count == 0)
            {
                // No valid entities to process
                return new Dictionary<string, List<object>>
                {
                    { "GravityForce", outputForces }
                };
            }

            foreach (var entityId in inputBundle.ValidEntityIds)
            {
                // Get the index of this entity in the mass array
                int massIndex = inputBundle.EntityToPropertyIndices[entityId]["Mass"];
                int gravityForceIndex = inputBundle.EntityToPropertyIndices[entityId].ContainsKey("GravityForce")
                    ? inputBundle.EntityToPropertyIndices[entityId]["GravityForce"]
                    : -1;

                // Extract mass
                var mass = massIndex < massValues.Count
                    ? massValues[massIndex] as float? ?? 1.0f
                    : 1.0f;

                // Calculate gravitational force: F_gravity = mass * g (downward in Y direction)
                double[] gravityForce = new double[] { 0, -mass * GravitationalAcceleration, 0 };
                
                // Update or add force at correct index to preserve alignment
                if (gravityForceIndex >= 0 && gravityForceIndex < outputForces.Count)
                    outputForces[gravityForceIndex] = gravityForce;
                else
                    outputForces.Add(gravityForce);
            }

            // Return output as dictionary
            return new Dictionary<string, List<object>>
            {
                { "GravityForce", outputForces }
            };
        });
    }

    // No helper methods needed - gravity is a simple constant force
}
