namespace Simulation.ServiceManager;

using DataStorage.RepositoryManager;
using Simulation.EntityManager;
using Simulation.Interfaces;
using Simulation.StateManager;
using Simulation.TransformExecutor;

/// <summary>
/// Concrete implementation of <see cref="IInnerService"/>.
/// Wraps a fully wired simulation stack (RepositoryManager, EntityManager,
/// StateManager, TransformExecutor, ServiceManager, InteractionController)
/// behind the minimal surface composite services need.
///
/// Created exclusively by <see cref="ServiceManager"/> via <see cref="IInnerServiceFactory"/>.
/// </summary>
internal sealed class InnerService : IInnerService
{
    private readonly global::Simulation.InteractionController.InteractionController _controller;

    public int SimulationSteps { get; }

    internal InnerService(
        global::Simulation.InteractionController.InteractionController controller,
        int simulationSteps)
    {
        _controller = controller;
        SimulationSteps = simulationSteps;
    }

    public Task CreateEntityAsync(string name, Dictionary<string, object> properties, string? description = null)
        => _controller.CreateEntityAsync(name, properties, description);

    public Task OneStepAsync()
        => _controller.OneStepAsync();

    public Task StopAsync()
        => _controller.StopAsync();
}
