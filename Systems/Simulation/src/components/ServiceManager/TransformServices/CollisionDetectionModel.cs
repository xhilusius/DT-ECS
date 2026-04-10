namespace Simulation.ServiceManager.TransformServices;

using Simulation.Interfaces;
using Simulation.PropertyTypes;
using Simulation.StateManager;

/// <summary>
/// Detects pairwise collisions between entities by comparing inter-entity distances to the sum of their radii.
/// 
/// Input properties:  ["Position", "Radius", "CollisionDetected"]
/// Output properties: ["CollisionDetected"]
/// 
/// The "CollisionDetected" input serves two roles:
/// 1. Archetype discriminator: only entities that carry this property participate in collision detection.
///    This allows bodies like Earth to be excluded from inter-satellite collision checks even though
///    they share the same orbital simulation setup.
/// 2. Cumulative flag: once an entity has a CollisionRecord, that record is preserved for the remainder
///    of the simulation — the first collision event is kept even if further collisions occur later.
/// 
/// Output value per entity:
/// - null              → no collision detected yet
/// - CollisionRecord   → first collision: contains the other entity's ID and both entity positions at impact
/// 
/// Physics:
/// - Collision condition: |P_i - P_j| < R_i + R_j
/// - Positions and radii use the units defined in the root PropertiesConfig.json (SI meters by default).
/// - Radius represents the collision detection zone, not the physical body size.
/// 
/// Runs AFTER PositionModel so it operates on updated positions for the current step.
/// </summary>
public class CollisionDetectionModel : ITransformService
{
    /// <summary>
    /// Executes pairwise collision detection for all entities that carry the CollisionDetected property.
    /// </summary>
    public async Task<Dictionary<string, List<object>>> ExecuteAsync(PropertyArrayBundle inputBundle)
    {
        if (inputBundle == null)
            throw new ArgumentNullException(nameof(inputBundle));

        return await Task.Run(() =>
        {
            var positionValues = inputBundle.Arrays.ContainsKey("Position")
                ? inputBundle.Arrays["Position"] : new List<object>();
            var radiusValues = inputBundle.Arrays.ContainsKey("Radius")
                ? inputBundle.Arrays["Radius"] : new List<object>();
            var collisionDetectedValues = inputBundle.Arrays.ContainsKey("CollisionDetected")
                ? inputBundle.Arrays["CollisionDetected"] : new List<object>();

            // Output preserves previous state — cumulative: once collided, stays collided
            var outputCollisions = new List<object>(collisionDetectedValues);

            // Collect entity data for O(n^2) pairwise check
            var entities = new List<(int EntityId, double[] Position, double Radius, int CollisionIndex)>();

            foreach (var entityId in inputBundle.ValidEntityIds)
            {
                var indices = inputBundle.EntityToPropertyIndices[entityId];

                int positionIndex = indices["Position"];
                int radiusIndex = indices["Radius"];
                int collisionIndex = indices["CollisionDetected"];

                var position = positionIndex < positionValues.Count && positionValues[positionIndex] is double[] posArr
                    ? posArr
                    : new double[] { 0, 0, 0 };

                double radius = radiusIndex < radiusValues.Count
                    ? Convert.ToDouble(radiusValues[radiusIndex])
                    : 0.0;

                entities.Add((entityId, position, radius, collisionIndex));
            }

            // Pairwise distance check
            for (int i = 0; i < entities.Count; i++)
            {
                for (int j = i + 1; j < entities.Count; j++)
                {
                    var (entityIdI, posI, radI, colIdxI) = entities[i];
                    var (entityIdJ, posJ, radJ, colIdxJ) = entities[j];

                    double dx = posI[0] - posJ[0];
                    double dy = posI[1] - posJ[1];
                    double dz = posI[2] - posJ[2];
                    double distanceSquared = dx * dx + dy * dy + dz * dz;
                    double collisionThreshold = radI + radJ;

                    if (distanceSquared < collisionThreshold * collisionThreshold)
                    {
                        // Clone position arrays so the record is a true snapshot — immune to
                        // in-place mutations by PositionModel in future steps.
                        if (outputCollisions[colIdxI] is not CollisionRecord)
                            outputCollisions[colIdxI] = new CollisionRecord(entityIdJ, (double[])posI.Clone(), (double[])posJ.Clone());
                        if (outputCollisions[colIdxJ] is not CollisionRecord)
                            outputCollisions[colIdxJ] = new CollisionRecord(entityIdI, (double[])posJ.Clone(), (double[])posI.Clone());
                    }
                }
            }

            return new Dictionary<string, List<object>>
            {
                ["CollisionDetected"] = outputCollisions
            };
        });
    }
}


