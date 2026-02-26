namespace Simulation.ServiceManager;

using Simulation.Interfaces;
using Simulation.ServiceManager.SimulationModels;

/// <summary>
/// Factory for creating simulation model instances based on their type names.
/// Maps configuration names to actual ISimulationModel implementations.
/// </summary>
public class SimulationModelFactory
{
    /// <summary>
    /// Creates a simulation model instance by name.
    /// </summary>
    /// <param name="modelName">Name of the model (e.g., "GravityModel", "PositionModel")</param>
    /// <returns>An instance of the requested ISimulationModel</returns>
    /// <exception cref="ArgumentException">If the model name is not recognized</exception>
    public static ISimulationModel CreateModel(string modelName, float timeStepSeconds)
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
            _ => throw new ArgumentException($"Unknown simulation model: {modelName}")
        };
    }
}
