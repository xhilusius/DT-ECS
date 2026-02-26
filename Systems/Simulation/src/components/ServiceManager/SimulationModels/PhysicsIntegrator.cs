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
/// Uses double precision for CurrentSpeed and Displacement to handle Earth-scale simulations.
/// </summary>
public class PhysicsIntegrator : ISimulationModel
{
    private readonly double _timeStepSeconds;

    public PhysicsIntegrator(float timeStepSeconds)
    {
        if (timeStepSeconds <= 0)
            throw new ArgumentOutOfRangeException(nameof(timeStepSeconds), "Time step must be greater than zero.");

        _timeStepSeconds = (double)timeStepSeconds;
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

                // Extract gravity force (optional) - now double[]
                var gravityForce = gravityForceIndex >= 0 && gravityForceIndex < gravityForceValues.Count && gravityForceValues[gravityForceIndex] is double[] gf && gf.Length == 3
                    ? gf
                    : new double[] { 0, 0, 0 };

                // Extract drag force (optional) - now double[]
                var dragForce = dragForceIndex >= 0 && dragForceIndex < dragForceValues.Count && dragForceValues[dragForceIndex] is double[] df && df.Length == 3
                    ? df
                    : new double[] { 0, 0, 0 };

                // Extract magnetism force (optional) - now double[]
                var magnetismForce = magnetismForceIndex >= 0 && magnetismForceIndex < magnetismForceValues.Count && magnetismForceValues[magnetismForceIndex] is double[] mf && mf.Length == 3
                    ? mf
                    : new double[] { 0, 0, 0 };

                // Extract wind force (optional) - now double[]
                var windForce = windForceIndex >= 0 && windForceIndex < windForceValues.Count && windForceValues[windForceIndex] is double[] wf && wf.Length == 3
                    ? wf
                    : new double[] { 0, 0, 0 };

                // Extract current speed - now double[]
                var currentSpeed = speedIndex < currentSpeedValues.Count && currentSpeedValues[speedIndex] is double[] cs && cs.Length == 3
                    ? cs
                    : new double[] { 0, 0, 0 };

                // Sum all forces to get net force (component-wise)
                double[] netForce = new double[3];
                netForce[0] = gravityForce[0] + dragForce[0] + magnetismForce[0] + windForce[0];
                netForce[1] = gravityForce[1] + dragForce[1] + magnetismForce[1] + windForce[1];
                netForce[2] = gravityForce[2] + dragForce[2] + magnetismForce[2] + windForce[2];

                // Calculate net acceleration from accumulated forces
                double[] netAcceleration = new double[3];
                netAcceleration[0] = mass > 0 ? netForce[0] / mass : 0;
                netAcceleration[1] = mass > 0 ? netForce[1] / mass : 0;
                netAcceleration[2] = mass > 0 ? netForce[2] / mass : 0;

                // Calculate new velocity: v_new = v_old + a * t
                double[] newSpeed = new double[3];
                newSpeed[0] = currentSpeed[0] + netAcceleration[0] * _timeStepSeconds;
                newSpeed[1] = currentSpeed[1] + netAcceleration[1] * _timeStepSeconds;
                newSpeed[2] = currentSpeed[2] + netAcceleration[2] * _timeStepSeconds;

                // Calculate displacement: d = v_old * t + 0.5 * a * t²
                double[] displacement = new double[3];
                displacement[0] = currentSpeed[0] * _timeStepSeconds + 0.5 * netAcceleration[0] * _timeStepSeconds * _timeStepSeconds;
                displacement[1] = currentSpeed[1] * _timeStepSeconds + 0.5 * netAcceleration[1] * _timeStepSeconds * _timeStepSeconds;
                displacement[2] = currentSpeed[2] * _timeStepSeconds + 0.5 * netAcceleration[2] * _timeStepSeconds * _timeStepSeconds;

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
