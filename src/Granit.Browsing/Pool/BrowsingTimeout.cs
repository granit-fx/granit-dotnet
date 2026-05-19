namespace Granit.Browsing.Pool;

/// <summary>
/// Wraps a provider operation with an optional render-duration cap. Lifted into a shared
/// helper so every <see cref="IBrowserPage"/> method in every provider applies the same
/// sandbox-aware timeout semantics across every provider.
/// </summary>
/// <remarks>
/// The wrapper links the caller's cancellation token to a fresh
/// <see cref="CancellationTokenSource"/> that cancels after the supplied duration. The
/// caller's task is awaited with <see cref="Task.WaitAsync(TimeSpan, CancellationToken)"/>,
/// surfacing a <see cref="TimeoutException"/> when the cap is hit and propagating
/// outer cancellation immediately.
/// </remarks>
internal static class BrowsingTimeout
{
    /// <summary>Runs <paramref name="op"/> with an optional duration cap.</summary>
    public static async Task<T> RunAsync<T>(
        Func<CancellationToken, Task<T>> op,
        TimeSpan? maxRender,
        CancellationToken outer)
    {
        ArgumentNullException.ThrowIfNull(op);

        if (maxRender is null)
        {
            return await op(outer).ConfigureAwait(false);
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(outer);
        cts.CancelAfter(maxRender.Value);
        CancellationToken effective = cts.Token;

        try
        {
            Task<T> task = op(effective);
            return await task.WaitAsync(maxRender.Value, outer).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!outer.IsCancellationRequested)
        {
            // Cap-CTS fired (not the caller's token) — surface as TimeoutException.
            throw new TimeoutException(
                $"Browsing operation exceeded the render-duration cap of {maxRender.Value.TotalMilliseconds:F0} ms.");
        }
    }

    /// <summary>Runs a void-returning <paramref name="op"/> with an optional duration cap.</summary>
    public static async Task RunAsync(
        Func<CancellationToken, Task> op,
        TimeSpan? maxRender,
        CancellationToken outer)
    {
        ArgumentNullException.ThrowIfNull(op);

        if (maxRender is null)
        {
            await op(outer).ConfigureAwait(false);
            return;
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(outer);
        cts.CancelAfter(maxRender.Value);
        CancellationToken effective = cts.Token;

        try
        {
            Task task = op(effective);
            await task.WaitAsync(maxRender.Value, outer).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!outer.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"Browsing operation exceeded the render-duration cap of {maxRender.Value.TotalMilliseconds:F0} ms.");
        }
    }
}
