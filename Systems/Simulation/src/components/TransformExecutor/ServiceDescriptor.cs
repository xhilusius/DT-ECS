namespace Simulation.TransformExecutor;

using Simulation.Interfaces;

/// <summary>
/// Describes a simulation service's input and output properties.
/// Allows ServiceManager to determine execution order based on dependencies.
/// 
/// A ServiceDescriptor contains:
/// - Service: The ISimulationModel to execute
/// - InputProperties: Property names this service needs (must be available before execution)
/// - OutputProperties: Property names this service produces (available after execution)
/// 
/// ServiceManager uses this to build execution batches:
/// - A service can execute when all its InputProperties are available
/// - After execution, OutputProperties become available for dependent services
/// </summary>
public class ServiceDescriptor
{
    /// <summary>
    /// Unique identifier for this service.
    /// </summary>
    public string ServiceName { get; set; }

    /// <summary>
    /// The actual service to execute (must implement ITransformService).
    /// </summary>
    public ITransformService Service { get; set; }

    /// <summary>
    /// List of property names this service requires as input (must exist before execution).
    /// </summary>
    public List<string> InputProperties { get; set; }

    /// <summary>
    /// Optional input properties to fetch when available (do not gate execution).
    /// </summary>
    public List<string> OptionalInputProperties { get; set; }

    /// <summary>
    /// List of property names this service produces as output (created/modified by execution).
    /// </summary>
    public List<string> OutputProperties { get; set; }

    public ServiceDescriptor(string serviceName, ITransformService service, List<string> inputProperties, List<string> outputProperties, List<string>? optionalInputProperties = null)
    {
        if (string.IsNullOrWhiteSpace(serviceName))
            throw new ArgumentException("Service name cannot be null or empty", nameof(serviceName));
        if (service == null)
            throw new ArgumentNullException(nameof(service));

        ServiceName = serviceName;
        Service = service;
        InputProperties = inputProperties ?? new List<string>();
        OptionalInputProperties = optionalInputProperties ?? new List<string>();
        OutputProperties = outputProperties ?? new List<string>();
    }
}
