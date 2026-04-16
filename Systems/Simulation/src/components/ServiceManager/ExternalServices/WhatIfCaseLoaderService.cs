namespace Simulation.ServiceManager.ExternalServices;

using Simulation.EntityManager;
using Simulation.PropertyTypes;

/// <summary>
/// Sensing-phase service that creates outer-ECS entities for a what-if test case.
///
/// Reads pre-parsed base entities and scenario definitions and registers them in the
/// outer <see cref="EntityManager"/> so that <see cref="WhatIfService"/> can read them
/// as flat property arrays — exactly like any other ECS transform service.
///
/// <b>Base-world entities</b> (Earth, background constellation, target) receive only
/// physical properties (<c>Position</c>, <c>CurrentSpeed</c>, <c>Radius</c>,
/// <c>Color</c>, plus any template extras such as <c>Mass</c>).
///
/// <b>Scenario entities</b> receive the same physical properties as their candidate
/// snapshot, plus a <see cref="WhatIfLabel"/> discriminant and a default
/// <see cref="ScenarioResult"/> placeholder that <see cref="WhatIfService"/>
/// overwrites after the inner simulation for that scenario completes.
/// </summary>
public class WhatIfCaseLoaderService
{
    private readonly EntityManager _entityManager;

    /// <summary>
    /// Inner-simulation setup name stored during <see cref="InitializeAsync"/>
    /// and exposed so the caller can forward it to <see cref="WhatIfService.InitializeAsync"/>.
    /// </summary>
    public string InnerSetupName { get; private set; } = string.Empty;

    public WhatIfCaseLoaderService(EntityManager entityManager)
    {
        _entityManager = entityManager ?? throw new ArgumentNullException(nameof(entityManager));
    }

    /// <summary>
    /// Creates outer entities for the base world and for every what-if scenario candidate.
    /// </summary>
    /// <param name="innerSetupName">
    ///   Physics setup name used by the inner simulations (e.g. "SatelliteSetup").
    /// </param>
    /// <param name="baseEntities">
    ///   Snapshots from step 0: Earth, background constellation, target satellite.
    ///   These entities carry no <see cref="WhatIfLabel"/> — <see cref="WhatIfService"/>
    ///   treats them as the shared background world.
    /// </param>
    /// <param name="scenarios">
    ///   Per-scenario pairs of (<paramref name="label"/>, <paramref name="candidateSnapshot"/>).
    ///   Each pair produces one outer entity with a <see cref="WhatIfLabel"/> and
    ///   a default <see cref="ScenarioResult"/> property.
    ///   Each scenario must contain exactly one candidate snapshot.
    /// </param>
    public async Task InitializeAsync(
        string innerSetupName,
        IReadOnlyList<BaseEntitySnapshot> baseEntities,
        IReadOnlyList<(string Label, BaseEntitySnapshot Snapshot)> scenarios)
    {
        if (string.IsNullOrWhiteSpace(innerSetupName))
            throw new ArgumentException("Inner setup name cannot be null or empty", nameof(innerSetupName));
        if (baseEntities == null)
            throw new ArgumentNullException(nameof(baseEntities));
        if (scenarios == null)
            throw new ArgumentNullException(nameof(scenarios));

        InnerSetupName = innerSetupName;

        // ── Base-world entities (Earth, background sats, target) ─────────────
        foreach (var snapshot in baseEntities)
        {
            var props = new Dictionary<string, object>(snapshot.Properties);
            await _entityManager.RegisterNewEntityWithStateAsync(
                snapshot.Name, props, snapshot.Description);
        }

        // ── One outer entity per scenario candidate ───────────────────────────
        for (int i = 0; i < scenarios.Count; i++)
        {
            var (label, snapshot) = scenarios[i];
            var props = new Dictionary<string, object>(snapshot.Properties)
            {
                ["WhatIfLabel"]    = new WhatIfLabel(label),
                ["ScenarioResult"] = ScenarioResult.Default,
            };

            await _entityManager.RegisterNewEntityWithStateAsync(
                $"WhatIfScenario_{i}", props, snapshot.Description);
        }
    }
}
