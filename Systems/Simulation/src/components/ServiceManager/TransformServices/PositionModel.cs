namespace Simulation.ServiceManager.TransformServices;

using System.Numerics;
using Simulation.Interfaces;
using Simulation.StateManager;
using DataStorage.Interfaces;

/// <summary>
/// Position simulation model that updates entity positions based on accumulated displacement.
/// 
/// Input properties:  ["Position", "Displacement"]
/// Output properties: ["Position", "Displacement"]
/// 
/// IMPORTANT: This model depends on GravityModel (or other models) providing Displacement first.
/// After applying displacement to position, resets Displacement to zero for the next timestep.
/// Pure computation: receives input property arrays, calculates and returns updated positions.
/// SimEngine handles fetching inputs from repository and writing outputs back.
/// Uses double precision for Position and Displacement to handle Earth-scale simulations.
/// </summary>
public class PositionModel : ITransformService
{
    /// <summary>
    /// Executes position update on entities that have Position AND Displacement.
    /// Receives PropertyArrayBundle with input arrays and index mappings.
    /// Updates positions only for valid entities.
    /// </summary>
    public async Task<Dictionary<string, List<object>>> ExecuteAsync(PropertyArrayBundle inputBundle)
    {
        if (inputBundle == null)
            throw new ArgumentNullException(nameof(inputBundle));

        return await Task.Run(() =>
        {
            // Extract input arrays
            var positionValues = inputBundle.Arrays.ContainsKey("Position") ? inputBundle.Arrays["Position"] : new List<object>();
            var displacementValues = inputBundle.Arrays.ContainsKey("Displacement") ? inputBundle.Arrays["Displacement"] : new List<object>();

            // Prepare output arrays - start with copies of original arrays to preserve indices
            // Only update positions for entities in ValidEntityIds that have both properties
            var newPositions = new List<object>(positionValues);
            var resetDisplacements = new List<object>(displacementValues);

            if (inputBundle.ValidEntityIds.Count == 0)
            {
                // No valid entities to process - return unchanged arrays
                return new Dictionary<string, List<object>> 
                { 
                    { "Position", newPositions },
                    { "Displacement", resetDisplacements }
                };
            }

            foreach (var entityId in inputBundle.ValidEntityIds)
            {
                // Get the indices of this entity in each property array
                int positionIndex = inputBundle.EntityToPropertyIndices[entityId]["Position"];
                int displacementIndex = inputBundle.EntityToPropertyIndices[entityId]["Displacement"];

                // Extract current position - now double[]
                var currentPosition = positionIndex < positionValues.Count && positionValues[positionIndex] is double[] pos && pos.Length == 3
                    ? pos
                    : new double[] { 0, 0, 0 };

                // Extract displacement (provided by GravityModel or other displacement sources) - now double[]
                var displacement = displacementIndex < displacementValues.Count && displacementValues[displacementIndex] is double[] disp && disp.Length == 3
                    ? disp
                    : new double[] { 0, 0, 0 };

                // Calculate new position: newPosition = currentPosition + displacement (component-wise)
                double[] newPosition = new double[3];
                newPosition[0] = currentPosition[0] + displacement[0];
                newPosition[1] = currentPosition[1] + displacement[1];
                newPosition[2] = currentPosition[2] + displacement[2];

                // Update at the CORRECT index to preserve array alignment
                newPositions[positionIndex] = newPosition;
                // Reset displacement to zero for next timestep at correct index
                resetDisplacements[displacementIndex] = new double[] { 0, 0, 0 };
            }

            // Return output as dictionary
            return new Dictionary<string, List<object>>
            {
                { "Position", newPositions },
                { "Displacement", resetDisplacements }
            };
        });
    }
}
