namespace Simulation.ServiceManager.CompositeServices;

using Simulation.EntityManager;
using Simulation.Interfaces;

/// <summary>
/// Composite service that runs an inner isolated physics simulation for a single test case.
///
/// Responsibilities:
/// - Receives parsed step-0 entity spawn actions and mid-simulation step actions from
///   <see cref="TestExecutorService"/> at construction time.
/// - On <see cref="ExecuteAsync"/>: creates a fresh inner service stack, spawns step-0
///   entities into it, then drives the N-step physics loop — applying any mid-simulation
///   actions (spawn / remove) at the correct step before calling <see cref="IInnerService.OneStepAsync"/>.
/// - Propagates cancellation and pause signals into the per-step loop.
///
/// The outer <see cref="EntityManager"/> is NOT used here.
/// All entity lifecycle operations target the isolated inner store created by
/// <see cref="IInnerServiceFactory.CreateInnerServiceAsync"/>.
/// </summary>
public class TestSimulationService : ICompositeService
{
    private string _innerSetup;
    private readonly List<TestActionDefinition> _step0Actions;
    private readonly Dictionary<int, List<TestActionDefinition>> _midSimActions;
    private readonly IInnerServiceFactory _innerFactory;
    private readonly int _printEveryNSteps;
    private readonly bool _printOnlyFirstAndLast;

    public TestSimulationService(
        string innerSetup,
        List<TestActionDefinition> step0Actions,
        Dictionary<int, List<TestActionDefinition>> midSimActions,
        IInnerServiceFactory innerFactory,
        int printEveryNSteps = 1,
        bool printOnlyFirstAndLast = false)
    {
        _innerSetup  = innerSetup  ?? throw new ArgumentNullException(nameof(innerSetup));
        _step0Actions = step0Actions ?? throw new ArgumentNullException(nameof(step0Actions));
        _midSimActions = midSimActions ?? throw new ArgumentNullException(nameof(midSimActions));
        _innerFactory  = innerFactory  ?? throw new ArgumentNullException(nameof(innerFactory));
        _printEveryNSteps = printEveryNSteps > 0 ? printEveryNSteps : 1;
        _printOnlyFirstAndLast = printOnlyFirstAndLast;
    }

    /// <inheritdoc/>
    public Task InitializeAsync(string setupName)
    {
        _innerSetup = setupName;
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task ExecuteAsync(CancellationToken ct, PauseHandle pauseHandle)
    {
        var inner = await _innerFactory.CreateInnerServiceAsync(_innerSetup, silent: false);

        // Spawn step-0 entities into the fresh inner store
        foreach (var action in _step0Actions)
            await ApplyActionAsync(inner, action);

        int steps = inner.SimulationSteps;

        // Always report the initial state before any physics step runs
        await inner.ReportStateAsync($"Initial state (step 0 of {steps})");

        for (int step = 1; step <= steps; step++)
        {
            await pauseHandle.WaitIfPausedAsync(ct);
            ct.ThrowIfCancellationRequested();

            // Apply any mid-simulation actions declared for this step
            if (_midSimActions.TryGetValue(step, out var stepActions))
                foreach (var action in stepActions)
                    await ApplyActionAsync(inner, action);

            await inner.OneStepAsync();
            await inner.UpdateVisualizationAsync();

            if (ShouldPrint(step, steps))
                await inner.ReportStateAsync($"Step {step} of {steps}");
        }

        await inner.StopAsync();
    }

    private bool ShouldPrint(int step, int totalSteps)
    {
        if (_printOnlyFirstAndLast)
            return step == totalSteps;
        return (step % _printEveryNSteps == 0) || step == totalSteps;
    }

    /// <inheritdoc/>
    public Task ReinitializeAsync(string setupName)
    {
        _innerSetup = setupName;
        return Task.CompletedTask;
    }

    // -------------------------------------------------------------------------
    // Action execution helpers
    // -------------------------------------------------------------------------

    private static async Task ApplyActionAsync(IInnerService inner, TestActionDefinition action)
    {
        if (string.Equals(action.Type, "spawn", StringComparison.OrdinalIgnoreCase))
        {
            var props = new Dictionary<string, object>(action.Entity.Properties);
            if (action.PropertyOverrides != null)
                foreach (var (k, v) in action.PropertyOverrides)
                    props[k] = v;

            await inner.CreateEntityAsync(action.Entity.Name, props, action.Entity.Description);
        }
        else if (string.Equals(action.Type, "remove", StringComparison.OrdinalIgnoreCase))
        {
            if (action.PropertyOverrides != null &&
                action.PropertyOverrides.TryGetValue("EntityId", out var idObj) &&
                int.TryParse(idObj.ToString(), out int entityId))
            {
                await inner.RemoveEntityAsync(entityId);
            }
            else
            {
                Console.WriteLine("Warning: remove action missing EntityId property — skipping.");
            }
        }
        else
        {
            Console.WriteLine($"Warning: unsupported action type '{action.Type}' — skipping.");
        }
    }
}
