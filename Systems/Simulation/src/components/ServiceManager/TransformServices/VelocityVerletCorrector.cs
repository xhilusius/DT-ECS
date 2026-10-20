namespace Simulation.ServiceManager.TransformServices;

using Simulation.Interfaces;
using Simulation.StateManager;

public class VelocityVerletCorrector : ITransformService
{
    private readonly double _timeStepSeconds;

    public VelocityVerletCorrector(float timeStepSeconds)
    {
        if (timeStepSeconds <= 0)
            throw new ArgumentOutOfRangeException(nameof(timeStepSeconds), "Time step must be greater than zero.");

        _timeStepSeconds = timeStepSeconds;
    }

    public async Task<Dictionary<string, List<object>>> ExecuteAsync(PropertyArrayBundle inputBundle)
    {
        ArgumentNullException.ThrowIfNull(inputBundle);

        return await Task.Run(() =>
        {
            var massValues = inputBundle.Arrays["Mass"];
            var speedValues = inputBundle.Arrays["CurrentSpeed"];
            var forceValues = inputBundle.Arrays["GravityForce"];
            var previousAccelerationValues = inputBundle.Arrays["PreviousAcceleration"];
            var correctedSpeeds = new List<object>(speedValues);

            foreach (var entityId in inputBundle.ValidEntityIds)
            {
                var indices = inputBundle.EntityToPropertyIndices[entityId];
                var mass = Convert.ToDouble(massValues[indices["Mass"]]);
                if (mass <= 0)
                    throw new InvalidOperationException($"Entity {entityId} must have positive mass.");

                var speedIndex = indices["CurrentSpeed"];
                var force = GetVector(forceValues, indices["GravityForce"], "GravityForce");
                var previousAcceleration = GetVector(
                    previousAccelerationValues,
                    indices["PreviousAcceleration"],
                    "PreviousAcceleration");
                var speed = GetVector(speedValues, speedIndex, "CurrentSpeed");
                var correctedSpeed = new double[3];

                for (var axis = 0; axis < 3; axis++)
                {
                    var currentAcceleration = force[axis] / mass;
                    correctedSpeed[axis] = speed[axis]
                        + 0.5 * (previousAcceleration[axis] + currentAcceleration) * _timeStepSeconds;
                }

                correctedSpeeds[speedIndex] = correctedSpeed;
            }

            return new Dictionary<string, List<object>>
            {
                ["CurrentSpeed"] = correctedSpeeds
            };
        });
    }

    private static double[] GetVector(List<object> values, int index, string propertyName)
    {
        if (index >= 0 && index < values.Count && values[index] is double[] vector && vector.Length == 3)
            return vector;

        throw new InvalidOperationException($"Entity property '{propertyName}' must be a 3D double vector.");
    }
}