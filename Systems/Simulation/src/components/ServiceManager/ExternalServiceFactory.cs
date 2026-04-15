namespace Simulation.ServiceManager;

using Simulation.Interfaces;
using Simulation.ServiceManager.ExternalServices;

/// <summary>
/// Factory for creating IExternalService instances based on their configuration name.
/// Mirrors the pattern of TransformServiceFactory for the external-service tier.
/// </summary>
public static class ExternalServiceFactory
{
    /// <summary>
    /// Creates an external service instance by name.
    /// </summary>
    /// <param name="serviceName">Configuration name of the service (e.g., "SensingService")</param>
    /// <returns>A new instance of the requested IExternalService</returns>
    /// <exception cref="ArgumentException">If the service name is not recognised</exception>
    public static IExternalService Create(string serviceName)
    {
        if (string.IsNullOrWhiteSpace(serviceName))
            throw new ArgumentException("Service name cannot be null or empty", nameof(serviceName));

        return serviceName switch
        {
            "SensingService"   => new SensingService(),
            "ActuatingService" => new ActuatingService(),
            _ => throw new ArgumentException($"Unknown external service: '{serviceName}'", nameof(serviceName))
        };
    }
}
