namespace Simulation.ServiceManager.SimulationModels;

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
/// </summary>
public class PositionModel : ISimulationModel
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

            // Prepare output arrays - Position will be updated, Displacement will be reset to zero
            var newPositions = new List<object>();
            var resetDisplacements = new List<object>();

            if (inputBundle.ValidEntityIds.Count == 0)
            {
                // No valid entities to process
                return new Dictionary<string, List<object>> 
                { 
                    { "Position", new List<object>() },
                    { "Displacement", new List<object>() }
                };
            }

            foreach (var entityId in inputBundle.ValidEntityIds)
            {
                // Get the indices of this entity in each property array
                int positionIndex = inputBundle.EntityToPropertyIndices[entityId]["Position"];
                int displacementIndex = inputBundle.EntityToPropertyIndices[entityId]["Displacement"];

                // Extract current position
                var currentPosition = positionIndex < positionValues.Count
                    ? positionValues[positionIndex] as Vector3? ?? Vector3.Zero
                    : Vector3.Zero;

                // Extract displacement (provided by GravityModel or other displacement sources)
                var displacement = displacementIndex < displacementValues.Count
                    ? displacementValues[displacementIndex] as Vector3? ?? Vector3.Zero
                    : Vector3.Zero;

                // Calculate new position: newPosition = currentPosition + displacement
                Vector3 newPosition = currentPosition + displacement;

                newPositions.Add(newPosition);
                // Reset displacement to zero for next timestep
                resetDisplacements.Add(Vector3.Zero);
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
