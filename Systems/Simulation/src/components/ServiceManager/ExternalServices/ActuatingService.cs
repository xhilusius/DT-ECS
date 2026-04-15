namespace Simulation.ServiceManager.ExternalServices;

using Simulation.Interfaces;

/// <summary>
/// Stub actuating service that represents the system-boundary step of dispatching commands
/// to the real physical system based on simulation outputs.
/// 
/// In a full implementation this would read decision properties from the outer entity store
/// (e.g., AdjustmentRequired, DeltaV) and dispatch the corresponding commands to hardware
/// or a control interface.
/// 
/// MVP: prints a line to confirm it is scheduled and called correctly within the outer pipeline.
/// No property reads or command dispatches are performed.
/// </summary>
public class ActuatingService : IExternalService
{
    public async Task ExecuteAsync()
    {
        Console.WriteLine("[ActuatingService] Actuating: dispatching commands to system");
        await Task.Delay(1500);
    }
}
