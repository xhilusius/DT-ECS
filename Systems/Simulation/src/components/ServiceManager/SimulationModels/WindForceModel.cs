namespace Simulation.ServiceManager.SimulationModels;

using System.Numerics;
using Simulation.Interfaces;
using Simulation.StateManager;

/// <summary>
/// Wind force simulation model that calculates constant wind-based forces on objects.
/// Part of a parallel force composition system where multiple models calculate forces independently.
/// Assumes constant wind velocity (user-specified, not dependent on object motion).
/// 
/// Input properties:  ["Radius"]
/// Output properties: ["WindForce"]
/// 
/// Physics:
/// - Wind force: F_wind = 0.5 * ρ * v_wind² * Cd * A
/// - Where: ρ = air density (1.225 kg/m³)
///          v_wind = constant wind velocity magnitude
///          Cd = drag coefficient (0.47 for sphere)
///          A = cross-section area = π * r²
/// - Direction: direction of constant wind vector (positive Z by default)
/// - Note: Wind force is independent of object mass (mass affects acceleration, not force)
/// 
/// Wind applies a directional force to all objects based on wind velocity and cross-sectional area.
/// 
/// The wind force is output independently and summed by PhysicsIntegrator
/// with forces from other models (e.g., GravityModel, DragModel, MagnetismModel).
/// This enables true parallel execution of force models on multi-core systems.
/// </summary>
public class WindForceModel : ISimulationModel
{
    private const float AirDensity = 1.225f; // kg/m³ at sea level
    private const float DragCoefficientSphere = 0.47f; // Typical drag coefficient for a sphere
    private Vector3 _windVelocity; // Wind velocity vector in m/s

    public WindForceModel(Vector3? windVelocity = null)
    {
        // Default wind blows in positive z direction at 1 m/s
        _windVelocity = windVelocity ?? new Vector3(0, 0, 5.0f);
    }

    /// <summary>
    /// Sets the wind velocity vector.
    /// </summary>
    public void SetWindVelocity(Vector3 velocity)
    {
        _windVelocity = velocity;
    }

    /// <summary>
    /// Executes wind force calculation on entities that have CurrentSpeed and Radius.
    /// Calculates wind resistance based on wind velocity and object shape.
    /// </summary>
    public async Task<Dictionary<string, List<object>>> ExecuteAsync(PropertyArrayBundle inputBundle)
    {
        if (inputBundle == null)
            throw new ArgumentNullException(nameof(inputBundle));

        return await Task.Run(() =>
        {
            // Extract input arrays
            var radiusValues = inputBundle.Arrays.ContainsKey("Radius") ? inputBundle.Arrays["Radius"] : new List<object>();

            // Output array for wind forces
            var outputForces = new List<object>();

            if (inputBundle.ValidEntityIds.Count == 0)
            {
                // No valid entities
                return new Dictionary<string, List<object>>
                {
                    { "WindForce", outputForces }
                };
            }

            // Calculate wind force for each entity
            foreach (var entityId in inputBundle.ValidEntityIds)
            {
                // Get the index of this entity in each property array
                int radiusIndex = inputBundle.EntityToPropertyIndices[entityId]["Radius"];

                // Extract radius for wind calculation
                var radius = radiusIndex < radiusValues.Count
                    ? radiusValues[radiusIndex] as float? ?? 0.01f
                    : 0.01f;

                // Calculate wind force
                Vector3 windForce = CalculateWindForce(radius);
                outputForces.Add(windForce);
            }

            return new Dictionary<string, List<object>>
            {
                { "WindForce", outputForces }
            };
        });
    }

    /// <summary>
    /// Calculates wind force based on wind velocity and object radius.
    /// Wind applies force in the direction of the wind.
    /// </summary>
    private Vector3 CalculateWindForce(float radius)
    {
        if (radius <= 0)
            return Vector3.Zero;

        float windSpeedMagnitude = _windVelocity.Length();
        Vector3 windForce = Vector3.Zero;

        if (windSpeedMagnitude > 0)
        {
            // Wind force: F_wind = 0.5 * ρ * v_wind² * Cd * A
            float crossSectionArea = MathF.PI * radius * radius;
            float windForceMagnitude = 0.5f * AirDensity * windSpeedMagnitude * windSpeedMagnitude 
                                      * DragCoefficientSphere * crossSectionArea;

            // Wind force acts in the direction of wind
            Vector3 windDirection = _windVelocity / windSpeedMagnitude;
            windForce = windDirection * windForceMagnitude;
        }

        return windForce;
    }
}