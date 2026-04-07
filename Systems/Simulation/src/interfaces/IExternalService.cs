namespace Simulation.Interfaces;

/// <summary>
/// Service that crosses the system boundary — either injecting external data into the
/// property store (sensing) or dispatching commands to the real system (actuating).
/// 
/// Unlike ITransformService, ExternalServices are not pure — they have side effects
/// and do not operate on pre-fetched property arrays. They receive the context they
/// need (StateManager, EntityManager) via constructor injection, not through the call.
/// 
/// In a full implementation:
/// - Sensing services read from hardware interfaces, networks, or sensor APIs
///   and write normalised values into the property store as entity properties
/// - Actuating services read decision outputs from the property store
///   and dispatch commands to the real physical system
/// 
/// In an MVP implementation sensing may return synthetic or hardcoded data,
/// and actuating may only log its output. The interface remains structurally
/// correct for future real-system integration without changes at the call site.
/// 
/// Examples:
/// - SatelliteSensingService:  reads telemetry → writes Position, CurrentSpeed to outer store
/// - OrbitCorrectionActuator:  reads AdjustmentRequired, NokFraction → logs / dispatches DeltaV
/// </summary>
public interface IExternalService
{
    /// <summary>
    /// Executes the external interaction.
    /// Sensing: injects real-world (or synthetic) data into the property store.
    /// Actuating: reads decision state and dispatches commands to the real system.
    /// </summary>
    Task ExecuteAsync();
}
