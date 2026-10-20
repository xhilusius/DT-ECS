namespace Simulation.ServiceManager.TransformServices;

using Simulation.Interfaces;
using Simulation.StateManager;

public class VelocityVerletPredictor : ITransformService
{
    private readonly double _timeStepSeconds;

    public VelocityVerletPredictor(float timeStepSeconds)
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
            var positionValues = inputBundle.Arrays["Position"];
            var speedValues = inputBundle.Arrays["CurrentSpeed"];
            var forceValues = inputBundle.Arrays["GravityForce"];
            var accelerationValues = inputBundle.Arrays["PreviousAcceleration"];

            var predictedPositions = new List<object>(positionValues);
            var previousAccelerations = new List<object>(accelerationValues);

            foreach (var entityId in inputBundle.ValidEntityIds)
            {
                var indices = inputBundle.EntityToPropertyIndices[entityId];
                var mass = Convert.ToDouble(massValues[indices["Mass"]]);
                if (mass <= 0)
                    throw new InvalidOperationException($"Entity {entityId} must have positive mass.");

                var positionIndex = indices["Position"];
                var speedIndex = indices["CurrentSpeed"];
                var forceIndex = indices["GravityForce"];
                var accelerationIndex = indices["PreviousAcceleration"];
                var position = GetVector(positionValues, positionIndex, "Position");
                var speed = GetVector(speedValues, speedIndex, "CurrentSpeed");
                var force = GetVector(forceValues, forceIndex, "GravityForce");

                var acceleration = new double[3];
                var predictedPosition = new double[3];
                for (var axis = 0; axis < 3; axis++)
                {
                    acceleration[axis] = force[axis] / mass;
                    predictedPosition[axis] = position[axis]
                        + speed[axis] * _timeStepSeconds
                        + 0.5 * acceleration[axis] * _timeStepSeconds * _timeStepSeconds;
                }

                predictedPositions[positionIndex] = predictedPosition;
                previousAccelerations[accelerationIndex] = acceleration;
            }

            return new Dictionary<string, List<object>>
            {
                ["Position"] = predictedPositions,
                ["PreviousAcceleration"] = previousAccelerations
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