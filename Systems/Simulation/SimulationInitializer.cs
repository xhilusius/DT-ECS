namespace Simulation;

using EM = Simulation.EntityManager.EntityManager;
using IC = Simulation.InteractionController.InteractionController;
using RM = DataStorage.RepositoryManager.RepositoryManager;
using Simulation.ServiceManager.CompositeServices;
using SM = Simulation.ServiceManager.ServiceManager;
using ST = Simulation.StateManager.StateManager;
using TE = Simulation.TransformExecutor.TransformExecutor;
using Simulation.StateManager;

/// <summary>
/// Builds and wires the full simulation stack for a given test case file.
/// </summary>
public static class SimulationInitializer
{
    /// <summary>
    /// Creates and wires all simulation components for the test case at <paramref name="tcFilePath"/>.
    /// </summary>
    /// <param name="tcFilePath">Absolute path to the .jsonc test case file.</param>
    /// <param name="visualizationMapper">Optional visualization mapper (Godot, Unity, etc.).</param>
    /// <param name="innerSetupOverride">Optional override for the inner physics setup declared in the TC file.</param>
    /// <returns>An <see cref="IC"/> ready to run via <c>RunAsync()</c>.</returns>
    public static async Task<IC> CreateAsync(
        string tcFilePath,
        VisualizationMapper? visualizationMapper = null,
        string? innerSetupOverride = null)
    {
        var repositoryManager = new RM();
        var entityManager     = new EM();
        var stateManager      = new ST(repositoryManager, entityManager);
        entityManager.SetStateManager(stateManager);

        if (visualizationMapper != null)
            stateManager.SetVisualizationMapper(visualizationMapper);

        var transformExecutor = new TE(stateManager);

        // ServiceManager is passed as IInnerServiceFactory only — no InitializeAsync call here.
        var serviceManager = new SM(transformExecutor);

        var executorService = new TestExecutorService(entityManager, serviceManager);
        await executorService.InitializeAsync(tcFilePath, innerSetupOverride);

        return new IC(entityManager, executorService);
    }
}
