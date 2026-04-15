namespace Simulation.Interfaces;

/// <summary>
/// Async pause/resume token shared across a simulation execution hierarchy.
///
/// Any loop at any level that calls <see cref="WaitIfPausedAsync"/> will suspend
/// until <see cref="Resume"/> is called. Cancellation of the provided
/// <see cref="CancellationToken"/> also unblocks a waiting caller with an
/// <see cref="OperationCanceledException"/>.
///
/// Thread-safe. Multiple concurrent waiters are all resumed together.
/// </summary>
public class PauseHandle
{
    private readonly object _lock = new();
    private TaskCompletionSource<bool>? _pauseTcs;

    /// <summary>
    /// Suspends the caller if the handle is currently paused.
    /// Returns immediately when not paused.
    /// </summary>
    public async Task WaitIfPausedAsync(CancellationToken ct)
    {
        Task? waitTask;
        lock (_lock)
        {
            waitTask = _pauseTcs?.Task;
        }
        if (waitTask != null)
            await waitTask.WaitAsync(ct);
    }

    /// <summary>
    /// Enters the paused state. Subsequent <see cref="WaitIfPausedAsync"/> calls will block.
    /// </summary>
    public void Pause()
    {
        lock (_lock)
        {
            _pauseTcs ??= new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }

    /// <summary>
    /// Leaves the paused state. All blocked callers are released.
    /// </summary>
    public void Resume()
    {
        TaskCompletionSource<bool>? tcs;
        lock (_lock)
        {
            tcs = _pauseTcs;
            _pauseTcs = null;
        }
        tcs?.TrySetResult(true);
    }

    /// <summary>Whether the handle is currently in the paused state.</summary>
    public bool IsPaused
    {
        get { lock (_lock) { return _pauseTcs != null; } }
    }
}
