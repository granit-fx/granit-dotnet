using Granit.Timing;

namespace Granit.Notifications.GoogleFcm.Internal;

/// <summary>
/// Caches the FCM access token and refreshes it with a safety margin before expiry.
/// Refreshes are single-flight: N concurrent sends holding an expired token trigger
/// exactly one mint against Google, never a thundering herd.
/// </summary>
internal sealed class GoogleFcmTokenProvider(
    IGoogleFcmTokenSource tokenSource,
    IClock clock) : IDisposable
{
    /// <summary>Refresh this long before actual expiry so in-flight requests never carry a token that dies mid-request.</summary>
    private static readonly TimeSpan RefreshMargin = TimeSpan.FromMinutes(5);

    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private GoogleFcmAccessToken? _cached;

    /// <summary>Returns the cached token, minting a fresh one when missing or within the refresh margin.</summary>
    public async ValueTask<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        GoogleFcmAccessToken? cached = Volatile.Read(ref _cached);
        if (IsUsable(cached))
        {
            return cached.AccessToken;
        }

        await _refreshLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Another caller may have refreshed while this one waited on the semaphore.
            cached = Volatile.Read(ref _cached);
            if (IsUsable(cached))
            {
                return cached.AccessToken;
            }

            GoogleFcmAccessToken minted = await tokenSource.MintTokenAsync(cancellationToken).ConfigureAwait(false);
            Volatile.Write(ref _cached, minted);
            return minted.AccessToken;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    /// <summary>Drops the cached token so the next call mints a fresh one (401 recovery).</summary>
    public void Invalidate() => Volatile.Write(ref _cached, null);

    /// <inheritdoc />
    public void Dispose() => _refreshLock.Dispose();

    private bool IsUsable([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] GoogleFcmAccessToken? token) =>
        token is not null && clock.Now < token.ExpiresAt - RefreshMargin;
}
