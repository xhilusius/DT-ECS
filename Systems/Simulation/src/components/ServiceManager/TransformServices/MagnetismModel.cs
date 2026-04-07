namespace Simulation.ServiceManager.TransformServices;

using System.Numerics;
using Simulation.Interfaces;
using Simulation.StateManager;

/// <summary>
/// Magnetism simulation model that calculates magnetic force on metallic objects.
/// Part of a parallel force composition system where multiple models calculate forces independently.
/// Uses property-by-presence: only entities WITH the "MagnetismForce" property are affected.
/// Applies a force opposite to gravity's direction (upward).
/// 
/// Input properties:  ["MagnetismForce", "Mass"]
/// Output properties: ["MagnetismForce"]
/// 
/// Architecture:
/// - MagnetismForce property presence indicates the entity is metallic and affected by magnetism
/// - Archetype query automatically filters to only entities with MagnetismForce property
/// - If entity lacks MagnetismForce property, it is not affected by magnetism
/// 
/// Physics:
/// - Magnetic force: F_magnetic = mass * 5.0 m/s² (upward, positive Y)
/// - Force direction: opposite to gravity (upward, positive Y)
/// 
/// The magnetic force is output independently and summed by PhysicsIntegrator
/// with forces from other models (e.g., GravityModel, WindForceModel).
/// This enables true parallel execution of force models on multi-core systems.
/// Uses double precision for MagnetismForce to handle Earth-scale simulations.
/// </summary>
public class MagnetismModel : ITransformService
{
    private const double MagneticFieldStrength = 2.5; // m/s² (adjustable magnetic acceleration)
    private readonly float _timeStepSeconds;

    public MagnetismModel(float timeStepSeconds)
    {
        if (timeStepSeconds <= 0)
            throw new ArgumentOutOfRangeException(nameof(timeStepSeconds), "Time step must be greater than zero.");

        _timeStepSeconds = timeStepSeconds;
    }

    /// <summary>
    /// Executes magnetism force calculation on entities that have MagnetismForce and Mass.
    /// Calculates magnetic force, outputs as MagnetismForce.
    /// Only metallic entities (those with MagnetismForce property) are affected.
    /// </summary>
    public async Task<Dictionary<string, List<object>>> ExecuteAsync(PropertyArrayBundle inputBundle)
    {
        if (inputBundle == null)
            throw new ArgumentNullException(nameof(inputBundle));

        return await Task.Run(() =>
        {
            // Extract input arrays
            var massValues = inputBundle.Arrays.ContainsKey("Mass") ? inputBundle.Arrays["Mass"] : new List<object>();
            var magnetismForceValues = inputBundle.Arrays.ContainsKey("MagnetismForce") ? inputBundle.Arrays["MagnetismForce"] : new List<object>();

            // Preserve existing force array to maintain entity indices
            var outputForces = new List<object>(magnetismForceValues);

            if (inputBundle.ValidEntityIds.Count == 0)
            {
                // No metallic entities
                return new Dictionary<string, List<object>>
                {
                    { "MagnetismForce", outputForces }
                };
            }

            // Calculate magnetic force for each metallic entity
            // All entities in ValidEntityIds ARE metallic (filtered by archetype query)
            foreach (var entityId in inputBundle.ValidEntityIds)
            {
                // Get the indices of this entity in each property array
                int massIndex = inputBundle.EntityToPropertyIndices[entityId]["Mass"];
                int magnetismForceIndex = inputBundle.EntityToPropertyIndices[entityId].ContainsKey("MagnetismForce")
                    ? inputBundle.EntityToPropertyIndices[entityId]["MagnetismForce"]
                    : -1;

                // Extract mass
                var mass = massIndex < massValues.Count
                    ? massValues[massIndex] as float? ?? 1.0f
                    : 1.0f;

                // Calculate magnetic force: F_magnetic = mass * a_magnetic
                // Magnetic acceleration is constant: 2.5 m/s² upward
                double[] magneticForce = new double[] { 0, mass * MagneticFieldStrength, 0 };
                
                // Update or add force at correct index to preserve alignment
                if (magnetismForceIndex >= 0 && magnetismForceIndex < outputForces.Count)
                    outputForces[magnetismForceIndex] = magneticForce;
                else
                    outputForces.Add(magneticForce);
            }

            return new Dictionary<string, List<object>>
            {
                { "MagnetismForce", outputForces }
            };
        });
    }
}
