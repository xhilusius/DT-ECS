namespace Simulation.Interfaces;

/// <summary>
/// Factory that creates fully wired, isolated inner service sessions.
/// Implemented by the ServiceManager layer, which is the appropriate owner
/// of the infrastructure knowledge required to assemble a full simulation stack.
///
/// Composite services that need inner simulations depend only on this interface,
/// keeping them free of any coupling to concrete infrastructure types.
/// </summary>
public interface IInnerServiceFactory
{
    /// <summary>
    /// Creates and initializes a new isolated inner service session.
    /// The session is loaded from the named setup configuration
    /// (e.g. "SatelliteSetup") and is ready to receive entities.
    /// </summary>
    Task<IInnerService> CreateInnerServiceAsync(string setupName);
}
