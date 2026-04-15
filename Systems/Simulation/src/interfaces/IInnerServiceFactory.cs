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
    /// <param name="setupName">The configuration setup name to load.</param>
    /// <param name="silent">When true, suppresses state reporting output. Default false.</param>
    Task<IInnerService> CreateInnerServiceAsync(string setupName, bool silent = false);
}
