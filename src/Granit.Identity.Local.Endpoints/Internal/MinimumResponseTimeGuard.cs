using System.Diagnostics;
using System.Security.Cryptography;

namespace Granit.Identity.Local.Endpoints.Internal;

/// <summary>
/// Pads a request handler's elapsed time to a randomised minimum floor so that
/// timing-sensitive failure paths (user-not-found vs wrong-password vs
/// account-locked) become indistinguishable to a remote attacker.
/// </summary>
/// <remarks>
/// <para>
/// Usage:
/// </para>
/// <code>
/// await using MinimumResponseTimeGuard floor = MinimumResponseTimeGuard.Begin(500, 700);
/// // ... handler logic ...
/// </code>
/// <para>
/// On <see cref="DisposeAsync"/>, the guard delays for whatever portion of the
/// floor remains. The floor is picked uniformly at random in
/// <c>[minimumMs, maximumMs]</c> using a cryptographic RNG so an attacker
/// cannot fingerprint a fixed threshold across requests.
/// </para>
/// </remarks>
internal readonly struct MinimumResponseTimeGuard : IAsyncDisposable
{
    private readonly long _startTimestamp;
    private readonly int _floorMs;
    private readonly CancellationToken _cancellationToken;

    private MinimumResponseTimeGuard(long startTimestamp, int floorMs, CancellationToken cancellationToken)
    {
        _startTimestamp = startTimestamp;
        _floorMs = floorMs;
        _cancellationToken = cancellationToken;
    }

    /// <summary>
    /// Starts a new minimum-response-time guard. Pick a per-request floor
    /// uniformly at random in <c>[minimumMs, maximumMs]</c>.
    /// </summary>
    /// <param name="minimumMs">Lower bound of the floor (inclusive). Must be ≥ 0.</param>
    /// <param name="maximumMs">Upper bound of the floor (inclusive). Must be ≥ <paramref name="minimumMs"/>.</param>
    /// <param name="cancellationToken">
    /// Aborts the padding delay (client disconnect, host drain) — the timing floor is
    /// pointless once nobody is measuring the response.
    /// </param>
    public static MinimumResponseTimeGuard Begin(int minimumMs, int maximumMs, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(minimumMs);
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumMs, minimumMs);

        int floorMs = minimumMs == maximumMs
            ? minimumMs
            : RandomNumberGenerator.GetInt32(minimumMs, maximumMs + 1);

        return new MinimumResponseTimeGuard(Stopwatch.GetTimestamp(), floorMs, cancellationToken);
    }

    /// <summary>
    /// Pads the elapsed time to the per-request floor. No-op if the handler
    /// already took at least <see cref="_floorMs"/> milliseconds.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        TimeSpan elapsed = Stopwatch.GetElapsedTime(_startTimestamp);
        int remainingMs = _floorMs - (int)elapsed.TotalMilliseconds;

        if (remainingMs > 0)
        {
            try
            {
                await Task.Delay(remainingMs, _cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Connection gone — padding is moot; never fail disposal over it.
            }
        }
    }
}
