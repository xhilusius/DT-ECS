namespace Simulation.ServiceManager.SimulationModels;

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
/// </summary>
public class DragModel : ISimulationModel
{
    private const float AirDensity = 1.225f; // kg/m³ at sea level
    private const float DragCoefficientSphere = 0.47f; // Typical drag coefficient for a sphere

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

            // Output array for drag forces
            var outputForces = new List<object>();

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

                // Extract current speed
                var currentSpeed = speedIndex < speedValues.Count
                    ? speedValues[speedIndex] as Vector3? ?? Vector3.Zero
                    : Vector3.Zero;

                // Extract radius for drag calculation
                var radius = radiusIndex < radiusValues.Count
                    ? radiusValues[radiusIndex] as float? ?? 0.01f
                    : 0.01f;

                // Calculate drag force
                Vector3 dragForce = CalculateDragForce(radius, currentSpeed);
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
    private Vector3 CalculateDragForce(float radius, Vector3 currentSpeed)
    {
        if (radius <= 0)
            return Vector3.Zero;

        float speedMagnitude = currentSpeed.Length();
        Vector3 dragForce = Vector3.Zero;

        if (speedMagnitude > 0)
        {
            // Drag force: F_drag = 0.5 * ρ * v² * Cd * A
            float crossSectionArea = MathF.PI * radius * radius;
            float dragForceMagnitude = 0.5f * AirDensity * speedMagnitude * speedMagnitude * DragCoefficientSphere * crossSectionArea;

            // Drag acts opposite to velocity direction
            Vector3 dragDirection = -(currentSpeed / speedMagnitude);
            dragForce = dragDirection * dragForceMagnitude;
        }

        return dragForce;
    }
}
