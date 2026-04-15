namespace Simulation.ServiceManager.CompositeServices;

using Simulation.Interfaces;
using Simulation.PropertyTypes;
/// <summary>
/// Composite service that runs one isolated inner simulation per scenario entity.
///
/// In ECS terms:
///   - Outer entities carrying a <c>ScenarioConfig</c> property are the "scenarios".
///   - This service is scheduled by the outer ServiceManager once per outer step.
///   - For each scenario entity it creates a fresh inner simulation stack
///     (RepositoryManager, EntityManager, StateManager, TransformExecutor, ServiceManager),
///     spawns both the shared base entities and the scenario-specific entities,
///     runs the inner loop for the configured number of steps, then writes a
///     <c>ScenarioResult</c> property value back to the outer store.
///
/// Naming:
///   - "WhatIfService" is the name of this particular instance/use-case.
///   - The abstraction it implements is ICompositeService: any service that
///     internally owns and orchestrates a full inner service pipeline.
/// </summary>
public class WhatIfService : ICompositeService
{
    private string _setupName = string.Empty;

    /// <summary>
    /// Factory that creates isolated inner service sessions on demand.
    /// Provided by the ServiceManager layer; composite services have no knowledge
    /// of the concrete stack behind it.
    /// </summary>
    private readonly IInnerServiceFactory _innerServiceFactory;

    /// <summary>
    /// Entity property defaults keyed by template name (e.g. "Satellite", "Earth_ball").
    /// Injected at construction so WhatIfService has no dependency on any file path.
    /// </summary>
    private readonly IReadOnlyDictionary<string, Dictionary<string, object>> _entityTemplates;

    public WhatIfService(
        IInnerServiceFactory innerServiceFactory,
        IReadOnlyDictionary<string, Dictionary<string, object>> entityTemplates)
    {
        _innerServiceFactory = innerServiceFactory ?? throw new ArgumentNullException(nameof(innerServiceFactory));
        _entityTemplates = entityTemplates ?? throw new ArgumentNullException(nameof(entityTemplates));
    }

    /// <summary>
    /// All scenario configs to process, keyed by their outer entity ID.
    /// Populated by the outer test orchestration before ExecuteAsync is called.
    /// Each config carries its own snapshot of the base entities, so inner sims
    /// are fully isolated from one another.
    /// </summary>
    public Dictionary<int, ScenarioConfig> Scenarios { get; } = new();

    /// <summary>
    /// Results produced after ExecuteAsync completes — one entry per scenario entity ID.
    /// </summary>
    public Dictionary<int, ScenarioResult> Results { get; } = new();

    /// <inheritdoc/>
    public Task InitializeAsync(string setupName)
    {
        if (string.IsNullOrWhiteSpace(setupName))
            throw new ArgumentException("Setup name cannot be null or empty", nameof(setupName));

        _setupName = setupName;
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task ExecuteAsync(CancellationToken ct, PauseHandle pauseHandle)
    {
        Results.Clear();

        foreach (var (entityId, config) in Scenarios)
        {
            await pauseHandle.WaitIfPausedAsync(ct);
            ct.ThrowIfCancellationRequested();

            var result = await RunSingleScenarioAsync(entityId, config);
            Results[entityId] = result;
        }
    }

    /// <inheritdoc/>
    public async Task ReinitializeAsync(string setupName)
    {
        Scenarios.Clear();
        Results.Clear();
        await InitializeAsync(setupName);
    }

    // -------------------------------------------------------------------------
    // Inner simulation runner
    // -------------------------------------------------------------------------

    private async Task<ScenarioResult> RunSingleScenarioAsync(int scenarioEntityId, ScenarioConfig config)
    {
        try
        {
            var inner = await _innerServiceFactory.CreateInnerServiceAsync(config.SetupName, silent: true);

            // Spawn base entities (Earth, pre-existing satellites) — each scenario carries
            // its own snapshot, so inner sims are isolated from each other.
            foreach (var baseEntity in config.BaseEntities)
            {
                await inner.CreateEntityAsync(
                    baseEntity.Name,
                    new Dictionary<string, object>(baseEntity.Properties),
                    baseEntity.Description);
            }

            // Spawn the scenario-specific entities (the candidate(s))
            foreach (var spawn in config.EntitySpawns)
            {
                if (!_entityTemplates.TryGetValue(spawn.TemplateName, out var templateProps))
                    throw new KeyNotFoundException(
                        $"Entity template '{spawn.TemplateName}' not found in injected entity library.");

                var properties = new Dictionary<string, object>(templateProps);
                foreach (var (key, value) in spawn.PropertyOverrides)
                    properties[key] = value;

                await inner.CreateEntityAsync(spawn.TemplateName, properties);
            }

            int steps = inner.SimulationSteps;
            for (int step = 0; step < steps; step++)
                await inner.OneStepAsync();

            await inner.StopAsync();

            return new ScenarioResult(scenarioEntityId, true, steps, $"Inner sim completed ({steps} steps)");
        }
        catch (Exception ex)
        {
            return new ScenarioResult(scenarioEntityId, false, 0, $"Failed: {ex.Message}");
        }
    }

}
