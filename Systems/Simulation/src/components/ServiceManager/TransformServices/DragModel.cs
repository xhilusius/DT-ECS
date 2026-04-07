namespace Simulation.ServiceManager.TransformServices;

using System.Numerics;
using Simulation.Interfaces;
using Simulation.StateManager;

/// <summary>
/// Drag simulation model that calculates air resistance forces on objects.
/// Part of a parallel force composition system where multiple models calculate forces independently.
/// 
/// Input properties:  ["CurrentSpeed", "Radius", "Mass"]
/// Output properties: ["DragForce"]
/// 
/// Physics:
/// - Drag force: F_drag = 0.5 * ρ * v² * Cd * A
/// - Where: ρ = air density (1.225 kg/m³)
///          v = velocity magnitude
///          Cd = drag coefficient (0.47 for sphere)
///          A = cross-section area = π * r²
/// - Direction: opposite to velocity
/// 
/// Drag acts as a velocity-dependent resistance force and naturally causes objects
/// to converge to terminal velocity when drag equals gravitational force.
/// 
/// The drag force is output independently and summed by PhysicsIntegrator
/// with forces from other models (e.g., GravityModel, MagnetismModel).
/// This enables true parallel execution of force models on multi-core systems.
/// Uses double precision for CurrentSpeed and DragForce to handle Earth-scale simulations.
/// </summary>
public class DragModel : ITransformService
{
    private const double AirDensity = 1.225; // kg/m³ at sea level
    private const double DragCoefficientSphere = 0.47; // Typical drag coefficient for a sphere

    public DragModel()
    {
    }

    /// <summary>
    /// Executes drag force calculation on entities that have CurrentSpeed, Radius, and Mass.
    /// Calculates air resistance based on velocity and object shape.
    /// </summary>
    public async Task<Dictionary<string, List<object>>> ExecuteAsync(PropertyArrayBundle inputBundle)
    {
        if (inputBundle == null)
            throw new ArgumentNullException(nameof(inputBundle));

        return await Task.Run(() =>
        {
            // Extract input arrays
            var speedValues = inputBundle.Arrays.ContainsKey("CurrentSpeed") ? inputBundle.Arrays["CurrentSpeed"] : new List<object>();
            var radiusValues = inputBundle.Arrays.ContainsKey("Radius") ? inputBundle.Arrays["Radius"] : new List<object>();
            var massValues = inputBundle.Arrays.ContainsKey("Mass") ? inputBundle.Arrays["Mass"] : new List<object>();
            var dragForceValues = inputBundle.Arrays.ContainsKey("DragForce") ? inputBundle.Arrays["DragForce"] : new List<object>();

            // Preserve existing force array to maintain entity indices
            var outputForces = new List<object>(dragForceValues);

            if (inputBundle.ValidEntityIds.Count == 0)
            {
                // No valid entities
                return new Dictionary<string, List<object>>
                {
                    { "DragForce", outputForces }
                };
            }

            // Calculate drag force for each entity
            foreach (var entityId in inputBundle.ValidEntityIds)
            {
                // Get the indices of this entity in each property array
                int speedIndex = inputBundle.EntityToPropertyIndices[entityId]["CurrentSpeed"];
                int radiusIndex = inputBundle.EntityToPropertyIndices[entityId]["Radius"];
                int dragForceIndex = inputBundle.EntityToPropertyIndices[entityId].ContainsKey("DragForce")
                    ? inputBundle.EntityToPropertyIndices[entityId]["DragForce"]
                    : -1;

                // Extract current speed - now double[]
                var currentSpeed = speedIndex < speedValues.Count && speedValues[speedIndex] is double[] cs && cs.Length == 3
                    ? cs
                    : new double[] { 0, 0, 0 };

                // Extract radius for drag calculation
                var radius = radiusIndex < radiusValues.Count
                    ? radiusValues[radiusIndex] as float? ?? 0.01f
                    : 0.01f;

                // Calculate drag force
                double[] dragForce = CalculateDragForce(radius, currentSpeed);
                
                // Update or add force at correct index to preserve alignment
                if (dragForceIndex >= 0 && dragForceIndex < outputForces.Count)
                    outputForces[dragForceIndex] = dragForce;
                else
                    outputForces.Add(dragForce);
            }

            return new Dictionary<string, List<object>>
            {
                { "DragForce", outputForces }
            };
        });
    }

    /// <summary>
    /// Calculates drag force based on velocity and object radius.
    /// Drag acts opposite to the velocity direction.
    /// </summary>
    private double[] CalculateDragForce(float radius, double[] currentSpeed)
    {
        if (radius <= 0)
            return new double[] { 0, 0, 0 };

        double speedMagnitude = Math.Sqrt(currentSpeed[0] * currentSpeed[0] + currentSpeed[1] * currentSpeed[1] + currentSpeed[2] * currentSpeed[2]);
        double[] dragForce = new double[] { 0, 0, 0 };

        if (speedMagnitude > 0)
        {
            // Drag force: F_drag = 0.5 * ρ * v² * Cd * A
            double crossSectionArea = Math.PI * radius * radius;
            double dragForceMagnitude = 0.5 * AirDensity * speedMagnitude * speedMagnitude * DragCoefficientSphere * crossSectionArea;

            // Drag acts opposite to velocity direction
            dragForce[0] = -(currentSpeed[0] / speedMagnitude) * dragForceMagnitude;
            dragForce[1] = -(currentSpeed[1] / speedMagnitude) * dragForceMagnitude;
            dragForce[2] = -(currentSpeed[2] / speedMagnitude) * dragForceMagnitude;
        }

        return dragForce;
    }
}
