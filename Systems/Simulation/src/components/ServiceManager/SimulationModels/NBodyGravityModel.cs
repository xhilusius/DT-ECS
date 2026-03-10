namespace Simulation.ServiceManager.SimulationModels;

using System.Numerics;
using Simulation.Interfaces;
using Simulation.StateManager;

/// <summary>
/// N-body gravity model that computes mutual gravitational forces between all entities.
/// 
/// Input properties:  ["Mass", "Position"]
/// Output properties: ["GravityForce"]
/// 
/// Physics:
/// - F = G * m1 * m2 / r^2, direction along the separation vector
/// - G and distance thresholds are scaled based on the position unit from PropertiesConfig
/// - Default: G = 6.67430e-11 m^3 kg^-1 s^-2 (SI units for meters)
/// - For km: G is scaled by 1e-6 to maintain correct force magnitudes
/// 
/// Each entity''s net force is the sum of pairwise forces from all other entities.
/// Uses double precision for Position and Force calculations to handle Earth-scale simulations.
/// </summary>
public class NBodyGravityModel : ISimulationModel
{
    private const double GravitationalConstantSI = 6.67430e-11; // m^3 kg^-1 s^-2
    private const double MinDistanceSI = 0.001; // Softening threshold in meters

    private readonly double _gravitationalConstant;
    private readonly double _minDistance;

    /// <summary>
    /// Creates N-body gravity model with default SI units (meters).
    /// </summary>
    public NBodyGravityModel(float timeStepSeconds)
        : this(timeStepSeconds, 1.0)
    {
        // Time step not needed for gravity calculation (force only)
    }

    /// <summary>
    /// Creates N-body gravity model with unit scaling.
    /// </summary>
    /// <param name="timeStepSeconds">Simulation time step (not used for gravity)</param>
    /// <param name="unitScale">How many SI units per property unit (e.g., 1000 for km, 1 for m)</param>
    public NBodyGravityModel(float timeStepSeconds, double unitScale)
    {
        if (unitScale <= 0)
            throw new ArgumentException("Unit scale must be greater than zero", nameof(unitScale));

        // Scale constants for the given unit system
        // If distance is scaled by unitScale, then distance� is scaled by unitScale�
        // So G must be scaled by 1/(unitScale�) to maintain correct force magnitudes
        _gravitationalConstant = GravitationalConstantSI / (unitScale * unitScale);
        
        // Min distance threshold should also be in the same units
        _minDistance = MinDistanceSI / unitScale;
    }

    /// <summary>
    /// Executes N-body gravity force calculation on entities with Mass and Position.
    /// </summary>
    public async Task<Dictionary<string, List<object>>> ExecuteAsync(PropertyArrayBundle inputBundle)
    {
        if (inputBundle == null)
            throw new ArgumentNullException(nameof(inputBundle));

        return await Task.Run(() =>
        {
            var massValues = inputBundle.Arrays.ContainsKey("Mass") ? inputBundle.Arrays["Mass"] : new List<object>();
            var positionValues = inputBundle.Arrays.ContainsKey("Position") ? inputBundle.Arrays["Position"] : new List<object>();
            var gravityForceValues = inputBundle.Arrays.ContainsKey("GravityForce") ? inputBundle.Arrays["GravityForce"] : new List<object>();

            // Preserve existing force array to maintain entity indices
            var outputForces = new List<object>(gravityForceValues);

            foreach (var entityId in inputBundle.ValidEntityIds)
            {
                int massIndex = inputBundle.EntityToPropertyIndices[entityId]["Mass"];
                int positionIndex = inputBundle.EntityToPropertyIndices[entityId]["Position"];
                int gravityForceIndex = inputBundle.EntityToPropertyIndices[entityId].ContainsKey("GravityForce")
                    ? inputBundle.EntityToPropertyIndices[entityId]["GravityForce"]
                    : -1;

                var mass = massIndex < massValues.Count
                    ? massValues[massIndex] as float? ?? 1.0f
                    : 1.0f;

                var position = positionIndex < positionValues.Count && positionValues[positionIndex] is double[] posArr && posArr.Length == 3
                    ? posArr
                    : new double[] { 0, 0, 0 };

                double[] totalForce = new double[] { 0, 0, 0 };

                foreach (var otherEntityId in inputBundle.ValidEntityIds)
                {
                    if (otherEntityId == entityId)
                        continue;

                    int otherMassIndex = inputBundle.EntityToPropertyIndices[otherEntityId]["Mass"];
                    int otherPositionIndex = inputBundle.EntityToPropertyIndices[otherEntityId]["Position"];

                    var otherMass = otherMassIndex < massValues.Count
                        ? massValues[otherMassIndex] as float? ?? 1.0f
                        : 1.0f;

                    var otherPosition = otherPositionIndex < positionValues.Count && positionValues[otherPositionIndex] is double[] otherPosArr && otherPosArr.Length == 3
                        ? otherPosArr
                        : new double[] { 0, 0, 0 };

                    // Calculate separation vector
                    double dx = otherPosition[0] - position[0];
                    double dy = otherPosition[1] - position[1];
                    double dz = otherPosition[2] - position[2];
                    double distance = Math.Sqrt(dx * dx + dy * dy + dz * dz);

                    if (distance <= _minDistance)
                        continue;

                    double forceMagnitude = _gravitationalConstant * mass * otherMass / (distance * distance);
                    
                    // Apply force in direction of separation (normalized)
                    totalForce[0] += (dx / distance) * forceMagnitude;
                    totalForce[1] += (dy / distance) * forceMagnitude;
                    totalForce[2] += (dz / distance) * forceMagnitude;
                }

                // Update or add force at correct index to preserve alignment
                if (gravityForceIndex >= 0 && gravityForceIndex < outputForces.Count)
                    outputForces[gravityForceIndex] = totalForce;
                else
                    outputForces.Add(totalForce);
            }

            return new Dictionary<string, List<object>>
            {
                { "GravityForce", outputForces }
            };
        });
    }
}
