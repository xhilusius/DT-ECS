namespace Simulation.ServiceManager.ExternalServices;

using Simulation.Interfaces;

/// <summary>
/// Stub sensing service that represents the system-boundary step of reading real-world state.
/// 
/// In a full implementation this would read from hardware interfaces, sensor APIs, or telemetry
/// feeds and normalise the results into entity properties on the outer entity store.
/// 
/// MVP: prints a line to confirm it is scheduled and called correctly within the outer pipeline.
/// No property reads or writes are performed.
/// </summary>
public class SensingService : IExternalService
{
    public async Task ExecuteAsync()
    {
        Console.WriteLine("[SensingService] Sensing: reading environment state");
        await Task.Delay(1500);
    }
}
