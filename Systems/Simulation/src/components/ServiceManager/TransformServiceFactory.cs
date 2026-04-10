namespace Simulation.ServiceManager;

using Simulation.Interfaces;
using Simulation.ServiceManager.TransformServices;

/// <summary>
/// Factory for creating TransformService instances based on their type names.
/// Maps configuration names to actual ITransformService implementations.
/// </summary>
public class TransformServiceFactory
{
    /// <summary>
    /// Creates a simulation model instance by name with SI units (default).
    /// </summary>
    /// <param name="modelName">Name of the model (e.g., "GravityModel", "PositionModel")</param>
    /// <param name="timeStepSeconds">Time step in seconds</param>
    /// <returns>An instance of the requested ITransformService</returns>
    /// <exception cref="ArgumentException">If the service name is not recognized</exception>
    public static ITransformService CreateModel(string modelName, float timeStepSeconds)
    {
        if (string.IsNullOrWhiteSpace(modelName))
            throw new ArgumentException("Model name cannot be null or empty", nameof(modelName));

        return modelName switch
        {
            "GravityModel" => new GravityModel(timeStepSeconds),
            "NBodyGravityModel" => new NBodyGravityModel(timeStepSeconds),
            "DragModel" => new DragModel(),
            "MagnetismModel" => new MagnetismModel(timeStepSeconds),
            "WindForceModel" => new WindForceModel(),
            "PhysicsIntegrator" => new PhysicsIntegrator(timeStepSeconds),
            "PositionModel" => new PositionModel(),
            "CollisionDetectionModel" => new CollisionDetectionModel(),
            _ => throw new ArgumentException($"Unknown simulation model: {modelName}")
        };
    }
}

