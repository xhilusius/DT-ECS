namespace Simulation.ServiceManager.CompositeServices;

using Simulation.EntityManager;
using Simulation.Interfaces;
using Simulation.PropertyTypes;
using Simulation.StateManager;

/// <summary>
/// Collision event captured inside a what-if inner simulation.
/// Private to WhatIfService — this is collision-domain knowledge.
/// </summary>
file record WhatIfCollisionEvent(
    int EntityId,
    string EntityName,
    int OtherEntityId,
    string OtherEntityName,
    double[] EntityPosition);

/// <summary>
/// Composite service that runs one isolated inner simulation per what-if scenario.
///
/// Reads its inputs from the outer ECS property arrays like any other transform service:
/// entities that carry a <see cref="WhatIfLabel"/> are scenario candidates;
/// all remaining entities form the shared base world (Earth, background constellation, target).
///
/// For each candidate entity the service:
///   1. Creates a fresh inner simulation stack.
///   2. Spawns all base-world entities.
///   3. Spawns the candidate entity.
///   4. Advances the inner loop step-by-step, stopping immediately on a collision.
///   5. Writes a <see cref="ScenarioResult"/> back to the candidate's outer property slot.
///
/// After all scenarios finish, a consolidated what-if summary table is printed.
/// </summary>
public class WhatIfService : ICompositeService
{
    private string _setupName       = string.Empty;
    private double _timeStepSeconds = 1.0;

    private readonly IInnerServiceFactory _innerServiceFactory;
    private readonly EntityManager        _outerEntityManager;

    public WhatIfService(IInnerServiceFactory innerServiceFactory, EntityManager outerEntityManager)
    {
        _innerServiceFactory = innerServiceFactory ?? throw new ArgumentNullException(nameof(innerServiceFactory));
        _outerEntityManager  = outerEntityManager  ?? throw new ArgumentNullException(nameof(outerEntityManager));
    }

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
        var stateManager = _outerEntityManager.GetStateManager();

        // ── 1. Load all property arrays into a local cache (one read per type) ─
        var propArrayCache = await BuildPropertyArrayCacheAsync(stateManager);

        // ── 2. Classify entities into candidates vs base-world ────────────────
        var candidateIds = new HashSet<int>(
            _outerEntityManager.GetEntitiesForProperty("WhatIfLabel"));

        var baseEntityIds = _outerEntityManager.GetAllEntityIds()
            .Where(id => !candidateIds.Contains(id))
            .ToList();

        // ── 3. Build base-world snapshots once (shared across all scenarios) ──
        var baseEntities = baseEntityIds
            .Select(id => BuildSnapshotFromCache(id, propArrayCache))
            .ToList();

        // ── 4. Run one inner simulation per candidate (ordered by entity ID) ──
        var orderedCandidates = candidateIds.OrderBy(id => id).ToList();
        int total   = orderedCandidates.Count;
        int current = 0;

        var allResults = new List<ScenarioResult>(total);

        foreach (var candidateId in orderedCandidates)
        {
            current++;
            await pauseHandle.WaitIfPausedAsync(ct);
            ct.ThrowIfCancellationRequested();

            // Resolve scenario label and run inner simulation
            string label = ResolveLabel(candidateId, propArrayCache, current);

            Console.WriteLine($"  Running scenario [{current}/{total}]: {label} …");

            var candidateSnapshot = BuildSnapshotFromCache(candidateId, propArrayCache);
            var result = await RunSingleScenarioAsync(candidateId, label, baseEntities, candidateSnapshot);
            allResults.Add(result);

            await WriteResultToOuterStoreAsync(candidateId, result, stateManager);
        }

        PrintSummary(allResults);
    }

    /// <inheritdoc/>
    public async Task ReinitializeAsync(string setupName)
    {
        await InitializeAsync(setupName);
    }

    // -------------------------------------------------------------------------
    // Outer-store helpers
    // -------------------------------------------------------------------------

    /// <summary>Reads every property type present in the outer store into a dictionary.</summary>
    private async Task<Dictionary<string, List<object>>> BuildPropertyArrayCacheAsync(
        StateManager stateManager)
    {
        var cache = new Dictionary<string, List<object>>(StringComparer.OrdinalIgnoreCase);

        var allPropTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entityId in _outerEntityManager.GetAllEntityIds())
        {
            foreach (var pt in _outerEntityManager.GetEntityComposition(entityId))
                allPropTypes.Add(pt);
        }

        foreach (var pt in allPropTypes)
        {
            var arr = await stateManager.GetPropertiesByTypeAsync(pt);
            if (arr != null) cache[pt] = arr;
        }

        return cache;
    }

    /// <summary>
    /// Builds a <see cref="BaseEntitySnapshot"/> using the pre-loaded property array cache.
    /// Meta-properties (<c>WhatIfLabel</c>, <c>ScenarioResult</c>) are excluded so they
    /// are not forwarded into inner simulations.
    /// </summary>
    private BaseEntitySnapshot BuildSnapshotFromCache(
        int entityId,
        Dictionary<string, List<object>> propArrayCache)
    {
        var composition = _outerEntityManager.GetEntityComposition(entityId);
        var props       = new Dictionary<string, object>();

        foreach (var propType in composition)
        {
            if (propType is "WhatIfLabel" or "ScenarioResult") continue;

            if (!propArrayCache.TryGetValue(propType, out var array)) continue;

            int idx = _outerEntityManager.GetEntityIndexInProperty(entityId, propType);
            if (idx >= 0 && idx < array.Count)
                props[propType] = array[idx];
        }

        var entity = _outerEntityManager.GetEntity(entityId);
        return new BaseEntitySnapshot(
            entity?.Name ?? $"Entity_{entityId}",
            entity?.Description,
            props);
    }

    /// <summary>Resolves the human-readable scenario label from the WhatIfLabel property array.</summary>
    private string ResolveLabel(
        int candidateId,
        Dictionary<string, List<object>> propArrayCache,
        int fallbackIndex)
    {
        int labelIdx = _outerEntityManager.GetEntityIndexInProperty(candidateId, "WhatIfLabel");
        if (propArrayCache.TryGetValue("WhatIfLabel", out var labelArr)
            && labelIdx >= 0 && labelIdx < labelArr.Count)
        {
            return ((WhatIfLabel)labelArr[labelIdx]).Value;
        }
        return $"Scenario {fallbackIndex}";
    }

    /// <summary>
    /// Patches the candidate entity's <see cref="ScenarioResult"/> slot in the outer store
    /// with the completed result.
    /// </summary>
    private async Task WriteResultToOuterStoreAsync(
        int candidateId,
        ScenarioResult result,
        StateManager stateManager)
    {
        var resultArray = await stateManager.GetPropertiesByTypeAsync("ScenarioResult");
        if (resultArray == null) return;

        int idx = _outerEntityManager.GetEntityIndexInProperty(candidateId, "ScenarioResult");
        if (idx >= 0 && idx < resultArray.Count)
        {
            resultArray[idx] = result;
            await stateManager.SetPropertiesByTypeAsync("ScenarioResult", resultArray);
        }
    }

    // -------------------------------------------------------------------------
    // Inner simulation runner
    // -------------------------------------------------------------------------

    private async Task<ScenarioResult> RunSingleScenarioAsync(
        int candidateEntityId,
        string label,
        IReadOnlyList<BaseEntitySnapshot> baseEntities,
        BaseEntitySnapshot candidateSnapshot)
    {
        try
        {
            var inner = await _innerServiceFactory.CreateInnerServiceAsync(_setupName, silent: true);
            _timeStepSeconds = inner.TimeStepSeconds;

            // Spawn shared base entities (Earth, background constellation, target)
            foreach (var baseEntity in baseEntities)
            {
                await inner.CreateEntityAsync(
                    baseEntity.Name,
                    new Dictionary<string, object>(baseEntity.Properties),
                    baseEntity.Description);
            }

            // Spawn the scenario candidate
            await inner.CreateEntityAsync(
                candidateSnapshot.Name,
                new Dictionary<string, object>(candidateSnapshot.Properties),
                candidateSnapshot.Description);

            int maxSteps = inner.SimulationSteps;
            WhatIfCollisionEvent? collision = null;
            int collisionStep = 0;

            for (int step = 1; step <= maxSteps; step++)
            {
                await inner.OneStepAsync();

                // Query the generic property API; cast to CollisionRecord here where
                // we have the domain knowledge to do so.
                var entries = await inner.GetPropertyValuesAsync("CollisionDetected");
                foreach (var (entityId, entityName, rawValue) in entries)
                {
                    if (rawValue is CollisionRecord cr)
                    {
                        string otherName = entries
                            .FirstOrDefault(e => e.EntityId == cr.CollidedWithEntityId)
                            .EntityName ?? $"Entity_{cr.CollidedWithEntityId}";

                        collision     = new WhatIfCollisionEvent(entityId, entityName,
                            cr.CollidedWithEntityId, otherName, cr.EntityPosition);
                        collisionStep = step;
                        break;
                    }
                }
                if (collision != null) break;
            }

            await inner.StopAsync();

            bool crashed = collision != null;
            return new ScenarioResult(
                ScenarioEntityId:       candidateEntityId,
                Completed:              true,
                StepsExecuted:          crashed ? collisionStep : maxSteps,
                Summary:                crashed
                                            ? $"Crash at step {collisionStep} with '{collision!.OtherEntityName}'"
                                            : $"No collision — {maxSteps} steps completed",
                Label:                  label,
                CollisionDetected:      crashed,
                CollisionAtStep:        collisionStep,
                CollidedWithEntityId:   collision?.OtherEntityId ?? -1,
                CollidedWithEntityName: collision?.OtherEntityName ?? "",
                CollisionPosition:      collision?.EntityPosition);
        }
        catch (Exception ex)
        {
            return new ScenarioResult(
                ScenarioEntityId: candidateEntityId,
                Completed:        false,
                StepsExecuted:    0,
                Summary:          $"Failed: {ex.Message}",
                Label:            label);
        }
    }

    // -------------------------------------------------------------------------
    // Summary output
    // -------------------------------------------------------------------------

    private void PrintSummary(IReadOnlyList<ScenarioResult> results)
    {
        const int W = 72; // total inner width (between ║ and ║)

        Console.WriteLine();
        Console.WriteLine($"╔{new string('═', W)}╗");
        Console.WriteLine($"║{"  WHAT-IF ANALYSIS — RESULTS".PadRight(W)}║");
        Console.WriteLine($"╠{new string('═', W)}╣");

        for (int i = 0; i < results.Count; i++)
        {
            var result = results[i];

            // Scenario label line
            string labelLine = $"  {result.Label}";
            Console.WriteLine($"║{labelLine.PadRight(W)}║");

            if (result.CollisionDetected)
            {
                double realSecs = result.CollisionAtStep * _timeStepSeconds;
                int    mm       = (int)(realSecs / 60);
                int    ss       = (int)(realSecs % 60);

                var pos = result.CollisionPosition;
                string posStr = pos != null && pos.Length >= 3
                    ? $"({pos[0] / 1000.0:F0} km, {pos[1] / 1000.0:F0} km, {pos[2] / 1000.0:F0} km)"
                    : "unknown";

                Console.WriteLine($"║{"    \u2620  CRASH".PadRight(W)}║");
                Console.WriteLine($"║{$"       Step : {result.CollisionAtStep}  ({mm:D2}:{ss:D2} sim-time)".PadRight(W)}║");
                Console.WriteLine($"║{$"       With : {result.CollidedWithEntityName} (id={result.CollidedWithEntityId})".PadRight(W)}║");
                Console.WriteLine($"║{$"       At   : {posStr}".PadRight(W)}║");
            }
            else
            {
                double totalSecs = result.StepsExecuted * _timeStepSeconds;
                int    mm        = (int)(totalSecs / 60);
                int    ss        = (int)(totalSecs % 60);

                Console.WriteLine($"║{"    \u2713  NO CRASH".PadRight(W)}║");
                Console.WriteLine($"║{$"       Steps: {result.StepsExecuted}  ({mm:D2}:{ss:D2} sim-time)".PadRight(W)}║");
            }

            // Use closing border on the last entry, separator on all others
            bool isLast = i == results.Count - 1;
            Console.WriteLine(isLast
                ? $"╚{new string('═', W)}╝"
                : $"╠{new string('═', W)}╣");
        }

        Console.WriteLine();
    }
}

