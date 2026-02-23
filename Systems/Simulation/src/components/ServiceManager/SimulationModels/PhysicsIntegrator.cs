namespace Simulation.ServiceManager.SimulationModels;

using System.Numerics;
using Simulation.Interfaces;
using Simulation.StateManager;

/// <summary>
/// Physics integrator that applies accumulated forces to calculate velocity and displacement changes.
/// Reads forces from independent parallel force models (GravityModel, DragModel, MagnetismModel, WindForceModel, etc.)
/// and converts their sum to velocity and displacement changes.
/// 
/// Input properties:  ["Mass", "GravityForce", "DragForce", "MagnetismForce", "WindForce", "CurrentSpeed"]
/// Output properties: ["CurrentSpeed", "Displacement"]
/// 
/// Physics:
/// - Net acceleration: a = F_net / mass = (F_gravity + F_drag + F_magnetism + ...) / mass
/// - Velocity update: v_new = v_old + a * t
/// - Displacement: d = v_old * t + 0.5 * a * t²
/// 
/// This model runs AFTER all force-producing models so that all forces are accumulated
/// before integration. The use of separate force properties enables safe parallel execution
/// of force models on multi-core systems without race conditions.
/// </summary>
public class PhysicsIntegrator : ISimulationModel
{
    private readonly float _timeStepSeconds;

    public PhysicsIntegrator(float timeStepSeconds)
    {
        if (timeStepSeconds <= 0)
            throw new ArgumentOutOfRangeException(nameof(timeStepSeconds), "Time step must be greater than zero.");

        _timeStepSeconds = timeStepSeconds;
    }

    /// <summary>
    /// Executes physics integration on entities that have Mass, forces (GravityForce, DragForce, MagnetismForce), and CurrentSpeed.
    /// Sums all force contributions and converts net force to velocity and displacement updates.
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
            var dragForceValues = inputBundle.Arrays.ContainsKey("DragForce") ? inputBundle.Arrays["DragForce"] : new List<object>();
            var magnetismForceValues = inputBundle.Arrays.ContainsKey("MagnetismForce") ? inputBundle.Arrays["MagnetismForce"] : new List<object>();
            var windForceValues = inputBundle.Arrays.ContainsKey("WindForce") ? inputBundle.Arrays["WindForce"] : new List<object>();
            var currentSpeedValues = inputBundle.Arrays.ContainsKey("CurrentSpeed") ? inputBundle.Arrays["CurrentSpeed"] : new List<object>();

            // Output arrays for updated velocity and displacement
            var outputSpeeds = new List<object>(currentSpeedValues);
            var outputDisplacements = new List<object>();

            if (inputBundle.ValidEntityIds.Count == 0)
            {
                // No valid entities to process
                return new Dictionary<string, List<object>>
                {
                    { "CurrentSpeed", outputSpeeds },
                    { "Displacement", outputDisplacements }
                };
            }

            foreach (var entityId in inputBundle.ValidEntityIds)
            {
                // Get the indices of this entity in each property array
                // All force properties are optional - only present if entity is affected by that force
                int massIndex = inputBundle.EntityToPropertyIndices[entityId]["Mass"];
                int speedIndex = inputBundle.EntityToPropertyIndices[entityId]["CurrentSpeed"];
                
                int gravityForceIndex = inputBundle.EntityToPropertyIndices[entityId].ContainsKey("GravityForce") 
                    ? inputBundle.EntityToPropertyIndices[entityId]["GravityForce"] 
                    : -1;
                int dragForceIndex = inputBundle.EntityToPropertyIndices[entityId].ContainsKey("DragForce") 
                    ? inputBundle.EntityToPropertyIndices[entityId]["DragForce"] 
                    : -1;
                int magnetismForceIndex = inputBundle.EntityToPropertyIndices[entityId].ContainsKey("MagnetismForce") 
                    ? inputBundle.EntityToPropertyIndices[entityId]["MagnetismForce"] 
                    : -1;
                int windForceIndex = inputBundle.EntityToPropertyIndices[entityId].ContainsKey("WindForce") 
                    ? inputBundle.EntityToPropertyIndices[entityId]["WindForce"] 
                    : -1;

                // Extract mass
                var mass = massIndex < massValues.Count
                    ? massValues[massIndex] as float? ?? 1.0f
                    : 1.0f;

                // Extract gravity force (optional)
                var gravityForce = gravityForceIndex >= 0 && gravityForceIndex < gravityForceValues.Count
                    ? gravityForceValues[gravityForceIndex] as Vector3? ?? Vector3.Zero
                    : Vector3.Zero;

                // Extract drag force (optional)
                var dragForce = dragForceIndex >= 0 && dragForceIndex < dragForceValues.Count
                    ? dragForceValues[dragForceIndex] as Vector3? ?? Vector3.Zero
                    : Vector3.Zero;

                // Extract magnetism force (optional)
                var magnetismForce = magnetismForceIndex >= 0 && magnetismForceIndex < magnetismForceValues.Count
                    ? magnetismForceValues[magnetismForceIndex] as Vector3? ?? Vector3.Zero
                    : Vector3.Zero;

                // Extract wind force (optional)
                var windForce = windForceIndex >= 0 && windForceIndex < windForceValues.Count
                    ? windForceValues[windForceIndex] as Vector3? ?? Vector3.Zero
                    : Vector3.Zero;

                // Extract current speed
                var currentSpeed = speedIndex < currentSpeedValues.Count
                    ? currentSpeedValues[speedIndex] as Vector3? ?? Vector3.Zero
                    : Vector3.Zero;

                // Sum all forces to get net force
                Vector3 netForce = gravityForce + dragForce + magnetismForce + windForce;

                // Calculate net acceleration from accumulated forces
                Vector3 netAcceleration = mass > 0 ? netForce / mass : Vector3.Zero;

                // Calculate new velocity: v_new = v_old + a * t
                Vector3 newSpeed = currentSpeed + (netAcceleration * _timeStepSeconds);

                // Calculate displacement: d = v_old * t + 0.5 * a * t²
                Vector3 displacement = (currentSpeed * _timeStepSeconds) + (netAcceleration * 0.5f * _timeStepSeconds * _timeStepSeconds);

                // Update output arrays
                if (speedIndex < outputSpeeds.Count)
                    outputSpeeds[speedIndex] = newSpeed;
                else
                    outputSpeeds.Add(newSpeed);

                outputDisplacements.Add(displacement);
            }

            // Return output as dictionary
            return new Dictionary<string, List<object>>
            {
                { "CurrentSpeed", outputSpeeds },
                { "Displacement", outputDisplacements }
            };
        });
    }
}
