namespace Simulation.ServiceManager.SimulationModels;

using System.Numerics;
using Simulation.Interfaces;
using Simulation.StateManager;

/// <summary>
/// Magnetism simulation model that calculates magnetic force on metallic objects.
/// Part of a parallel force composition system where multiple models calculate forces independently.
/// Uses tag-by-presence: only entities WITH the "IsMetal" property are affected.
/// Applies a force opposite to gravity's direction (upward).
/// 
/// Input properties:  ["IsMetal", "Mass"]
/// Output properties: ["MagnetismForce"]
/// 
/// Architecture:
/// - IsMetal acts as a tag: presence = metallic, absence = non-metallic
/// - Archetype query automatically filters to only entities with IsMetal property
/// - No boolean check needed - if entity is in the batch, it IS metallic
/// 
/// Physics:
/// - Magnetic force: F_magnetic = mass * 5.0 m/s² (upward, positive Y)
/// - Force direction: opposite to gravity (upward, positive Y)
/// 
/// The magnetic force is output independently and summed by PhysicsIntegrator
/// with forces from other models (e.g., GravityModel, future WindModel).
/// This enables true parallel execution of force models on multi-core systems.
/// </summary>
public class MagnetismModel : ISimulationModel
{
    private const float MagneticFieldStrength = 5.0f; // m/s² (adjustable magnetic acceleration)
    private readonly float _timeStepSeconds;

    public MagnetismModel(float timeStepSeconds)
    {
        if (timeStepSeconds <= 0)
            throw new ArgumentOutOfRangeException(nameof(timeStepSeconds), "Time step must be greater than zero.");

        _timeStepSeconds = timeStepSeconds;
    }

    /// <summary>
    /// Executes magnetism force calculation on entities that have IsMetal and Mass.
    /// Calculates magnetic force, outputs as MagnetismForce.
    /// Only metallic entities (those with IsMetal property) are affected.
    /// </summary>
    public async Task<Dictionary<string, List<object>>> ExecuteAsync(PropertyArrayBundle inputBundle)
    {
        if (inputBundle == null)
            throw new ArgumentNullException(nameof(inputBundle));

        return await Task.Run(() =>
        {
            // Extract input arrays
            var massValues = inputBundle.Arrays.ContainsKey("Mass") ? inputBundle.Arrays["Mass"] : new List<object>();
            // Note: IsMetal array exists but we don't need to read values - presence in ValidEntityIds means it's metallic

            // Output array for magnetic forces
            var outputForces = new List<object>();

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
                // IsMetal index exists but we don't need it - entity presence confirms it's metallic

                // Extract mass
                var mass = massIndex < massValues.Count
                    ? massValues[massIndex] as float? ?? 1.0f
                    : 1.0f;

                // Calculate magnetic force: F_magnetic = mass * a_magnetic
                // Magnetic acceleration is constant: 5.0 m/s² upward
                Vector3 magneticForce = new Vector3(0, mass * MagneticFieldStrength, 0);
                outputForces.Add(magneticForce);
            }

            return new Dictionary<string, List<object>>
            {
                { "MagnetismForce", outputForces }
            };
        });
    }
}
